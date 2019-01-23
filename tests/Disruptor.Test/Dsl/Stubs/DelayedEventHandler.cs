using Disruptor.Exceptions;
using Disruptor.Test.Support;
using System;
using System.Threading;

namespace Disruptor.Test.Dsl.Stubs
{
    public class DelayedEventHandler : IEventHandler<TestEvent>, ILifecycleAware
    {
        private int _readToProcessEvent;
        private volatile bool _stopped;
        private readonly Barrier _barrier;

        public DelayedEventHandler(Barrier barrier)
        {
            _barrier = barrier;
        }

        public DelayedEventHandler()
            : this(new Barrier(2))
        {
        }

        public void OnEvent(TestEvent @event, long sequence, bool endOfBatch)
        {
            WaitForAndSetFlag(0);
        }

        public void ProcessEvent()
        {
            WaitForAndSetFlag(1);
        }

        public void OnStart()
        {
            try
            {
                _barrier.SignalAndWait();
            }
            catch (Exception e)
            {
                throw new RuntimeException(e);
            }
        }

        public void AwaitStart()
        {
            _barrier.SignalAndWait();
        }

        public void StopWaiting()
        {
            _stopped = true;
        }

        public void OnShutdown()
        {

        }

        private void WaitForAndSetFlag(int newValue)
        {
            while (!_stopped &&
                   Thread.CurrentThread.IsAlive &&
                   Interlocked.Exchange(ref _readToProcessEvent, newValue) == newValue)
            {
                Thread.Yield();
            }
        }
    }
}
