using KitLib.Abstractions.Modding;

if (!TryParseArgs(args, out var bundleRoot, out var modId, out var requiredCompatTargets))
    return PrintUsage(1);

var libRoot = Path.Combine(bundleRoot, ModVariantLayout.LibDirectoryName);
if (!Directory.Exists(libRoot)) {
    Console.Error.WriteLine($"Missing lib directory: {libRoot}");
    return 1;
}

var implFile = ModVariantLayout.ImplementationFileName(modId);
var present = new List<string>();
foreach (var dir in Directory.EnumerateDirectories(libRoot).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)) {
    var marker = Path.Combine(dir, ModVariantLayout.CompatTargetMarkerName);
    if (!File.Exists(marker))
        continue;

    var label = File.ReadAllText(marker).Trim();
    if (string.IsNullOrWhiteSpace(label))
        continue;

    var folderName = Path.GetFileName(dir);
    if (!string.Equals(folderName, label, StringComparison.OrdinalIgnoreCase)) {
        Console.Error.WriteLine($"Variant folder '{folderName}' does not match {ModVariantLayout.CompatTargetMarkerName} ({label}).");
        return 1;
    }

    var dll = Path.Combine(dir, implFile);
    if (!File.Exists(dll)) {
        Console.Error.WriteLine($"Missing {implFile} under {dir}.");
        return 1;
    }

    present.Add(label);
}

if (present.Count == 0) {
    Console.Error.WriteLine($"No lib/<api>/{implFile} variants found under {libRoot}.");
    return 1;
}

if (requiredCompatTargets.Count > 0) {
    var presentSet = present.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var missing = requiredCompatTargets.Where(target => !presentSet.Contains(target)).OrderBy(static x => x).ToList();
    if (missing.Count > 0) {
        Console.Error.WriteLine(
            $"Release bundle missing variant(s): {string.Join(", ", missing)}. Build all API profiles before compose.");
        return 1;
    }
}

Console.WriteLine($"OK: {present.Count} variant(s) for {modId} ({string.Join(", ", present)}).");
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
