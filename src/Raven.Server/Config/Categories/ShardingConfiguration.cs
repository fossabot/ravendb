using System;
using System.ComponentModel;
using System.IO.Compression;
using System.Threading;
using Raven.Server.Config.Attributes;
using Raven.Server.Config.Settings;

namespace Raven.Server.Config.Categories
{
    [ConfigurationCategory(ConfigurationCategoryType.Sharding)]
    public class ShardingConfiguration : ConfigurationCategory
    {
        public ShardingConfiguration()
        {
            OrchestratorTimeoutInMinutes = new TimeSetting(Timeout.InfiniteTimeSpan);
        }

        [Description("The compression level to use when sending import streams to shards during smuggler import")]
        [DefaultValue(CompressionLevel.NoCompression)]
        [ConfigurationEntry("Sharding.Import.CompressionLevel", ConfigurationEntryScope.ServerWideOrPerDatabase)]
        public CompressionLevel CompressionLevel { get; set; }

        [Description("The compression to use when distributing requests from the orchestrator to the shards")]
        [DefaultValue(false)]
        [ConfigurationEntry("Sharding.ShardExecutorUseCompression", ConfigurationEntryScope.ServerWideOnly)]
        public bool ShardExecutorUseCompression { get; set; }

        [Description("Enable the timeout of the orchestrator's requests to the shards")]
        [DefaultValue(DefaultValueSetInConstructor)]
        [TimeUnit(TimeUnit.Minutes)]
        [ConfigurationEntry("Sharding.OrchestratorTimeoutInMinutes", ConfigurationEntryScope.ServerWideOnly)]
        public TimeSetting OrchestratorTimeoutInMinutes { get; set; }

        [DefaultValue(10 * 60)]
        [TimeUnit(TimeUnit.Seconds)]
        [ConfigurationEntry("Sharding.PeriodicDocumentsMigrationIntervalInSec", ConfigurationEntryScope.ServerWideOrPerDatabase)]
        [Description("Time (in seconds) between periodic documents migration.")]
        public TimeSetting PeriodicDocumentsMigrationInterval { get; set; }
    }
}
