using UnityEngine;
using System.Collections;

public class PlayerCombatActions : MonoBehaviour
{
    #region References

    private PlayerAgent playerAgent;
    private AttackTypeHandler attackTypeHandler;
    private ComboHandler comboHandler;
    private AttackAnimationDriver attackAnimationDriver;
    private PlayerAnimationController animController;
    private PlayerInput playerInput;
    private ParryHandler parryWindowHandler;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        playerAgent           = GetComponent<PlayerAgent>();
        attackTypeHandler     = GetComponent<AttackTypeHandler>();
        comboHandler          = GetComponent<ComboHandler>();
        attackAnimationDriver = GetComponent<AttackAnimationDriver>();
        animController        = GetComponent<PlayerAnimationController>();
        playerInput           = GetComponent<PlayerInput>();
        parryWindowHandler    = GetComponent<ParryHandler>();


        if (playerAgent           == null) Debug.LogError($"PlayerCombatActions on {gameObject.name}: PlayerAgent not found.");
        if (attackTypeHandler     == null) Debug.LogError($"PlayerCombatActions on {gameObject.name}: AttackTypeHandler not found.");
        if (comboHandler          == null) Debug.LogError($"PlayerCombatActions on {gameObject.name}: ComboHandler not found.");
        if (attackAnimationDriver == null) Debug.LogError($"PlayerCombatActions on {gameObject.name}: AttackAnimationDriver not found.");
        if (animController        == null) Debug.LogError($"PlayerCombatActions on {gameObject.name}: PlayerAnimationController not found.");
        if (playerInput           == null) Debug.LogError($"PlayerCombatActions on {gameObject.name}: PlayerInput not found.");
        if (parryWindowHandler    == null) Debug.LogError($"PlayerCombatActions on {gameObject.name}: ParryHandler not found.");
    }

    private void OnEnable()
    {
        if (playerInput != null)
        {
            playerInput.OnLightAttack += PerformLightAttack;
            playerInput.OnHeavyAttack += PerformHeavyAttack;
            playerInput.OnBlockStart  += OnBlockPressed;
            playerInput.OnBlockEnd    += OnBlockReleased;
        }

        CombatEvents.OnDodgeAttackBuffered += CommitDodgeAttack;
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            playerInput.OnLightAttack -= PerformLightAttack;
            playerInput.OnHeavyAttack -= PerformHeavyAttack;
            playerInput.OnBlockStart  -= OnBlockPressed;
            playerInput.OnBlockEnd    -= OnBlockReleased;
        }

        CombatEvents.OnDodgeAttackBuffered -= CommitDodgeAttack;
    }

    #endregion

    private bool _teleportLocked = false;
    public void SetTeleportLocked(bool locked) => _teleportLocked = locked;

    #region Attack
    [SerializeField] private PlayerFinisher _playerFinisher;
    public void PerformLightAttack() => PerformAttack(AttackType.Light);
    public void PerformHeavyAttack() => PerformAttack(AttackType.Heavy);

    /// <summary>
    /// Attempt to perform an attack of the given type.
    ///
    /// If an animation is playing AND the combo window is open:
    ///   ComboHandler buffers the input. The crossfade fires at exitWindowSeconds
    ///   via AAD — CommitChainedAttack() handles the state update then.
    ///
    /// If no animation is playing: fresh attack from Idle/Moving.
    /// </summary>
    public void PerformAttack(AttackType type)
    {
        if (_teleportLocked || _playerFinisher._isFinishing || ShopManager.instance.ShopUI.isOpen) return;

        if (attackAnimationDriver.IsPlaying)
        {
            if (comboHandler.IsWindowOpen)
            {
                // OnInputReceived buffers the input and returns null.
                // The crossfade + CommitChainedAttack will fire at exit time from AAD.
                comboHandler.OnInputReceived(type);
            }
            return;
        }

        if (playerAgent.CurrentState != AgentState.Idle &&
            playerAgent.CurrentState != AgentState.Moving)
            return;
        if (type == AttackType.Heavy)
        {
            if(_playerFinisher.TryFinisher(playerAgent.GetCurrentWeapon().GetDamage(type)))
            {
                return;
            }
        }
        AttackData attackData = comboHandler.OnInputReceived(type);

        if (attackData == null)
        {
            WeaponBase weapon = playerAgent.GetCurrentWeapon();
            if (weapon != null && weapon.Loadout != null)
            {
                attackData = type == AttackType.Light
                    ? weapon.Loadout.defaultLightAttack
                    : weapon.Loadout.defaultHeavyAttack;
            }

            if (attackData == null) return;
        }

        CommitAttack(attackData, type);
    }

    /// <summary>
    /// Called by AttackAnimationDriver at the exit window timestamp when a buffered
    /// chain input is ready to fire. The crossfade has already been sent to the Animator
    /// by AAD — this method updates stamina, state, and type handler, then calls
    /// BeginAttack (which will skip re-sending the CrossFade since it checks IsPlaying).
    /// </summary>
    public void CommitChainedAttack(AttackData attackData, AttackType type)
    {
        if (playerAgent.GetCurrentStamina() < attackData.staminaCost)
        {
            playerAgent.ChangeState(AgentState.Idle);
            comboHandler.ResetCombo();
            return;
        }

        attackTypeHandler.SetAttackType(type);
        attackTypeHandler.SetAttackData(attackData);

        playerAgent.ChangeState(type == AttackType.Light
            ? AgentState.LightAttacking
            : AgentState.HeavyAttacking);

        CombatEvents.RaiseAttackStartedWithIcon(attackData, isChained: true);
        comboHandler.OnAttackCommitted(attackData);

        WeaponBase weapon = playerAgent.GetCurrentWeapon();
        if (weapon != null)
            weapon.PerformAttack(type);

        attackAnimationDriver.BeginAttack(attackData, chain: true);
    }

    /// <summary>
    /// Called by AttackAnimationDriver at the end of an animation when a buffered
    /// input is waiting (legacy path — kept for fallback if exit window never fires).
    /// </summary>
    public void CommitBufferedAttack()
    {
        AttackData buffered = comboHandler.ConsumeBuffer(out AttackType bufferedType);

        if (buffered == null)
        {
            playerAgent.ChangeState(AgentState.Idle);
            return;
        }

        if (playerAgent.GetCurrentStamina() < buffered.staminaCost)
        {
            playerAgent.ChangeState(AgentState.Idle);
            comboHandler.ResetCombo();
            return;
        }

        attackAnimationDriver.NotifyComboChain();
        CommitAttack(buffered, bufferedType);
    }

    private void CommitAttack(AttackData attackData, AttackType type)
    {
        playerAgent.ConsumeStamina(attackData.staminaCost);
        attackTypeHandler.SetAttackType(type);
        attackTypeHandler.SetAttackData(attackData);

        playerAgent.ChangeState(type == AttackType.Light
            ? AgentState.LightAttacking
            : AgentState.HeavyAttacking);

        bool isChain = attackAnimationDriver.IsPlaying;

        CombatEvents.RaiseAttackStartedWithIcon(attackData);
        comboHandler.OnAttackCommitted(attackData);
        attackAnimationDriver.BeginAttack(attackData, isChain);

        WeaponBase weapon = playerAgent.GetCurrentWeapon();
        if (weapon != null)
            weapon.PerformAttack(type);
    }

    #endregion

    #region Parry

    private void CommitDodgeAttack(AttackData attackData, AttackType type)
    {
        if (attackData == null) return;
        if (playerAgent.GetCurrentStamina() < attackData.staminaCost) return;

        comboHandler.ResetCombo();
        CommitAttack(attackData, type);
    }

    public void OnBlockPressed()
    {
        if (_teleportLocked) return;
        if (playerAgent.CurrentState != AgentState.Idle &&
            playerAgent.CurrentState != AgentState.Moving) return;
        if (parryWindowHandler.IsOnCooldown) return;

        playerAgent.ChangeState(AgentState.Parrying);
        parryWindowHandler.OpenWindow();
    }

    public void OnBlockReleased() { }

    #endregion
}
