using System;
using System.Collections.Generic;
using Godot;
using KitLib;
using KitLib.CombatStats;
using KitLib.Icons;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

/// <summary>Right-edge combat HUD: team mode draws one share ring per player, solo lists every
/// enabled metric against its run average, and a gear opens the matching settings.</summary>
internal static partial class CombatRingsHud {
    private const int HudZIndex = 1100;

    // Wide enough for the band's body (CombatRingBand.BodyWidth) plus room for the row labels.
    private const float BandWidth = 60f;
    // Keeps the band clear of the game's right edge frame.
    private const float BandInsetRight = 16f;
    // Clears the band's flare plus a margin, so rows never crowd the top and bottom tips.
    private const float BandPadV = CombatRingBand.FlareHeight + 14f;
    private const float LabelWidth = 28f;
    private const int RingGlyphSize = 12;
    private const int RowSeparation = 10;
    private const float GearSize = 26f;
    private const int GearGlyphSize = 12;
    private const int GearGap = 8;
    // Extra offset on top of the arc fit, to slide the disc right and down into the notch.
    private const float GearNudgeX = 5f;
    private const float GearNudgeY = 9f;
    // Hover wave: the front travels from just off the disc's left edge to just past its right one.
    private const float GearSweepIn = 1.2f;
    private const float GearSweepOut = -0.2f;
    private const float GearWaveSeconds = 0.15f;

    private static readonly Color BandBg = KitLibScreenFrame.FrameColor;
    private static readonly Color GearWaveColor = new(0.16f, 0.16f, 0.20f, 1f);

    // The front bows across the disc's height so the sweep reads as a wave rather than a straight
    // wipe. UV is the disc mask's own coordinate, so sweep and the band are fractions of its width.
    private const string GearWaveShaderCode = """
        shader_type canvas_item;

        uniform float sweep = -0.2;
        uniform vec4 fill : source_color = vec4(0.16, 0.16, 0.20, 1.0);

        void fragment() {
            float front = sweep - 0.16 * sin(UV.y * PI);
            // Fill trails behind the front, so raising sweep sweeps the colour in from the left.
            float mask = 1.0 - smoothstep(front - 0.18, front + 0.18, UV.x);
            COLOR.rgb = mix(COLOR.rgb, fill.rgb, mask);
        }
        """;

    private static readonly Color LabelColor = new(0.86f, 0.87f, 0.92f);
    private static readonly Color GearColor = new(0.86f, 0.87f, 0.92f);

    private static Control? _root;
    private static CombatRingBand? _band;
    private static VBoxContainer? _strip;
    private static Control? _ringsBox;
    private static Button? _gear;
    private static TextureRect? _gearDisc;
    private static ShaderMaterial? _gearWave;
    private static Tween? _gearSweep;
    private static readonly List<(VBoxContainer Root, CombatStatRing Ring, Label Label)> _rows = new();
    private static bool _refreshQueued;
    private static string? _lastDiag;

    // The band enters the same way the left rail does: off screen, then sliding in once the run scene
    // has finished its transition fade, so it does not pop in with the scene.
    private static KitLibSlide? _bandSlide;
    private static float _bandSlideT = 1f;
    private static readonly KitLibRevealWait _revealWait = new();

    #region debug-point mp-stats-values-missing
    // Instrumentation only: proves whether the right-edge band stopped repainting and why.
    private static string? _dbgSig;
    private static long _dbgSeq;
    private static long _dbgTriggers;

    private static void Dbg(string line) {
        _dbgSeq++;
        GD.Print($"[CombatRings][TRACE] seq={_dbgSeq} {line}");
    }

    // Same-state repaints are dropped, so one line marks one distinct roster / metric / value state.
    private static void DbgTrace(string signature) {
        if (signature == _dbgSig)
            return;

        _dbgSig = signature;
        Dbg(signature);
    }

    private static string Describe(Control? node) =>
        node == null ? "null" : GodotObject.IsInstanceValid(node) ? "valid" : "freed";
    #endregion

    // ──────── Lifecycle ────────

    /// <summary>Builds the right-edge band as a child of the global UI (never touches the left rail).</summary>
    internal static void Attach(NGlobalUi globalUi) {
        if (_root != null && GodotObject.IsInstanceValid(_root))
            return;

        var root = new Control {
            Name = "KitLibCombatRingsHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = HudZIndex,
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // Fixed-width strip flush to the right edge; the pill hugs the right side and centres vertically.
        var strip = new VBoxContainer {
            Name = "CombatRingsStrip",
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 1f,
            OffsetLeft = -(BandWidth + BandInsetRight),
            OffsetRight = -BandInsetRight,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var band = new CombatRingBand {
            Name = "CombatRingsBand",
            CustomMinimumSize = new Vector2(BandWidth, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            MouseFilter = Control.MouseFilterEnum.Stop,
            BgColor = BandBg,
        };
        strip.AddChild(band);

        // Plain control: rows are placed by hand so each one rides the band's bow.
        _ringsBox = new Control {
            Name = "CombatRings",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _ringsBox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        band.AddChild(_ringsBox);

        // Metric picker gear: a dark disc tucked into the notch beside the band's bottom flare.
        // The disc is its own layer under the button, so the hover fill can sweep in as a wave
        // without tinting the icon along with it.
        _gearWave = new ShaderMaterial { Shader = GearWaveShader() };
        _gearWave.SetShaderParameter("fill", GearWaveColor);
        _gearWave.SetShaderParameter("sweep", GearSweepOut);
        _gearDisc = new TextureRect {
            Name = "CombatRingsGearDisc",
            Size = new Vector2(GearSize, GearSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = BuildDiscTexture((int)GearSize, BandBg),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = _gearWave,
        };
        _ringsBox.AddChild(_gearDisc);

        _gear = new Button {
            Name = "CombatRingsGear",
            CustomMinimumSize = new Vector2(GearSize, GearSize),
            FocusMode = Control.FocusModeEnum.None,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Center,
            TooltipText = I18N.T("combatStats.ring.metric", "Ring metric"),
        };
        // Empty styleboxes: the disc owns the look, so hover never flashes a flat colour swap.
        _gear.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _gear.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        _gear.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        _gear.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _gear.Pressed += () => DevPanelUI.ShowCombatRingMetricPicker(globalUi);
        _gear.MouseEntered += () => SweepGearWave(GearSweepIn);
        _gear.MouseExited += () => SweepGearWave(GearSweepOut);
        _ringsBox.AddChild(_gear);

        root.AddChild(strip);
        ((Node)globalUi).AddChild(root);

        _root = root;
        _band = band;
        _strip = strip;
        _rows.Clear();

        CombatStatsTracker.Changed -= OnChanged;
        CombatStatsTracker.Changed += OnChanged;
        KitLibScreenFrame.RemoveWrapListener(ApplyWrap);
        KitLibScreenFrame.AddWrapListener(ApplyWrap);
        GD.Print("[CombatRings] hud attached");
        _bandSlide = new KitLibSlide(0f, SetBandSlide, KitLibScreenFrame.AnimationSeconds);
        _bandSlideT = 0f;
        ApplyWrap();
        Refresh();
        // Built before the node enters the tree; redraw once it is in place.
        Callable.From(Refresh).CallDeferred();
        _revealWait.Begin(root, () => _bandSlide?.To(_root, 1f, animated: true));
    }

    private static void SetBandSlide(float t) {
        _bandSlideT = t;
        ApplyWrap();
    }

    /// <summary>Whether the band is on screen, so an exit slide has something to move.</summary>
    internal static bool IsBandOnScreen => _bandSlide != null && _bandSlideT > 0.01f;

    /// <summary>
    /// <para xml:lang="en">Slide the band out past the right edge for a return to the main menu; the window edge itself stays put.</para>
    /// <para xml:lang="zh-CN">回主菜单时把右侧竖带划出屏幕右缘，外层边框保持不动。</para>
    /// </summary>
    internal static void SlideOutForExit() {
        _revealWait.Cancel();
        _bandSlide?.To(_root, 0f, animated: true);
    }

    /// <summary>Round, anti-aliased disc mask holding the colour the hover wave sweeps across.</summary>
    private static ImageTexture BuildDiscTexture(int size, Color fill) {
        float radius = size * 0.5f;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float d = new Vector2(x + 0.5f - radius, y + 0.5f - radius).Length();
                float coverage = Mathf.Clamp(radius - d + 0.5f, 0f, 1f);
                image.SetPixel(x, y, new Color(fill.R, fill.G, fill.B, fill.A * coverage));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Shader that sweeps the disc's hover fill in from the left as a bowed wave.</summary>
    private static Shader GearWaveShader() => new() { Code = GearWaveShaderCode };

    /// <summary>Drives the wave front to <paramref name="to"/>; hovering again mid-wave resumes in place.</summary>
    private static void SweepGearWave(float to) {
        if (_gearWave == null || _gear == null || !GodotObject.IsInstanceValid(_gear))
            return;

        if (_gearSweep != null && _gearSweep.IsValid())
            _gearSweep.Kill();

        float from = (float)_gearWave.GetShaderParameter("sweep");
        _gearSweep = _gear.CreateTween();
        _gearSweep.TweenMethod(
            Callable.From<float>(v => _gearWave.SetShaderParameter("sweep", v)),
            from, to, GearWaveSeconds).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// <para xml:lang="en">Rides the band out with the frame's right border: the clearance collapses to zero as the wrap retracts.</para>
    /// <para xml:lang="zh-CN">竖带随右边框一起外移：wrap 收起时右侧留白归零，贴住屏幕右缘。</para>
    /// </summary>
    private static void ApplyWrap() {
        if (_strip == null || !GodotObject.IsInstanceValid(_strip))
            return;

        float inset = BandInsetRight * KitLibScreenFrame.WrapT;
        // Slide the whole strip in from beyond the right edge; at 0 the band sits fully off screen.
        float slide = (BandWidth + inset) * (1f - _bandSlideT);
        // Ride both edges so the strip keeps the band's exact width: the VBoxContainer truncates the
        // fractional cross-axis slack, which left the band's right edge a pixel short of the screen edge.
        _strip.OffsetLeft = -(BandWidth + inset) + slide;
        _strip.OffsetRight = -inset + slide;
    }

    internal static void Detach() {
        CombatStatsTracker.Changed -= OnChanged;
        KitLibScreenFrame.RemoveWrapListener(ApplyWrap);
        KitLibScreenFrame.SetRightStrokeCut(-1f, -1f);
        _bandSlide?.Stop();
        _bandSlide = null;
        _bandSlideT = 1f;
        _revealWait.Cancel();
        if (_root != null && GodotObject.IsInstanceValid(_root))
            _root.QueueFree();

        _root = null;
        _band = null;
        _strip = null;
        _ringsBox = null;
        _gear = null;
        _gearDisc = null;
        _gearWave = null;
        _gearSweep = null;
        _rows.Clear();
        _refreshQueued = false;
        #region debug-point mp-stats-values-missing
        Dbg("hud detached: refresh chain is dead until a fresh Attach");
        #endregion
    }

    private static void OnChanged() {
        #region debug-point mp-stats-values-missing
        _dbgTriggers++;
        #endregion
        if (_refreshQueued || _root == null) {
            #region debug-point mp-stats-values-missing
            // The only silent path that can freeze the band for good.
            if (_root == null)
                Dbg("tracker changed with no root: refresh chain dead, band can no longer repaint");
            #endregion
            return;
        }

        _refreshQueued = true;
        Callable.From(Refresh).CallDeferred();
    }

    // ──────── Refresh ────────

    internal static void Refresh() {
        _refreshQueued = false;
        #region debug-point mp-stats-values-missing
        // Reports a repaint that never reached the band; the real guard below stays untouched.
        if (_root == null || _ringsBox == null || _gear == null || !GodotObject.IsInstanceValid(_root))
            Dbg($"refresh dropped: root={Describe(_root)} ringsBox={(_ringsBox == null ? "null" : "set")} "
                + $"gear={(_gear == null ? "null" : "set")}");
        #endregion
        if (_root == null || _ringsBox == null || _gear == null || !GodotObject.IsInstanceValid(_root))
            return;

        _gear.Icon = MdiIcon.Cog.Texture(GearGlyphSize, GearColor);

        var snapshot = CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : CombatStatsTracker.Last;
        var diag = $"players={snapshot?.Players.Count ?? 0} rows={_rows.Count} "
            + $"ring={(_rows.Count > 0 ? _rows[0].Ring.Size.ToString() : "none")} "
            + $"band={_band?.Size.ToString() ?? "none"}@{_band?.GlobalPosition.ToString() ?? "none"} "
            + $"view={_root.GetViewportRect().Size}";
        if (diag != _lastDiag) {
            _lastDiag = diag;
            GD.Print($"[CombatRings] {diag}");
        }

        if (snapshot == null || snapshot.Players.Count == 0) {
            #region debug-point mp-stats-values-missing
            Dbg($"band hidden: snapshot={(snapshot == null ? "null" : $"{snapshot.Players.Count} players")}");
            #endregion
            SetHudVisible(false);
            return;
        }

        #region debug-point mp-stats-values-missing
        // One line per distinct roster / metric / value state, so the last state before a vanish is visible.
        {
            var dbgMetric = SettingsStore.GetCombatRingMetric();
            var dbgVals = new List<string>();
            long dbgTotal = 0;
            foreach (var p in snapshot.Players.Values) {
                int v = MetricValue(p, dbgMetric);
                dbgTotal += v;
                dbgVals.Add($"{p.Key}={v}");
            }

            DbgTrace($"tracking={CombatStatsTracker.IsTracking} snap={snapshot.Players.Count}p "
                + $"vals=[{string.Join(",", dbgVals)}] total={dbgTotal} metric={dbgMetric} "
                + $"soloMetrics={SettingsStore.GetCombatRingMetrics().Count} triggers={_dbgTriggers} "
                + $"band={(_band == null ? "null" : $"{_band.Visible}@{_band.GlobalPosition.ToString()} {_band.Size.ToString()}")} "
                + $"view={_root.GetViewportRect().Size.ToString()} localNetId={(RunManager.Instance?.NetService == null ? "null" : RunManager.Instance.NetService.NetId.ToString())}");
        }
        #endregion

        var players = OrderPlayers(snapshot);
        if (players.Count > 1) {
            // Team mode: one ring per player, each on its share of the picked metric.
            var metric = SettingsStore.GetCombatRingMetric();
            EnsureRingRows(players.Count);
            SetHudVisible(true);
            LayoutRows();
            for (int i = 0; i < players.Count; i++)
                ApplyRingRow(i, BuildShareView(players[i], players, metric));
            return;
        }

        // Solo: one row per enabled metric, the ring comparing this combat with the run average.
        var metrics = SettingsStore.GetCombatRingMetrics();
        EnsureRingRows(metrics.Count);
        SetHudVisible(metrics.Count > 0);
        if (metrics.Count == 0)
            return;

        LayoutRows();
        for (int i = 0; i < metrics.Count; i++)
            ApplyRingRow(i, BuildMetricView(players[0], metrics[i]));
    }

    /// <summary>Shows or hides the band and every row on it — the gear rides along.</summary>
    private static void SetHudVisible(bool visible) {
        if (_band != null)
            _band.Visible = visible;
        if (_ringsBox != null)
            _ringsBox.Visible = visible;
        // No band on screen means nothing for the border's stroke to merge into; restore it whole.
        if (!visible)
            KitLibScreenFrame.SetRightStrokeCut(-1f, -1f);
    }

    /// <summary>True while the tracked session has teammates, so the rings read as team shares.</summary>
    internal static bool IsSharedMode() {
        var snapshot = CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : CombatStatsTracker.Last;
        return snapshot != null && snapshot.Players.Count > 1;
    }

    /// <summary>Sizes the band to wrap its rows and rides each row along the band's bow.</summary>
    private static void LayoutRows() {
        if (_band == null || _ringsBox == null)
            return;

        float contentHeight = 0f;
        for (int i = 0; i < _rows.Count; i++) {
            contentHeight += _rows[i].Root.GetCombinedMinimumSize().Y;
            if (i > 0)
                contentHeight += RowSeparation;
        }

        float bandHeight = BandPadV * 2f + contentHeight;
        _band.CustomMinimumSize = new Vector2(BandWidth, bandHeight);
        SyncBandStrokeCut(bandHeight);

        float y = BandPadV;
        for (int i = 0; i < _rows.Count; i++) {
            var row = _rows[i].Root;
            var min = row.GetCombinedMinimumSize();
            float centerX = CombatRingBand.CenterlineAt(y + min.Y * 0.5f, bandHeight, BandWidth);
            row.Position = new Vector2(centerX - min.X * 0.5f, y);
            y += min.Y + RowSeparation;
        }

        // The gear rides the notch left of the bottom flare, spaced off the arc's own curve.
        if (_gear != null) {
            float centerY = bandHeight - CombatRingBand.FlareHeight * 0.6f;
            float edge = CombatRingBand.LeftEdgeAt(centerY, bandHeight, BandWidth);
            var spot = new Vector2(
                edge - GearGap - GearSize + GearNudgeX,
                centerY - GearSize * 0.5f + GearNudgeY);
            _gear.Position = spot;
            if (_gearDisc != null) {
                _gearDisc.Position = spot;
                _gearDisc.Size = new Vector2(GearSize, GearSize);
            }
        }
    }

    /// <summary>
    /// <para xml:lang="en">Mutes the frame's inner stroke behind the band's own span, so the band's right edge and the window border read as one block instead of a hairline seam.</para>
    /// <para xml:lang="zh-CN">在竖带所占高度内抑制边框内描边，使竖带右缘与窗口边框连成一整块，而不是留一条细缝。</para>
    /// </summary>
    private static void SyncBandStrokeCut(float bandHeight) {
        if (_root == null || !GodotObject.IsInstanceValid(_root)) {
            KitLibScreenFrame.SetRightStrokeCut(-1f, -1f);
            return;
        }

        // The strip spans the viewport and centres the band, so the span follows without waiting a frame
        // for the container to sort.
        float top = (_root.GetViewportRect().Size.Y - bandHeight) * 0.5f;
        KitLibScreenFrame.SetRightStrokeCut(top, top + bandHeight);
    }

    private static void EnsureRingRows(int count) {
        if (_ringsBox == null)
            return;

        while (_rows.Count > count) {
            var last = _rows[^1];
            _ringsBox.RemoveChild(last.Root);
            last.Root.QueueFree();
            _rows.RemoveAt(_rows.Count - 1);
        }

        while (_rows.Count < count) {
            var ring = new CombatStatRing();
            var label = new Label {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(LabelWidth, 14f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", LabelColor);

            var row = new VBoxContainer {
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            row.AddThemeConstantOverride("separation", 0);
            row.AddChild(ring);
            row.AddChild(label);

            _ringsBox.AddChild(row);
            _rows.Add((row, ring, label));
        }
    }

    private static void ApplyRingRow(int index, RingView view) {
        var (_, ring, label) = _rows[index];
        ring.Fraction = view.Fraction;
        ring.ValueColor = view.Color;
        ring.Glyph = view.Glyph;
        ring.TooltipText = view.Tooltip;
        ring.Apply();
        label.Text = view.Text;
        label.AddThemeColorOverride("font_color", LabelColor);
    }

    // ──────── Ring values ────────

    private readonly record struct RingView(float Fraction, Color Color, ImageTexture? Glyph, string Text, string Tooltip);

    /// <summary>Multiplayer: each ring is one player's share of the team total.</summary>
    private static RingView BuildShareView(PlayerCombatStats player, List<PlayerCombatStats> players, CombatRingMetric metric) {
        long total = 0;
        foreach (var p in players)
            total += MetricValue(p, metric);

        int value = MetricValue(player, metric);
        float share = total > 0 ? value / (float)total : 0f;
        string percent = $"{Mathf.RoundToInt(share * 100f)}%";
        var color = MetricColor(metric);

        return new RingView(
            Mathf.Clamp(share, 0f, 1f),
            color,
            MdiIcon.Account.Texture(RingGlyphSize, color.Lightened(0.15f)),
            percent,
            I18N.T("combatStats.ring.tooltipMp", "{0}\n{1} {2} ({3})",
                PlayerName(player), MetricLabel(metric), value, percent));
    }

    /// <summary>Solo: the ring compares this combat against the run's average, the row shows the count.</summary>
    private static RingView BuildMetricView(PlayerCombatStats player, CombatRingMetric metric) {
        int value = MetricValue(player, metric);
        var color = MetricColor(metric);
        var glyph = MetricIcon(metric).Texture(RingGlyphSize, color.Lightened(0.15f));

        float baseline = 0f;
        if (CombatStatsTracker.RunCombatCount > 0
            && CombatStatsTracker.RunTotal.Players.TryGetValue(player.Key, out var runPlayer))
            baseline = MetricValue(runPlayer!, metric) / (float)CombatStatsTracker.RunCombatCount;

        // No history yet (first combat of the run): this combat becomes its own baseline.
        if (baseline <= 0f)
            baseline = Mathf.Max(value, 1f);

        float ratio = value / baseline;

        return new RingView(
            Mathf.Clamp(ratio, 0f, 1f),
            ratio > 1f ? KitLibTheme.Accent : color,
            glyph,
            value.ToString(),
            I18N.T("combatStats.ring.tooltipSp", "{0}\n{1} {2} (run avg {3})",
                PlayerName(player), MetricLabel(metric), value,
                Mathf.RoundToInt(baseline).ToString()));
    }

    private static List<PlayerCombatStats> OrderPlayers(CombatStatsSnapshot snapshot) {
        ulong localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        var list = new List<PlayerCombatStats>(snapshot.Players.Values);
        list.Sort((a, b) => {
            ulong ka = ParseNetId(a.Key), kb = ParseNetId(b.Key);
            bool la = ka == localNetId, lb = kb == localNetId;
            if (la != lb)
                return la ? -1 : 1;
            return ka.CompareTo(kb);
        });
        return list;
    }

    private static ulong ParseNetId(string key) => ulong.TryParse(key, out ulong id) ? id : ulong.MaxValue;

    private static string PlayerName(PlayerCombatStats player) =>
        string.IsNullOrEmpty(player.DisplayName) ? player.Key : player.DisplayName;

    // ──────── Metric definitions ────────

    private static int MetricValue(PlayerCombatStats player, CombatRingMetric metric) => metric switch {
        CombatRingMetric.Damage => player.DamageDealt,
        CombatRingMetric.Block => player.BlockGained,
        CombatRingMetric.Cards => player.CardsPlayed,
        CombatRingMetric.Energy => player.EnergySpent,
        CombatRingMetric.Potion => player.PotionsUsed,
        _ => ContributionScore(player),
    };

    /// <summary>Composite score, reviving the retired CombatScoreCalculator weights over surviving aggregates.</summary>
    private static int ContributionScore(PlayerCombatStats player) {
        double score = SumValues(player.DamageByCard) + SumValues(player.PowerDamageBySource);
        score += player.BlockGained * 0.65;
        score += player.DebuffsApplied * 12;
        score += player.BuffsApplied * 8;
        score += player.PotionsUsed * 25;
        foreach (int energy in player.EnergySpentByCard.Values)
            score += 3 + 5 * energy;
        return (int)Math.Round(score);
    }

    private static int SumValues(Dictionary<string, int> values) {
        int total = 0;
        foreach (int v in values.Values)
            total += v;
        return total;
    }

    private static MdiIcon MetricIcon(CombatRingMetric metric) => metric switch {
        CombatRingMetric.Damage => MdiIcon.Sword,
        CombatRingMetric.Block => MdiIcon.Shield,
        CombatRingMetric.Cards => MdiIcon.Cards,
        CombatRingMetric.Energy => MdiIcon.LightningBolt,
        CombatRingMetric.Potion => MdiIcon.FlaskOutline,
        _ => MdiIcon.ChartDonut,
    };

    internal static string MetricLabel(CombatRingMetric metric) => metric switch {
        CombatRingMetric.Damage => I18N.T("combatStats.ring.damage", "Damage"),
        CombatRingMetric.Block => I18N.T("combatStats.ring.block", "Block"),
        CombatRingMetric.Cards => I18N.T("combatStats.ring.cards", "Cards"),
        CombatRingMetric.Energy => I18N.T("combatStats.ring.energy", "Energy"),
        CombatRingMetric.Potion => I18N.T("combatStats.ring.potion", "Potions"),
        _ => I18N.T("combatStats.ring.score", "Score"),
    };

    private static Color MetricColor(CombatRingMetric metric) => metric switch {
        CombatRingMetric.Damage => new Color("#E87A64"),
        CombatRingMetric.Block => new Color("#7AA2E8"),
        CombatRingMetric.Cards => new Color("#B7B7D8"),
        CombatRingMetric.Energy => new Color("#E8C864"),
        CombatRingMetric.Potion => new Color("#6FCF8A"),
        _ => KitLibTheme.Accent,
    };

    // ──────── Ring widget ────────
}
