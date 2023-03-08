# Cheat sheet for porting from System.Data.SqlClient to Microsoft.Data.SqlClient

This guide is meant to cover all namespace changes needed in client applications when porting SqlClient references to Microsoft.Data.SqlClient:

## Namespace Changes needed

### Microsoft.Data.SqlClient v5.0 and newer

| Namespace Change | Applicability |
|--|--|
| <s>`using System.Data.SqlClient;`</s><br>`using Microsoft.Data.SqlClient;` | Applicable to all classes, enums and delegates. |
| <s>`using Microsoft.SqlServer.Server;`</s><br>`using Microsoft.Data.SqlClient.Server;` | Applicable Classes: <br>`SqlDataRecord`<br>`SqlMetaData` <br/><br/> <sup>1</sup> _All remaining types continue to be referenced from Microsoft.SqlServer.Server namespace._|
| <s>`using System.Data.SqlTypes;`</s> <br>`using Microsoft.Data.SqlTypes;` | Applicable Classes:<br>`SqlFileStream`|
| <s>`using System.Data.Sql;`</s> <br>`using Microsoft.Data.Sql;`</s> | Applicable Classes:<br>`SqlNotificationRequest`<br> |
| <s>`using System.Data;`</s> <br>`using Microsoft.Data;`</s> | Applicable Classes:<br>`OperationAbortedException`|

<sup>1</sup> Breaking change for User-Defined types and Microsoft.SqlServer.Types support over _Microsoft.Data.SqlClient v3.0.0_.

### Microsoft.Data.SqlClient v4.0 and older

| Namespace Change | Applicability |
|--|--|
| <s>`using System.Data.SqlClient;`</s><br>`using Microsoft.Data.SqlClient;` | Applicable to all classes, enums and delegates. |
| <s>`using Microsoft.SqlServer.Server;`</s><br>`using Microsoft.Data.SqlClient.Server;` | Applicable Classes: <br>`InvalidUdtException`<br>`SqlDataRecord`<br>`SqlFunctionAttribute`<br>`SqlMetaData`<br>`SqlMethodAttribute`<br>`SqlUserDefinedAggregateAttribute`<br>`SqlUserDefinedTypeAttribute`<br><br>Applicable Interfaces: <br>`IBinarySerialize`<br><br>Applicable Enums: <br>`DataAccessKind`<br>`Format`<br>`SystemDataAccessKind`|
| <s>`using System.Data.SqlTypes;`</s> <br>`using Microsoft.Data.SqlTypes;` | Applicable Classes:<br>`SqlFileStream`|
| <s>`using System.Data.Sql;`</s> <br>`using Microsoft.Data.Sql;`</s> | Applicable Classes:<br>`SqlNotificationRequest`<br> |
| <s>`using System.Data;`</s> <br>`using Microsoft.Data;`</s> | Applicable Classes:<br>`OperationAbortedException`|

## Configuration

For .NET Framework projects it may be necessary to include the following in your App.config or Web.config file:

``` xml
<configuration>
    ...
    <system.data>
        <DbProviderFactories>
            <add name="SqlClient Data Provider"
                invariant="Microsoft.Data.SqlClient"
                description=".Net Framework Data Provider for SqlServer" 
                type="Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient" />
        </DbProviderFactories>
    </system.data>
    ...
</configuration>
```

## Functionality Changes

| System.Data.SqlClient | Microsoft.Data.SqlClient |
|--|--|
| Can use DateTime object as value for SqlParameter with type `DbType.Time`. | Must use TimeSpan object as value for SqlParameter with type `DbType.Time`. |
| Using DateTime object as value for SqlParameter with type `DbType.Date` would send date and time to SQL Server. | DateTime object's time components will be truncated when sent to SQL Server using `DbType.Date`. |
| `Encrypt` defaults to `false`. | Starting in v4.0, default encryption settings were made more secure, requiring opt-in to non-encrypted connections. `Encrypt` defaults to `true` and the driver will always validate the server certificate based on `TrustServerCertificate`. (Previously, server certificates would only be validated if `Encrypt` was also `true`.)<br/><br/>If you need to turn off encryption, you must specify `Encrypt=false`. If you use encryption with a self-signed certificate on the server, you must specify `TrustServerCertificate=true`.<br/><br/>In v5.0, `SqlConnectionStringBuilder.Encrypt` is no longer a `bool`. It's a `SqlConnectionEncryptOption` with multiple values to support `Strict` encryption mode (TDS 8.0). It uses implicit conversion operators to remain code-backwards compatible, but it was a binary breaking change, requiring a recompile of applications. |

## Contribute to this Cheat Sheet

We would love the SqlClient community to help enhance this cheat sheet by contributing experiences and challenges faced when porting their applications.
