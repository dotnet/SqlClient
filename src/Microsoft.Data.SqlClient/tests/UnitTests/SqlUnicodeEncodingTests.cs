// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    /// <summary>
    /// Guards raw UTF-16LE code-unit decoding and source-buffer ownership during sequential reads.
    /// </summary>
    public class SqlUnicodeEncodingTests
    {
        private const string Text = "\0\uFEFFCaf\u00E9 \u0627\u0939\u0E20\u65E5\uD55C\uD83D\uDE00\uD834\uDD1E\uD800X\uDC00\uFFFF";

        /// <summary>
        /// Verifies capacity-limited decoding respects offsets, leaves sentinels and all input bytes intact,
        /// and reports consumption without buffering an odd trailing byte, regardless of flushing.
        /// </summary>
        /// <param name="capacity">The output capacity, including zero and excess capacity.</param>
        /// <param name="oddByte">Whether an incomplete code unit follows the text.</param>
        /// <param name="flush">Whether the caller requests a final conversion.</param>
        [Theory]
        [InlineData(0, false, false)]
        [InlineData(0, false, true)]
        [InlineData(1, false, false)]
        [InlineData(3, false, true)]
        [InlineData(100, false, false)]
        [InlineData(100, false, true)]
        [InlineData(100, true, false)]
        [InlineData(100, true, true)]
        public void Convert_WithOffsetsAndCapacity_PreservesSourceAndCodeUnits(int capacity, bool oddByte, bool flush)
        {
            byte[] source = CreateSource(Text);
            byte[] original = (byte[])source.Clone();
            char[] destination = new string('#', capacity + 4).ToCharArray();
            Decoder decoder = SqlUnicodeEncoding.SqlUnicodeEncodingInstance.GetDecoder();
            int byteCount = Text.Length * 2 + (oddByte ? 1 : 0);

            decoder.Convert(source, 1, byteCount, destination, 2, capacity, flush,
                out int bytesUsed, out int charsUsed, out bool completed);

            int expectedCount = Math.Min(capacity, Text.Length);
            Assert.Equal(expectedCount, charsUsed);
            Assert.Equal(expectedCount * 2, bytesUsed);
            Assert.Equal(bytesUsed == byteCount, completed);
            Assert.Equal(Text.Substring(0, expectedCount), new string(destination, 2, charsUsed));
            Assert.Equal("##", new string(destination, 0, 2));
            Assert.All(destination.AsSpan(2 + charsUsed).ToArray(), value => Assert.Equal('#', value));
            Assert.Equal(original, source);
        }

        /// <summary>
        /// Reuses unconsumed source bytes across one-code-unit conversions, including split surrogate pairs
        /// and isolated surrogates, without replacement, hidden decoder state, or input mutation.
        /// </summary>
        /// <param name="flush">Whether each conversion requests flushing.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Convert_RepeatedPartialReads_PreservesLeftovers(bool flush)
        {
            byte[] source = CreateSource(Text);
            byte[] original = (byte[])source.Clone();
            char[] destination = new char[Text.Length];
            Decoder decoder = SqlUnicodeEncoding.SqlUnicodeEncodingInstance.GetDecoder();

            for (int i = 0; i < Text.Length; i++)
            {
                decoder.Convert(source, 1 + i * 2, (Text.Length - i) * 2, destination, i, 1, flush,
                    out int bytesUsed, out int charsUsed, out bool completed);

                Assert.Equal(2, bytesUsed);
                Assert.Equal(1, charsUsed);
                Assert.Equal(i == Text.Length - 1, completed);
                Assert.Equal(Text[i], destination[i]);
                Assert.Equal(original, source);
            }

            Assert.Equal(Text, new string(destination));
        }

        /// <summary>
        /// Ensures an empty conversion at the end of both arrays completes without accessing their contents.
        /// </summary>
        [Fact]
        public void Convert_EmptyInputAndOutput_Completes()
        {
            byte[] source = { 0xA5 };
            char[] destination = { '#' };
            Decoder decoder = SqlUnicodeEncoding.SqlUnicodeEncodingInstance.GetDecoder();

            decoder.Convert(source, source.Length, 0, destination, destination.Length, 0, true,
                out int bytesUsed, out int charsUsed, out bool completed);

            Assert.Equal(0, bytesUsed);
            Assert.Equal(0, charsUsed);
            Assert.True(completed);
            Assert.Equal(new byte[] { 0xA5 }, source);
            Assert.Equal(new[] { '#' }, destination);
        }

        /// <summary>
        /// Creates UTF-16LE bytes explicitly so native byte order and encoding fallback cannot alter the test data.
        /// </summary>
        /// <param name="text">Raw code units, including unpaired surrogates.</param>
        /// <returns>The code units between leading and trailing byte sentinels.</returns>
        private static byte[] CreateSource(string text)
        {
            byte[] source = new byte[text.Length * 2 + 2];
            source[0] = 0xA5;
            source[source.Length - 1] = 0x5A;
            for (int i = 0; i < text.Length; i++)
            {
                source[1 + i * 2] = (byte)text[i];
                source[2 + i * 2] = (byte)(text[i] >> 8);
            }
            return source;
        }
    }
}
