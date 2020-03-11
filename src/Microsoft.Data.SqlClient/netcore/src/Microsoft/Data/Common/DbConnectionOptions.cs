// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Data.Common
{
    internal partial class DbConnectionOptions
    {
        // instances of this class are intended to be immutable, i.e readonly
        // used by pooling classes so it is much easier to verify correctness
        // when not worried about the class being modified during execution

        public DbConnectionOptions(string connectionString, Dictionary<string, string> synonyms)
        {
            _parsetable = new Dictionary<string, string>();
            _usersConnectionString = ((null != connectionString) ? connectionString : "");

            // first pass on parsing, initial syntax check
            if (0 < _usersConnectionString.Length)
            {
                _keyChain = ParseInternal(_parsetable, _usersConnectionString, true, synonyms, false);
                HasPasswordKeyword = (_parsetable.ContainsKey(KEY.Password) || _parsetable.ContainsKey(SYNONYM.Pwd));
                HasUserIdKeyword = (_parsetable.ContainsKey(KEY.User_ID) || _parsetable.ContainsKey(SYNONYM.UID));
            }
        }

        protected DbConnectionOptions(DbConnectionOptions connectionOptions)
        { // Clone used by SqlConnectionString
            _usersConnectionString = connectionOptions._usersConnectionString;            
            _parsetable = connectionOptions._parsetable;
            _keyChain = connectionOptions._keyChain;
            HasPasswordKeyword = connectionOptions.HasPasswordKeyword;
            HasUserIdKeyword = connectionOptions.HasUserIdKeyword;
        }

        public bool IsEmpty => _keyChain == null;

        internal bool TryGetParsetableValue(string key, out string value) => _parsetable.TryGetValue(key, out value);

        // same as Boolean, but with SSPI thrown in as valid yes
        public bool ConvertValueToIntegratedSecurity()
        {
            string value;
            return _parsetable.TryGetValue(KEY.Integrated_Security, out value) && value != null ?
                ConvertValueToIntegratedSecurityInternal(value) :
                false;
        }

        internal bool ConvertValueToIntegratedSecurityInternal(string stringValue)
        {
            if (CompareInsensitiveInvariant(stringValue, "sspi") || CompareInsensitiveInvariant(stringValue, "true") || CompareInsensitiveInvariant(stringValue, "yes"))
                return true;
            else if (CompareInsensitiveInvariant(stringValue, "false") || CompareInsensitiveInvariant(stringValue, "no"))
                return false;
            else
            {
                string tmp = stringValue.Trim();  // Remove leading & trailing whitespace.
                if (CompareInsensitiveInvariant(tmp, "sspi") || CompareInsensitiveInvariant(tmp, "true") || CompareInsensitiveInvariant(tmp, "yes"))
                    return true;
                else if (CompareInsensitiveInvariant(tmp, "false") || CompareInsensitiveInvariant(tmp, "no"))
                    return false;
                else
                {
                    throw ADP.InvalidConnectionOptionValue(KEY.Integrated_Security);
                }
            }
        }

        public int ConvertValueToInt32(string keyName, int defaultValue)
        {
            string value;
            return _parsetable.TryGetValue(keyName, out value) && value != null ?
                ConvertToInt32Internal(keyName, value) :
                defaultValue;
        }

        internal static int ConvertToInt32Internal(string keyname, string stringValue)
        {
            try
            {
                return int.Parse(stringValue, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(keyname, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(keyname, e);
            }
        }

        public string ConvertValueToString(string keyName, string defaultValue)
        {
            string value;
            return _parsetable.TryGetValue(keyName, out value) && value != null ? value : defaultValue;
        }

        public bool ContainsKey(string keyword)
        {
            return _parsetable.ContainsKey(keyword);
        }

        protected internal virtual string Expand()
        {
            return _usersConnectionString;
        }

        // SxS notes:
        // * this method queries "DataDirectory" value from the current AppDomain.
        //   This string is used for to replace "!DataDirectory!" values in the connection string, it is not considered as an "exposed resource".
        // * This method uses GetFullPath to validate that root path is valid, the result is not exposed out.
        internal static string ExpandDataDirectory(string keyword, string value)
        {
            string fullPath = null;
            if ((null != value) && value.StartsWith(DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // find the replacement path
                object rootFolderObject = AppDomain.CurrentDomain.GetData("DataDirectory");
                var rootFolderPath = (rootFolderObject as string);
                if ((null != rootFolderObject) && (null == rootFolderPath))
                {
                    throw ADP.InvalidDataDirectory();
                }
                else if (string.IsNullOrEmpty(rootFolderPath))
                {
                    rootFolderPath = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                }

                var fileName = value.Substring(DataDirectory.Length);

                if (Path.IsPathRooted(fileName))
                {
                    fileName = fileName.TrimStart(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                }

                fullPath = Path.Combine(rootFolderPath, fileName);

                // verify root folder path is a real path without unexpected "..\"
                if (!Path.GetFullPath(fullPath).StartsWith(rootFolderPath, StringComparison.Ordinal))
                {
                    throw ADP.InvalidConnectionOptionValue(keyword);
                }
            }
            return fullPath;
        }

        internal string ExpandAttachDbFileName(string replacementValue)
        {
            int copyPosition = 0;

            StringBuilder builder = new StringBuilder(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; null != current; current = current.Next)
            {
                if (current.Name == KEY.AttachDBFileName)
                {
                    builder.Append($"{KEY.AttachDBFileName}={replacementValue};");
                }
                else
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                }
                copyPosition += current.Length;
            }

            return builder.ToString();
        }
    }
}
