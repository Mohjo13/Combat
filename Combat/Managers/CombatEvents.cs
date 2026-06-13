using System;
using UnityEngine;

/// <summary>
/// Centralized event hub for the combat system.
/// Subscribe here to react to combat without touching combat scripts.
/// Always unsubscribe in OnDisable/OnDestroy.
/// </summary>
public static class CombatEvents
{
    #region Attack Events

    public static event Action<AttackType> OnPlayerAttacked;
    public static event Action<float> OnPlayerHit;
    public static event Action<AttackType, float, GameObject, Vector3, Vector3> OnPlayerHitTarget;
    public static event Action<AttackData, bool> OnAttackStartedWithIcon;

    public static void RaiseAttackStartedWithIcon(AttackData data, bool isChained = false) => OnAttackStartedWithIcon?.Invoke(data, isChained);
    public static void RaisePlayerAttacked(AttackType type)                => OnPlayerAttacked?.Invoke(type);
    public static void RaisePlayerHit(float damage)                        => OnPlayerHit?.Invoke(damage);
    public static void RaisePlayerHitTarget(AttackType type, float damage, GameObject target, Vector3 hitPosition, Vector3 attackDirection) => OnPlayerHitTarget?.Invoke(type, damage, target, hitPosition, attackDirection);

    #endregion

    #region Knockback Events

    public static event Action<Transform, float, float> OnEnemyKnockedBack;
    public static void RaiseEnemyKnockedBack(Transform enemy, float duration, float force)
        => OnEnemyKnockedBack?.Invoke(enemy, duration, force);

    #endregion

    #region Gap Closer Events

    /// <summary>
    /// A gap-closer attack successfully hit an enemy.
    /// GameObject = enemy root. Vector3 = hit position.
    /// </summary>
    public static event Action<GameObject, Vector3> OnGapCloserHit;

    public static void RaiseGapCloserHit(GameObject enemy, Vector3 hitPosition) => OnGapCloserHit?.Invoke(enemy, hitPosition);

    public static event Action OnPlayerLungeStarted;
    public static void RaisePlayerLungeStarted() => OnPlayerLungeStarted?.Invoke();

    #endregion

    #region Parry Events

    /// <summary>Player's parry window opened.</summary>
    public static event Action OnParryWindowOpen;

    /// <summary>Parry window closed without a hit.</summary>
    public static event Action OnParryWindowClose;

    /// <summary>
    /// Player successfully parried.
    /// Vector3 = world hit position. GameObject = enemy root that was parried.
    /// </summary>
    public static event Action<Vector3, GameObject> OnParrySuccess;

    /// <summary>Enemy entered Parried state. GameObject = enemy root.</summary>
    public static event Action<GameObject> OnEnemyParriedBegin;

    /// <summary>Enemy's Parried state ended. GameObject = enemy root.</summary>
    public static event Action<GameObject> OnEnemyParriedEnd;

    public static void RaiseParryWindowOpen()  => OnParryWindowOpen?.Invoke();
    public static void RaiseParryWindowClose() => OnParryWindowClose?.Invoke();
    public static void RaiseParrySuccess(Vector3 hitPosition, GameObject enemy) => OnParrySuccess?.Invoke(hitPosition, enemy);
    public static void RaiseEnemyParriedBegin(GameObject enemy) => OnEnemyParriedBegin?.Invoke(enemy);
    public static void RaiseEnemyParriedEnd(GameObject enemy)   => OnEnemyParriedEnd?.Invoke(enemy);

    #endregion

    #region Dodge Attack Events

    public static event Action<AttackData, AttackType> OnDodgeAttackBuffered;
    public static void RaiseDodgeAttackBuffered(AttackData data, AttackType type) => OnDodgeAttackBuffered?.Invoke(data, type);

    #endregion


    #region Combo UI Events

    public static event Action<string> OnComboCompleted;
    public static event Action OnComboFailed;

    /// <summary>A streak icon finished its fly-in animation and landed in its slot.</summary>
    public static event Action OnStreakIconLanded;

    /// <summary>A streak row just received its 3rd icon — row is now full.</summary>
    public static event Action OnStreakRowFull;

    /// <summary>A completed streak row finished its float-away animation and was destroyed.</summary>
    public static event Action OnStreakRowFloatedAway;

    public static void RaiseComboCompleted(string comboName) => OnComboCompleted?.Invoke(comboName);
    public static void RaiseComboFailed() => OnComboFailed?.Invoke();
    public static void RaiseStreakIconLanded() => OnStreakIconLanded?.Invoke();
    public static void RaiseStreakRowFull() => OnStreakRowFull?.Invoke();
    public static void RaiseStreakRowFloatedAway() => OnStreakRowFloatedAway?.Invoke();

    #endregion

    #region Enemy Death Events

    /// <summary>An enemy has been killed. GameObject = enemy root.</summary>
    public static event Action<GameObject> OnEnemyKilled;

    public static void RaiseEnemyKilled(GameObject enemy) => OnEnemyKilled?.Invoke(enemy);

    #endregion
}
