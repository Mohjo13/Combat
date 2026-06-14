using System;
using System.Collections;
using UnityEngine;

#region Combat Manager

/// <summary>
/// Scene-level singleton. The central authority for all hit resolution.
/// </summary>
public class CombatManager : MonoBehaviour
{
    #region Singleton

    public static CombatManager Instance { get; private set; }

    [SerializeField] private PlayerAgent playerAgent1;

    [Header("Extra Hits")]
    [SerializeField] private PlayerUpgradeHandler playerUpgradeHandler;
    [SerializeField] private float extraHitDelay = 0.12f;
    public Agent playerAgent { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CombatManager: duplicate instance found and destroyed.");
            Destroy(gameObject);
            return;
        }

        playerAgent = playerAgent1;
        Instance = this;

        if (playerUpgradeHandler == null && playerAgent1 != null)
            playerUpgradeHandler = playerAgent1.GetComponent<PlayerUpgradeHandler>();

    }

    #endregion



    public void ResolveHit(GameObject attackerObj, GameObject receiverObj, Vector3 hitPosition, Vector3 attackDirection)
    {

        if (attackerObj == null || receiverObj == null) return;

        Agent receiverAgent = receiverObj.GetComponent<Agent>();
        EnemyAI receiverEnemy = receiverObj.GetComponent<EnemyAI>();

        AgentState? receiverState = null;
        if (receiverAgent != null) receiverState = receiverAgent.CurrentState;
        else if (receiverEnemy != null) receiverState = receiverEnemy.CurrentState;


        if (receiverState == AgentState.Dead) return;

        if (receiverAgent != null && receiverAgent == playerAgent)
        {
            switch (receiverState)
            {
                case AgentState.Dodging: return;
                case AgentState.Parrying:
                    ResolveParry(attackerObj, receiverObj);
                    return;
            }
        }

        ResolveDirectHit(attackerObj, receiverObj, hitPosition, attackDirection);
    }

    #endregion

    #region Outcome Handlers

    /// <summary>
    /// Parry succeeds. Attacker enters AgentState.Parried directly.
    /// EnemyAI.ChangeState branches into ParriedRecoveryRoutine and fires CombatEvents.
    /// </summary>
    public void ResolveParry(GameObject attackerObj, GameObject receiverObj)
    {
        EnemyAI attackerEnemy = attackerObj.GetComponent<EnemyAI>();
        if (attackerEnemy != null)
        {
            attackerEnemy.ChangeState(AgentState.Parried);
        }
        else
        {
            Agent attackerAgent = attackerObj.GetComponent<Agent>();
            if (attackerAgent != null)
                attackerAgent.ChangeState(AgentState.Staggered);
        }

        HitboxManager attackerHitbox = attackerObj.GetComponentInChildren<HitboxManager>();
        if (attackerHitbox != null)
            attackerHitbox.ForceDisableHitbox();


    }

    /// <summary>
    /// Direct hit. Applies ParriedState.DamageTakenMultiplier if enemy is currently Parried.
    /// </summary>
    private void ResolveDirectHit(GameObject attackerObj, GameObject receiverObj, Vector3 hitPosition, Vector3 attackDirection)
    {

        DamageInfo dmgInfo = BuildDamageInfo(attackerObj);

        PlayerAgent receiverPlayer = receiverObj.GetComponent<PlayerAgent>();
        if (receiverPlayer != null)
        {
            receiverPlayer.TakeDamage(Mathf.FloorToInt(dmgInfo.amount));
            return;
        }

        EnemyAI receiverEnemy = receiverObj.GetComponent<EnemyAI>();
        if (receiverEnemy != null)
        {
            float finalDamage = dmgInfo.amount;

            if (receiverEnemy.IsParried)
                finalDamage *= receiverEnemy.ParriedDamageMult;

            float knockbackMultiplier = 1f;
            bool isGapCloser = false;

            AttackTypeHandler atkHandler = attackerObj.GetComponentInChildren<AttackTypeHandler>();
            if (atkHandler != null && atkHandler.CurrentAttackData != null && atkHandler.CurrentAttackData.isGapCloser)
            {
                knockbackMultiplier = 0f; // gap closer staggers in place, no knockback
                isGapCloser = true;
            }

            ApplyHitToEnemy(receiverEnemy, dmgInfo.type, finalDamage, hitPosition, attackDirection, true, knockbackMultiplier);

            if (isGapCloser)
                CombatEvents.RaiseGapCloserHit(receiverEnemy.gameObject, hitPosition);

            if (IsPlayerAttacker(attackerObj))
            {
                ApplyPlayerOnHitEffects(receiverEnemy);
                ApplyExtraHits(receiverEnemy, dmgInfo.type, finalDamage, hitPosition, attackDirection);
            }
        }
    }

    private void ApplyHitToEnemy(EnemyAI enemy, AttackType attackType, float damage, Vector3 hitPosition, Vector3 attackDirection, bool raiseCombatEvent, float knockbackMultiplier = 1f)
    {
        if (enemy == null || enemy.CurrentState == AgentState.Dead) return;

        if (raiseCombatEvent)
            CombatEvents.RaisePlayerHitTarget(attackType, damage, enemy.gameObject, hitPosition, attackDirection);

        enemy.TakeDamage(damage, hitPosition, attackType, knockbackMultiplier);
    }

    private void ApplyExtraHits(EnemyAI enemy, AttackType attackType, float baseDamage, Vector3 hitPosition, Vector3 attackDirection)
    {
        if (playerUpgradeHandler == null) return;
        if (enemy == null || enemy.CurrentState == AgentState.Dead) return;

        int extraHits = playerUpgradeHandler.ExtraHitsOnHit;
        if (extraHits <= 0) return;

        float extraDamage = 1f;

        StartCoroutine(ApplyExtraHitsRoutine(enemy, attackType, extraDamage, hitPosition, attackDirection, extraHits));
    }
    private IEnumerator ApplyExtraHitsRoutine(EnemyAI enemy, AttackType attackType, float extraDamage, Vector3 hitPosition, Vector3 attackDirection, int extraHits)
    {
        for (int i = 0; i < extraHits; i++)
        {
            yield return new WaitForSeconds(extraHitDelay);

            if (enemy == null || enemy.CurrentState == AgentState.Dead)
                yield break;

            ApplyHitToEnemy(enemy, attackType, extraDamage, hitPosition, attackDirection, true);
        }
    }
    private void ApplyPlayerOnHitEffects(EnemyAI enemy)
    {
        if (playerUpgradeHandler == null)
            return;

        if (enemy == null || enemy.CurrentState == AgentState.Dead)
            return;

        if (!playerUpgradeHandler.HasBurnOnHit())
            return;

        EnemyStatusEffectHandler statusHandler = enemy.GetComponent<EnemyStatusEffectHandler>();

        if (statusHandler == null)
        {
            Debug.LogWarning($"{enemy.name} is missing EnemyStatusEffectHandler, so burn could not be applied.");
            return;
        }

        foreach (BurnOnHitEffectData burnEffect in playerUpgradeHandler.BurnOnHitEffects)
        {
            statusHandler.ApplyBurn(
                burnEffect,
                playerUpgradeHandler.BurnDamageMultiplier,
                playerUpgradeHandler.BurnSlowPercent
            );
        }
    }

    private bool IsPlayerAttacker(GameObject attackerObj)
    {
        if (attackerObj == null) return false;

        PlayerAgent player = attackerObj.GetComponentInParent<PlayerAgent>();
        return player != null && player == playerAgent1;
    }

    #endregion

    #region Damage Info Builder

    private DamageInfo BuildDamageInfo(GameObject attackerObj)
    {
        Agent attackerAgent = attackerObj.GetComponent<Agent>();

        if (attackerAgent != null)
        {
            WeaponBase weapon = attackerAgent.GetCurrentWeapon();
            AttackTypeHandler attackTypeHandler = attackerObj.GetComponentInChildren<AttackTypeHandler>();
            AttackType type = attackTypeHandler != null ? attackTypeHandler.CurrentAttackType : AttackType.Light;
            float damage = weapon != null ? weapon.GetDamage(type) : 0f;
            return new DamageInfo(damage, type, attackerAgent, weapon);
        }

        EnemyWeapon enemyWeapon = attackerObj.GetComponent<EnemyWeapon>();
        Debug.Log(enemyWeapon);
        if (enemyWeapon == null)
            enemyWeapon = attackerObj.GetComponentInChildren<EnemyWeapon>();

        float enemyDamage = enemyWeapon != null ? enemyWeapon.Damage : 1f;
        AttackTypeHandler enemyATHandler = attackerObj.GetComponentInChildren<AttackTypeHandler>();
        AttackType enemyType = enemyATHandler != null ? enemyATHandler.CurrentAttackType : AttackType.Light;
        return new DamageInfo(enemyDamage, enemyType, null, null);
    }
}

#endregion