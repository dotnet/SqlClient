// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ConnectionPoolRampStress;

internal sealed class ObservedSampleRunner
{
    public SupervisedSample Run(Settings settings, Sample sample, Action<Packet> progress, string? fixture = null,
        Func<string, string, int, ILoginXEventCapture>? createCapture = null, Action<string>? captureStarting = null)
    {
        if (!settings.XEvents) return new Supervisor().Run(settings, sample, progress, fixture);

        LoginXEventKind eventKind = settings.XEventEvent switch
        {
            "process_login_finish" => LoginXEventKind.ProcessLoginFinish,
            "login" => LoginXEventKind.Login,
            _ => throw new ArgumentException("Unsupported login event selection.")
        };
        string applicationName = $"ConnectionPoolRampStress_{Guid.NewGuid():N}";
        ILoginXEventCapture? capture = null;
        LoginXEventResult? telemetry = null;
        SupervisedSample result;
        SafeFailure? setupFailure = null;
        bool unsupported = false;
        try
        {
            string input = Environment.GetEnvironmentVariable(
                settings.XEventConnectionEnvironment ?? settings.ConnectionEnvironment) ?? throw new ArgumentException();
            capture = createCapture is null
                ? new LoginXEvents(input, applicationName, settings.XEventTimeoutSeconds, eventKind: eventKind)
                : createCapture(input, applicationName, settings.XEventTimeoutSeconds);
            captureStarting?.Invoke(applicationName);
            capture.Start();
            result = new Supervisor().Run(settings, sample, progress, fixture, applicationName);
        }
        catch (Exception exception)
        {
            // This is a process-boundary diagnostic, never the raw SQL exception or XML.
            setupFailure = Failure.Describe(exception, setup: true);
            unsupported = exception is NotSupportedException;
            result = new(sample, Outcome.SetupFailure, null, false, false, 0, null, setupFailure);
        }
        finally
        {
            // The observer belongs to the parent, so killing a workload child cannot skip cleanup.
            if (capture is not null) telemetry = capture.Finish();
        }

        telemetry ??= LoginXEventResult.NotStarted(unsupported ? "unavailable" : "failed", setupFailure, eventKind);
        if (setupFailure is not null)
            telemetry = telemetry with { Status = unsupported ? "unavailable" : "failed", Failure = setupFailure };
        else if (result.Outcome == Outcome.Incomplete && telemetry.Status == "captured")
            telemetry = telemetry with { Status = "incomplete" };
        return result with { XEvents = telemetry };
    }
}
