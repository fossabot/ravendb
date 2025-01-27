﻿using JetBrains.Annotations;
using Raven.Client.ServerWide;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Documents.Handlers.Processors.DocumentsCompression
{
    internal sealed class DocumentsCompressionHandlerProcessorForGet : AbstractDocumentsCompressionHandlerProcessorForGet<DatabaseRequestHandler, DocumentsOperationContext>
    {
        public DocumentsCompressionHandlerProcessorForGet([NotNull] DatabaseRequestHandler requestHandler) : base(requestHandler)
        {
        }

        protected override DocumentsCompressionConfiguration GetDocumentsCompressionConfiguration()
        {
            using (RequestHandler.Server.ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (context.OpenReadTransaction())
            {
                DocumentsCompressionConfiguration compressionConfig;
                using (var recordRaw = RequestHandler.Server.ServerStore.Cluster.ReadRawDatabaseRecord(context, RequestHandler.Database.Name))
                {
                    compressionConfig = recordRaw?.DocumentsCompressionConfiguration;
                }

                return compressionConfig;
            }
        }
    }
}
