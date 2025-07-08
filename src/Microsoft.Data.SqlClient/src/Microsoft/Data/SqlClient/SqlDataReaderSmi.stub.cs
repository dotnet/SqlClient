// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: This is only a stub class for clearing errors while merging other files.

using System.Data;

namespace Microsoft.Data.SqlClient
{
    internal class SqlDataReaderSmi : SqlDataReader
    {
        internal SqlDataReaderSmi() : base(null, CommandBehavior.Default)
        {
        }
    }
}
