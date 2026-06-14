using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives on the player's weapon VFX manager object.
/// Handles all weapon-related VFX: swing trail, parry spark, hit impact effects.
/// Hit effects are driven by direct prefab references — no ScriptableObject dependency.
/// Direction is received from CombatEvents and used to orient the effect on spawn.
/// </summary>
public class WeaponVFX : MonoBehaviour
{
    #region Serialized Fields

    [Header("Parry VFX")]
    [SerializeField] private ParticleSystem parryParticlesPrefab;

    [Header("Hit Effect Prefabs")]
    [Tooltip("Prefab spawned on light hits.")]
    [SerializeField] private GameObject lightHitPrefab;
    [Tooltip("Prefab spawned on heavy hits.")]
    [SerializeField] private GameObject heavyHitPrefab;

    [Header("Hit Particle Pool")]
    public int maxLightParticles = 10;
    public int maxHeavyParticles = 5;

    [Header("Swing Trail")]
    public GameObject trailRendererObj;

    [Header("Timing")]
    public float defaultAttackDuration      = 0.5f;
    public float defaultLightAttackDuration = 0.4f;
    public float defaultHeavyAttackDuration = 0.7f;
    public float defaultWindupDuration      = 0.5f;
    public float defaultLightWindupDuration = 0.5f;
    public float defaultHeavyWindupDuration = 1f;

    #endregion

    #region Private

    private Coroutine _trailCoroutine;
    private readonly Queue<GameObject> _lightHitPool = new Queue<GameObject>();
    private readonly Queue<GameObject> _heavyHitPool = new Queue<GameObject>();


    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        CombatEvents.OnPlayerAttacked  += HandlePlayerAttacked;
        CombatEvents.OnParrySuccess    += HandleParrySuccess;
        CombatEvents.OnPlayerHitTarget += HandlePlayerHitTarget;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerAttacked  -= HandlePlayerAttacked;
        CombatEvents.OnParrySuccess    -= HandleParrySuccess;
        CombatEvents.OnPlayerHitTarget -= HandlePlayerHitTarget;
    }

    #endregion

    #region Event Handlers

    private void HandlePlayerAttacked(AttackType type)
    {
        if (trailRendererObj != null)
            trailRendererObj.SetActive(false);

        float attackTime = type == AttackType.Light ? defaultLightAttackDuration : defaultHeavyAttackDuration;
        float windUpTime = type == AttackType.Light ? defaultLightWindupDuration  : defaultHeavyWindupDuration;

        if (_trailCoroutine != null) StopCoroutine(_trailCoroutine);
        _trailCoroutine = StartCoroutine(TrailRoutine(attackTime, windUpTime));
    }

    private void HandleParrySuccess(Vector3 hitPosition, GameObject enemy)
    {
        if (parryParticlesPrefab == null)
        {
            Debug.LogWarning("[WeaponVFX] No parry particles prefab assigned.");
            return;
        }
        ParticleSystem instance = Instantiate(parryParticlesPrefab, hitPosition, Quaternion.identity);
        instance.Play();
        Destroy(instance.gameObject, instance.main.duration + instance.main.startLifetime.constantMax);
    }

    private void HandlePlayerHitTarget(AttackType type, float damage, GameObject target, Vector3 hitPosition, Vector3 attackDirection)
    {
        GameObject prefab = type == AttackType.Heavy ? heavyHitPrefab : lightHitPrefab;
        
        if (prefab == null)
        {
            Debug.LogWarning($"[WeaponVFX] No prefab assigned for {type} hit.");
            return;
        }

        // Orient root to attack direction
        Quaternion rootRot = attackDirection != Vector3.zero
            ? Quaternion.LookRotation(attackDirection)
            : Quaternion.identity;

        GameObject instance = Instantiate(prefab, hitPosition, rootRot, target != null ? target.transform : null);
        Queue<GameObject> pool = type == AttackType.Heavy ? _heavyHitPool : _lightHitPool;
        int maxPool = type == AttackType.Heavy ? maxHeavyParticles : maxLightParticles;

        pool.Enqueue(instance);
        if (pool.Count > maxPool)
        {
            GameObject oldest = pool.Dequeue();
            if (oldest != null) Destroy(oldest);
        }
        Debug.Log($"[VFX] Light pool: {_lightHitPool.Count} | Heavy pool: {_heavyHitPool.Count}"); // <== REMOVE LATER
        // Orient debris child perpendicular to swing
        OrientDebris(instance, attackDirection);

        // Play all child particle systems
        float maxLifetime = 0f;
        foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Play();
            float psLife = ps.main.duration + ps.main.startLifetime.constantMax;
            if (psLife > maxLifetime) maxLifetime = psLife;
        }

        Destroy(instance, maxLifetime + 0.05f);
    }

    #endregion

    #region Orientation

    /// <summary>
    /// Rotates the Debris child PS so its cone sprays perpendicular to the attack direction.
    /// Blood flies across the swing plane rather than along it.
    /// </summary>
    private void OrientDebris(GameObject instance, Vector3 attackDirection)
    {
        Transform debrisTransform = null;
        foreach (Transform child in instance.transform)
        {
            if (child.name == "Debris")
            {
                debrisTransform = child;
                break;
            }
        }

        if (debrisTransform == null) return;

        Vector3 sprayDir = Camera.main.transform.right;
        debrisTransform.rotation = Quaternion.LookRotation(sprayDir);
    }

    #endregion

    #region Coroutines

    private IEnumerator TrailRoutine(float attackTime, float windUpTime)
    {
        yield return new WaitForSeconds(windUpTime);
        if (trailRendererObj != null) trailRendererObj.SetActive(true);
        yield return new WaitForSeconds(attackTime);
        if (trailRendererObj != null) trailRendererObj.SetActive(false);
        _trailCoroutine = null;
    }

    #endregion
}
