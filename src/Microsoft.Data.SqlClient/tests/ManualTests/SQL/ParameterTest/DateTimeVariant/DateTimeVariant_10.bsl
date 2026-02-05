
------------------------------ TestSimpleParameter_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSimpleParameter_Type>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSimpleParameter_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSimpleParameter_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSqlDataRecordParameterToTVP_Type>>> EXCEPTION: [System.ArgumentException] Invalid value for this metadata.

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSqlDataRecordParameterToTVP_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSqlDataReaderParameterToTVP_Type>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSqlDataReaderParameterToTVP_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataReader_TVP_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSqlDataReader_TVP_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.
The statement has been terminated.

------------------------------ TestSqlDataReader_TVP_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSqlDataReader_TVP_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.
The statement has been terminated.

------------------------------ TestSimpleDataReader_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSimpleDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.
The statement has been terminated.

------------------------------ TestSimpleDataReader_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
TestSimpleDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.
The statement has been terminated.

------------------------------ SqlBulkCopySqlDataReader_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopySqlDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.
The statement has been terminated.

------------------------------ SqlBulkCopySqlDataReader_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopySqlDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] The conversion of a varchar data type to a smalldatetime data type resulted in an out-of-range value.
The statement has been terminated.

------------------------------ SqlBulkCopyDataTable_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataTable_Type>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ SqlBulkCopyDataTable_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataTable_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ SqlBulkCopyDataRow_Type [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataRow_Type>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ SqlBulkCopyDataRow_Variant [type: smalldatetime value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataRow_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.
