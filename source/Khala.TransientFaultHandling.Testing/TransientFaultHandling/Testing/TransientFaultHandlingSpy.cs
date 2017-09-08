﻿namespace Khala.TransientFaultHandling.Testing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class TransientFaultHandlingSpy
    {
        private static readonly Random _random = new Random();

        private readonly Func<CancellationToken, Task> _callback;
        private readonly int _maximumRetryCount;
        private readonly int _transientFaultCount;
        private int _invocationCount;

        public TransientFaultHandlingSpy(Func<CancellationToken, Task> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _maximumRetryCount = _random.Next(1000, 2000);
            _transientFaultCount = _random.Next(0, _maximumRetryCount);
            _invocationCount = 0;

            Policy = new RetryPolicy(
                _maximumRetryCount,
                new TransientFaultDetectionStrategy(),
                new ConstantRetryIntervalStrategy(TimeSpan.Zero, immediateFirstRetry: true));

            OperationNonCancellable = Operation;
            OperationCancellable = Operation;
        }

        public TransientFaultHandlingSpy()
            : this(cancellationToken => Task.FromResult(true))
        {
        }

        public RetryPolicy Policy { get; }

        public Func<Task> OperationNonCancellable { get; }

        public Func<CancellationToken, Task> OperationCancellable { get; }

        public void Verify()
        {
            if (_invocationCount == _transientFaultCount + 1)
            {
                return;
            }

            throw new InvalidOperationException("It seems that operation did not invoked by retry policy.");
        }

        private Task Operation() => Operation(CancellationToken.None);

        private async Task Operation(CancellationToken cancellationToken)
        {
            _invocationCount++;

            try
            {
                await _callback.Invoke(cancellationToken);
            }
            catch
            {
            }

            if (_invocationCount == _transientFaultCount + 1)
            {
                return;
            }

            throw new InvalidOperationException("Transient fault occured. Try more please.");
        }
    }
}
