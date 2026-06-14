using DG.Tweening;
using UnityEngine;

#region Combo Handler

/// <summary>
/// Lives on the Player GameObject.
/// Tracks where the player is in a ComboSequence chain and whether a follow-up
/// input is currently valid.
///
/// INPUT BUFFERING:
/// When the player presses attack during a combo window, the input is stored in a
/// buffer slot. AttackAnimationDriver reads the buffer at the exit window timestamp
/// and fires the crossfade then. This separates "accepting input" (combo window)
/// from "executing the transition" (exit window).
///
/// AttackAnimationDriver signals this script when the combo window opens and closes.
/// PlayerCombatActions calls OnInputReceived() on every attack press.
/// AttackAnimationDriver polls HasBufferedInput and calls ConsumeBuffer() at exit time.
/// </summary>
public class ComboHandler : MonoBehaviour
{
    #region References

    private PlayerAgent playerAgent;

    #endregion

    #region State

    private ComboSequence activeSequence;
    private int currentStepIndex;
    private bool inputWindowOpen;

    #endregion

    #region Input Buffer

    /// <summary>
    /// The AttackData queued during the combo window, waiting for the exit window
    /// timestamp before the crossfade fires.
    /// </summary>
    private AttackData bufferedAttack;
    private AttackType bufferedAttackType;
    private int _pendingStepIndex;

    public bool HasBufferedInput => bufferedAttack != null;

    #endregion

    #region Window State

    public bool IsWindowOpen => inputWindowOpen;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerAgent = GetComponent<PlayerAgent>();
        if (playerAgent == null)
            Debug.LogError($"ComboHandler on {gameObject.name}: PlayerAgent not found.");
    }

    #endregion

    #region Window Control

    /// <summary>
    /// Opens the combo input window. Clears stale buffer from previous attack.
    /// Called by AttackAnimationDriver when elapsed >= comboWindowStartSeconds.
    /// </summary>
    public void OpenComboWindow()
    {
        inputWindowOpen = true;
        Debug.Log("ComboHandler: combo window open.");
    }

    /// <summary>
    /// Closes the combo input window.
    /// Buffer is preserved — it will be consumed at the exit window timestamp.
    /// If no input was buffered and resetOnMiss is true, combo resets.
    /// </summary>
    public void CloseComboWindow()
    {
        if (inputWindowOpen && bufferedAttack == null)
        {
            bool comboJustCompleted = activeSequence != null && currentStepIndex == activeSequence.StepCount - 1;
            bool wasMidCombo = currentStepIndex > 0 && !comboJustCompleted;
            Debug.Log($"[ComboHandler] CloseComboWindow: stepIndex={currentStepIndex}, comboJustCompleted={comboJustCompleted}, wasMidCombo={wasMidCombo}");
            if (activeSequence != null && activeSequence.resetOnMiss)
                ResetCombo();
            if (wasMidCombo)
                CombatEvents.RaiseComboFailed();
        }

        inputWindowOpen = false;
        Debug.Log("ComboHandler: combo window closed.");
    }

    #endregion

    #region Input

    /// <summary>
    /// Called by PlayerCombatActions on every attack press.
    ///
    /// FRESH ATTACK (no window open):
    ///   Finds a matching opener and returns it immediately — fires right away.
    ///
    /// BUFFERED INPUT (window is open):
    ///   Validates against the next step. If valid, stores in buffer and returns null.
    ///   The crossfade will fire at the exit window timestamp via ConsumeBuffer().
    ///
    /// Returns null if buffered, nothing matched, or window is open with no sequence.
    /// </summary>
    public AttackData OnInputReceived(AttackType type)
    {
        if (inputWindowOpen && activeSequence != null)
        {
            int nextIndex = currentStepIndex + 1;

            // Search all sequences that share the same opener as the active one,
            // find one whose step at nextIndex matches the incoming input type.
            WeaponBase weapon = playerAgent.GetCurrentWeapon();
            WeaponLoadout loadout = weapon != null ? weapon.Loadout : null;
            ComboStep openerStep = activeSequence.GetStep(0);

            if (loadout != null && loadout.availableCombos != null && openerStep != null)
            {
                // 1. Try to advance within the current active sequence first
                if (nextIndex < activeSequence.StepCount)
                {
                    ComboStep nextInActive = activeSequence.GetStep(nextIndex);
                    if (nextInActive != null && nextInActive.requiredInput == type)
                    {
                        _pendingStepIndex  = nextIndex;
                        bufferedAttack     = nextInActive.attack;
                        bufferedAttackType = type;
                        Debug.Log($"ComboHandler: chain input buffered — staying on '{activeSequence.sequenceName}' step {_pendingStepIndex} ('{nextInActive.attack.attackName}'). Waiting for exit window.");
                        return null;
                    }
                }

                // 2. Active sequence didn't match — search for a branching sequence
                foreach (ComboSequence seq in loadout.availableCombos)
                {
                    if (seq == null || seq.StepCount == 0 || seq == activeSequence) continue;
                    ComboStep seqOpener = seq.GetStep(0);
                    if (seqOpener == null || seqOpener.requiredInput != openerStep.requiredInput) continue;
                    if (nextIndex >= seq.StepCount) continue;

                    ComboStep nextStep = seq.GetStep(nextIndex);
                    if (nextStep != null && nextStep.requiredInput == type)
                    {
                        activeSequence     = seq;
                        _pendingStepIndex  = nextIndex;
                        bufferedAttack     = nextStep.attack;
                        bufferedAttackType = type;
                        Debug.Log($"ComboHandler: chain input buffered — switched to '{seq.sequenceName}' step {_pendingStepIndex} ('{nextStep.attack.attackName}'). Waiting for exit window.");
                        return null;
                    }
                }
            }

            Debug.Log($"ComboHandler: input {type} did not match any sequence at step {nextIndex}. Combo failed.");
            ResetCombo();
            CombatEvents.RaiseComboFailed();
            return null;
        }

        if (inputWindowOpen)
        {
            Debug.Log($"ComboHandler: input {type} during window but no active sequence. Ignored.");
            return null;
        }

        return FindAndStartSequence(type);
    }

    /// <summary>
    /// Called by AttackAnimationDriver at the exit window timestamp (or animation end).
    /// Returns the buffered AttackData and clears the buffer.
    /// Returns null if nothing was buffered.
    /// </summary>
    public AttackData ConsumeBuffer(out AttackType bufferedType)
    {
        if (bufferedAttack == null)
        {
            bufferedType = AttackType.Light;
            return null;
        }

        AttackData result = bufferedAttack;
        bufferedType      = bufferedAttackType;
        bufferedAttack    = null;
        currentStepIndex  = _pendingStepIndex;

        if (currentStepIndex == activeSequence.StepCount - 1)
            CombatEvents.RaiseComboCompleted(activeSequence.sequenceName);

        Debug.Log($"ComboHandler: buffer consumed — firing '{result.attackName}'.");
        return result;
    }

    /// <summary>
    /// Register which attack just started. Hook for future use.
    /// Called by PlayerCombatActions after committing to an attack.
    /// </summary>
    public void OnAttackCommitted(AttackData attack) { }

    #endregion

    #region Sequence Search

    private AttackData FindAndStartSequence(AttackType type)
    {
        WeaponBase weapon = playerAgent.GetCurrentWeapon();
        if (weapon == null)
        {
            Debug.LogWarning("ComboHandler: no weapon equipped.");
            return null;
        }

        WeaponLoadout loadout = weapon.Loadout;
        if (loadout == null || loadout.availableCombos == null)
        {
            Debug.LogWarning("ComboHandler: weapon has no loadout or combos.");
            return null;
        }

        foreach (ComboSequence sequence in loadout.availableCombos)
        {
            if (sequence == null || sequence.StepCount == 0) continue;

            ComboStep opener = sequence.GetStep(0);
            if (opener == null) continue;

            if (opener.requiredInput == type)
            {
                activeSequence   = sequence;
                currentStepIndex = 0;
                bufferedAttack   = null;

                Debug.Log($"ComboHandler: starting sequence '{sequence.sequenceName}'.");
                return opener.attack;
            }
        }

        Debug.LogWarning($"ComboHandler: no matching sequence for input {type}.");
        return null;
    }

    #endregion

    #region Reset

    /// <summary>
    /// Clear all combo state including the input buffer.
    /// </summary>
    public void ResetCombo()
    {
        activeSequence    = null;
        currentStepIndex = 0;
        _pendingStepIndex = 0;
        bufferedAttack   = null;
        inputWindowOpen  = false;

        Debug.Log("ComboHandler: combo reset.");
    }

    #endregion
}

#endregion
