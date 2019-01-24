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

        [Fact]
        public void ShouldSetSequenceForHandlerIfAddedAfterPublish()
        {
            var rb = _disruptor.GetRingBuffer();

            var h1 = new SleepingEventHandler();
            var h2 = new SleepingEventHandler();
            var h3 = new SleepingEventHandler();

            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());

            _disruptor.HandleEventsWith(h1, h2, h3);

            Assert.Equal(5L, _disruptor.GetSequenceValueFor(h1));
            Assert.Equal(5L, _disruptor.GetSequenceValueFor(h2));
            Assert.Equal(5L, _disruptor.GetSequenceValueFor(h3));
        }

        [Fact]
        public void ShouldSetSequenceForWorkProcessorIfAddedAfterPublish()
        {
            var rb = _disruptor.GetRingBuffer();

            var w1 = CreateTestWorkHandler();
            var w2 = CreateTestWorkHandler();
            var w3 = CreateTestWorkHandler();

            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());
            rb.Publish(rb.Next());

            _disruptor.HandleEventsWithWorkerPool(w1, w2, w3);

            Assert.Equal(5L, rb.GetMinimumGatingSequence());
        }

        [Fact]
        public async Task ShouldCreateEventProcessorGroupForFirstEventProcessorsAsync()
        {
            _executor.IgnoreExecutions();

            var eventHandler1 = new SleepingEventHandler();
            var eventHandler2 = new SleepingEventHandler();

            var eventHandlerGroup = _disruptor.HandleEventsWith(eventHandler1, eventHandler2);

            await _disruptor.StartAsync();

            Assert.NotNull(eventHandlerGroup);
            Assert.Equal(2, _executor.ExecutionCount);
        }

        [Fact]
        public void ShouldMakeEntriesAvailableToFirstHandlersImmediately()
        {
            var countDownLatch = new CountdownEvent(2);
            var eventHandler = new EventHandlerStub<TestEvent>(countDownLatch);

            _disruptor.HandleEventsWith(CreateDelayedEventHandler(), eventHandler);

            EnsureTwoEventsProcessedAccordingToDependencies(countDownLatch);
        }

        private async Task EnsureTwoEventsProcessedAccordingToDependencies(CountdownEvent countDownLatch, params DelayedEventHandler[] dependencies)
        {
            await PublishEventAsync();
            await PublishEventAsync();

            foreach (var dependency in dependencies)
            {
                Assert.Equal(2, countDownLatch.CurrentCount);
                dependency.ProcessEvent();
                dependency.ProcessEvent();
            }

            AssertThatCountDownLatchIsZero(countDownLatch);
        }

        private async Task<TestEvent> PublishEventAsync()
        {
            if (_ringBuffer == null)
            {
                _ringBuffer = await _disruptor.StartAsync();
                foreach (var eventHandler in _delayedEventHandlers)
                {
                    eventHandler.AwaitStart();
                }
            }

            _disruptor.PublishEvent(new EventTranslator(_lastPublishedEvent));

            return _lastPublishedEvent;
        }

        private DelayedEventHandler CreateDelayedEventHandler()
        {
            var delayedEventHandler = new DelayedEventHandler();
            _delayedEventHandlers.Add(delayedEventHandler);
            return delayedEventHandler;
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

        private TestWorkHandler CreateTestWorkHandler()
        {
            var testWorkHandler = new TestWorkHandler();
            _testWorkHandlers.Add(testWorkHandler);
            return testWorkHandler;
        }

        private void AssertThatCountDownLatchIsZero(CountdownEvent countDownLatch)
        {
            var released = countDownLatch.Wait(TIMEOUT_IN_SECONDS);
            Assert.True(released);
        }
    }
}
