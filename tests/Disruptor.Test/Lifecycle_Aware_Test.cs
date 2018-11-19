using Disruptor.Impl;
using Disruptor.Test.Support;
using System;
using System.Threading;
using Xunit;

namespace Disruptor.Test
{
    public class Lifecycle_Aware_Test
    {
        private readonly CountdownEvent _startLatch;
        private readonly CountdownEvent _shutdownLatch;

        private readonly RingBuffer<StubEvent> _ringBuffer;
        private readonly ISequenceBarrier _barrier;
        private readonly LifecycleAwareEventHandler _handler;
        private readonly IBatchEventProcessor<StubEvent> _processor;

        public Lifecycle_Aware_Test()
        {
            _startLatch = new CountdownEvent(1);
            _shutdownLatch = new CountdownEvent(1);

            _ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(() => new StubEvent(-1), 16);
            _barrier = _ringBuffer.NewBarrier();
            _handler = new LifecycleAwareEventHandler(_startLatch, _shutdownLatch);
            _processor = new BatchEventProcessor<StubEvent>(_ringBuffer, _barrier, _handler);
        }

        [Fact(DisplayName = "接收批处理生命周期通知")]
        public void Should_Notify_Of_Batch_Processor_Lifecycle()
        {
            new Thread(() => _processor.Run()).Start();

            _startLatch.Wait();
            _processor.Halt();

            _shutdownLatch.Wait();

            Assert.Equal(1, _handler.StartCounter);
            Assert.Equal(1, _handler.ShutdownCounter);
        }

        private class LifecycleAwareEventHandler : IEventHandler<StubEvent>, ILifecycleAware
        {
            public int StartCounter;
            public int ShutdownCounter;

            private readonly CountdownEvent _startLatch;
            private readonly CountdownEvent _shutdownLatch;

            public LifecycleAwareEventHandler(CountdownEvent startLatch, CountdownEvent shutdownLatch)
            {
                _startLatch = startLatch;
                _shutdownLatch = shutdownLatch;
            }

            public void OnEvent(StubEvent @event, long sequence, bool endOfBatch)
            {
                throw new NotImplementedException();
            }

            public void OnShutdown()
            {
                ++ShutdownCounter;
                _shutdownLatch.Signal();
            }

            public void OnStart()
            {
                ++StartCounter;
                _startLatch.Signal();
            }
        }
    }
}
