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
        public void SingleUnbracketed() => RunParse("foo", ["foo"]);

        [Fact]
        public void SingleUnbracketedOvercount() => RunParse("foo", [null, "foo"], maxCount: 2);

        [Fact]
        public void SingleUnbracketedContainsWhitespace() => RunParse("foo bar", ["foo bar"]);

        [Fact]
        public void SingleUnbracketedStartWithWhitespace() => RunParse("  foo", ["foo"]);

        [Fact]
        public void SingleUnbracketedEndWithWhitespace() => RunParse("foo  ", ["foo"]);

        [Fact]
        public void SingleBracketedKeepQuote() => RunParse("[foo]", ["foo"]);

        [Fact]
        public void SingleBracketedLeadingWhitespace() => RunParse("[ foo]", [" foo"]);

        [Fact]
        public void SingleBracketedTrailingWhitespace() => RunParse("[foo ]", ["foo "]);

        [Fact]
        public void BracketedContainsWhitespace() => RunParse("[foo bar]", ["foo bar"]);

        [Fact]
        public void SingleBracketedInternalAndTrailingWhitespace() => RunParse("[foo bar ]", ["foo bar "]);

        [Fact]
        public void SingleBracketedInternalAndLeadingWhitespace() => RunParse("[ foo bar]", [" foo bar"]);

        [Fact]
        public void SingleBracketedContainsAndLeadingAndTrailingWhitespace() => RunParse("[ foo bar ]", [" foo bar "]);

        [Fact]
        public void SingleBracketedEscapedBracket() => RunParse("[foo]]bar]", ["foo]bar"]);

        [Fact]
        public void DoubleUnbracketedParts() => RunParse("foo.bar", ["foo", "bar"]);

        [Fact]
        public void DoubleUnbracketedPartContainsTrailingWhitespace() => RunParse("foo .bar", ["foo", "bar"]);

        [Fact]
        public void DoubleUnbracketedPartContainsLeadingWhitespace() => RunParse("foo. bar", ["foo", "bar"]);

        [Fact]
        public void DoubleUnbracketedEmptyFirst() => RunParse(".bar", ["", "bar"]);

        [Fact]
        public void DoubleUnbracketedEmptyLast() => RunParse("foo.", ["foo", ""]);

        [Fact]
        public void DoubleBracketedParts() => RunParse("[foo].[bar]", ["foo", "bar"]);

        [Fact]
        public void DoubleBracketedPartContainsLeadingWhitespace() => RunParse("[foo]. [bar]", ["foo", "bar"]);

        [Fact]
        public void DoubleBracketedPartContainsTrailingWhitespace() => RunParse("[foo] .[bar]", ["foo", "bar"]);


        [Fact]
        public void TripleUnbracketedParts() => RunParse("foo.bar.ed", ["foo", "bar", "ed"]);

        [Fact]
        public void TripleUnbracketedMissingMiddle() => RunParse("foo..bar", ["foo", "", "bar"]);

        [Fact]
        public void TripleUnbracketedPartContainsTrailingWhitespace() => RunParse("foo .bar .ed", ["foo", "bar", "ed"]);

        [Fact]
        public void TripleUnbracketedPartContainsEmptyAndTrailingWhitespace() => RunParse(" .bar .ed", ["", "bar", "ed"]);

        [Fact]
        public void TripleUnbracketedPartContainsLeadingWhitespace() => RunParse("foo. bar.", ["foo", "bar", ""]);

        [Fact]
        public void TripleUnbracketedEmptyPart() => RunParse(".bar", ["", "bar"]);

        [Fact]
        public void TripleBracketedParts() => RunParse("[foo].[bar]", ["foo", "bar"]);

        [Fact]
        public void TripleBracketedPartContainsLeadingWhitespace() => RunParse("[foo]. [bar]", ["foo", "bar"]);

        [Fact]
        public void TripleBracketedPartContainsTrailingWhitespace() => RunParse("[foo] .[bar]", ["foo", "bar"]);

        [Fact]
        public void InvalidUnbracketedEmpty() => ThrowParse<ArgumentException>("", [""]);

        [Fact]
        public void InvalidContainsOpenBracket() => ThrowParse<ArgumentException>("foo[bar", ["foo[bar"]);

        [Fact]
        public void InvalidContainsCloseBracket() => ThrowParse<ArgumentException>("foo]bar", ["foo]bar"]);

        [Fact]
        public void InvalidStartsWithCloseBracket() => ThrowParse<ArgumentException>("]bar", ["]bar"]);

        [Fact]
        public void InvalidEndsWithCloseBracket() => ThrowParse<ArgumentException>("bar]", ["bar]"]);

        [Fact]
        public void InvalidUnclosedBracketOpen() => ThrowParse<ArgumentException>("[foo", ["[foo"]);

        [Fact]
        public void InvalidUnclosedQuoteOpen() => ThrowParse<ArgumentException>("\"foo", ["\"foo"]);

        [Fact]
        public void InvalidBracketedPartContainsTrailingNonWhitespace() => ThrowParse<ArgumentException>("[foo]!.[bar]", ["foo", "bar"]);

        [Fact]
        public void InvalidBracketedPartContainsTrailingWhiteSpaceThenNonWhitespace() => ThrowParse<ArgumentException>("[foo] !.[bar]", ["foo", "bar"]);

        [Fact]
        public void InvalidTooManyParts_2to1() => ThrowParse<ArgumentException>("foo.bar", ["foo"]);

        [Fact]
        public void InvalidTooManyPartsEndsInSeparator() => ThrowParse("a.", 1);

        [Fact]
        public void InvalidTooManyPartsAfterTrailingWhitespace() => ThrowParse("foo .bar .ed", 1);

        [Fact]
        public void InvalidTooManyPartsEndsWithCloseBracket() => ThrowParse("a.[b]", 1);

        [Fact]
        public void InvalidTooManyPartsEndsWithWhitespace() => ThrowParse("a.foo  ", 1);

        [Fact]
        public void InvalidTooManyPartsBracketedPartContainsLeadingWhitespace() => ThrowParse("a.[b].c", 1);

        [Fact]
        public void InvalidTooManyPartsWhiteSpaceBeforeSeparator() => ThrowParse("a.b ..", 2);

        [Fact]
        public void InvalidTooManyPartsAfterCloseBracket() => ThrowParse("a.[b] .c", 1);

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
