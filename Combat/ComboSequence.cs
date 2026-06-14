using UnityEngine;

#region Combo Step

/// <summary>
/// One step in a ComboSequence chain.
/// Defines which attack plays at this step, what input is required to reach it,
/// and an optional timing window override so the same AttackData asset can be
/// reused across multiple combos with different windows.
/// Not a ScriptableObject — lives inside ComboSequence as a serialized class.
///
/// Window override values are in real seconds, matching AttackData's timing fields.
/// </summary>
[System.Serializable]
public class ComboStep
{
    /// <summary>
    /// The attack to play at this step in the chain.
    /// </summary>
    public AttackData attack;

    /// <summary>
    /// The input type the player must press to reach this step.
    /// Light or Heavy — ComboHandler matches this against the incoming input.
    /// </summary>
    public AttackType requiredInput;

    /// <summary>
    /// If true, use windowStartSeconds and windowEndSeconds below instead of
    /// the values on AttackData. Allows the same AttackData asset to be reused
    /// with different timing windows in different combo chains without needing
    /// duplicate assets.
    /// </summary>
    private bool overrideWindow;

    /// <summary>
    /// Override for comboWindowStartSeconds. Only used if overrideWindow is true.
    /// Real seconds from attack start when the combo input window opens for this step.
    /// </summary>
    private float windowStartSeconds;

    /// <summary>
    /// Override for comboWindowEndSeconds. Only used if overrideWindow is true.
    /// Real seconds from attack start when the combo input window closes for this step.
    /// </summary>
    private float windowEndSeconds;

    /// <summary>
    /// Returns the effective combo window start for this step.
    /// Uses the override if enabled, otherwise falls back to the AttackData value.
    /// </summary>
    public float EffectiveWindowStart => (overrideWindow && attack != null)
        ? windowStartSeconds
        : (attack != null ? attack.comboWindowStartSeconds : 0f);

    /// <summary>
    /// Returns the effective combo window end for this step.
    /// Uses the override if enabled, otherwise falls back to the AttackData value.
    /// </summary>
    public float EffectiveWindowEnd => (overrideWindow && attack != null)
        ? windowEndSeconds
        : (attack != null ? attack.comboWindowEndSeconds : 0f);
}

#endregion

#region Combo Sequence

/// <summary>
/// ScriptableObject defining one full combo chain as an ordered list of ComboStep entries.
/// The first step is always the opener. The last step is the finisher.
/// ComboHandler walks this list as the player inputs attacks within the combo window.
/// Every attack — including standalone single hits — must be its own ComboSequence with one step.
/// Create assets via: Assets > Create > Combat > Combo Sequence
/// </summary>
[CreateAssetMenu(fileName = "NewComboSequence", menuName = "Combat/Combo Sequence")]
public class ComboSequence : ScriptableObject
{
    #region Identity

    /// <summary>
    /// Internal name for this combo chain.
    /// Used in debug logs and asset organisation.
    /// Example: "SwordLLH", "SwordHeavy"
    /// </summary>
    [SerializeField] public string sequenceName;

    /// <summary>
    /// Optional display name shown in UI combo indicators.
    /// Example: "Cleave Combo"
    /// Leave blank if no UI display is needed.
    /// </summary>
    [SerializeField] public string comboName;

    #endregion

    #region Chain

    /// <summary>
    /// The ordered list of steps that make up this combo.
    /// Index 0 is always the opener — the first attack in the chain.
    /// The last entry is the finisher — it has no follow-up.
    /// ComboHandler walks this list by index as the player chains inputs.
    /// </summary>
    [SerializeField] public ComboStep[] steps;

    /// <summary>
    /// If true, missing the combo input window resets the chain to idle.
    /// If false, the last successfully completed attack simply stands and
    /// the player can start a fresh attack from Idle.
    /// </summary>
    [SerializeField] public bool resetOnMiss = true;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Returns the AttackData of the opener — steps[0].attack.
    /// Used by ComboHandler to find matching sequences when a fresh attack is input.
    /// </summary>
    public AttackData GetOpener()
    {
        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning($"ComboSequence '{sequenceName}': steps array is empty. Cannot get opener.");
            return null;
        }

        return steps[0].attack;
    }

    /// <summary>
    /// Returns the ComboStep at the given index with bounds checking.
    /// Returns null if the index is out of range.
    /// </summary>
    public ComboStep GetStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Length)
        {
            Debug.LogWarning($"ComboSequence '{sequenceName}': step index {index} is out of range.");
            return null;
        }

        return steps[index];
    }

    /// <summary>
    /// Returns true if the given index is the last step in the chain.
    /// ComboHandler uses this to know when to reset after the chain completes.
    /// </summary>
    public bool IsFinisher(int index)
    {
        if (steps == null) return false;
        return index == steps.Length - 1;
    }

    /// <summary>
    /// The total number of steps in this sequence.
    /// </summary>
    public int StepCount => steps != null ? steps.Length : 0;

    #endregion
}

#endregion