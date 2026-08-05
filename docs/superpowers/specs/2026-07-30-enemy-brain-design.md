# 적 행동 확장 구조 (IEnemyBrain) — 설계

- 날짜: 2026-07-30
- 브랜치: `feat/combat-prototype`
- 범위: 적 종류별 행동 차이를 갈아끼울 수 있는 층을 만들고, 근접 적 하나만 구현한다.

## 1. 배경 · 문제

현재 적 AI는 [`EnemyControl`](../../../Assets/Scripts/Control/EnemyControl.cs) 하나에 if/else로 박혀 있다.

```
Tick(dt)
  ├ 재타겟 (0.5s 주기 / 타겟 사망)
  ├ 사거리(1.8) 안 → 쿨(1.5s) 만료 시 Command.Attack
  └ 밖         → MoveDirection = 방향, Command.Move
```

프로토타입 동작으로는 문제없지만 종류를 늘리는 순간 이 함수에 분기가 쌓인다. 수치는
[`EnemyData`](../../../Assets/Scripts/Characters/EnemyData.cs)에 `hp`/`atk`만 있고 사거리·쿨·이동속도는
프리팹 인스펙터 값이라 종류별 테이블도 못 만든다.

**목표**: 판단 로직을 교체 가능한 단위로 분리한다. 지금은 근접 하나만 만들되, 원거리·돌진을
나중에 추가할 때 기존 코드를 건드리지 않는다.

**비목표**: Behaviour Tree 도입, 둘러싸기·간격 유지·선회, 적 애니메이션, 스포너·오브젝트 풀.

## 2. 아키텍처

기존 계층을 유지하고 **판단 층만** 빼낸다.

```
Enemy : Entity
  ├─ EnemyData (SO)          수치 테이블 + 어떤 브레인을 쓸지
  ├─ EnemyControl : Control  어댑터. 감지·타이머·게이트 + 브레인 호출
  │    └─ IEnemyBrain        MeleeBrainAsset (지금은 이거 하나)
  └─ StateMachine            Entity 공용. 실행 계층 — 변경 없음
```

| 단위 | 하는 일 | 안 하는 일 |
|---|---|---|
| `IEnemyBrain` | 컨텍스트를 받아 이번 프레임의 의도를 돌려준다 | Transform 접근, 타겟 탐색, 타이머 보관, 상태 저장 |
| `EnemyControl` | 타겟 탐색·재타겟, 공격 쿨, `IsBusy`/경직 게이트, 의도를 `Command`/`MoveDirection`으로 반영 | 종류별 행동 판단 |
| `EnemyData` | 수치와 브레인 참조 | 로직 |

### 브레인을 무상태로 두는 이유

타이머·타겟을 전부 `EnemyControl`이 들면 브레인은 순수 함수가 된다. 그래서:

- ScriptableObject 한 애셋을 여러 적이 공유할 수 있다 (인스턴스당 상태가 없으니 안전).
- 씬 없이 단위 테스트가 된다.
- 브레인마다 타겟 탐색을 다시 구현하지 않는다.

### ScriptableObject를 쓰는 이유

`[SerializeReference]`로 인터페이스를 직렬화하는 방법도 있으나 채택하지 않는다.
클래스 이름·네임스페이스 변경 시 참조가 끊기고, 프리팹 오버라이드와 궁합이 나쁘고,
인스펙터 드롭다운을 위해 커스텀 드로어가 필요하다.

SO 애셋은 GUID 참조라 안정적이고, 인스펙터 드래그&드롭이 기본으로 되며,
수치만 다른 변종(`Brain_고블린` / `Brain_오크`)을 애셋으로 찍을 수 있다.

## 3. 인터페이스

`Assets/Scripts/AI/IEnemyBrain.cs`

```csharp
namespace Prototype
{
    /// <summary>브레인이 판단에 쓰는 입력. EnemyControl이 매 프레임 채운다.</summary>
    public struct EnemyBrainContext
    {
        public Entity self;
        public Entity target;        // 없으면 null
        public Vector3 toTarget;     // XZ 평면. 정규화 전
        public float distance;       // toTarget.magnitude
        public bool attackReady;     // 공격 쿨이 끝났는지
        public EnemyBrainParams p;   // EnemyData에서 온 수치
        public float dt;
    }

    /// <summary>브레인이 내는 결론. EnemyControl이 그대로 옮긴다.</summary>
    public struct EnemyIntent
    {
        public Command command;
        public Vector3 moveDirection;

        public static readonly EnemyIntent None = default;
        public static EnemyIntent Move(Vector3 dir)
            => new EnemyIntent { command = Command.Move, moveDirection = dir };
        public static EnemyIntent Attack(Vector3 face)
            => new EnemyIntent { command = Command.Attack, moveDirection = face };
    }

    public interface IEnemyBrain
    {
        EnemyIntent Decide(in EnemyBrainContext ctx);
    }

    /// <summary>SO로 만드는 브레인의 공통 베이스.</summary>
    public abstract class EnemyBrainAsset : ScriptableObject, IEnemyBrain
    {
        public abstract EnemyIntent Decide(in EnemyBrainContext ctx);
    }
}
```

`EnemyIntent`에 쿨 소모 플래그를 두지 않는다. `command == Command.Attack`이면 쿨을 리셋하는 것으로
`EnemyControl`이 판단한다 — 별도 필드는 항상 이 조건과 같은 값이 되어 중복이다.

`Attack` 의도에도 `moveDirection`을 실어 보낸다. [`AttackState.Enter`](../../../Assets/Scripts/StateMachine/States/EntityStates.cs#L143)가
`Control.MoveDirection`으로 `Physics.Face()`를 호출하므로, 이 값이 없으면 적이 타겟을 안 보고 때린다.
현재 코드에는 이 버그가 있다 — 공격 프레임에 `MoveDirection`이 비어 있다. 이번에 같이 고친다.

### `EnemyBrainParams`

브레인 애셋은 여러 적이 공유하므로 특정 `EnemyData`를 참조해선 안 된다. 수치만 담은
`[Serializable]` struct로 전달을 끊는다. `EnemyControl`의 인스펙터 폴백 값으로도 쓰이므로
직렬화 가능해야 한다.

```csharp
[Serializable]
public struct EnemyBrainParams
{
    public float attackRange;
    public float leashRange;     // 0이면 무제한
}
```

이동속도는 넣지 않는다 — `MoveState`가 `Stats.MoveSpeed`를 직접 읽으므로 브레인이 알 필요가 없다.

## 4. MeleeBrain — 이번에 구현하는 유일한 브레인

`Assets/Scripts/AI/Brains/MeleeBrainAsset.cs`

```
target == null                → None
leash > 0 && distance > leash → None
distance <= attackRange
    attackReady               → Attack(toTarget 방향)
    아니면                     → None (제자리 대기)
그 외                          → Move(toTarget.normalized)
```

현재 `EnemyControl` 동작과 동일하며, `leashRange`만 추가된다.
[`AllyControl`](../../../Assets/Scripts/Control/AllyControl.cs#L14)에 이미 있는 개념이고
스포너·방 경계가 들어오면 필요해진다. 기본값 `0`(무제한)이면 지금 동작이 그대로 유지된다.

## 5. EnemyControl 개편

```csharp
public class EnemyControl : Control
{
    [SerializeField] private EnemyBrainAsset brain;          // data.brain 없을 때 폴백
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private float retargetInterval = 0.5f;
    [SerializeField] private EnemyBrainParams parameters = /* attackRange 1.8, leash 0 */;

    public Entity Target { get; }
    public void SetTarget(Entity forced);                     // TauntEffect가 쓴다 — 유지
    public void SetActive(bool value);                        // Enemy.StopAI가 쓴다 — 유지
    public void ApplyData(EnemyData data);                    // 신규. Enemy.Awake에서 호출

    public override void Tick(float dt)
    {
        Clear();
        if (!active) return;
        if (Owner != null && (Owner.IsBusy || CombatStateRules.IsStunned(Owner.Combat.CombatState)))
            return;

        attackTimer -= dt;
        retargetTimer -= dt;

        Retarget();                                  // 기존 FindNearestAlly 로직 그대로
        if (brain == null) return;

        EnemyIntent intent = brain.Decide(BuildContext(dt));

        Command = intent.command;
        MoveDirection = intent.moveDirection;
        if (intent.command == Command.Attack) attackTimer = attackInterval;
    }
}
```

`brain`이 null이면 아무 명령도 내지 않는다(적이 가만히 서 있음). 조용히 기본 동작으로 넘어가지 않고
`Start`에서 `BattleLog` 경고를 한 번 남긴다 — 배선 누락을 씬에서 바로 알 수 있게.

## 6. EnemyData 확장

```csharp
[Header("전투")]
public float hp = 40f;
public float atk = 5f;

[Header("행동")]                              // ← 추가
public EnemyBrainAsset brain;
public float moveSpeed = 4f;
public float attackRange = 1.8f;
public float attackInterval = 1.5f;
public float retargetInterval = 0.5f;
public float leashRange = 0f;                 // 0 = 무제한
```

`Enemy.ApplyData()`:

```csharp
if (data == null) return;                     // 인스펙터 기본값 유지 — 기존 씬 안 깨짐

Stats.Set(StatType.AttackPower, data.atk);
Stats.Set(StatType.MoveSpeed, data.moveSpeed);   // MoveState가 읽는다
Combat.SetMaxHealth(data.hp);
enemyControl?.ApplyData(data);
```

`EnsureDefaults`가 `MoveSpeed`를 6으로 채우지만 `Awake` 안에서 `EnsureDefaults` → `ApplyData` 순서이므로
데이터 값이 이긴다. 순서를 유지해야 한다.

## 7. 파일 변경 목록

| 파일 | 종류 |
|---|---|
| `Assets/Scripts/AI/IEnemyBrain.cs` | 신규 — 인터페이스, 두 struct, `EnemyBrainParams`, `EnemyBrainAsset` |
| `Assets/Scripts/AI/Brains/MeleeBrainAsset.cs` | 신규 |
| `Assets/Scripts/Control/EnemyControl.cs` | 수정 — 판단을 브레인으로 위임 |
| `Assets/Scripts/Characters/EnemyData.cs` | 수정 — 행동 파라미터 추가 |
| `Assets/Scripts/Characters/Enemy.cs` | 수정 — `ApplyData` 확장 |
| `Assets/Editor/TestSceneBuilder.cs` | 수정 — 브레인 애셋 생성 + `EnemyData`·프리팹 배선 |

## 8. 검증

`TestSceneBuilder`로 씬을 재생성하고 플레이해서 확인한다.

1. 적 4마리가 이전과 동일하게 아군을 추적하고, 1.8 이내에서 1.5초 간격으로 평타를 낸다.
2. 공격 시 적이 타겟을 향해 돌아선다 (`Physics.Face`) — 기존 버그 수정 확인.
3. 불렛타임 진입 시 적이 멈춘다 (`TimeControl.Scale` 경로 무변경).
4. `TauntEffect` 사용 시 반경 내 적의 타겟이 시전자로 바뀐다.
5. 타겟 사망 후 0.5초 내 다른 아군으로 재타겟한다.
6. `EnemyData`를 비운 적 프리팹도 인스펙터 기본값으로 정상 동작한다.
7. `brain`이 비면 적이 정지하고 `BattleLog`에 경고가 1회 남는다.

`MeleeBrainAsset.Decide`는 순수 함수이므로 씬 없이 검증 가능하다. 다만 이 프로젝트에는 아직
테스트 어셈블리가 없다. 테스트 프레임워크 도입은 이번 범위 밖으로 두고, 위 항목은 플레이 검증으로 확인한다.

## 9. 이후 작업 (이번 범위 아님)

- `RangedBrainAsset` — 선호 거리 유지, 접근 시 후퇴
- `ChargerBrainAsset` — 조준 후 돌진
- 적 상호 밀어내기 (`Physics`) — 여러 적이 한 타겟에 겹치는 문제
- 스포너 · 오브젝트 풀 — `Enemy.StopAI()` 호출처가 아직 없다
- 브레인이 3종 이상이 되면 선택 UX 검토
