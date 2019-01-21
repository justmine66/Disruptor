using System;

namespace Disruptor.Dsl.Impl
{
    public class ExceptionHandlerWrapper<T> : IExceptionHandler<T>
    {
        private IExceptionHandler<T> _delegate;

        public void SwitchTo(IExceptionHandler<T> exceptionHandler)
        {
            // default: FatalExceptionHandler
            _delegate = exceptionHandler;
        }

        public void HandleEventException(Exception ex, long sequence, T @event)
        {
            _delegate.HandleEventException(ex, sequence, @event);
        }

        public void HandleOnShutdownException(Exception ex)
        {
            _delegate.HandleOnStartException(ex);
        }

        public void HandleOnStartException(Exception ex)
        {
            _delegate.HandleOnStartException(ex);
        }
    }
}
