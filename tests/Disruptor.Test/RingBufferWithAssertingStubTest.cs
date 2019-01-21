using Disruptor.Impl;
using Disruptor.Test.Support;
using System;
using Xunit;

namespace Disruptor.Test
{
    public class RingBufferWithAssertingStubTest
    {
        private readonly RingBuffer<StubEvent> _ringBuffer;
        private readonly ISequencer _sequencer;

        public RingBufferWithAssertingStubTest()
        {
            _sequencer = new AssertingSequencer(16);
            _ringBuffer = new RingBuffer<StubEvent>(_sequencer, StubEvent.EventFactory);
        }

        [Fact]
        public void ShouldDelegateNextAndPublish()
        {
            _ringBuffer.Publish(_ringBuffer.Next());
        }

        [Fact]
        public void ShouldDelegateTryNextAndPublish()
        {
            if (_ringBuffer.TryNext(out var sequence))
            {
                _ringBuffer.Publish(sequence);
            }
        }

        [Fact]
        public void ShouldDelegateNextNAndPublish()
        {
            var hi = _ringBuffer.Next(10);
            _ringBuffer.Publish(hi - 9, hi);
        }

        [Fact]
        public void ShouldDelegateTryNextNAndPublish()
        {
            _ringBuffer.TryNext(10,out var hi);
            _ringBuffer.Publish(hi - 9, hi);
        }

        private class AssertingSequencer : ISequencer
        {
            private readonly int _size;
            private long _lastBatchSize = -1;
            private long _lastValue = -1;

            public AssertingSequencer(int size)
            {
                _size = size;
            }

            public long GetCursor()
            {
                throw new System.NotImplementedException();
            }

            public int GetBufferSize()
            {
                return _size;
            }

            public bool HasAvailableCapacity(int requiredCapacity)
            {
                return requiredCapacity < _size;
            }

            public long GetRemainingCapacity()
            {
                return _size;
            }

            public long Next()
            {
                _lastValue = new Random().Next(0, 1000000);
                _lastBatchSize = 1;
                return _lastValue;
            }

            public long Next(int requiredCapacity)
            {
                _lastValue = new Random().Next(requiredCapacity, 1000000);
                _lastBatchSize = requiredCapacity;
                return _lastValue;
            }

            public bool TryNext(out long sequence)
            {
                sequence = Next();
                return true;
            }

            public bool TryNext(int requiredCapacity, out long sequence)
            {
                sequence = Next(requiredCapacity);
                return true;
            }

            public void Publish(long sequence)
            {
                Assert.Equal(sequence, _lastValue);
                Assert.Equal(1, _lastBatchSize);
            }

            public void Publish(long lo, long hi)
            {
                Assert.Equal(hi, _lastValue);
                Assert.Equal((hi - lo) + 1, _lastBatchSize);
            }

            public void Claim(long sequence)
            {

            }

            public bool IsAvailable(long sequence)
            {
                return false;
            }

            public void AddGatingSequences(params ISequence[] gatingSequences)
            {

            }

            public bool RemoveGatingSequence(ISequence sequence)
            {
                return false;
            }

            public ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack)
            {
                return null;
            }

            public long GetMinimumSequence()
            {
                return 0;
            }

            public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
            {
                return 0;
            }

            public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params ISequence[] gatingSequences)
            {
                return null;
            }
        }
    }
}
