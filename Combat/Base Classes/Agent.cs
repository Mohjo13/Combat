using UnityEngine;

#region Agent Base Class

/// <summary>
/// Abstract base class for all characters in the game � Player and all Enemy types.
/// Holds shared stats, state machine, weapon reference, hurtbox reference, and damage reception.
/// PlayerAgent and EnemyBase extend this and add their own behaviour on top.
/// Never placed directly in a scene.
/// </summary>
public abstract class Agent : MonoBehaviour, IStateMachine
{
    #region Stats

    /// <summary>
    /// The maximum HP this agent can have. Set in the inspector per character type.
    /// Never modified at runtime � used as the ceiling when restoring health.
    /// </summary>
    [SerializeField] protected float maxHp;

    /// <summary>
    /// The agent's current HP. Reduced by TakeDamage(), never drops below zero.
    /// When it reaches zero, Die() is called.
    /// </summary>
    protected float currentHp;

    /// <summary>
    /// Base attack power. Used by CombatManager when building a DamageInfo struct.
    /// WeaponBase damage is multiplied against this value.
    /// </summary>
    [SerializeField] protected float attackStat;

    /// <summary>
    /// Base defence value. Subtracted from incoming damage inside TakeDamage().
    /// Higher values reduce damage taken � cannot reduce damage below zero.
    /// </summary>
    [SerializeField] protected float defenceStat;

    #endregion

    #region Weapon

    /// <summary>
    /// The weapon this agent currently has equipped.
    /// Swappable at runtime via SetCurrentWeapon().
    /// CombatManager reads this when resolving hits.
    /// </summary>
    [SerializeField] protected WeaponBase currentWeapon;

    #endregion

    #region Hurtbox

    /// <summary>
    /// Reference to the hurtbox child GameObject.
    /// HurtboxHandler lives on this object and fires ResolveHit() to CombatManager
    /// when another character's weapon hitbox overlaps it.
    /// </summary>
    [SerializeField] protected GameObject hurtboxObject;

    #endregion

#region State Machine

    [SerializeField] protected float staggerDuration = 0.8f;

    /// <summary>
    /// The state this agent is currently in.
    /// Read by CombatManager to decide hit, block, and parry outcomes.
    /// Only changed through ChangeState() — never set directly.
    /// </summary>
    protected AgentState currentState;

    /// <summary>
    /// Public read access to the current state.
    /// Satisfies the IStateMachine interface contract.
    /// </summary>
    public AgentState CurrentState => currentState;

    private Coroutine staggerRecoveryCoroutine;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initialise shared values on startup.
    /// Sets currentHp to maxHp and puts the agent into the Idle state.
    /// Subclasses that override Awake() must call base.Awake() first.
    /// </summary>
    protected virtual void Awake()
    {
        // Start at full health
        currentHp = maxHp;

        // All agents begin in Idle � CombatManager and AI will drive transitions from here
        currentState = AgentState.Idle;
    }

    #endregion

    #region State Machine Implementation

    /// <summary>
    /// Transition from the current state into a new one.
    /// Logs the transition in the editor for debugging.
    /// Subclasses can override this to hook in animation or audio triggers on state change.
    /// </summary>
    /// <param name="newState">The AgentState to transition into.</param>
public virtual void ChangeState(AgentState newState)
    {
        if (currentState == newState) return;

        Debug.Log($"{gameObject.name}: {currentState} -> {newState}");

        if (staggerRecoveryCoroutine != null)
        {
            StopCoroutine(staggerRecoveryCoroutine);
            staggerRecoveryCoroutine = null;
        }

        currentState = newState;

        if (newState == AgentState.Staggered)
            staggerRecoveryCoroutine = StartCoroutine(StaggerRecoveryRoutine());
    }

    private System.Collections.IEnumerator StaggerRecoveryRoutine()
    {
        yield return new WaitForSeconds(staggerDuration);

        if (currentState == AgentState.Staggered)
        {
            currentState = AgentState.Idle;
            Debug.Log($"{gameObject.name}: Staggered -> Idle (recovery)");
        }

        staggerRecoveryCoroutine = null;
    }

    #endregion



    #region Health Utility

    /// <summary>
    /// Returns the agent's current HP.
    /// Used by UI systems and AI decision logic to read health without direct field access.
    /// </summary>
    public float GetCurrentHp() => currentHp;

    /// <summary>
    /// Returns the agent's maximum HP.
    /// Used by health bar UI to calculate the fill ratio.
    /// </summary>
    public float GetMaxHp() => maxHp;

    public virtual void TakeDamage(float damageAmount)
    {
        currentHp -= damageAmount;
        if (currentHp <= 0f)
            Die();
    }

    #endregion

    #region Weapon Utility

    /// <summary>
    /// Returns the weapon this agent currently has equipped.
    /// CombatManager and animation controllers use this to read weapon data.
    /// </summary>
    public WeaponBase GetCurrentWeapon() => currentWeapon;

    /// <summary>
    /// Swap the agent's equipped weapon at runtime.
    /// Disables the old weapon's hitbox before switching to avoid stale colliders staying active.
    /// </summary>
    /// <param name="newWeapon">The WeaponBase to equip.</param>
    public void SetCurrentWeapon(WeaponBase newWeapon)
    {
        // Disable the outgoing weapon's hitbox before swapping
        if (currentWeapon != null)
            currentWeapon.HitboxCollider.enabled = false;

        currentWeapon = newWeapon;
    }

    #endregion

    #region Death

    /// <summary>
    /// Handle shared death behaviour. Sets state to Dead and disables the hurtbox
    /// so the corpse cannot receive further hits.
    /// Overridden by PlayerAgent to trigger game over and by EnemyBase to trigger loot and despawn.
    /// </summary>
    protected virtual void Die()
    {
        ChangeState(AgentState.Dead);

        // Disable the hurtbox so this agent stops receiving hits after death
        if (hurtboxObject != null)
            hurtboxObject.SetActive(false);
    }

    /// <summary>
    /// Reset health and state for object-pooling reuse.
    /// </summary>
    public virtual void ResetAgent()
    {
        currentHp = maxHp;
        currentState = AgentState.Idle;
        if (hurtboxObject != null)
            hurtboxObject.SetActive(true);
    }

    #endregion
}

#endregion