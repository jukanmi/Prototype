# Prototype — 클래스다이어그램 코드 반영분

`클래스다이어그램.canvas` + `프로토타입.pdf` + `기획 가지치기.pdf` 를 코드로 옮긴 결과물.
전부 `Prototype` 네임스페이스. 외부 의존성은 `com.unity.inputsystem` 하나.

## 좌표 규약 (2.5D 벨트스크롤)

논리 좌표는 3D 를 쓴다.

| 축 | 의미 |
|---|---|
| X | 좌우 (벨트 진행 방향) |
| Z | 깊이 (바닥 안쪽/바깥쪽) |
| Y | 높이 (점프 · 띄우기) |

렌더는 `BeltScrollView` 가 담당한다. 스프라이트는 `(X, Z*depthToScreen + Y)` 위치에,
그림자는 항상 바닥 `(X, Z*depthToScreen)` 에 고정된다. 정렬 순서는 Z 기준.

## 폴더

```
Core/       Enums, HitData, Interfaces, CombatStateRules, TimeControl, BattleRegistry
Stats/      Stat, Energy, Stats, Energies
StateMachine/  IState, StateMachine, States/EntityStates
Entities/   Entity, Physics, Combat, Attack, BeltScrollView
Characters/ Player, Ally, Enemy, EnemyData
Control/    Control, PlayerControl, EnemyControl, AllyControl
Skill/      SkillData, SkillState, SkillContext, ISkillEffect, Effects, EffectRunner
Deck/       ComboCard, Deck / Hand / Discard
Battle/     BulletTimeController, ComboSlotBoard, ComboExecutor, ComboPredictor, TargetSelector
yg/         씬 흐름 — Boot / MainMenu / Battle 전환 (namespace Prototype.YG, yg/README.md)
```

## 테스트 씬 — 메뉴 한 번으로 생성

수동 조립이 귀찮으면 **`Prototype ▸ 테스트 씬 만들기`** 를 누른다.
`Assets/Editor/TestSceneBuilder.cs` 가 아래를 전부 만든다.

| 생성물 | 위치 |
|---|---|
| 스킬 에셋 20개 (콤보 16 + 고유기 4) | `Assets/Skills/` |
| EnemyData (고블린) | `Assets/Data/` |
| 머티리얼 8개 | `Assets/Prefabs/Materials/` |
| 프리팹 6개 (Player, Ally ×4, Enemy) | `Assets/Prefabs/` |
| 씬 | `Assets/Scenes/TestBattle.unity` |
| `Wall` 레이어 | ProjectSettings |

씬에는 플레이어 1 + 동료 4(탱/전/궁/마) + 적 4마리, 사방 벽, 비스듬한 직교 카메라,
`BattleSystem` 오브젝트(BulletTime / Board / Executor / Predictor / TargetSelector /
DebugComboHUD / BattleLogSettings)가 배치되고 참조까지 전부 연결된다.

### 테스트 씬의 표현 방식

스프라이트 대신 **3D 프리미티브**로 그린다. 논리 좌표(X 좌우 / Z 깊이 / Y 높이)를
눈으로 직접 보려는 목적이라 `BeltScrollView` 는 붙이지 않았다.
스프라이트 작업이 붙으면 Body 메시를 SpriteRenderer로 갈고 `BeltScrollView` 를 연결하면 된다.

바닥은 **콜라이더가 없는 시각물**이다. 높이(Y)는 `Physics` 가 코드로 계산하므로
유니티 물리와 싸우면 안 된다. 벽만 진짜 콜라이더 — 넉백 → 벽 바운드 전이를 여기서 확인한다.

### 화면 HUD (`DebugComboHUD`)

UI 가 붙기 전까지 쓰는 임시 조작기. OnGUI 로만 그려서 캔버스가 필요 없다.
게이지 · 덱/손패/Discard 장수 · 손패 목록 · 슬롯별 예측 상태와 강화/기본 · 조준 정보 ·
파티 HP/상태 · 적 HP/상태/높이/공중히트 카운트를 실시간으로 보여준다.

불릿타임 중 조작:

| 입력 | 동작 |
|---|---|
| 카드 클릭 · 드래그 | 손패 카드 선택 (조준 시작) · 순서 교환 |
| 방향키 / 마우스 이동 | 조준점 이동 |
| 좌클릭 | 조준 확정 + 빈 슬롯에 배치 |
| 우클릭 | 조준 취소 |
| `Space` | 실행 |

### 확인해 볼 콤보

1. `Space` 로 불릿타임 진입 (게이지는 시작부터 full)
2. `소용돌이 베기`(모으기) 배치 → 적 무리 한가운데를 클릭. 반경 안 적 수와 헛침 경고를 HUD 에서 확인
3. `올려베기`(띄우기) 배치 → 적 클릭. 예측이 `AerialHit [강화]` 로 뜨는지 확인
4. `연속 사격` 배치 → 공중 콤보 유지
5. `강력 사격` 배치 → `Knockback`, 벽에 닿으면 `WallBound`
6. `Space` → 실행. 콘솔에서 텔레포트 · Z 정렬 · 흡입 마릿수 · 상태 전이가 순서대로 찍힌다

## 씬 조립 순서 (수동)

### 1. 캐릭터 프리팹 (Player / Ally / Enemy 공통)

루트 GameObject 에:

- `Rigidbody` — 설정은 `Physics.Awake()` 가 강제하므로 그대로 둬도 된다
- `CapsuleCollider` (isTrigger = false) — 벽 충돌용
- `Physics` — `groundY` 는 바닥 높이, `wallMask` 는 벽 레이어
- `Combat` — maxHealth 등
- `Player` / `Ally` / `Enemy` 중 하나
- `PlayerControl` / `AllyControl` / `EnemyControl` 중 하나

자식으로:

- `Attack` (Collider, isTrigger = true) — 평타 히트박스. 비활성 상태로 시작한다
- `Attack` 하나 더 — 스킬 히트박스. `Entity.skillAttack` 에 연결. 비우면 평타 것을 재사용
- `View` (SpriteRenderer) + `Shadow` (SpriteRenderer) — `BeltScrollView` 에 연결

레이어를 나눠 두면 좋다: `Ally`, `Enemy`, `Wall`. Attack 히트박스는
Physics Settings 의 Layer Collision Matrix 에서 상대 진영만 부딪히게 잘라 둘 것.

### 2. 전투 매니저 오브젝트

빈 GameObject 하나에 아래를 전부 붙인다.

- `BulletTimeController`
- `ComboSlotBoard` (slotCount 3~4)
- `ComboExecutor`
- `ComboPredictor`
- `TargetSelector`

`BulletTimeController` 의 `player` 필드에 Player 를, Player 의 `party` 배열에
동료 4명(탱/전/궁/마)을 넣는다.

### 3. 스킬 에셋

`Create > Prototype > Skill Data` 로 만든다. **에셋 1개 = 스킬 1개.**

- `requireState` = 선행 조건, `resultState` = 결과 상태 (예측 표시용)
- `targeting` = 조준 방식. `GroundPoint`(좌표 하나) · `Direction`(방향) · `None` 셋만 쓴다.
  `EnemyUnit` 은 사용 중단 — 대상은 시전 순간 `SkillState.ResolveTarget` 이
  좌표에서 최근접 적으로 다시 뽑는다
- `radius` = `GroundPoint` 의 유효 반경. **최소 3** (`SkillTableBuilder` 검증이 잡는다)
- `hitDataList` = 다단 히트. `hitInterval` 간격으로 순차 발동
- `effects` = `PullEffect`, `AirborneEffect` 등을 조합. switch 분기 없음

기획서 SK-01~05 대응 예시:

| ID | 이름 | requireState → resultState | effects |
|---|---|---|---|
| SK-01 | 소용돌이 베기 | Neutral → LightHit | `PullEffect` |
| SK-02 | 올려베기 | LightHit → AerialHit | `AirborneEffect` |
| SK-03 | 돌진 베기 | Neutral → LightHit | `ChargeEffect` |
| SK-04 | 연속 사격 | AerialHit → AerialHit | (hitDataList 다단) |
| SK-05 | 강력 사격 | AerialHit → Knockback | (hitDataList, mode = AwayFromCaster) |

### 4. 덱 구성

각 `Ally` 의 `equipped` 에 카드 4장을 넣고, 전투 시작 전에
`BulletTimeController.BuildDeckFromParty()` 를 호출하면 16장 덱이 만들어진다.
**덱 수정은 포스트 배틀에서만** — 전투 중 호출 금지.

## 조작 (PlayerControl)

| 키         | 동작                      |
| --------- | ----------------------- |
| 방향키       | 이동 (←/→ = X축, ↑/↓ = Z축) |
| X         | 평타                      |
| C         | 점프                      |
| LeftShift | 대쉬                      |
| Z         | 손패 맨 왼쪽 카드 사용 (실시간)     |
| Space     | 불릿타임 진입 · 콤보 실행 + 해제    |

동료 고유기 직접 발동 키는 없앴다. `Ally.CastSelfSkill()` API 는 남아 있으니
필요하면 AI 나 다른 트리거에서 호출하면 된다.
불릿타임 중 방향키는 `TargetSelector` 가 조준용으로 직접 읽는다 — 이동은 어차피 정지 중이라 막혀 있다.
손패 카드 선택 · 순서 교환은 전부 마우스( `ComboBoardUI` )다.

## 시간 제어

`Time.timeScale` 은 **쓰지 않는다**. `TimeControl.Scale` 이 게임플레이 dt 에만 곱해진다.

- 엔티티 · 상태머신 · 물리 → `TimeControl.DeltaTime`
- UI · 조준 · 지휘 입력 · 게이지 → `TimeControl.UnscaledDeltaTime`

불릿타임 중 `Scale = 0` 이 되어 적과 동료는 완전히 멈추지만
카드 조작과 조준은 계속 돌아간다.

## 손패 → 슬롯 배치 흐름

UI 가 아직 없으므로 아래 순서를 직접 호출하면 된다.

```csharp
// 1. 카드를 집는다 — 조준 시작
targetSelector.Begin(hand.Get(handIndex).Data);

// 2. 마우스를 움직이면 TargetSelector 가 CursorPoint / EnemiesInRange 를 갱신
//    WillWhiff == true 면 반경 안에 적이 없다는 뜻 (모으기 헛침 경고)

// 3. 확정 후 슬롯에 배치
TargetInfo info = targetSelector.Confirm();
bulletTime.PlaceFromHand(handIndex, slotIndex, in info);

// 4. 예측 결과 읽기
predictor.Simulate(board.Slots);
bool enhanced = predictor.IsChained(board.Slots, slotIndex);
```

## 콘솔 디버그 로그

`BattleLog` 이 전 계층에 깔려 있다. 카테고리별로 색이 다르고 프레임 번호가 붙는다.

| 카테고리 | 색 | 찍히는 것 |
|---|---|---|
| `State` | 하늘 | 상태머신 전이, 슈퍼아머 **거부**, 사망 **관통** |
| `Combat` | 빨강 | 적중 / 피격 / 무적으로 흘림 / 보호막 흡수 / 흡혈 / airHitCount·중력배율 / 사망 / 기상 완료 |
| `Physics` | 회색 | 점프 G 계산, 대쉬, 넉백, 띄우기, 착지, 벽 접촉, Z 정렬, 텔레포트 |
| `Skill` | 노랑 | 스킬 시전 헤더, 효과 적용, n/m타 발동, 종료, PullEffect 흡입 마릿수 |
| `Deck` | 보라 | 드로우 목록, 덱 소진 재셔플, 미사용 손패 소멸, 덱 구성 |
| `Bullet` | 청록 | 불릿타임 진입 / 해제 / **진입 거부 사유** |
| `Combo` | 초록 | 슬롯 배치·회수·순서변경, 실행 큐, 슬롯별 실행, 재타겟, 타임아웃 |
| `Predict` | 주황 | 슬롯별 예측 상태 + 강화/기본 판정, 조준 확정, 헛침 경고 |

**끄는 법** — 씬 아무 오브젝트에 `BattleLogSettings` 를 붙이고 `mask` 에서 카테고리를 체크 해제한다.
코드로는 `BattleLog.Mask = LogCategory.Combat | LogCategory.Combo;` 처럼 직접 넣어도 된다.

로그 호출에는 `[Conditional("UNITY_EDITOR")]` / `[Conditional("DEVELOPMENT_BUILD")]` 가 붙어 있어
릴리즈 빌드에서는 **호출 자체가 사라진다**. 문자열 보간 비용도 남지 않는다.

`[HideInCallstack]` 덕분에 콘솔에서 더블클릭하면 BattleLog 내부가 아니라
실제로 로그를 찍은 코드로 바로 점프한다.

### 콤보 한 번의 출력 예시

```
[State]   f120  Ally_Tanker 준비 완료 | Ally | Control AllyControl | HP 100
[Bullet]  f430  불릿타임 진입 — TimeControl.Scale = 0 (Time.timeScale 미사용)
[Deck]    f430  드로우 5/5장 | 덱 잔여 11 | 소용돌이 베기, 올려베기, ...
[Predict] f455  조준 확정: 소용돌이 베기 | GroundPoint | point (3.0, 0.0, 1.5)
[Combo]   f455  슬롯 0 ← 소용돌이 베기 | 시전자 Ally_Tanker | 조준 GroundPoint
[Predict] f455  슬롯 0 소용돌이 베기: 선행 Neutral → 예측 LightHit [강화]
[Combo]   f501  콤보 실행 시작 — 2슬롯
[Combo]   f501  ── 슬롯 0: 소용돌이 베기 / Ally_Tanker
[Skill]   f501  Ally_Tanker 시전: 소용돌이 베기 | Gather | 선행 Neutral → 결과 LightHit | 불릿타임 | 히트 1단
[Physics] f501  Ally_Tanker 텔레포트 (0.0, 0.0, 0.0) → (3.0, 0.0, 1.5)
[Skill]   f501    └ 효과 적용: PullEffect
[Combat]  f501  Ally_Tanker → Enemy_01 적중 | dmg 5 | TowardCaster | 결과요청 LightHit
[Physics] f501  Enemy_01 Z 정렬 2.40 → 1.50 (모으기 보정)
[Physics] f501  Enemy_01 넉백 dir=(-0.8, 0.0, 0.0) force=12
[Combat]  f501  Enemy_01 피격 | Neutral → LightHit | HP 35/40 | 경직 0.4s
[Skill]   f501    └ PullEffect: 중심 (3.0, 0.0, 1.5) 반경 4 → 2마리 흡입
[State]   f503  Enemy_01: IdleState → HitState
```

## 아직 없는 것 (3단계)

`BattleHUD`, `StageManager`, `StageData`, `EnemySpawner`, `RewardSystem`,
`SettingsManager`. 씬 · UI 의존이라 별도 작업이 필요하다.

`GameManager` · `SceneLoader` · `AudioManager` 와 메인화면 ↔ 배틀 씬 전환은
`yg/` 에 들어갔다 (`Prototype.YG`). 씬 조립은 **`Prototype ▸ YG ▸ 메인화면 흐름 씬 만들기`**
메뉴 하나로 끝난다. 자세한 내용은 `yg/README.md`.
