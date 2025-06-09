// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/ISqlAuthenticationParameters/*'/>
public class SqlAuthenticationParametersBase
{
    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/AuthenticationMethod/*'/>
    public SqlAuthenticationMethod AuthenticationMethod { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/Resource/*'/>
    public string Resource { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/Authority/*'/>
    public string Authority { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/UserId/*'/>
    public string? UserId { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/Password/*'/>
    public string? Password { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/ConnectionId/*'/>
    public Guid ConnectionId { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/ServerName/*'/>
    public string ServerName { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/DatabaseName/*'/>
    public string DatabaseName { get; }

    /// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/ConnectionTimeout/*'/>
    public int ConnectionTimeout { get; }

    /// <include file='../doc/Microsoft.Data.SqlClient/SqlAuthenticationParametersBase.xml' path='docs/members[@name="SqlAuthenticationParametersBase"]/ctor/*'/>
    protected SqlAuthenticationParametersBase(
        SqlAuthenticationMethod authenticationMethod,
        string serverName,
        string databaseName,
        string resource,
        string authority,
        string userId,
        string password,
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
