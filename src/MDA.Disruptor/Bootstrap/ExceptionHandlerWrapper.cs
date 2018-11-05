using System;

namespace MDA.Disruptor.Bootstrap
{
    public class ExceptionHandlerWrapper<T> : IExceptionHandler<T>
    {
        private IExceptionHandler<T> _delegate;

        public void SwithTo(IExceptionHandler<T> exceptionHandler)
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
