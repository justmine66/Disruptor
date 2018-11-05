namespace MDA.Disruptor.Bootstrap
{
    public interface IEventProcessorInfo : IConsumerInfo
    {
        IEventProcessor GetProcessor();
    }
}
