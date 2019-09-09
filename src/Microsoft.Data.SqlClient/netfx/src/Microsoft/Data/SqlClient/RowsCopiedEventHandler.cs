// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// Represents the method that handles the <see cref="Microsoft.Data.SqlClient.SqlBulkCopy.SqlRowsCopied" /> event of a <see cref="Microsoft.Data.SqlClient.SqlBulkCopy" />.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks>To be added.</remarks>
    public delegate void SqlRowsCopiedEventHandler(object sender, SqlRowsCopiedEventArgs e);
}

