using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

public class StreakRowUI : MonoBehaviour
{
    #region Settings

    [SerializeField] private float completeScaleDuration = 0.2f;
    [SerializeField] private float completedScale = 0.6f;
    [SerializeField] private float completedMoveUp = 80f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] public float iconTimeout = 2f;
    [SerializeField] public float comboTimeout = 4f;
    public int IconCount => icons.Count;
    public bool IsFull => icons.Count >= 3;
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 8f;
    [SerializeField] private int shakeVibrato = 10;
    [SerializeField] public Image rowImage;

    [Header("Fail Drain")]
    [SerializeField] private Color failColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private float failDrainDuration = 0.15f;


    #endregion

    #region State

    private List<StreakIconUI> icons = new List<StreakIconUI>();
    private bool completed = false;
    private bool locked = false;
    private Coroutine idleCoroutine;
    private Coroutine comboCoroutine;

    public bool IsLocked => locked;

    #endregion

    #region Public API

    public void AddIcon(StreakIconUI icon)
    {
        icons.Add(icon);
        if (!completed)
            RestartIdleTimer();
    }

    public void OnComboComplete(string comboName)
    {
        if (completed) return;
        completed = true;
        StopIdleTimer();
        if (comboCoroutine != null) StopCoroutine(comboCoroutine);
        comboCoroutine = StartCoroutine(ComboTimeoutCoroutine());

    }



    public void FloatAway()
    {
        StopIdleTimer();
        if (comboCoroutine != null)
        {
            StopCoroutine(comboCoroutine);
            comboCoroutine = null;
        }
        DOTween.Sequence()
            .Append(transform.DOScale(completedScale, completeScaleDuration).SetEase(Ease.OutQuad))
            .Join(transform.DOLocalMoveY(transform.localPosition.y + completedMoveUp, completeScaleDuration))
            .Append(transform.DOScale(0f, fadeDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                CombatEvents.RaiseStreakRowFloatedAway();
                Destroy(gameObject);
            });
    }

    public void HideBackground() => rowImage.gameObject.SetActive(false);

    public void Shake()
    {
        transform.DOShakePosition(shakeDuration, strength: new Vector3(shakeStrength, 0f, 0f), vibrato: shakeVibrato, randomness: 0);
    }

    public void OnComboFail()
    {
        locked = true;
        StopIdleTimer();
        if (comboCoroutine != null)
        {
            StopCoroutine(comboCoroutine);
            comboCoroutine = null;
        }

        HideBackground();

        foreach (var icon in icons)
            icon.TintTo(failColor, failDrainDuration);

        DOTween.Sequence()
            .AppendInterval(failDrainDuration)
            .Append(transform.DOShakePosition(shakeDuration, strength: new Vector3(shakeStrength, 0f, 0f), vibrato: shakeVibrato, randomness: 0))
            .OnComplete(() =>
            {
                foreach (var icon in icons)
                    icon.FallOff();
                Destroy(gameObject, 0.5f);
            });
    }

    #endregion

    #region Idle Timer

    private void RestartIdleTimer()
    {
        if (idleCoroutine != null)
            StopCoroutine(idleCoroutine);
        idleCoroutine = StartCoroutine(IdleTimeoutCoroutine());
    }

    private void StopIdleTimer()
    {
        if (idleCoroutine != null)
        {
            StopCoroutine(idleCoroutine);
            idleCoroutine = null;
        }
    }

    private IEnumerator IdleTimeoutCoroutine()
    {
        yield return new WaitForSeconds(iconTimeout);
        OnComboFail();
    }

    private IEnumerator ComboTimeoutCoroutine()
    {
        yield return new WaitForSeconds(comboTimeout);
        OnComboFail();
    }

    #endregion
}
