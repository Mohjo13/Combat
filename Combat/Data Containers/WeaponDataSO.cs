using UnityEngine;

#region Weapon Data ScriptableObject

/// <summary>
/// ScriptableObject holding all static stats for a weapon type.
/// Assigned to WeaponBase in the inspector. Never modified at runtime.
/// Create assets via Assets > Create > Combat > Weapon Data.
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    #region Base Stats

    /// <summary>
    /// The base damage value before attack type multipliers are applied.
    /// WeaponBase.GetDamage() multiplies this against the light or heavy multiplier.
    /// </summary>
    [SerializeField] public float baseDamage;

    /// <summary>
    /// Controls how quickly this weapon can swing.
    /// Used by the animation controller to set the attack animation speed.
    /// Higher values mean faster swings.
    /// </summary>
    [SerializeField] public float attackSpeed;

    #endregion

    #region Attack Multipliers

    /// <summary>
    /// Damage multiplier applied to light attacks.
    /// Typically below 1.0 — light attacks are fast but deal less damage.
    /// </summary>
    [SerializeField] public float lightMultiplier;

    /// <summary>
    /// Damage multiplier applied to heavy attacks.
    /// Typically above 1.0 — heavy attacks are slow but deal more damage.
    /// </summary>
    [SerializeField] public float heavyMultiplier;

    #endregion

    #region Hitbox Settings

    /// <summary>
    /// The size of this weapon's hitbox collider.
    /// Applied to the BoxCollider on the weapon GameObject at setup.
    /// Larger weapons like spears should have a longer reach here.
    /// </summary>
    [SerializeField] public Vector3 hitboxSize;

    /// <summary>
    /// Optional offset to reposition the hitbox relative to the weapon pivot.
    /// Useful for weapons where the contact point is not at the origin.
    /// </summary>
    [SerializeField] public Vector3 hitboxOffset;

    #endregion

    #region Timing

    /// <summary>
    /// Time in seconds before the active hitbox frames begin.
    /// Used by the animation system to delay hitbox activation after the swing starts.
    /// </summary>
    [SerializeField] public float windUpTime;

    /// <summary>
    /// How long in seconds the hitbox stays active during a swing.
    /// Longer windows give the player more time to land hits but increase risk.
    /// </summary>
    [SerializeField] public float activeFramesDuration;

    /// <summary>
    /// Time in seconds after the active frames end before the agent returns to Idle.
    /// Longer recovery makes attacks feel more committed and punishable.
    /// </summary>
    [SerializeField] public float recoveryTime;

    #endregion

    #region Special Properties

    /// <summary>
    /// How much of the defender's defence stat this weapon ignores.
    /// A value of 0 means full defence applies. A value of 1 means defence is ignored entirely.
    /// </summary>
    [SerializeField] public float armourPenetration;

    /// <summary>
    /// How strongly this weapon staggers on hit.
    /// Higher values could extend stagger duration in future stagger scaling logic.
    /// </summary>
    [SerializeField] public float staggerStrength;

    /// <summary>
    /// Force applied to the receiver on hit.
    /// Used by a future knockback system — stored here so it can be tuned per weapon.
    /// </summary>
    [SerializeField] public float knockbackForce;

    #endregion
}

#endregion