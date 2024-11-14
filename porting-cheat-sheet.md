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
| ConnectionString property uses non-backward compatible keywords with spaces. | The `SqlConnectionStringBuilder` has a `ConnectionString` property that can be used to get the connection string to connect with. The [`Microsoft.Data.SqlClient` connection string](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring) and the [`System.Data.SqlClient` connection string](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring) do not support all of the same keywords, so be mindful of that. The `Microsoft.Data.SqlClient` also adds new aliases for some existing keywords. For example, `Microsoft.Data.SqlClient` now supports `Application Intent=ReadOnly` and `Multi Subnet Failover=True`, in addition to the previous `ApplicationIntent=ReadOnly` and `MultiSubnetFailover=True` (notice the spaces in the keywords). Unfortunately, the `SqlConnectionStringBuilder.ConnectionString` uses the new aliases with spaces, which do not exist and are unsupported in `System.Data.SqlClient`. This means if you build a connection string with `Microsoft.Data.SqlClient`, but attempt to use it in an application using `System.Data.SqlClient`, the connection string will not be valid for `System.Data.SqlClient`; you will need to sanitize the connection string by removing the spaces from the keywords. |

## .NET Framework to .NET Considerations

In .NET Framework and .NET versions prior to .NET 5, globalization APIs use National Language Support (NLS) on Windows. Starting in .NET 5, .NET globalization APIs changed to use International Components for Unicode (ICU) on Windows in order to be consistent across all platforms (Windows, Linux, macOS, etc.). This affects the behavior of comparisons of some SqlStrings in System.Data.SqlTypes. Comparisons using ICU don't always match NLS for some strings. Since SQL Server still uses NLS on the server side for string comparisons, this difference can result in SqlString comparisons behaving differently than server side string comparisons. If your application relies on SqlString behavior matching server side behavior, you need to resolve this issue. For detailed information in .NET, see [Globalization and ICU](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu).

If your application is affected, the workaround is to [Use NLS instead of ICU](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu#use-nls-instead-of-icu) in your application.

## Contribute to this Cheat Sheet

We would love the SqlClient community to help enhance this cheat sheet by contributing experiences and challenges faced when porting their applications.
