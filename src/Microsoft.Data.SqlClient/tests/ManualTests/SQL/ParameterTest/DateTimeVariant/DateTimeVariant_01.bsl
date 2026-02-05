
------------------------------ TestSimpleParameter_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
Test Simple Parameter [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSimpleParameter_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test Simple Parameter [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]
Test Simple Parameter [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
Test SqlDataRecord Parameter To TVP [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]
Test SqlDataRecord Parameter To TVP [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
Test SqlDataReader Parameter To TVP [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]
Test SqlDataReader Parameter To TVP [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReader_TVP_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
Test SqlDataReader TVP [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReader_TVP_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
Test SqlDataReader TVP [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSimpleDataReader_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
Test Simple Data Reader [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSimpleDataReader_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
Test Simple Data Reader [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopySqlDataReader_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
SqlBulkCopy From SqlDataReader [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopySqlDataReader_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : date
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
SqlBulkCopy From SqlDataReader [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopyDataTable_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
SqlBulkCopy From Data Table [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopyDataTable_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]
SqlBulkCopy From Data Table [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopyDataRow_Type [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual ==  : 
Value       => Expected : Actual == 3155378975999999999 : 3155378112000000000
SqlBulkCopy From Data Row [Data Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 12:00:00 AM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopyDataRow_Variant [type: date value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == date : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = date]
SqlBulkCopy From Data Row [Variant Type][EXPECTED ERROR]: VALUE MISMATCH - [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]
