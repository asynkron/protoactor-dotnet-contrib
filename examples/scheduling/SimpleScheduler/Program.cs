using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Schedulers.SimpleScheduler;

namespace SimpleScheduler
{
    class Program
    {
        private static void Main()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var props = Props.FromProducer(() => new ScheduleActor());

            var pid = context.Spawn(props);

            context
                .ScheduleTellOnce(TimeSpan.FromMilliseconds(100), pid, new SimpleMessage("test 1 from root context"))
                .ScheduleSendOnce(TimeSpan.FromMilliseconds(200), pid, new SimpleMessage("test 2 from root context"))
                .ScheduleSendOnce(TimeSpan.FromMilliseconds(300), pid, new SimpleMessage("test 3 from root context"))
                .ScheduleSendOnce(TimeSpan.FromMilliseconds(400), pid, new SimpleMessage("test 4 from root context"))
                .ScheduleSendOnce(TimeSpan.FromMilliseconds(500), pid, new SimpleMessage("test 5 from root context"));

            Console.ReadLine();
        }
    }

    public class Hello { }

    public class HickUp { }

    public class AbortHickUp { }

    public class Greet
    {
        public Greet(string who) => Who = who;

        public string Who { get; }
    }

    public class SimpleMessage
    {
        public SimpleMessage(string msg) => Msg = msg;

        public string Msg { get; }
    }

    public class ScheduleActor : IActor
    {
        private CancellationTokenSource _timer;

        private int _counter;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    var pid = context.Spawn(Props.FromProducer(() => new ScheduleGreetActor()));
                    context
                        .ScheduleTellOnce(TimeSpan.FromMilliseconds(100), context.Self, new SimpleMessage("test 1"))
                        .ScheduleSendOnce(TimeSpan.FromMilliseconds(200), context.Self, new SimpleMessage("test 2"))
                        .ScheduleSendOnce(TimeSpan.FromMilliseconds(300), context.Self, new SimpleMessage("test 3"))
                        .ScheduleSendOnce(TimeSpan.FromMilliseconds(400), context.Self, new SimpleMessage("test 4"))
                        .ScheduleSendOnce(TimeSpan.FromMilliseconds(500), context.Self, new SimpleMessage("test 5"))
                        .ScheduleRequestOnce(TimeSpan.FromSeconds(1), context.Self, pid, new Greet("Daniel"))
                        .ScheduleSendOnce(TimeSpan.FromSeconds(5), context.Self, new Hello());
                    break;
                case Hello _:
                    Console.WriteLine("Hello Once, let's give you a hiccup every 0.5 second starting in 3 seconds!");
                    context.ScheduleTellRepeatedly(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(500), context.Self, new HickUp(), out _timer);
                    break;
                case HickUp _:
                    _counter++;
                    Console.WriteLine("Hello!");
                    if (_counter == 5)
                    {
                        _timer.Cancel();
                        context.Send(context.Self, new AbortHickUp());
                    }
                    break;
                case AbortHickUp _:
                    Console.WriteLine($"Aborted hickup after {_counter.ToString()} times");
                    Console.WriteLine("All this was scheduled calls, have fun!");
                    break;
                case Greet msg:
                    Console.WriteLine($"Thanks {msg.Who}");
                    break;
                case SimpleMessage sm:
                    Console.WriteLine(sm.Msg);
                    break;
            }

            return Task.CompletedTask;
        }
    }

    public class ScheduleGreetActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Greet msg:
                    Console.WriteLine($"Hi {msg.Who}!");
                    context.Respond(new Greet("Roger"));
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
