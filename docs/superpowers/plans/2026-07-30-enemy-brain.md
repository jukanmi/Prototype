# 적 행동 확장 구조 (IEnemyBrain) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 적의 판단 로직을 교체 가능한 `IEnemyBrain`으로 분리하고, 근접 브레인 하나만 구현한다.

**Architecture:** `EnemyControl`은 감지·타이머·게이트만 맡는 어댑터로 남고, 종류별 판단은 ScriptableObject 브레인이 담당한다. 브레인은 무상태 순수 함수(`EnemyBrainContext` → `EnemyIntent`)이므로 애셋 하나를 여러 적이 공유한다. 실행 계층(`Entity.StateMachine`)은 건드리지 않는다.

**Tech Stack:** Unity / C# · `Prototype` 네임스페이스 · asmdef 없음(`Assembly-CSharp`) · 외부 의존성 `com.unity.inputsystem`

## Global Constraints

- 전부 `Prototype` 네임스페이스. 에디터 코드만 `PrototypeEditor`.
- 자동 테스트를 붙이지 않는다. 검증은 `Assets/Scenes/TestBattle.unity`를 열고 Play해서 `BattleLog` 콘솔 출력으로 확인한다. (스펙 8절 — 테스트 어셈블리 도입은 범위 밖)
- `Time.timeScale`을 쓰지 않는다. dt는 `TimeControl.DeltaTime`.
- 좌표 규약: X 좌우 / Z 깊이 / Y 높이. AI 거리 계산은 **XZ 평면만** (`toTarget.y = 0f`).
- 브레인은 무상태여야 한다. 타이머·타겟은 `EnemyControl`이 보관한다.
- 기존 씬·프리팹이 깨지지 않아야 한다. `EnemyData`가 null이면 인스펙터 폴백값으로 동작한다.
- 주석은 한국어. 왜 그렇게 했는지를 적고, 무엇을 하는지는 코드로 드러낸다 (기존 코드 스타일).

---

### Task 1: 브레인 계약 정의

판단 층의 인터페이스와 입출력 타입만 만든다. 아직 아무도 사용하지 않는다.

**Files:**
- Create: `Assets/Scripts/AI/IEnemyBrain.cs`

**Interfaces:**
- Consumes: `Prototype.Command` ([Enums.cs:24](../../../Assets/Scripts/Core/Enums.cs#L24)), `Prototype.Entity`
- Produces:
  - `struct EnemyBrainParams { float attackRange; float leashRange; }` — `[Serializable]`
  - `struct EnemyBrainContext { Entity self; Entity target; Vector3 toTarget; float distance; bool attackReady; EnemyBrainParams p; float dt; }`
  - `struct EnemyIntent { Command command; Vector3 moveDirection; }` + `static EnemyIntent None`, `static EnemyIntent Move(Vector3 dir)`, `static EnemyIntent Attack(Vector3 face)`
  - `interface IEnemyBrain { EnemyIntent Decide(in EnemyBrainContext ctx); }`
  - `abstract class EnemyBrainAsset : ScriptableObject, IEnemyBrain`

- [ ] **Step 1: 폴더 만들기**

```bash
mkdir -p Assets/Scripts/AI/Brains
```

- [ ] **Step 2: `Assets/Scripts/AI/IEnemyBrain.cs` 작성**

```csharp
using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 브레인에 넘기는 수치. EnemyData에서 오거나, 없으면 EnemyControl의 인스펙터 폴백값.
    /// 브레인 애셋이 특정 EnemyData를 참조하면 여러 적이 공유할 수 없으므로 struct로 끊는다.
    /// </summary>
    [Serializable]
    public struct EnemyBrainParams
    {
        [Tooltip("이 거리 안이면 공격한다.")]
        public float attackRange;

        [Tooltip("타겟이 이보다 멀면 포기한다. 0이면 무제한.")]
        public float leashRange;
    }

    /// <summary>브레인이 판단에 쓰는 입력. EnemyControl이 매 프레임 채운다.</summary>
    public struct EnemyBrainContext
    {
        public Entity self;

        /// <summary>없으면 null. 브레인이 먼저 검사해야 한다.</summary>
        public Entity target;

        /// <summary>타겟까지의 벡터. XZ 평면이고 정규화 전.</summary>
        public Vector3 toTarget;

        /// <summary>toTarget.magnitude. 브레인이 다시 sqrt를 돌리지 않도록 미리 넣어 준다.</summary>
        public float distance;

        /// <summary>공격 쿨이 끝났는지. 쿨 관리는 EnemyControl이 한다.</summary>
        public bool attackReady;

        public EnemyBrainParams p;
        public float dt;
    }

    /// <summary>브레인이 내는 결론. EnemyControl이 그대로 Control 프로퍼티로 옮긴다.</summary>
    public struct EnemyIntent
    {
        public Command command;
        public Vector3 moveDirection;

        public static EnemyIntent None => default;

        public static EnemyIntent Move(Vector3 dir)
            => new EnemyIntent { command = Command.Move, moveDirection = dir };

        /// <summary>
        /// face는 AttackState.Enter가 Physics.Face에 쓴다.
        /// 비우면 Facing이 갱신되지 않아 마지막 이동 방향으로 헛휘두른다.
        /// </summary>
        public static EnemyIntent Attack(Vector3 face)
            => new EnemyIntent { command = Command.Attack, moveDirection = face };
    }

    /// <summary>
    /// 적 종류별 판단 로직. <b>무상태여야 한다</b> — 애셋 하나를 여러 적이 공유한다.
    /// 타이머·타겟이 필요하면 EnemyControl에 두고 컨텍스트로 받는다.
    /// </summary>
    public interface IEnemyBrain
    {
        EnemyIntent Decide(in EnemyBrainContext ctx);
    }

    /// <summary>
    /// ScriptableObject로 만드는 브레인의 공통 베이스.
    /// SerializeReference 대신 애셋을 쓰는 이유: GUID 참조라 클래스 이름을 바꿔도 안 끊기고,
    /// 인스펙터 드래그&드롭이 기본으로 되고, 수치만 다른 변종을 애셋으로 찍을 수 있다.
    /// </summary>
    public abstract class EnemyBrainAsset : ScriptableObject, IEnemyBrain
    {
        public abstract EnemyIntent Decide(in EnemyBrainContext ctx);
    }
}
```

- [ ] **Step 3: 컴파일 확인**

유니티 에디터로 포커스를 옮겨 컴파일을 트리거한다.
기대: Console에 에러 0건. `Assets/Scripts/AI/IEnemyBrain.cs`가 임포트되고 `.meta`가 생성된다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/AI
git commit -m "feat: IEnemyBrain 계약 정의 (Context/Intent/Params/Asset 베이스)"
```

---

### Task 2: MeleeBrainAsset

근접 판단 로직. 현재 `EnemyControl`의 동작을 그대로 옮기고 `leashRange`만 추가한다.

**Files:**
- Create: `Assets/Scripts/AI/Brains/MeleeBrainAsset.cs`

**Interfaces:**
- Consumes: Task 1의 `EnemyBrainAsset`, `EnemyBrainContext`, `EnemyIntent`
- Produces: `class MeleeBrainAsset : EnemyBrainAsset` — `CreateAssetMenu` 경로 `Prototype/Enemy Brain/Melee`

- [ ] **Step 1: `Assets/Scripts/AI/Brains/MeleeBrainAsset.cs` 작성**

```csharp
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 근접. 타겟에게 붙어 사거리 안에서 평타를 낸다.
    /// 무상태 — 타이머와 타겟은 EnemyControl이 들고 컨텍스트로 넘어온다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Brain/Melee", fileName = "Brain_Melee")]
    public class MeleeBrainAsset : EnemyBrainAsset
    {
        public override EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            if (ctx.target == null) return EnemyIntent.None;

            // leash 0은 무제한. 스포너·방 경계가 붙으면 여기서 추격을 끊는다.
            if (ctx.p.leashRange > 0f && ctx.distance > ctx.p.leashRange)
                return EnemyIntent.None;

            // distance를 이미 받았으므로 normalized(sqrt 재계산) 대신 나눈다.
            Vector3 dir = ctx.distance > 0.0001f ? ctx.toTarget / ctx.distance : Vector3.zero;

            if (ctx.distance <= ctx.p.attackRange)
                return ctx.attackReady ? EnemyIntent.Attack(dir) : EnemyIntent.None;

            return EnemyIntent.Move(dir);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

기대: Console 에러 0건.

- [ ] **Step 3: 애셋 생성 확인**

Project 창에서 `Assets/Data` 우클릭 → `Create ▸ Prototype ▸ Enemy Brain ▸ Melee`.
기대: `Assets/Data/Brain_Melee.asset`이 생성되고, 인스펙터에 `MeleeBrainAsset` 스크립트가 표시된다.
(이 애셋은 Task 4의 검증에서 사용하므로 지운다면 다시 만들어야 한다. Task 6에서 빌더가 자동 생성하도록 바꾼다.)

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/AI/Brains Assets/Data
git commit -m "feat: MeleeBrainAsset — 추격/사거리/leash 판단"
```

---

### Task 3: EnemyData에 행동 파라미터 추가

`EnemyControl`이 읽을 필드를 먼저 만든다. 이 태스크만으로는 아무 동작도 바뀌지 않는다.

**Files:**
- Modify: `Assets/Scripts/Characters/EnemyData.cs`

**Interfaces:**
- Consumes: Task 1의 `EnemyBrainAsset`
- Produces: `EnemyData`의 새 public 필드 — `brain`, `moveSpeed`, `attackRange`, `attackInterval`, `retargetInterval`, `leashRange`

- [ ] **Step 1: `Assets/Scripts/Characters/EnemyData.cs` 전체 교체**

```csharp
using UnityEngine;

namespace Prototype
{
    /// <summary>적 한 종류의 수치 테이블.</summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Data", fileName = "Enemy_")]
    public class EnemyData : ScriptableObject
    {
        public string enemyId = "Enemy_001";
        public string enemyName = "고블린";

        [Header("전투")]
        public float hp = 40f;
        public float atk = 5f;

        [Header("행동")]
        [Tooltip("판단 로직. 비우면 프리팹 EnemyControl의 폴백 브레인을 쓴다.")]
        public EnemyBrainAsset brain;

        public float moveSpeed = 4f;
        public float attackRange = 1.8f;
        public float attackInterval = 1.5f;
        public float retargetInterval = 0.5f;

        [Tooltip("타겟이 이보다 멀면 추격을 포기한다. 0이면 무제한.")]
        public float leashRange = 0f;

        [Header("보상")]
        public int exp = 10;
        public int gold = 5;

        public GameObject prefab;
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 기존 애셋 점검**

기대: Console 에러 0건.
`Assets/Data/Enemy_001_고블린.asset`을 선택한다. 기존 애셋에는 새 필드가 직렬화되어 있지 않으므로
**`moveSpeed`·`attackRange` 등이 0으로 보인다**. Task 6에서 빌더가 값을 채운다. 지금은 그대로 둔다.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Characters/EnemyData.cs
git commit -m "feat: EnemyData에 행동 파라미터(brain/사거리/쿨/leash) 추가"
```

---

### Task 4: EnemyControl을 브레인 위임 구조로 개편

판단 로직을 브레인으로 넘기고, 공격 시 `Facing`이 갱신되지 않던 문제를 함께 고친다.

**현재 버그:** `EnemyControl`이 `Command.Attack`을 낼 때 `MoveDirection`을 채우지 않는다.
[`AttackState.Enter`](../../../Assets/Scripts/StateMachine/States/EntityStates.cs#L147-L148)는
`Control.MoveDirection`으로 `Physics.Face()`를 호출하는데, 값이 `Vector3.zero`라 `Face`의
`sqrMagnitude > 0.0001f` 가드에 걸려 아무 일도 하지 않는다. `Physics.Move(zero)`도 `Facing`을
지우지 않으므로([Physics.cs:115-116](../../../Assets/Scripts/Entities/Physics.cs#L115-L116))
**마지막 이동 방향이 그대로 남는다**. 사거리 안에서 제자리 공격을 반복하는 동안 타겟이 옆·뒤로
돌면 히트박스(자식, local +Z)가 낡은 방향을 향해 헛휘두른다. `EnemyIntent.Attack(dir)`이 이걸 해결한다.

**Files:**
- Modify: `Assets/Scripts/Control/EnemyControl.cs` (전체 교체)

**Interfaces:**
- Consumes: Task 1의 `EnemyBrainAsset`/`EnemyBrainContext`/`EnemyIntent`/`EnemyBrainParams`, Task 3의 `EnemyData` 필드
- Produces:
  - `EnemyControl.Target` (get) — 기존 유지
  - `EnemyControl.SetTarget(Entity)` — 기존 유지. [`TauntEffect`](../../../Assets/Scripts/Skill/Effects.cs#L163)가 호출한다
  - `EnemyControl.SetActive(bool)` — 기존 유지. `Enemy.StopAI()`가 호출한다
  - `EnemyControl.ApplyData(EnemyData)` — **신규**. Task 5의 `Enemy.ApplyData()`가 호출한다

- [ ] **Step 1: `Assets/Scripts/Control/EnemyControl.cs` 전체 교체**

```csharp
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적 제어. 감지 · 타이머 · 게이트를 맡고 <b>판단은 브레인에 위임</b>한다.
    /// 종류별 행동 차이는 브레인 애셋을 갈아끼워 만든다.
    /// 스테이지 종료 시 <see cref="SetActive"/>(false)로 정지시킨다.
    /// </summary>
    public class EnemyControl : Control
    {
        [Tooltip("EnemyData.brain이 비었을 때 쓰는 폴백.")]
        [SerializeField] private EnemyBrainAsset brain;

        [Header("타이머")]
        [SerializeField] private float attackInterval = 1.5f;
        [SerializeField] private float retargetInterval = 0.5f;

        [Header("브레인 파라미터")]
        [SerializeField] private EnemyBrainParams parameters = new EnemyBrainParams
        {
            attackRange = 1.8f,
            leashRange = 0f,
        };

        private Entity target;
        private float attackTimer;
        private float retargetTimer;
        private bool active = true;

        public Entity Target => target;

        /// <summary>도발 등으로 타겟을 강제 지정한다.</summary>
        public void SetTarget(Entity forced)
        {
            target = forced;
            retargetTimer = retargetInterval;
        }

        public void SetActive(bool value)
        {
            active = value;
            if (!active) Clear();
        }

        /// <summary>
        /// EnemyData의 행동 수치를 주입한다. Enemy.Awake가 호출한다.
        /// data가 null이면 인스펙터 값을 그대로 쓴다 — 데이터를 안 꽂은 프리팹도 동작해야 한다.
        /// </summary>
        public void ApplyData(EnemyData data)
        {
            if (data == null) return;

            if (data.brain != null) brain = data.brain;

            attackInterval = data.attackInterval;
            retargetInterval = data.retargetInterval;
            parameters.attackRange = data.attackRange;
            parameters.leashRange = data.leashRange;
        }

        // 배선 누락을 조용히 넘기지 않는다. ApplyData는 Awake에서 끝나므로 Start에서 판단한다.
        private void Start()
        {
            if (brain == null)
                BattleLog.Warn(LogCategory.State, $"{name}: 브레인이 없다. 이 적은 움직이지 않는다.", this);
        }

        public override void Tick(float dt)
        {
            Clear();
            if (!active) return;
            if (brain == null) return;
            if (Owner != null && (Owner.IsBusy || CombatStateRules.IsStunned(Owner.Combat.CombatState))) return;

            attackTimer -= dt;
            retargetTimer -= dt;

            Retarget();

            EnemyIntent intent = brain.Decide(BuildContext(dt));

            Command = intent.command;
            MoveDirection = intent.moveDirection;

            // 쿨 소모는 여기서 판단한다. 브레인이 별도 플래그를 돌려주면 항상 이 조건과 같은 값이 되어 중복이다.
            if (intent.command == Command.Attack)
                attackTimer = attackInterval;
        }

        private void Retarget()
        {
            if (retargetTimer > 0f && target != null && !target.Combat.IsDead) return;

            retargetTimer = retargetInterval;
            target = FindNearestAlly();
        }

        private EnemyBrainContext BuildContext(float dt)
        {
            Vector3 toTarget = Vector3.zero;
            float distance = 0f;

            if (target != null)
            {
                toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;                 // 거리 판정은 XZ 평면만. 높이는 무시한다.
                distance = toTarget.magnitude;
            }

            return new EnemyBrainContext
            {
                self = Owner,
                target = target,
                toTarget = toTarget,
                distance = distance,
                attackReady = attackTimer <= 0f,
                p = parameters,
                dt = dt,
            };
        }

        private Entity FindNearestAlly()
        {
            Entity best = null;
            float bestSqr = float.MaxValue;

            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || e.Combat.IsDead) continue;

                float sqr = (e.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

기대: Console 에러 0건.

- [ ] **Step 3: 기존 프리팹에 브레인을 손으로 꽂고 값 확인**

`Assets/Prefabs/Enemy_고블린.prefab`을 선택하고 `EnemyControl`을 본다.

1. `Brain` 필드에 `Assets/Data/Brain_Melee.asset`을 드래그한다.
2. **`Brain Parameters ▸ Attack Range`가 `0`이면 `1.8`로 직접 넣는다.** 새로 추가한 직렬화 필드라
   기존 프리팹에는 `default(struct)`(=0)가 들어간다. 0이면 적이 절대 공격하지 않는다.
3. `Leash Range`는 `0`(무제한)으로 둔다.
4. `Attack Interval` `1.5`, `Retarget Interval` `0.5` 확인.

- [ ] **Step 4: 플레이 검증 — 추격과 공격**

`Assets/Scenes/TestBattle.unity`를 열고 Play.

기대 (Console):
```
[State]  Enemy_01: IdleState → MoveState
[State]  Enemy_01: MoveState → AttackState
[Combat] Enemy_01 → Ally_Tanker 적중 | dmg 5 | ...
```
- 적 4마리가 좌측 아군 쪽으로 이동한다.
- 1.8 이내에서 약 1.5초 간격으로 `AttackState` 전이가 반복된다.
- `[State] ... 브레인이 없다` 경고가 **뜨지 않는다**.

- [ ] **Step 5: 플레이 검증 — Facing 갱신 (버그 수정 확인)**

Play 상태에서 `WASD`로 플레이어를 적 하나에게 붙인 뒤, **사거리 안에 머문 채로 적 주위를 돌린다**
(적을 중심으로 W/S로 Z축 이동, A/D로 X축 이동).

기대: 어느 방향에 있어도 `[Combat] Enemy_NN → Player 적중` 로그가 계속 찍힌다.
수정 전이라면 적이 처음 접근한 방향으로만 히트박스를 내밀어 적중 로그가 끊긴다.

- [ ] **Step 6: 플레이 검증 — 재타겟 · 도발 · 불렛타임**

1. **재타겟** — 적에게 맞아 아군 하나를 죽인다(또는 HUD로 HP 확인). 기대: 0.5초 안에 다른 아군 쪽으로 방향을 바꾼다.
2. **도발** — `E`로 불렛타임 진입 → 손패에서 탱커 콤보 카드 **`도발 방벽`**(`SK_T4_도발방벽`,
   [TestSceneBuilder.cs:220-223](../../../Assets/Editor/TestSceneBuilder.cs#L220-L223)에서 `TauntEffect`가 붙는다)을
   슬롯에 배치 → `Space` 실행. 손패에 안 나오면 `Backspace`로 회수하고 다시 뽑는다.
   기대: 시전자 반경 6 안의 적들이 탱커 쪽으로 방향을 바꾼다.
3. **불렛타임 정지** — `E` 진입 상태에서 기대: 적이 완전히 멈추고 `[State]` 전이 로그가 더 이상 찍히지 않는다.

- [ ] **Step 7: 플레이 검증 — 브레인 누락 경고**

프리팹의 `Brain` 필드를 비우고 Play.
기대: 해당 적이 제자리에 서 있고 Console에 `[State] Enemy_NN: 브레인이 없다. 이 적은 움직이지 않는다.`가 **1회** 찍힌다.
확인 후 `Brain_Melee.asset`을 다시 꽂는다.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/Control/EnemyControl.cs Assets/Prefabs/Enemy_고블린.prefab
git commit -m "refactor: EnemyControl 판단을 브레인으로 위임 + 공격 시 Facing 갱신 수정"
```

---

### Task 5: Enemy가 EnemyData를 EnemyControl에 전달

`moveSpeed`를 `Stats`에 넣고 나머지 행동 수치를 `EnemyControl`로 넘긴다.

**Files:**
- Modify: `Assets/Scripts/Characters/Enemy.cs:32-38`

**Interfaces:**
- Consumes: Task 4의 `EnemyControl.ApplyData(EnemyData)`, Task 3의 `EnemyData.moveSpeed`
- Produces: 없음 (기존 `Enemy` 공개 API 변화 없음)

- [ ] **Step 1: `Enemy.ApplyData()` 교체**

`Assets/Scripts/Characters/Enemy.cs`의 `ApplyData()`를 아래로 바꾼다.

```csharp
        private void ApplyData()
        {
            if (data == null) return;

            Stats.Set(StatType.AttackPower, data.atk);
            Stats.Set(StatType.MoveSpeed, data.moveSpeed);   // MoveState가 읽는다
            Combat.SetMaxHealth(data.hp);

            enemyControl?.ApplyData(data);
        }
```

`Awake`는 그대로 둔다. `base.Awake()`가 `EnsureDefaults()`로 `MoveSpeed`를 6으로 채운 **뒤에**
`ApplyData()`가 돌아야 데이터 값이 이긴다. 순서를 바꾸면 안 된다.

- [ ] **Step 2: 컴파일 확인**

기대: Console 에러 0건.

- [ ] **Step 3: 플레이 검증 — 데이터 값이 이기는지**

`Assets/Data/Enemy_001_고블린.asset`에 임시로 눈에 띄는 값을 넣는다: `moveSpeed = 12`, `attackRange = 4`.
Play.

기대:
- 적이 눈에 띄게 빨라진다 (기본 6 → 12).
- 4 거리에서 이미 공격을 시작한다 (`MoveState → AttackState` 전이가 더 멀리서 발생).

확인 후 값을 되돌린다: `moveSpeed = 4`, `attackRange = 1.8`.

- [ ] **Step 4: 플레이 검증 — data 없이도 동작**

프리팹 `EnemyControl`은 그대로 두고, 프리팹 `Enemy` 컴포넌트의 `Data` 필드를 **비운다**. Play.

기대: 적이 인스펙터 폴백값(사거리 1.8, 쿨 1.5)으로 정상 추격·공격한다. 경고 없음.
확인 후 `Enemy_001_고블린.asset`을 다시 꽂는다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Characters/Enemy.cs Assets/Data/Enemy_001_고블린.asset
git commit -m "feat: EnemyData의 행동 수치를 EnemyControl·Stats에 주입"
```

---

### Task 6: TestSceneBuilder에 브레인 배선

`Prototype ▸ 테스트 씬 만들기` 한 번으로 브레인 애셋 · `EnemyData` 값 · 프리팹 참조가 전부 채워지게 한다.
Task 4·5에서 손으로 꽂은 것을 자동화하는 단계다.

**Files:**
- Modify: `Assets/Editor/TestSceneBuilder.cs` — `Build()`, `CreateEnemyData()`, `BuildEnemyPrefab()`, `CreateMeleeBrain()` 추가

**Interfaces:**
- Consumes: Task 2의 `MeleeBrainAsset`, Task 3의 `EnemyData` 필드, Task 4의 `EnemyControl` 직렬화 필드명 `brain` / `parameters`
- Produces: `Assets/Data/Brain_Melee.asset`

- [ ] **Step 1: `CreateMeleeBrain()` 추가**

`CreateEnemyData()` 바로 위(`Assets/Editor/TestSceneBuilder.cs`의 `// ── 프리팹 ──` 주석 앞)에 넣는다.

```csharp
        private static MeleeBrainAsset CreateMeleeBrain()
        {
            string path = $"{DataDir}/Brain_Melee.asset";
            var brain = AssetDatabase.LoadAssetAtPath<MeleeBrainAsset>(path);

            if (brain == null)
            {
                brain = ScriptableObject.CreateInstance<MeleeBrainAsset>();
                AssetDatabase.CreateAsset(brain, path);
            }

            // MeleeBrainAsset은 무상태라 직렬화할 필드가 없다. 존재 자체가 전부다.
            EditorUtility.SetDirty(brain);
            return brain;
        }
```

- [ ] **Step 2: `CreateEnemyData()`를 브레인 인자를 받도록 교체**

```csharp
        private static EnemyData CreateEnemyData(MeleeBrainAsset brain)
        {
            string path = $"{DataDir}/Enemy_001_고블린.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.enemyId = "Enemy_001";
            data.enemyName = "고블린";
            data.hp = 60f;
            data.atk = 5f;

            data.brain = brain;
            data.moveSpeed = 4f;
            data.attackRange = 1.8f;
            data.attackInterval = 1.5f;
            data.retargetInterval = 0.5f;
            data.leashRange = 0f;              // 프로토타입은 무제한 추격

            data.exp = 10;
            data.gold = 5;

            EditorUtility.SetDirty(data);
            return data;
        }
```

- [ ] **Step 3: `BuildEnemyPrefab()`에 브레인 폴백 배선 추가**

시그니처에 `MeleeBrainAsset brain`을 추가하고, `EnemyControl`을 변수로 받아 직렬화 필드를 채운다.

```csharp
        private static GameObject BuildEnemyPrefab(Material mat, EnemyData data, MeleeBrainAsset brain)
        {
            GameObject root = BuildCharacterBase("Enemy_고블린", mat, 0.9f);

            Attack basic = AddHitbox(root, "BasicHitbox", new Vector3(0f, 0.9f, 1f), new Vector3(1.3f, 1.4f, 1.4f));

            SetSerialized(root.GetComponent<Combat>(), so => so.FindProperty("maxHealth").floatValue = 60f);

            var enemy = root.AddComponent<Enemy>();
            var control = root.AddComponent<EnemyControl>();

            SetSerialized(enemy, so =>
            {
                so.FindProperty("basicAttack").objectReferenceValue = basic;
                so.FindProperty("data").objectReferenceValue = data;
            });

            // data.brain이 있으면 런타임에 덮어쓰지만, data를 비운 프리팹도 굴러가도록 폴백을 채워 둔다.
            SetSerialized(control, so =>
            {
                so.FindProperty("brain").objectReferenceValue = brain;
                so.FindProperty("attackInterval").floatValue = 1.5f;
                so.FindProperty("retargetInterval").floatValue = 0.5f;
                so.FindProperty("parameters.attackRange").floatValue = 1.8f;
                so.FindProperty("parameters.leashRange").floatValue = 0f;
            });

            return SavePrefab(root, $"{PrefabDir}/Enemy_고블린.prefab");
        }
```

- [ ] **Step 4: `Build()`의 호출부 수정**

`Build()` 안의 두 줄을 바꾼다.

```csharp
            Dictionary<string, Material> mats = CreateMaterials();
            SkillSet skills = CreateSkills();
            MeleeBrainAsset meleeBrain = CreateMeleeBrain();
            EnemyData enemyData = CreateEnemyData(meleeBrain);

            GameObject playerPrefab = BuildPlayerPrefab(mats["Player"]);
            Dictionary<Role, GameObject> allyPrefabs = BuildAllyPrefabs(mats, skills);
            GameObject enemyPrefab = BuildEnemyPrefab(mats["Enemy"], enemyData, meleeBrain);
```

확인 다이얼로그 문구도 갱신한다.

```csharp
            if (!EditorUtility.DisplayDialog(
                    "테스트 씬 만들기",
                    "스킬 에셋 20개, 브레인 에셋 1개, 프리팹 6개, 씬 1개(TestBattle)를 생성한다.\n" +
                    "같은 이름의 기존 에셋은 덮어쓴다. 계속할까?",
                    "만들기", "취소"))
                return;
```

- [ ] **Step 5: 컴파일 확인**

기대: Console 에러 0건.

- [ ] **Step 6: 빌더 실행 후 배선 확인**

메뉴 `Prototype ▸ 테스트 씬 만들기` → `만들기`.

기대:
- `Assets/Data/Brain_Melee.asset`이 존재한다.
- `Assets/Data/Enemy_001_고블린.asset`의 `Brain` = `Brain_Melee`, `Move Speed` = 4, `Attack Range` = 1.8.
- `Assets/Prefabs/Enemy_고블린.prefab`의 `EnemyControl ▸ Brain` = `Brain_Melee`, `Attack Range` = 1.8.
- Console에 `[TestSceneBuilder] 완료 — Assets/Scenes/TestBattle.unity ...`

- [ ] **Step 7: 플레이 전체 검증 (스펙 8절 체크리스트)**

`Assets/Scenes/TestBattle.unity`를 열고 Play. 아래를 순서대로 확인한다.

1. 적 4마리가 아군 쪽으로 이동하고, 1.8 이내에서 약 1.5초 간격으로 평타를 낸다.
2. 사거리 안에서 플레이어를 적 주위로 돌려도 `[Combat] Enemy_NN → Player 적중`이 계속 찍힌다.
3. `E`로 불렛타임 진입 → 적이 완전히 멈추고 `[State]` 전이가 끊긴다. `Space`로 해제 → 재개된다.
4. 도발 스킬 실행 → 반경 내 적의 타겟이 시전자로 바뀐다.
5. 아군 하나가 죽으면 0.5초 내 다른 아군으로 재타겟한다.
6. Console에 `브레인이 없다` 경고가 없다.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Editor/TestSceneBuilder.cs Assets/Data Assets/Prefabs Assets/Scenes
git commit -m "feat: TestSceneBuilder에 브레인 애셋 생성·배선 추가"
```

---

### Task 7: 문서 갱신

`Assets/Scripts/README.md`가 폴더 구조와 씬 조립 순서를 안내하는데, `AI/`와 브레인 배선이 빠져 있다.

**Files:**
- Modify: `Assets/Scripts/README.md:19-31` (폴더 목록), `:87-105` (씬 조립 순서), `:41` (빌더 생성물 표)

**Interfaces:**
- Consumes: 없음
- Produces: 없음

- [ ] **Step 1: 폴더 목록에 `AI/` 추가**

`## 폴더` 블록의 `Control/` 줄 **아래**에 넣는다.

```
Control/    Control, PlayerControl, EnemyControl, AllyControl
AI/         IEnemyBrain (Context / Intent / Params), Brains/MeleeBrainAsset
```

- [ ] **Step 2: 빌더 생성물 표에 브레인 애셋 추가**

`| EnemyData (고블린) | \`Assets/Data/\` |` 줄 **위**에 넣는다.

```
| 브레인 에셋 (Brain_Melee) | `Assets/Data/` |
```

- [ ] **Step 3: 적 AI 절 추가**

`## 조작 (PlayerControl)` 섹션 **바로 앞**에 아래를 넣는다.
(아래 블록은 4중 백틱으로 감쌌다 — 안에 3중 백틱 코드블록이 들어 있어서다. README에는 4중 백틱 줄을 넣지 않는다.)

````markdown
## 적 AI — 브레인 교체

`EnemyControl` 은 감지 · 타이머 · 게이트만 맡는다. 종류별 판단은 `IEnemyBrain` 애셋이 한다.

```
EnemyControl.Tick
  ├ 재타겟 (retargetInterval)
  ├ 공격 쿨 (attackInterval)
  ├ IsBusy / 경직 게이트
  └ brain.Decide(EnemyBrainContext) → EnemyIntent → Command / MoveDirection
```

브레인은 **무상태**다. 애셋 하나를 여러 적이 공유하므로 타이머 · 타겟을 들면 안 된다.
필요한 값은 `EnemyBrainContext` 로 받는다 (`target`, `distance`, `attackReady`, `p.attackRange`, `p.leashRange`).

| 브레인 | 행동 |
|---|---|
| `MeleeBrainAsset` | 타겟에게 붙어 사거리 안에서 평타. `leashRange` 초과 시 포기(0 = 무제한) |

새 종류를 넣는 법:

1. `EnemyBrainAsset` 을 상속해 `Decide` 하나만 구현한다 (`Assets/Scripts/AI/Brains/`)
2. `CreateAssetMenu` 로 애셋을 만든다
3. `EnemyData.brain` 에 꽂는다. 비우면 프리팹 `EnemyControl.brain` 폴백을 쓴다

수치는 `EnemyData` 가 단독 출처다. `Enemy.Awake` → `ApplyData` 가 `moveSpeed` 는 `Stats` 로,
나머지는 `EnemyControl.ApplyData` 로 넘긴다. `EnemyData` 를 비우면 프리팹 인스펙터 값으로 동작한다.
````

- [ ] **Step 4: 씬 조립 순서(수동) 갱신**

`### 1. 캐릭터 프리팹` 의 `- \`PlayerControl\` / \`AllyControl\` / \`EnemyControl\` 중 하나` 줄 아래에 넣는다.

```
  - `EnemyControl` 은 `brain` 에 브레인 에셋을 꽂아야 움직인다. 비면 콘솔에 경고가 찍히고 정지한다
```

- [ ] **Step 5: 렌더 확인**

`Assets/Scripts/README.md`를 마크다운 프리뷰로 열어 중첩 코드블록이 깨지지 않았는지 본다.
기대: `적 AI — 브레인 교체` 절의 파이프라인 블록과 표가 정상 렌더된다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/README.md
git commit -m "docs: 적 AI 브레인 구조와 배선 절차 추가"
```

---

## 완료 조건

- `Prototype ▸ 테스트 씬 만들기` 한 번으로 브레인까지 배선된 씬이 나온다.
- 근접 적의 동작이 개편 전과 동일하고, 제자리 공격 중 방향 갱신 버그가 고쳐졌다.
- 새 적 종류를 넣을 때 `EnemyControl`을 건드릴 필요가 없다 — `EnemyBrainAsset` 상속 + 애셋 생성 + `EnemyData.brain` 연결로 끝난다.
- `TauntEffect`·불렛타임·재타겟 등 기존 연동이 전부 유지된다.

## 이후 작업 (이번 범위 아님)

- `RangedBrainAsset` — 선호 거리 유지, 접근 시 후퇴
- `ChargerBrainAsset` — 조준 후 돌진
- 적 상호 밀어내기 — 여러 적이 한 타겟에 겹치는 문제
- 스포너 · 오브젝트 풀 — `Enemy.StopAI()` 호출처가 아직 없다
- 브레인 자동 테스트 — `Assets/Scripts`에 asmdef를 추가하면 `Decide`는 순수 함수라 EditMode 테스트로 전부 덮을 수 있다
