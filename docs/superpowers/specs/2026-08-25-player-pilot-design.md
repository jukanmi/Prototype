# 조종사 분리 — 빙의 모델 폐기

- 작성일: 2026-08-25
- 상태: 구현됨 (`7955701`)
- 범위: 유저 조작을 몸에서 떼어 씬에 하나뿐인 조종사로 옮긴다. 적 AI는 그대로 몸에 남는다.

## 목적

태그 게임인데 **조종사를 몸 수만큼 복제해 두고 껐다 켰다** 하고 있었다.
로스터 5명이면 `PlayerControl` 인스턴스도 5개고, 태그 교대는 그중 하나만
`enabled = true`로 켜는 일이었다(빙의).

조종사는 하나고 몸이 바뀌는 게 맞다. 그렇게 바꾸면 아래 두 가지가
**구조적으로 불가능해진다** — 규칙으로 막는 게 아니라 표현할 수단이 사라진다.

### 새던 것 1 — 컷인 중 시전자가 제 발로 걸어 다녔다

`ComboExecutor.Run`의 순서가 이랬다:

```
Stage.Enter(caster)      몸 활성화 + AllyControl 부착
PlayCutin(...)           ← 이 구간 내내 IsCommanded == false
RunSlot(slot)
  → caster.IsCommanded = true      여기서야 잠근다
```

컷인이 도는 동안 시전자는 **활성 + 자율 BT + 잠기지 않음**이다.
시간이 멈춰서 무해하지도 않았다 — 컷인 배속이 0이 아니라
`SkillCutinUI.timeScale = 0.15f`라 `StateMachine.Tick(dt)`까지 돌아
BT가 낸 명령이 실제로 소비됐다.

### 새던 것 2 — 프리팹과 씬이 조용히 어긋났다

`Assets/Prefabs/Ally.prefab`에는 `PlayerControl`이 **없는데**, 씬 9개가
Ally 인스턴스마다 개별로 얹어 놨다(씬당 4개, `m_PrefabInstance: {fileID: 0}`).
어떤 몸에 붙고 어떤 몸에 안 붙어도 게임이 그냥 돌아가 버려서 눈치채기 어려웠다.

## 범위

```
씬 계층 (변경 없음)
  BattleInput   ← PlayerInputController · BattleCommander · TagSwapController · PlayerPilot
  Player / Ally_*   ← Pilotable
  Enemy_*           ← EnemyControl

몸에 붙는 Control:
  전:  Player ← PlayerControl
       Ally   ← PlayerControl + AllyControl   (UseControl<T>로 하나만 enable)
  후:  없음. 유저가 모는 몸은 그냥 몸이다
```

**씬 파일은 건드리지 않았다**(다른 작업과의 충돌 회피).
`BattleInput.prefab`이 9개 씬에 프리팹 인스턴스로 들어가 있어, 거기에
`PlayerPilot`을 붙이는 것만으로 전 씬에 전파됐다.

## 구성

### 1. `Entity` — 의도를 몸이 든다

`Control`이 들고 있던 상태는 셋뿐이었고 전부 "이번 프레임의 의도"다:
`Command`, `MoveDirection`, `attackBuffer`. 이걸 `Entity`로 올렸다.

**근거는 추측이 아니라 이 저장소의 선례다.** `Entity.IsCommanded`가 왜
`Control`이 아니라 `Entity`에 있는지 그 주석이 직접 설명한다 —
*"몸을 모는 주체가 바뀌는데 지휘 플래그가 Control에 붙어 있으면 갈아타는 순간
값이 통째로 사라진다."* 의도도 똑같다.

```csharp
public Command Command { get; private set; }
public Vector3 MoveDirection { get; private set; }

public void Drive(Command command, Vector3 moveDirection);
public void Consume();        // Command만 지운다 — MoveDirection은 남긴다
public void ClearCommand();   // 의도만 비운다. 선입력은 안 건드린다
public void ClearIntent();    // 선입력까지 전부

public void BufferAttack(float window);
public void TickAttackBuffer(float dt);
public bool HasAttackBuffer { get; }
public bool TryConsumeAttackBuffer();
public void ClearAttackBuffer();

public bool IsPiloted { get; set; }   // 조종사가 켜고 끈다
```

`Consume()`이 `MoveDirection`을 남기는 것은 **의도된 기존 동작**이다.
걷는 도중 명령만 소비되는 경우가 있다.

### 2. `PlayerPilot` — 씬에 하나뿐인 조종사

`BattleInput`에 붙는다. `BattleCommander` 옆이 제자리다 —
둘 다 몸과 무관한 입력 처리이고, 그쪽은 이미 밖에 있던 검증된 패턴이다.

```csharp
public Entity Body { get; private set; }
public void Take(Entity body);   // TagSwapController가 부른다
public void Release();
public void Tick(float dt);      // Update가 부른다. 테스트가 직접 펌프한다
```

`Tick`의 조기 반환 순서는 **재작성하지 않고 그대로 옮겼다**. 지금 조작 감각이
그 순서에서 나온다:

1. `ClearCommand()`
2. `TickAttackBuffer(dt)` — **아래 어떤 return보다 먼저.** 지휘·정지·경직으로
   빠져나가는 동안 창이 얼면 풀리는 순간 묵은 입력이 터진다
3. 대시 쿨 감소
4. `IsCommanded`면 return
5. `PlayerInputController.Instance == null`이면 return — **매번 다시 읽는다.**
   캐싱하면 입력 호스트 교체 시 파괴된 인스턴스를 붙든다
6. `TimeControl.IsFrozen`이면 return
7. 차징 중도 해제 — `IsBusy`보다 **먼저** 본다(모으는 중엔 슈퍼아머라 아래가 다 막힘)
8. `IsBusy` / `IsStunned`
9. 입력 → `Drive(command, direction)`

**실행 순서**: `[DefaultExecutionOrder(-100)]`. 몸의 `Update`보다 먼저 돌아야
이번 프레임에 채운 의도를 같은 프레임의 상태머신이 읽는다. 뒤집히면 조작이
한 프레임씩 밀린다.

**폴백**: `Start()`에서 `Body`가 비어 있으면 씬의 `Pilotable` 중 하나를 잡는다
(플레이어 우선). 태그 컨트롤러 없이 씬을 그냥 열어 돌리는 경우를 위한 것으로,
예전 `Entity.Awake`의 `FirstEnabledControl()`이 하던 일이다.

### 3. `Pilotable` — 조종 가능 표식 + 캐릭터 수치

```csharp
[SerializeField] private float dashCooldown = 0.6f;
[SerializeField] private float attackBufferWindow = 0.25f;
private float dashTimer;
```

**왜 몸에 두나.** 셋 다 캐릭터 성능이지 플레이어 성능이 아니다 —
대시 쿨은 그 캐릭터의 능력이고, 선입력 창은 그 캐릭터의 콤보 모션 길이
(`cancelStart`)에 묶인다. 조종사에 두면 전사로 대시하고 교대한 마법사가
그 쿨을 물려받는다.

쿨은 **조종 중인 몸만** 흐른다. 벤치에 내려간 몸은 조종사가 안 부른다 —
예전에 `SetActive(false)`로 `Update`가 통째로 멈추던 것과 같은 동작이다.

### 4. `Control` — 적 전용 드라이버로 얇아진다

`EnemyControl`은 몸마다 있어야 한다. 적은 각자 AI가 필요하다.

```csharp
protected Entity Owner { get; private set; }
public virtual void Tick(float dt);
protected void Clear();                                   // Owner.ClearCommand()
protected void Drive(Command command, Vector3 direction); // Owner.Drive(...)
```

공격 버퍼 API는 베이스에서 뺐다 — AI는 연타를 칠 수단이 애초에 없어야 한다.
`Entity.Update`의 `Control?.Tick(dt)`는 그대로다. 유저가 모는 몸은
`Control`이 null이라 그냥 건너뛴다.

### 5. `TagSwapController` — 빙의 스위치가 한 줄로

```csharp
// 전: 로스터를 전부 돌며 몸마다 UseControl<PlayerControl/AllyControl>()
// 후:
private void Possess(Entity incoming) => pilot?.Take(incoming);
```

`ICasterStage.Enter`의 `UseControl<AllyControl>()`은 **삭제**했다.
불려 나온 시전자에 아무도 안 붙으므로 컷인 창 문제가 원인째 사라진다.

### 6. `EntityStates` — `Entity`에서 읽는다

`protected Control Control => Entity.Control` 접근자를 없애고 모든 읽기를
`Entity.X`로 바꿨다. 대시 패리를 열던 `Control is PlayerControl` 판별은
`Entity.IsPiloted`가 대신한다.

## 몸에 남는 것 vs 조종사로 가는 것

| 값 | 어디 | 왜 |
| --- | --- | --- |
| `Command` · `MoveDirection` · 선입력 | 몸 | 몸이 바뀌어도 안 사라져야 한다(`IsCommanded` 선례) |
| `dashCooldown` · `dashTimer` | 몸 | 캐릭터 능력이지 플레이어 능력이 아니다 |
| `attackBufferWindow` | 몸 | 그 캐릭터의 콤보 모션 길이에 묶인다 |
| 콤보 진행(`AttackState.stage`) | 몸 | 원래부터 상태머신 소유. 안 건드렸다 |
| 입력 판독 · 게이트 순서 | 조종사 | 유저 하나에 하나뿐이다 |

## 삭제

`PlayerControl` · `AllyControl` · `PartyFormation`.

`PartyFormation`은 `AllyControl.Follow`가 유일한 소비자였고, 태그 교대가
도입된 **뒤에** 들어와 사실상 태어날 때부터 안 돌았다.

## 남은 것

- 씬 인스턴스에 직접 얹혀 있던 옛 `PlayerControl`이 **누락 스크립트로 남는다**(씬당 4개,
  총 36개). 재생에는 지장 없고 경고만 뜬다. 씬을 만질 여유가 생기면
  `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`로 한 번에 정리한다.
- `SkillTestScene`은 `BattleInput` 인스턴스가 없어 조종사가 없다.
  그 씬에 `BattleInput` 프리팹을 놓거나 빈 오브젝트에 `PlayerPilot`을 붙이면 된다.
- `Pilotable`의 두 수치가 코드 기본값(0.6 / 0.25)으로 새로 붙었다.
  예전 `PlayerControl` 인스펙터에 다른 값이 저작돼 있었다면 안 넘어왔다.

## 검증

### 테스트

- `PlayerPilotIntentTests` — 게이트 7 + 인계 5.
  입력 호스트 없이 안전한가, 정지·지휘 중 명령이 새지 않는가,
  벤치 몸을 건드리지 않는가, `Take`/`Release`가 표식과 의도를 정리하는가.
- `BattleInputPrefabTests` — `Pilotable` 존재 + **빙의 잔해 감시**:
  몸에 붙은 `Control`이 `EnemyControl` 외에 있으면 실패한다.

### 씬 재생

1. 이동 · 점프 · 대시 · 평타의 **감각이 이전과 같다**.
2. 1타 도중 평타를 눌러도 2타로 이어진다(선입력 이관).
3. 대시 패리가 조종 중인 몸에서만 열린다(`IsPiloted`).
4. **불릿타임 컷인 중 시전자가 가만히 서 있다** — 걷거나 평타 치지 않는다.
5. F키 교대 시 들어온 몸만 움직인다. 두 몸이 같이 움직이지 않는다.
6. 실시간 차징 스킬이 점프키로 터진다.
7. 적의 행동은 변화 없다(`EnemyControl` 경로 무수정).
