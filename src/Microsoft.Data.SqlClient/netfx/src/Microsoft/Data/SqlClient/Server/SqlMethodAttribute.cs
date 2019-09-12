// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Server
{

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false), Serializable]
    public sealed class SqlMethodAttribute : SqlFunctionAttribute
    {
        private bool m_fCallOnNullInputs;
        private bool m_fMutator;
        private bool m_fInvokeIfReceiverIsNull;

        public SqlMethodAttribute()
        {
            // default values
            m_fCallOnNullInputs = true;
            m_fMutator = false;
            m_fInvokeIfReceiverIsNull = false;

        } // SqlMethodAttribute

        public bool OnNullCall
        {
            get
            {
                return m_fCallOnNullInputs;
            }
            set
            {
                m_fCallOnNullInputs = value;
            }
        } // CallOnNullInputs

        public bool IsMutator
        {
            get
            {
                return m_fMutator;
            }
            set
            {
                m_fMutator = value;
            }
        } // IsMutator

        public bool InvokeIfReceiverIsNull
        {
            get
            {
                return m_fInvokeIfReceiverIsNull;
            }
            set
            {
                m_fInvokeIfReceiverIsNull = value;
            }
        } // InvokeIfReceiverIsNull
    } // class SqlMethodAttribute
}
