using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;

namespace KitLib;

/// <summary>
/// Lightweight self-contained localization helper for DevMode.
/// Loads flat key-value JSON files from the mod's localization/ folder,
/// falling back to embedded resources, and reacts to game locale changes.
/// </summary>
public static class I18N {
    private static readonly string ModDir =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

    private static readonly string LocDir =
        Path.Combine(ModDir, "localization");

    private static Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private static string? _loadedLang;

    /// <summary>Initialize and subscribe to locale changes.</summary>
    public static void Initialize() {
        Reload();
        TrySubscribe();
    }

    /// <summary>Get a localized string, falling back to <paramref name="fallback"/>.</summary>
    public static string T(string key, string fallback) {
        EnsureLoaded();
        return _translations.GetValueOrDefault(key) ?? fallback;
    }

    /// <summary>Get a localized format string and apply <see cref="string.Format(string,object[])"/>.</summary>
    public static string T(string key, string fallback, params object[] args) {
        var fmt = T(key, fallback);
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }

    /// <summary>Fired after translations reload on game locale change.</summary>
    public static event Action? LanguageChanged;

    /// <summary>Loaded language folder id: <c>eng</c> or <c>zhs</c>.</summary>
    public static string LangCode {
        get {
            EnsureLoaded();
            return string.Equals(_loadedLang, "zhs", StringComparison.OrdinalIgnoreCase) ? "zhs" : "eng";
        }
    }

    // ── Internals ──

    private static void EnsureLoaded() {
        var lang = ResolveLanguage();
        if (string.Equals(_loadedLang, lang, StringComparison.OrdinalIgnoreCase)) return;
        Reload(lang);
    }

    private static void Reload(string? lang = null) {
        lang ??= ResolveLanguage();
        _translations = Load(lang);
        _loadedLang = lang;
    }

    private static Dictionary<string, string> Load(string lang) {
        // 1. Try external file (allows user overrides)
        var fsPath = Path.Combine(LocDir, $"{lang}.json");
        if (File.Exists(fsPath)) {
            try {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(fsPath));
                if (dict != null) return dict;
            }
            catch {
                // ignore corrupt external locale file
            }
        }

        // 2. Fall back to embedded resource
        var resourceName = $"KitLib.Localization.{lang}.json";
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream != null) {
            try {
                using var reader = new StreamReader(stream);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
                if (dict != null) return dict;
            }
            catch {
                // ignore corrupt embedded locale resource
            }
        }

        // 3. If non-English locale not found, fall back to eng
        if (!string.Equals(lang, "eng", StringComparison.OrdinalIgnoreCase))
            return Load("eng");

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveLanguage() {
        string? lang = null;
        try { lang = LocManager.Instance?.Language; } catch { }
        if (!string.IsNullOrWhiteSpace(lang)) return Normalize(lang);
        return "eng";
    }

    private static string Normalize(string? lang) {
        if (string.IsNullOrWhiteSpace(lang)) return "eng";
        var s = lang.Trim().Replace('-', '_').ToLowerInvariant();
        return s switch {
            "zh_cn" or "zh_hans" or "zh_sg" or "zh" => "zhs",
            "en_us" or "en_gb" or "en" or "eng" => "eng",
            _ => s,
        };
    }

    private static void TrySubscribe() {
        try {
            LocManager.Instance?.SubscribeToLocaleChange(OnLocaleChanged);
        }
        catch {
            // locale subscription optional
        }
    }

    private static void OnLocaleChanged() {
        _loadedLang = null;
        Reload();
        LanguageChanged?.Invoke();
    }
}
