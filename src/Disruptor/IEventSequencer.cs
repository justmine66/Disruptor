namespace Disruptor
{
    public interface IEventSequencer<out TEvent> : IDataProvider<TEvent>, ISequenced
    {

    }
}
