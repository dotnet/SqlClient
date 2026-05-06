// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/SqlAuthenticationParameters/*'/>
public sealed class SqlAuthenticationParameters
{
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
    // We would like to deprecate this method in favour of a new AuthenticationTimeout property.
    //
    public int ConnectionTimeout { get; }
}
