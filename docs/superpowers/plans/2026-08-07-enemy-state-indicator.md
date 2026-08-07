# 적 상태 표시 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 적의 `CombatState`를 몸 색과 머리 위 라벨로 이중 부호화해, 콤보 중 어떤 적이 스킬에 걸렸는지 한눈에 보이게 한다.

**Architecture:** 상태 → 표현 매핑을 `CombatStateVisuals` static 표 한 곳에 두고, 색을 칠하는 `EnemyStateTint`(적마다 1개, 이벤트 구동)와 라벨을 그리는 `EnemyStateLabel`(씬에 1개, 프레임 구동)이 같은 표를 본다. 둘 다 배선 없이 코드로 자동 부착된다.

**Tech Stack:** Unity 2D (URP), C#, NUnit EditMode 테스트

설계 문서: [2026-08-07-enemy-state-indicator-design.md](../specs/2026-08-07-enemy-state-indicator-design.md)

## Global Constraints

- 네임스페이스는 `Prototype`. 테스트는 `Prototype.Tests`.
- 새 파일 3개는 전부 `Assets/Scripts/Vfx/` 에 둔다.
- 테스트는 `Assets/Editor/Tests/` 에 둔다. asmdef로 만든 어셈블리는 `Assembly-CSharp`(게임 코드)를 참조할 수 없어서 이 위치가 유일하게 동작한다.
- 주석은 한국어. **무엇을 하는지가 아니라 왜 그렇게 했는지**를 적는다 — 기존 코드베이스의 규칙이다.
- `TintStrength = 0.8f`. 적 고유색의 흔적을 남긴다.
- 색 알파는 절대 덮지 않는다. 사망 페이드가 알파를 쓴다.
- 표시 대상 상태는 `Neutral`·`Dead`를 뺀 6개.

**테스트 실행 방법 (모든 태스크 공통):**

이 저장소에는 CLI 테스트 러너가 없다. 유니티 에디터에서 돌린다:

1. 유니티 에디터를 연다 (스크립트 컴파일이 끝날 때까지 기다린다)
2. `Window > General > Test Runner`
3. `EditMode` 탭 선택
4. 대상 테스트 클래스를 고르고 `Run Selected`

컴파일 에러가 있으면 Test Runner에 테스트가 아예 안 뜬다. 그때는 Console 창을 먼저 본다.

---

### Task 1: `CombatStateVisuals` — 상태 → 표현 매핑표

**Files:**
- Create: `Assets/Scripts/Vfx/CombatStateVisuals.cs`
- Test: `Assets/Editor/Tests/CombatStateVisualsTests.cs`

**Interfaces:**
- Consumes: `CombatState` enum ([Assets/Scripts/Core/Enums.cs](../../../Assets/Scripts/Core/Enums.cs))
- Produces:
  - `static bool CombatStateVisuals.ShouldShow(CombatState)`
  - `static string CombatStateVisuals.Label(CombatState)` — 표시 대상이 아니면 `string.Empty`
  - `static Color CombatStateVisuals.StateColor(CombatState)` — 표시 대상이 아니면 `Color.white`
  - `static Color CombatStateVisuals.Tint(Color baseColor, CombatState)`
  - `const float CombatStateVisuals.TintStrength = 0.8f`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/CombatStateVisualsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 유니티 객체를 만지지 않는 순수 표라서 GameObject 없이 그대로 검증된다.
    /// </summary>
    public class CombatStateVisualsTests
    {
        /// <summary>머리 위에 뜨는 6개 상태. Neutral · Dead는 빠진다.</summary>
        private static readonly CombatState[] Shown =
        {
            CombatState.LightHit,
            CombatState.AerialHit,
            CombatState.Knockback,
            CombatState.WallBound,
            CombatState.Down,
            CombatState.Getup,
        };

        [Test]
        public void NeutralAndDead_AreNotShown()
        {
            Assert.That(CombatStateVisuals.ShouldShow(CombatState.Neutral), Is.False);
            Assert.That(CombatStateVisuals.ShouldShow(CombatState.Dead), Is.False);
        }

        [Test]
        public void EveryOtherState_IsShown()
        {
            foreach (CombatState s in Shown)
                Assert.That(CombatStateVisuals.ShouldShow(s), Is.True, s.ToString());
        }

        [Test]
        public void ShownStates_HaveNonEmptyLabel()
        {
            foreach (CombatState s in Shown)
                Assert.That(CombatStateVisuals.Label(s), Is.Not.Empty, s.ToString());
        }

        [Test]
        public void HiddenStates_HaveEmptyLabel()
        {
            Assert.That(CombatStateVisuals.Label(CombatState.Neutral), Is.Empty);
            Assert.That(CombatStateVisuals.Label(CombatState.Dead), Is.Empty);
        }

        [Test]
        public void ShownStates_HaveDistinctLabels()
        {
            // 두 상태가 같은 글자를 쓰면 라벨이 상태를 구분해 주지 못한다.
            for (int i = 0; i < Shown.Length; i++)
                for (int j = i + 1; j < Shown.Length; j++)
                    Assert.That(CombatStateVisuals.Label(Shown[i]),
                                Is.Not.EqualTo(CombatStateVisuals.Label(Shown[j])),
                                $"{Shown[i]} 와 {Shown[j]} 가 같은 라벨을 쓴다");
        }

        [Test]
        public void Tint_LeavesHiddenStatesAlone()
        {
            var baseColor = new Color(0.9f, 0.3f, 0.28f, 1f);

            Assert.That(CombatStateVisuals.Tint(baseColor, CombatState.Neutral), Is.EqualTo(baseColor));
            Assert.That(CombatStateVisuals.Tint(baseColor, CombatState.Dead), Is.EqualTo(baseColor));
        }

        [Test]
        public void Tint_KeepsBaseAlpha()
        {
            // 사망 페이드가 알파를 쓴다. 상태색 알파가 새어 들어오면 죽는 연출이 끊긴다.
            var baseColor = new Color(0.9f, 0.3f, 0.28f, 0.4f);

            foreach (CombatState s in Shown)
                Assert.That(CombatStateVisuals.Tint(baseColor, s).a,
                            Is.EqualTo(0.4f).Within(0.0001f), s.ToString());
        }

        [Test]
        public void Tint_LandsBetweenBaseAndStateColor()
        {
            var baseColor = new Color(0.9f, 0.3f, 0.28f, 1f);
            Color state = CombatStateVisuals.StateColor(CombatState.AerialHit);
            Color mixed = CombatStateVisuals.Tint(baseColor, CombatState.AerialHit);

            Assert.That(mixed.r, Is.EqualTo(Mathf.Lerp(baseColor.r, state.r, CombatStateVisuals.TintStrength)).Within(0.0001f));
            Assert.That(mixed.g, Is.EqualTo(Mathf.Lerp(baseColor.g, state.g, CombatStateVisuals.TintStrength)).Within(0.0001f));
            Assert.That(mixed.b, Is.EqualTo(Mathf.Lerp(baseColor.b, state.b, CombatStateVisuals.TintStrength)).Within(0.0001f));
        }

        [Test]
        public void Tint_DoesNotFullyCoverBaseColor()
        {
            // 완전히 덮으면 어떤 종류의 적이었는지 알 수 없게 된다.
            var melee = new Color(0.90f, 0.30f, 0.28f, 1f);
            var ranged = new Color(0.35f, 0.62f, 1f, 1f);

            Color a = CombatStateVisuals.Tint(melee, CombatState.Down);
            Color b = CombatStateVisuals.Tint(ranged, CombatState.Down);

            Assert.That(a, Is.Not.EqualTo(b), "고유색이 다르면 물든 색도 달라야 한다");
        }

        [Test]
        public void ShownStates_HaveDistinctColors()
        {
            for (int i = 0; i < Shown.Length; i++)
                for (int j = i + 1; j < Shown.Length; j++)
                    Assert.That(CombatStateVisuals.StateColor(Shown[i]),
                                Is.Not.EqualTo(CombatStateVisuals.StateColor(Shown[j])),
                                $"{Shown[i]} 와 {Shown[j]} 가 같은 색을 쓴다");
        }
    }
}
```

- [ ] **Step 2: 실행해서 실패를 확인한다**

Test Runner → EditMode → `CombatStateVisualsTests` → Run Selected

Expected: 테스트가 목록에 안 뜨고 Console에 컴파일 에러
`The name 'CombatStateVisuals' does not exist in the current context`

- [ ] **Step 3: 최소 구현을 쓴다**

`Assets/Scripts/Vfx/CombatStateVisuals.cs`:

```csharp
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전투 상태를 화면 표현(색 · 글자)으로 옮기는 표. <b>한 벌만</b> 유지한다.
    ///
    /// <see cref="EnemyStateTint"/>(몸 색)와 <see cref="EnemyStateLabel"/>(머리 위 글자)이
    /// 같은 함수를 부른다. 두 벌로 갈리면 색과 글자가 어긋나 이중 부호화가 의미를 잃는다.
    ///
    /// 유니티 객체를 만지지 않으므로 EditMode에서 그대로 검증된다.
    /// </summary>
    public static class CombatStateVisuals
    {
        /// <summary>
        /// 기본 색을 상태색 쪽으로 끌어당기는 정도. 1이면 완전히 덮는다.
        /// 1로 두지 않는 이유는 적 고유색(고블린 빨강 · 궁수 파랑 · 멧돼지 주황)의
        /// 흔적을 남겨, 물든 뒤에도 무슨 적이었는지 알아볼 수 있게 하기 위해서다.
        /// </summary>
        public const float TintStrength = 0.8f;

        // 색과 글자를 함께 쓴다. 색만으로는 색약자가 구분하지 못하고,
        // 글자만으로는 난전에서 안 읽힌다.
        private static readonly Color LightHitColor = new Color(1f, 1f, 1f);           // #FFFFFF
        private static readonly Color AerialHitColor = new Color(0.349f, 0.761f, 1f);  // #59C2FF
        private static readonly Color KnockbackColor = new Color(1f, 0.549f, 0.259f);  // #FF8C42
        private static readonly Color WallBoundColor = new Color(0.886f, 0.290f, 1f);  // #E24AFF
        private static readonly Color DownColor = new Color(0.478f, 0.478f, 0.522f);   // #7A7A85
        private static readonly Color GetupColor = new Color(1f, 0.820f, 0.400f);      // #FFD166

        /// <summary>화면에 드러낼 상태인지. 평상시(Neutral)와 사망은 표시하지 않는다.</summary>
        public static bool ShouldShow(CombatState state)
            => state != CombatState.Neutral && state != CombatState.Dead;

        /// <summary>머리 위에 띄울 글자. 표시 대상이 아니면 빈 문자열.</summary>
        public static string Label(CombatState state)
        {
            switch (state)
            {
                case CombatState.LightHit: return "경직";
                case CombatState.AerialHit: return "공중";
                case CombatState.Knockback: return "넉백";
                case CombatState.WallBound: return "벽꽂";
                case CombatState.Down: return "다운";
                case CombatState.Getup: return "기상";
                default: return string.Empty;
            }
        }

        /// <summary>상태를 대표하는 색. 표시 대상이 아니면 흰색(중립값).</summary>
        public static Color StateColor(CombatState state)
        {
            switch (state)
            {
                case CombatState.LightHit: return LightHitColor;
                case CombatState.AerialHit: return AerialHitColor;
                case CombatState.Knockback: return KnockbackColor;
                case CombatState.WallBound: return WallBoundColor;
                case CombatState.Down: return DownColor;
                case CombatState.Getup: return GetupColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 기본 색을 상태색 쪽으로 섞는다.
        ///
        /// 알파는 항상 기본 색의 것을 쓴다 — 사망 페이드가 알파를 깎는데
        /// 여기서 덮으면 죽는 연출이 도중에 끊긴다.
        /// </summary>
        public static Color Tint(Color baseColor, CombatState state)
        {
            if (!ShouldShow(state)) return baseColor;

            Color mixed = Color.Lerp(baseColor, StateColor(state), TintStrength);
            mixed.a = baseColor.a;
            return mixed;
        }
    }
}
```

- [ ] **Step 4: 실행해서 통과를 확인한다**

Test Runner → EditMode → `CombatStateVisualsTests` → Run Selected

Expected: 9개 테스트 전부 PASS

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Vfx/CombatStateVisuals.cs Assets/Scripts/Vfx/CombatStateVisuals.cs.meta \
        Assets/Editor/Tests/CombatStateVisualsTests.cs Assets/Editor/Tests/CombatStateVisualsTests.cs.meta
git commit -m "feat: 전투 상태 → 색·라벨 매핑표 추가"
```

`.meta` 파일은 유니티가 만든다. 에디터를 한 번 포커스해서 임포트가 끝난 뒤에 커밋한다.

---

### Task 2: `EnemyStateTint` — 상태에 따라 적 몸 색을 물들인다

**Files:**
- Modify: `Assets/Scripts/Entities/BeltScrollView.cs` (`SpriteRoot` 프로퍼티 추가)
- Create: `Assets/Scripts/Vfx/EnemyStateTint.cs`
- Modify: `Assets/Scripts/Characters/Enemy.cs` (`Awake`에서 자동 부착)
- Test: `Assets/Editor/Tests/EnemyStateTintTests.cs`

**Interfaces:**
- Consumes:
  - `CombatStateVisuals.Tint(Color, CombatState)` (Task 1)
  - `Combat.OnCombatStateChanged` — `event Action<CombatState, CombatState>` (prev, next)
  - `Combat.Tick(float dt)` — public. 테스트가 경직 해제를 직접 몬다.
- Produces:
  - `BeltScrollView.SpriteRoot` — `Transform`
  - `EnemyStateTint.BaseColor` — `Color`
  - `EnemyStateTint.Body` — `SpriteRenderer`
  - `EnemyStateTint.Apply(CombatState)` — `void`

**배경 — 적 프리팹이 두 형태로 존재한다:**

| 프리팹 | 몸 렌더러 위치 | BeltScrollView |
|---|---|---|
| `enemy.prefab` | `Sprite` 자식 | 있음 |
| `Enemy_Ranged` · `Enemy_Charger` | 루트 | 없음 |

변종 프리팹이 낡은 기준 프리팹에서 찍혀 자식을 잃은 상태다(별도 스펙에서 고친다).
그래서 몸 렌더러를 찾는 규칙이 두 형태를 모두 다뤄야 한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/EnemyStateTintTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 상태는 실제 타격 경로(<c>Combat.Attack</c>)로 만든다.
    /// <c>Combat.SetCombatState</c>가 private이라 밖에서 상태를 직접 세울 수 없다 —
    /// RecentHitEnemyHUDTests와 같은 방식이다.
    /// </summary>
    public class EnemyStateTintTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        [Test]
        public void Enemy_GetsTintComponentAutomatically()
        {
            Enemy enemy = NewRootRendererEnemy(Color.red);

            Assert.That(enemy.GetComponent<EnemyStateTint>(), Is.Not.Null);
        }

        [Test]
        public void BeforeAnyHit_BodyKeepsItsOwnColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();

            Assert.That(tint.BaseColor, Is.EqualTo(identity));
            Assert.That(tint.Body.color, Is.EqualTo(identity));
        }

        [Test]
        public void OnHit_BodyTurnsTheStateColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);

            Assert.That(enemy.Combat.CombatState, Is.EqualTo(CombatState.LightHit),
                        "선행 조건: 경직 상태로 들어가야 한다");
            Assert.That(tint.Body.color,
                        Is.EqualTo(CombatStateVisuals.Tint(identity, CombatState.LightHit)));
        }

        [Test]
        public void WhenStunEnds_BodyReturnsToItsOwnColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);
            Assert.That(tint.Body.color, Is.Not.EqualTo(identity), "선행 조건: 일단 물들어야 한다");

            // Poke의 경직은 0.3초다. 넉넉히 넘겨 경직을 푼다.
            enemy.Combat.Tick(1f);

            Assert.That(enemy.Combat.CombatState, Is.EqualTo(CombatState.Neutral));
            Assert.That(tint.Body.color, Is.EqualTo(identity));
        }

        [Test]
        public void OnDeath_BodyReturnsToItsOwnColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);
            enemy.Combat.TakeDamage(new DamageData(9999f));

            Assert.That(enemy.Combat.IsDead, Is.True);
            Assert.That(tint.Body.color, Is.EqualTo(identity), "Dead는 표시 대상이 아니다");

            // 이게 성립하는 이유는 Combat.Die()의 순서다:
            //   SetCombatState(Dead) → 여기서 색이 원복된다
            //   owner.ForceDead()    → DeadState.Enter가 그제야 색을 캐시한다
            // 순서가 뒤집히면 페이드가 물든 색에서 시작해 죽는 연출이 이상해진다.
        }

        [Test]
        public void TintKeepsAlpha_SoDespawnFadeSurvives()
        {
            var faded = new Color(0.9f, 0.3f, 0.28f, 0.5f);
            Enemy enemy = NewRootRendererEnemy(faded);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);

            Assert.That(tint.Body.color.a, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void WithBeltScrollView_PicksTheSpriteChildNotTheShadow()
        {
            Enemy enemy = NewBeltScrollEnemy(
                body: new Color(0.9f, 0.3f, 0.28f, 1f),
                shadow: new Color(0f, 0f, 0f, 0.35f));

            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();

            Assert.That(tint.Body, Is.Not.Null);
            Assert.That(tint.Body.gameObject.name, Is.EqualTo("Sprite"));
            Assert.That(tint.BaseColor, Is.EqualTo(new Color(0.9f, 0.3f, 0.28f, 1f)));
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>죽지 않을 만큼 약한 한 대. 경직만 일으킨다.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        private Combat NewAlly()
        {
            GameObject go = NewObject("Ally");
            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }

        /// <summary>
        /// 변종 프리팹(Enemy_Ranged · Enemy_Charger) 형태 — 루트에 몸이 붙어 있다.
        /// 렌더러를 <b>Enemy보다 먼저</b> 붙여야 한다. Enemy.Awake가 즉시 돌면서
        /// EnemyStateTint를 붙이고, 그 Awake가 그 자리에서 색을 읽기 때문이다.
        /// </summary>
        private Enemy NewRootRendererEnemy(Color identity)
        {
            GameObject go = NewObject("Enemy");
            go.AddComponent<SpriteRenderer>().color = identity;
            return go.AddComponent<Enemy>();
        }

        /// <summary>기준 프리팹(enemy.prefab) 형태 — Sprite · Shadow 자식이 있다.</summary>
        private Enemy NewBeltScrollEnemy(Color body, Color shadow)
        {
            GameObject go = NewObject("Enemy");

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.AddComponent<SpriteRenderer>().color = shadow;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            spriteGo.AddComponent<SpriteRenderer>().color = body;

            var view = go.AddComponent<BeltScrollView>();

            // private [SerializeField]는 SerializedObject로만 안전하게 건드린다.
            var so = new SerializedObject(view);
            so.FindProperty("sprite").objectReferenceValue = spriteGo.transform;
            so.FindProperty("shadow").objectReferenceValue = shadowGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go.AddComponent<Enemy>();
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

- [ ] **Step 2: 실행해서 실패를 확인한다**

Test Runner → EditMode → `EnemyStateTintTests` → Run Selected

Expected: 컴파일 에러
`The name 'EnemyStateTint' does not exist in the current context`
`'BeltScrollView' does not contain a definition for 'SpriteRoot'`

- [ ] **Step 3-a: `BeltScrollView`에 `SpriteRoot`를 연다**

`Assets/Scripts/Entities/BeltScrollView.cs` — `private Physics physics;` 선언 바로 위에 넣는다:

```csharp
        /// <summary>
        /// 몸 스프라이트가 붙은 자식. 색을 바꾸려는 쪽(<see cref="EnemyStateTint"/>)이
        /// 그림자를 잘못 집지 않도록 정확히 이 하나만 열어 준다.
        /// </summary>
        public Transform SpriteRoot => sprite;

```

- [ ] **Step 3-b: `EnemyStateTint`를 쓴다**

`Assets/Scripts/Vfx/EnemyStateTint.cs`:

```csharp
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적의 몸 색을 전투 상태에 맞춰 물들인다. 스킬에 걸린 적을 난전에서 골라내기 위한 것.
    ///
    /// <b>Update가 없다</b> — 상태가 바뀌는 순간에만 일한다.
    /// <see cref="Enemy"/>가 스스로 붙이므로 프리팹 · 씬 배선이 없다.
    /// </summary>
    [RequireComponent(typeof(Combat))]
    public class EnemyStateTint : MonoBehaviour
    {
        [Tooltip("물들일 몸 렌더러. 비우면 스스로 찾는다. 그림자를 넣으면 안 된다.")]
        [SerializeField] private SpriteRenderer body;

        private Combat combat;

        /// <summary>물들이기 전의 색. 적 종류를 구분하는 고유색이다.</summary>
        public Color BaseColor { get; private set; } = Color.white;

        public SpriteRenderer Body => body;

        private void Awake()
        {
            combat = GetComponent<Combat>();

            if (body == null) body = ResolveBody();
            if (body != null) BaseColor = body.color;
        }

        private void OnEnable()
        {
            if (combat != null) combat.OnCombatStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (combat != null) combat.OnCombatStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(CombatState prev, CombatState next) => Apply(next);

        /// <summary>
        /// 상태를 색에 반영한다.
        /// Neutral · Dead로 돌아오면 <see cref="CombatStateVisuals.Tint"/>가
        /// 기본 색을 그대로 돌려주므로 원복은 따로 처리하지 않는다.
        /// </summary>
        public void Apply(CombatState state)
        {
            if (body == null) return;
            body.color = CombatStateVisuals.Tint(BaseColor, state);
        }

        /// <summary>
        /// 몸 렌더러를 찾는다.
        ///
        /// <c>GetComponentInChildren</c>은 쓰지 않는다 — 자식 순서에 따라 그림자를 집을 수 있고,
        /// 그러면 몸은 그대로인데 발밑 타원만 물든다.
        /// </summary>
        private SpriteRenderer ResolveBody()
        {
            var view = GetComponent<BeltScrollView>();
            if (view != null && view.SpriteRoot != null)
            {
                SpriteRenderer sr = view.SpriteRoot.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }

            // BeltScrollView가 없는 변종 프리팹은 루트에 몸이 붙어 있다.
            return GetComponent<SpriteRenderer>();
        }
    }
}
```

- [ ] **Step 3-c: `Enemy.Awake`가 자동으로 붙이게 한다**

`Assets/Scripts/Characters/Enemy.cs` — `Awake`를 이렇게 바꾼다:

```csharp
        protected override void Awake()
        {
            base.Awake();
            enemyControl = Control as EnemyControl;
            ApplyData(data);

            // 상태 색은 모든 적에게 붙어야 한다. 프리팹이 두 형태로 갈려 있어
            // 배선으로 보장하면 한쪽이 조용히 빠진다 — 코드로 붙인다.
            if (GetComponent<EnemyStateTint>() == null)
                gameObject.AddComponent<EnemyStateTint>();
        }
```

- [ ] **Step 4: 실행해서 통과를 확인한다**

Test Runner → EditMode → `EnemyStateTintTests` → Run Selected

Expected: 7개 테스트 전부 PASS

`WhenStunEnds_BodyReturnsToItsOwnColor`가 실패하면 `Poke.hitStunDuration`이 0인지 본다.
0이면 `Combat.Tick`이 `stunTimer <= 0f`에서 곧장 빠져나가 상태가 영원히 `LightHit`으로 남는다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Vfx/EnemyStateTint.cs Assets/Scripts/Vfx/EnemyStateTint.cs.meta \
        Assets/Scripts/Entities/BeltScrollView.cs Assets/Scripts/Characters/Enemy.cs \
        Assets/Editor/Tests/EnemyStateTintTests.cs Assets/Editor/Tests/EnemyStateTintTests.cs.meta
git commit -m "feat: 적 몸 색이 전투 상태를 따라간다"
```

---

### Task 3: `EnemyStateLabel` — 머리 위 상태 글자

**Files:**
- Create: `Assets/Scripts/Vfx/EnemyStateLabel.cs`
- Modify: `Assets/Scripts/Vfx/BattleVfx.cs:165` 부근 (러너 부트스트랩에 한 줄 추가)
- Test: `Assets/Editor/Tests/EnemyStateLabelTests.cs`

**Interfaces:**
- Consumes:
  - `CombatStateVisuals.ShouldShow / Label / StateColor` (Task 1)
  - `BattleRegistry.Enemies` — `IReadOnlyList<Entity>`
  - `BeltScroll.ToView(Vector3 ground, float height)` — `Vector3`
  - `BeltScroll.DepthToScreen` — `static float` (get/set)
  - `Entity.Physics` → `Physics.GroundPosition` (`Vector3`), `Physics.Height` (`float`)
- Produces:
  - `static Vector3 EnemyStateLabel.LabelPosition(Vector3 ground, float height, float headOffset)`

**왜 배치 테스트가 없나:** `BattleRegistry`는 `Enemy.Start`에서 채워지는데 EditMode 테스트는
`Start`를 돌리지 않는다. 그래서 순회·풀링은 EditMode에서 검증할 수 없다.
위치 계산만 static 순수 함수로 떼어 검증하고, 나머지는 플레이 모드에서 눈으로 확인한다.

**죽은 적은 순회에서 저절로 빠진다.** `DeadState.Enter`가 `BattleRegistry.Unregister`를 부르므로
목록에서 즉시 사라진다. `IsDead` 검사를 따로 넣지 않는 이유다 — `ShouldShow(Dead)`가 false인 것과
합쳐 이중으로 막힌다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/EnemyStateLabelTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 라벨 위치 계산만 검증한다. 순회 · 풀링은 BattleRegistry에 의존하는데
    /// 그 목록은 Enemy.Start에서 채워지고 EditMode는 Start를 돌리지 않는다.
    /// </summary>
    public class EnemyStateLabelTests
    {
        private float saved;

        [SetUp]
        public void SetUp()
        {
            // DepthToScreen은 static이고 BeltScrollView가 매 프레임 덮어쓴다.
            // 테스트가 값을 고정했다가 원래대로 돌려놓는다.
            saved = BeltScroll.DepthToScreen;
            BeltScroll.DepthToScreen = 0.9f;
        }

        [TearDown]
        public void TearDown() => BeltScroll.DepthToScreen = saved;

        [Test]
        public void LabelFoldsDepthIntoScreenHeight()
        {
            // 깊이 z=2는 화면 세로 1.8로 접힌다(0.9 배율).
            Vector3 p = EnemyStateLabel.LabelPosition(new Vector3(5f, 0f, 2f), height: 0f, headOffset: 1.9f);

            Assert.That(p.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(p.y, Is.EqualTo(1.8f + 1.9f).Within(0.0001f));
        }

        [Test]
        public void LabelRisesWithJumpHeight()
        {
            Vector3 ground = new Vector3(0f, 0f, 0f);

            Vector3 low = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);
            Vector3 high = EnemyStateLabel.LabelPosition(ground, height: 3f, headOffset: 1.9f);

            Assert.That(high.y - low.y, Is.EqualTo(3f).Within(0.0001f),
                        "띄워진 적의 라벨은 같이 올라가야 한다");
        }

        [Test]
        public void LabelSitsAboveTheHead()
        {
            Vector3 ground = new Vector3(0f, 0f, 4f);

            Vector3 body = BeltScroll.ToView(ground, 0f);
            Vector3 label = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);

            Assert.That(label.y, Is.GreaterThan(body.y));
        }

        [Test]
        public void LabelClearsTheChargeGauge()
        {
            // ChargeGauge는 1.6에 뜬다. 라벨이 그보다 낮으면 겹친다.
            const float ChargeGaugeOffset = 1.6f;

            Vector3 ground = Vector3.zero;
            Vector3 gauge = BeltScroll.ToView(ground, ChargeGaugeOffset);
            Vector3 label = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);

            Assert.That(label.y, Is.GreaterThan(gauge.y));
        }
    }
}
```

- [ ] **Step 2: 실행해서 실패를 확인한다**

Test Runner → EditMode → `EnemyStateLabelTests` → Run Selected

Expected: 컴파일 에러 `The name 'EnemyStateLabel' does not exist in the current context`

- [ ] **Step 3-a: `EnemyStateLabel`을 쓴다**

`Assets/Scripts/Vfx/EnemyStateLabel.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적 머리 위에 전투 상태를 글자로 띄운다.
    ///
    /// <see cref="EnemyStateTint"/>의 색과 짝을 이룬다 — 색만으로는 난전에서 안 읽히고,
    /// 색약자는 아예 구분하지 못한다.
    ///
    /// <see cref="ChargeGauge"/>와 같은 자리(<c>[BattleVfx]</c>)에 붙는다 — 씬 배선 0.
    /// </summary>
    public class EnemyStateLabel : MonoBehaviour
    {
        [Tooltip("머리 위로 띄우는 높이. 차징 게이지(1.6)보다 위여야 겹치지 않는다.")]
        [SerializeField] private float headOffset = 1.9f;

        [Tooltip("정렬 오프셋. 차징 게이지(300)보다 앞이다.")]
        [SerializeField] private int sortingOffset = 320;

        [Tooltip("월드 단위 글자 크기.")]
        [SerializeField] private float characterSize = 0.06f;

        [SerializeField] private int fontSize = 48;

        /// <summary>TextMesh와 그 MeshRenderer를 같이 들고 다닌다 — 매 프레임 GetComponent하지 않게.</summary>
        private struct Slot
        {
            public TextMesh text;
            public MeshRenderer renderer;
        }

        private readonly List<Slot> pool = new List<Slot>();

        /// <summary>
        /// 라벨이 놓일 자리.
        ///
        /// 논리 좌표가 아니라 <see cref="BeltScroll.ToView"/>를 거친 <b>그리는 위치</b>다.
        /// 루트 transform은 깊이가 화면 세로로 접히기 전 값(x, 0, z)을 들고 있어서,
        /// 그대로 쓰면 라벨이 발밑 훨씬 아래에 찍힌다.
        /// </summary>
        public static Vector3 LabelPosition(Vector3 ground, float height, float headOffset)
            => BeltScroll.ToView(ground, height + headOffset);

        // 캐릭터 위치는 LateUpdate에 확정된다(BeltScrollView). 그 뒤에 읽는다.
        // TimeControl을 보지 않으므로 불릿타임 중에도 보인다 — 조준하는 동안 상태가 보여야 한다.
        private void LateUpdate()
        {
            int used = Draw(BattleRegistry.Enemies);

            // 남는 슬롯은 끈다. 파괴하지 않는다 — 다음 타격에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                pool[i].renderer.enabled = false;
        }

        private int Draw(IReadOnlyList<Entity> list)
        {
            if (list == null) return 0;

            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null) continue;

                CombatState state = e.Combat.CombatState;
                if (!CombatStateVisuals.ShouldShow(state)) continue;

                Draw(Take(n), e, state);
                n++;
            }

            return n;
        }

        private void Draw(Slot slot, Entity e, CombatState state)
        {
            Physics phys = e.Physics;
            Vector3 ground = phys.GroundPosition;

            slot.text.text = CombatStateVisuals.Label(state);
            slot.text.color = CombatStateVisuals.StateColor(state);

            slot.text.transform.position = LabelPosition(ground, phys.Height, headOffset);
            slot.text.transform.rotation = Quaternion.identity;   // 빌보드

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋만 준다.
            slot.renderer.sortingOrder = Mathf.RoundToInt(-ground.z * 100f) + sortingOffset;
            slot.renderer.enabled = true;
        }

        private Slot Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("EnemyStateLabel");
                go.transform.SetParent(transform, false);

                var text = go.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
                text.characterSize = characterSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;

                var renderer = go.GetComponent<MeshRenderer>();

                // TextMesh는 폰트를 꽂아도 렌더러 머티리얼을 스스로 맞추지 않는다.
                // 이걸 빼면 글자가 분홍 사각형으로 나온다.
                renderer.sharedMaterial = text.font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                pool.Add(new Slot { text = text, renderer = renderer });
            }

            return pool[index];
        }
    }
}
```

- [ ] **Step 3-b: 러너 부트스트랩에 등록한다**

`Assets/Scripts/Vfx/BattleVfx.cs` — `go.AddComponent<ChargeGauge>();` 바로 아래에 한 줄 넣는다:

```csharp
                var go = new GameObject("[BattleVfx]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<VfxRunner>();
                go.AddComponent<RangeIndicator>();
                go.AddComponent<ChargeGauge>();
                // 이 러너는 첫 연출이 터질 때 만들어진다. 라벨이 필요한 시점은
                // 적이 맞은 뒤인데 타격은 항상 Impact 연출을 동반하므로 순서가 어긋나지 않는다.
                go.AddComponent<EnemyStateLabel>();
                return instance;
```

- [ ] **Step 4: 실행해서 통과를 확인한다**

Test Runner → EditMode → `EnemyStateLabelTests` → Run Selected

Expected: 4개 테스트 전부 PASS

이어서 Task 1·2의 테스트도 다시 돌려 회귀가 없는지 본다:
Test Runner → EditMode → `Run All`
Expected: 기존 테스트 포함 전부 PASS

- [ ] **Step 5: 플레이 모드에서 눈으로 확인한다**

`Assets/Scenes/SampleScene.unity`를 열고 Play.

확인할 것:
1. 적을 때리면 몸이 흰색으로 물들고 머리 위에 `경직`이 뜬다
2. 경직이 풀리면 색과 글자가 같이 사라진다
3. 띄우는 스킬을 맞히면 `공중`(하늘색), 떨어지면 `다운`(회색), 일어나면 `기상`(노랑)
4. 여러 마리를 광역기로 치면 **맞은 놈들만** 물든다
5. 적이 죽으면 라벨이 사라지고 페이드가 정상적으로 돈다

글자가 너무 크거나 작으면 `[BattleVfx]` 오브젝트의 `EnemyStateLabel`에서
`characterSize`(0.06)와 `headOffset`(1.9)을 조정한다. 이 값들은 화면에서 맞추는 값이다.

글자가 **분홍 사각형**으로 나오면 Step 3-a의 `renderer.sharedMaterial = text.font.material;`
줄이 빠진 것이다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Vfx/EnemyStateLabel.cs Assets/Scripts/Vfx/EnemyStateLabel.cs.meta \
        Assets/Scripts/Vfx/BattleVfx.cs \
        Assets/Editor/Tests/EnemyStateLabelTests.cs Assets/Editor/Tests/EnemyStateLabelTests.cs.meta
git commit -m "feat: 적 머리 위에 전투 상태 라벨 표시"
```

튜닝으로 `characterSize`·`headOffset`을 바꿨다면 별도 커밋으로 남긴다:

```bash
git commit -am "fix: 상태 라벨 크기·높이 조정"
```

---

## 완료 기준

- [ ] EditMode 테스트 전부 통과 (`Run All`)
- [ ] SampleScene 플레이에서 5가지 확인 항목이 전부 맞음
- [ ] 커밋 3~4개

## 이 계획에서 뺀 것

별도 스펙으로 처리한다. 지금 손대면 회귀 원인을 못 가린다.

1. **깊이감** — 뒷벽 + 깊이 스케일. 현재 바닥이 단색 사각형 한 장이라 평면으로 읽힌다.
2. **변종 프리팹 복구** — `Enemy_Ranged`·`Enemy_Charger`에 `Sprite`/`Shadow` 자식과
   `BeltScrollView`가 없다. `EnemyPrefabBuilder.Paint`가 루트 대신 Sprite 자식을 칠하도록
   고치고 빌더를 다시 돌려야 한다.
3. **스킬 디버프 표시** — 둔화·도발·피해감소. `StatusEffect` 레지스트리 신설이 선행돼야 한다.
