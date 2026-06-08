using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace KitLib;

/// <summary>
/// Background asset preloader. Warms up card textures, relic icons,
/// power icons, potion images, and monster scenes to reduce in-game hitches.
/// Runs as a Godot Node with frame-budgeted _Process.
/// </summary>
internal sealed class AssetWarmupService {
    private const int MaxJobsPerFrame = 16;
    private const double FrameBudgetMs = 1.8;
    private const int MaxLoggedErrors = 6;

    private static readonly string[] SceneMemberNames =
        ["Scene", "MonsterScene", "Prefab", "PrefabScene", "NodeScene", "CombatScene", "ScenePath", "MonsterScenePath", "PrefabPath", "NodePath"];

    private readonly Queue<Action> _jobs = new();
    private readonly HashSet<string> _seenCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenRelics = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenPowers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenPotions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenMonsters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedScenePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedTexturePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, MemberInfo[]> _sceneMembersCache = new();

    private int _errors;
    private int _executedJobs;
    private int _loadedScenes;
    private int _loadedTextures;
    private bool _built;
    private bool _completed;
    private bool _buildPending;
    private bool _buildOnNextProcess;
    private double _buildRetrySec;
    private int _buildRetryCount;

    public bool IsCompleted => _completed;

    public void Ready() {
        TryBuildWarmupJobs();
    }

    /// <summary>Queue job list build on the next <see cref="Process"/> tick (avoids stack pressure during embark).</summary>
    public void DeferBuildToProcess() {
        _buildPending = true;
        _buildOnNextProcess = true;
    }

    public void Process(double delta) {
        if (!_built) {
            if (_buildPending) {
                if (_buildOnNextProcess) {
                    _buildOnNextProcess = false;
                    TryBuildWarmupJobs();
                    return;
                }

                _buildRetrySec += delta;
                if (_buildRetrySec >= 1.0) {
                    _buildRetrySec = 0;
                    TryBuildWarmupJobs();
                }
            }
            return;
        }

        if (_completed) return;

        var sw = Stopwatch.StartNew();
        int count = 0;
        while (_jobs.Count > 0 && count < MaxJobsPerFrame && sw.Elapsed.TotalMilliseconds < FrameBudgetMs) {
            var job = _jobs.Dequeue();
            _executedJobs++;
            count++;
            try { job(); }
            catch (Exception ex) {
                _errors++;
                if (_errors <= MaxLoggedErrors)
                    MainFile.Logger.Warn($"Asset warmup job failed: {ex.Message}");
            }
        }

        if (_jobs.Count == 0) {
            _completed = true;
            MainFile.Logger.Info($"Asset warmup finished. jobs={_executedJobs}, scenes={_loadedScenes}, textures={_loadedTextures}, errors={_errors}");
        }
    }

    private void TryBuildWarmupJobs() {
        try {
            _jobs.Clear();
            EnqueueCards();
            EnqueueRelics();
            EnqueuePowers();
            EnqueuePotions();
            EnqueueMonsters();
            _built = true;
            _buildPending = false;
            _completed = _jobs.Count == 0;
            if (_completed)
                MainFile.Logger.Info("Asset warmup skipped (no jobs).");
            else
                MainFile.Logger.Info($"Asset warmup started. jobs={_jobs.Count}");
        }
        catch (Exception ex) {
            _jobs.Clear();
            _built = false;
            _buildPending = true;
            _buildRetryCount++;
            if (_buildRetryCount <= MaxLoggedErrors)
                MainFile.Logger.Warn($"Asset warmup build deferred (ModelDb not ready): {ex.Message}");
        }
    }

    private void EnqueueCards() {
        foreach (var card in ModelDb.AllCards) {
            var entry = ((AbstractModel)card).Id.Entry;
            if (!string.IsNullOrWhiteSpace(entry) && _seenCards.Add(entry))
                _jobs.Enqueue(() => {
                    TryTouchTexture(card.Portrait);
                    TryTouchTexture(card.BannerTexture);
                    TryTouchTexture(card.Frame);
                });
        }
    }

    private void EnqueueRelics() {
        foreach (var relic in ModelDb.AllRelics) {
            var entry = ((AbstractModel)relic).Id.Entry;
            if (!string.IsNullOrWhiteSpace(entry) && _seenRelics.Add(entry))
                _jobs.Enqueue(() => {
                    TryTouchTexture(relic.Icon);
                    TryTouchTexture(relic.BigIcon);
                });
        }
    }

    private void EnqueuePowers() {
        foreach (var power in ModelDb.AllPowers) {
            var entry = ((AbstractModel)power).Id.Entry;
            if (!string.IsNullOrWhiteSpace(entry) && _seenPowers.Add(entry))
                _jobs.Enqueue(() => {
                    TryTouchTexture(power.Icon);
                    TryTouchTexture(power.BigIcon);
                });
        }
    }

    private void EnqueuePotions() {
        foreach (var potion in ModelDb.AllPotions) {
            var entry = ((AbstractModel)potion).Id.Entry;
            if (!string.IsNullOrWhiteSpace(entry) && _seenPotions.Add(entry))
                _jobs.Enqueue(() => {
                    TryTouchTexture(potion.Image);
                    TryTouchTexture(potion.Outline);
                });
        }
    }

    private void EnqueueMonsters() {
        foreach (var monster in ModelDb.Monsters) {
            var entry = ((AbstractModel)monster).Id.Entry;
            if (!string.IsNullOrWhiteSpace(entry) && _seenMonsters.Add(entry))
                _jobs.Enqueue(() => TryTouchMonsterScene(monster));
        }
    }

    private void TryTouchMonsterScene(MonsterModel monster) {
        var members = GetSceneMembers(monster.GetType());
        foreach (var member in members) {
            object? val = member switch {
                PropertyInfo pi => SafeRead(() => pi.GetValue(monster)),
                FieldInfo fi => SafeRead(() => fi.GetValue(monster)),
                _ => null
            };
            TouchSceneValue(val);
        }

        // Fallback: try well-known member names
        foreach (var name in SceneMemberNames) {
            var val = TryGetMemberValue(monster, name);
            TouchSceneValue(val);
        }
    }

    private MemberInfo[] GetSceneMembers(Type type) {
        if (_sceneMembersCache.TryGetValue(type, out var cached)) return cached;

        var list = new List<MemberInfo>();
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
            if (prop.GetIndexParameters().Length != 0 || prop.GetMethod == null) continue;
            var pt = prop.PropertyType;
            if (pt == typeof(PackedScene) || pt == typeof(string) || typeof(Resource).IsAssignableFrom(pt))
                list.Add(prop);
        }
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
            var ft = field.FieldType;
            if (ft == typeof(PackedScene) || ft == typeof(string) || typeof(Resource).IsAssignableFrom(ft))
                list.Add(field);
        }

        var result = list.ToArray();
        _sceneMembersCache[type] = result;
        return result;
    }

    private void TouchSceneValue(object? value) {
        switch (value) {
            case PackedScene ps:
                TryLoadScenePath(ps.ResourcePath);
                break;
            case Resource res:
                TryLoadScenePath(res.ResourcePath);
                break;
            case string path:
                TryLoadScenePath(path);
                break;
            case IEnumerable<string> paths:
                foreach (var p in paths) TryLoadScenePath(p);
                break;
        }
    }

    private void TryLoadScenePath(string? path) {
        if (string.IsNullOrWhiteSpace(path)) return;
        var trimmed = path.Trim();
        if (!(trimmed.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith(".scn", StringComparison.OrdinalIgnoreCase)))
            return;
        if (!_loadedScenePaths.Add(trimmed) || !ResourceLoader.Exists(trimmed)) return;

        try {
            var err = ResourceLoader.LoadThreadedRequest(trimmed, "PackedScene", true);
            if (err == Error.Ok)
                _loadedScenes++;
            else {
                _errors++;
                if (_errors <= MaxLoggedErrors)
                    MainFile.Logger.Warn($"Failed to request scene '{trimmed}': {err}");
            }
        }
        catch (Exception ex) {
            _errors++;
            if (_errors <= MaxLoggedErrors)
                MainFile.Logger.Warn($"Failed to preload scene '{trimmed}': {ex.Message}");
        }
    }

    private void TryTouchTexture(Texture2D? texture) {
        if (texture == null) return;
        var path = texture.ResourcePath;
        if (string.IsNullOrWhiteSpace(path) || !_loadedTexturePaths.Add(path) || !ResourceLoader.Exists(path)) return;

        ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
        _loadedTextures++;
    }

    private static object? TryGetMemberValue(object target, string memberName) {
        var type = target.GetType();
        var prop = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop is { } && prop.GetIndexParameters().Length == 0)
            return SafeRead(() => prop.GetValue(target));
        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? SafeRead(() => field.GetValue(target)) : null;
    }

    private static object? SafeRead(Func<object?> reader) {
        try { return reader(); } catch { return null; }
    }
}
