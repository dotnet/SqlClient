// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Internal.Logging.Test;

public class SqlClientEventSourceTest
{
    [Fact]
    public void SqlClientEventSource_Log_IsNotNull()
    {
        Assert.NotNull(SqlClientEventSource.Log);
    }
}
