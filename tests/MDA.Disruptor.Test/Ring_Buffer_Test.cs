using MDA.Disruptor.Exceptions;
using MDA.Disruptor.Impl;
using MDA.Disruptor.Test.Support;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

        [Fact(DisplayName = "在一个单独的线程里，应该能发布和获取事件。")]
        public void Should_Claim_And_Get_In_Separate_Thread()
        {
            var messages = GetMessages(0, 0);

            var expectedEvent = new StubEvent(2701);
            _ringBuffer.PublishEvent(StubEvent.Translator, expectedEvent.Value, expectedEvent.TestString);

            Assert.Equal(expectedEvent, messages.Result[0]);
        }

        [Fact(DisplayName = "应该能发布和获取多个事件。")]
        public void Should_Claim_And_Get_Multiple_Messages()
        {
            var numMessages = _ringBuffer.GetBufferSize();
            for (var i = 0; i < numMessages; i++)
            {
                _ringBuffer.PublishEvent(StubEvent.Translator, i, "");
            }

            var expectedSequence = numMessages - 1;
            var availableSequence = _barrier.WaitFor(expectedSequence);

            Assert.True(expectedSequence == availableSequence);

            for (var i = 0; i < numMessages; i++)
            {
                Assert.Equal(i, _ringBuffer.Get(i).Value);
            }
        }

        [Fact]
        public void Should_Wrap()
        {
            var numMessages = _ringBuffer.GetBufferSize();
            var offset = 1000;
            for (var i = 0; i < numMessages + offset; i++)
            {
                _ringBuffer.PublishEvent(StubEvent.Translator, i, "");
            }

            var expectedSequence = numMessages + offset - 1;
            var available = _barrier.WaitFor(expectedSequence);
            Assert.Equal(expectedSequence, available);

            for (var i = offset; i < numMessages + offset; i++)
            {
                Assert.Equal(i, _ringBuffer.Get(i).Value);
            }
        }

        [Fact]
        public void Should_Prevent_Wrapping()
        {
            var sequence = new Sequence();
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(StubEvent.EventFactory, 4);
            ringBuffer.AddGatingSequences(sequence);

            ringBuffer.PublishEvent(StubEvent.Translator, 0, "0");
            ringBuffer.PublishEvent(StubEvent.Translator, 1, "1");
            ringBuffer.PublishEvent(StubEvent.Translator, 2, "2");
            ringBuffer.PublishEvent(StubEvent.Translator, 3, "3");

            Assert.False(ringBuffer.TryPublishEvent(StubEvent.Translator, 4, "4"));
        }

        [Fact]
        public void Should_Throw_Exception_If_BufferIsFull()
        {
            _ringBuffer.AddGatingSequences(new Sequence(_ringBuffer.GetBufferSize()));

            Assert.Throws<Exception>(() =>
            {
                for (int i = 0; i < _ringBuffer.GetBufferSize(); i++)
                {
                    if (_ringBuffer.TryNext(out long sequence))
                    {
                        _ringBuffer.Publish(sequence);
                    }
                }
            });

            Assert.Throws<InsufficientCapacityException>(() =>
            {
                _ringBuffer.TryNext(out long sequence);
            });
        }

        private Task<List<StubEvent>> GetMessages(long initial, long toWaitFor)
        {
            var barrier = new Barrier(2);
            var dependencyBarrier = _ringBuffer.NewBarrier();

            var f = Task.Factory.StartNew(() => new TestWaiter(barrier, dependencyBarrier, _ringBuffer, initial, toWaitFor).Call());

            barrier.SignalAndWait();

            return f;
        }
    }
}
