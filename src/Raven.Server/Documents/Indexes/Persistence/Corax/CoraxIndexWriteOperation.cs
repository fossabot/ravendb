﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Corax;
using Raven.Client.Documents.Indexes;
using Raven.Server.Exceptions;
using Raven.Server.Utils;
using Sparrow.Json;
using Sparrow.Logging;
using Voron;
using Voron.Impl;

namespace Raven.Server.Documents.Indexes.Persistence.Corax
{
    public class CoraxIndexWriteOperation : IndexWriteOperationBase
    {
        private readonly IndexWriter _indexWriter;
        private CoraxDocumentConverter _converter;
        private readonly Dictionary<Slice, int> _knownFields;
        private int _entriesCount = 0;
        private readonly IDisposable _releaseWriteTransaction;

        public CoraxIndexWriteOperation(Index index, Transaction writeTransaction, CoraxDocumentConverter converter, Logger logger) : base(index, logger)
        {
            _converter = converter;
            _knownFields = _converter.GetKnownFields();
            _releaseWriteTransaction = writeTransaction;
            try
            { 
                _indexWriter = new IndexWriter(writeTransaction);
            }
            catch (Exception e) when (e.IsOutOfMemory())
            {
                throw;
            }
            catch (Exception e)
            {
                throw new IndexWriteException(e);
            }
        }

        public override void Dispose()
        {
            _indexWriter?.Dispose();
        }

        public override void Commit(IndexingStatsScope stats)
        {
            if (_indexWriter != null)
            {
                using (var commitStats = stats.For(IndexingOperation.Corax.Commit))
                {
                    _indexWriter.Commit();
                }
            }
        }

        public override void Optimize()
        {
            //Lucene method, not used (for now?) in Corax.
            //throw new NotImplementedException();
        }

        public override void IndexDocument(LazyStringValue key, LazyStringValue sourceDocumentId, object document, IndexingStatsScope stats, JsonOperationContext indexContext)
        {
            EnsureValidStats(stats);
            _entriesCount++;
            Span<byte> data;
            string id;

            using (Stats.ConvertStats.Start())
                data = _converter.GetFields(key, sourceDocumentId, document, indexContext, out id);

            _indexWriter.Index(id, data, _knownFields);
            stats.RecordIndexingOutput();

        }

        public override int EntriesCount() => _entriesCount;

        public override (long RamSizeInBytes, long FilesAllocationsInBytes) GetAllocations()
        {
            //tududu
            return (1024 * 1024, 1024 * 1024);
            //throw new NotImplementedException();
        }

        public override void Delete(LazyStringValue key, IndexingStatsScope stats)
        {
            throw new NotImplementedException();
        }

        public override void DeleteBySourceDocument(LazyStringValue sourceDocumentId, IndexingStatsScope stats)
        {
            throw new NotImplementedException();
        }

        public override void DeleteReduceResult(LazyStringValue reduceKeyHash, IndexingStatsScope stats)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureValidStats(IndexingStatsScope stats)
        {
            if (_statsInstance == stats)
                return;

            _statsInstance = stats;

            Stats.DeleteStats = stats.For(IndexingOperation.Corax.Delete, start: false);
            Stats.AddStats = stats.For(IndexingOperation.Corax.AddDocument, start: false);
            Stats.ConvertStats = stats.For(IndexingOperation.Corax.Convert, start: false);
        }
    }
}
