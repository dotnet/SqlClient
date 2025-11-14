// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/SqlAuthenticationParameters/*'/>
public sealed class SqlAuthenticationParameters
{
    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/AuthenticationMethod/*'/>
    public SqlAuthenticationMethod AuthenticationMethod { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Resource/*'/>
    public string Resource { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Authority/*'/>
    public string Authority { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/UserId/*'/>
    public string? UserId { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Password/*'/>
    public string? Password { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ConnectionId/*'/>
    public Guid ConnectionId { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ServerName/*'/>
    public string ServerName { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/DatabaseName/*'/>
    public string DatabaseName { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ConnectionTimeout/*'/>
    //
    // We would like to deprecate this method in favour of a new
    // AuthenticationTimeout property.  See:
    //
    // https://microsoft.sharepoint-df.com/:fl:/g/contentstorage/CSP_e68c6b62-34b4-4eaa-b836-82e9cdaa0149/IQCyPmTP5HlYSpafY3DJ-8sQAbY4Ajjn2ztRZrM_eQZkyJQ?e=k1nHJd&nav=cz0lMkZjb250ZW50c3RvcmFnZSUyRkNTUF9lNjhjNmI2Mi0zNGI0LTRlYWEtYjgzNi04MmU5Y2RhYTAxNDkmZD1iJTIxWW11TTVyUTBxazY0Tm9McHphb0JTYXhVNHFkaEY5ZE9yS0ZkWTR0cDY3WU5rRUhKaHM0R1JJTjhQanNwcGliSyZmPTAxWklZTVRaNVNIWlNNN1pEWkxCRkpOSDNET0RFN1hTWVEmYz0lMkYmYT1Mb29wQXBwJnA9JTQwZmx1aWR4JTJGbG9vcC1wYWdlLWNvbnRhaW5lciZ4PSU3QiUyMnclMjIlM0ElMjJUMFJUVUh4dGFXTnliM052Wm5RdWMyaGhjbVZ3YjJsdWRDMWtaaTVqYjIxOFlpRlpiWFZOTlhKUk1IRnJOalJPYjB4d2VtRnZRbE5oZUZVMGNXUm9SamxrVDNKTFJtUlpOSFJ3TmpkWlRtdEZTRXBvY3pSSFVrbE9PRkJxYzNCd2FXSkxmREF4V2tsWlRWUmFXbE5DTlVVMFJrMVFSemRhUlROWlV6Vk9SVkZDTmxkRE1rRSUzRCUyMiUyQyUyMmklMjIlM0ElMjI1YzA2ZTE4OS03NWExLTRkNDktYjQyYi1iOTk2YmM4MDc4ZjklMjIlN0Q%3D
    //
    public int ConnectionTimeout { get; }

    /// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ctor/*'/>
    public SqlAuthenticationParameters(
        SqlAuthenticationMethod authenticationMethod,
        string serverName,
        string databaseName,
        string resource,
        string authority,
        string? userId,
        string? password,
        Guid connectionId,
        // This parameter should really be named authenticationTimeout, but we
        // must keep the old name for backwards compatibility.
        int connectionTimeout)
    {
        AuthenticationMethod = authenticationMethod;
        ServerName = serverName;
        DatabaseName = databaseName;
        Resource = resource;
        Authority = authority;
        UserId = userId;
        Password = password;
        ConnectionId = connectionId;
        ConnectionTimeout = connectionTimeout;
    }
}
