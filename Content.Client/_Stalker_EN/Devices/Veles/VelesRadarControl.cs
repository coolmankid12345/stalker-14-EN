using System.Numerics;
using Content.Shared._Stalker_EN.Devices.Veles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._Stalker_EN.Devices.Veles;

/// <summary>
/// Custom control that renders a 180-degree arc radar display showing artifact blips
/// with animated sonar sweep and fading blip reveal.
/// </summary>
public sealed class VelesRadarControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private List<VelesBlip> _blips = new();
    private float _range = 17f;

    // Pre-allocated array for DrawFilledArc to avoid per-frame allocation
    private readonly Vector2[] _arcPoints = new Vector2[34]; // segments (32) + 2

    // Sonar sweep animation
    private const float SweepSpeed = 0.4f; // Oscillations per second
    private const float BlipFadeDuration = 2.5f; // Seconds for blip to fully fade
    private const float SweepHitThreshold = 0.15f; // Radians tolerance for sweep detection

    private float _lastSweepAngle;
    private readonly Dictionary<int, float> _blipRevealTimes = new(); // blip index -> reveal time

    // Colors for the radar display (green stalker aesthetic)
    private static readonly Color BackgroundColor = new(0.05f, 0.1f, 0.05f, 0.9f);
    private static readonly Color GridColor = new(0.1f, 0.4f, 0.1f, 0.5f);
    private static readonly Color RingColor = new(0.1f, 0.5f, 0.1f, 0.6f);
    private static readonly Color CenterColor = new(0.2f, 0.8f, 0.2f, 0.8f);

    public VelesRadarControl()
    {
        IoCManager.InjectDependencies(this);
    }

    public void UpdateBlips(List<VelesBlip> blips, float range)
    {
        _blips = blips;
        _range = range;

        // Check which blips the sweep just passed over
        var currentTime = (float)_timing.CurTime.TotalSeconds;
        var sweepAngle = MathF.Sin(currentTime * SweepSpeed * MathF.PI) * (MathF.PI / 2);

        for (int i = 0; i < blips.Count; i++)
        {
            var blipAngle = blips[i].Angle;

            // Check if sweep passed this blip since last frame
            var crossedBlip = (_lastSweepAngle <= blipAngle && sweepAngle >= blipAngle) ||
                              (_lastSweepAngle >= blipAngle && sweepAngle <= blipAngle) ||
                              MathF.Abs(sweepAngle - blipAngle) < SweepHitThreshold;

            if (crossedBlip)
            {
                _blipRevealTimes[i] = currentTime;
            }
        }

        _lastSweepAngle = sweepAngle;
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
        var centerY = size.Y - 10; // Bottom center with small margin
        var maxRadiusByWidth = (size.X / 2f) - 10; // Horizontal constraint
        var maxRadiusByHeight = centerY - 10; // Vertical constraint (10px top margin)
        var radius = Math.Min(maxRadiusByWidth, maxRadiusByHeight);

        // Draw background arc
        DrawFilledArc(handle, new Vector2(centerX, centerY), radius, BackgroundColor);

        // Draw range rings (at 33%, 66%, 100%)
        DrawArcRing(handle, new Vector2(centerX, centerY), radius * 0.33f, RingColor);
        DrawArcRing(handle, new Vector2(centerX, centerY), radius * 0.66f, RingColor);
        DrawArcRing(handle, new Vector2(centerX, centerY), radius, RingColor);

        // Draw radial grid lines (at -60, -30, 0, 30, 60 degrees)
        for (var angle = -60; angle <= 60; angle += 30)
        {
            var rad = angle * MathF.PI / 180f;
            var endX = centerX + MathF.Sin(rad) * radius;
            var endY = centerY - MathF.Cos(rad) * radius;
            handle.DrawLine(new Vector2(centerX, centerY), new Vector2(endX, endY), GridColor);
        }

        // Draw sweep line with trail
        DrawSweepLine(handle, centerX, centerY, radius);

        // Draw center point
        handle.DrawCircle(new Vector2(centerX, centerY), 3f, CenterColor);

        // Draw blips with fade effect
        DrawBlips(handle, centerX, centerY, radius);
    }

    private void DrawSweepLine(DrawingHandleScreen handle, float centerX, float centerY, float radius)
    {
        var time = (float)_timing.CurTime.TotalSeconds;
        var sweepAngle = MathF.Sin(time * SweepSpeed * MathF.PI) * (MathF.PI / 2);

        // Draw fading trail
        var sweepDirection = MathF.Cos(time * SweepSpeed * MathF.PI);
        for (int i = 5; i >= 1; i--)
        {
            var trailAngle = sweepAngle - (i * 0.08f * MathF.Sign(sweepDirection));
            trailAngle = Math.Clamp(trailAngle, -MathF.PI / 2, MathF.PI / 2);

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

        for (int i = 0; i < _blips.Count; i++)
        {
            var blip = _blips[i];

            // Calculate blip alpha based on time since revealed
            float alpha = 0f;
            if (_blipRevealTimes.TryGetValue(i, out var revealTime))
            {
                var timeSinceReveal = currentTime - revealTime;
                if (timeSinceReveal < BlipFadeDuration)
                {
                    // Bright at reveal, fades to 0 over BlipFadeDuration
                    alpha = 1f - (timeSinceReveal / BlipFadeDuration);
                }
            }

            if (alpha <= 0.01f)
                continue; // Don't draw invisible blips

            // Convert blip angle and distance to screen coordinates
            var screenAngle = blip.Angle;
            var normalizedDistance = blip.Distance / _range;
            var blipRadius = normalizedDistance * radius;

            var blipX = centerX + MathF.Sin(screenAngle) * blipRadius;
            var blipY = centerY - MathF.Cos(screenAngle) * blipRadius;

            // Size based on distance (closer = larger)
            var blipSize = 4f + (1f - normalizedDistance) * 4f;

            // Color with fade alpha - bright when just revealed
            var intensity = alpha > 0.8f ? 1f : 0.8f;
            var color = new Color(intensity, intensity, intensity, alpha);

            handle.DrawCircle(new Vector2(blipX, blipY), blipSize, color);
        }
    }

    private void DrawFilledArc(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        // Draw a filled semi-circle (180-degree arc facing up)
        const int segments = 32;
        _arcPoints[0] = center;

        for (var i = 0; i <= segments; i++)
        {
            var angle = MathF.PI - MathF.PI * i / segments;
            _arcPoints[i + 1] = new Vector2(
                center.X + MathF.Cos(angle) * radius,
                center.Y - MathF.Sin(angle) * radius
            );
        }

        // Draw as triangles from center
        for (var i = 1; i < _arcPoints.Length - 1; i++)
        {
            DrawTriangle(handle, _arcPoints[0], _arcPoints[i], _arcPoints[i + 1], color);
        }
    }

    private void DrawTriangle(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        var vertices = new Vector2[] { a, b, c };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, vertices, color);
    }

    private void DrawArcRing(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        // Draw a semi-circle arc (180 degrees facing up)
        const int segments = 32;
        Vector2? prevPoint = null;

        for (var i = 0; i <= segments; i++)
        {
            var angle = MathF.PI - MathF.PI * i / segments;
            var point = new Vector2(
                center.X + MathF.Cos(angle) * radius,
                center.Y - MathF.Sin(angle) * radius
            );

            if (prevPoint != null)
            {
                handle.DrawLine(prevPoint.Value, point, color);
            }

            prevPoint = point;
        }
    }
}
