// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for DateTime variant parameters with different date/time types.
    /// These tests run independently with their own baseline comparison.
    /// </summary>
    public class DateTimeVariantTests
    {
        private const string BaselineDirectory = "DateTimeVariant";

        /// <summary>
        /// Parameterized test for DateTime variant parameters.
        /// Each parameter combination runs 16 test methods and compares against 16 baseline files.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetParameterCombinations))]
        public void DateTimeVariantParameterTest(int paramIndex, object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            Assert.True(RunTestAndCompareWithBaseline(paramIndex, paramValue, expectedTypeName, expectedBaseTypeName));
        }

        /// <summary>
        /// Gets parameter combinations as indices for MemberData.
        /// Using indices for xUnit serialization compatibility.
        /// </summary>
        public static IEnumerable<object[]> GetParameterCombinations()
        {
            yield return new object[] { 0, DateTime.MinValue, "System.DateTime", "date" };
            yield return new object[] { 1, DateTime.MaxValue, "System.DateTime", "date" };
            yield return new object[] { 2, DateTime.MinValue, "System.DateTime", "datetime2" };
            yield return new object[] { 3, DateTime.MaxValue, "System.DateTime", "datetime2" };
            yield return new object[] { 4, DateTime.MinValue, "System.DateTime", "datetime" };
            yield return new object[] { 5, DateTime.MaxValue, "System.DateTime", "datetime" };
            yield return new object[] { 6, DateTimeOffset.MinValue, "System.DateTimeOffset", "datetimeoffset" };
            yield return new object[] { 7, DateTimeOffset.MaxValue, "System.DateTimeOffset", "datetimeoffset" };
            yield return new object[] { 8, DateTimeOffset.Parse("12/31/1999 23:59:59.9999999 -08:30"), "System.DateTimeOffset", "datetimeoffset" };
            yield return new object[] { 9, DateTime.Parse("1998-01-01 23:59:59.995"), "System.DateTime", "datetime2" };
            yield return new object[] { 10, DateTime.MinValue, "System.DateTime", "smalldatetime" };
            yield return new object[] { 11, DateTime.MaxValue, "System.DateTime", "smalldatetime" };
            yield return new object[] { 12, TimeSpan.MinValue, "System.TimeSpan", "time" };
            yield return new object[] { 13, TimeSpan.MaxValue, "System.TimeSpan", "time" };
            yield return new object[] { 14, DateTime.MinValue, "System.DateTime", "time" };
            yield return new object[] { 15, DateTime.MaxValue, "System.DateTime", "time" };
        }

        private bool RunTestAndCompareWithBaseline(int paramIndex, object paramValue, string expectedTypeName, string expectedBaseTypeName)
        {
            string outputPath = $"DateTimeVariant_{paramIndex}.out";

            var fstream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var swriter = new StreamWriter(fstream, Encoding.UTF8);
            var twriter = new TvpTest.CarriageReturnLineFeedReplacer(swriter);
            Console.SetOut(twriter);

            // Run Test - calls 16 methods for this parameter combination
            DateTimeVariantTest.SendInfoPublic(paramValue, expectedTypeName, expectedBaseTypeName, DataTestUtility.TCPConnectionString);

            Console.Out.Flush();
            Console.Out.Dispose();

            // Recover the standard output stream
            StreamWriter standardOutput = new(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);

            // Compare output file against the 16 baseline files for this parameter
            var comparisonResult = FindDiffFromBaselineFiles(paramIndex, outputPath);

            if (string.IsNullOrEmpty(comparisonResult))
            {
                return true;
            }

            Console.WriteLine($"DateTimeVariantParameterTest[{paramIndex}] Failed!");
            Console.WriteLine("Comparison Results:");
            Console.WriteLine(comparisonResult);
            return false;
        }

        /// <summary>
        /// Gets the baseline file for a specific parameter index and compares against output.
        /// Each parameter has a single combined baseline file: DateTimeVariant_XX.bsl
        /// </summary>
        private static string FindDiffFromBaselineFiles(int paramIndex, string outputPath)
        {
            // Get the baseline file for this parameter
            string baselineFile = Path.Combine(BaselineDirectory, $"DateTimeVariant_{paramIndex:D2}.bsl");

            if (!File.Exists(baselineFile))
            {
                return $"Baseline file not found: {baselineFile}";
            }

            var expectedLines = File.ReadAllLines(baselineFile);

            var outputLines = File.ReadAllLines(outputPath);

            var comparisonSb = new StringBuilder();

            var expectedLength = expectedLines.Length;
            var outputLength = outputLines.Length;
            var findDiffLength = Math.Min(expectedLength, outputLength);

            for (var lineNo = 0; lineNo < findDiffLength; lineNo++)
            {
                if (!expectedLines[lineNo].Equals(outputLines[lineNo]))
                {
                    comparisonSb.AppendFormat("** DIFF at line {0} \n", lineNo);
                    comparisonSb.AppendFormat("A : {0} \n", outputLines[lineNo]);
                    comparisonSb.AppendFormat("E : {0} \n", expectedLines[lineNo]);
                }
            }

            var startIndex = findDiffLength - 1;
            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (findDiffLength < expectedLength)
            {
                comparisonSb.AppendFormat("** MISSING \n");
                for (var lineNo = startIndex; lineNo < expectedLength; lineNo++)
                {
                    comparisonSb.AppendFormat("{0} : {1}", lineNo, expectedLines[lineNo]);
                }
            }
            if (findDiffLength < outputLength)
            {
                comparisonSb.AppendFormat("** EXTRA \n");
                for (var lineNo = startIndex; lineNo < outputLength; lineNo++)
                {
                    comparisonSb.AppendFormat("{0} : {1}", lineNo, outputLines[lineNo]);
                }
            }

            return comparisonSb.ToString();
        }
    }
}
