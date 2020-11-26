// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Proto.Serialization.Wire;

namespace Node2
{
    public class EchoActor : IActor
    {
        private PID _sender;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case StartRemote sr:
                    Console.WriteLine("Starting");
                    _sender = sr.Sender;
                    context.Respond(new Start());
                    return Task.CompletedTask;
                case Ping _:
                    context.Send(_sender, new Pong());
                    return Task.CompletedTask;
                default:
                    return Task.CompletedTask;
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var serialization = new Serialization();
            //Registering "knownTypes" is not required, but improves performance as those messages
            //do not need to pass any typename manifest

            var remoteConfig = GrpcCoreRemoteConfig.BindToLocalhost(12000);
            system.WithRemote(remoteConfig);
            
            var wire = new WireSerializer(new[] { typeof(Ping), typeof(Pong), typeof(StartRemote), typeof(Start) });
            system.Serialization().RegisterSerializer(wire,true);
            await system.Remote().StartAsync();
            context.SpawnNamed(Props.FromProducer(() => new EchoActor()), "remote");
            Console.ReadLine();
        }
    }
}