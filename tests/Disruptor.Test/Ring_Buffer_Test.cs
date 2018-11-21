using Disruptor.Exceptions;
using Disruptor.Impl;
using Disruptor.Test.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Disruptor.Test
{
    public class Ring_Buffer_Test
    {
        private readonly RingBuffer<StubEvent> _ringBuffer;
        private readonly ISequenceBarrier _barrier;

        public Ring_Buffer_Test()
        {
            _ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(StubEvent.EventFactory, 32);
            _barrier = _ringBuffer.NewBarrier();
            _ringBuffer.AddGatingSequences(new NoOpEventProcessor<StubEvent>(_ringBuffer).GetSequence());
        }

        [Fact(DisplayName = "应该能发布和获取事件。")]
        public void Should_Publish_And_Get()
        {
            Assert.Equal(Sequence.InitialValue, _ringBuffer.GetCursor());

            var expectedEvent = new StubEvent(2701);
            _ringBuffer.PublishEvent(StubEvent.Translator, expectedEvent.Value, expectedEvent.TestString);

            var sequence = _barrier.WaitFor(0L);
            Assert.Equal(0L, sequence);

            var @event = _ringBuffer.Get(sequence);
            Assert.Equal(expectedEvent, @event);

            Assert.Equal(0L, _barrier.GetCursor());
        }

        [Fact(DisplayName = "在一个单独的线程里，应该能发布和获取事件。")]
        public void Should_Claim_And_Get_In_Separate_Thread()
        {
            var messages = GetMessages(0, 0);

            var expectedEvent = new StubEvent(2701);
            _ringBuffer.PublishEvent(StubEvent.Translator, expectedEvent.Value, expectedEvent.TestString);

            Assert.Equal(expectedEvent, messages.Result[0]);
        }

        [Fact(DisplayName = "应该能发布和获取多个事件。")]
        public void Should_Claim_And_Get_Multiple_Messages()
        {
            var numMessages = _ringBuffer.GetBufferSize();
            for (var i = 0; i < numMessages; i++)
            {
                _ringBuffer.PublishEvent(StubEvent.Translator, i, "");
            }

            var expectedSequence = numMessages - 1;
            var availableSequence = _barrier.WaitFor(expectedSequence);

            Assert.True(expectedSequence == availableSequence);

            for (var i = 0; i < numMessages; i++)
            {
                Assert.Equal(i, _ringBuffer.Get(i).Value);
            }
        }

        [Fact(DisplayName = "门控序号跟上消费序号，应该能够无限持续发布事件。")]
        public void Should_Wrap()
        {
            var numMessages = _ringBuffer.GetBufferSize();
            var offset = 1000;
            for (var i = 0; i < numMessages + offset; i++)
            {
                _ringBuffer.PublishEvent(StubEvent.Translator, i, "");
            }

            var expectedSequence = numMessages + offset - 1;
            var available = _barrier.WaitFor(expectedSequence);
            Assert.Equal(expectedSequence, available);

            for (var i = offset; i < numMessages + offset; i++)
            {
                Assert.Equal(i, _ringBuffer.Get(i).Value);
            }
        }

        [Fact(DisplayName = "门控序号未跟上消费序号，只能发布ringBuffer容量（包装点=消费序号-门控序号）长度的事件。")]
        public void Should_Prevent_Wrapping()
        {
            var sequence = new Sequence();
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(StubEvent.EventFactory, 4);
            ringBuffer.AddGatingSequences(sequence);

            ringBuffer.PublishEvent(StubEvent.Translator, 0, "0");
            ringBuffer.PublishEvent(StubEvent.Translator, 1, "1");
            ringBuffer.PublishEvent(StubEvent.Translator, 2, "2");
            ringBuffer.PublishEvent(StubEvent.Translator, 3, "3");

            Assert.False(ringBuffer.TryPublishEvent(StubEvent.Translator, 4, "4"));
        }

        [Fact(DisplayName = "当buffer装满时，应该无法再次申请序号，并抛出异常。")]
        public void Should_Throw_Exception_If_Buffer_Is_Full()
        {
            _ringBuffer.AddGatingSequences(new Sequence(_ringBuffer.GetBufferSize()));

            for (var i = 0; i < _ringBuffer.GetBufferSize(); i++)
            {
                if (_ringBuffer.TryNext(out var sequence))
                {
                    _ringBuffer.Publish(sequence);
                }
            }

            Assert.Throws<InsufficientCapacityException>(() =>
            {
                _ringBuffer.TryNext(out var sequence);
            });
        }

        [Fact]
        public void Should_Prevent_Publishers_Over_taking_Event_Processor_Wrap_Point()
        {
            var ringBufferSize = 16;
            var latch = new CountdownEvent(ringBufferSize);
            var publisherComplete = false;
            var ringBuffer = RingBuffer<StubEvent>.CreateMultiProducer(StubEvent.EventFactory, ringBufferSize);
            var processor = new TestEventProcessor(ringBuffer.NewBarrier());
            ringBuffer.AddGatingSequences(processor.GetSequence());

            var thread = new Thread(() =>
              {
                  for (var i = 0; i <= ringBufferSize; i++)
                  {
                      var sequence = ringBuffer.Next();
                      var @event = ringBuffer.Get(sequence);
                      @event.Value = i;
                      ringBuffer.Publish(sequence);

                      if (i < ringBufferSize)
                      {
                          latch.Signal();
                      }
                  }

                  publisherComplete = true;
              });
            thread.Start();

            latch.Wait();
            Assert.Equal(ringBufferSize - 1, ringBuffer.GetCursor());
            Assert.False(publisherComplete);

            processor.Run();
            thread.Join();

            Assert.True(publisherComplete);
        }

        [Fact(DisplayName = "应该能够发布无参事件。")]
        public void Should_Publish_Event()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            ringBuffer.PublishEvent(translator);
            ringBuffer.TryPublishEvent(translator);

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { 0L }, new object[1] { 1L }));
        }

        [Fact(DisplayName = "应该能够发布带一个参数的事件。")]
        public void Should_Publish_Event_One_Arg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Arg");
            ringBuffer.TryPublishEvent(translator, "Arg");

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Arg-0" }, new object[1] { "Arg-1" }));
        }

        [Fact(DisplayName = "应该能够发布带两个参数的事件。")]
        public void Should_Publish_Event_Two_Arg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Arg0", "Arg1");
            ringBuffer.TryPublishEvent(translator, "Arg0", "Arg1");

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Arg0Arg1-0" }, new object[1] { "Arg0Arg1-1" }));
        }

        [Fact(DisplayName = "应该能够发布带三个参数的事件。")]
        public void Should_Publish_Event_Three_Arg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Arg0", "Arg1", "Arg2");
            ringBuffer.TryPublishEvent(translator, "Arg0", "Arg1", "Arg2");

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Arg0Arg1Arg2-0" }, new object[1] { "Arg0Arg1Arg2-1" }));
        }

        [Fact(DisplayName = "应该能够发布带可变参数的事件。")]
        public void Should_Publish_Event_Var_Arg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            ringBuffer.PublishEvent(translator, "Arg0");
            ringBuffer.TryPublishEvent(translator, "Arg0", "Arg1");
            ringBuffer.PublishEvent(translator, "Arg0", "Arg1", "Arg2");
            ringBuffer.TryPublishEvent(translator, "Arg0", "Arg1", "Arg2", "Arg3");

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(
                new object[1] { "Arg0-0" },
                new object[1] { "Arg0Arg1-1" },
                new object[1] { "Arg0Arg1Arg2-2" },
                new object[1] { "Arg0Arg1Arg2Arg3-3" }));
        }

        [Fact(DisplayName = "应该能够批量发布事件。")]
        public void Should_Publish_Events()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();
            var translators = new[] { translator, translator };

            ringBuffer.PublishEvents(translators);
            Assert.True(ringBuffer.TryPublishEvents(translators));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(
                new object[1] { 0L },
                new object[1] { 1L },
                new object[1] { 2L },
                new object[1] { 3L }));
        }

        [Fact(DisplayName = "如果批量大于RingBuffer的容量，不应该发布事件。")]
        public void Should_Not_Publish_Events_If_Batch_Is_Larger_Than_RingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();
            var translators = new[] { translator, translator, translator, translator, translator };

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translators));
            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translators));
        }

        [Fact(DisplayName = "应该能够发布具有批量大小的事件。")]
        public void Should_Publish_Events_With_Batch_Size_Of_One()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();
            var translators = new[] { translator, translator, translator };

            ringBuffer.PublishEvents(translators, 0, 1);
            Assert.True(ringBuffer.TryPublishEvents(translators, 0, 1));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(
                new object[1] { 0L },
                new object[1] { 1L }));
        }

        [Fact(DisplayName = "应该能够发布批次范围内的事件。")]
        public void Should_Publish_Events_Within_Batch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();
            var translators = new[] { translator, translator, translator };

            ringBuffer.PublishEvents(translators, 1, 2);
            Assert.True(ringBuffer.TryPublishEvents(translators, 1, 2));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(
                new object[1] { 0L },
                new object[1] { 1L },
                new object[1] { 2L },
                new object[1] { 3L }));
        }

        [Fact(DisplayName = "应该能够批量发布带一个参数的事件。")]
        public void Should_Publish_Events_One_Arg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            ringBuffer.PublishEvents(translator, new string[] { "Boo", "Foo" });
            Assert.True(ringBuffer.TryPublishEvents(translator, new string[] { "Boo", "Foo" }));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Boo-0" }, new object[1] { "Foo-1" }, new object[1] { "Boo-2" }, new object[1] { "Foo-3" }));
        }

        [Fact]
        public void Should_Not_Publish_Events_One_Arg_If_Batch_Is_Larger_Than_Ring_Buffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, new string[] { "Boo", "Foo", "Foo", "Foo", "Foo" }));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Publish_Events_One_Arg_Batch_Size_Of_One()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            ringBuffer.PublishEvents(translator, 0, 1, new[] { "Boo" });
            Assert.True(ringBuffer.TryPublishEvents(translator, 0, 1, new[] { "Boo" }));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Boo-0" }, new object[1] { "Boo-1" }));
        }

        [Fact]
        public void Should_Publish_Events_One_Arg_Within_Batch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            ringBuffer.PublishEvents(translator, 1, 2, new[] { "Boo", "Foo", "Zoo" });
            Assert.True(ringBuffer.TryPublishEvents(translator, 1, 2, new[] { "Boo", "Foo", "Zoo" }));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Foo-0" }, new object[1] { "Zoo-1" }, new object[1] { "Foo-2" }, new object[1] { "Zoo-3" }));
        }

        [Fact]
        public void Should_Publish_Events_Two_Arg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvents(translator, new[] { "Foo", "Hoo" }, new[] { "Bar", "Car" });
            Assert.True(ringBuffer.TryPublishEvents(translator, new[] { "Foo", "Hoo" }, new[] { "Bar", "Car" }));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "FooBar-0" }, new object[1] { "HooCar-1" }, new object[1] { "FooBar-2" }, new object[1] { "HooCar-3" }));
        }

        [Fact]
        public void Should_Not_Publish_Events_Two_Arg_If_Batch_Size_Is_Bigger_Than_RingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                translator,
                new[] { "Foo", "Foo", "Foo", "Foo", "Foo" },
                new[] { "Bar", "Bar", "Bar", "Bar", "Bar" }));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Publish_Events_Two_Arg_With_Batch_Size_Of_One()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvents(translator, 0, 1, new[] { "Foo0", "Foo1" }, new[] { "Bar0", "Bar1" });
            ringBuffer.TryPublishEvents(translator, 0, 1, new[] { "Foo2", "Foo3" }, new[] { "Bar2", "Bar3" });
        }

        [Fact]
        public void Should_Publish_Events_Two_Arg_Within_Batch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            ringBuffer.PublishEvents(
            translator, 1, 2, new[] { "Foo0", "Foo1", "Foo2" }, new[] { "Bar0", "Bar1", "Bar2" });
            ringBuffer.TryPublishEvents(
                translator, 1, 2, new[] { "Foo3", "Foo4", "Foo5" }, new[] { "Bar3", "Bar4", "Bar5" });

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Foo1Bar1-0" }, new object[1] { "Foo2Bar2-1" }, new object[1] { "Foo4Bar4-2" }, new object[1] { "Foo5Bar5-3" }));
        }

        [Fact]
        public void Should_Publish_Events_Three_Arg()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvents(
            translator, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });
            ringBuffer.TryPublishEvents(
                translator, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "FooBarBaz-0" }, new object[1] { "FooBarBaz-1" }, new object[1] { "FooBarBaz-2" }, new object[1] { "FooBarBaz-3" }));
        }

        [Fact]
        public void Should_Not_Publish_Events_Three_Arg_If_Batch_Is_Larger_Than_RingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                translator,
                new[] { "Foo", "Foo", "Foo", "Foo", "Foo" },
                new[] { "Bar", "Bar", "Bar", "Bar", "Bar" },
                new[] { "Baz", "Baz", "Baz", "Baz", "Baz" }));


            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Publish_Events_Three_Arg_Batch_Size_Of_One()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvents(
           translator, 0, 1, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });
            ringBuffer.TryPublishEvents(
                translator, 0, 1, new[] { "Foo", "Foo" }, new[] { "Bar", "Bar" }, new[] { "Baz", "Baz" });


            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "FooBarBaz-0" }, new object[1] { "FooBarBaz-1" }));
        }

        [Fact]
        public void Should_Publish_Events_Three_Arg_Within_Batch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            ringBuffer.PublishEvents(
            translator, 1, 2, new[] { "Foo0", "Foo1", "Foo2" }, new[] { "Bar0", "Bar1", "Bar2" },
            new[] { "Baz0", "Baz1", "Baz2" });
            Assert.True(ringBuffer.TryPublishEvents(
                translator, 1, 2, new[] { "Foo3", "Foo4", "Foo5" }, new[] { "Bar3", "Bar4", "Bar5" },
                new[] { "Baz3", "Baz4", "Baz5" }));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Foo1Bar1Baz1-0" }, new object[1] { "Foo2Bar2Baz2-1" }, new object[1] { "Foo4Bar4Baz4-2" }, new object[1] { "Foo5Bar5Baz5-3" }));
        }

        [Fact]
        public void Should_Not_Publish_Events_Var_Arg_If_Batch_Is_Larger_Than_RingBuffer()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(
                translator,
                new[] { "Foo", "Bar", "Baz", "Bam" },
                new[] { "Foo", "Bar", "Baz", "Bam" },
                new[] { "Foo", "Bar", "Baz", "Bam" },
                new[] { "Foo", "Bar", "Baz", "Bam" },
                new[] { "Foo", "Bar", "Baz", "Bam" }));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Publish_Events_Var_Arg_Batch_Size_Of_One()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            ringBuffer.PublishEvents(
            translator, 0, 1, new[] { "Foo", "Bar", "Baz", "Bam" }, new[] { "Foo1", "Bar1", "Baz1", "Bam1" });
            Assert.True(

                ringBuffer.TryPublishEvents(
                    translator, 0, 1, new[] { "Foo", "Bar", "Baz", "Bam" }, new[] { "Foo2", "Bar2", "Baz2", "Bam2" }));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "FooBarBazBam-0" }, new object[1] { "FooBarBazBam-1" }));
        }

        [Fact]
        public void Should_Publish_Events_Var_Arg_Within_Batch()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            ringBuffer.PublishEvents(
            translator, 1, 2, new[] { "Foo0", "Bar0", "Baz0", "Bam0" },
            new[] { "Foo1", "Bar1", "Baz1", "Bam1" },
            new[] { "Foo2", "Bar2", "Baz2", "Bam2" });
            Assert.True(
                ringBuffer.TryPublishEvents(
                    translator, 1, 2, new[] { "Foo3", "Bar3", "Baz3", "Bam3" },
                    new[] { "Foo4", "Bar4", "Baz4", "Bam4" },
                    new[] { "Foo5", "Bar5", "Baz5", "Bam5" }));

            var matcher = new RingBufferEventMatcher(ringBuffer);
            Assert.True(matcher.RingBufferWithEvents(new object[1] { "Foo1Bar1Baz1Bam1-0" }, new object[1] { "Foo2Bar2Baz2Bam2-1" }, new object[1] { "Foo4Bar4Baz4Bam4-2" }, new object[1] { "Foo5Bar5Baz5Bam5-3" }));
        }

        [Fact]
        public void Should_Not_Publish_Events_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator, translator }, 1, 0));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator, translator }, 1, 0));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator }, 1, 3));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator }, 1, 3));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator }, 1, -1));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator }, 1, -1));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(new[] { translator, translator, translator }, -1, 2));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new NoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(new[] { translator, translator, translator }, -1, 2));

            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_One_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_One_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_One_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_One_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_One_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_One_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_One_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_One_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new OneArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Two_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Two_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Two_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Two_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Two_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Two_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Two_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Two_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new TwoArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Three_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Three_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Three_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Three_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Three_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Three_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Three_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Three_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new ThreeArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Var_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Var_Arg_When_Batch_Size_Is_0()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 0, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Var_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Var_Arg_When_Batch_Extends_Past_End_Of_Array()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, 3, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Var_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Events_Var_Arg_When_Batch_Size_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, 1, -1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Publish_Events_Var_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.PublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        [Fact]
        public void Should_Not_Try_Publish_Var_Three_Arg_When_Batch_Starts_At_Is_Negative()
        {
            var ringBuffer = RingBuffer<object[]>.CreateSingleProducer(new ArrayFactory(1), 4);
            var translator = new VarArgEventTranslator();

            Assert.Throws<ArgumentException>(() => ringBuffer.TryPublishEvents(translator, -1, 1, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }, new[] { "Foo", "Foo" }));
            AssertEmptyRingBuffer(ringBuffer);
        }

        private Task<List<StubEvent>> GetMessages(long initial, long toWaitFor)
        {
            var barrier = new Barrier(2);
            var dependencyBarrier = _ringBuffer.NewBarrier();

            var f = Task.Factory.StartNew(() => new TestWaiter(barrier, dependencyBarrier, _ringBuffer, initial, toWaitFor).Call());

            barrier.SignalAndWait();

            return f;
        }

        public class TestEventProcessor : IEventProcessor
        {
            private readonly ISequenceBarrier _barrier;
            private readonly ISequence _sequence = new Sequence();

            private int _running;

            public TestEventProcessor(ISequenceBarrier barrier)
            {
                _barrier = barrier;
            }

            public void Run()
            {
                if (Interlocked.Exchange(ref _running, 1) == 1)
                {
                    throw new IllegalStateException("Thread is already running");
                }

                try
                {
                    _barrier.WaitFor(0L);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException(ex);
                }

                _sequence.SetValue(0L);
            }

            public ISequence GetSequence()
            {
                return _sequence;
            }

            public void Halt()
            {
                Interlocked.Exchange(ref _running, 0);
            }

            public bool IsRunning()
            {
                return _running == 1;
            }
        }

        public class ArrayFactory : IEventFactory<object[]>
        {
            private readonly int _size;
            public ArrayFactory(int size)
            {
                _size = size;
            }

            public object[] NewInstance()
            {
                return new object[_size];
            }
        }

        public class NoArgEventTranslator : IEventTranslator<object[]>
        {
            public void TranslateTo(object[] @event, long sequence)
            {
                @event[0] = sequence;
            }
        }

        public class VarArgEventTranslator : IEventTranslatorVarArg<object[]>
        {
            public void TranslateTo(object[] @event, long sequence, params object[] args)
            {
                @event[0] = args?.Aggregate((x, y) => x?.ToString() + y?.ToString())?.ToString() + "-" + sequence;

            }
        }

        public class ThreeArgEventTranslator : IEventTranslatorThreeArg<object[], string, string, string>
        {
            public void TranslateTo(object[] @event, long sequence, string arg0, string arg1, string arg2)
            {
                @event[0] = arg0 + arg1 + arg2 + "-" + sequence;
            }
        }

        public class TwoArgEventTranslator : IEventTranslatorTwoArg<object[], string, string>
        {
            public void TranslateTo(object[] @event, long sequence, string arg0, string arg1)
            {
                @event[0] = arg0 + arg1 + "-" + sequence;
            }
        }

        public class OneArgEventTranslator : IEventTranslatorOneArg<object[], string>
        {
            public void TranslateTo(object[] @event, long sequence, string arg0)
            {
                @event[0] = arg0 + "-" + sequence;
            }
        }

        private void AssertEmptyRingBuffer(RingBuffer<object[]> ringBuffer)
        {
            for (var i = 0; i < ringBuffer.GetBufferSize(); i++)
            {
                Assert.Null(ringBuffer.Get(i)[0]);
            }
        }
    }
}
