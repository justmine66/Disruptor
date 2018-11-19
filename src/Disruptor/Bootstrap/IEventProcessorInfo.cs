namespace Disruptor.Bootstrap
{
    public interface IEventProcessorInfo<in T> : IConsumerInfo
    {
        IEventProcessor GetEventProcessor();
        IEventHandler<T> GetHandler();
    }
}
