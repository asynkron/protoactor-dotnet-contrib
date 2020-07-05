using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
using static Proto.Cluster.Kubernetes.ProtoLabels;

// ReSharper disable InvertIf

namespace Proto.Cluster.Kubernetes
{
    public static class KubernetesExtensions
    {
        public static V1ContainerPort FindPort(this V1Pod pod, int port) => pod.Spec.Containers[0].Ports.FirstOrDefault(x => x.ContainerPort == port);

        public static Task<V1Pod> AddPodLabels(this IKubernetes kubernetes, string podName, string podNamespace, IDictionary<string, string> labels)
        {
            var patch = new JsonPatchDocument<V1Pod>();
            patch.Add(x => x.Metadata.Labels, labels);
            return kubernetes.PatchNamespacedPodAsync(new V1Patch(patch), podName, podNamespace);
        }

        public static Task<V1Pod> ReplacePodLabels(
            this IKubernetes kubernetes, string podName, string podNamespace, IDictionary<string, string> labels
        )
        {
            var patch = new JsonPatchDocument<V1Pod>();
            patch.Replace(x => x.Metadata.Labels, labels);
            return kubernetes.PatchNamespacedPodAsync(new V1Patch(patch), podName, podNamespace);
        }

        public static (bool IsCandidate, MemberStatus Status) GetMemberStatus(this V1Pod pod, IMemberStatusValueSerializer serializer)
        {
            var isCandidate = pod.Status.Phase == "Running" && pod.Status.PodIP != null;

            var kinds       = pod.Metadata.Labels[LabelKinds].Split(',');
            var statusValue = serializer.Deserialize(pod.Metadata.Labels[LabelStatusValue]);
            var host        = pod.Status.PodIP ?? "";
            var port        = Convert.ToInt32(pod.Metadata.Labels[LabelPort]);
            var alive       = pod.Status.ContainerStatuses.All(x => x.Ready);

            return (isCandidate, new MemberStatus(pod.Uid(), host, port, kinds, alive, statusValue));
        }

        static string cachedNamespace;

        public static string GetKubeNamespace()
        {
            if (cachedNamespace == null)
            {
                var namespaceFile = Path.Combine(
                    $"{Path.DirectorySeparatorChar}var",
                    "run",
                    "secrets",
                    "kubernetes.io",
                    "serviceaccount",
                    "namespace"
                );
                cachedNamespace = File.ReadAllText(namespaceFile);
            }

            return cachedNamespace;
        }

        public static string GetPodName() => Environment.MachineName;
    }
}
