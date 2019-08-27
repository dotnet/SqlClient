// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public abstract class DbObject : ICreatable, IDroppable
    {
        protected DbObject(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public abstract void Create(SqlConnection sqlConnection);

        public abstract void Drop(SqlConnection sqlConnection);
    }
}
