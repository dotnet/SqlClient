// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/SqlAuthenticationParametersBase/*'/>
public abstract class SqlAuthenticationParametersBase
{
    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/AuthenticationMethod/*'/>
    public SqlAuthenticationMethod AuthenticationMethod { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/Resource/*'/>
    public string Resource { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/Authority/*'/>
    public string Authority { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/UserId/*'/>
    public string? UserId { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/Password/*'/>
    public string? Password { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/ConnectionId/*'/>
    public Guid ConnectionId { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/ServerName/*'/>
    public string ServerName { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/DatabaseName/*'/>
    public string DatabaseName { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/ConnectionTimeout/*'/>
    public int ConnectionTimeout { get; }

    /// <include file='../doc/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/ctor/*'/>
    protected SqlAuthenticationParametersBase(
        SqlAuthenticationMethod authenticationMethod,
        string serverName,
        string databaseName,
        string resource,
        string authority,
        string? userId,
        string? password,
        Guid connectionId,
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
