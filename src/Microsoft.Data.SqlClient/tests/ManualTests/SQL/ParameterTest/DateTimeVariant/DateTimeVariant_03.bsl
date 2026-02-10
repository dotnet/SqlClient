
------------------------------ TestSimpleParameter_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ TestSimpleParameter_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test Simple Parameter [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = datetime2]

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = datetime2]

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = datetime2]

------------------------------ TestSqlDataReader_TVP_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ TestSqlDataReader_TVP_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime2
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ TestSimpleDataReader_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ TestSimpleDataReader_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime2
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ SqlBulkCopySqlDataReader_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ SqlBulkCopySqlDataReader_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime2
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ SqlBulkCopyDataTable_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ SqlBulkCopyDataTable_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = datetime2]

------------------------------ SqlBulkCopyDataRow_Type [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378975999999999

------------------------------ SqlBulkCopyDataRow_Variant [type: datetime2 value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == datetime2 : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = datetime2]
