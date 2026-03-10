// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace Microsoft.Data.Common.UnitTests;

public class MultipartIdentifierTests
{
    /// <summary>
    /// Gets a collection of test data containing various part identifier strings and their expected
    /// parse results.
    /// </summary>
    /// <remarks>
    /// The returned data includes different combinations of part identifier formats, such as
    /// those with embedded whitespace, leading or trailing whitespace, and bracket characters.
    /// </remarks>
    /// <see cref="SinglePartIdentifierWithBracketsAndWhiteSpace_ParsesCorrectly" />
    public static TheoryData<string, string[]> ValidSinglePartIdentifierVariations
    {
        get
        {
            ReadOnlySpan<string> part1Words = ["word1", "word 1"];
            TheoryData<string, string[]> data = [];

            // Combination 1: embedded and non-embedded whitespace.
            // Combination 2: leading and/or trailing whitespace, and no whitespace
            // Combination 3: bracket characters "" or []
            // Combination 4: wrapped in bracket characters, no bracket characters
            // Combination 5: if wrapped in bracket characters, embedded (escaped) characters
            foreach (string part1 in part1Words)
            {
                foreach ((string p1Combination, string p1Expected) in GeneratePartCombinations(part1))
                {
                    string onePartCombination = p1Combination;
                    string[] onePartExpected = [p1Expected];

                    data.Add(onePartCombination, onePartExpected);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Gets a collection of test cases representing various formats and structures of multipart identifiers.
    /// </summary>
    /// <remarks>
    /// This property provides examples of multipart identifiers with different numbers of
    /// parts, including cases with empty segments, variations of whitespace around the separator, and bracketed components.
    /// </remarks>
    /// <see cref="MultipartIdentifier_ParsesCorrectly" />
    public static TheoryData<string, string[]> ValidMultipartIdentifierVariations =>
        new()
        {
            // Two parts, bracketed and unbracketed
            { "[word1].[word2]", ["word1", "word2"] },
            { "word1.word2", ["word1", "word2"] },
            // Two parts, one of which is empty
            { ".word2", ["", "word2"] },
            { "word1.", ["word1", ""] },
            // Two parts, with whitespace around the separator
            { "word1 .word2", ["word1", "word2"] },
            { "word1. word2", ["word1", "word2"] },
            { "word1 . word2", ["word1", "word2"] },
            { "[word1] .[word2]", ["word1", "word2"] },
            { "[word1]. [word2]", ["word1", "word2"] },
            { "[word1] . [word2]", ["word1", "word2"] },
            // Three parts, one of which is empty
            { ".word2.word3", ["", "word2", "word3"] },
            { "word1..word3", ["word1", "", "word3"] },
            { "word1.word2.", ["word1", "word2", ""] },
            // Four parts, one of which is empty
            { ".word2.word3.word4", ["", "word2", "word3", "word4"] },
            { "word1..word3.word4", ["word1", "", "word3", "word4"] },
            { "word1.word2..word4", ["word1", "word2", "", "word4"] },
            { "word1.word2.word3.", ["word1", "word2", "word3", ""] },
        };

    /// <summary>
    /// Gets a collection of test cases representing various formats of invalid part identifiers.
    /// </summary>
    /// <remarks>
    /// This property provides examples of single part identifiers with mismatched brackets, invalid bracket
    /// placement, unclosed brackets or quotes, and cases with more parts than expected.
    /// These cases are intended to test the parser's ability to correctly identify and reject invalid formats.
    /// </remarks>
    /// <see cref="InvalidSinglePartIdentifier_Throws" />
    public static TheoryData<string> InvalidSinglePartIdentifierVariations =>
        new()
        {
            // Empty string
            {  "" },
            // Bracket halfway through a part
            { "word1[word2" },
            { "word1]word2" },
            { "word1\"word2" },
            // Invalid bracket placement (i.e. starts with a close bracket or ends with an open bracket)
            { "]word1" },
            { "word1[" },
            // Unclosed brackets or quotes
            { "[word1" },
            { "\"word1" },
            // Part starts with one bracket and ends with another
            { "[word1\"" },
            { "\"word1]" },
            // More parts than expected, in various conditions.
            // Additional part is empty
            {  "word1." },
            // Additional part, wrapped in brackets
            { "word1.word2" },
            { "word1.[word2]" },
            { "word1.\"word2\"" },
            // Additional part, with whitespace before or after the separator
            { "word1 .word2" },
            { "word1. word2" },
            // Additional part, with whitespace after the second part
            { "word1.word2  " },
            { "word1.[word2  ]" },
            { "word1.\"word2  \"" },
            // Additional part, with separator after the second part
            { "word1.word2." },
            { "word1.word2.word3" },
            { "word1.[word2]." },
            { "word1.\"word2\"." },
            { "word1.[word2].word3" },
            { "word1.\"word2\".word3" },
        };

    /// <summary>
    /// Gets a collection of test cases representing various formats and structures of invalid multipart identifiers.
    /// </summary>
    /// <remarks>
    /// This property provides examples of multipart identifiers with trailing non-whitespace characters after bracketed
    /// parts, and cases with whitespace followed by non-whitespace characters after bracketed parts.
    /// These cases are intended to test the parser's ability to correctly identify and reject invalid multipart identifier formats.
    /// </remarks>
    /// <see cref="InvalidMultipartIdentifier_Throws" />
    public static TheoryData<string, int> InvalidMultipartIdentifierVariations =>
        new()
        {
            // Bracketed part with trailing non-whitespace characters
            { "[foo]!.[bar]", 2 },
            { "\"foo\"!.\"bar\"", 2 },
            { "[foo].[bar]!", 2 },
            { "\"foo\".\"bar\"!", 2 },
            // Bracketed part with trailing whitespace followed by non-whitespace characters
            { "[foo] !.[bar]", 2 },
            { "\"foo\" !.\"bar\"", 2 },
            { "[foo]. ![bar]", 2 },
            { "\"foo\". !\"bar\"", 2 },
            { "[foo].[bar] !", 2 },
            { "\"foo\".\"bar\" !", 2 },
        };

    /// <summary>
    /// Gets a collection of test cases representing the results of processing an identifier with fewer parts than expected.
    /// </summary>
    /// <see cref="SingleUnbracketedOvercount_FillsFirstElementsWithNull" />
    public static TheoryData<string, string?[], int> OvercountMultipartIdentifierVariations =>
        new()
        {
            { "word1", [null, "word1"], 2 },
            { "word1", [null, null, "word1"], 3 },
            { "word1", [null, null, null, "word1"], 4 },

            { "word1.word2", [null, "word1", "word2"], 3 },
            { "word1.word2", [null, null, "word1", "word2"], 4 },

            { "word1.word2.word3", [null, "word1", "word2", "word3"], 4 },
        };

    /// <summary>
    /// Verifies that one part in an identifier parses successfully when it contains various
    /// combinations of brackets and whitespace, and that the expected value is returned.
    /// </summary>
    /// <param name="partIdentifier">The raw identifier to parse.</param>
    /// <param name="expected">The expected output of parsing the identifier.</param>
    [Theory]
    [MemberData(nameof(ValidSinglePartIdentifierVariations))]
    public void SinglePartIdentifierWithBracketsAndWhiteSpace_ParsesSuccessfully(string partIdentifier, string[] expected) =>
        RunParse(partIdentifier, expected);

    /// <summary>
    /// Verifies that a multi-part identifier parses successfully when its parts are combinations
    /// of bracketed and unbracketed identifiers, with various placements of whitespace, and that
    /// the expected values are returned.
    /// </summary>
    /// <param name="partIdentifier">The raw identifier to parse.</param>
    /// <param name="expected">The expected output of parsing the identifier.</param>
    [Theory]
    [MemberData(nameof(ValidMultipartIdentifierVariations))]
    public void MultipartIdentifier_ParsesSuccessfully(string partIdentifier, string[] expected) =>
        RunParse(partIdentifier, expected);

    /// <summary>
    /// Verifies that parsing one part in an identifier throws an exception when it is invalid.
    /// This encompasses mismatched, misplaced or unclosed brackets and more parts than expected,
    /// in various combinations with whitespace.
    /// </summary>
    /// <param name="partIdentifier">The raw identifier to parse.</param>
    [Theory]
    [MemberData(nameof(InvalidSinglePartIdentifierVariations))]
    public void InvalidSinglePartIdentifier_Throws(string partIdentifier) =>
        ThrowParse(partIdentifier, expectedLength: 1);

    /// <summary>
    /// Verifies that parsing a multi-part identifier throws an exception when it is invalid (such as
    /// containing non-whitespace characters between a closing bracket and the separator, or between a
    /// closing bracket and the end of the string.)
    /// </summary>
    /// <param name="partIdentifier">The raw identifier to parse.</param>
    /// <param name="expectedLength">The expected number of components in the identifier.</param>
    [Theory]
    [MemberData(nameof(InvalidMultipartIdentifierVariations))]
    public void InvalidMultipartIdentifier_Throws(string partIdentifier, int expectedLength) =>
        ThrowParse(partIdentifier, expectedLength);

    /// <summary>
    /// Verifies that when a multipart identifier contains fewer parts than expected, the parser fills the
    /// missing elements in the array with null values (starting from the first element) and successfully
    /// parses the identifier.
    /// </summary>
    /// <param name="partIdentifier">The raw identifier to parse.</param>
    /// <param name="expected">The expected output of parsing the identifier.</param>
    /// <param name="maxCount">The number of parts which the part parsing should normally expect.</param>
    [Theory]
    [MemberData(nameof(OvercountMultipartIdentifierVariations))]
    public void SingleUnbracketedOvercount_FillsFirstElementsWithNull(string partIdentifier, string?[] parts, int maxCount) =>
        RunParse(partIdentifier, parts, maxCount);

    /// <summary>
    /// Verifies that multipart identifier strings containing zero-length segments are parsed into the expected
    /// array of empty strings.
    /// </summary>
    /// <remarks>
    /// This test case contrasts with <see cref="EmptyMultipartIdentifierWithThrowOnEmptyTrue_Throws"/>, where the
    /// input is a completely empty string rather than a multipart identifier with empty segments.
    /// </remarks>
    /// <param name="partIdentifier">The raw identifier to parse.</param>
    [Theory]
    [InlineData("[].[].[].[]")]
    [InlineData("...")]
    [InlineData(".[].[].")]
    [InlineData("[]...[]")]
    [InlineData(" . . . ")]
    [InlineData(" []. [] . [] .[] ")]
    public void MultipartIdentifierOfZeroLengthParts_ParsesSuccessfully(string partIdentifier) =>
        RunParse(partIdentifier, ["", "", "", ""]);

    /// <summary>
    /// Verifies that parsing a multipart identifier with more parts than expected throws an exception.
    /// </summary>
    /// <param name="maxCount">The number of parts which the part parsing should normally expect.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MultipartIdentifierWithMorePartsThanExpected_Throws(int maxCount) =>
        ThrowParse("word1.word2.word3.word4", maxCount);

    /// <summary>
    /// Verifies that parsing an empty multipart identifier with the throwOnEmpty flag set to false returns an array
    /// of nulls with the specified number of parts (rather than throwing an exception.)
    /// </summary>
    /// <param name="expectedLength">The expected number of components in the identifier.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EmptyMultipartIdentifierWithThrowOnEmptyFalse_ReturnsArrayOfNulls(int expectedLength) =>
        RunParse("", new string?[expectedLength], expectedLength, throwOnEmpty: false);

    /// <summary>
    /// Verifies that parsing an empty multipart identifier with the throwOnEmpty flag set to true throws an exception.
    /// </summary>
    /// <remarks>
    /// This test case contrasts with <see cref="MultipartIdentifierOfZeroLengthParts_ParsesSuccessfully"/>, where the
    /// input is a multipart identifier with empty segments rather than a completely empty string.
    /// </remarks>
    /// <param name="expectedLength">The expected number of components in the identifier.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EmptyMultipartIdentifierWithThrowOnEmptyTrue_Throws(int expectedLength) =>
        ThrowParse("", expectedLength);

    private static void RunParse(string name, string?[] expected, int maxCount = 0, bool throwOnEmpty = true)
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

        string?[] originalParts = MultipartIdentifier.ParseMultipartIdentifier(name, "", throwOnEmpty, maxCount);

        Assert.Equal(expected.Length, originalParts.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            string? expectedPart = expected[index];
            string? originalPart = originalParts[index];

            Assert.Equal(expectedPart, originalPart);
        }
    }

    private static void ThrowParse(string name, int expectedLength)
    {
        ArgumentException originalException = Assert.Throws<ArgumentException>(() =>
            MultipartIdentifier.ParseMultipartIdentifier(name, "test", true, expectedLength)
        );

        Assert.NotNull(originalException);
    }

    private static IEnumerable<(string, string)> GeneratePartCombinations(string word)
    {
        Debug.Assert(word is "word1" or "word 1");
        (string OpeningBracket, string ClosingBracket)[] bracketCombinations = [("[", "]"), ("\"", "\"")];

        // Combinations of whitespace, contained entirely within various combinations of brackets
        foreach (string wsCombination in GenerateWhitespaceCombinations(word))
        {
            foreach ((string openingBracket, string closingBracket) in bracketCombinations)
            {
                foreach ((string bracketCombination, string expectedValue) in GenerateBracketCombinations(wsCombination, openingBracket, closingBracket))
                {
                    yield return (bracketCombination, expectedValue);
                }
            }
        }

        // Combinations of brackets, with whitespace outside the brackets
        foreach ((string openingBracket, string closingBracket) in bracketCombinations)
        {
            foreach ((string bracketCombination, string unbracketedValue) in GenerateBracketCombinations(word, openingBracket, closingBracket))
            {
                foreach (string wsCombination in GenerateWhitespaceCombinations(bracketCombination))
                {
                    yield return (wsCombination, unbracketedValue);
                }
            }
        }

        static IEnumerable<string> GenerateWhitespaceCombinations(string word)
        {
            yield return word;
            yield return $"  {word}";
            yield return $"{word}  ";
            yield return $"  {word}  ";
        }

        static IEnumerable<(string Combination, string Expected)> GenerateBracketCombinations(string word, string openingBracket, string closingBracket)
        {
            yield return (word, word.Trim());
            yield return (openingBracket + word + closingBracket, word);
            yield return (openingBracket + word.Insert(word.Length - 3, closingBracket + closingBracket) + closingBracket, word.Insert(word.Length - 3, closingBracket));
        }
    }
}
