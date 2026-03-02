// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;

#nullable enable

namespace Microsoft.Data.Common
{
    internal class MultipartIdentifier
    {
        private const char IdentifierSeparator = '.';
        // The indexes of identifier start and end characters in these strings need to
        // align 1-to-1. An identifier which starts with [ can only end with ]. As such,
        // both of the below constants must be the same length.
        // Separately, neither constant may contain the identifier separator.
        private const string IdentifierStartCharacters = "[\"";
        private const string IdentifierEndCharacters = "]\"";

        private const int MaxParts = 4;
        internal const int ServerIndex = 0;
        internal const int CatalogIndex = 1;
        internal const int SchemaIndex = 2;
        internal const int TableIndex = 3;

        private enum MPIState
        {
            MPI_Value,
            MPI_ParseNonQuote,
            MPI_LookForSeparator,
            MPI_LookForNextCharOrSeparator,
            MPI_ParseQuote,
            MPI_RightQuote,
        }

        private static void IncrementStringCount(string identifier, string?[] ary, ref int position, string property)
        {
            ++position;
            int limit = ary.Length;
            if (position >= limit)
            {
                throw ADP.InvalidMultipartNameToManyParts(property, identifier, limit);
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
        /// <param name="identifier">String to parse.</param>
        /// <param name="property">Name of the property containing the multipart identifier. If an exception is thrown, its message will include this property name.</param>
        /// <param name="throwOnEmptyMultipartIdentifier">If <c>true</c>, throw <see cref="ADP.InvalidMultipartName"/> if the multipart identifier is whitespace.</param>
        /// <param name="limit">Number of parts to parse out. Defaults to four (to allow for an identifier formatted as [server].[database].[schema].[object].)</param>
        /// <returns>An array of <paramref name="limit"/> strings containing the various parts in the identifier.</returns>
        internal static string?[] ParseMultipartIdentifier(string identifier, string property, bool throwOnEmptyMultipartIdentifier, int limit = MaxParts)
        {
            Debug.Assert(limit > 0 && limit <= MaxParts);

            string?[] parts = new string?[limit];   // return string array                     
            int stringCount = 0;                        // index of current string in the buffer
            MPIState state = MPIState.MPI_Value;        // Initialize the starting state

            StringBuilder sb = new StringBuilder(identifier.Length); // String buffer to hold the string being currently built, init the string builder so it will never be resized
            StringBuilder? whitespaceSB = null;                       // String buffer to hold whitespace used when parsing nonquoted strings  'a b .  c d' = 'a b' and 'c d'
            char rightQuoteChar = ' ';                               // Right quote character to use given the left quote character found.
            for (int index = 0; index < identifier.Length; ++index)
            {
                char testchar = identifier[index];
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
                                // This is NOT a redundant setting of string.Empty. It solves the case where we are parsing ".foo" and we should be returning null, null, empty, foo
                                parts[stringCount] = string.Empty;
                                IncrementStringCount(identifier, parts, ref stringCount, property);
                            }
                            else if ((quoteIndex = IdentifierStartCharacters.IndexOf(testchar)) != -1)
                            {
                                // If we are a left quote, record the corresponding right quote for the left quote
                                rightQuoteChar = IdentifierEndCharacters[quoteIndex];
                                sb.Length = 0;
                                state = MPIState.MPI_ParseQuote;
                            }
                            else if (ContainsChar(IdentifierEndCharacters, testchar))
                            {
                                // If we shouldn't see a right quote
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, identifier);
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
                                parts[stringCount] = sb.ToString();
                                IncrementStringCount(identifier, parts, ref stringCount, property);
                                state = MPIState.MPI_Value;
                            }
                            else if (ContainsChar(IdentifierEndCharacters, testchar))
                            {
                                // Quotes are not valid inside a non-quoted identifier
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, identifier);
                            }
                            else if (ContainsChar(IdentifierStartCharacters, testchar))
                            {
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, identifier);
                            }
                            else if (char.IsWhiteSpace(testchar))
                            {
                                // If it is whitespace, set the currently parsed string
                                parts[stringCount] = sb.ToString();

                                whitespaceSB ??= new StringBuilder();
                                // Start to record the whitespace. If we are parsing an identifier like "foo bar" we should return "foo bar"
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
                            if (testchar == IdentifierSeparator)
                            {
                                IncrementStringCount(identifier, parts, ref stringCount, property);
                                state = MPIState.MPI_Value;
                            }
                            else if (!char.IsWhiteSpace(testchar))
                            {
                                sb.Append(whitespaceSB);
                                sb.Append(testchar);
                                // Need to set the identifier part here in case the string ends here.
                                parts[stringCount] = sb.ToString();
                                state = MPIState.MPI_ParseNonQuote;
                            }
                            else
                            {
                                whitespaceSB ??= new StringBuilder();
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
                                parts[stringCount] = sb.ToString();
                                IncrementStringCount(identifier, parts, ref stringCount, property);
                                state = MPIState.MPI_Value;
                            }
                            else if (!char.IsWhiteSpace(testchar))
                            {
                                // If it is not whitespace then we have problems
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, identifier);
                            }
                            else
                            {
                                // It is a whitespace character so the following char should be whitespace, separator, or end of string. Anything else is bad
                                parts[stringCount] = sb.ToString();
                                state = MPIState.MPI_LookForSeparator;
                            }
                            break;
                        }

                    case MPIState.MPI_LookForSeparator:
                        {
                            if (testchar == IdentifierSeparator)
                            {
                                // If it is a separator 
                                IncrementStringCount(identifier, parts, ref stringCount, property);
                                state = MPIState.MPI_Value;
                            }
                            else if (!char.IsWhiteSpace(testchar))
                            {
                                // Otherwise not a separator
                                throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, identifier);
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
                    parts[stringCount] = sb.ToString();
                    break;

                // Invalid Ending States
                case MPIState.MPI_ParseQuote:
                default:
                    throw ADP.InvalidMultipartNameIncorrectUsageOfQuotes(property, identifier);
            }

            if (parts[0] == null)
            {
                // Identifier is entirely made up of whitespace
                if (throwOnEmptyMultipartIdentifier)
                {
                    throw ADP.InvalidMultipartName(property, identifier);
                }
            }
            else
            {
                // Shuffle the identifier parts, from left justification to right justification, i.e. [a][b][null][null] goes to [null][null][a][b]
                int offset = limit - stringCount - 1;
                if (offset > 0)
                {
                    for (int x = limit - 1; x >= offset; --x)
                    {
                        parts[x] = parts[x - offset];
                        parts[x - offset] = null;
                    }
                }
            }
            return parts;
        }
    }
}
