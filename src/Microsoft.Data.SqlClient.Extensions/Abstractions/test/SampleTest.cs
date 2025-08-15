namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SampleTest
{
    [Fact]
    public void Construction()
    {
        Assert.Equal("test", new Sample("test").Name);
    }

    [Fact]
    public void SetName()
    {
        var sample = new Sample("test");
        Assert.Equal("test", sample.Name);
        sample.SetName("new name");
        Assert.Equal("new name", sample.Name);
    }
}
