#region Damage Info Struct

/// <summary>
/// Carries all data related to a single hit event.
/// Built exclusively by CombatManager.BuildDamageInfo() — never by characters themselves.
/// Passed into TakeDamage() on the receiving Agent so all hit context travels together.
/// </summary>
public struct DamageInfo
{
    #region Fields

    /// <summary>
    /// The final damage amount before defence is applied.
    /// Calculated from the attacker's weapon data and attack type multiplier.
    /// </summary>
    public float amount;

    /// <summary>
    /// Whether this was a Light or Heavy attack.
    /// Used by Agent.TakeDamage() to decide whether to apply stagger.
    /// Used by CombatManager.ResolveBlock() to decide whether to break the block.
    /// </summary>
    public AttackType type;

    /// <summary>
    /// The Agent who dealt this hit.
    /// Stored for tracking purposes — kill credit, aggro, UI feedback.
    /// </summary>
    public Agent attacker;

    /// <summary>
    /// The weapon that landed this hit.
    /// Stored for source tracking — enchantment triggers, VFX selection.
    /// </summary>
    public WeaponBase sourceWeapon;

    #endregion

    #region Constructor

    /// <summary>
    /// Construct a fully populated DamageInfo.
    /// Called by CombatManager.BuildDamageInfo() when a hit event is received.
    /// </summary>
    /// <param name="amount">Final damage value before defence reduction.</param>
    /// <param name="type">Light or Heavy — determines stagger and block-break rules.</param>
    /// <param name="attacker">The Agent who initiated the attack.</param>
    /// <param name="sourceWeapon">The weapon that made contact.</param>
    public DamageInfo(float amount, AttackType type, Agent attacker, WeaponBase sourceWeapon)
    {
        this.amount = amount;
        this.type = type;
        this.attacker = attacker;
        this.sourceWeapon = sourceWeapon;
    }

    #endregion
}

#endregion