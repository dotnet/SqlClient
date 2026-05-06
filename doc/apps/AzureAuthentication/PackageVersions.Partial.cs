namespace Microsoft.Data.SqlClient.Samples.AzureAuthentication;

/// <summary>
/// Hand-written companion to the auto-generated <c>PackageVersions.g.cs</c> file
/// (produced by <c>GeneratePackageVersions.targets</c> at build time).
///
/// The generated half contains a string constant for every NuGet PackageReference
/// in the project.  This partial class adds members that require conditional
/// compilation (e.g. the <c>AZURE_EXTENSIONS</c> define) and therefore cannot be
/// expressed by the purely data-driven code generator.
/// </summary>
internal static partial class PackageVersions
{
    /// <summary>
    /// Version of the Azure extensions package, or "N/A" when not referenced.
    /// </summary>
    public const string AzureExtensionsVersion =
        #if AZURE_EXTENSIONS
            MicrosoftDataSqlClientExtensionsAzure;
        #else
            "N/A";
        #endif
}
