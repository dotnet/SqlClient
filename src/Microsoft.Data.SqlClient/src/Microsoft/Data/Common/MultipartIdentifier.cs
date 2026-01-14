// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;

namespace Microsoft.Data.Common
{
    internal class MultipartIdentifier
    {
        private const char IdentifierSeparator = '.';
        // The indexes of identifier start and end characters in these strings need to
        // align 1-to-1. An identifier which starts with [ can only end with ].
        private const string IdentifierStartCharacters = "[\"";
        private const string IdentifierEndCharacters = "]\"";

        private const int MaxParts = 4;
        internal const int ServerIndex = 0;
        internal const int CatalogIndex = 1;
        internal const int SchemaIndex = 2;
        internal const int TableIndex = 3;

        internal static string[] ParseMultipartIdentifier(string name, string property, bool ThrowOnEmptyMultipartName)
        {
            return ParseMultipartIdentifier(name, MaxParts, property, ThrowOnEmptyMultipartName);
        }

        private enum MPIState
        {
            MPI_Value,
            MPI_ParseNonQuote,
            MPI_LookForSeparator,
            MPI_LookForNextCharOrSeparator,
            MPI_ParseQuote,
            MPI_RightQuote,
        }

        private static void IncrementStringCount(string name, string[] ary, ref int position, string property)
        {
            ++position;
            int limit = ary.Length;
            if (position >= limit)
            {
                throw ADP.InvalidMultipartNameToManyParts(property, name, limit);
            }
            ary[position] = string.Empty;
        }

        private static bool ContainsChar(string str, char ch) =>
#if NET
            str.Contains(ch);
#else
            str.IndexOf(ch) != -1;
#endif

        /// <summary>
        /// Core function for parsing the multipart identifier string.
        /// </summary>
        /// <param name="name">String to parse.</param>
        /// <param name="limit">Number of names to parse out.</param>
        /// <param name="property">Name of the property containing the multipart identifier.</param>
        /// <param name="ThrowOnEmptyMultipartName">If <c>true</c>, throw <see cref="ADP.InvalidMultipartName"/> if the name is whitespace.</param>
        /// <returns>An array of <paramref name="limit"/> strings containing the various parts in the identifier.</returns>
        internal static string[] ParseMultipartIdentifier(string name, int limit, string property, bool ThrowOnEmptyMultipartName)
        {
            Debug.Assert(limit >= 0 && limit <= MaxParts);
            Debug.Assert(!ContainsChar(IdentifierStartCharacters, IdentifierSeparator));
            Debug.Assert(!ContainsChar(IdentifierEndCharacters, IdentifierSeparator));
            Debug.Assert(IdentifierStartCharacters.Length == IdentifierEndCharacters.Length);

            string[] parsedNames = new string[limit];   // return string array                     
            int stringCount = 0;                        // index of current string in the buffer
            MPIState state = MPIState.MPI_Value;        // Initialize the starting state

            StringBuilder sb = new StringBuilder(name.Length); // String buffer to hold the string being currently built, init the string builder so it will never be resized
            StringBuilder whitespaceSB = null;                  // String buffer to hold whitespace used when parsing nonquoted strings  'a b .  c d' = 'a b' and 'c d'
            char rightQuoteChar = ' ';                          // Right quote character to use given the left quote character found.
            for (int index = 0; index < name.Length; ++index)
            {
                char testchar = name[index];
                switch (state)
                {
                    case MPIState.MPI_Value:
                        {
                            int quoteIndex;
                            if (char.IsWhiteSpace(testchar))
                            {
                                // Skip whitespace
                                continue;
                            }
                            else if (testchar == IdentifierSeparator)
                            {
                                // If we found a separator, no string was found, initialize the string we are parsing to Empty and the next one to Empty.
                                // This is NOT a redundant setting of string.Empty it solves the case where we are parsing ".foo" and we should be returning null, null, empty, foo
                                parsedNames[stringCount] = string.Empty;
                                IncrementStringCount(name, parsedNames, ref stringCount, property);
                            }
                            else if (-1 != (quoteIndex = IdentifierStartCharacters.IndexOf(testchar)))
                            {
                                // If we are a left quote, record the corresponding right quote for the left quote
                                rightQuoteChar = IdentifierEndCharacters[quoteIndex];
                                sb.Length = 0;
                                state = MPIState.MPI_ParseQuote;
                            }
                            else if (ContainsChar(IdentifierEndCharacters, testchar))
                            {
                                // If we shouldn't see a right quote
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, name);
                            }
                            else
                            {
                                sb.Length = 0;
                                sb.Append(testchar);
                                state = MPIState.MPI_ParseNonQuote;
                            }
                            break;
                        }

                    case MPIState.MPI_ParseNonQuote:
                        {
                            if (testchar == IdentifierSeparator)
                            {
                                // Set the currently parsed string
                                parsedNames[stringCount] = sb.ToString();
                                IncrementStringCount(name, parsedNames, ref stringCount, property);
                                state = MPIState.MPI_Value;
                            }
                            else if (ContainsChar(IdentifierEndCharacters, testchar))
                            {
                                // Quotes are not valid inside a non-quoted name
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, name);
                            }
                            else if (ContainsChar(IdentifierStartCharacters, testchar))
                            {
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, name);
                            }
                            else if (char.IsWhiteSpace(testchar))
                            {
                                // If it is whitespace, set the currently parsed string
                                parsedNames[stringCount] = sb.ToString();
                                if (whitespaceSB == null)
                                {
                                    whitespaceSB = new StringBuilder();
                                }
                                // Start to record the whitespace. If we are parsing a name like "foo bar" we should return "foo bar"
                                whitespaceSB.Length = 0;
                                whitespaceSB.Append(testchar);
                                state = MPIState.MPI_LookForNextCharOrSeparator;
                            }
                            else
                            {
                                sb.Append(testchar);
                            }
                            break;
                        }

                    case MPIState.MPI_LookForNextCharOrSeparator:
                        {
                            if (!char.IsWhiteSpace(testchar))
                            {
                                if (testchar == IdentifierSeparator)
                                {
                                    IncrementStringCount(name, parsedNames, ref stringCount, property);
                                    state = MPIState.MPI_Value;
                                }
                                else
                                {
                                    sb.Append(whitespaceSB);
                                    sb.Append(testchar);
                                    // Need to set the name here in case the string ends here.
                                    parsedNames[stringCount] = sb.ToString();
                                    state = MPIState.MPI_ParseNonQuote;
                                }
                            }
                            else
                            {
                                whitespaceSB.Append(testchar);
                            }
                            break;
                        }

                    case MPIState.MPI_ParseQuote:
                        {
                            // If we are on a right quote, see if we are escaping the right quote or ending the quoted string
                            if (testchar == rightQuoteChar)
                            {
                                state = MPIState.MPI_RightQuote;
                            }
                            else
                            {
                                sb.Append(testchar);
                            }
                            break;
                        }

                    case MPIState.MPI_RightQuote:
                        {
                            if (testchar == rightQuoteChar)
                            {
                                // If the next char is another right quote then we were escaping the right quote
                                sb.Append(testchar);
                                state = MPIState.MPI_ParseQuote;
                            }
                            else if (testchar == IdentifierSeparator)
                            {
                                // If it's a separator then record what we've parsed
                                parsedNames[stringCount] = sb.ToString();
                                IncrementStringCount(name, parsedNames, ref stringCount, property);
                                state = MPIState.MPI_Value;
                            }
                            else if (!char.IsWhiteSpace(testchar))
                            {
                                // If it is not whitespace then we have problems
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, name);
                            }
                            else
                            {
                                // It is a whitespace character so the following char should be whitespace, separator, or end of string. Anything else is bad
                                parsedNames[stringCount] = sb.ToString();
                                state = MPIState.MPI_LookForSeparator;
                            }
                            break;
                        }

                    case MPIState.MPI_LookForSeparator:
                        {
                            if (!char.IsWhiteSpace(testchar))
                            {
                                if (testchar == IdentifierSeparator)
                                {
                                    // If it is a separator 
                                    IncrementStringCount(name, parsedNames, ref stringCount, property);
                                    state = MPIState.MPI_Value;
                                }
                                else
                                {
                                    // Otherwise not a separator
                                    throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, name);
                                }
                            }
                            break;
                        }
                }
            }

            // Resolve final states after parsing the string            
            switch (state)
            {
                // These states require no extra action
                case MPIState.MPI_Value:
                case MPIState.MPI_LookForSeparator:
                case MPIState.MPI_LookForNextCharOrSeparator:
                    break;

                // Dump whatever was parsed
                case MPIState.MPI_ParseNonQuote:
                case MPIState.MPI_RightQuote:
                    parsedNames[stringCount] = sb.ToString();
                    break;

                // Invalid Ending States
                case MPIState.MPI_ParseQuote:
                default:
                    throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, name);
            }

            if (parsedNames[0] == null)
            {
                // Name is entirely made up of whitespace
                if (ThrowOnEmptyMultipartName)
                {
                    throw ADP.InvalidMultipartName(property, name);
                }
            }
            else
            {
                // Shuffle the parsed name, from left justification to right justification, i.e. [a][b][null][null] goes to [null][null][a][b]
                int offset = limit - stringCount - 1;
                if (offset > 0)
                {
                    for (int x = limit - 1; x >= offset; --x)
                    {
                        parsedNames[x] = parsedNames[x - offset];
                        parsedNames[x - offset] = null;
                    }
                }
            }
            return parsedNames;
        }
    }
}
