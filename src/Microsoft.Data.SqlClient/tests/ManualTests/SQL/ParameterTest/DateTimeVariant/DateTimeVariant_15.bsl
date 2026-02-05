
------------------------------ TestSimpleParameter_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
TestSimpleParameter_Type>>> EXCEPTION: [System.InvalidCastException] Failed to convert parameter value from a DateTime to a TimeSpan.

------------------------------ TestSimpleParameter_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test Simple Parameter [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]
Test Simple Parameter [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
TestSqlDataRecordParameterToTVP_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
TestSqlDataReaderParameterToTVP_Type>>> EXCEPTION: [System.InvalidCastException] Failed to convert parameter value from a DateTime to a TimeSpan.

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReader_TVP_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 
TestSqlDataReader_TVP_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSqlDataReader_TVP_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time
TestSqlDataReader_TVP_Variant>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSimpleDataReader_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 
TestSimpleDataReader_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ TestSimpleDataReader_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time
TestSimpleDataReader_Variant>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ SqlBulkCopySqlDataReader_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 
SqlBulkCopySqlDataReader_Type>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ SqlBulkCopySqlDataReader_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time
SqlBulkCopySqlDataReader_Variant>>> EXCEPTION: [System.InvalidCastException] Specified cast is not valid.

------------------------------ SqlBulkCopyDataTable_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
SqlBulkCopyDataTable_Type[EXPECTED INVALID OPERATION EXCEPTION] The given value '12/31/9999 23:59:59.9999999' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.

------------------------------ SqlBulkCopyDataTable_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopyDataRow_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
SqlBulkCopyDataRow_Type[EXPECTED INVALID OPERATION EXCEPTION] The given value '12/31/9999 23:59:59.9999999' of type DateTime from the data source cannot be converted to type time for Column 0 [f1] Row 1.

------------------------------ SqlBulkCopyDataRow_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]
