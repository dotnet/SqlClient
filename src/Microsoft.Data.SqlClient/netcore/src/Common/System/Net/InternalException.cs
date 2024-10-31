// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Net
{
    [Serializable]
    internal class InternalException : Exception
    {
        public InternalException() : this("InternalException thrown.")
        {
        }

        public InternalException(string message) : this(message, null)
        {
        }

        public InternalException(string message, Exception innerException) : base(message, innerException)
        {
            NetEventSource.Fail(this, message);
        }
    }
}
