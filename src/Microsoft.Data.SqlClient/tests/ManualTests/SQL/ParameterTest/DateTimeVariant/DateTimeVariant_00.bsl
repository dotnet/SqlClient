
------------------------------ TestSimpleParameter_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ TestSimpleParameter_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
TestSimpleParameter_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
TestSqlDataRecordParameterToTVP_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
TestSqlDataReaderParameterToTVP_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ TestSqlDataReader_TVP_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ TestSqlDataReader_TVP_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 0 : 0

------------------------------ TestSimpleDataReader_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ TestSimpleDataReader_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 0 : 0

------------------------------ SqlBulkCopySqlDataReader_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ SqlBulkCopySqlDataReader_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 0 : 0

------------------------------ SqlBulkCopyDataTable_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ SqlBulkCopyDataTable_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataTable_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.

------------------------------ SqlBulkCopyDataRow_Type [type: date value:1/1/0001 00:00:00] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 0 : 0

------------------------------ SqlBulkCopyDataRow_Variant [type: date value:1/1/0001 00:00:00] ------------------------------
SqlBulkCopyDataRow_Variant>>> EXCEPTION: [System.Data.SqlTypes.SqlTypeException] SqlDateTime overflow. Must be between 1/1/1753 12:00:00 AM and 12/31/9999 11:59:59 PM.
