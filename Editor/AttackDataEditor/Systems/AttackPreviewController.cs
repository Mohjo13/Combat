using UnityEngine;
using UnityEditor;

// Scene preview system: animation sampling, transport controls,
// scrub tracks, and scene-view gizmo overlay.
public partial class AttackDataEditor
{
    #region Preview State

    private bool       _previewActive  = false;
    private float      _previewTime    = 0f;
    private bool       _previewPlaying = false;
    private double     _previewPlayStart = 0;
    private float      _previewPlayFrom  = 0f;
    private GameObject _previewTarget  = null;
    private WeaponBase _previewWeapon  = null;
    private Collider   _previewCollider = null;

    private void OnDisable() => StopPreview();

    #endregion

    #region DrawScenePreview

    private void DrawScenePreview(AttackData data)
    {
        EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);

        if (data.clip == null)
        {
            EditorGUILayout.HelpBox("Assign a clip above to enable scene preview.", MessageType.None);
            return;
        }

        EditorGUI.BeginChangeCheck();
        _previewTarget = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Preview Target", "Drag the player GameObject from the Hierarchy."),
            _previewTarget, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck()) { StopPreview(); if (_previewTarget != null) CacheWeaponComponents(); }

        if (_previewTarget == null)
        { EditorGUILayout.HelpBox("Drag the player GameObject from the Hierarchy.", MessageType.Info); return; }
        if (_previewWeapon == null) CacheWeaponComponents();
        if (_previewWeapon == null) EditorGUILayout.HelpBox("No WeaponBase found — hitbox gizmos unavailable.", MessageType.Warning);

        float realDur = data.HasPhaseOverrides ? data.RemappedDuration : data.ActiveDuration;

        if (_previewPlaying)
        {
            _previewTime = _previewPlayFrom + (float)(EditorApplication.timeSinceStartup - _previewPlayStart);
            if (_previewTime >= realDur) { _previewTime = realDur; _previewPlaying = false; }
            Repaint();
        }

        // Transport bar
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⏮", GUILayout.Width(32))) { _previewTime = 0f;       _previewPlaying = false; SampleScene(data); }
        if (GUILayout.Button("<",  GUILayout.Width(28)))  StepFrame(data, -1);
        if (_previewPlaying)
        { if (GUILayout.Button("▐ ▌", GUILayout.Width(28))) _previewPlaying = false; }
        else
        {
            if (GUILayout.Button("▶", GUILayout.Width(35)))
            {
                if (_previewTime >= realDur) _previewTime = 0f;
                _previewPlayFrom = _previewTime; _previewPlayStart = EditorApplication.timeSinceStartup;
                _previewPlaying = true;
                if (!_previewActive) StartPreview(data);
            }
        }
        if (GUILayout.Button(">",  GUILayout.Width(28)))  StepFrame(data, +1);
        if (GUILayout.Button("⏭",  GUILayout.Width(32))) { _previewTime = realDur;  _previewPlaying = false; SampleScene(data); }
        GUILayout.FlexibleSpace();
        bool wasActive = _previewActive;
        GUI.backgroundColor = _previewActive ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.3f, 0.75f, 0.3f);
        _previewActive = GUILayout.Toggle(_previewActive, _previewActive ? "Stop" : "Preview",
            EditorStyles.miniButton, GUILayout.Width(72));
        GUI.backgroundColor = Color.white;
        if (_previewActive != wasActive) { if (_previewActive) StartPreview(data); else StopPreview(); }
        EditorGUILayout.EndHorizontal();

        if (_previewActive) SampleScene(data);

        // Phase label
        if (_previewActive)
        {
            (string phaseName, PhaseSpeed speed) = GetPhaseInfo(data, _previewTime);
            Color phaseCol = phaseName == "STARTUP"  ? new Color(0.5f, 0.6f, 1f)  :
                             phaseName == "IMPACT"   ? new Color(1f, 0.4f, 0.4f)  :
                                                       new Color(0.4f, 0.9f, 0.5f);
            var ps = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            ps.normal.textColor = phaseCol;
            string speedLabel = data.HasPhaseOverrides ? $"  [{speed}  {speed.ToMultiplier():F2}x]" : "";
            EditorGUILayout.LabelField($"{phaseName}{speedLabel}  {_previewTime:F2}s / {realDur:F2}s", ps);
        }

        // Desired-phase track (click to scrub)
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Desired phase time — click to scrub", new GUIStyle(EditorStyles.miniLabel));
        Rect desTrack = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(desTrack, new Color(0.15f, 0.15f, 0.15f));
        float clipLen = data.clip != null ? data.clip.length : data.totalDuration;

        if (data.HasPhaseOverrides)
        {
            float realS = data.impactTime * data.startupSpeed.ToMultiplier();
            float realI = (data.recoveryTime - data.impactTime) * data.impactSpeed.ToMultiplier();
            float realR = (clipLen - data.recoveryTime) * data.recoverySpeed.ToMultiplier();
            float desTot = realS + realI + realR; if (desTot <= 0f) desTot = realDur;
            float fS2 = realS / desTot; float fI2 = realI / desTot;

            EditorGUI.DrawRect(new Rect(desTrack.x,                            desTrack.y, desTrack.width * fS2,            desTrack.height), ColStripStartup);
            EditorGUI.DrawRect(new Rect(desTrack.x + desTrack.width * fS2,    desTrack.y, desTrack.width * fI2,            desTrack.height), ColStripImpact);
            EditorGUI.DrawRect(new Rect(desTrack.x + desTrack.width*(fS2+fI2),desTrack.y, desTrack.width*(1f-fS2-fI2),    desTrack.height), ColStripRecovery);

            float hS3 = Mathf.Clamp01(data.hitboxStartSeconds / desTot);
            float hW3 = Mathf.Clamp01((data.hitboxEndSeconds - data.hitboxStartSeconds) / desTot);
            EditorGUI.DrawRect(new Rect(desTrack.x + desTrack.width * hS3, desTrack.y, desTrack.width * hW3, desTrack.height * 0.4f), new Color(0.95f, 0.2f, 0.2f, 0.9f));
            float cS3 = Mathf.Clamp01(data.comboWindowStartSeconds / desTot);
            float cW3 = Mathf.Clamp01((data.comboWindowEndSeconds - data.comboWindowStartSeconds) / desTot);
            EditorGUI.DrawRect(new Rect(desTrack.x + desTrack.width * cS3, desTrack.y + desTrack.height * 0.6f, desTrack.width * cW3, desTrack.height * 0.4f), new Color(0.2f, 0.95f, 0.2f, 0.9f));

            EditorGUI.DrawRect(new Rect(desTrack.x + desTrack.width * fS2          - 1f, desTrack.y, 2f, desTrack.height), new Color(1, 1, 1, 0.7f));
            EditorGUI.DrawRect(new Rect(desTrack.x + desTrack.width * (fS2 + fI2)  - 1f, desTrack.y, 2f, desTrack.height), new Color(1, 1, 1, 0.7f));

            float desHeadX = desTrack.x + desTrack.width * Mathf.Clamp01(_previewTime / desTot);
            EditorGUI.DrawRect(new Rect(desHeadX - 1.5f, desTrack.y - 2f, 3f, desTrack.height + 4f), Color.white);

            Event ev = Event.current;
            if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && ev.button == 0 && desTrack.Contains(ev.mousePosition))
            {
                _previewTime = Mathf.Clamp01((ev.mousePosition.x - desTrack.x) / desTrack.width) * desTot;
                _previewPlaying = false;
                if (!_previewActive) StartPreview(data);
                SampleScene(data); ev.Use(); Repaint();
            }
        }
        else
        {
            EditorGUI.DrawRect(desTrack, ColStripDisabled);
            float desHeadX = desTrack.x + desTrack.width * Mathf.Clamp01(_previewTime / Mathf.Max(realDur, 0.001f));
            EditorGUI.DrawRect(new Rect(desHeadX - 1.5f, desTrack.y - 2f, 3f, desTrack.height + 4f), Color.white);
            Event ev = Event.current;
            if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && ev.button == 0 && desTrack.Contains(ev.mousePosition))
            {
                _previewTime = Mathf.Clamp01((ev.mousePosition.x - desTrack.x) / desTrack.width) * realDur;
                _previewPlaying = false;
                if (!_previewActive) StartPreview(data);
                SampleScene(data); ev.Use(); Repaint();
            }
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        DrawStaticLegend("Hitbox", new Color(1f, 0.4f, 0.4f));
        DrawStaticLegend("Combo",  new Color(0.4f, 1f, 0.4f));
        EditorGUILayout.EndHorizontal();
        if (_previewActive) EditorGUILayout.LabelField("Scene view updates as you scrub  ^", EditorStyles.centeredGreyMiniLabel);
    }

    #endregion

    #region Preview Controller

    private void CacheWeaponComponents()
    {
        _previewWeapon   = _previewTarget.GetComponentInChildren<WeaponBase>();
        _previewCollider = _previewWeapon?.HitboxCollider;
    }

    private void StartPreview(AttackData data)
    {
        if (_previewTarget == null) return;
        CacheWeaponComponents();
        AnimationMode.StartAnimationMode();
        _previewActive = true;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        SampleScene(data);
    }

    private void StopPreview()
    {
        _previewPlaying = false;
        _previewActive  = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        SceneView.RepaintAll();
    }

    internal float RemapTimeToClip(AttackData data, float realT)
    {
        if (!data.HasPhaseOverrides) return realT;
        float clipLen = data.clip != null ? data.clip.length : data.totalDuration;
        float natS = data.impactTime; float natI = data.recoveryTime - data.impactTime; float natR = clipLen - data.recoveryTime;
        float desS = natS * data.startupSpeed.ToMultiplier(); float desI = natI * data.impactSpeed.ToMultiplier(); float desR = natR * data.recoverySpeed.ToMultiplier();
        if (realT <= desS) return desS > 0f ? (realT / desS) * natS : 0f;
        if (realT <= desS + desI) return data.impactTime + (desI > 0f ? ((realT - desS) / desI) * natI : 0f);
        return data.recoveryTime + (desR > 0f ? ((realT - desS - desI) / desR) * natR : 0f);
    }

    private void SampleScene(AttackData data)
    {
        if (_previewTarget == null || data.clip == null) return;
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        float clipTime = Mathf.Clamp(RemapTimeToClip(data, _previewTime), 0f, data.clip.length);
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_previewTarget, data.clip, clipTime);
        AnimationMode.EndSampling();
        SceneView.RepaintAll();
    }

    private void StepFrame(AttackData data, int dir)
    {
        float dur = data.HasPhaseOverrides ? data.RemappedDuration : data.ActiveDuration;
        _previewTime = Mathf.Clamp(_previewTime + dir * (1f / 30f), 0f, dur);
        _previewPlaying = false;
        if (!_previewActive) StartPreview(data);
        SampleScene(data);
    }

    private static (string name, PhaseSpeed speed) GetPhaseInfo(AttackData data, float t)
    {
        if (!data.HasPhaseOverrides) return ("--", PhaseSpeed.Normal);
        float desS = data.impactTime * data.startupSpeed.ToMultiplier();
        float desI = (data.recoveryTime - data.impactTime) * data.impactSpeed.ToMultiplier();
        if (t < desS) return ("STARTUP", data.startupSpeed);
        if (t < desS + desI) return ("IMPACT", data.impactSpeed);
        return ("RECOVERY", data.recoverySpeed);
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sv)
    {
        if (!_previewActive || _previewTarget == null) return;
        AttackData data = (AttackData)target;

        bool hitboxOn = _previewTime >= data.hitboxStartSeconds && _previewTime <= data.hitboxEndSeconds;
        bool comboOn  = _previewTime >= data.comboWindowStartSeconds && _previewTime <= data.comboWindowEndSeconds;

        if (hitboxOn && _previewCollider != null)
        {
            Handles.color = new Color(1f, 0.15f, 0.15f, 0.9f);
            BoxCollider box = _previewCollider as BoxCollider;
            if (box != null)
            {
                Matrix4x4 prev = Handles.matrix;
                Handles.matrix = box.transform.localToWorldMatrix;
                Handles.DrawWireCube(box.center, box.size);
                Handles.matrix = prev;
            }
            else Handles.DrawWireDisc(_previewCollider.bounds.center, Vector3.up, _previewCollider.bounds.extents.magnitude);
        }

        if (comboOn && _previewWeapon != null)
        {
            Handles.color = new Color(0.2f, 1f, 0.2f, 0.6f);
            Handles.DrawWireDisc(_previewWeapon.transform.position, Vector3.up,     0.35f);
            Handles.DrawWireDisc(_previewWeapon.transform.position, Vector3.forward, 0.35f);
        }

        if (_previewWeapon != null)
        {
            (string phaseName, PhaseSpeed speed) = GetPhaseInfo(data, _previewTime);
            Color phaseCol = phaseName == "STARTUP" ? new Color(0.5f, 0.6f, 1f) :
                             phaseName == "IMPACT"  ? new Color(1f, 0.4f, 0.4f) :
                                                      new Color(0.4f, 0.9f, 0.5f);
            var ls = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 13 };
            ls.normal.textColor = phaseCol;
            string label = data.HasPhaseOverrides
                ? $"# {phaseName}  [{speed}]  {_previewTime:F2}s"
                : $"# {_previewTime:F2}s";
            Handles.Label(_previewWeapon.transform.position + Vector3.up * 2.2f, label, ls);
        }

        if (hitboxOn && _previewCollider != null)
        {
            var hs = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 12 };
            hs.normal.textColor = Color.red;
            Handles.Label(_previewCollider.bounds.center + Vector3.up * 0.5f, "HITBOX ACTIVE", hs);
        }

        sv.Repaint();
    }

    #endregion
}
