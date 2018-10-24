using MDA.Disruptor.Exceptions;
using MDA.Disruptor.Impl;
using MDA.Disruptor.Test.Support;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace MDA.Disruptor.Test
{
    public class Fatal_Exception_Handler_Test
    {
        private readonly Exception _exception;
        private readonly TestEvent _event;
        private readonly IExceptionHandler<TestEvent> _handler;

        public Fatal_Exception_Handler_Test()
        {
            _exception = new Exception();
            _event = new TestEvent();

            var provider = new ServiceCollection()
                .AddLogging()
                .AddScoped<IExceptionHandler<TestEvent>, FatalExceptionHandler<TestEvent>>()
                .BuildServiceProvider();

            _handler = provider.GetService<IExceptionHandler<TestEvent>>();
        }

        [Fact(DisplayName = "测试HandleEventException后抛出RuntimeException异常。")]
        public void Should_Handle_Fatal_Exception()
        {
            Assert.Throws<RuntimeException>(() => _handler.HandleEventException(_exception, 0L, _event));
        }
    }
}