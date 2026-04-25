---
applyTo: "**/VectorTest/**,**/SqlVector*,**/Vector*"
---
# Vector Datatype Feature Reference

## Overview

SQL Server 2025+ introduces the `vector` data type for storing vector data, useful for AI/ML workloads (embeddings, similarity search). Microsoft.Data.SqlClient supports this via the `SqlVector<T>` type and `SqlDbTypeExtensions.Vector`.

### Supported Vector Base Types

| Base Type | SQL Declaration | Element Type Byte | Client Support |
|-----------|----------------|-------------------|----------------|
| `float32` (default) | `VECTOR(N)` or `VECTOR(N, float32)` | `0x00` | Native TDS + varchar(max) backward compat |
| `float16` (preview) | `VECTOR(N, float16)` | TBD | varchar(max) backward compat only (no native TDS feature extension yet) |

> **Note**: `float16` requires `ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON;` on the server while it remains a preview feature.

## Implementation History (PRs)

| PR | Milestone | Title | Key Changes |
|----|-----------|-------|-------------|
| [#3433](https://github.com/dotnet/SqlClient/pull/3433) | 6.1-preview2 | Initial `SqlVectorFloat32` type (closes [#3317](https://github.com/dotnet/SqlClient/issues/3317)) | Added `SqlVectorFloat32` sealed class, `ISqlVector` interface, `SqlDbTypeExtensions.Vector`, TDS protocol support (SQLVECTOR=0xF5, FEATUREEXT_VECTORSUPPORT=0x0E, VECTOR_HEADER_SIZE=8, MAX_SUPPORTED_VECTOR_VERSION=0x01), `SqlBuffer.StorageType.Vector`, MetaType for Vector, feature negotiation, BulkCopy support, backward compatibility + native test suites. 38 files, +2578/-25 |
| [#3443](https://github.com/dotnet/SqlClient/pull/3443) | 6.1-preview2 | Refactor to generic `SqlVector<T>` | Replaced `SqlVectorFloat32` with generic `SqlVector<T> where T : unmanaged`, renamed APIs (`GetSqlVectorFloat32` → `GetSqlVector<T>`), `Values` → `Memory`, removed `ToString`/`ToArray` from public API. 24 files, +722/-522 |
| [#3468](https://github.com/dotnet/SqlClient/pull/3468) | 6.1-preview2 | API enhancements | Removed `Size` property (inferred from `SqlParameter.Value`; if specified it is ignored), changed `ctor(length)` → static factory `CreateNull(length)`, changed from `sealed class` → `readonly struct`. 10 files, +110/-117 |
| [#3559](https://github.com/dotnet/SqlClient/pull/3559) | 6.1 GA | Enable tests + ref assembly validation | Enabled vector tests against Azure SQL DB, added `VectorAPIValidationTest`, replaced static `IsJsonSupported`/`IsVectorSupported` flags with `IsAzureServer` helper, fixed XML doc include path for `ctor1`. 11 files, +163/-73 |
| [#4105](https://github.com/dotnet/SqlClient/pull/4105) | 7.1.0-preview1 (Hotfix 7.0.1, 6.1.5) | Fix `GetFieldType`/`GetProviderSpecificFieldType` (fixes [#4104](https://github.com/dotnet/SqlClient/issues/4104)) | Both now return `typeof(SqlVector<float>)` for vector columns instead of the underlying `byte[]` type. Adds private static `GetVectorFieldType(byte vectorElementType)` helper in `SqlDataReader`, switching on `MetaType.SqlVectorElementType`. 2 files, +59/-3 |

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

Defined in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlDbTypeExtensions.cs`:

```csharp
#if NET10_0_OR_GREATER
public const SqlDbType Vector = SqlDbType.Vector;   // Uses BCL value on .NET 10+
#else
public const SqlDbType Vector = (SqlDbType)36;      // Cast for older TFMs (net462, net8.0, net9.0)
#endif
```

## Backward Compatibility (varchar(max) path)

When the driver or SQL Server doesn't support the native vector TDS type, vector data can be exchanged as `varchar(max)` using JSON serialization (e.g., `"[1.1, 2.2, 3.3]"`).

### How it works
1. Client serializes `float[]` to JSON string
2. Uses `SqlDbType.VarChar` with `Size = -1` (varchar(max))
3. SQL Server implicitly converts JSON text to/from vector column
4. Client reads back as `GetString()` and deserializes via `JsonSerializer`

### Test Coverage: `VectorTypeBackwardCompatibilityTests.cs`

This test suite validates the varchar(max) path with these test methods (all gated on `IsSqlVectorSupported`):

| Test Method | Sync/Async | What it tests |
|-------------|-----------|---------------|
| `TestVectorDataInsertionAsVarchar` | Sync | 4 SqlParameter construction patterns × (non-null + null) — also covers reads via `GetString`/`GetSqlString`/`GetFieldValue<string>` |
| `TestVectorDataInsertionAsVarcharAsync` | Async | Same 4 patterns × (non-null + null) async |
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

### Test Coverage: `Float16VectorTypeBackwardCompatibilityTests.cs` (new)

Mirrors `VectorTypeBackwardCompatibilityTests` but targets `vector(3, float16)` columns via the varchar(max) JSON path. All tests gated on `IsSqlVectorFloat16Supported` (which implies `IsSqlVectorSupported`).

| Test Method | Sync/Async | What it tests |
|-------------|-----------|---------------|
| `TestVectorDataInsertionAsVarchar` | Sync | 4 SqlParameter patterns × (non-null + null) — reads via `GetString`/`GetSqlString`/`GetFieldValue<string>` |
| `TestVectorDataInsertionAsVarcharAsync` | Async | Same 4 patterns × (non-null + null) async |
| `TestStoredProcParamsForVectorAsVarchar` | Sync | SP with VARCHAR(MAX) input/output params |
| `TestStoredProcParamsForVectorAsVarcharAsync` | Async | Same SP test async |
| `TestSqlBulkCopyForVectorAsVarchar` | Sync | BulkCopy from varchar(max) source to vector(float16) dest |
| `TestSqlBulkCopyForVectorAsVarcharAsync` | Async | Same bulk copy async |
| `TestInsertVectorsAsVarcharWithPrepare` | Sync | Prepared statement with varchar vector param |

#### Key Differences from Float32 Backward Compat Tests

- **Column DDL**: `vector(3, float16)` instead of `vector(3)` (implicit float32)
- **Test data**: Uses `{ 1.0f, 2.0f, 3.0f }` — values exactly representable in IEEE-754 binary16 (no precision loss on round-trip)
- **Predicate**: `IsSqlVectorFloat16Supported` instead of `IsSqlVectorSupported`
- **Object names**: Prefixed with `VectorF16` to avoid collision (`VectorF16TestTable`, `VectorF16BulkCopyTestTable`, `VectorF16AsVarcharSp`)
- **Test data class**: `Float16VarcharVectorTestData` (mirrors `VarcharVectorTestData`)

### Backward Compat Test Base Class: `VectorBackwardCompatTestBase`

Both `VectorTypeBackwardCompatibilityTests` (float32) and `Float16VectorTypeBackwardCompatibilityTests` (float16) derive from `VectorBackwardCompatTestBase`, which provides all shared test logic and RAII-based DB resource management.

#### Design
- **Constructor** accepts `(ITestOutputHelper output, string columnDefinition, string namePrefix)` — e.g., `"vector(3)"` / `"Vector"` for float32, `"vector(3, float16)"` / `"VectorF16"` for float16.
- **RAII cleanup**: Uses `Table` and `StoredProcedure` RAII classes from `Microsoft.Data.SqlClient.TestCommon.Fixtures.DatabaseObjects` (PR #4050). Resources are created in constructor and automatically dropped via `Dispose()` in reverse order.
- **Abstract method**: `GetPrepareTestValues(int i)` — returns the per-iteration test values for `PreparedInsertRoundTrip`. Float32 uses `{ i+0.1f, i+0.2f, i+0.3f }` (fractional), float16 uses `{ i+1, i+2, i+3 }` (whole numbers representable in binary16).
- **Protected test methods**: `InsertAndValidateAsVarchar`, `InsertAndValidateAsVarcharAsync`, `StoredProcRoundTrip`, `StoredProcRoundTripAsync`, `BulkCopyRoundTrip`, `BulkCopyRoundTripAsync`, `PreparedInsertRoundTrip`.
- Derived classes are thin wrappers: constructor, one override, and test methods that delegate to the base class.

## Native Vector Support Test Suite: `NativeVectorFloat32Tests.cs`

Uses `[ConditionalTheory]`/`[ConditionalFact]` + `[MemberData]` for parameterized tests, all gated on `DataTestUtility.IsSqlVectorSupported`.

### Test methods

| Test Method | Kind | Coverage |
|-------------|------|----------|
| `TestSqlVectorFloat32ParameterInsertionAndReads` | Theory | Insert + read using 4 SqlParameter patterns × 4 value types |
| `TestSqlVectorFloat32ParameterInsertionAndReadsAsync` | Theory (async) | Async variant |
| `TestStoredProcParamsForVectorFloat32` | Theory | Stored procedure input/output params |
| `TestStoredProcParamsForVectorFloat32Async` | Theory (async) | Async SP variant |
| `TestBulkCopyFromSqlTable` | Theory | `SqlBulkCopy` from a vector source table |
| `TestBulkCopyFromSqlTableAsync` | Theory (async) | Async bulk copy |
| `TestGetFieldTypeReturnsSqlVectorForVectorColumn` | Fact | Added in PR #4105 — verifies `GetFieldType`/`GetProviderSpecificFieldType`/`GetValue`/`GetFieldValue<SqlVector<float>>` |
| `TestInsertVectorsFloat32WithPrepare` | Fact | Prepared statement with native vector param |

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

All implementation lives in the unified project under `src/Microsoft.Data.SqlClient/src/`. (PR #3433 originally modified the legacy `netcore/src/` and `netfx/src/` trees; subsequent unification consolidated everything here.)

| File | Purpose |
|------|---------|
| `src/Microsoft/Data/SqlTypes/SqlVector.cs` | `SqlVector<T>` readonly struct definition |
| `src/Microsoft/Data/SqlClient/ISqlVector.cs` | Internal `ISqlVector` interface (`Length`, `ElementType`, `ElementSize`, `VectorPayload`, `Size`) |
| `src/Microsoft/Data/SqlClient/SqlBuffer.cs` | Vector storage in result buffers (`StorageType.Vector`, `_vectorInfo`) |
| `src/Microsoft/Data/SqlClient/SqlEnums.cs` | `MetaType` for vector, `MetaType.SqlVectorElementType` enum (`Float32 = 0x00`) |
| `src/Microsoft/Data/SqlClient/TdsEnums.cs` | TDS constants: `SQLVECTOR = 0xF5`, `FEATUREEXT_VECTORSUPPORT = 0x0E`, `VECTOR_HEADER_SIZE = 8`, `MAX_SUPPORTED_VECTOR_VERSION = 0x01`, `FeatureExtension.VectorSupport = 1 << (FEATUREEXT_VECTORSUPPORT - 1)` |
| `src/Microsoft/Data/SqlDbTypeExtensions.cs` | `SqlDbTypeExtensions.Vector` |
| `src/Microsoft/Data/SqlClient/TdsParser.cs` | TDS read/write for vector data |
| `src/Microsoft/Data/SqlClient/SqlDataReader.cs` | `GetSqlVector<T>()`, `GetFieldType`/`GetProviderSpecificFieldType` (PR #4105), `GetVectorFieldType` private helper |
| `src/Microsoft/Data/SqlClient/SqlCommand.cs` | `BuildParamList` vector handling |
| `src/Microsoft/Data/SqlClient/Connection/SqlConnectionInternal.cs` | Feature negotiation (handles `FEATUREEXT_VECTORSUPPORT` ack; rejects versions `0` or `> MAX_SUPPORTED_VECTOR_VERSION`) |
| `netcore/ref/Microsoft.Data.SqlClient.cs` | Public API surface for .NET (still active) |
| `netfx/ref/Microsoft.Data.SqlClient.cs` | Public API surface for .NET Framework (still active) |
| `doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml` | Public XML doc snippets (`SqlVector`, `ctor1`, `CreateNull`, `IsNull`, `Null`, `Length`, `Memory`) |
| `doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml` | Doc snippets for `SqlDbTypeExtensions.Vector` |

## Test Files

| File | Purpose |
|------|---------|
| `tests/ManualTests/SQL/VectorTest/NativeVectorFloat32Tests.cs` | Native vector type integration tests |
| `tests/ManualTests/SQL/VectorTest/VectorBackwardCompatTestBase.cs` | Abstract base class for backward compat tests (shared logic for float32/float16) |
| `tests/ManualTests/SQL/VectorTest/VectorTypeBackwardCompatibilityTests.cs` | varchar(max) backward compat tests (float32) — thin wrapper over `VectorBackwardCompatTestBase` |
| `tests/ManualTests/SQL/VectorTest/Float16VectorTypeBackwardCompatibilityTests.cs` | varchar(max) backward compat tests (float16) — thin wrapper over `VectorBackwardCompatTestBase` |
| `tests/ManualTests/SQL/VectorTest/VectorAPIValidationTest.cs` | Ref assembly API validation (no DB): `ValidateVectorSqlDbType`, `TestSqlVectorCreationAPIWithFloatArr`, `TestSqlVectorCreationAPIWithROM`, `TestSqlVectorCreationAPICreateNull`, `TestIsNullProperty`, `TestNullProperty`, `TestLengthProperty`, `TestMemoryProperty` |
| `tests/UnitTests/Microsoft/Data/SqlTypes/SqlVectorTest.cs` | Unit tests for `SqlVector<T>` (originally `tests/FunctionalTests/SqlVectorFloat32Test.cs` in PR #3433, later renamed and moved to UnitTests) |
| `doc/samples/SqlVectorExample.cs` | Public usage sample |

## DataTestUtility Predicates

| Predicate | Cache Field | Detection Method |
|-----------|-------------|------------------|
| `IsSqlVectorSupported` | `s_isVectorSupported` | `IsTypePresent("vector")` — checks `SYS.TYPES` for the `vector` type |
| `IsSqlVectorFloat16Supported` | `s_isVectorFloat16Supported` | Implies `IsSqlVectorSupported`. Probes with `ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON; DECLARE @v AS VECTOR(5, float16) = '[1.0, 1.0, 1.0, 1.0, 1.0]'; SELECT @v;`. Reads result via `GetString(0)`, deserializes JSON to `float[]`, and verifies it matches `{ 1.0f, 1.0f, 1.0f, 1.0f, 1.0f }`. Catches `SqlException` (server errors) and `JsonException` (unexpected payload). **New on this branch**. |

> **Design note**: The float16 probe uses values exactly representable in binary16 (`1.0f`) so the round-trip comparison is bit-exact. The probe does **not** use `ExecuteScalar()` because we want to validate the full `GetString` read path. The catch is narrowed to `SqlException` + `JsonException` rather than a blanket `catch (Exception)` so genuine driver bugs surface instead of being silently swallowed.
