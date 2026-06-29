# Async Roslyn Analyzer — raw output

- Generated (UTC): 2026-06-29 17:51:06Z
- Files scanned: 344
- Configurations: net8.0-unix, net9.0-unix, net8.0-windows, net9.0-windows, net462-windows
- Target methods: TryReadNetworkPacket, ReadSniSyncOverAsync, TryProcessDone, ConsumePreLoginHandshake, TryConnectParallel, AuthenticateAsClient, GetHostAddresses

## Counts by analyzer

| Analyzer | Findings |
| --- | --- |
| allocation | 106 |
| blocking-sync | 77 |
| call-site | 19 |
| missing-configureawait | 1 |
| sync-over-async | 35 |

## allocation

| File | Line | Container | Async | Detail | Configs |
| --- | --- | --- | --- | --- | --- |
| Microsoft/Data/SqlClient/ManagedSni/ConcurrentQueueSemaphore.netcore.cs | 37 | ConcurrentQueueSemaphore.WaitAsync |  | new TaskCompletionSource | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 50 | SniMarsConnection..ctor |  | new byte[] | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniProxy.netcore.cs | 67 | SniProxy.CreateConnectionHandle |  | new byte[] | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 98 | SsrpClient.CreateInstanceInfoRequest |  | new byte[] | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 150 | SsrpClient.CreateDacPortInfoRequest |  | new byte[] | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 405 | SsrpClient.SendBroadcastUDPRequest |  | new byte[] | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 1500 | ValueUtilsSmi.SetCompatibleValue |  | new char[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 2250 | ValueUtilsSmi.SetBytes_FromRecord |  | new byte[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 2283 | ValueUtilsSmi.SetBytes_FromReader |  | new byte[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 2352 | ValueUtilsSmi.SetChars_FromRecord |  | new char[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 2415 | ValueUtilsSmi.SetChars_FromReader |  | new char[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 2831 | ValueUtilsSmi.GetByteArray_Unchecked |  | new byte[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 2864 | ValueUtilsSmi.GetCharArray_Unchecked |  | new char[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 3031 | ValueUtilsSmi.SetStream_Unchecked |  | new byte[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 3061 | ValueUtilsSmi.SetTextReader_Unchecked |  | new char[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 3238 | ValueUtilsSmi.SetSqlBytes_Unchecked |  | new byte[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 3284 | ValueUtilsSmi.SetSqlChars_Unchecked |  | new char[] | all |
| Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs | 3648 | ValueUtilsSmi.CopyIntoNewSmiScratchStream |  | new byte[] | all |
| Microsoft/Data/SqlClient/SqlCommand.Encryption.cs | 929 | SqlCommand.ReadDescribeEncryptionParameterResultsAttestation |  | new byte[] | all |
| Microsoft/Data/SqlClient/SqlCommand.Encryption.cs | 989 | SqlCommand.ReadDescribeEncryptionParameterResultsKeys |  | new byte[] | all |
| Microsoft/Data/SqlClient/SqlCommand.Encryption.cs | 999 | SqlCommand.ReadDescribeEncryptionParameterResultsKeys |  | new byte[] | all |
| Microsoft/Data/SqlClient/SqlCommand.Encryption.cs | 1054 | SqlCommand.ReadDescribeEncryptionParameterResultsKeys |  | new byte[] | all |
| Microsoft/Data/SqlClient/SqlCommand.NonQuery.cs | 186 | SqlCommand.BeginExecuteNonQueryInternal |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.NonQuery.cs | 187 | SqlCommand.BeginExecuteNonQueryInternal |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.NonQuery.cs | 647 | SqlCommand.InternalExecuteNonQueryAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.NonQuery.cs | 756 | SqlCommand.RunExecuteNonQueryTds |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 252 | SqlCommand.BeginExecuteReaderInternal |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 253 | SqlCommand.BeginExecuteReaderInternal |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 955 | SqlCommand.InternalExecuteReaderAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 1261 | SqlCommand.RunExecuteReaderTds |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 1716 | SqlCommand.RunExecuteReaderTdsWithTransparentParameterEncryption |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Scalar.cs | 93 | SqlCommand.ExecuteScalarBatchAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Scalar.cs | 242 | SqlCommand.ExecuteScalarAsyncInternal |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Xml.cs | 211 | SqlCommand.BeginExecuteXmlReaderInternal |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Xml.cs | 212 | SqlCommand.BeginExecuteXmlReaderInternal |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommand.Xml.cs | 466 | SqlCommand.InternalExecuteXmlReaderAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlCommandSet.cs | 147 | SqlCommandSet.Append |  | new byte[] | all |
| Microsoft/Data/SqlClient/SqlCommandSet.cs | 164 | SqlCommandSet.Append |  | new char[] | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 4491 | SqlDataReader.NextResultAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 4586 | SqlDataReader.GetBytesAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 4759 | SqlDataReader.GetBytesAsyncReadDataStage |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 4910 | SqlDataReader.ReadAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 5065 | SqlDataReader.IsDBNullAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 5199 | SqlDataReader.GetFieldValueAsync |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 806 | TdsParser.SendPreLoginHandshake |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 1046 | TdsParser.ConsumePreLoginHandshake |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 1995 | TdsParser.SerializeShort |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 2126 | TdsParser.SerializeInt |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 2175 | TdsParser.SerializeFloat |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 2203 | TdsParser.SerializeLong |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 2263 | TdsParser.SerializePartialLong |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 2326 | TdsParser.SerializeDouble |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 3351 | TdsParser.TryProcessEnvChange |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 3853 | TdsParser.TryProcessFeatureExtAck |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 4238 | TdsParser.TryProcessSessionState |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 4275 | TdsParser.TryProcessSessionState |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 4467 | TdsParser.TryProcessFedAuthInfo |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 5377 | TdsParser.TryReadCipherInfoEntry |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 6826 | TdsParser.DeserializeUnencryptedValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 7383 | TdsParser.TryReadSqlValueInternal |  | new byte[] | net462-windows |
| Microsoft/Data/SqlClient/TdsParser.cs | 8084 | TdsParser.SerializeCurrency |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8182 | TdsParser.SerializeDateTime2 |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8213 | TdsParser.SerializeDateTimeOffset |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8316 | TdsParser.SerializeSqlDecimal |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8389 | TdsParser.SerializeDecimal |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8517 | TdsParser.SerializeCharArray |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8538 | TdsParser.WriteCharArray |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8549 | TdsParser.SerializeString |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 8570 | TdsParser.WriteString |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 9708 | TdsParser.GetDTCAddress |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 10269 | TdsParser.TdsExecuteRPC |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 10325 | TdsParser.TdsExecuteRPC |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13341 | TdsParser.SerializeUnencryptedValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13391 | TdsParser.SerializeUnencryptedValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13410 | TdsParser.SerializeUnencryptedValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13438 | TdsParser.SerializeUnencryptedValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13457 | TdsParser.SerializeUnencryptedValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13539 | TdsParser.SerializeUnencryptedSqlValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13643 | TdsParser.SerializeUnencryptedSqlValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13662 | TdsParser.SerializeUnencryptedSqlValue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13935 | TdsParser.TryReadPlpUnicodeChars |  | new char[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 13984 | TdsParser.TryReadPlpUnicodeChars |  | new char[] | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 14182 | TdsParser.ReadPlpAnsiChars |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserSafeHandles.windows.cs | 164 | SNIHandle..ctor |  | new byte[] | net462-windows net8.0-windows net9.0-windows |
| Microsoft/Data/SqlClient/TdsParserStateObject.Multiplexer.cs | 320 | TdsParserStateObject.MultiplexPackets |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.Multiplexer.cs | 432 | TdsParserStateObject.MultiplexPackets |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.Multiplexer.cs | 450 | TdsParserStateObject.MultiplexPackets |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 224 | TdsParserStateObject |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 236 | TdsParserStateObject |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 768 | TdsParserStateObject.NullBitmap.TryInitialize |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1395 | TdsParserStateObject.TrySetBufferSecureStrings |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1424 | TdsParserStateObject.NewBuffer |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1558 | TdsParserStateObject.SetPacketSize |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1572 | TdsParserStateObject.SetPacketSize |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1726 | TdsParserStateObject.TryReadByteArrayWithContinue |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2186 | TdsParserStateObject.TryReadStringWithEncoding |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2395 | TdsParserStateObject.TryReadPlpBytes |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2419 | TdsParserStateObject.TryReadPlpBytes |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2428 | TdsParserStateObject.TryReadPlpBytes |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3108 | TdsParserStateObject.SNIWritePacket |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3265 | TdsParserStateObject.WaitForAccumulatedWrites |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3493 | TdsParserStateObject.TryReadNetworkPacket |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 4298 | TdsParserStateObject.WriteBytes |  | new TaskCompletionSource | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 4304 | TdsParserStateObject.WriteBytes |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStaticMethods.cs | 103 | TdsParserStaticMethods.ObfuscatePassword |  | new byte[] | all |
| Microsoft/Data/SqlClient/TdsParserStaticMethods.cs | 210 | TdsParserStaticMethods.GetNetworkPhysicalAddressForTdsLoginOnly |  | new byte[] | all |

## blocking-sync

| File | Line | Container | Async | Detail | Configs |
| --- | --- | --- | --- | --- | --- |
| Microsoft/Data/SqlClient/ManagedSni/ConcurrentQueueSemaphore.netcore.cs | 31 | ConcurrentQueueSemaphore.WaitAsync |  | _semaphore.Wait() (blocking) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/LocalDB.netcore.windows.cs | 123 | LocalDB.LoadUserInstanceDll |  | lock(this) | net8.0-windows net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 62 | SniMarsConnection.CreateMarsSession |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 101 | SniMarsConnection.Send |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 117 | SniMarsConnection.SendAsync |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 142 | SniMarsConnection.ReceiveAsync |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 161 | SniMarsConnection.CheckConnection |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 217 | SniMarsConnection.HandleReceiveComplete |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 227 | SniMarsConnection.HandleReceiveComplete |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs | 342 | SniMarsConnection.HandleReceiveComplete |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 107 | SniMarsHandle.SendControlPacket |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 166 | SniMarsHandle.Send |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 177 | SniMarsHandle.Send |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 185 | SniMarsHandle.Send |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 204 | SniMarsHandle.InternalSendAsync |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 232 | SniMarsHandle.SendPendingPackets |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 276 | SniMarsHandle.SendAsync |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 297 | SniMarsHandle.ReceiveAsync |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 326 | SniMarsHandle.ReceiveAsync |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 348 | SniMarsHandle.HandleReceiveError |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 368 | SniMarsHandle.HandleSendComplete |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 389 | SniMarsHandle.HandleAck |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 410 | SniMarsHandle.HandleReceiveComplete |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 418 | SniMarsHandle.HandleReceiveComplete |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 438 | SniMarsHandle.HandleReceiveComplete |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 455 | SniMarsHandle.SendAckIfNecessary |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 484 | SniMarsHandle.Receive |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 511 | SniMarsHandle.Receive |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 161 | SniNpHandle.Dispose |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 192 | SniNpHandle.Receive |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 272 | SniNpHandle.Send |  | Monitor.TryEnter | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 276 | SniNpHandle.Send |  | Monitor.Enter | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 283 | SniNpHandle.Send |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 307 | SniNpHandle.Send |  | Monitor.Exit | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 54 | SniTcpHandle.Dispose |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 824 | SniTcpHandle.Send |  | Monitor.TryEnter | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 828 | SniTcpHandle.Send |  | Monitor.Enter | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 835 | SniTcpHandle.Send |  | lock | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 864 | SniTcpHandle.Send |  | Monitor.Exit | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 883 | SniTcpHandle.Receive |  | lock(this) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/SqlCommand.NonQuery.cs | 368 | SqlCommand.EndExecuteNonQueryAsync |  | lock | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 677 | SqlCommand.EndExecuteReaderAsync |  | lock | all |
| Microsoft/Data/SqlClient/SqlCommand.Xml.cs | 401 | SqlCommand.EndExecuteXmlReaderAsync |  | lock | all |
| Microsoft/Data/SqlClient/SqlCommand.cs | 1106 | SqlCommand.Cancel |  | lock | all |
| Microsoft/Data/SqlClient/SqlCommand.cs | 2455 | SqlCommand.CreateLocalCompletionTask |  | lock | all |
| Microsoft/Data/SqlClient/SqlCommand.cs | 3095 | SqlCommand.WaitForAsyncResults |  | .WaitOne() | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 881 | SqlDataReader.Close |  | .WaitOne() | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 887 | SqlDataReader.Close |  | .WaitOne() | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 898 | SqlDataReader.Close |  | lock | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 917 | SqlDataReader.Close |  | lock | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 5561 | SqlDataReader.ContinueAsyncCall |  | lock | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 5718 | SqlDataReader.CleanupAfterAsyncInvocation |  | lock | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 1900 | TdsParser.CheckResetConnection |  | .WaitOne() | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 4234 | TdsParser.TryProcessSessionState |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserSessionPool.cs | 66 | TdsParserSessionPool.Deactivate |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserSessionPool.cs | 96 | TdsParserSessionPool.Dispose |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserSessionPool.cs | 135 | TdsParserSessionPool.GetSession |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserSessionPool.cs | 172 | TdsParserSessionPool.PutSession |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 852 | TdsParserStateObject.Cancel |  | Monitor.TryEnter | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 898 | TdsParserStateObject.Cancel |  | Monitor.Exit | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 908 | TdsParserStateObject.ResetCancelAndProcessAttention |  | lock(this) | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1252 | TdsParserStateObject.ExecuteFlush |  | lock(this) | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2648 | TdsParserStateObject.AddError |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2673 | TdsParserStateObject.get |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2695 | TdsParserStateObject.AddWarning |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2718 | TdsParserStateObject.get |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2734 | TdsParserStateObject.get |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2753 | TdsParserStateObject.get |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2774 | TdsParserStateObject.GetFullErrorAndWarningCollection |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2811 | TdsParserStateObject.StoreErrorAndWarningForAttention |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 2830 | TdsParserStateObject.RestoreErrorAndWarningAfterAttention |  | lock | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3803 | TdsParserStateObject.OnTimeoutCore |  | lock(this) | all |
| Microsoft/Data/SqlClient/TdsParserStateObjectNative.windows.cs | 233 | TdsParserStateObjectNative.RemovePacketFromPendingList |  | lock | net462-windows net8.0-windows net9.0-windows |
| Microsoft/Data/SqlClient/TdsParserStateObjectNative.windows.cs | 349 | TdsParserStateObjectNative.AddPacketToPendingList |  | lock | net462-windows net8.0-windows net9.0-windows |
| Microsoft/Data/SqlClient/TdsParserStateObjectNative.windows.cs | 373 | TdsParserStateObjectNative.GetResetWritePacket |  | lock | net462-windows net8.0-windows net9.0-windows |
| Microsoft/Data/SqlClient/TdsParserStateObjectNative.windows.cs | 388 | TdsParserStateObjectNative.ClearAllWritePackets |  | lock | net462-windows net8.0-windows net9.0-windows |
| Microsoft/Data/SqlClient/TdsParserStateObjectNative.windows.cs | 466 | TdsParserStateObjectNative.DisposePacketCache |  | lock | net462-windows net8.0-windows net9.0-windows |

## call-site

| File | Line | Container | Async | Detail | Configs |
| --- | --- | --- | --- | --- | --- |
| Microsoft/Data/SqlClient/ManagedSni/SniCommon.netcore.cs | 181 | SniCommon.GetDnsIpAddresses |  | GetHostAddresses | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 338 | SniNpHandle.EnableSsl |  | AuthenticateAsClient | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs | 343 | SniNpHandle.EnableSsl |  | AuthenticateAsClient | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 167 | SniTcpHandle..ctor |  | TryConnectParallel | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 203 | SniTcpHandle..ctor |  | TryConnectParallel | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 222 | SniTcpHandle..ctor |  | TryConnectParallel | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 330 | SniTcpHandle.GetHostAddressesSortedByPreference |  | GetHostAddresses | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 735 | SniTcpHandle.EnableSsl |  | AuthenticateAsClient | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs | 739 | SniTcpHandle.EnableSsl |  | AuthenticateAsClient | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/TdsParser.cs | 580 | TdsParser.Connect |  | ConsumePreLoginHandshake | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 634 | TdsParser.Connect |  | ConsumePreLoginHandshake | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 1023 | TdsParser.ConsumePreLoginHandshake |  | TryReadNetworkPacket | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 2627 | TdsParser.TryRun |  | TryProcessDone | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1330 | TdsParserStateObject.TryProcessHeader |  | TryReadNetworkPacket | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1455 | TdsParserStateObject.TryPrepareBuffer |  | TryReadNetworkPacket | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1464 | TdsParserStateObject.TryPrepareBuffer |  | TryReadNetworkPacket | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 1482 | TdsParserStateObject.TryPrepareBuffer |  | TryReadNetworkPacket | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3480 | TdsParserStateObject.TryReadNetworkPacket |  | ReadSniSyncOverAsync | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3488 | TdsParserStateObject.TryReadNetworkPacket |  | ReadSniSyncOverAsync | all |

## missing-configureawait

| File | Line | Container | Async | Detail | Configs |
| --- | --- | --- | --- | --- | --- |
| Microsoft/Data/SqlClient/ManagedSni/SniHandle.netcore.cs | 34 | SniHandle.AuthenticateAsClientAsync | Y | await without ConfigureAwait(false) | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |

## sync-over-async

| File | Line | Container | Async | Detail | Configs |
| --- | --- | --- | --- | --- | --- |
| Microsoft/Data/SqlClient/ManagedSni/SniHandle.netcore.cs | 39 | SniHandle.AuthenticateAsClient |  | .GetAwaiter().GetResult() | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 175 | SniMarsHandle.Send |  | .Wait() | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs | 522 | SniMarsHandle.Receive |  | .Wait() | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SniPacket.netcore.cs | 292 | SniPacket.ReadFromStreamAsyncContinuation |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SslOverTdsStream.netcore.cs | 191 | SslOverTdsStream.ReadAsync | Y | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SslOverTdsStream.netcore.cs | 212 | SslOverTdsStream.ReadAsync | Y | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SslOverTdsStream.netcore.cs | 240 | SslOverTdsStream.ReadAsync | Y | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SslOverTdsStream.netcore.cs | 267 | SslOverTdsStream.ReadAsync | Y | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 313 | SsrpClient.SendUDPRequest |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 316 | SsrpClient.SendUDPRequest |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 328 | SsrpClient.SendUDPRequest |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 353 | SsrpClient.SendUDPRequest |  | .Wait() | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 353 | SsrpClient.SendUDPRequest |  | .Wait() | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 356 | SsrpClient.SendUDPRequest |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 418 | SsrpClient.SendBroadcastUDPRequest |  | .Wait() | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 422 | SsrpClient.SendBroadcastUDPRequest |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 424 | SsrpClient.SendBroadcastUDPRequest |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/ManagedSni/SsrpClient.netcore.cs | 424 | SsrpClient.SendBroadcastUDPRequest |  | .Result | net8.0-unix net8.0-windows net9.0-unix net9.0-windows |
| Microsoft/Data/SqlClient/SqlCommand.NonQuery.cs | 337 | SqlCommand.CleanupAfterExecuteNonQueryAsync |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 237 | SqlCommand.ExecuteDbDataReaderAsync |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.Reader.cs | 608 | SqlCommand.CleanupExecuteReaderAsync |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.Scalar.cs | 110 | SqlCommand.ExecuteScalarBatchAsync |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.Scalar.cs | 136 | SqlCommand.ExecuteScalarBatchAsync |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.Scalar.cs | 260 | SqlCommand.ExecuteScalarAsyncInternal |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.Scalar.cs | 292 | SqlCommand.ExecuteScalarAsyncInternal |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.Xml.cs | 371 | SqlCommand.CleanupAfterExecuteXmlReaderAsync |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.cs | 2460 | SqlCommand.CreateLocalCompletionTask |  | .Result | all |
| Microsoft/Data/SqlClient/SqlCommand.cs | 2542 | SqlCommand.CreateLocalCompletionTask |  | .Result | all |
| Microsoft/Data/SqlClient/SqlDataReader.cs | 5630 | SqlDataReader.CompleteAsyncCall |  | .Result | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 1708 | TdsParser.ThrowExceptionAndWarning |  | .Wait() | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 9767 | TdsParser.TdsExecuteTransactionManagerRequest |  | .Wait() | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 10009 | TdsParser.TdsExecuteSQLBatch |  | .Wait() | all |
| Microsoft/Data/SqlClient/TdsParser.cs | 10134 | TdsParser.TdsExecuteRPC |  | .Wait() | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3082 | TdsParserStateObject.SNIWritePacket |  | .Wait() | all |
| Microsoft/Data/SqlClient/TdsParserStateObject.cs | 3358 | TdsParserStateObject.SendAttention |  | .Wait() | all |

