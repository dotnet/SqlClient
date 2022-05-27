// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/SqlEnclaveAttestationParameters/*' />
    internal class SqlEnclaveAttestationParameters
    {
        private readonly byte[] _input;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/ctor/*' />
        internal SqlEnclaveAttestationParameters(int protocol, byte[] input, ECDiffieHellman clientDiffieHellmanKey)
        {
            if (input == null)
            {
                throw SQL.NullArgumentInConstructorInternal(nameof(input), nameof(SqlEnclaveAttestationParameters));
            }
            if (clientDiffieHellmanKey == null)
            {
                throw SQL.NullArgumentInConstructorInternal(nameof(clientDiffieHellmanKey), nameof(SqlEnclaveAttestationParameters));
            }

            _input = input;
            Protocol = protocol;
            ClientDiffieHellmanKey = clientDiffieHellmanKey;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/Protocol/*' />
        internal int Protocol { get; private set; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/ClientDiffieHellmanKey/*' />
        internal ECDiffieHellman ClientDiffieHellmanKey { get; private set; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/GetInput/*' />
        internal byte[] GetInput()
        {
            // return a new array for safety so the caller cannot mutate the original
            if (_input == null)
            {
                return null;
            }

            byte[] output = new byte[_input.Length];
            Buffer.BlockCopy(_input, 0, output, 0, _input.Length);
            return output;
        }
    }
}
