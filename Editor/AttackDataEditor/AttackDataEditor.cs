using UnityEngine;
using UnityEditor;

// Main entry point. All draw logic is split across partial files:
//   Drawers/PhaseSectionDrawer.cs   — phase speed UI + fine-tune sliders
//   Drawers/TimelineDrawer.cs       — clip strip + drag-handle timeline
//   Systems/AttackPreviewController.cs — scene preview + gizmo
//   Styles/AttackEditorStyles.cs    — color constants, speed presets, shared helpers

[CustomEditor(typeof(AttackData))]
public partial class AttackDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        AttackData data = (AttackData)target;
        LoadTweaks(data);

        // ── Identity ──────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isDodgeAttack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("clip"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

        EditorGUILayout.Space(4);
        if (data.clip != null)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField("Clip Length (s)", data.clip.length);
            EditorGUI.EndDisabledGroup();
            if (Mathf.Abs(data.clip.length - data.totalDuration) > 0.05f)
                EditorGUILayout.HelpBox(
                    $"Clip length ({data.clip.length:F2}s) differs from Total Duration ({data.totalDuration:F2}s). Clip length is used at runtime.",
                    MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("No clip assigned — timing runs on Total Duration.", MessageType.Info);
        }

        // ── Duration ──────────────────────────────────────────────────────────
        // totalDuration intentionally hidden — designers never need to change it.

        // ── Attack ────────────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Attack", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("damageMultiplier"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("staggerStrength"));

        // ── Stamina ───────────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Stamina", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("staminaCost"));

        // ── Lunge Override ────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Lunge Override", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useCustomLunge"), new GUIContent("Use Custom Lunge"));
        if (data.useCustomLunge)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("customLungeMultiplier"), new GUIContent("Lunge Multiplier"));

        // ── Hitbox Timing ─────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Hitbox Timing (seconds)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hitboxStartSeconds"), new GUIContent("Hitbox Opens At (s)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hitboxEndSeconds"),   new GUIContent("Hitbox Closes At (s)"));
        if (data.hitboxEndSeconds <= data.hitboxStartSeconds)
            EditorGUILayout.HelpBox("Hitbox End must be greater than Hitbox Start.", MessageType.Error);

        // ── Combo Window ──────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Combo Window (seconds)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("comboWindowStartSeconds"), new GUIContent("Combo Window Opens At (s)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("comboWindowEndSeconds"),   new GUIContent("Combo Window Closes At (s)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("exitWindowSeconds"),
            new GUIContent("Exit Window (s)", "When the crossfade to the next attack fires. 0 = auto (uses Combo Window End)."));
        if (data.exitWindowSeconds > 0f && data.exitWindowSeconds < data.comboWindowStartSeconds)
            EditorGUILayout.HelpBox("Exit Window is before Combo Window Start — input can't be buffered yet.", MessageType.Warning);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crossfadeDuration"),
            new GUIContent("Crossfade Duration (s)", "Blend time when chaining into this attack."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("clipExitTime"),
            new GUIContent("Clip Exit Time (s)", "Crossfade to Idle at this time. 0 = play full clip."));
        if (data.clipExitTime > 0f && data.clipExitTime <= data.recoveryTime)
            EditorGUILayout.HelpBox("Clip Exit Time is before or at Recovery — player may be cut off mid-attack.", MessageType.Warning);
        if (data.comboWindowEndSeconds <= data.comboWindowStartSeconds)
            EditorGUILayout.HelpBox("Combo Window End must be greater than Start.", MessageType.Error);
        if (data.comboWindowEndSeconds > data.ActiveDuration)
            EditorGUILayout.HelpBox($"Combo Window End ({data.comboWindowEndSeconds:F2}s) exceeds attack duration ({data.ActiveDuration:F2}s).", MessageType.Warning);

        // ── Phase Speed ───────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        DrawPhaseSection(data);

        // ── Timeline ──────────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUI.BeginChangeCheck();
        DrawSingleTimeline(data, serializedObject);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            if (_previewActive) SceneView.RepaintAll();
        }

        // ── Scene Preview ─────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        DrawScenePreview(data);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Open Combo Timing Tool", GUILayout.Height(28)))
            AttackTimingToolWindow.OpenFromMenu();

        serializedObject.ApplyModifiedProperties();
    }
}
