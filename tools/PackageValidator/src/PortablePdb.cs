// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;

namespace PackageValidator;

/// <summary>
/// Low-level helpers for reading identity and checksum information out of portable PDB byte
/// streams, used to match a symbol-package PDB to the assembly it belongs to.
/// </summary>
internal static class PortablePdb
{
    /// <summary>The metadata header signature <c>BSJB</c>, marking the start of a portable PDB blob.</summary>
    private const uint MetadataSignature = 0x424A5342;

    /// <summary>The length in bytes of the PDB id (a 16-byte GUID followed by a 4-byte stamp).</summary>
    private const int PdbIdLength = 20;

    private static readonly Guid SourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");
    private static readonly Guid EmbeddedSourceKind = new("0e8a571b-6926-466e-b4ad-8ab04611f5fe");

    /// <summary>Inspects document coverage without downloading sources or decompressing embedded source content.</summary>
    /// <param name="reader">The matched or embedded portable PDB metadata reader.</param>
    /// <param name="pdb">The symbol-package entry path or <c>"embedded"</c>.</param>
    /// <returns>The source documents that fail the offline Source Link or deterministic path checks.</returns>
    /// <exception cref="InvalidDataException">The source metadata or Source Link map is malformed.</exception>
    public static PdbSourceCoverage ReadSourceCoverage(MetadataReader reader, string pdb)
    {
        try
        {
            List<string> mappings = ReadSourceMappings(reader);
            var missing = new List<string>();
            var untracked = new List<string>();
            var nonNormalized = new List<string>();
            int documentCount = 0;
            foreach (DocumentHandle handle in reader.Documents)
            {
                Document document = reader.GetDocument(handle);
                if (document.Name.IsNil || document.Language.IsNil ||
                    document.HashAlgorithm.IsNil || document.Hash.IsNil)
                {
                    continue;
                }
                documentCount++;
                string path = reader.GetString(document.Name);
                bool embedded = reader.GetCustomDebugInformation(handle)
                    .Any(h => reader.GetGuid(reader.GetCustomDebugInformation(h).Kind) == EmbeddedSourceKind);
                if (!embedded)
                {
                    bool mapped = !path.Contains('*') && mappings.Any(key => key.EndsWith('*')
                        ? path.StartsWith(key[..^1], StringComparison.OrdinalIgnoreCase)
                        : string.Equals(path, key, StringComparison.OrdinalIgnoreCase));
                    if (!mapped)
                    {
                        missing.Add(path);
                    }
                    if (!mapped || path.Split(['/', '\\']).Any(segment =>
                        segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                        segment.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
                        segment.Equals("tmp", StringComparison.OrdinalIgnoreCase)))
                    {
                        untracked.Add(path);
                    }
                }
                if (!path.StartsWith("/_", StringComparison.OrdinalIgnoreCase))
                {
                    nonNormalized.Add(path);
                }
            }
            return new PdbSourceCoverage
            {
                Pdb = pdb,
                DocumentCount = documentCount,
                MissingSourceLinkDocuments = missing,
                UntrackedDocuments = untracked,
                NonNormalizedDocuments = nonNormalized,
            };
        }
        catch (Exception ex) when (ex is BadImageFormatException or JsonException)
        {
            throw new InvalidDataException($"Invalid source metadata in PDB '{pdb}'.", ex);
        }
    }

    /// <summary>Reads and validates module-level Source Link patterns using NPE's exact/trailing-wildcard rules.</summary>
    /// <param name="reader">The portable PDB reader.</param>
    /// <returns>Validated document path patterns; URL reachability is deliberately not checked.</returns>
    private static List<string> ReadSourceMappings(MetadataReader reader)
    {
        var mappings = new List<string>();
        foreach (CustomDebugInformationHandle handle in reader.GetCustomDebugInformation(
            MetadataTokens.EntityHandle(TableIndex.Module, 1)))
        {
            CustomDebugInformation data = reader.GetCustomDebugInformation(handle);
            if (reader.GetGuid(data.Kind) != SourceLinkKind)
            {
                continue;
            }
            using JsonDocument json = JsonDocument.Parse(
                reader.GetBlobBytes(data.Value), new JsonDocumentOptions { AllowTrailingCommas = true });
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Source Link must be a JSON object.");
            }
            foreach (JsonProperty property in json.RootElement.EnumerateObject())
            {
                if (property.Name != "documents")
                {
                    continue;
                }
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("Source Link 'documents' must be an object.");
                }
                foreach (JsonProperty mapping in property.Value.EnumerateObject())
                {
                    string key = mapping.Name;
                    int star = key.IndexOf('*');
                    if (key.Length == 0 || (star >= 0 && star != key.Length - 1) ||
                        mapping.Value.ValueKind != JsonValueKind.String)
                    {
                        throw new InvalidDataException("Invalid Source Link document mapping.");
                    }
                    string url = mapping.Value.GetString()!;
                    int urlStar = url.IndexOf('*');
                    if (urlStar >= 0 && (star < 0 || urlStar != url.LastIndexOf('*')))
                    {
                        throw new InvalidDataException("Invalid Source Link URL wildcard.");
                    }
                    mappings.Add(key);
                }
            }
        }
        return mappings;
    }

    /// <summary>
    /// Reads the debug GUID from a portable PDB.
    /// </summary>
    /// <param name="pdb">The full PDB byte content.</param>
    /// <returns>The PDB's debug GUID, or <see langword="null"/> if the bytes are not a readable portable PDB.</returns>
    public static Guid? TryReadGuid(byte[] pdb)
    {
        try
        {
            using var stream = new MemoryStream(pdb, writable: false);
            using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            MetadataReader reader = provider.GetMetadataReader();

            // The PDB id begins with the 16-byte GUID that matches the assembly's CodeView GUID; the
            // trailing bytes are a timestamp/stamp we do not need here.
            DebugMetadataHeader? header = reader.DebugMetadataHeader;
            if (header is not null && header.Id.Length >= 16)
            {
                return new Guid(header.Id.AsSpan(0, 16));
            }
        }
        catch (BadImageFormatException)
        {
            // Not a portable PDB (for example a Windows PDB); it cannot be matched by GUID.
        }

        return null;
    }

    /// <summary>
    /// Determines whether the given bytes look like a legacy Windows (MSF) PDB rather than a
    /// portable PDB. Windows PDBs cannot be matched by the portable-PDB GUID scheme.
    /// </summary>
    /// <param name="pdb">The PDB byte content.</param>
    /// <returns><see langword="true"/> if the bytes begin with the Windows PDB MSF signature.</returns>
    public static bool IsWindowsPdb(byte[] pdb)
    {
        // Windows PDBs begin with the ASCII magic "Microsoft C/C++ MSF 7.00\r\n\x1aDS".
        ReadOnlySpan<byte> magic = "Microsoft C/C++ MSF 7.00"u8;
        return pdb.Length >= magic.Length && pdb.AsSpan(0, magic.Length).SequenceEqual(magic);
    }

    /// <summary>
    /// Verifies a portable PDB against a checksum recorded in the owning assembly's debug directory.
    /// </summary>
    /// <param name="pdb">The full PDB byte content.</param>
    /// <param name="checksum">The checksum (algorithm and expected hash) recorded by the assembly.</param>
    /// <returns>
    /// <see langword="true"/> when the computed hash matches the expected hash; <see langword="false"/>
    /// when it does not; <see langword="null"/> when the checksum could not be evaluated (unknown
    /// algorithm or an unparseable PDB layout).
    /// </returns>
    /// <remarks>
    /// The recorded checksum is computed over the PDB content with its 20-byte PDB id zeroed, because
    /// for deterministic builds the id is derived from the checksum and written afterward. This
    /// method therefore locates the <c>#Pdb</c> stream, zeroes the id in a working copy, and hashes
    /// that copy with the named algorithm.
    /// </remarks>
    public static bool? TryVerifyChecksum(byte[] pdb, PdbChecksum checksum)
    {
        int idOffset = FindPdbIdOffset(pdb);
        if (idOffset < 0 || idOffset + PdbIdLength > pdb.Length)
        {
            return null;
        }

        using HashAlgorithm? algorithm = CreateHashAlgorithm(checksum.Algorithm);
        if (algorithm is null)
        {
            return null;
        }

        // Hash a copy in which the PDB id bytes are zeroed, matching how the checksum was produced.
        byte[] working = (byte[])pdb.Clone();
        Array.Clear(working, idOffset, PdbIdLength);
        byte[] computed = algorithm.ComputeHash(working);

        return computed.AsSpan().SequenceEqual(checksum.Hash);
    }

    /// <summary>
    /// Locates the byte offset of the PDB id (the start of the <c>#Pdb</c> metadata stream) within a
    /// portable PDB.
    /// </summary>
    /// <param name="pdb">The PDB byte content.</param>
    /// <returns>The offset of the <c>#Pdb</c> stream, or <c>-1</c> if it cannot be located.</returns>
    private static int FindPdbIdOffset(byte[] pdb)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(pdb, writable: false));

            if (reader.ReadUInt32() != MetadataSignature)
            {
                return -1;
            }

            reader.ReadUInt16(); // Major version.
            reader.ReadUInt16(); // Minor version.
            reader.ReadUInt32(); // Reserved.

            int versionLength = reader.ReadInt32();

            // A malformed or hostile PDB can encode a negative or oversized length; treat it as
            // unparseable rather than letting the seek below throw and fail the whole run.
            if (versionLength < 0 || versionLength > pdb.Length)
            {
                return -1;
            }

            reader.BaseStream.Position += versionLength; // Version string (padded to a 4-byte boundary).

            // ECMA-335 rounds the recorded version length up to a multiple of four, so the position is
            // normally already aligned; align defensively in case a producer records an unrounded
            // length, otherwise the following flags/stream-header reads would be misaligned.
            reader.BaseStream.Position = (reader.BaseStream.Position + 3) & ~3L;

            reader.ReadUInt16(); // Flags.
            ushort streamCount = reader.ReadUInt16();

            for (int i = 0; i < streamCount; i++)
            {
                uint offset = reader.ReadUInt32();
                reader.ReadUInt32(); // Stream size.
                string name = ReadStreamName(reader);
                if (name == "#Pdb")
                {
                    return (int)offset;
                }
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ArgumentOutOfRangeException)
        {
            // Truncated, malformed, or hostile PDB; treat as unlocatable.
        }

        return -1;
    }

    /// <summary>
    /// Reads a null-terminated, 4-byte-aligned ASCII metadata stream name from the current position.
    /// </summary>
    /// <param name="reader">The reader positioned at the start of a stream name.</param>
    /// <returns>The decoded stream name.</returns>
    private static string ReadStreamName(BinaryReader reader)
    {
        var bytes = new List<byte>(16);
        int read = 0;
        while (true)
        {
            byte b = reader.ReadByte();
            read++;
            if (b == 0)
            {
                break;
            }
            bytes.Add(b);
        }

        // Stream names are padded with additional null bytes to a 4-byte boundary.
        while (read % 4 != 0)
        {
            reader.ReadByte();
            read++;
        }

        return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
    }

    /// <summary>
    /// Creates a <see cref="HashAlgorithm"/> for a PDB checksum algorithm name.
    /// </summary>
    /// <remarks>
    /// Only SHA-2 family algorithms are supported; weak hashes such as SHA-1 are deliberately not
    /// recognized, so a PDB recording one is reported as inconclusive rather than verified.
    /// </remarks>
    /// <param name="algorithm">The algorithm name as recorded in the debug directory (for example <c>"SHA256"</c>).</param>
    /// <returns>A new hash algorithm instance, or <see langword="null"/> if the name is not recognized.</returns>
    private static HashAlgorithm? CreateHashAlgorithm(string algorithm) =>
        algorithm.ToUpperInvariant() switch
        {
            "SHA256" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            _ => null,
        };
}
