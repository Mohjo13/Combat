using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class StreakVFXManager : MonoBehaviour
{
    #region Settings

    [SerializeField] private StreakUI streakUI;

    [Header("--- Row Image ---")]
    [SerializeField] private float rowFadeInAlpha    = 0.4f;
    [SerializeField] private float rowFadeInDuration = 0.15f;
    [SerializeField] private Color rowHitColor       = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private float rowHitDuration    = 0.2f;
    [SerializeField] private Color rowComboColor     = new Color(1f, 0.04f, 0f, 1f);
    [SerializeField] private float rowComboDuration  = 0.3f;
    [SerializeField] private float rowComboIntensity = 1f;

    [Header("--- Panel Image ---")]
    [SerializeField] private Image panelImage;
    [SerializeField] private float panelFadeInAlpha    = 1f;
    [SerializeField] private float panelFadeInDuration = 0.15f;
    [SerializeField] private Color panelHitColor       = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private float panelFadeOutDuration = 0.5f;
    [SerializeField] private float panelComboFadeOutDuration = 0.3f;

    #endregion

    #region Shader Property IDs

    private static readonly int PropAlpha         = Shader.PropertyToID("_Alpha");
    private static readonly int PropGlowIntensity = Shader.PropertyToID("_GlowIntensity");
    private static readonly int PropGlowColor     = Shader.PropertyToID("_GlowColor");

    private StreakRowUI cachedRow;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        CombatEvents.OnAttackStartedWithIcon += OnIconSpawned;
        CombatEvents.OnPlayerHitTarget       += OnHitTarget;
        CombatEvents.OnStreakIconLanded      += OnIconLanded;
        CombatEvents.OnStreakRowFull         += OnRowFull;
        CombatEvents.OnComboCompleted        += OnComboCompleted;
        CombatEvents.OnStreakRowFloatedAway  += OnRowFloatedAway;
        CombatEvents.OnComboFailed           += OnComboFailed;
    }

    private void OnDisable()
    {
        CombatEvents.OnAttackStartedWithIcon -= OnIconSpawned;
        CombatEvents.OnPlayerHitTarget       -= OnHitTarget;
        CombatEvents.OnStreakIconLanded      -= OnIconLanded;
        CombatEvents.OnStreakRowFull         -= OnRowFull;
        CombatEvents.OnComboCompleted        -= OnComboCompleted;
        CombatEvents.OnStreakRowFloatedAway  -= OnRowFloatedAway;
        CombatEvents.OnComboFailed           -= OnComboFailed;
    }

    #endregion

    #region Helpers

    private Material GetRowMaterial()
    {
        if (cachedRow == null || cachedRow.rowImage == null) return null;
        return cachedRow.rowImage.material;
    }

    #endregion

    #region Streak Handlers

    private void OnIconSpawned(AttackData data, bool isChained)
    {
        DOVirtual.DelayedCall(0f, () =>
        {
            bool isNewRow = cachedRow == null;
            cachedRow = streakUI?.ActiveRow;
            if (!isNewRow) return;

            // Row
            var mat = GetRowMaterial();
            if (mat != null)
            {
                mat.SetFloat(PropAlpha, 0f);
                mat.SetFloat(PropGlowIntensity, 0f);
                DOTween.To(() => mat.GetFloat(PropAlpha),
                           x => mat.SetFloat(PropAlpha, x),
                           rowFadeInAlpha, rowFadeInDuration);
            }

            // Panel
            if (panelImage != null)
                panelImage.DOFade(panelFadeInAlpha, panelFadeInDuration).From(0f);
        });
    }

    private void OnIconLanded() { }

    private void OnHitTarget(AttackType type, float damage, GameObject target, Vector3 hitPos, Vector3 dir)
    {
        // Row
        var mat = GetRowMaterial();
        if (mat != null)
        {
            mat.SetColor(PropGlowColor, rowHitColor);
            DOTween.To(() => mat.GetFloat(PropGlowIntensity),
                       x => mat.SetFloat(PropGlowIntensity, x),
                       1f, rowHitDuration * 0.5f)
                   .OnComplete(() =>
                       DOTween.To(() => mat.GetFloat(PropGlowIntensity),
                                  x => mat.SetFloat(PropGlowIntensity, x),
                                  0f, rowHitDuration * 0.5f));
        }

        // Panel
        if (panelImage != null)
            panelImage.color = panelHitColor;
    }

    private void OnRowFull() { }

    private void OnComboCompleted(string comboName)
    {
        var mat = GetRowMaterial();
        cachedRow = null;

        if (mat != null)
        {
            mat.SetColor(PropGlowColor, rowComboColor);
            DOTween.To(() => mat.GetFloat(PropGlowIntensity),
                       x => mat.SetFloat(PropGlowIntensity, x),
                       rowComboIntensity, rowComboDuration);
        }

        // Panel fade out on complete
        if (panelImage != null)
            panelImage.DOFade(0f, panelComboFadeOutDuration);
    }

    private void OnRowFloatedAway() { }

    private void OnComboFailed()
    {
        cachedRow = null;

        // Panel fade out
        if (panelImage != null)
            panelImage.DOFade(0f, panelFadeOutDuration);

        // Row HideBackground handled in StreakRowUI.OnComboFail()
    }

    #endregion
}
