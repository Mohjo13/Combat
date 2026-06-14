using UnityEngine;

#region Attack Data Scriptable Object

/// <summary>
/// ScriptableObject defining one single attack — one swing.
/// Every attack in every combo chain is its own AttackData asset.
/// Pure data only — no references to other AttackData assets.
/// Combo chaining is defined entirely by ComboSequence assets.
///
/// All timing fields are in real seconds — authored first, animation clips
/// are then built by animators to match. The clip field is optional:
/// when assigned, clip.length is used as the live duration. When absent,
/// totalDuration drives the timing coroutine instead.
///
/// PHASE SPEED SYSTEM:
/// The clip is divided into three phases: Startup, Impact, Recovery.
/// impactTime   — where startup ends and impact begins (seconds into clip)
/// recoveryTime — where impact ends and recovery begins (seconds into clip)
/// These are clip-space stamps — they do not change with speed.
///
/// Each phase is assigned a PhaseSpeed preset (Sluggish → Flash).
/// AttackAnimationDriver converts the preset to a SpeedMulti multiplier and
/// sets it on the Animator at each phase boundary.
/// Set all three phases to Normal to play the clip at natural speed.
///
/// COMBO / EXIT WINDOW SYSTEM:
/// comboWindowStartSeconds — when input starts being accepted for chaining
/// comboWindowEndSeconds   — when input acceptance closes
/// exitWindowSeconds       — when the crossfade to the next attack fires
///                           (if a valid chain input was received during the combo window)
///                           Must be >= comboWindowStartSeconds. Defaults to comboWindowEndSeconds.
///
/// Create assets via: Assets > Create > Combat > Attack Data
/// </summary>
[CreateAssetMenu(fileName = "NewAttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    #region Identity

    [SerializeField] public string attackName;

    /// <summary>If true, this attack was triggered by a dodge input. Used by StreakUI to complete the row immediately.</summary>
    [SerializeField] public bool isDodgeAttack;

    /// <summary>Blend duration in seconds when crossfading into this attack from another.</summary>
    [SerializeField] public float crossfadeDuration = 0.1f;

    /// <summary>
    /// Optional animation clip. When assigned, clip.length is the authoritative duration.
    /// When absent, totalDuration is used instead.
    /// </summary>
    [SerializeField] public AnimationClip clip;

    public string AnimationClipName => clip != null ? clip.name : string.Empty;

    /// <summary>Authoritative duration when no clip is assigned.</summary>
    [SerializeField] public float totalDuration = 1f;

    public float ActiveDuration => clip != null ? clip.length : totalDuration;

    [SerializeField] public Sprite icon;

    #endregion

    #region Attack Type

    [SerializeField] public AttackType attackType;

    #endregion

    #region Damage

    [SerializeField] public float damageMultiplier = 1f;
    [SerializeField] public float staggerStrength  = 1f;

    #endregion

    #region Stamina

    [SerializeField] public float staminaCost = 10f;

    #endregion

    #region Hitbox Timing

    /// <summary>Seconds from attack start when the hitbox opens.</summary>
    [SerializeField] public float hitboxStartSeconds = 0.3f;

    /// <summary>Seconds from attack start when the hitbox closes.</summary>
    [SerializeField] public float hitboxEndSeconds = 0.6f;

    #endregion

    #region Combo Window

    /// <summary>
    /// Seconds from attack start when the animation is cut short and crossfaded to Idle.
    /// Set to 0 to disable — clip plays to full duration.
    /// </summary>
    [SerializeField] public float clipExitTime = 0f;

    /// <summary>Seconds from attack start when the combo input window opens.</summary>
    [SerializeField] public float comboWindowStartSeconds = 0.65f;

    /// <summary>Seconds from attack start when the combo input window closes.</summary>
    [SerializeField] public float comboWindowEndSeconds = 0.9f;

    /// <summary>
    /// Seconds from attack start when the crossfade to the next chained attack fires,
    /// provided a valid input was buffered during the combo window.
    /// Must be >= comboWindowStartSeconds. If set to 0, falls back to comboWindowEndSeconds.
    /// Allows independent tuning of "when input is accepted" vs "when the transition happens".
    /// </summary>
    [SerializeField] public float exitWindowSeconds = 0f;

    /// <summary>
    /// The effective exit point — when the crossfade to the next attack fires.
    /// Falls back to comboWindowEndSeconds if exitWindowSeconds is not set.
    /// </summary>
    public float EffectiveExitSeconds => exitWindowSeconds > 0f ? exitWindowSeconds : comboWindowEndSeconds;

    #endregion

    #region Phase Boundaries

    /// <summary>
    /// Clip-space stamp: where startup ends and impact begins (seconds into the clip).
    /// Set to 0 to disable phase speed entirely.
    /// </summary>
    [SerializeField] public float impactTime = 0f;

    /// <summary>
    /// Clip-space stamp: where impact ends and recovery begins (seconds into the clip).
    /// Must be greater than impactTime. Set to 0 to disable phase speed entirely.
    /// </summary>
    [SerializeField] public float recoveryTime = 0f;

    #endregion

    #region Gap Closer

    /// <summary>
    /// If true, this attack acts as a gap closer on hit: the enemy is knocked back
    /// harder, and the player lunges toward them immediately after impact.
    /// </summary>
    [SerializeField] public bool isGapCloser = false;

    /// <summary>Multiplier applied to enemy knockback force when isGapCloser is true.</summary>
    [SerializeField] public float gapCloserKnockbackMult = 2.5f;

    /// <summary>Max distance the player will lunge toward the target after a gap-closer hit.</summary>
    [SerializeField] public float gapCloserLungeDistance = 5f;

    /// <summary>Duration of the post-hit gap-closing lunge in seconds.</summary>
    [SerializeField] public float gapCloserLungeDuration = 0.15f;

    /// <summary>Delay before the gap-closing lunge begins, allowing the knockback to start.</summary>
    [SerializeField] public float gapCloserLungeDelay = 0.05f;

    #endregion

    #region Lunge Override

    /// <summary>If true, overrides the LungeMotor's default lunge distance multiplier for this attack.</summary>
    [SerializeField] public bool useCustomLunge = false;

    /// <summary>Multiplied against LungeMotor.lungeDistance when useCustomLunge is true.</summary>
    [SerializeField] public float customLungeMultiplier = 1f;

    #endregion

    #region Phase Speed Presets

    /// <summary>Speed preset for the Startup phase (0s → impactTime).</summary>
    [SerializeField] public PhaseSpeed startupSpeed = PhaseSpeed.Normal;

    /// <summary>Speed preset for the Impact phase (impactTime → recoveryTime).</summary>
    [SerializeField] public PhaseSpeed impactSpeed = PhaseSpeed.Normal;

    /// <summary>Speed preset for the Recovery phase (recoveryTime → end).</summary>
    [SerializeField] public PhaseSpeed recoverySpeed = PhaseSpeed.Normal;

    /// <summary>
    /// True when phase boundaries are valid. Phase speed is always applied when this is true —
    /// even Normal preset will explicitly set SpeedMulti to 1.0, keeping behaviour predictable.
    /// </summary>
    public bool HasPhaseOverrides => impactTime > 0f && recoveryTime > impactTime;

    /// <summary>Returns the real-world duration of a phase given its natural clip length.</summary>
    public float GetPhaseDuration(int phase)
    {
        float clipLen = ActiveDuration;
        switch (phase)
        {
            case 0: return impactTime                      * startupSpeed.ToMultiplier();
            case 1: return (recoveryTime - impactTime)     * impactSpeed.ToMultiplier();
            case 2: return (clipLen - recoveryTime)        * recoverySpeed.ToMultiplier();
            default: return 0f;
        }
    }

    /// <summary>Total real-world duration when phase speeds are applied.</summary>
    public float RemappedDuration =>
        HasPhaseOverrides
            ? GetPhaseDuration(0) + GetPhaseDuration(1) + GetPhaseDuration(2)
            : ActiveDuration;

    #endregion
}

#endregion
