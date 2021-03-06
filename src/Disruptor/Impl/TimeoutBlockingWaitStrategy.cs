﻿using System;
using System.Threading;

namespace Disruptor.Impl
{
    /// <summary p="This strategy can be used when throughput and low-latency are not as important as CPU resource.">
    /// Blocking strategy that uses a lock and condition variable for <see cref="IEventProcessor"/>s waiting on a barrier. However it will periodically wake up if it has been idle for specified period by throwing a <see cref="TimeoutException"/>. To make use of this, the event handler class should implement the <see cref="ITimeoutHandler"/>, which the <see cref="IBatchEventProcessor{TEvent}"/> will call if the timeout occurs.
    /// </summary>
    public class TimeoutBlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _mutex = new object();
        private readonly int _timeoutInMillis;
        public TimeoutBlockingWaitStrategy(int millisecondsTimeout)
        {
            _timeoutInMillis = millisecondsTimeout;
        }

        public long WaitFor(
            long sequence,
            ISequence cursor,
            ISequence dependentSequence,
            ISequenceBarrier barrier)
        {
            var timeoutInMillis = _timeoutInMillis;

            if (cursor.GetValue() < sequence)
            {
                lock (_mutex)
                {
                    while (cursor.GetValue() < sequence)
                    {
                        barrier.CheckAlert();
                        if (!Monitor.Wait(_mutex, timeoutInMillis))
                        {
                            throw new TimeoutException();
                        }
                    }
                }
            }

            long availableSequence;
            while ((availableSequence = dependentSequence.GetValue()) < sequence)
            {
                barrier.CheckAlert();
            }

            return availableSequence;
        }

        public void SignalAllWhenBlocking()
        {
            lock (_mutex)
            {
                Monitor.PulseAll(_mutex);
            }
        }

        public override string ToString()
        {
            return $"[mutex:{_mutex},millisecondsTimeout:{_timeoutInMillis}]";
        }
    }
}
