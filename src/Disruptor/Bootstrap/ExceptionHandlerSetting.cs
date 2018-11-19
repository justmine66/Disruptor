using Disruptor.Exceptions;

namespace Disruptor.Bootstrap
{
    /// <summary>
    /// A support class used as part of setting an exception handler for a specific event handler.
    /// 
    /// For example:
    /// disruptorWizard.handleExceptionsIn(eventHandler).with(exceptionHandler);
    /// </summary>
    /// <typeparam name="T">the type of event being handled.</typeparam>
    public class ExceptionHandlerSetting<T> where T : class
    {
        private readonly IEventHandler<T> _eventHandler;
        private readonly ConsumerRepository<T> _consumerRepository;

        public ExceptionHandlerSetting(IEventHandler<T> eventHandler, ConsumerRepository<T> consumerRepository)
        {
            _eventHandler = eventHandler;
            _consumerRepository = consumerRepository;
        }

        /// <summary>
        /// Specify the <see cref="IExceptionHandler{TEvent}"/> to use with the event handler.
        /// </summary>
        /// <param name="exceptionHandler">the exception handler to use.</param>
        public void With(IExceptionHandler<T> exceptionHandler)
        {
            var eventProcessor = _consumerRepository.GetEventProcessorFor(_eventHandler);
            if (eventProcessor is IBatchEventProcessor<T> batchEventProcessor)
            {
                batchEventProcessor.SetExceptionHandler(exceptionHandler);
                _consumerRepository.GetBarrierFor(_eventHandler).Alert();
            }
            else
            {
                throw new RuntimeException(
                    "EventProcessor: " + eventProcessor + " is not a BatchEventProcessor " +
                    "and does not support exception handlers.");
            }
        }
    }
}
