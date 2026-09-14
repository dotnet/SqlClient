// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Xunit;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Validates bounded login event aggregation without a database or observer connection.</summary>
public sealed class LoginXEventsTests
{
    private const string Session = "ConnectionPoolRampStress_0123456789abcdef0123456789abcdef";

    /// <summary>Overlapping snapshots and reordered XML do not double count login durations.</summary>
    [Fact]
    public void SnapshotsDeduplicateAndSortEvents()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(2, Event(2, "12.5", "false") + Event(1, "4", "true")));
        accumulator.Read(Ring(3, Event(3, "20", "1") + Event(1, "4", "true") + Event(2, "12.5", "0")));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("captured", result.Status);
        Assert.Equal(3, result.CapturedEvents);
        Assert.Equal(2, result.SuccessfulLogins.Count);
        Assert.Equal(24, result.SuccessfulLogins.TotalMilliseconds);
        Assert.Equal(12.5, result.FailedLogins.TotalMilliseconds);
        Assert.Equal(20, result.SuccessfulLogins.MaxMilliseconds);
        Assert.InRange(result.SuccessfulLogins.P50Milliseconds!.Value, 4, 4 * 1.08);
        LoginXEventInterval interval = Assert.Single(result.Intervals);
        Assert.Equal(DateTimeOffset.Parse("2026-09-14T12:34:56Z"), interval.ServerSecondUtc);
        Assert.Equal(2, interval.SuccessfulLogins.Count);
        Assert.Equal(1, interval.FailedLogins.Count);
    }

    /// <summary>An empty target has null percentiles and cannot be advertised as a usable capture.</summary>
    [Fact]
    public void EmptyTargetHasNullPercentiles()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(0, ""));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("incomplete", result.Status);
        Assert.Null(result.SuccessfulLogins.P50Milliseconds);
        Assert.Null(result.FailedLogins.P99Milliseconds);
        Assert.Empty(result.Intervals);
    }

    /// <summary>A constructor failure result represents no server session and preserves the supplied safe status.</summary>
    [Fact]
    public void NotStartedHasEmptyMetricsAndNoSession()
    {
        SafeFailure failure = new("ArgumentException", null, "setup");
        LoginXEventResult result = LoginXEventResult.NotStarted("failed", failure);
        Assert.Equal("failed", result.Status);
        Assert.Equal("", result.SessionName);
        Assert.Equal("unknown", result.Scope);
        Assert.True(result.SessionDropped);
        Assert.Equal(failure, result.Failure);
        Assert.Null(result.TotalEventsProcessed);
        Assert.Equal(0, result.CapturedEvents);
        Assert.Equal(0, result.SuccessfulLogins.Count);
        Assert.Equal(0, result.FailedLogins.Count);
        Assert.Null(result.SuccessfulLogins.P50Milliseconds);
        Assert.Null(result.FailedLogins.P99Milliseconds);
        Assert.Empty(result.Intervals);
    }

    /// <summary>Target eviction is detected even when the XML has no truncation or drop indicators.</summary>
    [Fact]
    public void CircularEvictionIsIncomplete()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(5, Event(4) + Event(5)));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(5, result.TotalEventsProcessed);
        Assert.Equal(2, result.CapturedEvents);
        Assert.False(result.Truncated);
    }

    /// <summary>Eviction of events already consumed does not imply lost coverage.</summary>
    [Fact]
    public void PreviouslyConsumedEvictionRemainsComplete()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(2, Event(1) + Event(2)));
        accumulator.Read(Ring(3, Event(3)));
        Assert.Equal("captured", accumulator.Snapshot(true, true).Status);
    }

    /// <summary>Every server loss indicator independently prevents a complete result.</summary>
    [Theory]
    [InlineData(1, 0, 0, false)]
    [InlineData(0, 1, 0, false)]
    [InlineData(0, 0, 1, false)]
    [InlineData(0, 0, 0, true)]
    public void LossCountersRemainVisible(long events, long buffers, long target, bool truncated)
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(1, Event(1), target, truncated), events, buffers);
        accumulator.Read(Ring(1, Event(1)));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(events, result.DroppedEvents);
        Assert.Equal(buffers, result.DroppedBuffers);
        Assert.Equal(target, result.TargetDroppedEvents);
        Assert.Equal(truncated, result.Truncated);
    }

    /// <summary>Malformed login data is counted as invalid and never recorded as zero milliseconds.</summary>
    [Theory]
    [InlineData("NaN", "true")]
    [InlineData("Infinity", "true")]
    [InlineData("1e308", "true")]
    [InlineData("-1", "true")]
    [InlineData("nonsense", "true")]
    [InlineData("1", "yes")]
    public void InvalidFieldsAreNotDurations(string duration, string success)
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(1, Event(1, duration, success)));
        accumulator.Read(Ring(1, Event(1, duration, success)));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(1, result.InvalidEvents);
        Assert.Equal(0, result.CapturedEvents);
        Assert.Null(result.SuccessfulLogins.P50Milliseconds);
    }

    /// <summary>Missing identity, required fields, and non-login events cannot produce complete telemetry.</summary>
    [Theory]
    [InlineData("event_sequence", "other_sequence")]
    [InlineData("total_time_ms", "duration")]
    [InlineData("is_success", "other_success")]
    [InlineData("process_login_finish", "other_event")]
    [InlineData("package0", "other_package")]
    [InlineData("sqlserver", "other_package")]
    [InlineData("2026-09-14T12:34:56.789Z", "bad-timestamp")]
    public void MissingRequiredFieldsAreIncomplete(string oldValue, string newValue)
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(1, Event(1).Replace(oldValue, newValue, StringComparison.Ordinal)));
        Assert.NotEqual("captured", accumulator.Snapshot(true, true).Status);
    }

    /// <summary>Sequence gaps and events arriving behind the high-water mark are never silently complete.</summary>
    [Fact]
    public void LateSequenceAndGapRemainIncomplete()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(2, Event(1) + Event(3)));
        accumulator.Read(Ring(3, Event(1) + Event(2) + Event(3)));
        Assert.Equal("incomplete", accumulator.Snapshot(true, true).Status);
    }

    /// <summary>Server-second buckets are absolute, bounded, and mark overflow as incomplete.</summary>
    [Fact]
    public void TimeBucketsHaveBoundedCapacity()
    {
        LoginXEventAccumulator accumulator = new(Session, maximumIntervals: 2);
        accumulator.Read(Ring(3, Event(1) +
            Event(2).Replace("12:34:56", "12:34:57") +
            Event(3).Replace("12:34:56", "12:34:58")));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(3, result.CapturedEvents);
        Assert.Equal(2, result.Intervals.Count);
    }

    /// <summary>Malformed or entity-bearing XML retains only a fixed safe failure.</summary>
    [Theory]
    [InlineData("<RingBufferTarget private-secret")]
    [InlineData("<!DOCTYPE x [<!ENTITY secret SYSTEM 'file:///private-secret'>]><RingBufferTarget>&secret;</RingBufferTarget>")]
    public void XmlFailureDoesNotSerializeInput(string xml)
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(xml);
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("failed", result.Status);
        Assert.NotNull(result.Failure);
        Assert.DoesNotContain("private-secret", JsonSerializer.Serialize(result));
    }

    /// <summary>Documents exceeding the bounded parser limit fail without retaining their contents.</summary>
    [Fact]
    public void OversizedXmlIsRejected()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(new string('x', LoginXEventAccumulator.MaximumXmlCharacters + 1));
        Assert.Equal("failed", accumulator.Snapshot(true, true).Status);
    }

    /// <summary>Cleanup failures retain the controlled session name but never exception details.</summary>
    [Fact]
    public void CleanupFailureIsExplicitAndRedacted()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(1, Event(1)));
        accumulator.Fail(new InvalidOperationException("private-secret"), cleanup: true);
        LoginXEventResult result = accumulator.Snapshot(true, false);
        Assert.Equal("failed", result.Status);
        Assert.Equal(Session, result.SessionName);
        Assert.False(result.SessionDropped);
        Assert.Equal("xevent-cleanup", result.Failure!.Category);
        Assert.DoesNotContain("private-secret", JsonSerializer.Serialize(result));
    }

    /// <summary>A successful final read and cleanup cannot erase an earlier polling failure.</summary>
    [Fact]
    public void PollFailureSurvivesFinalRead()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(0, ""));
        accumulator.Fail(new TimeoutException("private-secret"));
        accumulator.Read(Ring(1, Event(1)));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("failed", result.Status);
        Assert.True(result.SessionDropped);
        Assert.Equal(1, result.CapturedEvents);
        Assert.Equal("TimeoutException", result.Failure!.ExceptionType);
        Assert.Equal("xevent-capture", result.Failure.Category);
        Assert.DoesNotContain("private-secret", JsonSerializer.Serialize(result));
    }

    /// <summary>Counter corruption cannot be mistaken for a complete empty capture.</summary>
    [Theory]
    [InlineData("totalEventsProcessed", "other_counter")]
    [InlineData("droppedCount=\"0\"", "droppedCount=\"-1\"")]
    [InlineData("truncated=\"0\"", "truncated=\"unknown\"")]
    public void MalformedCountersFailSafely(string oldValue, string newValue)
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(0, "").Replace(oldValue, newValue, StringComparison.Ordinal));
        Assert.Equal("failed", accumulator.Snapshot(true, true).Status);
    }

    /// <summary>A cumulative target counter reset is retained as a loss indicator.</summary>
    [Fact]
    public void TargetCounterResetIsIncomplete()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(1, Event(1)));
        accumulator.Read(Ring(0, ""));
        Assert.Equal("incomplete", accumulator.Snapshot(true, true).Status);
    }

    /// <summary>Timeouts cannot disable the observer's bounded connection and command operations.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public void UnboundedTimeoutIsRejected(int timeout)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoginXEvents("", Session, timeout));
    }

    /// <summary>Only the exact generated identifier format is permitted in dynamic DDL.</summary>
    [Theory]
    [InlineData("ConnectionPoolRampStress_0123456789abcdef0123456789abcdef';DROP EVENT SESSION x--")]
    [InlineData("ConnectionPoolRampStress_0123456789abcdef0123456789abcdef\n")]
    [InlineData("ConnectionPoolRampStress_0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("arbitrary-application")]
    public void UnsafeNamesAreRejectedWithoutIo(string name)
    {
        Assert.Throws<ArgumentException>(() => new LoginXEvents("", name));
        foreach (LoginXEventScope scope in new[] { LoginXEventScope.Server, LoginXEventScope.Database })
        {
            Assert.Throws<ArgumentException>(() => LoginXEvents.CreateSessionSql(name, true, scope));
            Assert.Throws<ArgumentException>(() => LoginXEvents.StartSessionSql(name, scope));
            Assert.Throws<ArgumentException>(() => LoginXEvents.StopSessionSql(name, scope));
            Assert.Throws<ArgumentException>(() => LoginXEvents.DropSessionSql(name, scope));
        }
    }

    /// <summary>The login event's own application field is preferred before session context initialization.</summary>
    [Theory]
    [InlineData(true, "[application_name]", 5)]
    [InlineData(false, "[sqlserver].[client_app_name]", 5)]
    [InlineData(true, "[application_name]", 3)]
    [InlineData(false, "[sqlserver].[client_app_name]", 3)]
    public void SessionPredicateUsesVerifiedApplicationSource(bool eventFieldAvailable, string predicate, int edition)
    {
        string sql = LoginXEvents.CreateSessionSql(Session, eventFieldAvailable, LoginXEvents.ScopeFromEngineEdition(edition));
        Assert.Contains("ADD EVENT sqlserver.process_login_finish", sql);
        Assert.Contains($"WHERE ({predicate}=N'{Session}')", sql);
        Assert.Contains("EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS", sql);
        Assert.Contains("STARTUP_STATE=OFF", sql);
        Assert.Contains("max_memory=1024", sql);
        Assert.Contains("max_events_limit=128", sql);
        Assert.DoesNotContain("NO_EVENT_LOSS", sql);
    }

    /// <summary>Azure SQL Database uses database scope while SQL Server and MI retain server scope.</summary>
    [Theory]
    [InlineData(1, "server")]
    [InlineData(2, "server")]
    [InlineData(3, "server")]
    [InlineData(4, "server")]
    [InlineData(5, "database")]
    [InlineData(8, "server")]
    [InlineData(9, "server")]
    public void ScopeDiscoverySelectsMatchingSqlAndMetadata(int edition, string expected)
    {
        LoginXEventScope scope = LoginXEvents.ScopeFromEngineEdition(edition);
        string clause = expected.ToUpperInvariant();
        string sessions = expected == "database" ? "sys.dm_xe_database_sessions" : "sys.dm_xe_sessions";
        string targets = expected == "database" ? "sys.dm_xe_database_session_targets" : "sys.dm_xe_session_targets";
        Assert.Contains($"CREATE EVENT SESSION [{Session}] ON {clause}", LoginXEvents.CreateSessionSql(Session, true, scope));
        Assert.Equal($"ALTER EVENT SESSION [{Session}] ON {clause} STATE=START;", LoginXEvents.StartSessionSql(Session, scope));
        string read = LoginXEvents.ReadTargetSql(scope);
        Assert.Contains($"FROM {sessions} AS s", read);
        Assert.Contains($"JOIN {targets} AS t ON t.event_session_address=s.address", read);
        Assert.Contains("s.dropped_event_count, s.dropped_buffer_count, t.target_data", read);
        Assert.Contains("WHERE s.name=@name", read);
        string stop = LoginXEvents.StopSessionSql(Session, scope);
        Assert.Contains($"IF EXISTS (SELECT 1 FROM {sessions} WHERE name=N'{Session}')", stop);
        Assert.Contains($"ALTER EVENT SESSION [{Session}] ON {clause} STATE=STOP;", stop);
        string drop = LoginXEvents.DropSessionSql(Session, scope);
        Assert.Contains($"IF EXISTS (SELECT 1 FROM sys.{expected}_event_sessions WHERE name=N'{Session}')", drop);
        Assert.Contains($"DROP EVENT SESSION [{Session}] ON {clause};", drop);
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(1, Event(1, "12.5")));
        LoginXEventResult result = accumulator.Snapshot(true, true, scope);
        Assert.Equal(expected, result.Scope);
        Assert.Equal("captured", result.Status);
        Assert.Equal(12.5, result.SuccessfulLogins.TotalMilliseconds);
        Assert.Contains($"\"Scope\":\"{expected}\"", JsonSerializer.Serialize(result));
    }

    /// <summary>Unknown editions and non-SQL Server engines cannot select server scope silently.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(999)]
    public void UnsupportedEngineEditionIsRejected(int? edition)
    {
        Assert.Throws<NotSupportedException>(() => LoginXEvents.ScopeFromEngineEdition(edition));
        Assert.Throws<NotSupportedException>(() => LoginXEvents.ScopeFromEngineEdition(DBNull.Value));
    }

    /// <summary>An undiscovered or invalid scope cannot produce session DDL or DMV reads.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void InvalidScopeCannotGenerateSql(int value)
    {
        LoginXEventScope scope = (LoginXEventScope)value;
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginXEvents.CreateSessionSql(Session, true, scope));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginXEvents.StartSessionSql(Session, scope));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginXEvents.ReadTargetSql(scope));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginXEvents.StopSessionSql(Session, scope));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginXEvents.DropSessionSql(Session, scope));
    }

    /// <summary>Database-scoped empty, lossy, and failed captures retain their scope and cannot report success.</summary>
    [Fact]
    public void DatabaseCaptureKeepsIncompleteAndFailureStatuses()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(0, ""));
        LoginXEventResult empty = accumulator.Snapshot(true, true, LoginXEventScope.Database);
        Assert.Equal("database", empty.Scope);
        Assert.Equal("incomplete", empty.Status);
        accumulator.Read(Ring(1, Event(1)), droppedEvents: 1);
        LoginXEventResult lost = accumulator.Snapshot(true, true, LoginXEventScope.Database);
        Assert.Equal("incomplete", lost.Status);
        Assert.Equal(1, lost.DroppedEvents);
        accumulator.Fail(new NotSupportedException(), "unavailable");
        LoginXEventResult unavailable = accumulator.Snapshot(true, true, LoginXEventScope.Database);
        Assert.Equal("database", unavailable.Scope);
        Assert.Equal("unavailable", unavailable.Status);
        accumulator.Fail(new TimeoutException(), cleanup: true);
        LoginXEventResult failed = accumulator.Snapshot(true, false, LoginXEventScope.Database);
        Assert.Equal("database", failed.Scope);
        Assert.Equal("failed", failed.Status);
        Assert.False(failed.SessionDropped);
    }

    /// <summary>Cleanup without Start is idempotent and does not open an admin connection.</summary>
    [Fact]
    public void FinishWithoutStartIsUnavailable()
    {
        LoginXEvents collector = new("", Session);
        ILoginXEventCapture capture = collector;
        LoginXEventResult first = capture.Finish();
        Assert.Equal("unavailable", first.Status);
        Assert.Equal("unknown", first.Scope);
        Assert.True(first.SessionDropped);
        Assert.Equal(first, collector.Finish());
        Assert.Equal(first, collector.Snapshot());
    }

    /// <summary>Explicit login capture counts new and pooled logins without inventing duration samples.</summary>
    [Fact]
    public void LoginModeCountsNewAndCachedEventsWithoutDurations()
    {
        LoginXEventAccumulator accumulator = new(Session, eventKind: LoginXEventKind.Login);
        accumulator.Read(Ring(2, LoginEvent(2, "true") + LoginEvent(1)));
        accumulator.Read(Ring(3, LoginEvent(1) + LoginEvent(2, "true") +
            LoginEvent(3, "1").Replace("12:34:56", "12:34:57")));
        LoginXEventResult result = accumulator.Snapshot(true, true, LoginXEventScope.Database);
        Assert.Equal("captured", result.Status);
        Assert.Equal("database", result.Scope);
        Assert.Equal("login", result.EventName);
        Assert.False(result.DurationAvailable);
        Assert.Equal(3, result.CapturedEvents);
        Assert.Equal(1, result.Counts!.NewConnectionLogins);
        Assert.Equal(2, result.Counts.CachedConnectionLogins);
        Assert.Equal(2, result.Counts.Intervals.Count);
        Assert.Equal(new LoginXEventCountInterval(DateTimeOffset.Parse("2026-09-14T12:34:56Z"), 1, 1),
            result.Counts.Intervals[0]);
        Assert.Equal(0, result.SuccessfulLogins.Count);
        Assert.Equal(0, result.FailedLogins.Count);
        Assert.Null(result.SuccessfulLogins.P95Milliseconds);
        Assert.Null(result.FailedLogins.P95Milliseconds);
        Assert.Empty(result.Intervals);
    }

    /// <summary>The timing mode remains the default and cannot silently treat a login event as timing data.</summary>
    [Fact]
    public void DefaultModeNeverFallsBackToLoginCounts()
    {
        LoginXEventAccumulator accumulator = new(Session);
        accumulator.Read(Ring(1, LoginEvent(1)));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("process_login_finish", result.EventName);
        Assert.True(result.DurationAvailable);
        Assert.Null(result.Counts);
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(0, result.CapturedEvents);
        Assert.Equal(1, result.InvalidEvents);
    }

    /// <summary>Count-only session DDL uses a fixed login event and the client application predicate in either scope.</summary>
    [Theory]
    [InlineData(3, "SERVER")]
    [InlineData(5, "DATABASE")]
    public void LoginModeSqlUsesExplicitEventAndSafePredicate(int edition, string clause)
    {
        string sql = LoginXEvents.CreateSessionSql(Session, true, LoginXEvents.ScopeFromEngineEdition(edition), LoginXEventKind.Login);
        Assert.Contains($"ON {clause}", sql);
        Assert.Contains("ADD EVENT sqlserver.login", sql);
        Assert.Contains($"WHERE ([sqlserver].[client_app_name]=N'{Session}')", sql);
        Assert.Contains("max_memory=1024, max_events_limit=1024", sql);
        Assert.DoesNotContain("process_login_finish", sql);
        Assert.DoesNotContain("[application_name]", sql);
    }

    /// <summary>Count-only bursts within the larger bounded target can be consumed in one snapshot.</summary>
    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void LoginModeConsumesBoundedBurst(int count)
    {
        LoginXEventAccumulator accumulator = new(Session, eventKind: LoginXEventKind.Login);
        string xml = Ring(count, string.Concat(Enumerable.Range(1, count).Select(sequence => LoginEvent(sequence))));
        Assert.True(xml.Length < LoginXEventAccumulator.MaximumXmlCharacters);
        accumulator.Read(xml);
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("captured", result.Status);
        Assert.Equal(count, result.CapturedEvents);
        Assert.Equal(count, result.Counts!.NewConnectionLogins);
        Assert.Equal(0, result.Counts.CachedConnectionLogins);
    }

    /// <summary>Capability discovery requires the selected event's own fields and never substitutes another event.</summary>
    [Theory]
    [InlineData("is_cached", "data", "boolean", true)]
    [InlineData("is_cached", "data", "uint32", true)]
    [InlineData("is_cached", "data", "unicode_string", false)]
    [InlineData("is_cached", "customizable", "boolean", false)]
    [InlineData("is_recovered", "data", "boolean", false)]
    public void LoginCapabilitiesRequireCachedFlag(string column, string columnKind, string type, bool supported)
    {
        LoginXEventCapability[] rows = Capabilities(new LoginXEventCapability("sqlserver", "login", "event", column, columnKind, type));
        if (supported)
            Assert.False(LoginXEvents.VerifyCapabilities(rows, LoginXEventKind.Login));
        else
            Assert.Throws<NotSupportedException>(() => LoginXEvents.VerifyCapabilities(rows, LoginXEventKind.Login));
        Assert.Throws<NotSupportedException>(() => LoginXEvents.VerifyCapabilities(rows, LoginXEventKind.ProcessLoginFinish));
    }

    /// <summary>The original event still requires duration and success metadata when the count-only event is available.</summary>
    [Fact]
    public void ProcessCapabilitiesStillRequireDurationAndSuccess()
    {
        LoginXEventCapability[] rows = Capabilities(
            new("sqlserver", "process_login_finish", "event", "total_time_ms", "data", "uint64"),
            new("sqlserver", "process_login_finish", "event", "is_success", "data", "boolean"));
        Assert.False(LoginXEvents.VerifyCapabilities(rows, LoginXEventKind.ProcessLoginFinish));
        Assert.Throws<NotSupportedException>(() => LoginXEvents.VerifyCapabilities(rows, LoginXEventKind.Login));
        Assert.Throws<NotSupportedException>(() => LoginXEvents.VerifyCapabilities(
            rows.Where(row => row.Column != "total_time_ms"), LoginXEventKind.ProcessLoginFinish));
    }

    /// <summary>Malformed count-only payloads cannot become successful captures or duration measurements.</summary>
    [Theory]
    [InlineData("is_cached", "is_recovered")]
    [InlineData("<value>false</value>", "<value>unknown</value>")]
    [InlineData("event_sequence", "other_sequence")]
    [InlineData("2026-09-14T12:34:56.789Z", "bad-timestamp")]
    [InlineData("name=\"login\"", "name=\"process_login_finish\"")]
    public void LoginModeRejectsMissingOrInvalidFields(string oldValue, string newValue)
    {
        LoginXEventAccumulator accumulator = new(Session, eventKind: LoginXEventKind.Login);
        accumulator.Read(Ring(1, LoginEvent(1).Replace(oldValue, newValue, StringComparison.Ordinal)));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.NotEqual("captured", result.Status);
        Assert.Equal(0, result.CapturedEvents);
        Assert.False(result.DurationAvailable);
    }

    /// <summary>Empty targets, all loss counters, truncation, and bounded interval overflow remain explicit in count mode.</summary>
    [Theory]
    [InlineData(1, 0, 0, false)]
    [InlineData(0, 1, 0, false)]
    [InlineData(0, 0, 1, false)]
    [InlineData(0, 0, 0, true)]
    public void LoginModePreservesLossAndBounds(long events, long buffers, long target, bool truncated)
    {
        LoginXEventAccumulator accumulator = new(Session, maximumIntervals: 1, eventKind: LoginXEventKind.Login);
        accumulator.Read(Ring(0, ""));
        Assert.Equal("incomplete", accumulator.Snapshot(true, true).Status);
        accumulator.Read(Ring(1, LoginEvent(1), target, truncated), events, buffers);
        Assert.Equal("incomplete", accumulator.Snapshot(true, true).Status);
        accumulator.Read(Ring(2, LoginEvent(2, "true").Replace("12:34:56", "12:34:57")));
        LoginXEventResult result = accumulator.Snapshot(true, true);
        Assert.Equal("incomplete", result.Status);
        Assert.Single(result.Counts!.Intervals);
        Assert.Equal(2, result.CapturedEvents);
        Assert.Equal(events, result.DroppedEvents);
        Assert.Equal(buffers, result.DroppedBuffers);
        Assert.Equal(target, result.TargetDroppedEvents);
        Assert.Equal(truncated, result.Truncated);
        accumulator.Fail(new TimeoutException(), cleanup: true);
        Assert.Equal("failed", accumulator.Snapshot(true, false).Status);
    }

    /// <summary>Count mode detects eviction and interval overflow even without any reported server loss.</summary>
    [Fact]
    public void LoginModeDetectsUnreportedEvictionAndIntervalOverflow()
    {
        LoginXEventAccumulator evicted = new(Session, eventKind: LoginXEventKind.Login);
        evicted.Read(Ring(3, LoginEvent(3)));
        Assert.Equal("incomplete", evicted.Snapshot(true, true).Status);
        LoginXEventAccumulator bounded = new(Session, maximumIntervals: 1, eventKind: LoginXEventKind.Login);
        bounded.Read(Ring(2, LoginEvent(1) + LoginEvent(2).Replace("12:34:56", "12:34:57")));
        LoginXEventResult result = bounded.Snapshot(true, true);
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(2, result.CapturedEvents);
        Assert.Single(result.Counts!.Intervals);
    }

    /// <summary>Setup failures preserve count-only metadata and invalid enum values never enter SQL.</summary>
    [Fact]
    public void LoginModeSetupMetadataAndInvalidSelection()
    {
        LoginXEventResult result = LoginXEventResult.NotStarted("unavailable", null, LoginXEventKind.Login);
        Assert.Equal("unknown", result.Scope);
        Assert.Equal("login", result.EventName);
        Assert.False(result.DurationAvailable);
        Assert.Equal(0, result.Counts!.NewConnectionLogins);
        Assert.Equal(0, result.Counts.CachedConnectionLogins);
        Assert.Empty(result.Counts.Intervals);
        LoginXEvents collector = new("", Session, eventKind: LoginXEventKind.Login);
        Assert.Equal("login", collector.Finish().EventName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoginXEvents("", Session, eventKind: (LoginXEventKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginXEvents.CreateSessionSql(
            Session, false, LoginXEventScope.Database, (LoginXEventKind)999));
        Assert.Throws<ArgumentException>(() => LoginXEvents.CreateSessionSql(
            "unsafe';", false, LoginXEventScope.Database, LoginXEventKind.Login));
    }

    /// <summary>Builds shared metadata capabilities without opening an observer connection.</summary>
    /// <param name="events">Event-specific metadata columns.</param>
    /// <returns>Required shared predicate, action, target, and supplied event metadata.</returns>
    private static LoginXEventCapability[] Capabilities(params LoginXEventCapability[] events) =>
    [
        new("sqlserver", "client_app_name", "pred_source"),
        new("package0", "event_sequence", "action"),
        new("package0", "ring_buffer", "target", "max_memory", "customizable"),
        new("package0", "ring_buffer", "target", "max_events_limit", "customizable"),
        .. events
    ];

    /// <summary>Builds a count-only login event with no duration or success fields.</summary>
    /// <param name="sequence">Session-local event sequence.</param>
    /// <param name="cached">Whether the server identifies a cached connection login.</param>
    /// <returns>A synthetic login event.</returns>
    private static string LoginEvent(long sequence, string cached = "false") =>
        $"""
        <event name="login" package="sqlserver" timestamp="2026-09-14T12:34:56.789Z">
          <data name="is_cached"><value>{cached}</value></data>
          <action name="event_sequence" package="package0"><value>{sequence}</value></action>
        </event>
        """;

    /// <summary>Builds a synthetic target containing only bounded test events.</summary>
    /// <param name="total">Cumulative target event count.</param>
    /// <param name="events">Synthetic event XML.</param>
    /// <param name="dropped">Target drop counter.</param>
    /// <param name="truncated">Whether XML conversion omitted events.</param>
    /// <returns>A complete target document.</returns>
    private static string Ring(long total, string events, long dropped = 0, bool truncated = false) =>
        $"<RingBufferTarget totalEventsProcessed=\"{total}\" droppedCount=\"{dropped}\" truncated=\"{(truncated ? 1 : 0)}\">{events}</RingBufferTarget>";

    /// <summary>Builds a synthetic event with explicit millisecond and server timestamp fields.</summary>
    /// <param name="sequence">Session-local event sequence.</param>
    /// <param name="duration">Millisecond field value.</param>
    /// <param name="success">Login outcome field value.</param>
    /// <returns>A login completion event.</returns>
    private static string Event(long sequence, string duration = "1", string success = "true") =>
        $"""
        <event name="process_login_finish" package="sqlserver" timestamp="2026-09-14T12:34:56.789Z">
          <data name="total_time_ms"><value>{duration}</value></data>
          <data name="is_success"><value>{success}</value></data>
          <action name="event_sequence" package="package0"><value>{sequence}</value></action>
        </event>
        """;
}
