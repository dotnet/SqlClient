// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
        /// Guards ANSI parameter declarations against TDS's byte limit under multibyte encodings.
    /// </summary>
    public class SqlCommandAnsiParameterTests
    {
        /// <summary>
        /// Exercises sp_executesql and prepared command declarations at UTF-8 boundaries.
        /// </summary>
        [Theory]
        [InlineData("\u00e9", 4000, 8000, false, false)]
        [InlineData("\u00e9", 4001, 8000, false, true)]
        [InlineData("\u00e9", 4001, 0, false, true)]
        [InlineData("\u20ac", 2666, 8000, false, false)]
        [InlineData("\u20ac", 2667, 8000, false, true)]
        [InlineData("\U0001f600", 2001, 8000, false, true)]
        [InlineData("a", 8000, 8000, false, false)]
        [InlineData("\u00e9", 4001, 4000, false, false)]
        [InlineData("\u00e9", 4001, -1, false, true)]
        [InlineData("\u00e9", 4001, 8000, true, true)]
        public void BuildParamList_UsesEncodedByteCount(
            string character, int count, int size, bool sqlString, bool expectMax)
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
                string transmittedText = text.Substring(0, parameter.GetActualSize());
                int encodedBytes = Encoding.UTF8.GetByteCount(transmittedText);
                Assert.Contains($"varchar({Math.Max(size, encodedBytes)})", declaration);
            }
            // GetActualSize's ANSI character-count contract must remain intact for serialization.
            Assert.Equal(size > 0 ? Math.Min(size, text.Length) : text.Length,
                parameter.GetActualSize());
        }

        /// <summary>
        /// Promotion uses the connection encoding rather than assuming all ANSI values are UTF-8.
        /// </summary>
        [Fact]
        public void BuildParamList_SingleByteEncoding_DoesNotPromote()
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar, 8000)
            {
                Value = new string('\u00e9', 4001)
            };
            string declaration = BuildDeclaration(parameter, Encoding.GetEncoding(28591));

            Assert.Contains("varchar(8000)", declaration);
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
                Value = new string('a', 4001) + new string('\u00e9', 4001),
                Offset = 4001
            };
            Assert.Contains("varchar(max)", BuildDeclaration(parameter, Encoding.UTF8));
        }

        [Theory]
        [InlineData(4000, 0, false)]
        [InlineData(4001, 0, true)]
        [InlineData(4000, 8000, false)]
        [InlineData(4001, 8000, true)]
        [InlineData(4001, 4000, false)]
        public void BuildParamList_OffsetNearEnd_UsesRemainingCharacters(
            int count, int size, bool expectMax)
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar, size)
            {
                Value = "a" + new string('\u00e9', count),
                Offset = 1
            };

            string declaration = BuildDeclaration(parameter, Encoding.UTF8);

            Assert.Equal(expectMax, parameter.InternalMetaType.IsPlp);
            Assert.Equal(size > 0 ? Math.Min(size, count) : count, parameter.GetActualSize());
            bool declaresMax = declaration.IndexOf("varchar(max)", StringComparison.Ordinal) >= 0;
            Assert.Equal(expectMax, declaresMax);
        }

        [Fact]
        public void BuildParamList_OffsetChanged_RecalculatesTransmittedLength()
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar)
            {
                Value = new string('\u00e9', 4000)
            };
            BuildDeclaration(parameter, Encoding.UTF8);
            Assert.Equal(4000, parameter.GetActualSize());

            parameter.Offset = 1;
            Assert.Equal(3999, parameter.GetActualSize());
            string declaration = BuildDeclaration(parameter, Encoding.UTF8);

            Assert.Equal(3999, parameter.GetActualSize());
            Assert.Contains("varchar(7998)", declaration);
        }

        [Theory]
        [InlineData(4000, 0, false)]
        [InlineData(4001, 0, true)]
        [InlineData(4000, 8000, false)]
        [InlineData(4001, 8000, true)]
        [InlineData(4001, 4000, false)]
        [InlineData(4001, -1, true)]
        public void GetMetadataForTypeInfo_Utf8Value_UsesEncodedByteLength(
            int count, int size, bool expectMax)
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar, size)
            {
                Value = new string('\u00e9', count)
            };
            TdsParser parser = CreateParser(Encoding.UTF8);

            // This is the metadata entry point used when writing Always Encrypted type_info.
            SmiParameterMetaData metadata = parameter.GetMetadataForTypeInfo(parser);

            Assert.Equal(SqlDbType.VarChar, metadata.SqlDbType);
            Assert.Equal(expectMax, parameter.InternalMetaType.IsPlp);
            int expectedBytes = (size > 0 ? Math.Min(size, count) : count) * 2;
            Assert.Equal(expectMax ? -1L : Math.Max(parameter.Size, expectedBytes),
                metadata.MaxLength);
            Assert.Equal(size != 0 ? size : count, parameter.Size);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetMetadataForTypeInfo_SqlCharsWithOffset_UsesTransmittedSlice(bool expectMax)
        {
            int count = expectMax ? 4001 : 4000;
            SqlParameter parameter = new("@p", SqlDbType.VarChar)
            {
                Value = new SqlChars(("a" + new string('\u00e9', count)).ToCharArray()),
                Offset = 1
            };
            TdsParser parser = CreateParser(Encoding.UTF8);

            SmiParameterMetaData metadata = parameter.GetMetadataForTypeInfo(parser);

            Assert.Equal(expectMax ? -1L : 8000L, metadata.MaxLength);
            Assert.Equal(expectMax, parameter.InternalMetaType.IsPlp);
            Assert.Equal(count, parameter.GetActualSize());
        }

        [Theory]
        [InlineData(SqlDbType.Char, false)]
        [InlineData(SqlDbType.Char, true)]
        [InlineData(SqlDbType.VarChar, false)]
        [InlineData(SqlDbType.VarChar, true)]
        public void GetMetadataForTypeInfo_AfterBuildParamList_PreservesMax(
            SqlDbType sqlDbType, bool sqlString)
        {
            string text = new('\u00e9', 4001);
            SqlParameter parameter = new("@p", sqlDbType, 8000)
            {
                Value = sqlString ? new SqlString(text) : (object)text
            };
            string declaration = BuildDeclaration(parameter, Encoding.UTF8);
            TdsParser parser = CreateParser(Encoding.UTF8);

            SmiParameterMetaData metadata = parameter.GetMetadataForTypeInfo(parser);

            Assert.Contains("varchar(max)", declaration);
            Assert.Equal(SqlDbType.VarChar, metadata.SqlDbType);
            Assert.Equal(-1L, metadata.MaxLength);
        }

        [Fact]
        public void GetMetadataForTypeInfo_SingleByteEncoding_DoesNotPromote()
        {
            SqlParameter parameter = new("@p", SqlDbType.VarChar)
            {
                Value = new string('\u00e9', 4001)
            };
            TdsParser parser = CreateParser(Encoding.GetEncoding(28591));

            SmiParameterMetaData metadata = parameter.GetMetadataForTypeInfo(parser);

            Assert.False(parameter.InternalMetaType.IsPlp);
            Assert.Equal(4001L, metadata.MaxLength);
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

            TdsParser parser = CreateParser(Encoding.UTF8);
            SmiParameterMetaData metadata = parameter.GetMetadataForTypeInfo(parser);
            Assert.Equal(8000L, metadata.MaxLength);
        }

        /// <summary>
        /// Calls the SQL declaration builder with a negotiated encoding, without a server.
        /// </summary>
        /// <param name="parameter">The parameter to validate and declare.</param>
        /// <param name="encoding">The encoding negotiated by the connection.</param>
        /// <returns>The SQL declaration emitted for the parameter.</returns>
        private static string BuildDeclaration(SqlParameter parameter, Encoding encoding)
        {
            TdsParser parser = CreateParser(encoding);
            using SqlCommand command = new();
            command.Parameters.Add(parameter);
            MethodInfo buildParamList = typeof(SqlCommand).GetMethod(
                "BuildParamList", BindingFlags.Instance | BindingFlags.NonPublic)!;
            string declaration = (string)buildParamList.Invoke(
                command, new object[] { parser, command.Parameters, false })!;

            return declaration;
        }

        private static TdsParser CreateParser(Encoding encoding)
        {
            TdsParser parser = new(false, false);
            FieldInfo encodingField = typeof(TdsParser).GetField(
                "_defaultEncoding", BindingFlags.Instance | BindingFlags.NonPublic)!;
            encodingField.SetValue(parser, encoding);

            return parser;
        }
    }
}
