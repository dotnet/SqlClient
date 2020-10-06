// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;
using Microsoft.Data.Common;

namespace Microsoft.Data
{
    /// <include file='../../../../../doc/snippets/Microsoft.Data/OperationAbortedException.xml' path='docs/members[@name="OperationAbortedException"]/OperationAbortedException/*' />
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class OperationAbortedException : SystemException
    {
        private OperationAbortedException(string message, Exception innerException) : base(message, innerException)
        {
            HResult = unchecked((int)0x80131936);
        }

        private OperationAbortedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        internal static OperationAbortedException Aborted(Exception inner)
        {
            OperationAbortedException e;
            if (inner == null)
            {
                e = new OperationAbortedException(Strings.ADP_OperationAborted, null);
            }
            else
            {
                e = new OperationAbortedException(Strings.ADP_OperationAbortedExceptionMessage, inner);
            }
            ADP.TraceExceptionAsReturnValue(e);
            return e;
        }
    }
}
