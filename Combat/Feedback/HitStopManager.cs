using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    private bool isHitStopping;
    private float hitStopTimer;
    [SerializeField] private float hitStopLight = 0.04f;
    [SerializeField] private float hitStopHeavy = 0.06f;
    public static HitStopManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!isHitStopping) return;

        hitStopTimer -= Time.unscaledDeltaTime;

        if(hitStopTimer <= 0f)
        {
            Time.timeScale = 1f;
            isHitStopping = false;
        }
    }

    private void OnEnable()
    {
        CombatEvents.OnPlayerHitTarget += HandlePlayerHitTarget;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerHitTarget -= HandlePlayerHitTarget;
    }

   
    private void HandlePlayerHitTarget(AttackType type, float damage, GameObject target, Vector3 hitPosition, Vector3 attackDirection)
    {
        switch (type)
        {
            case AttackType.Light:
                Stop(hitStopLight);
                break;

            case AttackType.Heavy:
                Stop(hitStopHeavy);
                break;

            default:
                Stop(0.05f);
                break;
        }
    }

    public void Stop(float duration)
    {
        Time.timeScale = 0f;
        hitStopTimer = duration;
        isHitStopping = true;
        Debug.Log("hitstop");
   
    }
}
