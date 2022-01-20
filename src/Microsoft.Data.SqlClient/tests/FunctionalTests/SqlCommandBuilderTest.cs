// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlCommandBuilderTest
    {
        [Fact]
        public void CatalogLocationTest()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            Assert.Equal(CatalogLocation.Start, cb.CatalogLocation);
            cb.CatalogLocation = CatalogLocation.Start;
            Assert.Equal(CatalogLocation.Start, cb.CatalogLocation);
        }

        [Fact]
        public void CatalogLocation_Value_Invalid()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            try
            {
                cb.CatalogLocation = (CatalogLocation)666;
            }
            catch (ArgumentException ex)
            {
                // The only acceptable value for the property
                // 'CatalogLocation' is 'Start'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'CatalogLocation'") != -1);
                Assert.True(ex.Message.IndexOf("'Start'") != -1);
                Assert.Null(ex.ParamName);
            }
            Assert.Equal(CatalogLocation.Start, cb.CatalogLocation);

            try
            {
                cb.CatalogLocation = CatalogLocation.End;
            }
            catch (ArgumentException ex)
            {
                // The only acceptable value for the property
                // 'CatalogLocation' is 'Start'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'CatalogLocation'") != -1);
                Assert.True(ex.Message.IndexOf("'Start'") != -1);
                Assert.Null(ex.ParamName);
            }
            Assert.Equal(CatalogLocation.Start, cb.CatalogLocation);
        }

        [Fact]
        public void CatalogSeparator()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            Assert.Equal(".", cb.CatalogSeparator);
        }

        [Theory]
        [InlineData("x")]
        [InlineData("'")]
        [InlineData("[x")]
        [InlineData("")]
        [InlineData(null)]
        public void CatalogSeparator_Value_Invalid(string separator)
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();

            try
            {
                cb.CatalogSeparator = separator;
            }
            catch (ArgumentException ex)
            {
                // The acceptable value for the property
                // 'CatalogSeparator' is '.'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'CatalogSeparator'") != -1);
                Assert.True(ex.Message.IndexOf("'.'") != -1);
                Assert.Null(ex.ParamName);
            }
        }

        [Fact]
        public void ConflictOptionTest()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            Assert.Equal(ConflictOption.CompareAllSearchableValues, cb.ConflictOption);
            cb.ConflictOption = ConflictOption.CompareRowVersion;
            Assert.Equal(ConflictOption.CompareRowVersion, cb.ConflictOption);
        }

        [Fact]
        public void ConflictOption_Value_Invalid()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            cb.ConflictOption = ConflictOption.CompareRowVersion;
            try
            {
                cb.ConflictOption = (ConflictOption)666;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // The ConflictOption enumeration value, 666, is invalid
                Assert.Equal(typeof(ArgumentOutOfRangeException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("ConflictOption") != -1);
                Assert.True(ex.Message.IndexOf("666") != -1);
                Assert.Equal("ConflictOption", ex.ParamName);
            }
            Assert.Equal(ConflictOption.CompareRowVersion, cb.ConflictOption);
        }

        [Fact]
        public void QuoteIdentifier()
        {
            SqlCommandBuilder cb;

            cb = new SqlCommandBuilder();
            Assert.Equal("[dotnet]", cb.QuoteIdentifier("dotnet"));
            Assert.Equal("[]", cb.QuoteIdentifier(string.Empty));
            Assert.Equal("[Z]", cb.QuoteIdentifier("Z"));
            Assert.Equal("[[]", cb.QuoteIdentifier("["));
            Assert.Equal("[A[C]", cb.QuoteIdentifier("A[C"));
            Assert.Equal("[]]]", cb.QuoteIdentifier("]"));
            Assert.Equal("[A]]C]", cb.QuoteIdentifier("A]C"));
            Assert.Equal("[[]]]", cb.QuoteIdentifier("[]"));
            Assert.Equal("[A[]]C]", cb.QuoteIdentifier("A[]C"));

            cb = new SqlCommandBuilder();
            cb.QuotePrefix = "\"";
            cb.QuoteSuffix = "\"";
            Assert.Equal("\"dotnet\"", cb.QuoteIdentifier("dotnet"));
            Assert.Equal("\"\"", cb.QuoteIdentifier(string.Empty));
            Assert.Equal("\"Z\"", cb.QuoteIdentifier("Z"));
            Assert.Equal("\"\"\"\"", cb.QuoteIdentifier("\""));
            Assert.Equal("\"A\"\"C\"", cb.QuoteIdentifier("A\"C"));
        }

        [Fact]
        public void QuoteIdentifier_PrefixSuffix_NoMatch()
        {
            SqlCommandBuilder cb;

            cb = new SqlCommandBuilder();
            cb.QuoteSuffix = "\"";
            try
            {
                cb.QuoteIdentifier("dotnet");
            }
            catch (ArgumentException ex)
            {
                // Specified QuotePrefix and QuoteSuffix values
                // do not match
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("QuotePrefix") != -1);
                Assert.True(ex.Message.IndexOf("QuoteSuffix") != -1);
                Assert.Null(ex.ParamName);
            }

            cb = new SqlCommandBuilder();
            cb.QuotePrefix = "\"";
            try
            {
                cb.QuoteIdentifier("dotnet");
            }
            catch (ArgumentException ex)
            {
                // Specified QuotePrefix and QuoteSuffix values
                // do not match
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("QuotePrefix") != -1);
                Assert.True(ex.Message.IndexOf("QuoteSuffix") != -1);
                Assert.Null(ex.ParamName);
            }
        }

        [Fact]
        public void QuoteIdentifier_UnquotedIdentifier_Null()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            try
            {
                cb.QuoteIdentifier((string)null);
            }
            catch (ArgumentNullException ex)
            {
                Assert.Equal(typeof(ArgumentNullException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Equal("unquotedIdentifier", ex.ParamName);
            }
        }

        [Fact]
        public void QuotePrefix()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            Assert.Equal("[", cb.QuotePrefix);
            Assert.Equal("]", cb.QuoteSuffix);
            cb.QuotePrefix = "\"";
            Assert.Equal("\"", cb.QuotePrefix);
            Assert.Equal("]", cb.QuoteSuffix);
            cb.QuotePrefix = "[";
            Assert.Equal("[", cb.QuotePrefix);
            Assert.Equal("]", cb.QuoteSuffix);
        }

        [Theory]
        [InlineData("x")]
        [InlineData("'")]
        [InlineData("[x")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("]")]
        public void QuotePrefix_Value_Invalid(string prefix)
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            try
            {
                cb.QuotePrefix = prefix;
            }
            catch (ArgumentException ex)
            {
                // The acceptable values for the property
                // 'QuoteSuffix' are ']' or '"'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }
        }

        [Fact]
        public void QuoteSuffix()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            Assert.Equal("[", cb.QuotePrefix);
            Assert.Equal("]", cb.QuoteSuffix);
            cb.QuoteSuffix = "\"";
            Assert.Equal("[", cb.QuotePrefix);
            Assert.Equal("\"", cb.QuoteSuffix);
            cb.QuoteSuffix = "]";
            Assert.Equal("[", cb.QuotePrefix);
            Assert.Equal("]", cb.QuoteSuffix);
        }

        [Theory]
        [InlineData("x")]
        [InlineData("'")]
        [InlineData("[x")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("[")]
        public void QuoteSuffix_Value_Invalid(string suffix)
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();

            try
            {
                cb.QuoteSuffix = suffix;
            }
            catch (ArgumentException ex)
            {
                // The acceptable values for the property
                // 'QuoteSuffix' are ']' or '"'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }
        }

        [Fact]
        public void SchemaSeparator()
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();
            Assert.Equal(".", cb.SchemaSeparator);
            cb.SchemaSeparator = ".";
            Assert.Equal(".", cb.SchemaSeparator);
        }

        [Theory]
        [InlineData("x")]
        [InlineData("'")]
        [InlineData("[x")]
        [InlineData("")]
        [InlineData(null)]
        public void SchemaSeparator_Value_Invalid(string separator)
        {
            SqlCommandBuilder cb = new SqlCommandBuilder();

            try
            {
                cb.SchemaSeparator = separator;
            }
            catch (ArgumentException ex)
            {
                // The acceptable value for the property
                // 'SchemaSeparator' is '.'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'SchemaSeparator'") != -1);
                Assert.True(ex.Message.IndexOf("'.'") != -1);
                Assert.Null(ex.ParamName);
            }
        }
    }
}
