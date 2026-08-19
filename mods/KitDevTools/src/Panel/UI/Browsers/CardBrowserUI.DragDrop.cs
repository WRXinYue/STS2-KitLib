using System;
using System.Threading.Tasks;
using Godot;
using KitLib;
using KitLib.Actions;
using KitLib.Multiplayer.Cheat;
using KitLib.Presets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace KitLib.UI;

internal static partial class CardBrowserUI {
    private enum CardBrowserDropZone {
        None,
        DrawPile,
        DiscardPile,
        Hand,
    }

    /// <summary>
    /// Overlay lives on Dual.Root (not MainPanel). Ticking uses a native Timer because
    /// this project strips Godot source generators, so custom Control._Process is never called.
    /// </summary>
    private sealed class CardBrowserDragLayer {
        private const float DragStartThresholdPx = 4f;

        private readonly State _state;
        private readonly Control _host;
        private readonly Control _zoneTop;
        private readonly Control _zoneRight;
        private readonly Control _zoneBottom;

        private NGridCardHolder? _dragHolder;
        private Control? _ghost;
        private CardModel? _dragCard;
        private bool _active;
        private NGridCardHolder? _pendingHolder;
        private Vector2? _pendingGlobalPos;
        private bool _wasMousePressed;
        private bool _suppressNextClick;

        internal CardBrowserDragLayer(State state, Control host) {
            _state = state;
            _host = host;

            _zoneTop = CreateZoneStrip(
                I18N.T("topbar.card.drawPile", "Draw Pile"),
                new Color(0.25f, 0.45f, 0.85f, 0.28f));
            _zoneRight = CreateZoneStrip(
                I18N.T("topbar.card.discardPile", "Discard"),
                new Color(0.85f, 0.45f, 0.20f, 0.28f));
            _zoneBottom = CreateZoneStrip(
                I18N.T("cardBrowser.dropHand", "Hand"),
                new Color(0.25f, 0.65f, 0.35f, 0.28f));
        }

        internal bool IsDragging => _active;

        internal bool ShouldSuppressClick() {
            if (!_suppressNextClick)
                return false;
            _suppressNextClick = false;
            return true;
        }

        internal void TryBeginDrag(NGridCardHolder holder, Vector2 globalMouse) {
            if (_pickerCallback != null || _active)
                return;

            var card = holder.CardModel;
            if (card == null)
                return;

            if (MpCheatSession.InMultiplayerRun && !MpCheatSession.CanUseMultiplayerCheats) {
                _state.StatusLabel.Text = I18N.T(
                    "mpcheat.blocked",
                    "Multiplayer cheat inactive: {0}",
                    MpCheatSession.LastBlockReason ?? "unknown");
                return;
            }

            var ghost = CreateCardGhost(holder);
            _pendingHolder = null;
            _pendingGlobalPos = null;
            _dragHolder = holder;
            _dragCard = card;
            _ghost = ghost;
            _active = true;

            holder.Modulate = new Color(1f, 1f, 0.95f, 0.35f);
            LayoutOutsideZones();
            UpdateGhostPosition(globalMouse);
            SetZoneHighlight(HitTestZone(globalMouse));
        }

        internal void NotifyPointerDown(NGridCardHolder holder, Vector2 globalPos) {
            if (_active || _pickerCallback != null)
                return;

            _pendingHolder = holder;
            _pendingGlobalPos = globalPos;
        }

        internal void NotifyPointerMove(NGridCardHolder holder) {
            if (_active || _pickerCallback != null)
                return;

            var mouse = MouseCanvas;
            if (_pendingHolder == null) {
                _pendingHolder = holder;
                _pendingGlobalPos = mouse;
                return;
            }

            if (_pendingGlobalPos != null
                && _pendingGlobalPos.Value.DistanceTo(mouse) >= DragStartThresholdPx)
                TryBeginDrag(_pendingHolder, mouse);
        }

        internal void Poll() {
            bool mousePressed = Input.IsMouseButtonPressed(MouseButton.Left);
            var mouse = MouseCanvas;

            if (_active) {
                LayoutOutsideZones();
                UpdateGhostPosition(mouse);
                SetZoneHighlight(HitTestZone(mouse));
                if (!mousePressed) {
                    var zone = HitTestZone(mouse);
                    if (zone != CardBrowserDropZone.None)
                        _suppressNextClick = true;
                    CompleteDrop(mouse);
                }

                _wasMousePressed = mousePressed;
                return;
            }

            if (mousePressed && !_wasMousePressed)
                NotifyPointerDownAt(mouse);

            if (_pendingHolder != null && _pendingGlobalPos != null && mousePressed) {
                if (_pendingGlobalPos.Value.DistanceTo(mouse) >= DragStartThresholdPx)
                    TryBeginDrag(_pendingHolder, mouse);
            }

            if (!mousePressed && _wasMousePressed) {
                _pendingHolder = null;
                _pendingGlobalPos = null;
            }

            _wasMousePressed = mousePressed;
        }

        private Vector2 MouseCanvas =>
            GodotObject.IsInstanceValid(_state.GridContent)
                ? _state.GridContent.GetGlobalMousePosition()
                : _host.GetGlobalMousePosition();

        internal void NotifyPointerDownAt(Vector2 globalPos) {
            if (_active || _pickerCallback != null)
                return;

            var holder = FindHolderAt(globalPos);
            if (holder != null)
                NotifyPointerDown(holder, globalPos);
        }

        private Control? CreateCardGhost(NGridCardHolder holder) {
            var source = holder.CardNode as Node ?? holder;
            var dup = source.Duplicate(14) as Control;
            if (dup == null)
                return null;

            dup.Name = "CardBrowserDragGhost";
            dup.MouseFilter = Control.MouseFilterEnum.Ignore;
            dup.ZIndex = 90;
            if (dup is NCard nCard)
                nCard.MouseFilter = Control.MouseFilterEnum.Ignore;
            _host.AddChild(dup);
            return dup;
        }

        private NGridCardHolder? FindHolderAt(Vector2 globalMouse) {
            var half = CardSize() * 0.5f;
            NGridCardHolder? fallback = null;
            var fallbackDistSq = float.MaxValue;

            foreach (var row in _state.CardRows) {
                foreach (var holder in row) {
                    if (!holder.Visible || !GodotObject.IsInstanceValid(holder))
                        continue;

                    if (holder.CardNode is Control cardNode) {
                        var cardRect = cardNode.GetGlobalRect();
                        if (cardRect.Size.X > 1f && cardRect.Size.Y > 1f && cardRect.HasPoint(globalMouse))
                            return holder;
                    }

                    var center = holder.GetGlobalTransformWithCanvas().Origin;
                    var local = globalMouse - center;
                    var distSq = local.LengthSquared();
                    if (distSq < fallbackDistSq) {
                        fallbackDistSq = distSq;
                        fallback = holder;
                    }

                    if (Mathf.Abs(local.X) > half.X || Mathf.Abs(local.Y) > half.Y)
                        continue;
                }
            }

            if (fallback != null && fallbackDistSq <= half.LengthSquared())
                return fallback;
            return null;
        }

        private void CompleteDrop(Vector2 globalMouse) {
            if (!_active)
                return;

            var zone = HitTestZone(globalMouse);
            var card = _dragCard;
            FinishDragVisuals();

            if (zone == CardBrowserDropZone.None || card == null)
                return;

            TaskHelper.RunSafely(ExecuteDropAsync(_state, card, zone));
        }

        private void FinishDragVisuals() {
            if (_dragHolder != null && GodotObject.IsInstanceValid(_dragHolder))
                _dragHolder.Modulate = _state.SelectedHolder == _dragHolder
                    ? ColCardPickSelected
                    : ColCardPickNormal;

            if (_ghost != null && GodotObject.IsInstanceValid(_ghost))
                _ghost.QueueFree();

            _zoneTop.Visible = false;
            _zoneRight.Visible = false;
            _zoneBottom.Visible = false;
            _active = false;
            _dragHolder = null;
            _ghost = null;
            _dragCard = null;
        }

        private void UpdateGhostPosition(Vector2 globalMouse) {
            if (_ghost == null || !GodotObject.IsInstanceValid(_ghost))
                return;

            var local = _host.GetGlobalTransformWithCanvas().AffineInverse() * globalMouse;
            _ghost.Position = local;
        }

        private Control CreateZoneStrip(string label, Color bg) {
            var strip = new PanelContainer {
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var style = new StyleBoxFlat {
                BgColor = bg,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 8,
                ContentMarginBottom = 8,
            };
            strip.AddThemeStyleboxOverride("panel", style);

            var lbl = new Label {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.AddThemeColorOverride("font_color", Colors.White);
            strip.AddChild(lbl);
            _host.AddChild(strip);
            return strip;
        }

        private Rect2 ManagerGlobalRect() {
            var rect = _state.Dual.MainPanel.GetGlobalRect();
            if (_state.Dual.ExtSlot.Visible && GodotObject.IsInstanceValid(_state.Dual.ExtPanel))
                rect = rect.Merge(_state.Dual.ExtPanel.GetGlobalRect());
            return rect;
        }

        private void LayoutOutsideZones() {
            var overlay = _host.GetGlobalRect();
            var manager = ManagerGlobalRect();
            var toLocal = _host.GetGlobalTransformWithCanvas().AffineInverse();
            var origin = toLocal * overlay.Position;
            var size = overlay.Size;
            var mgrPos = toLocal * manager.Position - origin;
            var mgrEnd = toLocal * manager.End - origin;

            float topH = Mathf.Max(0f, mgrPos.Y);
            float bottomY = Mathf.Clamp(mgrEnd.Y, 0f, size.Y);
            float bottomH = Mathf.Max(0f, size.Y - bottomY);
            float rightX = Mathf.Clamp(mgrEnd.X, 0f, size.X);
            float rightW = Mathf.Max(0f, size.X - rightX);
            float midY = topH;
            float midH = Mathf.Max(0f, bottomY - topH);

            PlaceZone(_zoneTop, new Rect2(0, 0, size.X, topH));
            PlaceZone(_zoneBottom, new Rect2(0, bottomY, size.X, bottomH));
            PlaceZone(_zoneRight, new Rect2(rightX, midY, rightW, midH));
        }

        private static void PlaceZone(Control strip, Rect2 rect) {
            bool show = rect.Size.X >= 8f && rect.Size.Y >= 8f;
            strip.Visible = show;
            if (!show)
                return;

            strip.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            strip.Position = rect.Position;
            strip.Size = rect.Size;
        }

        private void SetZoneHighlight(CardBrowserDropZone zone) {
            SetZoneEmphasis(_zoneTop, zone == CardBrowserDropZone.DrawPile);
            SetZoneEmphasis(_zoneRight, zone == CardBrowserDropZone.DiscardPile);
            SetZoneEmphasis(_zoneBottom, zone == CardBrowserDropZone.Hand);
        }

        private static void SetZoneEmphasis(Control strip, bool active) {
            strip.Modulate = active ? Colors.White : new Color(1f, 1f, 1f, 0.45f);
        }

        private CardBrowserDropZone HitTestZone(Vector2 globalMouse) {
            var manager = ManagerGlobalRect();
            if (manager.HasPoint(globalMouse))
                return CardBrowserDropZone.None;

            if (globalMouse.Y < manager.Position.Y)
                return CardBrowserDropZone.DrawPile;
            if (globalMouse.Y > manager.End.Y)
                return CardBrowserDropZone.Hand;
            if (globalMouse.X > manager.End.X)
                return CardBrowserDropZone.DiscardPile;
            return CardBrowserDropZone.None;
        }
    }

    private sealed class CardBrowserDragController {
        internal CardBrowserDragLayer Layer { get; }

        private CardBrowserDragController(CardBrowserDragLayer layer) => Layer = layer;

        internal static CardBrowserDragController Attach(Control root, State state) {
            var host = new Control {
                Name = "CardBrowserDragHost",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 80,
            };
            host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(host);
            root.MoveChild(host, root.GetChildCount() - 1);

            var layer = new CardBrowserDragLayer(state, host);

            var timer = new Godot.Timer {
                Name = "CardBrowserDragPoll",
                WaitTime = 0.016,
                Autostart = true,
                ProcessMode = Node.ProcessModeEnum.Always,
            };
            timer.Timeout += layer.Poll;
            root.AddChild(timer);

            return new CardBrowserDragController(layer);
        }

        internal void NotifyPointerDown(NGridCardHolder holder, Vector2 globalPos) =>
            Layer.NotifyPointerDown(holder, globalPos);

        internal void NotifyPointerMove(NGridCardHolder holder) =>
            Layer.NotifyPointerMove(holder);

        internal bool ShouldSuppressClick() => Layer.ShouldSuppressClick();

        internal bool IsDragging => Layer.IsDragging;
    }

    private static CardTarget TargetForDropZone(CardBrowserDropZone zone) => zone switch {
        CardBrowserDropZone.DrawPile => CardTarget.DrawPile,
        CardBrowserDropZone.DiscardPile => CardTarget.DiscardPile,
        CardBrowserDropZone.Hand => CardTarget.Hand,
        _ => CardTarget.Hand,
    };

    private static async Task ExecuteDropAsync(State s, CardModel card, CardBrowserDropZone zone) {
        var target = TargetForDropZone(zone);
        if (IsLibrarySource) {
            await AddCardAsync(s, card, target);
            RebuildGridAndSyncRightPanel(s, GridRebuildOptions.ForCardListChange);
            return;
        }

        var browseTarget = BrowseSourceToTarget(_browseSource);
        if (!browseTarget.HasValue) {
            s.StatusLabel.Text = I18N.T("cardBrowser.dragLibraryOnly", "Drag-add from the All Cards tab.");
            return;
        }

        if (zone == CardBrowserDropZone.Hand)
            await AddCardAsync(s, card.CanonicalInstance, CardTarget.Hand, card);
        else if (browseTarget.Value == target)
            s.StatusLabel.Text = I18N.T("cardBrowser.dragSamePile", "Card is already in that pile.");
        else
            await MoveOwnedCardAsync(s, card, browseTarget.Value, target);

        RebuildGridAndSyncRightPanel(s, GridRebuildOptions.ForCardListChange);
    }

    private static async Task AddCardAsync(State s, CardModel canonical, CardTarget target, CardModel? editSource = null) {
        var player = s.Player;
        var state = s.RunState;
        var upgradeLevels = GetUpgradeLevelsToApply(s, canonical);
        CardEditTemplate? template = null;
        if (editSource != null) {
            var captured = CardEditActions.CaptureTemplate(editSource);
            if (captured.HasAnyPatch())
                template = captured;
        }

        var request = new AddCardRequest {
            Target = target,
            Duration = KitLibState.EffectDuration,
            UpgradeLevelsToApply = upgradeLevels,
            StagedTemplate = template,
        };

        if (!CardActions.TryValidateAdd(state, player, canonical, request, out var error)) {
            s.StatusLabel.Text = error;
            return;
        }

        if (MpCheatSession.InMultiplayerRun) {
            if (request.Duration == EffectDuration.Permanent && CardActions.HasStagedEdits(canonical, request)) {
                s.StatusLabel.Text = I18N.T(
                    "mpcheat.cardAdd.permanentEditedBlocked",
                    "Permanent add is disabled while card stats are edited — use Temporary or reset edits.");
                return;
            }

            s.StatusLabel.Text = MpCheatSession.IsHost
                ? I18N.T("mpcheat.cardAdd.pending", "Syncing add card to all players…")
                : I18N.T("mpcheat.cardAdd.clientPending", "Requesting host to sync add card…");
            var result = MpCheatSession.IsHost
                ? await MpCheatCardAddCoordinator.TryHostAddCardAsync(
                    state, player, canonical, request, null)
                : await MpCheatCardAddCoordinator.TryClientRequestAddCardAsync(
                    state, player, canonical, request, null);
            s.StatusLabel.Text = result;
            return;
        }

        var builder = CardActions.Add(state, player, canonical)
            .Target(target)
            .Duration(request.Duration)
            .UpgradeLevels(upgradeLevels);
        if (template?.HasAnyPatch() == true)
            builder = builder.StagedTemplate(template);
        await builder.RunAsync();
        s.StatusLabel.Text = string.Format(
            I18N.T("cardBrowser.addedCard", "Added: {0}"),
            CardEditActions.GetCardDisplayName(canonical));
    }

    private static async Task MoveOwnedCardAsync(State s, CardModel card, CardTarget from, CardTarget to) {
        var player = s.Player;
        var state = s.RunState;
        var canonical = card.CanonicalInstance;
        var upgradeLevels = 0;
        try {
            upgradeLevels = Math.Max(0, card.CurrentUpgradeLevel - canonical.CurrentUpgradeLevel);
        }
        catch {
            /* keep 0 */
        }

        var template = CardEditActions.CaptureTemplate(card);
        var addRequest = new AddCardRequest {
            Target = to,
            Duration = EffectDuration.Temporary,
            UpgradeLevelsToApply = upgradeLevels,
            StagedTemplate = template.HasAnyPatch() ? template : null,
        };

        if (!CardActions.TryValidateAdd(state, player, canonical, addRequest, out var addError)) {
            s.StatusLabel.Text = addError;
            return;
        }

        if (!CardActions.TryValidateRemove(state, player, card, from,
                removeFromRunState: from == CardTarget.Deck, out var removeError)) {
            s.StatusLabel.Text = removeError;
            return;
        }

        if (MpCheatSession.InMultiplayerRun) {
            s.StatusLabel.Text = MpCheatSession.IsHost
                ? I18N.T("mpcheat.cardRemove.pending", "Syncing remove card to all players…")
                : I18N.T("mpcheat.cardRemove.clientPending", "Requesting host to sync remove card…");
            var removeResult = MpCheatSession.IsHost
                ? await MpCheatCardRemoveCoordinator.TryHostRemoveCardAsync(
                    state, player, card, from, from == CardTarget.Deck)
                : await MpCheatCardRemoveCoordinator.TryClientRequestRemoveCardAsync(
                    state, player, card, from, from == CardTarget.Deck);
            if (!removeResult.Contains("OK", StringComparison.OrdinalIgnoreCase)
                && !removeResult.Contains("成功", StringComparison.Ordinal)) {
                s.StatusLabel.Text = removeResult;
                return;
            }

            s.StatusLabel.Text = MpCheatSession.IsHost
                ? I18N.T("mpcheat.cardAdd.pending", "Syncing add card to all players…")
                : I18N.T("mpcheat.cardAdd.clientPending", "Requesting host to sync add card…");
            var addResult = MpCheatSession.IsHost
                ? await MpCheatCardAddCoordinator.TryHostAddCardAsync(
                    state, player, canonical, addRequest, null)
                : await MpCheatCardAddCoordinator.TryClientRequestAddCardAsync(
                    state, player, canonical, addRequest, null);
            s.StatusLabel.Text = addResult;
            return;
        }

        if (from == CardTarget.Deck)
            await CardPileCmd.RemoveFromDeck(new[] { card }, true);
        else {
            await CardPileCmd.RemoveFromCombat(new[] { card });
            if (state.ContainsCard(card))
                state.RemoveCard(card);
        }

        var addBuilder = CardActions.Add(state, player, canonical)
            .Target(to)
            .Duration(EffectDuration.Temporary)
            .UpgradeLevels(upgradeLevels);
        if (template.HasAnyPatch())
            addBuilder = addBuilder.StagedTemplate(template);
        await addBuilder.RunAsync();

        s.StatusLabel.Text = string.Format(
            I18N.T("cardBrowser.movedCard", "Moved: {0}"),
            CardEditActions.GetCardDisplayName(card));
    }

    private static int GetUpgradeLevelsToApply(State s, CardModel card) {
        if (!IsLibrarySource || !s.LibraryShowUpgradePreview)
            return 0;
        try {
            var display = CardPreviewHelper.GetDisplayModel(card, true);
            if (!ReferenceEquals(display, card))
                return Math.Max(0, display.CurrentUpgradeLevel - card.CurrentUpgradeLevel);
        }
        catch {
            /* keep 0 */
        }

        return 0;
    }
}
