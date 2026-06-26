using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace PackageValidator;

/// <summary>
/// Reads Win32 version-resource information from native (unmanaged) binaries.
/// </summary>
internal static class NativeVersionReader
{
    /// <summary>
    /// Reads the Win32 version resource and architecture from a native binary's bytes.
    /// </summary>
    /// <param name="bytes">The full bytes of the native DLL.</param>
    /// <returns>
    /// A <see cref="NativeVersionInfo"/>, or <see langword="null"/> if no version-resource or
    /// architecture data could be read.
    /// </returns>
    public static NativeVersionInfo? Read(byte[] bytes)
    {
        string? architecture = ReadArchitecture(bytes);

        string? fileVersion = null;
        string? productVersion = null;
        string? productName = null;

        // FileVersionInfo needs a real file path; it parses the PE version resource on all
        // platforms, so write the bytes to a temp file, read, and clean up.
        string tempPath = Path.Combine(Path.GetTempPath(), $"pkgval-{Guid.NewGuid():N}.dll");
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(tempPath);
            fileVersion = NullIfEmpty(info.FileVersion);
            productVersion = NullIfEmpty(info.ProductVersion);
            productName = NullIfEmpty(info.ProductName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Could not stage or read the temp file; fall back to architecture only.
        }
        finally
        {
            TryDelete(tempPath);
        }

        if (fileVersion is null && productVersion is null && productName is null && architecture is null)
        {
            return null;
        }

        return new NativeVersionInfo
        {
            FileVersion = fileVersion,
            ProductVersion = productVersion,
            ProductName = productName,
            Architecture = architecture,
        };
    }

    /// <summary>
    /// Reads the processor architecture from a PE image's COFF header.
    /// </summary>
    /// <param name="bytes">The full bytes of the binary.</param>
    /// <returns>A short architecture name, or <see langword="null"/> if it cannot be determined.</returns>
    private static string? ReadArchitecture(byte[] bytes)
    {
        try
        {
            using var pe = new PEReader(new MemoryStream(bytes, writable: false));
            return pe.PEHeaders.CoffHeader.Machine switch
            {
                Machine.Amd64 => "x64",
                Machine.I386 => "x86",
                Machine.Arm64 => "arm64",
                Machine.Arm => "arm",
                Machine.IA64 => "ia64",
                Machine.Unknown => null,
                var other => other.ToString().ToLowerInvariant(),
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>Returns <see langword="null"/> for empty or whitespace strings, otherwise the trimmed value.</summary>
    private static string? NullIfEmpty(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Deletes a file, ignoring any failure.</summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Best-effort cleanup.
        }
    }
}
