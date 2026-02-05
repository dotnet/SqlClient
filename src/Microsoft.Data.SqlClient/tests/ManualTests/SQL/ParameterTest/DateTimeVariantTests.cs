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
        private readonly string _connStr;
        private const string BaselineDirectory = "DateTimeVariant";

        public DateTimeVariantTests()
        {
            _connStr = DataTestUtility.TCPConnectionString;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DateTimeVariantParameterTest()
        {
            Assert.True(RunTestAndCompareWithBaseline());
        }

        private bool RunTestAndCompareWithBaseline()
        {
            string outputPath = "DateTimeVariant.out";

            var fstream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var swriter = new StreamWriter(fstream, Encoding.UTF8);
            var twriter = new TvpTest.CarriageReturnLineFeedReplacer(swriter);
            Console.SetOut(twriter);

            // Run Test
            DateTimeVariantTest.TestAllDateTimeWithDataTypeAndVariant(_connStr);

            Console.Out.Flush();
            Console.Out.Dispose();

            // Recover the standard output stream
            StreamWriter standardOutput = new(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);

            // Compare output file against concatenated baseline files
            var comparisonResult = FindDiffFromBaselineDirectory(BaselineDirectory, outputPath);

            if (string.IsNullOrEmpty(comparisonResult))
            {
                return true;
            }

            Console.WriteLine("DateTimeVariantParameterTest Failed!");
            Console.WriteLine("Please compare baseline directory: {0} with output: {1}", Path.GetFullPath(BaselineDirectory), Path.GetFullPath(outputPath));
            Console.WriteLine("Comparison Results:");
            Console.WriteLine(comparisonResult);
            return false;
        }

        /// <summary>
        /// Concatenates all .bsl files from the baseline directory (sorted by filename)
        /// and compares against the output file.
        /// </summary>
        private static string FindDiffFromBaselineDirectory(string baselineDir, string outputPath)
        {
            // Get all baseline files sorted by name
            var baselineFiles = Directory.GetFiles(baselineDir, "*.bsl")
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();

            // Concatenate all baseline files
            var expectedLines = new List<string>();
            foreach (var file in baselineFiles)
            {
                expectedLines.AddRange(File.ReadAllLines(file));
            }

            var outputLines = File.ReadAllLines(outputPath);

            var comparisonSb = new StringBuilder();

            var expectedLength = expectedLines.Count;
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
