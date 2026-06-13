// No Unity-specific using needed here because this is a plain C# interface file.

/// <summary>
/// Contract for anything that can block or parry incoming attacks.
///
/// The parry button is the single input. Whether it resolves as a parry or block
/// is determined by CombatManager at the moment of impact:
///   - Hit arrives while AgentState == Parrying  → parry succeeds
///   - Hit arrives while AgentState == Blocking  → block resolves
/// ParryWindowHandler manages the Parrying → Blocking transition automatically.
/// </summary>
public interface IBlockable
{
    #region Properties

    /// <summary>True while the character is in the Blocking state.</summary>
    bool IsBlocking { get; }

    /// <summary>True while the character is in the Parrying window state.</summary>
    bool IsParrying { get; }

    #endregion

    #region Methods

    /// <summary>Parry button pressed. Opens parry window; falls through to Blocking if unhit.</summary>
    void OnParryPress();

    /// <summary>Parry button released. Exits Blocking state if active.</summary>
    void OnParryRelease();

    #endregion
}
