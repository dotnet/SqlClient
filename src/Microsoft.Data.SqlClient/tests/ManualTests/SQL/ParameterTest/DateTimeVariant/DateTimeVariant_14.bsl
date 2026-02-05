
------------------------------ TestSimpleParameter_Type [type: time value:1/1/0001 00:00:00] ------------------------------
TestSimpleParameter_Type>>> EXCEPTION: [System.InvalidCastException] Failed to convert parameter value from a DateTime to a TimeSpan.

------------------------------ TestSimpleParameter_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
TestSimpleParameter_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: time value:1/1/0001 00:00:00] ------------------------------
TestSqlDataRecordParameterToTVP_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
TestSqlDataRecordParameterToTVP_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: time value:1/1/0001 00:00:00] ------------------------------
TestSqlDataReaderParameterToTVP_Type>>> EXCEPTION: [System.InvalidCastException] Failed to convert parameter value from a DateTime to a TimeSpan.

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
TestSqlDataReaderParameterToTVP_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataReader_TVP_Type [type: time value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 
TestSqlDataReader_TVP_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSqlDataReader_TVP_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time
TestSqlDataReader_TVP_Variant>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSimpleDataReader_Type [type: time value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 
TestSimpleDataReader_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSimpleDataReader_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time
TestSimpleDataReader_Variant>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ SqlBulkCopySqlDataReader_Type [type: time value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 
SqlBulkCopySqlDataReader_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ SqlBulkCopySqlDataReader_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time
SqlBulkCopySqlDataReader_Variant>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ SqlBulkCopyDataTable_Type [type: time value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataTable_Type[EXPECTED INVALID OPERATION EXCEPTION] The given value '1/1/0001 00:00:00' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.

------------------------------ SqlBulkCopyDataTable_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataTable_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ SqlBulkCopyDataRow_Type [type: time value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataRow_Type[EXPECTED INVALID OPERATION EXCEPTION] The given value '1/1/0001 00:00:00' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.

------------------------------ SqlBulkCopyDataRow_Variant [type: time value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataRow_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.
