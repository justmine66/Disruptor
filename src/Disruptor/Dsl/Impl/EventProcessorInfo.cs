using System.Threading.Tasks;

namespace Disruptor.Dsl.Impl
{
    /// <summary>
    /// Wrapper class to tie together a particular event processing stage.
    /// </summary>
    /// <remarks>
    /// Tracks the event processor instance, the event handler instance, and sequence barrier which the stage is attached to.
    /// </remarks>
    /// <typeparam name="T">the type of the configured <see cref="IEventHandler{TEvent}"/></typeparam>
    public class EventProcessorInfo<T> : IEventProcessorInfo<T>
    {
        private readonly IEventProcessor _processor;
        private readonly IEventHandler<T> _handler;
        private readonly ISequenceBarrier _barrier;

        private bool _endOfChain = true;

        public EventProcessorInfo(
            IEventProcessor processor,
            IEventHandler<T> handler,
            ISequenceBarrier barrier)
        {
            _processor = processor;
            _handler = handler;
            _barrier = barrier;
        }

        public IEventProcessor GetEventProcessor()
        {
            return _processor;
        }

        public IEventHandler<T> GetHandler()
        {
            return _handler;
        }

        public ISequenceBarrier GetBarrier()
        {
            return _barrier;
        }

        public ISequence[] GetSequences()
        {
            return new[] { _processor.GetSequence() };
        }

        public void Halt()
        {
            _processor.Halt();
        }

        public bool IsEndOfChain()
        {
            return _endOfChain;
        }

        public bool IsRunning()
        {
            return _processor.IsRunning();
        }

        public void MarkAsUsedInBarrier()
        {
            _endOfChain = false;
        }

        public async Task StartAsync(IExecutor executor)
        {
            await executor.ExecuteAsync(_processor);
        }
    }
}
