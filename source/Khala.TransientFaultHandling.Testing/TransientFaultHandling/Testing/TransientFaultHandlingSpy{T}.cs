﻿namespace Khala.TransientFaultHandling.Testing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class TransientFaultHandlingSpy<T>
    {
        private static readonly Random _random = new Random();

        private readonly Func<CancellationToken, Task<T>> _callback;
        private readonly int _maximumRetryCount;
        private readonly int _transientFaultCount;
        private int _intercepted;
        private int _invocations;

        public TransientFaultHandlingSpy()
            : this(cancellationToken => Task.FromResult(default(T)))
        {
        }

        public TransientFaultHandlingSpy(Func<CancellationToken, Task<T>> callback)
        {
            _callback = callback;
            _maximumRetryCount = _random.Next(1000, 2000);
            _transientFaultCount = _random.Next(0, _maximumRetryCount);
            _intercepted = 0;
            _invocations = 0;

            Policy = new SpyRetryPolicy(
                _maximumRetryCount,
                new TransientFaultDetectionStrategy<T>(),
                new ConstantRetryIntervalStrategy(TimeSpan.Zero, immediateFirstRetry: true),
                Interceptor);

            OperationNonCancellable = Operation;
            OperationCancellable = Operation;
        }

        public RetryPolicy<T> Policy { get; }

        public Func<Task<T>> OperationNonCancellable { get; }

        public Func<CancellationToken, Task<T>> OperationCancellable { get; }

        public void Verify()
        {
            if (_invocations == _transientFaultCount + 1 &&
                _invocations == _intercepted)
            {
                return;
            }

            throw new InvalidOperationException("It seems that the operation did not invoked by retry policy or invoked directly.");
        }

        private Func<CancellationToken, Task<T>> Interceptor(Func<CancellationToken, Task<T>> operation)
        {
            async Task<T> Intercept(CancellationToken cancellationToken)
            {
                _intercepted++;

                T result = await operation.Invoke(cancellationToken);

                if (_invocations == _transientFaultCount + 1)
                {
                    return result;
                }

                throw new InvalidOperationException("Transient fault occured. Try more please.");
            }

            return Intercept;
        }

        private Task<T> Operation() => Operation(CancellationToken.None);

        private async Task<T> Operation(CancellationToken cancellationToken)
        {
            _invocations++;

            try
            {
                return await _callback.Invoke(cancellationToken);
            }
            catch
            {
                return default(T);
            }
        }

        private class SpyRetryPolicy : RetryPolicy<T>
        {
            private readonly Func<Func<CancellationToken, Task<T>>, Func<CancellationToken, Task<T>>> _interceptor;

            public SpyRetryPolicy(
                int maximumRetryCount,
                TransientFaultDetectionStrategy<T> transientFaultDetectionStrategy,
                RetryIntervalStrategy retryIntervalStrategy,
                Func<Func<CancellationToken, Task<T>>, Func<CancellationToken, Task<T>>> interceptor)
                : base(maximumRetryCount, transientFaultDetectionStrategy, retryIntervalStrategy)
            {
                _interceptor = interceptor;
            }

            public override Task<T> Run(
                Func<CancellationToken, Task<T>> operation,
                CancellationToken cancellationToken)
            {
                return base.Run(_interceptor.Invoke(operation), cancellationToken);
            }
        }
    }
}
