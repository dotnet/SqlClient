// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Microsoft.Data.ProviderBase
{
    internal class DbMetaDataFactory
    {
        // well known column names
        private const string CollectionNameKey = "CollectionName";
        private const string PopulationMechanismKey = "PopulationMechanism";
        private const string PopulationStringKey = "PopulationString";
        private const string MaximumVersionKey = "MaximumVersion";
        private const string MinimumVersionKey = "MinimumVersion";
        private const string RestrictionDefaultKey = "RestrictionDefault";
        private const string RestrictionNumberKey = "RestrictionNumber";
        private const string NumberOfRestrictionsKey = "NumberOfRestrictions";
        private const string RestrictionNameKey = "RestrictionName";
        private const string ParameterNameKey = "ParameterName";

        protected DataSet CollectionDataSet { get; set; }

        protected string ServerVersion { get; set; }

        protected virtual DataTable CloneAndFilterCollection(string collectionName, ReadOnlySpan<string> hiddenColumnNames)
        {
            throw ADP.MethodNotImplemented();
        }

        protected virtual DataTable ExecuteCommand(DataRow requestedCollectionRow, string[] restrictions, DbConnection connection)
        {
            throw ADP.MethodNotImplemented();
        }

        protected virtual DataTable PrepareCollection(string collectionName, string[] restrictions, DbConnection connection)
        {
            throw ADP.NotSupported();
        }

        protected bool SupportedByCurrentVersion(DataRow requestedCollectionRow)
        {
            DataColumnCollection tableColumns = requestedCollectionRow.Table.Columns;
            DataColumn versionColumn;
            object version;

            // check the minimum version first
            versionColumn = tableColumns[MinimumVersionKey];
            if (versionColumn is not null)
            {
                version = requestedCollectionRow[versionColumn];

                if (version is string minVersion
                    && string.Compare(ServerVersion, minVersion, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            // if the minimum version was ok what about the maximum version
            versionColumn = tableColumns[MaximumVersionKey];
            if (versionColumn is not null)
            {
                version = requestedCollectionRow[versionColumn];

                if (version is string maxVersion
                    && string.Compare(ServerVersion, maxVersion, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
