using System.Threading;
using System.Threading.Tasks;
using MDA.Disruptor.Exceptions;
using MDA.Disruptor.Impl;
using MDA.Disruptor.Utility;

namespace MDA.Disruptor.Bootstrap
{
    /// <summary>
    /// WorkerPool contains a pool of <see cref="WorkProcessor{T}"/>s that will consume sequences so jobs can be farmed out across a pool of workers. Each of the <see cref="WorkProcessor{T}"/>s manage and calls a <see cref="IWorkHandler{TEvent}"/> to process the events.
    /// </summary>
    /// <typeparam name="T">event to be processed by a pool of workers.</typeparam>
    public class WorkerPool<T>
    {
        private volatile int _started;

        private readonly ISequence _workSequence = new Sequence();
        private readonly RingBuffer<T> _ringBuffer;
        // WorkProcessors are created to wrap each of the provided WorkHandlers.
        private readonly WorkProcessor<T>[] _workProcessors;

        /// <summary>
        /// Create a worker pool to enable an array of <see cref="IWorkHandler{TEvent}"/>s to consume published sequences.
        /// 
        /// This option requires a pre-configured <see cref="RingBuffer{TEvent}"/> which must have <see cref="RingBuffer{TEvent}.AddGatingSequences(ISequence[])"/> called before the work pool is started.
        /// </summary>
        /// <param name="ringBuffer">of events to be consumed.</param>
        /// <param name="barrier">on which the workers will depend.</param>
        /// <param name="exceptionHandler">to callback when an error occurs which is not handled by the <see cref="IWorkHandler{TEvent}"/>s.</param>
        /// <param name="workHandlers">to distribute the work load across.</param>
        public WorkerPool(
            RingBuffer<T> ringBuffer,
            ISequenceBarrier barrier,
            IExceptionHandler<T> exceptionHandler,
            params IWorkHandler<T>[] workHandlers)
        {
            _ringBuffer = ringBuffer;
            var numWorkers = workHandlers.Length;
            _workProcessors = new WorkProcessor<T>[numWorkers];

            for (int i = 0; i < numWorkers; i++)
            {
                _workProcessors[i] = new WorkProcessor<T>(
                    ringBuffer,
                    barrier,
                    workHandlers[i],
                    exceptionHandler,
                    _workSequence);
            }
        }

        /// <summary>
        /// Create a worker pool to enable an array of <see cref="IWorkHandler{TEvent}"/>s to consume published sequences.
        /// 
        /// This option requires a pre-configured <see cref="RingBuffer{TEvent}"/> which must have <see cref="RingBuffer{TEvent}.AddGatingSequences(ISequence[])"/> called before the work pool is started.
        /// </summary>
        /// <param name="eventFactory">for filling the <see cref="RingBuffer{TEvent}"/>.</param>
        /// <param name="exceptionHandler">to callback when an error occurs which is not handled by the <see cref="IWorkHandler{TEvent}"/>s.</param>
        /// <param name="workHandlers">to distribute the work load across.</param>
        public WorkerPool(
            IEventFactory<T> eventFactory,
            IExceptionHandler<T> exceptionHandler,
            params IWorkHandler<T>[] workHandlers)
        {
            _ringBuffer = RingBuffer<T>.CreateMultiProducer(eventFactory, 1024);
            var barrier = _ringBuffer.NewBarrier();
            var numWorkers = workHandlers.Length;
            _workProcessors = new WorkProcessor<T>[numWorkers];

            for (int i = 0; i < numWorkers; i++)
            {
                _workProcessors[i] = new WorkProcessor<T>(
                    _ringBuffer,
                    barrier,
                    workHandlers[i],
                    exceptionHandler,
                    _workSequence);
            }

            _ringBuffer.AddGatingSequences(GetWorkerSequences());
        }

        /// <summary>
        /// Get an array of <see cref="ISequence"/>s representing the progress of the workers.
        /// </summary>
        public ISequence[] GetWorkerSequences()
        {
            var sequences = new ISequence[_workProcessors.Length + 1];
            for (int i = 0; i < _workProcessors.Length; i++)
            {
                sequences[i] = _workProcessors[i].GetSequence();
            }
            sequences[sequences.Length - 1] = _workSequence;

            return sequences;
        }

        /// <summary>
        /// Asynchronously start the worker pool processing events in sequence.
        /// </summary>
        /// <param name="executor">providing threads for running the workers.</param>
        /// <returns>the <see cref="RingBuffer{TEvent}"/> used for the work queue.</returns>
        /// <exception cref="IllegalStateException">if the pool has already been started and not halted yet.</exception>
        public async Task<RingBuffer<T>> StartAsync(IExecutor executor)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                throw new IllegalStateException("WorkerPool has already been started and cannot be restarted until halted.");
            }

            var cursor = _ringBuffer.GetCursor();
            _workSequence.SetValue(cursor);

            foreach (var processor in _workProcessors)
            {
                processor.GetSequence().SetValue(cursor);
                await executor.ExecuteAsync(processor);
            }

            return _ringBuffer;
        }

        /// <summary>
        /// Halt all workers immediately at the end of their current cycle.
        /// </summary>
        public void Halt()
        {
            foreach (var processor in _workProcessors)
            {
                processor.Halt();
            }

            _started = 0;
        }

        /// <summary>
        /// Wait for the <see cref="RingBuffer{TEvent}"/> to drain of published events then halt the workers.
        /// </summary>
        public void DrainAndHalt()
        {
            var workerSequences = GetWorkerSequences();
            while (_ringBuffer.GetCursor() > SequenceGroupManager.GetMinimumSequence(workerSequences))
            {
                Thread.Yield();
            }

            foreach (var processor in _workProcessors)
            {
                processor.Halt();
            }

            _started = 0;
        }

        public bool IsRunning()
        {
            return _started == 1;
        }
    }
}
