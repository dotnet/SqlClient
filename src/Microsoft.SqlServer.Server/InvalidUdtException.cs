// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.SqlServer.Server
{
    /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/InvalidUdtException.xml' path='docs/members[@name="InvalidUdtException"]/InvalidUdtException/*' />
    [Serializable]
    public sealed class InvalidUdtException : SystemException
    {
        private const int InvalidUdtHResult = unchecked((int)0x80131937);

        internal InvalidUdtException() : base()
        {
            HResult = InvalidUdtHResult;
        }

        internal InvalidUdtException(string message) : base(message)
        {
            HResult = InvalidUdtHResult;
        }

        internal InvalidUdtException(string message, Exception innerException) : base(message, innerException)
        {
            HResult = InvalidUdtHResult;
        }

        private InvalidUdtException(SerializationInfo si, StreamingContext sc) : base(si, sc)
        {
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/InvalidUdtException.xml' path='docs/members[@name="InvalidUdtException"]/GetObjectData/*' />
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Flags = System.Security.Permissions.SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo si, StreamingContext context)
        {
            base.GetObjectData(si, context);
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/InvalidUdtException.xml' path='docs/members[@name="InvalidUdtException"]/Create/*' />
        public static InvalidUdtException Create(Type udtType, string resourceReason = "SqlUdtReason_NoUdtAttribute")
        {
            string reason = StringsHelper.GetString(resourceReason);
            string message = StringsHelper.GetString(Strings.SqlUdt_InvalidUdtMessage, udtType.FullName, reason);
            InvalidUdtException e = new InvalidUdtException(message);
            return e;
        }
    }
}
