// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

#nullable enable
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
        public void DateTimeVariantParameterTest(
            int paramIndex, 
            object paramValue, 
            string expectedTypeName, 
            string expectedBaseTypeName, 
            Dictionary<TestVariations, ExceptionChecker>? expectedExceptions = null, 
            Dictionary<TestVariations, ExceptionChecker>? expectedInvalidOperationExceptions = null,
            Dictionary<TestVariations, ExceptionChecker>? expectedButUncaughtExceptions = null,
            Dictionary<TestVariations, object>? expectedValueOverrides = null,
            Dictionary<TestVariations, object>? unexpectedValueOverrides = null)
        {
            expectedExceptions ??= new Dictionary<TestVariations, ExceptionChecker>();
            expectedInvalidOperationExceptions ??= new Dictionary<TestVariations, ExceptionChecker>();
            expectedButUncaughtExceptions ??= new Dictionary<TestVariations, ExceptionChecker>();
            expectedValueOverrides ??= new Dictionary<TestVariations, object>();
            unexpectedValueOverrides ??= new Dictionary<TestVariations, object>();
            Assert.True(RunTestAndCompareWithBaseline(
                paramIndex, 
                paramValue, 
                expectedTypeName, 
                expectedBaseTypeName, 
                expectedExceptions, 
                expectedInvalidOperationExceptions,
                expectedButUncaughtExceptions,
                expectedValueOverrides,
                unexpectedValueOverrides));
        }

        /// <summary>
        /// Gets parameter combinations as indices for MemberData.
        /// Using indices for xUnit serialization compatibility.
        /// </summary>
        public static IEnumerable<object?[]> GetParameterCombinations()
        {
            yield return new object?[] { 0, DateTime.MinValue, "System.DateTime", "date", null, null,
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }}, null, null};
            yield return new object?[] { 1, DateTime.MaxValue, "System.DateTime", "date", null, null, null, 
            new Dictionary<TestVariations, object>
            {
                { TestVariations.TestSimpleParameter_Type, new DateTime(3155378112000000000) },
                { TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, new DateTime(3155378112000000000) },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, new DateTime(3155378112000000000) },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataReader_TVP_Type, new DateTime(3155378112000000000) },
                { TestVariations.TestSqlDataReader_TVP_Variant, new DateTime(3155378112000000000) },
                { TestVariations.TestSimpleDataReader_Type, new DateTime(3155378112000000000) },
                { TestVariations.TestSimpleDataReader_Variant, new DateTime(3155378112000000000) },
                { TestVariations.SqlBulkCopySqlDataReader_Type, new DateTime(3155378112000000000) },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, new DateTime(3155378112000000000) },
                { TestVariations.SqlBulkCopyDataTable_Type, new DateTime(3155378112000000000) },
                { TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopyDataRow_Type, new DateTime(3155378112000000000) },
                { TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000) }
            }, null};
            yield return new object?[] { 2, DateTime.MinValue, "System.DateTime", "datetime2", null, null,
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }}, null, null};
            yield return new object?[] { 3, DateTime.MaxValue, "System.DateTime", "datetime2", null, null, null, null,
            new Dictionary<TestVariations, object> {
                {TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000)},
                {TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000)},
                {TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000)},
                {TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000)},
                {TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000)}
            }};
            yield return new object?[] { 4, DateTime.MinValue, "System.DateTime", "datetime", 
            new Dictionary<TestVariations, ExceptionChecker> { 
                { TestVariations.TestSimpleParameter_Type, SqlDateTimeOverflow },
                { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReader_TVP_Type, VarcharToDateTimeOutOfRange},
                { TestVariations.TestSqlDataReader_TVP_Variant, VarcharToDateTimeOutOfRange},
                { TestVariations.TestSimpleDataReader_Type, VarcharToDateTimeOutOfRange},
                { TestVariations.TestSimpleDataReader_Variant, VarcharToDateTimeOutOfRange},
                { TestVariations.SqlBulkCopySqlDataReader_Type, VarcharToDateTimeOutOfRange},
                { TestVariations.SqlBulkCopySqlDataReader_Variant, VarcharToDateTimeOutOfRange}, 
                { TestVariations.SqlBulkCopyDataTable_Type, SqlDateTimeOverflow},
                { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow},
                { TestVariations.SqlBulkCopyDataRow_Type, SqlDateTimeOverflow},
                { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow}}, null, null, null, null};
            yield return new object?[] { 5, DateTime.MaxValue, "System.DateTime", "datetime", 
            new Dictionary<TestVariations, ExceptionChecker> { 
                { TestVariations.TestSqlDataReader_TVP_Type, CannotConvertCharacterStringToDateOrTime},
                { TestVariations.TestSqlDataReader_TVP_Variant, CannotConvertCharacterStringToDateOrTime},
                { TestVariations.TestSimpleDataReader_Type, CannotConvertCharacterStringToDateOrTime},
                { TestVariations.TestSimpleDataReader_Variant, CannotConvertCharacterStringToDateOrTime},
                { TestVariations.SqlBulkCopySqlDataReader_Type, CannotConvertCharacterStringToDateOrTime},
                { TestVariations.SqlBulkCopySqlDataReader_Variant, CannotConvertCharacterStringToDateOrTime}}, 
                null,
                null,
            new Dictionary<TestVariations, object>
            {
                { TestVariations.TestSimpleParameter_Type, new DateTime(3155378975999970000) },
                { TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataReader_TVP_Type, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataReader_TVP_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSimpleDataReader_Type, new DateTime(3155378975999970000) },
                { TestVariations.TestSimpleDataReader_Variant, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopySqlDataReader_Type, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopyDataTable_Type, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopyDataRow_Type, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000) }
            }, null};
            yield return new object[] { 6, DateTimeOffset.MinValue, "System.DateTimeOffset", "datetimeoffset"};
            yield return new object[] { 7, DateTimeOffset.MaxValue, "System.DateTimeOffset", "datetimeoffset"};
            yield return new object[] { 8, DateTimeOffset.Parse("12/31/1999 23:59:59.9999999 -08:30"), "System.DateTimeOffset", "datetimeoffset"};
            yield return new object?[] { 9, DateTime.Parse("1998-01-01 23:59:59.995"), "System.DateTime", "datetime2", null, null, null, null,
            new Dictionary<TestVariations, object>
            {
                {TestVariations.TestSimpleParameter_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                {TestVariations.TestSqlDataRecordParameterToTVP_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                {TestVariations.TestSqlDataReaderParameterToTVP_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                {TestVariations.SqlBulkCopyDataTable_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                {TestVariations.SqlBulkCopyDataRow_Variant, DateTime.Parse("1998-01-01 23:59:59.997")}
            }
            };
            yield return new object?[] { 10, DateTime.MinValue, "System.DateTime", "smalldatetime", null, null,
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Type, SqlDateTimeOverflow },
                { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReader_TVP_Type, VarcharToSmallDateTimeOutOfRange },
                { TestVariations.TestSqlDataReader_TVP_Variant, VarcharToSmallDateTimeOutOfRange },
                { TestVariations.TestSimpleDataReader_Type, VarcharToSmallDateTimeOutOfRange },
                { TestVariations.TestSimpleDataReader_Variant, VarcharToSmallDateTimeOutOfRange },
                { TestVariations.SqlBulkCopySqlDataReader_Type, VarcharToSmallDateTimeOutOfRange },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, VarcharToSmallDateTimeOutOfRange },
                { TestVariations.SqlBulkCopyDataTable_Type, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Type, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }}, null, null};
            yield return new object?[] { 11, DateTime.MaxValue, "System.DateTime", "smalldatetime", null, null,
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Type, UnRepresentableDateTime },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, UnRepresentableDateTime },
                { TestVariations.TestSqlDataReader_TVP_Type, ConversionFailedCharStringToSmallDateTime },
                { TestVariations.TestSqlDataReader_TVP_Variant, ConversionFailedCharStringToSmallDateTime },
                { TestVariations.TestSimpleDataReader_Type, ConversionFailedCharStringToSmallDateTime },
                { TestVariations.TestSimpleDataReader_Variant, ConversionFailedCharStringToSmallDateTime },
                { TestVariations.SqlBulkCopySqlDataReader_Type, ConversionFailedCharStringToSmallDateTime },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, ConversionFailedCharStringToSmallDateTime },
                { TestVariations.SqlBulkCopyDataTable_Type, UnRepresentableDateTime },
                { TestVariations.SqlBulkCopyDataRow_Type, UnRepresentableDateTime }}, null,
            new Dictionary<TestVariations, object> {
                { TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000) },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000) },
                { TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000) }
            }};
            yield return new object?[] { 12, TimeSpan.MinValue, "System.TimeSpan", "time", null, null,
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Type, TimeOverflow },
                { TestVariations.TestSimpleParameter_Variant, TimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, TimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, TimeOverflow },
                { TestVariations.TestSqlDataReader_TVP_Type, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.TestSqlDataReader_TVP_Variant, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.TestSimpleDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.TestSimpleDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.SqlBulkCopySqlDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.SqlBulkCopyDataTable_Type, TimeOverflow },
                { TestVariations.SqlBulkCopyDataTable_Variant, TimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Type, TimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Variant, TimeOverflow }}, null, 
            new Dictionary<TestVariations, object> {
                {TestVariations.TestSqlDataRecordParameterToTVP_Variant, TimeSpan.Zero},
            }};
            yield return new object?[] { 13, TimeSpan.MaxValue, "System.TimeSpan", "time", null, null,
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Type, TimeOverflow },
                { TestVariations.TestSimpleParameter_Variant, TimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidValueForMetadata },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, TdsRpcProtocolStreamIncorrect },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, TimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, TimeOverflow },
                { TestVariations.TestSqlDataReader_TVP_Type, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.TestSqlDataReader_TVP_Variant, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.TestSimpleDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.TestSimpleDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.SqlBulkCopySqlDataReader_Type, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, CannotConvertCharacterStringToDateOrTime },
                { TestVariations.SqlBulkCopyDataTable_Type, TimeOverflow },
                { TestVariations.SqlBulkCopyDataTable_Variant, TimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Type, TimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Variant, TimeOverflow }}, null, null};
            yield return new object?[] { 14, DateTime.MinValue, "System.DateTime", "time", null, 
            new Dictionary<TestVariations, ExceptionChecker> { 
                { TestVariations.SqlBulkCopyDataTable_Type, CannotConvertMinDateTimeToTime},
                { TestVariations.SqlBulkCopyDataRow_Type, CannotConvertMinDateTimeToTime}},
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Type, InvalidCastDateTimeToTimeSpan },
                { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidCastNotValid },
                { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, InvalidCastDateTimeToTimeSpan },
                { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                { TestVariations.TestSqlDataReader_TVP_Type, InvalidCastNotValid },
                { TestVariations.TestSqlDataReader_TVP_Variant, InvalidCastNotValid },
                { TestVariations.TestSimpleDataReader_Type, InvalidCastNotValid },
                { TestVariations.TestSimpleDataReader_Variant, InvalidCastNotValid },
                { TestVariations.SqlBulkCopySqlDataReader_Type, InvalidCastNotValid },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, InvalidCastNotValid },
                { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }}, null, null};
            yield return new object?[] { 15, DateTime.MaxValue, "System.DateTime", "time", null, 
            new Dictionary<TestVariations, ExceptionChecker> { 
                { TestVariations.SqlBulkCopyDataTable_Type, CannotConvertMaxDateTimeToTime },
                { TestVariations.SqlBulkCopyDataRow_Type, CannotConvertMaxDateTimeToTime }},
            new Dictionary<TestVariations, ExceptionChecker> {
                { TestVariations.TestSimpleParameter_Type, InvalidCastDateTimeToTimeSpan },
                { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidCastNotValid },
                { TestVariations.TestSqlDataReaderParameterToTVP_Type, InvalidCastDateTimeToTimeSpan },
                { TestVariations.TestSqlDataReader_TVP_Type, InvalidCastNotValid },
                { TestVariations.TestSqlDataReader_TVP_Variant, InvalidCastNotValid },
                { TestVariations.TestSimpleDataReader_Type, InvalidCastNotValid },
                { TestVariations.TestSimpleDataReader_Variant, InvalidCastNotValid },
                { TestVariations.SqlBulkCopySqlDataReader_Type, InvalidCastNotValid },
                { TestVariations.SqlBulkCopySqlDataReader_Variant, InvalidCastNotValid }}, null, 
            new Dictionary<TestVariations, object> {
                {TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000)},
                {TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000)},
                {TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000)},
                {TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000)},
                {TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000)}
            }};
        }

        private static ExceptionChecker SqlDateTimeOverflow = (e, paramValue) =>
            (e.GetType() == typeof(System.Data.SqlTypes.SqlTypeException)) &&
            e.Message.Contains("SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.");

        private static ExceptionChecker VarcharToDateTimeOutOfRange = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("The conversion of a varchar data type to a datetime data type resulted in an out-of-range value.");

        private static ExceptionChecker CannotConvertMinDateTimeToTime = (e, paramValue) =>
            (e.GetType() == typeof(InvalidOperationException)) &&
            e.Message.Contains("The given value '1/1/0001 12:00:00 AM' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.");

        private static ExceptionChecker CannotConvertMaxDateTimeToTime = (e, paramValue) =>
            (e.GetType() == typeof(InvalidOperationException)) &&
            e.Message.Contains("The given value '12/31/9999 11:59:59 PM' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.");

        private static ExceptionChecker CannotConvertCharacterStringToDateOrTime = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("Conversion failed when converting date and/or time from character string.");

        private static ExceptionChecker InvalidValueForMetadata = (e, paramValue) =>
            (e.GetType() == typeof(ArgumentException)) &&
            e.Message.Contains("Invalid value for this metadata.");

        private static ExceptionChecker VarcharToSmallDateTimeOutOfRange = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.");

        private static ExceptionChecker ConversionFailedCharStringToSmallDateTime = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("Conversion failed when converting character string to smalldatetime data type.");

        private static ExceptionChecker UnRepresentableDateTime = (e, paramValue) =>
            (e.GetType() == typeof(ArgumentOutOfRangeException)) &&
            e.Message.Contains("The added or subtracted value results in an un-representable DateTime.");

        private static ExceptionChecker TimeOverflow = (e, paramValue) =>
            (e.GetType() == typeof(OverflowException)) &&
            e.Message.Contains("SqlDbType.Time overflow.");

        private static ExceptionChecker InvalidCastDateTimeToTimeSpan = (e, paramValue) =>
            (e.GetType() == typeof(InvalidCastException)) &&
            e.Message.Contains("Failed to convert parameter value from a DateTime to a TimeSpan.");

        private static ExceptionChecker InvalidCastNotValid = (e, paramValue) =>
            (e.GetType() == typeof(InvalidCastException)) &&
            e.Message.Contains("Specified cast is not valid.");

        private static ExceptionChecker TdsRpcProtocolStreamIncorrect = (e, paramValue) =>
            (e.GetType() == typeof(SqlException)) &&
            e.Message.Contains("The incoming tabular data stream (TDS) remote procedure call (RPC) protocol stream is incorrect.");

        private bool RunTestAndCompareWithBaseline(
            int paramIndex, 
            object paramValue,
            string expectedTypeName, 
            string expectedBaseTypeName, 
            Dictionary<TestVariations, ExceptionChecker> expectedExceptions, 
            Dictionary<TestVariations, ExceptionChecker> expectedInvalidOperationExceptions,
            Dictionary<TestVariations, ExceptionChecker> expectedButUncaughtExceptions,
            Dictionary<TestVariations, object> expectedValueOverrides,
            Dictionary<TestVariations, object> unexpectedValueOverrides)
        {
            string outputPath = $"DateTimeVariant_{paramIndex}.out";

            var fstream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var swriter = new StreamWriter(fstream, Encoding.UTF8);
            var twriter = new TvpTest.CarriageReturnLineFeedReplacer(swriter);
            Console.SetOut(twriter);

            // Run Test - calls 16 methods for this parameter combination
            DateTimeVariantTest.SendInfo(
                paramValue, 
                expectedTypeName, 
                expectedBaseTypeName, 
                DataTestUtility.TCPConnectionString, 
                expectedExceptions, 
                expectedInvalidOperationExceptions,
                expectedButUncaughtExceptions,
                expectedValueOverrides,
                unexpectedValueOverrides);

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
