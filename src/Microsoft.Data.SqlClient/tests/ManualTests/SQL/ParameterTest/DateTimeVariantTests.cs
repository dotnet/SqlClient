// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for DateTime variant parameters with different date/time types.
    /// These tests run independently with their own baseline comparison.
    /// </summary>
    [Collection("ParameterBaselineTests")]
    public class DateTimeVariantTests
    {
        private readonly string _connStr;

        public DateTimeVariantTests()
        {
            _connStr = DataTestUtility.TCPConnectionString;
        }

        [Trait("Category", "flaky")]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void DateTimeVariantParameterTest()
        {
            Assert.True(RunTestAndCompareWithBaseline());
        }

        private bool RunTestAndCompareWithBaseline()
        {
            string outputPath = "DateTimeVariant.out";
            string baselinePath;
#if DEBUG
            if (DataTestUtility.IsNotAzureServer() || DataTestUtility.IsManagedInstance)
            {
                baselinePath = "DateTimeVariant_DebugMode.bsl";
            }
            else
            {
                baselinePath = "DateTimeVariant_DebugMode_Azure.bsl";
            }
#else
            if (DataTestUtility.IsNotAzureServer() || DataTestUtility.IsManagedInstance)
            {
                baselinePath = "DateTimeVariant_ReleaseMode.bsl";
            }
            else
            {
                baselinePath = "DateTimeVariant_ReleaseMode_Azure.bsl";
            }
#endif

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

            // Compare output file
            var comparisonResult = FindDiffFromBaseline(baselinePath, outputPath);

            if (string.IsNullOrEmpty(comparisonResult))
            {
                return true;
            }

            Console.WriteLine("DateTimeVariantParameterTest Failed!");
            Console.WriteLine("Please compare baseline: {0} with output: {1}", Path.GetFullPath(baselinePath), Path.GetFullPath(outputPath));
            Console.WriteLine("Comparison Results:");
            Console.WriteLine(comparisonResult);
            return false;
        }

        private static string FindDiffFromBaseline(string baselinePath, string outputPath)
        {
            var expectedLines = File.ReadAllLines(baselinePath);
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
