
------------------------------ TestSimpleParameter_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
TestSimpleParameter_Type>>> EXCEPTION: [System.ArgumentOutOfRangeException] The added or subtracted value results in an un-representable DateTime.
Parameter name: value

------------------------------ TestSimpleParameter_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == smalldatetime : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test Simple Parameter [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = smalldatetime]
Test Simple Parameter [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataRecordParameterToTVP_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
TestSqlDataRecordParameterToTVP_Type>>> EXCEPTION: [System.ArgumentException] Invalid value for this metadata.

------------------------------ TestSqlDataRecordParameterToTVP_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == smalldatetime : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = smalldatetime]
Test SqlDataRecord Parameter To TVP [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReaderParameterToTVP_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
TestSqlDataReaderParameterToTVP_Type>>> EXCEPTION: [System.ArgumentOutOfRangeException] The added or subtracted value results in an un-representable DateTime.
Parameter name: value

------------------------------ TestSqlDataReaderParameterToTVP_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == smalldatetime : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = smalldatetime]
Test SqlDataReader Parameter To TVP [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ TestSqlDataReader_TVP_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
TestSqlDataReader_TVP_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting character string to smalldatetime data type.

------------------------------ TestSqlDataReader_TVP_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
TestSqlDataReader_TVP_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting character string to smalldatetime data type.

------------------------------ TestSimpleDataReader_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
TestSimpleDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting character string to smalldatetime data type.

------------------------------ TestSimpleDataReader_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
TestSimpleDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting character string to smalldatetime data type.

------------------------------ SqlBulkCopySqlDataReader_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
SqlBulkCopySqlDataReader_Type>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting character string to smalldatetime data type.

------------------------------ SqlBulkCopySqlDataReader_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
SqlBulkCopySqlDataReader_Variant>>> EXCEPTION: [Microsoft.Data.SqlClient.SqlException] Conversion failed when converting character string to smalldatetime data type.

------------------------------ SqlBulkCopyDataTable_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
SqlBulkCopyDataTable_Type>>> EXCEPTION: [System.ArgumentOutOfRangeException] The added or subtracted value results in an un-representable DateTime.
Parameter name: value

------------------------------ SqlBulkCopyDataTable_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == smalldatetime : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = smalldatetime]
SqlBulkCopy From Data Table [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]

------------------------------ SqlBulkCopyDataRow_Type [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
SqlBulkCopyDataRow_Type>>> EXCEPTION: [System.ArgumentOutOfRangeException] The added or subtracted value results in an un-representable DateTime.
Parameter name: value

------------------------------ SqlBulkCopyDataRow_Variant [type: smalldatetime value:12/31/9999 23:59:59.9999999] ------------------------------
Type        => Expected : Actual == System.DateTime : System.DateTime
Base Type   => Expected : Actual == smalldatetime : datetime
Value       => Expected : Actual == 3155378975999999999 : 3155378975999970000
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VARIANT BASE TYPE MISMATCH!!! [Actual = datetime] [Expected = smalldatetime]
SqlBulkCopy From Data Row [Variant Type]>>> ERROR: VALUE MISMATCH!!! [Actual = 12/31/9999 11:59:59 PM] [Expected = 12/31/9999 11:59:59 PM]
