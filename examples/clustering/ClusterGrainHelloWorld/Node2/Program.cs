// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class HelloGrain : IHelloGrain
    {
        public Task<HelloResponse> SayHello(HelloRequest request)
        {
            return Task.FromResult(new HelloResponse
            {
                Message = "Hello from typed grain"
            });
        }
    }
    
    class Program
    {
        static async Task Main(string[] args)
        {
            var system = new ActorSystem();

            var remoteConfig =  GrpcCoreRemoteConfig
                .BindToLocalhost(12000)
                .WithProtoMessages(ProtosReflection.Descriptor);
            
            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://consul:8500/"));

            var clusterConfig =
                ClusterConfig.Setup("MyCluster", consulProvider, new PartitionIdentityLookup());
            
            system
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            var grains = new Grains(system.Cluster());
            grains.HelloGrainFactory(() => new HelloGrain());
            
            await system.Cluster().StartMemberAsync();

            Console.CancelKeyPress += async (e, y) =>
            {
                Console.WriteLine("Shutting Down...");
                await system.Cluster().ShutdownAsync();
            };
            await Task.Delay(-1);
        }
    }
}