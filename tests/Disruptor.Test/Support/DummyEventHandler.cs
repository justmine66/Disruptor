namespace Disruptor.Test.Support
{
    public class DummyEventHandler<TEvent> : IEventHandler<TEvent>, ILifecycleAware
    {
        public int StartCalls = 0;
        public int ShutdownCalls = 0;
        public TEvent LastEvent;
        public long LastSequence;

        public void OnEvent(TEvent @event, long sequence, bool endOfBatch)
        {
            LastEvent = @event;
            LastSequence = sequence;
        }

        public void OnShutdown()
        {
            ShutdownCalls++;
        }

        public void OnStart()
        {
            StartCalls++;
        }
    }
}
