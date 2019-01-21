using Disruptor.Test.Support;
using System.Threading;

namespace Disruptor.Test.Dsl.Stubs
{
    public class SleepingEventHandler : IEventHandler<TestEvent>
    {
        public void OnEvent(TestEvent @event, long sequence, bool endOfBatch)
        {
            Thread.Sleep(1000);
        }
    }
}
