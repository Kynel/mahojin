# AGENTS.md — Duckov-like Top-Down Shooter + Rune Drawing Magic (Prototype)

## Project One-liner
Top-down WASD shooter where LMB Down/Drag/Up drives magic-circle input, recognizes one-stroke runes, and casts spells.

## Target & Scope
- Platform: PC
- Unity: Latest LTS (Universal 3D / URP)
- Prototype scope: PvE only, single arena scene, 5–10 minutes playable loop.
- Visuals: clean simple cartoon primitives (cubes/capsules). Placeholder VFX/Audio OK.
- Non-goals (prototype): networking/PvP, deep economy, complex story systems, high-fidelity art.

## Core Fantasy (what must feel great)
Player moves while quickly drawing runes to chain/cycle spells; casting feedback (VFX/SFX/hit feel) should be satisfying and readable.

## Controls (must match)
- WASD: move
- Mouse: aim (cursor-based)
- LMB Down: acquire AimLock (Enemy under cursor first, else Ground point)
- LMB Drag: draw magic-circle stroke
- LMB Up: classify rune -> cast spell immediately

## “Done” Criteria for the Prototype (must all work)
1) Scene `Prototype_Arena` runs with a controllable player (WASD + mouse aim)
2) LMB drag enters Rune Draw Mode, captures mouse stroke points, renders stroke line
3) On LMB release, rune is recognized from 5–8 templates (start with 3)
4) A spell is cast based on rune result with obvious feedback (projectile/AoE/etc.)
5) Enemies spawn, chase/attack, can be damaged and killed
6) Player has HP, can die, and restart loop is functional (quick reset)

## Key Technical Decisions (do not change without strong reason)
- Use **New Input System**
- Keep **one scene** for the whole prototype: `Prototype_Arena`
- Rune recognition: **$1 Unistroke Recognizer** (normalize stroke, choose best match)
- Spell system should be **data-driven** via ScriptableObjects:
  - RuneId -> SpellDefinition(SO) -> runtime Spell behavior
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
  - Runes/
  - Spells/
  - Combat/
  - AI/
  - UI/
- ScriptableObjects/
  - Runes/
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
  - Current state (Normal vs Rune Draw)
  - Recognized rune name + score
  - Cooldowns (if any)
- Fail gracefully:
  - If stroke is too short, treat as "Line" or default basic spell (no hard fail)
  - Recognition should default to best match (prototype should not block casting)
- Legacy note:
  - Space 기반 룬 드로잉/캐스팅 입력은 제거됨

## Immediate Roadmap (P0 order)
1) Player movement + aim
2) Rune draw mode + stroke capture + line rendering
3) $1 recognizer + 3 rune templates + 1 spell (Firebolt)
4) Enemy spawn + chase + damage loop
5) Expand to 5–8 runes + 4 spells + basic VFX feedback

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
