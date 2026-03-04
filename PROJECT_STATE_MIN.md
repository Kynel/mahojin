# PROJECT_STATE_MIN

## env
- unity: `6000.3.10f1`
- render_pipeline: `URP`
- input_system: `New Input System` (`activeInputHandler=1`)
- input_actions: `Assets/InputSystem_Actions.inputactions`

## scenes
- main_scene: `Assets/_Project/Scenes/Prototype_Arena.unity`
- scene_roots: `CameraRig`, `Directional Light`, `Global Volume`, `Player`(prefab instance), `Ground`, `EnemySpawner`
- camera_hierarchy(scene): `CameraRig/Pivot/Main Camera`
- runtime_map_builder: `PrototypeMapBootstrapper` auto-run on scene load
- runtime_map_root: `MapRoot/{Terrain,Landmarks,Obstacles,Props,SpawnPoints}`
- legacy_ground: scene object `Ground` is disabled only after terrain creation succeeds

## prefabs
- player_prefab: `Assets/_Project/Prefabs/Player.prefab`
- player_tag_layer: tag=`Player`, layer=`Player(6)`
- player_components_core: `PlayerMotor`, `PlayerAim`, `RuneDrawController`, `RuneCastController`
- player_components_combat: `Health(max=100,destroyOnDeath=false)`, `Mana(max=100,current=100,regen=12,delay=0.25)`, `PlayerVitals`
- player_components_spells: `FireboltSpell`, `IceLanceSpell`, `BlinkSpell`, `NovaSpell`, `ChainLightningSpell`
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
- enemy_chaser_avoid_fields: `enableObstacleAvoidance`, `avoidRayDistance`, `avoidStrength`, `avoidProbeRadius`, `obstacleMask`
- enemy_chaser_collision_fields: `enableCollisionSlide`, `collisionSkin`, `maxSlideIterations`, `stuckThresholdDistance`, `stuckTimeForNudge`, `stuckNudgeStrength`
- enemy_chaser_grounding: raycast ground snap each physics tick
- `Assets/_Project/Scripts/Player/PlayerMotor.cs`: deadzone input + accel/decel + capsule slide collision + ground snap
- player_motor_collision_fields: `ignoreEnemyCollision`, `obstacleMask`, `collisionSkin`, `maxSlideIterations`
- player_motor_ground_fields: `followGroundHeight`, `groundMask`, `groundProbeHeight`, `groundProbeDistance`, `groundOffset`
- `Assets/_Project/Scripts/Runes/RuneCastController.cs`: rune recognition + spell cast + cooldown/mana gate
- rune_cast_mana_costs: Firebolt`14`, IceLance`8`, Blink`22`, Nova`30`, Chain`26`
- rune_cast_hud_api: `AbilityId`, `GetCooldownRemaining`, `GetCooldownDuration`, `GetGlobalCooldownRemaining`, `GetManaCost`, `IsCastAvailable`, `GetRuneLabel`
- rune_cast_block_state: mana 부족 시 `LastSpellName=Blocked`, `CooldownStateText=Not enough mana`(0.5s)
- `Assets/_Project/Scripts/UI/GameHUD.cs`: runtime HUD build (`GameHUDCanvas`) + vitals bars + ability bar + state text + top-right system buttons
- hud_vitals_layout: left-bottom anchor `(20,20)`, panel `440x122`, bars `412x34`, font `17`
- hud_ability_layout: bottom-center 5 slots, radial cooldown overlay, rune label, mana cost, global dim
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
- ground_raycast_dependents: `PlayerAim`, `RuneDrawController`, `PlayerMotor`, `EnemyChaser`, `FollowCameraRig`
- player_vitals_wiring(prefab): `PlayerVitals.health -> Health`, `PlayerVitals.mana -> Mana`
- rune_cast_wiring(prefab): `RuneCastController.mana -> Mana`
- player_enemy_collision_rule: `PlayerMotor.ignoreEnemyCollision=true`로 Player(6)-Enemy(8) 물리 충돌 무시(접촉 데미지는 유지)
- player_vision_wiring(prefab): `EnemyVisibilityByFOV.fovConeMesh -> FOVConeMesh`
- vision_masks: `FOVConeMesh.wallMask=Wall(10)`, `FOVConeMesh.groundMask=Ground(7)`, `EnemyVisibilityByFOV.enemyMask=Enemy(8)`
- vision_enemy_rule: 적은 `FOV+LOS` 충족 시만 표시, 나머지는 Renderer 완전 숨김

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

## known_issues
- runtime_map_nonpersistent: `MapRoot`는 런타임 생성(씬 파일에 baked 배치 아님)
- editor_verification_gap: 본 세션에서는 Unity Play Mode 직접 실행 검증 미완
- visibility_mechanics_note: 화면에서 안 보여도 EnemyContactDamage는 계속 동작(의도)
