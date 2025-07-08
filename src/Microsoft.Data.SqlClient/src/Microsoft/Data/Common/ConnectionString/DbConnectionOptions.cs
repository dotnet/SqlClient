// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#if NETFRAMEWORK
using System.Runtime.Versioning;
using System.Security;
#endif

namespace Microsoft.Data.Common.ConnectionString
{
    internal partial class DbConnectionOptions
    {
        // instances of this class are intended to be immutable, i.e readonly
        // used by pooling classes so it is much easier to verify correctness
        // when not worried about the class being modified during execution

        private const string ConnectionStringValidKeyPattern = "^(?![;\\s])[^\\p{Cc}]+(?<!\\s)$"; // key not allowed to start with semi-colon or space or contain non-visible characters or end with space
        private const string ConnectionStringValidValuePattern = "^[^\u0000]*$";                    // value not allowed to contain embedded null
        private const string ConnectionStringQuoteValuePattern = "^[^\"'=;\\s\\p{Cc}]*$";           // generally do not quote the value if it matches the pattern
        private const string ConnectionStringQuoteOdbcValuePattern = "^\\{([^\\}\u0000]|\\}\\})*\\}$"; // do not quote odbc value if it matches this pattern
        internal const string DataDirectory = "|datadirectory|";

        private static readonly Regex s_connectionStringValidKeyRegex = new Regex(ConnectionStringValidKeyPattern, RegexOptions.Compiled);
        private static readonly Regex s_connectionStringValidValueRegex = new Regex(ConnectionStringValidValuePattern, RegexOptions.Compiled);

        private static readonly Regex s_connectionStringQuoteValueRegex = new Regex(ConnectionStringQuoteValuePattern, RegexOptions.Compiled);
        private static readonly Regex s_connectionStringQuoteOdbcValueRegex = new Regex(ConnectionStringQuoteOdbcValuePattern, RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        internal readonly bool _hasPasswordKeyword;
        internal readonly bool _hasUserIdKeyword;
        internal readonly NameValuePair _keyChain;

        private readonly string _usersConnectionString;
        private readonly Dictionary<string, string> _parsetable;

        internal Dictionary<string, string> Parsetable => _parsetable;
        public bool IsEmpty => _keyChain == null;

        public DbConnectionOptions(string connectionString, IReadOnlyDictionary<string, string> synonyms)
        {
            _parsetable = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _usersConnectionString = connectionString ?? "";

            // first pass on parsing, initial syntax check
            if (_usersConnectionString.Length > 0)
            {
                _keyChain = ParseInternal(_parsetable, _usersConnectionString, true, synonyms, false);
                _hasPasswordKeyword = _parsetable.ContainsKey(DbConnectionStringKeywords.Password) || 
                                      _parsetable.ContainsKey(DbConnectionStringSynonyms.Pwd);
                _hasUserIdKeyword = _parsetable.ContainsKey(DbConnectionStringKeywords.UserId) ||
                                    _parsetable.ContainsKey(DbConnectionStringSynonyms.Uid);
            }
        }

        protected DbConnectionOptions(DbConnectionOptions connectionOptions)
        { // Clone used by SqlConnectionString
            _usersConnectionString = connectionOptions._usersConnectionString;
            _parsetable = connectionOptions._parsetable;
            _keyChain = connectionOptions._keyChain;
            _hasPasswordKeyword = connectionOptions._hasPasswordKeyword;
            _hasUserIdKeyword = connectionOptions._hasUserIdKeyword;
        }

        internal bool TryGetParsetableValue(string key, out string value) => _parsetable.TryGetValue(key, out value);

        // same as Boolean, but with SSPI thrown in as valid yes
        public bool ConvertValueToIntegratedSecurity()
        {
            return _parsetable.TryGetValue(DbConnectionStringKeywords.IntegratedSecurity, out string value) && value != null
                ? ConvertValueToIntegratedSecurityInternal(value)
                : false;
        }

        internal bool ConvertValueToIntegratedSecurityInternal(string stringValue)
        {
            if (CompareInsensitiveInvariant(stringValue, "sspi") || CompareInsensitiveInvariant(stringValue, "true") || CompareInsensitiveInvariant(stringValue, "yes"))
            {
                return true;
            }
            
            if (CompareInsensitiveInvariant(stringValue, "false") || CompareInsensitiveInvariant(stringValue, "no"))
            {
                return false;
            }

            string tmp = stringValue.Trim();  // Remove leading & trailing whitespace.
            if (CompareInsensitiveInvariant(tmp, "sspi") || CompareInsensitiveInvariant(tmp, "true") || CompareInsensitiveInvariant(tmp, "yes"))
            {
                return true;
            }

            if (CompareInsensitiveInvariant(tmp, "false") || CompareInsensitiveInvariant(tmp, "no"))
            {
                return false;
            }

            throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.IntegratedSecurity);
        }

        public int ConvertValueToInt32(string keyName, int defaultValue)
        {
            return _parsetable.TryGetValue(keyName, out string value) && value != null ?
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
            => _parsetable.TryGetValue(keyName, out string value) && value != null ? value : defaultValue;

        public bool ContainsKey(string keyword) => _parsetable.ContainsKey(keyword);

        protected internal virtual string Expand() => _usersConnectionString;

        public string UsersConnectionString(bool hidePassword) => UsersConnectionString(hidePassword, false);

        internal string UsersConnectionStringForTrace() => UsersConnectionString(true, true);

        private string UsersConnectionString(bool hidePassword, bool forceHidePassword)
        {
            string connectionString = _usersConnectionString;
            if (_hasPasswordKeyword && (forceHidePassword || (hidePassword && !HasPersistablePassword)))
            {
                ReplacePasswordPwd(out connectionString, false);
            }
            return connectionString ?? string.Empty;
        }

        internal bool HasPersistablePassword => _hasPasswordKeyword
            ? ConvertValueToBoolean(DbConnectionStringKeywords.PersistSecurityInfo, DbConnectionStringDefaults.PersistSecurityInfo)
            : true; // no password means persistable password so we don't have to munge

        public bool ConvertValueToBoolean(string keyName, bool defaultValue)
        {
            string value;
            return _parsetable.TryGetValue(keyName, out value) ?
                ConvertValueToBooleanInternal(keyName, value) :
                defaultValue;
        }

        internal static bool ConvertValueToBooleanInternal(string keyName, string stringValue)
        {
            if (CompareInsensitiveInvariant(stringValue, "true") || CompareInsensitiveInvariant(stringValue, "yes"))
                return true;
            else if (CompareInsensitiveInvariant(stringValue, "false") || CompareInsensitiveInvariant(stringValue, "no"))
                return false;
            else
            {
                string tmp = stringValue.Trim();  // Remove leading & trailing whitespace.
                if (CompareInsensitiveInvariant(tmp, "true") || CompareInsensitiveInvariant(tmp, "yes"))
                    return true;
                else if (CompareInsensitiveInvariant(tmp, "false") || CompareInsensitiveInvariant(tmp, "no"))
                    return false;
                else
                {
                    throw ADP.InvalidConnectionOptionValue(keyName);
                }
            }
        }

        private static bool CompareInsensitiveInvariant(string strvalue, string strconst)
            => (0 == StringComparer.OrdinalIgnoreCase.Compare(strvalue, strconst));

        private static string GetKeyName(StringBuilder buffer)
        {
            int count = buffer.Length;
            while ((0 < count) && char.IsWhiteSpace(buffer[count - 1]))
            {
                count--; // trailing whitespace
            }
            return buffer.ToString(0, count).ToLower(CultureInfo.InvariantCulture);
        }

        private static string GetKeyValue(StringBuilder buffer, bool trimWhitespace)
        {
            int count = buffer.Length;
            int index = 0;
            if (trimWhitespace)
            {
                while ((index < count) && char.IsWhiteSpace(buffer[index]))
                {
                    index++; // leading whitespace
                }
                while ((0 < count) && char.IsWhiteSpace(buffer[count - 1]))
                {
                    count--; // trailing whitespace
                }
            }
            return buffer.ToString(index, count - index);
        }

        // transition states used for parsing
        private enum ParserState
        {
            NothingYet = 1,   //start point
            Key,
            KeyEqual,
            KeyEnd,
            UnquotedValue,
            DoubleQuoteValue,
            DoubleQuoteValueQuote,
            SingleQuoteValue,
            SingleQuoteValueQuote,
            BraceQuoteValue,
            BraceQuoteValueQuote,
            QuotedValueEnd,
            NullTermination,
        };

        // @TODO: This should probably be extracted into its own parser class
        internal static int GetKeyValuePair(string connectionString, int currentPosition, StringBuilder buffer, bool useOdbcRules, out string keyname, out string keyvalue)
        {
            int startposition = currentPosition;

            buffer.Length = 0;
            keyname = null;
            keyvalue = null;

            char currentChar = '\0';

            ParserState parserState = ParserState.NothingYet;
            int length = connectionString.Length;
            for (; currentPosition < length; ++currentPosition)
            {
                currentChar = connectionString[currentPosition];

                switch (parserState)
                {
                    case ParserState.NothingYet: // [\\s;]*
                        if ((';' == currentChar) || char.IsWhiteSpace(currentChar))
                        {
                            continue;
                        }
                        if ('\0' == currentChar)
                        { parserState = ParserState.NullTermination; continue; }
                        if (char.IsControl(currentChar))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        startposition = currentPosition;
                        if ('=' != currentChar)
                        {
                            parserState = ParserState.Key;
                            break;
                        }
                        else
                        {
                            parserState = ParserState.KeyEqual;
                            continue;
                        }

                    case ParserState.Key: // (?<key>([^=\\s\\p{Cc}]|\\s+[^=\\s\\p{Cc}]|\\s+==|==)+)
                        if ('=' == currentChar)
                        { parserState = ParserState.KeyEqual; continue; }
                        if (char.IsWhiteSpace(currentChar))
                        { break; }
                        if (char.IsControl(currentChar))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.KeyEqual: // \\s*=(?!=)\\s*
                        if (!useOdbcRules && '=' == currentChar)
                        { parserState = ParserState.Key; break; }
                        keyname = GetKeyName(buffer);
                        if (string.IsNullOrEmpty(keyname))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        buffer.Length = 0;
                        parserState = ParserState.KeyEnd;
                        goto case ParserState.KeyEnd;

                    case ParserState.KeyEnd:
                        if (char.IsWhiteSpace(currentChar))
                        { continue; }
                        if (useOdbcRules)
                        {
                            if ('{' == currentChar)
                            { parserState = ParserState.BraceQuoteValue; break; }
                        }
                        else
                        {
                            if ('\'' == currentChar)
                            { parserState = ParserState.SingleQuoteValue; continue; }
                            if ('"' == currentChar)
                            { parserState = ParserState.DoubleQuoteValue; continue; }
                        }
                        if (';' == currentChar)
                        { goto ParserExit; }
                        if ('\0' == currentChar)
                        { goto ParserExit; }
                        if (char.IsControl(currentChar))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        parserState = ParserState.UnquotedValue;
                        break;

                    case ParserState.UnquotedValue: // "((?![\"'\\s])" + "([^;\\s\\p{Cc}]|\\s+[^;\\s\\p{Cc}])*" + "(?<![\"']))"
                        if (char.IsWhiteSpace(currentChar))
                        { break; }
                        if (char.IsControl(currentChar) || ';' == currentChar)
                        { goto ParserExit; }
                        break;

                    case ParserState.DoubleQuoteValue: // "(\"([^\"\u0000]|\"\")*\")"
                        if ('"' == currentChar)
                        { parserState = ParserState.DoubleQuoteValueQuote; continue; }
                        if ('\0' == currentChar)
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.DoubleQuoteValueQuote:
                        if ('"' == currentChar)
                        { parserState = ParserState.DoubleQuoteValue; break; }
                        keyvalue = GetKeyValue(buffer, false);
                        parserState = ParserState.QuotedValueEnd;
                        goto case ParserState.QuotedValueEnd;

                    case ParserState.SingleQuoteValue: // "('([^'\u0000]|'')*')"
                        if ('\'' == currentChar)
                        { parserState = ParserState.SingleQuoteValueQuote; continue; }
                        if ('\0' == currentChar)
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.SingleQuoteValueQuote:
                        if ('\'' == currentChar)
                        { parserState = ParserState.SingleQuoteValue; break; }
                        keyvalue = GetKeyValue(buffer, false);
                        parserState = ParserState.QuotedValueEnd;
                        goto case ParserState.QuotedValueEnd;

                    case ParserState.BraceQuoteValue: // "(\\{([^\\}\u0000]|\\}\\})*\\})"
                        if ('}' == currentChar)
                        { parserState = ParserState.BraceQuoteValueQuote; break; }
                        if ('\0' == currentChar)
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.BraceQuoteValueQuote:
                        if ('}' == currentChar)
                        { parserState = ParserState.BraceQuoteValue; break; }
                        keyvalue = GetKeyValue(buffer, false);
                        parserState = ParserState.QuotedValueEnd;
                        goto case ParserState.QuotedValueEnd;

                    case ParserState.QuotedValueEnd:
                        if (char.IsWhiteSpace(currentChar))
                        { continue; }
                        if (';' == currentChar)
                        { goto ParserExit; }
                        if ('\0' == currentChar)
                        { parserState = ParserState.NullTermination; continue; }
                        throw ADP.ConnectionStringSyntax(startposition);  // unbalanced single quote

                    case ParserState.NullTermination: // [\\s;\u0000]*
                        if ('\0' == currentChar)
                        { continue; }
                        if (char.IsWhiteSpace(currentChar))
                        { continue; }
                        throw ADP.ConnectionStringSyntax(currentPosition);

                    default:
                        throw ADP.InternalError(ADP.InternalErrorCode.InvalidParserState1);
                }
                buffer.Append(currentChar);
            }
        ParserExit:
            switch (parserState)
            {
                case ParserState.Key:
                case ParserState.DoubleQuoteValue:
                case ParserState.SingleQuoteValue:
                case ParserState.BraceQuoteValue:
                    // keyword not found/unbalanced double/single quote
                    throw ADP.ConnectionStringSyntax(startposition);

                case ParserState.KeyEqual:
                    // equal sign at end of line
                    keyname = GetKeyName(buffer);
                    if (string.IsNullOrEmpty(keyname))
                    { throw ADP.ConnectionStringSyntax(startposition); }
                    break;

                case ParserState.UnquotedValue:
                    // unquoted value at end of line
                    keyvalue = GetKeyValue(buffer, true);

                    char tmpChar = keyvalue[keyvalue.Length - 1];
                    if (!useOdbcRules && (('\'' == tmpChar) || ('"' == tmpChar)))
                    {
                        throw ADP.ConnectionStringSyntax(startposition);    // unquoted value must not end in quote, except for odbc
                    }
                    break;

                case ParserState.DoubleQuoteValueQuote:
                case ParserState.SingleQuoteValueQuote:
                case ParserState.BraceQuoteValueQuote:
                case ParserState.QuotedValueEnd:
                    // quoted value at end of line
                    keyvalue = GetKeyValue(buffer, false);
                    break;

                case ParserState.NothingYet:
                case ParserState.KeyEnd:
                case ParserState.NullTermination:
                    // do nothing
                    break;

                default:
                    throw ADP.InternalError(ADP.InternalErrorCode.InvalidParserState2);
            }
            if ((';' == currentChar) && (currentPosition < connectionString.Length))
            {
                currentPosition++;
            }
            return currentPosition;
        }

        private static bool IsValueValidInternal(string keyvalue)
        {
            if (keyvalue != null)
            {
#if DEBUG
                bool compValue = s_connectionStringValidValueRegex.IsMatch(keyvalue);
                Debug.Assert((-1 == keyvalue.IndexOf('\u0000')) == compValue, "IsValueValid mismatch with regex");
#endif
                return (-1 == keyvalue.IndexOf('\u0000'));
            }
            return true;
        }

        private static bool IsKeyNameValid(string keyname)
        {
            if (keyname != null)
            {
#if DEBUG
                bool compValue = s_connectionStringValidKeyRegex.IsMatch(keyname);
                Debug.Assert(((0 < keyname.Length) && (';' != keyname[0]) && !char.IsWhiteSpace(keyname[0]) && (-1 == keyname.IndexOf('\u0000'))) == compValue, "IsValueValid mismatch with regex");
#endif
                return ((0 < keyname.Length) && (';' != keyname[0]) && !char.IsWhiteSpace(keyname[0]) && (-1 == keyname.IndexOf('\u0000')));
            }
            return false;
        }

        private static NameValuePair ParseInternal(
            Dictionary<string, string> parsetable,
            string connectionString,
            bool buildChain,
            IReadOnlyDictionary<string, string> synonyms,
            bool firstKey)
        {
            Debug.Assert(connectionString != null, "null connectionstring");
            StringBuilder buffer = new StringBuilder();
            NameValuePair localKeychain = null, keychain = null;

            #if DEBUG
            try
            {
            #endif
                int nextStartPosition = 0;
                int endPosition = connectionString.Length;
                while (nextStartPosition < endPosition)
                {
                    int startPosition = nextStartPosition;

                    string keyname, keyvalue;
                    nextStartPosition = GetKeyValuePair(connectionString, startPosition, buffer, firstKey, out keyname, out keyvalue);
                    if (string.IsNullOrEmpty(keyname))
                    {
                        // if (nextStartPosition != endPosition) { throw; }
                        break;
                    }

                    DebugTraceKeyValuePair(keyname, keyvalue, synonyms);
                    Debug.Assert(IsKeyNameValid(keyname), "ParseFailure, invalid keyname");
                    Debug.Assert(IsValueValidInternal(keyvalue), "parse failure, invalid keyvalue");

                    string realkeyname = (synonyms is not null) ?
                                         (synonyms.TryGetValue(keyname, out string synonym) ? synonym : null) :
                                          keyname;

                    if (!IsKeyNameValid(realkeyname))
                    {
                        throw ADP.KeywordNotSupported(keyname);
                    }
                    if (!firstKey || !parsetable.ContainsKey(realkeyname))
                    {
                        parsetable[realkeyname] = keyvalue; // last key-value pair wins (or first)
                    }

                    if (localKeychain != null)
                    {
                        localKeychain = localKeychain.Next = new NameValuePair(realkeyname, keyvalue, nextStartPosition - startPosition);
                    }
                    else if (buildChain)
                    { // first time only - don't contain modified chain from UDL file
                        keychain = localKeychain = new NameValuePair(realkeyname, keyvalue, nextStartPosition - startPosition);
                    }
                }
            #if DEBUG
            }
            catch (ArgumentException e)
            {
                ParseComparison(parsetable, connectionString, synonyms, firstKey, e);
                throw;
            }
            ParseComparison(parsetable, connectionString, synonyms, firstKey, null);
            #endif
            
            return keychain;
        }

        internal NameValuePair ReplacePasswordPwd(out string constr, bool fakePassword)
        {
            bool expanded = false;
            int copyPosition = 0;
            NameValuePair head = null, tail = null, next = null;
            StringBuilder builder = new StringBuilder(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; current != null; current = current.Next)
            {
                if (!CompareInsensitiveInvariant(DbConnectionStringKeywords.Password, current.Name) &&
                    !CompareInsensitiveInvariant(DbConnectionStringSynonyms.Pwd, current.Name))
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                    if (fakePassword)
                    {
                        next = new NameValuePair(current.Name, current.Value, current.Length);
                    }
                }
                else if (fakePassword)
                {
                    // replace user password/pwd value with *
                    const string equalstar = "=*;";
                    builder.Append(current.Name).Append(equalstar);
                    next = new NameValuePair(current.Name, "*", current.Name.Length + equalstar.Length);
                    expanded = true;
                }
                else
                {
                    // drop the password/pwd completely in returning for user
                    expanded = true;
                }

                if (fakePassword)
                {
                    if (tail != null)
                    {
                        tail = tail.Next = next;
                    }
                    else
                    {
                        tail = head = next;
                    }
                }
                copyPosition += current.Length;
            }
            Debug.Assert(expanded, "password/pwd was not removed");
            constr = builder.ToString();
            return head;
        }
        
        // SxS notes:
        // * this method queries "DataDirectory" value from the current AppDomain.
        //   This string is used for to replace "!DataDirectory!" values in the connection string, it is not considered as an "exposed resource".
        // * This method uses GetFullPath to validate that root path is valid, the result is not exposed out.
        #if NETFRAMEWORK
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        #endif
        internal static string ExpandDataDirectory(string keyword, string value)
        {
            string fullPath = null;
            if (value != null && value.StartsWith(DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // find the replacement path
                object rootFolderObject = AppDomain.CurrentDomain.GetData("DataDirectory");
                var rootFolderPath = (rootFolderObject as string);
                if (rootFolderObject != null && rootFolderPath == null)
                {
                    throw ADP.InvalidDataDirectory();
                }

                if (string.IsNullOrEmpty(rootFolderPath))
                {
                    rootFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                }

                var fileName = value.Substring(DataDirectory.Length);

                if (Path.IsPathRooted(fileName))
                {
                    fileName = fileName.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
        
        #region NetCore Methods
        #if NET
        
        internal string ExpandAttachDbFileName(string replacementValue)
        {
            int copyPosition = 0;

            StringBuilder builder = new(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; current != null; current = current.Next)
            {
                if (string.Equals(current.Name, DbConnectionStringKeywords.AttachDbFilename, StringComparison.InvariantCultureIgnoreCase))
                {
                    builder.Append($"{current.Name}={replacementValue};");
                }
                else
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                }
                copyPosition += current.Length;
            }

            return builder.ToString();
        }
        
        #endif
        #endregion
        
        #region NetFx Methods
        #if NETFRAMEWORK
        
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

                    return (_parsetable.TryGetValue(DbConnectionStringKeywords.UserId, out value) && !string.IsNullOrEmpty(value)) || 
                           (_parsetable.TryGetValue(DbConnectionStringSynonyms.Uid, out value) && !string.IsNullOrEmpty(value));
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

        internal static void AppendKeyValuePairBuilder(StringBuilder builder, string keyName, string keyValue)
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

            builder.Append(keyName.Replace("=", "=="));
            builder.Append("=");

            if (keyValue != null)
            { // else <keyword>=;
                if (s_connectionStringQuoteValueRegex.IsMatch(keyValue))
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
        #endif
        #endregion
    }
}
