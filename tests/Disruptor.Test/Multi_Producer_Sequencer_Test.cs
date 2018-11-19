using Disruptor.Impl;
using Xunit;

namespace Disruptor.Test
{
    public class Multi_Producer_Sequencer_Test
    {
        private readonly ISequencer _publisher;

        public Multi_Producer_Sequencer_Test()
        {
            _publisher = new MultiProducerSequencer(1024, new BlockingWaitStrategy());
        }

        [Fact(DisplayName = "已发布的消息应该是可用的")]
        public void Should_Only_Allow_Messages_To_Be_Available_If_Specifically_Published()
        {
            _publisher.Publish(3);
            _publisher.Publish(5);

            Assert.False(_publisher.IsAvailable(0));
            Assert.False(_publisher.IsAvailable(1));
            Assert.False(_publisher.IsAvailable(2));
            Assert.True(_publisher.IsAvailable(3));
            Assert.False(_publisher.IsAvailable(4));
            Assert.True(_publisher.IsAvailable(5));
            Assert.False(_publisher.IsAvailable(6));
        }
    }
}
