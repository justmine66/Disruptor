using Disruptor.Dsl.Impl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Disruptor.Dsl
{
    public class ConsumerRepository<T> : IEnumerable<IConsumerInfo> where T : class
    {
        private readonly Dictionary<IEventHandler<T>, EventProcessorInfo<T>> _eventProcessorInfoByEventHandler;
        private readonly Dictionary<ISequence, IConsumerInfo> _eventProcessorInfoBySequence;
        private readonly IList<IConsumerInfo> _consumerInfos;

        public ConsumerRepository()
        {
            _eventProcessorInfoByEventHandler = new Dictionary<IEventHandler<T>, EventProcessorInfo<T>>(new IdentityComparer<IEventHandler<T>>());
            _eventProcessorInfoBySequence = new Dictionary<ISequence, IConsumerInfo>(new IdentityComparer<ISequence>());
            _consumerInfos = new List<IConsumerInfo>();
        }

        public IEnumerator<IConsumerInfo> GetEnumerator()
        {
            return _consumerInfos.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(
            IEventProcessor processor,
            IEventHandler<T> handler,
            ISequenceBarrier barrier)
        {
            var consumerInfo = new EventProcessorInfo<T>(processor, handler, barrier);
            _eventProcessorInfoByEventHandler[handler] = consumerInfo;
            _eventProcessorInfoBySequence[processor.GetSequence()] = consumerInfo;
            _consumerInfos.Add(consumerInfo);
        }

        public void Add(IEventProcessor processor)
        {
            var consumerInfo = new EventProcessorInfo<T>(processor, null, null);
            _eventProcessorInfoBySequence[processor.GetSequence()] = consumerInfo;
            _consumerInfos.Add(consumerInfo);
        }

        public void Add(WorkerPool<T> workerPool, ISequenceBarrier barrier)
        {
            var workerPoolInfo = new WorkerPoolInfo<T>(workerPool, barrier);
            _consumerInfos.Add(workerPoolInfo);
            foreach (var sequence in workerPool.GetWorkerSequences())
            {
                _eventProcessorInfoBySequence[sequence] = workerPoolInfo;
            }
        }

        public ISequence[] GetLastSequenceInChain(bool includeStopped)
        {
            var lastSequence = new List<ISequence>();
            foreach (var consumerInfo in _consumerInfos)
            {
                if ((includeStopped || consumerInfo.IsRunning()) && consumerInfo.IsEndOfChain())
                {
                    var sequences = consumerInfo.GetSequences();
                    lastSequence.AddRange(sequences);
                }
            }

            return lastSequence.ToArray();
        }

        public IEventProcessor GetEventProcessorFor(IEventHandler<T> handler)
        {
            var eventProcessorInfo = GetEventProcessorInfo(handler);
            if (eventProcessorInfo == null)
            {
                throw new ArgumentNullException("The event handler " + handler + " is not processing events.");
            }

            return eventProcessorInfo.GetEventProcessor();
        }

        public ISequence GetSequenceFor(IEventHandler<T> handler)
        {
            return GetEventProcessorFor(handler).GetSequence();
        }

        public void UnMarkEventProcessorsAsEndOfChain(params ISequence[] sequences)
        {
            foreach (var sequence in sequences)
            {
                GetEventProcessorInfo(sequence).MarkAsUsedInBarrier();
            }
        }

        public ISequenceBarrier GetBarrierFor(IEventHandler<T> handler)
        {
            var consumerInfo = GetEventProcessorInfo(handler);
            return consumerInfo?.GetBarrier();
        }

        private EventProcessorInfo<T> GetEventProcessorInfo(IEventHandler<T> handler)
        {
            return _eventProcessorInfoByEventHandler.TryGetValue(handler, out var value) ? value : default(EventProcessorInfo<T>);
        }

        private IConsumerInfo GetEventProcessorInfo(ISequence barrierEventProcessor)
        {
            return _eventProcessorInfoBySequence[barrierEventProcessor];
        }

        private class IdentityComparer<TKey> : IEqualityComparer<TKey>
        {
            public bool Equals(TKey x, TKey y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(TKey obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
