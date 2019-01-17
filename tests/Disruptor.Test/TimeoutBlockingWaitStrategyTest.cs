using Disruptor.Impl;
using Disruptor.Test.Support;
using System;
using Xunit;

namespace Disruptor.Test
{
    public class TimeoutBlockingWaitStrategyTest
    {
        private readonly ISequenceBarrier _barrier;
        private readonly IWaitStrategy _strategy;
        private readonly ISequence _cursor;
        private readonly ISequence _dependent;

        private const int TimeoutMilliseconds = 1000;

        public TimeoutBlockingWaitStrategyTest()
        {
            _barrier = new DummySequenceBarrier();
            _strategy = new TimeoutBlockingWaitStrategy(TimeoutMilliseconds);
            _cursor = new Sequence(5);
            _dependent = _cursor;
        }

        [Fact(DisplayName = "应该抛出等待超时异常")]
        public void ShouldTimeoutWaitFor()
        {
            var startUtc = DateTime.UtcNow;

            Assert.Throws<TimeoutException>(() => _strategy.WaitFor(6, _cursor, _dependent, _barrier));

            var endUtc = DateTime.UtcNow;
            var timeWaiting = endUtc.Subtract(startUtc).Ticks;

            Assert.True(timeWaiting >= TimeoutMilliseconds);
        }
    }
}
