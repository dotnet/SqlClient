// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using Xunit;

#nullable enable
namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.ParameterTest
{
    public sealed class NumericVariantTestData : DataTypeTestDataBase
    {
        private static readonly ExceptionChecker s_conversionFailedCharToSmallMoney = (e, paramValue) =>
            e is SqlException sqlEx && sqlEx.Number == 294;
        private static readonly ExceptionChecker s_smallMoneyOverflow = (e, paramValue) =>
            e is OverflowException;
        private static readonly ExceptionChecker s_decimalOverflow = (e, paramValue) =>
            e is OverflowException;
        private static readonly ExceptionChecker s_decimalBulkCopyOverflow = (e, paramValue) =>
            e is InvalidOperationException && e.InnerException is OverflowException;

        public override IEnumerable<object?[]> TestData
        {
            get
            {
                yield return GenerateSuccessCase(value: true, "bit");
                yield return GenerateSuccessCase(value: false, "bit");
                yield return GenerateSuccessCase(inputValue: SqlBoolean.True, outputValue: true, "bit");
                yield return GenerateSuccessCase(inputValue: SqlBoolean.False, outputValue: false, "bit");

                yield return GenerateSuccessCase(value: byte.MinValue, "tinyint");
                // No coverage for -50: this is outside of the bounds of SQL Server tinyints
                yield return GenerateSuccessCase(value: (byte)50, "tinyint");
                yield return GenerateSuccessCase(value: byte.MaxValue, "tinyint");
                yield return GenerateSuccessCase(inputValue: SqlByte.MinValue, outputValue: byte.MinValue, "tinyint");
                yield return GenerateSuccessCase(inputValue: new SqlByte(50), outputValue: (byte)50, "tinyint");
                yield return GenerateSuccessCase(inputValue: SqlByte.MaxValue, outputValue: byte.MaxValue, "tinyint");

                yield return GenerateSuccessCase(value: short.MinValue, "smallint");
                yield return GenerateSuccessCase(value: (short)-50, "smallint");
                yield return GenerateSuccessCase(value: (short)+50, "smallint");
                yield return GenerateSuccessCase(value: short.MaxValue, "smallint");
                yield return GenerateSuccessCase(inputValue: SqlInt16.MinValue, outputValue: short.MinValue, "smallint");
                yield return GenerateSuccessCase(inputValue: new SqlInt16(-50), outputValue: (short)-50, "smallint");
                yield return GenerateSuccessCase(inputValue: new SqlInt16(+50), outputValue: (short)+50, "smallint");
                yield return GenerateSuccessCase(inputValue: SqlInt16.MaxValue, outputValue: short.MaxValue, "smallint");

                yield return GenerateSuccessCase(value: int.MinValue, "int");
                yield return GenerateSuccessCase(value: -50, "int");
                yield return GenerateSuccessCase(value: +50, "int");
                yield return GenerateSuccessCase(value: int.MaxValue, "int");
                yield return GenerateSuccessCase(inputValue: SqlInt32.MinValue, outputValue: int.MinValue, "int");
                yield return GenerateSuccessCase(inputValue: new SqlInt32(-50), outputValue: -50, "int");
                yield return GenerateSuccessCase(inputValue: new SqlInt32(+50), outputValue: +50, "int");
                yield return GenerateSuccessCase(inputValue: SqlInt32.MaxValue, outputValue: int.MaxValue, "int");

                yield return GenerateSuccessCase(value: long.MinValue, "bigint");
                yield return GenerateSuccessCase(value: (long)-50, "bigint");
                yield return GenerateSuccessCase(value: (long)+50, "bigint");
                yield return GenerateSuccessCase(value: long.MaxValue, "bigint");
                yield return GenerateSuccessCase(inputValue: SqlInt64.MinValue, outputValue: long.MinValue, "bigint");
                yield return GenerateSuccessCase(inputValue: new SqlInt64(-50), outputValue: (long)-50, "bigint");
                yield return GenerateSuccessCase(inputValue: new SqlInt64(+50), outputValue: (long)+50, "bigint");
                yield return GenerateSuccessCase(inputValue: SqlInt64.MaxValue, outputValue: long.MaxValue, "bigint");

                yield return GenerateSuccessCase(value: float.MinValue, "real");
                yield return GenerateSuccessCase(value: -50.5F, "real");
                yield return GenerateSuccessCase(value: +50.5F, "real");
                yield return GenerateSuccessCase(value: float.MaxValue, "real");
                yield return GenerateSuccessCase(inputValue: SqlSingle.MinValue, outputValue: float.MinValue, "real");
                yield return GenerateSuccessCase(inputValue: new SqlSingle(-50.5F), outputValue: -50.5F, "real");
                yield return GenerateSuccessCase(inputValue: new SqlSingle(+50.5F), outputValue: +50.5F, "real");
                yield return GenerateSuccessCase(inputValue: SqlSingle.MaxValue, outputValue: float.MaxValue, "real");

                yield return GenerateSuccessCase(value: double.MinValue, "float");
                yield return GenerateSuccessCase(value: -50.5D, "float");
                yield return GenerateSuccessCase(value: +50.5D, "float");
                yield return GenerateSuccessCase(value: double.MaxValue, "float");
                yield return GenerateSuccessCase(inputValue: SqlDouble.MinValue, outputValue: double.MinValue, "float");
                yield return GenerateSuccessCase(inputValue: new SqlDouble(-50.5D), outputValue: -50.5D, "float");
                yield return GenerateSuccessCase(inputValue: new SqlDouble(+50.5D), outputValue: +50.5D, "float");
                yield return GenerateSuccessCase(inputValue: SqlDouble.MaxValue, outputValue: double.MaxValue, "float");

                // System.Decimal values of parameter type "smallmoney" have a server-side type of "numeric" or "money" in some cases.
                // This also masks client-side or server-side overflows, since the upper and lower bounds of these types differ.
                yield return [ SqlMoney.MinValue.Value, "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Type, s_smallMoneyOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Type, s_smallMoneyOverflow },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, s_conversionFailedCharToSmallMoney },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSimpleDataReader_Type, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSimpleDataReader_Variant, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSimpleParameter_Type, s_smallMoneyOverflow },
                        { TestVariations.TestSqlDataReader_TVP_Type, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSqlDataReader_TVP_Variant, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, s_smallMoneyOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, s_smallMoneyOverflow },
                    },
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return [ SqlMoney.MaxValue.Value, "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Type, s_smallMoneyOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Type, s_smallMoneyOverflow },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, s_conversionFailedCharToSmallMoney },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSimpleDataReader_Type, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSimpleDataReader_Variant, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSimpleParameter_Type, s_smallMoneyOverflow },
                        { TestVariations.TestSqlDataReader_TVP_Type, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSqlDataReader_TVP_Variant, s_conversionFailedCharToSmallMoney },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, s_smallMoneyOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, s_smallMoneyOverflow },
                    },
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];

                yield return [ -214748.3648M, "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return [ -50.5M, "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return [ +50.5M, "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return [ +214748.3647M, "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];

                // SqlMoney values of parameter type "smallmoney" have a server-side type of "money" in some cases.
                yield return [ new SqlMoney(-214748.3648M), "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>()
                    {
                        { TestVariations.TestSimpleParameter_Type, -214748.3648M },
                        { TestVariations.TestSimpleParameter_Variant, -214748.3648M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, -214748.3648M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, -214748.3648M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, -214748.3648M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, -214748.3648M },
                        { TestVariations.TestSqlDataReader_TVP_Type, -214748.3648M },
                        { TestVariations.TestSqlDataReader_TVP_Variant, -214748.3648M },
                        { TestVariations.TestSimpleDataReader_Type, -214748.3648M },
                        { TestVariations.TestSimpleDataReader_Variant, -214748.3648M },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, -214748.3648M },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, -214748.3648M },
                        { TestVariations.SqlBulkCopyDataTable_Type, -214748.3648M },
                        { TestVariations.SqlBulkCopyDataTable_Variant, -214748.3648M },
                        { TestVariations.SqlBulkCopyDataRow_Type, -214748.3648M },
                        { TestVariations.SqlBulkCopyDataRow_Variant, -214748.3648M }
                    },
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "money" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "money" },
                        { TestVariations.TestSimpleParameter_Variant, "money" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "money" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "money" },
                    }
                ];
                yield return [ new SqlMoney(-50.5M), "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>()
                    {
                        { TestVariations.TestSimpleParameter_Type, -50.5M },
                        { TestVariations.TestSimpleParameter_Variant, -50.5M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, -50.5M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, -50.5M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, -50.5M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, -50.5M },
                        { TestVariations.TestSqlDataReader_TVP_Type, -50.5M },
                        { TestVariations.TestSqlDataReader_TVP_Variant, -50.5M },
                        { TestVariations.TestSimpleDataReader_Type, -50.5M },
                        { TestVariations.TestSimpleDataReader_Variant, -50.5M },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, -50.5M },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, -50.5M },
                        { TestVariations.SqlBulkCopyDataTable_Type, -50.5M },
                        { TestVariations.SqlBulkCopyDataTable_Variant, -50.5M },
                        { TestVariations.SqlBulkCopyDataRow_Type, -50.5M },
                        { TestVariations.SqlBulkCopyDataRow_Variant, -50.5M }
                    },
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "money" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "money" },
                        { TestVariations.TestSimpleParameter_Variant, "money" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "money" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "money" },
                    }
                ];
                yield return [ new SqlMoney(+50.5M), "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>()
                    {
                        { TestVariations.TestSimpleParameter_Type, +50.5M },
                        { TestVariations.TestSimpleParameter_Variant, +50.5M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, +50.5M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, +50.5M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, +50.5M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, +50.5M },
                        { TestVariations.TestSqlDataReader_TVP_Type, +50.5M },
                        { TestVariations.TestSqlDataReader_TVP_Variant, +50.5M },
                        { TestVariations.TestSimpleDataReader_Type, +50.5M },
                        { TestVariations.TestSimpleDataReader_Variant, +50.5M },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, +50.5M },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, +50.5M },
                        { TestVariations.SqlBulkCopyDataTable_Type, +50.5M },
                        { TestVariations.SqlBulkCopyDataTable_Variant, +50.5M },
                        { TestVariations.SqlBulkCopyDataRow_Type, +50.5M },
                        { TestVariations.SqlBulkCopyDataRow_Variant, +50.5M }
                    },
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "money" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "money" },
                        { TestVariations.TestSimpleParameter_Variant, "money" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "money" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "money" },
                    }
                ];
                yield return [ new SqlMoney(+214748.3647M), "smallmoney",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>()
                    {
                        { TestVariations.TestSimpleParameter_Type, +214748.3647M },
                        { TestVariations.TestSimpleParameter_Variant, +214748.3647M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, +214748.3647M },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, +214748.3647M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, +214748.3647M },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, +214748.3647M },
                        { TestVariations.TestSqlDataReader_TVP_Type, +214748.3647M },
                        { TestVariations.TestSqlDataReader_TVP_Variant, +214748.3647M },
                        { TestVariations.TestSimpleDataReader_Type, +214748.3647M },
                        { TestVariations.TestSimpleDataReader_Variant, +214748.3647M },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, +214748.3647M },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, +214748.3647M },
                        { TestVariations.SqlBulkCopyDataTable_Type, +214748.3647M },
                        { TestVariations.SqlBulkCopyDataTable_Variant, +214748.3647M },
                        { TestVariations.SqlBulkCopyDataRow_Type, +214748.3647M },
                        { TestVariations.SqlBulkCopyDataRow_Variant, +214748.3647M }
                    },
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "money" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "money" },
                        { TestVariations.TestSimpleParameter_Variant, "money" },
                        { TestVariations.TestSqlDataReader_TVP_Variant, "money" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "money" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "money" },
                    }
                ];

                // System.Decimal values of parameter type "money" have a server-side type of "numeric" in some cases.
                yield return [ SqlMoney.MinValue.Value, "money",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return [ -50.5M, "money",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return [ +50.5M, "money",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return [ SqlMoney.MaxValue.Value, "money",
                    new Dictionary<TestVariations, ExceptionChecker>(),
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Variant, "numeric" },
                        { TestVariations.SqlBulkCopyDataTable_Variant, "numeric" },
                        { TestVariations.TestSimpleParameter_Variant, "numeric" },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, "numeric" },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, "numeric" },
                    }
                ];
                yield return GenerateSuccessCase(inputValue: SqlMoney.MinValue, outputValue: SqlMoney.MinValue.Value, "money");
                yield return GenerateSuccessCase(inputValue: new SqlMoney(-50.5M), outputValue: -50.5M, "money");
                yield return GenerateSuccessCase(inputValue: new SqlMoney(+50.5M), outputValue: +50.5M, "money");
                yield return GenerateSuccessCase(inputValue: SqlMoney.MaxValue, outputValue: SqlMoney.MaxValue.Value, "money");

                yield return GenerateSuccessCase(value: decimal.MinValue, "numeric(29,0)");
                yield return GenerateSuccessCase(value: -50M, "numeric(29,0)");
                yield return GenerateSuccessCase(value: +50M, "numeric(29,0)");
                yield return GenerateSuccessCase(value: decimal.MaxValue, "numeric(29,0)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(decimal.MinValue), outputValue: decimal.MinValue, "numeric(29,0)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(-50M), outputValue: -50M, "numeric(29,0)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(+50M), outputValue: +50M, "numeric(29,0)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(decimal.MaxValue), outputValue: decimal.MaxValue, "numeric(29,0)");

                // System.Decimal supports up to 29 significant figures, so a SqlDecimal's minimum or
                // maximum value will always overflow. This overflow can occur during a bulk copy
                // or when calling SqlDataReader.GetValue (which returns a System.Decimal for SQL
                // Server values of the numeric data type.)
                yield return [ SqlDecimal.MinValue, "numeric(38,0)",
                    new Dictionary<TestVariations, ExceptionChecker>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Type, s_decimalOverflow },
                        { TestVariations.SqlBulkCopyDataRow_Variant, s_decimalBulkCopyOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Type, s_decimalOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Variant, s_decimalBulkCopyOverflow },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, s_decimalOverflow },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, s_decimalOverflow },
                        { TestVariations.TestSimpleDataReader_Type, s_decimalOverflow },
                        { TestVariations.TestSimpleDataReader_Variant, s_decimalOverflow },
                        { TestVariations.TestSimpleParameter_Type, s_decimalOverflow },
                        { TestVariations.TestSimpleParameter_Variant, s_decimalOverflow },
                        { TestVariations.TestSqlDataReader_TVP_Type, s_decimalOverflow },
                        { TestVariations.TestSqlDataReader_TVP_Variant, s_decimalOverflow },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, s_decimalOverflow },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, s_decimalOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, s_decimalOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, s_decimalOverflow },
                    },
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                ];
                yield return [ SqlDecimal.MaxValue, "numeric(38,0)",
                    new Dictionary<TestVariations, ExceptionChecker>()
                    {
                        { TestVariations.SqlBulkCopyDataRow_Type, s_decimalOverflow },
                        { TestVariations.SqlBulkCopyDataRow_Variant, s_decimalBulkCopyOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Type, s_decimalOverflow },
                        { TestVariations.SqlBulkCopyDataTable_Variant, s_decimalBulkCopyOverflow },
                        { TestVariations.SqlBulkCopySqlDataReader_Type, s_decimalOverflow },
                        { TestVariations.SqlBulkCopySqlDataReader_Variant, s_decimalOverflow },
                        { TestVariations.TestSimpleDataReader_Type, s_decimalOverflow },
                        { TestVariations.TestSimpleDataReader_Variant, s_decimalOverflow },
                        { TestVariations.TestSimpleParameter_Type, s_decimalOverflow },
                        { TestVariations.TestSimpleParameter_Variant, s_decimalOverflow },
                        { TestVariations.TestSqlDataReader_TVP_Type, s_decimalOverflow },
                        { TestVariations.TestSqlDataReader_TVP_Variant, s_decimalOverflow },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Type, s_decimalOverflow },
                        { TestVariations.TestSqlDataReaderParameterToTVP_Variant, s_decimalOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Type, s_decimalOverflow },
                        { TestVariations.TestSqlDataRecordParameterToTVP_Variant, s_decimalOverflow },
                    },
                    new Dictionary<TestVariations, object>(),
                    new Dictionary<TestVariations, string>()
                ];

                yield return GenerateSuccessCase(value: -50.5M, "numeric(3,1)");
                yield return GenerateSuccessCase(value: +50.5M, "numeric(3,1)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(-50.5M), outputValue: -50.5M, "numeric(3,1)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(+50.5M), outputValue: +50.5M, "numeric(3,1)");

                yield return GenerateSuccessCase(value: -0.05M, "numeric(2,2)");
                yield return GenerateSuccessCase(value: +0.05M, "numeric(2,2)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(-0.05M), outputValue: -0.05M, "numeric(2,2)");
                yield return GenerateSuccessCase(inputValue: new SqlDecimal(+0.05M), outputValue: +0.05M, "numeric(2,2)");
            }
        }
    }

    /// <summary>
    /// Tests for numeric variant parameters with different numeric types.
    /// </summary>
    [Trait("Set", "3")]
    public class NumericVariantTests : DataTypeTestBase<NumericVariantTestData>
    {
        protected override string ManagedTypeToString(object value) =>
            value switch
            {
                bool typedVal => typedVal ? "1" : "0",
                SqlBoolean typedVal => typedVal.IsTrue ? "1" : "0",
                // The ToString behaviour of float and double differs between netcore and netfx,
                // returning a different number of significant figures. Enforce 9 s.f for floats
                // and 17 for doubles - enough digits to represent the MinValue and MaxValue of each.
                float typedVal => typedVal.ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
                SqlSingle typedVal => typedVal.Value.ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
                double typedVal => typedVal.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
                SqlDouble typedVal => typedVal.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString()!
            };
    }
}
