namespace MDA.Disruptor.DSL
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

        public void With(IExceptionHandler<T> exceptionHandler)
        {
            
        }
    }
}
