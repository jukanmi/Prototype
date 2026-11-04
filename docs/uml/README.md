# 모듈 UML — 역방향 의존 정리

층마다 **현황 UML을 그리고 → 역방향 의존을 끊고 → 결과를 이 폴더에 남긴다.**

UML 원본은 PlantUML(`*.puml`)이다. VS Code `PlantUML` 확장(jebbs.plantuml)이나 IntelliJ에서 바로 미리보기된다.
`.md` 는 설명 · 변경 내역, `.puml` 은 그림.
asmdef · 네임스페이스 · 이식은 이 작업 범위가 아니다 (`docs/Porting_Module_Plan.md`).

## 유스케이스 다이어그램

이식 범위를 정하는 기준. 괄호 안 클래스가 클래스 다이어그램으로 넘어가는 연결점이다.
플레이어가 직접 안 하는 처리(피격 · 웨이브 스폰 · 자동 카드)도 `게임 시스템` 액터를 두고 유스케이스로 넣었다.

| 파일 | 범위 |
|---|---|
| [usecase-00-Overview.puml](usecase-00-Overview.puml) | 액터 5 + 하위 시스템 5개 개요 |
| [usecase-01-App.puml](usecase-01-App.puml) | ① 게임 시작 · 파티 선택 · 설정 · 키 바인딩 · 재시작 |
| [usecase-02-Run.puml](usecase-02-Run.puml) | ② 런 맵 · 상점 · 이벤트 · 휴식 · 레벨업 · 합성 · 결과 |
| [usecase-03-Combat.puml](usecase-03-Combat.puml) | ③ 이동 · 평타 · 대시 패리 · 고유 스킬 · 피격 · 상태이상 · 가드 |
| [usecase-04-Tactic.puml](usecase-04-Tactic.puml) | ④ 불릿타임 · 카드 배치 · 조준 · 콤보 실행 · 어시스트 · 자동 카드 |
| [usecase-05-Stage.puml](usecase-05-Stage.puml) | ⑤ 웨이브 · 적 행동 · 보스 패턴 · 클리어 · 경험치 |

제외: 태그 교대(삭제됨) · 동료 고유기 ZXCV(삭제됨) · `DeckBuilderUI`(개발용) · `CastTester` · `TrainingDummy`(테스트용).

## 클래스 다이어그램 — 층마다 한 장

| 파일 | 층 | 폴더 |
|---|---:|---|
| [01-Kernel.puml](01-Kernel.puml) | 0 | Core · Stats · StateMachine |
| [02-Actor.puml](02-Actor.puml) · [02-Actor-Hit.puml](02-Actor-Hit.puml)(시퀀스) | 1 | Entities |
| [03-Input.puml](03-Input.puml) | 1.5 | Input |
| [04-Skill.puml](04-Skill.puml) | 2 | Skill |
| [05-Agent.puml](05-Agent.puml) | 3 | Characters · Control · AI |
| [06-Stage.puml](06-Stage.puml) | 4 | Stage |
| [07-Party.puml](07-Party.puml) | 4.5 | Party |
| [08-Run.puml](08-Run.puml) | 5 | Progression |
| [09-Tactic.puml](09-Tactic.puml) · [09-Tactic-UI.puml](09-Tactic-UI.puml) | 6 | Battle · Deck (로직 · UI) |
| [10-App.puml](10-App.puml) | 9 | FlowScene |
| [11-Vfx.puml](11-Vfx.puml) | 10 | Vfx |

읽는 법: 회색 패키지 = 다른 층(이름 옆 괄호가 층 번호). 점선 삼각형(`..|>`) = 위층이 아래층 인터페이스를 구현하는 자리.
멤버는 **다른 층이 쓰는 공개 면**만 적었다. 내부 구현 · private은 생략.

## 층 순서 — 역방향 0

낮은 번호가 아래층. **위 → 아래 참조만 허용.** 같은 층 안의 폴더끼리는 자유.
그림: [00-Layers.puml](00-Layers.puml)

| # | 층 | 폴더 | 메모 |
|---:|---|---|---|
| 0 | Kernel | Core · Stats · StateMachine | 규칙 · 데이터 · 기계. `SkillVfx`·`VfxClip`(연출 *데이터*), `GameplayModal` 포함 |
| 1 | Actor | Entities | 몸 · 물리 · 피격 · 상태. 인터페이스 6개 — [02-Actor](02-Actor.md) |
| 1.5 | Input | Input | 장치 입력. 아무 게임 규칙도 모른다 |
| 2 | Skill | Skill | 스킬 · 효과 · `ComboCard` |
| 3 | Agent | Characters · Control · AI | 몸의 종류(Player/Ally/Enemy) · 조작 · 적 AI · 공격 토큰 |
| 4 | Stage | Stage | 방 · 웨이브 · 스폰 · 엘리트 표기 |
| 4.5 | Party | Party | 파티 조합 데이터(`PartyLoadout` · `PartyCatalog`) |
| 5 | Run | Progression | 런 **데이터** — 경험치 · 골드 · 지도 · 카드 제안 규칙 |
| 6 | Tactic | Battle · Deck | 불릿타임 · 콤보 · 어시스트 · 전투 UI · 파티 조립 · 레벨업 창 |
| 9 | App | FlowScene | 씬 흐름 · `GameManager` (`IStageContext` · `IRunHost` 구현) |
| 10 | Presentation | Vfx | **맨 위 표현층** — 아무도 Vfx를 직접 부르지 않는다 |

Vfx를 맨 위에 둔 이유: 본게임에서 연출을 갈아 끼울 때 Vfx 폴더만 바꾸면 되게 하려고.
Input을 Agent 아래로 내린 이유: 장치 입력은 게임 규칙을 몰라야 하고, 조작(PlayerPilot)이 그것을 읽는다.

검증 방법: 폴더 간 타입 이름 참조를 집계해 층 순위를 거스르는 것을 센다(주석 · 문자열 · enum 멤버 · `X.Y` 멤버 접근 제외).
이름이 겹치는 `Entry` · `Row` · `Result`(각 폴더의 중첩 타입) · `Phase` · `Control`(속성 이름) · `Sample`(메서드 이름)은
각각 확인 후 제외했다. 결과 **0건**, `Assembly-CSharp` · `Assembly-CSharp-Editor` 컴파일 오류 0.

## 끊은 내역

### 옮김 (클래스가 엉뚱한 층에 있었다)

| 클래스 | 전 → 후 |
|---|---|
| `EntityState` + 상태 14종 | StateMachine → Entities |
| `WallFinder` | Skill → Core |
| `BattleRegistry` · `CombatLayers` · `GroundRules`(`GroundRegistry`) · `EffectUtil` | Core · Stage · Skill → Entities |
| `SkillVfx` · `VfxClip` | Vfx → Core |
| `AttackRangePreview` | AI → Entities |
| `SkillFxKind` | Vfx → Skill (`ISkillFx.cs`) |
| `ComboCard` (+ `GoldenDamageMul`) | Deck · Progression → Skill |
| `PartyMemberData` · `PlayerData` | Party → Characters |
| `AttackTokenPool` · `EnemyAttackTokens` · `SpawnEntry` | Stage → AI |
| `EliteMarkRules` | Vfx → Stage |
| `GameplayModal` | Progression → Core (등록식으로 바꿈) |
| `AudioManager` | Core → FlowScene |
| `CameraFollow` · `CameraAnchor` | Core → Stage |
| `InputMapSwitcher` | Input → Battle (전술 페이즈를 읽는다) |
| `AssistControlModule` (+ `AssistReady/MoveState`) | Control → Battle (어시스트 시스템) |
| `LevelUpSession` · `DeckBuilderUI` | Progression → Battle/Components/UI (전투 씬 UI) |
| `PartyAssembler` | Party → Battle (전투 씬에 몸을 세운다) |
| `RunMapRules.EncounterOf` | Stage `EncounterModifierRules.For` → Progression (`MapNode`를 읽는다) |

MonoBehaviour · ScriptableObject 파일은 `.meta` 째 `git mv` — GUID가 그대로라 씬 · 프리팹 · 에셋 연결이 유지된다.

### 인터페이스 파일 (아래층에 두고 위층이 구현)

| 파일 | 층 | 구현하는 쪽 | 끊은 것 |
|---|---:|---|---|
| `Entities/Support/IHittable.cs` | 1 | `Combat` | Core `HitData.cs` → `Combat` |
| `Entities/Support/IControl.cs` | 1 | `Control` | `Entity.Control` → Control |
| `Entities/Support/IAnimatedState.cs` | 1 | `SkillState` · `AssistReady/MoveState` | `EntityAnimator` → Skill · Agent 상태 |
| `Entities/Support/IGroundPlate.cs` | 1 | `GroundPlate` | `GroundRegistry` → Stage |
| `Entities/Support/IBattleVfx.cs` (+`BattleVfxSink`) | 1 | `BattleVfx.Sink` | Entities · Skill → `BattleVfx.Cast/Impact/WallBounce` |
| `Skill/Support/ISkillFx.cs` (+`ISkillFxFactory` · `SkillFxSink`) | 2 | `SkillFx` | `SkillState` → `SkillFx` |
| `Skill/Support/ITauntable.cs` | 2 | `EnemyControl` | `TauntEffect` → `EnemyControl` |
| `Stage/Support/IStageContext.cs` (+`StageContext`) | 4 | `GameManager` | `StageDirector` · `StageRoom` → `GameManager` · `MapNode` |
| `Progression/Support/IRunHost.cs` (+`RunHost`) | 5 | `GameManager` | `RunProgression.Current` · `BulletTimeController` · `PartyAssembler` → `GameManager` |
| `Battle/Support/IStageRestarter.cs` | 6 | `BattleSceneController` | `BattleRestartUI` → `BattleSceneController` |

### 이벤트 (아래층이 쏘고 위층이 구독)

| 이벤트 | 구독 | 전에는 |
|---|---|---|
| `Enemy.Created` | `EnemyStateTint` (Vfx) — 몸 색 컴포넌트 부착 | `Enemy.Awake`가 `AddComponent<EnemyStateTint>` |
| `Enemy.Defeated` | `ExpRewards` (Run) — 경험치 적립 | `Enemy`가 `ExpRewards.Award` 직접 호출 |
| `StageDirector.AnyWaveCleared` | `CastDirector` (Tactic) — 남은 시전 회수 | `CastDirector.RecallScene()` 직접 호출 |
| `StageDirector.AllWavesCleared` | `LevelUpSession` (Tactic) — 정산 후 레벨업 창 | `ExpRewards.Settle()` · `LevelUpSession.RequestOpen()` 직접 호출 |

구현을 꽂거나 구독하는 자리는 전부 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` +
`[UnityEditor.InitializeOnLoadMethod]`(에디터 전용) 두 개를 단다 — 에디트 모드(EditMode 테스트)에서도
예전과 같이 연결돼 있어야 해서다. 정적 이벤트는 `SubsystemRegistration`에서 비운다(Domain Reload 꺼짐 대비).
