using UnityEngine;
using System.Collections.Generic;

#region Hurtbox Handler

/// <summary>
/// Lives on the hurtbox child GameObject of every character.
/// Detects when an incoming weapon hitbox overlaps this character's hurtbox
/// and forwards the event to CombatManager to resolve the outcome.
/// Supports both Agent (player) and EnemyAI receivers.
/// This script does not decide what happens – it only detects and reports.
/// All hit resolution logic lives in CombatManager.
/// </summary>
public class HurtboxHandler : MonoBehaviour
{
    #region References

    private MonoBehaviour owner;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        owner = GetComponentInParent<Agent>();

        if (owner == null)
            owner = GetComponentInParent<EnemyAI>();

        if (owner == null)
            Debug.LogError($"HurtboxHandler on {gameObject.name} could not find an Agent or EnemyAI in parent. Hit detection will not function.");
    }

    #endregion

    #region Hit Detection

    private void OnTriggerEnter(Collider other)
    {
        WeaponBase incomingWeapon = other.GetComponentInParent<WeaponBase>();
        if (incomingWeapon == null) return;

        MonoBehaviour attacker = incomingWeapon.GetComponentInParent<Agent>();
        if (attacker == null)
            attacker = incomingWeapon.GetComponentInParent<EnemyAI>();

        if (attacker == null)
        {
            Debug.LogWarning($"HurtboxHandler on {gameObject.name}: incoming weapon has no attacker component in parent. Hit ignored.");
            return;
        }

        if (attacker == owner) return;

        HitboxManager attackerHitbox = incomingWeapon.GetComponentInParent<HitboxManager>();
        if (attackerHitbox == null) return;

        bool registered = attackerHitbox.TryRegisterHit(owner.gameObject);
        if (!registered) return;

        Vector3 hitPosition = transform.position;
        Vector3 attackDirection = (hitPosition - incomingWeapon.transform.position).normalized;

        CombatManager.Instance.ResolveHit(attacker.gameObject, owner.gameObject, hitPosition, attackDirection);
        Debug.LogError("HIT");
    }

    #endregion
}

#endregion
