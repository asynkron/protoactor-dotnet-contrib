// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using ProtosReflection = Messages.ProtosReflection;

namespace Node1
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var log = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug));
            Log.SetLoggerFactory(log);

            Console.WriteLine("Starting Node1");
            var system = new ActorSystem();
            
            var context = new RootContext(system);

            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost(12000)
                .WithProtoMessages(ProtosReflection.Descriptor);
            
            var clusterConfig = ClusterConfig.Setup("MyCluster",
                new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://consul:8500/")),
                new PartitionIdentityLookup()
            );

            system
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);
            
            // CONSUL 

            await system
                .Cluster()
                .StartMemberAsync();
            
            var i = 10000;

            while (i-- > 0)
            {
                var res = await system
                    .Cluster()
                    .RequestAsync<HelloResponse>("TheName", "HelloKind", new HelloRequest(),CancellationToken.None);
                
                Console.WriteLine(res.Message);
                await Task.Delay(500);
            }

            await Task.Delay(-1);
            Console.WriteLine("Shutting Down...");

            await system.Cluster().ShutdownAsync();
        }
    }
}
