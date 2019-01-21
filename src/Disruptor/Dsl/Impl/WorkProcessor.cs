using Disruptor.Exceptions;
using Disruptor.Impl;
using System;
using System.Threading;

namespace Disruptor.Dsl.Impl
{
    /// <summary>
    /// A <see cref="WorkProcessor{T}"/> wraps a single <see cref="IWorkHandler{TEvent}"/>, effectively consuming the sequence and ensuring appropriate barriers.
    /// 
    /// Generally, this will be used as part of a <see cref="WorkerPool{T}"/>.
    /// </summary>
    /// <typeparam name="T">event implementation storing the details for the work to processed.</typeparam>
    public class WorkProcessor<T> : IEventProcessor
    {
        private volatile int _running;
        private readonly ISequence _sequence;
        private readonly RingBuffer<T> _ringBuffer;
        private readonly ISequenceBarrier _barrier;
        private readonly IWorkHandler<T> _workHandler;
        private readonly IExceptionHandler<T> _exceptionHandler;
        private readonly ISequence _workSequence;
        private readonly ITimeoutHandler _timeoutHandler;
        private readonly ILifecycleAware _lifecycleAware;
        private readonly IEventReleaser _eventReleaser;

        /// <summary>
        /// Construct a <see cref="WorkProcessor{T}"/>.
        /// </summary>
        /// <param name="ringBuffer">to which events are published.</param>
        /// <param name="barrier">on which it is waiting.</param>
        /// <param name="workHandler">is the delegate to which events are dispatched.</param>
        /// <param name="exceptionHandler">to be called back when an error occurs.</param>
        /// <param name="workSequence">from which to claim the next event to be worked on. It should always be initialised <see cref="Sequence.InitialValue"/></param>
        public WorkProcessor(
          RingBuffer<T> ringBuffer,
          ISequenceBarrier barrier,
          IWorkHandler<T> workHandler,
          IExceptionHandler<T> exceptionHandler,
          ISequence workSequence)
        {
            _ringBuffer = ringBuffer;
            _barrier = barrier;
            _workHandler = workHandler;
            _exceptionHandler = exceptionHandler;
            _workSequence = workSequence;
            _sequence = new Sequence();

            if (_workHandler is ITimeoutHandler timeoutHandler)
            {
                _timeoutHandler = timeoutHandler;
            }

            if (_workHandler is ILifecycleAware lifecycleAware)
            {
                _lifecycleAware = lifecycleAware;
            }

            if (_workHandler is IEventReleaseAware eventReleaseAware)
            {
                _eventReleaser = new EventReleaser(_sequence);
                eventReleaseAware.SetEventReleaser(_eventReleaser);
            }
        }

        public ISequence GetSequence()
        {
            return _sequence;
        }

        public void Halt()
        {
            Interlocked.Exchange(ref _running, 0);
            _barrier.Alert();
        }

        public bool IsRunning()
        {
            return _running == 1;
        }

        /// <summary>
        /// It is ok to have another thread re-run this method after a halt().
        /// </summary>
        /// <exception cref="IllegalStateException">if this processor is already running.</exception>
        public void Run()
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) == 1)
            {
                throw new IllegalStateException("Thread is already running");
            }

            _barrier.ClearAlert();

            NotifyStart();

            var processedSequence = true;
            var cachedAvailableSequence = long.MinValue;
            var nextSequence = _sequence.GetValue();
            var @event = default(T);

            while (true)
            {
                try
                {
                    // if previous sequence was processed - fetch the next sequence and set
                    // that we have successfully processed the previous sequence
                    // typically, this will be true
                    // this prevents the sequence getting too far forward if an exception
                    // is thrown from the WorkHandler
                    if (processedSequence)
                    {
                        processedSequence = false;
                        do
                        {
                            nextSequence = _workSequence.GetValue() + 1L;
                            _sequence.SetValue(nextSequence - 1L);
                        } while (!_workSequence.CompareAndSet(nextSequence - 1L, nextSequence));
                    }

                    if (cachedAvailableSequence >= nextSequence)
                    {
                        @event = _ringBuffer.Get(nextSequence);
                        _workHandler.OnEvent(@event);
                        processedSequence = true;
                    }
                    else
                    {
                        cachedAvailableSequence = _barrier.WaitFor(nextSequence);
                    }
                }
                catch (TimeoutException)
                {
                    NotifyTimeout(_sequence.GetValue());
                }
                catch (AlertException)
                {
                    if (_running == 0)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    _exceptionHandler.HandleEventException(e, nextSequence, @event);
                }
            }

            NotifyShutdown();
            _running = 0;
        }

        private void NotifyTimeout(long availableSequence)
        {
            try
            {
                _timeoutHandler?.OnTimeout(availableSequence);
            }
            catch (Exception e)
            {
                _exceptionHandler.HandleEventException(e, availableSequence, default(T));
            }
        }
        private void NotifyStart()
        {
            try
            {
                _lifecycleAware?.OnStart();
            }
            catch (Exception e)
            {
                _exceptionHandler.HandleOnStartException(e);
            }
        }
        private void NotifyShutdown()
        {
            try
            {
                _lifecycleAware?.OnShutdown();
            }
            catch (Exception e)
            {
                _exceptionHandler.HandleOnShutdownException(e);
            }
        }
        private class EventReleaser : IEventReleaser
        {
            private readonly ISequence _sequence;

            public EventReleaser(ISequence sequence)
            {
                _sequence = sequence;
            }

            public void Release()
            {
                _sequence.SetValue(long.MaxValue);
            }
        }
    }
}
