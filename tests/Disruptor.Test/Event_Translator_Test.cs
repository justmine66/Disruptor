using Disruptor.Test.Support;
using Xunit;

namespace Disruptor.Test
{
    public class Event_Translator_Test
    {
        private static readonly string TestValue = "Wibble";

        [Fact(DisplayName = "翻译事件")]
        public void Should_Translate_Other_Data_Into_An_Event()
        {
            var @event = StubEvent.EventFactory.NewInstance();
            var translator = new ExampleEventTranslator(TestValue);
            translator.TranslateTo(@event, 0);

            Assert.Equal(@event.TestString, TestValue);
        }

        private class ExampleEventTranslator : IEventTranslator<StubEvent>
        {
            private readonly string _testValue;
            public ExampleEventTranslator(string testValue)
            {
                _testValue = testValue;
            }

            public void TranslateTo(StubEvent @event, long sequence)
            {
                @event.TestString = _testValue;
            }
        }
    }
}
