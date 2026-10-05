using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient;

using var listener = new AuthTraceListener();
#if CFGPROBE
// Read SqlClient's section the way Abstractions would: no reference to the internal handler type.
try
{
    var sec = System.Configuration.ConfigurationManager.GetSection("SqlClientAuthenticationProviders");
    if (sec is System.Configuration.ConfigurationSection cs)
    {
        var props = cs.ElementInformation.Properties;
        var prov = (System.Configuration.ProviderSettingsCollection)props["providers"]!.Value;
        var names = string.Join(",", System.Linq.Enumerable.Select(System.Linq.Enumerable.Cast<System.Configuration.ProviderSettings>(prov), x => x.Name + "=>" + x.Type));
        Console.WriteLine($"PROBE handler={cs.GetType().FullName}; clientId='{props["applicationClientId"]!.Value}'; useWamBroker='{props["useWamBroker"]!.Value}'; initializerType='{props["initializerType"]!.Value}'; providers=[{names}]");
    }
    else Console.WriteLine($"PROBE section={(sec is null ? "null" : sec.GetType().FullName)}");
}
catch (Exception e) { Console.WriteLine($"PROBE threw {e.GetType().Name}: {e.Message.Split('\n')[0]}"); }
#endif

// Root SqlClient the way a real app does.
using var conn = new SqlConnection("Server=localhost;Authentication=Active Directory Default");
if (args.Length > 0) conn.Open(); // rooted like a real app, never executed

var before = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault);
Console.WriteLine($"GetProvider(ActiveDirectoryDefault) = {before?.GetType().FullName ?? "null"}");

bool set = SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, new MyProvider());
var after = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive);
Console.WriteLine($"SetProvider(ActiveDirectoryInteractive, MyProvider) = {set}; GetProvider -> {after?.GetType().Name ?? "null"}");

Console.WriteLine($"Initializer SetProvider result={MyInit.Result?.ToString() ?? "n/a"} error={MyInit.Error ?? "none"}; GetProvider(DeviceCodeFlow) -> {SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)?.GetType().Name ?? "null"}");
foreach (var m in listener.Messages) Console.WriteLine("  trace: " + m);

public sealed class MyProvider : SqlAuthenticationProvider
{
    public override bool IsSupported(SqlAuthenticationMethod m) => true;
    public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters p) => throw new NotSupportedException();
}

sealed class AuthTraceListener : EventListener
{
    public List<string> Messages { get; } = new();
    protected override void OnEventSourceCreated(EventSource s)
    {
        if (s.Name == "Microsoft.Data.SqlClient.EventSource") EnableEvents(s, EventLevel.Verbose, (EventKeywords)(-1));
    }
    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        var text = e.Payload is { Count: > 0 } ? e.Payload[0]?.ToString() ?? "" : "";
        if (text.Contains("Azure") || text.Contains("SqlAuthenticationProvider")) lock (Messages) Messages.Add(text.Length > 700 ? text[..700] + "…" : text);
    }
}

public sealed class MyInit : SqlAuthenticationInitializer
{
    public static bool? Result;
    public static string? Error;
    public override void Initialize()
    {
        try { Result = SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, new MyProvider()); }
        catch (Exception e) { Error = e.GetType().Name + ": " + e.Message; }
    }
}
