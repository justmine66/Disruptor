using Disruptor.Impl;
using System;
using System.Linq;

namespace Disruptor.Test.Support
{
    public class RingBufferEventMatcher
    {
        private readonly RingBuffer<object[]> _ringBuffer;

        public RingBufferEventMatcher(RingBuffer<object[]> ringBuffer)
        {
            _ringBuffer = ringBuffer;
        }

        public bool RingBufferWithEvents(params dynamic[] events)
        {
            var len = events.Length;
            var actualValues = new dynamic[len];
            var expectedValues = new dynamic[len];

            for (var i = 0; i < len; i++)
            {
                actualValues[i] = events[i][0];
                expectedValues[i] = _ringBuffer.Get(i)[0];
            }

            return expectedValues.SequenceEqual(actualValues);
        }
    }
}
