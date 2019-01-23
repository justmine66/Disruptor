using Disruptor.Dsl;
using Disruptor.Impl;
using Disruptor.Test.Dsl.Stubs;
using Disruptor.Test.Support;
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
                Assert.False(true, $"Did not process event published before start was called. Missed events: {eventCounter.CurrentCount}");
            }
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
    }
}
