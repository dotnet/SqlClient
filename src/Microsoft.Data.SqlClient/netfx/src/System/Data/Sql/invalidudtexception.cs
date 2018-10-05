//------------------------------------------------------------------------------
// <copyright file="InvalidUdtException.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

using System;
using Microsoft.Data;
using Microsoft.Data.Common;
using System.Runtime.Serialization;

namespace Microsoft.SqlServer.Server {
    
    [Serializable]
    public sealed class InvalidUdtException : SystemException {
     
        internal InvalidUdtException() : base() {
            HResult = HResults.InvalidUdt;
        }

        internal InvalidUdtException(String message) : base(message) {
            HResult = HResults.InvalidUdt;
        }

        internal InvalidUdtException(String message, Exception innerException) : base(message, innerException) {
            HResult = HResults.InvalidUdt;
        }

        private InvalidUdtException(SerializationInfo si, StreamingContext sc) : base(si, sc) {
        }

        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Flags=System.Security.Permissions.SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo si, StreamingContext context) {
            base.GetObjectData(si, context);
        }

        internal static InvalidUdtException Create(Type udtType, string resourceReason) {
            string reason = ResHelper.GetString(resourceReason);
            string message = ResHelper.GetString(Res.SqlUdt_InvalidUdtMessage, udtType.FullName, reason);
            InvalidUdtException e =  new InvalidUdtException(message);
            ADP.TraceExceptionAsReturnValue(e);
            return e;
        }
    }
}
