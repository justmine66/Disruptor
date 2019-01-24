using System.Threading;

namespace Disruptor.Test.Dsl.Stubs
{
    public class EventHandlerStub<T> : IEventHandler<T>
    {
        private readonly CountdownEvent _counter;

        public EventHandlerStub(CountdownEvent counter)
        {
            _counter = counter;
        }

        public void OnEvent(T @event, long sequence, bool endOfBatch)
        {
            _counter.Signal();
        }
    }
}
