using System;
using System.Collections;
using UnityEngine;

#region Attack Animation Driver

public class AttackAnimationDriver : MonoBehaviour
{
    #region Static Debug State

    public static event Action<int, string> OnAttackStarted;
    public static event Action OnAttackEnded;

    public static int    CurrentStepIndex    { get; private set; } = -1;
    public static string CurrentAttackName   { get; private set; } = string.Empty;
    public static bool   IsAttackActive      { get; private set; } = false;
    public static bool   IsHitboxActive      { get; private set; } = false;
    public static bool   IsComboWindowActive { get; private set; } = false;

    #endregion

    #region References

    [SerializeField] private Animator animator;
    private HitboxManager       hitboxManager;
    private ComboHandler        comboHandler;
    private PlayerAgent         playerAgent;
    private PlayerCombatActions combatActions;
    private AttackTypeHandler   attackTypeHandler;

    private static readonly int SpeedMultiHash = Animator.StringToHash("SpeedMulti");
    [SerializeField] private int attackAnimatorLayerIndex = 0;


    #endregion

    #region Instance State

    private Coroutine activeCoroutine;
    private int  currentStepIndex  = 0;
    private bool _pendingChain     = false;
    private float currentSpeedMulti = 1f;

    public bool IsPlaying => activeCoroutine != null;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        hitboxManager     = GetComponent<HitboxManager>();
        comboHandler      = GetComponent<ComboHandler>();
        playerAgent       = GetComponent<PlayerAgent>();
        combatActions     = GetComponent<PlayerCombatActions>();
        attackTypeHandler = GetComponent<AttackTypeHandler>();

        if (animator          == null) Debug.LogError($"[AAD] {gameObject.name}: Animator not assigned.");
        if (hitboxManager     == null) Debug.LogError($"[AAD] {gameObject.name}: HitboxManager not found.");
        if (comboHandler      == null) Debug.LogError($"[AAD] {gameObject.name}: ComboHandler not found.");
        if (playerAgent       == null) Debug.LogError($"[AAD] {gameObject.name}: PlayerAgent not found.");
        if (combatActions     == null) Debug.LogError($"[AAD] {gameObject.name}: PlayerCombatActions not found.");
        if (attackTypeHandler == null) Debug.LogError($"[AAD] {gameObject.name}: AttackTypeHandler not found.");
    }

    private void OnDisable()
    {
        ResetSpeedMulti();
        ClearStaticState();
    }

    #endregion

    #region Public API

    public void NotifyComboChain() => _pendingChain = true;

    public void BeginAttack(AttackData data, bool chain = false)
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            hitboxManager.ForceDisableHitbox();
            activeCoroutine = null;
            currentStepIndex++;
        }
        else if (_pendingChain)
        {
            currentStepIndex++;
        }
        else
        {
            currentStepIndex = 0;
        }

        _pendingChain = false;
        ResetSpeedMulti();

        if (!string.IsNullOrEmpty(data.AnimationClipName))
        {
            if (chain)
            {
                animator.CrossFade(data.AnimationClipName, data.crossfadeDuration, attackAnimatorLayerIndex);
                print("playAnimation");
            }

            else
            {
                animator.Play(data.AnimationClipName, attackAnimatorLayerIndex, 0f);
                print("playAnimation");
            }
        
        }

        CurrentStepIndex    = currentStepIndex;
        CurrentAttackName   = data.attackName;
        IsAttackActive      = true;
        IsHitboxActive      = false;
        IsComboWindowActive = false;

        OnAttackStarted?.Invoke(currentStepIndex, data.attackName);

        AttackType type = attackTypeHandler != null ? attackTypeHandler.CurrentAttackType : AttackType.Light;
        CombatEvents.RaisePlayerAttacked(type);

        activeCoroutine = StartCoroutine(HitboxTimingCoroutine(data));
    }

    #endregion

    #region Coroutine

    private IEnumerator HitboxTimingCoroutine(AttackData data)
    {
        bool hitboxEnabled    = false;
        bool hitboxWindowDone = false;
        bool comboWindowOpen  = false;
        bool comboWindowDone  = false;
        bool exitFired        = false;
        float elapsed   = 0f;
        float duration  = data.ActiveDuration;
        int   currentPhase = 0;

        float exitPoint = data.EffectiveExitSeconds;

        if (data.HasPhaseOverrides)
            ApplyPhaseSpeed(data, 0);

        yield return null;

        while (true)
        {
            if (playerAgent.CurrentState == AgentState.Staggered ||
                playerAgent.CurrentState == AgentState.Dead       ||
                playerAgent.CurrentState == AgentState.Dodging)
            {
                Interrupt();
                yield break;
            }

            elapsed += Time.deltaTime * currentSpeedMulti;

            // ── Phase transitions ──────────────────────────────────────────────
            if (data.HasPhaseOverrides)
            {
                if (currentPhase == 0 && elapsed >= data.impactTime)
                {
                    currentPhase = 1;
                    ApplyPhaseSpeed(data, 1);
                }
                else if (currentPhase == 1 && elapsed >= data.recoveryTime)
                {
                    currentPhase = 2;
                    ApplyPhaseSpeed(data, 2);
                }
            }

            // ── Hitbox open ────────────────────────────────────────────────────
            if (!hitboxEnabled && !hitboxWindowDone && elapsed >= data.hitboxStartSeconds)
            {
                hitboxManager.EnableHitbox();
                hitboxEnabled  = true;
                IsHitboxActive = true;
            }

            // ── Hitbox close ───────────────────────────────────────────────────
            if (hitboxEnabled && elapsed >= data.hitboxEndSeconds)
            {
                hitboxManager.DisableHitbox();
                hitboxEnabled    = false;
                hitboxWindowDone = true;
                IsHitboxActive   = false;
            }

            // ── Combo window open ──────────────────────────────────────────────
            if (!comboWindowOpen && !comboWindowDone && elapsed >= data.comboWindowStartSeconds)
            {
                comboHandler.OpenComboWindow();
                comboWindowOpen     = true;
                IsComboWindowActive = true;
            }

            // ── Combo window close ─────────────────────────────────────────────
            if (comboWindowOpen && elapsed >= data.comboWindowEndSeconds)
            {
                comboHandler.CloseComboWindow();
                comboWindowOpen     = false;
                comboWindowDone     = true;
                IsComboWindowActive = false;
            }

            // ── Exit window — fire buffered crossfade ──────────────────────────
            if (!exitFired && elapsed >= exitPoint && comboHandler.HasBufferedInput)
            {
                exitFired = true;
                AttackData next = comboHandler.ConsumeBuffer(out AttackType nextType);

                if (next != null)
                {
                    Debug.Log($"[AAD] Exit window reached at {elapsed:F2}s — crossfading to '{next.attackName}'.");

                    // Crossfade NOW, then commit the state
                    if (!string.IsNullOrEmpty(next.AnimationClipName))
                    {
                        animator.CrossFade(next.AnimationClipName, next.crossfadeDuration, attackAnimatorLayerIndex);
                        print("playAnimation");
                    }


                        // Hand off to combatActions — BeginAttack inside will stop this coroutine
                        // and start a new one. Do NOT touch activeCoroutine after this point.
                        combatActions.CommitChainedAttack(next, nextType);
                    ResetSpeedMulti();
                    yield break;
                }
            }

            // ── Clip exit time — crossfade to Idle early ────────────────────
            if (data.clipExitTime > 0f && elapsed >= data.clipExitTime && !exitFired)
            {
                animator.CrossFade("idle", data.crossfadeDuration, attackAnimatorLayerIndex);
                ResetSpeedMulti();
                playerAgent.ChangeState(AgentState.Idle);
                yield return null;
                activeCoroutine = null;
                ClearStaticState();
                OnAttackEnded?.Invoke();
                yield break;
            }

            // ── Attack complete ────────────────────────────────────────────────
            if (elapsed >= duration)
            {
                ResetSpeedMulti();
                playerAgent.ChangeState(AgentState.Idle);
                yield return null;
                activeCoroutine = null;
                ClearStaticState();
                OnAttackEnded?.Invoke();
                yield break;
            }

            yield return null;
        }
    }

    #endregion

    #region Phase Speed

    private void ApplyPhaseSpeed(AttackData data, int phase)
    {
        if (animator == null) return;

        PhaseSpeed preset;
        float naturalLength;

        switch (phase)
        {
            case 0:
                preset        = data.startupSpeed;
                naturalLength = data.impactTime;
                break;
            case 1:
                preset        = data.impactSpeed;
                naturalLength = data.recoveryTime - data.impactTime;
                break;
            case 2:
                preset        = data.recoverySpeed;
                naturalLength = data.ActiveDuration - data.recoveryTime;
                break;
            default:
                ResetSpeedMulti();
                return;
        }

        if (naturalLength <= 0f)
        {
            animator.SetFloat(SpeedMultiHash, 1f);
            return;
        }

        float speedMulti  = 1f / preset.ToMultiplier();
        currentSpeedMulti = speedMulti;
        animator.SetFloat(SpeedMultiHash, speedMulti);
    }

    private void ResetSpeedMulti()
    {
        if (animator != null)
            animator.SetFloat(SpeedMultiHash, 1f);
        currentSpeedMulti = 1f;
    }

    #endregion

    #region Helpers

    public void CancelAttack()
    {
        if (activeCoroutine == null) return;
        StopCoroutine(activeCoroutine);
        Interrupt();
    }

    private void Interrupt()
    {
        ResetSpeedMulti();
        hitboxManager.ForceDisableHitbox();
        comboHandler.CloseComboWindow();
        activeCoroutine = null;
        ClearStaticState();
        OnAttackEnded?.Invoke();
    }

    private static void ClearStaticState()
    {
        CurrentStepIndex    = -1;
        CurrentAttackName   = string.Empty;
        IsAttackActive      = false;
        IsHitboxActive      = false;
        IsComboWindowActive = false;
    }

    #endregion
}

#endregion
