using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Drives the TMP/PSXDissolve shader's _DissolveAmount to materialize text.
/// Attach to a GameObject with a TextMeshProUGUI using a material whose shader is "TMP/PSXDissolve".
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TMPDissolveDriver : MonoBehaviour
{
    #region Inspector
    [Header("Timing")]
    [SerializeField] private float dissolveDuration = 0.6f;
    [SerializeField] private bool  playOnEnable     = true;
    [SerializeField] private AnimationCurve ease    = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Shader Property")]
    [SerializeField] private string dissolveProperty = "_DissolveAmount";
    #endregion

    #region Cached
    private TMP_Text  _text;
    private Material  _mat;
    private int       _propId;
    private Coroutine _routine;
    #endregion

    #region Public API
    public float DissolveDuration => dissolveDuration;

    /// <summary>Materialize in (0 -> 1).</summary>
    public void Materialize() => Run(0f, 1f);

    /// <summary>Dissolve out (1 -> 0).</summary>
    public void Dematerialize() => Run(1f, 0f);

    /// <summary>Set instantly without animating.</summary>
    public void SetAmount(float v) => _mat.SetFloat(_propId, Mathf.Clamp01(v));


    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        _text   = GetComponent<TMP_Text>();
        _mat    = _text.fontMaterial;
        _propId = Shader.PropertyToID(dissolveProperty);
    }

    private void OnEnable()
    {
        if (playOnEnable) Materialize();
    }
    #endregion

    #region Internal
    private void Run(float from, float to)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(from, to));
    }

    private IEnumerator Animate(float from, float to)
    {
        _mat.SetFloat(_propId, from);
        float t = 0f;
        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / dissolveDuration));
            _mat.SetFloat(_propId, Mathf.Lerp(from, to, k));
            yield return null;
        }
        _mat.SetFloat(_propId, to);
        _routine = null;
    }
    #endregion
}
