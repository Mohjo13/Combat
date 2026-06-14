using UnityEngine;

#region Attack Type Handler

/// <summary>
/// Lives on each character GameObject.
/// Tracks the AttackType the character most recently committed to � Light or Heavy.
/// Read by CombatManager when building a DamageInfo struct to ensure the correct
/// damage multiplier and block-breaking rules are applied.
/// Set by PlayerCombatActions for the player and by EnemyBase subtypes for enemies.
/// Will also be read by ComboHandler when combo chain logic is implemented.
/// </summary>
public class AttackTypeHandler : MonoBehaviour
{
    #region State

    /// <summary>
    /// The AttackType the character most recently committed to.
    /// Defaults to Light on startup � no assumption of a heavy attack until one is set.
    /// </summary>
    private AttackType currentAttackType = AttackType.Light;

    /// <summary>
    /// Public read access to the current attack type.
    /// CombatManager reads this when building a DamageInfo struct on hit resolution.
    /// </summary>
    public AttackType CurrentAttackType => currentAttackType;

    /// <summary>
    /// The AttackData of the most recently committed attack.
    /// Used by CombatManager to read gap-closer and other per-attack flags.
    /// </summary>
    public AttackData CurrentAttackData { get; private set; }

    #endregion

    #region Attack Type Control

    /// <summary>
    /// Set the current attack type before triggering an attack.
    /// Called by PlayerCombatActions when the player inputs a light or heavy attack,
    /// and by EnemyBase subtypes when the AI decides which attack to perform.
    /// Must be called before PerformAttack() so CombatManager reads the correct type
    /// when the hitbox overlaps a hurtbox.
    /// </summary>
    /// <param name="type">Light or Heavy � the attack the character is committing to.</param>
    public void SetAttackType(AttackType type)
    {
        currentAttackType = type;
    }

    /// <summary>
    /// Set the current attack data before triggering an attack.
    /// Called by PlayerCombatActions so downstream systems can read per-attack flags.
    /// </summary>
    public void SetAttackData(AttackData data)
    {
        CurrentAttackData = data;
    }

    #endregion
}

#endregion