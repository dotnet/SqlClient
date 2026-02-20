# Microsoft.SqlServer.Server

[![NuGet](https://img.shields.io/nuget/v/Microsoft.SqlServer.Server.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.SqlServer.Server)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.SqlServer.Server?style=flat-square)](https://www.nuget.org/packages/Microsoft.SqlServer.Server)

## Description

This is a helper library for [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient), enabling cross-framework support for **User-Defined Types (UDTs)** in SQL Server.

The package provides the types and attributes needed to create SQL Server CLR user-defined types, user-defined aggregates, and user-defined functions that work seamlessly across .NET Framework and .NET Core/.NET.

## Supportability

This package supports:

- .NET Framework 4.6+
- .NET Standard 2.0 (for .NET Core 2.0+ and .NET 5+)

## Installation

Install the package via NuGet:

```bash
dotnet add package Microsoft.SqlServer.Server
```

Or via the Package Manager Console:

```powershell
Install-Package Microsoft.SqlServer.Server
```

## Available Types

This package provides the following types in the `Microsoft.SqlServer.Server` namespace:

| Type | Description |
|------|-------------|
| `IBinarySerialize` | Interface for custom binary serialization of UDTs |
| `InvalidUdtException` | Exception thrown when a UDT is invalid |
| `SqlFacetAttribute` | Specifies additional information about UDT properties |
| `SqlFunctionAttribute` | Marks a method as a SQL Server function |
| `SqlMethodAttribute` | Specifies method properties for UDT methods |
| `SqlUserDefinedAggregateAttribute` | Marks a class as a user-defined aggregate |
| `SqlUserDefinedTypeAttribute` | Marks a class as a user-defined type |
| `DataAccessKind` | Describes data access behavior for a function |
| `SystemDataAccessKind` | Describes system data access behavior |
| `Format` | Specifies the serialization format for UDTs |

## Getting Started

### Creating a User-Defined Type

```csharp
using Microsoft.SqlServer.Server;
using System.Data.SqlTypes;

[SqlUserDefinedType(Format.Native)]
public struct Point : INullable
{
    private bool _isNull;
    private double _x;
    private double _y;

    public bool IsNull => _isNull;

    public static Point Null
    {
        get
        {
            var p = new Point();
            p._isNull = true;
            return p;
        }
    }

    public double X
    {
        get => _x;
        set => _x = value;
    }

    public double Y
    {
        get => _y;
        set => _y = value;
    }

    public static Point Parse(SqlString s)
    {
        if (s.IsNull)
            return Null;
        
        var parts = s.Value.Split(',');
        return new Point
        {
            X = double.Parse(parts[0]),
            Y = double.Parse(parts[1])
        };
    }

    public override string ToString() => _isNull ? "NULL" : $"{_x},{_y}";
}
```

### Creating a User-Defined Aggregate

```csharp
using Microsoft.SqlServer.Server;
using System.Data.SqlTypes;
using System.IO;

[SqlUserDefinedAggregate(Format.UserDefined, MaxByteSize = 8000)]
public class Concatenate : IBinarySerialize
{
    private StringBuilder _builder;

    public void Init()
    {
        _builder = new StringBuilder();
    }

    public void Accumulate(SqlString value)
    {
        if (!value.IsNull)
        {
            if (_builder.Length > 0)
                _builder.Append(",");
            _builder.Append(value.Value);
        }
    }

    public void Merge(Concatenate other)
    {
        if (other._builder.Length > 0)
        {
            if (_builder.Length > 0)
                _builder.Append(",");
            _builder.Append(other._builder);
        }
    }

    public SqlString Terminate() => new SqlString(_builder.ToString());

    public void Read(BinaryReader reader) => _builder = new StringBuilder(reader.ReadString());
    public void Write(BinaryWriter writer) => writer.Write(_builder.ToString());
}
```

## Documentation

- [SQL Server CLR Integration](https://learn.microsoft.com/sql/relational-databases/clr-integration/clr-integration-overview)
- [CLR User-Defined Types](https://learn.microsoft.com/sql/relational-databases/clr-integration-database-objects-user-defined-types/clr-user-defined-types)
- [CLR User-Defined Aggregates](https://learn.microsoft.com/sql/relational-databases/clr-integration-database-objects-user-defined-functions/clr-user-defined-aggregates)
- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)

## Release Notes

Release notes are available at: https://go.microsoft.com/fwlink/?linkid=2185441

## License

This package is licensed under the [MIT License](https://licenses.nuget.org/MIT).

## Related Packages

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) - The main SqlClient driver
