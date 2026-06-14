using UnityEngine;
using UnityEditor;

// Shared color constants, speed preset data, and static utility helpers
// used across all AttackDataEditor partial files.
public partial class AttackDataEditor
{
    #region Colors

    internal static readonly Color ColStripStartup  = new Color(0.30f, 0.50f, 0.90f, 0.80f);
    internal static readonly Color ColStripImpact   = new Color(0.90f, 0.25f, 0.25f, 0.80f);
    internal static readonly Color ColStripRecovery = new Color(0.25f, 0.72f, 0.35f, 0.80f);
    internal static readonly Color ColStripDisabled = new Color(0.35f, 0.35f, 0.35f, 0.35f);

    #endregion

    #region Speed Preset Table

    public struct SpeedPresetEntry
    {
        public PhaseSpeed value;
        public string     name;
        public string     mult;
        public Color      bgCol;
        public Color      textCol;
    }

    public static SpeedPresetEntry[] BuildSpeedPresets() => new SpeedPresetEntry[]
    {
        new SpeedPresetEntry { value=PhaseSpeed.Sluggish,   name="Sluggish",   mult="x1.50", bgCol=new Color(0.90f,0.37f,0.37f,1f), textCol=new Color(1f,0.88f,0.88f,1f) },
        new SpeedPresetEntry { value=PhaseSpeed.Leisurely,  name="Leisurely",  mult="x1.25", bgCol=new Color(0.85f,0.52f,0.25f,1f), textCol=new Color(1f,0.93f,0.85f,1f) },
        new SpeedPresetEntry { value=PhaseSpeed.Deliberate, name="Deliberate", mult="x1.10", bgCol=new Color(0.75f,0.70f,0.20f,1f), textCol=new Color(1f,0.98f,0.80f,1f) },
        new SpeedPresetEntry { value=PhaseSpeed.Normal,     name="Normal",     mult="x1.00", bgCol=new Color(0.20f,0.60f,0.30f,1f), textCol=new Color(0.88f,1f,0.90f,1f)  },
        new SpeedPresetEntry { value=PhaseSpeed.Rapid,      name="Rapid",      mult="x0.90", bgCol=new Color(0.22f,0.55f,0.80f,1f), textCol=new Color(0.88f,0.95f,1f,1f)  },
        new SpeedPresetEntry { value=PhaseSpeed.Quick,      name="Quick",      mult="x0.80", bgCol=new Color(0.28f,0.35f,0.80f,1f), textCol=new Color(0.88f,0.90f,1f,1f)  },
        new SpeedPresetEntry { value=PhaseSpeed.Flash,      name="Flash",      mult="x0.67", bgCol=new Color(0.45f,0.22f,0.85f,1f), textCol=new Color(0.95f,0.88f,1f,1f)  },
    };

    #endregion

    #region Shared Helpers

    // Effective multiplier: preset base * (1 + fine-tune offset)
    public static float EffMult(PhaseSpeed preset, float tweak)
        => preset.ToMultiplier() * (1f + tweak);

    internal static string GetGuid(AttackData data)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data, out string guid, out long _);
        return guid ?? "";
    }

    internal static void DrawStaticLegend(string text, Color color)
    {
        GUILayout.Label(text, new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color } });
    }

    #endregion
}
