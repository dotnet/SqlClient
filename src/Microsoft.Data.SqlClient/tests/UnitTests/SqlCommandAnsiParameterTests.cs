// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Reflection;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Guards ANSI parameter declarations against exceeding TDS's byte limit under multibyte encodings.
    /// </summary>
    public class SqlCommandAnsiParameterTests
    {
        /// <summary>
        /// Exercises the declaration path used by sp_executesql and prepared commands at UTF-8 boundaries.
        /// </summary>
        [Theory]
        [InlineData("é", 4000, 8000, false, false)]
        [InlineData("é", 4001, 8000, false, true)]
        [InlineData("é", 4001, 0, false, true)]
        [InlineData("€", 2666, 8000, false, false)]
        [InlineData("€", 2667, 8000, false, true)]
        [InlineData("😀", 2001, 8000, false, true)]
        [InlineData("a", 8000, 8000, false, false)]
        [InlineData("é", 4001, 4000, false, false)]
        [InlineData("é", 4001, -1, false, true)]
        [InlineData("é", 4001, 8000, true, true)]
        public void BuildParamList_UsesEncodedByteCount(string character, int count, int size, bool sqlString, bool expectMax)
        {
            string text = string.Concat(System.Linq.Enumerable.Repeat(character, count));
            SqlParameter parameter = new("@p", SqlDbType.VarChar, size)
            {
                Value = sqlString ? new SqlString(text) : (object)text
            };
            string declaration = BuildDeclaration(parameter, Encoding.UTF8);

            Assert.Equal(expectMax, parameter.InternalMetaType.IsPlp);
            if (expectMax)
            {
                Assert.Contains("varchar(max)", declaration);
            }
            else
            {
                Assert.DoesNotContain("(max)", declaration);
                int encodedBytes = Encoding.UTF8.GetByteCount(text.Substring(0, parameter.GetActualSize()));
                Assert.Contains($"varchar({Math.Max(size, encodedBytes)})", declaration);
            }
            // GetActualSize's ANSI character-count contract must remain intact for serialization.
            Assert.Equal(size > 0 ? Math.Min(size, text.Length) : text.Length, parameter.GetActualSize());
        }

        /// <summary>
        /// Promotion uses the connection encoding rather than assuming all ANSI values are UTF-8.
        /// </summary>
        [Fact]
        public void BuildParamList_SingleByteEncoding_DoesNotPromote()
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar, 8000) { Value = new string('é', 4001) };
            Assert.Contains("varchar(8000)", BuildDeclaration(parameter, Encoding.GetEncoding(28591)));
            Assert.False(parameter.InternalMetaType.IsPlp);
        }

        /// <summary>
        /// Truncation and offset select which characters determine the encoded byte count.
        /// </summary>
        [Fact]
        public void BuildParamList_Offset_UsesTransmittedSlice()
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar, 4001)
            {
                Value = new string('a', 4001) + new string('é', 4001),
                Offset = 4001
            };
            Assert.Contains("varchar(max)", BuildDeclaration(parameter, Encoding.UTF8));
        }

        /// <summary>
        /// Null parameters must not require conversion to a string for the byte-count check.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildParamList_NullValue_DoesNotPromote(bool sqlNull)
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar, 8000)
            {
                Value = sqlNull ? SqlString.Null : DBNull.Value
            };
            Assert.Contains("varchar(8000)", BuildDeclaration(parameter, Encoding.UTF8));
            Assert.False(parameter.InternalMetaType.IsPlp);
        }

        /// <summary>
        /// Calls the real SQL declaration builder with a connection's negotiated encoding, without a server.
        /// </summary>
        /// <param name="parameter">The parameter to validate and declare.</param>
        /// <param name="encoding">The encoding negotiated by the connection.</param>
        /// <returns>The SQL declaration emitted for the parameter.</returns>
        private static string BuildDeclaration(SqlParameter parameter, Encoding encoding)
        {
            TdsParser parser = new(false, false);
            typeof(TdsParser).GetField("_defaultEncoding", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parser, encoding);
            using SqlCommand command = new();
            command.Parameters.Add(parameter);
            return (string)typeof(SqlCommand).GetMethod("BuildParamList", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(command, new object[] { parser, command.Parameters, false })!;
        }
    }
}
