using System.Threading.Tasks;

namespace MDA.Disruptor.Bootstrap.Impl
{
    public class WorkerPoolInfo<T> : IConsumerInfo where T : class
    {
        private readonly WorkerPool<T> _workerPool;
        private readonly ISequenceBarrier _sequenceBarrier;

        private bool _endOfChain = true;

        public WorkerPoolInfo(WorkerPool<T> workerPool, ISequenceBarrier sequenceBarrier)
        {
            _workerPool = workerPool;
            _sequenceBarrier = sequenceBarrier;
        }

        public ISequenceBarrier GetBarrier()
        {
            return _sequenceBarrier;
        }

        public ISequence[] GetSequences()
        {
            return _workerPool.GetWorkerSequences();
        }

        public void Halt()
        {
            _workerPool.Halt();
        }

        public bool IsEndOfChain()
        {
            return _endOfChain;
        }

        public bool IsRunning()
        {
            return _workerPool.IsRunning();
        }

        public void MarkAsUsedInBarrier()
        {
            _endOfChain = false;
        }

        public async Task StartAsync(IExecutor executor)
        {
            await _workerPool.StartAsync(executor);
        }
    }
}