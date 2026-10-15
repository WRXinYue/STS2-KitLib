using System;
using System.Linq;
using System.Text;
using Godot;
using KitLib.CombatStats;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>
/// In-game combat statistics panel. Subscribes to <see cref="CombatStatsTracker.Changed"/>
/// and renders a live per-player breakdown + event timeline + live creature states.
/// Real-time only: no export, no history, no persistence.
/// </summary>
internal static class CombatStatsUI {
    private const string ColHeader = "#C8C8DC";
    private const string ColValue = "#FFFFFF";
    private const string ColMuted = "#8A8AA0";
    private const string ColEnemy = "#E8A0A0";
    private const string ColAccent = "#A0C0F0";
    private const string ColSep = "#55556A";

    private static VBoxContainer? _playersBox;
    private static RichTextLabel? _eventsText;
    private static VBoxContainer? _creaturesBox;
    private static Label? _encounterLabel;
    private static bool _dirty;
    private static bool _subscribed;

    /// <summary>
    /// Build the stats UI into <paramref name="parent"/> and begin live updates.
    /// Pair with <see cref="Detach"/> (the host's TreeExiting also detaches).
    /// </summary>
    public static void Attach(VBoxContainer parent) {
        parent.TreeExiting += Detach;
        Subscribe();

        var scroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            ClipContents = true,
        };
        scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        var col = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        col.AddThemeConstantOverride("separation", 10);

        _encounterLabel = new Label();
        _encounterLabel.AddThemeFontSizeOverride("font_size", 14);
        _encounterLabel.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        col.AddChild(_encounterLabel);

        _playersBox = new VBoxContainer();
        _playersBox.AddThemeConstantOverride("separation", 8);
        col.AddChild(_playersBox);

        col.AddChild(DevPanelUI.CreateOverlaySeparator());

        var eventsHeader = DevPanelUI.CreateSectionHeader(I18N.T("combatStats.section.events", "Events"));
        col.AddChild(eventsHeader);
        _eventsText = new RichTextLabel {
            BbcodeEnabled = true,
            FitContent = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ScrollFollowing = true,
            ScrollActive = true,
        };
        _eventsText.AddThemeFontSizeOverride("normal_font_size", 12);
        col.AddChild(_eventsText);

        col.AddChild(DevPanelUI.CreateOverlaySeparator());

        var creaturesHeader = DevPanelUI.CreateSectionHeader(I18N.T("combatStats.section.creatures", "Creatures"));
        col.AddChild(creaturesHeader);
        _creaturesBox = new VBoxContainer();
        _creaturesBox.AddThemeConstantOverride("separation", 6);
        col.AddChild(_creaturesBox);

        scroll.AddChild(col);
        parent.AddChild(scroll);

        RefreshNow();
    }

    public static void Detach() {
        if (_subscribed) {
            CombatStatsTracker.Changed -= MarkDirty;
            _subscribed = false;
        }
        _playersBox = null;
        _eventsText = null;
        _creaturesBox = null;
        _encounterLabel = null;
        _dirty = false;
    }

    private static void Subscribe() {
        if (_subscribed)
            return;
        CombatStatsTracker.Changed += MarkDirty;
        _subscribed = true;
    }

    private static void MarkDirty() {
        if (_dirty)
            return;
        _dirty = true;
        Callable.From(RefreshNow).CallDeferred();
    }

    private static void RefreshNow() {
        _dirty = false;
        if (_playersBox == null || !GodotObject.IsInstanceValid(_playersBox))
            return;

        var snap = CombatStatsTracker.IsTracking
            ? CombatStatsTracker.Current
            : CombatStatsTracker.Last;
        var dto = snap == null ? null : CombatStatsSnapshotDto.From(snap);

        if (_encounterLabel != null) {
            string title = dto == null
                ? I18N.T("combatStats.empty", "No combat yet")
                : $"{dto.EncounterKey} · T{dto.MaxTurn} · " +
                  (dto.IsActive
                      ? I18N.T("combatStats.live", "In combat")
                      : I18N.T("combatStats.done", "Ended"));
            _encounterLabel.Text = title;
        }

        RebuildPlayers(dto);
        RebuildEvents(dto);
        RebuildCreatures(dto);
    }

    private static void ClearChildren(Node parent) {
        for (int i = parent.GetChildCount() - 1; i >= 0; i--) {
            var c = parent.GetChild(i);
            parent.RemoveChild(c);
            c.QueueFree();
        }
    }

    private static void RebuildPlayers(CombatStatsSnapshotDto? dto) {
        if (_playersBox == null)
            return;
        ClearChildren(_playersBox);

        var players = dto?.Players;
        if (players == null || players.Count == 0)
            return;

        foreach (var p in players) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var name = new Label { Text = p.DisplayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
            name.AddThemeFontSizeOverride("font_size", 12);
            name.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            row.AddChild(name);

            void AddStat(string label, int value) {
                var stat = new Label {
                    Text = $"{label} {value}",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    CustomMinimumSize = new Vector2(70, 0),
                };
                stat.AddThemeFontSizeOverride("font_size", 12);
                stat.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
                row.AddChild(stat);
            }

            AddStat(I18N.T("combatStats.dealt", "Dealt"), p.DamageDealt);
            AddStat(I18N.T("combatStats.taken", "Taken"), p.DamageTaken);
            AddStat(I18N.T("combatStats.block", "Block"), p.BlockGained);
            AddStat(I18N.T("combatStats.cards", "Cards"), p.CardsPlayed);
            _playersBox.AddChild(row);
        }
    }

    private static void RebuildEvents(CombatStatsSnapshotDto? dto) {
        if (_eventsText == null)
            return;
        var events = dto?.CombatEvents;
        if (events == null || events.Count == 0) {
            _eventsText.Text = "";
            return;
        }

        var sb = new StringBuilder();
        for (int i = Math.Max(0, events.Count - 80); i < events.Count; i++) {
            var ev = events[i];
            string line = $"[color={ColMuted}]T{ev.Turn}[/color] · " +
                          $"[color={ColValue}]{EscapeBb(ev.Text)}[/color]";
            if (ev.Amount > 0)
                line += $" [color={ColAccent}]({ev.Amount})[/color]";
            sb.Append(line).Append('\n');
        }
        _eventsText.Text = sb.ToString();
    }

    private static void RebuildCreatures(CombatStatsSnapshotDto? dto) {
        if (_creaturesBox == null)
            return;
        ClearChildren(_creaturesBox);
        var creatures = dto?.LiveCreatures;
        if (creatures == null || creatures.Count == 0)
            return;

        foreach (var c in creatures) {
            string color = c.Side == "Player" ? ColValue : ColEnemy;
            var sb = new StringBuilder();
            sb.Append($"[color={color}]{EscapeBb(c.DisplayName)}[/color] " +
                      $"[color={ColMuted}]{c.CurrentHp}/{c.MaxHp} HP[/color]");
            if (c.Block > 0)
                sb.Append($" · [color={ColAccent}]{c.Block} Block[/color]");
            if (!string.IsNullOrWhiteSpace(c.IntentSummary))
                sb.Append($" · [color={ColMuted}]{EscapeBb(c.IntentSummary)}[/color]");
            if (c.Powers.Count > 0) {
                var powers = string.Join(", ", c.Powers.Take(4)
                    .Select(p => $"{EscapeBb(p.DisplayName)} {p.Amount}"));
                sb.Append($" · [color={ColMuted}]{powers}[/color]");
            }

            var row = new RichTextLabel {
                BbcodeEnabled = true,
                FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            row.AddThemeFontSizeOverride("normal_font_size", 12);
            row.Text = sb.ToString();
            _creaturesBox.AddChild(row);
        }
    }

    private static string EscapeBb(string text) =>
        text.Replace("[", "[lb]").Replace("]", "[rb]");
}
