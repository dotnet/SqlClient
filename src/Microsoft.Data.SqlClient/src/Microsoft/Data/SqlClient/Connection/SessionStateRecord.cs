// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Connection
{
    // @TODO: if this is a "record", why not make it a record?
    // @TODO: All names should be renamed to be follow guidelines.
    // @TODO: All the values in this "record" are assigned but allegedly never used (might be used in parser but since it's not analyzed it's not showing up)
    internal sealed class SessionStateRecord
    {
        internal bool _recoverable;
        internal uint _version;
        internal int _dataLength;
        internal byte[] _data;
    }
}
