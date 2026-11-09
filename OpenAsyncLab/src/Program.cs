using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.SqlClient;

namespace OpenAsyncLab;

internal static class Program
{
    private const string PoolV2Switch = "Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2";
    private static volatile bool s_quit;

    private static int Main(string[] args)
    {
        bool demo = args.Contains("--demo");
        bool netem = args.Contains("--netem");
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Contains("--help"))
        {
            Console.WriteLine("OpenAsyncLab [--demo] [--netem] [--pool-v1 | --pool-v2] [--frames N]");
            Console.WriteLine("Reads SQLCLIENT_TUI_CONNECTION_STRING or prompts privately.");
            Console.WriteLine("Starts paused. Use Linux/root or sudo -n tc for --netem.");
            return 0;
        }
        if (args.Contains("--pool-v1") && args.Contains("--pool-v2"))
        {
            Console.Error.WriteLine("Choose only one pool version: --pool-v1 or --pool-v2.");
            return 2;
        }

        int frames = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--demo" or "--netem" or "--pool-v1" or "--pool-v2")
            {
                continue;
            }
            if (args[i] == "--frames" && ++i < args.Length
                && int.TryParse(args[i], out frames) && frames > 0 && demo)
            {
                continue;
            }
            Console.Error.WriteLine("Unknown/invalid arguments; use --help. --frames requires --demo.");
            return 2;
        }
        if (!demo && (Console.IsInputRedirected || Console.IsOutputRedirected))
        {
            Console.Error.WriteLine("Live mode requires an interactive terminal.");
            return 2;
        }

        if (args.Contains("--pool-v1") || args.Contains("--pool-v2"))
        {
            AppContext.SetSwitch(PoolV2Switch, args.Contains("--pool-v2"));
        }

        try
        {
            return Run(demo, netem, frames);
        }
        catch (Exception error) when (error is ArgumentException or IOException
            or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception
            or System.Text.Json.JsonException or PlatformNotSupportedException)
        {
            Console.Error.WriteLine("OpenAsyncLab: " + error.Message);
            return 1;
        }
    }

    private static string Connection()
    {
        const string Variable = "SQLCLIENT_TUI_CONNECTION_STRING";
        string? configured = Environment.GetEnvironmentVariable(Variable);
        Environment.SetEnvironmentVariable(Variable, null);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _ = new SqlConnectionStringBuilder(configured);
            return configured;
        }
        Console.Write("SQL host [127.0.0.1]: ");
        string host = Console.ReadLine() ?? "";
        if (host.Length == 0)
        {
            host = "127.0.0.1";
        }

        Console.Write("SQL user [sa]: ");
        string user = Console.ReadLine() ?? "";
        if (user.Length == 0)
        {
            user = "sa";
        }

        Console.Write("SQL password (not echoed): ");
        var password = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                throw new InvalidOperationException("Cancelled.");
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
        Console.WriteLine();
        return new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{host},1433",
            InitialCatalog = "master",
            UserID = user,
            Password = password.ToString(),
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 15
        }.ConnectionString;
    }

    private static int Run(bool demo, bool netemEnabled, int frames)
    {
        bool poolV2 = AppContext.TryGetSwitch(PoolV2Switch, out bool enabled) && enabled;
        if (!demo)
        {
            Console.WriteLine(poolV2 ? "Pool V2: ChannelDbConnectionPool"
                : "Pool V1: WaitHandleDbConnectionPool");
        }

        Settings settings = Settings.Default();
        string connection = demo ? "" : Connection();
        var measurements = new Measurements();
        var history = new History();
        var clock = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        using var limits = demo ? null : new PoolLimits();
        using var network = new NetworkDelay(!demo && netemEnabled, connection);
        var workload = demo ? null : new Workload(connection, measurements, settings);
        using var probes = demo ? null : new ProbePump(measurements, clock);
        int selected = 0, page = 0, painted = 0;
        string? edit = null;
        string status = demo ? "DEMO only. Controls are simulated."
            : "Paused. Do not share this SQL endpoint with a benchmark ramp.";
        bool running = demo, startAfterNetwork = false, demoChanged = false;
        double lastSample = -1;
        Sample? latest = null;
        ConsoleCancelEventHandler cancel = (_, e) =>
        {
            e.Cancel = true;
            s_quit = true;
        };
        Console.CancelKeyPress += cancel;
        using PosixSignalRegistration? terminate = OperatingSystem.IsLinux()
            ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                s_quit = true;
            }) : null;
        using PosixSignalRegistration? hangup = OperatingSystem.IsLinux()
            ? PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
            {
                context.Cancel = true;
                s_quit = true;
            }) : null;
        bool terminal = !Console.IsOutputRedirected;
        string? previousFrame = null;
        int previousWidth = 0, previousHeight = 0;
        if (terminal)
        {
            Console.Write("\u001b[?1049h\u001b[?25l");
        }

        try
        {
            while (!s_quit)
            {
                try
                {
                    if (network.Poll())
                    {
                        settings = settings with { DelayMilliseconds = network.Applied };
                        measurements.MarkChange();
                        status = "Netem applied. In-flight packets may span the transition.";
                        if (startAfterNetwork)
                        {
                            workload!.Start(settings.Workers);
                            running = true;
                            startAfterNetwork = false;
                        }
                    }
                }
                catch (Exception error) when (error is InvalidOperationException or IOException
                    or System.ComponentModel.Win32Exception or System.Text.Json.JsonException
                    or TimeoutException)
                {
                    workload?.Pause();
                    running = startAfterNetwork = false;
                    status = error.Message + " Workload paused.";
                }
                workload?.Reap();
                if (workload?.Failure != null || probes?.Failure != null)
                {
                    workload?.Pause();
                    running = false;
                    status = workload?.Failure is string failure
                        ? failure + ". Press Space to retry after workers drain."
                        : probes!.Failure!;
                }
                double now = clock.Elapsed.TotalSeconds;
                if (now - lastSample >= 1)
                {
                    if (demo)
                    {
                        latest = Demo(now, running, settings) with { Changed = demoChanged };
                        demoChanged = false;
                    }
                    else
                    {
                        (int pending, double oldest) = probes!.Pending();
                        process.Refresh();
                        latest = measurements.Take(
                            now, pending, oldest, workload!.Active, process.Threads.Count);
                    }
                    history.Add(latest);
                    lastSample = now;
                }
                // Leave the last column unused to avoid automatic wrapping and scrolling.
                int width = terminal ? Math.Max(1, Console.WindowWidth - 1) : 110;
                int height = terminal ? Console.WindowHeight : 34;
                string message = status;
                if (measurements.Error is string errorText)
                {
                    message += " Last error: " + errorText;
                }
                string frame = Screen.Render(settings, history, latest, lastSample, width, height,
                    selected, page, edit, message, demo, running, netemEnabled, poolV2);
                if (terminal)
                {
                    if (width != previousWidth || height != previousHeight)
                    {
                        previousFrame = null;
                    }

                    string update = Screen.Update(frame, previousFrame);
                    if (update.Length > 0)
                    {
                        Console.Write(update);
                    }
                    previousFrame = frame;
                    previousWidth = width;
                    previousHeight = height;
                }
                else
                {
                    Console.WriteLine(frame);
                }
                if (frames > 0 && ++painted >= frames)
                {
                    break;
                }

                if (!Console.IsInputRedirected && Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                    try
                    {
                        if (edit != null)
                        {
                            if (key.Key == ConsoleKey.Escape)
                            {
                                edit = null;
                            }
                            else if (key.Key == ConsoleKey.Backspace && edit.Length > 0)
                            {
                                edit = edit[..^1];
                            }
                            else if (char.IsAsciiDigit(key.KeyChar) && edit.Length < 6)
                            {
                                edit += key.KeyChar;
                            }
                            else if (key.Key == ConsoleKey.Enter)
                            {
                                if (!int.TryParse(edit, out int number))
                                {
                                    throw new ArgumentException("Enter a whole number.");
                                }
                                Apply(settings.WithValue(selected, number));
                                edit = null;
                            }
                        }
                        else if (key.Key == ConsoleKey.Escape)
                        {
                            s_quit = true;
                        }
                        else if (key.Key == ConsoleKey.UpArrow)
                        {
                            selected = (selected + Settings.ControlCount - 1) % Settings.ControlCount;
                        }
                        else if (key.Key == ConsoleKey.DownArrow)
                        {
                            selected = (selected + 1) % Settings.ControlCount;
                        }
                        else if (key.Key == ConsoleKey.Tab)
                        {
                            page++;
                        }
                        else if (key.Key == ConsoleKey.Enter)
                        {
                            edit = "";
                        }
                        else if (key.KeyChar is '+' or '=' or '-')
                        {
                            int step = selected == 4 ? 5 : 1;
                            int adjustment = key.KeyChar == '-' ? -step : step;
                            int next = settings.Value(selected) + adjustment;
                            Apply(settings.WithValue(selected, next));
                        }
                        else if (key.Key == ConsoleKey.P)
                        {
                            Apply(settings with { Pooling = !settings.Pooling });
                            status = settings.Pooling
                                ? "Pooled OpenAsync enabled. In-flight non-pooled opens drain."
                                : "Non-pooled OpenAsync enabled. Each new open creates a connection.";
                        }
                        else if (key.Key == ConsoleKey.L)
                        {
                            if (!poolV2)
                            {
                                throw new InvalidOperationException(
                                    "Dispatch selection applies only to Pool V2.");
                            }

                            Apply(settings with { LongRunningOpen = !settings.LongRunningOpen });
                            status = demo ? "DEMO: dispatch toggle is simulated."
                                : "V2 dispatch changed for new acquisitions; existing work is unchanged.";
                        }
                        else if (key.Key == ConsoleKey.C)
                        {
                            bool cleared = demo ? settings.Pooling : workload!.ClearPool();
                            status = !cleared ? "Non-pooled mode: no connection pool to clear."
                                : demo ? "DEMO: simulated pool clear; no SQL pool was changed."
                                : "Pool cleared. Borrowed connections are discarded when returned.";
                            if (cleared)
                            {
                                measurements.MarkChange();
                                demoChanged = true;
                            }
                        }
                        else if (key.Key is ConsoleKey.W or ConsoleKey.Q)
                        {
                            WorkloadKind kind = settings.Kind == WorkloadKind.OpenClose
                                ? WorkloadKind.OpenQueryClose : WorkloadKind.OpenClose;
                            Apply(settings with { Kind = kind });
                            status = "Workload changed for new iterations; in-flight work finishes.";
                        }
                        else if (key.Key == ConsoleKey.Spacebar)
                        {
                            if (running || startAfterNetwork)
                            {
                                workload?.Pause();
                                running = startAfterNetwork = false;
                                status = "Paused; in-flight operations are draining.";
                            }
                            else if (!demo && network.Enabled && !network.Ready)
                            {
                                network.Request(settings.DelayMilliseconds);
                                startAfterNetwork = true;
                                status = "Acquiring exclusive SQL netem queue before starting...";
                            }
                            else
                            {
                                workload?.Start(settings.Workers);
                                running = true;
                                status = "Running. Live changes may overlap in-flight operations.";
                            }
                            measurements.MarkChange();
                            demoChanged = true;
                        }
                    }
                    catch (Exception error) when (
                        error is ArgumentException or InvalidOperationException)
                    {
                        status = error.Message;
                    }
                }
                Thread.Sleep(100);
            }
            return workload?.Failure != null || probes?.Failure != null ? 1 : 0;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            if (terminal)
            {
                Console.Write("\u001b[?25h\u001b[?1049l");
            }

            // Restore capacity before draining continuations that may have been starved.
            limits?.Dispose();
            workload?.Dispose();
        }

        void Apply(Settings next)
        {
            next.Validate(Environment.ProcessorCount);
            if (!demo && next.DelayMilliseconds != settings.DelayMilliseconds)
            {
                network.Request(next.DelayMilliseconds);
                status = "Changing owned netem queue...";
                return;
            }
            if (!demo)
            {
                limits!.Apply(next.MinimumThreads, next.MaximumThreads);
                try
                {
                    workload!.Configure(next);
                }
                catch (Exception error) when (
                    error is ArgumentException or InvalidOperationException)
                {
                    limits.Apply(settings.MinimumThreads, settings.MaximumThreads);
                    throw;
                }

                if (next.LongRunningOpen != settings.LongRunningOpen)
                {
                    AppContext.SetSwitch(Settings.LongRunningSwitch, next.LongRunningOpen);
                }
            }
            settings = next;
            measurements.MarkChange();
            demoChanged = true;
            status = "Settings applied. Old SQL connections drain; history is retained.";
        }
    }

    private static Sample Demo(double now, bool running, Settings settings)
    {
        double wave = (Math.Sin(now / 3) + 1) / 2;
        double rate = running ? 100 + 300 * wave : 0;
        var values = new Dictionary<string, double?>
        {
            ["Opens/s"] = rate, ["Open p50 ~ms"] = running ? 2 + wave * 4 : null,
            ["Open p99 ~ms"] = running ? 12 + wave * 50 : null,
            ["Cycle p99 ~ms"] = running ? 20 + wave * 55 : null,
            ["Errors/s"] = 0, ["SQL in flight"] = running ? settings.Workers : 0,
            ["Active async workers"] = running ? settings.Workers : 0,
            ["Dispatch p99 ~ms"] = 1 + wave * 8, ["Producer p99 ~ms"] = wave * 2,
            ["Probes completed/s"] = 40, ["Probes outstanding"] = 0,
            ["Oldest probe ms"] = 0, ["Probes dropped/s"] = 0, ["Producer missed/s"] = 0,
            ["ThreadPool threads"] = settings.MinimumThreads, ["Busy workers (proxy)"] = 2,
            ["ThreadPool queued"] = wave * 5, ["Process threads"] = 22
        };
        return new Sample(now, values, false);
    }
}
