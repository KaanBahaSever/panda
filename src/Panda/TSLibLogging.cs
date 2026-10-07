using NLog.Config;
using NLog.Targets;

namespace Panda;

/// <summary>TSLib logs through NLog; only show its warnings and errors.</summary>
static class TSLibLogging
{
	public static void Configure()
	{
		var cfg = new LoggingConfiguration();
		cfg.AddRule(NLog.LogLevel.Warn, NLog.LogLevel.Fatal,
			new ConsoleTarget("tslib") { Layout = "${date:format=HH\\:mm\\:ss} ${level:lowercase=true}: TSLib: ${message}" });
		NLog.LogManager.Configuration = cfg;
	}
}
