// -----------------------------------------------------------------------
//   <copyright file="KubernetesProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using static Proto.Cluster.Kubernetes.Messages;
using static Proto.Cluster.Kubernetes.ProtoLabels;

namespace Proto.Cluster.Kubernetes
{
    public class KubernetesClusterMonitor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<KubernetesClusterMonitor>();

        private readonly ActorSystem _system;
        private readonly IKubernetes _kubernetes;

        public KubernetesClusterMonitor(ActorSystem system, IKubernetes kubernetes)
        {
            _system = system;
            _kubernetes = kubernetes;
        }

        public async Task ReceiveAsync(IContext context)
        {
            var task = context.Message switch
            {
                RegisterMember cmd       => Register(cmd),
                StartWatchingCluster cmd => StartWatchingCluster(cmd.ClusterName),
                DeregisterMember _       => UnregisterService(),
                UpdateStatusValue cmd    => UpdateStatusValue(cmd.StatusValue),
                ReregisterMember _       => RegisterService(),
                Stopping _               => Stop(),
                _                        => Task.CompletedTask
            };
            await task.ConfigureAwait(false);

            Task Stop()
            {
                Logger.LogInformation("Stopping monitoring for {PodName} with ip {PodIp}", _podName, _address);
                return _registered ? UnregisterService() : Actor.Done;
            }
        }

        private async Task Register(RegisterMember cmd)
        {
            _clusterName = cmd.ClusterName;
            _address = cmd.Address;
            _port = cmd.Port;
            _kinds = cmd.Kinds;
            _statusValueSerializer = cmd.StatusValueSerializer;
            _statusValue = cmd.StatusValue;
            _podName = KubernetesExtensions.GetPodName();

            await RegisterService();
        }

        private async Task UpdateStatusValue(IMemberStatusValue statusValue)
        {
            Logger.LogDebug("Updating the status value to {statusValue}", statusValue);

            var labels = new Dictionary<string, string>
            {
                [LabelStatusValue] = _statusValueSerializer.Serialize(statusValue)
            };

            try
            {
                await _kubernetes.AddPodLabels(_podName, KubernetesExtensions.GetKubeNamespace(), labels);
            }
            catch (HttpOperationException e)
            {
                Logger.LogError(e, "Unable to update pod labels");
            }

            _statusValue = statusValue;
        }

        private async Task RegisterService()
        {
            Logger.LogInformation("Registering service {PodName} on {PodIp}", _podName, _address);

            var pod = await _kubernetes.ReadNamespacedPodAsync(_podName, KubernetesExtensions.GetKubeNamespace());

            if (pod == null) throw new ApplicationException($"Unable to get own pod information for {_podName}");

            var matchingPort = pod.FindPort(_port);

            if (matchingPort == null)
            {
                Logger.LogWarning("Registration port doesn't match any of the container ports");
            }

            var protoKinds = new List<string>();

            if (pod.Metadata.Labels.TryGetValue(LabelKinds, out var protoKindsString))
            {
                protoKinds.AddRange(protoKindsString.Split(','));
            }

            protoKinds.AddRange(_kinds);

            var labels = new Dictionary<string, string>(pod.Metadata.Labels)
            {
                [LabelCluster] = _clusterName,
                [LabelKinds] = string.Join(",", protoKinds.Distinct()),
                [LabelPort] = _port.ToString(),
                [LabelStatusValue] = _statusValueSerializer.Serialize(_statusValue)
            };

            try
            {
                await _kubernetes.ReplacePodLabels(_podName, KubernetesExtensions.GetKubeNamespace(), labels);
            }
            catch (HttpOperationException e)
            {
                Logger.LogError(e, "Unable to update pod labels, registration failed");
                throw;
            }

            _registered = true;
        }

        private async Task UnregisterService()
        {
            Logger.LogInformation("Unregistering service {PodName} on {PodIp}", _podName, _address);

            try
            {
                _watcher.Dispose();
                await _watcherTask;
            }
            catch (TaskCanceledException)
            {
                // expected
            }

            _registered = false;
        }

        private Task StartWatchingCluster(string clusterName)
        {
            var selector = $"{LabelCluster}={clusterName}";
            Logger.LogInformation("Starting to watch pods with {Selector}", selector);

            _watcherTask = _kubernetes.ListNamespacedPodWithHttpMessagesAsync(
                KubernetesExtensions.GetKubeNamespace(),
                labelSelector: selector,
                watch: true
            );
            _watcher = _watcherTask.Watch<V1Pod, V1PodList>(Watch, Error);

            void Watch(WatchEventType eventType, V1Pod eventPod)
            {
                Logger.LogInformation("Kubernetes update {EventType}: {@PodUpdate}", eventType, eventPod);

                var podLabels = eventPod.Metadata.Labels;

                if (!podLabels.TryGetValue(LabelCluster, out var podClusterName))
                {
                    Logger.LogInformation("The pod {PodName} is not a Proto.Cluster node", eventPod.Metadata.Name);
                    return;
                }

                if (clusterName != podClusterName)
                {
                    Logger.LogInformation("The pod {PodName} is from another cluster {Cluster}", eventPod.Metadata.Name, clusterName);
                    return;
                }

                // Update the list of known pods
                if (eventType == WatchEventType.Deleted)
                {
                    _clusterPods.Remove(eventPod.Uid());
                }
                else
                {
                    _clusterPods[eventPod.Uid()] = eventPod;
                }

                if (eventPod.Name() == _podName && eventPod.Status.PodIP != _address)
                {
                    Logger.LogCritical("FUCK! My ip address changed from {OldIp} to {NewIp}!!!", _address, eventPod.Status.PodIP);
                }

                var memberStatuses = _clusterPods.Values
                    .Select(x => x.GetMemberStatus(_statusValueSerializer))
                    .Where(x => x.IsCandidate)
                    .Select(x => x.Status)
                    .ToList();
                Logger.LogInformation("Cluster members updated {@Members}", memberStatuses);

                _system.EventStream.Publish(new ClusterTopologyEvent(memberStatuses));
            }

            return Actor.Done;

            static void Error(Exception ex)
            {
                if (ex is TaskCanceledException || ex is OperationCanceledException)
                    Logger.LogInformation("The watcher is stopping");
                else
                    Logger.LogWarning("Error occured watching the cluster status: {Error}", ex.Message);
            }
        }

        private readonly Dictionary<string, V1Pod> _clusterPods = new Dictionary<string, V1Pod>();

        private Watcher<V1Pod> _watcher;
        private IMemberStatusValue _statusValue;
        private string _clusterName;
        private string _address;
        private int _port;
        private string[] _kinds;
        private bool _registered;
        private string _podName;
        private Task<HttpOperationResponse<V1PodList>> _watcherTask;
        private IMemberStatusValueSerializer _statusValueSerializer;
    }
}
