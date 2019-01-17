using Disruptor.Impl;
using Disruptor.Test.Support;
using System;
using Xunit;

namespace Disruptor.Test
{
    public class Lite_Timeout_Blocking_Wait_Strategy_Test
    {
        private readonly ISequenceBarrier _barrier;
        private readonly IWaitStrategy _strategy;
        private readonly ISequence _cursor;
        private readonly ISequence _dependent;

        private const int TimeoutMilliseconds = 1000;

        public Lite_Timeout_Blocking_Wait_Strategy_Test()
        {
            _barrier = new DummySequenceBarrier();
            _strategy = new LiteTimeoutBlockingWaitStrategy(TimeoutMilliseconds);
            _cursor = new Sequence(5);
            _dependent = _cursor;
        }

        [Fact(DisplayName = "等待超时异常")]
        public void Should_Timeout_WaitFor()
        {
            Assert.Throws<TimeoutException>(() => _strategy.WaitFor(6, _cursor, _dependent, _barrier));
        }
    }
}
