// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class MultipartIdentifierTests
    {
        [Fact]
        public void SingleUnquoted() => RunParse("foo", new[] { "foo" });

        [Fact]
        public void SingleUnquotedOvercount() => RunParse("foo", new[] { null, "foo" }, maxCount: 2);

        [Fact]
        public void SingleUnquotedContainsWhitespace() => RunParse("foo bar", new[] { "foo bar" });

        [Fact]
        public void SingleUnquotedStartWithShitespace() => RunParse("  foo", new[] { "foo" });

        [Fact]
        public void SingleUnquotedEndWithShitespace() => RunParse("foo  ", new[] { "foo" });

        [Fact]
        public void SingleQuotedKeepQuote() => RunParse("[foo]", new[] { "foo" });

        [Fact]
        public void SingleQuotedLeadingWhitespace() => RunParse("[ foo]", new[] { " foo" });

        [Fact]
        public void SingleQuotedTrailingWhitespace() => RunParse("[foo ]", new[] { "foo " });

        [Fact]
        public void QuotedContainsWhitespace() => RunParse("[foo bar]", new[] { "foo bar" });

        [Fact]
        public void SingleQuotedContainsAndTrailingWhitespace() => RunParse("[foo bar ]", new[] { "foo bar " });

        [Fact]
        public void SingleQuotedInternalAndLeadingWhitespace() => RunParse("[ foo bar]", new[] { " foo bar" });

        [Fact]
        public void SingleQuotedContainsAndLeadingAndTrailingWhitespace() => RunParse("[ foo bar ]", new[] { " foo bar " });

        [Fact]
        public void SingleQuotedEscapedQuote() => RunParse("[foo]]bar]", new[] { "foo]bar" });


        [Fact]
        public void DoubleUnquotedParts() => RunParse("foo.bar", new[] { "foo", "bar" });

        [Fact]
        public void DoubleUnquotedPartContainsTrailngWhitespace() => RunParse("foo .bar", new[] { "foo", "bar" });

        [Fact]
        public void DoubleUnquotedPartContainsLeadingWhitespace() => RunParse("foo. bar", new[] { "foo", "bar" });

        [Fact]
        public void DoubleUnquotedEmptyFirst() => RunParse(".bar", new[] { "", "bar" });

        [Fact]
        public void DoubleUnquotedEmptyLast() => RunParse("foo.", new[] { "foo", "" });

        [Fact]
        public void DoubleQuotedParts() => RunParse("[foo].[bar]", new string[] { "foo", "bar" });

        [Fact]
        public void DoubleQuotedPartContainsLeadingWhitespace() => RunParse("[foo]. [bar]", new[] { "foo", "bar" });

        [Fact]
        public void DoubleQuotedPartContainsTrailngWhitespace() => RunParse("[foo] .[bar]", new[] { "foo", "bar" });


        [Fact]
        public void TripleUnquotedParts() => RunParse("foo.bar.ed", new[] { "foo", "bar", "ed" });

        [Fact]
        public void TripleUnquotedMissingMiddle() => RunParse("foo..bar", new[] { "foo", "", "bar" });

        [Fact]
        public void TripleUnquotedPartContainsTrailingWhitespace() => RunParse("foo .bar .ed", new[] { "foo", "bar", "ed" });

        [Fact]
        public void TripleUnquotedPartContainsEmptyAndTrailngWhitespace() => RunParse(" .bar .ed", new[] { "", "bar", "ed" });

        [Fact]
        public void TripleUnquotedPartContainsLeadingWhitespace() => RunParse("foo. bar.", new[] { "foo", "bar", "" });

        [Fact]
        public void TripleUnquotedEmptyPart() => RunParse(".bar", new[] { "", "bar" });

        [Fact]
        public void TripleQuotedParts() => RunParse("[foo].[bar]", new[] { "foo", "bar" });

        [Fact]
        public void TripleQuotedPartContainsLeadingWhitespace() => RunParse("[foo]. [bar]", new[] { "foo", "bar" });

        [Fact]
        public void TripleQuotedPartContainsTrailngWhitespace() => RunParse("[foo] .[bar]", new[] { "foo", "bar" });

        [Fact]
        public void InvalidUnquotedEmpty() => ThrowParse<ArgumentException>("", new[] { "" });

        [Fact]
        public void InvalidContainsOpen() => ThrowParse<ArgumentException>("foo[bar", new[] { "foo[bar" });

        [Fact]
        public void InvalidContainsClose() => ThrowParse<ArgumentException>("foo]bar", new[] { "foo]bar" });

        [Fact]
        public void InvalidStartsWithClose() => ThrowParse<ArgumentException>("]bar", new[] { "]bar" });

        [Fact]
        public void InvalidEndsWithClose() => ThrowParse<ArgumentException>("bar]", new[] { "bar]" });

        [Fact]
        public void InvalidUnfinishedBraceOpen() => ThrowParse<ArgumentException>("[foo", new[] { "[foo" });

        [Fact]
        public void InvalidUnfinishedQuoteOpen() => ThrowParse<ArgumentException>("\"foo", new[] { "\"foo" });

        [Fact]
        public void InvalidCapacity()
        {
            ThrowParse<ArgumentException>("", Array.Empty<string>());
        }

        [Fact]
        public void InvalidQuotedPartContainsTrailngNonWhitespace() => ThrowParse<ArgumentException>("[foo]!.[bar]", new[] { "foo", "bar" });

        [Fact]
        public void InvalidQuotedPartContainsTrailngWhiteSpaceThenNonWhitespace() => ThrowParse<ArgumentException>("[foo] !.[bar]", new[] { "foo", "bar" });

        [Fact]
        public void InvalidTooManyParts_2to1() => ThrowParse<ArgumentException>("foo.bar", new[] { "foo" });

        [Fact]
        public void InvalidTooManyPartsEndsInSeparator() => ThrowParse("a.", 1);

        [Fact]
        public void InvalidTooManyPartsAfterTrailingWhitespace() => ThrowParse("foo .bar .ed", 1);

        [Fact]
        public void InvalidTooManyPartsEndsWithCloseQuote() => ThrowParse("a.[b]", 1);

        [Fact]
        public void InvalidTooManyPartsEndsWithWhitespace() => ThrowParse("a.foo  ", 1);

        [Fact]
        public void InvalidTooManyPartsQuotedPartContainsLeadingWhitespace() => ThrowParse("a.[b].c", 1);

        [Fact]
        public void InvalidTooManyPartsWhiteSpaceBeforeSeparator() => ThrowParse("a.b ..", 2);

        [Fact]
        public void InvalidTooManyPartsAfterCloseQuote() => ThrowParse("a.[b] .c", 1);

        [Fact]
        public void InvalidTooManyPartsSeparatorAfterPart() => ThrowParse("a.b.c", 1);


        private static void RunParse(string name, string[] expected, int maxCount = 0)
        {
            if (maxCount == 0)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (expected[index] != null)
                    {
                        maxCount += 1;
                    }
                }
            }

            string[] originalParts = MultipartIdentifier.ParseMultipartIdentifier(name, maxCount, "", true);

            for (int index = 0; index < expected.Length; index++)
            {
                string expectedPart = expected[index];
                string originalPart = originalParts[index];

                Assert.Equal(expectedPart, originalPart);
            }
        }

        private static void ThrowParse<TException>(string name, string[] expected)
            where TException : Exception
        {
            int maxCount = 0;
            for (int index = 0; index < expected.Length; index++)
            {
                if (expected[index] != null)
                {
                    maxCount += 1;
                }
            }

            Exception originalException = Assert.Throws<TException>(() =>
              MultipartIdentifier.ParseMultipartIdentifier(name, maxCount, "", true)
            );

            Assert.NotNull(originalException);
        }



        private static void ThrowParse(string name, int expectedLength)
        {
            Exception originalException = Assert.Throws<ArgumentException>(
                () =>
                {
                    MultipartIdentifier.ParseMultipartIdentifier(name, expectedLength, "test", true);
                }
            );
            Assert.NotNull(originalException);
        }

    }
}

namespace Microsoft.Data.Common
{
    // this is needed for the inclusion of MultipartIdentifier class
    internal class ADP
    {
        internal static ArgumentException InvalidMultipartName(string property, string name)
        {
            return new ArgumentException();
        }

        internal static ArgumentException InvalidMultipartNameIncorrectUsageOfQuotes(string property, string name)
        {
            return new ArgumentException();
        }

        internal static ArgumentException InvalidMultipartNameToManyParts(string property, string name, int limit)
        {
            return new ArgumentException();
        }
    }
}
