
------------------------------ TestSimpleParameter_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ TestSimpleParameter_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test Simple Parameter [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]

------------------------------ TestSqlDataReader_TVP_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ TestSqlDataReader_TVP_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ TestSimpleDataReader_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ TestSimpleDataReader_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ SqlBulkCopySqlDataReader_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ SqlBulkCopySqlDataReader_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ SqlBulkCopyDataTable_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ SqlBulkCopyDataTable_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]

------------------------------ SqlBulkCopyDataRow_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000

------------------------------ SqlBulkCopyDataRow_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]
