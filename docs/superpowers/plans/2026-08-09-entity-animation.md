# 주인공·적 스프라이트 애니메이션 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 동료의 스프라이트 시트 애니메이션을 주인공·적 6종이 공유하게 하고, 그 과정에서 애니메이션을 통째로 죽이고 있던 바인딩 경로 버그를 고친다.

**Architecture:** 컨트롤러는 `EntityAnimator.controller` 한 벌. 클립도 `Ally_*.anim` 한 벌. 캐릭터 구분은 `SpriteRenderer.color` RGB 틴트뿐이다. 애셋은 전부 텍스트 YAML이므로 직접 편집하고, 배선은 EditMode 테스트가 지킨다.

**Tech Stack:** Unity 6 / C# / NUnit (EditMode) / UnityEditor.Animations

**설계서:** [2026-08-09-entity-animation-design.md](../specs/2026-08-09-entity-animation-design.md)

## Global Constraints

- **테스트를 실행하지 않는다.** Unity Test Runner가 알아서 돌린다. 테스트 **코드는 계획대로 쓰되**, "돌려서 실패를 확인" 단계는 건너뛴다 (프로젝트 CLAUDE.md).
- 테스트 위치는 `Assets/Editor/Tests/`, 네임스페이스 `Prototype.Tests`, NUnit. `Assets/Tests/EditMode/`가 아니다.
- `Assets/Scenes/SampleScene.unity`는 **건드리지 않는다.** 커밋되지 않은 변경 448줄이 올라가 있다. 씬의 색 오버라이드는 이미 죽은 참조(`fileID 2715491290645001719` — `enemy.prefab`에 없는 오브젝트)라 프리팹 틴트를 가리지 않는다.
- 애셋 파일을 지울 때는 `.meta`도 같이 지운다.
- 새 `fileID`는 이 계획이 지정한 값을 그대로 쓴다. 파일 안에서 유일함을 확인해 뒀다.
- Animator는 **루트에 둔다.** `View`로 옮기지 않는다.
- **이미 빨간 테스트가 3개 있다** — `EnemyPrefabBuilderTests`의 `EveryVariant_UsesMeshBodyNotSprite`와 `Body` 머티리얼 검사 2개. 커밋 `7099ac1`이 변종 프리팹을 캡슐 메쉬에서 스프라이트로 손수 바꾸면서 빌더와 그 테스트를 안 고쳐 생긴 기존 부채다. **이 계획의 범위가 아니다.** 쫓아가지 말고, 새로 빨개진 것만 본다.

---

## File Structure

| 파일 | 역할 | 작업 |
|---|---|---|
| `Assets/Data/Animation/*.anim` (26) | 클립. 바인딩 경로가 `Sprite` → `View/Sprite`로 바뀐다 | 수정 (Task 1) |
| `Assets/Editor/Tests/AnimationBindingTests.cs` | 클립 경로가 프리팹 계층에서 해석되는지 | 신규 (Task 1) |
| `Assets/Data/Animation/EntityAnimator.controller` | 6종 공용 상태머신. 모션을 `Ally_*`로 교체 | 수정 (Task 2) |
| `Assets/Data/Animation/AllyAnimator.controller` | 중복 상태머신 | 삭제 (Task 2) |
| `Assets/Data/Animation/{Idle,Move,...}.anim` (10) | 미사용 플레이스홀더 | 삭제 (Task 2) |
| `Assets/Editor/Tests/AnimatorSetupTests.cs` | 컨트롤러 배선·스킬 슬롯·스테이트 1:1 | 신규 (Task 2) |
| `Assets/Prefabs/Enemy_{Melee,Charger,Ranged}.prefab` | `Animator` + `EntityAnimator` 없음 | 수정 (Task 3) |
| `Assets/Prefabs/{Player,Ally,enemy,Enemy_*}.prefab` | 시각 설정(오프셋·플립·시트·틴트) | 수정 (Task 4) |
| `Assets/Editor/Tests/EntityVisualSetupTests.cs` | 시각 설정 고정 | 신규 (Task 4) |
| `Assets/Editor/EnemyPrefabBuilder.cs` | 재빌드 때 변종 틴트가 날아가지 않게 | 수정 (Task 5) |

---

## Task 1: 클립 바인딩 경로 복구

커밋 `7099ac1`이 `Sprite` 위에 `View`를 끼워 넣었는데 클립 26개는 아직 `path: Sprite`를 가리킨다. Animator 기준 상대 경로라 **전부 무효다** — 스프라이트 교체도, 스케일도, 알파도 안 돈다. 지금 화면에 보이는 건 프리팹에 직렬화된 정지 프레임이다.

**Files:**
- Modify: `Assets/Data/Animation/*.anim` (26개 전부)
- Create: `Assets/Editor/Tests/AnimationBindingTests.cs`

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces: 이후 모든 태스크가 "클립이 실제로 재생된다"를 전제로 한다

---

- [ ] **Step 1: 회귀 방지 테스트를 쓴다**

`Assets/Editor/Tests/AnimationBindingTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 애니메이션 커브는 <b>Animator 기준 상대 경로</b>로 대상을 찾는다.
    /// 계층에 노드를 하나 끼워 넣으면 클립이 통째로 조용히 죽는다 — 에러도 경고도 없이,
    /// 그냥 아무것도 안 움직인다. 실제로 커밋 7099ac1이 `View`를 넣으면서 그렇게 됐다.
    ///
    /// 그래서 "경로가 계층에서 해석되는가"를 테스트로 고정한다.
    /// </summary>
    public class AnimationBindingTests
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Player.prefab",
            "Assets/Prefabs/Ally.prefab",
            "Assets/Prefabs/enemy.prefab",
            "Assets/Prefabs/Enemy_Melee.prefab",
            "Assets/Prefabs/Enemy_Charger.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
        };

        [TestCaseSource(nameof(PrefabPaths))]
        public void EveryCurvePath_ResolvesInPrefabHierarchy(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            var animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null, $"{root.name}: 루트에 Animator가 없다");

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Assert.That(controller, Is.Not.Null, $"{root.name}: Animator에 컨트롤러가 비었다");
            Assert.That(controller.animationClips, Is.Not.Empty, $"{root.name}: 컨트롤러에 클립이 없다");

            foreach (AnimationClip clip in controller.animationClips)
            {
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                    AssertResolves(root, clip, b);

                // 스프라이트 교체는 PPtr 커브라 위 목록에 안 나온다. 따로 물어봐야 한다.
                foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    AssertResolves(root, clip, b);
            }
        }

        private static void AssertResolves(GameObject root, AnimationClip clip, EditorCurveBinding binding)
        {
            Transform target = string.IsNullOrEmpty(binding.path)
                ? root.transform
                : root.transform.Find(binding.path);

            Assert.That(target, Is.Not.Null,
                $"{root.name} / {clip.name}: 경로 '{binding.path}'가 계층에 없다 ({binding.propertyName})");
        }
    }
}
```

- [ ] **Step 2: 경로를 치환한다**

`.anim`에는 경로가 두 군데 적힌다. 사람이 읽는 문자열과 런타임이 쓰는 CRC32 해시다. **둘 다** 바꿔야 한다.

| 찾기 | 바꾸기 |
|---|---|
| `path: Sprite` | `path: View/Sprite` |
| `path: 850496168` | `path: 3701061421` |

`crc32("Sprite") = 850496168`, `crc32("View/Sprite") = 3701061421` — 확인 완료.

```bash
python - <<'PY'
import glob, io
changed = 0
for p in glob.glob("Assets/Data/Animation/*.anim"):
    t = io.open(p, encoding="utf-8", newline="").read()
    n = t.replace("path: Sprite\n", "path: View/Sprite\n").replace("path: 850496168\n", "path: 3701061421\n")
    n = n.replace("path: Sprite\r\n", "path: View/Sprite\r\n").replace("path: 850496168\r\n", "path: 3701061421\r\n")
    if n != t:
        io.open(p, "w", encoding="utf-8", newline="").write(n)
        changed += 1
print("changed", changed)
PY
```

줄 끝(`\n`)까지 포함해 매칭하는 이유는 `path: Sprite`가 `path: SpriteRoot` 같은 더 긴 이름의 접두사가 되는 사고를 막기 위해서다. 현재 프로젝트에 그런 경로는 없지만, 치환은 되돌리기 어렵다.

- [ ] **Step 3: 치환 결과를 확인한다**

```bash
grep -rc "path: Sprite$" Assets/Data/Animation/*.anim | grep -v ":0" ; echo "위가 비어야 정상"
grep -rho "path: 850496168" Assets/Data/Animation/*.anim | wc -l ; echo "위가 0이어야 정상"
grep -rho "path: View/Sprite" Assets/Data/Animation/*.anim | wc -l ; echo "123이어야 정상"
grep -rho "path: 3701061421" Assets/Data/Animation/*.anim | wc -l ; echo "54여야 정상"
```

기대값: 문자열 123건, 해시 54건. 옛 값은 0건.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Data/Animation Assets/Editor/Tests/AnimationBindingTests.cs
git commit -m "fix: View 노드 삽입으로 죽은 애니메이션 클립 바인딩 경로 복구"
```

---

## Task 2: 컨트롤러 한 벌로 통합

`AllyAnimator.controller`와 `EntityAnimator.controller`가 **같은 스테이트 집합**을 갖고 클립만 다르다. 한 벌로 합치면서 `Ally_*` 클립을 채택한다. 덤으로 동료의 스킬 클립 교체 버그가 풀린다 — `AnimatorOverrideController`의 키는 원본 클립 이름인데, `AllyAnimator`의 `Skill` 모션이 `Ally_Skill`이라 `EntityAnimator`가 찾는 `Skill_Placeholder`와 안 맞았다.

**Files:**
- Modify: `Assets/Data/Animation/EntityAnimator.controller`
- Modify: `Assets/Prefabs/Ally.prefab:418`
- Delete: `Assets/Data/Animation/AllyAnimator.controller` (+ `.meta`)
- Delete: `Assets/Data/Animation/{Idle,Move,Attack,AerialAttack,Hit,AerialHit,Down,Getup,Dead,Jump}.anim` (+ `.meta`)
- Create: `Assets/Editor/Tests/AnimatorSetupTests.cs`

**Interfaces:**
- Consumes: Task 1이 고친 `Ally_*.anim`
- Produces: `EntityAnimator.controller`(guid `4e799fc9f3995094589e7f87844c1d5d`)가 6종 공용 컨트롤러. Task 3이 이 guid를 프리팹에 꽂는다.

---

- [ ] **Step 1: 배선 테스트를 쓴다**

`Assets/Editor/Tests/AnimatorSetupTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 컨트롤러는 <b>한 벌</b>이다. 6종이 같은 상태머신·같은 클립을 본다.
    /// 캐릭터별 아트가 생기면 컨트롤러를 복사하는 게 아니라
    /// AnimatorOverrideController 애셋으로 클립만 갈아끼운다.
    /// </summary>
    public class AnimatorSetupTests
    {
        private const string ControllerPath = "Assets/Data/Animation/EntityAnimator.controller";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Player.prefab",
            "Assets/Prefabs/Ally.prefab",
            "Assets/Prefabs/enemy.prefab",
            "Assets/Prefabs/Enemy_Melee.prefab",
            "Assets/Prefabs/Enemy_Charger.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
        };

        private static AnimatorController Controller()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(c, Is.Not.Null, $"컨트롤러가 없다: {ControllerPath}");
            return c;
        }

        private static ChildAnimatorState[] States() => Controller().layers[0].stateMachine.states;

        [TestCaseSource(nameof(PrefabPaths))]
        public void EveryEntityPrefab_SharesOneController(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            var animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null, $"{root.name}: 루트에 Animator가 없다");
            Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Is.EqualTo(ControllerPath), $"{root.name}: 공용 컨트롤러를 안 본다");

            var entityAnimator = root.GetComponent<EntityAnimator>();
            Assert.That(entityAnimator, Is.Not.Null, $"{root.name}: EntityAnimator가 없다");

            // 직렬화된 animator 필드가 비면 Awake의 GetComponentInChildren 폴백에 기대게 된다.
            // 그 폴백은 자식 순서에 의존하므로 프리팹에서는 명시 배선을 강제한다.
            var so = new SerializedObject(entityAnimator);
            Assert.That(so.FindProperty("animator").objectReferenceValue, Is.SameAs(animator),
                $"{root.name}: EntityAnimator.animator가 루트 Animator를 안 가리킨다");
        }

        /// <summary>
        /// AnimatorOverrideController의 인덱서 키는 <b>원본 클립의 이름</b>이다.
        /// Skill 스테이트에 다른 클립을 꽂으면 EntityAnimator.SwapSkillClip이 조용히 아무 일도 안 한다.
        /// </summary>
        [Test]
        public void SkillState_KeepsOverrideSlotClip()
        {
            ChildAnimatorState skill = States().Single(s => s.state.name == "Skill");
            Assert.That(skill.state.motion, Is.Not.Null, "Skill 스테이트에 모션이 비었다");
            Assert.That(skill.state.motion.name, Is.EqualTo(EntityAnimator.SkillSlotClip));
        }

        /// <summary>
        /// EntityAnimator는 상태 클래스 이름에서 "State"를 떼어 Animator 스테이트를 찾는다.
        /// 못 찾으면 HasState 검사에 걸려 조용히 넘어가므로, 오타가 런타임에 드러나지 않는다.
        /// </summary>
        [Test]
        public void EveryEntityState_HasMatchingAnimatorState()
        {
            var names = new HashSet<string>(States().Select(s => s.state.name));

            IEnumerable<Type> stateTypes = typeof(EntityState).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IState).IsAssignableFrom(t));

            foreach (Type t in stateTypes)
                Assert.That(names, Contains.Item(AnimatorStateName(t)),
                    $"{t.Name}에 대응하는 Animator 스테이트가 없다");
        }

        /// <summary>EntityAnimator.Apply와 같은 규칙. 스킬 계열은 전부 "Skill" 하나로 모인다.</summary>
        private static string AnimatorStateName(Type t)
        {
            if (typeof(SkillState).IsAssignableFrom(t)) return "Skill";
            return t.Name.EndsWith("State") ? t.Name.Substring(0, t.Name.Length - "State".Length) : t.Name;
        }
    }
}
```

- [ ] **Step 2: 컨트롤러의 모션 guid를 교체한다**

`Assets/Data/Animation/EntityAnimator.controller`에서 `m_Motion:` 줄의 guid만 바꾼다. `Skill`은 건드리지 않는다.

| 스테이트 | 옛 guid (플레이스홀더) | 새 guid (`Ally_*`) |
|---|---|---|
| Idle | `d2ba0595ffca9c74bbaa118c07c9defb` | `6fd85f817888f0d40b265252a4044cfc` |
| Move | `c31690461fe1eee4c8d9568cc5c77b7e` | `41dc38dd4bb71d64e87fcd0d0a84e743` |
| Attack | `ef74227289007d147b456a6a8d6ec2c2` | `bfe234f40cc3fa04996361a4e08d0bfb` |
| AerialAttack | `f265362c04ccca74d8a3e85f2c07b537` | `2c6ff87de5f3b9d4686763bfa5948c3b` |
| Hit | `d180997ba989ba049a85923faebb402e` | `2fa6c6d517101c64d851da43b234bb3e` |
| AerialHit | `68d37f390feb91b4fa8dbc0c5d0c3feb` | `91c2af281e3812746831241cc77934bd` |
| Down | `f9b1b4276b697e0418538fd174bc3a1e` | `98adba4e21fa5ca4aba45869df59ad3b` |
| Getup | `83eee1de1efe6104d912b9ab3f09509e` | `4b05706ad5f47ed458e5ee12c73763b6` |
| Dead | `e46e8e291eef2464b889263e84c58f04` | `ddb2f4a2d6b1e8549a8e0cacb3494cd4` |
| Jump | `496bdfb9f71f2174aa0ef4152a90a5de` | `abf37a0aea25bfb40aea704c31e78b5f` |
| **Skill** | `f0fb86f470d1a6245b645bce79b12752` | **바꾸지 않는다** |

```bash
python - <<'PY'
import io
PAIRS = {
    "d2ba0595ffca9c74bbaa118c07c9defb": "6fd85f817888f0d40b265252a4044cfc",  # Idle
    "c31690461fe1eee4c8d9568cc5c77b7e": "41dc38dd4bb71d64e87fcd0d0a84e743",  # Move
    "ef74227289007d147b456a6a8d6ec2c2": "bfe234f40cc3fa04996361a4e08d0bfb",  # Attack
    "f265362c04ccca74d8a3e85f2c07b537": "2c6ff87de5f3b9d4686763bfa5948c3b",  # AerialAttack
    "d180997ba989ba049a85923faebb402e": "2fa6c6d517101c64d851da43b234bb3e",  # Hit
    "68d37f390feb91b4fa8dbc0c5d0c3feb": "91c2af281e3812746831241cc77934bd",  # AerialHit
    "f9b1b4276b697e0418538fd174bc3a1e": "98adba4e21fa5ca4aba45869df59ad3b",  # Down
    "83eee1de1efe6104d912b9ab3f09509e": "4b05706ad5f47ed458e5ee12c73763b6",  # Getup
    "e46e8e291eef2464b889263e84c58f04": "ddb2f4a2d6b1e8549a8e0cacb3494cd4",  # Dead
    "496bdfb9f71f2174aa0ef4152a90a5de": "abf37a0aea25bfb40aea704c31e78b5f",  # Jump
}
p = "Assets/Data/Animation/EntityAnimator.controller"
t = io.open(p, encoding="utf-8", newline="").read()
for old, new in PAIRS.items():
    assert t.count(old) == 1, (old, t.count(old))
    t = t.replace(old, new)
io.open(p, "w", encoding="utf-8", newline="").write(t)
print("ok")
PY
```

`assert t.count(old) == 1`이 안전장치다. guid가 두 번 나오면 스테이트 하나가 아닌 다른 것까지 바꾸는 것이므로 즉시 멈춘다.

- [ ] **Step 3: Ally 프리팹을 공용 컨트롤러로 돌린다**

`Assets/Prefabs/Ally.prefab:418`:

```
  m_Controller: {fileID: 9100000, guid: 13538b9511267ca43a992a7199bd5667, type: 2}
```
→
```
  m_Controller: {fileID: 9100000, guid: 4e799fc9f3995094589e7f87844c1d5d, type: 2}
```

- [ ] **Step 4: 쓸모없어진 애셋을 지운다**

```bash
cd Assets/Data/Animation
git rm AllyAnimator.controller AllyAnimator.controller.meta
for n in Idle Move Attack AerialAttack Hit AerialHit Down Getup Dead Jump; do
  git rm "$n.anim" "$n.anim.meta"
done
cd -
```

**남기는 것**: `Skill_Placeholder.anim`(오버라이드 슬롯), `SK_AR01/SK_TK01/SK_WR01/SK_WZ01.anim`(스킬 클립), `Ally_Skill.anim`(`SkillData.animation` 후보), `Ally_*` 나머지 10개.

- [ ] **Step 5: 참조가 끊기지 않았는지 확인한다**

```bash
grep -rl "13538b9511267ca43a992a7199bd5667" Assets/ ; echo "위가 비어야 정상 (AllyAnimator guid)"
for g in d2ba0595ffca9c74bbaa118c07c9defb c31690461fe1eee4c8d9568cc5c77b7e ef74227289007d147b456a6a8d6ec2c2 \
         f265362c04ccca74d8a3e85f2c07b537 d180997ba989ba049a85923faebb402e 68d37f390feb91b4fa8dbc0c5d0c3feb \
         f9b1b4276b697e0418538fd174bc3a1e 83eee1de1efe6104d912b9ab3f09509e e46e8e291eef2464b889263e84c58f04 \
         496bdfb9f71f2174aa0ef4152a90a5de; do
  grep -rl "$g" Assets/ && echo "  ^^^ $g 아직 참조됨"
done
echo "위에 '아직 참조됨'이 없어야 정상"
grep -c "f0fb86f470d1a6245b645bce79b12752" Assets/Data/Animation/EntityAnimator.controller ; echo "1이어야 정상 (Skill 슬롯 유지)"
```

- [ ] **Step 6: 커밋**

```bash
git add -A Assets/Data/Animation Assets/Prefabs/Ally.prefab Assets/Editor/Tests/AnimatorSetupTests.cs
git commit -m "refactor: 애니메이터 컨트롤러를 EntityAnimator 한 벌로 통합

동료 스킬 클립 교체가 안 되던 것도 같이 풀린다 - 오버라이드 키가
원본 클립 이름이라 Skill 스테이트는 Skill_Placeholder여야 한다."
```

---

## Task 3: 적 변종 3종에 Animator 달기

`Enemy_Melee` · `Enemy_Charger` · `Enemy_Ranged`에는 `Animator`가 아예 없다. 셋 다 루트에 `Prototype.Enemy`(→ `Entity`)를 갖고 있으므로 `EntityAnimator`의 `[RequireComponent(typeof(Entity))]`를 만족한다.

**Files:**
- Modify: `Assets/Prefabs/Enemy_Melee.prefab`
- Modify: `Assets/Prefabs/Enemy_Charger.prefab`
- Modify: `Assets/Prefabs/Enemy_Ranged.prefab`

**Interfaces:**
- Consumes: Task 2의 `EntityAnimator.controller` (guid `4e799fc9f3995094589e7f87844c1d5d`)
- Produces: 6종 전부가 `Animator` + `EntityAnimator`를 갖는다. Task 2에 쓴 `EveryEntityPrefab_SharesOneController`가 여기서 초록이 된다.

**쓸 값** (파일 안에서 유일함 확인 완료):

| 프리팹 | 루트 GameObject fileID | Animator fileID | EntityAnimator fileID |
|---|---|---|---|
| Enemy_Melee | `2237326508346302774` | `1000008238677715959` | `1001229206819609396` |
| Enemy_Charger | `8205942163614616472` | `1000610670440005824` | `1003116601532776549` |
| Enemy_Ranged | `132684243561720156` | `1002766158367450207` | `1003633028894053985` |

`EntityAnimator` 스크립트 guid: `96942c0634270d740b89f3501714a026`

---

- [ ] **Step 1: 루트 GameObject의 컴포넌트 목록에 두 줄을 더한다**

`Enemy_Melee.prefab` — `m_Component:` 목록 마지막 줄 뒤:

```
  - component: {fileID: 1974912672099923827}
```
→
```
  - component: {fileID: 1974912672099923827}
  - component: {fileID: 1000008238677715959}
  - component: {fileID: 1001229206819609396}
```

`Enemy_Charger.prefab`:

```
  - component: {fileID: 1843587529696900597}
```
→
```
  - component: {fileID: 1843587529696900597}
  - component: {fileID: 1000610670440005824}
  - component: {fileID: 1003116601532776549}
```

`Enemy_Ranged.prefab`:

```
  - component: {fileID: 9166346733289529953}
```
→
```
  - component: {fileID: 9166346733289529953}
  - component: {fileID: 1002766158367450207}
  - component: {fileID: 1003633028894053985}
```

- [ ] **Step 2: 파일 끝에 컴포넌트 문서 두 개를 붙인다**

`Enemy_Melee.prefab` 맨 끝에:

```yaml
--- !u!95 &1000008238677715959
Animator:
  serializedVersion: 7
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2237326508346302774}
  m_Enabled: 1
  m_Avatar: {fileID: 0}
  m_Controller: {fileID: 9100000, guid: 4e799fc9f3995094589e7f87844c1d5d, type: 2}
  m_CullingMode: 0
  m_UpdateMode: 0
  m_ApplyRootMotion: 0
  m_LinearVelocityBlending: 0
  m_StabilizeFeet: 0
  m_AnimatePhysics: 0
  m_WarningMessage: 
  m_HasTransformHierarchy: 1
  m_AllowConstantClipSamplingOptimization: 1
  m_KeepAnimatorStateOnDisable: 0
  m_WriteDefaultValuesOnDisable: 0
--- !u!114 &1001229206819609396
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2237326508346302774}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 96942c0634270d740b89f3501714a026, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Prototype.EntityAnimator
  animator: {fileID: 1000008238677715959}
  crossFade: 0.05
```

`m_WarningMessage: ` 뒤의 **공백 한 칸은 그대로 둔다** — `Ally.prefab`의 직렬화 형태와 같게 맞춘다.

`Enemy_Charger.prefab` 맨 끝에: 위와 같은 두 문서를, `&1000008238677715959`→`&1000610670440005824`, `&1001229206819609396`→`&1003116601532776549`, `m_GameObject: {fileID: 2237326508346302774}`(2군데)→`{fileID: 8205942163614616472}`, `animator: {fileID: 1000008238677715959}`→`{fileID: 1000610670440005824}`로 바꿔 붙인다.

`Enemy_Ranged.prefab` 맨 끝에: 같은 두 문서를, `&1000008238677715959`→`&1002766158367450207`, `&1001229206819609396`→`&1003633028894053985`, `m_GameObject`(2군데)→`{fileID: 132684243561720156}`, `animator:`→`{fileID: 1002766158367450207}`로 바꿔 붙인다.

- [ ] **Step 3: fileID 중복과 참조 정합을 확인한다**

```bash
python - <<'PY'
import re, io
expect = {
    "Enemy_Melee":   ("2237326508346302774", "1000008238677715959", "1001229206819609396"),
    "Enemy_Charger": ("8205942163614616472", "1000610670440005824", "1003116601532776549"),
    "Enemy_Ranged":  ("132684243561720156",  "1002766158367450207", "1003633028894053985"),
}
for name, (root, anim, ea) in expect.items():
    p = f"Assets/Prefabs/{name}.prefab"
    t = io.open(p, encoding="utf-8").read()
    ids = re.findall(r"^--- !u!\d+ &(\d+)", t, re.M)
    assert len(ids) == len(set(ids)), f"{name}: fileID 중복 {[i for i in ids if ids.count(i)>1]}"
    assert ids.count(anim) == 1 and ids.count(ea) == 1, f"{name}: 새 컴포넌트 앵커가 없다"
    assert t.count(f"- component: {{fileID: {anim}}}") == 1, f"{name}: Animator가 컴포넌트 목록에 없다"
    assert t.count(f"- component: {{fileID: {ea}}}") == 1, f"{name}: EntityAnimator가 컴포넌트 목록에 없다"
    assert t.count(f"animator: {{fileID: {anim}}}") == 1, f"{name}: animator 필드 배선이 틀렸다"
    assert t.count("guid: 4e799fc9f3995094589e7f87844c1d5d") == 1, f"{name}: 컨트롤러 참조가 없다"
    print(name, "ok")
PY
```

- [ ] **Step 4: 커밋**

```bash
git add Assets/Prefabs/Enemy_Melee.prefab Assets/Prefabs/Enemy_Charger.prefab Assets/Prefabs/Enemy_Ranged.prefab
git commit -m "feat: 적 변종 3종에 Animator·EntityAnimator 부착"
```

---

## Task 4: 시각 설정을 동료 기준으로 맞춘다

동료만 스프라이트 시트 기준으로 세팅돼 있고 나머지는 사각형 플레이스홀더 기준이다. `spriteOffsetY`가 0.5면 발이 바닥에서 뜨고, `flipToFacing`이 꺼져 있으면 왼쪽으로 걸어도 오른쪽을 본다.

**Files:**
- Modify: `Assets/Prefabs/Player.prefab`
- Modify: `Assets/Prefabs/Ally.prefab`
- Modify: `Assets/Prefabs/enemy.prefab`
- Modify: `Assets/Prefabs/Enemy_Melee.prefab`
- Modify: `Assets/Prefabs/Enemy_Charger.prefab`
- Modify: `Assets/Prefabs/Enemy_Ranged.prefab`
- Create: `Assets/Editor/Tests/EntityVisualSetupTests.cs`

**Interfaces:**
- Consumes: Task 3까지의 프리팹 상태
- Produces: 6종의 `BeltScrollView` 시각 필드와 `SpriteRenderer` 틴트가 고정된다. Task 5의 빌더가 이 틴트값을 재현해야 한다.

**쓸 값:**

| 프리팹 | Sprite의 SpriteRenderer fileID | 새 m_Color |
|---|---|---|
| Player | `6118572367038369068` | `{r: 1, g: 1, b: 1, a: 1}` |
| Ally | `3572346084441602960` | `{r: 0.35, g: 1, b: 0.5, a: 1}` |
| enemy | `7720110155626573987` | `{r: 0.9, g: 0.3, b: 0.28, a: 1}` |
| Enemy_Melee | `1543204692497008705` | 이미 `0.9, 0.3, 0.28` — **그대로** |
| Enemy_Charger | `6447802561654303158` | 이미 `1, 0.65, 0.2` — **그대로** |
| Enemy_Ranged | `4632419540242219351` | 이미 `0.35, 0.62, 1` — **그대로** |

> 주인공의 초록(`0, 1, 0.242`)이 흰색으로 바뀌고, 그 초록 자리를 동료가 물려받는다. "초록 = 아군" 쪽이 관례에 맞고, 주인공은 시트 원색 그대로가 제일 잘 읽힌다.

Ally `_Idle` 시트 첫 프레임: `{fileID: 3204480812468917802, guid: e0f8723057d189b448ec92be461e8175, type: 3}`

---

- [ ] **Step 1: 시각 설정 테스트를 쓴다**

`Assets/Editor/Tests/EntityVisualSetupTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 6종이 같은 스프라이트 시트를 쓰므로 시각 설정도 같아야 한다.
    /// spriteOffsetY가 0이 아니면 발이 바닥에서 뜨고,
    /// flipToFacing이 꺼져 있으면 왼쪽으로 걸어도 오른쪽을 본다 — 시트에 좌향 프레임이 없다.
    /// </summary>
    public class EntityVisualSetupTests
    {
        private const string AllyIdleSheet = "Assets/Art/Character/Ally/_Idle.png";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Player.prefab",
            "Assets/Prefabs/Ally.prefab",
            "Assets/Prefabs/enemy.prefab",
            "Assets/Prefabs/Enemy_Melee.prefab",
            "Assets/Prefabs/Enemy_Charger.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
        };

        private static SpriteRenderer BodyOf(GameObject root)
        {
            Transform sprite = root.transform.Find("View/Sprite");
            Assert.That(sprite, Is.Not.Null, $"{root.name}: View/Sprite가 없다");
            var sr = sprite.GetComponent<SpriteRenderer>();
            Assert.That(sr, Is.Not.Null, $"{root.name}: View/Sprite에 SpriteRenderer가 없다");
            return sr;
        }

        [TestCaseSource(nameof(PrefabPaths))]
        public void EveryEntity_UsesAllySheetAndFlipsToFacing(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            SpriteRenderer body = BodyOf(root);
            Assert.That(AssetDatabase.GetAssetPath(body.sprite), Is.EqualTo(AllyIdleSheet),
                $"{root.name}: 몸 스프라이트가 동료 시트가 아니다");

            var view = root.GetComponent<BeltScrollView>();
            Assert.That(view, Is.Not.Null, $"{root.name}: BeltScrollView가 없다");

            var so = new SerializedObject(view);
            Assert.That(so.FindProperty("spriteOffsetY").floatValue, Is.EqualTo(0f).Within(0.0001f),
                $"{root.name}: spriteOffsetY가 0이 아니라 발이 뜬다");
            Assert.That(so.FindProperty("flipToFacing").boolValue, Is.True,
                $"{root.name}: flipToFacing이 꺼져 있다");
            Assert.That(so.FindProperty("facingRenderer").objectReferenceValue, Is.SameAs(body),
                $"{root.name}: facingRenderer가 몸 렌더러를 안 가리킨다");
        }

        /// <summary>
        /// 틴트는 캐릭터를 구별하는 유일한 수단이다. 두 종류가 같은 색이면 난전에서 못 가른다.
        /// 알파는 검사하지 않는다 — 사망 페이드 커브 소유다.
        /// </summary>
        [Test]
        public void EveryEntity_HasDistinctTint()
        {
            var seen = new System.Collections.Generic.Dictionary<Color, string>();

            foreach (string path in PrefabPaths)
            {
                if (path.EndsWith("enemy.prefab")) continue;   // 기준 더미. Melee와 같은 색이 맞다.

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Color c = BodyOf(root).color;
                c.a = 1f;

                Assert.That(seen.ContainsKey(c), Is.False,
                    $"{root.name}과 {(seen.ContainsKey(c) ? seen[c] : "?")}의 틴트가 같다: {c}");
                seen[c] = root.name;
            }
        }
    }
}
```

- [ ] **Step 2: `BeltScrollView` 필드를 고친다 (5개 프리팹)**

Ally는 이미 맞다. 나머지 5개에서 아래 세 줄을 바꾼다.

```
  spriteOffsetY: 0.5
  flipToFacing: 0
  facingRenderer: {fileID: 0}
```
→ (`<SR>`은 각 프리팹의 Sprite SpriteRenderer fileID)
```
  spriteOffsetY: 0
  flipToFacing: 1
  facingRenderer: {fileID: <SR>}
```

| 프리팹 | 위치 | `<SR>` |
|---|---|---|
| Player | [Player.prefab:574-576](../../../Assets/Prefabs/Player.prefab#L574-L576) | `6118572367038369068` |
| enemy | [enemy.prefab:218-220](../../../Assets/Prefabs/enemy.prefab#L218-L220) | `7720110155626573987` |
| Enemy_Melee | [Enemy_Melee.prefab:308-310](../../../Assets/Prefabs/Enemy_Melee.prefab#L308-L310) | `1543204692497008705` |
| Enemy_Charger | [Enemy_Charger.prefab:687-689](../../../Assets/Prefabs/Enemy_Charger.prefab#L687-L689) | `6447802561654303158` |
| Enemy_Ranged | [Enemy_Ranged.prefab:216-218](../../../Assets/Prefabs/Enemy_Ranged.prefab#L216-L218) | `4632419540242219351` |

- [ ] **Step 3: 몸 스프라이트를 동료 시트로 바꾼다 (5개 프리팹)**

Ally는 이미 맞다. 나머지 5개의 Sprite SpriteRenderer 블록에서:

```
  m_Sprite: {fileID: 7482667652216324306, guid: 75f5f34dc1b5347e0b8351032682f224, type: 3}
```
(Player만 guid가 `311925a002f4447b3a28927169b83ea6`)
→
```
  m_Sprite: {fileID: 3204480812468917802, guid: e0f8723057d189b448ec92be461e8175, type: 3}
```

| 프리팹 | 줄 |
|---|---|
| Player | [Player.prefab:319](../../../Assets/Prefabs/Player.prefab#L319) |
| enemy | [enemy.prefab:436](../../../Assets/Prefabs/enemy.prefab#L436) |
| Enemy_Melee | [Enemy_Melee.prefab:430](../../../Assets/Prefabs/Enemy_Melee.prefab#L430) |
| Enemy_Charger | [Enemy_Charger.prefab:351](../../../Assets/Prefabs/Enemy_Charger.prefab#L351) |
| Enemy_Ranged | [Enemy_Ranged.prefab:541](../../../Assets/Prefabs/Enemy_Ranged.prefab#L541) |

**주의**: 각 프리팹의 `Attack` 자식과 `Shadow` 자식에도 `m_Sprite` 줄이 있다. 위 줄 번호의 것만 바꾼다.

- [ ] **Step 4: 틴트를 바꾼다 (2개 프리팹만)**

적 3종은 이미 맞는 색을 갖고 있다. 두 줄만 바꾼다.

[Player.prefab:320](../../../Assets/Prefabs/Player.prefab#L320):
```
  m_Color: {r: 0, g: 1, b: 0.24236071, a: 1}
```
→
```
  m_Color: {r: 1, g: 1, b: 1, a: 1}
```

[enemy.prefab:437](../../../Assets/Prefabs/enemy.prefab#L437):
```
  m_Color: {r: 1, g: 0, b: 0, a: 1}
```
→
```
  m_Color: {r: 0.9, g: 0.3, b: 0.28, a: 1}
```

[Ally.prefab:618](../../../Assets/Prefabs/Ally.prefab#L618):
```
  m_Color: {r: 1, g: 1, b: 1, a: 1}
```
→
```
  m_Color: {r: 0.35, g: 1, b: 0.5, a: 1}
```

- [ ] **Step 5: 결과를 확인한다**

```bash
python - <<'PY'
import re, io
SR = {"Player":"6118572367038369068","Ally":"3572346084441602960","enemy":"7720110155626573987",
      "Enemy_Melee":"1543204692497008705","Enemy_Charger":"6447802561654303158","Enemy_Ranged":"4632419540242219351"}
WANT = {"Player":"{r: 1, g: 1, b: 1, a: 1}", "Ally":"{r: 0.35, g: 1, b: 0.5, a: 1}",
        "enemy":"{r: 0.9, g: 0.3, b: 0.28, a: 1}", "Enemy_Melee":"{r: 0.9, g: 0.3, b: 0.28, a: 1}",
        "Enemy_Charger":"{r: 1, g: 0.65, b: 0.2, a: 1}", "Enemy_Ranged":"{r: 0.35, g: 0.62, b: 1, a: 1}"}
for name, sid in SR.items():
    lines = io.open(f"Assets/Prefabs/{name}.prefab", encoding="utf-8").read().split("\n")
    s = next(i for i, l in enumerate(lines) if l.startswith(f"--- !u!212 &{sid}"))
    blk = "\n".join(lines[s:s+80])
    assert "guid: e0f8723057d189b448ec92be461e8175" in blk, f"{name}: 몸 스프라이트가 동료 시트가 아니다"
    assert f"m_Color: {WANT[name]}" in blk, f"{name}: 틴트가 기대값과 다르다"
    t = "\n".join(lines)
    assert "spriteOffsetY: 0\n" in t, f"{name}: spriteOffsetY"
    assert "flipToFacing: 1\n" in t, f"{name}: flipToFacing"
    assert f"facingRenderer: {{fileID: {sid}}}" in t, f"{name}: facingRenderer"
    print(name, "ok")
PY
```

- [ ] **Step 6: 커밋**

```bash
git add Assets/Prefabs Assets/Editor/Tests/EntityVisualSetupTests.cs
git commit -m "feat: 주인공·적 6종을 동료 스프라이트 시트 + 색 틴트로 통일"
```

---

## Task 5: 재빌드해도 적 틴트가 살아남게 한다

`Enemy_Melee/Charger/Ranged`는 [EnemyPrefabBuilder](../../../Assets/Editor/EnemyPrefabBuilder.cs) 메뉴가 `enemy.prefab`을 복제해 찍어낸다. 복제본이므로 Animator도 `BeltScrollView` 설정도 자동으로 따라오지만, **변종별 색은 지금 머티리얼에만 반영되고 스프라이트에는 안 닿는다.** 메뉴를 다시 누르면 세 프리팹의 틴트가 전부 `enemy.prefab`의 붉은색으로 무너진다.

캡슐 메쉬(`BuildBody`)는 이 태스크에서 건드리지 않는다 — 별건이다.

**Files:**
- Modify: `Assets/Editor/EnemyPrefabBuilder.cs`
- Modify: `Assets/Editor/Tests/EntityVisualSetupTests.cs`

**Interfaces:**
- Consumes: Task 4가 프리팹에 박은 틴트값
- Produces: `EnemyPrefabBuilder.TryGetVariantColor(string enemyId, out Color color)` — 변종 id(`Enemy_Melee` 등)로 색을 조회한다. 테스트가 프리팹과 빌더 표의 일치를 검사하는 데 쓴다.

---

- [ ] **Step 1: 빌더 표와 프리팹의 일치를 검사하는 테스트를 더한다**

`Assets/Editor/Tests/EntityVisualSetupTests.cs`에 아래를 추가한다 (`using Prototype.EditorTools;`도 파일 위쪽에 더한다):

```csharp
        /// <summary>
        /// 변종 프리팹은 EnemyPrefabBuilder 메뉴가 enemy.prefab을 복제해 만든다.
        /// 빌더가 스프라이트 색을 안 넣으면 메뉴를 누르는 순간 세 변종이 전부 기준 프리팹 색이 된다.
        /// 그래서 "빌더가 아는 색"과 "프리팹에 박힌 색"이 같은지 검사한다.
        /// </summary>
        [TestCase("Assets/Prefabs/Enemy_Melee.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Charger.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Ranged.prefab")]
        public void VariantTint_MatchesBuilderTable(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            Assert.That(EnemyPrefabBuilder.TryGetVariantColor(root.name, out Color expected), Is.True,
                $"빌더 표에 {root.name}이 없다");

            Color actual = BodyOf(root).color;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f), $"{root.name} R");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f), $"{root.name} G");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f), $"{root.name} B");
        }
```

- [ ] **Step 2: 빌더에 색 조회 API를 연다**

`Assets/Editor/EnemyPrefabBuilder.cs`의 `Variants` 배열 선언 바로 아래에 추가한다:

```csharp
        /// <summary>
        /// 변종 색을 밖에서 물어볼 수 있게 연다. 프리팹에 박힌 틴트와 이 표가
        /// 어긋나면 메뉴를 누르는 순간 색이 바뀌므로, 테스트가 둘을 맞물려 놓는다.
        /// </summary>
        /// <param name="enemyId">프리팹 이름(<c>Enemy_Melee</c>) 또는 변종 id(<c>Melee</c>).</param>
        internal static bool TryGetVariantColor(string enemyId, out Color color)
        {
            string id = enemyId != null && enemyId.StartsWith("Enemy_")
                ? enemyId.Substring("Enemy_".Length)
                : enemyId;

            foreach (Variant v in Variants)
            {
                if (v.id != id) continue;
                color = v.color;
                return true;
            }

            color = Color.white;
            return false;
        }
```

- [ ] **Step 3: 빌더가 몸 스프라이트를 물들이게 한다**

`BuildVariant` 안, `BuildBody(root, material);` 줄 **바로 다음**에 한 줄을 넣는다:

```csharp
            BuildBody(root, material);
            TintSprite(root, v.color);
```

그리고 `BuildBody` 메서드 정의 바로 아래에 헬퍼를 추가한다:

```csharp
        /// <summary>
        /// 몸 스프라이트를 변종색으로 물들인다. 이 색이 곧 EnemyStateTint의 BaseColor가 되어,
        /// 상태에 물든 뒤에도 "무슨 적이었는지"의 흔적으로 남는다.
        ///
        /// 알파는 건드리지 않는다 — 사망 페이드 커브 소유다.
        /// </summary>
        private static void TintSprite(GameObject root, Color color)
        {
            Transform sprite = root.transform.Find("View/Sprite");
            if (sprite == null)
            {
                Debug.LogWarning($"[EnemyPrefabBuilder] {root.name}: View/Sprite가 없어 틴트를 못 넣는다");
                return;
            }

            var sr = sprite.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            Color c = color;
            c.a = sr.color.a;
            sr.color = c;
        }
```

- [ ] **Step 4: 컴파일 가능한 형태인지 눈으로 확인한다**

```bash
grep -n "TryGetVariantColor\|TintSprite" Assets/Editor/EnemyPrefabBuilder.cs
grep -n "using Prototype.EditorTools;" Assets/Editor/Tests/EntityVisualSetupTests.cs
```

`EnemyPrefabBuilder`(`Assets/Editor/`)와 테스트(`Assets/Editor/Tests/`)는 둘 다 `Assembly-CSharp-Editor`라 `internal`로 닿는다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Editor/EnemyPrefabBuilder.cs Assets/Editor/Tests/EntityVisualSetupTests.cs
git commit -m "fix: 적 프리팹 재빌드 때 변종 틴트가 날아가지 않게"
```

---

## 마무리 — 눈으로 확인할 것

테스트로 못 잡는다. 씬을 재생해서 본다. 위에서부터 순서대로.

- [ ] 동료·주인공·적이 **실제로 움직인다**. 안 움직이면 Task 1이 안 먹은 것 — 다른 걸 보기 전에 여기부터.
- [ ] 발이 바닥에 닿는다 (`spriteOffsetY: 0`이 이 시트에 맞는가)
- [ ] 왼쪽으로 이동하면 스프라이트가 뒤집힌다
- [ ] 그림자(`0.9 × 0.35`)가 새 실루엣 발밑에 맞는다
- [ ] `Attack` 히트박스(중심 `0, 0.9, 1`)가 새 스프라이트 기준으로 안 어긋난다
- [ ] 6종의 틴트가 서로 구분된다
- [ ] 상태 틴트와 안 헷갈린다. 두 조합이 위험하다:
      Charger 주황(`1, 0.65, 0.2`) × 넉백 주황(`1, 0.549, 0.259`),
      Ranged 파랑(`0.35, 0.62, 1`) × 공중 하늘색(`0.349, 0.761, 1`).
      `TintStrength`가 0.8이라 궁수가 뜨면 원래 색과 거의 같아진다.
      안 보이면 라벨로 충분한지 판단하고, 아니면 궁수 색 이동을 별건으로 뺀다.
- [ ] 죽을 때 페이드가 끝까지 간다
- [ ] 스킬을 쓰면 스킬별 클립이 나온다 (동료 스킬 슬롯 버그가 풀렸는지)
