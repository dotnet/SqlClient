// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/SqlConnectionEncryptOptions/*'/>
    public sealed class SqlConnectionEncryptOption
    {
        private const string TRUE = "True";
        private const string FALSE = "False";
        private const string STRICT = "Strict";
        private const string TRUE_LOWER = "true";
        private const string YES_LOWER = "yes";
        private const string MANDATORY_LOWER = "mandatory";
        private const string FALSE_LOWER = "false";
        private const string NO_LOWER = "no";
        private const string OPTIONAL_LOWER = "optional";
        private const string STRICT_LOWER = "strict";
        private readonly string _value;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/ctor/*' />
        public SqlConnectionEncryptOption(string value)
        {
            switch (value.ToLower())
            {
                case TRUE_LOWER:
                case YES_LOWER:
                case MANDATORY_LOWER:
                    {
                        _value = TRUE;
                        break;
                    }
                case FALSE_LOWER:
                case NO_LOWER:
                case OPTIONAL_LOWER:
                    {
                        _value = FALSE;
                        break;
                    }
                case STRICT_LOWER:
                    {
                        _value = STRICT;
                        break;
                    }
                default:
                    throw ADP.InvalidConnectionOptionValue(SqlConnectionString.KEY.Encrypt);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Optional/*' />
        public static SqlConnectionEncryptOption Optional = new SqlConnectionEncryptOption(FALSE);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Mandatory/*' />
        public static SqlConnectionEncryptOption Mandatory = new SqlConnectionEncryptOption(TRUE);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Strict/*' />
        public static SqlConnectionEncryptOption Strict = new SqlConnectionEncryptOption(STRICT);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/BoolToOption/*' />
        public static implicit operator SqlConnectionEncryptOption(bool value) => value ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/OptionToBool/*' />
        public static implicit operator bool(SqlConnectionEncryptOption value) => !Optional.Equals(value);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/ToString/*' />
        public override string ToString() => _value;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Equals/*' />
        public override bool Equals(object obj)
        {
            if (obj != null &&
                obj is SqlConnectionEncryptOption)
            {
                return ToString().Equals(((SqlConnectionEncryptOption)obj).ToString());
            }

            return false;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/GetHashCode/*' />
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }

}
