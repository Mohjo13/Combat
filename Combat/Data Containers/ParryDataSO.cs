using UnityEngine;

/// <summary>
/// ScriptableObject: all timing and modifier config for the parry system.
/// Create via: Assets > Create > Combat > Defence > Parry Data
/// Assign to ParryWindowHandler on the Player.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Defence/Parry Data", fileName = "ParryData")]
public class ParryDataSO : ScriptableObject
{
    #region Animation

    [Header("Animation")]
    [Tooltip("Animation clip played when the parry is triggered.")]
    [SerializeField] public AnimationClip parryClip;

    #endregion

    #region Timing

    [Header("Timing")]
    [Tooltip("Total duration of the parry animation in seconds. Authoring reference for the timeline.")]
    [SerializeField] public float totalDuration = 0.5f;

    [Tooltip("How long the parry input window stays open (seconds).")]
    [SerializeField] public float windowDuration = 0.15f;

    [Tooltip("After a successful or expired parry, how long before the player can parry again.")]
    [SerializeField] public float cooldown = 0.6f;

    #endregion

    #region ParryBox Timing

    [Header("ParryBox Active Frames")]
    [Tooltip("When the parrybox collider turns ON (seconds from parry start).")]
    [SerializeField] public float parryBoxStartTime = 0.0f;

    [Tooltip("When the parrybox collider turns OFF (seconds from parry start).")]
    [SerializeField] public float parryBoxEndTime = 0.15f;

    #endregion

    #region Phase Boundaries

    [Header("Phase Boundaries")]
    [Tooltip("Clip-space stamp: where startup ends and impact begins (seconds). Set to 0 to disable phase speed.")]
    [SerializeField] public float impactTime = 0f;

    [Tooltip("Clip-space stamp: where impact ends and recovery begins (seconds). Must be > impactTime.")]
    [SerializeField] public float recoveryTime = 0f;

    #endregion

    #region Phase Speed Presets

    [Header("Phase Speed")]
    [SerializeField] public PhaseSpeed startupSpeed  = PhaseSpeed.Normal;
    [SerializeField] public PhaseSpeed impactSpeed   = PhaseSpeed.Normal;
    [SerializeField] public PhaseSpeed recoverySpeed = PhaseSpeed.Normal;

    public bool HasPhaseOverrides => impactTime > 0f && recoveryTime > impactTime;

    public float ActiveDuration => parryClip != null ? parryClip.length : totalDuration;

    public float GetPhaseDuration(int phase)
    {
        float clipLen = ActiveDuration;
        switch (phase)
        {
            case 0: return impactTime                  * startupSpeed.ToMultiplier();
            case 1: return (recoveryTime - impactTime) * impactSpeed.ToMultiplier();
            case 2: return (clipLen - recoveryTime)    * recoverySpeed.ToMultiplier();
            default: return 0f;
        }
    }

    public float RemappedDuration =>
        HasPhaseOverrides
            ? GetPhaseDuration(0) + GetPhaseDuration(1) + GetPhaseDuration(2)
            : ActiveDuration;

    #endregion

}
