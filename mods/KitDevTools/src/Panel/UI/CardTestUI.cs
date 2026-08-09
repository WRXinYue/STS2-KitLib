using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using KitLib.Actions;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>
/// Card Test panel — wraps the card browser as the primary UI, embedding queue/action
/// controls in the right panel. Queue persists across panel open/close within a session.
/// Single-click a card to preview; double-click to add to queue instantly.
/// "Test" injects the queue at upgrade=0, plays, then injects again at the configured
/// upgrade level (at least 1) and plays again, letting you compare base vs upgraded.
/// </summary>
internal static class CardTestUI {
    // Queue persists while the game session is alive.
    private static readonly List<CardTestEntry> _queue = new();

    // ──────────────────────────────────── Public API ────────────────────────────────────

    public static void Show(NGlobalUi globalUi) {
        if (!RunContext.TryGetRunAndPlayer(out var state, out var player) || player == null) return;

        var refreshHandle = new RefreshHandle();

        CardBrowserUI.ShowPicker(
            globalUi, state, player,
            onCardPicked: card => {
                _queue.Add(new CardTestEntry(card, 0));
                refreshHandle.Refresh?.Invoke();
            },
            buildPersistentContent: container => BuildPersistentContent(container, refreshHandle));
    }

    // ──────────────────────────────────── Right-panel persistent section ────────────────────────────────────

    private static void BuildPersistentContent(VBoxContainer container, RefreshHandle refreshHandle) {
        // Queue header: label | Add All | Clear All
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);

        var queueLabel = new Label { Text = I18N.T("cardtest.queue", "Queue") };
        queueLabel.AddThemeFontSizeOverride("font_size", 12);
        queueLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        queueLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(queueLabel);

        var addAllBtn = new Button { Text = I18N.T("cardtest.addAll", "Add All") };
        addAllBtn.AddThemeFontSizeOverride("font_size", 11);
        addAllBtn.TooltipText = I18N.T("cardtest.addAllHint", "Add all cards visible in the current filter to the queue");
        addAllBtn.Pressed += () => {
            foreach (var card in CardBrowserUI.GetPickerFilteredCards())
                _queue.Add(new CardTestEntry(card.CanonicalInstance, 0));
            refreshHandle.Refresh?.Invoke();
        };
        header.AddChild(addAllBtn);

        var clearBtn = new Button { Text = I18N.T("cardtest.clearAll", "Clear All") };
        clearBtn.AddThemeFontSizeOverride("font_size", 11);
        clearBtn.Pressed += () => {
            _queue.Clear();
            refreshHandle.Refresh?.Invoke();
        };
        header.AddChild(clearBtn);
        container.AddChild(header);

        // Queue items — rebuilt in-place via RefreshHandle.Refresh
        var queueList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        queueList.AddThemeConstantOverride("separation", 2);
        container.AddChild(queueList);

        // Test button + status
        container.AddChild(new HSeparator());

        var testBtn = new Button {
            Text = I18N.T("cardtest.test", "Test"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 34),
        };
        ApplyAccentBtnStyle(testBtn);
        container.AddChild(testBtn);

        var statusLabel = new Label {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        container.AddChild(statusLabel);

        testBtn.Pressed += () => {
            if (!RunContext.TryGetRunAndPlayer(out var st, out var pl) || pl == null) {
                SetStatus(statusLabel, I18N.T("cardtest.notInCombat", "Enter combat to enable inject / play."));
                return;
            }
            if (!CardTestActions.CanRunCardTest(st, pl)) {
                SetStatus(statusLabel, I18N.T("cardtest.notInCombat", "Enter combat to enable inject / play."));
                return;
            }
            // Snapshot the queue so changes mid-run don't affect either pass.
            var snapshot = _queue.ToList();
            TaskHelper.RunSafely(CardTestActions.TestQueue(snapshot, CardTarget.Hand, st, pl));
            SetStatus(statusLabel, I18N.T("cardtest.testing", "Testing..."));
        };

        // Refresh: clear and rebuild just the queue list items in-place.
        refreshHandle.Refresh = () => {
            if (!GodotObject.IsInstanceValid(queueList)) return;
            foreach (var child in queueList.GetChildren())
                if (child is Node n) n.QueueFree();

            if (_queue.Count == 0) {
                var empty = new Label { Text = I18N.T("cardtest.queueEmpty", "No cards in queue. Double-click a card to add.") };
                empty.AddThemeFontSizeOverride("font_size", 11);
                empty.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
                queueList.AddChild(empty);
            }
            else {
                for (var i = 0; i < _queue.Count; i++) {
                    var entry = _queue[i];
                    var idx = i;

                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 4);

                    var nameLabel = new Label {
                        Text = entry.Card.Title,
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                        ClipText = true,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    nameLabel.AddThemeFontSizeOverride("font_size", 12);
                    row.AddChild(nameLabel);

                    var lvlLabel = new Label {
                        Text = $"+{entry.UpgradeLevels}",
                        CustomMinimumSize = new Vector2(28, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    lvlLabel.AddThemeFontSizeOverride("font_size", 12);
                    lvlLabel.AddThemeColorOverride("font_color", entry.UpgradeLevels > 0 ? KitLibTheme.Accent : KitLibTheme.Subtle);

                    var minusBtn = MakeSmallBtn("−");
                    minusBtn.Pressed += () => {
                        if (entry.UpgradeLevels > 0) {
                            entry.UpgradeLevels--;
                            lvlLabel.Text = $"+{entry.UpgradeLevels}";
                            lvlLabel.AddThemeColorOverride("font_color", entry.UpgradeLevels > 0 ? KitLibTheme.Accent : KitLibTheme.Subtle);
                        }
                    };

                    var plusBtn = MakeSmallBtn("+");
                    plusBtn.Pressed += () => {
                        entry.UpgradeLevels++;
                        lvlLabel.Text = $"+{entry.UpgradeLevels}";
                        lvlLabel.AddThemeColorOverride("font_color", KitLibTheme.Accent);
                    };

                    var removeBtn = MakeSmallBtn("×");
                    removeBtn.Pressed += () => {
                        if (idx < _queue.Count) {
                            _queue.RemoveAt(idx);
                            refreshHandle.Refresh?.Invoke();
                        }
                    };

                    row.AddChild(minusBtn);
                    row.AddChild(lvlLabel);
                    row.AddChild(plusBtn);
                    row.AddChild(removeBtn);
                    queueList.AddChild(row);
                }
            }

            var inMp = MpCheatSession.InMultiplayerRun;
            var inRun = RunContext.TryGetRunAndPlayer(out var runState, out var currentPlayer);
            var canTest = inRun && currentPlayer != null && CardTestActions.CanRunCardTest(runState, currentPlayer);
            testBtn.Disabled = _queue.Count == 0 || !canTest || inMp;
        };

        refreshHandle.Refresh();
    }

    // ──────────────────────────────────── Helpers ────────────────────────────────────

    private static void SetStatus(Label label, string text) {
        if (!GodotObject.IsInstanceValid(label)) return;
        label.Text = text;
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
    }

    private static Button MakeSmallBtn(string text) {
        var btn = new Button {
            Text = text,
            CustomMinimumSize = new Vector2(26, 26),
            FocusMode = Control.FocusModeEnum.None,
        };
        btn.AddThemeFontSizeOverride("font_size", 14);
        return btn;
    }

    private static void ApplyAccentBtnStyle(Button btn) {
        var accent = KitLibTheme.Accent;
        StyleBoxFlat MakeStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        btn.AddThemeStyleboxOverride("normal", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.20f)));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.35f)));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.50f)));
        btn.AddThemeColorOverride("font_color", accent);
        btn.AddThemeColorOverride("font_hover_color", accent);
        btn.AddThemeColorOverride("font_pressed_color", accent);
    }

    private sealed class RefreshHandle {
        public Action? Refresh;
    }
}
