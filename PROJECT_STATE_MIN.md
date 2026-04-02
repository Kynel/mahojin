# PROJECT_STATE_MIN

## env
- unity: `6000.3.10f1`
- render_pipeline: `URP`
- input_system: `New Input System`
- input_actions: `Assets/InputSystem_Actions.inputactions`

## scenes
- `Assets/_Project/Scenes/Prototype_Arena.unity`: only build/playable scene
- `Assets/Scenes/SampleScene.unity`: unused scene asset, not in build
- runtime_map: `PrototypeMapBootstrapper` builds `MapRoot` in arena

## prefabs
- `Assets/_Project/Prefabs/Player.prefab`: tag=`Player`, layer=`Player(6)`
- player_core: `PlayerMotor`, `PlayerAim`, `AimLockController`, `MagicCircleUI`, `MagicCircleDrawController`, `MagicCircleCastPipeline`, `MagicCircleCastController`, `SpellRunner`
- player_vitals: `Health`, `Mana`, `PlayerVitals`
- player_vision: `VisionPostFXBootstrapper`, `FOVConeMesh`, `EnemyVisibilityByFOV`
- `Assets/_Project/Prefabs/Enemy.prefab`: layer=`Enemy(8)`
- enemy_core: `Health`, `EnemyChaser`, `EnemyContactDamage`, `EnemyManaReward`

## scripts
- `Assets/_Project/Scripts/Combat/AimLockController.cs`: cursor 기준 enemy 우선, 없으면 ground point lock; target lock aim point는 collider center 기준
- `Assets/_Project/Scripts/Player/PlayerMotor.cs`: `InputActionReference Move` 기반 Rigidbody 이동 + wall slide + terrain follow
- `Assets/_Project/Scripts/Player/PlayerVitals.cs`: 사망 시 지연 리스폰 + HP/MP full reset
- `Assets/_Project/Scripts/Player/PlayerRuntimeLocator.cs`: player/vitals/health/mana 공용 조회 캐시
- `Assets/_Project/Scripts/MagicCircles/MagicCircleRuntimeLocator.cs`: pipeline/cast/draw/HUD canvas 공용 조회 캐시
- `Assets/_Project/Scripts/MagicCircles/MagicCircleDrawController.cs`: `RMB Down` cast mode + AimLock, `LMB Drag` stroke capture, `LMB/RMB Up` submit
- `Assets/_Project/Scripts/MagicCircles/MagicCircleCastController.cs`: mana/global/spell cooldown gate + cast state text + last magic circle/spell labels
- `Assets/_Project/Scripts/MagicCircles/MagicCircleCastPipeline.cs`: `MagicCircleLibrarySO` 전체 평가, threshold gate 포함 best circle 선택, `SpellLibrarySO` lookup, cast gate 연결, recent result cache, runtime asset auto-discovery 제거
- `Assets/_Project/Scripts/MagicCircles/Data/MagicCircleDefinitionSO.cs`: magic circle id/display/description/linkedSpellId/theme/reference stroke/threshold/normalize options + hint
- `Assets/_Project/Scripts/MagicCircles/Data/MagicCircleLibrarySO.cs`: magic circle 목록 + id lookup
- `Assets/_Project/Scripts/MagicCircles/Matching/StrokeMathSimple.cs`: arc-length resample, centroid/scale normalize, reverse, best-fit rotation, RMSE score
- `Assets/_Project/Scripts/MagicCircles/Matching/MagicCircleMatcher.cs`: user stroke vs reference stroke 직접 비교
- `Assets/_Project/Scripts/MagicCircles/Matching/MagicCircleMatchResult.cs`: score/pass/reason/aligned reference/aligned user cache
- `Assets/_Project/Scripts/AI/PrototypeMapBootstrapper.cs`: arena terrain/walls/spawn roots 런타임 생성
- `Assets/_Project/Scripts/AI/EnemySpawner.cs`: 초기/주기 spawn + terrain/spawn-point 기반 배치; defaults `maxAlive=8`, `initialSpawnCount=8`
- `Assets/_Project/Scripts/UI/MagicCircleGuidePanel.cs` + `.Layout.cs` + `.Presentation.cs`: 우측 중앙 가이드 패널, layout/build와 selection/presentation 분리
- `Assets/_Project/Scripts/UI/GameHUD.cs` + `.Bootstrap.cs` + `.Layout.cs`: HUD bootstrap/layout/runtime refs 분리
- `Assets/_Project/Scripts/UI/MagicCircleUI.cs`: cast circle + grid + border + user line + message
- `Assets/_Project/Scripts/Spells/SpellRunner.cs`: `Projectile/Hitscan/Channel` executor dispatch
- `Assets/_Project/Scripts/Spells/Executors/ProjectileExecutor.cs`: water projectile + 1 bounce
- `Assets/_Project/Scripts/Spells/Executors/ChannelExecutor.cs`: fire channel 2s + 3 ticks + aim lock 유지
- `Assets/_Project/Scripts/Spells/Executors/HitscanExecutor.cs`: lightning rail instant hit

## wiring
- layers: `Player=6`, `Ground=7`, `Enemy=8`, `Projectile=9`, `Wall=10`
- input_asset_actions: `Move`, `Look`, `Attack`, `Interact`, `Crouch`, `Jump`, `Previous`, `Next`, `Sprint`
- runtime_input_usage: movement=`InputActionReference Move`, casting/draw=`Mouse.current`
- aim_lock_hit_policy: cursor ray에서 wall은 cover로 유지, enemy가 wall에 가려지지 않으면 ground보다 enemy 우선 lock, wall 뒤 enemy direct lock 방지
- spell_aim_direction: spell executors는 실제 cast origin 높이에서 locked aim point 방향을 계산해 arena 고저차에서도 적 위로 스치지 않게 조준
- magic_circle_data_wiring: player prefab serialized refs 우선, editor `OnValidate`만 library asset auto-fill
- input_flow: `RMB Down -> AimLock + MagicCircleUI`, `LMB Drag -> stroke`, `LMB Up/RMB Up -> submit`
- draw_rules: `maxDrawTime=3s`, `minPoints=16`, out-of-circle cancel, UI over pointer start block
- fail_rules: short stroke=`Too short`, low score=`Unclear score/threshold`, low score cast block
- magic_circle_library_asset: `Assets/_Project/ScriptableObjects/MagicCircles/MagicCircleLibrary.asset`
- magic_circle_assets: `MagicCircle_Water`, `MagicCircle_Fire`, `MagicCircle_LightningRail`
- spell_library_asset: `Assets/_Project/ScriptableObjects/Spells/SpellLibrary.asset`
- spell_assets: `Spell_WaterBolt`, `Spell_FireBurst`, `Spell_LightningRail`
- circle_to_spell: `MagicCircleDefinitionSO.linkedSpellId -> SpellLibrarySO.GetById`
- player_prefab_runtime_path: `MagicCircleDrawController -> MagicCircleCastPipeline -> MagicCircleCastController -> SpellRunner`
- prefab_binding: `Assets/_Project/Prefabs/Player.prefab` references `MagicCircleLibrary.asset` + `SpellLibrary.asset`
- arena_enemy_density: `Prototype_Arena/EnemySpawner` uses `maxAlive=8`, `initialSpawnCount=8`
- channel_lock: `SpellDefinitionSO.requireAimLockDuringCast=1` on fire; `ChannelExecutor` keeps lock alive
- guide_overlay_behavior: right detail panel auto-focuses latest best circle and overlays the last submitted stroke in mint over the guide line
- guide_preview_visibility: guide/user overlay in `MagicCircleGuidePanel` now uses `Image` segment lines with bright core + halo instead of preview polyline graphics

## done
- [x] only `Prototype_Arena` build/runtime path retained
- [x] complex `Sigil` system removed
- [x] `MagicCircle` similarity matcher retained as sole recognition path
- [x] wrapper/migration-only pipeline removed
- [x] legacy `PrototypeHUD` removed
- [x] pattern-test scene/code removed
- [x] unused old spell scripts removed
- [x] water/fire/lightning spell assets restored and linked to the current magic-circle library
- [x] current runtime input/matcher behavior synced into docs
- [x] AI/player lookup scattered `FindWithTag` paths consolidated into `PlayerRuntimeLocator`
- [x] aim lock now respects nearest wall/ground hit instead of locking enemies through cover
- [x] unreferenced legacy projectile helper scripts removed
- [x] HUD / guide panel runtime lookup consolidated into `MagicCircleRuntimeLocator`
- [x] `MagicCircleGuidePanel` / `GameHUD` partial split로 layout/bootstrap/presentation 책임 분리

## wip
- play_tuning: 실제 Play Mode에서 threshold와 shape 감각 확인 필요

## todo
- verify_unity_playmode: `Prototype_Arena`에서 실제 draw/cast feel 확인
- tune_thresholds: water/fire/lightning `passThreshold` 실전값 미세 조정
- ui_polish: `MagicCircleGuidePanel` spacing 최종 점검

## known_issues
- playmode_gap: 본 세션에서는 Unity Editor Play Mode 직접 실행 검증은 못함
- runtime_map_nonpersistent: `MapRoot`는 런타임 생성
- short_stroke_block: `minPoints=16` 미만 stroke는 `Too short`로 종료
- threshold_block: best match가 threshold 미만이면 `Unclear`로 종료하고 캐스팅하지 않음
