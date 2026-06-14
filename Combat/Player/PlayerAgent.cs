using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

#region Player Agent

/// <summary>
/// Concrete Agent subclass for the player character.
/// Extends Agent with stamina, and implements IAttacker and IBlockable.
/// Lives on the Player GameObject alongside PlayerCombatActions and PlayerController.
/// PlayerCombatActions handles the execution of attacks and blocks —
/// this class owns the stats and state only.
///
/// Block and Parry are the same button. ParryWindowHandler resolves which one
/// applies at the moment of impact via AgentState (Parrying vs Blocking).
/// </summary>
public class PlayerAgent : Agent, IAttacker, IBlockable
{
    public static bool canTakeDamage = true;
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    [SerializeField] private PizzaHealthUI healthUI;
    [SerializeField] private DeathScreen deathScreen;

    public static PlayerAgent pAgent;

    [SerializeField] private List<Transform> striffingPoints;
    public List<Transform> StriffingPoints => striffingPoints;

    #region Stamina

    [SerializeField] private float maxStamina;
    private float currentStamina;

    #endregion

    #region IBlockable State

    /// <summary>True while the player is in the Blocking state.</summary>
    public bool IsBlocking => currentState == AgentState.Blocking;

    /// <summary>True while the player is in the Parrying window state.</summary>
    public bool IsParrying => currentState == AgentState.Parrying;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        pAgent = this;
        currentStamina = maxStamina;
        canTakeDamage = true; 
    }

    #endregion

    #region IAttacker Implementation

    public void PerformAttack(AttackType type)
    {
        GetComponent<PlayerCombatActions>().PerformAttack(type);
    }

    #endregion

    #region IBlockable Implementation

    /// <summary>Parry button pressed. Delegates to PlayerCombatActions.</summary>
    public void OnParryPress()
    {
        GetComponent<PlayerCombatActions>().OnBlockPressed();
    }

    /// <summary>Parry button released. Delegates to PlayerCombatActions.</summary>
    public void OnParryRelease()
    {
        GetComponent<PlayerCombatActions>().OnBlockReleased();
    }

    #endregion

    #region Stamina Regeneration

    [Header("Stamina Regeneration")]
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float staminaRegenDelay = 1.5f;

    private float staminaRegenTimer;

    private void Update()
    {
        if (currentState == AgentState.Dead) return;

        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
        }
        else if (currentState == AgentState.Idle || currentState == AgentState.Moving)
        {
            RestoreStamina(staminaRegenRate * Time.deltaTime);
        }
    }

    public void NotifyStaminaConsumed()
    {
        staminaRegenTimer = staminaRegenDelay;
    }

    #endregion

    #region Stamina Utility

    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina()     => maxStamina;

    public void ConsumeStamina(float amount)
    {
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        NotifyStaminaConsumed();
    }

    public void RestoreStamina(float amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }

    #endregion

    #region Damage

    public void TakeDamage(int amount)
    {
        if (!canTakeDamage) return;

        CombatEvents.RaisePlayerHit(amount);
        DungeonManager.Instance?.Stats?.NotifyDamageTaken(amount);
        currentHp -= amount;
        Debug.Log($"{currentHp}");

        if (currentHp <= 0f)
        {
            Debug.Log("Player has died");
            Die();
        }

        healthUI.UpdateHealth(Convert.ToInt32(currentHp));
        DamageToggle(invulnerabilityDuration);
    }

    private Coroutine invulCoroutine;

    IEnumerator DamageInvul(float duration)
    {
        canTakeDamage = false;
        yield return new WaitForSeconds(duration);
        canTakeDamage = true;
        invulCoroutine = null;
    }

    public void DamageToggle(float duration)
    {
        if (invulCoroutine != null)
            StopCoroutine(invulCoroutine);
        invulCoroutine = StartCoroutine(DamageInvul(duration));
    }

    #endregion

    #region Death Override

    protected override void Die()
    {
        deathScreen.ShowDeathScreen();
        base.Die();
        Debug.Log("Player has died. Game over.");
    }

    #endregion
}

#endregion
