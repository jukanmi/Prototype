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
| `A S D F G` | 손패 카드 선택 (조준 시작) |
| 좌클릭 | 조준 확정 + 빈 슬롯에 배치 |
| 우클릭 | 조준 취소 |
| `Backspace` | 마지막 슬롯 회수 |
| `Space` | 실행 |

### 확인해 볼 콤보

1. `E` 로 불릿타임 진입 (게이지는 시작부터 full)
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

## 조작

키는 코드가 아니라 `Assets/Settings/InputSystem_Actions.inputactions` 에 있다.
아래는 **기본 바인딩**이고, 런타임에 바뀔 수 있다.

### Gameplay 맵 — 전투 중 항상

| 키         | 액션           | 동작                      |
| --------- | ------------ | ----------------------- |
| WASD      | `Move`       | 이동 (A/D = X축, W/S = Z축) |
| J         | `Attack`     | 평타                      |
| K         | `Jump`       | 점프                      |
| LeftShift | `Dash`       | 대쉬                      |
| Z X C V   | `Skill1`~`4` | 동료 고유기 (라이브 페이즈)        |
| E         | `BulletTime` | 불릿타임 진입 · 실행            |
| U         | `CardUse`    | 손패 맨 왼쪽 카드 즉시 사용        |

진입과 실행이 **한 액션 · 한 키**다. 예전에는 `Execute`(Space)가 따로 있었지만
Order 페이즈에서 E 와 똑같이 Resolve 로 가는 같은 동작이라, 리바인드 화면에서
서로 다른 기능처럼 보였다. **Space 는 이제 아무 데도 안 묶여 있다.**

`BulletTimeController.Exit()` 는 `OnExecuteKey` 를 그대로 쓴다. 입력에서만 합쳤을 뿐
전술 상태머신의 두 진입점은 남아 있다.

기획서는 고유기를 ASDF 로 잡았지만 WASD 이동과 충돌한다. YUIOP 를 쓰다가
U 를 카드 사용에 내주면서 ZXCV 로 옮겼다.

### BulletTime 맵 — Order 페이즈, 조준 중이 아닐 때

손패 카드 조작. **액션이 `Navigate` 하나뿐이다** — 집기 · 놓기까지 방향에 실려 있다.
**한 번 누르면 한 칸** 가는 이산 입력이다.

| 키   | 커서 이동 중  | 카드를 집은 상태     |
| --- | -------- | ------------- |
| A/D | 카드 선택 이동 | 카드 순서 변경      |
| W   | 카드 집기    | 시전 위치 지정으로 진입 |
| S   | —        | 놓기 (순서 확정)    |

집기 · 놓기를 별도 액션으로 두지 않는 이유 — 그 키가 `Navigate` 의 방향과 겹치면
한 번 누른 W 가 "집기"와 "조준 진입"을 함께 터뜨린다. 방향 하나로 모으면 그 조합이
아예 없고, 리바인드도 컴포지트 하나만 바꾸면 네 방향이 전부 따라온다.

되돌리기는 없다. 잘못 옮겼으면 다시 집어서 되밀면 된다.

마우스 드래그 앤 드롭도 그대로 동작한다. 다만 키보드로 카드를 집고 있는 동안에는 막힌다 —
두 경로가 같은 손패를 동시에 밀면 어느 쪽이 이겼는지 화면으로 알 수 없다.
마우스는 `Navigate` 에 묶지 않는다. 묶으면 아무 데나 클릭해도 카드가 집히거나 놓인다.

### BulletTimeSkillShot 맵 — 시전 위치 지정

| 키          | 액션         | 동작                     |
| ---------- | ---------- | ---------------------- |
| WASD       | `Aim`      | 조준점 **연속** 이동          |
| 마우스 이동     | `AimPoint` · `AimDelta` | 조준점을 커서 위치로 |
| J / 좌클릭    | `Confirm`  | 확정 → 카드를 **놓고** 커서 이동로 |
| K / 우클릭    | `Cancel`   | 취소 → 카드를 **집은 채로** 복귀  |

확정과 취소가 다르게 끝나는 이유 — 위치까지 찍었으면 그 카드는 볼일이 끝났으므로 놓는다.
취소는 조준만 무르는 것이라 다시 겨냥할 수 있게 집은 상태를 남긴다.

마우스가 **실제로 움직인 프레임**에만 마우스가 키보드를 이긴다. 그 판정은 `AimDelta`
(`<Mouse>/delta`) 를 직접 읽는다 — `AimPoint` 의 프레임 차분으로 대신하면 안 된다.
맵을 켠 첫 프레임에 position 이 0 을 뱉어서, 실제 좌표가 들어오는 다음 프레임이
화면 절반만큼의 이동으로 읽히고 조준점이 손도 안 댄 마우스로 끌려간다.

조준 시작점은 **그 카드 바로 위**다. `TargetSelector.Begin` 의 기본값(가장 가까운 적)을
쓰면 벨트스크롤 투영이 깊은 z 를 `x + z·0.45`, `y + z·0.5` 로 밀어 올려서, 방 안쪽 적이
늘 화면 우측 상단에 걸린다. `ComboBoardUI` 가 카드의 화면 좌표를
`TargetSelector.ScreenToGround` 로 되돌려 넘긴다 — 마우스 피킹과 같은 길이다.

`Navigate` 와 `Aim` 이 같은 WASD 인데 맵을 나눈 이유는 **성격이 다르기 때문**이다.
카드 선택은 한 번에 한 칸(이산), 조준은 누르고 있으면 계속 이동(연속)이다.
한 맵에 두면 둘 중 하나가 반드시 이상해진다.

`Gameplay/Move` 까지 셋 다 WASD 인 것도 의도된 것이다. 정지 중에는 `PlayerControl` 이
이동을 막고, 아래 맵 전환이 나머지 둘을 갈라 놓는다.

### 맵 전환

`InputMapSwitcher` 가 매 프레임 페이즈와 조준 여부를 보고 두 맵을 여닫는다.

| 상황                          | BulletTime | SkillShot |
| --------------------------- | ---------- | --------- |
| 실시간 · Freeze · Resolve       | 닫힘         | 닫힘        |
| Order, 조준 중 아님               | **열림**     | 닫힘        |
| Order, 조준 중                  | 닫힘         | **열림**    |

**두 맵은 절대 동시에 열리지 않는다.** J 가 카드 놓기와 조준 확정 양쪽에 걸려 있어서,
겹치면 한 번 누른 J 가 조준을 확정하고 그 카드를 놓는 것까지 한 프레임에 해 버린다.
`ComboBoardUI.Update` 도 세 상태 중 하나만 도는 배타 분기다.

`OnEnter`/`OnExit` 이벤트를 안 쓰는 이유 — 두 이벤트는 Freeze 진입과 Resolve 진입에
걸려 있어 실제로 조작이 열리는 구간과 한 프레임씩 어긋난다.

### 키 프리셋

`KeyBindingPreset` (ScriptableObject) 한 장이 키 세팅 한 벌이다.
JSON 뭉치가 아니라 **항목 표**로 적는다 — 바인딩 순서가 바뀌어도 어디를 가리키는지 남는다.

| 필드 | 값 |
| --- | --- |
| `map` | `Gameplay` · `BulletTime` · `UI` |
| `action` | `Move` · `Attack` · `Skill1` … |
| `part` | 컴포지트 파트(`up`/`down`/`left`/`right`). 단일 바인딩이면 비운다 |
| `path` | `<Keyboard>/upArrow` |

`ApplyTo` 는 **기존 오버라이드를 전부 지우고** 얹는다. 지우지 않으면 이전 프리셋의 키가
이 프리셋이 안 건드리는 액션에 남는다. 항목이 빈 프리셋이 곧 "기본으로 되돌리기"다.

세 벌은 메뉴 `Prototype > 입력 - 예시 키 프리셋 만들기` 로 만든다.

| | 기본 | 방향키 프리셋 | 마우스 + 키보드 |
| --- | --- | --- | --- |
| 이동 · 카드 커서 · 조준 | WASD | 화살표 | WASD |
| 동료 고유기 | Z X C V | 1 2 3 4 | Z X C V |
| 평타 | J | Z | **좌클릭** |
| 점프 | K | X | Space |
| 대쉬 | LShift | C | **우클릭** |
| 불릿타임 | E | Space | E |
| 카드 즉시 사용 | U | A | Q |
| 조준 확정 · 취소 | J K | Z X | **좌 · 우클릭** |

이동만 옮기고 나머지를 두면 양손이 키보드 양 끝으로 벌어진다. 그래서 행동키까지 왼손으로 당긴다.

평타와 조준 확정이 같은 키인 건 **기본 세팅도 마찬가지**다(둘 다 J). 정지 중 평타는
`IsFrozen` 게이트에 막히므로 부딪히지 않는다 — 위 충돌 표의 두 번째 줄이 그 이야기다.
마우스 + 키보드에서 평타(좌클릭)가 조준 확정(좌클릭)과 겹치는 것도 같은 이유로 괜찮다.

조준 확정의 마우스 클릭 자리는 프리셋이 안 건드린다. `part` 가 빈 항목은
**첫 번째 비컴포지트 바인딩**, 즉 키보드 자리(0번)만 바꾼다.

**프리셋은 리바인드 UI 의 충돌 검사를 거치지 않는다** — 항목을 직접 얹기 때문이다.
프리셋 하나가 같은 키를 두 자리에 넣어도 아무도 안 잡으므로,
`KeyBindingPresetTests.ShippedPresets_ProduceNoConflicts` 가 저장소의 프리셋을 전부 적용해 본다.

### 런타임 키 변경

`RebindUI` 를 아무 GameObject 에 붙이면 화면 오른쪽 아래에 [조작키] 버튼이 뜬다.
프리셋 버튼은 인스펙터의 `presets` 배열에 넣은 것만 나온다.

바뀐 키는 `PlayerPrefs["input.bindings"]` 에 오버라이드 JSON 으로 들어가고,
`PlayerInputController.Awake` 가 액션을 잡은 직후 얹는다.

**충돌 검사는 직접 한다** — Input System 은 겹치는 키를 막아 주지 않고 그냥 둘 다 발동시킨다.
다만 "같이 켜져 있는 맵끼리 겹치면 충돌"로 잡으면 **기본 바인딩부터 빨개진다**.
`InputRebindRules.CanFireTogether` 가 실제 규칙을 담는다:

| 조합 | 충돌인가 | 왜 |
| --- | --- | --- |
| 같은 맵 | ○ | 늘 함께 산다 |
| BulletTime ↔ SkillShot | ✕ | `InputMapSwitcher` 가 배타로 잡는다 |
| Gameplay 이동·평타 ↔ 불릿타임 맵 | ✕ | 정지 중 `IsFrozen` 게이트에 막힌다 |
| Gameplay **지휘키** ↔ 불릿타임 맵 | ○ | E·Space·U 는 정지 중에도 산다 |

목록에서 빠지는 것 — UI 맵 전체(모듈이 이름으로 찾아 쓰는 규약), `AimPoint`·`AimDelta`
(키로 바꿀 값이 아니다), 모든 마우스 바인딩(조준 확정의 좌클릭까지 바꾸게 하면
키보드 자리를 잘못 잡았을 때 빠져나올 길이 없다), 컴포지트 머리.

키를 받는 동안에는 액션 자산 전체를 잠근다. 안 그러면 W 를 누르는 순간 카드가 집히면서
그 W 가 새 키로도 들어간다. ESC 취소는 액션이 아니라 디바이스에서 직접 듣기 때문에
전부 잠가도 동작한다.

### UI 위 클릭

버튼 액션은 **마우스로 UI를 클릭한 것**을 세지 않는다. 손패 카드를 집으려고 클릭했을 뿐인데
평타가 나가는 걸 막는다.

판정 기준이 "커서가 UI 위인가"가 아니라 "**포인터가 발동시켰고** 그 포인터가 UI 위인가"다
(`InputAction.activeControl.device is Pointer`). 키보드로 눌렀을 때는 커서가 어디 있든
통과한다 — 마우스를 손패 위에 올려 둔 채 키보드로 싸우는 것까지 막히면 안 된다.

예외는 **조준 취소**다. 빠져나오는 길은 커서가 어디 있든 열려 있어야 한다.

### 입력을 읽는 곳

`PlayerInputController` **하나뿐이다.** 다른 스크립트는 `Keyboard.current` ·
`Mouse.current` 를 직접 만지지 않는다 — 그렇게 새어 나간 키는 리바인드 대상에서 빠져
사용자가 키를 바꿔도 그것만 옛 키로 남는다. `InputSourceTests` 가 소스를 훑어 막는다.

플레이어 밖 스크립트(`TargetSelector`, 전투 UI)는 `PlayerInputController.Instance` 로 읽는다.

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
