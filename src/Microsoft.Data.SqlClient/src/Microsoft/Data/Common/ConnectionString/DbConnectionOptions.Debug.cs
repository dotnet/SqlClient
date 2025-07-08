// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.Common.ConnectionString
{
    internal partial class DbConnectionOptions
    {
        #if DEBUG
        private const string ConnectionStringPattern =                     // may not contain embedded null except trailing last value
            "([\\s;]*"                                                 // leading whitespace and extra semicolons
            + "(?![\\s;])"                                             // key does not start with space or semicolon
            + "(?<key>([^=\\s\\p{Cc}]|\\s+[^=\\s\\p{Cc}]|\\s+==|==)+)" // allow any visible character for keyname except '=' which must quoted as '=='
            + "\\s*=(?!=)\\s*"                                         // the equal sign divides the key and value parts
            + "(?<value>"
            + "(\"([^\"\u0000]|\"\")*\")"                              // double-quoted string, " must be quoted as ""
            + "|"
            + "('([^'\u0000]|'')*')"                                   // single-quoted string, ' must be quoted as ''
            + "|"
            + "((?![\"'\\s])"                                          // unquoted value must not start with " or ' or space, would also like = but too late to change
            + "([^;\\s\\p{Cc}]|\\s+[^;\\s\\p{Cc}])*"                   // control characters must be quoted
            + "(?<![\"']))"                                            // unquoted value must not stop with " or '
            + ")(\\s*)(;|[\u0000\\s]*$)"                               // whitespace after value up to semicolon or end-of-line
            + ")*"                                                     // repeat the key-value pair
            + "[\\s;]*[\u0000\\s]*";                                   // trailing whitespace/semicolons (DataSourceLocator), embedded nulls are allowed only in the end
        private static readonly Regex ConnectionStringRegex = new Regex(ConnectionStringPattern, RegexOptions.ExplicitCapture | RegexOptions.Compiled);
        
        private const string ConnectionStringPatternOdbc =             // may not contain embedded null except trailing last value
            "([\\s;]*"                                                 // leading whitespace and extra semicolons
            + "(?![\\s;])"                                             // key does not start with space or semicolon
            + "(?<key>([^=\\s\\p{Cc}]|\\s+[^=\\s\\p{Cc}])+)"           // allow any visible character for keyname except '='
            + "\\s*=\\s*"                                              // the equal sign divides the key and value parts
            + "(?<value>"
            + "(\\{([^\\}\u0000]|\\}\\})*\\})"                         // quoted string, starts with { and ends with }
            + "|"
            + "((?![\\{\\s])"                                          // unquoted value must not start with { or space, would also like = but too late to change
            + "([^;\\s\\p{Cc}]|\\s+[^;\\s\\p{Cc}])*"                   // control characters must be quoted
            + ")"                                                      // although the spec does not allow {} embedded within a value, the retail code does.
            + ")(\\s*)(;|[\u0000\\s]*$)"                               // whitespace after value up to semicolon or end-of-line
            + ")*"                                                     // repeat the key-value pair
            + "[\\s;]*[\u0000\\s]*";                                   // trailing whitespace/semicolons (DataSourceLocator), embedded nulls are allowed only in the end
        private static readonly Regex ConnectionStringRegexOdbc = new Regex(ConnectionStringPatternOdbc, RegexOptions.ExplicitCapture | RegexOptions.Compiled);
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
                        SqlClientEventSource.Log.AdvancedTraceEvent("<comm.DbConnectionOptions|INFO|ADV> KeyName='{0}', KeyValue='{1}'", keyname, keyvalue);
                    }
                    else
                    {
                        SqlClientEventSource.Log.AdvancedTraceEvent("<comm.DbConnectionOptions|INFO|ADV> KeyName='{0}'", keyname);
                    }
                }
            }
        }
        
        #if DEBUG
        private static void ParseComparison(
            Dictionary<string, string> parseTable,
            string connectionString,
            IReadOnlyDictionary<string, string> synonyms,
            bool firstKey,
            Exception e)
        {
            try
            {
                var parsedValues = SplitConnectionString(connectionString, synonyms, firstKey);
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
            IReadOnlyDictionary<string, string> synonyms,
            bool firstKey)
        {
            var parseTable = new Dictionary<string, string>();
            Regex parser = firstKey ? ConnectionStringRegexOdbc : ConnectionStringRegex;

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
                    string keyName = (firstKey ? keypair.Value : keypair.Value.Replace("==", "=")).ToLower(CultureInfo.InvariantCulture);
                    string keyValue = keyValues[indexValue++].Value;
                    if (0 < keyValue.Length)
                    {
                        if (!firstKey)
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
                    
                    if (!firstKey || !parseTable.ContainsKey(realKeyName))
                    {
                        parseTable[realKeyName] = keyValue; // last key-value pair wins (or first)
                    }
                }
            }
            
            return parseTable;
        }
        #endif
    }
}
