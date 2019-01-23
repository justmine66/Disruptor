using Disruptor.Dsl;
using Disruptor.Impl;
using Disruptor.Test.Dsl.Stubs;
using Disruptor.Test.Support;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Disruptor.Test.Dsl
{
    public class DisruptorTest
    {
        private const int TIMEOUT_IN_SECONDS = 2;

        private readonly List<DelayedEventHandler> _delayedEventHandlers;
        private readonly List<TestWorkHandler> _testWorkHandlers;

        private RingBuffer<TestEvent> _ringBuffer;
        private TestEvent _lastPublishedEvent;

        private Disruptor<TestEvent> _disruptor;
        private StubExecutor _executor;

        public DisruptorTest()
        {
            _delayedEventHandlers = new List<DelayedEventHandler>();
            _testWorkHandlers = new List<TestWorkHandler>();
            _executor = new StubExecutor();
            _disruptor = new Disruptor<TestEvent>(() => new TestEvent(), 4, _executor);
        }

        [Fact]
        public async Task ShouldProcessMessagesPublishedBeforeStartIsCalled()
        {
            var eventCounter = new CountdownEvent(2);
            _disruptor.HandleEventsWith(new EventHandler(eventCounter));

            _disruptor.PublishEvent(new EventTranslator(_lastPublishedEvent));

            await _disruptor.StartAsync();

            _disruptor.PublishEvent(new EventTranslator(_lastPublishedEvent));

            if (!eventCounter.Wait(5))
            {
                throw new Exception($"Did not process event published before start was called. Missed events: {eventCounter.CurrentCount}");
            }
        }

        [Fact]
        public async Task ShouldBatchOfEvents()
        {
            var eventCounter = new CountdownEvent(2);
            _disruptor.HandleEventsWith(new EventHandler(eventCounter));

            await _disruptor.StartAsync();

            _disruptor.PublishEvents(new EventTranslatorOneArg(_lastPublishedEvent), new object[] { "a", "b" });

            if (!eventCounter.Wait(5))
            {
                throw new Exception($"Did not process event published before start was called. Missed events: {eventCounter.CurrentCount}");
            }
        }

        [Fact]
        public void ShouldAddEventProcessorsAfterPublishing()
        {
            var rb = _disruptor.GetRingBuffer();

            var b1 = new BatchEventProcessor<TestEvent>(rb, rb.NewBarrier(), new SleepingEventHandler());
            var b2 = new BatchEventProcessor<TestEvent>(rb, rb.NewBarrier(b1.GetSequence()), new SleepingEventHandler());
            var b3 = new BatchEventProcessor<TestEvent>(
             rb, rb.NewBarrier(b2.GetSequence()), new SleepingEventHandler());

            Assert.Equal(b1.GetSequence().GetValue(), -1L);
            Assert.Equal(b2.GetSequence().GetValue(), -1L);
            Assert.Equal(b3.GetSequence().GetValue(), -1L);

            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());

            _disruptor.HandleEventsWith(b1, b2, b3);

            Assert.Equal(5L, b1.GetSequence().GetValue());
            Assert.Equal(5L, b2.GetSequence().GetValue());
            Assert.Equal(5L, b3.GetSequence().GetValue());
        }

        private class EventHandler : IEventHandler<TestEvent>
        {
            private readonly CountdownEvent _counter;
            public EventHandler(CountdownEvent counter)
            {
                _counter = counter;
            }

            public void OnEvent(TestEvent @event, long sequence, bool endOfBatch)
            {
                _counter.Signal();
            }
        }

        private class EventTranslator : IEventTranslator<TestEvent>
        {
            private TestEvent _event;

            public EventTranslator(TestEvent lastPublishedEvent)
            {
                _event = lastPublishedEvent;
            }

            public void TranslateTo(TestEvent @event, long sequence)
            {
                _event = @event;
            }
        }

        private class EventTranslatorOneArg : IEventTranslatorOneArg<TestEvent, object>
        {
            private TestEvent _event;

            public EventTranslatorOneArg(TestEvent lastPublishedEvent)
            {
                _event = lastPublishedEvent;
            }

            public void TranslateTo(TestEvent @event, long sequence, object arg)
            {
                _event = @event;
            }
        }
    }
}
