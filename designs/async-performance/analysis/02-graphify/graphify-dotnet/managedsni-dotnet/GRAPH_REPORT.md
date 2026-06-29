# Graph Report - ManagedSni  (2026-06-29)

## Summary
- 310 nodes · 551 edges · 31 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS

## God Nodes (most connected - your core abstractions)
1. `Microsoft.Data.SqlClient.ManagedSni` - 2 edges
2. `LocalDB` - 2 edges
3. `Microsoft.Data.SqlClient.ManagedSni` - 2 edges
4. `Microsoft.Data.SqlClient.ManagedSni` - 2 edges
5. `Microsoft.Data.SqlClient.ManagedSni` - 2 edges
6. `SniProxy` - 2 edges
7. `Microsoft.Data.SqlClient.ManagedSni` - 2 edges
8. `SsrpResult` - 2 edges
9. `History` - 2 edges
10. `Microsoft.Data.SqlClient.ManagedSni` - 2 edges

## Surprising Connections (you probably didn't know these)
- None detected - all connections are within the same source files.

## Communities

### Community 0 - "Entity (Community 0)"
Cohesion: 0.08
Nodes (24): SniTcpHandle.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, KillConnection(), is(), GetHostAddressesSortedByPreference(), SniTcpHandle(), SetAsyncCallbacks(), SendAsync() (+16 more)

### Community 1 - "Entity (Community 1)"
Cohesion: 0.18
Nodes (22): SniPacket.netcore.cs, SniPacket.netcore.cs, InvokeAsyncIOCompletionCallback(), GetData(), History, if(), GetHeaderBuffer(), SniPacket() (+14 more)

### Community 2 - "Entity (Community 2)"
Cohesion: 0.10
Nodes (21): SniMarsHandle.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, EnableSsl(), InternalSendAsync(), KillConnection(), HandleSendComplete(), HandleReceiveError(), HandleReceiveComplete() (+13 more)

### Community 3 - "Entity (Community 3)"
Cohesion: 0.20
Nodes (19): SsrpClient.netcore.cs, SsrpClient.netcore.cs, SsrpResult, SplitIPv4AndIPv6(), SendUDPRequest(), using(), switch(), SocketException() (+11 more)

### Community 4 - "Entity (Community 4)"
Cohesion: 0.22
Nodes (18): SniHandle.netcore.cs, SniHandle.netcore.cs, EnableSsl(), AuthenticateAsClient(), CheckConnection(), DisableSsl(), AuthenticateAsClientAsync(), Dispose() (+10 more)

### Community 5 - "Entity (Community 5)"
Cohesion: 0.24
Nodes (16): SslOverTdsStream.netcore.cs, SslOverTdsStream.netcore.cs, SetupPreLoginPacketHeader(), if(), ReadAsync(), FinishHandshake(), Read(), Seek() (+8 more)

### Community 8 - "Entity (Community 8)"
Cohesion: 0.12
Nodes (16): SniProxy.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, GetLocalDBDataSource(), InferConnectionDetails(), GetLocalDBInstance(), IsLocalHost(), SniTcpHandle(), SniNpHandle() (+8 more)

### Community 6 - "Entity (Community 6)"
Cohesion: 0.12
Nodes (16): SniNpHandle.netcore.cs, Dispose(), DisableSsl(), EnableSsl(), AuthenticateAsClient(), CheckConnection(), KillConnection(), Send() (+8 more)

### Community 7 - "Entity (Community 7)"
Cohesion: 0.12
Nodes (16): SniMarsConnection.netcore.cs, EnableSsl(), catch(), DisableSsl(), CheckConnection(), CreateMarsSession(), Microsoft.Data.SqlClient.ManagedSni, HandleReceiveComplete() (+8 more)

### Community 9 - "Entity (Community 9)"
Cohesion: 0.14
Nodes (14): SniTcpHandle.netcore.cs, ReportErrorAndReleasePacket(), Win32Exception(), when(), using(), SetKeepAliveValues(), ReturnPacket(), nameof() (+6 more)

### Community 10 - "Entity (Community 10)"
Cohesion: 0.15
Nodes (13): LocalDB.netcore.windows.cs, GetUserInstanceDllPath(), GetLocalDBConnectionString(), GetConnectionString(), LoadUserInstanceDll(), GetProcAddress(), foreach(), Microsoft.Data.SqlClient.ManagedSni (+5 more)

### Community 11 - "Entity (Community 11)"
Cohesion: 0.18
Nodes (11): SniMarsHandle.netcore.cs, using(), SendAckIfNecessary(), SetupSMUXHeader(), HandleAck(), SendPendingPackets(), SendControlPacket(), if() (+3 more)

### Community 12 - "Entity (Community 12)"
Cohesion: 0.20
Nodes (10): SniProxy.netcore.cs, ReportSNIError(), catch(), GetSqlServerSPNs(), DataSource(), PopulateProtocol(), new(), InferLocalServerName() (+2 more)

### Community 13 - "Entity (Community 13)"
Cohesion: 0.25
Nodes (8): SniMarsConnection.netcore.cs, using(), ReturnPacket(), SniMarsConnection(), HandleReceiveError(), if(), lock(), while()

### Community 18 - "Entity (Community 18)"
Cohesion: 0.29
Nodes (7): SniNpHandle.netcore.cs, if(), ReportErrorAndReleasePacket(), lock(), ReturnPacket(), using(), catch()

### Community 16 - "Entity (Community 16)"
Cohesion: 0.52
Nodes (7): ConcurrentQueueSemaphore.netcore.cs, ConcurrentQueueSemaphore.netcore.cs, WaitAsync(), Microsoft.Data.SqlClient.ManagedSni, if(), ConcurrentQueueSemaphore(), Release()

### Community 17 - "Entity (Community 17)"
Cohesion: 0.52
Nodes (7): SniSslStream.netcore.cs, SniSslStream.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, catch(), SniSslStream(), WriteAsync(), ReadAsync()

### Community 15 - "Entity (Community 15)"
Cohesion: 0.52
Nodes (7): SniNetworkStream.netcore.cs, SniNetworkStream.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, catch(), ReadAsync(), WriteAsync(), SniNetworkStream()

### Community 14 - "Entity (Community 14)"
Cohesion: 0.29
Nodes (7): SniCommon.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, catch(), TimeoutException(), ValidateSslServerCertificate(), foreach(), SniCommon

### Community 19 - "Entity (Community 19)"
Cohesion: 0.60
Nodes (6): LocalDB.netcore.unix.cs, LocalDB.netcore.unix.cs, LocalDB, GetLocalDBConnectionString(), PlatformNotSupportedException(), Microsoft.Data.SqlClient.ManagedSni

### Community 23 - "Entity (Community 23)"
Cohesion: 0.70
Nodes (5): SniError.netcore.cs, SniError.netcore.cs, SniError(), Microsoft.Data.SqlClient.ManagedSni, if()

### Community 21 - "Entity (Community 21)"
Cohesion: 0.70
Nodes (5): SniSmuxHeader.netcore.cs, SniSmuxHeader.netcore.cs, Write(), Read(), Microsoft.Data.SqlClient.ManagedSni

### Community 22 - "Entity (Community 22)"
Cohesion: 0.40
Nodes (5): SniCommon.netcore.cs, if(), GetDnsIpAddresses(), using(), ReportSNIError()

### Community 20 - "Entity (Community 20)"
Cohesion: 0.40
Nodes (5): SniPhysicalHandle.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, SniPhysicalHandle(), GetStackParts(), RentPacket()

### Community 27 - "Entity (Community 27)"
Cohesion: 0.50
Nodes (4): LocalDB.netcore.windows.cs, switch(), using(), if()

### Community 25 - "Entity (Community 25)"
Cohesion: 0.50
Nodes (4): SniPhysicalHandle.netcore.cs, for(), if(), ReturnPacket()

### Community 24 - "Entity (Community 24)"
Cohesion: 0.83
Nodes (4): ResolvedServerSpn.cs, ResolvedServerSpn.cs, ResolvedServerSpn(), Microsoft.Data.SqlClient.ManagedSni

### Community 26 - "Entity (Community 26)"
Cohesion: 0.83
Nodes (4): SniAsyncCallback.netcore.cs, SniAsyncCallback.netcore.cs, SniAsyncCallback(), Microsoft.Data.SqlClient.ManagedSni

### Community 28 - "Entity (Community 28)"
Cohesion: 1.00
Nodes (3): SniProviders.netcore.cs, SniProviders.netcore.cs, Microsoft.Data.SqlClient.ManagedSni

### Community 29 - "Entity (Community 29)"
Cohesion: 1.00
Nodes (3): SniSmuxFlags.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, SniSmuxFlags.netcore.cs

### Community 30 - "Entity (Community 30)"
Cohesion: 1.00
Nodes (3): SniLoadHandle.netcore.cs, SniLoadHandle.netcore.cs, Microsoft.Data.SqlClient.ManagedSni

## Suggested Questions
_Not enough signal to generate questions. The graph has no ambiguous edges, no bridge nodes, and all communities are well-connected._

