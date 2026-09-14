// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace ConnectionPoolRampStress;

internal interface ILoginXEventCapture
{
    void Start();
    LoginXEventResult Finish();
}

internal enum LoginXEventScope
{
    Unknown,
    Server,
    Database
}

internal enum LoginXEventKind
{
    ProcessLoginFinish,
    Login
}

internal sealed record LoginXEventCapability(string Package, string Name, string Kind,
    string? Column = null, string? ColumnKind = null, string? Type = null);

internal sealed record LoginXEventCountInterval(DateTimeOffset ServerSecondUtc,
    long NewConnectionLogins, long CachedConnectionLogins);

internal sealed record LoginXEventCounts(long NewConnectionLogins, long CachedConnectionLogins,
    IReadOnlyList<LoginXEventCountInterval> Intervals);

internal sealed record LoginXEventInterval(DateTimeOffset ServerSecondUtc,
    Distribution SuccessfulLogins, Distribution FailedLogins);

internal sealed record LoginXEventResult(string Status, string SessionName,
    Distribution SuccessfulLogins, Distribution FailedLogins, long CapturedEvents,
    long InvalidEvents, long? TotalEventsProcessed, long DroppedEvents, long DroppedBuffers,
    long TargetDroppedEvents, bool Truncated, bool SessionDropped, SafeFailure? Failure,
    IReadOnlyList<LoginXEventInterval> Intervals)
{
    public string Scope { get; init; } = "server";
    public string EventName { get; init; } = "process_login_finish";
    public bool DurationAvailable { get; init; } = true;
    public LoginXEventCounts? Counts { get; init; }

    public static LoginXEventResult NotStarted(string status, SafeFailure? failure,
        LoginXEventKind eventKind = LoginXEventKind.ProcessLoginFinish) =>
        new(status, "", new Histogram().Snapshot(), new Histogram().Snapshot(),
            0, 0, null, 0, 0, 0, false, true, failure, [])
        {
            Scope = "unknown",
            EventName = LoginXEvents.EventName(eventKind),
            DurationAvailable = eventKind == LoginXEventKind.ProcessLoginFinish,
            Counts = eventKind == LoginXEventKind.Login ? new(0, 0, []) : null
        };
}

/// <summary>Owns a bounded, automatically scoped observer isolated from all workload threads.</summary>
internal sealed class LoginXEvents : ILoginXEventCapture
{
    internal const string UnsupportedMessage = "Required login Extended Events capabilities are unavailable.";
    // Full login payloads expand substantially in XML. Retire consumed events before its 4-MB limit.
    internal const int MaximumRetainedEvents = 128;
    internal const int MaximumRetainedLoginEvents = 1024;
    private readonly string _connectionString;
    private readonly string _sessionName;
    private readonly int _timeoutSeconds;
    private readonly LoginXEventKind _eventKind;
    private readonly LoginXEventAccumulator _accumulator;
    private readonly ManualResetEventSlim _finish = new(false);
    private SqlConnection? _connection;
    private Thread? _thread;
    private LoginXEventScope _scope;
    private bool _startCalled;
    private bool _createAttempted;
    private volatile bool _sessionDropped = true;
    private volatile bool _finished;
    private LoginXEventResult? _result;

    public LoginXEvents(string connectionString, string applicationName, int timeoutSeconds = 5,
        LoginXEventKind eventKind = LoginXEventKind.ProcessLoginFinish)
    {
        ValidateName(applicationName);
        _ = EventName(eventKind);
        _eventKind = eventKind;
        if (timeoutSeconds is < 1 or > 60)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        _sessionName = applicationName;
        _timeoutSeconds = timeoutSeconds;
        _connectionString = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = applicationName + "-observer",
            Pooling = false,
            ConnectRetryCount = 0,
            ConnectTimeout = timeoutSeconds,
            Enlist = false
        }.ConnectionString;
        _accumulator = new(applicationName, eventKind: eventKind);
    }

    public void Start()
    {
        if (_startCalled || _finished) throw new InvalidOperationException("Observer has already been used.");
        _startCalled = true;
        try
        {
            _connection = new(_connectionString);
            _connection.Open();
            using (SqlCommand command = Command("SELECT CAST(SERVERPROPERTY('EngineEdition') AS int);"))
                _scope = ScopeFromEngineEdition(command.ExecuteScalar());
            bool eventApplicationName = VerifyCapabilities();
            // The generated name is the only interpolated SQL input. Mark uncertainty before CREATE.
            _createAttempted = true;
            _sessionDropped = false;
            Execute(CreateSessionSql(_sessionName, eventApplicationName, _scope, _eventKind));
            Execute(StartSessionSql(_sessionName, _scope));
            ReadTarget();
            _thread = new Thread(Poll) { IsBackground = true, Name = "LoginXEvents observer" };
            _thread.Start();
        }
        catch (Exception exception) when (IsExpected(exception) || exception is NotSupportedException)
        {
            _accumulator.Fail(exception, exception is NotSupportedException ? "unavailable" : "failed");
            _thread = null;
            Cleanup();
            _finished = true;
            throw;
        }
    }

    public LoginXEventResult Finish()
    {
        if (_result is not null) return _result;
        if (_thread is not null)
        {
            _finish.Set();
            // Poll, final read, optional reconnect, STOP and DROP all have bounded command timeouts.
            // A driver that exceeds those bounds keeps ownership on its background thread.
            if (!_thread.Join(TimeSpan.FromSeconds(6 * _timeoutSeconds + 5)))
                _accumulator.Fail(new TimeoutException(), "failed", cleanup: true);
        }
        else if (!_finished)
        {
            Cleanup();
            _finished = true;
        }
        if (_finished) _finish.Dispose();
        _result = _accumulator.Snapshot(_finished, _sessionDropped, _scope);
        return _result;
    }

    public LoginXEventResult Snapshot() => _result ?? _accumulator.Snapshot(_finished, _sessionDropped, _scope);

    private void Poll()
    {
        try
        {
            bool readFailed = false;
            while (!_finish.Wait(TimeSpan.FromSeconds(1)))
            {
                if (!readFailed)
                {
                    try { ReadTarget(); }
                    catch (Exception exception) when (IsExpected(exception))
                    {
                        _accumulator.Fail(exception);
                        readFailed = true;
                    }
                }
            }
            // Reading this DMV forces dispatch. STOP would discard the ring buffer.
            try { ReadTarget(); }
            catch (Exception exception) when (IsExpected(exception)) { _accumulator.Fail(exception); }
        }
        finally
        {
            Cleanup();
            _finished = true;
        }
    }

    internal static LoginXEventScope ScopeFromEngineEdition(object? edition) => edition switch
    {
        5 => LoginXEventScope.Database,
        1 or 2 or 3 or 4 or 8 or 9 => LoginXEventScope.Server,
        _ => throw new NotSupportedException("The database engine does not support login event scope discovery.")
    };

    internal static string EventName(LoginXEventKind eventKind) => eventKind switch
    {
        LoginXEventKind.ProcessLoginFinish => "process_login_finish",
        LoginXEventKind.Login => "login",
        _ => throw new ArgumentOutOfRangeException(nameof(eventKind))
    };

    private static (string Clause, string Sessions, string Targets, string Catalog) ScopeIdentifiers(LoginXEventScope scope) =>
        scope switch
        {
            LoginXEventScope.Server => ("SERVER", "sys.dm_xe_sessions", "sys.dm_xe_session_targets", "sys.server_event_sessions"),
            LoginXEventScope.Database => ("DATABASE", "sys.dm_xe_database_sessions", "sys.dm_xe_database_session_targets", "sys.database_event_sessions"),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };

    internal static string CreateSessionSql(string applicationName, bool eventApplicationName,
        LoginXEventScope scope = LoginXEventScope.Server, LoginXEventKind eventKind = LoginXEventKind.ProcessLoginFinish)
    {
        ValidateName(applicationName);
        string predicate = eventKind == LoginXEventKind.ProcessLoginFinish && eventApplicationName
            ? "[application_name]" : "[sqlserver].[client_app_name]";
        int retainedEvents = eventKind == LoginXEventKind.Login ? MaximumRetainedLoginEvents : MaximumRetainedEvents;
        return $"""
            CREATE EVENT SESSION [{applicationName}] ON {ScopeIdentifiers(scope).Clause}
            ADD EVENT sqlserver.{EventName(eventKind)}
            (
                ACTION(package0.event_sequence)
                WHERE ({predicate}=N'{applicationName}')
            )
            ADD TARGET package0.ring_buffer(SET max_memory=1024, max_events_limit={retainedEvents})
            WITH (MAX_MEMORY=4096 KB, MAX_DISPATCH_LATENCY=1 SECONDS,
                EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS, STARTUP_STATE=OFF);
            """;
    }

    internal static string StartSessionSql(string applicationName, LoginXEventScope scope)
    {
        ValidateName(applicationName);
        return $"ALTER EVENT SESSION [{applicationName}] ON {ScopeIdentifiers(scope).Clause} STATE=START;";
    }

    internal static string ReadTargetSql(LoginXEventScope scope)
    {
        var identifiers = ScopeIdentifiers(scope);
        return $"""
            SELECT s.dropped_event_count, s.dropped_buffer_count, t.target_data
            FROM {identifiers.Sessions} AS s
            JOIN {identifiers.Targets} AS t ON t.event_session_address=s.address
            WHERE s.name=@name AND t.target_name=N'ring_buffer';
            """;
    }

    internal static string StopSessionSql(string applicationName, LoginXEventScope scope)
    {
        ValidateName(applicationName);
        var identifiers = ScopeIdentifiers(scope);
        return $"""
            IF EXISTS (SELECT 1 FROM {identifiers.Sessions} WHERE name=N'{applicationName}')
                ALTER EVENT SESSION [{applicationName}] ON {identifiers.Clause} STATE=STOP;
            """;
    }

    internal static string DropSessionSql(string applicationName, LoginXEventScope scope)
    {
        ValidateName(applicationName);
        var identifiers = ScopeIdentifiers(scope);
        return $"""
            IF EXISTS (SELECT 1 FROM {identifiers.Catalog} WHERE name=N'{applicationName}')
                DROP EVENT SESSION [{applicationName}] ON {identifiers.Clause};
            """;
    }

    private static void ValidateName(string applicationName)
    {
        if (applicationName is null || !Regex.IsMatch(applicationName,
            @"\AConnectionPoolRampStress_[0-9a-f]{32}\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("A generated sample application name is required.", nameof(applicationName));
    }

    private bool VerifyCapabilities()
    {
        using SqlCommand command = Command("""
            SELECT p.name, o.name, o.object_type, c.name, c.column_type, c.type_name
            FROM sys.dm_xe_objects AS o
            JOIN sys.dm_xe_packages AS p ON p.guid=o.package_guid
            LEFT JOIN sys.dm_xe_object_columns AS c
                ON c.object_package_guid=o.package_guid AND c.object_name=o.name
            WHERE (p.name=N'sqlserver' AND o.name IN (@event,N'client_app_name'))
               OR (p.name=N'package0' AND o.name IN (N'event_sequence',N'ring_buffer'));
            """);
        command.Parameters.Add("@event", SqlDbType.NVarChar, 128).Value = EventName(_eventKind);
        using SqlDataReader reader = command.ExecuteReader();
        return VerifyCapabilities(Rows(), _eventKind);

        IEnumerable<LoginXEventCapability> Rows()
        {
            while (reader.Read())
                yield return new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5));
        }
    }

    internal static bool VerifyCapabilities(IEnumerable<LoginXEventCapability> capabilities, LoginXEventKind eventKind)
    {
        string eventName = EventName(eventKind);
        bool eventFound = false, duration = false, success = false, predicate = false, sequence = false,
            target = false, memory = false, limit = false, applicationName = false, cached = false;
        foreach (var (package, name, kind, column, columnKind, type) in capabilities)
        {
            if (package == "sqlserver" && name == eventName && kind == "event")
            {
                eventFound = true;
                duration |= column == "total_time_ms" && columnKind == "data" &&
                    type is "uint64" or "uint32" or "int64" or "int32" or "float64" or "float32";
                success |= column == "is_success" && columnKind == "data" &&
                    type is "boolean" or "uint8" or "uint32" or "int32";
                cached |= column == "is_cached" && columnKind == "data" &&
                    type is "boolean" or "uint8" or "uint32" or "int32";
                applicationName |= column == "application_name" && columnKind == "data" && type == "unicode_string";
            }
            predicate |= package == "sqlserver" && name == "client_app_name" && kind == "pred_source";
            sequence |= package == "package0" && name == "event_sequence" && kind == "action";
            if (package == "package0" && name == "ring_buffer" && kind == "target")
            {
                target = true;
                memory |= column == "max_memory" && columnKind == "customizable";
                limit |= column == "max_events_limit" && columnKind == "customizable";
            }
        }
        bool fields = eventKind == LoginXEventKind.Login ? cached : duration && success;
        if (!(eventFound && fields && predicate && sequence && target && memory && limit))
            throw new NotSupportedException(UnsupportedMessage);
        return eventKind == LoginXEventKind.ProcessLoginFinish && applicationName;
    }

    private void ReadTarget()
    {
        using SqlCommand command = Command(ReadTargetSql(_scope));
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = _sessionName;
        using SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow);
        if (!reader.Read())
            throw new InvalidOperationException("Login event target is unavailable.");
        long events = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
        long buffers = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        if (reader.IsDBNull(2))
            throw new InvalidOperationException("Login event target is unavailable.");
        using TextReader text = reader.GetTextReader(2);
        StringBuilder xml = new();
        char[] chunk = new char[4096];
        int count;
        while ((count = text.Read(chunk, 0, chunk.Length)) != 0)
        {
            if (xml.Length + count > LoginXEventAccumulator.MaximumXmlCharacters)
                throw new FormatException("Login event target exceeds the document limit.");
            xml.Append(chunk, 0, count);
        }
        _accumulator.Read(xml.ToString(), events, buffers);
    }

    private void Cleanup()
    {
        try
        {
            if (_createAttempted)
            {
                if (_connection?.State != ConnectionState.Open)
                {
                    _connection?.Dispose();
                    _connection = new(_connectionString);
                    _connection.Open();
                }
                try
                {
                    Execute(StopSessionSql(_sessionName, _scope));
                }
                catch (Exception exception) when (IsExpected(exception))
                {
                    _accumulator.Fail(exception, cleanup: true);
                }
                // DROP still runs if STOP fails. IF EXISTS also covers uncertain CREATE/START outcomes.
                Execute(DropSessionSql(_sessionName, _scope));
                _sessionDropped = true;
            }
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            _accumulator.Fail(exception, cleanup: true);
        }
        finally
        {
            try { _connection?.Dispose(); }
            catch (Exception exception) when (IsExpected(exception))
            {
                _accumulator.Fail(exception, cleanup: true);
            }
            _connection = null;
        }
    }

    private SqlCommand Command(string sql) => new(sql, _connection) { CommandTimeout = _timeoutSeconds };

    private void Execute(string sql)
    {
        using SqlCommand command = Command(sql);
        command.ExecuteNonQuery();
    }

    private static bool IsExpected(Exception exception) =>
        exception is SqlException or InvalidOperationException or IOException or XmlException or
            FormatException or OverflowException or ArgumentException or TimeoutException or ThreadStateException;
}

/// <summary>Aggregates bounded target snapshots without retaining XML, SQL text, or server metadata.</summary>
internal sealed class LoginXEventAccumulator
{
    internal const int MaximumXmlCharacters = 4 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly string _sessionName;
    private readonly int _maximumIntervals;
    private readonly LoginXEventKind _eventKind;
    private readonly Histogram _successful = new();
    private readonly Histogram _failed = new();
    private readonly SortedDictionary<long, (Histogram Successful, Histogram Failed)> _intervals = new();
    private readonly SortedDictionary<long, (long New, long Cached)> _countIntervals = new();
    private long _newConnectionLogins;
    private long _cachedConnectionLogins;
    private long _highWater = -1;
    private long _consumed;
    private long _invalid;
    private long? _processed;
    private long _droppedEvents;
    private long _droppedBuffers;
    private long _targetDropped;
    private bool _truncated;
    private bool _incomplete;
    private SafeFailure? _failure;
    private string? _failureStatus;

    public LoginXEventAccumulator(string sessionName, int maximumIntervals = 3600,
        LoginXEventKind eventKind = LoginXEventKind.ProcessLoginFinish)
    {
        _ = LoginXEvents.EventName(eventKind);
        _eventKind = eventKind;
        _sessionName = sessionName;
        if (maximumIntervals is < 1 or > 3600) throw new ArgumentOutOfRangeException(nameof(maximumIntervals));
        _maximumIntervals = maximumIntervals;
    }

    public void Read(string xml, long droppedEvents = 0, long droppedBuffers = 0)
    {
        lock (_gate)
        {
            try
            {
                if (xml.Length > MaximumXmlCharacters) throw new FormatException();
                using XmlReader reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumXmlCharacters,
                    MaxCharactersFromEntities = 1024
                });
                XElement root = XElement.Load(reader);
                if (root.Name != "RingBufferTarget") throw new FormatException();
                long processed = Counter(root.Attribute("totalEventsProcessed")?.Value);
                long targetDropped = Counter(root.Attribute("droppedCount")?.Value);
                bool truncated = Boolean(root.Attribute("truncated")?.Value);
                if (droppedEvents < 0 || droppedBuffers < 0) throw new FormatException();
                if (_processed > processed) _incomplete = true;
                _processed = Math.Max(_processed ?? 0, processed);
                _targetDropped = Math.Max(_targetDropped, targetDropped);
                _droppedEvents = Math.Max(_droppedEvents, droppedEvents);
                _droppedBuffers = Math.Max(_droppedBuffers, droppedBuffers);
                _truncated |= truncated;
                // Sort each bounded snapshot before advancing the high-water mark.
                var events = root.Elements("event").Select(e => (Event: e,
                    Sequence: Counter(Value(e, "action", "event_sequence", "package0"))))
                    .OrderBy(e => e.Sequence).ToArray();
                foreach ((XElement element, long sequence) in events)
                {
                    if (sequence <= _highWater) continue;
                    if (_highWater >= 0 && sequence != _highWater + 1) _incomplete = true;
                    _highWater = sequence;
                    _consumed++;
                    try
                    {
                        if ((string?)element.Attribute("name") != LoginXEvents.EventName(_eventKind) ||
                            (string?)element.Attribute("package") != "sqlserver")
                            throw new FormatException();
                        string? timestamp = (string?)element.Attribute("timestamp");
                        if (timestamp is null || !timestamp.EndsWith("Z", StringComparison.Ordinal) ||
                            !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset utc))
                            throw new FormatException();
                        long second = utc.ToUnixTimeSeconds();
                        if (_eventKind == LoginXEventKind.Login)
                        {
                            bool cached = Boolean(Value(element, "data", "is_cached"));
                            if (cached) _cachedConnectionLogins++;
                            else _newConnectionLogins++;
                            if (!_countIntervals.TryGetValue(second, out var counts) && _countIntervals.Count == _maximumIntervals)
                            {
                                _incomplete = true;
                                continue;
                            }
                            _countIntervals[second] = (counts.New + (cached ? 0 : 1), counts.Cached + (cached ? 1 : 0));
                            continue;
                        }
                        double milliseconds = double.Parse(Value(element, "data", "total_time_ms"),
                            NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (!double.IsFinite(milliseconds) || milliseconds < 0 ||
                            !double.IsFinite(milliseconds / 0.001))
                            throw new FormatException();
                        bool success = Boolean(Value(element, "data", "is_success"));
                        Histogram histogram = success ? _successful : _failed;
                        if (!double.IsFinite(histogram.Sum + milliseconds)) throw new FormatException();
                        histogram.Add(milliseconds);
                        if (!_intervals.TryGetValue(second, out var interval))
                        {
                            if (_intervals.Count == _maximumIntervals)
                            {
                                _incomplete = true;
                                continue;
                            }
                            _intervals.Add(second, interval = (new(), new()));
                        }
                        (success ? interval.Successful : interval.Failed).Add(milliseconds);
                    }
                    catch (Exception exception) when (exception is FormatException or OverflowException)
                    {
                        _invalid++;
                        _incomplete = true;
                    }
                }
            }
            catch (Exception exception) when (exception is XmlException or FormatException or OverflowException)
            {
                Fail(exception);
            }
        }
    }

    public void Fail(Exception exception, string status = "failed", bool cleanup = false)
    {
        lock (_gate)
        {
            if (_failure is null || cleanup)
            {
                _failure = ConnectionPoolRampStress.Failure.Describe(exception) with
                {
                    Category = cleanup ? "xevent-cleanup" : status == "unavailable" ? "xevent-capability" : "xevent-capture"
                };
                _failureStatus = status;
            }
        }
    }

    public LoginXEventResult Snapshot(bool finished, bool sessionDropped, LoginXEventScope scope = LoginXEventScope.Server)
    {
        lock (_gate)
        {
            long captured = _eventKind == LoginXEventKind.Login
                ? _newConnectionLogins + _cachedConnectionLogins : _successful.Count + _failed.Count;
            bool complete = finished && sessionDropped && captured > 0 &&
                _processed == _consumed &&
                !_incomplete && !_truncated && _droppedEvents == 0 && _droppedBuffers == 0 && _targetDropped == 0;
            return new(_failureStatus ?? (_processed is null ? "unavailable" : complete ? "captured" : "incomplete"),
                _sessionName, _successful.Snapshot(), _failed.Snapshot(), captured,
                _invalid, _processed, _droppedEvents, _droppedBuffers, _targetDropped, _truncated,
                sessionDropped, _failure, _intervals.Select(pair => new LoginXEventInterval(
                    DateTimeOffset.FromUnixTimeSeconds(pair.Key),
                    pair.Value.Successful.Snapshot(), pair.Value.Failed.Snapshot())).ToArray())
            {
                EventName = LoginXEvents.EventName(_eventKind),
                DurationAvailable = _eventKind == LoginXEventKind.ProcessLoginFinish,
                Counts = _eventKind == LoginXEventKind.Login
                    ? new(_newConnectionLogins, _cachedConnectionLogins,
                        _countIntervals.Select(pair => new LoginXEventCountInterval(
                            DateTimeOffset.FromUnixTimeSeconds(pair.Key), pair.Value.New, pair.Value.Cached)).ToArray())
                    : null,
                Scope = scope switch
                {
                    LoginXEventScope.Server => "server",
                    LoginXEventScope.Database => "database",
                    LoginXEventScope.Unknown => "unknown",
                    _ => throw new ArgumentOutOfRangeException(nameof(scope))
                }
            };
        }
    }

    private static long Counter(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number >= 0
            ? number : throw new FormatException();

    private static bool Boolean(string? value) => value switch
    {
        "1" => true,
        "0" => false,
        _ when bool.TryParse(value, out bool result) => result,
        _ => throw new FormatException()
    };

    private static string Value(XElement element, string kind, string name, string? package = null)
    {
        XElement[] fields = element.Elements(kind).Where(field => (string?)field.Attribute("name") == name &&
            (package is null || (string?)field.Attribute("package") == package)).Take(2).ToArray();
        if (fields.Length != 1 || fields[0].Elements("value").Count() != 1) throw new FormatException();
        return fields[0].Element("value")!.Value;
    }
}
