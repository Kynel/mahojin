# Mahojin Prototype (Duckov-like Top-Down Rune Shooter)

Unity URP 기반의 탑다운 PvE 프로토타입입니다.  
WASD 이동 + 마우스 조준 + Space 홀드 드로잉으로 룬을 그려 즉시 스펠을 발동합니다.

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
- `Space (Hold)`: Rune Draw 모드
- `Space (Release)`: 룬 인식 + 스펠 캐스팅

## Current Gameplay Features
- 플레이어 HP/MP 시스템
- MP 자연 리젠 + 적 처치 시 MP 회복(킬 리젠)
- 마나 부족 시 캐스팅 차단 + 상태 메시지
- 적 스폰/추적/접촉 데미지/사망
- 플레이어 사망 후 리셋 루프
- 5개 스펠 + 개별 쿨다운 + 글로벌 쿨다운
- 확장 맵(런타임 Terrain + 커버/랜드마크/스폰 포인트)
- 탑다운 Follow Camera Rig (지형 추종 + 장애물 대응)
- 시야 시스템(FOV/LOS 기반 적 가시화 + 암시야 오버레이)

## HUD
- 좌하단: HP/MP 바
- 하단 중앙: 5개 스킬 슬롯, 라디얼 쿨다운, 코스트/룬 라벨
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
- `Assets/_Project/Scripts/Player/PlayerMotor.cs`
- `Assets/_Project/Scripts/UI/GameHUD.cs`
- `Assets/_Project/Scripts/AI/PrototypeMapBootstrapper.cs`
- `Assets/_Project/Scripts/AI/EnemySpawner.cs`
- `Assets/_Project/Scripts/AI/EnemyChaser.cs`
- `Assets/_Project/Scripts/Vision/FOVConeMesh.cs`
- `Assets/_Project/Scripts/Vision/EnemyVisibilityByFOV.cs`

## Tuning Tips
- 시야 어두움 강도: `FOVConeMesh.overlayDarkAlpha`
- FOV 체감: `viewAngleDeg`, `viewDistance`, `softEdge`, `endFade`
- 재시작 씬 변경: `GameHUD.restartSceneName`, `restartScenePath`

## Notes
- 맵(`MapRoot`)은 런타임 생성입니다.
- 최신 구현 스냅샷은 `PROJECT_STATE_MIN.md`를 기준으로 확인하세요.
