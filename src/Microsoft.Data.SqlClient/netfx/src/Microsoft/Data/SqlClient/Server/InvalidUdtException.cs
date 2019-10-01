// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\InvalidUdtException.xml' path='docs/members[@name="InvalidUdtException"]/InvalidUdtException/*' />
    [Serializable]
    public sealed class InvalidUdtException : SystemException
    {

        internal InvalidUdtException() : base()
        {
            HResult = HResults.InvalidUdt;
        }

        internal InvalidUdtException(String message) : base(message)
        {
            HResult = HResults.InvalidUdt;
        }

        internal InvalidUdtException(String message, Exception innerException) : base(message, innerException)
        {
            HResult = HResults.InvalidUdt;
        }

        private InvalidUdtException(SerializationInfo si, StreamingContext sc) : base(si, sc)
        {
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\InvalidUdtException.xml' path='docs/members[@name="InvalidUdtException"]/GetObjectData/*' />
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Flags = System.Security.Permissions.SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo si, StreamingContext context)
        {
            base.GetObjectData(si, context);
        }

        internal static InvalidUdtException Create(Type udtType, string resourceReason)
        {
            string reason = StringsHelper.GetString(resourceReason);
            string message = StringsHelper.GetString(Strings.SqlUdt_InvalidUdtMessage, udtType.FullName, reason);
            InvalidUdtException e = new InvalidUdtException(message);
            ADP.TraceExceptionAsReturnValue(e);
            return e;
        }
    }
}
