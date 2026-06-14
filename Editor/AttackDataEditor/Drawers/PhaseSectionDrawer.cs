using UnityEngine;
using UnityEditor;

// Per-phase speed UI: clip distribution strip, runtime feel bar,
// preset pill rows, and fine-tune center-zero sliders.
public partial class AttackDataEditor
{
    #region Fine-tune State

    private enum PhaseEditMode { Presets, FineTune }
    private PhaseEditMode _editMode = PhaseEditMode.Presets;

    private float  _tweakS = 0f, _tweakI = 0f, _tweakR = 0f;
    private string _tweakGuid = "";

    private void LoadTweaks(AttackData data)
    {
        string guid = GetGuid(data);
        if (guid == _tweakGuid) return;
        _tweakGuid = guid;
        _tweakS = EditorPrefs.GetFloat("ATD_S_" + guid, 0f);
        _tweakI = EditorPrefs.GetFloat("ATD_I_" + guid, 0f);
        _tweakR = EditorPrefs.GetFloat("ATD_R_" + guid, 0f);
    }

    private void SaveTweaks(AttackData data)
    {
        string guid = GetGuid(data);
        _tweakGuid = guid;
        EditorPrefs.SetFloat("ATD_S_" + guid, _tweakS);
        EditorPrefs.SetFloat("ATD_I_" + guid, _tweakI);
        EditorPrefs.SetFloat("ATD_R_" + guid, _tweakR);
    }

    #endregion

    #region DrawPhaseSection (entry point)

    private void DrawPhaseSection(AttackData data)
    {
        EditorGUILayout.LabelField("Phase Speed", EditorStyles.boldLabel);

        float total = data.ActiveDuration;
        if (total <= 0f) return;

        bool active = data.impactTime > 0f && data.recoveryTime > data.impactTime;

        if (!active)
            EditorGUILayout.HelpBox(
                "Set Impact Time > 0 and Recovery Time > Impact Time to enable per-phase speed.",
                MessageType.None);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("impactTime"),   new GUIContent("Impact at (s)"),   GUILayout.MinWidth(60));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("recoveryTime"), new GUIContent("Recovery at (s)"), GUILayout.MinWidth(60));
        EditorGUILayout.EndHorizontal();

        if (data.impactTime > 0f && data.recoveryTime <= data.impactTime)
            EditorGUILayout.HelpBox("Recovery Time must be greater than Impact Time.", MessageType.Error);

        EditorGUILayout.Space(5);

        if (active)
        {
            float mS = _editMode == PhaseEditMode.FineTune ? EffMult(data.startupSpeed,  _tweakS) : data.startupSpeed.ToMultiplier();
            float mI = _editMode == PhaseEditMode.FineTune ? EffMult(data.impactSpeed,   _tweakI) : data.impactSpeed.ToMultiplier();
            float mR = _editMode == PhaseEditMode.FineTune ? EffMult(data.recoverySpeed, _tweakR) : data.recoverySpeed.ToMultiplier();

            float realS = data.impactTime * mS;
            float realI = (data.recoveryTime - data.impactTime) * mI;
            float realR = (total - data.recoveryTime) * mR;
            float realTotal = realS + realI + realR;
            if (realTotal <= 0f) realTotal = total;

            float fS = realS / realTotal;
            float fI = realI / realTotal;

            EditorGUILayout.LabelField("Runtime feel (after speed)", new GUIStyle(EditorStyles.miniLabel));
            Rect feelRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(feelRect.x,                           feelRect.y, feelRect.width * fS,             feelRect.height), ColStripStartup);
            EditorGUI.DrawRect(new Rect(feelRect.x + feelRect.width * fS,    feelRect.y, feelRect.width * fI,             feelRect.height), ColStripImpact);
            EditorGUI.DrawRect(new Rect(feelRect.x + feelRect.width*(fS+fI), feelRect.y, feelRect.width*(1f - fS - fI),   feelRect.height), ColStripRecovery);
            DrawPhaseLabel(feelRect, 0f,    fS,    $"startup\n{realS:F2}s");
            DrawPhaseLabel(feelRect, fS,    fS+fI, $"impact\n{realI:F2}s");
            DrawPhaseLabel(feelRect, fS+fI, 1f,    $"recovery\n{realR:F2}s");

            // Draggable boundary handles
            int idFI = GUIUtility.GetControlID(FocusType.Passive);
            int idFR = GUIUtility.GetControlID(FocusType.Passive);
            float impactHandleX   = feelRect.x + feelRect.width * fS;
            float recoveryHandleX = feelRect.x + feelRect.width * (fS + fI);
            DrawHandle(impactHandleX,   feelRect.y, 6f, feelRect.height, Color.white);
            DrawHandle(recoveryHandleX, feelRect.y, 6f, feelRect.height, Color.white);

            Event fe = Event.current;
            float minImpact = 0.01f;
            float maxImpact = data.recoveryTime - 0.01f;
            float minRecovery = data.impactTime + 0.01f;
            float maxRecovery = total - 0.01f;

            Rect impactHitzone   = new Rect(impactHandleX   - 5f, feelRect.y, 10f, feelRect.height);
            Rect recoveryHitzone = new Rect(recoveryHandleX - 5f, feelRect.y, 10f, feelRect.height);
            EditorGUIUtility.AddCursorRect(impactHitzone,   MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(recoveryHitzone, MouseCursor.ResizeHorizontal);

            switch (fe.type)
            {
                case EventType.MouseDown:
                    if (fe.button == 0)
                    {
                        if (impactHitzone.Contains(fe.mousePosition))   { GUIUtility.hotControl = idFI; fe.Use(); }
                        else if (recoveryHitzone.Contains(fe.mousePosition)) { GUIUtility.hotControl = idFR; fe.Use(); }
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == idFI || GUIUtility.hotControl == idFR)
                    {
                        float t = Mathf.Clamp01((fe.mousePosition.x - feelRect.x) / feelRect.width);
                        // t is in runtime-feel space — convert back to clip space
                        float clipT = t * realTotal;
                        if (GUIUtility.hotControl == idFI)
                        {
                            // impactTime = clipT / mS (inverse of realS = impactTime * mS)
                            float newImpact = Mathf.Clamp(clipT / Mathf.Max(mS, 0.001f), minImpact, maxImpact);
                            serializedObject.FindProperty("impactTime").floatValue = (float)System.Math.Round(newImpact, 3);
                            serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(data);
                        }
                        else
                        {
                            // recoveryTime: clipT accounts for startup real duration
                            float clipTRaw = (clipT - realS) / Mathf.Max(mI, 0.001f) + data.impactTime;
                            float newRecovery = Mathf.Clamp(clipTRaw, minRecovery, maxRecovery);
                            serializedObject.FindProperty("recoveryTime").floatValue = (float)System.Math.Round(newRecovery, 3);
                            serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(data);
                        }
                        GUI.changed = true;
                        fe.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == idFI || GUIUtility.hotControl == idFR)
                    { GUIUtility.hotControl = 0; fe.Use(); }
                    break;
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Total runtime: {realTotal:F2}s  (clip: {total:F2}s)",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel));
        }

        EditorGUILayout.Space(6);

        // Tab bar: Presets / Fine-tune
        _editMode = (PhaseEditMode)GUILayout.Toolbar((int)_editMode,
            new[] { "Presets", "Fine-tune" }, GUILayout.Height(22));
        EditorGUILayout.Space(4);

        if (_editMode == PhaseEditMode.Presets)
            DrawPresetsTab(data, active);
        else
            DrawFineTuneTab(data, active);
    }

    #endregion

    #region Presets Tab

    private void DrawPresetsTab(AttackData data, bool active)
    {
        var presets = BuildSpeedPresets();

        if (active)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(70);
            foreach (var p in presets)
            {
                var hs = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
                hs.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label(p.name, hs, GUILayout.MinWidth(1));
            }
            EditorGUILayout.EndHorizontal();
            DrawPillRow(data, "startup",  "startupSpeed",  ColStripStartup,  data.startupSpeed,  presets);
            DrawPillRow(data, "impact",   "impactSpeed",   ColStripImpact,   data.impactSpeed,   presets);
            DrawPillRow(data, "recovery", "recoverySpeed", ColStripRecovery, data.recoverySpeed, presets);
        }
        else
        {
            EditorGUI.BeginDisabledGroup(true);
            DrawPillRow(data, "startup",  "startupSpeed",  ColStripDisabled, PhaseSpeed.Normal, BuildSpeedPresets());
            DrawPillRow(data, "impact",   "impactSpeed",   ColStripDisabled, PhaseSpeed.Normal, BuildSpeedPresets());
            DrawPillRow(data, "recovery", "recoverySpeed", ColStripDisabled, PhaseSpeed.Normal, BuildSpeedPresets());
            EditorGUI.EndDisabledGroup();
        }
    }

    private void DrawPillRow(AttackData data, string phaseName, string propName, Color phaseColor, PhaseSpeed current, SpeedPresetEntry[] presets)
    {
        var prop = serializedObject.FindProperty(propName);

        EditorGUILayout.BeginHorizontal();
        var tagStyle = new GUIStyle(GUI.skin.box) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        tagStyle.normal.textColor = Color.white;
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = phaseColor;
        GUILayout.Box(phaseName, tagStyle, GUILayout.Width(64), GUILayout.Height(30));
        GUI.backgroundColor = prevBg;
        GUILayout.Space(4);

        foreach (var preset in presets)
        {
            bool isActive = current == preset.value;
            var pillStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 9, fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal, alignment = TextAnchor.MiddleCenter };

            if (isActive)
            {
                GUI.backgroundColor = preset.bgCol;
                pillStyle.normal.textColor = pillStyle.hover.textColor = pillStyle.active.textColor = preset.textCol;
            }
            else
            {
                GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
                pillStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
            }

            if (GUILayout.Button(new GUIContent(preset.name, preset.mult), pillStyle, GUILayout.MinWidth(1), GUILayout.Height(30)))
            {
                Undo.RecordObject(data, $"Set {phaseName} speed");
                prop.enumValueIndex = (int)preset.value;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                if (_previewActive) SampleScene(data);
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Fine-tune Tab

    private void DrawFineTuneTab(AttackData data, bool active)
    {
        if (!active)
        {
            EditorGUILayout.HelpBox("Enable phase boundaries above to use fine-tune.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("Drag to nudge each phase +/-15% from its preset.",
            new GUIStyle(EditorStyles.miniLabel));
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        _tweakS = DrawTweakRow("startup",  ColStripStartup,  data.startupSpeed,  _tweakS);
        EditorGUILayout.Space(2);
        _tweakI = DrawTweakRow("impact",   ColStripImpact,   data.impactSpeed,   _tweakI);
        EditorGUILayout.Space(2);
        _tweakR = DrawTweakRow("recovery", ColStripRecovery, data.recoverySpeed, _tweakR);
        if (EditorGUI.EndChangeCheck())
        {
            SaveTweaks(data);
            if (_previewActive) SampleScene(data);
            Repaint();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset tweaks", EditorStyles.miniButton, GUILayout.Width(90)))
        {
            _tweakS = _tweakI = _tweakR = 0f;
            SaveTweaks(data);
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
    }

    // One fine-tune row: [phase tag] [center-zero slider] [xN.NNN]
    private float DrawTweakRow(string phaseName, Color phaseCol, PhaseSpeed preset, float tweak)
    {
        const float range = 0.15f;
        const float rowH  = 36f;
        const float tagW  = 64f;
        const float readW = 54f;

        EditorGUILayout.BeginHorizontal(GUILayout.Height(rowH));

        var tagStyle = new GUIStyle(GUI.skin.box)
        { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        tagStyle.normal.textColor = Color.white;
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = phaseCol;
        GUILayout.Box(phaseName, tagStyle, GUILayout.Width(tagW), GUILayout.Height(rowH));
        GUI.backgroundColor = prevBg;
        GUILayout.Space(6);

        Rect area = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
        float newTweak = DrawCenterSlider(area, tweak, -range, range, phaseCol);

        GUILayout.Space(6);

        float eff  = EffMult(preset, newTweak);
        float t01  = Mathf.InverseLerp(-range, range, newTweak);
        var readStyle = new GUIStyle(EditorStyles.miniLabel)
        { alignment = TextAnchor.MiddleCenter, fontSize = 9, fontStyle = FontStyle.Bold };
        readStyle.normal.textColor = Color.Lerp(new Color(0.4f, 0.9f, 1f), new Color(1f, 0.5f, 0.3f), t01);
        GUILayout.Label($"x{eff:F3}", readStyle, GUILayout.Width(readW), GUILayout.Height(rowH));

        EditorGUILayout.EndHorizontal();
        return newTweak;
    }

    // Custom center-zero slider. Returns the new value.
    private static float DrawCenterSlider(Rect area, float value, float min, float max, Color accent)
    {
        float trackY  = area.y + area.height * 0.5f;
        float trackX0 = area.x + 2f;
        float trackX1 = area.xMax - 2f;
        float trackW  = trackX1 - trackX0;
        float centerX = trackX0 + trackW * 0.5f;
        float frac    = Mathf.InverseLerp(min, max, value);
        float thumbX  = trackX0 + frac * trackW;

        // Track background
        EditorGUI.DrawRect(new Rect(trackX0, trackY - 2f, trackW, 4f), new Color(0.15f, 0.15f, 0.15f));

        // Fill: center → thumb
        float fillL = Mathf.Min(centerX, thumbX);
        float fillW = Mathf.Abs(thumbX - centerX);
        Color fill  = accent; fill.a = 0.80f;
        EditorGUI.DrawRect(new Rect(fillL, trackY - 2f, fillW, 4f), fill);

        // Center tick
        EditorGUI.DrawRect(new Rect(centerX - 0.5f, trackY - 6f, 1f, 12f), new Color(1f, 1f, 1f, 0.30f));

        // Thumb pill
        const float th = 5f, tv = 14f;
        Rect thumbRect = new Rect(thumbX - th, trackY - tv * 0.5f, th * 2f, tv);
        Color thumbCol = accent; thumbCol.a = 1f;
        EditorGUI.DrawRect(thumbRect, thumbCol);
        EditorGUI.DrawRect(new Rect(thumbX - th + 1f, trackY - tv * 0.5f + 2f, th * 2f - 2f, 3f),
            new Color(1f, 1f, 1f, 0.40f));

        // Range labels
        var lbl = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 };
        lbl.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
        lbl.alignment = TextAnchor.UpperLeft;
        EditorGUI.LabelField(new Rect(trackX0,        area.y, 22f, 12f), $"{(int)(min * 100)}%", lbl);
        lbl.alignment = TextAnchor.UpperRight;
        EditorGUI.LabelField(new Rect(trackX1 - 22f,  area.y, 22f, 12f), $"+{(int)(max * 100)}%", lbl);

        EditorGUIUtility.AddCursorRect(area, MouseCursor.SlideArrow);

        // Mouse input
        int   id     = GUIUtility.GetControlID(FocusType.Passive);
        Event e      = Event.current;
        float result = value;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && area.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    result = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - trackX0) / trackW));
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    result = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - trackX0) / trackW));
                    if (Mathf.Abs(result) < 0.012f) result = 0f;
                    GUI.changed = true;
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                break;
        }

        return (float)System.Math.Round(Mathf.Clamp(result, min, max), 4);
    }

    #endregion
}
