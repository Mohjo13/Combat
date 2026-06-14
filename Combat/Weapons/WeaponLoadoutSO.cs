using UnityEngine;

#region Weapon Loadout

/// <summary>
/// ScriptableObject that is the single source of truth for a complete weapon setup.
/// WeaponBase holds a reference to this instead of a bare WeaponData asset.
/// A new weapon is created entirely by filling in a new WeaponLoadout asset and prefab �
/// no code changes required.
/// Create assets via: Assets > Create > Combat > Weapon Loadout
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponLoadout", menuName = "Combat/Weapon Loadout")]
public class WeaponLoadout : ScriptableObject
{
    #region Identity

    /// <summary>
    /// Display name for this loadout.
    /// Used in debug logs and asset organisation.
    /// Example: "BasicSword", "BattleAxe"
    /// </summary>
    [SerializeField] public string loadoutName;

    #endregion

    #region Weapon Stats

    /// <summary>
    /// The existing WeaponData ScriptableObject for this weapon.
    /// Holds baseDamage, attackSpeed, hitbox size, and all static stat data.
    /// WeaponBase.GetDamage() still reads from this via the loadout.
    /// All existing damage reads continue to work � nothing in CombatManager changes.
    /// </summary>
    [SerializeField] public WeaponData weaponData;

    #endregion

    #region Animation

    /// <summary>
    /// The AnimatorOverrideController that swaps this weapon's attack clips
    /// into the base Player Animator Controller.
    /// PlayerAnimationController applies this on Start and on weapon swap.
    /// Each weapon type has its own override asset � the base controller is never modified.
    /// </summary>
    [SerializeField] public AnimatorOverrideController overrideController;

    #endregion

    #region Default Attacks

    /// <summary>
    /// The AttackData used when no combo is active and the player presses Light.
    /// This is always the entry point for a light attack chain.
    /// PlayerCombatActions falls back to this when ComboHandler returns null.
    /// </summary>
    [SerializeField] public AttackData defaultLightAttack;

    /// <summary>
    /// The AttackData used when no combo is active and the player presses Heavy.
    /// This is always the entry point for a heavy attack chain.
    /// PlayerCombatActions falls back to this when ComboHandler returns null.
    /// </summary>
    [SerializeField] public AttackData defaultHeavyAttack;

    /// <summary>Attack played when Light is pressed during a dodge window.</summary>
    [SerializeField] public AttackData dodgeAttackLight;

    /// <summary>Attack played when Heavy is pressed during a dodge window.</summary>
    [SerializeField] public AttackData dodgeAttackHeavy;

    #endregion

    #region Combos

    /// <summary>
    /// All combo chains available to this weapon.
    /// ComboHandler checks this list to find valid chains when the player
    /// inputs attacks within the combo window.
    /// Leave empty until combo chains are defined � single attacks still work.
    /// </summary>
    [SerializeField] public ComboSequence[] availableCombos;

    #endregion

    #region Enchantment

    /// <summary>
    /// Optional status effect applied on hit.
    /// Plugs into the buff/debuff system via WeaponEnchantment.
    /// Leave null for a weapon with no enchantment.
    /// </summary>
   // [SerializeField] public WeaponEnchantment enchantment;

    #endregion

    #region Prefab

    /// <summary>
    /// Optional reference to the weapon prefab for runtime spawning.
    /// Not used in the core combat loop � available for inventory
    /// and weapon pickup systems.
    /// </summary>
    [SerializeField] public GameObject weaponPrefab;

    #endregion
}

#endregion