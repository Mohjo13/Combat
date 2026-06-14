using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns pixel-style trailing squares behind a UI element as it moves.
/// Attach to the icon RectTransform. Assign a square Sprite or leave null for solid color.
/// Component type: Mono
/// How to use: Attach to the flying icon GameObject. Optionally assign TrailSprite in Inspector.
/// </summary>
public class UIPixelTrail : MonoBehaviour
{
    #region Settings

    [Header("Trail")]
    [SerializeField] private Sprite trailSprite;
    [SerializeField] private Color trailColor = Color.white;
    [SerializeField] private float lifetime = 0.3f;
    [SerializeField] private int spawnEveryNFrames = 2;

    [Header("Size")]
    [SerializeField] private float startSize = 18f;
    [SerializeField] private float endSize = 4f;

    [Header("Rotation")]
    [SerializeField] private bool randomRotation = true;

    [Header("Fade")]
    [SerializeField] private float startAlpha = 0.8f;
    [SerializeField] private float endAlpha = 0f;

    #endregion

    #region Private

    private RectTransform _rectTransform;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private int _frameCounter;
    private bool _spawning = true;
    private readonly List<TrailPiece> _pieces = new List<TrailPiece>();

    private class TrailPiece
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Img;
        public float TimeAlive;
    }

    #endregion

    #region Unity

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
            _canvasRect = _canvas.GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        _frameCounter = 0;
        _spawning = true;
    }

    private void OnDisable()
    {
        ClearAll();
    }

    private void LateUpdate()
    {
        if (_spawning)
        {
            _frameCounter++;
            if (_frameCounter >= spawnEveryNFrames)
            {
                _frameCounter = 0;
                SpawnPiece();
            }
        }

        UpdatePieces();
    }

    #endregion

    #region Public API

    public void StopTrail()
    {
        _spawning = false;
        ClearAll();
    }

    #endregion

    #region Trail Logic

    private void SpawnPiece()
    {
        var go = new GameObject("TrailPiece");
        go.transform.SetParent(_canvas.transform, false);

        var rect = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();

        // Position
        Vector2 screenPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            RectTransformUtility.WorldToScreenPoint(null, _rectTransform.position),
            _canvas.worldCamera,
            out screenPos
        );
        rect.anchoredPosition = screenPos;
        rect.sizeDelta = Vector2.one * startSize;

        // Random rotation
        if (randomRotation)
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        // Visuals
        img.sprite = trailSprite;
        img.color = new Color(trailColor.r, trailColor.g, trailColor.b, startAlpha);

        // Draw behind icon
        go.transform.SetSiblingIndex(0);

        _pieces.Add(new TrailPiece { Go = go, Rect = rect, Img = img, TimeAlive = 0f });
    }

    private void UpdatePieces()
    {
        for (int i = _pieces.Count - 1; i >= 0; i--)
        {
            var p = _pieces[i];
            p.TimeAlive += Time.deltaTime;
            float t = Mathf.Clamp01(p.TimeAlive / lifetime);

            float size = Mathf.Lerp(startSize, endSize, t);
            p.Rect.sizeDelta = Vector2.one * size;

            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            var c = p.Img.color;
            c.a = alpha;
            p.Img.color = c;

            if (p.TimeAlive >= lifetime)
            {
                Destroy(p.Go);
                _pieces.RemoveAt(i);
            }
        }
    }

    private void ClearAll()
    {
        foreach (var p in _pieces)
            if (p.Go != null) Destroy(p.Go);
        _pieces.Clear();
    }

    #endregion
}
