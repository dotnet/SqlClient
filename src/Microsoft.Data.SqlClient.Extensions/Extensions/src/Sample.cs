namespace Microsoft.Data.SqlClient.Extensions;

/// <include file='../doc/Sample.xml' path='docs/members[@name="Sample"]/Sample/*'/>
public class Sample
{
    /// <include file='../doc/Sample.xml' path='docs/members[@name="Sample"]/ctor/*'/>
    public Sample(string name)
    {
        Name = name;
    }

    /// <include file='../doc/Sample.xml' path='docs/members[@name="Sample"]/Name/*'/>
    public string Name { get; private set; }

    // Update the name.
    internal void SetName(string name)
    {
        Name = name;
    }
}
