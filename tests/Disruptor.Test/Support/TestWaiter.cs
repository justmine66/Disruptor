using Disruptor.Impl;
using System.Collections.Generic;
using System.Threading;

namespace Disruptor.Test.Support
{
    public class TestWaiter : ICallable<List<StubEvent>>
    {
        private readonly long _toWaitForSequence;
        private readonly long _initialSequence;
        private readonly Barrier _barrier;
        private readonly ISequenceBarrier _sequenceBarrier;
        private readonly RingBuffer<StubEvent> _ringBuffer;

        public TestWaiter(
            Barrier barrier,
            ISequenceBarrier sequenceBarrier,
            RingBuffer<StubEvent> ringBuffer,
            long initialSequence,
            long toWaitForSequence)
        {
            _barrier = barrier;
            _initialSequence = initialSequence;
            _ringBuffer = ringBuffer;
            _toWaitForSequence = toWaitForSequence;
            _sequenceBarrier = sequenceBarrier;
        }

        public List<StubEvent> Call()
        {
            _barrier.SignalAndWait();
            _sequenceBarrier.WaitFor(_toWaitForSequence);

            var messages = new List<StubEvent>();
            for (long l = _initialSequence; l <= _toWaitForSequence; l++)
            {
                messages.Add(_ringBuffer.Get(l));
            }

            return messages;
        }
    }
}
