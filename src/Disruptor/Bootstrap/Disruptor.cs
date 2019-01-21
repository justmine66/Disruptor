using Disruptor.Bootstrap.Impl;
using Disruptor.Exceptions;
using Disruptor.Impl;
using Disruptor.Utility;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Bootstrap
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
            _consumerRepository=new ConsumerRepository<T>();
            _exceptionHandler= new ExceptionHandlerWrapper<T>();
        }

        /// <summary>
        /// Create a new Disruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="ringBufferSize">the size of the ring buffer.</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads to for processors.</param>
        public Disruptor(IEventFactory<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler)
            : this(RingBuffer<T>.CreateMultiProducer(eventFactory, ringBufferSize), new NewSingleThreadExecutor(taskScheduler))
        {
        }

        /// <summary>
        /// Create a new Disruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="ringBufferSize">the size of the ring buffer.</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads to for processors.</param>
        public Disruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler)
            : this(RingBuffer<T>.CreateMultiProducer(eventFactory, ringBufferSize), new NewSingleThreadExecutor(taskScheduler))
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
            new NewSingleThreadExecutor(taskScheduler))
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
            new NewSingleThreadExecutor(taskScheduler))
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
            var barrierSequences = new ISequence[0];
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
            for (var i = 0; i < processors.Length; i++)
            {
                sequences[i] = processors[i].GetSequence();
            }

            _ringBuffer.AddGatingSequences(sequences);
            return new EventHandlerGroup<T>(this, _consumerRepository, SequenceGroupManager.GetSequencesFor(processors));
        }

        /// <summary>
        /// Set up a <see cref="WorkerPool{T}"/> to distribute an event to one of a pool of work handler threads. Each event will only be processed by one of the work handlers. The Disruptor will automatically start this processors when <see cref="StartAsync()"/> is called.
        /// </summary>
        /// <param name="workHandlers">the work handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWithWorkerPool(params IWorkHandler<T>[] workHandlers)
        {
            return CreateWorkerPool(new ISequence[0], workHandlers);
        }

        /// <summary>
        /// Specify an exception handler to be used for event handlers and worker pools created by this Disruptor.
        /// The exception handler will be used by existing and future event handlers and worker pools created by this Disruptor instance.
        /// </summary>
        /// <param name="exceptionHandler">the exception handler to use.</param>
        public void SetDefaultExceptionHandler(IExceptionHandler<T> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// Override the default exception handler for a specific handler.
        /// <code>disruptor.handleExceptionsIn(eventHandler).with(exceptionHandler);</code>
        /// </summary>
        /// <param name="eventHandler">the event handler to set a different exception handler for.</param>
        /// <returns>an ExceptionHandlerSetting dsl object - intended to be used by chaining the with method call.</returns>
        public ExceptionHandlerSetting<T> HandleExceptionsFor(IEventHandler<T> eventHandler)
        {
            return new ExceptionHandlerSetting<T>(eventHandler, _consumerRepository);
        }

        /// <summary>
        /// Create a group of event handlers to be used as a dependency.
        /// For example if the handler <code>A</code> must process events before handler <code>B</code>:
        /// <code>dw.After(A).HandleEventsWith(B);</code>
        /// </summary>
        /// <param name="handlers">the event handlers, previously set up with <see cref="HandleEventsWith(Disruptor.IEventHandler{T}[])"/>, that will form the barrier for subsequent handlers or processors.</param>
        /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a dependency barrier over the specified event handlers.</returns>
        public EventHandlerGroup<T> After(params IEventHandler<T>[] handlers)
        {
            var sequences = handlers.Select(it => _consumerRepository.GetSequenceFor(it));
            return new EventHandlerGroup<T>(this, _consumerRepository, sequences);
        }

        /// <summary>
        /// Create a group of event processors to be used as a dependency.
        /// </summary>
        /// <param name="processors">the event processors, previously set up with <see cref="HandleEventsWith(Disruptor.IEventProcessor[]) "/>, that will form the barrier for subsequent handlers or processors.</param>
        /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a {@link SequenceBarrier} over the specified event processors.</returns>
        public EventHandlerGroup<T> After(params IEventProcessor[] processors)
        {
            foreach (var processor in processors)
            {
                _consumerRepository.Add(processor);
            }

            return new EventHandlerGroup<T>(this, _consumerRepository, SequenceGroupManager.GetSequencesFor(processors));
        }

        /// <summary>
        /// Publish an event to the ring buffer.
        /// </summary>
        /// <param name="eventTranslator">the translator that will load data into the event.</param>
        public void PublishEvent(IEventTranslator<T> eventTranslator)
        {
            _ringBuffer.PublishEvent(eventTranslator);
        }

        /// <summary>
        /// Publish an event to the ring buffer.
        /// </summary>
        /// <typeparam name="TArg">Class of the user supplied argument.</typeparam>
        /// <param name="eventTranslator">the translator that will load data into the event.</param>
        /// <param name="arg">A single argument to load into the event.</param>
        public void PublishEvent<TArg>(IEventTranslatorOneArg<T, TArg> eventTranslator, TArg arg)
        {
            _ringBuffer.PublishEvent(eventTranslator, arg);
        }

        /// <summary>
        /// Publish a batch of events to the ring buffer.
        /// </summary>
        /// <typeparam name="TArg">Class of the user supplied argument.</typeparam>
        /// <param name="eventTranslator">the translator that will load data into the event.</param>
        /// <param name="args">An array single arguments to load into the events. One Per event.</param>
        public void PublishEvents<TArg>(IEventTranslatorOneArg<T, TArg> eventTranslator, TArg[] args)
        {
            _ringBuffer.PublishEvents(eventTranslator, args);
        }

        /// <summary>
        /// Publish an event to the ring buffer.
        /// </summary>
        /// <typeparam name="TArg0">Class of the user supplied argument.</typeparam>
        /// <typeparam name="TArg1">Class of the user supplied argument.</typeparam>
        /// <param name="eventTranslator">the translator that will load data into the event.</param>
        /// <param name="arg0">The first argument to load into the event</param>
        /// <param name="arg1">The second argument to load into the event</param>
        public void PublishEvent<TArg0, TArg1>(IEventTranslatorTwoArg<T, TArg0, TArg1> eventTranslator, TArg0 arg0,
            TArg1 arg1)
        {
            _ringBuffer.PublishEvent(eventTranslator, arg0, arg1);
        }

        /// <summary>
        /// Publish an event to the ring buffer.
        /// </summary>
        /// <typeparam name="TArg0">Class of the user supplied argument.</typeparam>
        /// <typeparam name="TArg1">Class of the user supplied argument.</typeparam>
        /// <typeparam name="TArg2">Class of the user supplied argument.</typeparam>
        /// <param name="eventTranslator">the translator that will load data into the event.</param>
        /// <param name="arg0">The first argument to load into the event</param>
        /// <param name="arg1">The second argument to load into the event</param>
        /// <param name="arg2">The third argument to load into the event</param>
        public void PublishEvent<TArg0, TArg1, TArg2>(IEventTranslatorThreeArg<T, TArg0, TArg1, TArg2> eventTranslator, TArg0 arg0,
            TArg1 arg1, TArg2 arg2)
        {
            _ringBuffer.PublishEvent(eventTranslator, arg0, arg1, arg2);
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

        /// <summary>
        /// Calls <see cref="IEventProcessor.Halt()"/> on all of the event processors created via this disruptor.
        /// </summary>
        public void Halt()
        {
            foreach (var consumer in _consumerRepository)
            {
                consumer.Halt();
            }
        }

        /// <summary>
        /// Waits until all events currently in the disruptor have been processed by all event processors and then halts the processors. It is critical that publishing to the ring buffer has stopped before calling this method, otherwise it may never return.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                Shutdown(Timeout.InfiniteTimeSpan);
            }
            catch (TimeoutException e)
            {
                _exceptionHandler.HandleOnShutdownException(e);
            }
        }

        /// <summary>
        /// Waits until all events currently in the disruptor have been processed by all event processors and then halts the processors.
        /// </summary>
        /// <remarks>
        /// This method will not shutdown the executor, nor will it await the final termination of the processor threads.
        /// </remarks>
        /// <param name="timeout">the amount of time to wait for all events to be processed. <code>-1</code> will give an infinite timeout</param>
        public void Shutdown(TimeSpan timeout)
        {
            var timeOutAt = DateTime.UtcNow.Add(timeout);
            while (HasBacklog())
            {
                if (timeOutAt.Ticks > 0 && DateTime.UtcNow > timeOutAt)
                {
                    throw new TimeoutException();
                }
            }

            Halt();
        }

        /// <summary>
        /// The <see cref="RingBuffer{T}"/> used by this Disruptor. This is useful for creating custom
        /// event processors if the behaviour of <see cref="BatchEventProcessor{T}"/> is not suitable.
        /// </summary>
        public RingBuffer<T> RingBuffer => _ringBuffer;

        /// <summary>
        /// Get the value of the cursor indicating the published sequence.
        /// </summary>
        public long Cursor => _ringBuffer.GetCursor();

        /// <summary>
        /// The capacity of the data structure to hold entries.
        /// </summary>
        public long BufferSize => _ringBuffer.GetBufferSize();

        /// <summary>
        /// Get the event for a given sequence in the RingBuffer.
        /// </summary>
        /// <param name="sequence">for the event.</param>
        public T this[long sequence] => _ringBuffer.Get(sequence);

        /// <summary>
        /// Get the <see cref="ISequenceBarrier"/> used by a specific handler. Note that the <see cref="ISequenceBarrier"/> may be shared by multiple event handlers.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ISequenceBarrier GetBarrierFor(IEventHandler<T> handler)
        {
            return _consumerRepository.GetBarrierFor(handler);
        }

        /// <summary>
        /// Gets the sequence value for the specified event handlers.
        /// </summary>
        /// <param name="handler">to get the sequence for.</param>
        public long GetSequenceValueFor(IEventHandler<T> handler)
        {
            return _consumerRepository.GetSequenceFor(handler).GetValue();
        }

        public EventHandlerGroup<T> CreateEventProcessors(
            ISequence[] barrierSequences,
            IEventHandler<T>[] eventHandlers)
        {
            CheckNotStarted();

            var processorSequences = new ISequence[eventHandlers.Length];
            var barrier = _ringBuffer.NewBarrier(barrierSequences);

            for (var i = 0; i < eventHandlers.Length; i++)
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

        public EventHandlerGroup<T> CreateEventProcessors(
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
                throw new IllegalStateException($"{nameof(StartAsync)} must only be called once.");
            }
        }

        private void UpdateGatingSequencesForNextInChain(ISequence[] barrierSequences, ISequence[] processorSequences)
        {
            if (processorSequences.Length <= 0) return;

            _ringBuffer.AddGatingSequences(processorSequences);
            foreach (var barrierSequence in barrierSequences)
            {
                _ringBuffer.RemoveGatingSequence(barrierSequence);
            }
            _consumerRepository.UnMarkEventProcessorsAsEndOfChain(barrierSequences);
        }

        public override string ToString()
        {
            return "Disruptor{" +
            "ringBuffer=" + _ringBuffer +
            ", started=" + _started +
            ", executor=" + _executor +
            '}';
        }

        /// <summary>
        /// Confirms if all messages have been consumed by all event processors.
        /// </summary>
        private bool HasBacklog()
        {
            var cursor = _ringBuffer.GetCursor();
            foreach (var sequence in _consumerRepository.GetLastSequenceInChain(false))
            {
                if (cursor > sequence.GetValue())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
