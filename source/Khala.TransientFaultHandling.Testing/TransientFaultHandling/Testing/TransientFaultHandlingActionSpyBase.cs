﻿namespace Khala.TransientFaultHandling.Testing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class TransientFaultHandlingActionSpyBase
    {
        private static readonly Random _random = new Random();

        private readonly int _maximumRetryCount;
        private readonly int _transientFaultCount;
        private int _intercepted;
        private int _invocations;

        internal TransientFaultHandlingActionSpyBase()
        {
            _maximumRetryCount = _random.Next(1000, 2000);
            _transientFaultCount = _random.Next(0, _maximumRetryCount);
            _intercepted = 0;
            _invocations = 0;

            Policy = new SpyRetryPolicy(
                _maximumRetryCount,
                new TransientFaultDetectionStrategy(),
                new ConstantRetryIntervalStrategy(TimeSpan.Zero, immediateFirstRetry: true),
                Interceptor);
        }

        public RetryPolicy Policy { get; }

        public void Verify()
        {
            if (_invocations == _transientFaultCount + 1 &&
                _invocations == _intercepted)
            {
                return;
            }

            throw new InvalidOperationException("It seems that the operation did not invoked by retry policy or invoked directly.");
        }

        internal void OnInvoked() => _invocations++;

        private Func<CancellationToken, Task> Interceptor(Func<CancellationToken, Task> operation)
        {
            async Task Intercept(CancellationToken cancellationToken)
            {
                _intercepted++;

                await operation.Invoke(cancellationToken);

                if (_invocations == _transientFaultCount + 1)
                {
                    return;
                }

                throw new InvalidOperationException("Transient fault occured. Try more please.");
            }

            return Intercept;
        }

        private class SpyRetryPolicy : RetryPolicy
        {
            private readonly Func<Func<CancellationToken, Task>, Func<CancellationToken, Task>> _interceptor;

            public SpyRetryPolicy(
                int maximumRetryCount,
                TransientFaultDetectionStrategy transientFaultDetectionStrategy,
                RetryIntervalStrategy retryIntervalStrategy,
                Func<Func<CancellationToken, Task>, Func<CancellationToken, Task>> interceptor)
                : base(maximumRetryCount, transientFaultDetectionStrategy, retryIntervalStrategy)
            {
                _interceptor = interceptor;
            }

            public override Task Run(
                Func<CancellationToken, Task> operation,
                CancellationToken cancellationToken)
            {
                return base.Run(_interceptor.Invoke(operation), cancellationToken);
            }
        }
    }
}