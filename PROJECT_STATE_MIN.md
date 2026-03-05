# PROJECT_STATE_MIN

## env
- unity: `6000.3.10f1`
- render_pipeline: `URP`
- input_system: `New Input System` (`activeInputHandler=1`)
- input_actions: `Assets/InputSystem_Actions.inputactions`

## scenes
- main_scene: `Assets/_Project/Scenes/Prototype_Arena.unity`
- test_scene: `Assets/_Project/Scenes/Prototype_PatternTest.unity` (flat plane, no terrain runtime map)
- scene_roots: `CameraRig`, `Directional Light`, `Global Volume`, `Player`(prefab instance), `Ground`, `Enemy_Pattern`
- camera_hierarchy(scene): `CameraRig/Pivot/Main Camera`
- runtime_map_builder: `PrototypeMapBootstrapper` auto-run on scene load
- runtime_map_root: `MapRoot/{Terrain,Landmarks,Obstacles,Props,SpawnPoints}`
- legacy_ground: scene object `Ground` is disabled only after terrain creation succeeds

## prefabs
- player_prefab: `Assets/_Project/Prefabs/Player.prefab`
- player_tag_layer: tag=`Player`, layer=`Player(6)`
- player_components_core: `PlayerMotor`, `PlayerAim`, `AimLockController`, `MagicCircleUI`, `MagicCircleDrawController`, `RuneCastController`, `SpellRunner`, `RuneCastPipelineController`
- player_components_combat: `Health(max=100,destroyOnDeath=false)`, `Mana(max=100,current=100,regen=12,delay=0.25)`, `PlayerVitals`
- player_components_spells: legacy 임시 스펠 컴포넌트 제거됨(신규 rune-driven spellId 파이프라인 준비)
- player_components_vision: `VisionPostFXBootstrapper(postExposure=0)`, `FOVConeMesh(140deg,28m,rayCount=240,maskDistance=52,meshHeightOffset=0.02,overlayDarkAlpha=0.62,debugDisableVisionRenderer)`, `EnemyVisibilityByFOV(update=0.075s)`
- enemy_prefab: `Assets/_Project/Prefabs/Enemy.prefab`
- enemy_tag_layer: tag=`Untagged`, layer=`Enemy(8)`
- enemy_components_core: `Health`, `EnemyChaser`, `EnemyContactDamage`, `EnemyManaReward`
- enemy_defaults_contact: `dps=12`, `tickInterval=0.25`
- enemy_defaults_reward: `killManaRestore=10`

## scripts
- `Assets/_Project/Scripts/AI/PrototypeMapBootstrapper.cs`: terrain+obstacle+spawnpoints runtime build, player reposition, spawner context wiring
- map_settings: `terrainSize=200x20x200`, `heightmapResolution=257`, `playerSafeRadius=15`
- map_landmarks: central `RuinRing` + 2 ramps
- map_obstacles: `RockLarge(22)` + `WallChunk(16)` + boundary rocks
- map_spawn_defaults: `PlayerSpawn=1`, `EnemySpawn_XX=10`, `enemySpawnMinDistance=18`, slope cap `35`
- terrain_fallback: if terrain create fails -> keep `Ground` active + error log
- `Assets/_Project/Scripts/AI/EnemySpawner.cs`: spawnPoints-priority spawning + terrain fallback sampling + wall/slope/player-distance validation
- enemy_spawner_api: `ApplyMapContext(Terrain, Transform[], float minPlayerDistance=-1f)`
- enemy_spawner_checks: terrain bounds, slope, wall overlap, min player distance
- `Assets/_Project/Scripts/AI/EnemyChaser.cs`: chase + wall-slide collision resolution + obstacle steering + stuck nudge recovery
- `Assets/_Project/Scripts/AI/PatternEnemyMover.cs`: enemy circle-pattern movement(rigidbody kinematic) for deterministic motion test
- `Assets/_Project/Scripts/AI/PatternTestSceneBootstrapper.cs`: when scene=`Prototype_PatternTest`, disable `EnemySpawner/PrototypeMapBootstrapper/ArenaWallBootstrapper`, disable `EnemyChaser`, ensure at least one `PatternEnemyMover`
- enemy_chaser_avoid_fields: `enableObstacleAvoidance`, `avoidRayDistance`, `avoidStrength`, `avoidProbeRadius`, `obstacleMask`
- enemy_chaser_collision_fields: `enableCollisionSlide`, `collisionSkin`, `maxSlideIterations`, `stuckThresholdDistance`, `stuckTimeForNudge`, `stuckNudgeStrength`
- enemy_chaser_grounding: raycast ground snap each physics tick
- `Assets/_Project/Scripts/Player/PlayerMotor.cs`: deadzone input + accel/decel + capsule slide collision + ground snap
- player_motor_collision_fields: `ignoreEnemyCollision`, `obstacleMask`, `collisionSkin`, `maxSlideIterations`
- player_motor_ground_fields: `followGroundHeight`, `groundMask`, `groundProbeHeight`, `groundProbeDistance`, `groundOffset`
- `Assets/_Project/Scripts/Player/PlayerAim.cs`: aim lock 우선 적용(락 활성 중에는 마우스 무시), 조준선/회전이 lock point를 지속 추적
- `Assets/_Project/Scripts/Combat/AimLockController.cs`: 커서 기준 `Enemy` 우선 lock, 실패 시 `Ground` point lock, `lockDuration(3s)` 동안 aim point/direction 고정 (`BeginLockFromCursor` API)
- aim_lock_api_extra: `IsTargetLock`, `IsPointLock`, `RemainingLockTime`
- aim_lock_api_channel: `ExtendLock(float)`, `EnsureLockUntil(float durationFromNow)`
- `Assets/_Project/Scripts/UI/MagicCircleUI.cs`: RMB down 위치 앵커 마법진 UI(원형 테두리+그리드+점선 링+red user line+상태 메시지), 화면 clamp/close 제공
- magic_circle_user_line_impl: red user line은 UI `Image` 세그먼트 체인 + polyline mesh 병행 렌더(가시성 안정화)
- magic_circle_visual_tune: 반투명 원형 배경 채움 추가 + border/grid/dot 알파·두께 상향(흐릿함 개선)
- `Assets/_Project/Scripts/Runes/MagicCircleDrawController.cs`: `RMB Down` 캐스팅 모드+AimLock 시작, `LMB Hold/Drag` 스트로크 수집, `LMB Up` 또는 `RMB Up` 종료/정규화 이벤트 발행, out-of-circle/time/minPoints 규칙 처리
- legacy_input_removed: `Assets/_Project/Scripts/Input/MagicCircleInputBridge.cs` 삭제(미사용 정리)
- `Assets/_Project/Scripts/Runes/RuneCastController.cs`: spellId 기반 cast gate(`TryCastSpell(SpellDefinitionSO, CastContext, out failReason)` + rune label overload), global/spell cooldown dictionary, mana gate, HUD state 갱신
- rune_cast_api: `TryCastSpell(spell, ctx, out failReason)`, `TryCastSpell(spell, ctx, runeDisplayName, out failReason)`, `IsSpellCastAvailable(spell)`, `ReportState(text,duration)`
- rune_cast_cooldown_storage: `globalNextReadyTime`, `nextReadyBySpellId`, `cooldownBySpellId`
- rune_cast_block_state: 실패 시 `LastSpellName=Blocked`, 상태 텍스트(`Not enough mana`/`cooldown`/`Missing SpellRunner`) 표시
- rune_cast_hud_lock_state: ready 상태에서 aim lock 시 `Aim lock: Target/Point (x.xs)` 표시
- `Assets/_Project/Scripts/Runes/RuneCastPipelineController.cs`: `MagicCircleDrawController.StrokeSubmittedNorm` 구독 -> rune별 score 계산(번개는 reference stroke 비교) -> `SpellLibrarySO` lookup -> `RuneCastController.TryCastSpell` 연결
- rune_pipeline_cast_context: `CastContext(caster/aimLock/enemyMask/wallMask/groundMask/cam)`를 stroke 제출 시점마다 구성
- rune_pipeline_debug: `logResolveDetails`로 룬 판정 score/reason 콘솔 출력 가능
- rune_pipeline_score_cache: stroke 제출 시 각 rune score/passScore 캐시(최근 `scoreCacheLifetime=8s`), HUD에서 카드별 일치도 표시
- rune_pipeline_lightning_reference: 번개 룬(`useReferenceStrokeComparison=true`)은 `LightningReferenceMatcher` 사용(arc-length N=128 -> center/scale normalize -> Procrustes best rotation -> RMSE+Chamfer 지수 score)
- rune_pipeline_lightning_missing_reference_policy: `referenceStrokeNorm` 비어있으면 warning + score 0 처리(기존 scorer fallback 없음)
- rune_pipeline_lightning_tuning: strict 실패 감점 배수 `0.25 -> 0.6` 완화, `Rune_LightningRail` 기준값 조정(`passScore=0.50`, `rmseTol=0.24`, `chamferTol=0.18`, `strictAngle=16`, `strictYStd=0.10`)
- legacy_removed: `RuneDrawController` 삭제, Space 기반 stroke/recognize/cast 경로 제거
- `Assets/_Project/Scripts/Runes/Data/RuneDefinitionSO.cs`: rune 데이터 SO(id/displayName/guide/strictSegments/gates/baseTol/passScore/linkedSpellId/extraRuleFlags + reference 비교 필드 `useReferenceStrokeComparison/referenceStrokeNorm/referenceRmseTol/referenceChamferTol/enforceStrictHorizontalSegment/strictAngleDeg/strictYStd/strictSegmentStartIdx/strictSegmentEndIdx`)
- `Assets/_Project/Scripts/Runes/StrokeMath.cs`: resample/normalize(Procrustes 준비)/best-fit rotation/RMSE/Chamfer 공통 유틸
- `Assets/_Project/Scripts/Runes/LightningReferenceMatcher.cs`: 번개 reference 비교 전용 scorer(+reverse stroke 후보 비교 + strict horizontal 검사)
- `Assets/_Project/Scripts/Runes/Data/RuneLibrarySO.cs`: rune 목록+id lookup
- `Assets/_Project/Scripts/Spells/Data/SpellDefinitionSO.cs`: spell 데이터 SO(id/displayName/castType/mana/cooldown/range/damage/speed/bounce/duration/tick/requireAimLock)
- `Assets/_Project/Scripts/Spells/Data/SpellLibrarySO.cs`: spell 목록+id lookup
- `Assets/_Project/Scripts/Spells/CastContext.cs`: spell 실행 컨텍스트(caster/aimLock/enemyMask/wallMask/groundMask/cam + state reporter)
- `Assets/_Project/Scripts/Spells/SpellRunner.cs`: `Execute(SpellDefinitionSO spell, CastContext ctx)` 진입점(castType dispatch)
- `Assets/_Project/Scripts/Spells/Executors/ProjectileExecutor.cs`: WaterRicochet 실행기(벽 반사 `bounceCount`, range/lifetime, enemy `Health.TakeDamage`)
- `Assets/_Project/Scripts/Spells/Executors/HitscanExecutor.cs`: LightningRail 실행기(SphereCast radius `0.2`, default range `45`, beam VFX `0.1s`)
- `Assets/_Project/Scripts/Spells/Executors/ChannelExecutor.cs`: FlameThrower 실행기(duration/tickInterval/tickCount, 채널 락 강제/취소, cone overlap tick damage)
- `Assets/_Project/Scripts/Runes/RuneGuideScorer.cs`: arc-length N=128 + symmetric chamfer + strict segment/gate/special-rule 기반 score(0..1) 계산, user stroke를 rune guide bounds로 정렬(normalize) 후 점수화
- rune_scorer_tuning: `StrictHorizontal` 규칙 완화(`angle<=12deg`, `yStd<=0.06`)
- `Assets/_Project/Scripts/Runes/RuneResolver.cs`: 다중 rune 후보 best-match 선택, passScore 미만 실패 처리
- rune_resolver_fallback_api: `ResolveBestAllowLowScore(...)` 제공(최고점 후보 조회용)
- data_assets_runes: `Assets/_Project/ScriptableObjects/Runes/{Rune_Water,Rune_Fire,Rune_LightningRail,RuneLibrary}.asset`
- rune_lightning_reference_asset: `Rune_LightningRail.asset`에 `useReferenceStrokeComparison=1`, `referenceStrokeNorm(36pts)`, `referenceRmseTol=0.11`, `referenceChamferTol=0.08`, strict segment index `44..84` 설정
- data_assets_spells: `Assets/_Project/ScriptableObjects/Spells/{Spell_WaterBolt,Spell_FireBurst,Spell_LightningRail,SpellLibrary}.asset`
- spell_asset_profiles: WaterBolt=`Projectile+bounce1`, FireBurst=`Channel duration2/tick3/lockRequired`, LightningRail=`Hitscan range45`
- `Assets/_Project/Scripts/UI/GameHUD.cs`: runtime HUD build (`GameHUDCanvas`) + HP/MP bars + bottom-center state text + right-center `Rune Examples`(3 cards, mini preview + strict red lines) + top-right system buttons
- hud_vitals_layout: left-bottom anchor `(20,20)`, panel `440x122`, bars `412x34`, font `17`
- hud_rune_examples_layout: right-center panel `460x400`, title `Rune Examples`, 3 cards(이름/설명/대형 원형 미니 프리뷰+start/end dot)
- hud_rune_examples_desc: Water=`Ricochet x1`, Fire=`Channel 2s / 3 ticks / keep aim`, Rail=`Instant / strict horizontal`
- hud_rune_examples_visibility_fix: guide line/strict line 두께 상향 + 프리뷰 반경 확대 + pipeline 참조 실패 시 `RuneLibrary` fallback 검색
- hud_rune_preview_source_policy: `useReferenceStrokeComparison=true` 룬은 우측 카드가 `referenceStrokeNorm`을 우선 렌더(번개 가이드 일치), strict 빨간선도 reference strict index 범위 기준으로 표시
- hud_rune_preview_render_stability: `Rune Examples` 가이드/원형 테두리는 `UiPolylineGraphic` 외에 Image segment chain fallback도 함께 렌더(기기별 라인 미표시 현상 완화)
- hud_rune_match_text: 카드별 `Match xx% / Need yy% (OK|Fail)` 표시(최근 stroke score 기반)
- hud_active_layout: bottom-left vitals(HP/MP) + bottom-center state text 유지
- hud_system_buttons: top-right `재시작`(기본 `Prototype_Arena` 재로드: `restartSceneName/restartScenePath` 사용, 실패 시 현재 씬 fallback), `게임종료`(Editor play stop / build quit)
- hud_eventsystem_bootstrap: `GameHUD`가 `EventSystem` 자동 보장(`InputSystemUIInputModule` 우선, 없으면 `StandaloneInputModule` fallback)
- hud_legacy_handling: `PrototypeHUD` component/canvas auto-disable
- `Assets/_Project/Scripts/Camera/FollowCameraRig.cs`: top-down follow rig with smoothing, terrain Y follow, wall LOS distance pull-in, optional look-ahead
- camera_defaults: `pivotPitch=60`, `cameraDistance=20`, `minDistance=7`, `maxDistance=26`
- camera_masks: ground=`Ground(7)`, obstacle=`Wall(10)`
- `Assets/_Project/Scripts/Vision/VisionPostFXBootstrapper.cs`: Global Volume `ColorAdjustments.postExposure` 기본값 적용(현재 0, 어둠은 FOV 마스크가 담당)
- vision_postfx_default: `targetPostExposure=0`
- `Assets/_Project/Scripts/Vision/FOVConeMesh.cs`: 비가시영역 dark-mask(360도 strip), `Wall` SphereCast 차폐 컷 + dilation, 경계 adaptive refine(고정밀 샘플), Vision renderer를 overlay 전용으로 강제(`ShadowCasting Off`, `receiveShadows=false`, `lightProbeUsage=Off`, `reflectionProbeUsage=Off`, `allowOcclusionWhenDynamic=false`, `ForceNoMotion`), debug 토글 `debugDisableVisionRenderer`
- vision_fov_defaults: `viewAngleDeg=140`, `viewDistance=28`, `rayCount=240`, `maskDistance=52`, `meshHeightOffset=0.02(clamp 0~0.05)`, `overlayDarkAlpha=0.62`, `softEdge=1.2`, `endFade=3.0`, `obstacleProbeRadius=0.24`, `wallInset=0.04`, `occlusionDilateSteps=1`, `occlusionExtraInset=0.08`, `hardOcclusionDarkness=true`, `enableAdaptiveRefine=true`, `maxRefineDepth=4`, `refineDistanceThreshold=0.55`, `minRefineAngleDeg=0.2`, `maxAdaptiveSamples=1400`, `renderQueue=3550`, `shader=VisionOverlayMaskURP(ZTest LEqual)`
- vision_fov_runtime_cleanup: runtime 생성 `mesh/material`은 `OnDestroy`에서 정리, material `ZWrite Off`/`ZTest LEqual` 고정
- `Assets/_Project/Scripts/Vision/EnemyVisibilityByFOV.cs`: `OverlapSphere + 각도 + multi-sample SphereCast LOS`로 적 Renderer 표시/숨김(+짧은 grace), `Wall` 렌더러는 FOV+LOS 밖일 때 dim 처리
- vision_visibility_defaults: `enemyMask=Enemy(8)`, `wallMask=Wall(10)`, `fovAnglePadding=1.25`, `distancePadding=0.22`, `losProbeRadius=0.14`, `updateInterval=0.075`, `cacheRefreshInterval=0.35`, `visibilityGraceTime=0.06`, `dimWallsOutsideFov=false`, `hiddenWallBrightness=0.38`, `wallSampleInset=0.25`, `wallCacheRefreshInterval=0.75`
- vision_visibility_alloc: LOS 샘플/캐시 정리 경로를 non-alloc 구조로 정리(프레임 GC 완화)
- vision_visibility_init: `MaterialPropertyBlock`은 field initializer가 아닌 `Awake/EnsureWallPropertyBlock`에서 생성(ctor UnityException 방지)
- `Assets/_Project/Shaders/VisionOverlayMaskURP.shader`: FOV 기본 오버레이 셰이더(단일 pass, `LightMode=SRPDefaultUnlit`, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, `ZTest LEqual`, `Cull Off`, `Queue=Transparent+550`, `final=_Color*vertexColor`)
- `Assets/_Project/Shaders/VisionConeURP.shader`: legacy 대체 셰이더(보존)
- `Assets/_Project/Scripts/AI/ArenaWallBootstrapper.cs`: skips runtime wall generation if `MapRoot` exists

## wiring
- layers: `Ground=7`, `Enemy=8`, `Projectile=9`, `Wall=10`, `Player=6`
- player_aim: uses `Camera.main` fallback when `aimCamera` null
- camera_main_tag: `Main Camera` tag preserved under `CameraRig/Pivot`
- camera_rig_refs(scene): `FollowCameraRig.pivot -> Pivot`, `targetCamera -> Main Camera`
- projectile_wall_interaction: spell projectiles use `obstacleMask=Wall` + `StopAndExpire`
- map_to_spawner: `PrototypeMapBootstrapper -> EnemySpawner.ApplyMapContext(terrain, enemySpawns,18)`
- pattern_test_bootstrap: scene `Prototype_PatternTest`에서 런타임 스폰/맵 생성 비활성 + 적 이동을 패턴 모드로 강제
- ground_raycast_dependents: `PlayerAim`, `PlayerMotor`, `EnemyChaser`, `FollowCameraRig`
- player_vitals_wiring(prefab): `PlayerVitals.health -> Health`, `PlayerVitals.mana -> Mana`
- rune_cast_wiring(prefab): `RuneCastController.spellRunner -> SpellRunner`, `mana -> Mana`, `aimLockController -> AimLockController`
- rune_pipeline_wiring(prefab): `RuneCastPipelineController.{drawController,runeCastController,spellRunner,aimLockController,runeLibrary,spellLibrary}` 연결
- aim_lock_wiring(runtime): `Player.prefab`의 `MagicCircleDrawController`가 `RMB Down`에서 `AimLockController.BeginLockFromCursor` 호출
- aim_lock_visual_wiring: `PlayerAim`이 `AimLockController.HasLock` 우선, 락 중 조준선/시야(회전) 모두 lock target/point 기준으로 유지
- magic_circle_rules: `maxDrawTime=3s`, circle 밖 즉시 취소, `minPoints=16` 미만 실패, `EventSystem.IsPointerOverGameObject`면 draw start 무시, 성공 제출 시 aim lock은 자동 즉시 해제하지 않음(만료/채널 유지용)
- input_mode_now: Space 입력 처리 코드 제거, `RMB Down`(cast mode start) + `LMB Hold/Drag`(draw) + `LMB/RMB Up`(finish)만 활성
- player_enemy_collision_rule: `PlayerMotor.ignoreEnemyCollision=true`로 Player(6)-Enemy(8) 물리 충돌 무시(접촉 데미지는 유지)
- player_vision_wiring(prefab): `EnemyVisibilityByFOV.fovConeMesh -> FOVConeMesh`
- vision_masks: `FOVConeMesh.wallMask=Wall(10)`, `FOVConeMesh.groundMask=Ground(7)`, `EnemyVisibilityByFOV.enemyMask=Enemy(8)`
- vision_enemy_rule: 적은 `FOV+LOS` 충족 시만 표시, 나머지는 Renderer 완전 숨김
- rune_spell_data_wiring: `RuneDefinitionSO.linkedSpellId` -> `SpellLibrarySO` id lookup 전제
- spell_runner_wiring: `SpellRunner`가 `SpellDefinitionSO.castType`에 따라 `Projectile/Hitscan/Channel` executor 실행
- rune_pipeline_unclear_policy: 기본은 `requiredPassScore(룬별)`+global minimum(`minimumAcceptScore=0.35`) 동시 통과 시 캐스팅, 실패 시 `Unclear xx%/yy%`; fallback은 옵션(`allowLowScoreFallback`)으로만 허용

## done
- [x] player HP/MP system + death/reset loop + enemy contact damage + kill mana restore
- [x] mana-cost spell casting + blocked-state feedback + HUD state exposure API
- [x] GameHUD(게이지/스킬슬롯/라디얼쿨다운/상태표시) 적용, legacy HUD 비활성
- [x] large terrain map runtime generation + landmarks/covers/spawnpoints
- [x] wall obstacle projectile interaction in expanded map
- [x] enemy spawn sampling upgraded for large terrain
- [x] enemy simple obstacle avoidance steering added
- [x] camera rig follow system added with terrain/obstacle awareness
- [x] vitals HUD enlarged and moved to bottom-left
- [x] vision system: 암시야 postFX + FOV 밝힘 메쉬 + 적 FOV/LOS 기반 가시성 제어
- [x] movement stability pass: player drift 완화 + enemy wall collision/막힘 recovery + LOS 판정 정밀화
- [x] root `README.md` 작성(실행/조작/시스템/튜닝 가이드)
- [x] AimLockController 추가: cursor 기반 target/point lock API + 드로잉 중 유지 + cast direction lock 연동
- [x] `Prototype_PatternTest` 테스트 씬 추가 + 패턴 이동 적 사전 구성
- [x] Space 기반 RuneDraw/RuneCast 파이프라인 제거 + RMB/LMB 마우스 입력 체계로 이관
- [x] Player prefab에서 `RuneDrawController` 제거
- [x] MagicCircle UI + RMB 시작/LMB 드래그/LMB·RMB 종료 stroke capture 추가(정규화 이벤트 발행, 취소/실패 규칙 포함)
- [x] cleanup: Player prefab 임시 스펠 컴포넌트(5종) 제거 + HUD를 vitals/state 중심으로 단순화(legacy ability bar 기본 비활성)
- [x] data-driven 기반 추가: RuneDefinitionSO/SpellDefinitionSO/LibrarySO + RuneGuideScorer + RuneResolver + 3 rune/3 spell asset 생성
- [x] SpellRunner + Executors 추가: 물(리코쉐 1회), 불(2초 3틱 채널+락강제), 번개(즉발 레일) 실행 경로 + AimLock 연장 API
- [x] draw->resolve->spell cast 파이프라인 연결: `RuneCastPipelineController`로 best rune 자동선택 + spellId 게이트 + cast 실행
- [x] HUD 우측 중앙 `Rune Examples` 패널 추가(미니 프리뷰/strict segment 강조/설명 텍스트)
- [x] 번개 룬 판정 재설계: reference stroke 비교 기반(Procrustes+RMSE+Chamfer)으로 교체 + UI 카드 Match/Need 연동 유지

## wip
- playmode_tuning: camera distance/avoid feel, obstacle density, spawn combat rhythm
- vision_tuning: `viewAngle/viewDistance/exposure` 체감값 미세 조정

## todo
- playmode_check: terrain 가시성 + ground fallback 로그 실제 확인
- tune_camera: `followSmoothTime/avoidSmoothTime/minDistance` 체감값 조정
- tune_map: obstacle count/size로 이동 막힘 vs 커버 밀도 밸런싱
- tune_spawns: spawnpoint 분포와 전투 템포 재조정
- optional_persist: runtime map을 scene-baked 배치로 전환 여부 결정
- vision_polish: FOV cone 색/강도/soft-edge 값 플레이 감성 튜닝
- balance_tuning: rune passScore/tolerance(오인식/미인식 균형) 조정
- channel_feel_tuning: Fire channel VFX/틱 판정 반경 체감값 조정
- ui_polish: Rune Examples 카드 폰트/색 대비/모바일 해상도 레이아웃 미세조정
- rune_reference_expand: 원/세모 등 나머지 룬도 reference stroke 비교 방식으로 단계적 전환

## known_issues
- runtime_map_nonpersistent: `MapRoot`는 런타임 생성(씬 파일에 baked 배치 아님)
- editor_verification_gap: 본 세션에서는 Unity Play Mode 직접 실행 검증 미완
- visibility_mechanics_note: 화면에서 안 보여도 EnemyContactDamage는 계속 동작(의도)
