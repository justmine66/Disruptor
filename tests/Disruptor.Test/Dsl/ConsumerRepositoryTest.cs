using Disruptor.Dsl;
using Disruptor.Dsl.Impl;
using Disruptor.Test.Dsl.Stubs;
using Disruptor.Test.Support;
using System;
using Xunit;

namespace Disruptor.Test.Dsl
{
    public class ConsumerRepositoryTest
    {
        private readonly ConsumerRepository<TestEvent> _repository;
        private readonly IEventProcessor _processor1;
        private readonly IEventProcessor _processor2;
        private readonly SleepingEventHandler _handler1;
        private readonly SleepingEventHandler _handler2;
        private readonly ISequenceBarrier _barrier1;
        private readonly ISequenceBarrier _barrier2;

        public ConsumerRepositoryTest()
        {
            _repository = new ConsumerRepository<TestEvent>();
            _processor1 = new DummyEventProcessor();
            _processor2 = new DummyEventProcessor();

            _processor1.Run();
            _processor2.Run();

            _handler1 = new SleepingEventHandler();
            _handler2 = new SleepingEventHandler();

            _barrier1 = new DummySequenceBarrier();
            _barrier2 = new DummySequenceBarrier();
        }

        [Fact]
        public void ShouldGetBarrierByHandler()
        {
            _repository.Add(_processor1, _handler1, _barrier1);
            var barrier2 = _repository.GetBarrierFor(_handler1);

            Assert.Equal(barrier2, _barrier1);
        }

        [Fact]
        public void ShouldReturnNullForBarrierWhenHandlerIsNotRegistered()
        {
            Assert.Null(_repository.GetBarrierFor(_handler1));
        }

        [Fact]
        public void ShouldGetLastEventProcessorsInChain()
        {
            _repository.Add(_processor1, _handler1, _barrier1);
            _repository.Add(_processor2, _handler2, _barrier2);

            _repository.UnMarkEventProcessorsAsEndOfChain(_processor2.GetSequence());
            var sequences = _repository.GetLastSequenceInChain(true);

            Assert.Single(sequences);
            Assert.Equal(sequences[0], _processor1.GetSequence());
        }

        [Fact]
        public void ShouldRetrieveEventProcessorForHandler()
        {
            _repository.Add(_processor1, _handler1, _barrier1);
            var processor = _repository.GetEventProcessorFor(_handler1);

            Assert.Equal(processor, _processor1);
        }

        [Fact]
        public void ShouldThrowExceptionWhenHandlerIsNotRegistered()
        {
            Assert.Throws<ArgumentException>(() => _repository.GetEventProcessorFor(_handler1));
        }

        [Fact]
        public void ShouldIterateAllEventProcessors()
        {
            _repository.Add(_processor1, _handler1, _barrier1);
            _repository.Add(_processor2, _handler2, _barrier2);

            var seen1 = false;
            var seen2 = false;
            foreach (var entry in _repository)
            {
                var eventProcessorInfo = (EventProcessorInfo<TestEvent>)entry;
                if (!seen1 &&
                    eventProcessorInfo.GetEventProcessor() == _processor1 &&
                    eventProcessorInfo.GetHandler() == _handler1)
                {
                    seen1 = true;
                }
                else if (!seen2 &&
                    eventProcessorInfo.GetEventProcessor() == _processor2 &&
                    eventProcessorInfo.GetHandler() == _handler2)
                {
                    seen2 = true;
                }
                else
                {
                    throw new Exception("Unexpected eventProcessor info: " + eventProcessorInfo);
                }
            }

            Assert.True(seen1);
            Assert.True(seen2);
        }
    }
}
