using Mono.Cecil;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KitLib.ApiCheck;

internal sealed class ManifestDocument {
    public Dictionary<string, string> Profiles { get; set; } = new(StringComparer.Ordinal);
    public List<TouchpointDocument> Touchpoints { get; set; } = [];
}

internal sealed class TouchpointDocument {
    public string Id { get; set; } = "";
    public string? Type { get; set; }
    public string? Member { get; set; }
    public string? Kind { get; set; }
    public bool Dynamic { get; set; }
    public bool Optional { get; set; }
    public Dictionary<string, ProfileMemberDocument>? Profiles { get; set; }
    public List<string>? Sources { get; set; }
}

internal sealed class ProfileMemberDocument {
    public string? Member { get; set; }
    public bool Skip { get; set; }
}

internal static class Program {
    static int Main(string[] args) {
        if (args.Length >= 3
            && args[0] == "--list-type"
            && !string.IsNullOrWhiteSpace(args[1])
            && !string.IsNullOrWhiteSpace(args[2])) {
            return ListTypeMembers(new FileInfo(args[2]), args[1]);
        }

        string? dllPath = null;
        string? profile = null;
        string? manifestPath = null;

        for (var i = 0; i < args.Length; i++) {
            switch (args[i]) {
                case "--dll" when i + 1 < args.Length:
                    dllPath = args[++i];
                    break;
                case "--profile" when i + 1 < args.Length:
                    profile = args[++i];
                    break;
                case "--manifest" when i + 1 < args.Length:
                    manifestPath = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    PrintUsage();
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(dllPath)
            || string.IsNullOrWhiteSpace(profile)
            || string.IsNullOrWhiteSpace(manifestPath)) {
            PrintUsage();
            return 1;
        }

        return Run(new FileInfo(dllPath), profile, new FileInfo(manifestPath));
    }

    static void PrintUsage() {
        Console.Error.WriteLine("Usage: KitLib.ApiCheck --dll <sts2.dll> --profile beta --manifest eng/api_touchpoints.yaml");
        Console.Error.WriteLine("       KitLib.ApiCheck --list-type <TypeName> <sts2.dll>");
    }

    static int ListTypeMembers(FileInfo dll, string shortName) {
        if (!dll.Exists) {
            Console.Error.WriteLine($"DLL not found: {dll.FullName}");
            return 1;
        }

        using var asm = AssemblyDefinition.ReadAssembly(dll.FullName);
        foreach (var type in asm.MainModule.Types.Where(t => t.Name == shortName)) {
            Console.WriteLine(type.FullName);
            Console.WriteLine("  properties: " + string.Join(", ", type.Properties.Select(p => p.Name)));
            Console.WriteLine("  fields: " + string.Join(", ", type.Fields.Select(f => f.Name)));
            Console.WriteLine("  methods: " + string.Join(", ", type.Methods.Select(m => m.Name)));
        }
        return 0;
    }

    static int Run(FileInfo dll, string profile, FileInfo manifest) {
        if (!dll.Exists) {
            Console.Error.WriteLine($"DLL not found: {dll.FullName}");
            return 1;
        }
        if (!manifest.Exists) {
            Console.Error.WriteLine($"Manifest not found: {manifest.FullName}");
            return 1;
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        ManifestDocument doc;
        try {
            doc = deserializer.Deserialize<ManifestDocument>(File.ReadAllText(manifest.FullName))
                ?? new ManifestDocument();
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Failed to parse manifest: {ex.Message}");
            return 1;
        }

        AssemblyIndex index;
        try {
            index = AssemblyIndex.Load(dll.FullName);
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Failed to load assembly: {ex.Message}");
            return 1;
        }

        var fails = new List<string>();
        var warns = new List<string>();
        var skipped = 0;
        var checkedCount = 0;

        foreach (var tp in doc.Touchpoints) {
            if (tp.Dynamic) {
                skipped++;
                warns.Add($"[SKIP] {profile} {tp.Id} (HarmonyTargetMethod — runtime resolved)");
                continue;
            }

            if (string.IsNullOrWhiteSpace(tp.Type) || string.IsNullOrWhiteSpace(tp.Member)) {
                skipped++;
                continue;
            }

            if (ShouldSkipTouchpoint(tp, profile)) {
                skipped++;
                warns.Add($"[SKIP] {profile} {tp.Id} (profile override)");
                continue;
            }

            var member = ResolveMemberName(tp, profile);
            if (!index.TryResolveType(tp.Type!, out var resolvedType)) {
                if (tp.Optional) {
                    skipped++;
                    warns.Add($"[OPTIONAL] {profile} {tp.Id} — type not found: {tp.Type}");
                    continue;
                }
                fails.Add(FormatFail(profile, tp, member, $"type not found: {tp.Type}"));
                continue;
            }

            if (!MemberExists(resolvedType!, member, tp.Kind ?? "method")) {
                if (tp.Optional) {
                    skipped++;
                    warns.Add($"[OPTIONAL] {profile} {tp.Id} ({member}) — member missing");
                    continue;
                }
                fails.Add(FormatFail(profile, tp, member, "member missing"));
                continue;
            }

            checkedCount++;
        }

        Console.WriteLine($"Profile: {profile}");
        Console.WriteLine($"DLL: {dll.FullName}");
        if (doc.Profiles.TryGetValue(profile, out var pinned))
            Console.WriteLine($"Pinned game version: {pinned}");
        Console.WriteLine($"Checked: {checkedCount}, skipped: {skipped}, failed: {fails.Count}");

        foreach (var w in warns)
            Console.WriteLine(w);
        foreach (var f in fails)
            Console.Error.WriteLine(f);

        return fails.Count == 0 ? 0 : 1;
    }

    static bool ShouldSkipTouchpoint(TouchpointDocument tp, string profile) =>
        tp.Profiles != null
        && tp.Profiles.TryGetValue(profile, out var alias)
        && alias.Skip;

    static string ResolveMemberName(TouchpointDocument tp, string profile) {
        if (tp.Profiles != null
            && tp.Profiles.TryGetValue(profile, out var alias)
            && !string.IsNullOrWhiteSpace(alias.Member))
            return alias.Member!;
        return tp.Member!;
    }

    static string FormatFail(string profile, TouchpointDocument tp, string member, string reason) {
        var src = tp.Sources is { Count: > 0 } ? tp.Sources[0] : "?";
        return $"[FAIL] {profile} {tp.Id} ({member}) — {reason} [{src}]";
    }

    static bool MemberExists(TypeDefinition type, string member, string kind) {
        return kind.ToLowerInvariant() switch {
            "property" => type.Properties.Any(p => string.Equals(p.Name, member, StringComparison.Ordinal))
                || HasMethod(type, "get_" + member),
            "field" => type.Fields.Any(f => string.Equals(f.Name, member, StringComparison.Ordinal)),
            _ => HasMethod(type, member),
        };
    }

    static bool HasMethod(TypeDefinition type, string name) =>
        type.Methods.Any(m => string.Equals(m.Name, name, StringComparison.Ordinal));

    static bool IsPreferredSts2Type(TypeDefinition type) =>
        type.Namespace?.StartsWith("MegaCrit.Sts2", StringComparison.Ordinal) == true;

    sealed class AssemblyIndex {
        readonly Dictionary<string, TypeDefinition> _byFullName = new(StringComparer.Ordinal);
        readonly Dictionary<string, TypeDefinition> _byShortName = new(StringComparer.Ordinal);

        public static AssemblyIndex Load(string dllPath) {
            var index = new AssemblyIndex();
            using var asm = AssemblyDefinition.ReadAssembly(
                dllPath,
                new ReaderParameters { ReadingMode = ReadingMode.Deferred });
            foreach (var type in asm.MainModule.GetTypes()) {
                if (type.Name.StartsWith("<", StringComparison.Ordinal))
                    continue;
                index._byFullName[type.FullName] = type;
                if (!index._byShortName.TryGetValue(type.Name, out var existing))
                    index._byShortName[type.Name] = type;
                else if (IsPreferredSts2Type(type) && !IsPreferredSts2Type(existing))
                    index._byShortName[type.Name] = type;
            }
            return index;
        }

        public bool TryResolveType(string typeName, out TypeDefinition? resolved) {
            if (_byFullName.TryGetValue(typeName, out resolved))
                return true;

            if (typeName.Contains('.')) {
                resolved = null;
                return false;
            }

            return _byShortName.TryGetValue(typeName, out resolved);
        }
    }
}
