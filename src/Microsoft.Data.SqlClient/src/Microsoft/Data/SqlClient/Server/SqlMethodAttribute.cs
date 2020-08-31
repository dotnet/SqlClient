// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMethodAttribute.xml' path='docs/members[@name="SqlMethodAttribute"]/SqlMethodAttribute/*' />
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false), Serializable]
    public sealed class SqlMethodAttribute : SqlFunctionAttribute
    {
        private bool _isCalledOnNullInputs;
        private bool _isMutator;
        private bool _shouldInvokeIfReceiverIsNull;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMethodAttribute.xml' path='docs/members[@name="SqlMethodAttribute"]/ctor/*' />
        public SqlMethodAttribute()
        {
            // default values
            _isCalledOnNullInputs = true;
            _isMutator = false;
            _shouldInvokeIfReceiverIsNull = false;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMethodAttribute.xml' path='docs/members[@name="SqlMethodAttribute"]/OnNullCall/*' />
        public bool OnNullCall
        {
            get => _isCalledOnNullInputs;
            set => _isCalledOnNullInputs = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMethodAttribute.xml' path='docs/members[@name="SqlMethodAttribute"]/IsMutator/*' />
        public bool IsMutator
        {
            get => _isMutator;
            set => _isMutator = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMethodAttribute.xml' path='docs/members[@name="SqlMethodAttribute"]/InvokeIfReceiverIsNull/*' />
        public bool InvokeIfReceiverIsNull
        {
            get => _shouldInvokeIfReceiverIsNull;
            set => _shouldInvokeIfReceiverIsNull = value;
        }
    }
}
