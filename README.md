# Mahojin Prototype (Duckov-like Top-Down Rune Shooter)

Unity URP 기반의 탑다운 PvE 프로토타입입니다.  
WASD 이동 + 마우스 조준 + RMB 시작 / LMB 드래그 기반 마법진 입력 파이프라인을 사용합니다.

## Tech Stack
- Unity `6000.3.10f1`
- URP
- New Input System (`activeInputHandler=1`)

## Quick Start
1. Unity Hub에서 프로젝트 열기
2. 씬 열기: `Assets/_Project/Scenes/Prototype_Arena.unity`
3. Play

## Controls
- `WASD`: 이동
- `Mouse`: 조준
- `RMB Down`: 캐스팅 모드 진입 + AimLock 획득 + MagicCircle UI 생성
- `LMB Hold/Drag`: MagicCircle UI에 스트로크 드로잉
- `LMB Up` 또는 `RMB Up`: 드로잉 종료(스트로크 제출/실패 처리) + UI 닫기 + 룬 판정/캐스팅 시도
- `LMB`만 단독 클릭: 동작 없음
- AimLock: 성공 제출 후 즉시 강제 해제하지 않고 lock duration 동안 유지(채널 스펠 지원)

## Current Gameplay Features
- 플레이어 HP/MP 시스템
- MP 자연 리젠 + 적 처치 시 MP 회복(킬 리젠)
- 마나 부족 시 캐스팅 차단 + 상태 메시지
- 번개 룬은 reference stroke 비교 방식으로 판정(Procrustes 회전 정렬 + RMSE/Chamfer 거리 score)
- 우측 `Rune Examples` 카드에 룬별 `Match/Need`(일치도/필요치) 표시
- `RuneCastController.TryCastSpell` spellId 기반 게이트(마나/글로벌쿨다운/스펠쿨다운)
- 적 스폰/추적/접촉 데미지/사망
- 플레이어 사망 후 리셋 루프
- 확장 맵(런타임 Terrain + 커버/랜드마크/스폰 포인트)
- 탑다운 Follow Camera Rig (지형 추종 + 장애물 대응)
- 시야 시스템(FOV/LOS 기반 적 가시화 + 암시야 오버레이)

## HUD
- 좌하단: HP/MP 바
- 하단 중앙: 상태 텍스트(마나 부족/쿨다운/실패 메시지)
- 우측 중앙: `Rune Examples` (물/불/번개 룬 미니 프리뷰 + 특징 설명)
- 우상단: `재시작`, `게임종료` 버튼
  - 재시작 기본 대상: `Prototype_Arena`
  - 에디터에서 게임종료: Play 모드 종료
  - 빌드에서 게임종료: `Application.Quit()`

## Vision System Policy
- 오버레이 셰이더: `Assets/_Project/Shaders/VisionOverlayMaskURP.shader`
- 오버레이는 `FOVConeMesh`가 생성하는 메시로 처리
- 기본 벽 dim(`EnemyVisibilityByFOV.dimWallsOutsideFov`)은 `false`로 비활성(이중 암전 방지)

## Important Scene/Prefab Paths
- Main Scene: `Assets/_Project/Scenes/Prototype_Arena.unity`
- Player Prefab: `Assets/_Project/Prefabs/Player.prefab`
- Enemy Prefab: `Assets/_Project/Prefabs/Enemy.prefab`

## Key Scripts
- `Assets/_Project/Scripts/Runes/RuneCastController.cs`
- `Assets/_Project/Scripts/Runes/RuneCastPipelineController.cs`
- `Assets/_Project/Scripts/Runes/RuneGuideScorer.cs`
- `Assets/_Project/Scripts/Runes/RuneResolver.cs`
- `Assets/_Project/Scripts/Spells/SpellRunner.cs`
- `Assets/_Project/Scripts/Spells/Executors/ProjectileExecutor.cs`
- `Assets/_Project/Scripts/Spells/Executors/HitscanExecutor.cs`
- `Assets/_Project/Scripts/Spells/Executors/ChannelExecutor.cs`
- `Assets/_Project/Scripts/Player/PlayerMotor.cs`
- `Assets/_Project/Scripts/UI/GameHUD.cs`
- `Assets/_Project/Scripts/AI/PrototypeMapBootstrapper.cs`
- `Assets/_Project/Scripts/AI/EnemySpawner.cs`
- `Assets/_Project/Scripts/AI/EnemyChaser.cs`
- `Assets/_Project/Scripts/Vision/FOVConeMesh.cs`
- `Assets/_Project/Scripts/Vision/EnemyVisibilityByFOV.cs`

## Data Assets
- Rune SO: `Assets/_Project/ScriptableObjects/Runes/RuneLibrary.asset` (`물/불/번개레일` 포함)
- Spell SO: `Assets/_Project/ScriptableObjects/Spells/SpellLibrary.asset` (3종 포함)

## Tuning Tips
- 시야 어두움 강도: `FOVConeMesh.overlayDarkAlpha`
- FOV 체감: `viewAngleDeg`, `viewDistance`, `softEdge`, `endFade`
- 재시작 씬 변경: `GameHUD.restartSceneName`, `restartScenePath`

## Notes
- 맵(`MapRoot`)은 런타임 생성입니다.
- 레거시 `Space` 기반 룬 드로잉/캐스팅 파이프라인은 제거되었습니다.
- 최신 구현 스냅샷은 `PROJECT_STATE_MIN.md`를 기준으로 확인하세요.
