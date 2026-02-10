
------------------------------ TestSimpleParameter_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------

------------------------------ TestSimpleParameter_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test Simple Parameter [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]

------------------------------ TestSqlDataReader_TVP_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 

------------------------------ TestSqlDataReader_TVP_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time

------------------------------ TestSimpleDataReader_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 

------------------------------ TestSimpleDataReader_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time

------------------------------ SqlBulkCopySqlDataReader_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual ==  : 

------------------------------ SqlBulkCopySqlDataReader_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.TimeSpan
Base Type   => Expected : Actual == time : time

------------------------------ SqlBulkCopyDataTable_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------

------------------------------ SqlBulkCopyDataTable_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]

------------------------------ SqlBulkCopyDataRow_Type [type: time value:12/31/9999 23:59:59.9999999] ------------------------------

------------------------------ SqlBulkCopyDataRow_Variant [type: time value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == time : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = time]
