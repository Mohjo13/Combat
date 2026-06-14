using UnityEngine;
using UnityEditor;

// Timeline strip drawing and drag-handle interaction.
// DrawSingleTimeline is called from AttackDataEditor.OnInspectorGUI
// and from AttackTimingToolWindow.DrawAttackTab.
public partial class AttackDataEditor
{
    #region Clip Strip

    internal static void DrawClipStrip(Rect rect, float impactTime, float recoveryTime, float total, bool active)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
        if (!active) { EditorGUI.DrawRect(rect, ColStripDisabled); return; }

        float fS = impactTime / total;
        float fI = (recoveryTime - impactTime) / total;

        EditorGUI.DrawRect(new Rect(rect.x,                           rect.y, rect.width * fS,             rect.height), new Color(ColStripStartup.r,  ColStripStartup.g,  ColStripStartup.b,  0.45f));
        EditorGUI.DrawRect(new Rect(rect.x + rect.width * fS,         rect.y, rect.width * fI,             rect.height), new Color(ColStripImpact.r,   ColStripImpact.g,   ColStripImpact.b,   0.45f));
        EditorGUI.DrawRect(new Rect(rect.x + rect.width * (fS + fI),  rect.y, rect.width * (1f - fS - fI), rect.height), new Color(ColStripRecovery.r, ColStripRecovery.g, ColStripRecovery.b, 0.45f));

        EditorGUI.DrawRect(new Rect(rect.x + rect.width * fS          - 0.5f, rect.y, 1f, rect.height), new Color(1, 1, 1, 0.4f));
        EditorGUI.DrawRect(new Rect(rect.x + rect.width * (fS + fI)   - 0.5f, rect.y, 1f, rect.height), new Color(1, 1, 1, 0.4f));

        var ml = EditorStyles.miniLabel;
        EditorGUI.LabelField(new Rect(rect.x + 2,         rect.y, 30,  rect.height), "0s",            ml);
        EditorGUI.LabelField(new Rect(rect.xMax - 36,     rect.y, 36,  rect.height), $"{total:F2}s",  ml);
        if (fS > 0.12f) EditorGUI.LabelField(new Rect(rect.x + rect.width * fS          - 22f, rect.y, 44, rect.height), $"{impactTime:F2}s",   ml);
        if (fI > 0.12f) EditorGUI.LabelField(new Rect(rect.x + rect.width * (fS + fI)   - 22f, rect.y, 44, rect.height), $"{recoveryTime:F2}s", ml);
    }

    internal static void DrawClipStrip(Rect rect, AttackData data, float total, bool active)
        => DrawClipStrip(rect, data.impactTime, data.recoveryTime, total, active);

    #endregion

    #region Phase Label

    internal static void DrawPhaseLabel(Rect strip, float fracStart, float fracEnd, string text)
    {
        float blockW = (fracEnd - fracStart) * strip.width;
        if (blockW < 28f) return;
        var s = new GUIStyle(EditorStyles.miniLabel)
        { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontSize = 9 };
        s.normal.textColor = Color.white;
        EditorGUI.LabelField(new Rect(strip.x + fracStart * strip.width, strip.y, blockW, strip.height), text, s);
    }

    #endregion

    #region Single-Attack Timeline (Inspector + Tool Window)

    // Track height constants
    private const float TrackHitbox  = 0.28f; // top 28% — phase bands
    private const float TrackCombo   = 0.28f; // middle 28%
    private const float TrackExit    = 0.22f; // bottom 22% (exit window marker)
    // gap between combo and exit row: 1 - 0.28 - 0.28 - 0.22 = 0.22 → used as spacing

    public static void DrawSingleTimeline(AttackData data, SerializedObject so = null)
    {
        float duration = data.ActiveDuration;
        if (duration <= 0f) return;

        // Timeline is taller now to fit three rows
        Rect rect = GUILayoutUtility.GetRect(0, 52, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));

        // ── Row layout ────────────────────────────────────────────────────────
        // Row 0: phase bands  (y: 0    h: 28%)
        // Row 1: hitbox       (y: 28%  h: 22%)
        // Row 2: combo        (y: 50%  h: 22%)
        // Row 3: exit marker  (y: 72%  h: 28%)

        float row0y = rect.y;
        float row0h = rect.height * 0.28f;
        float row1y = rect.y + rect.height * 0.28f;
        float row1h = rect.height * 0.22f;
        float row2y = rect.y + rect.height * 0.50f;
        float row2h = rect.height * 0.22f;
        float row3y = rect.y + rect.height * 0.72f;
        float row3h = rect.height * 0.28f;

        // Phase bands (row 0)
        if (data.impactTime > 0f && data.recoveryTime > data.impactTime)
        {
            DrawStaticBand(rect, 0f,                data.impactTime,   duration, row0y, row0h, new Color(0.35f, 0.35f, 0.9f, 0.4f));
            DrawStaticBand(rect, data.impactTime,   data.recoveryTime, duration, row0y, row0h, new Color(0.9f,  0.25f, 0.25f, 0.4f));
            DrawStaticBand(rect, data.recoveryTime, duration,          duration, row0y, row0h, new Color(0.25f, 0.75f, 0.35f, 0.4f));
        }

        // Control IDs
        int idHS = GUIUtility.GetControlID(FocusType.Passive);
        int idHE = GUIUtility.GetControlID(FocusType.Passive);
        int idCS = GUIUtility.GetControlID(FocusType.Passive);
        int idCE = GUIUtility.GetControlID(FocusType.Passive);
        int idEX = GUIUtility.GetControlID(FocusType.Passive);
        int idCT = GUIUtility.GetControlID(FocusType.Passive);

        float hS = Mathf.Clamp01(data.hitboxStartSeconds / duration);
        float hW = Mathf.Clamp01((data.hitboxEndSeconds - data.hitboxStartSeconds) / duration);
        float cS = Mathf.Clamp01(data.comboWindowStartSeconds / duration);
        float cW = Mathf.Clamp01((data.comboWindowEndSeconds - data.comboWindowStartSeconds) / duration);
        float eX = Mathf.Clamp01(data.EffectiveExitSeconds / duration);
        bool  hasClipExit = data.clipExitTime > 0f;
        float cT = hasClipExit ? Mathf.Clamp01(data.clipExitTime / duration) : 0f;

        // Hitbox bar (row 1)
        Rect hR = new Rect(rect.x + rect.width * hS, row1y, rect.width * hW, row1h);
        EditorGUI.DrawRect(hR, new Color(0.9f, 0.2f, 0.2f, 0.9f));

        // Combo window bar (row 2)
        Rect cR = new Rect(rect.x + rect.width * cS, row2y, rect.width * cW, row2h);
        EditorGUI.DrawRect(cR, new Color(0.2f, 0.9f, 0.2f, 0.9f));

        // Exit window marker — vertical line + small label (row 3)
        float exX = rect.x + rect.width * eX;
        bool exitIsExplicit = data.exitWindowSeconds > 0f;
        Color exitColor = exitIsExplicit ? new Color(1f, 0.75f, 0f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
        EditorGUI.DrawRect(new Rect(exX - 1f, row3y, 2f, row3h), exitColor);
        // Tiny filled diamond at top of marker
        EditorGUI.DrawRect(new Rect(exX - 3f, row3y, 6f, 4f), exitColor);
        // Label
        string exitLabel = exitIsExplicit ? $"Exit {data.exitWindowSeconds:F2}s" : $"Exit (auto {data.EffectiveExitSeconds:F2}s)";
        var miniStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 };
        miniStyle.normal.textColor = exitColor;
        EditorGUI.LabelField(new Rect(exX + 3f, row3y, 90f, row3h), exitLabel, miniStyle);

        Vector2 mp = Event.current.mousePosition;

        // Clip exit time marker — purple vertical line (row 3)
        if (hasClipExit)
        {
            Color clipExitColor = new Color(0.7f, 0.3f, 1f, 1f);
            float ctX = rect.x + rect.width * cT;
            EditorGUI.DrawRect(new Rect(ctX - 1f, row3y, 2f, row3h), clipExitColor);
            EditorGUI.DrawRect(new Rect(ctX - 3f, row3y, 6f, 4f), clipExitColor);
            var clipExitStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 };
            clipExitStyle.normal.textColor = clipExitColor;
            EditorGUI.LabelField(new Rect(ctX + 3f, row3y, 90f, row3h), $"ClipExit {data.clipExitTime:F2}s", clipExitStyle);
            if (new Rect(ctX - 6f, row3y, 12f, row3h).Contains(mp))
                GUI.Label(new Rect(mp.x + 10, mp.y - 18, 260, 18), $"Clip Exit: {data.clipExitTime:F2}s — crossfades to Idle (drag to adjust)", EditorStyles.helpBox);
        }

        // Row labels (left side)
        var rowLabelStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8 };
        rowLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        EditorGUI.LabelField(new Rect(rect.x + 2, row1y, 36, row1h), "Hitbox",  rowLabelStyle);
        EditorGUI.LabelField(new Rect(rect.x + 2, row2y, 36, row2h), "Combo",   rowLabelStyle);

        // Tooltips
        if (hR.Contains(mp)) GUI.Label(new Rect(mp.x + 10, mp.y - 18, 220, 18), $"Hitbox: {data.hitboxStartSeconds:F2}s → {data.hitboxEndSeconds:F2}s",              EditorStyles.helpBox);
        if (cR.Contains(mp)) GUI.Label(new Rect(mp.x + 10, mp.y - 18, 260, 18), $"Combo Window: {data.comboWindowStartSeconds:F2}s → {data.comboWindowEndSeconds:F2}s", EditorStyles.helpBox);
        if (new Rect(exX - 6f, row3y, 12f, row3h).Contains(mp))
            GUI.Label(new Rect(mp.x + 10, mp.y - 18, 260, 18),
                exitIsExplicit
                    ? $"Exit Window: {data.exitWindowSeconds:F2}s  (drag to adjust)"
                    : $"Exit Window: auto = comboWindowEnd ({data.EffectiveExitSeconds:F2}s)  (drag to set explicit)",
                EditorStyles.helpBox);

        if (so != null)
        {
            // Hitbox handles (row 1)
            DrawHandle(rect.x + rect.width * hS,        row1y, 6f, row1h, Color.yellow);
            DrawHandle(rect.x + rect.width * (hS + hW), row1y, 6f, row1h, Color.yellow);
            // Combo handles (row 2)
            DrawHandle(rect.x + rect.width * cS,        row2y, 6f, row2h, Color.cyan);
            DrawHandle(rect.x + rect.width * (cS + cW), row2y, 6f, row2h, Color.cyan);
            // Exit handle (row 3)
            DrawHandle(exX, row3y, 6f, row3h, exitColor);
            // Clip exit handle (row 3, purple)
            if (hasClipExit)
            {
                float ctX = rect.x + rect.width * cT;
                DrawHandle(ctX, row3y, 6f, row3h, new Color(0.7f, 0.3f, 1f, 1f));
            }

            ProcessDragHandle(idHS, rect, duration, so, "hitboxStartSeconds",      0f,                              data.hitboxEndSeconds - 0.01f);
            ProcessDragHandle(idHE, rect, duration, so, "hitboxEndSeconds",        data.hitboxStartSeconds + 0.01f, duration);
            ProcessDragHandle(idCS, rect, duration, so, "comboWindowStartSeconds", 0f,                              data.comboWindowEndSeconds - 0.01f);
            ProcessDragHandle(idCE, rect, duration, so, "comboWindowEndSeconds",   data.comboWindowStartSeconds + 0.01f, duration);
            ProcessDragHandle(idEX, rect, duration, so, "exitWindowSeconds",       data.comboWindowStartSeconds,    duration);
            ProcessDragHandle(idCT, rect, duration, so, "clipExitTime",             data.recoveryTime > 0f ? data.recoveryTime : 0f, duration);
        }

        EditorGUI.LabelField(new Rect(rect.x + 2,     rect.y + 2, 30, 14), "0s",              EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.xMax - 38,  rect.y + 2, 38, 14), $"{duration:F2}s", EditorStyles.miniLabel);

        if (rect.Contains(Event.current.mousePosition) && (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag))
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        DrawStaticLegend("Startup",  new Color(0.5f, 0.5f, 1f));
        DrawStaticLegend("Impact",   new Color(1f, 0.4f, 0.4f));
        DrawStaticLegend("Recovery", new Color(0.4f, 0.9f, 0.5f));
        DrawStaticLegend($"Hitbox  {data.hitboxStartSeconds:F2}→{data.hitboxEndSeconds:F2}s",              new Color(1f,  0.4f, 0.4f));
        DrawStaticLegend($"Combo  {data.comboWindowStartSeconds:F2}→{data.comboWindowEndSeconds:F2}s",    new Color(0.4f, 1f, 0.4f));
        DrawStaticLegend($"Exit  {data.EffectiveExitSeconds:F2}s{(exitIsExplicit ? "" : " (auto)")}",      new Color(1f, 0.75f, 0f));
        EditorGUILayout.EndHorizontal();

        if (so != null)
            EditorGUILayout.LabelField("<- yellow: hitbox  |  cyan: combo window  |  orange: exit ->",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel));
    }

    #endregion

    #region Handle Helpers

    internal static void DrawHandle(float x, float y, float w, float h, Color color)
    {
        EditorGUI.DrawRect(new Rect(x - w * 0.5f, y, w, h), color);
        EditorGUIUtility.AddCursorRect(new Rect(x - w * 0.5f - 2f, y, w + 4f, h), MouseCursor.ResizeHorizontal);
    }

    internal static void ProcessDragHandle(int id, Rect tl, float dur, SerializedObject so, string prop, float min, float max)
    {
        var p = so.FindProperty(prop);
        if (p == null) return;
        float hx = tl.x + tl.width * Mathf.Clamp01(p.floatValue / dur);
        Rect hz = new Rect(hx - 5f, tl.y, 10f, tl.height);
        Event e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (hz.Contains(e.mousePosition) && e.button == 0) { GUIUtility.hotControl = id; e.Use(); }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    float t = Mathf.Clamp((e.mousePosition.x - tl.x) / tl.width, 0f, 1f);
                    p.floatValue = (float)System.Math.Round(Mathf.Clamp(t * dur, min, max), 3);
                    so.ApplyModifiedProperties();
                    GUI.changed = true;
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                break;
        }
    }

    private static void DrawStaticBand(Rect rect, float start, float end, float duration, float by, float bh, Color color)
    {
        if (duration <= 0f) return;
        EditorGUI.DrawRect(
            new Rect(rect.x + rect.width * Mathf.Clamp01(start / duration), by,
                     rect.width * Mathf.Clamp01((end - start) / duration), bh), color);
    }

    #endregion
}
