namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationParametersTest
{
    // Verify that the properties are set correctly when nullable arguments are
    // null.
    [Fact]
    public void Constructor_WithNulls()
    {
        var method = SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
        var server = "server";
        var database = "database";
        var resource = "resource";
        var authority = "authority";
        string? user = null;
        string? pass = null;
        var id = Guid.NewGuid();
        var timeout = 30;

        SqlAuthenticationParameters parameters = new(
            method,
            server,
            database,
            resource,
            authority,
            user,
            pass,
            id,
            timeout);

        Assert.Equal(method, parameters.AuthenticationMethod);
        Assert.Equal(server, parameters.ServerName);
        Assert.Equal(database, parameters.DatabaseName);
        Assert.Equal(resource, parameters.Resource);
        Assert.Equal(authority, parameters.Authority);
        Assert.Null(parameters.UserId);
        Assert.Null(parameters.Password);
        Assert.Equal(id, parameters.ConnectionId);
        Assert.Equal(timeout, parameters.ConnectionTimeout);
    }

    // Verify that the properties are set correctly when nullable arguments
    // are non-null.
    [Fact]
    public void Constructor_WithoutNulls()
    {
        var method = SqlAuthenticationMethod.ActiveDirectoryIntegrated;
        var server = "server";
        var database = "database";
        var resource = "resource";
        var authority = "authority";
        var user = "user";
        var pass = "pass";
        var id = Guid.NewGuid();
        var timeout = 30;

        SqlAuthenticationParameters parameters = new(
            method,
            server,
            database,
            resource,
            authority,
            user,
            pass,
            id,
            timeout);

        Assert.Equal(method, parameters.AuthenticationMethod);
        Assert.Equal(server, parameters.ServerName);
        Assert.Equal(database, parameters.DatabaseName);
        Assert.Equal(resource, parameters.Resource);
        Assert.Equal(authority, parameters.Authority);
        Assert.Equal(user, parameters.UserId);
        Assert.Equal(pass, parameters.Password);
        Assert.Equal(id, parameters.ConnectionId);
        Assert.Equal(timeout, parameters.ConnectionTimeout);
    }
}
