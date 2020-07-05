using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Extensions
{
    public class ProtoClusterService : IHostedService
    {
        private readonly Cluster _cluster;
        private readonly ClusterConfig _clusterConfig;

        public ProtoClusterService(
            Cluster cluster, ClusterConfig clusterConfig, RegisterRemoteKinds registerRemoteKinds, ILoggerFactory loggerFactory
        )
        {
            _cluster = cluster;
            _clusterConfig = clusterConfig;
            Log.SetLoggerFactory(loggerFactory);
            registerRemoteKinds(_cluster.Remote);
        }

        public Task StartAsync(CancellationToken cancellationToken) => _cluster.Start(_clusterConfig);

        public Task StopAsync(CancellationToken cancellationToken) => _cluster.Shutdown();
    }

    public delegate void RegisterRemoteKinds(Remote.Remote remote);
}
