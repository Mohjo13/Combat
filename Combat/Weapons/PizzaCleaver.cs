using UnityEngine;

#region Sword

/// <summary>
/// Standard balanced weapon. Extends WeaponBase.
/// No special mechanics — serves as the baseline all other weapons are tuned against.
/// The first weapon implemented and the one used to verify the full damage flow end to end.
/// </summary>
public class PizzaCleaver : WeaponBase
{
    #region IAttacker Implementation

    /// <summary>
    /// Perform a light or heavy sword attack.
    /// Light attacks are fast with a standard damage multiplier.
    /// Heavy attacks are slower but deal increased damage and can break blocks.
    /// Damage calculation is handled by the base class GetDamage() method.
    /// The actual hit resolution happens in CombatManager when the hitbox overlaps a hurtbox.
    /// </summary>
    /// <param name="type">Light or Heavy — drives the damage multiplier and animation variant.</param>
    public override void PerformAttack(AttackType type)
    {
        // Retrieve the final damage value for this attack type from the base class
        float damage = GetDamage(type);

        // Log attack in editor for debugging during development
        Debug.Log($"Pizza Cleaver attack: {type} — damage value: {damage}");

        // Hitbox activation is handled by HitboxManager via Animation Events,
        // not here. PerformAttack() triggers the intent — the animation drives the rest.
    }

    #endregion
}

#endregion