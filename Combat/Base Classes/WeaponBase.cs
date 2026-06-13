using UnityEngine;

/// <summary>
/// Abstract base class for all weapons in the game.
/// Sword, Axe, and Spear extend this. Never placed directly in a scene.
/// Held by Agent as a reference and swappable at runtime.
/// Implements IAttacker — each subtype overrides PerformAttack() with its own behaviour.
/// WeaponData is now accessed through the WeaponLoadout reference rather than directly.
/// All existing damage reads in GetDamage() and CombatManager continue to work unchanged.
///
/// Gizmos have been removed from this class.
/// All combat debug visuals (hitbox box, combo sphere, step color cube) are drawn by
/// CombatGizmoDrawer in AttackDataEditor.cs. Do not add gizmo code here.
/// </summary>
public abstract class WeaponBase : MonoBehaviour, IAttacker
{
    #region Loadout

    [SerializeField] protected WeaponLoadout loadout;

    public WeaponLoadout Loadout => loadout;

    public WeaponData Data => loadout != null ? loadout.weaponData : null;

    #endregion

    #region Hitbox

    [SerializeField] protected Collider hitboxCollider;

    public Collider HitboxCollider => hitboxCollider;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }

    #endregion

    #region IAttacker

    public abstract void PerformAttack(AttackType type);

    #endregion

    #region Damage Calculation

    public virtual float GetDamage(AttackType type)
    {
        if (Data == null)
        {
            Debug.LogWarning($"WeaponBase on {gameObject.name}: no WeaponData found on loadout. Damage returns 0.");
            return 0f;
        }

        float multiplier = type == AttackType.Light
            ? Data.lightMultiplier
            : Data.heavyMultiplier;

        return Data.baseDamage * multiplier;
    }

    #endregion
}