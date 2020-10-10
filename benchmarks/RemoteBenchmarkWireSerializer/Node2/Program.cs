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
            var wire = new WireSerializer(new[] { typeof(Ping), typeof(Pong), typeof(StartRemote), typeof(Start) });

            var remoteConfig = new RemoteConfig("127.0.0.1", 12001);
            remoteConfig.Serialization.RegisterSerializer(wire,true);
            var remote = new Remote(system, remoteConfig);
            await remote.StartAsync();
            context.SpawnNamed(Props.FromProducer(() => new EchoActor()), "remote");
            Console.ReadLine();
        }
    }
}