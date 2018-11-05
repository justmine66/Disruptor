using System;
using System.Threading;
using System.Threading.Tasks;
using MDA.Disruptor.Exceptions;
using MDA.Disruptor.Impl;
using MDA.Disruptor.Utility;

namespace MDA.Disruptor.Bootstrap
{
    /// <summary>
    /// A DSL-style API for setting up the disruptor pattern around a ring buffer(aka the Builder pattern).
    /// 
    /// A simple example of setting up the disruptor with two event handlers that must process events in order:
    /// <code>
    /// Disruptor{MyEvent} disruptor = new Disruptor{MyEvent}(MyEvent.FACTORY, 32, Executors.newCachedThreadPool()); 
    /// EventHandler{MyEvent} handler1 = new EventHandler{MyEvent};() { ... }; 
    /// EventHandler{MyEvent}; handler2 = new EventHandler{MyEvent};() { ... }; 
    /// disruptor.handleEventsWith(handler1); 
    /// disruptor.after(handler1).handleEventsWith(handler2); 
    /// RingBuffer ringBuffer = disruptor.start();
    /// </code>
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    public class Disruptor<T> where T : class
    {
        private readonly RingBuffer<T> _ringBuffer;
        private readonly IExecutor _executor;
        private readonly ConsumerRepository<T> _consumerRepository;

        private IExceptionHandler<T> _exceptionHandler;
        private volatile int _started;

        #region [ constructors ]

        /// <summary>
        /// Private constructor helper
        /// </summary>
        /// <param name="ringBuffer"></param>
        /// <param name="executor">an <see cref="IExecutor"/> to execute event processors.</param>
        private Disruptor(RingBuffer<T> ringBuffer, IExecutor executor)
        {
            _ringBuffer = ringBuffer;
            _executor = executor;
        }

        /// <summary>
        /// Create a new Disruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="ringBufferSize">the size of the ring buffer.</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads to for processors.</param>
        public Disruptor(IEventFactory<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler)
            : this(RingBuffer<T>.CreateMultiProducer(eventFactory, ringBufferSize), new BasicExecutor(taskScheduler))
        {
        }

        /// <summary>
        /// Create a new Disruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="ringBufferSize">the size of the ring buffer.</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads to for processors.</param>
        public Disruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler)
            : this(RingBuffer<T>.CreateMultiProducer(eventFactory, ringBufferSize), new BasicExecutor(taskScheduler))
        {
        }

        /// <summary>
        /// Create a new Disruptor.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="ringBufferSize">the size of the ring buffer.</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors.</param>
        /// <param name="producerType">the claim strategy to use for the ring buffer.</param>
        /// <param name="waitStrategy">the wait strategy to use for the ring buffer.</param>
        public Disruptor(
            IEventFactory<T> eventFactory,
            int ringBufferSize,
            TaskScheduler taskScheduler,
            ProducerType producerType,
            IWaitStrategy waitStrategy)
            : this(
            RingBuffer<T>.Create(producerType, eventFactory, ringBufferSize, waitStrategy),
            new BasicExecutor(taskScheduler))
        {
        }

        public RingBuffer<T> GetRingBuffer()
        {
            return _ringBuffer;
        }

        /// <summary>
        /// Create a new Disruptor.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="ringBufferSize">the size of the ring buffer.</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors.</param>
        /// <param name="producerType">the claim strategy to use for the ring buffer.</param>
        /// <param name="waitStrategy">the wait strategy to use for the ring buffer.</param>
        public Disruptor(
            Func<T> eventFactory,
            int ringBufferSize,
            TaskScheduler taskScheduler,
            ProducerType producerType,
            IWaitStrategy waitStrategy)
            : this(
            RingBuffer<T>.Create(producerType, eventFactory, ringBufferSize, waitStrategy),
            new BasicExecutor(taskScheduler))
        {
        }

        #endregion

        /// <summary>
        /// Set up event handlers to handle events from the ring buffer. These handlers will process events as soon as they become available, in parallel.
        /// 
        /// This method can be used as the start of a chain. For example if the handler
        /// <code>A</code> must process events before handler<code>B</code>:
        /// <code>dw.handleEventsWith(A).then(B);</code>
        /// 
        /// This call is additive, but generally should only be called once when setting up the Disruptor instance.
        /// </summary>
        /// <param name="handlers">the event handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventHandler<T>[] handlers)
        {
            return CreateEventProcessors(new ISequence[0], handlers);
        }

        /// <summary>
        /// Set up custom event processors to handle events from the ring buffer. The Disruptor will automatically start these processors when <see cref="StartAsync"/> is called.
        /// </summary>
        /// <param name="eventProcessorFactories">the event processor factories to use to create the event processors that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventProcessorFactory<T>[] eventProcessorFactories)
        {
            var barrierSequences = new Sequence[0];
            return CreateEventProcessors(barrierSequences, eventProcessorFactories);
        }

        /// <summary>
        /// Set up custom event processors to handle events from the ring buffer. The Disruptor will automatically start these processors when <see cref="StartAsync"/> is called.
        /// </summary>
        /// <param name="processors">the event processors that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventProcessor[] processors)
        {
            foreach (var processor in processors)
            {
                _consumerRepository.Add(processor);
            }

            var sequences = new ISequence[processors.Length];
            for (int i = 0; i < processors.Length; i++)
            {
                sequences[i] = processors[i].GetSequence();
            }

            _ringBuffer.AddGatingSequences(sequences);
            return new EventHandlerGroup<T>(this, _consumerRepository, SequenceGroupManager.GetSequencesFor(processors));
        }

        /// <summary>
        /// Starts the event processors and returns the fully configured ring buffer.
        /// 
        /// The ring buffer is set up to prevent overwriting any entry that is yet to be processed by the slowest event processor.
        /// 
        /// This method must only be called once after all event processors have been added.
        /// </summary>
        /// <returns>the configured ring buffer.</returns>
        public async Task<RingBuffer<T>> StartAsync()
        {
            CheckOnlyStartedOnce();

            foreach (var consumerInfo in _consumerRepository)
            {
                await consumerInfo.StartAsync(_executor);
            }

            return _ringBuffer;
        }

        public EventHandlerGroup<T> CreateEventProcessors(
            ISequence[] barrierSequences,
            IEventHandler<T>[] eventHandlers)
        {
            CheckNotStarted();

            var processorSequences = new ISequence[eventHandlers.Length];
            var barrier = _ringBuffer.NewBarrier(barrierSequences);

            for (int i = 0; i < eventHandlers.Length; i++)
            {
                var eventHandler = eventHandlers[i];
                var batchEventProcessor = new BatchEventProcessor<T>(_ringBuffer, barrier, eventHandler);

                if (_exceptionHandler != null)
                {
                    batchEventProcessor.SetExceptionHandler(_exceptionHandler);
                }

                _consumerRepository.Add(batchEventProcessor, eventHandler, barrier);
                processorSequences[i] = batchEventProcessor.GetSequence();
            }

            UpdateGatingSequencesForNextInChain(barrierSequences, processorSequences);
            return new EventHandlerGroup<T>(this, _consumerRepository, processorSequences);
        }

        public EventHandlerGroup<T> CreateWorkerPool(
        ISequence[] barrierSequences, IWorkHandler<T>[] workHandlers)
        {
            var barrier = _ringBuffer.NewBarrier(barrierSequences);
            var workerPool = new WorkerPool<T>(_ringBuffer, barrier, _exceptionHandler, workHandlers);

            _consumerRepository.Add(workerPool, barrier);
            var workerSequences = workerPool.GetWorkerSequences();

            UpdateGatingSequencesForNextInChain(barrierSequences, workerSequences);

            return new EventHandlerGroup<T>(this, _consumerRepository, workerSequences);
        }

        private void UpdateGatingSequencesForNextInChain(ISequence[] barrierSequences, ISequence[] processorSequences)
        {
            if (processorSequences.Length > 0)
            {
                _ringBuffer.AddGatingSequences(processorSequences);
                foreach (var barrierSequence in barrierSequences)
                {
                    _ringBuffer.RemoveGatingSequence(barrierSequence);
                }
                _consumerRepository.UnMarkEventProcessorsAsEndOfChain(barrierSequences);
            }
        }

        private EventHandlerGroup<T> CreateEventProcessors(
        ISequence[] barrierSequences, IEventProcessorFactory<T>[] processorFactories)
        {
            var eventProcessors = new IEventProcessor[processorFactories.Length];
            for (int i = 0; i < processorFactories.Length; i++)
            {
                eventProcessors[i] = processorFactories[i].CreateEventProcessor(_ringBuffer, barrierSequences);
            }

            return HandleEventsWith(eventProcessors);
        }

        private void CheckNotStarted()
        {
            if (_started == 1)
            {
                throw new IllegalStateException("All event handlers must be added before calling starts.");
            }
        }

        private void CheckOnlyStartedOnce()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                throw new IllegalStateException("Disruptor.start() must only be called once.");
            }
        }

        public override string ToString()
        {
            return "Disruptor{" +
            "ringBuffer=" + _ringBuffer +
            ", started=" + _started +
            ", executor=" + _executor +
            '}';
        }
    }
}
