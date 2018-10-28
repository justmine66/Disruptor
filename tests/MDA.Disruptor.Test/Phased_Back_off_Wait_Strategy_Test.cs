namespace MDA.Disruptor.Test
{
    using MDA.Disruptor.Impl;
    using System;
    using Xunit;
    using static Support.WaitStrategyTestUtil;

    public class Phased_Back_off_Wait_Strategy_Test
    {
        [Fact(DisplayName = "当序号变化时，立即处理。")]
        public void Should_Handle_Immediate_Sequence_Change()
        {
            AssertWaitForWithDelayOf(0, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            AssertWaitForWithDelayOf(0, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }

        [Fact(DisplayName = "当序号变化时，延迟1毫秒再处理。")]
        public void Should_Handle_Sequence_Change_With_One_Millisecond_Delay()
        {
            AssertWaitForWithDelayOf(1, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            AssertWaitForWithDelayOf(1, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }

        [Fact(DisplayName = "当序号变化时，延迟2毫秒再处理。")]
        public void Should_Handle_Sequence_Change_With_Two_Millisecond_Delay()
        {
            AssertWaitForWithDelayOf(2, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            AssertWaitForWithDelayOf(2, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }

        [Fact(DisplayName = "当序号变化时，延迟10毫秒再处理。")]
        public void Should_Handle_Sequence_Change_With_Ten_Millisecond_Delay()
        {
            AssertWaitForWithDelayOf(10, PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
            AssertWaitForWithDelayOf(10, PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)));
        }
    }
}
