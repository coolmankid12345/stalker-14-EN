using System.Numerics;
using Content.Shared._Stalker_EN.Devices.ArtifactRadar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._Stalker_EN.Devices.ArtifactRadar;

/// <summary>
/// Custom control that renders a 360-degree radar display showing artifact blips
/// with animated sonar sweep and fading blip reveal.
/// </summary>
public sealed class ArtifactRadarControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private float _range = 17f;
    private bool _scannerEnabled;

    // Pre-allocated array for DrawFilledCircle to avoid per-frame allocation
    private readonly Vector2[] _circlePoints = new Vector2[66]; // 64 segments + 2

    // Reusable collections to avoid per-frame allocations
    private readonly HashSet<NetEntity> _currentIds = new();
    private readonly List<NetEntity> _toRemove = new();
    private readonly Vector2[] _triangleVerts = new Vector2[3];

    // Sonar sweep animation
    private const float SweepSpeed = 0.25f; // Rotations per second
    private const float BlipFadeDuration = 2.5f; // Seconds for blip to fully fade
    private const float SweepHitThreshold = 0.15f; // Radians tolerance for sweep detection

    private float _lastSweepAngle;
    private float _sweepStartTime;

    // Struct to hold revealed blip state (position at time of reveal)
    private struct RevealedBlip
    {
        public float Angle;
        public float Distance;
        public int Level;
        public float RevealTime;
    }

    // ID-based tracking: blips "stick" to their revealed position until faded or re-swept
    private readonly Dictionary<NetEntity, RevealedBlip> _revealedBlips = new();

    // Colors for the radar display (green stalker aesthetic)
    private static readonly Color BackgroundColor = new(0.05f, 0.1f, 0.05f, 0.9f);
    private static readonly Color GridColor = new(0.1f, 0.4f, 0.1f, 0.5f);
    private static readonly Color RingColor = new(0.1f, 0.5f, 0.1f, 0.6f);
    private static readonly Color CenterColor = new(0.2f, 0.8f, 0.2f, 0.8f);

    public ArtifactRadarControl()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Updates the radar display with current artifact blip data.
    /// </summary>
    public void UpdateBlips(List<ArtifactRadarBlip> blips, float range, bool scannerEnabled)
    {
        _range = range;

        // Handle scanner disabled
        if (!scannerEnabled)
        {
            _revealedBlips.Clear();
            _scannerEnabled = false;
            return;
        }

        // If scanner just turned on, reset sweep to start from top (0 radians)
        if (!_scannerEnabled && scannerEnabled)
        {
            _sweepStartTime = (float)_timing.CurTime.TotalSeconds;
            _lastSweepAngle = 0f; // Start at top
        }

        _scannerEnabled = scannerEnabled;

        var currentTime = (float)_timing.CurTime.TotalSeconds;
        var elapsedTime = currentTime - _sweepStartTime;

        // Rotating sweep: continuous rotation around full circle
        var sweepAngle = (elapsedTime * SweepSpeed * MathF.PI * 2) % (MathF.PI * 2);
        // Adjust to -PI to PI range for comparison with blip angles
        if (sweepAngle > MathF.PI)
            sweepAngle -= MathF.PI * 2;

        // Build set of current blip IDs for cleanup
        _currentIds.Clear();

        foreach (var blip in blips)
        {
            _currentIds.Add(blip.Id);

            // Check if sweep crossed this blip's CURRENT position
            var crossedBlip = DidSweepCross(_lastSweepAngle, sweepAngle, blip.Angle);

            if (crossedBlip)
            {
                // BUG FIX: Only reveal if not already revealed (prevents fade reset when player moves)
                // This ensures blips stay at their revealed position until fully faded,
                // then can be re-revealed on the next sweep cycle.
                if (!_revealedBlips.ContainsKey(blip.Id))
                {
                    _revealedBlips[blip.Id] = new RevealedBlip
                    {
                        Angle = blip.Angle,
                        Distance = blip.Distance,
                        Level = blip.Level,
                        RevealTime = currentTime
                    };
                }
                // If already revealed, keep existing position and fade progress
            }
            // If not crossed, keep existing revealed position (if any)
        }

        // Remove revealed blips that are no longer in range OR have fully faded
        _toRemove.Clear();
        foreach (var kvp in _revealedBlips)
        {
            var timeSinceReveal = currentTime - kvp.Value.RevealTime;
            if (!_currentIds.Contains(kvp.Key) || timeSinceReveal >= BlipFadeDuration)
            {
                _toRemove.Add(kvp.Key);
            }
        }
        foreach (var id in _toRemove)
        {
            _revealedBlips.Remove(id);
        }

        _lastSweepAngle = sweepAngle;
    }

    private bool DidSweepCross(float lastAngle, float currentAngle, float targetAngle)
    {
        // Normal crossing check
        if ((lastAngle <= targetAngle && currentAngle >= targetAngle) ||
            (lastAngle >= targetAngle && currentAngle <= targetAngle))
        {
            return true;
        }

        // Handle wraparound from PI to -PI
        if (lastAngle > MathF.PI / 2 && currentAngle < -MathF.PI / 2)
        {
            if (targetAngle > lastAngle || targetAngle < currentAngle)
                return true;
        }

        // Threshold check for blips very close to sweep
        if (MathF.Abs(currentAngle - targetAngle) < SweepHitThreshold)
            return true;

        return false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        // Force redraw every frame for smooth animation
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var centerX = size.X / 2f;
        var centerY = size.Y / 2f; // True center for 360° radar
        var radius = Math.Min(size.X, size.Y) / 2f - 10; // Full circle fitting with margin

        // Draw background circle
        DrawFilledCircle(handle, new Vector2(centerX, centerY), radius, BackgroundColor);

        // Draw range rings (at 33%, 66%, 100%)
        DrawCircleRing(handle, new Vector2(centerX, centerY), radius * 0.33f, RingColor);
        DrawCircleRing(handle, new Vector2(centerX, centerY), radius * 0.66f, RingColor);
        DrawCircleRing(handle, new Vector2(centerX, centerY), radius, RingColor);

        // Draw radial grid lines at 45° intervals around full circle
        for (var angle = 0; angle < 360; angle += 45)
        {
            var rad = angle * MathF.PI / 180f;
            var endX = centerX + MathF.Sin(rad) * radius;
            var endY = centerY - MathF.Cos(rad) * radius;
            handle.DrawLine(new Vector2(centerX, centerY), new Vector2(endX, endY), GridColor);
        }

        // Draw sweep line with trail (only if scanner enabled)
        if (_scannerEnabled)
            DrawSweepLine(handle, centerX, centerY, radius);

        // Draw center point
        handle.DrawCircle(new Vector2(centerX, centerY), 3f, CenterColor);

        // Draw blips with fade effect
        DrawBlips(handle, centerX, centerY, radius);
    }

    private void DrawSweepLine(DrawingHandleScreen handle, float centerX, float centerY, float radius)
    {
        var time = (float)_timing.CurTime.TotalSeconds;
        var elapsedTime = time - _sweepStartTime;

        // Rotating sweep: continuous clockwise rotation around full circle
        var sweepAngle = (elapsedTime * SweepSpeed * MathF.PI * 2) % (MathF.PI * 2);

        // Draw fading trail (always trails behind the sweep direction)
        for (int i = 5; i >= 1; i--)
        {
            var trailAngle = sweepAngle - (i * 0.08f); // Trail behind sweep

            var trailEndX = centerX + MathF.Sin(trailAngle) * radius;
            var trailEndY = centerY - MathF.Cos(trailAngle) * radius;
            var alpha = 0.4f - (i * 0.07f);

            handle.DrawLine(
                new Vector2(centerX, centerY),
                new Vector2(trailEndX, trailEndY),
                new Color(0.1f, 0.5f, 0.1f, alpha));
        }

        // Main sweep line
        var endX = centerX + MathF.Sin(sweepAngle) * radius;
        var endY = centerY - MathF.Cos(sweepAngle) * radius;

        handle.DrawLine(
            new Vector2(centerX, centerY),
            new Vector2(endX, endY),
            new Color(0.2f, 0.9f, 0.2f, 0.9f));
    }

    private void DrawBlips(DrawingHandleScreen handle, float centerX, float centerY, float radius)
    {
        var currentTime = (float)_timing.CurTime.TotalSeconds;

        foreach (var kvp in _revealedBlips)
        {
            var revealed = kvp.Value;

            // Calculate alpha based on time since revealed
            var timeSinceReveal = currentTime - revealed.RevealTime;
            if (timeSinceReveal >= BlipFadeDuration)
                continue;

            var alpha = 1f - (timeSinceReveal / BlipFadeDuration);
            if (alpha <= 0.01f)
                continue;

            // Use REVEALED position (not current position)
            var normalizedDistance = revealed.Distance / _range;
            var blipRadius = normalizedDistance * radius;

            var blipX = centerX + MathF.Sin(revealed.Angle) * blipRadius;
            var blipY = centerY - MathF.Cos(revealed.Angle) * blipRadius;

            // Size based on distance (closer = larger)
            var blipSize = 4f + (1f - normalizedDistance) * 4f;

            // Color with fade alpha
            var color = new Color(1f, 1f, 1f, alpha);

            handle.DrawCircle(new Vector2(blipX, blipY), blipSize, color);
        }
    }

    private void DrawFilledCircle(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        // Draw a filled circle (360 degrees)
        const int segments = 64;
        _circlePoints[0] = center;

        for (var i = 0; i <= segments; i++)
        {
            var angle = MathF.PI * 2 * i / segments;
            _circlePoints[i + 1] = new Vector2(
                center.X + MathF.Sin(angle) * radius,
                center.Y - MathF.Cos(angle) * radius
            );
        }

        // Draw as triangles from center
        for (var i = 1; i < segments + 1; i++)
        {
            DrawTriangle(handle, _circlePoints[0], _circlePoints[i], _circlePoints[i + 1], color);
        }
    }

    private void DrawTriangle(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        _triangleVerts[0] = a;
        _triangleVerts[1] = b;
        _triangleVerts[2] = c;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _triangleVerts, color);
    }

    private void DrawCircleRing(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        // Draw a full circle ring (360 degrees)
        const int segments = 64;
        Vector2? prevPoint = null;

        for (var i = 0; i <= segments; i++)
        {
            var angle = MathF.PI * 2 * i / segments;
            var point = new Vector2(
                center.X + MathF.Sin(angle) * radius,
                center.Y - MathF.Cos(angle) * radius
            );

            if (prevPoint != null)
            {
                handle.DrawLine(prevPoint.Value, point, color);
            }

            prevPoint = point;
        }
    }
}
