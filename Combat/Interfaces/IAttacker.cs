#region Using Directives

// No Unity-specific using needed here because this is a plain C# interface file.

#endregion

/// <summary>
/// Contract for anything that can perform an attack.
/// 
/// This allows different systems (Player, Enemy, Weapon) to trigger attacks
/// in a consistent way without knowing the exact implementation details.
/// 
/// Keeps combat modular — new attackers can be added later without
/// changing existing systems.
/// </summary>
public interface IAttacker
{
    #region Methods

    /// <summary>
    /// Executes an attack based on the provided attack type.
    /// 
    /// The implementing class decides what this means:
    /// - Player → consumes stamina, triggers animation
    /// - Enemy → runs AI-driven attack logic
    /// - Weapon → may define hitbox behaviour or damage scaling
    /// </summary>
    /// <param name="type">The type of attack (Light or Heavy).</param>
    void PerformAttack(AttackType type);

    #endregion
}