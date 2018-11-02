using MDA.Disruptor.Impl;
using MDA.Disruptor.Test.Support;
using Xunit;

namespace MDA.Disruptor.Test
{
    public class Ring_Buffer_Test
    {
        private readonly RingBuffer<StubEvent> _ringBuffer;
        private readonly ISequenceBarrier _barrier;

        public Ring_Buffer_Test()
        {
            _ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(StubEvent.EventFactory, 32);
            _barrier = _ringBuffer.NewBarrier();
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<StubEvent>(_ringBuffer).GetSequence());
        }

        [Fact(DisplayName = "应该能发布和获取事件。")]
        public void Should_Publish_And_Get()
        {
            Assert.Equal(Sequence.InitialValue, _ringBuffer.GetCursor());

            var expectedEvent = new StubEvent(2701);
            _ringBuffer.PublishEvent(StubEvent.Translator, expectedEvent.Value, expectedEvent.TestString);

            var sequence = _barrier.WaitFor(0L);
            Assert.Equal(0L, sequence);

            var @event = _ringBuffer.Get(sequence);
            Assert.Equal(expectedEvent, @event);

            Assert.Equal(0L, _barrier.GetCursor());
        }
    }
}
