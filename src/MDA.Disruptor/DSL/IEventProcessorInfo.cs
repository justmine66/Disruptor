namespace MDA.Disruptor.DSL
{
    public interface IEventProcessorInfo : IConsumerInfo
    {
        IEventProcessor GetProcessor();
    }
}
