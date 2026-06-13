

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Cinemachine;

public class ScreenShakeController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cam;
    [Header("Light Attacks")]
    [SerializeField] private float lightDuration = 0.08f;
    [SerializeField] private float lightAmplitude= 1.2f;
    [SerializeField] private float lightFrequency= 2f;

    [Header("Heavy Attacks")]
    [SerializeField] private float heavyDuration = 0.12f;
    [SerializeField] private float heavyAmplitude= 2f;
    [SerializeField] private float heavyFrequency= 3f;


    private CinemachineCameraController controller;
    private CinemachineBasicMultiChannelPerlin noise;
    private float shakeTimer;

    private void Awake()
    {
        noise = cam.GetComponent<CinemachineBasicMultiChannelPerlin>();
        controller = cam.GetComponent<CinemachineCameraController>();
    }

    private void OnEnable()
    {
        CombatEvents.OnPlayerHitTarget += HandlePlayerHitTarget;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerHitTarget -= HandlePlayerHitTarget;
    }

    private void Update()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;

            if (shakeTimer <= 0f)
            {
                noise.AmplitudeGain = 0f;
                noise.FrequencyGain = 0f;
            }
        }
    }

    public void Shake(float duration, float amplitude, float frequency)
    {
        if (!controller.IsLockedOn) return;

        shakeTimer = duration;
        noise.AmplitudeGain = amplitude;
        noise.FrequencyGain = frequency;
    }



    private void HandlePlayerHitTarget(AttackType attackType, float damage, GameObject target, Vector3 hitPosition, Vector3 attackDirection) 
    {
        switch (attackType) 
        {
            case AttackType.Light:
                Shake(lightDuration, lightAmplitude,lightFrequency);
                break;
            case AttackType.Heavy:
                Shake(heavyDuration, heavyAmplitude, heavyFrequency);
                break;
        }
    
    }

}




