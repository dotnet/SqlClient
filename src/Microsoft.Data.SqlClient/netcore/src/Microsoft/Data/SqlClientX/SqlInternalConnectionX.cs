// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX
{
    internal class SqlConnector
    {
        public string DataSource => throw new NotImplementedException();

        public string ServerVersion => throw new NotImplementedException();

        public ConnectionState State => throw new NotImplementedException();

        public void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        public Task Close(bool async)
        {
            throw new NotImplementedException();
        }

        public Task Open(bool async)
        {
            throw new NotImplementedException();
        }
    }
}
