// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlMetaDataFactory
    {
        private sealed class RestrictionsCollection : MetaDataCollectionBase
        {
            internal RestrictionsCollection() : base(DbMetaDataCollectionNames.Restrictions, 0, 0)
            {
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
                        new DataColumn(DbMetaDataColumnNames.CollectionName, typeof(string)),
                        new DataColumn(RestrictionNameKey, typeof(string)),
                        new DataColumn(ParameterNameKey, typeof(string)),
                        new DataColumn(RestrictionDefaultKey, typeof(string)),
                        new DataColumn(RestrictionNumberKey, typeof(int)),
                    }
                };

                foreach (MetaDataCollectionBase mdc in s_metaDataCollection)
                {
                    if (mdc is SqlCommandCollection sqlCollection &&
                        sqlCollection.RestrictionParams != null &&
                        mdc.SupportedByCurrentVersion(context.ServerVersion))
                    {
                        foreach (Restriction restriction in sqlCollection.RestrictionParams)
                        {
                            DataRow row = table.NewRow();
                            table.Rows.Add([mdc.CollectionName, restriction.RestrictionName, restriction.ParameterName, null, restriction.RestrictionNumber]);
                        }
                    }
                }

                return new ValueTask<DataTable>(table);
            }
        }
    }
}
