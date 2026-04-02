# Mahojin Prototype

Unity URP 기반 탑다운 PvE 프로토타입이다.  
현재 저장소는 `Prototype_Arena` 실행 경로를 중심으로 정리되어 있고, 마법 입력은 `Magic Circle` 단일 시스템으로 통합되어 있다.

## Tech
- Unity `6000.3.10f1`
- URP
- New Input System

## Scene
- build scene: `Assets/_Project/Scenes/Prototype_Arena.unity`
- unused asset: `Assets/Scenes/SampleScene.unity` (build 미포함)

## Controls
- `WASD`: 이동
- `Mouse`: 조준
- `RMB Down`: 캐스팅 모드 시작 + AimLock + MagicCircle UI 표시
- `LMB Hold/Drag`: 한붓 stroke 드로잉
- `LMB Up` 또는 `RMB Up`: stroke 제출 -> 가장 닮은 `Magic Circle` 선택 -> spell cast 시도

## Input Notes
- 이동은 `Assets/InputSystem_Actions.inputactions`의 `Move` action을 사용
- 마우스 조준/드로잉 입력은 현재 `Mouse.current` 직접 읽기 기반

## Essential Runtime Path
- `Assets/_Project/Scripts/Player/PlayerMotor.cs`
- `Assets/_Project/Scripts/Player/PlayerAim.cs`
- `Assets/_Project/Scripts/Combat/AimLockController.cs`
- `Assets/_Project/Scripts/MagicCircles/MagicCircleDrawController.cs`
- `Assets/_Project/Scripts/MagicCircles/MagicCircleCastPipeline.cs`
- `Assets/_Project/Scripts/MagicCircles/MagicCircleCastController.cs`
- `Assets/_Project/Scripts/Spells/SpellRunner.cs`
- `Assets/_Project/Scripts/AI/PrototypeMapBootstrapper.cs`
- `Assets/_Project/Scripts/AI/EnemySpawner.cs`
- `Assets/_Project/Scripts/UI/MagicCircleGuidePanel.cs`
- `Assets/_Project/Scripts/UI/GameHUD.cs`

## Matching / Cast Gate
- `MagicCircleMatcher`는 reference stroke와 user stroke를 resample/normalize/rotate/RMSE 비교해서 전체 library를 점수순으로 정렬한다
- 현재 최고 점수가 template의 `passThreshold`를 넘지 못하면 `Unclear` 상태를 띄우고 캐스팅하지 않는다
- stroke point 수가 부족하면 `Too short`로 처리하고 제출을 취소한다

## Guide Panel
- 우측 패널은 선택된 `Magic Circle`의 기준 라인과 최근 유저 stroke overlay를 함께 표시한다
- 새 stroke 제출 시 가장 점수가 높은 circle로 자동 포커스한다
- 기준 라인은 진한 색, 유저 stroke는 민트색 overlay로 보여 준다

## Magic Circle Data
- `Assets/_Project/Scripts/MagicCircles/Data/MagicCircleDefinitionSO.cs`
- `Assets/_Project/Scripts/MagicCircles/Data/MagicCircleLibrarySO.cs`
- `Assets/_Project/ScriptableObjects/MagicCircles/MagicCircleLibrary.asset`

## Current Magic Circles
- `magic_circle_water` -> `spell_water_bolt`
- `magic_circle_fire` -> `spell_fire_burst`
- `magic_circle_lightning` -> `spell_lightning_rail`

## Combat Loop
- `PrototypeMapBootstrapper`가 arena 지형과 스폰 포인트를 런타임 생성
- `EnemySpawner`가 최대 5체까지 적을 유지
- 적은 플레이어를 추적하고 접촉 피해를 준다
- 플레이어는 HP/MP를 가지며 사망 시 자동 리스폰한다
- HUD에서 scene restart / quit 버튼을 제공한다

## Cleanup Result
- complex `Sigil` matcher 제거
- wrapper/migration-only pipeline 제거
- legacy `PrototypeHUD` 제거
- `Prototype_PatternTest` 및 관련 테스트 코드 제거
- 미사용 old spell scripts 제거

## Verification
- `dotnet build Assembly-CSharp.csproj`
  - 결과는 현재 워크트리 기준으로 재확인 권장
