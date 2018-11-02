using MDA.Disruptor.Impl;

namespace MDA.Disruptor
{
    /// <summary>
    /// Callback interface to be implemented for processing units of work as they become available in the <see cref="RingBuffer{TEvent}"/>.
    /// </summary>
    /// <typeparam name="TEvent">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public interface IWorkHandler<in TEvent>
    {
        /// <summary>
        /// Callback to indicate a unit of work needs to be processed.
        /// </summary>
        /// <param name="event">published to the <see cref="RingBuffer{TEvent}"/></param>
        void OnEvent(TEvent @event);
    }
}
