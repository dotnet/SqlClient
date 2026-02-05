
------------------------------ TestSimpleParameter_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSimpleParameter_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSimpleParameter_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSimpleParameter_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSqlDataRecordParameterToTVP_Type>>> EXCEPTION: [System.ArgumentException] Invalid value for this metadata.

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSqlDataRecordParameterToTVP_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] The incoming tabular data stream (TDS) remote procedure call (RPC) protocol stream is incorrect. Table-valued parameter 0 (""), row 1, column 1: The supplied value is not a valid instance of data type sql_variant. Check the source data for invalid values. An example of an invalid value is data of numeric type with scale greater than precision.
The statement has been terminated.

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSqlDataReaderParameterToTVP_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSqlDataReaderParameterToTVP_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ TestSqlDataReader_TVP_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSqlDataReader_TVP_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ TestSqlDataReader_TVP_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSqlDataReader_TVP_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ TestSimpleDataReader_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSimpleDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ TestSimpleDataReader_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
TestSimpleDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ SqlBulkCopySqlDataReader_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
SqlBulkCopySqlDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ SqlBulkCopySqlDataReader_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
SqlBulkCopySqlDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting date and/or time from character string.

------------------------------ SqlBulkCopyDataTable_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
SqlBulkCopyDataTable_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ SqlBulkCopyDataTable_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
SqlBulkCopyDataTable_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ SqlBulkCopyDataRow_Type [type: time value:10675199.02:48:05.4775807] ------------------------------
SqlBulkCopyDataRow_Type>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.

------------------------------ SqlBulkCopyDataRow_Variant [type: time value:10675199.02:48:05.4775807] ------------------------------
SqlBulkCopyDataRow_Variant>>> EXCEPTION: [System.OverflowException] SqlDbType.Time overflow.  Value '10675199.02:48:05.4775807' is out of range.  Must be between 00:00:00.0000000 and 23:59:59.9999999.
