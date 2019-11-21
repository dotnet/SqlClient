# Cheat sheet for porting from System.Data.SqlClient to Microsoft.Data.SqlClient

This guide is to cover all namespace changes needed in client applications when attempting porting SqlClient reference to Microsoft.Data.SqlClient:

## Namespace Changes needed

| Namespace Change | Applicability |
|--|--|
| <pre><code><del>using System.Data.SqlClient;</del> <br>using Microsoft.Data.SqlClient;</pre></code> | Applicable on all classes, enums and delegates. |
| <pre><code><del>using Microsoft.SqlServer.Server;</del> <br>using Microsoft.Data.SqlClient.Server;</pre></code> | Applicable Classes: <br>`InvalidUdtException`<br>`SqlDataRecord`<br>`SqlFunctionAttribute`<br>`SqlMetaData`<br>`SqlMethodAttribute`<br>`SqlUserDefinedAggregateAttribute`<br>`SqlUserDefinedTypeAttribute`<br><br>Applicable Interfaces: <br>`IBinarySerialize`<br><br>Applicable Enums: <br>`DataAccessKind`<br>`Format`<br>`SystemDataAccessKind`|
| <pre><code><del>using System.Data.SqlTypes;</del> <br>using Microsoft.Data.SqlTypes;</pre></code> | Applicable Classes:<br>`SqlFileStream`|
| <pre><code><del>using System.Data.Sql;</del> <br>using Microsoft.Data.Sql;</pre></code> | Applicable Classes:<br>`SqlNotificationRequest`<br> |
| <pre><code><del>using System.Data;</del> <br>using Microsoft.Data;</pre></code> | Applicable Classes:<br>`OperationAbortedException`|

## Contribute to Cheat Sheet

We would love SqlClient community to help advance this cheat sheet by contributing experiences and challenges faced when porting their applications.