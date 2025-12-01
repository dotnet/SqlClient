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
    }
}
