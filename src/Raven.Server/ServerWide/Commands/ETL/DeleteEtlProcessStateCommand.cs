﻿using Raven.Client.Documents.Operations.ETL;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.ServerWide.Commands.ETL
{
    public sealed class RemoveEtlProcessStateCommand : UpdateValueForDatabaseCommand
    {
        public string ConfigurationName { get; set; }

        public string TransformationName { get; set; }

        public RemoveEtlProcessStateCommand()
        {
            // for deserialization
        }

        public RemoveEtlProcessStateCommand(string databaseName, string configurationName, string transformationName, string uniqueRequestId) : base(databaseName, uniqueRequestId)
        {
            ConfigurationName = configurationName;
            TransformationName = transformationName;
        }

        public override string GetItemId()
        {
            return EtlProcessState.GenerateItemName(DatabaseName, ConfigurationName, TransformationName);
        }

        protected override UpdatedValue GetUpdatedValue(long index, RawDatabaseRecord record, ClusterOperationContext context,
            BlittableJsonReaderObject existingValue)
        {
            return new UpdatedValue(UpdatedValueActionType.Delete, value: null);
        }

        public override void FillJson(DynamicJsonValue json)
        {
            json[nameof(ConfigurationName)] = ConfigurationName;
            json[nameof(TransformationName)] = TransformationName;
        }
    }
}
