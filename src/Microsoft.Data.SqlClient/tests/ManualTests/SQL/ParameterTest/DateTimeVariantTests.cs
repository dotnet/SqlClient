// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using Xunit;

#nullable enable
namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.ParameterTest
{
    public sealed class DateTimeVariantTestData : DataTypeTestDataBase
    {
        public override IEnumerable<object?[]> TestData
        {
            get
            {
                #if NET
                yield return GenerateSuccessCase(inputValue: DateOnly.MinValue, outputValue: new DateTime(0), "date");
                yield return GenerateSuccessCase(inputValue: DateOnly.MaxValue, outputValue: new DateTime(3155378112000000000), "date");
                #endif
                yield return new object[] { DateTime.MinValue, "date",
                    new Dictionary<TestVariations, ExceptionChecker> {
                        { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                        { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }},
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()};
                yield return new object[] { DateTime.MaxValue, "date",
                    new Dictionary<TestVariations, ExceptionChecker>(),
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
                    },
                    new Dictionary<TestVariations, string>
                    {
                        {TestVariations.TestSimpleParameter_Variant, "datetime"},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                    }};
                yield return new object[] { DateTime.MinValue, "datetime2",
                    new Dictionary<TestVariations, ExceptionChecker> {
                        { TestVariations.TestSimpleParameter_Variant, SqlDateTimeOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, SqlDateTimeOverflow },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, SqlDateTimeOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Variant, SqlDateTimeOverflow },
                        { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }},
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()};
                yield return new object[] { DateTime.MaxValue, "datetime2",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object> {
                        {TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000)}
                    },
                    new Dictionary<TestVariations, string>
                    {
                        {TestVariations.TestSimpleParameter_Variant, "datetime"},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                    }};
                yield return new object[] { DateTime.MinValue, "datetime",
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
                        { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow}},
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()};
                yield return new object[] { DateTime.MaxValue, "datetime",
                    new Dictionary<TestVariations, ExceptionChecker> {
                        { TestVariations.TestSqlDataReader_TVP_Type, CannotConvertCharacterStringToDateOrTime},
                        { TestVariations.TestSqlDataReader_TVP_Variant, CannotConvertCharacterStringToDateOrTime},
                        { TestVariations.TestSimpleDataReader_Type, CannotConvertCharacterStringToDateOrTime},
                        { TestVariations.TestSimpleDataReader_Variant, CannotConvertCharacterStringToDateOrTime},
                        { TestVariations.SqlBulkCopySqlDataReader_Type, CannotConvertCharacterStringToDateOrTime},
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, CannotConvertCharacterStringToDateOrTime}},
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
                    },
                    new Dictionary<TestVariations, string>()};
                yield return GenerateSuccessCase(value: DateTimeOffset.MinValue, "datetimeoffset");
                yield return GenerateSuccessCase(value: DateTimeOffset.MaxValue, "datetimeoffset");
                yield return GenerateSuccessCase(value: DateTimeOffset.Parse("1999-12-31 23:59:59.9999999 -08:30"), "datetimeoffset");
                yield return new object[] { DateTime.Parse("1998-01-01 23:59:59.995"), "datetime2",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>
                    {
                        {TestVariations.TestSimpleParameter_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                        {TestVariations.SqlBulkCopyDataTable_Variant, DateTime.Parse("1998-01-01 23:59:59.997")},
                        {TestVariations.SqlBulkCopyDataRow_Variant, DateTime.Parse("1998-01-01 23:59:59.997")}
                    },
                    new Dictionary<TestVariations, string>
                    {
                        {TestVariations.TestSimpleParameter_Variant, "datetime"},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                    }};
                yield return new object[] { DateTime.MinValue, "smalldatetime",
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
                        { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }},
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()};
                yield return new object[] { DateTime.MaxValue, "smalldatetime",
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
                        { TestVariations.SqlBulkCopyDataRow_Type, UnRepresentableDateTime }},
                    new Dictionary<TestVariations, object> {
                        { TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000) },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000) },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000) },
                        { TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000) },
                        { TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000) }
                    },
                    new Dictionary<TestVariations, string>
                    {
                        {TestVariations.TestSimpleParameter_Variant, "datetime"},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                    }};
                #if NET
                yield return new object[] { TimeOnly.MinValue, "time",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>()
                    {
                        { TestVariations.TestSimpleParameter_Type, TimeSpan.Zero },
                        { TestVariations.TestSimpleParameter_Variant, TimeSpan.Zero },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, TimeSpan.Zero },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, TimeSpan.Zero },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, TimeSpan.Zero },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, TimeSpan.Zero },
                        { TestVariations.TestSqlDataReader_TVP_Type, TimeSpan.Zero },
                        { TestVariations.TestSqlDataReader_TVP_Variant, TimeSpan.Zero },
                        { TestVariations.TestSimpleDataReader_Type, TimeSpan.Zero },
                        { TestVariations.TestSimpleDataReader_Variant, TimeSpan.Zero },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, TimeSpan.Zero },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, TimeSpan.Zero },
                        { TestVariations.SqlBulkCopyDataTable_Type, TimeSpan.Zero },
                        { TestVariations.SqlBulkCopyDataTable_Variant, TimeSpan.Zero },
                        { TestVariations.SqlBulkCopyDataRow_Type, TimeSpan.Zero },
                        { TestVariations.SqlBulkCopyDataRow_Variant, TimeSpan.Zero }
                    },
                    new Dictionary<TestVariations, string>()};
                yield return new object[] { TimeOnly.MaxValue, "time",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>()
                    {
                        { TestVariations.TestSimpleParameter_Type, new TimeSpan(863999999999) },
                        { TestVariations.TestSimpleParameter_Variant, new TimeSpan(863999999999) },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, new TimeSpan(863999999999) },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, new TimeSpan(863999999999) },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, new TimeSpan(863999999999) },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, new TimeSpan(863999999999) },
                        { TestVariations.TestSqlDataReader_TVP_Type, new TimeSpan(863999999999) },
                        { TestVariations.TestSqlDataReader_TVP_Variant, new TimeSpan(863999999999) },
                        { TestVariations.TestSimpleDataReader_Type, new TimeSpan(863999999999) },
                        { TestVariations.TestSimpleDataReader_Variant, new TimeSpan(863999999999) },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, new TimeSpan(863999999999) },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, new TimeSpan(863999999999) },
                        { TestVariations.SqlBulkCopyDataTable_Type, new TimeSpan(863999999999) },
                        { TestVariations.SqlBulkCopyDataTable_Variant, new TimeSpan(863999999999) },
                        { TestVariations.SqlBulkCopyDataRow_Type, new TimeSpan(863999999999) },
                        { TestVariations.SqlBulkCopyDataRow_Variant, new TimeSpan(863999999999) }
                    },
                    new Dictionary<TestVariations, string>()};
                #endif
                yield return new object[] { TimeSpan.MinValue, "time",
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
                        { TestVariations.SqlBulkCopyDataRow_Variant, TimeOverflow }},
                    new Dictionary<TestVariations, object> {
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, TimeSpan.Zero},
                    },
                    new Dictionary<TestVariations, string>()};
                yield return GenerateSuccessCase(value: TimeSpan.FromSeconds(1.0), "time");
                yield return new object[] { TimeSpan.MaxValue, "time",
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
                        { TestVariations.SqlBulkCopyDataRow_Variant, TimeOverflow }},
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()};
                yield return new object[] { DateTime.MinValue, "time",
                    new Dictionary<TestVariations, ExceptionChecker> {
                        { TestVariations.SqlBulkCopyDataTable_Type, CannotConvertMinDateTimeToTime},
                        { TestVariations.SqlBulkCopyDataRow_Type, CannotConvertMinDateTimeToTime},
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
                        { TestVariations.SqlBulkCopyDataRow_Variant, SqlDateTimeOverflow }},
                    new Dictionary<TestVariations, object> {
                        {TestVariations.SqlBulkCopySqlDataReader_Type, TimeSpan.Zero},
                        {TestVariations.SqlBulkCopySqlDataReader_Variant, TimeSpan.Zero},
                        {TestVariations.TestSqlDataReader_TVP_Type, TimeSpan.Zero},
                        {TestVariations.TestSqlDataReader_TVP_Variant, TimeSpan.Zero},
                        {TestVariations.TestSimpleDataReader_Type, TimeSpan.Zero},
                        {TestVariations.TestSimpleDataReader_Variant, TimeSpan.Zero},
                    },
                    new Dictionary<TestVariations, string>()};
                yield return new object[] { DateTime.MaxValue, "time",
                    new Dictionary<TestVariations, ExceptionChecker> {
                        { TestVariations.SqlBulkCopyDataTable_Type, CannotConvertMaxDateTimeToTime },
                        { TestVariations.SqlBulkCopyDataRow_Type, CannotConvertMaxDateTimeToTime },
                        { TestVariations.TestSimpleParameter_Type, InvalidCastDateTimeToTimeSpan },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, InvalidCastNotValid },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, InvalidCastDateTimeToTimeSpan },
                        { TestVariations.TestSqlDataReader_TVP_Type, InvalidCastNotValid },
                        { TestVariations.TestSqlDataReader_TVP_Variant, InvalidCastNotValid },
                        { TestVariations.TestSimpleDataReader_Type, InvalidCastNotValid },
                        { TestVariations.TestSimpleDataReader_Variant, InvalidCastNotValid },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, InvalidCastNotValid },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, InvalidCastNotValid }},
                    new Dictionary<TestVariations, object> {
                        {TestVariations.TestSqlDataReader_TVP_Variant, TimeSpan.Parse("23:59:59.9999999")},
                        {TestVariations.SqlBulkCopySqlDataReader_Type, TimeSpan.Parse("23:59:59.9999999")},
                        {TestVariations.SqlBulkCopySqlDataReader_Variant, TimeSpan.Parse("23:59:59.9999999")},
                        {TestVariations.TestSqlDataReader_TVP_Type, TimeSpan.Parse("23:59:59.9999999")},
                        {TestVariations.TestSimpleDataReader_Type, TimeSpan.Parse("23:59:59.9999999")},
                        {TestVariations.TestSimpleDataReader_Variant, TimeSpan.Parse("23:59:59.9999999")},
                        {TestVariations.TestSimpleParameter_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.SqlBulkCopyDataTable_Variant, new DateTime(3155378975999970000)},
                        {TestVariations.SqlBulkCopyDataRow_Variant, new DateTime(3155378975999970000)}
                    },
                    new Dictionary<TestVariations, string>
                    {
                        {TestVariations.TestSimpleParameter_Variant, "datetime"},
                        {TestVariations.TestSqlDataRecordParameterToTVP_Variant, "datetime"},
                        {TestVariations.TestSqlDataReaderParameterToTVP_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataTable_Variant, "datetime"},
                        {TestVariations.SqlBulkCopyDataRow_Variant, "datetime"}
                    }};
            }
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
    }

    /// <summary>
    /// Tests for DateTime variant parameters with different date/time types.
    /// </summary>
    [Trait("Set", "3")]
    [Trait("category", "flaky")]
    public class DateTimeVariantTests : DataTypeTestBase<DateTimeVariantTestData>
    {
        protected override string ManagedTypeToString(object value)
        {
            if (value.GetType() == typeof(DateTimeOffset))
            {
                DateTime dt = ((DateTimeOffset)value).UtcDateTime;
                return dt.ToString("M/d/yyyy") + " " + dt.TimeOfDay;
            }
            else if (value.GetType() == typeof(TimeSpan))
            {
                return ((TimeSpan)value).ToString();
            }
            #if NET
            else if (value.GetType() == typeof(DateOnly))
            {
                return ((DateOnly)value).ToString();
            }
            else if (value.GetType() == typeof(TimeOnly))
            {
                return ((TimeOnly)value).ToString("O");
            }
            #endif
            else
            {
                return ((DateTime)value).ToString("M/d/yyyy") + " " + ((DateTime)value).TimeOfDay;
            }
        }
    }
}
