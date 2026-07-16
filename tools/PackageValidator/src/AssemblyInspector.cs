// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;

namespace PackageValidator;

/// <summary>
/// Inspects a single <c>.dll</c> archive entry and extracts its identity, version, signing, and
/// debug information without loading the assembly into the runtime.
/// </summary>
internal static class AssemblyInspector
{
    /// <summary>
    /// Inspects a single <c>.dll</c> archive entry.
    /// </summary>
    /// <param name="entry">The <c>.dll</c> entry within the package archive.</param>
    /// <returns>
    /// A <see cref="BinaryReport"/>. For native or non-assembly DLLs this is a lightweight report
    /// with <see cref="BinaryReport.IsManagedAssembly"/> set to <see langword="false"/>.
    /// </returns>
    public static BinaryReport Inspect(ZipArchiveEntry entry)
    {
        // PEReader requires a seekable stream, but the zip entry stream is forward-only, so copy the
        // bytes into a seekable buffer first.
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            using (Stream stream = entry.Open())
            {
                stream.CopyTo(buffer);
            }
            bytes = buffer.ToArray();
        }

        try
        {
            using var pe = new PEReader(new MemoryStream(bytes, writable: false));

            // Native DLLs (for example the SNI runtimes) have no CLI metadata at all; report their
            // Win32 version-resource information instead of managed identity.
            if (!pe.HasMetadata)
            {
                return BinaryReport.Native(entry.FullName, NativeVersionReader.Read(bytes));
            }

            MetadataReader reader = pe.GetMetadataReader();

            // A managed module that is not itself an assembly (no manifest) carries no
            // version/strong-name identity.
            if (!reader.IsAssembly)
            {
                return new BinaryReport
                {
                    Path = entry.FullName,
                    Kind = BinaryKind.Other,
                    IsManagedAssembly = false,
                };
            }

            AssemblyDefinition asm = reader.GetAssemblyDefinition();

            // The assembly version and culture come straight from the assembly manifest row.
            string name = reader.GetString(asm.Name);
            string assemblyVersion = asm.Version.ToString();
            string culture = asm.Culture.IsNil ? "neutral" : reader.GetString(asm.Culture);
            if (string.IsNullOrEmpty(culture))
            {
                culture = "neutral";
            }

            // The public key (if the assembly is strong-name signed) yields the public key token.
            byte[] publicKey = reader.GetBlobBytes(asm.PublicKey);
            string? publicKeyToken = ComputePublicKeyToken(publicKey);

            // The strong-name signed CorFlag distinguishes a fully signed assembly from a
            // delay-signed one (which carries a public key but was never signed).
            SigningStatus signing = DetermineSigningStatus(pe, publicKeyToken is not null);

            // Read the debug directory to learn which PDB (by GUID) this assembly was built with,
            // whether it already carries an embedded portable PDB, and any recorded PDB checksums.
            (Guid? codeViewGuid, bool hasEmbeddedPdb, List<PdbChecksum>? checksums) = ReadDebugInfo(pe);

            // File, informational, and target-framework versions are assembly-level custom
            // attributes, so scan for them by attribute type name.
            string? fileVersion = null;
            string? informationalVersion = null;
            string? targetFramework = null;
            foreach (CustomAttributeHandle handle in asm.GetCustomAttributes())
            {
                CustomAttribute attribute = reader.GetCustomAttribute(handle);
                switch (GetAttributeTypeName(reader, attribute))
                {
                    case "System.Reflection.AssemblyFileVersionAttribute":
                        fileVersion = DecodeStringArgument(reader, attribute);
                        break;
                    case "System.Reflection.AssemblyInformationalVersionAttribute":
                        informationalVersion = DecodeStringArgument(reader, attribute);
                        break;
                    case "System.Runtime.Versioning.TargetFrameworkAttribute":
                        targetFramework = DecodeStringArgument(reader, attribute);
                        break;
                }
            }

            // Compose the canonical strong-name display name in the same shape AssemblyName.FullName
            // would produce, using "null" when the assembly is not strong-name signed.
            string strongName =
                $"{name}, Version={assemblyVersion}, Culture={culture}, " +
                $"PublicKeyToken={publicKeyToken ?? "null"}";

            return new BinaryReport
            {
                Path = entry.FullName,
                Kind = BinaryClassifier.Classify(entry.FullName),
                IsManagedAssembly = true,
                AssemblyName = name,
                AssemblyVersion = assemblyVersion,
                FileVersion = fileVersion,
                InformationalVersion = informationalVersion,
                TargetFramework = targetFramework,
                Culture = culture,
                PublicKeyToken = publicKeyToken,
                SigningStatus = signing,
                StrongName = strongName,
                CodeViewGuid = codeViewGuid,
                Checksums = checksums,
                HasEmbeddedSymbols = hasEmbeddedPdb,
            };
        }
        catch (BadImageFormatException)
        {
            // The entry was not a valid PE image (for example a renamed data file). Report it as
            // native rather than failing the whole package inspection.
            return BinaryReport.Native(entry.FullName);
        }
    }

    /// <summary>
    /// Determines the strong-name signing state of an assembly from its CLI header flags.
    /// </summary>
    /// <param name="pe">The PE reader positioned over the assembly.</param>
    /// <param name="hasPublicKey">Whether the assembly manifest declares a public key.</param>
    /// <returns>The inferred <see cref="SigningStatus"/>.</returns>
    private static SigningStatus DetermineSigningStatus(PEReader pe, bool hasPublicKey)
    {
        if (!hasPublicKey)
        {
            return PackageValidator.SigningStatus.Unsigned;
        }

        CorHeader? cor = pe.PEHeaders.CorHeader;
        bool strongNameSigned = cor is not null && (cor.Flags & CorFlags.StrongNameSigned) != 0;
        return strongNameSigned
            ? PackageValidator.SigningStatus.Signed
            : PackageValidator.SigningStatus.DelaySigned;
    }

    /// <summary>
    /// Reads an assembly's debug directory to determine the PDB it was built against, whether it
    /// embeds a portable PDB, and any recorded PDB checksums.
    /// </summary>
    /// <param name="pe">The PE reader positioned over the assembly.</param>
    /// <returns>The CodeView GUID, the embedded-PDB flag, and the recorded checksums (if any).</returns>
    private static (Guid? CodeViewGuid, bool HasEmbeddedPdb, List<PdbChecksum>? Checksums) ReadDebugInfo(PEReader pe)
    {
        Guid? codeViewGuid = null;
        bool hasEmbeddedPdb = false;
        List<PdbChecksum>? checksums = null;

        foreach (DebugDirectoryEntry entry in pe.ReadDebugDirectory())
        {
            switch (entry.Type)
            {
                case DebugDirectoryEntryType.CodeView:
                    try
                    {
                        codeViewGuid = pe.ReadCodeViewDebugDirectoryData(entry).Guid;
                    }
                    catch (BadImageFormatException)
                    {
                        // Malformed CodeView record; leave the GUID unknown.
                    }
                    break;

                case DebugDirectoryEntryType.EmbeddedPortablePdb:
                    hasEmbeddedPdb = true;
                    break;

                case DebugDirectoryEntryType.PdbChecksum:
                    try
                    {
                        PdbChecksumDebugDirectoryData data = pe.ReadPdbChecksumDebugDirectoryData(entry);
                        (checksums ??= []).Add(new PdbChecksum
                        {
                            Algorithm = data.AlgorithmName,
                            Hash = data.Checksum.ToArray(),
                        });
                    }
                    catch (BadImageFormatException)
                    {
                        // Malformed checksum record; skip it.
                    }
                    break;
            }
        }

        return (codeViewGuid, hasEmbeddedPdb, checksums);
    }

    /// <summary>
    /// Computes the 8-byte public key token (as a lowercase hex string) from a strong-name public
    /// key blob.
    /// </summary>
    /// <param name="publicKey">The raw public key bytes from the assembly manifest.</param>
    /// <returns>
    /// The 16-character hex public key token, or <see langword="null"/> when the assembly is not
    /// strong-name signed (empty public key).
    /// </returns>
    public static string? ComputePublicKeyToken(byte[] publicKey)
    {
        if (publicKey is null || publicKey.Length == 0)
        {
            return null;
        }

        // The token is defined by the CLI spec as the low 8 bytes of the SHA-1 hash of the public
        // key, emitted in reverse (little-endian) order.
        byte[] hash = SHA1.HashData(publicKey);
        var token = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            token[i] = hash[hash.Length - 1 - i];
        }

        var sb = new StringBuilder(16);
        foreach (byte b in token)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Resolves the full type name of the attribute that a custom attribute instance constructs.
    /// </summary>
    private static string? GetAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                MemberReference memberRef = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                return GetTypeName(reader, memberRef.Parent);

            case HandleKind.MethodDefinition:
                MethodDefinition methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                return GetTypeName(reader, methodDef.GetDeclaringType());

            default:
                return null;
        }
    }

    /// <summary>
    /// Builds the fully qualified name of a type referenced by a type reference or definition handle.
    /// </summary>
    private static string? GetTypeName(MetadataReader reader, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeReference:
                TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
                return Combine(reader.GetString(typeRef.Namespace), reader.GetString(typeRef.Name));

            case HandleKind.TypeDefinition:
                TypeDefinition typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return Combine(reader.GetString(typeDef.Namespace), reader.GetString(typeDef.Name));

            default:
                return null;
        }

        static string Combine(string ns, string name) =>
            string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <summary>
    /// Decodes the value of a custom attribute whose constructor takes a single string argument.
    /// </summary>
    private static string? DecodeStringArgument(MetadataReader reader, CustomAttribute attribute)
    {
        BlobReader blob = reader.GetBlobReader(attribute.Value);

        // Every custom attribute value blob begins with a fixed 0x0001 prolog; anything else means
        // the blob is malformed or not the single-string shape expected here.
        if (blob.RemainingBytes < 2 || blob.ReadUInt16() != 0x0001)
        {
            return null;
        }

        return blob.ReadSerializedString();
    }
}
