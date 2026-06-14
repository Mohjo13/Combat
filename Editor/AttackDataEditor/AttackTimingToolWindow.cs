using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#region Combo Timing Tool

public class AttackTimingToolWindow : EditorWindow
{
    #region Constants & Colors

    private static readonly Color DefaultStep0    = new Color(0.2f, 0.9f, 0.2f);
    private static readonly Color DefaultStep1    = new Color(0.2f, 0.5f, 1.0f);
    private static readonly Color DefaultStep2    = new Color(1.0f, 0.6f, 0.1f);
    private static readonly Color ColPhaseStartup  = new Color(0.35f, 0.35f, 0.9f, 0.55f);
    private static readonly Color ColPhaseImpact   = new Color(0.9f,  0.25f, 0.25f, 0.55f);
    private static readonly Color ColPhaseRecovery = new Color(0.25f, 0.75f, 0.35f, 0.55f);

    private static readonly float[]  CfPresetValues = { 0.25f, 0.1f, 0.05f };
    private static readonly string[] CfPresetNames  = { "Smooth", "Standard", "Snappy" };

    private const string PrefStep0R = "CTT_Step0_R", PrefStep0G = "CTT_Step0_G", PrefStep0B = "CTT_Step0_B";
    private const string PrefStep1R = "CTT_Step1_R", PrefStep1G = "CTT_Step1_G", PrefStep1B = "CTT_Step1_B";
    private const string PrefStep2R = "CTT_Step2_R", PrefStep2G = "CTT_Step2_G", PrefStep2B = "CTT_Step2_B";

    #endregion

    #region Open

    [MenuItem("Window/Combat/Combo Timing Tool")]
    public static void OpenFromMenu()
    {
        var w = GetWindow<AttackTimingToolWindow>(false, "Combo Timing Tool", true);
        w.minSize = new Vector2(420, 500);
        w.LoadPrefs();
    }

    #endregion

    #region State

    // Target
    private ComboSequence _targetSequence;

    // Steps
    private class StepRecord { public AttackData data; public string label; public Color color; }
    private List<StepRecord> _steps = new List<StepRecord>();

    // Step colors
    private Color _stepColor0 = new Color(0.2f, 0.9f, 0.2f);
    private Color _stepColor1 = new Color(0.2f, 0.5f, 1f);
    private Color _stepColor2 = new Color(1f,   0.6f, 0.1f);

    // Per-step crossfade state
    private enum CrossfadeEditMode { Presets, FineTune }
    private CrossfadeEditMode[] _cfEditMode = new CrossfadeEditMode[0];
    private float[]             _cfTweak    = new float[0];

    // Preview
    private bool       _previewActive   = false;
    private float      _previewTime     = 0f;
    private bool       _previewPlaying  = false;
    private double     _playStart       = 0;
    private float      _playFrom        = 0f;
    private GameObject _previewTarget   = null;
    private WeaponBase _previewWeapon   = null;

    // Scroll
    private Vector2 _scroll;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        LoadPrefs();
        EnsureStepList();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        StopPreview();
    }

    private void OnFocus()         { EnsureStepList(); RebuildSteps(); }
    private void OnInspectorUpdate() => Repaint();
    private void EnsureStepList()  { if (_steps == null) _steps = new List<StepRecord>(); }

    #endregion

    #region OnGUI

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Combo Timing Tool", new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
        EditorGUILayout.LabelField("Author combo chain transitions without touching the FBX.", EditorStyles.miniLabel);
        EditorGUILayout.Space(6);

        DrawTargetPicker();
        EditorGUILayout.Space(6);

        if (_steps.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a ComboSequence above to begin.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        EnsurePerStepArrays();

        DrawChainTransitionSection();
        EditorGUILayout.Space(8);
        DrawPreviewSection();

        EditorGUILayout.EndScrollView();
    }

    #endregion

    #region Target Picker

    private void DrawTargetPicker()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _targetSequence = (ComboSequence)EditorGUILayout.ObjectField(
            "Combo Sequence", _targetSequence, typeof(ComboSequence), false);
        if (EditorGUI.EndChangeCheck()) { RebuildSteps(); StopPreview(); }

        if (_targetSequence != null)
            EditorGUILayout.LabelField($"{_steps.Count} step(s) loaded.", EditorStyles.miniLabel);
    }

    #endregion

    #region Per-Step Arrays

    private void EnsurePerStepArrays()
    {
        int n = _steps.Count;
        if (_cfEditMode.Length != n) System.Array.Resize(ref _cfEditMode, n);
        if (_cfTweak.Length    != n) System.Array.Resize(ref _cfTweak,     n);
    }

    #endregion

    #region Chain Transition Section

    private void DrawChainTransitionSection()
    {
        EditorGUILayout.LabelField("Chain Transition Timing", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        for (int i = 0; i < _steps.Count; i++)
        {
            StepRecord rec = _steps[i];
            if (rec.data == null) continue;

            var so = new SerializedObject(rec.data);
            so.Update();

            // ── Step header ──────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            var pb = GUI.backgroundColor;
            GUI.backgroundColor = rec.color;
            GUILayout.Box("", GUILayout.Width(12), GUILayout.Height(16));
            GUI.backgroundColor = pb;
            EditorGUILayout.LabelField($"Step {i + 1}  —  {rec.label}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;

            // ── Exit Window + Crossfade fields ──────────────────────────────
            DrawTrackedField(so, rec.data, "exitWindowSeconds",   "Exit Window (s)");
            DrawTrackedField(so, rec.data, "crossfadeDuration",   "Crossfade Duration (s)");

            // ── Mini timeline ────────────────────────────────────────────────
            EditorGUILayout.Space(3);
            DrawStepMiniTimeline(rec.data, so, i);
            EditorGUILayout.Space(6);

            // ── Crossfade preset / fine-tune ─────────────────────────────────
            EditorGUILayout.LabelField("Crossfade", EditorStyles.boldLabel);
            _cfEditMode[i] = (CrossfadeEditMode)GUILayout.Toolbar(
                (int)_cfEditMode[i], new[] { "Presets", "Fine-tune" }, GUILayout.Height(22));
            EditorGUILayout.Space(3);

            if (_cfEditMode[i] == CrossfadeEditMode.Presets)
                DrawCrossfadePresets(rec.data, so, i);
            else
                DrawCrossfadeFineTune(rec.data, so, i);

            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(rec.data);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);

            // Divider between steps
            if (i < _steps.Count - 1)
            {
                Rect div = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(div, new Color(0.35f, 0.35f, 0.35f));
                EditorGUILayout.Space(6);
            }
        }
    }

    #endregion

    #region Step Mini Timeline

    private void DrawStepMiniTimeline(AttackData data, SerializedObject so, int stepIdx)
    {
        float duration = data.ActiveDuration;
        if (duration <= 0f) return;

        Rect rect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));

        // Row layout: phases top 40%, exit row bottom 60%
        float phaseH = rect.height * 0.40f;
        float exitY  = rect.y + phaseH;
        float exitH  = rect.height - phaseH;

        // ── Phase bands ──────────────────────────────────────────────────────
        if (data.impactTime > 0f && data.recoveryTime > data.impactTime)
        {
            float fS = data.impactTime / duration;
            float fI = (data.recoveryTime - data.impactTime) / duration;
            EditorGUI.DrawRect(new Rect(rect.x,                           rect.y, rect.width * fS,             phaseH), ColPhaseStartup);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * fS,        rect.y, rect.width * fI,             phaseH), ColPhaseImpact);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * (fS + fI), rect.y, rect.width * (1f - fS - fI), phaseH), ColPhaseRecovery);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * fS        - 0.5f, rect.y, 1f, phaseH), new Color(1, 1, 1, 0.3f));
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * (fS + fI) - 0.5f, rect.y, 1f, phaseH), new Color(1, 1, 1, 0.3f));
        }
        else
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, phaseH), AttackDataEditor.ColStripDisabled);
        }

        // ── Exit row background ──────────────────────────────────────────────
        EditorGUI.DrawRect(new Rect(rect.x, exitY, rect.width, exitH), new Color(0.10f, 0.10f, 0.10f));

        // Combo window shading in exit row
        float cwS = Mathf.Clamp01(data.comboWindowStartSeconds / duration);
        float cwW = Mathf.Clamp01((data.comboWindowEndSeconds - data.comboWindowStartSeconds) / duration);
        EditorGUI.DrawRect(new Rect(rect.x + rect.width * cwS, exitY, rect.width * cwW, exitH),
            new Color(0.2f, 0.5f, 0.2f, 0.25f));

        // Exit marker
        float exitSec    = data.EffectiveExitSeconds;
        bool  isExplicit = data.exitWindowSeconds > 0f;
        float exitFrac   = Mathf.Clamp01(exitSec / duration);
        float exitX      = rect.x + rect.width * exitFrac;
        Color exitCol    = isExplicit ? new Color(1f, 0.75f, 0f, 1f) : new Color(0.55f, 0.55f, 0.55f, 0.7f);

        EditorGUI.DrawRect(new Rect(exitX - 1f, exitY, 2f, exitH), exitCol);
        EditorGUI.DrawRect(new Rect(exitX - 3f, exitY, 6f, 4f), exitCol);

        // Exit label
        string exitLbl = isExplicit ? $"Exit {exitSec:F2}s" : $"Exit auto {exitSec:F2}s";
        var miniLbl = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 };
        miniLbl.normal.textColor = exitCol;
        EditorGUI.LabelField(new Rect(exitX + 4f, exitY, 100f, exitH), exitLbl, miniLbl);

        // Drag handle for exit window
        if (so != null)
        {
            AttackDataEditor.DrawHandle(exitX, exitY, 6f, exitH, exitCol);
            int idEX = GUIUtility.GetControlID(FocusType.Passive);
            AttackDataEditor.ProcessDragHandle(idEX, rect, duration, so, "exitWindowSeconds",
                data.comboWindowStartSeconds, duration);
        }

        // Time labels
        EditorGUI.LabelField(new Rect(rect.x + 2,     rect.y + 2, 28, 12), "0s",              EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.xMax - 38,  rect.y + 2, 38, 12), $"{duration:F2}s", EditorStyles.miniLabel);

        // Legend
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("S",        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 1f) } });
        GUILayout.Label("I",        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } });
        GUILayout.Label("R",        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 0.9f, 0.5f) } });
        GUILayout.Label("ComboWin", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.3f, 0.7f, 0.3f) } });
        GUILayout.Label(isExplicit ? "Exit" : "Exit(auto)", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = exitCol } });
        EditorGUILayout.EndHorizontal();

        if (rect.Contains(Event.current.mousePosition) &&
            (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag))
            Repaint();
    }

    #endregion

    #region Crossfade Presets

    private void DrawCrossfadePresets(AttackData data, SerializedObject so, int stepIdx)
    {
        float current = data.crossfadeDuration;
        EditorGUILayout.BeginHorizontal();
        for (int p = 0; p < CfPresetValues.Length; p++)
        {
            bool  isActive  = Mathf.Approximately(current, CfPresetValues[p]);
            Color activeCol = new Color(0.3f, 0.65f, 1f);
            var   style     = new GUIStyle(GUI.skin.button) { fontSize = 10, fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
            GUI.backgroundColor    = isActive ? activeCol : new Color(0.25f, 0.25f, 0.25f, 0.8f);
            style.normal.textColor = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            if (GUILayout.Button(new GUIContent(CfPresetNames[p], $"{CfPresetValues[p]:F2}s"), style, GUILayout.Height(28)))
            {
                Undo.RecordObject(data, $"Set crossfade {CfPresetNames[p]}");
                so.FindProperty("crossfadeDuration").floatValue = CfPresetValues[p];
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                _cfTweak[stepIdx] = 0f;
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField($"Current: {current:F3}s", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
    }

    #endregion

    #region Crossfade Fine-tune

    private void DrawCrossfadeFineTune(AttackData data, SerializedObject so, int stepIdx)
    {
        const float range = 0.50f, rowH = 34f, tagW = 76f, readW = 54f;
        EditorGUILayout.LabelField("Drag to nudge +/-50% from current value.", EditorStyles.miniLabel);
        EditorGUILayout.Space(3);
        EditorGUILayout.BeginHorizontal(GUILayout.Height(rowH));

        var tagStyle = new GUIStyle(GUI.skin.box) { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        tagStyle.normal.textColor = Color.white;
        GUI.backgroundColor = new Color(0.3f, 0.65f, 1f);
        GUILayout.Box("Crossfade", tagStyle, GUILayout.Width(tagW), GUILayout.Height(rowH));
        GUI.backgroundColor = Color.white;
        GUILayout.Space(6);

        Rect  area     = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
        float newTweak = DrawCenterSlider(area, _cfTweak[stepIdx], -range, range, new Color(0.3f, 0.65f, 1f));
        if (!Mathf.Approximately(newTweak, _cfTweak[stepIdx]))
        {
            float oldTweak = _cfTweak[stepIdx];
            _cfTweak[stepIdx] = newTweak;
            float baseVal = oldTweak != -1f ? data.crossfadeDuration / Mathf.Max(1f + oldTweak, 0.01f) : data.crossfadeDuration;
            float newVal  = Mathf.Clamp(baseVal * (1f + newTweak), 0.01f, 1f);
            so.FindProperty("crossfadeDuration").floatValue = (float)System.Math.Round(newVal, 3);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }
        GUILayout.Space(6);

        float t01 = Mathf.InverseLerp(-range, range, _cfTweak[stepIdx]);
        var   rs  = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9, fontStyle = FontStyle.Bold };
        rs.normal.textColor = Color.Lerp(new Color(0.4f, 0.9f, 1f), new Color(1f, 0.5f, 0.3f), t01);
        GUILayout.Label($"{data.crossfadeDuration:F3}s", rs, GUILayout.Width(readW), GUILayout.Height(rowH));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(60)))
            _cfTweak[stepIdx] = 0f;
        EditorGUILayout.EndHorizontal();
    }

    private static float DrawCenterSlider(Rect area, float value, float min, float max, Color accent)
    {
        float trackY  = area.y + area.height * 0.5f;
        float trackX0 = area.x + 2f, trackX1 = area.xMax - 2f, trackW = trackX1 - trackX0;
        float centerX = trackX0 + trackW * 0.5f;
        float frac    = Mathf.InverseLerp(min, max, value);
        float thumbX  = trackX0 + frac * trackW;

        EditorGUI.DrawRect(new Rect(trackX0, trackY - 2f, trackW, 4f), new Color(0.15f, 0.15f, 0.15f));
        Color fill = accent; fill.a = 0.80f;
        EditorGUI.DrawRect(new Rect(Mathf.Min(centerX, thumbX), trackY - 2f, Mathf.Abs(thumbX - centerX), 4f), fill);
        EditorGUI.DrawRect(new Rect(centerX - 0.5f, trackY - 6f, 1f, 12f), new Color(1f, 1f, 1f, 0.30f));
        Color thumbCol = accent; thumbCol.a = 1f;
        EditorGUI.DrawRect(new Rect(thumbX - 5f, trackY - 7f, 10f, 14f), thumbCol);
        EditorGUI.DrawRect(new Rect(thumbX - 4f, trackY - 5f,  8f,  3f), new Color(1f, 1f, 1f, 0.40f));

        var lbl = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 };
        lbl.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
        lbl.alignment = TextAnchor.UpperLeft;  EditorGUI.LabelField(new Rect(trackX0,       area.y, 24f, 12f), $"{(int)(min*100)}%",  lbl);
        lbl.alignment = TextAnchor.UpperRight; EditorGUI.LabelField(new Rect(trackX1 - 24f, area.y, 24f, 12f), $"+{(int)(max*100)}%", lbl);
        EditorGUIUtility.AddCursorRect(area, MouseCursor.SlideArrow);

        int id = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current; float result = value;
        switch (e.type)
        {
            case EventType.MouseDown: if (e.button == 0 && area.Contains(e.mousePosition)) { GUIUtility.hotControl = id; result = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - trackX0) / trackW)); e.Use(); } break;
            case EventType.MouseDrag: if (GUIUtility.hotControl == id) { result = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - trackX0) / trackW)); if (Mathf.Abs(result) < 0.012f) result = 0f; GUI.changed = true; e.Use(); } break;
            case EventType.MouseUp:   if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); } break;
        }
        return (float)System.Math.Round(Mathf.Clamp(result, min, max), 4);
    }

    #endregion

    #region Tracked Field

    private static void DrawTrackedField(SerializedObject so, AttackData data, string propName, string label)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        EditorGUILayout.PropertyField(prop, new GUIContent(label));
        so.ApplyModifiedProperties();
    }

    #endregion

    #region Preview Section

    private void DrawPreviewSection()
    {
        EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);

        bool anyClip = false;
        foreach (var s in _steps) if (s.data?.clip != null) { anyClip = true; break; }
        if (!anyClip)
        {
            EditorGUILayout.HelpBox("Assign clips to AttackData assets to enable preview.", MessageType.None);
            return;
        }

        EditorGUI.BeginChangeCheck();
        _previewTarget = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Preview Target", "Drag the player GameObject from the Hierarchy."),
            _previewTarget, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck()) { StopPreview(); if (_previewTarget != null) CacheComponents(); }

        if (_previewTarget == null)
        {
            EditorGUILayout.HelpBox("Drag the player GameObject from the Hierarchy.", MessageType.Info);
            return;
        }

        float chainDur = GetChainDuration();

        // Advance playback
        if (_previewPlaying)
        {
            _previewTime = _playFrom + (float)(EditorApplication.timeSinceStartup - _playStart);
            if (_previewTime >= chainDur) { _previewTime = chainDur; _previewPlaying = false; }
            Repaint();
        }

        // Transport
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⏮", GUILayout.Width(32))) { _previewTime = 0f; _previewPlaying = false; SampleChain(); }
        if (GUILayout.Button("<",  GUILayout.Width(28))) StepFrame(-1);
        if (_previewPlaying)
        { if (GUILayout.Button("▐▌", GUILayout.Width(28))) _previewPlaying = false; }
        else
        {
            if (GUILayout.Button("▶", GUILayout.Width(35)))
            {
                if (_previewTime >= chainDur) _previewTime = 0f;
                _playFrom = _previewTime; _playStart = EditorApplication.timeSinceStartup;
                _previewPlaying = true;
                if (!_previewActive) StartPreview();
            }
        }
        if (GUILayout.Button(">",  GUILayout.Width(28))) StepFrame(+1);
        if (GUILayout.Button("⏭", GUILayout.Width(32))) { _previewTime = chainDur; _previewPlaying = false; SampleChain(); }
        GUILayout.FlexibleSpace();
        bool wasActive = _previewActive;
        GUI.backgroundColor = _previewActive ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.3f, 0.75f, 0.3f);
        _previewActive = GUILayout.Toggle(_previewActive, _previewActive ? "Stop" : "Preview", EditorStyles.miniButton, GUILayout.Width(72));
        GUI.backgroundColor = Color.white;
        if (_previewActive != wasActive) { if (_previewActive) StartPreview(); else StopPreview(); }
        EditorGUILayout.EndHorizontal();

        if (_previewActive) SampleChain();

        // Chain scrub track
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Chain time — click to scrub", EditorStyles.miniLabel);
        Rect track = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(track, new Color(0.15f, 0.15f, 0.15f));
        DrawChainStrip(track, chainDur);

        // Cursor
        float headX = track.x + track.width * Mathf.Clamp01(_previewTime / Mathf.Max(chainDur, 0.001f));
        EditorGUI.DrawRect(new Rect(headX - 1.5f, track.y - 2f, 3f, track.height + 4f), Color.white);

        // Scrub
        Event ev = Event.current;
        if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && ev.button == 0 && track.Contains(ev.mousePosition))
        {
            _previewTime   = Mathf.Clamp01((ev.mousePosition.x - track.x) / track.width) * chainDur;
            _previewPlaying = false;
            if (!_previewActive) StartPreview();
            SampleChain(); ev.Use(); Repaint();
        }

        // Exit window bands + crossfade bands on chain track
        float cursor2 = 0f;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].data == null) continue;
            AttackData sd       = _steps[i].data;
            float      stepDur  = sd.HasPhaseOverrides ? sd.RemappedDuration : sd.ActiveDuration;
            bool       isExpl   = sd.exitWindowSeconds > 0f;
            Color      exCol    = isExpl ? new Color(1f, 0.75f, 0f, 0.9f) : new Color(0.6f, 0.6f, 0.6f, 0.5f);

            // Exit tick
            float exitAbs  = cursor2 + sd.EffectiveExitSeconds;
            float exFrac   = Mathf.Clamp01(exitAbs / chainDur);
            float exX      = track.x + track.width * exFrac;
            EditorGUI.DrawRect(new Rect(exX - 1f, track.y, 2f, track.height), exCol);

            // Crossfade band (from exit to exit+crossfadeDuration)
            if (sd.crossfadeDuration > 0f)
            {
                float cfEndAbs  = exitAbs + sd.crossfadeDuration;
                float cfFrac    = Mathf.Clamp01(exitAbs   / chainDur);
                float cfEndFrac = Mathf.Clamp01(cfEndAbs  / chainDur);
                Color cfCol     = new Color(0.3f, 0.65f, 1f, 0.35f);
                EditorGUI.DrawRect(new Rect(
                    track.x + track.width * cfFrac, track.y,
                    track.width * (cfEndFrac - cfFrac), track.height), cfCol);
            }

            // Exit label on track
            var exLbl = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 };
            exLbl.normal.textColor = exCol;
            EditorGUI.LabelField(new Rect(exX + 3f, track.y + 1f, 60f, 12f), $"Exit {sd.EffectiveExitSeconds:F2}s", exLbl);

            cursor2 += stepDur;
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        AttackDataEditor.DrawStaticLegend("Startup",  new Color(0.5f, 0.5f, 1f));
        AttackDataEditor.DrawStaticLegend("Impact",   new Color(1f, 0.4f, 0.4f));
        AttackDataEditor.DrawStaticLegend("Recovery", new Color(0.4f, 0.9f, 0.5f));
        AttackDataEditor.DrawStaticLegend("Exit",      new Color(1f, 0.75f, 0f));
        AttackDataEditor.DrawStaticLegend("Crossfade",  new Color(0.3f, 0.65f, 1f));
        EditorGUILayout.EndHorizontal();

        if (_previewActive)
            EditorGUILayout.LabelField("Scene view updates as you scrub  ^", EditorStyles.centeredGreyMiniLabel);
    }

    #endregion

    #region Chain Strip

    private void DrawChainStrip(Rect rect, float chainDur)
    {
        float cursor = 0f;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].data == null) continue;
            AttackData d      = _steps[i].data;
            float      desDur = d.HasPhaseOverrides ? d.RemappedDuration : d.ActiveDuration;
            float      nS     = cursor / chainDur;
            float      nW     = desDur / chainDur;

            if (i > 0) EditorGUI.DrawRect(new Rect(rect.x + rect.width * nS, rect.y, 1f, rect.height), new Color(1,1,1,0.4f));

            if (d.HasPhaseOverrides)
            {
                float realS = d.impactTime * d.startupSpeed.ToMultiplier();
                float realI = (d.recoveryTime - d.impactTime) * d.impactSpeed.ToMultiplier();
                float realR = (d.ActiveDuration - d.recoveryTime) * d.recoverySpeed.ToMultiplier();
                float tot   = Mathf.Max(realS + realI + realR, 0.001f);
                float fS    = realS / tot, fI = realI / tot;
                EditorGUI.DrawRect(new Rect(rect.x + rect.width * nS,                        rect.y, rect.width * nW * fS,             rect.height), AttackDataEditor.ColStripStartup);
                EditorGUI.DrawRect(new Rect(rect.x + rect.width * (nS + nW * fS),           rect.y, rect.width * nW * fI,             rect.height), AttackDataEditor.ColStripImpact);
                EditorGUI.DrawRect(new Rect(rect.x + rect.width * (nS + nW * (fS + fI)),    rect.y, rect.width * nW * (1f - fS - fI), rect.height), AttackDataEditor.ColStripRecovery);
            }
            else
            {
                EditorGUI.DrawRect(new Rect(rect.x + rect.width * nS, rect.y, rect.width * nW, rect.height), AttackDataEditor.ColStripDisabled);
            }

            // Step number label
            EditorGUI.LabelField(new Rect(rect.x + rect.width * nS + 3f, rect.y + 2f, 20f, 14f),
                $"{i + 1}", EditorStyles.miniLabel);

            cursor += desDur;
        }
    }

    #endregion

    #region Preview Helpers

    private float GetChainDuration()
    {
        float total = 0f;
        foreach (var s in _steps)
        {
            if (s.data == null) continue;
            total += s.data.HasPhaseOverrides ? s.data.RemappedDuration : s.data.ActiveDuration;
        }
        return Mathf.Max(total, 0.001f);
    }

    private void ResolveChainTime(float chainT, out AttackData stepData, out float localT)
    {
        float cursor = 0f;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].data == null) continue;
            float dur = _steps[i].data.HasPhaseOverrides ? _steps[i].data.RemappedDuration : _steps[i].data.ActiveDuration;
            if (chainT <= cursor + dur || i == _steps.Count - 1)
            {
                stepData = _steps[i].data;
                localT   = chainT - cursor;
                return;
            }
            cursor += dur;
        }
        stepData = _steps.Count > 0 ? _steps[0].data : null;
        localT   = 0f;
    }

    private static float RemapToClip(AttackData data, float realT)
    {
        if (!data.HasPhaseOverrides) return realT;
        float cl  = data.clip != null ? data.clip.length : data.totalDuration;
        float nS  = data.impactTime, nI = data.recoveryTime - data.impactTime, nR = cl - data.recoveryTime;
        float dS  = nS * data.startupSpeed.ToMultiplier();
        float dI  = nI * data.impactSpeed.ToMultiplier();
        float dR  = nR * data.recoverySpeed.ToMultiplier();
        if (realT <= dS)      return dS > 0f ? (realT / dS) * nS : 0f;
        if (realT <= dS + dI) return data.impactTime + (dI > 0f ? ((realT - dS) / dI) * nI : 0f);
        return data.recoveryTime + (dR > 0f ? ((realT - dS - dI) / dR) * nR : 0f);
    }

    private void CacheComponents()
    {
        if (_previewTarget == null) return;
        _previewWeapon = _previewTarget.GetComponentInChildren<WeaponBase>();
    }

    private void StartPreview()
    {
        if (_previewTarget == null) return;
        CacheComponents();
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        _previewActive = true;
        SampleChain();
    }

    private void StopPreview()
    {
        _previewPlaying = false;
        _previewActive  = false;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        SceneView.RepaintAll();
    }

    private void SampleChain()
    {
        if (_previewTarget == null) return;
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        ResolveChainTime(_previewTime, out AttackData stepData, out float localT);
        if (stepData == null || stepData.clip == null) return;
        float clipTime = Mathf.Clamp(RemapToClip(stepData, localT), 0f, stepData.clip.length);
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_previewTarget, stepData.clip, clipTime);
        AnimationMode.EndSampling();
        SceneView.RepaintAll();
    }

    private void StepFrame(int dir)
    {
        _previewTime    = Mathf.Clamp(_previewTime + dir * (1f / 30f), 0f, GetChainDuration());
        _previewPlaying = false;
        if (!_previewActive) StartPreview();
        SampleChain();
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sv)
    {
        if (!_previewActive || _previewTarget == null || _previewWeapon == null) return;

        ResolveChainTime(_previewTime, out AttackData data, out float localT);
        if (data == null) return;

        // Step label
        int stepIdx = 0;
        float c = 0f;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].data == null) continue;
            float dur = _steps[i].data.HasPhaseOverrides ? _steps[i].data.RemappedDuration : _steps[i].data.ActiveDuration;
            if (_previewTime <= c + dur || i == _steps.Count - 1) { stepIdx = i; break; }
            c += dur;
        }

        var ls = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 12 };
        ls.normal.textColor = _steps[stepIdx].color;
        Handles.Label(_previewWeapon.transform.position + Vector3.up * 2.2f,
            $"Step {stepIdx + 1}  {localT:F2}s", ls);

        sv.Repaint();
    }

    #endregion

    #region Rebuild & Prefs

    private void RebuildSteps()
    {
        if (_steps == null) _steps = new List<StepRecord>();
        _steps.Clear();
        Color[] defs = { _stepColor0, _stepColor1, _stepColor2 };

        if (_targetSequence != null)
            for (int i = 0; i < _targetSequence.StepCount; i++)
            {
                ComboStep cs = _targetSequence.GetStep(i);
                if (cs == null || cs.attack == null) continue;
                _steps.Add(new StepRecord
                {
                    data  = cs.attack,
                    label = cs.attack.attackName,
                    color = defs[Mathf.Min(i, defs.Length - 1)]
                });
            }
    }

    private void SavePrefs()
    {
        EditorPrefs.SetFloat(PrefStep0R, _stepColor0.r); EditorPrefs.SetFloat(PrefStep0G, _stepColor0.g); EditorPrefs.SetFloat(PrefStep0B, _stepColor0.b);
        EditorPrefs.SetFloat(PrefStep1R, _stepColor1.r); EditorPrefs.SetFloat(PrefStep1G, _stepColor1.g); EditorPrefs.SetFloat(PrefStep1B, _stepColor1.b);
        EditorPrefs.SetFloat(PrefStep2R, _stepColor2.r); EditorPrefs.SetFloat(PrefStep2G, _stepColor2.g); EditorPrefs.SetFloat(PrefStep2B, _stepColor2.b);
    }

    private void LoadPrefs()
    {
        _stepColor0 = new Color(EditorPrefs.GetFloat(PrefStep0R, DefaultStep0.r), EditorPrefs.GetFloat(PrefStep0G, DefaultStep0.g), EditorPrefs.GetFloat(PrefStep0B, DefaultStep0.b));
        _stepColor1 = new Color(EditorPrefs.GetFloat(PrefStep1R, DefaultStep1.r), EditorPrefs.GetFloat(PrefStep1G, DefaultStep1.g), EditorPrefs.GetFloat(PrefStep1B, DefaultStep1.b));
        _stepColor2 = new Color(EditorPrefs.GetFloat(PrefStep2R, DefaultStep2.r), EditorPrefs.GetFloat(PrefStep2G, DefaultStep2.g), EditorPrefs.GetFloat(PrefStep2B, DefaultStep2.b));
    }

    // Public accessors used by GizmoAndParryEditors
    public Color   GetStepColor(int i)  => i == 0 ? _stepColor0 : i == 1 ? _stepColor1 : _stepColor2;
    public bool    GetGizmoEnabled()    => true;
    public float   GetGizmoSize()       => 0.25f;
    public Vector3 GetGizmoOffset()     => Vector3.zero;

    #endregion
}

#endregion
