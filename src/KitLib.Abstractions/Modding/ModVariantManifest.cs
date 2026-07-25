using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Abstractions.Compat;

namespace KitLib.Abstractions.Modding;

public sealed class ModVariantManifestFile {
    public int Schema { get; set; } = ModVariantLayout.ManifestSchema;

    public List<ModVariantEntry> Variants { get; set; } = [];
}

public sealed class ModVariantEntry {
    public string CompatTarget { get; set; } = "";

    public string File { get; set; } = "";

    public string Sha256 { get; set; } = "";
}

public static class ModVariantManifestIO {
    private static readonly JsonSerializerOptions ReadOptions = new() {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ModVariantManifestFile Read(string path) {
        var text = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize<ModVariantManifestFile>(text, ReadOptions)
                       ?? throw new InvalidDataException($"Empty variant manifest: {path}");
        if (manifest.Schema != ModVariantLayout.ManifestSchema)
            throw new InvalidDataException(
                $"Unsupported variant manifest schema {manifest.Schema} in {path}; expected {ModVariantLayout.ManifestSchema}.");
        return manifest;
    }

    public static void Write(string path, ModVariantManifestFile manifest) {
        manifest.Schema = ModVariantLayout.ManifestSchema;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, WriteOptions) + Environment.NewLine);
    }

    public static string ComputeSha256Hex(string filePath) {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static ModVariantManifestFile CreateFromFlatLibDirectory(string libRoot, string modId) {
        if (!Directory.Exists(libRoot))
            throw new DirectoryNotFoundException($"Missing lib variants directory: {libRoot}");

        var variants = new List<ModVariantEntry>();
        foreach (var dllPath in Directory.EnumerateFiles(libRoot, "*.dll").OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)) {
            var fileName = Path.GetFileName(dllPath);
            if (!ModVariantLayout.TryParseVariantFileName(modId, fileName, out var compatTarget))
                continue;

            if (!Sts2GameVersion.TryParseCore(compatTarget, out _))
                throw new InvalidDataException($"Variant file has invalid compat target: {fileName}");

            variants.Add(new ModVariantEntry {
                CompatTarget = compatTarget,
                File = ModVariantLayout.VariantRelativePath(modId, compatTarget),
                Sha256 = ComputeSha256Hex(dllPath),
            });
        }

        if (variants.Count == 0)
            throw new InvalidDataException(
                $"No variant DLLs matching {modId}_<version>.dll found under {libRoot}");

        return new ModVariantManifestFile { Variants = variants };
    }
}
