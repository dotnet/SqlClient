using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>Only the explicitly selected TCP port can be represented as shaped SQL traffic.</summary>
public class NetworkDelayTests
{
    /// <summary>Alternate ports and implicit transports must not silently escape shaping.</summary>
    [Theory]
    [InlineData("Server=localhost")]
    [InlineData("Server=tcp:localhost,1444")]
    public void Host_UnsupportedEndpoint_Throws(string connectionString)
    {
        // Act
        var error = Record.Exception(() => NetworkDelay.Host(connectionString));

        // Assert
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>Bracketed IPv6 is passed to the route checker as an address, not a connection string.</summary>
    [Fact]
    public void Host_BracketedIpv6_ReturnsAddress()
    {
        // Act
        string host = NetworkDelay.Host("Server=tcp:[::1],1433");

        // Assert
        Assert.Equal("::1", host);
    }
}
