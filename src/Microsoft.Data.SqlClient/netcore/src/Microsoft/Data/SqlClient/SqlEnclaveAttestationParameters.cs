// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/SqlEnclaveAttestationParameters/*' />
    internal partial class SqlEnclaveAttestationParameters
    {
        private readonly byte[] _input = null;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/Protocol/*' />
        internal int Protocol { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlEnclaveAttestationParameters.xml' path='docs/members[@name="SqlEnclaveAttestationParameters"]/GetInput/*' />
        internal byte[] GetInput()
        {
            return Clone(_input);
        }

        /// <summary>
        /// Deep copy the array into a new array
        /// </summary>
        /// <param name="arrayToClone"></param>
        /// <returns></returns>
        private byte[] Clone(byte[] arrayToClone)
        {

            if (null == arrayToClone)
            {
                return null;
            }

            byte[] returnValue = new byte[arrayToClone.Length];

            for (int i = 0; i < arrayToClone.Length; i++)
            {
                returnValue[i] = arrayToClone[i];
            }

            return returnValue;
        }
    }
}
