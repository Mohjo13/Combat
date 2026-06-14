using UnityEngine;

/// <summary>
/// Defines a single attack for an enemy.
/// Create via Assets > Create > Pizza To Hell > Enemy Attack Data.
/// Assign to EnemyCombat's Attacks array in the inspector.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyAttack", menuName = "Pizza To Hell/Enemy Attack Data", order = 1)]
public class EnemyAttackDataSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Friendly name for debugging and UI")]
    public string displayName = "Attack";

    [Tooltip("Light or Heavy attack type")]
    public AttackType attackType = AttackType.Light;

    [Header("Timing (seconds)")]
    [Tooltip("Telegraph/windup duration — player can read and react")]
    public float telegraphDuration = 0.4f;

    [Tooltip("Active frames — hitbox is live, damage can land")]
    public float activeDuration = 0.15f;

    [Tooltip("Recovery duration — enemy is vulnerable, player punish window")]
    public float recoveryDuration = 0.3f;

    [Tooltip("Cooldown before this attack can be used again")]
    public float cooldown = 1.5f;

    [Header("Damage")]
    [Tooltip("Base damage dealt on hit")]
    public float damage = 10f;

    [Tooltip("Knockback force applied on hit")]
    public float knockbackForce = 4f;

    [Tooltip("Whether this is a gap closer — suppresses knockback on hit")]
    public bool isGapCloser = false;

    [Header("Animation")]
    [Tooltip("Animation clip to play for this attack")]
    public AnimationClip animationClip;

    [Header("Visual Telegraph")]
    [Tooltip("Indicator prefab shown during telegraph phase (e.g. ground marker for ranged)")]
    public GameObject telegraphIndicator;

    [Tooltip("Color for the telegraph indicator")]
    public Color telegraphColor = Color.red;

    [Header("Behavior")]
    [Tooltip("If true, this attack can be interrupted by stagger")]
    public bool interruptible = true;

    [Tooltip("Movement speed multiplier during this attack (0 = locked in place)")]
    [Range(0f, 1f)]
    public float movementSpeedMultiplier = 0f;

    #region Helpers

    public float TotalDuration => telegraphDuration + activeDuration + recoveryDuration;

    public float TelegraphEndTime => telegraphDuration;
    public float ActiveEndTime => telegraphDuration + activeDuration;

    #endregion
}
