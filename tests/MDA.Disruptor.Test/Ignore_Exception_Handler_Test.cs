using MDA.Disruptor.Impl;
using MDA.Disruptor.Test.Support;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace MDA.Disruptor.Test
{
    public class Ignore_Exception_Handler_Test
    {
        [Fact(DisplayName = "忽略异常，只记录日志。")]
        public void Should_Handle_And_Ignore_Exception()
        {
            var ex = new Exception();
            var @event = new TestEvent();

            var provider = new ServiceCollection()
               .AddLogging()
               .AddScoped<IExceptionHandler<object>, IgnoreExceptionHandler>()
               .BuildServiceProvider();

            var handler = provider.GetService<IExceptionHandler<object>>();
            handler.HandleEventException(ex, 0L, @event);
        }
    }
}
