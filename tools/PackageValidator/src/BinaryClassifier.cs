namespace PackageValidator;

/// <summary>
/// Classifies a DLL path into the role it plays inside a NuGet package.
/// </summary>
internal static class BinaryClassifier
{
    /// <summary>
    /// Classifies a managed DLL by its path within the package.
    /// </summary>
    /// <param name="path">The DLL path within the package archive (forward-slash separated).</param>
    /// <returns>The inferred <see cref="BinaryKind"/> (never <see cref="BinaryKind.Native"/>).</returns>
    public static BinaryKind Classify(string path)
    {
        string normalized = path.Replace('\\', '/');
        string fileName = System.IO.Path.GetFileName(normalized);

        // Satellite resource assemblies are named "<parent>.resources.dll" and live under a culture
        // subfolder. The name suffix alone is a reliable discriminator.
        if (fileName.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
        {
            return BinaryKind.Satellite;
        }

        // Reference assemblies are shipped under ref/ and carry no executable IL.
        if (normalized.StartsWith("ref/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/ref/", StringComparison.OrdinalIgnoreCase))
        {
            return BinaryKind.Reference;
        }

        // Implementation assemblies live under lib/ or runtimes/<rid>/lib/. Restrict the nested
        // match to runtimes/ paths so unrelated folders (for example build/lib/) are not treated
        // as implementation assemblies.
        if (normalized.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
            || (normalized.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("/lib/", StringComparison.OrdinalIgnoreCase)))
        {
            return BinaryKind.Implementation;
        }

        return BinaryKind.Other;
    }
}
