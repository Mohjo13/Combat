
#region Agent State Enum
/// <summary>
/// Defines all possible states an Agent can be in at any given moment.
/// States are mutually exclusive — an agent is always in exactly one state.
/// Used by: CombatManager, PlayerCombatActions, EnemyAIStateMachine, AnimationControllers.
/// Attack flavour (light/heavy) is handled separately by AttackType.cs.
/// </summary>
public enum AgentState
{
    #region Neutral States
    /// <summary>Standing still, no input, no threat response.</summary>
    Idle,

    /// <summary>Actively moving/repositioning. Not committed to any action.</summary>
    Moving,
    #endregion

    #region Attack States
    /// <summary>
    /// Telegraphing / wind-up before an attack. Animation is playing but hitbox is not yet active.
    /// Player reaction window. Can be interrupted by stagger.
    /// Transitions from: Idle, Moving.
    /// Transitions to: LightAttacking (telegraph ends), Staggered (if hit).
    /// </summary>
    Telegraphing,

    /// <summary>
    /// Performing a light attack. Fast, low damage, shorter hitbox window.
    /// Transitions from: Idle, Moving, LightAttacking (combo chain), HeavyAttacking (combo chain), Telegraphing.
    /// Transitions to: Idle (on complete), Staggered (if hit during), LightAttacking/HeavyAttacking (combo).
    /// </summary>
    LightAttacking,

    /// <summary>
    /// Performing a heavy attack. Slow, high damage, longer hitbox window.
    /// Transitions from: Idle, Moving, LightAttacking (combo chain).
    /// Transitions to: Idle (on complete), Staggered (if hit during).
    /// </summary>
    HeavyAttacking,
    #endregion

    #region Defensive States
    /// <summary>
    /// Actively blocking. Negates damage, costs stamina.
    /// Entered automatically when parry window expires without a hit.
    /// Transitions from: Parrying (window expired).
    /// Transitions to: Idle (on release), Staggered (if stamina depleted).
    /// </summary>
    Blocking,

    /// <summary>
    /// Brief window immediately after block input is pressed.
    /// If a hit lands during this window, a parry is triggered instead of a block.
    /// Duration controlled by ParryWindowHandler.cs.
    /// Transitions from: Idle, Moving (on block press).
    /// Transitions to: Blocking (window expired), Idle (parry successful).
    /// </summary>
    Parrying,

    /// <summary>
    /// Evading an attack. Grants invincibility frames during the roll.
    /// Cannot attack or block while dodging.
    /// Transitions from: Idle, Moving.
    /// Transitions to: Idle (on complete).
    /// </summary>
    Dodging,
    #endregion

    #region Reaction States
    /// <summary>
    /// Temporarily interrupted by a light hit or environmental hazard.
    /// Short recovery. Does NOT trigger parried-state bonuses.
    /// Transitions from: any state on hit received.
    /// Transitions to: Idle (on recovery complete).
    /// </summary>
    Staggered,

    /// <summary>
    /// Enemy was successfully parried by the player.
    /// Distinct from Staggered — longer duration, enables parry-follow-up window,
    /// can carry stat modifiers (e.g. increased damage taken).
    /// Managed by ParriedState.cs on the enemy.
    /// Transitions from: any attacking state (via CombatManager.ResolveParry).
    /// Transitions to: Idle (on ParriedState duration expired).
    /// </summary>
    Parried,
    #endregion

    #region Terminal State
    /// <summary>
    /// HP reached zero. No further state transitions possible.
    /// Checked as a guard at the top of ChangeState() and TakeDamage().
    /// </summary>
    Dead,
    #endregion

    #region Reserved / Future States
    // Recovering  — longer recovery animation after a heavy stagger or knockdown
    // Stunned     — longer than Staggered, used for special enemy abilities
    // KnockedDown — full ragdoll/floor state, requires a get-up animation
    Disabled,    //— used for scripted events, cutscenes, puzzle interactions
    // Interacting — talking to NPC, opening chest, pulling lever
    #endregion
}
#endregion
