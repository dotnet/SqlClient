// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace System.Text.UnitTests;

/// <summary>
/// Tests that the Encoding polyfills in netfx operate correctly and handle
/// invalid parameter values.
/// </summary>
/// <remarks>
/// In the netcore cases, we're testing the built-in GetBytes and GetByteCount
/// methods. The contract for our extension polyfills must match these implementations.
/// </remarks>
public class EncodingTest
{
    private const string ExampleStringValue = "ABCDéFG1234567abcdefg";

    /// <summary>
    /// Represents a series of invalid [offset, count] pairs into the <see cref="ExampleStringValue"/>
    /// constant.
    /// </summary>
    public static TheoryData<int, int> InvalidOffsetsAndCounts =>
        new()
        {
            // Group 1: offset starts before the string.
            // * Count extends beyond it.
            { -1, 999 },
            // * Count is valid.
            { -1, 5 },
            // Group 2: offset is valid.
            // * Count extends beyond the end of it.
            { 0, 999 },
            // * Count extends backwards to the start it.
            { 5, -5 },
            // Group 3: offset starts beyond the end of the string.
            // * Count extends beyond the end of it.
            { 999, 999 },
            // * Count extends backwards into the string.
            { 999, -1005 }
        };

    #if NET
    static EncodingTest()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
#endif

    /// <summary>
    /// Verifies that GetByteCount throws an ArgumentNullException when passed a null string.
    /// </summary>
    [Fact]
    public void GetByteCount_ThrowsOnNullString()
    {
        string nullString = null!;
        Action act = () => Encoding.Unicode.GetByteCount(nullString, 0, 0);

        Assert.Throws<ArgumentNullException>(act);
    }

    /// <summary>
    /// Verifies that GetBytes throws an ArgumentNullException when passed a null string.
    /// </summary>
    [Fact]
    public void GetBytes_ThrowsOnNullString()
    {
        string nullString = null!;
        Action act = () => Encoding.Unicode.GetBytes(nullString, 0, 0);

        Assert.Throws<ArgumentNullException>(act);
    }

    /// <summary>
    /// Verifies that GetByteCount throws an ArgumentOutOfRangeException when passes an offset
    /// or count which is outside of the string.
    /// </summary>
    /// <param name="offset">offset parameter of GetByteCount.</param>
    /// <param name="count">count parameter of GetByteCount.</param>
    /// <seealso cref="InvalidOffsetsAndCounts"/>
    [Theory]
    [MemberData(nameof(InvalidOffsetsAndCounts))]
    public void GetByteCount_ThrowsOnOutOfRangeOffsetOrCount(int offset, int count)
    {
        Action act = () => Encoding.Unicode.GetByteCount(ExampleStringValue, offset, count);

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    /// <summary>
    /// Verifies that GetBytes throws an ArgumentOutOfRangeException when passes an offset
    /// or count which is outside of the string.
    /// </summary>
    /// <param name="offset">offset parameter of GetBytes.</param>
    /// <param name="count">count parameter of GetBytes.</param>
    [Theory]
    [MemberData(nameof(InvalidOffsetsAndCounts))]
    public void GetBytes_ThrowsOnOutOfRangeOffsetOrCount(int offset, int count)
    {
        Action act = () => Encoding.Unicode.GetBytes(ExampleStringValue, offset, count);

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    /// <summary>
    /// Verifies that when using the new GetByteCount and GetBytes polyfills to encode the entire string, the return
    /// value is equal to passing the string as-is to GetByteCount(string) and GetBytes(string).
    /// </summary>
    [Fact]
    public void GetBytesOfFullStringByLength_MatchesGetBytesOfFullString()
    {
        byte[] fullStringBytes = Encoding.Unicode.GetBytes(ExampleStringValue);
        int fullStringByteCount = Encoding.Unicode.GetByteCount(ExampleStringValue);

        byte[] partialStringBytes = Encoding.Unicode.GetBytes(ExampleStringValue, 0, ExampleStringValue.Length);
        int partialStringByteCount = Encoding.Unicode.GetByteCount(ExampleStringValue, 0, ExampleStringValue.Length);

        Assert.Equal(fullStringByteCount, partialStringByteCount);
        Assert.Equal(fullStringByteCount, partialStringBytes.Length);
        Assert.Equal(fullStringBytes, partialStringBytes);
    }

    /// <summary>
    /// Verifies that encoding a specific substring returns a byte array which can be decoded into the same string, in
    /// various code pages.
    /// </summary>
    /// <param name="codePage">The code page identifier to use for transcoding.</param>
    [Theory]
    // Unicode
    [InlineData(1200)]
    // UTF8
    [InlineData(65001)]
    public void GetBytes_Roundtrips(int codePage)
    {
        Encoding encoding = Encoding.GetEncoding(codePage);
        byte[] partialStringBytes = encoding.GetBytes(ExampleStringValue, 4, 5);
        string expectedRoundtrippedValue = ExampleStringValue.Substring(4, 5);
        string roundtrip = encoding.GetString(partialStringBytes);

        Assert.Equal(expectedRoundtrippedValue, roundtrip);
    }

    /// <summary>
    /// Verifies that when a string contains a multibyte character, the byte array returns the correct number of
    /// elements for the encoding.
    /// </summary>
    [Fact]
    public void GetByteCount_ReturnsCorrectValueOnMultiCharacterRune()
    {
        // The character é is two bytes in UTF8.
        Assert.Equal(6, Encoding.UTF8.GetByteCount(ExampleStringValue, 4, 5));

        // All Unicode characters in our sample string are two bytes long.
        Assert.Equal(10, Encoding.Unicode.GetByteCount(ExampleStringValue, 4, 5));

        // Code page 1251 does not have the é character, so treats it as the single-byte character "e".
        Assert.Equal(5, Encoding.GetEncoding(1251).GetByteCount(ExampleStringValue, 4, 5));
    }
}
