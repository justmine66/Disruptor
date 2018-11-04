using System.Collections.Generic;
using System.Linq;

namespace MDA.Disruptor.DSL
{
    /// <summary>
    /// A group of <see cref="IEventProcessor"/>s used as part of the <see cref="Disruptor{T}"/>.
    /// </summary>
    /// <typeparam name="T">the type of entry used by the event processors.</typeparam>
    public class EventHandlerGroup<T> where T : class
    {
        private readonly Disruptor<T> _disruptor;
        private readonly ConsumerRepository<T> _consumerRepository;
        private readonly ISequence[] _sequences;

        public EventHandlerGroup(
         Disruptor<T> disruptor,
         ConsumerRepository<T> consumerRepository,
         IEnumerable<ISequence> sequences)
        {
            _disruptor = disruptor;
            _consumerRepository = consumerRepository;
            _sequences = sequences.ToArray();
        }

        /// <summary>
        /// Create a new event handler group that combines the consumers in this group with <tt>otherHandlerGroup</tt>.
        /// </summary>
        /// <param name="other">the event handler group to combine.</param>
        /// <returns>a new EventHandlerGroup combining the existing and new consumers into a single dependency group.</returns>
        public EventHandlerGroup<T> And(EventHandlerGroup<T> other)
        {
            return new EventHandlerGroup<T>(_disruptor, _consumerRepository, _sequences.Concat(other?._sequences));
        }

        /// <summary>
        /// Create a new event handler group that combines the handlers in this group with <paramref name="processors"/>.
        /// </summary>
        /// <param name="processors">the processors to combine.</param>
        /// <returns>a new EventHandlerGroup combining the existing and new processors into a single dependency group.</returns>
        public EventHandlerGroup<T> And(params IEventProcessor[] processors)
        {

            return new EventHandlerGroup<T>(_disruptor, _consumerRepository, processors?.Select(it => it.GetSequence())?.Concat(_sequences));
        }

        /// <summary>
        /// Set up batch handlers to consume events from the ring buffer. These handlers will only process events after every <see cref="IEventProcessor"/> in this group has processed the event.
        /// 
        /// This method is generally used as part of a chain. For example if the handler <code>A</code> must process events before handler<code>B</code>:<code>dw.handleEventsWith(A).then(B);</code>
        /// </summary>
        /// <param name="handlers">the batch handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
        public EventHandlerGroup<T> Then(params IEventHandler<T>[] handlers)
        {
            return HandleEventsWith(handlers);
        }

        /// <summary>
        /// Set up batch handlers to handle events from the ring buffer. These handlers will only process events after every <see cref="IEventProcessor"/> in this group has processed the event.
        /// 
        /// This method is generally used as part of a chain. For example if <code>A</code> must process events before<code> B</code>: <code>dw.after(A).handleEventsWith(B);</code>
        /// </summary>
        /// <param name="handlers">the batch handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a event processor barrier over the created event processors.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventHandler<T>[] handlers)
        {
            return _disruptor.CreateEventProcessors(_sequences, handlers);
        }

        public EventHandlerGroup<T> HandleEventsWithWorkerPool(params IWorkHandler<T>[] handlers)
        {
            return _disruptor.CreateWorkerPool(_sequences, handlers);
        }

        /// <summary>
        /// Create a dependency barrier for the processors in this group.This allows custom event processors to have dependencies on <see cref="IBatchEventProcessor{TEvent}"/>s created by the disruptor.
        /// </summary>
        /// <returns>a <see cref="ISequenceBarrier"/> including all the processors in this group.</returns>
        public ISequenceBarrier AsSequenceBarrier()
        {
            return _disruptor.GetRingBuffer().NewBarrier(_sequences);
        }
    }
}
