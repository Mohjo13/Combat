using UnityEngine;

#region Player Animation Controller

/// <summary>
/// Lives on the Player GameObject.
/// Watches AgentState every frame and drives Animator parameters to match.
/// Applies the AnimatorOverrideController from the equipped WeaponLoadout on Start.
/// Does NOT decide when to attack — only reacts to state changes driven by PlayerCombatActions.
/// </summary>
public class PlayerAnimationController : MonoBehaviour
{
    #region References

    /// <summary>
    /// The single Animator component on the Player GameObject.
    /// Assign in the Inspector. Driven entirely by this script.
    /// </summary>
    [SerializeField] private Animator animator;

    /// <summary>
    /// The PlayerAgent on the same GameObject.
    /// Read every frame to detect state changes and drive the correct parameters.
    /// </summary>
    private PlayerAgent playerAgent;

    /// <summary>
    /// The Animator layer index used for attack animations.
    /// Must match the attackLayerIndex set in AttackAnimationDriver.
    /// </summary>
    [SerializeField] private int attackLayerIndex = 0;
    [SerializeField] private int uppderbodyAnimatorLayerIndex = 0;


    #endregion

    #region Animator Parameter Names

    // Cached hashes — avoids repeated string allocation each frame.
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsBlocking = Animator.StringToHash("IsBlocking");
    private static readonly int IsParrying = Animator.StringToHash("IsParrying");
    private static readonly int IsStaggered = Animator.StringToHash("IsStaggered");
    private static readonly int PlayerHit = Animator.StringToHash("PlayerHit");

    #endregion

    #region State Tracking

    private AgentState previousState;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerAgent = GetComponent<PlayerAgent>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            Debug.LogError($"PlayerAnimationController on {gameObject.name}: Animator not found.");
        else
            animator.updateMode = AnimatorUpdateMode.Fixed;

        if (playerAgent == null)
            Debug.LogError($"PlayerAnimationController on {gameObject.name}: PlayerAgent not found.");
    }

    /// <summary>
    /// Apply the WeaponLoadout's AnimatorOverrideController on the first frame.
    /// Done in Start so WeaponBase has finished its own Awake first.
    /// </summary>
    private void Start()
    {
        WeaponBase weapon = playerAgent.GetCurrentWeapon();

        if (weapon != null && weapon.Loadout != null)
            ApplyOverride(weapon.Loadout);
        else
            Debug.LogWarning($"PlayerAnimationController on {gameObject.name}: no WeaponLoadout found on Start. Override not applied.");
    }

    private void OnEnable()
    {
        CombatEvents.OnPlayerHit += HandlePlayerHit;
    }
    private void OnDisable()
    {
        CombatEvents.OnPlayerHit -= HandlePlayerHit;
    }

    private void HandlePlayerHit(float damage)
    {
        animator.SetTrigger(PlayerHit);
    }

    /// <summary>
    /// Poll AgentState every frame and sync Animator parameters.
    /// </summary>
    private void Update()
    {
        AgentState current = playerAgent.CurrentState;

        SyncBoolParameters(current);

        if (current != previousState)
        {
            OnStateChanged(current);
            previousState = current;
        }
    }

    #endregion

    #region State Sync

    /// <summary>
    /// Set all bool parameters on the Animator to match the current AgentState.
    /// </summary>
    private void SyncBoolParameters(AgentState state)
    {
        animator.SetBool(IsMoving, state == AgentState.Moving);
        animator.SetBool(IsBlocking, state == AgentState.Blocking);
        animator.SetBool(IsParrying, state == AgentState.Parrying);
        animator.SetBool(IsStaggered, state == AgentState.Staggered);
    }

    /// <summary>
    /// Play an attack animation state by name.
    /// Called by PlayerCombatActions when committing to an attack.
    /// </summary>
    public void PlayAnimation(AgentState state, string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        if (state == AgentState.Parrying)
        {
            animator.Play(stateName, uppderbodyAnimatorLayerIndex, 0f);
        }
    }

    /// <summary>
    /// Crossfade into an attack animation — used when chaining combos.
    /// </summary>
    public void CrossfadeAnimation(AgentState state, string stateName, float fadeDuration)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        animator.CrossFade(stateName, fadeDuration, attackLayerIndex);
    }

    private void OnStateChanged(AgentState newState)
    {
        // PlayAttack() in PlayerCombatActions owns all attack trigger firing.
    }

    #endregion

    #region Override Controller

    /// <summary>
    /// Swap the active AnimatorOverrideController to match the equipped weapon.
    /// Called on Start and again on weapon swap at runtime.
    /// </summary>
    public void ApplyOverride(WeaponLoadout loadout)
    {
        if (loadout.overrideController == null)
            return;

        animator.runtimeAnimatorController = loadout.overrideController;
    }

    #endregion
}

#endregion