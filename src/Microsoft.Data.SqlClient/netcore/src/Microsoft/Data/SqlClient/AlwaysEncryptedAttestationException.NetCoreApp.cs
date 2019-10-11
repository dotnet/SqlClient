using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient
{
   
        /// <summary>
        /// Represents errors occuring during an Always Encrypted secure enclave operation
        /// </summary>
        internal class AlwaysEncryptedAttestationException : Exception
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="message"></param>
            /// <param name="innerException"></param>
            public AlwaysEncryptedAttestationException(string message, Exception innerException) : base(message, innerException) { }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="message"></param>
            public AlwaysEncryptedAttestationException(string message) : base(message) { }

            /// <summary>
            /// 
            /// </summary>
            public AlwaysEncryptedAttestationException() : base() { }
        }
}
