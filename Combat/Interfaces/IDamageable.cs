/// <summary>
/// Implemented by any object that can receive damage.
/// Agent is the primary implementor.
/// </summary>
public interface IDamageable
{
    #region Methods

    /// <summary>
    /// Apply damage to this object using the data packed into a DamageInfo struct.
    /// CombatManager builds the struct and calls this � characters never call it on themselves.
    /// </summary>
    /// <param name="info">Struct containing damage amount, type, attacker, and source weapon.</param>
    void TakeDamage(float info);

    #endregion
}