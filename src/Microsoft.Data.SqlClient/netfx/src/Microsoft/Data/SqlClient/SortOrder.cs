// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Specifies how rows of data are sorted.
    /// </summary>
    /// <remarks>To be added.</remarks>
    public enum SortOrder
    {
        /// <summary>
        /// The default. No sort order is specified.
        /// </summary>
        /// <value>-1</value>
        Unspecified = -1,

        /// <summary>
        /// Rows are sorted in ascending order.
        /// </summary>
        /// <Value>0</Value>
        Ascending = 0,

        /// <summary>
        /// Rows are sorted in descending order.
        /// </summary>
        /// <value>1</value>
        Descending = 1
    }
}
