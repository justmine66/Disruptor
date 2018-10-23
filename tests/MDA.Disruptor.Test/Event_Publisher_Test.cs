using MDA.Disruptor.Impl;
using MDA.Disruptor.Test.Support;
using Xunit;

namespace MDA.Disruptor.Test
{
    public class Event_Publisher_Test : IEventTranslator<LongEvent>
    {
        private static readonly int BufferSize = 32;
        private RingBuffer<LongEvent> _ringBuffer;

        public Event_Publisher_Test()
        {
            _ringBuffer = RingBuffer<LongEvent>.CreateMultiProducer(() => new LongEvent(), BufferSize);
        }

        public void TranslateTo(LongEvent @event, long sequence)
        {
            @event.Value = sequence + 29;
        }

        [Fact(DisplayName = "发布事件")]
        public void Should_Publish_Event()
        {
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<LongEvent>(_ringBuffer).GetSequence());

            _ringBuffer.PublishEvent(this);
            _ringBuffer.PublishEvent(this);

            Assert.Equal(_ringBuffer.Get(0).Value, 29L + 0);
            Assert.Equal(_ringBuffer.Get(1).Value, 29L + 1);
        }

        [Fact(DisplayName = "尝试发布事件")]
        public void Should_Try_Publish_Event()
        {
            _ringBuffer.AddGatingSequences(new Sequence());

            for (int i = 0; i < BufferSize; i++)
            {
                var pulished = _ringBuffer.TryPublishEvent(this);
                Assert.True(pulished);
            }

            for (int i = 0; i < BufferSize; i++)
            {
                Assert.Equal(_ringBuffer.Get(i).Value, 29L + i);
            }

            Assert.False(_ringBuffer.TryPublishEvent(this));
        }
    }
}
