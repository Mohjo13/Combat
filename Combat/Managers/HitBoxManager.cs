using UnityEngine;
using System.Collections.Generic;

#region Hitbox Manager

/// <summary>
/// Lives on each character GameObject. Owns the weapon's hitbox collider and
/// is responsible for enabling and disabling it at the correct animation frames.
/// Called exclusively by Animation Events — never enabled manually from code.
/// The hitbox is never always-on. It is only active during the active frames of a swing.
/// </summary>
public class HitboxManager : MonoBehaviour
{
    #region References

    private Agent owner;
    private WeaponBase currentWeapon;

    /// <summary>
    /// Tracks which GameObjects have already been hit during the current swing.
    /// Cleared at the start of each new swing in EnableHitbox().
    /// Prevents the same target being hit twice from collider flicker or physics double-ticks.
    /// </summary>
    private readonly HashSet<GameObject> _hitTargetsThisSwing = new HashSet<GameObject>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        owner = GetComponentInParent<Agent>();

        if (owner == null)
            Debug.LogError($"HitboxManager on {gameObject.name} could not find an Agent in parent. Hitbox will not function.");
    }

    #endregion

    #region Animation Event Callbacks

    public void EnableHitbox()
    {
        currentWeapon = owner.GetCurrentWeapon();

        if (currentWeapon == null)
        {
            Debug.LogWarning($"HitboxManager on {gameObject.name}: owner has no weapon equipped. Cannot enable hitbox.");
            return;
        }

        // Clear hit targets so each swing starts fresh
        _hitTargetsThisSwing.Clear();

        currentWeapon.HitboxCollider.enabled = true;
        Debug.Log($"{owner.gameObject.name}: hitbox enabled.");
    }

    public void DisableHitbox()
    {
        if (currentWeapon == null) return;

        currentWeapon.HitboxCollider.enabled = false;
        Debug.Log($"{owner.gameObject.name}: hitbox disabled.");
    }

    #endregion

    #region Safety

    public void ForceDisableHitbox()
    {
        if (currentWeapon == null) return;

        currentWeapon.HitboxCollider.enabled = false;
        Debug.Log($"{owner.gameObject.name}: hitbox force-disabled.");
    }

    #endregion

    #region Hit Registration

    /// <summary>
    /// Registers a hit against a target for this swing.
    /// Returns true if this is the first hit against that target — caller should proceed.
    /// Returns false if the target was already hit this swing — caller should abort.
    /// </summary>
    public bool TryRegisterHit(GameObject target)
    {
        return _hitTargetsThisSwing.Add(target);
    }

    #endregion
}

#endregion