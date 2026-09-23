// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PackageValidator.Tests;

/// <summary>Guards Source Link coverage and reproducible source paths using real portable PDB metadata.</summary>
public class SourceCoverageTests
{
    /// <summary>Checks the extra SourceRoot regression and generated-source exceptions without fetching sources.</summary>
    [Theory]
    [InlineData("/_1/src/File.cs", "/_/*", false, true, true, false)]
    [InlineData("/_/src/File.cs", "/_/*", false, false, false, false)]
    [InlineData("/_/obj/File.g.cs", null, true, false, false, false)]
    [InlineData("/_/obj/File.g.cs", "/_/*", false, false, true, false)]
    [InlineData("/_/temp/File.g.cs", null, false, true, true, false)]
    [InlineData("/_/TMP/File.g.cs", "/_/*", false, false, true, false)]
    [InlineData(@"C:\build\obj\File.g.cs", @"C:\build\*", false, false, true, true)]
    [InlineData("/home/build/src/File.cs", "/home/build/*", false, false, false, true)]
    [InlineData("/home/build/obj/File.cs", null, true, false, false, true)]
    [InlineData("/_/object/File.cs", "/_/*", false, false, false, false)]
    [InlineData("/_/src/File.cs", "/_/SRC/file.cs", false, false, false, false)]
    [InlineData("/_/src/File.cs", "/_/src/Other.cs", false, true, true, false)]
    [InlineData("/_/src/File.cs", "/_/SRC/*", false, false, false, false)]
    [InlineData("/_/src/File.cs", "/_/sr/*", false, true, true, false)]
    [InlineData("/_/src/*.cs", "/_/*", false, true, true, false)]
    public void Reads_document_coverage(
        string path, string? mapKey, bool embedded, bool missing, bool untracked, bool nonNormalized)
    {
        string? json = mapKey is null ? null : Map(mapKey);
        BlobBuilder pdb = CreatePdb(path, json, embedded);
        using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbImage(pdb.ToImmutableArray());

        PdbSourceCoverage coverage = PortablePdb.ReadSourceCoverage(provider.GetMetadataReader(), "test.pdb");

        Assert.Equal(1, coverage.DocumentCount);
        Assert.Equal(missing ? [path] : Array.Empty<string>(), coverage.MissingSourceLinkDocuments);
        Assert.Equal(untracked ? [path] : Array.Empty<string>(), coverage.UntrackedDocuments);
        Assert.Equal(nonNormalized ? [path] : Array.Empty<string>(), coverage.NonNormalizedDocuments);
    }

    /// <summary>Invalid Source Link data remains an inspection error rather than silently passing coverage.</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"documents\":[]}")]
    [InlineData("{\"documents\":{\"\": \"https://example.invalid/file\"}}")]
    [InlineData("{\"documents\":{\"/_/*/file\": \"https://example.invalid/*\"}}")]
    [InlineData("{\"documents\":{\"/_/*\": \"https://example.invalid/**\"}}")]
    [InlineData("{\"documents\":{\"/_/file\": \"https://example.invalid/*\"}}")]
    [InlineData("{\"documents\":{\"/_/*\": 1}}")]
    public void Rejects_invalid_source_maps(string json)
    {
        BlobBuilder pdb = CreatePdb("/_/src/File.cs", json);
        using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbImage(pdb.ToImmutableArray());
        Assert.Throws<InvalidDataException>(
            () => PortablePdb.ReadSourceCoverage(provider.GetMetadataReader(), "test.pdb"));
    }

    /// <summary>Source map extensibility, trailing commas, and URL suffixes follow NPE matching semantics.</summary>
    [Theory]
    [InlineData("{\"documents\":{\"/_/*\":\"https://example.invalid/*?raw=true\"},}")]
    [InlineData("{\"documents\":{\"/_/*\":\"https://example.invalid/raw\"},\"future\":true}")]
    [InlineData("{\"documents\":{\"/_/src/File.cs\":\"https://example.invalid/file\"}}")]
    public void Accepts_source_map_variants(string json)
    {
        BlobBuilder pdb = CreatePdb("/_/src/File.cs", json);
        using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbImage(pdb.ToImmutableArray());
        Assert.Empty(PortablePdb.ReadSourceCoverage(provider.GetMetadataReader(), "test.pdb").MissingSourceLinkDocuments);
    }

    /// <summary>Incomplete document records are ignored just as in NPE, rather than reported as untracked sources.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Skips_incomplete_document_records(int missingHandle)
    {
        BlobBuilder pdb = CreatePdb("/home/build/obj/File.cs", null, missingHandle: missingHandle);
        using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbImage(pdb.ToImmutableArray());
        PdbSourceCoverage coverage = PortablePdb.ReadSourceCoverage(provider.GetMetadataReader(), "test.pdb");
        Assert.Equal(0, coverage.DocumentCount);
        Assert.Empty(coverage.MissingSourceLinkDocuments);
        Assert.Empty(coverage.UntrackedDocuments);
        Assert.Empty(coverage.NonNormalizedDocuments);
    }

    /// <summary>Embedded PDB source coverage is inspected even when sibling symbol processing is disabled.</summary>
    [Fact]
    public void Embedded_pdb_reports_source_findings()
    {
        BlobBuilder pdb = CreatePdb("/_1/src/File.cs", Map("/_/*"));
        var debug = new DebugDirectoryBuilder();
        debug.AddEmbeddedPortablePdbEntry(pdb, 0x0100);
        byte[] assembly = CreateAssembly(debug);
        using var stream = new MemoryStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update);
        ZipArchiveEntry entry = archive.CreateEntry("lib/net10.0/Test.dll");
        using (Stream content = entry.Open())
        {
            content.Write(assembly);
        }
        BinaryReport binary = AssemblyInspector.Inspect(entry);
        SymbolPackageInfo symbols = SymbolResolver.Resolve("unused.nupkg", [binary], false);
        PackageReport report = Package(binary, symbols);
        Validator.Validate(report);

        Assert.True(binary.HasSymbols);
        Finding finding = Assert.Single(report.Findings!, f => f.Category == Categories.MissingSourceLink);
        Assert.Equal(binary.Path, finding.Target);
        Assert.Contains("/_1/src/File.cs", finding.Message);
        Assert.Equal("embedded", Assert.Single(binary.SourceCoverage!).Pdb);
    }

    /// <summary>Only GUID-matched symbol-package PDBs supply source findings, including all three gate categories.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Symbol_package_only_checks_matched_pdbs(bool matches)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "source-coverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] pdb = CreatePdb(@"C:\build\obj\File.g.cs", null).ToArray();
            string packagePath = Path.Combine(directory, "Test.nupkg");
            using (ZipArchive archive = ZipFile.Open(Path.ChangeExtension(packagePath, ".snupkg"), ZipArchiveMode.Create))
            using (Stream content = archive.CreateEntry("lib/net10.0/Test.pdb").Open())
            {
                content.Write(pdb);
            }
            var binary = new BinaryReport
            {
                Path = "lib/net10.0/Test.dll",
                Kind = BinaryKind.Implementation,
                IsManagedAssembly = true,
                CodeViewGuid = matches ? PortablePdb.TryReadGuid(pdb) : Guid.NewGuid(),
            };
            PackageReport report = Package(binary, SymbolResolver.Resolve(packagePath, [binary], true));
            Validator.Validate(report);

            string[] sourceCategories =
                [Categories.MissingSourceLink, Categories.UntrackedSource, Categories.NonDeterministicSourcePath];
            Assert.All(sourceCategories, category =>
            {
                Assert.Contains(category, Categories.All);
                Assert.Equal(matches, report.Findings!.Any(f => f.Category == category));
            });
            Assert.Equal(matches, binary.SourceCoverage is { Count: 1 });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Preserves negative and corrected SourceRoot baselines through real nupkg/snupkg archive inspection.</summary>
    [Theory]
    [InlineData("/_1/src/File.cs", true)]
    [InlineData("/_/src/File.cs", false)]
    public void Package_inspection_detects_extra_source_root(string document, bool missing)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "source-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] pdb = CreatePdb(document, Map("/_/*")).ToArray();
            var debug = new DebugDirectoryBuilder();
            debug.AddCodeViewEntry("Test.pdb", new BlobContentId(PortablePdb.TryReadGuid(pdb)!.Value, 0), 0x0100);
            string packagePath = Path.Combine(directory, "Test.nupkg");
            using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                using (Stream content = archive.CreateEntry("lib/net10.0/Test.dll").Open())
                {
                    content.Write(CreateAssembly(debug));
                }
                using var manifest = new StreamWriter(archive.CreateEntry("Test.nuspec").Open());
                manifest.Write("<package><metadata><id>Test</id><version>1.0.0</version></metadata></package>");
            }
            using (ZipArchive archive = ZipFile.Open(Path.ChangeExtension(packagePath, ".snupkg"), ZipArchiveMode.Create))
            using (Stream content = archive.CreateEntry("lib/net10.0/Test.pdb").Open())
            {
                content.Write(pdb);
            }

            PackageReport report = PackageInspector.Inspect(packagePath, processSnupkg: true);
            Validator.Validate(report);

            BinaryReport binary = Assert.Single(report.Binaries);
            Assert.True(binary.SymbolPackageSymbolsMatch);
            Assert.True(binary.HasSymbols);
            Assert.Equal(missing, report.Findings!.Any(f => f.Category == Categories.MissingSourceLink));
            Assert.Equal(missing, report.Findings!.Any(f => f.Category == Categories.UntrackedSource));
            Assert.DoesNotContain(report.Findings!, f =>
                f.Category == Categories.NonDeterministicSourcePath);
            if (missing)
            {
                Assert.Contains(document, Assert.Single(
                    report.Findings!, f => f.Category == Categories.MissingSourceLink).Message);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Reference and satellite assemblies with no PDB must not create source-coverage findings.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Legitimate_symbolless_assemblies_are_exempt(bool reference)
    {
        var binary = new BinaryReport
        {
            Path = "ref/net10.0/Test.dll",
            Kind = reference ? BinaryKind.Reference : BinaryKind.Satellite,
            IsManagedAssembly = true,
        };
        PackageReport report = Package(binary, SymbolResolver.Resolve("unused.nupkg", [binary], false));
        Validator.Validate(report);
        Assert.Null(report.Findings);
    }

    /// <summary>Serializes an exact or wildcard map with an inert URL, without network access.</summary>
    /// <param name="key">The source path pattern.</param>
    /// <returns>Source Link JSON containing one mapping.</returns>
    private static string Map(string key) => JsonSerializer.Serialize(new
    {
        documents = new Dictionary<string, string>
        {
            [key] = key.EndsWith('*') ? "https://example.invalid/*" : "https://example.invalid/file",
        },
    });

    /// <summary>Builds an in-memory portable PDB with a document and optional module/document custom debug records.</summary>
    /// <param name="path">The document path.</param>
    /// <param name="json">Source Link JSON, or null to omit the module record.</param>
    /// <param name="embedded">Whether to embed the document's source.</param>
    /// <param name="missingHandle">A document handle to omit (1: name, 2: language, 3: hash algorithm, 4: hash).</param>
    /// <returns>The serialized portable PDB.</returns>
    private static BlobBuilder CreatePdb(string path, string? json, bool embedded = false, int missingHandle = 0)
    {
        var metadata = new MetadataBuilder();
        DocumentHandle document = metadata.AddDocument(
            missingHandle == 1 ? default : metadata.GetOrAddDocumentName(path),
            missingHandle == 3 ? default : metadata.GetOrAddGuid(new Guid("8829d00f-11b8-4213-878b-770e8597ac16")),
            missingHandle == 4 ? default : metadata.GetOrAddBlob(System.Security.Cryptography.SHA256.HashData("//"u8)),
            missingHandle == 2 ? default : metadata.GetOrAddGuid(new Guid("3f5162f8-07c6-11d3-9053-00c04fa302a1")));
        if (json is not null)
        {
            metadata.AddCustomDebugInformation(
                MetadataTokens.EntityHandle(TableIndex.Module, 1),
                metadata.GetOrAddGuid(new Guid("cc110556-a091-4d38-9fec-25ab9a351a6a")),
                metadata.GetOrAddBlob(Encoding.UTF8.GetBytes(json)));
        }
        if (embedded)
        {
            metadata.AddCustomDebugInformation(
                document,
                metadata.GetOrAddGuid(new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe")),
                metadata.GetOrAddBlob(new byte[] { 0, 0, 0, 0, 47, 47 }));
        }
        var rows = new int[MetadataTokens.TableCount];
        rows[(int)TableIndex.Module] = 1;
        var builder = new PortablePdbBuilder(metadata, rows.ToImmutableArray(), default);
        var result = new BlobBuilder();
        builder.Serialize(result);
        return result;
    }

    /// <summary>Builds a metadata-only assembly with the supplied debug directory for embedded-PDB integration testing.</summary>
    /// <param name="debug">The PE debug directory to include.</param>
    /// <returns>The serialized PE image.</returns>
    private static byte[] CreateAssembly(DebugDirectoryBuilder debug)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Test.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("Test"), new Version(1, 0, 0, 0), default, default, default, AssemblyHashAlgorithm.None);
        var builder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll | Characteristics.ExecutableImage),
            new MetadataRootBuilder(metadata), new BlobBuilder(), debugDirectoryBuilder: debug);
        var result = new BlobBuilder();
        builder.Serialize(result);
        return result.ToArray();
    }

    /// <summary>Wraps an inspected binary for validation with unrelated package-signing findings suppressed.</summary>
    /// <param name="binary">The inspected assembly.</param>
    /// <param name="symbols">Its resolved symbols.</param>
    /// <returns>A package report ready for validation.</returns>
    private static PackageReport Package(BinaryReport binary, SymbolPackageInfo symbols) => new()
    {
        PackageFile = "Test.nupkg",
        IsSigned = true,
        Binaries = [binary],
        SymbolPackage = symbols,
    };
}
