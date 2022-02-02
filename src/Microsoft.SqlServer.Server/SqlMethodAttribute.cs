// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.SqlServer.Server
{
    /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlMethodAttribute.xml' path='Type[@Name="SqlMethodAttribute"]/Docs/*' />
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false), Serializable]
    public sealed class SqlMethodAttribute : SqlFunctionAttribute
    {
        private bool _isCalledOnNullInputs;
        private bool _isMutator;
        private bool _shouldInvokeIfReceiverIsNull;

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlMethodAttribute.xml' path='Type[@Name="SqlMethodAttribute"]/Members/Member[@MemberName=".ctor"]/Docs/*' />
        public SqlMethodAttribute()
        {
            // default values
            _isCalledOnNullInputs = true;
            _isMutator = false;
            _shouldInvokeIfReceiverIsNull = false;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlMethodAttribute.xml' path='Type[@Name="SqlMethodAttribute"]/Members/Member[@MemberName="OnNullCall"]/Docs/*' />
        public bool OnNullCall
        {
            get => _isCalledOnNullInputs;
            set => _isCalledOnNullInputs = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlMethodAttribute.xml' path='Type[@Name="SqlMethodAttribute"]/Members/Member[@MemberName="IsMutator"]/Docs/*' />
        public bool IsMutator
        {
            get => _isMutator;
            set => _isMutator = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlMethodAttribute.xml' path='Type[@Name="SqlMethodAttribute"]/Members/Member[@MemberName="InvokeIfReceiverIsNull"]/Docs/*' />
        public bool InvokeIfReceiverIsNull
        {
            get => _shouldInvokeIfReceiverIsNull;
            set => _shouldInvokeIfReceiverIsNull = value;
        }
    }
}
