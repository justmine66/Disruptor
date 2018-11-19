using Disruptor.Test.Support;
using Disruptor.Test.Support.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace Disruptor.Test
{
    [Collection("ObjectContainerCollection")]
    public class Ignore_Exception_Handler_Test
    {
        private readonly IExceptionHandler<object> _exceptionHandler;
        public Ignore_Exception_Handler_Test(ObjectContainerFixture provider)
        {
            _exceptionHandler= provider.Services.GetService<IExceptionHandler<object>>();
        }

        [Fact(DisplayName = "忽略异常，只记录日志。")]
        public void Should_Handle_And_Ignore_Exception()
        {
            var ex = new Exception();
            var @event = new TestEvent();
            _exceptionHandler.HandleEventException(ex, 0L, @event);
        }
    }
}
