// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using k8s;

namespace Proto.Cluster.Kubernetes
{
    public class KubernetesProvider : IClusterProvider
    {
        private readonly IKubernetes _kubernetes;

        private PID _clusterMonitor;
        private string _clusterName;

        public KubernetesProvider(IKubernetes kubernetes)
        {
            if (KubernetesExtensions.GetKubeNamespace() == null)
            {
                throw new InvalidOperationException("The application doesn't seem to be running in Kubernetes");
            }

            _kubernetes = kubernetes;
        }

        public Task RegisterMemberAsync(
            Cluster cluster,
            string clusterName, string address, int port, string[] kinds, IMemberStatusValue statusValue,
            IMemberStatusValueSerializer statusValueSerializer
        )
        {
            if (string.IsNullOrEmpty(clusterName)) throw new ArgumentNullException(nameof(clusterName));

            var props = Props
                .FromProducer(() => new KubernetesClusterMonitor(cluster.System, _kubernetes))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);
            _clusterMonitor = cluster.System.Root.SpawnNamed(props, "ClusterMonitor");
            _clusterName = clusterName;

            cluster.System.Root.Send(
                _clusterMonitor,
                new Messages.RegisterMember
                {
                    ClusterName = clusterName,
                    Address = address,
                    Port = port,
                    Kinds = kinds,
                    StatusValue = statusValue,
                    StatusValueSerializer = statusValueSerializer
                }
            );

            return Actor.Done;
        }

        public Task DeregisterMemberAsync(Cluster cluster)
        {
            cluster.System.Root.Send(_clusterMonitor, new Messages.DeregisterMember());
            return Actor.Done;
        }

        public Task Shutdown(Cluster cluster) => cluster.System.Root.StopAsync(_clusterMonitor);

        public void MonitorMemberStatusChanges(Cluster cluster)
            => cluster.System.Root.Send(_clusterMonitor, new Messages.StartWatchingCluster(_clusterName));

        public Task UpdateMemberStatusValueAsync(Cluster cluster, IMemberStatusValue statusValue)
        {
            cluster.System.Root.Send(_clusterMonitor, new Messages.UpdateStatusValue {StatusValue = statusValue});
            return Actor.Done;
        }
    }
}
