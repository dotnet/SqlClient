# Graph Report - SqlClient  (2026-06-29)

## Summary
- 4955 nodes · 9127 edges · 360 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS

## God Nodes (most connected - your core abstractions)
1. `SqlNoneIntervalEnumerator` - 2 edges
2. `Microsoft.Data.SqlClient` - 2 edges
3. `Microsoft.Data.SqlClient` - 2 edges
4. `Microsoft.Data.SqlClient` - 2 edges
5. `Microsoft.Data.SqlClient` - 2 edges
6. `AzureAttestationEnclaveProvider` - 2 edges
7. `Microsoft.Data.SqlClient` - 2 edges
8. `NullBitmap` - 2 edges
9. `Microsoft.Data.SqlClient` - 2 edges
10. `Microsoft.Data.SqlClient.AlwaysEncrypted` - 2 edges

## Surprising Connections (you probably didn't know these)
- None detected - all connections are within the same source files.

## Communities

### Community 0 - "Entity (Community 0)"
Cohesion: 0.01
Nodes (191): SqlUtil.cs, CR_ReconnectTimeout(), CR_NextAttemptWillExceedQueryTimeout(), SqlDependencyEventNoDuplicate(), SqlCommandHasExistingSqlNotificationRequest(), SocketDidNotThrow(), SqlDepDefaultOptionsButNoStart(), SmallDateTimeOverflow() (+183 more)

### Community 1 - "Entity (Community 1)"
Cohesion: 0.02
Nodes (188): ValueUtilsSmi.cs, ValueUtilsSmi.cs, SetSqlMoney_Checked(), SetSqlMoney(), SetSqlInt64_Unchecked(), SetSqlInt64(), SqlBoolean(), SetTimeSpan_Unchecked() (+180 more)

### Community 3 - "Entity (Community 3)"
Cohesion: 0.01
Nodes (134): TdsParser.cs, WriteUserAgentFeatureRequest(), WriteUnterminatedValue(), WriteTceFeatureRequest(), WriteTextFeed(), WriteStreamFeed(), WriteSqlVariantDate(), WriteSQLDNSCachingFeatureRequest() (+126 more)

### Community 2 - "Entity (Community 2)"
Cohesion: 0.01
Nodes (134): SqlUtil.cs, ParameterDirectionInvalidForOptimizedBinding(), ParamEncryptionMetadataMissing(), ParamUnExpectedEncryptionMetadata(), OverflowException(), OperationCancelled(), NotSupportedEnumerationValue(), MultiSubnetFailoverWithNonTcpProtocol() (+126 more)

### Community 4 - "Entity (Community 4)"
Cohesion: 0.02
Nodes (130): TdsParserStateObject.cs, GetPacketSize(), GetFullErrorAndWarningCollection(), GetHeaderSpan(), GetErrorDetails(), GetPacketID(), DisposePacketCache(), SetConnectionBufferSize() (+122 more)

### Community 5 - "Entity (Community 5)"
Cohesion: 0.02
Nodes (115): SqlDataReader.cs, GetString(), GetSqlString(), GetSqlMoney(), GetStream(), GetSqlSingle(), GetStreamingXmlChars(), GetSqlXml() (+107 more)

### Community 6 - "Entity (Community 6)"
Cohesion: 0.02
Nodes (114): TdsParser.cs, WriteShort(), WriteParameterName(), WriteMarsHeaderData(), WriteQueryNotificationHeaderData(), WritePartialLong(), WriteParameterVarLen(), WriteRPCBatchHeaders() (+106 more)

### Community 7 - "Entity (Community 7)"
Cohesion: 0.03
Nodes (72): TdsParserStateObject.cs, while(), Timer(), TryReadPlpBytes(), TryReadByteArray(), TimeoutState(), ThrowExceptionAndWarning(), using() (+64 more)

### Community 8 - "Entity (Community 8)"
Cohesion: 0.03
Nodes (62): SqlConnection.cs, UsesClearUserIdOrPassword(), UsesActiveDirectoryMSI(), UsesActiveDirectoryManagedIdentity(), UsesAuthentication(), UsesActiveDirectoryIntegrated(), UsesActiveDirectoryInteractive(), UsesActiveDirectoryDeviceCodeFlow() (+54 more)

### Community 10 - "Entity (Community 10)"
Cohesion: 0.04
Nodes (54): SqlCommand.cs, new(), GetRPCObject(), NotifyDependency(), Microsoft.Data.SqlClient, IsActiveConnectionValid(), InvalidateEnclaveSession(), handler() (+46 more)

### Community 9 - "Entity (Community 9)"
Cohesion: 0.04
Nodes (54): SqlDataRecord.cs, GetSqlByte(), GetInt16(), GetGuid(), GetOrdinal(), GetInt64(), GetSqlBinary(), GetName() (+46 more)

### Community 11 - "Entity (Community 11)"
Cohesion: 0.08
Nodes (49): TdsParserStateObjectNative.windows.cs, TdsParserStateObjectNative.windows.cs, PostReadAsyncForMars(), NormalizeServerSpn(), Microsoft.Data.SqlClient, IsValidPacket(), if(), lock() (+41 more)

### Community 12 - "Entity (Community 12)"
Cohesion: 0.08
Nodes (47): TdsParserStateObjectManaged.netcore.cs, TdsParserStateObjectManaged.netcore.cs, AssignPendingDNSInfo(), AddPacketToPendingList(), CreateSspiContextProvider(), ClearAllWritePackets(), CheckPacket(), base() (+39 more)

### Community 13 - "Entity (Community 13)"
Cohesion: 0.04
Nodes (46): SqlDataReader.cs, using(), TryReadColumnInternal(), TryReadColumnData(), while(), lock(), GetSqlValue(), GetSqlValues() (+38 more)

### Community 15 - "Entity (Community 15)"
Cohesion: 0.09
Nodes (43): MemoryRecordBuffer.cs, MemoryRecordBuffer.cs, GetBoolean(), GetByte(), for(), GetBytes(), GetBytesLength(), GetInt16() (+35 more)

### Community 14 - "Entity (Community 14)"
Cohesion: 0.09
Nodes (43): SqlDependency.cs, SqlDependency.cs, value(), using(), Stop(), ResCategory(), ObtainProcessDispatcher(), new() (+35 more)

### Community 16 - "Entity (Community 16)"
Cohesion: 0.05
Nodes (42): SqlBulkCopy.cs, IsCopyOption(), EXISTS(), GetValueFromSourceRow(), GetColumnMetadata(), CreateAndExecuteInitialQueryAsync(), FireRowsCopiedEvent(), IN() (+34 more)

### Community 17 - "Entity (Community 17)"
Cohesion: 0.05
Nodes (41): SmiTypedGetterSetter.cs, GetChars(), GetBytes(), GetBoolean(), GetBytesLength(), GetByte(), SetGuid(), SetDouble() (+33 more)

### Community 19 - "Entity (Community 19)"
Cohesion: 0.05
Nodes (40): SqlConnection.cs, Close(), catch(), BeginTransaction(), ChangePassword(), CheckAndThrowOnInvalidCombinationOfConnectionOptionAndAccessTokenCallback(), beforeDisconnect(), CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential() (+32 more)

### Community 18 - "Entity (Community 18)"
Cohesion: 0.05
Nodes (40): SqlParameter.cs, ValueSize(), ValuePrecision(), ValidateTypeLengths(), Validate(), SqlParameterConverter(), SetSqlBuffer(), StringSize() (+32 more)

### Community 20 - "Entity (Community 20)"
Cohesion: 0.05
Nodes (40): SqlConnectionInternal.cs, Activate(), Deactivate(), AttemptRetryADAuthWithTimeoutError(), BeginTransaction(), ChangeDatabase(), CleanupTransactionOnCompletion(), DecrementAsyncCount() (+32 more)

### Community 21 - "Entity (Community 21)"
Cohesion: 0.05
Nodes (39): SqlParameter.cs, GetCoercedValue(), RefreshProperties(), PropertyTypeChanging(), PropertyChanging(), MetaDataForSmi(), if(), HasFlag() (+31 more)

### Community 22 - "Entity (Community 22)"
Cohesion: 0.05
Nodes (38): SqlBuffer.cs, SqlBoolean(), SetToJson(), SetToMoney(), GetVectorInfo(), SetVectorInfo(), GetTypeFromStorageType(), NumericInfo (+30 more)

### Community 23 - "Entity (Community 23)"
Cohesion: 0.06
Nodes (35): SqlConnectionOptions.cs, IsValueValidInternal(), GetKeyName(), DemandPermission(), CreatePermissionSet(), ConvertValueToString(), ExpandDataDirectory(), IsKeyNameValid() (+27 more)

### Community 25 - "Entity (Community 25)"
Cohesion: 0.11
Nodes (35): SqlDependencyListener.cs, SqlDependencyListener.cs, QueueAppDomainUnloading(), StartWithDefault(), Start(), SqlDependencyProcessDispatcher(), SqlConnectionContainerHashHelper(), SqlConnectionContainer() (+27 more)

### Community 24 - "Entity (Community 24)"
Cohesion: 0.06
Nodes (35): SqlDiagnosticListener.cs, SqlDiagnosticListener(), SqlClientConnectionCloseError(), SqlClientTransactionCommitAfter(), SqlClientConnectionOpenBefore(), SqlClientConnectionOpenAfter(), SqlClientTransactionRollbackAfter(), SqlClientConnectionCloseBefore() (+27 more)

### Community 26 - "Entity (Community 26)"
Cohesion: 0.06
Nodes (33): SqlCommand.Reader.cs, AfterCleared(), CompleteAsyncExecuteReader(), BuildExecute(), CheckNotificationStateAndAutoEnlist(), BuildPrepExec(), CleanupExecuteReaderAsync(), CheckThrowSNIException() (+25 more)

### Community 27 - "Entity (Community 27)"
Cohesion: 0.06
Nodes (33): SqlConnectionStringBuilder.cs, ShouldSerialize(), GetStandardValuesSupportedInternal(), Microsoft.Data.SqlClient, SqlInitialCatalogConverter(), using(), TryGetValue(), SqlDataSourceConverter() (+25 more)

### Community 28 - "Entity (Community 28)"
Cohesion: 0.06
Nodes (32): SqlDataRecord.cs, GetTimeSpan(), GetSqlChars(), SetDecimal(), SetChar(), GetSqlValues(), GetSqlSingle(), GetSqlMoney() (+24 more)

### Community 30 - "Entity (Community 30)"
Cohesion: 0.06
Nodes (32): SqlConnectionInternal.cs, BeginSqlTransaction(), AttemptOneLogin(), TimeSpan(), ResetConnection(), PropagateTransactionCookie(), ProviderApiViolationException(), SqlConnectionInternal() (+24 more)

### Community 29 - "Entity (Community 29)"
Cohesion: 0.06
Nodes (32): SqlBulkCopy.cs, WriteToServerInternalRestAsync(), switch(), WriteRowSourceToServerAsync(), WriteToServerInternalRestContinuedAsync(), WriteRowSourceToServerCommon(), WriteToServerAsync(), static() (+24 more)

### Community 33 - "Entity (Community 33)"
Cohesion: 0.07
Nodes (30): SqlConnectionFactory.cs, GetObjectId(), GetConnectionPool(), SqlConnectionPoolGroupProviderInfo(), SetInnerConnectionTo(), FindSqlConnectionOptions(), SetInnerConnectionFrom(), SetInnerConnectionEvent() (+22 more)

### Community 31 - "Entity (Community 31)"
Cohesion: 0.07
Nodes (30): SqlEnums.cs, _Is70Supported(), GetMetaTypeFromType(), GetMaxMetaTypeFromMetaType(), GetTimeSizeFromScale(), GetStringFromXml(), GetSqlValueFromComVariant(), GetSqlDataType() (+22 more)

### Community 32 - "Entity (Community 32)"
Cohesion: 0.07
Nodes (30): SqlClientMetrics.cs, SqlClientMetrics(), HardDisconnectRequest(), ReclaimedConnectionRequest(), SoftDisconnectRequest(), HardConnectRequest(), SoftConnectRequest(), Microsoft.Data.SqlClient.Diagnostics (+22 more)

### Community 34 - "Entity (Community 34)"
Cohesion: 0.14
Nodes (28): SqlParameterCollection.cs, SqlParameterCollection.cs, foreach(), Clear(), AddRange(), AddWithValue(), Browsable(), CopyTo() (+20 more)

### Community 35 - "Entity (Community 35)"
Cohesion: 0.15
Nodes (27): SqlStream.cs, SqlStream.cs, while(), ToXmlReader(), this(), Write(), switch(), WriteXmlElement() (+19 more)

### Community 36 - "Entity (Community 36)"
Cohesion: 0.08
Nodes (26): SqlCommand.Reader.cs, RunExecuteReader(), WriteBeginExecuteEvent(), SetUpRPCParameters(), GetRPCObject(), PutStateObject(), InvalidateEnclaveSession(), ReliablePutStateObject() (+18 more)

### Community 37 - "Entity (Community 37)"
Cohesion: 0.15
Nodes (26): SqlMetaData.cs, SqlMetaData.cs, Adjust(), ValidateSortOrder(), VerifyTimeRange(), VerifyDateTimeRange(), VerifyMoneyRange(), ThrowInvalidType() (+18 more)

### Community 38 - "Entity (Community 38)"
Cohesion: 0.08
Nodes (25): SqlCommand.NonQuery.cs, InternalEndExecuteNonQuery(), Clear(), BeginExecuteNonQueryAsync(), EndExecuteNonQueryAsync(), AfterCleared(), GetStateObject(), ExecuteNonQueryAsync() (+17 more)

### Community 40 - "Entity (Community 40)"
Cohesion: 0.08
Nodes (25): TdsRecordBufferSetter.cs, SetTimeSpan(), SetSqlDecimal(), SetVariantMetaData(), while(), SetString(), SetInt64(), SetChars() (+17 more)

### Community 39 - "Entity (Community 39)"
Cohesion: 0.08
Nodes (25): WaitHandleDbConnectionPool.cs, ErrorCallback(), Dispose(), CreateObject(), CleanupCallback(), Clear(), CreateCleanupTimer(), IsIdleExpired() (+17 more)

### Community 41 - "Entity (Community 41)"
Cohesion: 0.08
Nodes (24): SniTcpHandle.netcore.cs, ParallelConnect(), ArgumentNullException(), Dispose(), DisableSsl(), KillConnection(), is(), EnableSsl() (+16 more)

### Community 46 - "Entity (Community 46)"
Cohesion: 0.09
Nodes (23): SqlCommand.cs, ValidateCommand(), Unprepare(), GetStateObject(), GetOptionsSetString(), foreach(), switch(), SqlCommand() (+15 more)

### Community 45 - "Entity (Community 45)"
Cohesion: 0.09
Nodes (23): SqlStatistics.cs, TimedScope(), ContinueOnNewConnection(), Dispose(), StatisticsDictionary(), GetDictionary(), RequestNetworkServerTimer(), RequestExecutionTimer() (+15 more)

### Community 44 - "Entity (Community 44)"
Cohesion: 0.17
Nodes (23): ITypedGettersV3.cs, ITypedGettersV3.cs, GetInt32(), IsDBNull(), GetBytes(), GetBytesLength(), GetDateTimeOffset(), GetSingle() (+15 more)

### Community 42 - "Entity (Community 42)"
Cohesion: 0.17
Nodes (23): ITypedSettersV3.cs, ITypedSettersV3.cs, SetCharsLength(), SetChars(), ITypedSettersV3, Microsoft.Data.SqlClient.Server, SetBytes(), SetByte() (+15 more)

### Community 43 - "Entity (Community 43)"
Cohesion: 0.17
Nodes (23): SqlClientPermission.netfx.cs, SqlClientPermission.netfx.cs, IsSubsetOf(), Union(), Copy(), Clear(), SqlClientPermission(), NameValuePermission() (+15 more)

### Community 47 - "Entity (Community 47)"
Cohesion: 0.09
Nodes (22): AzureAttestationBasedEnclaveProvider.cs, ComputeSHA256(), ArgumentException(), GetInnerMostExceptionMessage(), GetAttestationInstanceUrl(), GenerateListOfIssuers(), foreach(), GetOpenIdConfigForSigningKeys() (+14 more)

### Community 50 - "Entity (Community 50)"
Cohesion: 0.09
Nodes (22): SqlMetaDataFactory.cs, CreateMetaDataCollectionsDataTable(), PrepareCollectionAsync(), GetDataTypesTable(), FixUpDataSourceInformationRow(), GetParameterName(), CloneAndFilterCollection(), LoadDataTypesDataTables() (+14 more)

### Community 51 - "Entity (Community 51)"
Cohesion: 0.18
Nodes (22): SqlClientFactory.cs, SqlClientFactory.cs, SqlParameter(), CreateBatchCommand(), SqlClientFactory(), CreatePermission(), CreateConnectionStringBuilder(), CreateBatch() (+14 more)

### Community 48 - "Entity (Community 48)"
Cohesion: 0.09
Nodes (22): SqlConnectionStringBuilder.cs, SqlConnectionStringBuilder(), Remove(), SetAttestationProtocolValue(), SetApplicationIntentValue(), ConvertTo(), for(), Reset() (+14 more)

### Community 49 - "Entity (Community 49)"
Cohesion: 0.18
Nodes (22): SniPacket.netcore.cs, SniPacket.netcore.cs, WriteToStream(), ReadFromStream(), InvokeAsyncIOCompletionCallback(), Allocate(), if(), GetData() (+14 more)

### Community 52 - "Entity (Community 52)"
Cohesion: 0.19
Nodes (21): VirtualSecureModeEnclaveProvider.cs, VirtualSecureModeEnclaveProvider.cs, GetSizeInPayload(), catch(), ArgumentException(), EnclaveIdentity(), Microsoft.Data.SqlClient, HealthReport() (+13 more)

### Community 53 - "Entity (Community 53)"
Cohesion: 0.10
Nodes (21): SniMarsHandle.netcore.cs, CheckConnection(), DisableSsl(), Dispose(), EnableSsl(), SetPacketSMUXHeader(), SniMarsHandle(), SetAsyncCallbacks() (+13 more)

### Community 56 - "Entity (Community 56)"
Cohesion: 0.10
Nodes (20): SqlConnectionOptions.cs, for(), ExpandAttachDbFileName(), ExpandKeyword(), ConvertValueToBooleanInternal(), ConvertToInt32Internal(), catch(), AppendKeyValuePairBuilder() (+12 more)

### Community 55 - "Entity (Community 55)"
Cohesion: 0.10
Nodes (20): TdsValueSetter.cs, SetDateTimeOffset(), SetTimeSpan(), SetDouble(), SetSingle(), SetInt64(), SetInt32(), SetInt16() (+12 more)

### Community 54 - "Entity (Community 54)"
Cohesion: 0.10
Nodes (20): SqlCommand.Encryption.cs, EnclaveSessionParameters(), CheckThrowSNIException(), InvalidateEnclaveSession(), GetParameterEncryptionDataReader(), ReadDescribeEncryptionParameterResultsMetadata(), TryFetchInputParameterEncryptionInfo(), TryGetColumnEncryptionKeyStoreProvider() (+12 more)

### Community 57 - "Entity (Community 57)"
Cohesion: 0.11
Nodes (19): SqlBuffer.cs, Clear(), if(), decimal(), FillInTimeInfo(), SetToDate(), SqlBuffer(), SetToTime() (+11 more)

### Community 59 - "Entity (Community 59)"
Cohesion: 0.11
Nodes (19): WaitHandleDbConnectionPool.cs, ReturnInternalConnection(), QueuePoolCreateRequest(), TryCloneCachedException(), AbandonedMutexException(), for(), switch(), catch() (+11 more)

### Community 58 - "Entity (Community 58)"
Cohesion: 0.20
Nodes (19): SsrpClient.netcore.cs, SsrpClient.netcore.cs, switch(), using(), foreach(), SplitIPv4AndIPv6(), SocketException(), SendUDPRequest() (+11 more)

### Community 60 - "Entity (Community 60)"
Cohesion: 0.11
Nodes (18): SqlCommand.NonQuery.cs, VerifyEndExecuteState(), NotifyDependency(), static(), PutStateObject(), WriteBeginExecuteEvent(), InternalExecuteNonQuery(), InternalExecuteNonQueryWithRetry() (+10 more)

### Community 61 - "Entity (Community 61)"
Cohesion: 0.22
Nodes (18): TdsParserStateObject.Multiplexer.cs, TdsParserStateObject.Multiplexer.cs, InvalidOperationException(), ClearPartialPacket(), AssertValidState(), ProcessSniPacket(), MultiplexPackets(), if() (+10 more)

### Community 64 - "Entity (Community 64)"
Cohesion: 0.11
Nodes (18): VirtualSecureModeEnclaveProviderBase.cs, InvalidateEnclaveSessionHelper(), InvalidateEnclaveSession(), GetSigningCertificate(), GetAttestationParameters(), CreateEnclaveSession(), GetAttestationUrl(), catch() (+10 more)

### Community 63 - "Entity (Community 63)"
Cohesion: 0.11
Nodes (18): SqlCommand.Xml.cs, EndExecuteXmlReader(), ThrowIfReconnectionHasBeenCanceled(), Clear(), Microsoft.Data.SqlClient, Set(), nameof(), ValidateAsyncCommand() (+10 more)

### Community 65 - "Entity (Community 65)"
Cohesion: 0.22
Nodes (18): SniHandle.netcore.cs, SniHandle.netcore.cs, Microsoft.Data.SqlClient.ManagedSni, Send(), Receive(), ReturnPacket(), KillConnection(), EnableSsl() (+10 more)

### Community 62 - "Entity (Community 62)"
Cohesion: 0.22
Nodes (18): SqlMetaDataFactory.DataTypes.cs, SqlMetaDataFactory.DataTypes.cs, AddStringOrBinaryType(), AddFixedPrecisionDateTimeType(), Microsoft.Data.SqlClient, AddFixedLengthStringOrBinaryType(), CreateDataTypesDataTable(), AddRowVersionType() (+10 more)

### Community 69 - "Entity (Community 69)"
Cohesion: 0.12
Nodes (17): SqlSecurityUtility.cs, TryGetColumnEncryptionKeyStoreProvider(), Microsoft.Data.SqlClient, VerifyColumnMasterKeySignature(), ValidateAndGetEncryptionAlgorithmName(), ShouldUseInstanceLevelProviderFlow(), return(), SqlClientSymmetricKey() (+9 more)

### Community 70 - "Entity (Community 70)"
Cohesion: 0.12
Nodes (17): SqlConfigurableRetryLogicLoader.cs, SqlConfigurableRetryLogicLoader(), CreateRetryLogicProvider(), SplitErrorNumberList(), AssemblyResolver(), MakeFullPath(), typeof(), Default_Resolving() (+9 more)

### Community 66 - "Entity (Community 66)"
Cohesion: 0.12
Nodes (17): TdsParserHelperClasses.cs, ArgumentException(), SqlMetaDataPriv(), _SqlMetaDataSetCollection(), GetParameterByIndex(), ToFriendlyName(), SqlReturnValue(), SqlFedAuthInfo() (+9 more)

### Community 67 - "Entity (Community 67)"
Cohesion: 0.23
Nodes (17): SmiMetaData.cs, SmiMetaData.cs, SmiQueryMetaData(), SetDefaultsForType(), GetDefaultForType(), SmiParameterMetaData(), SmiExtendedMetaData(), SmiMetaData() (+9 more)

### Community 68 - "Entity (Community 68)"
Cohesion: 0.12
Nodes (17): TdsParserStaticMethods.cs, GetTimeoutSeconds(), NullAwareStringLength(), GetRemainingTimeout(), TimeoutHasExpired(), AliasRegistryLookup(), using(), return() (+9 more)

### Community 71 - "Entity (Community 71)"
Cohesion: 0.12
Nodes (16): SniMarsConnection.netcore.cs, KillConnection(), HandleReceiveComplete(), Microsoft.Data.SqlClient.ManagedSni, RentPacket(), SendAsync(), Send(), StartReceive() (+8 more)

### Community 72 - "Entity (Community 72)"
Cohesion: 0.24
Nodes (16): SqlQueryMetadataCache.cs, SqlQueryMetadataCache.cs, catch(), AddQueryMetadata(), private(), return(), SqlQueryMetadataCache(), Microsoft.Data.SqlClient (+8 more)

### Community 73 - "Entity (Community 73)"
Cohesion: 0.24
Nodes (16): SmiMetaDataProperty.cs, SmiMetaDataProperty.cs, CheckCount(), SmiColumnOrder, SmiOrderProperty(), Microsoft.Data.SqlClient.Server, TraceString(), foreach() (+8 more)

### Community 78 - "Entity (Community 78)"
Cohesion: 0.12
Nodes (16): SniNpHandle.netcore.cs, KillConnection(), Microsoft.Data.SqlClient.ManagedSni, Receive(), ValidateServerCertificate(), SniNpHandle(), SendAsync(), ReceiveAsync() (+8 more)

### Community 77 - "Entity (Community 77)"
Cohesion: 0.12
Nodes (16): MetadataUtilsSmi.cs, DetermineExtendedTypeCodeForUseWithSqlDbType(), SqlMetaDataToSmiExtendedMetaData(), InferSqlDbTypeFromType_2008(), SmiMetaDataFromDataColumn(), IsCharOrXmlType(), InferSqlDbTypeFromTypeCode(), IsCompatible() (+8 more)

### Community 76 - "Entity (Community 76)"
Cohesion: 0.12
Nodes (16): SniProxy.netcore.cs, GetLocalDBDataSource(), for(), GetLocalDBInstance(), SniProxy, CreateConnectionHandle(), SniTcpHandle(), CreateNpHandle() (+8 more)

### Community 75 - "Entity (Community 75)"
Cohesion: 0.24
Nodes (16): SslOverTdsStream.netcore.cs, SslOverTdsStream.netcore.cs, WriteAsync(), SslOverTdsStream(), Write(), SetupPreLoginPacketHeader(), while(), using() (+8 more)

### Community 74 - "Entity (Community 74)"
Cohesion: 0.12
Nodes (16): SqlSer.cs, GetNewSerializer(), GetUdtMaxLength(), SetLength(), return(), switch(), Seek(), Read() (+8 more)

### Community 82 - "Entity (Community 82)"
Cohesion: 0.13
Nodes (15): TdsParserHelperClasses.cs, foreach(), SqlFedAuthToken(), SetupHiddenColumns(), _SqlMetaDataSet(), HasFlag(), for(), RoutingInfo() (+7 more)

### Community 83 - "Entity (Community 83)"
Cohesion: 0.13
Nodes (15): SqlSequentialTextReader.cs, InternalRead(), GetMaxByteCount(), GetCharCount(), Dispose(), GetChars(), GetDecoder(), Microsoft.Data.SqlClient (+7 more)

### Community 81 - "Entity (Community 81)"
Cohesion: 0.13
Nodes (15): ChannelDbConnectionPool.cs, ReplaceConnection(), TransactionEnded(), Startup(), PruneConnections(), PutObjectFromTransactedPool(), Shutdown(), TryGetConnection() (+7 more)

### Community 79 - "Entity (Community 79)"
Cohesion: 0.26
Nodes (15): SqlBatchCommandCollection.cs, SqlBatchCommandCollection.cs, Clear(), Add(), RemoveAt(), GetEnumerator(), SqlBatchCommandCollection(), Contains() (+7 more)

### Community 85 - "Entity (Community 85)"
Cohesion: 0.13
Nodes (15): SqlCommandBuilder.cs, SqlRowUpdatingHandler(), ApplyParameterInfo(), Microsoft.Data.SqlClient, ResDescription(), QuoteIdentifier(), SetRowUpdatingHandler(), ResCategory() (+7 more)

### Community 84 - "Entity (Community 84)"
Cohesion: 0.13
Nodes (15): SqlDataAdapter.cs, ExecuteBatch(), GetBatchedRecordsAffected(), CreateRowUpdatedEvent(), GetBatchedParameter(), SqlRowUpdatedEventArgs(), OnRowUpdated(), InitializeBatching() (+7 more)

### Community 80 - "Entity (Community 80)"
Cohesion: 0.13
Nodes (15): SqlDependencyUtils.cs, DependencyList(), InitializeLifetimeService(), TimeoutTimerCallback(), SqlDependencyPerAppDomainDispatcher(), Microsoft.Data.SqlClient, TimerCallback(), InvalidateCommandID() (+7 more)

### Community 92 - "Entity (Community 92)"
Cohesion: 0.14
Nodes (14): SniTcpHandle.netcore.cs, ReturnPacket(), ReportTcpSNIError(), using(), SetKeepAliveValues(), Win32Exception(), when(), catch() (+6 more)

### Community 91 - "Entity (Community 91)"
Cohesion: 0.14
Nodes (14): SqlBatch.cs, DisposeAsync(), ExecuteDbDataReader(), Cancel(), CreateDbBatchCommand(), PrepareAsync(), Prepare(), Microsoft.Data.SqlClient (+6 more)

### Community 88 - "Entity (Community 88)"
Cohesion: 0.27
Nodes (14): NoneAttestationEnclaveProvider.cs, NoneAttestationEnclaveProvider.cs, UpdateEnclaveSessionLockStatus(), NoneAttestationEnclaveProvider, SqlEnclaveAttestationParameters(), if(), InvalidateEnclaveSessionHelper(), InvalidateEnclaveSession() (+6 more)

### Community 89 - "Entity (Community 89)"
Cohesion: 0.27
Nodes (14): AlwaysEncryptedHelperClasses.cs, AlwaysEncryptedHelperClasses.cs, SqlColumnEncryptionInputParameterInfo(), SerializeIntIntoBuffer(), SqlTceCipherInfoEntry(), SerializeToWriteFormat(), SqlTceCipherInfoTable(), Microsoft.Data.SqlClient (+6 more)

### Community 90 - "Entity (Community 90)"
Cohesion: 0.27
Nodes (14): SqlReferenceCollection.cs, SqlReferenceCollection.cs, Remove(), Microsoft.Data.SqlClient, HasOpenReaderPredicate(), FindLiveReader(), if(), Clear() (+6 more)

### Community 87 - "Entity (Community 87)"
Cohesion: 0.27
Nodes (14): AlwaysEncryptedKeyConverter.cs, AlwaysEncryptedKeyConverter.cs, using(), RSACng(), Microsoft.Data.SqlClient, InvalidOperationException(), if(), GetRSAFromCertificate() (+6 more)

### Community 86 - "Entity (Community 86)"
Cohesion: 0.14
Nodes (14): SqlBulkCopyColumnMappingCollection.cs, SqlBulkCopyColumnMappingCollection(), RemoveAt(), Remove(), Microsoft.Data.SqlClient, Insert(), IndexOf(), foreach() (+6 more)

### Community 93 - "Entity (Community 93)"
Cohesion: 0.29
Nodes (13): SqlRetryLogicProvider.cs, SqlRetryLogicProvider.cs, catch(), CreateException(), ApplyRetryingEvent(), if(), ArgumentNullException(), ExecuteAsync() (+5 more)

### Community 96 - "Entity (Community 96)"
Cohesion: 0.15
Nodes (13): LocalDbApi.windows.cs, LocalDbFormatMessageDelegate(), CreateLocalDbInstance(), catch(), GetLocalDbMessage(), AssertLocalDbPermissions(), LocalDbCreateInstanceDelegate(), foreach() (+5 more)

### Community 94 - "Entity (Community 94)"
Cohesion: 0.29
Nodes (13): CachedContexts.cs, CachedContexts.cs, TrySetCommandExecuteReaderAsyncContext(), Microsoft.Data.SqlClient.Connection, TrySetCommandExecuteNonQueryAsyncContext(), if(), TrySetCommandExecuteXmlReaderAsyncContext(), TrySetDataReaderReadAsyncContext() (+5 more)

### Community 95 - "Entity (Community 95)"
Cohesion: 0.15
Nodes (13): SqlInternalTransaction.cs, DecrementAndObtainOpenResultCount(), CloseFromConnection(), Commit(), Activate(), IncrementAndObtainOpenResultCount(), Microsoft.Data.SqlClient, GetServerTransactionLevel() (+5 more)

### Community 103 - "Entity (Community 103)"
Cohesion: 0.15
Nodes (13): SqlCommand.Xml.cs, EndExecuteXmlReaderInternal(), ReliablePutStateObject(), if(), WriteEndExecuteEvent(), BeginExecuteXmlReaderInternalReadStage(), catch(), ExecuteXmlReaderAsync() (+5 more)

### Community 102 - "Entity (Community 102)"
Cohesion: 0.29
Nodes (13): SqlTransaction.cs, SqlTransaction.cs, ZombieCheck(), Zombie(), Save(), catch(), Dispose(), Commit() (+5 more)

### Community 100 - "Entity (Community 100)"
Cohesion: 0.15
Nodes (13): SqlConnectionTimeoutErrorInternal.cs, SetFailoverScenario(), GetMilliSecondDuration(), StartCapture(), EndPhase(), ResetAndRestartPhase(), SqlConnectionTimeoutPhaseDuration, SetAllCompleteMarker() (+5 more)

### Community 101 - "Entity (Community 101)"
Cohesion: 0.15
Nodes (13): LocalDB.netcore.windows.cs, GetProcAddress(), MapLocalDBErrorStateToCode(), foreach(), LocalDBStartInstance(), Microsoft.Data.SqlClient.ManagedSni, MapLocalDBErrorStateToErrorMessage(), LocalDB() (+5 more)

### Community 99 - "Entity (Community 99)"
Cohesion: 0.15
Nodes (13): EnclaveDelegate.Crypto.cs, switch(), InvalidateEnclaveSession(), GetEnclaveProvider(), GetSerializedAttestationParameters(), GetAttestationParameters(), catch(), Microsoft.Data.SqlClient (+5 more)

### Community 98 - "Entity (Community 98)"
Cohesion: 0.15
Nodes (13): SqlNormalizer.cs, GetNormalizer(), NormalizeTopObject(), Microsoft.Data.SqlClient.Server, GetValue(), GetFields(), Exception(), DeNormalizeInternal() (+5 more)

### Community 97 - "Entity (Community 97)"
Cohesion: 0.15
Nodes (13): SqlCommand.Encryption.cs, BuildStoredProcedureStatementForColumnEncryption(), for(), ReadDescribeEncryptionParameterResultsAttestation(), ReadDescribeEncryptionParameterResults(), PrepareTransparentEncryptionFinallyBlock(), PrepareDescribeParameterEncryptionRequest(), if() (+5 more)

### Community 107 - "Entity (Community 107)"
Cohesion: 0.32
Nodes (12): IDbConnectionPool.cs, IDbConnectionPool.cs, Startup(), TransactionEnded(), TryGetConnection(), ReturnInternalConnection(), ReplaceConnection(), PutObjectFromTransactedPool() (+4 more)

### Community 109 - "Entity (Community 109)"
Cohesion: 0.32
Nodes (12): SignatureVerificationCache.cs, SignatureVerificationCache.cs, GetSignatureVerificationResult(), GetCacheLookupKey(), AddSignatureVerificationResult(), ColumnMasterKeyMetadataSignatureVerificationCache(), Microsoft.Data.SqlClient, ValidateStringArgumentNotNullOrEmpty() (+4 more)

### Community 108 - "Entity (Community 108)"
Cohesion: 0.17
Nodes (12): Packet.cs, GetHeaderSpan(), InvalidOperationException(), GetStatusFromHeader(), GetSpidFromHeader(), GetIsEOMFromHeader(), ObjectDisposedException(), GetIDFromHeader() (+4 more)

### Community 106 - "Entity (Community 106)"
Cohesion: 0.17
Nodes (12): SqlConnectionFactory.cs, Unload(), foreach(), SetConnectionPoolGroup(), QueuePoolGroupForRelease(), lock(), if(), SubscribeToAssemblyLoadContextUnload() (+4 more)

### Community 104 - "Entity (Community 104)"
Cohesion: 0.17
Nodes (12): SqlCommandSet.cs, GetBatchedAffected(), GetParameter(), BuildStoredProcedureName(), ExecuteNonQuery(), GetParameterCount(), using(), SqlCommandSet() (+4 more)

### Community 105 - "Entity (Community 105)"
Cohesion: 0.32
Nodes (12): EnclaveSessionCache.cs, EnclaveSessionCache.cs, return(), InvalidOperationException(), if(), GetEnclaveSession(), InvalidateSession(), lock() (+4 more)

### Community 110 - "Entity (Community 110)"
Cohesion: 0.17
Nodes (12): SqlMetaDataFactory.cs, while(), for(), foreach(), Dispose(), AddUDTsToDataTypesTableAsync(), DataColumn(), AddTVPsToDataTypesTableAsync() (+4 more)

### Community 112 - "Entity (Community 112)"
Cohesion: 0.32
Nodes (12): DbConnectionPoolGroup.cs, DbConnectionPoolGroup.cs, GetConnectionPool(), Prune(), Microsoft.Data.SqlClient.ConnectionPool, return(), lock(), if() (+4 more)

### Community 111 - "Entity (Community 111)"
Cohesion: 0.32
Nodes (12): AsyncHelper.cs, AsyncHelper.cs, SetTimeoutException(), ObserveContinuationException(), onSuccess(), Microsoft.Data.SqlClient.Utilities, catch(), static() (+4 more)

### Community 114 - "Entity (Community 114)"
Cohesion: 0.32
Nodes (12): AAsyncCallContext.cs, AAsyncCallContext.cs, if(), Microsoft.Data.SqlClient, Set(), DisposeCore(), Dispose(), ClearCore() (+4 more)

### Community 115 - "Entity (Community 115)"
Cohesion: 0.17
Nodes (12): SqlColumnEncryptionCspProvider.cs, VerifyColumnMasterKeyMetadata(), catch(), Microsoft.Data.SqlClient, GetProviderType(), EncryptColumnEncryptionKey(), DecryptColumnEncryptionKey(), RSACryptoServiceProvider() (+4 more)

### Community 113 - "Entity (Community 113)"
Cohesion: 0.17
Nodes (12): SqlRecordBuffer.cs, switch(), Microsoft.Data.SqlClient.Server, SqlRecordBuffer(), SmiMetaData(), SetNull(), SetChars(), string() (+4 more)

### Community 125 - "Entity (Community 125)"
Cohesion: 0.35
Nodes (11): SqlRetryIntervalEnumerators.cs, SqlRetryIntervalEnumerators.cs, Clone(), GetNextInterval(), SqlIncrementalIntervalEnumerator(), SqlNoneIntervalEnumerator, Reset(), SqlFixedIntervalEnumerator() (+3 more)

### Community 124 - "Entity (Community 124)"
Cohesion: 0.18
Nodes (11): SqlBulkCopyColumnOrderHintCollection.cs, Remove(), OnRemove(), OnClear(), Insert(), ColumnNameChanging(), IndexOf(), Contains() (+3 more)

### Community 121 - "Entity (Community 121)"
Cohesion: 0.18
Nodes (11): SqlCommand.Batch.cs, GetCurrentBatchCommand(), SetColumnEncryptionSetting(), GetErrors(), ReliablePutStateObject(), SetBatchRPCModeReadyToExecute(), AddBatchCommand(), SetBatchRPCMode() (+3 more)

### Community 122 - "Entity (Community 122)"
Cohesion: 0.18
Nodes (11): SqlCachedBuffer.cs, SqlNullValueException(), ToSqlXml(), Microsoft.Data.SqlClient, TryCreate(), ToStream(), ToString(), ToXmlReader() (+3 more)

### Community 123 - "Entity (Community 123)"
Cohesion: 0.18
Nodes (11): SqlCommandBuilder.cs, GetDeleteCommand(), GetInsertCommand(), GetParameterName(), SqlCommandBuilder(), DesignerSerializationVisibility(), ConsistentQuoteDelimiters(), GetUpdateCommand() (+3 more)

### Community 120 - "Entity (Community 120)"
Cohesion: 0.18
Nodes (11): SqlDependencyUtils.cs, SqlNotification(), RemoveDependencyFromCommandToDependenciesHash(), SubscribeToAssemblyLoadContextUnload(), SubscribeToAppDomainUnload(), lock(), foreach(), if() (+3 more)

### Community 117 - "Entity (Community 117)"
Cohesion: 0.18
Nodes (11): SqlAuthenticationProviderManager.cs, switch(), GetProviderType(), SqlClientAuthenticationProviderConfigurationSection, foreach(), Microsoft.Data.SqlClient, Initialize(), when() (+3 more)

### Community 118 - "Entity (Community 118)"
Cohesion: 0.35
Nodes (11): SqlException.cs, SqlException.cs, foreach(), for(), InternalClone(), if(), GetObjectData(), Microsoft.Data.SqlClient (+3 more)

### Community 119 - "Entity (Community 119)"
Cohesion: 0.18
Nodes (11): SniMarsHandle.netcore.cs, ReturnPacket(), if(), lock(), while(), SendAckIfNecessary(), using(), SendControlPacket() (+3 more)

### Community 116 - "Entity (Community 116)"
Cohesion: 0.18
Nodes (11): SqlDelegatedTransaction.cs, SqlDelegatedTransaction(), switch(), TransactionEnded(), Rollback(), Promote(), Microsoft.Data.SqlClient, Initialize() (+3 more)

### Community 129 - "Entity (Community 129)"
Cohesion: 0.20
Nodes (10): VirtualSecureModeEnclaveProviderBase.cs, for(), ArgumentException(), VerifyEnclavePolicyProperty(), using(), VerifyEnclavePolicy(), if(), VerifyEnclaveReportSignature() (+2 more)

### Community 126 - "Entity (Community 126)"
Cohesion: 0.20
Nodes (10): SniProxy.netcore.cs, if(), ReportSNIError(), catch(), PopulateProtocol(), new(), DataSource(), InferLocalServerName() (+2 more)

### Community 128 - "Entity (Community 128)"
Cohesion: 0.20
Nodes (10): SqlEnums.cs, switch(), _Is90Supported(), AssertIsUserDefinedTypeInstance(), GetMetaTypeFromValue(), case(), return(), if() (+2 more)

### Community 127 - "Entity (Community 127)"
Cohesion: 0.20
Nodes (10): ChannelDbConnectionPool.cs, ReturnInternalConnection(), RemoveConnection(), while(), PrepareConnection(), if(), ValidateOwnershipAndSetPoolingState(), NotImplementedException() (+2 more)

### Community 134 - "Entity (Community 134)"
Cohesion: 0.20
Nodes (10): SqlInternalTransaction.cs, catch(), Zombie(), Rollback(), ZombieParent(), using(), SqlInternalTransaction(), if() (+2 more)

### Community 131 - "Entity (Community 131)"
Cohesion: 0.20
Nodes (10): AzureAttestationBasedEnclaveProvider.cs, AzureAttestationToken(), VerifyAzureAttestationInfo(), AzureAttestationInfo(), catch(), ValidateAttestationClaims(), using(), if() (+2 more)

### Community 133 - "Entity (Community 133)"
Cohesion: 0.38
Nodes (10): SmiSettersStream.cs, SmiSettersStream.cs, SmiSettersStream(), Flush(), Seek(), SetLength(), if(), Write() (+2 more)

### Community 130 - "Entity (Community 130)"
Cohesion: 0.38
Nodes (10): SmiGettersStream.cs, SmiGettersStream.cs, Seek(), SetLength(), Write(), SmiGettersStream(), Read(), Flush() (+2 more)

### Community 132 - "Entity (Community 132)"
Cohesion: 0.20
Nodes (10): SqlSecurityUtility.cs, if(), GetListOfProviderNamesThatWereSearched(), ThrowIfKeyPathIsNotTrustedForServer(), GetKeyFromLocalProviders(), catch(), InstanceLevelProvidersAreRegistered(), foreach() (+2 more)

### Community 137 - "Entity (Community 137)"
Cohesion: 0.20
Nodes (10): SqlSequentialStream.cs, Read(), Microsoft.Data.SqlClient, Dispose(), SetLength(), Write(), Seek(), SqlSequentialStream() (+2 more)

### Community 135 - "Entity (Community 135)"
Cohesion: 0.38
Nodes (10): LocalDbInstancesCollection.netfx.cs, LocalDbInstancesCollection.netfx.cs, GetElementKey(), LocalDbInstanceElement(), if(), CreateNewElement(), LocalDbInstancesCollection(), Compare() (+2 more)

### Community 136 - "Entity (Community 136)"
Cohesion: 0.38
Nodes (10): ObjectPool.cs, ObjectPool.cs, Microsoft.Data.SqlClient.Utilities, if(), TryGet(), Return(), Rent(), ObjectWrapper (+2 more)

### Community 138 - "Entity (Community 138)"
Cohesion: 0.20
Nodes (10): EnclaveDelegate.cs, Microsoft.Data.SqlClient, using(), GetUintBytes(), EnclaveDelegate(), ComputeQueryStringHash(), ColumnEncryptionKeyInfo(), GetDecryptedKeysToBeSentToEnclave() (+2 more)

### Community 139 - "Entity (Community 139)"
Cohesion: 0.20
Nodes (10): SqlColumnEncryptionCngProvider.cs, CreateRSACngProvider(), EncryptColumnEncryptionKey(), DecryptColumnEncryptionKey(), catch(), Microsoft.Data.SqlClient, SignColumnMasterKeyMetadata(), SqlColumnEncryptionCngProvider (+2 more)

### Community 141 - "Entity (Community 141)"
Cohesion: 0.38
Nodes (10): SqlAeadAes256CbcHmac256Algorithm.cs, SqlAeadAes256CbcHmac256Algorithm.cs, Microsoft.Data.SqlClient, using(), SqlAeadAes256CbcHmac256Algorithm(), PrepareAuthenticationTag(), if(), EncryptData() (+2 more)

### Community 140 - "Entity (Community 140)"
Cohesion: 0.20
Nodes (10): SqlConfigurableRetryFactory.cs, lock(), Microsoft.Data.SqlClient, IsRetriable(), CreateIncrementalRetryProvider(), CreateExponentialRetryProvider(), CreateFixedRetryProvider(), ArgumentNullException() (+2 more)

### Community 149 - "Entity (Community 149)"
Cohesion: 0.22
Nodes (9): SqlSer.cs, if(), BinarySerializeSerializer(), DontDoIt(), GetSerializer(), Deserialize(), Serialize(), SizeInBytes() (+1 more)

### Community 147 - "Entity (Community 147)"
Cohesion: 0.42
Nodes (9): DbConnectionPoolAuthenticationContextKey.cs, DbConnectionPoolAuthenticationContextKey.cs, Equals(), GetHashCode(), Microsoft.Data.SqlClient.ConnectionPool, DbConnectionPoolAuthenticationContextKey(), ComputeHashCode(), return() (+1 more)

### Community 148 - "Entity (Community 148)"
Cohesion: 0.42
Nodes (9): SqlConnectionEncryptOptionConverter.cs, SqlConnectionEncryptOptionConverter.cs, SqlConnectionEncryptOptionConverter, Microsoft.Data.SqlClient, if(), ConvertTo(), CanConvertFrom(), CanConvertTo() (+1 more)

### Community 146 - "Entity (Community 146)"
Cohesion: 0.42
Nodes (9): ColumnMasterKeyMetadata.cs, ColumnMasterKeyMetadata.cs, using(), Sign(), Verify(), Sha256Hash, Microsoft.Data.SqlClient.AlwaysEncrypted, Dispose() (+1 more)

### Community 145 - "Entity (Community 145)"
Cohesion: 0.42
Nodes (9): SessionData.cs, SessionData.cs, SessionData(), Reset(), if(), Microsoft.Data.SqlClient.Connection, foreach(), AssertUnrecoverableStateCountIsCorrect() (+1 more)

### Community 143 - "Entity (Community 143)"
Cohesion: 0.42
Nodes (9): SqlEnclaveSession.cs, SqlEnclaveSession.cs, for(), EnclaveSessionParameters(), SqlEnclaveSession(), Microsoft.Data.SqlClient, if(), GetSessionKey() (+1 more)

### Community 144 - "Entity (Community 144)"
Cohesion: 0.42
Nodes (9): SqlClientLogger.cs, SqlClientLogger.cs, LogWarning(), SqlClientLogger, Microsoft.Data.SqlClient, if(), LogAssert(), LogInfo() (+1 more)

### Community 142 - "Entity (Community 142)"
Cohesion: 0.22
Nodes (9): SqlBatch.cs, ExecuteReaderAsync(), SqlBatch(), Dispose(), CheckDisposed(), SetupBatchCommandExecute(), if(), ValidateExecuteCommandBehavior() (+1 more)

### Community 161 - "Entity (Community 161)"
Cohesion: 0.46
Nodes (8): SqlColumnEncryptionKeyStoreProvider.cs, SqlColumnEncryptionKeyStoreProvider.cs, Microsoft.Data.SqlClient, EncryptColumnEncryptionKey(), DecryptColumnEncryptionKey(), SignColumnMasterKeyMetadata(), VerifyColumnMasterKeyMetadata(), NotImplementedException()

### Community 153 - "Entity (Community 153)"
Cohesion: 0.46
Nodes (8): TdsParserStateObjectFactory.windows.cs, TdsParserStateObjectFactory.windows.cs, TdsParserStateObjectManaged(), TdsParserStateObjectNative(), if(), CreateTdsParserStateObject(), Microsoft.Data.SqlClient, CreateSessionObject()

### Community 154 - "Entity (Community 154)"
Cohesion: 0.25
Nodes (8): SqlConnectionPoolGroupProviderInfo.cs, FailoverCheck(), FailoverPermissionDemand(), Microsoft.Data.SqlClient.ConnectionPool, return(), SqlConnectionPoolGroupProviderInfo(), AliasCheck(), CreateFailoverPermission()

### Community 155 - "Entity (Community 155)"
Cohesion: 0.46
Nodes (8): ColumnEncryptionKeyInfo.cs, ColumnEncryptionKeyInfo.cs, catch(), SerializeToBuffer(), Microsoft.Data.SqlClient, ColumnEncryptionKeyInfo(), GetLengthForSerialization(), if()

### Community 158 - "Entity (Community 158)"
Cohesion: 0.25
Nodes (8): SniMarsConnection.netcore.cs, lock(), ReturnPacket(), if(), while(), HandleReceiveError(), using(), SniMarsConnection()

### Community 159 - "Entity (Community 159)"
Cohesion: 0.25
Nodes (8): SqlRetryIntervalBaseEnumerator.cs, GetNextInterval(), Clone(), Dispose(), NotImplementedException(), Microsoft.Data.SqlClient, MoveNext(), Reset()

### Community 156 - "Entity (Community 156)"
Cohesion: 0.46
Nodes (8): AppConfigManager.cs, AppConfigManager.cs, return(), nameof(), catch(), if(), Microsoft.Data.SqlClient, SqlConfigurableRetryConnectionSection

### Community 152 - "Entity (Community 152)"
Cohesion: 0.25
Nodes (8): SqlConfigurableRetryLogicLoader.cs, InvalidOperationException(), nameof(), LoadType(), AssignProviders(), for(), catch(), if()

### Community 160 - "Entity (Community 160)"
Cohesion: 0.46
Nodes (8): SqlErrorCollection.cs, SqlErrorCollection.cs, CopyTo(), Microsoft.Data.SqlClient, ListBindable(), SqlErrorCollection(), Add(), GetEnumerator()

### Community 157 - "Entity (Community 157)"
Cohesion: 0.25
Nodes (8): SqlSequentialTextReader.cs, catch(), SetClosed(), DecodeBytesToChars(), Convert(), if(), Read(), ValidateReadParameters()

### Community 169 - "Entity (Community 169)"
Cohesion: 0.25
Nodes (8): SqlCommand.Scalar.cs, ExecuteScalarAsync(), ExecuteScalar(), CompleteExecuteScalar(), WriteEndExecuteEvent(), ExecuteScalarBatchAsync(), Microsoft.Data.SqlClient, WriteBeginExecuteEvent()

### Community 167 - "Entity (Community 167)"
Cohesion: 0.25
Nodes (8): SqlColumnEncryptionCertificateStoreProvider.cs, VerifyColumnMasterKeyMetadata(), EncryptColumnEncryptionKey(), GetCertificatePrivateKeyByPath(), Microsoft.Data.SqlClient, DecryptColumnEncryptionKey(), SqlColumnEncryptionCertificateStoreProvider, SignColumnMasterKeyMetadata()

### Community 165 - "Entity (Community 165)"
Cohesion: 0.46
Nodes (8): NegotiateSspiContextProvider.cs, NegotiateSspiContextProvider.cs, if(), Dispose(), Microsoft.Data.SqlClient, GetNegotiateAuthenticationForParams(), GenerateContext(), nameof()

### Community 164 - "Entity (Community 164)"
Cohesion: 0.46
Nodes (8): SqlSymmetricKeyCache.cs, SqlSymmetricKeyCache.cs, GetKey(), SqlSymmetricKeyCache(), catch(), Microsoft.Data.SqlClient, if(), GetInstance()

### Community 162 - "Entity (Community 162)"
Cohesion: 0.25
Nodes (8): EnclaveProviderBase.cs, AddEnclaveSessionToCache(), GetEnclaveSessionFromCache(), GetEnclaveSessionHelper(), Microsoft.Data.SqlClient, InvalidateEnclaveSessionHelper(), using(), UpdateEnclaveSessionLockStatus()

### Community 170 - "Entity (Community 170)"
Cohesion: 0.46
Nodes (8): SqlConfigurableRetryLogicManager.cs, SqlConfigurableRetryLogicManager.cs, SqlConfigurableRetryLogicManager(), catch(), nameof(), if(), SqlConfigurableRetryLogicLoader(), Microsoft.Data.SqlClient

### Community 166 - "Entity (Community 166)"
Cohesion: 0.25
Nodes (8): ConnectionPoolSlots.cs, Keep(), Dispose(), Microsoft.Data.SqlClient.ConnectionPool, Reservation(), TryRemove(), InvalidOperationException(), ConnectionPoolSlots()

### Community 168 - "Entity (Community 168)"
Cohesion: 0.46
Nodes (8): SensitivityClassification.cs, SensitivityClassification.cs, InformationType(), Microsoft.Data.SqlClient.DataClassification, SensitivityClassification(), ColumnSensitivity(), Label(), SensitivityProperty()

### Community 163 - "Entity (Community 163)"
Cohesion: 0.46
Nodes (8): DbConnectionPoolAuthenticationContext.cs, DbConnectionPoolAuthenticationContext.cs, ChooseAuthenticationContextToUpdate(), DbConnectionPoolAuthenticationContext(), LockToUpdate(), Microsoft.Data.SqlClient.ConnectionPool, ReleaseLockToUpdate(), return()

### Community 150 - "Entity (Community 150)"
Cohesion: 0.46
Nodes (8): SqlRetryLogicBase.cs, SqlRetryLogicBase.cs, Clone(), TryNextInterval(), RetryCondition(), NotImplementedException(), Reset(), Microsoft.Data.SqlClient

### Community 151 - "Entity (Community 151)"
Cohesion: 0.25
Nodes (8): Packet.cs, SetCreatedByImpl(), return(), if(), CheckDisposedImpl(), GetDataLengthFromHeader(), CheckDisposed(), ThrowDisposed()

### Community 180 - "Entity (Community 180)"
Cohesion: 0.29
Nodes (7): UserAgent.cs, UserAgent(), Truncate(), Clean(), Microsoft.Data.SqlClient, foreach(), Build()

### Community 179 - "Entity (Community 179)"
Cohesion: 0.52
Nodes (7): AeadAes256CbcHmac256Factory.cs, AeadAes256CbcHmac256Factory.cs, new(), Microsoft.Data.SqlClient.AlwaysEncrypted, AeadAes256CbcHmac256Factory(), if(), Create()

### Community 191 - "Entity (Community 191)"
Cohesion: 0.29
Nodes (7): SqlCommand.Batch.cs, if(), BuildExecuteSql(), ClearBatchCommand(), for(), GetBatchCommand(), GetCurrentBatchIndex()

### Community 186 - "Entity (Community 186)"
Cohesion: 0.52
Nodes (7): SniNetworkStream.netcore.cs, SniNetworkStream.netcore.cs, WriteAsync(), SniNetworkStream(), ReadAsync(), catch(), Microsoft.Data.SqlClient.ManagedSni

### Community 192 - "Entity (Community 192)"
Cohesion: 0.52
Nodes (7): SqlDependencyUtils.AssemblyLoadContext.netcore.cs, SqlDependencyUtils.AssemblyLoadContext.netcore.cs, SubscribeToAssemblyLoadContextUnload(), SqlDependencyPerAppDomainDispatcher, Microsoft.Data.SqlClient, SqlDependencyPerAppDomainDispatcher_Unloading(), UnloadEventHandler()

### Community 190 - "Entity (Community 190)"
Cohesion: 0.29
Nodes (7): SqlNormalizer.cs, if(), foreach(), FlipAllBits(), DeNormalize(), Normalize(), SetValue()

### Community 178 - "Entity (Community 178)"
Cohesion: 0.29
Nodes (7): ActiveDirectoryAuthenticationTimeoutRetryHelper.cs, using(), switch(), Microsoft.Data.SqlClient, IsConnectTimeoutError(), GetTokenHash(), CanRetryWithSqlException()

### Community 188 - "Entity (Community 188)"
Cohesion: 0.52
Nodes (7): SniSslStream.netcore.cs, SniSslStream.netcore.cs, ReadAsync(), WriteAsync(), Microsoft.Data.SqlClient.ManagedSni, catch(), SniSslStream()

### Community 187 - "Entity (Community 187)"
Cohesion: 0.29
Nodes (7): SqlClientMetrics.cs, RemovePerformanceCounters(), IncrementPlatformSpecificCounter(), if(), EnablePerformanceCounters(), EnableEventCounters(), DecrementPlatformSpecificCounter()

### Community 182 - "Entity (Community 182)"
Cohesion: 0.29
Nodes (7): MetadataUtilsSmi.cs, if(), foreach(), switch(), SmiExtendedMetaData(), IsAnsiType(), DetermineExtendedTypeCodeFromType()

### Community 185 - "Entity (Community 185)"
Cohesion: 0.29
Nodes (7): TdsValueSetter.cs, TdsValueSetter(), SetBytesLength(), SetBytesNoOffsetHandling(), SetBytes(), if(), CheckSettingOffset()

### Community 183 - "Entity (Community 183)"
Cohesion: 0.29
Nodes (7): SqlSequentialStream.cs, catch(), BeginRead(), ValidateReadParameters(), SetClosed(), if(), EndRead()

### Community 181 - "Entity (Community 181)"
Cohesion: 0.52
Nodes (7): DisposableTemporaryOnStack.cs, DisposableTemporaryOnStack.cs, Dispose(), Take(), Set(), if(), Microsoft.Data.SqlClient

### Community 189 - "Entity (Community 189)"
Cohesion: 0.52
Nodes (7): SqlColumnEncryptionEnclaveProvider.cs, SqlColumnEncryptionEnclaveProvider.cs, GetAttestationParameters(), CreateEnclaveSession(), GetEnclaveSession(), InvalidateEnclaveSession(), Microsoft.Data.SqlClient

### Community 184 - "Entity (Community 184)"
Cohesion: 0.29
Nodes (7): SqlBatchCommand.cs, switch(), Microsoft.Data.SqlClient, SetRecordAffected(), foreach(), for(), CreateParameter()

### Community 177 - "Entity (Community 177)"
Cohesion: 0.29
Nodes (7): TdsParserSafeHandles.windows.cs, WriteDispatcher(), SNIPacket(), Microsoft.Data.SqlClient, SNILoadHandle(), ReadDispatcher(), catch()

### Community 176 - "Entity (Community 176)"
Cohesion: 0.52
Nodes (7): ConcurrentQueueSemaphore.netcore.cs, ConcurrentQueueSemaphore.netcore.cs, Release(), WaitAsync(), Microsoft.Data.SqlClient.ManagedSni, ConcurrentQueueSemaphore(), if()

### Community 198 - "Entity (Community 198)"
Cohesion: 0.52
Nodes (7): LocalAppContextSwitches.cs, LocalAppContextSwitches.cs, if(), LocalAppContextSwitches(), catch(), AcquireAndReturn(), Microsoft.Data.SqlClient

### Community 193 - "Entity (Community 193)"
Cohesion: 0.29
Nodes (7): SqlConnectionEncryptOption.cs, ToString(), bool(), TryParse(), switch(), Equals(), Microsoft.Data.SqlClient

### Community 199 - "Entity (Community 199)"
Cohesion: 0.29
Nodes (7): TdsParserSessionPool.cs, Microsoft.Data.SqlClient, TraceString(), Deactivate(), Dispose(), GetSession(), using()

### Community 194 - "Entity (Community 194)"
Cohesion: 0.29
Nodes (7): SniNpHandle.netcore.cs, lock(), catch(), using(), ReportErrorAndReleasePacket(), ReturnPacket(), if()

### Community 196 - "Entity (Community 196)"
Cohesion: 0.29
Nodes (7): DbConnectionPoolIdentity.cs, Microsoft.Data.SqlClient.ConnectionPool, using(), GetCurrentWindowsIdentity(), GetCurrent(), Equals(), GetHashCode()

### Community 197 - "Entity (Community 197)"
Cohesion: 0.52
Nodes (7): SqlError.cs, SqlError.cs, SqlError(), if(), Microsoft.Data.SqlClient, typeof(), ToString()

### Community 195 - "Entity (Community 195)"
Cohesion: 0.29
Nodes (7): SqlCollation.cs, FirstSupportedCollationVersion(), switch(), Microsoft.Data.SqlClient, FromLCIDAndSort(), TraceString(), unchecked()

### Community 173 - "Entity (Community 173)"
Cohesion: 0.29
Nodes (7): SspiContextProvider.cs, WriteSSPIContext(), Microsoft.Data.SqlClient, catch(), CreateAuthParams(), SspiContextProvider(), RunGenerateSspiClientContext()

### Community 172 - "Entity (Community 172)"
Cohesion: 0.29
Nodes (7): SniCommon.netcore.cs, TimeoutException(), ValidateSslServerCertificate(), Microsoft.Data.SqlClient.ManagedSni, foreach(), catch(), SniCommon

### Community 175 - "Entity (Community 175)"
Cohesion: 0.29
Nodes (7): SqlDiagnosticListener.cs, Dispose(), if(), SqlDiagnosticListener_UnloadingAssemblyLoadContext(), Write(), WriteTransactionRollbackError(), WriteTransactionRollbackBefore()

### Community 174 - "Entity (Community 174)"
Cohesion: 0.52
Nodes (7): PacketHandle.netcore.windows.cs, PacketHandle.netcore.windows.cs, FromNativePacket(), Microsoft.Data.SqlClient, FromManagedPacket(), PacketHandle(), FromNativePointer()

### Community 171 - "Entity (Community 171)"
Cohesion: 0.29
Nodes (7): SqlCommand.Scalar.cs, ExecuteReaderAsync(), catch(), ExecuteScalarAsyncInternal(), if(), while(), ExecuteScalarUntilEndAsync()

### Community 203 - "Entity (Community 203)"
Cohesion: 0.60
Nodes (6): SqlClientCommandError.cs, SqlClientCommandError.cs, SqlClientCommandError(), for(), Microsoft.Data.SqlClient.Diagnostics, GetEnumerator()

### Community 202 - "Entity (Community 202)"
Cohesion: 0.33
Nodes (6): SqlDelegatedTransaction.cs, if(), lock(), Guid(), catch(), ValidateActiveOnConnection()

### Community 205 - "Entity (Community 205)"
Cohesion: 0.33
Nodes (6): LocalDbApi.windows.cs, if(), CreateLocalDbException(), DemandLocalDbPermissions(), lock(), InstanceInfo()

### Community 212 - "Entity (Community 212)"
Cohesion: 0.33
Nodes (6): TransactedConnectionPool.cs, Dispose(), TransactionEnded(), Microsoft.Data.SqlClient.ConnectionPool, TransactedConnectionList(), PutTransactedObject()

### Community 209 - "Entity (Community 209)"
Cohesion: 0.60
Nodes (6): SqlDbColumn.cs, SqlDbColumn.cs, Populate(), if(), Microsoft.Data.SqlClient, SqlDbColumn()

### Community 207 - "Entity (Community 207)"
Cohesion: 0.33
Nodes (6): ConnectionPoolSlots.cs, cleanupCallback(), for(), ArgumentOutOfRangeException(), if(), ReleaseReservation()

### Community 211 - "Entity (Community 211)"
Cohesion: 0.60
Nodes (6): SqlClientTransactionCommitAfter.cs, SqlClientTransactionCommitAfter.cs, SqlClientTransactionCommitAfter(), Microsoft.Data.SqlClient.Diagnostics, GetEnumerator(), for()

### Community 206 - "Entity (Community 206)"
Cohesion: 0.60
Nodes (6): TdsParserStateObjectFactory.unix.cs, TdsParserStateObjectFactory.unix.cs, TdsParserStateObjectManaged(), Microsoft.Data.SqlClient, CreateTdsParserStateObject(), CreateSessionObject()

### Community 210 - "Entity (Community 210)"
Cohesion: 0.60
Nodes (6): SqlClientCommandAfter.cs, SqlClientCommandAfter.cs, Microsoft.Data.SqlClient.Diagnostics, for(), SqlClientCommandAfter(), GetEnumerator()

### Community 208 - "Entity (Community 208)"
Cohesion: 0.33
Nodes (6): TdsParserSessionPool.cs, if(), lock(), TdsParserSessionPool(), PutSession(), for()

### Community 204 - "Entity (Community 204)"
Cohesion: 0.60
Nodes (6): LocalDbApi.unix.cs, LocalDbApi.unix.cs, GetLocalDbInstanceNameFromServerName(), PlatformNotSupportedException(), GetLocalDbMessage(), Microsoft.Data.SqlClient.LocalDb

### Community 200 - "Entity (Community 200)"
Cohesion: 0.33
Nodes (6): SqlColumnEncryptionCngProvider.cs, ValidateEncryptionAlgorithm(), PlatformNotSupportedException(), if(), ValidateNonEmptyKeyPath(), GetCngProviderAndKeyId()

### Community 213 - "Entity (Community 213)"
Cohesion: 0.60
Nodes (6): SqlClientTransactionRollbackError.cs, SqlClientTransactionRollbackError.cs, Microsoft.Data.SqlClient.Diagnostics, GetEnumerator(), SqlClientTransactionRollbackError(), for()

### Community 201 - "Entity (Community 201)"
Cohesion: 0.60
Nodes (6): SqlClientTransactionRollbackAfter.cs, SqlClientTransactionRollbackAfter.cs, for(), Microsoft.Data.SqlClient.Diagnostics, GetEnumerator(), SqlClientTransactionRollbackAfter()

### Community 230 - "Entity (Community 230)"
Cohesion: 0.60
Nodes (6): SessionHandle.netcore.windows.cs, SessionHandle.netcore.windows.cs, FromNativeHandle(), FromManagedSession(), Microsoft.Data.SqlClient, SessionHandle()

### Community 232 - "Entity (Community 232)"
Cohesion: 0.33
Nodes (6): SqlBulkCopyColumnOrderHintCollection.cs, Add(), ArgumentNullException(), RegisterColumnName(), UnregisterColumnName(), if()

### Community 234 - "Entity (Community 234)"
Cohesion: 0.60
Nodes (6): PacketHandle.netfx.cs, PacketHandle.netfx.cs, PacketHandle(), FromNativePacket(), FromNativePointer(), Microsoft.Data.SqlClient

### Community 229 - "Entity (Community 229)"
Cohesion: 0.33
Nodes (6): SqlRetryLogic.cs, RetryCondition(), Clone(), Reset(), TryNextInterval(), Microsoft.Data.SqlClient

### Community 228 - "Entity (Community 228)"
Cohesion: 0.60
Nodes (6): SqlClientTransactionCommitBefore.cs, SqlClientTransactionCommitBefore.cs, GetEnumerator(), for(), Microsoft.Data.SqlClient.Diagnostics, SqlClientTransactionCommitBefore()

### Community 233 - "Entity (Community 233)"
Cohesion: 0.60
Nodes (6): SqlClientPermissionAttribute.netfx.cs, SqlClientPermissionAttribute.netfx.cs, SqlClientPermission(), SqlClientPermissionAttribute(), CreatePermission(), Microsoft.Data.SqlClient

### Community 225 - "Entity (Community 225)"
Cohesion: 0.33
Nodes (6): SqlStatistics.cs, if(), ValidateCopyToArguments(), ValueSqlStatisticsScope(), foreach(), ArgumentException()

### Community 235 - "Entity (Community 235)"
Cohesion: 0.33
Nodes (6): EnclaveDelegate.cs, catch(), CombineByteArrays(), foreach(), RetryableEnclaveQueryExecutionException(), if()

### Community 226 - "Entity (Community 226)"
Cohesion: 0.60
Nodes (6): LocalDB.netcore.unix.cs, LocalDB.netcore.unix.cs, Microsoft.Data.SqlClient.ManagedSni, LocalDB, GetLocalDBConnectionString(), PlatformNotSupportedException()

### Community 227 - "Entity (Community 227)"
Cohesion: 0.60
Nodes (6): SqlClientCommandBefore.cs, SqlClientCommandBefore.cs, SqlClientCommandBefore(), for(), GetEnumerator(), Microsoft.Data.SqlClient.Diagnostics

### Community 231 - "Entity (Community 231)"
Cohesion: 0.33
Nodes (6): PoolPruner.cs, Dispose(), UpdateTimer(), Microsoft.Data.SqlClient.ConnectionPool, OnPruningCallback(), PoolPruner()

### Community 218 - "Entity (Community 218)"
Cohesion: 0.33
Nodes (6): ConnectionPoolKey.cs, Clone(), GetHashCode(), Equals(), return(), Microsoft.Data.SqlClient.ConnectionPool

### Community 223 - "Entity (Community 223)"
Cohesion: 0.33
Nodes (6): EncryptedColumnEncryptionKeyParameters.cs, Decrypt(), Dispose(), Encrypt(), EncryptedColumnEncryptionKeyParameters(), Microsoft.Data.SqlClient.AlwaysEncrypted

### Community 222 - "Entity (Community 222)"
Cohesion: 0.33
Nodes (6): DiagnosticTransactionScope.cs, SetException(), Dispose(), Microsoft.Data.SqlClient.Diagnostics, CreateTransactionRollbackScope(), CreateTransactionCommitScope()

### Community 220 - "Entity (Community 220)"
Cohesion: 0.60
Nodes (6): SqlClientConnectionOpenBefore.cs, SqlClientConnectionOpenBefore.cs, GetEnumerator(), for(), SqlClientConnectionOpenBefore(), Microsoft.Data.SqlClient.Diagnostics

### Community 219 - "Entity (Community 219)"
Cohesion: 0.60
Nodes (6): SqlBulkCopyColumnOrderHint.cs, SqlBulkCopyColumnOrderHint.cs, Microsoft.Data.SqlClient, OnNameChanging(), if(), SqlBulkCopyColumnOrderHint()

### Community 221 - "Entity (Community 221)"
Cohesion: 0.33
Nodes (6): NativeSspiContextProvider.windows.cs, SSPIError(), Initialize(), Microsoft.Data.SqlClient, GenerateContext(), lock()

### Community 224 - "Entity (Community 224)"
Cohesion: 0.60
Nodes (6): LocalesHelper.cs, LocalesHelper.cs, GetCodePageByLcid(), TryGetCodePage(), Microsoft.Data.SqlClient, if()

### Community 217 - "Entity (Community 217)"
Cohesion: 0.60
Nodes (6): SqlClientConnectionCloseBefore.cs, SqlClientConnectionCloseBefore.cs, Microsoft.Data.SqlClient.Diagnostics, GetEnumerator(), for(), SqlClientConnectionCloseBefore()

### Community 215 - "Entity (Community 215)"
Cohesion: 0.60
Nodes (6): SqlEnclaveAttestationParameters.Crypto.cs, SqlEnclaveAttestationParameters.Crypto.cs, GetInput(), SqlEnclaveAttestationParameters(), Microsoft.Data.SqlClient, if()

### Community 216 - "Entity (Community 216)"
Cohesion: 0.60
Nodes (6): DbConnectionPoolOptions.cs, DbConnectionPoolOptions.cs, DbConnectionPoolGroupOptions(), Microsoft.Data.SqlClient.ConnectionPool, ArgumentOutOfRangeException(), if()

### Community 214 - "Entity (Community 214)"
Cohesion: 0.60
Nodes (6): SqlInfoMessageEvent.cs, SqlInfoMessageEvent.cs, SqlInfoMessageEventArgs(), ShouldSerializeErrors(), ToString(), Microsoft.Data.SqlClient

### Community 242 - "Entity (Community 242)"
Cohesion: 0.33
Nodes (6): SqlConnectionOptions.Debug.cs, Microsoft.Data.SqlClient, switch(), SplitConnectionString(), ParseComparison(), catch()

### Community 245 - "Entity (Community 245)"
Cohesion: 0.33
Nodes (6): SqlAuthenticationProviderManager.cs, SetProvider(), catch(), SqlAuthenticationProviderManager(), nameof(), if()

### Community 239 - "Entity (Community 239)"
Cohesion: 0.33
Nodes (6): TdsRecordBufferSetter.cs, TdsRecordBufferSetter(), CheckSettingColumn(), CheckWritingToColumn(), SkipPossibleDefaultedColumns(), if()

### Community 246 - "Entity (Community 246)"
Cohesion: 0.60
Nodes (6): SqlClientTransactionCommitError.cs, SqlClientTransactionCommitError.cs, Microsoft.Data.SqlClient.Diagnostics, GetEnumerator(), SqlClientTransactionCommitError(), for()

### Community 247 - "Entity (Community 247)"
Cohesion: 0.60
Nodes (6): TdsParameterSetter.cs, TdsParameterSetter.cs, TdsParameterSetter(), Microsoft.Data.SqlClient, SetDBNull(), GetTypedGetterSetter()

### Community 241 - "Entity (Community 241)"
Cohesion: 0.60
Nodes (6): SqlClientConnectionOpenAfter.cs, SqlClientConnectionOpenAfter.cs, SqlClientConnectionOpenAfter(), Microsoft.Data.SqlClient.Diagnostics, GetEnumerator(), for()

### Community 237 - "Entity (Community 237)"
Cohesion: 0.33
Nodes (6): SQLFallbackDNSCache.cs, GetDNSInfo(), AddDNSInfo(), SQLFallbackDNSCache(), Microsoft.Data.SqlClient, return()

### Community 240 - "Entity (Community 240)"
Cohesion: 0.60
Nodes (6): SqlClientConnectionCloseAfter.cs, SqlClientConnectionCloseAfter.cs, GetEnumerator(), SqlClientConnectionCloseAfter(), for(), Microsoft.Data.SqlClient.Diagnostics

### Community 236 - "Entity (Community 236)"
Cohesion: 0.60
Nodes (6): SqlClientConnectionCloseError.cs, SqlClientConnectionCloseError.cs, Microsoft.Data.SqlClient.Diagnostics, for(), SqlClientConnectionCloseError(), GetEnumerator()

### Community 243 - "Entity (Community 243)"
Cohesion: 0.60
Nodes (6): SqlClientTransactionRollbackBefore.cs, SqlClientTransactionRollbackBefore.cs, for(), GetEnumerator(), Microsoft.Data.SqlClient.Diagnostics, SqlClientTransactionRollbackBefore()

### Community 244 - "Entity (Community 244)"
Cohesion: 0.33
Nodes (6): SqlColumnEncryptionCspProvider.cs, PlatformNotSupportedException(), if(), GetCspProviderAndKeyName(), ValidateNonEmptyCSPKeyPath(), ValidateEncryptionAlgorithm()

### Community 238 - "Entity (Community 238)"
Cohesion: 0.60
Nodes (6): SqlClientConnectionOpenError.cs, SqlClientConnectionOpenError.cs, GetEnumerator(), for(), SqlClientConnectionOpenError(), Microsoft.Data.SqlClient.Diagnostics

### Community 261 - "Entity (Community 261)"
Cohesion: 0.40
Nodes (5): SniCommon.netcore.cs, using(), if(), GetDnsIpAddresses(), ReportSNIError()

### Community 262 - "Entity (Community 262)"
Cohesion: 0.40
Nodes (5): SqlConnectionTimeoutErrorInternal.cs, SetAndBeginPhase(), if(), SqlConnectionTimeoutErrorInternal(), for()

### Community 275 - "Entity (Community 275)"
Cohesion: 0.40
Nodes (5): DiagnosticScope.cs, SetException(), Microsoft.Data.SqlClient.Diagnostics, Dispose(), CreateCommandScope()

### Community 276 - "Entity (Community 276)"
Cohesion: 0.70
Nodes (5): SqlClientSymmetricKey.cs, SqlClientSymmetricKey.cs, Microsoft.Data.SqlClient, SqlClientSymmetricKey(), if()

### Community 274 - "Entity (Community 274)"
Cohesion: 0.70
Nodes (5): SqlClientEncryptionAlgorithm.cs, SqlClientEncryptionAlgorithm.cs, EncryptData(), DecryptData(), Microsoft.Data.SqlClient

### Community 273 - "Entity (Community 273)"
Cohesion: 0.70
Nodes (5): SqlClientEventSource.cs, SqlClientEventSource.cs, Microsoft.Data.SqlClient, if(), SqlClientDiagnostics()

### Community 272 - "Entity (Community 272)"
Cohesion: 0.70
Nodes (5): ServerInfo.cs, ServerInfo.cs, SetDerivedNames(), ServerInfo(), Microsoft.Data.SqlClient.Connection

### Community 271 - "Entity (Community 271)"
Cohesion: 0.70
Nodes (5): SqlRowUpdatedEvent.cs, SqlRowUpdatedEvent.cs, SqlRowUpdatedEventArgs(), Microsoft.Data.SqlClient, return()

### Community 270 - "Entity (Community 270)"
Cohesion: 0.40
Nodes (5): SQLFallbackDNSCache.cs, SQLDNSInfo(), IsDuplicate(), if(), DeleteDNSInfo()

### Community 269 - "Entity (Community 269)"
Cohesion: 0.70
Nodes (5): SniSmuxHeader.netcore.cs, SniSmuxHeader.netcore.cs, Write(), Microsoft.Data.SqlClient.ManagedSni, Read()

### Community 268 - "Entity (Community 268)"
Cohesion: 0.70
Nodes (5): SqlAeadAes256CbcHmac256EncryptionKey.cs, SqlAeadAes256CbcHmac256EncryptionKey.cs, SqlAeadAes256CbcHmac256EncryptionKey(), Microsoft.Data.SqlClient, if()

### Community 267 - "Entity (Community 267)"
Cohesion: 0.40
Nodes (5): IdleConnectionChannel.cs, TryWrite(), TryRead(), Microsoft.Data.SqlClient.ConnectionPool, IdleConnectionChannel()

### Community 266 - "Entity (Community 266)"
Cohesion: 0.70
Nodes (5): SqlDependencyUtils.AppDomain.netcore.cs, SqlDependencyUtils.AppDomain.netcore.cs, SubscribeToAppDomainUnload(), SqlDependencyPerAppDomainDispatcher, Microsoft.Data.SqlClient

### Community 265 - "Entity (Community 265)"
Cohesion: 0.40
Nodes (5): SqlAuthenticationParametersBuilder.cs, WithUserId(), WithConnectionId(), WithAuthenticationTimeout(), Microsoft.Data.SqlClient

### Community 264 - "Entity (Community 264)"
Cohesion: 0.70
Nodes (5): SqlBulkCopyColumnMapping.cs, SqlBulkCopyColumnMapping.cs, SqlBulkCopyColumnMapping(), Microsoft.Data.SqlClient, if()

### Community 263 - "Entity (Community 263)"
Cohesion: 0.70
Nodes (5): SniError.netcore.cs, SniError.netcore.cs, SniError(), if(), Microsoft.Data.SqlClient.ManagedSni

### Community 260 - "Entity (Community 260)"
Cohesion: 0.40
Nodes (5): SqlConnectionEncryptOption.cs, Parse(), if(), GetHashCode(), SqlConnectionEncryptOption()

### Community 259 - "Entity (Community 259)"
Cohesion: 0.40
Nodes (5): SqlColumnEncryptionCertificateStoreProvider.cs, ValidateCertificatePathLength(), if(), GetCertificatePrivateKey(), ValidateEncryptionAlgorithm()

### Community 252 - "Entity (Community 252)"
Cohesion: 0.40
Nodes (5): DbConnectionPoolIdentity.cs, DbConnectionPoolIdentity(), GetCurrentNative(), if(), GetCurrentManaged()

### Community 254 - "Entity (Community 254)"
Cohesion: 0.40
Nodes (5): SqlAppContextSwitchManager.netcore.cs, Microsoft.Data.SqlClient, for(), ApplyContextSwitches(), catch()

### Community 255 - "Entity (Community 255)"
Cohesion: 0.40
Nodes (5): SniPhysicalHandle.netcore.cs, SniPhysicalHandle(), GetStackParts(), RentPacket(), Microsoft.Data.SqlClient.ManagedSni

### Community 256 - "Entity (Community 256)"
Cohesion: 0.70
Nodes (5): PacketHandle.netcore.unix.cs, PacketHandle.netcore.unix.cs, Microsoft.Data.SqlClient, PacketHandle(), FromManagedPacket()

### Community 249 - "Entity (Community 249)"
Cohesion: 0.70
Nodes (5): SessionHandle.netfx.cs, SessionHandle.netfx.cs, FromNativeHandle(), Microsoft.Data.SqlClient, SessionHandle()

### Community 258 - "Entity (Community 258)"
Cohesion: 0.70
Nodes (5): AlwaysEncryptedEnclaveProviderUtils.cs, AlwaysEncryptedEnclaveProviderUtils.cs, Microsoft.Data.SqlClient, EnclavePublicKey(), EnclaveDiffieHellmanInfo()

### Community 251 - "Entity (Community 251)"
Cohesion: 0.40
Nodes (5): SqlConfigurableRetryFactory.cs, if(), foreach(), TransientErrorsCondition(), SqlRetryLogicProvider()

### Community 257 - "Entity (Community 257)"
Cohesion: 0.40
Nodes (5): SqlCommandSet.cs, foreach(), for(), if(), ValidateCommandBehavior()

### Community 253 - "Entity (Community 253)"
Cohesion: 0.70
Nodes (5): SessionHandle.netcore.unix.cs, SessionHandle.netcore.unix.cs, SessionHandle(), FromManagedSession(), Microsoft.Data.SqlClient

### Community 250 - "Entity (Community 250)"
Cohesion: 0.40
Nodes (5): SspiContextProvider.cs, GenerateContext(), if(), Initialize(), SSPIError()

### Community 248 - "Entity (Community 248)"
Cohesion: 0.40
Nodes (5): TdsParserSafeHandles.windows.cs, if(), return(), ReleaseHandle(), SNIHandle()

### Community 302 - "Entity (Community 302)"
Cohesion: 0.83
Nodes (4): SqlNotificationEventArgs.cs, SqlNotificationEventArgs.cs, Microsoft.Data.SqlClient, SqlNotificationEventArgs()

### Community 284 - "Entity (Community 284)"
Cohesion: 0.50
Nodes (4): ConnectionPoolKey.cs, ConnectionPoolKey(), CalculateHashCode(), if()

### Community 300 - "Entity (Community 300)"
Cohesion: 0.50
Nodes (4): SqlRecordBuffer.cs, if(), return(), ConvertXmlStringToByteArray()

### Community 299 - "Entity (Community 299)"
Cohesion: 0.83
Nodes (4): ISqlVector.cs, ISqlVector.cs, Microsoft.Data.SqlClient, ISqlVector

### Community 283 - "Entity (Community 283)"
Cohesion: 0.83
Nodes (4): IAppContextSwitchOverridesSection.cs, IAppContextSwitchOverridesSection.cs, Microsoft.Data.SqlClient, IAppContextSwitchOverridesSection

### Community 298 - "Entity (Community 298)"
Cohesion: 0.50
Nodes (4): SniPhysicalHandle.netcore.cs, for(), ReturnPacket(), if()

### Community 301 - "Entity (Community 301)"
Cohesion: 0.83
Nodes (4): SqlInfoMessageEventHandler.cs, SqlInfoMessageEventHandler.cs, Microsoft.Data.SqlClient, SqlInfoMessageEventHandler()

### Community 297 - "Entity (Community 297)"
Cohesion: 0.50
Nodes (4): SqlCachedBuffer.cs, AddByteOrderMark(), SqlCachedBuffer(), if()

### Community 296 - "Entity (Community 296)"
Cohesion: 0.50
Nodes (4): LocalDB.netcore.windows.cs, switch(), if(), using()

### Community 295 - "Entity (Community 295)"
Cohesion: 0.50
Nodes (4): DiagnosticTransactionScope.cs, switch(), if(), DiagnosticTransactionScope()

### Community 294 - "Entity (Community 294)"
Cohesion: 0.83
Nodes (4): SqlRetryingEventArgs.cs, SqlRetryingEventArgs.cs, SqlRetryingEventArgs(), Microsoft.Data.SqlClient

### Community 293 - "Entity (Community 293)"
Cohesion: 0.50
Nodes (4): SqlRetryLogic.cs, SqlRetryLogic(), nameof(), if()

### Community 292 - "Entity (Community 292)"
Cohesion: 0.83
Nodes (4): RowsCopiedEventArgs.cs, RowsCopiedEventArgs.cs, SqlRowsCopiedEventArgs(), Microsoft.Data.SqlClient

### Community 291 - "Entity (Community 291)"
Cohesion: 0.83
Nodes (4): SspiAuthenticationParameters.cs, SspiAuthenticationParameters.cs, SspiAuthenticationParameters(), Microsoft.Data.SqlClient

### Community 290 - "Entity (Community 290)"
Cohesion: 0.83
Nodes (4): RowsCopiedEventHandler.cs, RowsCopiedEventHandler.cs, SqlRowsCopiedEventHandler(), Microsoft.Data.SqlClient

### Community 289 - "Entity (Community 289)"
Cohesion: 0.50
Nodes (4): DiagnosticScope.cs, switch(), if(), DiagnosticScope()

### Community 288 - "Entity (Community 288)"
Cohesion: 0.83
Nodes (4): BufferWriterExtensions.netfx.cs, BufferWriterExtensions.netfx.cs, Microsoft.Data.SqlClient.Utilities, GetBytes()

### Community 287 - "Entity (Community 287)"
Cohesion: 0.83
Nodes (4): ObjectPools.cs, ObjectPools.cs, new(), Microsoft.Data.SqlClient.Utilities

### Community 286 - "Entity (Community 286)"
Cohesion: 0.83
Nodes (4): SqlRowUpdatingEventHandler.cs, SqlRowUpdatingEventHandler.cs, Microsoft.Data.SqlClient, SqlRowUpdatingEventHandler()

### Community 285 - "Entity (Community 285)"
Cohesion: 0.83
Nodes (4): EncryptionAlgorithmFactory.cs, EncryptionAlgorithmFactory.cs, Create(), Microsoft.Data.SqlClient.AlwaysEncrypted

### Community 282 - "Entity (Community 282)"
Cohesion: 0.83
Nodes (4): EncryptionAlgorithmFactoryList.cs, EncryptionAlgorithmFactoryList.cs, Microsoft.Data.SqlClient.AlwaysEncrypted, GetAlgorithm()

### Community 281 - "Entity (Community 281)"
Cohesion: 0.83
Nodes (4): DbConnectionPoolProviderInfo.cs, DbConnectionPoolProviderInfo.cs, Microsoft.Data.SqlClient.ConnectionPool, DbConnectionPoolProviderInfo

### Community 280 - "Entity (Community 280)"
Cohesion: 0.50
Nodes (4): SqlRetryIntervalBaseEnumerator.cs, Validate(), SqlRetryIntervalBaseEnumerator(), if()

### Community 279 - "Entity (Community 279)"
Cohesion: 0.83
Nodes (4): ISqlConfigurableRetryConnectionSection.cs, ISqlConfigurableRetryConnectionSection.cs, Microsoft.Data.SqlClient, ISqlConfigurableRetryConnectionSection

### Community 277 - "Entity (Community 277)"
Cohesion: 0.50
Nodes (4): SqlCollation.cs, Equals(), SqlCollation(), if()

### Community 278 - "Entity (Community 278)"
Cohesion: 0.83
Nodes (4): ISqlConfigurableRetryCommandSection.cs, ISqlConfigurableRetryCommandSection.cs, ISqlConfigurableRetryCommandSection, Microsoft.Data.SqlClient

### Community 321 - "Entity (Community 321)"
Cohesion: 0.50
Nodes (4): PoolPruner.cs, if(), lock(), DivideRoundingUp()

### Community 303 - "Entity (Community 303)"
Cohesion: 0.83
Nodes (4): EnclavePackage.cs, EnclavePackage.cs, Microsoft.Data.SqlClient, EnclavePackage()

### Community 323 - "Entity (Community 323)"
Cohesion: 0.50
Nodes (4): SqlAuthenticationParametersBuilder.cs, SqlAuthenticationParametersBuilder(), SqlAuthenticationParameters(), WithPassword()

### Community 312 - "Entity (Community 312)"
Cohesion: 0.50
Nodes (4): SqlConnectionOptions.Debug.cs, if(), foreach(), DebugTraceKeyValuePair()

### Community 313 - "Entity (Community 313)"
Cohesion: 0.50
Nodes (4): TransactedConnectionPool.cs, lock(), TransactedConnectionPool(), if()

### Community 314 - "Entity (Community 314)"
Cohesion: 0.83
Nodes (4): SmiXetterAccessMap.cs, SmiXetterAccessMap.cs, IsSetterAccessValid(), Microsoft.Data.SqlClient.Server

### Community 315 - "Entity (Community 315)"
Cohesion: 0.83
Nodes (4): DbConnectionPoolGroupProviderInfo.cs, DbConnectionPoolGroupProviderInfo.cs, Microsoft.Data.SqlClient.ConnectionPool, DbConnectionPoolGroupProviderInfo

### Community 318 - "Entity (Community 318)"
Cohesion: 0.50
Nodes (4): SqlBulkCopyColumnMappingCollection.cs, AssertWriteAccess(), Add(), if()

### Community 309 - "Entity (Community 309)"
Cohesion: 0.83
Nodes (4): SqlRowUpdatedEventHandler.cs, SqlRowUpdatedEventHandler.cs, SqlRowUpdatedEventHandler(), Microsoft.Data.SqlClient

### Community 305 - "Entity (Community 305)"
Cohesion: 0.83
Nodes (4): ParameterPeekAheadValue.cs, ParameterPeekAheadValue.cs, Microsoft.Data.SqlClient, ParameterPeekAheadValue

### Community 304 - "Entity (Community 304)"
Cohesion: 0.50
Nodes (4): SqlAppContextSwitchManager.netcore.cs, if(), ArgumentException(), ApplySwitchValues()

### Community 322 - "Entity (Community 322)"
Cohesion: 0.50
Nodes (4): SqlBatchCommand.cs, SetCommandType(), if(), SqlBatchCommand()

### Community 317 - "Entity (Community 317)"
Cohesion: 0.83
Nodes (4): SqlRetryLogicBaseProvider.cs, SqlRetryLogicBaseProvider.cs, ExecuteAsync(), Microsoft.Data.SqlClient

### Community 306 - "Entity (Community 306)"
Cohesion: 0.50
Nodes (4): EnclaveDelegate.Crypto.cs, if(), CombineByteArrays(), GetEnclaveSession()

### Community 319 - "Entity (Community 319)"
Cohesion: 0.83
Nodes (4): SniAsyncCallback.netcore.cs, SniAsyncCallback.netcore.cs, SniAsyncCallback(), Microsoft.Data.SqlClient.ManagedSni

### Community 320 - "Entity (Community 320)"
Cohesion: 0.50
Nodes (4): SqlDataAdapter.cs, handler(), SqlDataAdapter(), if()

### Community 307 - "Entity (Community 307)"
Cohesion: 0.50
Nodes (4): SmiTypedGetterSetter.cs, EndElements(), if(), SetVariantMetaData()

### Community 310 - "Entity (Community 310)"
Cohesion: 0.50
Nodes (4): SqlUdtInfo.cs, TryGetFromType(), Microsoft.Data.SqlClient, GetFromType()

### Community 308 - "Entity (Community 308)"
Cohesion: 0.83
Nodes (4): OnChangedEventHandler.cs, OnChangedEventHandler.cs, OnChangeEventHandler(), Microsoft.Data.SqlClient

### Community 311 - "Entity (Community 311)"
Cohesion: 0.50
Nodes (4): TdsParserStaticMethods.cs, if(), for(), ObfuscatePassword()

### Community 316 - "Entity (Community 316)"
Cohesion: 0.83
Nodes (4): SqlRowUpdatingEvent.cs, SqlRowUpdatingEvent.cs, Microsoft.Data.SqlClient, SqlRowUpdatingEventArgs()

### Community 324 - "Entity (Community 324)"
Cohesion: 0.83
Nodes (4): ResolvedServerSpn.cs, ResolvedServerSpn.cs, ResolvedServerSpn(), Microsoft.Data.SqlClient.ManagedSni

### Community 325 - "Entity (Community 325)"
Cohesion: 0.50
Nodes (4): ActiveDirectoryAuthenticationTimeoutRetryHelper.cs, InvalidOperationException(), ActiveDirectoryAuthenticationTimeoutRetryHelper(), if()

### Community 326 - "Entity (Community 326)"
Cohesion: 0.67
Nodes (3): SqlCredential.cs, Microsoft.Data.SqlClient, SqlCredential()

### Community 348 - "Entity (Community 348)"
Cohesion: 1.00
Nodes (3): SmiXetterTypeCode.cs, SmiXetterTypeCode.cs, Microsoft.Data.SqlClient.Server

### Community 331 - "Entity (Community 331)"
Cohesion: 1.00
Nodes (3): ExtendedClrTypeCode.cs, ExtendedClrTypeCode.cs, Microsoft.Data.SqlClient.Server

### Community 351 - "Entity (Community 351)"
Cohesion: 1.00
Nodes (3): TriggerAction.netfx.cs, TriggerAction.netfx.cs, Microsoft.Data.SqlClient.Server

### Community 330 - "Entity (Community 330)"
Cohesion: 1.00
Nodes (3): SqlBulkCopyOptions.cs, SqlBulkCopyOptions.cs, Microsoft.Data.SqlClient

### Community 328 - "Entity (Community 328)"
Cohesion: 1.00
Nodes (3): SniLoadHandle.netcore.cs, SniLoadHandle.netcore.cs, Microsoft.Data.SqlClient.ManagedSni

### Community 345 - "Entity (Community 345)"
Cohesion: 1.00
Nodes (3): SqlClientMetaDataCollectionNames.cs, Microsoft.Data.SqlClient, SqlClientMetaDataCollectionNames.cs

### Community 356 - "Entity (Community 356)"
Cohesion: 0.67
Nodes (3): UserAgent.cs, if(), catch()

### Community 336 - "Entity (Community 336)"
Cohesion: 0.67
Nodes (3): SqlUdtInfo.cs, if(), SqlUdtInfo()

### Community 335 - "Entity (Community 335)"
Cohesion: 1.00
Nodes (3): Microsoft.Data.SqlClient, SqlNotificationSource.cs, SqlNotificationSource.cs

### Community 344 - "Entity (Community 344)"
Cohesion: 1.00
Nodes (3): SqlNotificationType.cs, Microsoft.Data.SqlClient, SqlNotificationType.cs

### Community 347 - "Entity (Community 347)"
Cohesion: 1.00
Nodes (3): Microsoft.Data.SqlClient.ManagedSni, SniProviders.netcore.cs, SniProviders.netcore.cs

### Community 350 - "Entity (Community 350)"
Cohesion: 0.67
Nodes (3): EncryptedColumnEncryptionKeyParameters.cs, if(), using()

### Community 349 - "Entity (Community 349)"
Cohesion: 1.00
Nodes (3): Microsoft.Data.SqlClient, TdsEnums.cs, TdsEnums.cs

### Community 337 - "Entity (Community 337)"
Cohesion: 1.00
Nodes (3): SortOrder.cs, SortOrder.cs, Microsoft.Data.SqlClient

### Community 329 - "Entity (Community 329)"
Cohesion: 0.67
Nodes (3): NativeSspiContextProvider.windows.cs, if(), LoadSSPILibrary()

### Community 352 - "Entity (Community 352)"
Cohesion: 1.00
Nodes (3): PoolBlockingPeriod.cs, PoolBlockingPeriod.cs, Microsoft.Data.SqlClient

### Community 327 - "Entity (Community 327)"
Cohesion: 0.67
Nodes (3): EnclaveProviderBase.cs, lock(), if()

### Community 339 - "Entity (Community 339)"
Cohesion: 1.00
Nodes (3): DbConnectionPoolState.cs, DbConnectionPoolState.cs, Microsoft.Data.SqlClient.ConnectionPool

### Community 333 - "Entity (Community 333)"
Cohesion: 0.67
Nodes (3): SqlConnectionPoolGroupProviderInfo.cs, lock(), if()

### Community 354 - "Entity (Community 354)"
Cohesion: 1.00
Nodes (3): Microsoft.Data.SqlClient, ApplicationIntent.cs, ApplicationIntent.cs

### Community 353 - "Entity (Community 353)"
Cohesion: 1.00
Nodes (3): Microsoft.Data.SqlClient.Connection, SessionStateRecord.cs, SessionStateRecord.cs

### Community 355 - "Entity (Community 355)"
Cohesion: 1.00
Nodes (3): SqlNotificationInfo.cs, Microsoft.Data.SqlClient, SqlNotificationInfo.cs

### Community 346 - "Entity (Community 346)"
Cohesion: 1.00
Nodes (3): TransactionRequest.cs, Microsoft.Data.SqlClient, TransactionRequest.cs

### Community 341 - "Entity (Community 341)"
Cohesion: 1.00
Nodes (3): LocalDbInstanceElement.netfx.cs, LocalDbInstanceElement.netfx.cs, Microsoft.Data.SqlClient.LocalDb

### Community 334 - "Entity (Community 334)"
Cohesion: 1.00
Nodes (3): SniSmuxFlags.netcore.cs, SniSmuxFlags.netcore.cs, Microsoft.Data.SqlClient.ManagedSni

### Community 342 - "Entity (Community 342)"
Cohesion: 1.00
Nodes (3): AssemblyRef.cs, Microsoft.Data.SqlClient, AssemblyRef.cs

### Community 332 - "Entity (Community 332)"
Cohesion: 1.00
Nodes (3): SqlClientEncryptionType.cs, SqlClientEncryptionType.cs, Microsoft.Data.SqlClient

### Community 338 - "Entity (Community 338)"
Cohesion: 1.00
Nodes (3): SqlConnectionPoolProviderInfo.cs, SqlConnectionPoolProviderInfo.cs, Microsoft.Data.SqlClient.ConnectionPool

### Community 343 - "Entity (Community 343)"
Cohesion: 0.67
Nodes (3): SqlEnvChange.cs, Clear(), Microsoft.Data.SqlClient

### Community 340 - "Entity (Community 340)"
Cohesion: 1.00
Nodes (3): Microsoft.Data.SqlClient.LocalDb, LocalDbConfigurationSection.netfx.cs, LocalDbConfigurationSection.netfx.cs

### Community 358 - "Entity (Community 358)"
Cohesion: 1.00
Nodes (2): IdleConnectionChannel.cs, if()

### Community 359 - "Entity (Community 359)"
Cohesion: 1.00
Nodes (2): SqlEnvChange.cs, if()

### Community 357 - "Entity (Community 357)"
Cohesion: 1.00
Nodes (2): SqlCredential.cs, if()

## Suggested Questions
_Not enough signal to generate questions. The graph has no ambiguous edges, no bridge nodes, and all communities are well-connected._

