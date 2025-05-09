// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using Microsoft.Data.Common.ConnectionString;

namespace Microsoft.Data.Common
{
    internal partial class DbConnectionOptions
    {
        public string this[string keyword] => _parsetable[keyword];

        private PermissionSet _permissionset;

        protected internal virtual PermissionSet CreatePermissionSet() => null;

        internal void DemandPermission()
        {
            if (_permissionset is null)
            {
                _permissionset = CreatePermissionSet();
            }
            _permissionset.Demand();
        }

        internal bool HasBlankPassword
        {
            get
            {
                if (!ConvertValueToIntegratedSecurity())
                {
                    if (_parsetable.TryGetValue(DbConnectionStringKeywords.Password, out string value))
                    {
                        return string.IsNullOrEmpty(value);
                    }
                    
                    if (_parsetable.TryGetValue(DbConnectionStringSynonyms.Pwd, out value))
                    {
                        return string.IsNullOrEmpty(value); // MDAC 83097
                    }

                    return (_parsetable.TryGetValue(DbConnectionStringKeywords.UserID, out value) && !string.IsNullOrEmpty(value)) || 
                           (_parsetable.TryGetValue(DbConnectionStringSynonyms.UID, out value) && !string.IsNullOrEmpty(value));
                }
                return false;
            }
        }

        internal string ExpandKeyword(string keyword, string replacementValue)
        {
            // preserve duplicates, updated keyword value with replacement value
            // if keyword not specified, append to end of the string
            bool expanded = false;
            int copyPosition = 0;

            StringBuilder builder = new(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; current != null; current = current.Next)
            {
                if ((current.Name == keyword) && (current.Value == this[keyword]))
                {
                    // only replace the parse end-result value instead of all values
                    // so that when duplicate-keywords occur other original values remain in place
                    AppendKeyValuePairBuilder(builder, current.Name, replacementValue);
                    builder.Append(';');
                    expanded = true;
                }
                else
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                }
                copyPosition += current.Length;
            }

            if (!expanded)
            {
                AppendKeyValuePairBuilder(builder, keyword, replacementValue);
            }
            return builder.ToString();
        }

        internal static void AppendKeyValuePairBuilder(StringBuilder builder, string keyName, string keyValue, bool useOdbcRules = false)
        {
            ADP.CheckArgumentNull(builder, nameof(builder));
            ADP.CheckArgumentLength(keyName, nameof(keyName));

            if (keyName == null || !s_connectionStringValidKeyRegex.IsMatch(keyName))
            {
                throw ADP.InvalidKeyname(keyName);
            }
            if (keyValue != null && !IsValueValidInternal(keyValue))
            {
                throw ADP.InvalidValue(keyName);
            }

            if ((0 < builder.Length) && (';' != builder[builder.Length - 1]))
            {
                builder.Append(";");
            }

            if (useOdbcRules)
            {
                builder.Append(keyName);
            }
            else
            {
                builder.Append(keyName.Replace("=", "=="));
            }
            builder.Append("=");

            if (keyValue != null)
            { // else <keyword>=;
                if (useOdbcRules)
                {
                    if ((0 < keyValue.Length) &&
                        (('{' == keyValue[0]) || (0 <= keyValue.IndexOf(';')) || (0 == string.Compare(DbConnectionStringKeywords.Driver, keyName, StringComparison.OrdinalIgnoreCase))) &&
                        !s_connectionStringQuoteOdbcValueRegex.IsMatch(keyValue))
                    {
                        // always quote Driver value (required for ODBC Version 2.65 and earlier)
                        // always quote values that contain a ';'
                        builder.Append('{').Append(keyValue.Replace("}", "}}")).Append('}');
                    }
                    else
                    {
                        builder.Append(keyValue);
                    }
                }
                else if (s_connectionStringQuoteValueRegex.IsMatch(keyValue))
                {
                    // <value> -> <value>
                    builder.Append(keyValue);
                }
                else if ((-1 != keyValue.IndexOf('\"')) && (-1 == keyValue.IndexOf('\'')))
                {
                    // <val"ue> -> <'val"ue'>
                    builder.Append('\'');
                    builder.Append(keyValue);
                    builder.Append('\'');
                }
                else
                {
                    // <val'ue> -> <"val'ue">
                    // <=value> -> <"=value">
                    // <;value> -> <";value">
                    // < value> -> <" value">
                    // <va lue> -> <"va lue">
                    // <va'"lue> -> <"va'""lue">
                    builder.Append('\"');
                    builder.Append(keyValue.Replace("\"", "\"\""));
                    builder.Append('\"');
                }
            }
        }

        // SxS notes:
        // * this method queries "DataDirectory" value from the current AppDomain.
        //   This string is used for to replace "!DataDirectory!" values in the connection string, it is not considered as an "exposed resource".
        // * This method uses GetFullPath to validate that root path is valid, the result is not exposed out.
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        internal static string ExpandDataDirectory(string keyword, string value, ref string datadir)
        {
            string fullPath = null;
            if (value != null && value.StartsWith(DataDirectory, StringComparison.OrdinalIgnoreCase))
            {

                string rootFolderPath = datadir;
                if (rootFolderPath == null)
                {
                    // find the replacement path
                    object rootFolderObject = AppDomain.CurrentDomain.GetData("DataDirectory");
                    rootFolderPath = (rootFolderObject as string);
                    if (rootFolderObject != null && rootFolderPath == null)
                    {
                        throw ADP.InvalidDataDirectory();
                    }
                    else if (string.IsNullOrEmpty(rootFolderPath))
                    {
                        rootFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                    }
                    if (rootFolderPath == null)
                    {
                        rootFolderPath = "";
                    }
                    // cache the |DataDir| for ExpandDataDirectories
                    datadir = rootFolderPath;
                }

                // We don't know if rootFolderpath ends with '\', and we don't know if the given name starts with onw
                int fileNamePosition = DataDirectory.Length;    // filename starts right after the '|datadirectory|' keyword
                bool rootFolderEndsWith = (0 < rootFolderPath.Length) && rootFolderPath[rootFolderPath.Length - 1] == '\\';
                bool fileNameStartsWith = (fileNamePosition < value.Length) && value[fileNamePosition] == '\\';

                // replace |datadirectory| with root folder path
                if (!rootFolderEndsWith && !fileNameStartsWith)
                {
                    // need to insert '\'
                    fullPath = rootFolderPath + '\\' + value.Substring(fileNamePosition);
                }
                else if (rootFolderEndsWith && fileNameStartsWith)
                {
                    // need to strip one out
                    fullPath = rootFolderPath + value.Substring(fileNamePosition + 1);
                }
                else
                {
                    // simply concatenate the strings
                    fullPath = rootFolderPath + value.Substring(fileNamePosition);
                }

                // verify root folder path is a real path without unexpected "..\"
                if (!ADP.GetFullPath(fullPath).StartsWith(rootFolderPath, StringComparison.Ordinal))
                {
                    throw ADP.InvalidConnectionOptionValue(keyword);
                }
            }
            return fullPath;
        }
    }
}
