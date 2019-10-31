// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [PlatformSpecific(TestPlatforms.Windows)]
    public class ConversionTests : IDisposable
    {

        private const string IdentityColumnName = "IdentityColumn";
        private const string FirstColumnName = "Column1";
        private const string FirstParamName = "@Param1";
        private const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA_256";
        private const decimal SmallMoneyMaxValue = 214748.3647M;
        private const decimal SmallMoneyMinValue = -214748.3648M;
        private const int MaxLength = 10000;
        private int NumberOfRows = DataTestUtility.EnclaveEnabled ? 10 : 100;
        private readonly X509Certificate2 certificate;
        private ColumnMasterKey columnMasterKey;
        private ColumnEncryptionKey columnEncryptionKey;
        private SqlColumnEncryptionCertificateStoreProvider certStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();
        protected List<DbObject> databaseObjects = new List<DbObject>();

        private class ColumnMetaData
        {
            public ColumnMetaData(SqlDbType columnType, int columnSize, int precision, int scale, bool useMax)
            {
                ColumnType = columnType;
                ColumnSize = columnSize;
                Precision = precision;
                Scale = scale;
                UseMax = useMax;
            }

            public SqlDbType ColumnType { get; set; }
            public int ColumnSize { get; set; }
            public int Precision { get; set; }
            public int Scale { get; set; }
            public bool UseMax { get; set; }
        }

        public ConversionTests()
        {
            certificate = CertificateUtility.CreateCertificate();
            columnMasterKey = new CspColumnMasterKey(DatabaseHelper.GenerateUniqueName("CMK"), certificate.Thumbprint, certStoreProvider, DataTestUtility.EnclaveEnabled);
            databaseObjects.Add(columnMasterKey);

            columnEncryptionKey = new ColumnEncryptionKey(DatabaseHelper.GenerateUniqueName("CEK"),
                                                          columnMasterKey,
                                                          certStoreProvider);
            databaseObjects.Add(columnEncryptionKey);

            foreach(string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connectionStr))
                {
                    sqlConnection.Open();
                    databaseObjects.ForEach(o => o.Create(sqlConnection));
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(ConversionSmallerToLargerInsertAndSelectData))]
        public void ConversionSmallerToLargerInsertAndSelect(string connString, SqlDbType smallDbType, SqlDbType largeDbType)
        {
            ColumnMetaData largeColumnInfo = new ColumnMetaData(largeDbType, 0, 1, 1, false);
            ColumnMetaData smallColumnInfo = new ColumnMetaData(smallDbType, 0, 1, 1, false);

            // Adjust the size, precision and scale for data types that have one.
            AdjustSizePrecisionAndScale(ref largeColumnInfo, ref smallColumnInfo);

            // Create the encrypted and unencrypted table with the proper column types.
            string encryptedTableName = DatabaseHelper.GenerateUniqueName("encrypted");
            string unencryptedTableName = DatabaseHelper.GenerateUniqueName("unencrypted");

            // Create the encrypted and unencrypted table with the proper column types.
            CreateTable(connString, largeColumnInfo, encryptedTableName, isEncrypted: true);
            CreateTable(connString, largeColumnInfo, unencryptedTableName, isEncrypted: false);

            // Insert data using the smaller type to the tables with the large type.
            object[] rawValues = PopulateTablesAndReturnRandomValue(connString, encryptedTableName, unencryptedTableName, smallColumnInfo);

            // Keep the values from unencryptedTable other than the rawValues to perform a select later for DateTime2 and DateTimeOffset.
            object[] valuesToSelect = RetriveDataFromDatabase(connString, unencryptedTableName);

            // Now read back everything and make sure the values and types are identical.
            CompareTables(connString, encryptedTableName, unencryptedTableName);

            // Now send a query with a predicate using the larger type and confirm that the row that was inserted with the smaller type can still be found.
            using (SqlConnection sqlConnectionEncrypted = new SqlConnection(connString))
            using (SqlConnection sqlConnectionUnencrypted = new SqlConnection(connString))
            {
                sqlConnectionEncrypted.Open();
                sqlConnectionUnencrypted.Open();

                try
                {
                    // Select each value we just inserted with a predicate and verify that encrypted and unencrypted return the same result.
                    for (int i = 0; i < NumberOfRows; i++)
                    {
                        object value;

                        // Use the retrieved values for DateTime2 and DateTimeOffset due to fractional insertion adjustment
                        if (smallColumnInfo.ColumnType is SqlDbType.DateTime2 || smallColumnInfo.ColumnType is SqlDbType.DateTimeOffset)
                        {
                            value = valuesToSelect[i];
                        }
                        else
                        {
                            value = rawValues[i];
                        }

                        using (SqlCommand cmdEncrypted = new SqlCommand(string.Format(@"SELECT {0} FROM [{1}] WHERE {0} = {2}", FirstColumnName, encryptedTableName, FirstParamName), sqlConnectionEncrypted, null, SqlCommandColumnEncryptionSetting.Enabled))
                        using (SqlCommand cmdUnencrypted = new SqlCommand(string.Format(@"SELECT {0} FROM [{1}] WHERE {0} = {2}", FirstColumnName, unencryptedTableName, FirstParamName), sqlConnectionUnencrypted, null, SqlCommandColumnEncryptionSetting.Disabled))
                        {
                            SqlParameter paramEncrypted = new SqlParameter();
                            paramEncrypted.ParameterName = FirstParamName;
                            paramEncrypted.SqlDbType = largeDbType;
                            SetParamSizeScalePrecision(ref paramEncrypted, largeColumnInfo);
                            paramEncrypted.Value = value;
                            cmdEncrypted.Parameters.Add(paramEncrypted);

                            SqlParameter paramUnencrypted = new SqlParameter();
                            paramUnencrypted.ParameterName = FirstParamName;
                            paramUnencrypted.SqlDbType = largeDbType;
                            SetParamSizeScalePrecision(ref paramUnencrypted, largeColumnInfo);
                            paramUnencrypted.Value = value;
                            cmdUnencrypted.Parameters.Add(paramUnencrypted);

                            using (SqlDataReader readerUnencrypted = cmdUnencrypted.ExecuteReader())
                            using (SqlDataReader readerEncrypted = cmdEncrypted.ExecuteReader())
                            {
                                // First check that we found some rows.
                                Assert.True(readerEncrypted.HasRows, @"We didn't find any rows.");

                                // Now compare the result.
                                CompareResults(readerEncrypted, readerUnencrypted);
                            }
                        }
                    }
                }
                finally
                {
                    // DropTables
                    DropTableIfExists(sqlConnectionEncrypted, encryptedTableName);
                    DropTableIfExists(sqlConnectionUnencrypted, unencryptedTableName);
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(ConversionSmallerToLargerInsertAndSelectBulkData))]
        public void ConversionSmallerToLargerInsertAndSelectBulk(string connString, SqlDbType smallDbType, SqlDbType largeDbType)
        {
            ColumnMetaData largeColumnInfo = new ColumnMetaData(largeDbType, 0, 1, 1, false);
            ColumnMetaData smallColumnInfo = new ColumnMetaData(smallDbType, 0, 1, 1, false);

            // Adjust the size, precision and scale for data types that have one.
            AdjustSizePrecisionAndScale(ref largeColumnInfo, ref smallColumnInfo);

            string originTableName = DatabaseHelper.GenerateUniqueName("small_type_pt");
            string targetTableName = DatabaseHelper.GenerateUniqueName("large_type_enc");
            string witnessTableName = DatabaseHelper.GenerateUniqueName("large_type_pt");

            // Create the encrypted and unencrypted table with the proper column types.
            CreateTable(connString, smallColumnInfo, originTableName, isEncrypted: false);
            CreateTable(connString, largeColumnInfo, targetTableName, isEncrypted: true);
            CreateTable(connString, largeColumnInfo, witnessTableName, isEncrypted: false);

            // Insert data using the smaller type to the tables with the large type.
            // Also keep the values on the side to perform a select later.
            object[] rawValues = PopulateTablesAndReturnRandomValuePlaintextOnly(connString, originTableName, smallColumnInfo);

            // Keep the values from originTable other than the rawValues to perform a select later for DateTime2 and DateTimeOffset.
            object[] valuesToSelect = RetriveDataFromDatabase(connString, originTableName);

            // populate the witness table & the target table using bulk insert
            portDataToTablePairViaBulkCopy(connString, originTableName, SqlConnectionColumnEncryptionSetting.Disabled, targetTableName, SqlConnectionColumnEncryptionSetting.Enabled);
            portDataToTablePairViaBulkCopy(connString, originTableName, SqlConnectionColumnEncryptionSetting.Disabled, witnessTableName, SqlConnectionColumnEncryptionSetting.Disabled);

            // Now read back everything and make sure the values and types are identical.
            CompareTables(connString, targetTableName, witnessTableName);

            // Now send a query with a predicate using the larger type and confirm that the row that was inserted with the smaller type can still be found.
            using (SqlConnection sqlConnectionEncrypted = new SqlConnection(connString))
            using (SqlConnection sqlConnectionUnencrypted = new SqlConnection(connString))
            {
                sqlConnectionEncrypted.Open();
                sqlConnectionUnencrypted.Open();

                try
                {
                    // Select each value we just inserted with a predicate and verify that encrypted and unencrypted return the same result.
                    for (int i = 0; i < NumberOfRows; i++)
                    {
                        object value;

                        // Use the retrieved values for DateTime2 and DateTimeOffset due to fractional insertion adjustment
                        if (smallColumnInfo.ColumnType is SqlDbType.DateTime2 ||
                            smallColumnInfo.ColumnType is SqlDbType.DateTimeOffset ||
                            smallColumnInfo.ColumnType is SqlDbType.Char ||
                            smallColumnInfo.ColumnType is SqlDbType.NChar)
                        {
                            value = valuesToSelect[i];
                        }
                        else
                        {
                            value = rawValues[i];
                        }

                        using (SqlCommand cmdEncrypted = new SqlCommand(string.Format(@"SELECT {0} FROM [{1}] WHERE {0} = {2}", FirstColumnName, targetTableName, FirstParamName), sqlConnectionEncrypted, null, SqlCommandColumnEncryptionSetting.Enabled))
                        using (SqlCommand cmdUnencrypted = new SqlCommand(string.Format(@"SELECT {0} FROM [{1}] WHERE {0} = {2}", FirstColumnName, witnessTableName, FirstParamName), sqlConnectionUnencrypted, null, SqlCommandColumnEncryptionSetting.Disabled))
                        {
                            SqlParameter paramEncrypted = new SqlParameter();
                            paramEncrypted.ParameterName = FirstParamName;
                            paramEncrypted.SqlDbType = largeDbType;
                            SetParamSizeScalePrecision(ref paramEncrypted, largeColumnInfo);
                            paramEncrypted.Value = value;
                            cmdEncrypted.Parameters.Add(paramEncrypted);

                            SqlParameter paramUnencrypted = new SqlParameter();
                            paramUnencrypted.ParameterName = FirstParamName;
                            paramUnencrypted.SqlDbType = largeDbType;
                            SetParamSizeScalePrecision(ref paramUnencrypted, largeColumnInfo);
                            paramUnencrypted.Value = value;
                            cmdUnencrypted.Parameters.Add(paramUnencrypted);

                            using (SqlDataReader readerUnencrypted = cmdUnencrypted.ExecuteReader())
                            using (SqlDataReader readerEncrypted = cmdEncrypted.ExecuteReader())
                            {
                                // First check that we found some rows.
                                Assert.True(readerEncrypted.HasRows, @"We didn't find any rows.");

                                // Now compare the result.
                                CompareResults(readerEncrypted, readerUnencrypted);
                            }
                        }
                    }
                }
                finally
                {
                    // DropTables
                    DropTableIfExists(sqlConnectionEncrypted, targetTableName);
                    DropTableIfExists(sqlConnectionUnencrypted, witnessTableName);
                    DropTableIfExists(sqlConnectionUnencrypted, originTableName);
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(TestOutOfRangeValuesData))]
        public void TestOutOfRangeValues(string connString, SqlDbType currentDbType)
        {
            ColumnMetaData currentColumnInfo = new ColumnMetaData(currentDbType, 0, 1, 1, false);
            ColumnMetaData dummyColumnInfo = null;

            // Adjust size, precision and scale if the type has one.
            AdjustSizePrecisionAndScale(ref currentColumnInfo, ref dummyColumnInfo);

            // Create the encrypted and unencrypted table with the proper column types.
            string encryptedTableName = DatabaseHelper.GenerateUniqueName("encrypted");
            string unencryptedTableName = DatabaseHelper.GenerateUniqueName("unencrypted");

            // Create the encrypted and unencrypted table with the proper column types.
            CreateTable(connString, currentColumnInfo, encryptedTableName, isEncrypted: true);
            CreateTable(connString, currentColumnInfo, unencryptedTableName, isEncrypted: false);

            // Generate a list of out of range values, indicating which should fail and which shouldn't.
            List<ValueErrorTuple> valueList = GenerateOutOfRangeValuesForType(currentDbType, currentColumnInfo.ColumnSize, currentColumnInfo.Precision, currentColumnInfo.Scale);
            Assert.True(valueList.Count != 0, "Test bug, the list is empty!");

            using (SqlConnection sqlConnectionEncrypted = new SqlConnection(connString))
            using (SqlConnection sqlConnectionUnencrypted = new SqlConnection(connString))
            {
                sqlConnectionEncrypted.Open();
                sqlConnectionUnencrypted.Open();

                try
                {
                    foreach (ValueErrorTuple tuple in valueList)
                    {
                        using (SqlCommand sqlCmd = new SqlCommand(String.Format("INSERT INTO [{0}] VALUES ({1})", encryptedTableName, FirstParamName), sqlConnectionEncrypted, null, SqlCommandColumnEncryptionSetting.Enabled))
                        {
                            SqlParameter param = new SqlParameter();
                            param.ParameterName = FirstParamName;
                            param.SqlDbType = currentColumnInfo.ColumnType;
                            SetParamSizeScalePrecision(ref param, currentColumnInfo);
                            param.Value = tuple.Value;
                            sqlCmd.Parameters.Add(param);

                            ExecuteAndCheckForError(sqlCmd, tuple.ExpectsError);
                        }

                        // Add same value to the unencrypted table
                        using (SqlCommand sqlCmd = new SqlCommand(String.Format("INSERT INTO [{0}] VALUES ({1})", unencryptedTableName, FirstParamName), sqlConnectionUnencrypted, null, SqlCommandColumnEncryptionSetting.Disabled))
                        {
                            SqlParameter param = new SqlParameter();
                            param.ParameterName = FirstParamName;
                            param.SqlDbType = currentColumnInfo.ColumnType;
                            SetParamSizeScalePrecision(ref param, currentColumnInfo);
                            param.Value = tuple.Value;
                            sqlCmd.Parameters.Add(param);

                            ExecuteAndCheckForError(sqlCmd, tuple.ExpectsError);
                        }

                    }

                    CompareTables(connString, encryptedTableName, unencryptedTableName);
                }
                finally
                {
                    DropTableIfExists(sqlConnectionEncrypted, encryptedTableName);
                    DropTableIfExists(sqlConnectionUnencrypted, unencryptedTableName);
                }
            }
        }


        /// <summary>
        /// Internal class to store a tupple of the value to insert and whether an exception is expected.
        /// </summary>
        private class ValueErrorTuple
        {
            public ValueErrorTuple(object value, bool expectsError)
            {
                Value = value;
                ExpectsError = expectsError;
            }

            public object Value { get; set; }
            public bool ExpectsError { get; set; }
        }

        /// <summary>
        /// Generate out of bound values for each data type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="length"></param>
        /// <param name="precision"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        List<ValueErrorTuple> GenerateOutOfRangeValuesForType(SqlDbType type, int length, int precision, int scale)
        {
            List<ValueErrorTuple> list = new List<ValueErrorTuple>();

            switch (type)
            {
                case SqlDbType.Bit:
                    // Sql actually allows to insert out of bound values for bit and it converts them to a bit value.
                    list.Add(new ValueErrorTuple(2, false));
                    list.Add(new ValueErrorTuple(-1, false));
                    break;
                case SqlDbType.BigInt:
                    list.Add(new ValueErrorTuple("9223372036854775808", true));
                    list.Add(new ValueErrorTuple("-9223372036854775809", true));
                    break;
                case SqlDbType.Binary:
                case SqlDbType.VarBinary:
                    {
                        byte[] upperValueArray = new byte[length + 1];
                        Random random = new Random();
                        random.NextBytes(upperValueArray);
                        list.Add(new ValueErrorTuple(upperValueArray, false));
                        break;
                    }
                case SqlDbType.Char:
                case SqlDbType.NChar:
                case SqlDbType.VarChar:
                case SqlDbType.NVarChar:
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < length + 1; i++)
                        {
                            sb.Append('a');
                        }
                        list.Add(new ValueErrorTuple(sb.ToString(), false));
                        break;
                    }
                case SqlDbType.DateTime:
                    // This value is out of range and should fail.
                    list.Add(new ValueErrorTuple(new DateTime(1752, 12, 31, 23, 59, 59, 997), true));

                    // This value has greater scale and it should get truncated, but not fail.
                    list.Add(new ValueErrorTuple(new DateTime(2014, 1, 1, 23, 59, 59, 998), false));
                    break;
                case SqlDbType.Int:
                    list.Add(new ValueErrorTuple((Int64)Int32.MaxValue + 1, true));
                    list.Add(new ValueErrorTuple((Int64)Int32.MinValue - 1, true));
                    break;
                case SqlDbType.Money:
                    list.Add(new ValueErrorTuple(SqlMoney.MaxValue.Value + (decimal)0.0001, true));
                    list.Add(new ValueErrorTuple(SqlMoney.MinValue.Value - (decimal)0.0001, true));

                    // This value has greater scale and it should get truncated, but not fail.
                    list.Add(new ValueErrorTuple(1.00001, false));
                    break;
                case SqlDbType.UniqueIdentifier:
                    list.Add(new ValueErrorTuple(new Guid().ToString() + "1", true));
                    list.Add(new ValueErrorTuple(new Guid().ToString().Substring(0, new Guid().ToString().Length - 1), true));
                    break;
                case SqlDbType.SmallDateTime:
                    list.Add(new ValueErrorTuple(new DateTime(2079, 6, 7, 0, 0, 0), true));

                    // This value is out of range and should fail.
                    list.Add(new ValueErrorTuple(new DateTime(1899, 12, 31, 23, 59, 29), true));

                    // This is rounded and inserted properly for both encrypted and unencrypted.
                    list.Add(new ValueErrorTuple(new DateTime(1899, 12, 31, 23, 59, 59), false));

                    // This value has greater scale and it should get truncated, but not fail.
                    list.Add(new ValueErrorTuple(new DateTime(2014, 1, 1, 23, 59, 59, 1), false));
                    break;
                case SqlDbType.SmallInt:
                    list.Add(new ValueErrorTuple((Int32)Int16.MaxValue + 1, true));
                    list.Add(new ValueErrorTuple((Int32)Int16.MinValue - 1, true));
                    break;
                case SqlDbType.SmallMoney:
                    list.Add(new ValueErrorTuple((decimal)214748.3648, true));
                    list.Add(new ValueErrorTuple((decimal)-214748.3649, true));

                    // This value has greater scale and it should get truncated, but not fail.
                    list.Add(new ValueErrorTuple((decimal)1.00001, false));
                    break;
                case SqlDbType.TinyInt:
                    list.Add(new ValueErrorTuple((Int16)byte.MaxValue + 1, true));
                    list.Add(new ValueErrorTuple(-1, true));
                    break;
                case SqlDbType.Date:
                    // These values are out of range and should fail.
                    list.Add(new ValueErrorTuple("10000/1/1", true));
                    list.Add(new ValueErrorTuple("0/12/31", true));
                    break;
                case SqlDbType.Time:
                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                    // All values with higher precision will get truncated but not fail.
                    String timeStringUpper = "23:59:59.";
                    String timeStringLower = "00:00:00.";

                    for (int i = 0; i < scale; i++)
                    {
                        timeStringUpper = timeStringUpper + "9";
                        timeStringLower = timeStringLower + "0";
                    }

                    timeStringUpper = timeStringUpper + "1";
                    timeStringLower = timeStringLower + "1";

                    if (type == SqlDbType.Time)
                    {
                        TimeSpan temp = new TimeSpan();
                        TimeSpan.TryParse(timeStringUpper, out temp);
                        list.Add(new ValueErrorTuple(temp, false));
                        TimeSpan.TryParse(timeStringLower, out temp);
                        list.Add(new ValueErrorTuple(temp, false));
                    }
                    else if (type == SqlDbType.DateTime2)
                    {
                        // These values are out of range and should fail.
                        list.Add(new ValueErrorTuple("10000/1/1 00:00:00", true));
                        list.Add(new ValueErrorTuple("0/12/31 23::59:59:999", true));

                        timeStringUpper = "2014/1/1 " + timeStringUpper;
                        timeStringLower = "2014/1/1 " + timeStringLower;
                        DateTime temp = new DateTime();
                        DateTime.TryParse(timeStringUpper, out temp);
                        list.Add(new ValueErrorTuple(temp, false));
                        DateTime.TryParse(timeStringLower, out temp);
                        list.Add(new ValueErrorTuple(temp, false));
                    }
                    else if (type == SqlDbType.DateTimeOffset)
                    {
                        // These values are out of range and should fail.
                        list.Add(new ValueErrorTuple("10000/1/1 00:00:00 -14:00", true));
                        list.Add(new ValueErrorTuple("0/12/31 23::59:59:999 +14:00", true));

                        timeStringUpper = "2014/1/1 " + timeStringUpper;
                        timeStringLower = "2014/1/1 " + timeStringLower;
                        DateTime temp = new DateTime();
                        DateTime.TryParse(timeStringUpper, out temp);
                        list.Add(new ValueErrorTuple(temp, false));
                        DateTime.TryParse(timeStringLower, out temp);
                        list.Add(new ValueErrorTuple(temp, false));

                        // These values are out of range and should fail.
                        list.Add(new ValueErrorTuple("2014/1/1 10:00 +14:01", true));
                        list.Add(new ValueErrorTuple("2014/1/1 10:00 -14:01", true));
                    }
                    break;
                case SqlDbType.Decimal:
                    decimal highPart = 0;
                    decimal lowPart = 1;

                    for (int i = 0; i < precision - scale; i++)
                    {
                        if (i == 0)
                        {
                            highPart = 1;
                        }
                        else
                        {
                            highPart *= 10;
                        }
                    }

                    for (int i = 0; i < scale; i++)
                    {
                        lowPart /= 10;
                    }

                    // Construct a value with higher precision than allowed.
                    list.Add(new ValueErrorTuple(highPart == 0 ? 1 : highPart * 10 + lowPart, true));

                    // This value has greater scale and it should get truncated, but not fail.
                    // If scale is 0 then this actually fails because of how .NET internally calculates the state.
                    list.Add(new ValueErrorTuple(highPart + lowPart / 10, scale == 0 ? true : false));
                    break;
                case SqlDbType.Float:
                    list.Add(new ValueErrorTuple("1.79770e+308", true));
                    list.Add(new ValueErrorTuple("-1.79770e+308", true));
                    list.Add(new ValueErrorTuple(Double.PositiveInfinity, true));
                    list.Add(new ValueErrorTuple(Double.NegativeInfinity, true));
                    list.Add(new ValueErrorTuple(Double.NaN, true));
                    list.Add(new ValueErrorTuple(Double.Epsilon, false));
                    break;
                case SqlDbType.Real:
                    list.Add(new ValueErrorTuple((double)3.40283e+038, true));
                    list.Add(new ValueErrorTuple((double)-3.40283e+038, true));
                    list.Add(new ValueErrorTuple(Single.PositiveInfinity, true));
                    list.Add(new ValueErrorTuple(Single.NegativeInfinity, true));
                    list.Add(new ValueErrorTuple(Single.NaN, true));
                    list.Add(new ValueErrorTuple(Single.Epsilon, false));
                    break;
                default:
                    Assert.True(false, "We should never get here");
                    break;
            }

            return list;
        }

        /// <summary>
        /// Check if this exception is expected.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool IsExpectedException(Exception e)
        {
            return e is OverflowException ||
                e is InvalidCastException ||
                e is SqlTypeException ||
                e is ArgumentException ||
                e is FormatException ||
                e is SqlException;
        }

        /// <summary>
        /// Try to execute the command and check if there was an error if one was expected.
        /// </summary>
        /// <param name="sqlCmd"></param>
        /// <param name="expectError"></param>
        private void ExecuteAndCheckForError(SqlCommand sqlCmd, bool expectError)
        {
            if (!expectError)
            {
                sqlCmd.ExecuteNonQuery();
            }
            else
            {
                try
                {
                    sqlCmd.ExecuteNonQuery();
                    Assert.True(false, "We should have gotten an error but passed instead.");
                }
                catch (Exception e)
                {
                    Type exceptionType = e.GetType();
                    if (!IsExpectedException(e))
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Adjust the size, scale and precision for the data types that have one.
        /// </summary>
        /// <param name="largeColumnMeta"></param>
        /// <param name="smallColumnMeta"></param>
        private void AdjustSizePrecisionAndScale(ref ColumnMetaData largeColumnMeta, ref ColumnMetaData smallColumnMeta)
        {
            Random random = new Random();

            if (TypeHasSize(largeColumnMeta.ColumnType))
            {
                // 20% of the time use (max) as the length.                
                largeColumnMeta.UseMax = (largeColumnMeta.ColumnType is SqlDbType.VarChar ||
                    largeColumnMeta.ColumnType is SqlDbType.NVarChar ||
                    largeColumnMeta.ColumnType is SqlDbType.VarBinary) &&
                    random.Next(0, 100) < 20;

                int unicodeMaxLength = 3500;
                int maxLength = 7500;

                if (largeColumnMeta.UseMax)
                {
                    largeColumnMeta.ColumnSize = -1;

                    if (smallColumnMeta != null)
                    {
                        if (largeColumnMeta.ColumnType is SqlDbType.NChar || largeColumnMeta.ColumnType is SqlDbType.NVarChar)
                        {
                            smallColumnMeta.ColumnSize = random.Next(1, unicodeMaxLength);
                        }
                        else
                        {
                            smallColumnMeta.ColumnSize = random.Next(1, maxLength);
                        }
                    }
                }
                else
                {
                    if (largeColumnMeta.ColumnType is SqlDbType.NChar || largeColumnMeta.ColumnType is SqlDbType.NVarChar)
                    {
                        largeColumnMeta.ColumnSize = random.Next(2, unicodeMaxLength);
                    }
                    else
                    {
                        largeColumnMeta.ColumnSize = random.Next(2, maxLength);
                    }

                    if (smallColumnMeta != null)
                    {
                        smallColumnMeta.ColumnSize = random.Next(1, largeColumnMeta.ColumnSize);
                    }
                }
            }
            else if (TypeHasScale(largeColumnMeta.ColumnType))
            {
                int precision = 0;
                int scale = random.Next(1, 8);
                int minScale = 1;

                if (largeColumnMeta.ColumnType is SqlDbType.Decimal)
                {
                    precision = random.Next(1, 28);
                    scale = random.Next(0, precision + 1);
                    minScale = 0;
                }

                largeColumnMeta.Precision = precision;
                largeColumnMeta.Scale = scale;

                if (smallColumnMeta != null)
                {
                    smallColumnMeta.Precision = 0;

                    // For Time / DateTime2 / DateTimeOffset types, actual scale is set to 7 when parameter.scale is zero. 
                    // Active Issue in SQLParameter.cs when user wants to specify zero as the actual scale.
                    smallColumnMeta.Scale = random.Next(minScale, largeColumnMeta.Scale);
                }
            }
            else if (TypeHasPrecision(largeColumnMeta.ColumnType))
            {
                largeColumnMeta.Precision = random.Next(2, 54);
                largeColumnMeta.Scale = 0;

                if (smallColumnMeta != null)
                {
                    smallColumnMeta.Precision = random.Next(1, largeColumnMeta.Precision);
                    smallColumnMeta.Scale = 0;
                }
            }
        }

        /// <summary>
        /// Check if this data type has size.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool TypeHasSize(SqlDbType type)
        {
            return type is SqlDbType.Binary ||
                type is SqlDbType.VarBinary ||
                type is SqlDbType.Char ||
                type is SqlDbType.VarChar ||
                type is SqlDbType.NChar ||
                type is SqlDbType.NVarChar;
        }

        /// <summary>
        /// Check if this data type has scale.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool TypeHasScale(SqlDbType type)
        {
            return type is SqlDbType.Time ||
                type is SqlDbType.DateTime2 ||
                type is SqlDbType.DateTimeOffset ||
                type is SqlDbType.Decimal;
        }

        /// <summary>
        /// Check if this data type has precision.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool TypeHasPrecision(SqlDbType type)
        {
            return type is SqlDbType.Decimal;
        }

        /// <summary>
        /// Populate the tables with data of the provided data type.
        /// </summary>
        /// <param name="encryptedTableName"></param>
        /// <param name="unencryptedTableName"></param>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        private object[] PopulateTablesAndReturnRandomValue(string connString, string encryptedTableName, string unencryptedTableName, ColumnMetaData columnInfo)
        {
            object[] valueArray = new object[NumberOfRows];

            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.Open();

                for (int i = 0; i < NumberOfRows; i++)
                {
                    valueArray[i] = GenerateRandomValue(columnInfo);

                    // Add value to the encrypted table
                    using (SqlCommand sqlCmd = new SqlCommand(String.Format("INSERT INTO [{0}] VALUES ({1})", encryptedTableName, FirstParamName), sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        SqlParameter param = new SqlParameter();
                        param.ParameterName = FirstParamName;
                        param.SqlDbType = columnInfo.ColumnType;
                        SetParamSizeScalePrecision(ref param, columnInfo);
                        param.Value = valueArray[i];
                        sqlCmd.Parameters.Add(param);

                        sqlCmd.ExecuteNonQuery();
                    }

                    // Add same value to the unencrypted table
                    using (SqlCommand sqlCmd = new SqlCommand(String.Format("INSERT INTO [{0}] VALUES ({1})", unencryptedTableName, FirstParamName), sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        SqlParameter param = new SqlParameter();
                        param.ParameterName = FirstParamName;
                        param.SqlDbType = columnInfo.ColumnType;
                        SetParamSizeScalePrecision(ref param, columnInfo);
                        param.Value = valueArray[i];
                        sqlCmd.Parameters.Add(param);

                        sqlCmd.ExecuteNonQuery();
                    }
                }
            }

            return valueArray;
        }

        /// <summary>
        /// Populate the tables with data of the provided data type.
        /// </summary>
        /// <param name="unencryptedTableName"></param>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        private object[] PopulateTablesAndReturnRandomValuePlaintextOnly(string connSting, string unencryptedTableName, ColumnMetaData columnInfo)
        {
            object[] valueArray = new object[NumberOfRows];

            using (SqlConnection sqlConnection = new SqlConnection(connSting))
            {
                sqlConnection.Open();

                for (int i = 0; i < NumberOfRows; i++)
                {
                    valueArray[i] = GenerateRandomValue(columnInfo);

                    // Add same value to the unencrypted table
                    using (SqlCommand sqlCmd = new SqlCommand(String.Format("INSERT INTO [{0}] VALUES ({1})", unencryptedTableName, FirstParamName), sqlConnection, null, SqlCommandColumnEncryptionSetting.Disabled))
                    {
                        SqlParameter param = new SqlParameter();
                        param.ParameterName = FirstParamName;
                        param.SqlDbType = columnInfo.ColumnType;
                        SetParamSizeScalePrecision(ref param, columnInfo);
                        param.Value = valueArray[i];
                        sqlCmd.Parameters.Add(param);

                        sqlCmd.ExecuteNonQuery();
                    }
                }
            }

            return valueArray;
        }

        /// <summary>
        /// Inserts identical data into two tables (for comparison purposes)
        /// </summary>
        /// <param name="sourceName"></param>
        /// <param name="sourceConnectionFlag"></param>
        /// <param name="targetName"></param>
        /// <param name="targetConnectionFlag"></param>
        private void portDataToTablePairViaBulkCopy(string connString, string sourceName, SqlConnectionColumnEncryptionSetting sourceConnectionFlag, string targetName, SqlConnectionColumnEncryptionSetting targetConnectionFlag)
        {
            SqlConnectionStringBuilder strbld = new SqlConnectionStringBuilder(connString);
            strbld.ColumnEncryptionSetting = sourceConnectionFlag;

            using (SqlConnection sourceConnection = new SqlConnection(strbld.ToString()))
            {
                sourceConnection.Open();

                SqlCommand sourceCmd = sourceConnection.CreateCommand();
                sourceCmd.CommandText = String.Format(@"SELECT * FROM [{0}]", sourceName);

                SqlDataReader reader = sourceCmd.ExecuteReader();

                strbld.ColumnEncryptionSetting = targetConnectionFlag;
                using (SqlConnection targetConnection = new SqlConnection(strbld.ToString()))
                {
                    targetConnection.Open();

                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(targetConnection))
                    {
                        bulkCopy.DestinationTableName = targetName;

                        try
                        {
                            bulkCopy.WriteToServer(reader);
                        }
                        finally
                        {
                            reader.Close();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retrive data from unecrypted table for comparison
        /// </summary>
        /// <param name="unencryptedTableName"></param>
        /// <returns></returns>
        private object[] RetriveDataFromDatabase(string connString, string unencryptedTableName)
        {
            object[] valueArray = new object[NumberOfRows];
            int index = 0;

            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.Open();

                using (SqlCommand cmdUnencrypted = new SqlCommand(String.Format("SELECT {0} FROM [{1}] ORDER BY {2}", FirstColumnName, unencryptedTableName, IdentityColumnName), sqlConnection, null, SqlCommandColumnEncryptionSetting.Disabled))
                {
                    using (SqlDataReader readerUnencrypted = cmdUnencrypted.ExecuteReader())
                    {
                        Assert.True(readerUnencrypted.HasRows, "We didn't find any rows in unEncryptedTable.");

                        while (readerUnencrypted.Read())
                        {
                            valueArray[index] = readerUnencrypted.GetValue(0);
                            index++;
                        }

                        Assert.True(NumberOfRows == index, String.Format("The number of rows retrieved is {0}", index));
                    }
                }
            }

            return valueArray;
        }

        /// <summary>
        /// Compare the two tables to check that they have identical rows.
        /// </summary>
        /// <param name="encryptedTableName"></param>
        /// <param name="unencryptedTableName"></param>
        private void CompareTables(string connString, string encryptedTableName, string unencryptedTableName)
        {
            using (SqlConnection sqlConnectionEncrypted = new SqlConnection(connString))
            using (SqlConnection sqlConnectionUnencrypted = new SqlConnection(connString))
            {
                sqlConnectionEncrypted.Open();
                sqlConnectionUnencrypted.Open();

                // Check that the tables contain identical data for the small types.
                using (SqlCommand cmdEncrypted = new SqlCommand(String.Format("SELECT * FROM [{0}] ORDER BY {1}", encryptedTableName, IdentityColumnName), sqlConnectionEncrypted, null, SqlCommandColumnEncryptionSetting.Enabled))
                using (SqlCommand cmdUnencrypted = new SqlCommand(String.Format("SELECT * FROM [{0}] ORDER BY {1}", unencryptedTableName, IdentityColumnName), sqlConnectionUnencrypted, null, SqlCommandColumnEncryptionSetting.Disabled))
                {
                    using (SqlDataReader readerUnencrypted = cmdUnencrypted.ExecuteReader())
                    using (SqlDataReader readerEncrypted = cmdEncrypted.ExecuteReader())
                    {
                        CompareResults(readerEncrypted, readerUnencrypted);
                    }
                }
            }
        }

        /// <summary>
        /// Read data using two sqlDataReaders and compare the results.
        /// </summary>
        /// <param name="sqlDataReaderEncrypted"></param>
        /// <param name="sqlDataReaderUnencrypted"></param>
        private void CompareResults(SqlDataReader sqlDataReaderEncrypted, SqlDataReader sqlDataReaderUnencrypted)
        {
            int rowId = 0;

            while (sqlDataReaderEncrypted.Read())
            {
                rowId++;

                Assert.True(sqlDataReaderUnencrypted.HasRows, "Unencrypted reader has less rows than the encrypted.");

                sqlDataReaderUnencrypted.Read();

                for (int i = 0; i < sqlDataReaderEncrypted.FieldCount; i++)
                {
                    Assert.True(sqlDataReaderEncrypted.GetDataTypeName(i).Equals(sqlDataReaderUnencrypted.GetDataTypeName(i)), string.Format("The types for column '{0}' are not identical.", sqlDataReaderEncrypted.GetName(i)));
                    Assert.True(sqlDataReaderEncrypted.GetValue(i).GetType().Equals(sqlDataReaderUnencrypted.GetValue(i).GetType()), string.Format("The types of the value read for row '{0}' column '{1}' are not identical", rowId, sqlDataReaderEncrypted.GetName(i)));

                    object encryptedValue = sqlDataReaderEncrypted.GetValue(i);
                    object unencryptedValue = sqlDataReaderUnencrypted.GetValue(i);
                    if (sqlDataReaderEncrypted.GetDataTypeName(i) == "binary" || sqlDataReaderEncrypted.GetDataTypeName(i) == "varbinary")
                    {
                        Assert.True(((byte[])encryptedValue).SequenceEqual((byte[])unencryptedValue), string.Format("The values read for row '{0}' column '{1}' are not identical", rowId, sqlDataReaderEncrypted.GetName(i)));
                    }
                    else if (sqlDataReaderEncrypted.GetDataTypeName(i) == "char" || sqlDataReaderEncrypted.GetDataTypeName(i) == "varchar" ||
                             sqlDataReaderEncrypted.GetDataTypeName(i) == "nchar" || sqlDataReaderEncrypted.GetDataTypeName(i) == "nvarchar")
                    {
                        Assert.True(((string)encryptedValue).TrimEnd().Equals(((string)unencryptedValue).TrimEnd()), string.Format("The values read for row '{0}' column '{1}' are not identical", rowId, sqlDataReaderEncrypted.GetName(i)));
                    }
                    else
                    {
                        Assert.True(encryptedValue.Equals(unencryptedValue), string.Format("The values read for row '{0}' column '{1}' are not identical", rowId, sqlDataReaderEncrypted.GetName(i)));
                    }
                }
            }

            Assert.False(sqlDataReaderUnencrypted.Read(), "Unencrypted reader has more rows than the encrypted.");
        }


        /// <summary>
        /// Generate random value for insertion according to database column
        /// </summary>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        private object GenerateRandomValue(ColumnMetaData columnInfo)
        {
            object returnValue;
            int year;
            int month;
            int day;
            int hour;
            int minute;
            int second;
            int millisecond;
            int count;
            long ticks;

            Random rand = new Random();
            bool isNegative = Convert.ToBoolean(rand.Next(0, 2));
            StringBuilder strBuilder = new StringBuilder();
            TimeSpan tempTime;

            switch (columnInfo.ColumnType)
            {
                case SqlDbType.BigInt:
                    returnValue = isNegative ? Convert.ToInt64(rand.NextDouble() * Int64.MinValue) : Convert.ToInt64(rand.NextDouble() * Int64.MaxValue);
                    break;

                case SqlDbType.Bit:
                    returnValue = Convert.ToBoolean(rand.Next(0, 2));
                    break;

                case SqlDbType.Int:
                    returnValue = rand.Next();
                    break;

                case SqlDbType.Date:
                    year = rand.Next(1, 9999);
                    month = rand.Next(1, 13);
                    day = rand.Next(1, 29);

                    returnValue = new System.DateTime(year, month, day);
                    break;

                case SqlDbType.DateTime:
                    year = rand.Next(1753, 9999);
                    month = rand.Next(1, 13);
                    day = rand.Next(1, 28);
                    hour = rand.Next(0, 24);
                    minute = rand.Next(0, 60);
                    second = rand.Next(0, 60);
                    millisecond = rand.Next(0, 998);

                    returnValue = new DateTime(year, month, day, hour, minute, second, millisecond);
                    break;

                case SqlDbType.Money:
                    returnValue = isNegative ? Convert.ToDecimal((SqlMoney)rand.NextDouble() * SqlMoney.MinValue) : Convert.ToDecimal((SqlMoney)rand.NextDouble() * SqlMoney.MaxValue);
                    break;

                case SqlDbType.Real:
                    returnValue = isNegative ? Convert.ToSingle(rand.NextDouble() * Single.MinValue) : Convert.ToSingle(rand.NextDouble() * Single.MaxValue);
                    break;

                case SqlDbType.SmallDateTime:
                    year = rand.Next(1900, 2079);
                    month = rand.Next(1, 13);
                    day = rand.Next(1, 28);
                    hour = rand.Next(0, 24);
                    minute = rand.Next(0, 60);
                    second = rand.Next(0, 60);

                    returnValue = new DateTime(year, month, day, hour, minute, second);
                    break;

                case SqlDbType.SmallInt:
                    returnValue = isNegative ? Convert.ToInt16(rand.NextDouble() * Int16.MinValue) : Convert.ToInt16(rand.NextDouble() * Int16.MaxValue);
                    break;

                case SqlDbType.SmallMoney:
                    returnValue = isNegative ? Convert.ToDecimal((decimal)rand.NextDouble() * SmallMoneyMinValue) : Convert.ToDecimal((decimal)rand.NextDouble() * SmallMoneyMaxValue);
                    break;

                case SqlDbType.TinyInt:
                    returnValue = Convert.ToByte(rand.Next(Byte.MinValue, Byte.MaxValue + 1));
                    break;

                case SqlDbType.Binary:
                    returnValue = DatabaseHelper.GenerateRandomBytes(columnInfo.ColumnSize);
                    break;

                case SqlDbType.Char:
                    returnValue = Encoding.UTF8.GetString(DatabaseHelper.GenerateRandomBytes(columnInfo.ColumnSize)).TrimEnd();
                    break;

                case SqlDbType.NChar:
                    returnValue = Encoding.Unicode.GetString(DatabaseHelper.GenerateRandomBytes(2 * columnInfo.ColumnSize)).TrimEnd();
                    break;

                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                    year = rand.Next(1, 9999);
                    month = rand.Next(1, 13);
                    day = rand.Next(1, 28);
                    hour = rand.Next(0, 24);
                    minute = rand.Next(0, 60);
                    second = rand.Next(0, 60);

                    strBuilder.Clear();
                    count = columnInfo.Scale > 3 ? 3 : columnInfo.Scale;

                    while (count > 0)
                    {
                        strBuilder.Append("9");
                        count--;
                    }

                    millisecond = (0 == strBuilder.Length) ? 0 : rand.Next(0, Int32.Parse(strBuilder.ToString()));

                    if (SqlDbType.DateTime2 == columnInfo.ColumnType)
                    {
                        returnValue = new DateTime(year, month, day, hour, minute, second, millisecond);
                    }
                    else
                    {
                        returnValue = new DateTimeOffset(year, month, day, hour, minute, second, millisecond, new TimeSpan(rand.Next(-14, 15), 0, 0));
                    }
                    break;

                case SqlDbType.Time:
                    ticks = Convert.ToInt64(rand.NextDouble() * (TimeSpan.TicksPerDay - 1));
                    strBuilder.Clear();

                    count = columnInfo.Scale;

                    if (0 == count)
                    {
                        strBuilder.Append(@"hh\:mm\:ss");
                    }
                    else
                    {
                        strBuilder.Append(@"hh\:mm\:ss\.");
                    }

                    while (count > 0)
                    {
                        strBuilder.Append("f");
                        count--;
                    }

                    tempTime = new TimeSpan(ticks);
                    returnValue = TimeSpan.Parse(tempTime.ToString(strBuilder.ToString()));
                    break;

                case SqlDbType.Decimal:
                    returnValue = isNegative ? Convert.ToDecimal((decimal)rand.NextDouble() * Decimal.MinValue) : Convert.ToDecimal((decimal)rand.NextDouble() * Decimal.MaxValue);
                    break;

                case SqlDbType.Float:
                    returnValue = isNegative ? rand.NextDouble() * Double.MinValue : rand.NextDouble() * Double.MaxValue;
                    break;

                case SqlDbType.VarChar:
                    if (columnInfo.UseMax)
                    {
                        returnValue = Encoding.UTF8.GetString(DatabaseHelper.GenerateRandomBytes(MaxLength)).TrimEnd();
                    }
                    else
                    {
                        returnValue = Encoding.UTF8.GetString(DatabaseHelper.GenerateRandomBytes(columnInfo.ColumnSize)).TrimEnd();
                    }
                    break;

                case SqlDbType.VarBinary:
                    if (columnInfo.UseMax)
                    {
                        returnValue = DatabaseHelper.GenerateRandomBytes(MaxLength);
                    }
                    else
                    {
                        returnValue = DatabaseHelper.GenerateRandomBytes(columnInfo.ColumnSize);
                    }
                    break;

                case SqlDbType.NVarChar:
                    if (columnInfo.UseMax)
                    {
                        returnValue = Encoding.Unicode.GetString(DatabaseHelper.GenerateRandomBytes(2 * MaxLength)).TrimEnd();
                    }
                    else
                    {
                        returnValue = Encoding.Unicode.GetString(DatabaseHelper.GenerateRandomBytes(2 * columnInfo.ColumnSize)).TrimEnd();
                    }
                    break;

                default:
                    returnValue = Encoding.Unicode.GetString(DatabaseHelper.GenerateRandomBytes(100)).TrimEnd();
                    break;
            }

            return returnValue;
        }

        /// <summary>
        /// Creates a table with the specified column type.
        /// </summary>
        /// <param name="columnMeta"></param>
        /// <param name="tableName"></param>
        /// <param name="isEncrypted"></param>
        private void CreateTable(string connString, ColumnMetaData columnMeta, string tableName, bool isEncrypted)
        {
            string columnType = columnMeta.ColumnType.ToString().ToLower();
            string columnInfo = "";
            StringBuilder builder = new StringBuilder();

            switch (columnMeta.ColumnType)
            {
                case SqlDbType.BigInt:
                case SqlDbType.Bit:
                case SqlDbType.Int:
                case SqlDbType.Date:
                case SqlDbType.DateTime:
                case SqlDbType.Money:
                case SqlDbType.Real:
                case SqlDbType.Float:
                case SqlDbType.SmallDateTime:
                case SqlDbType.SmallInt:
                case SqlDbType.SmallMoney:
                case SqlDbType.TinyInt:
                case SqlDbType.UniqueIdentifier:
                    columnInfo = columnType;
                    break;

                case SqlDbType.Binary:
                    columnInfo = $@"{columnMeta.ColumnType}({columnMeta.ColumnSize})";
                    break;

                case SqlDbType.Char:
                case SqlDbType.NChar:
                    columnInfo = $@"{columnMeta.ColumnType}({columnMeta.ColumnSize}) COLLATE Latin1_General_BIN2";
                    break;

                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                    if (columnMeta.Scale >= 0 && columnMeta.Scale <= 7)
                    {
                        columnInfo = $@"{columnType}({columnMeta.Scale})";
                    }
                    else
                    {
                        columnInfo = $@"{columnType}";
                    }
                    break;

                case SqlDbType.Time:
                    if (columnMeta.Scale >= 0 && columnMeta.Scale <= 7)
                    {
                        columnInfo = $@"{columnType}({columnMeta.Scale})";
                    }
                    break;

                case SqlDbType.Decimal:
                    builder.Clear();
                    builder.Append(columnType);

                    // If we have a valid precision
                    if (columnMeta.Precision >= 1 && columnMeta.Precision <= 38)
                    {
                        builder.AppendFormat("({0}", columnMeta.Precision);

                        // If we have a valid scale
                        if (columnMeta.Scale >= 0 && columnMeta.Scale <= columnMeta.Precision)
                        {
                            builder.AppendFormat(",{0}", columnMeta.Scale);
                        }

                        builder.Append(")");
                    }

                    columnInfo = builder.ToString();
                    break;

                case SqlDbType.VarBinary:
                    if (columnMeta.UseMax)
                    {
                        columnInfo = $@"{columnType}(max)";
                    }
                    else
                    {
                        columnInfo = $@"{columnType}({columnMeta.ColumnSize})";
                    }
                    break;

                case SqlDbType.VarChar:
                case SqlDbType.NVarChar:
                    if (columnMeta.UseMax)
                    {
                        columnInfo = $@"{columnType}(max) COLLATE Latin1_General_BIN2";
                    }
                    else
                    {
                        columnInfo = $@"{columnType}({columnMeta.ColumnSize}) COLLATE Latin1_General_BIN2";
                    }
                    break;

                default:
                    columnInfo = "nvarchar(50) COLLATE Latin1_General_BIN2";
                    break;
            }

            string sql;
            string encryptionType = DataTestUtility.EnclaveEnabled ? "RANDOMIZED" : "DETERMINISTIC";

            if (isEncrypted)
            {
                sql = $@"CREATE TABLE [dbo].[{tableName}]
                      (
                          [{IdentityColumnName}] int IDENTITY(1,1),
                          [{FirstColumnName}] {columnInfo} ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{columnEncryptionKey.Name}], ENCRYPTION_TYPE = {encryptionType}, ALGORITHM = '{ColumnEncryptionAlgorithmName}'),
                      )";
            }
            else
            {
                sql = $@"CREATE TABLE [dbo].[{tableName}]
                      (
                          [{IdentityColumnName}] int IDENTITY(1,1),
                          [{FirstColumnName}] {columnInfo}
                      )";
            }

            using (SqlConnection sqlConn = new SqlConnection(connString))
            {
                sqlConn.Open();

                using (SqlCommand command = sqlConn.CreateCommand())
                {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Drop the table if it exists.
        /// </summary>
        private void DropTableIfExists(SqlConnection sqlConnection, string tableName)
        {
            string cmdText = $@"IF EXISTS (select * from sys.objects where name = '{tableName}') BEGIN DROP TABLE [{tableName}] END";
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = cmdText;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Set the parameter size, precision and scale.
        /// </summary>
        /// <param name="param"></param>
        /// <param name="columnMeta"></param>
        private void SetParamSizeScalePrecision(ref SqlParameter param, ColumnMetaData columnMeta)
        {
            if (TypeHasSize(columnMeta.ColumnType))
            {
                param.Size = columnMeta.ColumnSize;
            }

            if (TypeHasScale(columnMeta.ColumnType))
            {
                param.Scale = (byte)columnMeta.Scale;
            }

            if (TypeHasPrecision(columnMeta.ColumnType))
            {
                param.Precision = (byte)columnMeta.Precision;
            }
        }

        public void Dispose()
        {
            databaseObjects.Reverse();
            foreach(string connectionStr in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connectionStr))
                {
                    sqlConnection.Open();
                    databaseObjects.ForEach(o => o.Drop(sqlConnection));
                }
            }
        }
    }

    public class ConversionSmallerToLargerInsertAndSelectData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, SqlDbType.SmallMoney, SqlDbType.Money };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.TinyInt };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.SmallInt };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.Int };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.TinyInt, SqlDbType.SmallInt };
                yield return new object[] { connStrAE, SqlDbType.TinyInt, SqlDbType.Int };
                yield return new object[] { connStrAE, SqlDbType.TinyInt, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.SmallInt, SqlDbType.Int };
                yield return new object[] { connStrAE, SqlDbType.SmallInt, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.Int, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.Binary, SqlDbType.Binary };
                yield return new object[] { connStrAE, SqlDbType.Binary, SqlDbType.VarBinary };
                yield return new object[] { connStrAE, SqlDbType.VarBinary, SqlDbType.Binary };
                yield return new object[] { connStrAE, SqlDbType.VarBinary, SqlDbType.VarBinary };
                yield return new object[] { connStrAE, SqlDbType.Char, SqlDbType.Char };
                yield return new object[] { connStrAE, SqlDbType.Char, SqlDbType.VarChar }; // padding whitespace issue, trimEnd for now
                yield return new object[] { connStrAE, SqlDbType.VarChar, SqlDbType.Char };
                yield return new object[] { connStrAE, SqlDbType.VarChar, SqlDbType.VarChar };
                yield return new object[] { connStrAE, SqlDbType.NChar, SqlDbType.NChar };
                yield return new object[] { connStrAE, SqlDbType.NChar, SqlDbType.NVarChar };
                yield return new object[] { connStrAE, SqlDbType.NVarChar, SqlDbType.NChar };
                yield return new object[] { connStrAE, SqlDbType.NVarChar, SqlDbType.NVarChar };
                yield return new object[] { connStrAE, SqlDbType.Time, SqlDbType.Time };
                yield return new object[] { connStrAE, SqlDbType.DateTime2, SqlDbType.DateTime2 };
                yield return new object[] { connStrAE, SqlDbType.DateTimeOffset, SqlDbType.DateTimeOffset };
                yield return new object[] { connStrAE, SqlDbType.Float, SqlDbType.Float };
                yield return new object[] { connStrAE, SqlDbType.Real, SqlDbType.Real };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class ConversionSmallerToLargerInsertAndSelectBulkData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, SqlDbType.SmallMoney, SqlDbType.Money };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.TinyInt };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.SmallInt };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.Int };
                yield return new object[] { connStrAE, SqlDbType.Bit, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.TinyInt, SqlDbType.SmallInt };
                yield return new object[] { connStrAE, SqlDbType.TinyInt, SqlDbType.Int };
                yield return new object[] { connStrAE, SqlDbType.TinyInt, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.SmallInt, SqlDbType.Int };
                yield return new object[] { connStrAE, SqlDbType.SmallInt, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.Int, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.Binary, SqlDbType.Binary };
                yield return new object[] { connStrAE, SqlDbType.Binary, SqlDbType.VarBinary };
                yield return new object[] { connStrAE, SqlDbType.VarBinary, SqlDbType.Binary };
                yield return new object[] { connStrAE, SqlDbType.VarBinary, SqlDbType.VarBinary };
                yield return new object[] { connStrAE, SqlDbType.Char, SqlDbType.Char }; // padding whitespace issue
                yield return new object[] { connStrAE, SqlDbType.Char, SqlDbType.VarChar }; // padding whitespace issue
                yield return new object[] { connStrAE, SqlDbType.VarChar, SqlDbType.Char };
                yield return new object[] { connStrAE, SqlDbType.VarChar, SqlDbType.VarChar };
                yield return new object[] { connStrAE, SqlDbType.NChar, SqlDbType.NChar };
                yield return new object[] { connStrAE, SqlDbType.NChar, SqlDbType.NVarChar };
                yield return new object[] { connStrAE, SqlDbType.NVarChar, SqlDbType.NChar };
                yield return new object[] { connStrAE, SqlDbType.NVarChar, SqlDbType.NVarChar };
                yield return new object[] { connStrAE, SqlDbType.Time, SqlDbType.Time };
                yield return new object[] { connStrAE, SqlDbType.DateTime2, SqlDbType.DateTime2 };
                yield return new object[] { connStrAE, SqlDbType.DateTimeOffset, SqlDbType.DateTimeOffset };
                yield return new object[] { connStrAE, SqlDbType.Float, SqlDbType.Float };
                yield return new object[] { connStrAE, SqlDbType.Real, SqlDbType.Real};
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    
    public class TestOutOfRangeValuesData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, SqlDbType.BigInt };
                yield return new object[] { connStrAE, SqlDbType.Binary };
                yield return new object[] { connStrAE, SqlDbType.Bit };
                yield return new object[] { connStrAE, SqlDbType.Char };
                yield return new object[] { connStrAE, SqlDbType.Date };
                yield return new object[] { connStrAE, SqlDbType.DateTime };
                yield return new object[] { connStrAE, SqlDbType.DateTime2 };
                yield return new object[] { connStrAE, SqlDbType.DateTimeOffset };
                yield return new object[] { connStrAE, SqlDbType.Decimal };
                yield return new object[] { connStrAE, SqlDbType.Float };
                yield return new object[] { connStrAE, SqlDbType.Int };
                yield return new object[] { connStrAE, SqlDbType.Money };
                yield return new object[] { connStrAE, SqlDbType.NChar };
                yield return new object[] { connStrAE, SqlDbType.NVarChar };
                yield return new object[] { connStrAE, SqlDbType.Real };
                yield return new object[] { connStrAE, SqlDbType.SmallDateTime };
                yield return new object[] { connStrAE, SqlDbType.SmallInt };
                yield return new object[] { connStrAE, SqlDbType.SmallMoney };
                yield return new object[] { connStrAE, SqlDbType.Time };
                yield return new object[] { connStrAE, SqlDbType.TinyInt };
                yield return new object[] { connStrAE, SqlDbType.UniqueIdentifier };
                yield return new object[] { connStrAE, SqlDbType.VarBinary };
                yield return new object[] { connStrAE, SqlDbType.VarChar };
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
