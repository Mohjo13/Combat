
> PSX-inspired melee action game built in Unity 6 (HDRP)

A student game project developed during internship at Södertörns högskola. My contributions focused on programming architecture for the combat system, editor tooling, and VFX/UI systems.

---

## 🗂️ My Contributions

### Combat Architecture
- **Event-driven Mediator** — all inter-system communication routes through `CombatEvents.cs`. No direct script-to-script references; systems are fully decoupled.
- **ScriptableObject data pipeline** — attack data, combo sequences, and weapon loadouts defined as SOs, editable without touching code.
- **Coroutine-driven attack flow** — `AttackAnimationDriver` manages phase transitions (startup → impact → recovery), clip exit timing, and crossfades.

### Combo System (`ComboHandler.cs`)
- Multi-branch sequence resolution — active sequence checked first to prevent list-order matching bugs
- Buffered input during attack windows — `_pendingStepIndex` prevents step skipping on fast inputs
- Wrong-input detection fires `RaiseComboFailed` and resets state cleanly

### Parry System (`ParryHandler.cs`)
- Merged from two separate handlers (`ParryWindowHandler` + `ParryBoxHandler`) into a single unified script
- Fires one event to `CombatEvents`; `EnemyAI` handles stagger internally via `ParryStaggerRoutine`

### Dodge Attack System
- Buffers light/heavy attack input during the dodge animation window
- Input committed at dodge completion via `PlayerCombatActions.CommitDodgeAttack`
- Uses Unity's New Input System throughout

### StreakUI — Combo HUD
- Icons fly from a spawn origin into row slots using DOTween tweens
- Rows group into sets of three; completed rows float away on next input or fall on timeout
- Combo name text animates in via a custom TMP dissolve shader
- `StreakVFXManager.cs` drives all VFX reactions (glow, shake, color drain) through events — no direct UI references

### Custom HLSL Shaders
- **`StreakGlow.shader`** — UI panel glow with exposed `_Alpha`, `_GlowColor`, `_GlowIntensity` properties
- **`Dissolver.shader`** (`TMP/PSXDissolve`) — procedural value noise dissolve for TextMeshPro, with posterized edge glow baked in (fullscreen PSX pass does not affect screen-space UI)

### Attack Timing Tool (Custom Editor)
Located in `Assets/Editor/AttackDataEditor/`

- Visual timeline scrubber with draggable phase markers (startup / impact / recovery / clip exit)
- Phase speed presets (Sluggish → Flash) with per-asset tweak offsets stored in `EditorPrefs`
- `impactTime` and `recoveryTime` editable via drag handles directly on the Runtime Feel bar
- Standalone `AttackTimingToolWindow` for editing any `AttackDataSO` asset outside the inspector

### VFX
- Hit particle system with dual object pool (`lightHitPrefab` / `heavyHitPrefab`)
- Sub-emitter floor splat on debris collision
- `UIPixelTrail.cs` — spawns trailing pixel squares behind flying combo icons

---

## 🛠️ Tech

| Area | Details |
|---|---|
| Engine | Unity 6000.3.12f1, HDRP |
| Language | C# |
| Shaders | Custom HLSL (UI canvas) |
| Animation | AnimatorOverrideController, coroutine-driven phase system |
| Tweening | DOTween |
| Input | Unity New Input System |
| Data | ScriptableObjects |
| Architecture | Event-driven Mediator (`CombatEvents.cs`) |

---

## 📁 Key Script Locations

```
Assets/
├── Scripts/
│   ├── Combat/
│   │   ├── Managers/        CombatEvents.cs
│   │   ├── Attacks/         AttackAnimationDriver.cs
│   │   ├── Combo/           ComboHandler.cs
│   │   ├── Player/          PlayerCombatActions.cs, PlayerLungeMotor.cs, ParryHandler.cs
│   │   ├── Data Containers/ AttackDataSO.cs
│   │   ├── Weapons/         WeaponLoadoutSO.cs
│   │   └── UI/              StreakUI.cs, StreakRowUI.cs, StreakIconUI.cs, StreakVFXManager.cs
│   ├── PlayerRelated/       PlayerMotion.cs
│   ├── UI/                  TMPDissolveDriver.cs, UIPixelTrail.cs
│   └── Enemy/               EnemyDamage.cs
├── Editor/
│   └── AttackDataEditor/    AttackDataEditor.cs, AttackTimingToolWindow.cs
│       ├── Drawers/         TimelineDrawer.cs, PhaseSectionDrawer.cs
│       └── Systems/         AttackPreviewController.cs
└── Prefabs/UI/UIShader/     StreakGlow.shader, Dissolver.shader
```

---

*Internship project — Södertörns högskola, Game Programming, 2026*
