// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data
{

    using System;
    using System.Runtime.Serialization;
    using Microsoft.Data.Common;

    [Serializable]
    public sealed class OperationAbortedException : SystemException
    {
        private OperationAbortedException(string message, Exception innerException) : base(message, innerException)
        {
            HResult = HResults.OperationAborted;
        }

        private OperationAbortedException(SerializationInfo si, StreamingContext sc) : base(si, sc)
        {
        }

        static internal OperationAbortedException Aborted(Exception inner)
        {
            OperationAbortedException e;
            if (inner == null)
            {
                e = new OperationAbortedException(StringsHelper.GetString(Strings.ADP_OperationAborted), null);
            }
            else
            {
                e = new OperationAbortedException(StringsHelper.GetString(Strings.ADP_OperationAbortedExceptionMessage), inner);
            }
            ADP.TraceExceptionAsReturnValue(e);
            return e;
        }
    }
}
