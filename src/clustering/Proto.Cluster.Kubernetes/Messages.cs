using System;

namespace Proto.Cluster.Kubernetes
{
    static class Messages
    {
        public class RegisterMember
        {
            public string   ClusterName   { get; set; }
            public string   Address       { get; set; }
            public int      Port          { get; set; }
            public string[] Kinds         { get; set; }

            public IMemberStatusValue           StatusValue           { get; set; }
            public IMemberStatusValueSerializer StatusValueSerializer { get; set; }
        }

        public class DeregisterMember { }

        public class StartWatchingCluster
        {
            public string ClusterName { get; }

            public StartWatchingCluster(string clusterName) => ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
        }

        public class UpdateStatusValue
        {
            public IMemberStatusValue StatusValue { get; set; }
        }

        public class ReregisterMember { }
    }
}
