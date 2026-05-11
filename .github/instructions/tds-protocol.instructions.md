---
applyTo: "**/TdsParser*.cs,**/TdsEnums.cs,**/Packet*.cs,**/Session*.cs"
---
# TDS Protocol Implementation Guide

## Overview

Microsoft.Data.SqlClient implements the Tabular Data Stream (TDS) protocol for SQL Server communication. The TDS specification is publicly documented as MS-TDS in Microsoft Open Specifications.

**Specification Reference**: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-tds

## Supported TDS Versions

| Version | SQL Server | Notes |
|---------|------------|-------|
| TDS 7.4 | SQL Server 2012+ | Standard protocol version |
| TDS 8.0 | SQL Server 2022+ | Required for `Encrypt=Strict`, TLS 1.3 support |

## Key Files

### TdsEnums.cs
Defines all TDS constants, tokens, and enumerations:
- Message types (`MT_SQL`, `MT_RPC`, `MT_LOGIN7`, etc.)
- Token types (`SQLCOLMETADATA`, `SQLROW`, `SQLDONE`, etc.)
- Environment change types
- Error severity levels

### TdsParser.cs
Core protocol state machine:
- Connection establishment and login
- Packet framing and routing
- Token stream parsing
- MARS session management

### TdsParserStateObject.cs
Per-session state management:
- Send/receive buffers
- Packet handling
- Async I/O coordination
- State tracking for pending operations

### TdsParserHelperClasses.cs
Supporting data structures:
- SqlMetaData parsing
- Column metadata
- Type conversion utilities

## Packet Structure

TDS packets have an 8-byte header:

```
Offset  Size  Field
0       1     Message Type (MT_*)
1       1     Status flags (ST_EOM, ST_IGNORE, etc.)
2       2     Length (big-endian)
4       2     SPID
6       2     Packet ID (for MARS)
```

### Important Constants

```csharp
public const int HEADER_LEN = 8;
public const int DEFAULT_LOGIN_PACKET_SIZE = 4096;
public const int MAX_PACKET_SIZE = 32768;
public const int MIN_PACKET_SIZE = 512;
```

## Message Types

| Constant | Value | Description |
|----------|-------|-------------|
| MT_SQL | 1 | SQL batch |
| MT_RPC | 3 | RPC call (stored procedures) |
| MT_TOKENS | 4 | Token response stream |
| MT_ATTN | 6 | Attention/cancel signal |
| MT_BULK | 7 | Bulk load data |
| MT_LOGIN7 | 16 | TDS 7.0+ login |
| MT_SSPI | 17 | SSPI authentication |
| MT_PRELOGIN | 18 | Pre-login handshake |

## Token Parsing

Response streams consist of token sequences. Key tokens:

| Token | Value | Description |
|-------|-------|-------------|
| SQLCOLMETADATA | 0x81 | Column metadata |
| SQLROW | 0xD1 | Data row |
| SQLNBCROW | 0xD2 | Null-bitmap compressed row |
| SQLDONE | 0xFD | Batch complete |
| SQLDONEPROC | 0xFE | Stored proc complete |
| SQLDONEINPROC | 0xFF | Statement complete (in proc) |
| SQLLOGINACK | 0xAD | Login acknowledged |
| SQLENVCHANGE | 0xE3 | Environment change |
| SQLERROR | 0xAA | Error message |
| SQLINFO | 0xAB | Info message |

## Environment Changes

Track session state changes:

| Type | Value | Description |
|------|-------|-------------|
| ENV_DATABASE | 1 | Database changed |
| ENV_LANG | 2 | Language changed |
| ENV_PACKETSIZE | 4 | Packet size negotiated |
| ENV_BEGINTRAN | 8 | Transaction started |
| ENV_COMMITTRAN | 9 | Transaction committed |
| ENV_ROLLBACKTRAN | 10 | Transaction rolled back |
| ENV_ROUTING | 20 | Connection rerouting |

## Connection Flow

1. **TCP Connect**: Establish TCP connection to server
2. **Pre-login**: Exchange TDS version, encryption, instance info
3. **TLS Handshake**: If encryption required
4. **Login7**: Send login packet with credentials
5. **Login Ack**: Receive LOGINACK token
6. **Feature Negotiation**: Exchange supported features

## Error Handling

Error severity levels:

```csharp
// Severity 0-10: Informational
// Severity 11-16: User-correctable errors
public const byte MIN_ERROR_CLASS = 11;
public const byte MAX_USER_CORRECTABLE_ERROR_CLASS = 16;
// Severity 17-19: Resource errors
// Severity 20-25: Fatal errors, connection terminated
public const byte FATAL_ERROR_CLASS = 20;
```

## Best Practices

### When Modifying TDS Code

1. **Test Against Multiple SQL Server Versions**: TDS behavior can vary
2. **Verify Protocol Compliance**: Use network captures to validate
3. **Handle Token Order Variations**: Servers may vary token ordering
4. **Consider MARS**: Multiple Active Result Sets changes state management
5. **Maintain Backward Compatibility**: Older servers must still work

### Performance Considerations

- Use buffer pooling for packet buffers
- Minimize allocations in hot paths
- Prefer async I/O operations
- Be careful with ConfigureAwait(false) in async code

### Security

- Never log packet contents (may contain credentials)
- Validate all server responses
- Handle malformed packets gracefully
- Ensure proper TLS negotiation

## Feature Extensions

TDS 7.4+ supports feature extensions negotiated during login:
- Column Encryption (Always Encrypted)
- Session Recovery (Connection Resiliency)
- Federated Authentication
- UTF-8 support
- Data Classification

See `TdsEnums.FeatureExtension` for feature flags.
