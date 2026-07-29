namespace ZktecoRelay.Hosting;

internal sealed class CallbackLoggerProvider : ILoggerProvider
{
    private readonly Action<string> _write;

    public CallbackLoggerProvider(Action<string> write)
    {
        _write = write;
    }

    public ILogger CreateLogger(string categoryName) =>
        new CallbackLogger(categoryName, _write);

    public void Dispose()
    {
    }

    private sealed class CallbackLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Action<string> _write;

        public CallbackLogger(
            string categoryName,
            Action<string> write)
        {
            _categoryName = categoryName;
            _write = write;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information &&
            (_categoryName.StartsWith(
                    "ZktecoRelay.",
                    StringComparison.Ordinal) ||
                string.Equals(
                    _categoryName,
                    "Microsoft.Hosting.Lifetime",
                    StringComparison.Ordinal));

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var category = _categoryName.Split('.').LastOrDefault()
                ?? _categoryName;
            var line = $"{logLevel.ToString().ToUpperInvariant(),-11} " +
                $"[{category}] {message}";
            if (exception is not null)
            {
                line += $" · {exception.GetType().Name}: {exception.Message}";
            }

            try
            {
                _write(line);
            }
            catch
            {
                // GUI logging must never alter Relay execution.
            }
        }
    }
}
