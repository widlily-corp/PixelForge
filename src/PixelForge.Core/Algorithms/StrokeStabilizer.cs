namespace PixelForge.Core.Algorithms;

/// <summary>
/// Stroke stabilizer providing weighted moving average / exponential smoothing
/// to eliminate hand jitter and produce smooth, confident lines.
/// </summary>
public class StrokeStabilizer
{
    private float _currentX;
    private float _currentY;
    private bool _hasStarted;
    private int _strength;

    /// <summary>
    /// Gets or sets smoothing strength from 0 (raw input, no smoothing) to 10 (maximum smooth lag).
    /// Values outside [0, 10] are clamped automatically.
    /// </summary>
    public int Strength
    {
        get => _strength;
        set => _strength = Math.Clamp(value, 0, 10);
    }

    /// <summary>
    /// Gets whether a stroke has started and the stabilizer holds an initial position.
    /// </summary>
    public bool HasStarted => _hasStarted;

    /// <summary>
    /// Gets the current floating point position of the stabilizer.
    /// </summary>
    public (float X, float Y) CurrentPosition => (_currentX, _currentY);

    /// <summary>
    /// Initializes a new instance of <see cref="StrokeStabilizer"/> with the specified strength.
    /// </summary>
    /// <param name="strength">Smoothing strength from 0 to 10.</param>
    public StrokeStabilizer(int strength = 0)
    {
        Strength = strength;
    }

    /// <summary>
    /// Resets the stabilizer state at the beginning of a stroke.
    /// </summary>
    public void Reset()
    {
        _hasStarted = false;
        _currentX = 0;
        _currentY = 0;
    }

    /// <summary>
    /// Feeds raw mouse coordinates and returns stabilized integer coordinates.
    /// </summary>
    /// <param name="rawX">Raw X coordinate from pointer device.</param>
    /// <param name="rawY">Raw Y coordinate from pointer device.</param>
    /// <returns>Smoothed integer coordinate pair.</returns>
    public (int X, int Y) Process(int rawX, int rawY)
    {
        if (Strength <= 0 || !_hasStarted)
        {
            _currentX = rawX;
            _currentY = rawY;
            _hasStarted = true;
            return (rawX, rawY);
        }

        // Higher strength = lower alpha = more smoothing
        float alpha = 1.0f / (1.0f + (Strength * 0.45f));
        _currentX += alpha * (rawX - _currentX);
        _currentY += alpha * (rawY - _currentY);

        return ((int)MathF.Round(_currentX), (int)MathF.Round(_currentY));
    }

    /// <summary>
    /// Feeds raw mouse coordinates and returns stabilized floating-point coordinates.
    /// </summary>
    /// <param name="rawX">Raw X coordinate from pointer device.</param>
    /// <param name="rawY">Raw Y coordinate from pointer device.</param>
    /// <returns>Smoothed floating-point coordinate pair.</returns>
    public (float X, float Y) Process(float rawX, float rawY)
    {
        if (Strength <= 0 || !_hasStarted)
        {
            _currentX = rawX;
            _currentY = rawY;
            _hasStarted = true;
            return (rawX, rawY);
        }

        float alpha = 1.0f / (1.0f + (Strength * 0.45f));
        _currentX += alpha * (rawX - _currentX);
        _currentY += alpha * (rawY - _currentY);

        return (_currentX, _currentY);
    }
}
