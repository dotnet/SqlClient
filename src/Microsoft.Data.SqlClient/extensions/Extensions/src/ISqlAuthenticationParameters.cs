// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/ISqlAuthenticationParameters.xml' path='docs/members[@name="ISqlAuthenticationParameters"]/ISqlAuthenticationParameters/*'/>
public interface ISqlAuthenticationParameters
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
}
