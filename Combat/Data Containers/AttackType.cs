#region Attack Type Enum
/// <summary>
/// Defines the type of attack being performed.
/// Used to differentiate behavior (damage, animation, combos).
/// </summary>
public enum AttackType
{
    #region Basic Attacks
    // Light attack (fast, low damage, combo starter)
    Light,

    // Heavy attack (slow, high damage, higher commitment)
    Heavy
    #endregion

    #region Future Extensions
    // Charged attack
    // Special attack
    // Ranged attack
    // etc.
    #endregion
}
#endregion