// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.Common.UnitTests
{
    public class MultipartIdentifierTests
    {
        [Fact]
        public void SingleUnquoted() => RunParse("foo", ["foo"]);

        [Fact]
        public void SingleUnquotedOvercount() => RunParse("foo", [null, "foo"], maxCount: 2);

        [Fact]
        public void SingleUnquotedContainsWhitespace() => RunParse("foo bar", ["foo bar"]);

        [Fact]
        public void SingleUnquotedStartWithShitespace() => RunParse("  foo", ["foo"]);

        [Fact]
        public void SingleUnquotedEndWithShitespace() => RunParse("foo  ", ["foo"]);

        [Fact]
        public void SingleQuotedKeepQuote() => RunParse("[foo]", ["foo"]);

        [Fact]
        public void SingleQuotedLeadingWhitespace() => RunParse("[ foo]", [" foo"]);

        [Fact]
        public void SingleQuotedTrailingWhitespace() => RunParse("[foo ]", ["foo "]);

        [Fact]
        public void QuotedContainsWhitespace() => RunParse("[foo bar]", ["foo bar"]);

        [Fact]
        public void SingleQuotedContainsAndTrailingWhitespace() => RunParse("[foo bar ]", ["foo bar "]);

        [Fact]
        public void SingleQuotedInternalAndLeadingWhitespace() => RunParse("[ foo bar]", [" foo bar"]);

        [Fact]
        public void SingleQuotedContainsAndLeadingAndTrailingWhitespace() => RunParse("[ foo bar ]", [" foo bar "]);

        [Fact]
        public void SingleQuotedEscapedQuote() => RunParse("[foo]]bar]", ["foo]bar"]);


        [Fact]
        public void DoubleUnquotedParts() => RunParse("foo.bar", ["foo", "bar"]);

        [Fact]
        public void DoubleUnquotedPartContainsTrailngWhitespace() => RunParse("foo .bar", ["foo", "bar"]);

        [Fact]
        public void DoubleUnquotedPartContainsLeadingWhitespace() => RunParse("foo. bar", ["foo", "bar"]);

        [Fact]
        public void DoubleUnquotedEmptyFirst() => RunParse(".bar", ["", "bar"]);

        [Fact]
        public void DoubleUnquotedEmptyLast() => RunParse("foo.", ["foo", ""]);

        [Fact]
        public void DoubleQuotedParts() => RunParse("[foo].[bar]", ["foo", "bar"]);

        [Fact]
        public void DoubleQuotedPartContainsLeadingWhitespace() => RunParse("[foo]. [bar]", ["foo", "bar"]);

        [Fact]
        public void DoubleQuotedPartContainsTrailngWhitespace() => RunParse("[foo] .[bar]", ["foo", "bar"]);


        [Fact]
        public void TripleUnquotedParts() => RunParse("foo.bar.ed", ["foo", "bar", "ed"]);

        [Fact]
        public void TripleUnquotedMissingMiddle() => RunParse("foo..bar", ["foo", "", "bar"]);

        [Fact]
        public void TripleUnquotedPartContainsTrailingWhitespace() => RunParse("foo .bar .ed", ["foo", "bar", "ed"]);

        [Fact]
        public void TripleUnquotedPartContainsEmptyAndTrailngWhitespace() => RunParse(" .bar .ed", ["", "bar", "ed"]);

        [Fact]
        public void TripleUnquotedPartContainsLeadingWhitespace() => RunParse("foo. bar.", ["foo", "bar", ""]);

        [Fact]
        public void TripleUnquotedEmptyPart() => RunParse(".bar", ["", "bar"]);

        [Fact]
        public void TripleQuotedParts() => RunParse("[foo].[bar]", ["foo", "bar"]);

        [Fact]
        public void TripleQuotedPartContainsLeadingWhitespace() => RunParse("[foo]. [bar]", ["foo", "bar"]);

        [Fact]
        public void TripleQuotedPartContainsTrailngWhitespace() => RunParse("[foo] .[bar]", ["foo", "bar"]);

        [Fact]
        public void InvalidUnquotedEmpty() => ThrowParse<ArgumentException>("", [""]);

        [Fact]
        public void InvalidContainsOpen() => ThrowParse<ArgumentException>("foo[bar", ["foo[bar"]);

        [Fact]
        public void InvalidContainsClose() => ThrowParse<ArgumentException>("foo]bar", ["foo]bar"]);

        [Fact]
        public void InvalidStartsWithClose() => ThrowParse<ArgumentException>("]bar", ["]bar"]);

        [Fact]
        public void InvalidEndsWithClose() => ThrowParse<ArgumentException>("bar]", ["bar]"]);

        [Fact]
        public void InvalidUnfinishedBraceOpen() => ThrowParse<ArgumentException>("[foo", ["[foo"]);

        [Fact]
        public void InvalidUnfinishedQuoteOpen() => ThrowParse<ArgumentException>("\"foo", ["\"foo"]);

        [Fact]
        public void InvalidQuotedPartContainsTrailngNonWhitespace() => ThrowParse<ArgumentException>("[foo]!.[bar]", ["foo", "bar"]);

        [Fact]
        public void InvalidQuotedPartContainsTrailngWhiteSpaceThenNonWhitespace() => ThrowParse<ArgumentException>("[foo] !.[bar]", ["foo", "bar"]);

        [Fact]
        public void InvalidTooManyParts_2to1() => ThrowParse<ArgumentException>("foo.bar", ["foo"]);

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


        private static void RunParse(string name, string?[] expected, int maxCount = 0)
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

            string?[] originalParts = MultipartIdentifier.ParseMultipartIdentifier(name, "", true, maxCount);

            for (int index = 0; index < expected.Length; index++)
            {
                string? expectedPart = expected[index];
                string? originalPart = originalParts[index];

                Assert.Equal(expectedPart, originalPart);
            }
        }

        private static void ThrowParse<TException>(string name, string?[] expected)
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
              MultipartIdentifier.ParseMultipartIdentifier(name, "", true, maxCount)
            );

            Assert.NotNull(originalException);
        }



        private static void ThrowParse(string name, int expectedLength)
        {
            Exception originalException = Assert.Throws<ArgumentException>(
                () =>
                {
                    MultipartIdentifier.ParseMultipartIdentifier(name, "test", true, expectedLength);
                }
            );
            Assert.NotNull(originalException);
        }

    }
}
