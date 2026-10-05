using System.Configuration;
namespace Reader;
// Mirrors Abstractions: no compile-time knowledge of Owner.AuthSection.
public static class Read
{
    public static string Describe(string name)
    {
        if (ConfigurationManager.GetSection(name) is not ConfigurationSection s) return "section not found";
        var p = s.ElementInformation.Properties;
        var providers = (ProviderSettingsCollection)p["providers"].Value;
        var list = string.Join(",", System.Linq.Enumerable.Select(System.Linq.Enumerable.Cast<ProviderSettings>(providers), x => x.Name + "=>" + x.Type));
        return $"handler={s.GetType().FullName}, clientId={p["applicationClientId"].Value}, wam='{p["useWamBroker"].Value}', providers=[{list}]";
    }
}
