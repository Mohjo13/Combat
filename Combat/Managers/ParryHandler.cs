using System.Collections;
using UnityEngine;

/// <summary>
/// Mono — on the Player root.
/// Merged ParryWindowHandler + ParryBoxHandler.
/// Owns the parry window timing, cooldown, and parrybox collider activation.
/// On hit: fires CombatEvents.RaiseParrySuccess only — EnemyAI and WeaponVFX subscribe from there.
///
/// Setup:
///   1. Attach to Player root.
///   2. Assign parryData (ParryDataSO), parryCollider (child trigger collider), animController.
///   3. PlayerCombatActions calls OpenWindow().
/// </summary>
public class ParryHandler : MonoBehaviour
{
    #region Inspector

    [SerializeField] private ParryDataSO parryData;
    [SerializeField] private Collider parryCollider;
    [SerializeField] private PlayerAnimationController animController;

    #endregion

    #region State

    public bool IsWindowOpen { get; private set; }
    public bool IsOnCooldown { get; private set; }
    public ParryDataSO ParryData => parryData;

    private Coroutine _windowRoutine;
    private Coroutine _cooldownRoutine;
    private PlayerAgent _playerAgent;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _playerAgent = GetComponent<PlayerAgent>();
        if (animController == null)
            animController = GetComponent<PlayerAnimationController>();

        if (_playerAgent == null)   Debug.LogError($"[ParryHandler] {gameObject.name}: PlayerAgent not found.");
        if (parryData == null)      Debug.LogError($"[ParryHandler] {gameObject.name}: ParryDataSO not assigned.");
        if (parryCollider == null)  Debug.LogError($"[ParryHandler] {gameObject.name}: parryCollider not assigned.");

        if (parryCollider != null) parryCollider.enabled = false;
    }

    #endregion

    #region Public API

    public void OpenWindow()
    {
        if (IsOnCooldown) return;

        if (_windowRoutine != null) StopCoroutine(_windowRoutine);
        _windowRoutine = StartCoroutine(WindowRoutine());
    }

    #endregion

    #region Window Routine

    private IEnumerator WindowRoutine()
    {
        IsWindowOpen = true;
        CombatEvents.RaiseParryWindowOpen();

        if (parryData.parryClip != null)
            animController?.PlayAnimation(AgentState.Parrying ,parryData.parryClip.name);

        if (parryData.parryBoxStartTime > 0f)
            yield return new WaitForSeconds(parryData.parryBoxStartTime);

        SetColliderActive(true);

        float boxDuration = parryData.parryBoxEndTime - parryData.parryBoxStartTime;
        if (boxDuration > 0f)
        {
            yield return new WaitForSeconds(boxDuration);
            SetColliderActive(false);
        }

        float remaining = parryData.windowDuration - parryData.parryBoxEndTime;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        CloseWindow();
    }

    private void CloseWindow()
    {
        if (_windowRoutine != null)
        {
            StopCoroutine(_windowRoutine);
            _windowRoutine = null;
        }
        IsWindowOpen = false;
        SetColliderActive(false);
        CombatEvents.RaiseParryWindowClose();
        _playerAgent.ChangeState(AgentState.Idle);
        StartCooldown();
    }

    private void SetColliderActive(bool active)
    {
        if (parryCollider != null) parryCollider.enabled = active;
    }

    #endregion

    #region Hit Detection

    private void OnTriggerEnter(Collider other)
    {
        if (parryCollider == null || !parryCollider.enabled) return;

        EnemyWeapon incomingWeapon = other.GetComponentInParent<EnemyWeapon>();
        if (incomingWeapon == null) return;

        EnemyAI attackerEnemy = incomingWeapon.GetComponentInParent<EnemyAI>();
        if (attackerEnemy == null) return;

        HitboxManager enemyHitbox = attackerEnemy.GetComponentInChildren<HitboxManager>();
        enemyHitbox?.ForceDisableHitbox();

        Vector3 hitPosition = other.ClosestPoint(transform.position);
        CombatEvents.RaiseParrySuccess(hitPosition, attackerEnemy.gameObject);

        CloseWindow();
    }

    #endregion

    #region Cooldown

    private void StartCooldown()
    {
        if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
        _cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        IsOnCooldown = true;
        yield return new WaitForSeconds(parryData.cooldown);
        IsOnCooldown = false;
        _cooldownRoutine = null;
    }

    #endregion
}
