namespace Disruptor.Dsl
{
    public interface IEventProcessorInfo<in T> : IConsumerInfo
    {
        IEventProcessor GetEventProcessor();
        IEventHandler<T> GetHandler();
    }
}
