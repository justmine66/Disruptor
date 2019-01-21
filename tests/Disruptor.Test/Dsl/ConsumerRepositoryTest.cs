using Disruptor.Dsl;
using Disruptor.Test.Dsl.Stubs;
using Disruptor.Test.Support;
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
    }
}
