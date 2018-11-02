using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MDA.Disruptor.DSL
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
