# 스킬 컷인 연출 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 콤보 실행 중 각 슬롯의 스킬이 발동하기 직전, 화면 왼쪽에 시전 동료의 얼굴과 스킬명이 0.55초간 슬라이드 인/아웃하고 그동안 전투가 완전히 멈춘다.

**Architecture:** `ComboExecutor`는 `ISkillCutin` 인터페이스만 알고, 슬롯 루프에서 스킬 상태로 진입하기 **전에** `Play`를 호출해 끝날 때까지 기다린다. 구현체 `SkillCutinUI`가 캔버스를 코드로 짓고 `TimeControl.Scale`을 0으로 내렸다 되돌리는 책임까지 진다. 인터페이스가 미배선(null)이면 컷인 없이 기존 흐름 그대로 돈다.

**Tech Stack:** Unity 2D, C#, uGUI(`UnityEngine.UI`), NUnit EditMode 테스트

## Global Constraints

- 테스트 파일 위치는 `Assets/Editor/Tests/`, 네임스페이스 `Prototype.Tests`, NUnit. `Assets/Tests/EditMode/`가 아니다.
- **테스트를 실행하지 않는다.** Unity Test Runner가 알아서 돌린다. "테스트를 돌려 실패를 확인한다" 단계는 건너뛰고 다음으로 간다. 테스트 **코드는 계획대로 전부 쓴다.**
- `Time.timeScale`을 쓰지 않는다. 게임플레이 정지는 `TimeControl.Scale`, UI 시간은 `TimeControl.UnscaledDeltaTime`.
- UI는 프리팹 없이 코드로 짓는다(`RecentHitEnemyHUD` 패턴). 폰트는 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
- 주석과 로그는 한국어. 코드·식별자는 영어.
- 타임라인 상수(초): In `0.12`, Hold `0.25`, Out `0.12`, 스킬명 라벨 지연 `0.06`. 컷인 전체 길이 `0.55`.
- Role 폴백 색: Tanker `#3D6EA8`, Warrior `#B0483C`, Archer `#3F8F5B`, Wizard `#7A4FA8`.
- 새 `.cs` 파일을 커밋할 때 Unity가 생성한 `.meta`가 있으면 함께 `git add` 한다.

---

### Task 1: `ISkillCutin` 인터페이스와 `ComboExecutor` 슬롯 루프 재구성

컷인 UI 없이도 "슬롯마다 인트로를 재생하고 기다린다"는 계약을 완성한다. 이 태스크만으로
페이크를 꽂아 동작을 전부 검증할 수 있다.

**Files:**
- Create: `Assets/Scripts/Battle/ISkillCutin.cs`
- Modify: `Assets/Scripts/Battle/ComboExecutor.cs`
- Test: `Assets/Editor/Tests/ComboExecutorCutinTests.cs`

**Interfaces:**
- Consumes: `ComboSlot`(`card`/`target`/`aimed`/`caster`, `Data`, `IsEmpty`), `Ally`, `SkillData`, `ComboCard(SkillData, float)`
- Produces:
  - `interface ISkillCutin { IEnumerator Play(Ally caster, SkillData data); void Cancel(); }`
  - `ComboExecutor.Cutin { get; set; }` — `ISkillCutin`
  - `static bool ComboExecutor.CanRunSlot(in ComboSlot slot)`
  - `public IEnumerator ComboExecutor.Run(Queue<ComboSlot> queue)` — 테스트가 직접 펌프한다

- [ ] **Step 1: 인터페이스 파일을 만든다**

`Assets/Scripts/Battle/ISkillCutin.cs`:

```csharp
using System.Collections;

namespace Prototype
{
    /// <summary>
    /// 스킬이 나가기 직전에 끼어드는 인트로 연출.
    /// <see cref="ComboExecutor"/>는 이 계약만 알고 UI 구현은 모른다 —
    /// 미배선(null)이면 컷인 없이 곧바로 스킬로 간다.
    /// </summary>
    public interface ISkillCutin
    {
        /// <summary>
        /// 컷인을 한 번 재생한다. Executor가 반환된 열거자를 소진할 때까지 슬롯이 대기한다.
        /// <b>호출 즉시</b> 연출을 시작해야 한다(시간 정지 · 패널 표시) —
        /// 반환값을 펌프하지 않는 호출자도 상태 변화를 관측할 수 있어야 하기 때문.
        /// </summary>
        IEnumerator Play(Ally caster, SkillData data);

        /// <summary>
        /// 재생을 즉시 중단하고 정지시킨 시간을 되돌린다.
        /// <see cref="ComboExecutor.Abort"/>가 부른다 — StopCoroutine으로 잘린 코루틴은
        /// finally가 돌지 않아 <see cref="TimeControl.Scale"/>이 0에 묶이기 때문.
        /// </summary>
        void Cancel();
    }
}
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/ComboExecutorCutinTests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// ComboExecutor가 슬롯마다 컷인을 앞세우는지 검증한다.
    ///
    /// Run 코루틴을 Unity 스케줄러 없이 손으로 펌프한다. 손 펌프는 중첩 열거자
    /// (yield return IEnumerator)를 대신 돌려주지 않으므로 RunSlot 본문은 실행되지 않는다 —
    /// 덕분에 StateMachine · SkillState · VFX를 세우지 않고도 컷인 호출만 깨끗이 관측된다.
    /// 그래서 ISkillCutin.Play는 "호출 즉시 기록"되어야 한다(FakeCutin이 그렇게 만들어져 있다).
    /// </summary>
    public class ComboExecutorCutinTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        private ComboExecutor executor;
        private FakeCutin cutin;

        [SetUp]
        public void SetUp()
        {
            executor = NewObject("ComboExecutor").AddComponent<ComboExecutor>();
            cutin = new FakeCutin();
            executor.Cutin = cutin;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++) Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            spawned.Clear();
            assets.Clear();
            executor = null;
            cutin = null;
        }

        [Test]
        public void EverySlot_PlaysOneCutin_InQueueOrder()
        {
            SkillData a = NewSkill("베어내기", Role.Warrior);
            SkillData b = NewSkill("화염구", Role.Wizard);
            Ally warrior = NewAlly("Warrior");
            Ally wizard = NewAlly("Wizard");

            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(a, warrior));
            queue.Enqueue(NewSlot(b, wizard));

            Drain(executor.Run(queue));

            Assert.That(cutin.Calls.Count, Is.EqualTo(2));
            Assert.That(cutin.Calls[0].caster, Is.SameAs(warrior));
            Assert.That(cutin.Calls[0].data, Is.SameAs(a));
            Assert.That(cutin.Calls[1].caster, Is.SameAs(wizard));
            Assert.That(cutin.Calls[1].data, Is.SameAs(b));
        }

        [Test]
        public void Cutin_PlaysBeforeCasterEntersSkillState()
        {
            SkillData skill = NewSkill("베어내기", Role.Warrior);
            Ally warrior = NewAlly("Warrior");

            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(skill, warrior));

            IEnumerator run = executor.Run(queue);

            // 첫 양보 지점이 곧 컷인이다. 아직 스킬 상태로 넘어가지 않았어야 한다.
            Assert.That(run.MoveNext(), Is.True);
            Assert.That(cutin.Calls.Count, Is.EqualTo(1));
            Assert.That(warrior.StateMachine == null || !(warrior.StateMachine.CurState is SkillState),
                        Is.True, "컷인이 끝나기 전에 스킬 상태로 들어가면 안 된다");

            Drain(run);
        }

        [Test]
        public void NoCutinWired_RunsWithoutError()
        {
            executor.Cutin = null;

            SkillData skill = NewSkill("베어내기", Role.Warrior);
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(skill, NewAlly("Warrior")));

            Assert.DoesNotThrow(() => Drain(executor.Run(queue)));
            Assert.That(cutin.Calls, Is.Empty);
        }

        [Test]
        public void SlotWithoutSkillData_SkipsCutin()
        {
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(new ComboSlot { card = new ComboCard(null), caster = NewAlly("Warrior") });

            Drain(executor.Run(queue));

            Assert.That(cutin.Calls, Is.Empty);
        }

        [Test]
        public void SlotWithoutCaster_SkipsCutin()
        {
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(NewSlot(NewSkill("베어내기", Role.Warrior), null));

            Drain(executor.Run(queue));

            Assert.That(cutin.Calls, Is.Empty);
        }

        [Test]
        public void SkippedSlot_StillGoesToDiscard()
        {
            var consumed = new List<ComboCard>();
            executor.OnSlotConsumed += c => consumed.Add(c);

            ComboSlot slot = NewSlot(NewSkill("베어내기", Role.Warrior), null);
            var queue = new Queue<ComboSlot>();
            queue.Enqueue(slot);

            Drain(executor.Run(queue));

            Assert.That(consumed, Has.Count.EqualTo(1), "발동 못 한 카드도 버린 더미로 가야 덱이 마르지 않는다");
            Assert.That(consumed[0], Is.SameAs(slot.card));
        }

        [Test]
        public void Abort_CancelsCutin()
        {
            executor.Abort();

            Assert.That(cutin.CancelCount, Is.EqualTo(1));
        }

        [Test]
        public void CanRunSlot_RejectsMissingPieces()
        {
            SkillData skill = NewSkill("베어내기", Role.Warrior);
            Ally warrior = NewAlly("Warrior");

            Assert.That(ComboExecutor.CanRunSlot(NewSlot(skill, warrior)), Is.True);
            Assert.That(ComboExecutor.CanRunSlot(NewSlot(skill, null)), Is.False);
            Assert.That(ComboExecutor.CanRunSlot(new ComboSlot { card = new ComboCard(null), caster = warrior }),
                        Is.False);
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>호출만 기록하는 스텁. Play가 이터레이터가 아니라서 호출 즉시 기록된다.</summary>
        private class FakeCutin : ISkillCutin
        {
            public readonly List<(Ally caster, SkillData data)> Calls = new List<(Ally, SkillData)>();
            public int CancelCount;

            public IEnumerator Play(Ally caster, SkillData data)
            {
                Calls.Add((caster, data));
                return Empty();
            }

            public void Cancel() => CancelCount++;

            private static IEnumerator Empty() { yield break; }
        }

        /// <summary>중첩 열거자는 펌프하지 않는다 — Run 본문의 진행만 본다.</summary>
        private static void Drain(IEnumerator routine)
        {
            int guard = 0;
            while (routine.MoveNext())
            {
                if (++guard > 1000)
                    Assert.Fail("Run 코루틴이 끝나지 않는다 — 무한 루프");
            }
        }

        private ComboSlot NewSlot(SkillData data, Ally caster)
            => new ComboSlot { card = new ComboCard(data), caster = caster, target = TargetInfo.None };

        private SkillData NewSkill(string skillName, Role role)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = skillName;
            data.role = role;
            assets.Add(data);
            return data;
        }

        private Ally NewAlly(string name) => NewObject(name).AddComponent<Ally>();

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
```

- [ ] **Step 3: `ComboExecutor`에 컷인 배선을 추가한다**

`Assets/Scripts/Battle/ComboExecutor.cs`의 `private Coroutine running;` 아래에 넣는다:

```csharp
        /// <summary>
        /// 슬롯 실행 직전에 재생할 인트로. null이면 컷인 없이 곧바로 스킬로 간다.
        /// 씬에서는 Awake가 자식에서 찾아 꽂고, 테스트는 세터로 페이크를 넣는다.
        /// </summary>
        public ISkillCutin Cutin { get; set; }

        private void Awake()
        {
            if (Cutin == null) Cutin = GetComponentInChildren<ISkillCutin>(true);
        }
```

- [ ] **Step 4: `Abort`가 컷인을 취소하게 고친다**

같은 파일의 `Abort`를 통째로 바꾼다:

```csharp
        public void Abort()
        {
            if (running != null) StopCoroutine(running);
            running = null;

            // StopCoroutine으로 잘린 코루틴은 finally가 돌지 않는다.
            // 컷인이 내려놓은 TimeControl.Scale을 여기서 되돌리지 않으면 게임이 영구 정지한다.
            Cutin?.Cancel();
        }
```

- [ ] **Step 5: 슬롯 유효성 판정을 밖으로 끌어올린다**

같은 파일에 새 메서드를 더한다(`RunSlot` 바로 위):

```csharp
        /// <summary>
        /// 이 슬롯을 실제로 발동할 수 있는지. 컷인을 띄울지도 이 판정을 따른다 —
        /// 발동하지 않을 슬롯에 인트로만 뜨면 유령 연출이 된다.
        /// </summary>
        public static bool CanRunSlot(in ComboSlot slot)
        {
            if (slot.Data == null) return false;
            if (slot.caster == null) return false;
            return !slot.caster.Combat.IsDead;
        }
```

- [ ] **Step 6: `Run`을 공개하고 루프를 재구성한다**

같은 파일의 `Run` 시그니처와 슬롯 루프를 바꾼다. 시그니처:

```csharp
        /// <summary>
        /// 큐를 순서대로 소화한다. <see cref="Execute"/>가 코루틴으로 돌린다.
        /// public인 이유는 에디트모드 테스트가 스케줄러 없이 직접 펌프하기 위해서다
        /// (<see cref="RecentHitEnemyHUD.TickExpiry"/>와 같은 취지).
        /// </summary>
        public IEnumerator Run(Queue<ComboSlot> queue)
```

`while (queue.Count > 0)` 블록을 통째로 아래로 교체한다:

```csharp
            int index = 0;
            while (queue.Count > 0)
            {
                ComboSlot slot = queue.Dequeue();
                BattleLog.Log(LogCategory.Combo,
                    $"── 슬롯 {index++}: {(slot.Data != null ? slot.Data.skillName : "(비어있음)")} / {BattleLog.Name(slot.caster)}", this);

                if (!CanRunSlot(in slot))
                {
                    BattleLog.Warn(LogCategory.Combo,
                        $"슬롯 건너뜀 — data {(slot.Data == null ? "없음" : slot.Data.skillName)} / caster {BattleLog.Name(slot.caster)}", this);

                    // 발동 못 해도 카드는 버린 더미로 보낸다. 안 그러면 덱에서 증발한다.
                    OnSlotConsumed?.Invoke(slot.card);

                    // 발동하지 않은 슬롯 때문에 콤보가 멈칫할 이유가 없어 slotGap은 건너뛴다.
                    continue;
                }

                // 컷인은 스킬보다 먼저다. 여기서 시간이 멈추고, 끝나야 다시 흐른다.
                IEnumerator intro = PlayCutin(in slot);
                if (intro != null) yield return intro;

                if (slot.Data.IsCharge)
                {
                    StartCharge(slot, pending);
                    OnSlotConsumed?.Invoke(slot.card);
                    continue;   // 대기하지 않는다 — 뒤 슬롯이 그대로 이어진다
                }

                yield return RunSlot(slot);

                OnSlotConsumed?.Invoke(slot.card);

                if (slotGap > 0f)
                    yield return WaitScaled(slotGap);
            }
```

- [ ] **Step 7: `PlayCutin`을 더한다**

`CanRunSlot` 바로 아래에 넣는다:

```csharp
        /// <summary>
        /// 컷인을 시작하고 대기용 열거자를 돌려준다. 컷인이 없으면 null.
        ///
        /// <b>이터레이터가 아니다</b> — 이 메서드를 부르는 순간 <see cref="ISkillCutin.Play"/>가
        /// 실행돼야 한다. 이터레이터로 만들면 호출자가 펌프하기 전까지 아무 일도 일어나지 않아,
        /// 스케줄러 없이 도는 에디트모드 테스트에서 컷인이 통째로 사라진다.
        /// </summary>
        private IEnumerator PlayCutin(in ComboSlot slot)
        {
            return Cutin?.Play(slot.caster, slot.Data);
        }
```

- [ ] **Step 8: `RunSlot`의 앞부분을 정리한다**

`RunSlot` 첫머리의 조기 탈출은 그대로 남긴다 — `CanRunSlot`과 중복이지만 `RunSlot`이 단독으로도
안전해야 한다. 다만 로그 문구를 바꿔 루프 쪽 경고와 구분되게 한다:

```csharp
            if (data == null || caster == null || caster.Combat.IsDead)
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"RunSlot 진입 직전 무효화 — data {(data == null ? "없음" : data.skillName)} / caster {BattleLog.Name(caster)}", this);
                yield break;
            }
```

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/Battle/ISkillCutin.cs Assets/Scripts/Battle/ISkillCutin.cs.meta \
        Assets/Scripts/Battle/ComboExecutor.cs \
        Assets/Editor/Tests/ComboExecutorCutinTests.cs Assets/Editor/Tests/ComboExecutorCutinTests.cs.meta
git commit -m "feat: 콤보 슬롯마다 컷인을 앞세우는 ISkillCutin 계약"
```

---

### Task 2: 컷인 타임라인 순수 함수

슬라이드 진행률 계산을 코루틴에서 떼어 내 단위 테스트한다. 다음 태스크가 이 함수 위에
캔버스를 얹는다.

**Files:**
- Create: `Assets/Scripts/Battle/UI/SkillCutinUI.cs`
- Test: `Assets/Editor/Tests/SkillCutinTimelineTests.cs`

**Interfaces:**
- Consumes: 없음 (순수 계산)
- Produces:
  - `const float SkillCutinUI.SlideIn = 0.12f`
  - `const float SkillCutinUI.Hold = 0.25f`
  - `const float SkillCutinUI.SlideOut = 0.12f`
  - `const float SkillCutinUI.LabelDelay = 0.06f`
  - `static float SkillCutinUI.Duration` — `0.55f`
  - `static float SkillCutinUI.SlideAmount(float elapsed, float delay)` — 0(대기 위치) ~ 1(등장 위치)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/SkillCutinTimelineTests.cs`:

```csharp
using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>컷인 슬라이드 진행률. 코루틴 없이 계산만 검증한다.</summary>
    public class SkillCutinTimelineTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void AtStart_IsFullyOffscreen()
        {
            Assert.That(SkillCutinUI.SlideAmount(0f, 0f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void AfterSlideIn_IsFullyShown()
        {
            Assert.That(SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn, 0f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void DuringHold_StaysFullyShown()
        {
            float mid = SkillCutinUI.SlideIn + SkillCutinUI.Hold * 0.5f;

            Assert.That(SkillCutinUI.SlideAmount(mid, 0f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void AtEnd_IsFullyOffscreenAgain()
        {
            float end = SkillCutinUI.SlideIn + SkillCutinUI.Hold + SkillCutinUI.SlideOut;

            Assert.That(SkillCutinUI.SlideAmount(end, 0f), Is.EqualTo(0f).Within(Tol));
            Assert.That(SkillCutinUI.SlideAmount(end + 5f, 0f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void SlideIn_IsMonotonicallyIncreasing()
        {
            float quarter = SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.25f, 0f);
            float half = SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.5f, 0f);
            float threeQuarter = SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.75f, 0f);

            Assert.That(quarter, Is.LessThan(half));
            Assert.That(half, Is.LessThan(threeQuarter));
            Assert.That(threeQuarter, Is.LessThan(1f));
        }

        [Test]
        public void SlideIn_EasesOut_FastThenSlow()
        {
            // ease-out은 절반 시점에 절반보다 많이 진행해 있다.
            Assert.That(SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn * 0.5f, 0f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void Delay_ShiftsTheWholeCurve()
        {
            float d = SkillCutinUI.LabelDelay;

            Assert.That(SkillCutinUI.SlideAmount(d, d), Is.EqualTo(0f).Within(Tol));
            Assert.That(SkillCutinUI.SlideAmount(d * 0.5f, d), Is.EqualTo(0f).Within(Tol),
                        "지연 구간에는 화면 밖에 머문다");
            Assert.That(SkillCutinUI.SlideAmount(SkillCutinUI.SlideIn + d, d), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void Duration_CoversTheDelayedLabel()
        {
            float expected = SkillCutinUI.SlideIn + SkillCutinUI.Hold + SkillCutinUI.SlideOut + SkillCutinUI.LabelDelay;

            Assert.That(SkillCutinUI.Duration, Is.EqualTo(expected).Within(Tol));
            Assert.That(SkillCutinUI.Duration, Is.EqualTo(0.55f).Within(Tol));
        }
    }
}
```

- [ ] **Step 2: 상수와 순수 함수만 담은 파일을 만든다**

`Assets/Scripts/Battle/UI/SkillCutinUI.cs`:

```csharp
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스킬 발동 직전에 화면 왼쪽으로 밀려 들어오는 컷인.
    /// 초상화가 먼저, 스킬명이 <see cref="LabelDelay"/>만큼 뒤따라 들어와 층을 이룬다.
    /// </summary>
    public class SkillCutinUI : MonoBehaviour
    {
        /// <summary>화면 밖 → 제자리. ease-out.</summary>
        public const float SlideIn = 0.12f;

        /// <summary>제자리에 머무는 시간. 스킬명을 읽을 여유.</summary>
        public const float Hold = 0.25f;

        /// <summary>제자리 → 화면 밖. ease-in.</summary>
        public const float SlideOut = 0.12f;

        /// <summary>스킬명 라벨이 초상화보다 늦게 들어오는 간격.</summary>
        public const float LabelDelay = 0.06f;

        /// <summary>컷인 전체 길이. 늦게 나가는 라벨까지 기다린다.</summary>
        public static float Duration => SlideIn + Hold + SlideOut + LabelDelay;

        /// <summary>
        /// 경과 시간을 0(화면 밖 대기 위치) ~ 1(등장 위치)로 접는다.
        /// <paramref name="delay"/>만큼 곡선 전체가 뒤로 밀린다 — 라벨이 초상화를 뒤따르게.
        /// </summary>
        public static float SlideAmount(float elapsed, float delay)
        {
            float t = elapsed - delay;

            if (t <= 0f) return 0f;

            if (t < SlideIn)
            {
                // ease-out: 빠르게 들어와 부드럽게 멈춘다.
                float x = t / SlideIn;
                return 1f - (1f - x) * (1f - x);
            }

            if (t < SlideIn + Hold) return 1f;

            if (t < SlideIn + Hold + SlideOut)
            {
                // ease-in: 천천히 떨어졌다 빠르게 빠진다.
                float x = (t - SlideIn - Hold) / SlideOut;
                return 1f - x * x;
            }

            return 0f;
        }
    }
}
```

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Battle/UI/SkillCutinUI.cs Assets/Scripts/Battle/UI/SkillCutinUI.cs.meta \
        Assets/Editor/Tests/SkillCutinTimelineTests.cs Assets/Editor/Tests/SkillCutinTimelineTests.cs.meta
git commit -m "feat: 컷인 슬라이드 타임라인 계산"
```

---

### Task 3: `Ally.portrait`와 `SkillCutinUI` 본체

캔버스를 짓고 `ISkillCutin`을 구현한다. 시간 정지와 복원까지 여기서 끝난다.

**Files:**
- Modify: `Assets/Scripts/Characters/Ally.cs`
- Modify: `Assets/Scripts/Battle/UI/SkillCutinUI.cs`
- Test: `Assets/Editor/Tests/SkillCutinUITests.cs`

**Interfaces:**
- Consumes: `SkillCutinUI.SlideAmount`, `SkillCutinUI.Duration`, `SkillCutinUI.LabelDelay`, `ISkillCutin`, `TimeControl.Scale`, `TimeControl.UnscaledDeltaTime`, `Role`
- Produces:
  - `Ally.Portrait` — `Sprite`
  - `static Color SkillCutinUI.RoleColor(Role role)`
  - `SkillCutinUI.IsPlaying` — `bool`
  - `IEnumerator SkillCutinUI.Play(Ally caster, SkillData data)` — 호출 즉시 시간을 멈춘다
  - `void SkillCutinUI.Cancel()`

- [ ] **Step 1: `Ally`에 초상화 필드를 더한다**

`Assets/Scripts/Characters/Ally.cs`의 `[SerializeField] private Role role;` 바로 아래:

```csharp
        [Tooltip("컷인에 뜨는 얼굴. 비우면 직업 색 박스로 대체된다.")]
        [SerializeField] private Sprite portrait;
```

그리고 `public Role Role => role;` 옆에:

```csharp
        public Sprite Portrait => portrait;
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/SkillCutinUITests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 컷인 본체. 시간 정지와 복원이 핵심이다 —
    /// 여기서 새면 게임이 영구 정지하거나 불릿타임이 제멋대로 풀린다.
    /// </summary>
    public class SkillCutinUITests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        private SkillCutinUI cutin;

        [SetUp]
        public void SetUp()
        {
            TimeControl.Reset();
            cutin = NewObject("SkillCutinUI").AddComponent<SkillCutinUI>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++) Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            spawned.Clear();
            assets.Clear();
            cutin = null;
            TimeControl.Reset();
        }

        [Test]
        public void Play_FreezesTimeImmediately()
        {
            cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));

            Assert.That(TimeControl.Scale, Is.EqualTo(0f));
            Assert.That(cutin.IsPlaying, Is.True);
        }

        [Test]
        public void DrainingTheRoutine_RestoresTime()
        {
            IEnumerator routine = cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));

            Drain(routine);

            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(cutin.IsPlaying, Is.False);
        }

        [Test]
        public void Cancel_MidPlay_RestoresTime()
        {
            IEnumerator routine = cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));
            routine.MoveNext();

            Assert.That(TimeControl.Scale, Is.EqualTo(0f), "선행 조건: 아직 멈춰 있어야 한다");

            cutin.Cancel();

            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(cutin.IsPlaying, Is.False);
        }

        [Test]
        public void Cancel_WhenIdle_DoesNotTouchTime()
        {
            TimeControl.Scale = 0f;   // 불릿타임 중이라고 가정

            cutin.Cancel();

            Assert.That(TimeControl.Scale, Is.EqualTo(0f), "재생 중이 아니면 남의 정지를 풀면 안 된다");
        }

        [Test]
        public void PlayingWhileAlreadyFrozen_RestoresToFrozen()
        {
            TimeControl.Scale = 0f;

            IEnumerator routine = cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));
            Drain(routine);

            Assert.That(TimeControl.Scale, Is.EqualTo(0f), "들어올 때 0이었으면 나갈 때도 0이다");
        }

        [Test]
        public void NullCaster_DoesNotThrowAndStillRestoresTime()
        {
            IEnumerator routine = cutin.Play(null, NewSkill("베어내기", Role.Warrior));

            Assert.DoesNotThrow(() => Drain(routine));
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void RoleColor_IsDistinctPerRole()
        {
            var seen = new HashSet<Color>
            {
                SkillCutinUI.RoleColor(Role.Tanker),
                SkillCutinUI.RoleColor(Role.Warrior),
                SkillCutinUI.RoleColor(Role.Archer),
                SkillCutinUI.RoleColor(Role.Wizard),
            };

            Assert.That(seen.Count, Is.EqualTo(4), "직업마다 색이 달라야 폴백이 구실을 한다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static void Drain(IEnumerator routine)
        {
            int guard = 0;
            while (routine.MoveNext())
            {
                if (++guard > 100000)
                    Assert.Fail("컷인 코루틴이 끝나지 않는다 — 무한 루프");
            }
        }

        private SkillData NewSkill(string skillName, Role role)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = skillName;
            data.role = role;
            assets.Add(data);
            return data;
        }

        private Ally NewAlly(string name, Role role)
        {
            Ally ally = NewObject(name).AddComponent<Ally>();

            // role은 [SerializeField] private이라 SerializedObject로 넣는다.
            var so = new UnityEditor.SerializedObject(ally);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.ApplyModifiedPropertiesWithoutUndo();

            return ally;
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
```

> `Drain`의 상한이 100000인 이유: 코루틴이 `TimeControl.UnscaledDeltaTime`으로 진행하는데
> 에디트모드에서는 `Time.deltaTime`이 0에 가깝게 나올 수 있다. 그래서 다음 단계에서
> **테스트가 걸리지 않도록 dt 하한을 둔다.**

- [ ] **Step 3: 컷인 본체를 구현한다**

`Assets/Scripts/Battle/UI/SkillCutinUI.cs`의 클래스 안, `SlideAmount` 아래에 이어 붙인다.
파일 맨 위 `using`에 `using System.Collections;`와 `using UnityEngine.UI;`를 더한다.
클래스 선언을 `public class SkillCutinUI : MonoBehaviour, ISkillCutin`으로 바꾼다.

```csharp
        [Tooltip("초상화 패널 한 변의 길이(1920×1080 기준).")]
        [SerializeField] private float portraitSize = 360f;

        [Tooltip("등장했을 때 초상화 패널의 왼쪽 여백.")]
        [SerializeField] private float portraitShownX = 48f;

        [Tooltip("등장했을 때 스킬명 라벨의 왼쪽 여백. 초상화 오른쪽에 겹친다.")]
        [SerializeField] private float labelShownX = 300f;

        /// <summary>에디트모드에서 Time.deltaTime이 0으로 나와 코루틴이 멈추는 걸 막는 하한.</summary>
        private const float MinStep = 1f / 240f;

        private const float PortraitHiddenX = -400f;
        private const float LabelHiddenX = -660f;

        private RectTransform portraitRect;
        private RectTransform labelRect;
        private Image portraitImage;
        private Text nameLabel;      // 초상화가 없을 때만 켜는 동료 이름
        private Text skillLabel;
        private GameObject panel;

        private float savedScale;

        public bool IsPlaying { get; private set; }

        /// <summary>초상화가 비었을 때 쓰는 직업 색.</summary>
        public static Color RoleColor(Role role)
        {
            switch (role)
            {
                case Role.Tanker:  return new Color32(0x3D, 0x6E, 0xA8, 0xFF);
                case Role.Warrior: return new Color32(0xB0, 0x48, 0x3C, 0xFF);
                case Role.Archer:  return new Color32(0x3F, 0x8F, 0x5B, 0xFF);
                default:           return new Color32(0x7A, 0x4F, 0xA8, 0xFF);   // Wizard
            }
        }

        private void Awake() => EnsureBuilt();

        /// <summary>
        /// 캔버스가 아직 없으면 짓는다.
        /// Awake에 의존하지 않는 이유: 에디트모드 테스트가 <see cref="Play"/>를 곧바로 부르는
        /// 경로가 있어 그때 참조가 비어 있으면 터진다.
        /// </summary>
        private void EnsureBuilt()
        {
            if (panel != null) return;

            BuildUI();
            SetVisible(false);
        }

        /// <summary>
        /// 컷인 재생. <b>호출 즉시</b> 시간을 멈추고 패널을 세운다 —
        /// 반환된 열거자를 펌프하지 않는 호출자도 상태를 관측할 수 있어야 하기 때문
        /// (<see cref="ISkillCutin.Play"/> 계약).
        /// </summary>
        public IEnumerator Play(Ally caster, SkillData data)
        {
            EnsureBuilt();

            // 겹쳐 들어오면 앞의 것을 정리하고 시작한다. 저장한 배율이 덮어써지는 걸 막는다.
            if (IsPlaying) Cancel();

            savedScale = TimeControl.Scale;
            TimeControl.Scale = 0f;
            IsPlaying = true;

            Dress(caster, data);
            SetVisible(true);
            Layout(0f);

            return Animate();
        }

        /// <summary>재생을 즉시 끝낸다. 정지시킨 시간을 반드시 되돌린다.</summary>
        public void Cancel()
        {
            if (!IsPlaying) return;

            IsPlaying = false;
            TimeControl.Scale = savedScale;
            SetVisible(false);
        }

        private IEnumerator Animate()
        {
            float elapsed = 0f;

            while (IsPlaying && elapsed < Duration)
            {
                Layout(elapsed);
                yield return null;

                elapsed += Mathf.Max(MinStep, TimeControl.UnscaledDeltaTime);
            }

            Cancel();
        }

        /// <summary>경과 시간에 맞춰 두 요소의 가로 위치와 투명도를 다시 그린다.</summary>
        private void Layout(float elapsed)
        {
            float p = SlideAmount(elapsed, 0f);
            float l = SlideAmount(elapsed, LabelDelay);

            if (portraitRect != null)
                portraitRect.anchoredPosition = new Vector2(Mathf.Lerp(PortraitHiddenX, portraitShownX, p), 0f);

            if (labelRect != null)
                labelRect.anchoredPosition = new Vector2(Mathf.Lerp(LabelHiddenX, labelShownX, l), 90f);
        }

        /// <summary>이번 컷인에 쓸 얼굴과 글자를 채운다.</summary>
        private void Dress(Ally caster, SkillData data)
        {
            Sprite portrait = caster != null ? caster.Portrait : null;
            Role role = caster != null ? caster.Role : Role.Wizard;

            portraitImage.sprite = portrait;
            portraitImage.color = portrait != null ? Color.white : RoleColor(role);

            // 그림이 없을 때만 이름을 띄운다 — 누구 차례인지는 알아야 한다.
            nameLabel.gameObject.SetActive(portrait == null);
            nameLabel.text = caster != null ? caster.name : string.Empty;

            // skillName이 비어 있으면 에셋 이름으로 대신한다. 빈 컷인이 뜨는 것보다 낫다.
            skillLabel.text = data == null
                ? string.Empty
                : (string.IsNullOrEmpty(data.skillName) ? data.name : data.skillName);
        }

        private void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("SkillCutinCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // RecentHitEnemyHUD(2) 위, DeckInspectorUI(20) 아래.
            canvas.sortingOrder = 10;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            panel = new GameObject("SkillCutinPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            Stretch(panel.GetComponent<RectTransform>());

            // ── 초상화 ──
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(panel.transform, false);
            portraitImage = portraitGo.GetComponent<Image>();
            portraitImage.raycastTarget = false;

            portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.sizeDelta = new Vector2(portraitSize, portraitSize);
            portraitRect.anchoredPosition = new Vector2(PortraitHiddenX, 0f);

            nameLabel = CreateText(portraitGo.transform, "CasterName", 28, TextAnchor.LowerCenter);
            RectTransform nameRect = nameLabel.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.offsetMin = new Vector2(8f, 14f);
            nameRect.offsetMax = new Vector2(-8f, 60f);

            // ── 스킬명 ──
            skillLabel = CreateText(panel.transform, "SkillName", 44, TextAnchor.MiddleLeft);
            labelRect = skillLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(640f, 72f);
            labelRect.anchoredPosition = new Vector2(LabelHiddenX, 90f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // 밝은 배경 위에서도 읽히도록 검은 외곽선을 깐다.
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return text;
        }
```

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Characters/Ally.cs Assets/Scripts/Battle/UI/SkillCutinUI.cs \
        Assets/Editor/Tests/SkillCutinUITests.cs Assets/Editor/Tests/SkillCutinUITests.cs.meta
git commit -m "feat: 컷인 UI 본체와 동료 초상화 필드"
```

---

### Task 4: 씬 배선

코드가 다 있어도 씬에 컴포넌트가 없으면 아무것도 안 뜬다. 여기서 실제로 화면에 나오게 만든다.

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (Unity 에디터에서 직접)

**Interfaces:**
- Consumes: `SkillCutinUI`, `ComboExecutor.Awake`의 `GetComponentInChildren<ISkillCutin>(true)`
- Produces: 없음 (씬 데이터)

- [ ] **Step 1: `ComboExecutor`를 찾는다**

Unity 에디터에서 `Assets/Scenes/SampleScene.unity`를 연다. Hierarchy에서 `ComboExecutor`
컴포넌트가 붙은 오브젝트를 찾는다(`BulletTimeController`가 `GetComponentInChildren<ComboExecutor>()`로
잡으므로 그 오브젝트나 자식에 있다).

- [ ] **Step 2: 컷인 오브젝트를 붙인다**

그 오브젝트의 **자식으로** 빈 GameObject를 만들고 이름을 `SkillCutin`으로 둔 뒤
`SkillCutinUI` 컴포넌트를 붙인다. `ComboExecutor.Awake`의
`GetComponentInChildren<ISkillCutin>(true)`가 자동으로 잡는다 — 인스펙터 배선은 없다.

- [ ] **Step 3: 동료 초상화를 비워 둔 채로 확인한다**

Hierarchy의 동료 4명(`Ally` 컴포넌트)에서 새로 생긴 `Portrait` 슬롯은 **비워 둔다.**
직업 색 박스 폴백이 도는지 먼저 본다. 그림은 나중에 꽂는다.

- [ ] **Step 4: 씬을 저장하고 커밋한다**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: 씬에 스킬 컷인 UI 배선"
```

- [ ] **Step 5: 재생해서 눈으로 확인한다**

테스트로 잡히지 않는 항목이다. 씬을 재생하고 E키로 불릿타임 진입 → 카드 배치 →
Space로 실행한 뒤 아래를 본다:

1. 슬롯마다 컷인이 뜨고, 뜬 동안 적·동료·투사체가 **전부** 멈추는가
2. 4슬롯 콤보에서 컷인 4번이 늘어지지 않는가 (길면 `SkillCutinUI`의 `Hold`를 줄인다)
3. 직업 색 박스가 화면 왼쪽에 잘리지 않고 붙는가 (16:9 외 해상도도 확인)
4. 스킬명이 초상화를 가리지 않고 읽히는가
5. 콤보 도중 동료가 죽어 슬롯이 건너뛰어질 때 컷인이 유령처럼 남지 않는가
6. 콤보가 끝난 뒤 시간이 정상 속도로 돌아오는가 (`TimeControl.Scale`이 0에 묶이지 않는가)

---

## 참고

- 설계서: `docs/superpowers/specs/2026-08-09-skill-cutin-design.md`
- 범위 밖: 라이브 고유기(ASDF)·실시간 카드(U키) 컷인, 컷인 전용 사운드·카메라 줌,
  실제 초상화 그림 제작, 컷인 스킵 입력
