
------------------------------ TestSimpleParameter_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSimpleParameter_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSimpleParameter_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSimpleParameter_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSqlDataRecordParameterToTVP_Type>>> EXCEPTION: [System.ArgumentException] Invalid value for this metadata.

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
Type        => Expected : Actual == System.TimeSpan : System.TimeSpan
Base Type   => Expected : Actual == time : time
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 00:00:00] [Expected = -10675199.02:48:05.4775808]

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSqlDataReaderParameterToTVP_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSqlDataReaderParameterToTVP_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSqlDataReader_TVP_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSqlDataReader_TVP_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ TestSqlDataReader_TVP_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSqlDataReader_TVP_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ TestSimpleDataReader_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSimpleDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ TestSimpleDataReader_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
TestSimpleDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ SqlBulkCopySqlDataReader_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
SqlBulkCopySqlDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ SqlBulkCopySqlDataReader_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
SqlBulkCopySqlDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ SqlBulkCopyDataTable_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
SqlBulkCopyDataTable_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ SqlBulkCopyDataTable_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
SqlBulkCopyDataTable_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ SqlBulkCopyDataRow_Type [type: time value:-10675199.02:48:05.4775808] ------------------------------
SqlBulkCopyDataRow_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ SqlBulkCopyDataRow_Variant [type: time value:-10675199.02:48:05.4775808] ------------------------------
SqlBulkCopyDataRow_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '-10675199.02:48:05.4775808' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.
