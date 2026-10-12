namespace Panda;

/// <param name="Source">Short name of what logged it: "Player 1", "TS 1", "Library", "TSLib"...</param>
public sealed record LogEntry(long Seq, DateTimeOffset Time, string Level, string Source, string Message);

/// <summary>
/// Keeps the latest log lines in memory for the panel's log screen (the console log is unchanged).
/// Takes everything from Information up, and TSLib's warnings through <see cref="TSLibLogging"/>.
/// </summary>
public sealed class LogBuffer : ILoggerProvider
{
	public const int Size = 2000;

	readonly LogEntry?[] ring = new LogEntry?[Size];
	readonly object gate = new();
	long next = 1;

	/// <summary>New lines arrived. Raised outside the lock.</summary>
	public event Action? Added;

	public void Add(LogLevel level, string category, string message, Exception? ex = null)
	{
		if (ex != null) message += "\n" + ex.GetType().Name + ": " + ex.Message;
		lock (gate)
		{
			var seq = next++;
			ring[seq % Size] = new LogEntry(seq, DateTimeOffset.UtcNow, LevelName(level), ShortSource(category), message);
		}
		try { Added?.Invoke(); } catch { }
	}

	/// <summary>Lines with a sequence number above <paramref name="after"/>, oldest first.</summary>
	public List<LogEntry> Since(long after)
	{
		lock (gate)
		{
			var from = Math.Max(after + 1, next - Size);
			var list = new List<LogEntry>((int)Math.Max(0, next - from));
			for (var s = from; s < next; s++)
				if (ring[s % Size] is { } e) list.Add(e);
			return list;
		}
	}

	static string LevelName(LogLevel level) => level switch
	{
		LogLevel.Critical or LogLevel.Error => "error",
		LogLevel.Warning => "warn",
		LogLevel.Information => "info",
		_ => "debug",
	};

	// "Panda.Player.2" → "Player 2", "Panda.TsBot.1" → "TS 1", "Microsoft.AspNetCore.X" → "Web".
	static string ShortSource(string category)
	{
		var parts = category.Split('.');
		if (parts[0] == "Microsoft") return "Web";
		if (category == "panda") return "Panda"; // the app's own logger is named after the program
		if (parts[0] != "Panda" || parts.Length < 2) return category;
		var name = parts[1] == "TsBot" ? "TS" : parts[1];
		return parts.Length > 2 ? $"{name} {parts[2]}" : name;
	}

	public ILogger CreateLogger(string categoryName) => new BufferLogger(this, categoryName);
	public void Dispose() { }

	sealed class BufferLogger(LogBuffer buffer, string category) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel level) => level is >= LogLevel.Information and < LogLevel.None;

		public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? ex, Func<TState, Exception?, string> formatter)
		{
			if (IsEnabled(level)) buffer.Add(level, category, formatter(state, ex), ex);
		}
	}
}
