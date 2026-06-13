/// <summary>
/// Speed preset for a single attack phase (Startup, Impact, or Recovery).
/// The multiplier is applied to the Animator SpeedMulti parameter at runtime.
/// Normal = 1.0x. Values above 1.0 slow the phase down; below 1.0 speed it up.
///
/// Labels reflect feel, not raw numbers — use these when authoring attacks.
///   Sluggish   × 1.50  (very slow wind-up or heavy recovery)
///   Leisurely  × 1.25
///   Deliberate × 1.10  (slightly heavier than natural)
///   Normal     × 1.00  (clip plays at natural speed)
///   Rapid      × 0.90
///   Quick      × 0.80
///   Flash      × 0.67  (very snappy impact or fast startup)
/// </summary>
public enum PhaseSpeed
{
    Sluggish   = 0,   // × 1.50
    Leisurely  = 1,   // × 1.25
    Deliberate = 2,   // × 1.10
    Normal     = 3,   // × 1.00
    Rapid      = 4,   // × 0.90
    Quick      = 5,   // × 0.80
    Flash      = 6,   // × 0.67
}

public static class PhaseSpeedExtensions
{
    /// <summary>Returns the animator SpeedMulti multiplier for this preset.</summary>
    public static float ToMultiplier(this PhaseSpeed speed)
    {
        switch (speed)
        {
            case PhaseSpeed.Sluggish:   return 1.50f;
            case PhaseSpeed.Leisurely:  return 1.25f;
            case PhaseSpeed.Deliberate: return 1.10f;
            case PhaseSpeed.Normal:     return 1.00f;
            case PhaseSpeed.Rapid:      return 0.90f;
            case PhaseSpeed.Quick:      return 0.80f;
            case PhaseSpeed.Flash:      return 0.67f;
            default:                    return 1.00f;
        }
    }

    /// <summary>Returns the real-world duration of a phase given its natural clip length.</summary>
    public static float ApplyTo(this PhaseSpeed speed, float naturalSeconds)
    {
        return naturalSeconds * speed.ToMultiplier();
    }
}
