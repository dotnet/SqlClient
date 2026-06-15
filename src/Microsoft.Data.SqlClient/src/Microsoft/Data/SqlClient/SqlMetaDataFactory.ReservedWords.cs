// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlMetaDataFactory
    {
        private sealed class ReservedWordsCollection : MetaDataCollectionBase
        {
            private readonly string[] _reservedWords;
            internal ReservedWordsCollection(string[] reserverdWords)
                : base(DbMetaDataCollectionNames.ReservedWords, 0, 0)
            {
                _reservedWords = reserverdWords;
            }

            public override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable accumulator = null)
            {
                if (!ADP.IsEmptyArray(context.RestrictionValues))
                {
                    throw ADP.TooManyRestrictions(CollectionName);
                }

                DataTable table = accumulator ?? new(CollectionName)
                {
                    Columns =
                    {
                        new DataColumn(DbMetaDataColumnNames.ReservedWord, typeof(string))
                    }
                };

                foreach (string reservedWord in _reservedWords)
                {
                    DataRow row = table.NewRow();
                    row[DbMetaDataColumnNames.ReservedWord] = reservedWord;
                    table.Rows.Add(row);
                }

                return new ValueTask<DataTable>(table);
            }
        }
    }
}
