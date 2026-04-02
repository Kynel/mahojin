# AGENTS.md — Duckov-like Top-Down Shooter + Magic Circle Casting (Current Prototype)

## Project One-liner
Top-down WASD shooter where RMB enters casting mode, LMB draws a one-stroke magic circle, and release resolves the closest circle to cast a spell.

## Target & Scope
- Platform: PC
- Unity: Latest LTS (Universal 3D / URP)
- Prototype scope: PvE only, single arena scene, 5–10 minutes playable loop.
- Visuals: clean simple cartoon primitives (cubes/capsules). Placeholder VFX/Audio OK.
- Non-goals (prototype): networking/PvP, deep economy, complex story systems, high-fidelity art.

## Core Fantasy (what must feel great)
Player moves while quickly locking aim, drawing magic circles, and chaining spells; casting feedback (VFX/SFX/hit feel) should be satisfying and readable.

## Controls (must match)
- WASD: move
- Mouse: aim (cursor-based)
- RMB Down: acquire AimLock (Enemy under cursor first, else Ground point) + show MagicCircle UI
- LMB Hold/Drag: draw magic-circle stroke
- LMB Up or RMB Up: submit stroke -> classify closest magic circle -> cast if recognition/cast gates pass

## “Done” Criteria for the Prototype (must all work)
1) Scene `Prototype_Arena` runs with a controllable player (WASD + mouse aim)
2) RMB enters Magic Circle Cast Mode, acquires AimLock, and shows the draw UI
3) LMB drag captures one-stroke points, renders the stroke line, and release submits the stroke
4) Submitted stroke is matched against the current 3 templates (`Water`, `Fire`, `Lightning`) and casts the linked spell when recognition/cast gates pass
5) Enemies spawn, chase/attack, can be damaged and killed
6) Player has HP/MP, can die, auto-respawns, and restart loop is functional from HUD

## Key Technical Decisions (do not change without strong reason)
- Use **New Input System**
- Keep **one build scene** for the prototype: `Prototype_Arena`
- Recognition uses the current **Magic Circle matcher**:
  - resample + normalize + optional reverse + best-fit rotation + RMSE score
- Spell system should be **data-driven** via ScriptableObjects:
  - MagicCircleId -> SpellDefinition(SO) -> runtime Spell behavior
- Prefer small scripts; avoid overengineering

## Implementation Guidelines
- Prioritize shipping the vertical slice over architecture polish.
- Each task/commit should keep the project playable.
- Avoid long “framework” work: build only what the next feature needs.
- When adding a system:
  - Provide a minimal working version
  - Include an in-editor setup note (what components to add / scene wiring)

## Code Style & Structure (recommended)
Assets/_Project/
- Scenes/
- Scripts/
  - Core/ (game loop, state, utilities)
  - Player/
  - Input/
  - MagicCircles/
  - Spells/
  - Combat/
  - AI/
  - UI/
- ScriptableObjects/
  - MagicCircles/
  - Spells/
  - Enemies/
- Prefabs/
- VFX/ (placeholder)

General:
- Use namespaces like `DuckovProto.*`
- Prefer explicit fields + `[SerializeField]` over Find-by-string.
- Use interfaces only when needed; keep it simple.

## Testing & Debugging Expectations
- Add lightweight debug overlays where useful:
  - Current state (Normal vs Magic Circle Draw)
  - Recognized magic circle name + score
  - Cooldowns (if any)
- Current fail behavior:
  - If stroke is too short, show `Too short` and close the draw UI
  - If best score is below threshold, show `Unclear XX%/YY%` and do not cast
- Legacy note:
  - Space 기반 룬 드로잉/캐스팅 입력은 제거됨
  - old `Runes` / `Sigil` path는 제거되고 `MagicCircles` 경로만 유지됨

## Immediate Roadmap (P0 order)
1) `Prototype_Arena` Play Mode에서 실제 draw/cast feel 검증
2) water/fire/lightning `passThreshold` 실전값 미세 조정
3) `MagicCircleGuidePanel` / HUD spacing 마감
4) 필요 시 magic circle / spell 확장

## Project State Snapshot (REQUIRED)
To keep collaboration efficient, maintain a single **minified** markdown snapshot of the current implementation state.

### File
- Always create/update: `PROJECT_STATE_MIN.md` (repo root)

### When to update
- Update `PROJECT_STATE_MIN.md` after **each completed task** (or any meaningful change).
- Keep it accurate to what is actually in the repo/scene/prefabs, not “planned”.

### Format rules (minify)
- No prose paragraphs. Prefer short lines.
- One bullet = one fact.
- Include file paths and object names.
- Do NOT paste long code blocks. (Only mention filenames + key public fields/events.)
- Keep it small: aim <= 120 lines total.

### Must include sections (in this order)
- `env` (unity version, render pipeline, input system)
- `scenes` (scene names + purpose)
- `prefabs` (important prefabs + components)
- `scripts` (key scripts + responsibilities + key serialized fields)
- `wiring` (critical references: layer masks, tags, input actions, inspector hookups)
- `done` (checked items)
- `wip` (current in-progress)
- `todo` (next 3–10 items)
- `known_issues` (bugs/oddities + repro if possible)

### Ground truth
- Prefer writing what is verifiable in project assets/scene.
- If unsure, mark as `?` rather than guessing.
