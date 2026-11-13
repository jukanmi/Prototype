# 0층 Kernel — Core · Stats · StateMachine

누구의 것도 아닌 규칙 · 데이터 · 기계. **이 층은 프로젝트의 다른 폴더를 하나도 참조하지 않는다.**
(Core → StateMachine은 같은 층 안: `BattleLog.StateName(IState)`)

## 클래스 다이어그램

> 다이어그램: [01-Kernel.puml](01-Kernel.puml)

`UiKit` · `MenuCursor` · `RoleNames` 는 생략 (UI 헬퍼 · 문자열 표).
작은 타입은 같은 폴더의 관련 파일에 들어 있다 — `RoleNames` → `Enums.cs`, `AttackInputBuffer` → `BasicAttackCombo.cs`, `GameplayModal` → `TimeControl.cs`.

## 이번에 끊은 역방향

| 전 | 문제 | 조치 | 수단 |
|---|---|---|---|
| `StateMachine.cs` 안의 `EntityState` + 상태 14종 | 기계가 `Entity`·`Combat`·`Physics` 를 앎 | → `Entities/Support/EntityStates.cs` | 옮김 |
| `HitData.cs` 의 `IHittable`·`IDamageable` | 시그니처에 `Combat` | → `Entities/Support/IHittable.cs` (지금은 `EntityContracts.cs`) | 인터페이스 파일 (Actor 층으로) |
| `WallFinder` (Skill 폴더에 있던 것) | Core `HitData` 가 Skill 폴더 클래스를 부름 | → `Core/Support/WallFinder.cs` | 옮김 (아래로) |
| `Core/Support/BattleRegistry.cs` | `Entity` 목록 레지스트리 | → `Entities/Support/` | 옮김 |
| `Core/Support/CombatLayers.cs` | `Attack` 히트박스에 레이어 적용 | → `Entities/Support/` | 옮김 |
| `Core/Components/AudioManager.cs` | `CombatEvents` 구독 · 씬 흐름 전용 | → `FlowScene/Components/` | 옮김 |
| `Core/Components/CameraFollow.cs` · `CameraAnchor.cs` | `StageBounds`·`Player`·`BeltScroll` | → `Stage/Components/` | 옮김 |
| (Actor 작업 때) `BattleVfx.cs` 의 `SkillVfx` 구조체 · `Vfx/Data/VfxClip.cs` | 히트 경로가 들고 다니는 연출 *데이터* | → `Core/Support/SkillVfx.cs` · `Core/Support/VfxClip.cs` | 옮김 (아래로) |

MonoBehaviour 파일(`AudioManager`, `Camera*`)은 `.meta` 와 함께 `git mv` — GUID 그대로라
씬 · 프리팹 연결이 유지된다. 나머지 셋(`EntityStates`, `IHittable`, `WallFinder`)은
MonoBehaviour가 아닌 새 파일이라 GUID 영향이 없다.

검증: `Assembly-CSharp` · `Assembly-CSharp-Editor`(테스트 포함) 컴파일 오류 0.
