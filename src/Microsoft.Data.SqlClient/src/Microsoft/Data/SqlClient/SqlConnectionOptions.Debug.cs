// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlConnectionOptions
    {
        #if DEBUG
        private static readonly Regex s_connectionStringValidKeyRegex = new Regex(
            // Key must not start with semi-colon or space, contain non-visible characters, or end with space.
            "^(?![;\\s])[^\\p{Cc}]+(?<!\\s)$",
            RegexOptions.Compiled);
        private static readonly Regex s_connectionStringValidValueRegex = new Regex(
            // Value must not contain embedded null.
            "^[^\u0000]*$",
            RegexOptions.Compiled);
        private static readonly Regex s_connectionStringRegex = new Regex(
            // Leading whitespace and extra semicolons.
            "([\\s;]*"
            // Key does not start with space or semicolon.
            + "(?![\\s;])"
            // Allow any visible character for key name (except for '=', which must be quoted as '==').
            + "(?<key>([^=\\s\\p{Cc}]|\\s+[^=\\s\\p{Cc}]|\\s+==|==)+)"
            // The equals sign divides the key and value parts.
            + "\\s*=(?!=)\\s*"
            + "(?<value>"
            // Double-quoted string: " must be quoted as "".
            + "(\"([^\"\u0000]|\"\")*\")"
            + "|"
            // Single-quoted string: ' must be quoted as ''.
            + "('([^'\u0000]|'')*')"
            + "|"
            // Unquoted value must not start with " or ' or space. Would also like =, but too late to change.
            + "((?![\"'\\s])"
            // Control characters must be quoted.
            + "([^;\\s\\p{Cc}]|\\s+[^;\\s\\p{Cc}])*"
            // Unquoted value must not end with " or '.
            + "(?<![\"']))"
            // Whitespace after value, up to semicolon or end-of-line.
            + ")(\\s*)(;|[\u0000\\s]*$)"
            // Repeat the key-value pair.
            + ")*"
            // Trailing whitespace/semicolons (DataSourceLocator). Embedded nulls are only allowed at the end.
            + "[\\s;]*[\u0000\\s]*",
            RegexOptions.ExplicitCapture | RegexOptions.Compiled);
        #endif

        [Conditional("DEBUG")]
        private static void DebugTraceKeyValuePair(string keyname, string keyvalue, IReadOnlyDictionary<string, string> synonyms)
        {
            if (SqlClientEventSource.Log.IsAdvancedTraceOn())
            {
                Debug.Assert(string.Equals(keyname, keyname?.ToLower(), StringComparison.InvariantCulture), "missing ToLower");
                string realkeyname = synonyms != null ? synonyms[keyname] : keyname;

                if (!CompareInsensitiveInvariant(DbConnectionStringKeywords.Password, realkeyname) &&
                    !CompareInsensitiveInvariant(DbConnectionStringSynonyms.Pwd, realkeyname))
                {
                    // don't trace passwords ever!
                    if (keyvalue != null)
                    {
                        SqlClientEventSource.Log.AdvancedTraceEvent("<comm.SqlConnectionOptions|INFO|ADV> KeyName='{0}', KeyValue='{1}'", keyname, keyvalue);
                    }
                    else
                    {
                        SqlClientEventSource.Log.AdvancedTraceEvent("<comm.SqlConnectionOptions|INFO|ADV> KeyName='{0}'", keyname);
                    }
                }
            }
        }

        #if DEBUG
        private static void ParseComparison(
            Dictionary<string, string> parseTable,
            string connectionString,
            IReadOnlyDictionary<string, string> synonyms,
            Exception e)
        {
            try
            {
                var parsedValues = SplitConnectionString(connectionString, synonyms);
                foreach (var parsedValue in parsedValues)
                {
                    string key = parsedValue.Key;
                    string value1 = parsedValue.Value;

                    bool parseTableContainsKey = parseTable.TryGetValue(key, out string value2);
                    Debug.Assert(parseTableContainsKey, $"{nameof(ParseInternal)} code vs. regex mismatch keyname <{key}>");
                    Debug.Assert(value1 == value2, $"{nameof(ParseInternal)} code vs. regex mismatch keyvalue <{value1}> <{value2}>");
                }
            }
            catch (ArgumentException f)
            {
                if (e != null)
                {
                    string msg1 = e.Message;
                    string msg2 = f.Message;

                    const string KeywordNotSupportedMessagePrefix = "Keyword not supported:";
                    const string WrongFormatMessagePrefix = "Format of the initialization string";
                    bool isEquivalent = msg1 == msg2;
                    if (!isEquivalent)
                    {
                        // We also accept cases were Regex parser (debug only) reports "wrong format" and
                        // retail parsing code reports format exception in different location or "keyword not supported"
                        if (msg2.StartsWith(WrongFormatMessagePrefix, StringComparison.Ordinal))
                        {
                            if (msg1.StartsWith(KeywordNotSupportedMessagePrefix, StringComparison.Ordinal) ||
                                msg1.StartsWith(WrongFormatMessagePrefix, StringComparison.Ordinal))
                            {
                                isEquivalent = true;
                            }
                        }
                    }

                    Debug.Assert(isEquivalent, "ParseInternal code vs regex message mismatch: <" + msg1 + "> <" + msg2 + ">");
                }
                else
                {
                    Debug.Fail("ParseInternal code vs regex throw mismatch " + f.Message);
                }

                e = null;
            }

            if (e != null)
            {
                Debug.Fail("ParseInternal code threw exception vs regex mismatch");
            }
        }
        #endif

        #if DEBUG
        private static Dictionary<string, string> SplitConnectionString(
            string connectionString,
            IReadOnlyDictionary<string, string> synonyms)
        {
            var parseTable = new Dictionary<string, string>();
            Regex parser = s_connectionStringRegex;

            const int KeyIndex = 1, ValueIndex = 2;
            Debug.Assert(KeyIndex == parser.GroupNumberFromName("key"), "wrong key index");
            Debug.Assert(ValueIndex == parser.GroupNumberFromName("value"), "wrong value index");

            if (connectionString != null)
            {
                Match match = parser.Match(connectionString);
                if (!match.Success || match.Length != connectionString.Length)
                {
                    throw ADP.ConnectionStringSyntax(match.Length);
                }

                int indexValue = 0;
                CaptureCollection keyValues = match.Groups[ValueIndex].Captures;
                foreach (Capture keypair in match.Groups[KeyIndex].Captures)
                {
                    string keyName = keypair.Value.Replace("==", "=").ToLower(CultureInfo.InvariantCulture);
                    string keyValue = keyValues[indexValue++].Value;
                    if (0 < keyValue.Length)
                    {
                        switch (keyValue[0])
                        {
                            case '\"':
                                keyValue = keyValue.Substring(1, keyValue.Length - 2).Replace("\"\"", "\"");
                                break;
                            case '\'':
                                keyValue = keyValue.Substring(1, keyValue.Length - 2).Replace("\'\'", "\'");
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        keyValue = null;
                    }

                    DebugTraceKeyValuePair(keyName, keyValue, synonyms);
                    string realKeyName = synonyms != null
                        ? synonyms.TryGetValue(keyName, out string synonym) ? synonym : null
                        : keyName;

                    if (!IsKeyNameValid(realKeyName))
                    {
                        throw ADP.KeywordNotSupported(keyName);
                    }

                    // Last key-value pair wins
                    parseTable[realKeyName] = keyValue;
                }
            }

            return parseTable;
        }
        #endif
    }
}
