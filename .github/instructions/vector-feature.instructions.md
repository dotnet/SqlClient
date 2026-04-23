---
applyTo: "**/VectorTest/**,**/SqlVector*,**/Vector*"
---
# Vector Datatype Feature Reference

## Overview

SQL Server 2025+ introduces the `vector` data type for storing float32 vector data, useful for AI/ML workloads (embeddings, similarity search). Microsoft.Data.SqlClient supports this via the `SqlVector<T>` type and `SqlDbTypeExtensions.Vector`.

## Implementation History (PRs)

| PR | Title | Key Changes |
|----|-------|-------------|
| [#3433](https://github.com/dotnet/SqlClient/pull/3433) | Initial `SqlVectorFloat32` type | Added `SqlVectorFloat32` sealed class, `ISqlVector` interface, `SqlDbTypeExtensions.Vector`, TDS protocol support (SQLVECTOR=0xF5, FEATUREEXT_VECTORSUPPORT=0x0E), `SqlBuffer.StorageType.Vector`, MetaType for Vector, feature negotiation, backward compatibility + native test suites |
| [#3443](https://github.com/dotnet/SqlClient/pull/3443) | Refactor to generic `SqlVector<T>` | Replaced `SqlVectorFloat32` with generic `SqlVector<T> where T : unmanaged`, renamed APIs (`GetSqlVectorFloat32` → `GetSqlVector<T>`), `Values` → `Memory`, removed `ToString`/`ToArray` from public API |
| [#3468](https://github.com/dotnet/SqlClient/pull/3468) | API enhancements | Removed `Size` property (inferred from value), changed `ctor(length)` → `CreateNull(length)`, changed from `sealed class` → `readonly struct` |
| [#3559](https://github.com/dotnet/SqlClient/pull/3559) | Enable tests + ref assembly validation | Enabled vector tests against Azure SQL DB, added `VectorAPIValidationTest`, null fix improvements |
| [#4105](https://github.com/dotnet/SqlClient/pull/4105) | Fix `GetFieldType`/`GetProviderSpecificFieldType` | Both now return `typeof(SqlVector<float>)` for vector columns instead of the underlying `byte[]` type |

## Architecture

### Type System

```
SqlVector<T> (readonly struct, public)
  ├── implements INullable
  ├── implements ISqlVector (internal)
  │   ├── ElementType: byte     (0x00 = Float32)
  │   ├── ElementSize: byte     (4 for float)
  │   ├── VectorPayload: byte[] (TDS-formatted payload)
  │   ├── Size: int             (total bytes for TDS)
  │   └── Length: int           (element count)
  ├── Memory: ReadOnlyMemory<T> (public)
  ├── Length: int               (public)
  ├── IsNull: bool              (public)
  ├── Null: SqlVector<T>?       (static, public)
  └── CreateNull(int): SqlVector<T> (static, public)
```

### TDS Wire Format

The vector type uses TDS type `0xF5` (SQLVECTOR). The binary payload has an 8-byte header:

```
Offset  Size  Value        Description
------  ----  ----------   -------------------------
0       1     0xA9         Magic number
1       1     0x01         Layout version
2-3     2     [count]      Element count (little-endian uint16)
4       1     [type]       Element type (0x00=Float32)
5-7     3     0x000000     Reserved
8+      N*4   [data]       Raw float32 elements
```

### Feature Negotiation

- Feature ID: `FEATUREEXT_VECTORSUPPORT = 0x0E`
- Max supported version: `MAX_SUPPORTED_VECTOR_VERSION = 0x01`
- Feature flag: `FeatureExtension.VectorSupport = 1 << 13`
- Always requested during login (like JSON support)
- Server acknowledges with 1-byte version

### Data Flow

```
Write path:
  SqlVector<float> → ISqlVector.VectorPayload → byte[] → TdsParser writes SQLVECTOR

Read path:
  TdsParser reads SQLVECTOR → SqlBinary in SqlBuffer → SetVectorInfo(count, type)
  → GetSqlVector<T>() reconstructs SqlVector<T> from SqlBinary payload
```

### SqlBuffer Storage

- `StorageType.Vector` added to enum
- `VectorInfo` struct stores `_elementCount` and `_elementType`
- Binary data stored as `SqlBinary` in `_object`
- Metadata stored in `_value._vectorInfo`

### MetaType

```csharp
MetaType s_MetaVector:
  tdsType = 0xF5 (SQLVECTOR)
  typeName = "vector"
  classType = typeof(byte[])     // Internal representation
  sqlType = typeof(SqlBinary)    // SQL type mapping
  sqlDbType = SqlDbTypeExtensions.Vector (= 36)
  dbType = DbType.Binary
  isPlp = false                  // NOT PLP
  isLong = false
```

### SqlDbType Extension

```csharp
// On .NET 10+: uses native SqlDbType.Vector
// On older frameworks: (SqlDbType)36
public const SqlDbType Vector = (SqlDbType)36;
```

## Backward Compatibility (varchar(max) path)

When the driver or SQL Server doesn't support the native vector TDS type, vector data can be exchanged as `varchar(max)` using JSON serialization (e.g., `"[1.1, 2.2, 3.3]"`).

### How it works
1. Client serializes `float[]` to JSON string
2. Uses `SqlDbType.VarChar` with `Size = -1` (varchar(max))
3. SQL Server implicitly converts JSON text to/from vector column
4. Client reads back as `GetString()` and deserializes via `JsonSerializer`

### Test Coverage: `VectorTypeBackwardCompatibilityTests.cs`

This test suite validates the varchar(max) path with these test methods:

| Test Method | Sync/Async | What it tests |
|-------------|-----------|---------------|
| `TestVectorDataInsertionAsVarchar` | Sync | 4 SqlParameter construction patterns × (non-null + null) |
| `TestVectorParameterInitializationAsync` | Async | Same 4 patterns × (non-null + null) |
| `TestVectorDataReadsAsVarchar` | Sync | GetString, GetSqlString, GetFieldValue\<string\> for non-null + null |
| `TestVectorDataReadsAsVarcharAsync` | Async | Same read methods async |
| `TestStoredProcParamsForVectorAsVarchar` | Sync | SP with VARCHAR(MAX) input/output params |
| `TestStoredProcParamsForVectorAsVarcharAsync` | Async | Same SP test async |
| `TestSqlBulkCopyForVectorAsVarchar` | Sync | BulkCopy from varchar(max) source to vector dest |
| `TestSqlBulkCopyForVectorAsVarcharAsync` | Async | Same bulk copy async |
| `TestInsertVectorsAsVarcharWithPrepare` | Sync | Prepared statement with varchar vector param |

### 4 SqlParameter Construction Patterns (backward compat)

1. **Default ctor + property setters**: `new SqlParameter() { ParameterName, SqlDbType=VarChar, Size=-1, Value=json }`
2. **Name + value ctor**: `new SqlParameter("@p", json)`
3. **Name + SqlDbType ctor**: `new SqlParameter("@p", SqlDbType.VarChar) { Value = json }`
4. **Name + SqlDbType + Size ctor**: `new SqlParameter("@p", SqlDbType.VarChar, -1) { Value = json }`

## Native Vector Support Test Suite: `NativeVectorFloat32Tests.cs`

Uses `[ConditionalTheory]` + `[MemberData]` for parameterized tests.

### Test data generator: `VectorFloat32TestData.GetVectorFloat32TestData()`

Generates 4 parameter patterns × 4 value types:
- `SqlVector<float>(testData)` (non-null)
- `SqlVector<float>.CreateNull(length)` (typed null)
- `DBNull.Value` (generic null)
- `SqlVector<float>.Null` (static null, only pattern 1 supported)

### 4 SqlParameter Construction Patterns (native)

1. **Explicit SqlDbType**: `new SqlParameter { SqlDbType = Vector, Value = sqlVector }`
2. **Value inference**: `new SqlParameter("@p", sqlVector)` (type inferred)
3. **SqlDbType + Value**: `new SqlParameter("@p", Vector) { Value = sqlVector }`
4. **SqlDbType + Size + Value**: `new SqlParameter("@p", Vector, size) { Value = sqlVector }` (size ignored)

## Key Source Files

| File | Purpose |
|------|---------|
| `src/.../SqlTypes/SqlVector.cs` | `SqlVector<T>` struct definition |
| `src/.../SqlClient/ISqlVector.cs` | Internal `ISqlVector` interface |
| `src/.../SqlClient/SqlBuffer.cs` | Vector storage in result buffers |
| `src/.../SqlClient/SqlEnums.cs` | `MetaType` for vector, `SqlVectorElementType` enum |
| `src/.../SqlClient/TdsEnums.cs` | TDS constants (SQLVECTOR, FEATUREEXT_VECTORSUPPORT) |
| `src/.../SqlDbTypeExtensions.cs` | `SqlDbTypeExtensions.Vector` |
| `src/.../SqlClient/TdsParser.cs` | TDS read/write for vector data |
| `src/.../SqlClient/SqlDataReader.cs` | `GetSqlVector<T>()`, `GetFieldType` fix |
| `src/.../SqlClient/SqlCommand.cs` | BuildParamList vector handling |
| `src/.../SqlClient/SqlInternalConnectionTds.cs` | Feature negotiation |
| `netcore/ref/Microsoft.Data.SqlClient.cs` | Public API surface (.NET Core) |
| `netfx/ref/Microsoft.Data.SqlClient.cs` | Public API surface (.NET Framework) |

## Test Files

| File | Purpose |
|------|---------|
| `tests/ManualTests/SQL/VectorTest/NativeVectorFloat32Tests.cs` | Native vector type integration tests |
| `tests/ManualTests/SQL/VectorTest/VectorTypeBackwardCompatibilityTests.cs` | varchar(max) backward compat tests |
| `tests/ManualTests/SQL/VectorTest/VectorAPIValidationTest.cs` | Ref assembly API validation (no DB) |
| `tests/FunctionalTests/SqlVectorTest.cs` | Unit tests for SqlVector<T> |
