using Disruptor.Exceptions;
using Disruptor.Impl;
using Disruptor.Test.Support;
using Microsoft.Extensions.DependencyInjection;
using System;
using Disruptor.Test.Support.DependencyInjection;
using Xunit;

namespace Disruptor.Test
{
    [Collection("ObjectContainerCollection")]
    public class Fatal_Exception_Handler_Test
    {
        private readonly Exception _exception;
        private readonly TestEvent _event;
        private readonly IExceptionHandler<TestEvent> _handler;

        public Fatal_Exception_Handler_Test(ObjectContainerFixture provider)
        {
            _exception = new Exception();
            _event = new TestEvent();
            _handler = provider.Services.GetService<IExceptionHandler<TestEvent>>();
        }

        [Fact(DisplayName = "测试HandleEventException后抛出RuntimeException异常。")]
        public void Should_Handle_Fatal_Exception()
        {
            Assert.Throws<RuntimeException>(() => _handler.HandleEventException(_exception, 0L, _event));
        }
    }
}