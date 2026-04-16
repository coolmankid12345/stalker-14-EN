using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared._Stalker.PdaMessenger;
using Content.Shared._Stalker_EN.PdaMessenger;
using Content.Shared._Stalker.CCCCVars;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Stalker.PdaMessenger;

/// <summary>
/// UI controller for displaying PDA General channel notifications in the bottom-left corner.
/// Notifications are visible for 7 seconds, then fade out over 3 seconds.
/// </summary>
public sealed class PdaNotificationUIController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;

    private LayoutContainer? _notificationContainer;
    private readonly List<NotificationEntry> _activeNotifications = new();

    // Timing constants
    private const float NotificationVisibleTime = 7f;
    private const float NotificationFadeTime = 3f;
    private const float NotificationTotalLifetime = NotificationVisibleTime + NotificationFadeTime;

    // Limit constants
    private const int MaxNotifications = 5;
    private const float MaxTotalHeight = 650f;
    private const int MaxContentLength = 450;
    private const float NotificationPanelHeight = 120f;
    private const float NotificationPanelWidth = 650f;
    private const float NotificationMaxHeight = 300f;

    // Update intervals
    private const float HotbarCheckInterval = 0.5f;
    private const float FadeUpdateInterval = 0.1f;

    // Layout constants
    private const float Spacing = 5f;
    private const float LeftMargin = 10f;
    private const float BottomMargin = 10f;

    private float _lastHotbarHeight;
    private float _hotbarCheckAccumulator;
    private float _fadeUpdateAccumulator;
    private bool _notificationsEnabled = true;

    public override void Initialize()
    {
        // Subscribe to CVar changes with immediate invocation to handle reconnects
        _configurationManager.OnValueChanged(CCCCVars.PdaNotificationsEnabled, OnNotificationsEnabledChanged, invokeImmediately: true);

        SubscribeNetworkEvent<PdaGeneralMessageEvent>(OnGeneralMessage);
        SubscribeNetworkEvent<PdaDirectMessageEvent>(OnDirectMessage);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnNotificationsEnabledChanged(bool enabled)
    {
        _notificationsEnabled = enabled;
    }

    /// <summary>
    /// Ensures the notification container is attached to the current active screen.
    /// Recreates it if detached or attached to a stale screen's ViewportContainer.
    /// </summary>
    private void EnsureContainerAttached()
    {
        var activeScreen = UIManager.ActiveScreen;
        var viewportContainer = activeScreen?.FindControl<Control>("ViewportContainer")
            ?? (Control?) activeScreen;

        if (_notificationContainer != null
            && _notificationContainer.IsInsideTree
            && _notificationContainer.Parent == viewportContainer)
            return;

        _notificationContainer?.Orphan();
        _notificationContainer = new LayoutContainer
        {
            Name = "PdaNotificationContainer",
            HorizontalExpand = true,
            VerticalExpand = true
        };

        if (viewportContainer != null)
        {
            viewportContainer.AddChild(_notificationContainer);
            _notificationContainer.SetPositionLast();
        }

        _lastHotbarHeight = GetHotbarHeight();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var entry in _activeNotifications)
            entry.Panel.Dispose();

        _activeNotifications.Clear();
        _notificationContainer?.Orphan();
        _notificationContainer = null;
    }

    private void OnGeneralMessage(PdaGeneralMessageEvent ev, EntitySessionEventArgs args)
    {
        if (!_notificationsEnabled)
            return;

        AddNotification(ev.Title, ev.Content, ev.Sender, ev.BandIcon);
    }

    private void OnDirectMessage(PdaDirectMessageEvent ev, EntitySessionEventArgs args)
    {
        if (!_notificationsEnabled)
            return;

        AddNotification("Direct Message", ev.Content, ev.Sender, ev.BandIcon);
    }

    /// <summary>
    /// Adds a new notification to the display.
    /// </summary>
    public void AddNotification(string title, string content, string sender, string? factionIcon = null)
    {
        EnsureContainerAttached();

        if (_notificationContainer == null)
            return;

        // Truncate very long messages to prevent RichTextLabel issues
        if (content.Length > MaxContentLength)
            content = content[..MaxContentLength] + "...";

        var notification = new PdaNotificationPanel(title, content, sender, factionIcon);
        notification.Visible = true;
        notification.HorizontalExpand = false;

        _notificationContainer.AddChild(notification);

        // Force measure with fixed width and auto height
        notification.Measure(new Vector2(NotificationPanelWidth, NotificationMaxHeight));
        notification.Arrange(new UIBox2(0, 0, NotificationPanelWidth, notification.DesiredSize.Y));

        // Add to list first, then update positions!
        var entry = new NotificationEntry(notification, _timing.CurTime);
        _activeNotifications.Add(entry);

        // Enforce limits
        EnforceLimits();

        UpdateNotificationPositions();
    }

    /// <summary>
    /// Calculates total height of all active notifications including spacing.
    /// </summary>
    private float GetTotalHeight()
    {
        var totalHeight = 0f;

        foreach (var entry in _activeNotifications)
        {
            var panelHeight = entry.Panel.DesiredSize.Y;
            if (panelHeight < 1f)
                panelHeight = NotificationPanelHeight;

            totalHeight += panelHeight + Spacing;
        }

        return totalHeight;
    }

    /// <summary>
    /// Enforces notification limits by removing oldest notifications if needed.
    /// </summary>
    /// <param name="updateCache">If true, updates the cached hotbar height (used when hotbar changes).</param>
    private void EnforceLimits(bool updateCache = false)
    {
        if (_activeNotifications.Count == 0)
            return;

        var hotbarHeight = GetHotbarHeight();

        // Remove oldest notifications until we fit within limits
        while (_activeNotifications.Count > MaxNotifications || GetTotalHeight() + hotbarHeight > MaxTotalHeight)
        {
            var oldest = _activeNotifications.First();
            oldest.Panel.Dispose();
            _activeNotifications.RemoveAt(0);

            if (_activeNotifications.Count == 0)
                break;
        }

        // Update cached hotbar height only when explicitly requested (hotbar changed)
        if (updateCache)
            _lastHotbarHeight = hotbarHeight;
    }

    /// <summary>
    /// Updates fade animation and removes expired notifications.
    /// </summary>
    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var curTime = _timing.CurTime;
        var needsPositionUpdate = false;

        // Update fade animation and check for expired notifications
        // Only update 10 times/sec to reduce CPU usage (visually smooth enough)
        _fadeUpdateAccumulator += args.DeltaSeconds;
        if (_fadeUpdateAccumulator >= FadeUpdateInterval)
        {
            _fadeUpdateAccumulator = 0f;

            var toRemove = new List<NotificationEntry>();

            foreach (var entry in _activeNotifications)
            {
                var elapsed = (curTime - entry.StartTime).TotalSeconds;

                if (elapsed >= NotificationTotalLifetime)
                {
                    // Notification lifetime ended, remove it
                    entry.Panel.Dispose();
                    toRemove.Add(entry);
                    needsPositionUpdate = true;
                }
                else if (elapsed >= NotificationVisibleTime)
                {
                    // Fade out phase: calculate alpha from 1.0 to 0.0
                    var fadeProgress = (elapsed - NotificationVisibleTime) / NotificationFadeTime;
                    var alpha = 1.0f - (float)fadeProgress;
                    entry.Panel.SetAlpha(alpha);
                }
            }

            // Remove expired notifications
            foreach (var entry in toRemove)
            {
                _activeNotifications.Remove(entry);
            }
        }

        // Periodically check for hotbar height changes
        _hotbarCheckAccumulator += args.DeltaSeconds;
        if (_hotbarCheckAccumulator >= HotbarCheckInterval)
        {
            _hotbarCheckAccumulator = 0f;
            if (CheckHotbarHeightChanged())
            {
                needsPositionUpdate = true;
            }
        }

        // Update positions if needed
        if (needsPositionUpdate)
        {
            UpdateNotificationPositions();
        }
    }

    /// <summary>
    /// Checks if hotbar height has changed significantly.
    /// </summary>
    private bool CheckHotbarHeightChanged()
    {
        if (UIManager.ActiveScreen == null || _activeNotifications.Count == 0)
            return false;

        var hotbarHeight = GetHotbarHeight();

        if (Math.Abs(hotbarHeight - _lastHotbarHeight) > 1f)
        {
            // Enforce limits after hotbar height change (also updates _lastHotbarHeight)
            EnforceLimits(updateCache: true);

            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current hotbar height including inventory if visible.
    /// </summary>
    private float GetHotbarHeight()
    {
        var hotbar = UIManager.ActiveScreen?.GetWidget<HotbarGui>();
        var hotbarHeight = hotbar?.Visible == true ? hotbar.Size.Y : 0f;

        // Also check inventory height if visible
        var inventory = UIManager.ActiveScreen?.GetWidget<Content.Client.UserInterface.Systems.Inventory.Widgets.InventoryGui>();
        if (inventory?.InventoryHotbar?.Visible == true)
        {
            var invHeight = inventory.InventoryHotbar.Size.Y;
            if (invHeight > 1f)
            {
                hotbarHeight += invHeight;
            }
        }

        return hotbarHeight;
    }

    /// <summary>
    /// Updates notification positions to stack from bottom of screen.
    /// </summary>
    private void UpdateNotificationPositions()
    {
        if (_notificationContainer == null || UIManager.ActiveScreen == null)
            return;

        var screenHeight = UIManager.ActiveScreen.Size.Y;
        var yOffset = 0f;

        // Position at bottom of screen (above hotbar)
        var baseY = screenHeight - BottomMargin - GetHotbarHeight();

        for (var i = _activeNotifications.Count - 1; i >= 0; i--)
        {
            var panel = _activeNotifications[i].Panel;
            var panelHeight = panel.DesiredSize.Y;

            // If panel not measured yet, use estimated height
            if (panelHeight < 1f)
            {
                panelHeight = NotificationPanelHeight;
            }

            var position = new Vector2(LeftMargin, baseY - yOffset - panelHeight);
            LayoutContainer.SetPosition(panel, position);

            yOffset += panelHeight + Spacing;
        }
    }

    private readonly record struct NotificationEntry(PdaNotificationPanel Panel, TimeSpan StartTime);

    public void OnStateEntered(GameplayState state)
    {
        // Force initialization of this controller when entering the game
        EnsureContainerAttached();
    }
}
