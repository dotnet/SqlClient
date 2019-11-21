# Cheat sheet for porting from System.Data.SqlClient to Microsoft.Data.SqlClient

This guide is meant to cover all namespace changes needed in client applications when porting SqlClient references to Microsoft.Data.SqlClient:

## Namespace Changes needed

| Namespace Change | Applicability |
|--|--|
| <s>`using System.Data.SqlClient;`</s><br>`using Microsoft.Data.SqlClient;` | Applicable to all classes, enums and delegates. |
| <s>`using Microsoft.SqlServer.Server;`</s><br>`using Microsoft.Data.SqlClient.Server;` | Applicable Classes: <br>`InvalidUdtException`<br>`SqlDataRecord`<br>`SqlFunctionAttribute`<br>`SqlMetaData`<br>`SqlMethodAttribute`<br>`SqlUserDefinedAggregateAttribute`<br>`SqlUserDefinedTypeAttribute`<br><br>Applicable Interfaces: <br>`IBinarySerialize`<br><br>Applicable Enums: <br>`DataAccessKind`<br>`Format`<br>`SystemDataAccessKind`|
| <s>`using System.Data.SqlTypes;`</s> <br>`using Microsoft.Data.SqlTypes;` | Applicable Classes:<br>`SqlFileStream`|
| <s>`using System.Data.Sql;`</s> <br>`using Microsoft.Data.Sql;`</s> | Applicable Classes:<br>`SqlNotificationRequest`<br> |
| <s>`using System.Data;`</s> <br>`using Microsoft.Data;`</s> | Applicable Classes:<br>`OperationAbortedException`|

## Contribute to this Cheat Sheet

We would love the SqlClient community to help enhance this cheat sheet by contributing experiences and challenges faced when porting their applications.
