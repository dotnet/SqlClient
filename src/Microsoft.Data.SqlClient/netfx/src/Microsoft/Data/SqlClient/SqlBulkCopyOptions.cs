// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum SqlBulkCopyOptions {

        /// <summary>
        /// 
        /// </summary>
        /// <Value>0</Value>
        Default             = 0,

        /// <summary>
        /// Identity is kept.
        /// </summary>
        /// <value>1</value>
        KeepIdentity        = 1 << 0,

        /// <summary>
        /// left-shifted by 1.
        /// </summary>
        /// <value>1</value>
        CheckConstraints    = 1 << 1,

        /// <summary>
        /// left-shifted by 2
        /// </summary>
        TableLock           = 1 << 2,

        /// <summary>
        /// left shifted by 3.
        /// </summary>
        KeepNulls           = 1 << 3,

        /// <summary>
        /// left-shifted by4
        /// </summary>
        FireTriggers        = 1 << 4,

        /// <summary>
        /// 
        /// </summary>
        UseInternalTransaction = 1 << 5,

        /// <summary>
        /// 
        /// </summary>
        AllowEncryptedValueModifications = 1 << 6,
    }
}




