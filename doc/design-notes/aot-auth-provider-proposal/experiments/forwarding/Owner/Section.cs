using System.Configuration;
namespace Owner;
// Mirrors SqlClient's internal section type, which stays in the "upper" assembly.
internal class AuthSection : ConfigurationSection
{
    [ConfigurationProperty("providers")] public ProviderSettingsCollection Providers => (ProviderSettingsCollection)this["providers"];
    [ConfigurationProperty("applicationClientId", IsRequired = false)] public string ApplicationClientId => this["applicationClientId"] as string ?? "";
    [ConfigurationProperty("useWamBroker", IsRequired = false)] public string UseWamBroker => this["useWamBroker"] as string ?? "";
}
public static class Anchor { public static void Touch() { } }
