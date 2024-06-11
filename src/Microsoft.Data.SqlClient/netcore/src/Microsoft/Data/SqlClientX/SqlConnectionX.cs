// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.SqlServer.Server;
using System.Transactions;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// 
    /// </summary>
    [DefaultEvent("InfoMessage")]
    [DesignerCategory("")]
    public sealed partial class SqlConnectionX : DbConnection, ICloneable
    {
        //TODO: reference to internal connection

        /// <inheritdoc/>
        public override string ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        /// <inheritdoc/>

        public override string Database => throw new NotImplementedException();
        /// <inheritdoc/>

        public override string DataSource => throw new NotImplementedException();
        /// <inheritdoc/>

        public override string ServerVersion => throw new NotImplementedException();
        /// <inheritdoc/>

        public override ConnectionState State => throw new NotImplementedException();
        /// <inheritdoc/>

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>

        public object Clone()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>

        public override void Close()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>

        public override void Open()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>

        protected override DbCommand CreateDbCommand()
        {
            throw new NotImplementedException();
        }
    }
}
