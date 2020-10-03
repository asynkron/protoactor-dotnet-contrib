using System;
using System.Threading;

namespace Proto.Schedulers.SimpleScheduler
{
    public interface ISimpleScheduler
    {
        ISimpleScheduler ScheduleSendOnce(TimeSpan delay, PID target, object message);
        ISimpleScheduler ScheduleSendRepeatedly(TimeSpan delay, TimeSpan interval, PID target, object message, out CancellationTokenSource cancellationTokenSource);
        ISimpleScheduler ScheduleRequestOnce(TimeSpan delay, PID sender, PID target, object message);
        ISimpleScheduler ScheduleRequestRepeatedly(TimeSpan delay, TimeSpan interval, PID sender, PID target, object message, out CancellationTokenSource cancellationTokenSource);
    }
}
