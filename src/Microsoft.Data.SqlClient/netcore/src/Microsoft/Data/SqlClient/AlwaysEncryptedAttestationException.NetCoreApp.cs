// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal class AlwaysEncryptedAttestationException : Exception
    {
        public AlwaysEncryptedAttestationException(string message, Exception innerException) : base(message, innerException) { }

        public AlwaysEncryptedAttestationException(string message) : base(message) { }

        public AlwaysEncryptedAttestationException() : base() { }
    }
}
