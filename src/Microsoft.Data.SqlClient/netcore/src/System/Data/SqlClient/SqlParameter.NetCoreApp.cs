using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlParameter
    {
        /// <summary>
        /// Get or set the encryption related metadata of this SqlParameter.
        /// Should be set to a non-null value only once.
        /// </summary>
        internal SqlCipherMetadata CipherMetadata { get; set; }

        /// <summary>
        /// Returns the normalization rule version number as a byte
        /// </summary>
        internal byte NormalizationRuleVersion
        {
            get
            {
                if (CipherMetadata != null)
                {
                    return CipherMetadata.NormalizationRuleVersion;
                }

                return 0x00;
            }
        }
    }
}
