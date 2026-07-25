using KitLib.Abstractions.Modding;

if (!TryParseArgs(args, out var bundleRoot, out var modId, out var requiredCompatTargets))
    return PrintUsage(1);

var libRoot = Path.Combine(bundleRoot, ModVariantLayout.LibDirectoryName);
var manifest = ModVariantManifestIO.CreateFromFlatLibDirectory(libRoot, modId);

if (requiredCompatTargets.Count > 0) {
    var present = manifest.Variants.Select(entry => entry.CompatTarget).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var missing = requiredCompatTargets.Where(target => !present.Contains(target)).OrderBy(static x => x).ToList();
    if (missing.Count > 0) {
        Console.Error.WriteLine(
            $"Release bundle missing variant(s): {string.Join(", ", missing)}. Build all API profiles before compose.");
        return 1;
    }
}

var manifestPath = Path.Combine(bundleRoot, ModVariantLayout.ManifestFileName(modId));
ModVariantManifestIO.Write(manifestPath, manifest);
Console.WriteLine($"Wrote {manifestPath}");
return 0;

static bool TryParseArgs(
    string[] args,
    out string bundleRoot,
    out string modId,
    out List<string> requiredCompatTargets) {
    bundleRoot = "";
    modId = "";
    requiredCompatTargets = [];

    for (var index = 0; index < args.Length; index++) {
        switch (args[index]) {
        case "--bundle-root":
            if (!TryReadValue(args, ref index, out bundleRoot))
                return false;
            break;
        case "--mod-id":
            if (!TryReadValue(args, ref index, out modId))
                return false;
            break;
        case "--require":
            if (!TryReadValue(args, ref index, out var compatTarget))
                return false;
            requiredCompatTargets.Add(compatTarget);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[index]}");
            return false;
        }
    }

    if (string.IsNullOrWhiteSpace(bundleRoot) || string.IsNullOrWhiteSpace(modId)) {
        Console.Error.WriteLine("Missing required --bundle-root or --mod-id.");
        return false;
    }

    bundleRoot = Path.GetFullPath(bundleRoot.Trim());
    modId = modId.Trim();
    return true;
}

static bool TryReadValue(string[] args, ref int index, out string value) {
    if (index + 1 >= args.Length) {
        Console.Error.WriteLine($"Missing value for {args[index]}.");
        value = "";
        return false;
    }

    value = args[++index];
    return true;
}

static int PrintUsage(int code) {
    Console.Error.WriteLine(
        "Usage: ModVariantBundleCompose --bundle-root <path> --mod-id <id> [--require <compatTarget> ...]");
    return code;
}
