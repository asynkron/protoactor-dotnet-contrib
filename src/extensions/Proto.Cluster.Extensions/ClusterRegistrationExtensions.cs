using System;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Proto.Remote;

namespace Proto.Cluster.Extensions
{
    [PublicAPI]
    public static class ClusterRegistrationExtensions
    {
        /// <summary>
        /// Add the Proto.Actor Cluster hosted service to the service collection.
        /// The <see cref="ActorSystem"/> and <see cref="IRootContext"/> must be registered explicitly.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="clusterName">Cluster name</param>
        /// <param name="serialization">The <see cref="Serialization"/> instance with proto descriptors registered</param>
        /// <param name="getAddress">Function to get the node address and port</param>
        /// <param name="clusterProvider">Optional: the cluster provider instance. If not provided, we'll try resolving it from the container.</param>
        /// <param name="registerKinds">Optional: function to register known kinds if the node has actors</param>
        /// <param name="configureCluster">Optional: configure additional cluster options</param>
        /// <returns></returns>
        public static IServiceCollection AddProtoCluster(
            this IServiceCollection services,
            string clusterName,
            Serialization serialization,
            Func<IServiceProvider, (string host, int port)> getAddress,
            IClusterProvider clusterProvider = null,
            RegisterRemoteKinds registerKinds = null,
            Action<ClusterConfig> configureCluster = null
        )
            => services
                .AddSingleton(serialization)
                .AddSingleton<Cluster>()
                .AddSingleton(
                    ctx =>
                    {
                        var provider =  clusterProvider ?? ctx.GetRequiredService<IClusterProvider>();
                        var (host, port) = getAddress(ctx);
                        var clusterConfig = new ClusterConfig(clusterName, host, port, provider);
                        configureCluster?.Invoke(clusterConfig);
                        return clusterConfig;
                    }
                )
                .AddSingleton(registerKinds ?? (_ => { }))
                .AddHostedService<ProtoClusterService>();
    }
}
