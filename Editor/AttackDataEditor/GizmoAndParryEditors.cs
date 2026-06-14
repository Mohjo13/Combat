using UnityEngine;
using UnityEditor;

#region Scene Gizmo Drawer

[InitializeOnLoad]
public static class CombatGizmoDrawer
{
    static CombatGizmoDrawer()
    {
        AttackAnimationDriver.OnAttackStarted += (_, __) => SceneView.RepaintAll();
        AttackAnimationDriver.OnAttackEnded   += ()      => SceneView.RepaintAll();
    }

    [DrawGizmo(GizmoType.Active | GizmoType.NotInSelectionHierarchy | GizmoType.InSelectionHierarchy)]
    private static void DrawWeaponGizmos(WeaponBase weapon, GizmoType gizmoType)
    {
        if (!Application.isPlaying) return;
        if (!AttackAnimationDriver.IsAttackActive) return;
        int si = AttackAnimationDriver.CurrentStepIndex; if (si < 0) return;
        AttackTimingToolWindow w = EditorWindow.GetWindow<AttackTimingToolWindow>(false, "Attack Timing Tool", false);
        if (w != null && !w.GetGizmoEnabled()) return;
        Color   sc  = w != null ? w.GetStepColor(si)  : GetFallbackColor(si);
        Vector3 off = w != null ? w.GetGizmoOffset()   : Vector3.zero;
        float   sz  = w != null ? w.GetGizmoSize()    : 0.25f;
        Gizmos.color = sc; Gizmos.DrawCube(weapon.transform.position + off, Vector3.one * sz);
        if (AttackAnimationDriver.IsHitboxActive && weapon.HitboxCollider != null)
        {
            Gizmos.color = Color.red;
            BoxCollider box = weapon.HitboxCollider as BoxCollider;
            if (box != null) { Matrix4x4 prev = Gizmos.matrix; Gizmos.matrix = box.transform.localToWorldMatrix; Gizmos.DrawWireCube(box.center, box.size); Gizmos.matrix = prev; }
            else Gizmos.DrawWireSphere(weapon.HitboxCollider.bounds.center, weapon.HitboxCollider.bounds.extents.magnitude);
        }
        if (AttackAnimationDriver.IsComboWindowActive) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(weapon.transform.position, 0.3f); }
    }

    private static Color GetFallbackColor(int i) => i == 0 ? new Color(0.2f, 0.9f, 0.2f) : i == 1 ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.6f, 0.1f);
}

#endregion


#region Parry Data Custom Inspector

[CustomEditor(typeof(ParryDataSO))]
public class ParryDataEditor : Editor
{
    private enum ParryEditMode { Presets, FineTune }
    private ParryEditMode _editMode = ParryEditMode.Presets;
    private float  _tweakS = 0f, _tweakI = 0f, _tweakR = 0f;
    private string _tweakGuid = "";
    private bool       _previewActive   = false;
    private float      _previewTime     = 0f;
    private bool       _previewPlaying  = false;
    private double     _previewPlayStart = 0;
    private float      _previewPlayFrom  = 0f;
    private GameObject _previewTarget   = null;

    private void OnDisable() => StopPreview();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ParryDataSO data = (ParryDataSO)target;
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parryClip"));
        EditorGUILayout.Space(4);
        if (data.parryClip != null)
        { EditorGUI.BeginDisabledGroup(true); EditorGUILayout.FloatField("Clip Length (s)", data.parryClip.length); EditorGUI.EndDisabledGroup();
          if (Mathf.Abs(data.parryClip.length - data.totalDuration) > 0.05f) EditorGUILayout.HelpBox($"Clip length ({data.parryClip.length:F2}s) differs from Total Duration ({data.totalDuration:F2}s).", MessageType.Warning); }
        else EditorGUILayout.HelpBox("No clip assigned -- timing runs on Total Duration.", MessageType.Info);
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("Duration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("totalDuration"), new GUIContent("Total Duration (s)"));
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("Window & Cooldown", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("windowDuration"), new GUIContent("Window Duration (s)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldown"),       new GUIContent("Cooldown (s)"));
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("ParryBox Active Frames", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parryBoxStartTime"), new GUIContent("ParryBox Opens At (s)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parryBoxEndTime"),   new GUIContent("ParryBox Closes At (s)"));
        if (data.parryBoxEndTime <= data.parryBoxStartTime) EditorGUILayout.HelpBox("ParryBox End must be greater than Start.", MessageType.Error);
        if (data.parryBoxEndTime > data.windowDuration)     EditorGUILayout.HelpBox($"ParryBox End ({data.parryBoxEndTime:F2}s) exceeds Window Duration ({data.windowDuration:F2}s).", MessageType.Warning);
        EditorGUILayout.Space(8); DrawParryPhaseSection(data);
        EditorGUILayout.Space(8);
        EditorGUI.BeginChangeCheck(); DrawParryInlineTimeline(data, serializedObject);
        if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); EditorUtility.SetDirty(data); if (_previewActive) SceneView.RepaintAll(); }
        EditorGUILayout.Space(8); DrawParryScenePreview(data);
        EditorGUILayout.Space(8);
        if (GUILayout.Button("Open Action Timing Tool", GUILayout.Height(28))) AttackTimingToolWindow.OpenFromMenu();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawParryPhaseSection(ParryDataSO data)
    {
        EditorGUILayout.LabelField("Phase Speed", EditorStyles.boldLabel);
        float total = data.ActiveDuration; if (total <= 0f) return;
        bool active = data.HasPhaseOverrides;
        if (!active) EditorGUILayout.HelpBox("Set Impact Time > 0 and Recovery Time > Impact Time to enable per-phase speed.", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("impactTime"),   new GUIContent("Impact at (s)"),   GUILayout.MinWidth(50));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("recoveryTime"), new GUIContent("Recovery at (s)"), GUILayout.MinWidth(50));
        EditorGUILayout.EndHorizontal();
        if (data.impactTime > 0f && data.recoveryTime <= data.impactTime) EditorGUILayout.HelpBox("Recovery Time must be greater than Impact Time.", MessageType.Error);
        EditorGUILayout.Space(6);
        Rect clipRect = GUILayoutUtility.GetRect(0, 10, GUILayout.ExpandWidth(true));
        AttackDataEditor.DrawClipStrip(clipRect, data.impactTime, data.recoveryTime, total, active);
        EditorGUILayout.Space(5);
        if (active)
        {
            float realS = data.impactTime * data.startupSpeed.ToMultiplier(), realI = (data.recoveryTime - data.impactTime) * data.impactSpeed.ToMultiplier(), realR = (total - data.recoveryTime) * data.recoverySpeed.ToMultiplier();
            float rt = realS + realI + realR; if (rt <= 0f) rt = total; float fS = realS / rt, fI = realI / rt;
            Rect fr = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(fr.x,                     fr.y, fr.width * fS,        fr.height), new Color(0.30f, 0.50f, 0.90f, 0.80f));
            EditorGUI.DrawRect(new Rect(fr.x + fr.width * fS,    fr.y, fr.width * fI,        fr.height), new Color(0.90f, 0.25f, 0.25f, 0.80f));
            EditorGUI.DrawRect(new Rect(fr.x + fr.width*(fS+fI), fr.y, fr.width*(1f-fS-fI), fr.height), new Color(0.25f, 0.72f, 0.35f, 0.80f));
            AttackDataEditor.DrawPhaseLabel(fr, 0f, fS, $"startup\n{realS:F2}s"); AttackDataEditor.DrawPhaseLabel(fr, fS, fS+fI, $"impact\n{realI:F2}s"); AttackDataEditor.DrawPhaseLabel(fr, fS+fI, 1f, $"recovery\n{realR:F2}s");
            EditorGUILayout.LabelField($"Total runtime: {rt:F2}s  (clip: {total:F2}s)", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
        }
        EditorGUILayout.Space(6);
        _editMode = (ParryEditMode)GUILayout.Toolbar((int)_editMode, new[] { "Presets", "Fine-tune" }, GUILayout.Height(22));
        EditorGUILayout.Space(4);
        var presets = AttackDataEditor.BuildSpeedPresets();
        if (_editMode == ParryEditMode.Presets)
        {
            if (active)
            {
                EditorGUILayout.BeginHorizontal(); GUILayout.Space(70);
                foreach (var p in presets) { var hs = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 }; hs.normal.textColor = new Color(0.6f, 0.6f, 0.6f); GUILayout.Label(p.name, hs, GUILayout.MinWidth(1)); }
                EditorGUILayout.EndHorizontal();
                DrawPillRow(data, "startup",  "startupSpeed",  new Color(0.30f, 0.50f, 0.90f, 0.80f), data.startupSpeed,  presets);
                DrawPillRow(data, "impact",   "impactSpeed",   new Color(0.90f, 0.25f, 0.25f, 0.80f), data.impactSpeed,   presets);
                DrawPillRow(data, "recovery", "recoverySpeed", new Color(0.25f, 0.72f, 0.35f, 0.80f), data.recoverySpeed, presets);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                DrawPillRow(data, "startup",  "startupSpeed",  new Color(0.35f, 0.35f, 0.35f, 0.35f), PhaseSpeed.Normal, presets);
                DrawPillRow(data, "impact",   "impactSpeed",   new Color(0.35f, 0.35f, 0.35f, 0.35f), PhaseSpeed.Normal, presets);
                DrawPillRow(data, "recovery", "recoverySpeed", new Color(0.35f, 0.35f, 0.35f, 0.35f), PhaseSpeed.Normal, presets);
                EditorGUI.EndDisabledGroup();
            }
        }
        else DrawFineTuneTab(data, active);
    }

    private void LoadTweaks(ParryDataSO data)
    {
        if (data == null) return;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
        if (guid == _tweakGuid) return;
        _tweakGuid = guid;
        _tweakS = EditorPrefs.GetFloat("ATD_PS_" + guid, 0f);
        _tweakI = EditorPrefs.GetFloat("ATD_PI_" + guid, 0f);
        _tweakR = EditorPrefs.GetFloat("ATD_PR_" + guid, 0f);
    }

    private void SaveTweaks(ParryDataSO data)
    {
        if (data == null) return;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
        _tweakGuid = guid;
        EditorPrefs.SetFloat("ATD_PS_" + guid, _tweakS);
        EditorPrefs.SetFloat("ATD_PI_" + guid, _tweakI);
        EditorPrefs.SetFloat("ATD_PR_" + guid, _tweakR);
    }

    private void DrawFineTuneTab(ParryDataSO data, bool active)
    {
        if (!active) { EditorGUILayout.HelpBox("Enable phase boundaries above to use fine-tune.", MessageType.None); return; }
        LoadTweaks(data);
        EditorGUILayout.LabelField("Drag to nudge each phase +/-15% from its preset.", new GUIStyle(EditorStyles.miniLabel));
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        _tweakS = DrawTweakRow("startup",  new Color(0.30f, 0.50f, 0.90f, 0.80f), data.startupSpeed,  _tweakS);
        EditorGUILayout.Space(2);
        _tweakI = DrawTweakRow("impact",   new Color(0.90f, 0.25f, 0.25f, 0.80f), data.impactSpeed,   _tweakI);
        EditorGUILayout.Space(2);
        _tweakR = DrawTweakRow("recovery", new Color(0.25f, 0.72f, 0.35f, 0.80f), data.recoverySpeed, _tweakR);
        if (EditorGUI.EndChangeCheck()) { SaveTweaks(data); Repaint(); }
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset tweaks", EditorStyles.miniButton, GUILayout.Width(90))) { _tweakS = _tweakI = _tweakR = 0f; SaveTweaks(data); Repaint(); }
        EditorGUILayout.EndHorizontal();
    }

    private static float DrawTweakRow(string phaseName, Color phaseCol, PhaseSpeed preset, float tweak)
    {
        const float range = 0.15f; const float rowH = 36f; const float tagW = 64f; const float readW = 54f;
        EditorGUILayout.BeginHorizontal(GUILayout.Height(rowH));
        var tagStyle = new GUIStyle(GUI.skin.box) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; tagStyle.normal.textColor = Color.white;
        var pb = GUI.backgroundColor; GUI.backgroundColor = phaseCol;
        GUILayout.Box(phaseName, tagStyle, GUILayout.Width(tagW), GUILayout.Height(rowH)); GUI.backgroundColor = pb; GUILayout.Space(6);
        Rect area = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
        float newTweak = DrawCenterSlider(area, tweak, -range, range, phaseCol);
        GUILayout.Space(6);
        float eff = preset.ToMultiplier() * (1f + newTweak); float t01 = Mathf.InverseLerp(-range, range, newTweak);
        var readStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9, fontStyle = FontStyle.Bold };
        readStyle.normal.textColor = Color.Lerp(new Color(0.4f, 0.9f, 1f), new Color(1f, 0.5f, 0.3f), t01);
        GUILayout.Label($"x{eff:F3}", readStyle, GUILayout.Width(readW), GUILayout.Height(rowH));
        EditorGUILayout.EndHorizontal(); return newTweak;
    }

    private static float DrawCenterSlider(Rect area, float value, float min, float max, Color accent)
    {
        float trackY = area.y + area.height * 0.5f, trackX0 = area.x + 2f, trackX1 = area.xMax - 2f, trackW = trackX1 - trackX0;
        float centerX = trackX0 + trackW * 0.5f, frac = Mathf.InverseLerp(min, max, value), thumbX = trackX0 + frac * trackW;
        EditorGUI.DrawRect(new Rect(trackX0, trackY - 2f, trackW, 4f), new Color(0.15f, 0.15f, 0.15f));
        float fillL = Mathf.Min(centerX, thumbX), fillW = Mathf.Abs(thumbX - centerX); Color fill = accent; fill.a = 0.80f;
        EditorGUI.DrawRect(new Rect(fillL, trackY - 2f, fillW, 4f), fill);
        EditorGUI.DrawRect(new Rect(centerX - 0.5f, trackY - 6f, 1f, 12f), new Color(1f, 1f, 1f, 0.30f));
        const float th = 5f, tv = 14f; Rect thumbRect = new Rect(thumbX - th, trackY - tv * 0.5f, th * 2f, tv);
        Color thumbCol = accent; thumbCol.a = 1f;
        EditorGUI.DrawRect(thumbRect, thumbCol);
        EditorGUI.DrawRect(new Rect(thumbX - th + 1f, trackY - tv * 0.5f + 2f, th * 2f - 2f, 3f), new Color(1f, 1f, 1f, 0.40f));
        var lbl = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 }; lbl.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
        lbl.alignment = TextAnchor.UpperLeft;  EditorGUI.LabelField(new Rect(trackX0, area.y, 22f, 12f), $"{(int)(min * 100)}%", lbl);
        lbl.alignment = TextAnchor.UpperRight; EditorGUI.LabelField(new Rect(trackX1 - 22f, area.y, 22f, 12f), $"+{(int)(max * 100)}%", lbl);
        EditorGUIUtility.AddCursorRect(area, MouseCursor.SlideArrow);
        int id = GUIUtility.GetControlID(FocusType.Passive); Event e = Event.current; float result = value;
        switch (e.type)
        {
            case EventType.MouseDown:   if (e.button == 0 && area.Contains(e.mousePosition)) { GUIUtility.hotControl = id; result = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - trackX0) / trackW)); e.Use(); } break;
            case EventType.MouseDrag:   if (GUIUtility.hotControl == id) { result = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - trackX0) / trackW)); if (Mathf.Abs(result) < 0.012f) result = 0f; GUI.changed = true; e.Use(); } break;
            case EventType.MouseUp:     if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); } break;
        }
        return (float)System.Math.Round(Mathf.Clamp(result, min, max), 4);
    }

    private void DrawPillRow(ParryDataSO data, string phaseName, string propName, Color phaseColor, PhaseSpeed current, AttackDataEditor.SpeedPresetEntry[] presets)
    {
        var prop = serializedObject.FindProperty(propName);
        EditorGUILayout.BeginHorizontal();
        var ts = new GUIStyle(GUI.skin.box) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; ts.normal.textColor = Color.white;
        var pb = GUI.backgroundColor; GUI.backgroundColor = phaseColor;
        GUILayout.Box(phaseName, ts, GUILayout.Width(64), GUILayout.Height(30)); GUI.backgroundColor = pb; GUILayout.Space(4);
        foreach (var preset in presets)
        {
            bool ia = current == preset.value; var ps = new GUIStyle(GUI.skin.button) { fontSize = 9, fontStyle = ia ? FontStyle.Bold : FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
            if (ia) { GUI.backgroundColor = preset.bgCol; ps.normal.textColor = ps.hover.textColor = ps.active.textColor = preset.textCol; }
            else    { GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.6f); ps.normal.textColor = new Color(0.65f, 0.65f, 0.65f); }
            if (GUILayout.Button(new GUIContent(preset.name, preset.mult), ps, GUILayout.MinWidth(1), GUILayout.Height(30)))
            { Undo.RecordObject(data, $"Set parry {phaseName} speed"); prop.enumValueIndex = (int)preset.value; serializedObject.ApplyModifiedProperties(); EditorUtility.SetDirty(data); }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawParryInlineTimeline(ParryDataSO data, SerializedObject so)
    {
        EditorGUILayout.LabelField("Parry Window Timeline", EditorStyles.boldLabel);
        float duration = Mathf.Max(data.windowDuration, 0.01f);
        Rect rect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height * 0.25f), new Color(0.3f, 0.6f, 1f, 0.35f));
        float bxS = Mathf.Clamp01(data.parryBoxStartTime / duration), bxW = Mathf.Clamp01((data.parryBoxEndTime - data.parryBoxStartTime) / duration);
        Rect pbR = new Rect(rect.x + rect.width * bxS, rect.y + rect.height * 0.28f, rect.width * bxW, rect.height * 0.72f);
        EditorGUI.DrawRect(pbR, new Color(0.9f, 0.6f, 0.1f, 0.85f));
        if (pbR.Contains(Event.current.mousePosition)) GUI.Label(new Rect(Event.current.mousePosition.x + 10f, Event.current.mousePosition.y - 18f, 230f, 18f), $"ParryBox: {data.parryBoxStartTime:F2}s -> {data.parryBoxEndTime:F2}s", EditorStyles.helpBox);
        int idPS = GUIUtility.GetControlID(FocusType.Passive), idPE = GUIUtility.GetControlID(FocusType.Passive);
        AttackDataEditor.DrawHandle(rect.x + rect.width * bxS,         rect.y + rect.height * 0.28f, 6f, rect.height * 0.72f, Color.yellow);
        AttackDataEditor.DrawHandle(rect.x + rect.width * (bxS + bxW), rect.y + rect.height * 0.28f, 6f, rect.height * 0.72f, Color.yellow);
        AttackDataEditor.ProcessDragHandle(idPS, rect, duration, so, "parryBoxStartTime", 0f,                           data.parryBoxEndTime - 0.01f);
        AttackDataEditor.ProcessDragHandle(idPE, rect, duration, so, "parryBoxEndTime",   data.parryBoxStartTime + 0.01f, duration);
        EditorGUI.LabelField(new Rect(rect.x + 2,     rect.y + 2, 30, 14), "0s",              EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.xMax - 42, rect.y + 2, 42, 14), $"{duration:F2}s", EditorStyles.miniLabel);
        EditorGUILayout.Space(2); EditorGUILayout.BeginHorizontal();
        AttackDataEditor.DrawStaticLegend("Window",  new Color(0.5f, 0.7f, 1f));
        AttackDataEditor.DrawStaticLegend($"ParryBox  {data.parryBoxStartTime:F2}->{data.parryBoxEndTime:F2}s", new Color(1f, 0.7f, 0.2f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("<- Drag yellow handles to adjust ParryBox frames ->", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
        if (rect.Contains(Event.current.mousePosition)) UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private void DrawParryScenePreview(ParryDataSO data)
    {
        EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
        if (data.parryClip == null) { EditorGUILayout.HelpBox("Assign a Parry Clip above to enable scene preview.", MessageType.None); return; }
        EditorGUI.BeginChangeCheck();
        _previewTarget = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Preview Target"), _previewTarget, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck()) StopPreview();
        if (_previewTarget == null) { EditorGUILayout.HelpBox("Drag the Player GameObject from the Hierarchy.", MessageType.Info); return; }
        float realDur = data.HasPhaseOverrides ? data.RemappedDuration : data.ActiveDuration;
        if (_previewPlaying) { _previewTime = _previewPlayFrom + (float)(EditorApplication.timeSinceStartup - _previewPlayStart); if (_previewTime >= realDur) { _previewTime = realDur; _previewPlaying = false; } Repaint(); }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("<<", GUILayout.Width(32))) { _previewTime = 0f; _previewPlaying = false; SampleScene(data); }
        if (GUILayout.Button("<",  GUILayout.Width(28))) StepFrame(data, -1);
        if (_previewPlaying) { if (GUILayout.Button("||", GUILayout.Width(28))) _previewPlaying = false; }
        else { if (GUILayout.Button(">", GUILayout.Width(28))) { if (_previewTime >= realDur) _previewTime = 0f; _previewPlayFrom = _previewTime; _previewPlayStart = EditorApplication.timeSinceStartup; _previewPlaying = true; if (!_previewActive) StartPreview(data); } }
        if (GUILayout.Button(">",  GUILayout.Width(28))) StepFrame(data, +1);
        if (GUILayout.Button(">>", GUILayout.Width(32))) { _previewTime = realDur; _previewPlaying = false; SampleScene(data); }
        GUILayout.FlexibleSpace();
        bool wa = _previewActive; GUI.backgroundColor = _previewActive ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.3f, 0.75f, 0.3f);
        _previewActive = GUILayout.Toggle(_previewActive, _previewActive ? "Stop" : "Preview", EditorStyles.miniButton, GUILayout.Width(72));
        GUI.backgroundColor = Color.white; if (_previewActive != wa) { if (_previewActive) StartPreview(data); else StopPreview(); }
        EditorGUILayout.EndHorizontal();
        if (_previewActive) SampleScene(data);
        EditorGUILayout.Space(2);
        float clipLen = data.parryClip != null ? data.parryClip.length : data.totalDuration;
        Rect ct2 = GUILayoutUtility.GetRect(0, 12, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(ct2, new Color(0.15f, 0.15f, 0.15f));
        AttackDataEditor.DrawClipStrip(ct2, data.impactTime, data.recoveryTime, clipLen, data.HasPhaseOverrides);
        float pbcS = Mathf.Clamp01(data.parryBoxStartTime / clipLen), pbcW = Mathf.Clamp01((data.parryBoxEndTime - data.parryBoxStartTime) / clipLen);
        EditorGUI.DrawRect(new Rect(ct2.x + ct2.width * pbcS, ct2.y, ct2.width * pbcW, ct2.height), new Color(1f, 0.7f, 0.2f, 0.8f));
        float ch = ct2.x + ct2.width * Mathf.Clamp01(RemapTimeToClip(data, _previewTime) / Mathf.Max(clipLen, 0.001f));
        EditorGUI.DrawRect(new Rect(ch - 1f, ct2.y, 2f, ct2.height), new Color(1, 1, 1, 0.7f));
        EditorGUILayout.Space(2); EditorGUILayout.BeginHorizontal();
        AttackDataEditor.DrawStaticLegend("ParryBox", new Color(1f, 0.7f, 0.2f)); EditorGUILayout.EndHorizontal();
        if (_previewActive) EditorGUILayout.LabelField("Scene view updates as you scrub  ^", EditorStyles.centeredGreyMiniLabel);
    }

    private void StartPreview(ParryDataSO data) { if (_previewTarget == null) return; AnimationMode.StartAnimationMode(); _previewActive = true; SceneView.duringSceneGui -= OnSceneGUI; SceneView.duringSceneGui += OnSceneGUI; SampleScene(data); }
    private void StopPreview() { _previewPlaying = false; _previewActive = false; SceneView.duringSceneGui -= OnSceneGUI; if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); SceneView.RepaintAll(); }
    private float RemapTimeToClip(ParryDataSO data, float realT) { if (!data.HasPhaseOverrides) return realT; float cl = data.parryClip != null ? data.parryClip.length : data.totalDuration; float nS = data.impactTime, nI = data.recoveryTime - data.impactTime, nR = cl - data.recoveryTime, dS = nS * data.startupSpeed.ToMultiplier(), dI = nI * data.impactSpeed.ToMultiplier(), dR = nR * data.recoverySpeed.ToMultiplier(); if (realT <= dS) return dS > 0f ? (realT / dS) * nS : 0f; if (realT <= dS + dI) return data.impactTime + (dI > 0f ? ((realT - dS) / dI) * nI : 0f); return data.recoveryTime + (dR > 0f ? ((realT - dS - dI) / dR) * nR : 0f); }
    private void SampleScene(ParryDataSO data) { if (_previewTarget == null || data.parryClip == null) return; if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode(); float ct = Mathf.Clamp(RemapTimeToClip(data, _previewTime), 0f, data.parryClip.length); AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(_previewTarget, data.parryClip, ct); AnimationMode.EndSampling(); SceneView.RepaintAll(); }
    private void StepFrame(ParryDataSO data, int dir) { float dur = data.HasPhaseOverrides ? data.RemappedDuration : data.ActiveDuration; _previewTime = Mathf.Clamp(_previewTime + dir * (1f / 30f), 0f, dur); _previewPlaying = false; if (!_previewActive) StartPreview(data); SampleScene(data); }
    private static (string name, PhaseSpeed speed) GetPhaseInfo(ParryDataSO data, float t) { if (!data.HasPhaseOverrides) return ("--", PhaseSpeed.Normal); float dS = data.impactTime * data.startupSpeed.ToMultiplier(), dI = (data.recoveryTime - data.impactTime) * data.impactSpeed.ToMultiplier(); if (t < dS) return ("STARTUP", data.startupSpeed); if (t < dS + dI) return ("IMPACT", data.impactSpeed); return ("RECOVERY", data.recoverySpeed); }
    private void OnSceneGUI(SceneView sv) { if (!_previewActive || _previewTarget == null) return; ParryDataSO data = (ParryDataSO)target; bool on = _previewTime >= data.parryBoxStartTime && _previewTime <= data.parryBoxEndTime; if (on) { Handles.color = new Color(1f, 0.7f, 0.2f, 0.9f); Handles.DrawWireDisc(_previewTarget.transform.position + Vector3.up * 0.8f, Vector3.up, 0.5f); Handles.DrawWireDisc(_previewTarget.transform.position + Vector3.up * 0.8f, Vector3.forward, 0.5f); var hs = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 12 }; hs.normal.textColor = new Color(1f, 0.7f, 0.2f); Handles.Label(_previewTarget.transform.position + Vector3.up * 2.2f, "PARRYBOX ACTIVE", hs); } (string pn, PhaseSpeed sp) = GetPhaseInfo(data, _previewTime); Color pc = pn == "STARTUP" ? new Color(0.5f, 0.6f, 1f) : pn == "IMPACT" ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.5f); var ls = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 13 }; ls.normal.textColor = pc; Handles.Label(_previewTarget.transform.position + Vector3.up * 2.8f, data.HasPhaseOverrides ? $"# {pn}  [{sp}]  {_previewTime:F2}s" : $"# {_previewTime:F2}s", ls); sv.Repaint(); }
}

#endregion
