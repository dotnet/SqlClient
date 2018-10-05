//------------------------------------------------------------------------------
// <copyright file="DbConnectionPoolGroupProviderInfo.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">mithomas</owner>
// <owner current="true" primary="false">stevesta</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.ProviderBase {

    using System;
    internal class DbConnectionPoolGroupProviderInfo {
        private DbConnectionPoolGroup _poolGroup;

        internal DbConnectionPoolGroup PoolGroup {
            get {
                return _poolGroup;
            }
            set {
                _poolGroup = value;
            }
        }
    }
}
