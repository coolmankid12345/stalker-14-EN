using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Stalker_EN.Devices.ArtifactRadar;

/// <summary>
/// Custom control that renders an animated sine waveform that oscillates faster
/// as the player gets closer to anomalies, matching the beep frequency.
/// Wave amplitude increases with proximity (more "bouncy" when closer).
/// </summary>
public sealed class ArtifactRadarWaveformControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;

    // Configuration matching the anomaly detector beep system
    private const float MaxBeepInterval = 2.5f;
    private const float MinBeepInterval = 0.05f;
    private const float DetectionRange = 10f;

    // Wave rendering parameters
    private const int WaveSegments = 100;
    private const float MaxAmplitude = 0.35f;
    private const float MinAmplitude = 0.10f;  // 10% of max when at edge of detection

    // Wave count scaling based on distance (tighter waves when closer)
    private const float MinWaveCount = 5f;   // At edge of detection (10m)
    private const float MaxWaveCount = 15f;  // When very close (0m)

    // Smooth animation tracking
    private float _accumulatedPhase;
    private float _lastUpdateTime = -1f;
    private float _currentFrequency;

    // State
    private bool _detectorEnabled;
    private float? _closestAnomalyDistance;

    // Colors (green stalker aesthetic)
    private static readonly Color WaveColor = new(0.2f, 0.8f, 0.2f, 0.9f);
    private static readonly Color WaveColorDim = new(0.1f, 0.5f, 0.1f, 0.6f);
    private static readonly Color FlatLineColor = new(0.1f, 0.3f, 0.1f, 0.5f);

    public ArtifactRadarWaveformControl()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Updates the waveform display state based on detector status and anomaly proximity.
    /// </summary>
    public void UpdateState(bool detectorEnabled, float? closestAnomalyDistance)
    {
        _detectorEnabled = detectorEnabled;
        _closestAnomalyDistance = closestAnomalyDistance;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var currentTime = (float)_timing.CurTime.TotalSeconds;

        // Initialize on first frame
        if (_lastUpdateTime < 0)
            _lastUpdateTime = currentTime;

        var deltaTime = currentTime - _lastUpdateTime;
        _lastUpdateTime = currentTime;

        // Calculate target frequency
        var targetFrequency = CalculateTargetFrequency();

        // Smoothly interpolate frequency to avoid phase jumps
        _currentFrequency = MathHelper.Lerp(_currentFrequency, targetFrequency, Math.Min(1f, deltaTime * 5f));

        // Accumulate phase continuously (no jumps when frequency changes)
        _accumulatedPhase += _currentFrequency * deltaTime * MathF.PI * 2f;

        // Keep phase in reasonable range to avoid float precision issues
        if (_accumulatedPhase > MathF.PI * 100f)
            _accumulatedPhase -= MathF.PI * 100f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var centerY = size.Y / 2f;

        // When detector is off, show empty black panel (flat dim line)
        if (!_detectorEnabled)
        {
            handle.DrawLine(
                new Vector2(0, centerY),
                new Vector2(size.X, centerY),
                FlatLineColor);
            return;
        }

        // When detector is on but no anomaly, show flat center line
        if (!_closestAnomalyDistance.HasValue)
        {
            handle.DrawLine(
                new Vector2(0, centerY),
                new Vector2(size.X, centerY),
                WaveColorDim);
            return;
        }

        // Anomaly detected - calculate amplitude based on distance (not frequency)
        // Scale: MinAmplitude at edge of range, 1.0 when right next to anomaly
        var distance = _closestAnomalyDistance.Value;
        var distanceScale = 1f - Math.Clamp(distance / DetectionRange, 0f, 1f);  // 0 at edge, 1 at anomaly
        var amplitudeScale = MinAmplitude + (1f - MinAmplitude) * distanceScale; // 0.1 to 1.0
        var amplitude = size.Y * MaxAmplitude * amplitudeScale;

        // Draw the animated sine wave using accumulated phase
        // Scale wave count based on distance (more waves = tighter when closer)
        var waveCount = MinWaveCount + (MaxWaveCount - MinWaveCount) * distanceScale;

        Vector2? prevPoint = null;
        for (var i = 0; i <= WaveSegments; i++)
        {
            var x = (float)i / WaveSegments * size.X;
            var normalizedX = (float)i / WaveSegments * MathF.PI * 2f * waveCount;

            var y = centerY + MathF.Sin(normalizedX + _accumulatedPhase) * amplitude;

            var point = new Vector2(x, y);

            if (prevPoint != null)
            {
                handle.DrawLine(prevPoint.Value, point, WaveColor);
            }

            prevPoint = point;
        }
    }

    private float CalculateTargetFrequency()
    {
        if (!_closestAnomalyDistance.HasValue)
        {
            return 0f;
        }

        var distance = _closestAnomalyDistance.Value;

        // Use the same formula as the beep system
        var scalingFactor = Math.Clamp(distance / DetectionRange, 0f, 1f);
        var interval = (MaxBeepInterval - MinBeepInterval) * scalingFactor + MinBeepInterval;

        // Frequency is inverse of interval
        return 1f / interval;
    }
}
