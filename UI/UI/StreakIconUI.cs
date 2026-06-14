using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StreakIconUI : MonoBehaviour
{
    #region References

    [SerializeField] private Image iconImage;
    [SerializeField] private Text scoreText;
    private RectTransform rectTransform;
    private UIPixelTrail _trail;

    #endregion

    #region Settings

    [SerializeField] private float flyDuration = 0.25f;
    [SerializeField] private float scoreFadeDuration = 0.5f;
    [SerializeField] private float fallDuration = 0.3f;
    [SerializeField] private float fallDistance = 200f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        _trail = GetComponent<UIPixelTrail>();
    }

    #endregion

    #region Public API

    public void SetIcon(Sprite sprite)
    {
        iconImage.sprite = sprite;
    }

    public void FlyToSlot(Vector2 spawnOrigin)
    {
        rectTransform.anchoredPosition = spawnOrigin;
        transform.localScale = Vector3.zero;
        StartCoroutine(FlyAfterLayout());
    }

    public void FlashScore(int score)
    {
        if (scoreText == null) return;
        scoreText.text = $"+{score}";
        scoreText.DOFade(0f, scoreFadeDuration).From(1f);
    }

    public void TintTo(Color color, float duration = 0.2f)
    {
        iconImage.DOColor(color, duration);
    }

    public void FallOff(System.Action onComplete = null)
    {
        DOTween.Sequence()
            .Append(rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y - fallDistance, fallDuration).SetEase(Ease.InQuad))
            .Join(iconImage.DOFade(0f, fallDuration))
            .OnComplete(() =>
            {
                onComplete?.Invoke();
                Destroy(gameObject);
            });
    }

    #endregion

    #region Coroutines

    private IEnumerator FlyAfterLayout()
    {
        Vector2 from = rectTransform.anchoredPosition;
        yield return null;
        Vector2 to = rectTransform.anchoredPosition;

        rectTransform.anchoredPosition = from;
        DOTween.Sequence()
            .Append(rectTransform.DOAnchorPos(to, flyDuration).SetEase(Ease.OutCubic))
            .Join(transform.DOScale(1f, flyDuration).SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                _trail?.StopTrail();
                transform.DOScale(1.3f, 0.08f).SetEase(Ease.OutQuad)
                    .OnComplete(() => transform.DOScale(1f, 0.1f).SetEase(Ease.InQuad));
                CombatEvents.RaiseStreakIconLanded();
            });
    }

    #endregion
}
