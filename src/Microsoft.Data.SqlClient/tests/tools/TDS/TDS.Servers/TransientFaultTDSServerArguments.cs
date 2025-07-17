// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SqlServer.TDS.Servers
{
    public class TransientFaultTdsServerArguments : TdsServerArguments
    {
        /// <summary>
        /// Transient error number to be raised by server.
        /// </summary>
        public uint Number { get; set; }

        /// <summary>
        /// Transient error message to be raised by server.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Flag to consider when raising Transient error.
        /// </summary>
        public bool IsEnabledTransientError { get; set; }

        /// <summary>
        /// Constructor to initialize
        /// </summary>
        public TransientFaultTdsServerArguments()
        {
            Number = 0;
            Message = string.Empty;
            IsEnabledTransientError = false;
        }
    }
}
