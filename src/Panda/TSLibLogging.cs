using NLog.Config;
using NLog.Targets;

namespace Panda;

/// <summary>TSLib logs through NLog; only show its warnings and errors (console and panel log).</summary>
static class TSLibLogging
{
	public static void Configure(LogBuffer buffer)
	{
		var cfg = new LoggingConfiguration();
		cfg.AddRule(NLog.LogLevel.Warn, NLog.LogLevel.Fatal,
			new ConsoleTarget("tslib") { Layout = "${date:format=HH\\:mm\\:ss} ${level:lowercase=true}: TSLib: ${message}" });
		cfg.AddRule(NLog.LogLevel.Warn, NLog.LogLevel.Fatal, new BufferTarget(buffer));
		NLog.LogManager.Configuration = cfg;
	}

	sealed class BufferTarget(LogBuffer buffer) : Target
	{
		protected override void Write(NLog.LogEventInfo e) =>
			buffer.Add(e.Level >= NLog.LogLevel.Error ? LogLevel.Error : LogLevel.Warning, "TSLib", e.FormattedMessage, e.Exception);
	}
}
