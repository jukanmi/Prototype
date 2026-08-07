# 벨트스크롤 깊이감 (뒷벽 + 깊이 스케일) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 점프한 높이와 깊이(Z)가 화면에서 갈라져 읽히도록 뒷벽·경계선을 세우고 캐릭터·투사체에 깊이 배율을 먹인다.

**Architecture:** 표현 전용 변경이다. 논리 좌표·콜라이더·이동은 한 줄도 안 바뀐다. `BeltScroll`에 순수 함수 `ScaleAt(z)`를 하나 더 두고, 캐릭터는 `root → View → Sprite` 로 노드를 한 겹 늘려 **깊이 배율은 View가, 스쿼시·스트레치는 Sprite가** 나눠 가진다(Animator 클립이 `Sprite.localScale`을 이미 쓰고 있어 한 트랜스폼에 둘을 얹으면 매 프레임 서로 덮어쓴다). 방 배경은 `SceneLayoutBuilder`가 `BackWall`·`Horizon` 판을 깔아 높이의 기준면을 만든다.

**Tech Stack:** Unity 6000.5.5f1 / C# / NUnit EditMode 테스트 (`Assets/Editor/Tests/`, asmdef 없음 — Assembly-CSharp-Editor에 그대로 둔다)

## Global Constraints

- **논리 좌표 불변.** `Physics`, 콜라이더, `wallMask`, 히트박스 위치를 건드리지 않는다. 깊이 배율은 렌더링 트랜스폼에만 걸린다.
- **바닥은 사각형.** X에는 원근을 먹이지 않는다 — `BeltScroll.ToGround` 역변환(마우스 피킹)과 히트박스가 논리 X에서 1:1로 유지돼야 한다.
- **깊이 배율 계수** `DepthScalePerUnit = 0.06f`. 방 깊이 z ∈ [-3, +3] 기준 1.18 ~ 0.82.
- **배율 하한** `BeltScroll.MinScale = 0.05f`.
- **깊이 → 화면 세로 비율** `DepthToScreen = 0.9f` (기존값 유지). 방 반경 `RoomHalfX = 6`, `RoomHalfZ = 3` (기존값 유지).
- **`Sprite`의 `localScale`은 Animator 소유다.** 런타임 코드가 절대 쓰지 않는다.
- 테스트 파일은 `Assets/Editor/Tests/`, 네임스페이스 `Prototype.Tests`, NUnit `[Test]`.
- 주석·커밋 메시지는 한국어. 기존 파일들의 톤(왜 그렇게 했는지를 적는다)을 따른다.
- 빌더(`SceneLayoutBuilder`, `AnimationBuilder`, `ArtImportBuilder`)는 **여러 번 돌려도 같은 결과**여야 한다.

## 시작 전 — 작업 트리 정리 (필수)

계획을 쓰는 시점에 **적 상태 표시 기능이 커밋 안 된 채 작업 트리에 있다**:

```
 M Assets/Scripts/Characters/Enemy.cs
 M Assets/Scripts/Entities/BeltScrollView.cs      ← Task 2가 또 건드린다
 M Assets/Scripts/Vfx/BattleVfx.cs
?? Assets/Scripts/Vfx/CombatStateVisuals.cs
?? Assets/Scripts/Vfx/EnemyStateLabel.cs
?? Assets/Scripts/Vfx/EnemyStateTint.cs
?? Assets/Editor/Tests/CombatStateVisualsTests.cs
?? Assets/Editor/Tests/EnemyStateTintTests.cs
```

`BeltScrollView.cs`가 겹친다(적 상태 표시가 `SpriteRoot` 프로퍼티를 더해 놨다).
이대로 Task 2를 커밋하면 두 기능이 한 커밋에 섞인다.

**Task 1을 시작하기 전에 이 작업을 먼저 커밋한다:**

```bash
git add Assets/Scripts/Characters/Enemy.cs Assets/Scripts/Entities/BeltScrollView.cs \
        Assets/Scripts/Vfx/BattleVfx.cs Assets/Scripts/Vfx/CombatStateVisuals.cs \
        Assets/Scripts/Vfx/EnemyStateLabel.cs Assets/Scripts/Vfx/EnemyStateTint.cs \
        Assets/Editor/Tests/CombatStateVisualsTests.cs Assets/Editor/Tests/EnemyStateTintTests.cs
git commit -m "feat: 적 상태 표시 — 색 변화 + 머리 위 라벨"
git status --short   # 남은 변경이 없어야 한다
```

**충돌 없음 확인:** `EnemyStateTint`는 몸 렌더러를 이름이 아니라
`BeltScrollView.SpriteRoot`(= `sprite` 필드)로 찾는다. View 노드를 끼워도 `sprite`가
가리키는 트랜스폼은 그대로라 안 깨진다. 하이어라키 경로로 찾는 코드는 없다.

## 테스트 실행 방법

에디터가 이 프로젝트를 열고 있지 않을 때:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe" \
  -batchmode -projectPath "C:/Users/onebe/Prototype" \
  -runTests -testPlatform EditMode \
  -testFilter "Prototype.Tests.<TestClassName>" \
  -testResults "C:/Users/onebe/Prototype/Temp/test-results.xml" \
  -logFile -
```

에디터가 열려 있으면 batchmode가 프로젝트 락 때문에 실패한다. 그때는 Unity 메뉴
`Window > General > Test Runner` → `EditMode` 탭 → 해당 클래스 실행.

결과 확인: `Temp/test-results.xml` 의 `<test-run ... result="Passed" failed="0">`.

## File Structure

| 파일 | 책임 | 상태 |
|---|---|---|
| `Assets/Scripts/Entities/BeltScroll.cs` | 논리↔화면 변환 규약 한 벌. `ScaleAt` 추가 | 수정 |
| `Assets/Scripts/Entities/BeltScrollView.cs` | 캐릭터 한 명의 표현 갱신. `depthRoot` 추가, `Sync()` 공개 | 수정 |
| `Assets/Scripts/Entities/Projectile.cs` | 투사체 표현. `UpdateView`에 배율 | 수정 |
| `Assets/Editor/SceneLayoutBuilder.cs` | 씬 배치·방 배경 생성. View 노드 배선 + BackWall/Horizon | 수정 |
| `Assets/Editor/AnimationBuilder.cs` | 플레이스홀더 클립. 바인딩 경로만 | 수정 |
| `Assets/Editor/ArtImportBuilder.cs` | 도트 시트 임포트. 경로 상수만 | 수정 |
| `Assets/Editor/Tests/BeltScrollDepthTests.cs` | `ScaleAt` 순수 함수 + 좌표 왕복 | 신규 |
| `Assets/Editor/Tests/BeltScrollViewDepthTests.cs` | View 노드 분리가 실제로 동작하는지 | 신규 |
| `Assets/Editor/Tests/SceneLayoutRoomTests.cs` | 방 배경 + View 노드 배선의 멱등성 | 신규 |
| `Assets/Scripts/Vfx/ChargeGauge.cs` | 차지 게이지. 머리 오프셋에만 배율 | 수정 (Task 7) |
| `Assets/Scripts/Vfx/EnemyStateLabel.cs` | 상태 라벨. 머리 오프셋에만 배율 | 수정 (Task 7) |
| `Assets/Editor/Tests/ChargeGaugePositionTests.cs` | 머리 오프셋 배율 | 신규 (Task 7) |

---

### Task 1: `BeltScroll.ScaleAt` — 깊이 배율 규약

**Files:**
- Modify: `Assets/Scripts/Entities/BeltScroll.cs`
- Test: `Assets/Editor/Tests/BeltScrollDepthTests.cs` (신규)

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces:
  - `public static float BeltScroll.DepthScalePerUnit { get; set; }` — 기본 `0.06f`
  - `public const float BeltScroll.MinScale = 0.05f`
  - `public static float BeltScroll.ScaleAt(float z)` → `Mathf.Max(MinScale, 1f - z * DepthScalePerUnit)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/BeltScrollDepthTests.cs` 신규 생성:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 깊이 배율은 순수 함수다. 여기서 값이 어긋나면 캐릭터 · 투사체가 한꺼번에 틀어진다.
    /// </summary>
    public class BeltScrollDepthTests
    {
        [SetUp]
        public void SetDefaults()
        {
            BeltScroll.DepthToScreen = 0.9f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void ResetStatics()
        {
            // static이라 다음 테스트로 새어 나간다. 원래 기본값으로 돌려놓는다.
            BeltScroll.DepthToScreen = 0.5f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [Test]
        public void ScaleAt_Origin_IsOne()
        {
            Assert.That(BeltScroll.ScaleAt(0f), Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>방 깊이 ±3에서 앞뒤 1.44배. 이게 "보통" 강도로 고른 값이다.</summary>
        [Test]
        public void ScaleAt_BackShrinks_FrontGrows()
        {
            Assert.That(BeltScroll.ScaleAt(3f), Is.EqualTo(0.82f).Within(0.0001f), "뒤쪽이 작아야 한다");
            Assert.That(BeltScroll.ScaleAt(-3f), Is.EqualTo(1.18f).Within(0.0001f), "앞쪽이 커야 한다");
        }

        /// <summary>하한이 없으면 배율이 음수가 돼 스프라이트가 뒤집힌다.</summary>
        [Test]
        public void ScaleAt_FarBeyondRoom_ClampsToMinimum()
        {
            Assert.That(BeltScroll.ScaleAt(1000f), Is.EqualTo(BeltScroll.MinScale).Within(0.0001f));
        }

        /// <summary>
        /// 배율이 좌표 변환에 새면 마우스 피킹("보이는 곳"과 "찍히는 곳")이 어긋난다.
        /// ScaleAt을 넣은 뒤에도 왕복이 그대로여야 한다.
        /// </summary>
        [Test]
        public void ViewToGround_RoundTrip_StillMatches()
        {
            var ground = new Vector3(2.5f, 0f, -1.75f);

            Vector3 back = BeltScroll.ToGround(BeltScroll.ToView(ground));

            Assert.That(back.x, Is.EqualTo(ground.x).Within(0.0001f));
            Assert.That(back.z, Is.EqualTo(ground.z).Within(0.0001f));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Test Runner에서 `BeltScrollDepthTests` 실행.
예상: **컴파일 에러** — `BeltScroll`에 `DepthScalePerUnit`, `MinScale`, `ScaleAt`이 없다.

- [ ] **Step 3: 최소 구현**

`Assets/Scripts/Entities/BeltScroll.cs` — `DepthToScreen` 프로퍼티 바로 아래에 추가:

```csharp
        /// <summary>
        /// 깊이 1당 줄어드는 표시 배율. z=0이 기준 1.0이다.
        /// <see cref="BeltScrollView"/>가 자기 설정값으로 덮어쓴다.
        /// </summary>
        public static float DepthScalePerUnit { get; set; } = 0.06f;

        /// <summary>
        /// 배율 하한. 방 밖으로 밀려나거나 계수를 크게 잡으면 1 - z·k가 0 아래로 내려가
        /// 스프라이트가 좌우로 뒤집히거나 사라진다.
        /// </summary>
        public const float MinScale = 0.05f;

        /// <summary>
        /// 깊이에 따른 표시 배율. <b>논리 좌표에는 영향이 없다</b> —
        /// 판정은 3D 콜라이더가 그대로 하고, 이건 그리는 크기만 바꾼다.
        /// </summary>
        public static float ScaleAt(float z) => Mathf.Max(MinScale, 1f - z * DepthScalePerUnit);
```

- [ ] **Step 4: 통과를 확인한다**

Test Runner에서 `BeltScrollDepthTests` 실행.
예상: 4개 전부 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Entities/BeltScroll.cs Assets/Editor/Tests/BeltScrollDepthTests.cs
git commit -m "feat: 깊이 배율 규약 BeltScroll.ScaleAt 추가"
```

---

### Task 2: `BeltScrollView` — 깊이 배율 전용 노드 분리

**Files:**
- Modify: `Assets/Scripts/Entities/BeltScrollView.cs`
- Test: `Assets/Editor/Tests/BeltScrollViewDepthTests.cs` (신규)

**Interfaces:**
- Consumes: `BeltScroll.ScaleAt(float)`, `BeltScroll.DepthScalePerUnit` (Task 1)
- Produces:
  - `public void BeltScrollView.Sync()` — 한 프레임 분 표현 갱신. `LateUpdate`가 이걸 부른다. 에디트 모드 테스트가 프레임을 기다리지 않고 직접 호출한다.
  - 직렬화 필드 `depthRoot` (`Transform`), `depthScalePerUnit` (`float`, 기본 `0.06f`) — Task 4의 `SceneLayoutBuilder`가 `SerializedObject`로 배선한다.

**하이어라키 규약** (Task 4가 실제로 만든다):

```
root ─ View     ← BeltScrollView: position · rotation · localScale
     │   └ Sprite  ← Animator: localScale · 알파만. localPosition은 0
     └ Shadow   ← BeltScrollView: position · localScale
```

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/BeltScrollViewDepthTests.cs` 신규 생성:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 깊이 배율을 어느 트랜스폼이 갖느냐가 이 기능의 전부다.
    /// Sprite의 localScale은 애니 클립(Idle 바운스 · Jump 스트레치) 소유라
    /// 거기 배율을 얹으면 매 프레임 서로 덮어써서 애니가 통째로 죽는다.
    /// </summary>
    public class BeltScrollViewDepthTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            // Sync가 static을 덮어쓴다. 다음 테스트로 새지 않게 되돌린다.
            BeltScroll.DepthToScreen = 0.5f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        /// <summary>root(Physics + BeltScrollView) → [View →] Sprite, root → Shadow.</summary>
        private BeltScrollView NewRig(Vector3 position, bool withDepthRoot)
        {
            var root = new GameObject("Rig");
            spawned.Add(root);

            // Rigidbody는 RequireComponent가 붙여준다.
            root.AddComponent<Physics>();
            root.transform.position = position;

            var shadow = new GameObject("Shadow").transform;
            shadow.SetParent(root.transform, false);

            Transform depthRoot = null;
            Transform spriteParent = root.transform;
            if (withDepthRoot)
            {
                depthRoot = new GameObject("View").transform;
                depthRoot.SetParent(root.transform, false);
                spriteParent = depthRoot;
            }

            var sprite = new GameObject("Sprite").transform;
            sprite.SetParent(spriteParent, false);
            sprite.gameObject.AddComponent<SpriteRenderer>();

            var view = root.AddComponent<BeltScrollView>();

            var so = new SerializedObject(view);
            so.FindProperty("sprite").objectReferenceValue = sprite;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("depthRoot").objectReferenceValue = depthRoot;
            so.FindProperty("depthToScreen").floatValue = 0.9f;
            so.FindProperty("depthScalePerUnit").floatValue = 0.06f;
            so.FindProperty("spriteOffsetY").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        [Test]
        public void WithDepthRoot_ScalesTheViewNode()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform depthRoot = view.transform.Find("View");

            view.Sync();

            Assert.That(depthRoot.localScale.x, Is.EqualTo(0.82f).Within(0.001f));
            Assert.That(depthRoot.localScale.y, Is.EqualTo(0.82f).Within(0.001f));
        }

        /// <summary>이 테스트가 이 태스크의 핵심이다. 애니 충돌 회귀 방지.</summary>
        [Test]
        public void WithDepthRoot_LeavesSpriteLocalScaleAlone()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform sprite = view.transform.Find("View/Sprite");
            sprite.localScale = new Vector3(1.12f, 0.9f, 1f);   // Animator가 쓴 값을 흉내낸다

            view.Sync();

            Assert.That(sprite.localScale.x, Is.EqualTo(1.12f).Within(0.0001f));
            Assert.That(sprite.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));
        }

        /// <summary>
        /// 센터 피벗 오프셋도 같이 줄어야 발이 그림자 위에 붙는다.
        /// ToView(z=3).y = 3 × 0.9 = 2.7, 거기에 spriteOffsetY(0.5) × 0.82.
        /// </summary>
        [Test]
        public void WithDepthRoot_ScalesTheFootOffset()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform depthRoot = view.transform.Find("View");

            view.Sync();

            Assert.That(depthRoot.position.y, Is.EqualTo(2.7f + 0.5f * 0.82f).Within(0.001f));
        }

        /// <summary>그림자는 바닥에 눕는 물체라 같은 깊이 배율을 받아야 한다.</summary>
        [Test]
        public void WithDepthRoot_ScalesTheShadow()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: true);
            Transform shadow = view.transform.Find("Shadow");

            view.Sync();

            // shadowBaseScale 기본값 (0.9, 0.35, 1). 높이 0이라 shrink는 1이다.
            Assert.That(shadow.localScale.x, Is.EqualTo(0.9f * 0.82f).Within(0.001f));
            Assert.That(shadow.localScale.y, Is.EqualTo(0.35f * 0.82f).Within(0.001f));
        }

        /// <summary>배선 안 된 프리팹(변종 적 등)이 깨지면 안 된다.</summary>
        [Test]
        public void WithoutDepthRoot_KeepsLegacyBehaviour()
        {
            BeltScrollView view = NewRig(new Vector3(0f, 0f, 3f), withDepthRoot: false);
            Transform sprite = view.transform.Find("Sprite");

            view.Sync();

            Assert.That(sprite.position.y, Is.EqualTo(2.7f + 0.5f).Within(0.001f), "배율이 끼면 안 된다");
            Assert.That(sprite.localScale, Is.EqualTo(Vector3.one));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Test Runner에서 `BeltScrollViewDepthTests` 실행.
예상: **컴파일 에러** — `depthRoot` 프로퍼티를 못 찾고(`FindProperty`는 런타임 null이지만 그 전에) `view.Sync()`가 없다.

- [ ] **Step 3: 구현 — 필드 추가**

`Assets/Scripts/Entities/BeltScrollView.cs`, `sprite` 필드 아래에 삽입:

```csharp
        [Tooltip("깊이 배율만 담당하는 노드. Sprite의 부모다. 비우면 깊이 배율을 적용하지 않는다.")]
        [SerializeField] private Transform depthRoot;
```

`depthToScreen` 필드 아래에 삽입:

```csharp
        [Tooltip("깊이(Z) 1당 줄어드는 표시 배율. 0이면 크기가 일정하다. 모든 인스턴스가 같은 값이어야 한다.")]
        [SerializeField] private float depthScalePerUnit = 0.06f;
```

- [ ] **Step 4: 구현 — `LateUpdate`를 `Sync()`로 갈아낸다**

기존 `LateUpdate` 메서드 전체를 아래로 교체한다:

```csharp
        private void LateUpdate() => Sync();

        /// <summary>
        /// 한 프레임 분의 표현 갱신. 에디트 모드 테스트가 프레임을 기다리지 않고 직접 부른다.
        /// </summary>
        public void Sync()
        {
            if (physics == null)
            {
                physics = GetComponentInParent<Physics>();
                if (physics == null) return;
            }

            // 조준점 등 다른 시스템도 같은 비율로 투영해야 한다. 한 벌만 유지한다.
            BeltScroll.DepthToScreen = depthToScreen;
            BeltScroll.DepthScalePerUnit = depthScalePerUnit;

            Vector3 ground = physics.GroundPosition;
            float height = Mathf.Max(0f, physics.Height);

            // depthRoot가 없는 프리팹은 예전 동작 그대로 둔다 — 배율 1.
            float scale = depthRoot != null ? BeltScroll.ScaleAt(ground.z) : 1f;

            if (depthRoot != null)
            {
                // 배율은 여기서만 건다. Sprite의 localScale은 Animator 것이라
                // 거기 쓰면 스쿼시 · 스트레치가 매 프레임 지워진다.
                depthRoot.localScale = new Vector3(scale, scale, 1f);

                // 오프셋에도 배율을 곱해야 뒤쪽 캐릭터의 발이 그림자 위에 붙는다.
                depthRoot.position = BeltScroll.ToView(ground, height + spriteOffsetY * scale);

                // 루트는 Facing 방향으로 돌아간다(Physics.Apply). 자식까지 돌면 옆면이 보이므로
                // 월드 회전을 매 프레임 되돌린다 — 빌보드.
                depthRoot.rotation = Quaternion.identity;
            }

            if (sprite != null)
            {
                if (depthRoot == null)
                {
                    sprite.position = BeltScroll.ToView(ground, height + spriteOffsetY);
                    sprite.rotation = Quaternion.identity;
                }

                // 빌보드로 회전을 지웠으니 방향은 좌우 반전으로만 표현된다.
                // 시트는 오른쪽을 보고 그려져 있다.
                if (flipToFacing)
                {
                    if (facingRenderer == null) facingRenderer = sprite.GetComponent<SpriteRenderer>();
                    if (facingRenderer != null && Mathf.Abs(physics.Facing.x) > 0.0001f)
                        facingRenderer.flipX = physics.Facing.x < 0f;
                }
            }

            if (shadow != null)
            {
                shadow.position = BeltScroll.ToView(ground);
                shadow.rotation = Quaternion.identity;

                float shrink = Mathf.Clamp(1f - height * shadowShrinkPerUnit, 0.3f, 1f);
                shadow.localScale = shadowBaseScale * (shrink * scale);
            }

            ApplySorting(ground.z);
        }
```

클래스 요약 주석(파일 상단 `<summary>`)에 한 줄 덧붙인다:

```
    /// 깊이 배율은 <c>depthRoot</c>(Sprite의 부모)가 받는다 — Sprite의 localScale은 애니 클립 소유다.
```

- [ ] **Step 5: 통과를 확인한다**

Test Runner에서 `BeltScrollViewDepthTests` 실행.
예상: 5개 전부 PASS.

`BeltScrollDepthTests`도 다시 돌려 회귀가 없는지 본다. 예상: 4개 PASS.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Entities/BeltScrollView.cs Assets/Editor/Tests/BeltScrollViewDepthTests.cs
git commit -m "feat: 깊이 배율 전용 View 노드 분리 — 애니 스케일과 충돌 제거"
```

---

### Task 3: `Projectile` — 같은 깊이 배율

**Files:**
- Modify: `Assets/Scripts/Entities/Projectile.cs`

**Interfaces:**
- Consumes: `BeltScroll.ScaleAt(float)` (Task 1)
- Produces: 없음 (외부에서 부르는 API 변화 없음)

**왜 노드 분리가 없나:** `Projectile`에는 Animator가 없다. 프리팹이 `Sprite`에 0.4, `Shadow`에 (0.3, 0.12, 1)을 구워 두었을 뿐이라([ProjectileBuilder.cs:87-88](../../../Assets/Editor/ProjectileBuilder.cs#L87-L88)) 그 값을 `Awake`에서 기억했다가 곱하면 된다. 배율을 안 먹이면 뒤쪽 적에게 날아가는 화염구만 안 줄어들어 눈에 띈다.

- [ ] **Step 1: 기준 배율 필드를 추가한다**

`Assets/Scripts/Entities/Projectile.cs` — `private Attack hitbox;` 아래에 추가:

```csharp
        // 프리팹에 구워진 원래 크기. 깊이 배율을 여기에 곱한다.
        private Vector3 spriteBaseScale = Vector3.one;
        private Vector3 shadowBaseScale = Vector3.one;
```

- [ ] **Step 2: `Awake`에서 기억한다**

기존 `Awake`를 교체:

```csharp
        private void Awake()
        {
            hitbox = GetComponent<Attack>();

            // 깊이 배율은 매 프레임 곱해지므로 원본을 한 번만 잡아 둬야 한다.
            // 갱신된 값을 다시 읽으면 배율이 누적돼 투사체가 점점 사라진다.
            if (sprite != null) spriteBaseScale = sprite.localScale;
            if (shadow != null) shadowBaseScale = shadow.localScale;
        }
```

- [ ] **Step 3: `UpdateView`에 배율을 먹인다**

기존 `UpdateView` 메서드 전체를 교체:

```csharp
        /// <summary>논리 좌표를 화면 좌표로 접는다. 캐릭터와 같은 변환 · 같은 깊이 배율을 쓴다.</summary>
        private void UpdateView()
        {
            Vector3 ground = logical;
            ground.y = 0f;

            float scale = BeltScroll.ScaleAt(ground.z);

            if (sprite != null)
            {
                sprite.position = BeltScroll.ToView(ground, logical.y);
                sprite.rotation = Quaternion.identity;   // 빌보드
                sprite.localScale = spriteBaseScale * scale;
            }

            if (shadow != null)
            {
                shadow.position = BeltScroll.ToView(ground);
                shadow.rotation = Quaternion.identity;
                shadow.localScale = shadowBaseScale * scale;
            }
        }
```

- [ ] **Step 4: 컴파일과 회귀를 확인한다**

Unity 콘솔에 에러가 없는지 본다. Test Runner에서 `BeltScrollDepthTests`, `BeltScrollViewDepthTests` 재실행 — 9개 PASS.

투사체 자체는 씬 재생 없이는 검증이 안 된다. Task 6의 눈 확인 목록에 들어 있다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Entities/Projectile.cs
git commit -m "feat: 투사체에도 깊이 배율 적용"
```

---

### Task 4: `SceneLayoutBuilder` — View 노드 배선

**Files:**
- Modify: `Assets/Editor/SceneLayoutBuilder.cs`
- Test: `Assets/Editor/Tests/SceneLayoutRoomTests.cs` (신규 — Task 5에서 방 배경 테스트를 같은 파일에 더한다)

**Interfaces:**
- Consumes: `BeltScrollView`의 직렬화 필드 `depthRoot`, `depthScalePerUnit` (Task 2)
- Produces:
  - `public static bool SceneLayoutBuilder.RigBeltScrollView(GameObject root)` — private에서 public으로 승격. 테스트가 씬 전체를 건드리지 않고 오브젝트 하나로 부를 수 있어야 한다.
  - `private const string ViewChild = "View"`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/SceneLayoutRoomTests.cs` 신규 생성:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 빌더는 여러 번 돌린다. 두 번째 실행에서 노드가 겹치거나 Sprite가 제자리로
    /// 안 돌아오면 씬이 조용히 망가진다 — 컴파일로는 안 잡힌다.
    /// </summary>
    public class SceneLayoutRoomTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            // 빌더가 Undo에 기록을 남긴다. 다음 테스트로 새지 않게 지운다.
            Undo.ClearAll();
        }

        private GameObject NewRoot(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.AddComponent<Physics>();
            return go;
        }

        [Test]
        public void RigBeltScrollView_MovesSpriteUnderViewNode()
        {
            GameObject root = NewRoot("Rig");

            SceneLayoutBuilder.RigBeltScrollView(root);

            Assert.That(root.transform.Find("View"), Is.Not.Null, "View 노드가 없다");
            Assert.That(root.transform.Find("View/Sprite"), Is.Not.Null, "Sprite가 View 아래에 없다");
            Assert.That(root.transform.Find("Sprite"), Is.Null, "Sprite가 루트에 남아 있다");
        }

        [Test]
        public void RigBeltScrollView_WiresDepthRoot()
        {
            GameObject root = NewRoot("Rig");

            SceneLayoutBuilder.RigBeltScrollView(root);

            var so = new SerializedObject(root.GetComponent<BeltScrollView>());
            Assert.That(so.FindProperty("depthRoot").objectReferenceValue,
                        Is.SameAs(root.transform.Find("View")));
            Assert.That(so.FindProperty("depthScalePerUnit").floatValue,
                        Is.EqualTo(0.06f).Within(0.0001f));
        }

        [Test]
        public void RigBeltScrollView_TwiceKeepsOneViewNode()
        {
            GameObject root = NewRoot("Rig");

            SceneLayoutBuilder.RigBeltScrollView(root);
            SceneLayoutBuilder.RigBeltScrollView(root);

            int views = 0;
            foreach (Transform child in root.transform)
                if (child.name == "View") views++;

            Assert.That(views, Is.EqualTo(1), "View 노드가 중복 생성됐다");
            Assert.That(root.transform.Find("View/Sprite"), Is.Not.Null);
        }

        /// <summary>이미 루트에 Sprite가 있던 예전 프리팹도 옮겨져야 한다.</summary>
        [Test]
        public void RigBeltScrollView_AdoptsExistingRootSprite()
        {
            GameObject root = NewRoot("Rig");
            var legacy = new GameObject("Sprite");
            legacy.transform.SetParent(root.transform, false);
            legacy.AddComponent<SpriteRenderer>();

            SceneLayoutBuilder.RigBeltScrollView(root);

            Assert.That(root.transform.Find("View/Sprite"), Is.SameAs(legacy.transform));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Test Runner에서 `SceneLayoutRoomTests` 실행.
예상: **컴파일 에러** — `RigBeltScrollView`가 private이라 접근 불가.

- [ ] **Step 3: 상수를 추가한다**

`Assets/Editor/SceneLayoutBuilder.cs` — `SpriteChild` 상수 옆에:

```csharp
        private const string ViewChild = "View";
```

`DepthToScreen` 상수 아래에:

```csharp
        /// <summary>깊이 1당 줄어드는 표시 배율. 방 깊이 ±3에서 앞뒤 1.44배.</summary>
        private const float DepthScalePerUnit = 0.06f;
```

- [ ] **Step 4: `RigBeltScrollView`를 고친다**

메서드 시그니처를 `public static bool RigBeltScrollView(GameObject root)`로 바꾸고,
XML 주석과 Sprite 탐색 · 생성 부분을 아래로 교체한다 (기존 `Transform sprite = ...`부터
`if (rootRenderer != null) Undo.DestroyObjectImmediate(rootRenderer);`까지):

```csharp
        /// <summary>
        /// 루트에 붙어 있던 SpriteRenderer를 자식 Sprite로 옮기고, 그 Sprite를 다시
        /// 깊이 배율 전용 노드 View 아래로 넣은 뒤 BeltScrollView에 물린다.
        /// 루트는 논리 좌표만 유지한다.
        ///
        /// View를 한 겹 끼우는 이유: 애니 클립이 Sprite의 localScale을 쓰므로
        /// 깊이 배율을 같은 트랜스폼에 얹으면 매 프레임 서로 덮어쓴다.
        ///
        /// 테스트가 씬을 건드리지 않고 오브젝트 하나로 부를 수 있게 public이다.
        /// </summary>
        public static bool RigBeltScrollView(GameObject root)
        {
            // 이미 옮겨 놓은 씬을 다시 돌릴 수 있어야 한다. 두 자리 다 본다.
            Transform sprite = root.transform.Find(ViewChild + "/" + SpriteChild);
            if (sprite == null) sprite = root.transform.Find(SpriteChild);

            SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();

            if (sprite == null)
            {
                var spriteGo = new GameObject(SpriteChild);
                Undo.RegisterCreatedObjectUndo(spriteGo, "sprite child");
                sprite = spriteGo.transform;
                sprite.SetParent(root.transform, false);

                var sr = spriteGo.AddComponent<SpriteRenderer>();
                if (rootRenderer != null)
                {
                    sr.sprite = rootRenderer.sprite;
                    sr.color = rootRenderer.color;
                    sr.sharedMaterial = rootRenderer.sharedMaterial;
                }
            }

            // 루트 렌더러는 이제 필요 없다. 두면 자식과 겹쳐 그려진다.
            if (rootRenderer != null)
                Undo.DestroyObjectImmediate(rootRenderer);

            Transform depthRoot = EnsureDepthRoot(root, sprite);
```

이어지는 Shadow 생성 블록은 그대로 두고, `SerializedObject` 배선 부분에 두 줄을 더한다
(`so.FindProperty("depthToScreen").floatValue = DepthToScreen;` 바로 아래):

```csharp
            so.FindProperty("depthRoot").objectReferenceValue = depthRoot;
            so.FindProperty("depthScalePerUnit").floatValue = DepthScalePerUnit;
```

- [ ] **Step 5: `EnsureDepthRoot`를 추가한다**

`RigBeltScrollView` 메서드 바로 아래에:

```csharp
        /// <summary>
        /// 깊이 배율 전용 노드. 있으면 그대로 쓰고, Sprite가 이미 그 아래면 아무것도 하지 않는다 —
        /// 빌더는 여러 번 돌린다.
        /// </summary>
        private static Transform EnsureDepthRoot(GameObject root, Transform sprite)
        {
            Transform view = root.transform.Find(ViewChild);
            if (view == null)
            {
                var go = new GameObject(ViewChild);
                Undo.RegisterCreatedObjectUndo(go, "depth root");
                view = go.transform;
                view.SetParent(root.transform, false);
            }

            view.localPosition = Vector3.zero;
            view.localRotation = Quaternion.identity;
            view.localScale = Vector3.one;   // 런타임에 BeltScrollView가 매 프레임 덮어쓴다

            if (sprite.parent != view)
                Undo.SetTransformParent(sprite, view, "reparent sprite");

            // 위치는 View가 잡는다. Sprite는 부모에 붙어만 있으면 된다.
            sprite.localPosition = Vector3.zero;
            sprite.localRotation = Quaternion.identity;

            return view;
        }
```

- [ ] **Step 6: 통과를 확인한다**

Test Runner에서 `SceneLayoutRoomTests` 실행.
예상: 4개 PASS.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Editor/SceneLayoutBuilder.cs Assets/Editor/Tests/SceneLayoutRoomTests.cs
git commit -m "feat: 씬 빌더가 깊이 배율 View 노드를 만들고 배선"
```

---

### Task 5: `SceneLayoutBuilder` — 뒷벽 · 경계선 · 카메라

**Files:**
- Modify: `Assets/Editor/SceneLayoutBuilder.cs`
- Test: `Assets/Editor/Tests/SceneLayoutRoomTests.cs` (Task 4에서 만든 파일에 추가)

**Interfaces:**
- Consumes: `SceneLayoutBuilder`의 기존 상수 `RoomHalfX`(6), `RoomHalfZ`(3), `DepthToScreen`(0.9), `FindSprite(string)`
- Produces:
  - `public static void SceneLayoutBuilder.BuildRoomVisual(GameObject room)` — private에서 public으로 승격
  - `Room` 아래 자식 3개: `BackWall`(sortingOrder -10001), `Floor`(-10000), `Horizon`(-9999)

**치수:** 바닥 윗변 = `RoomHalfZ × DepthToScreen` = 3 × 0.9 = **2.7**.

| 오브젝트 | localPosition | localScale | 색 |
|---|---|---|---|
| `BackWall` | (0, 2.7 + 2.0, 0) | (12, 4, 1) | (0.10, 0.11, 0.14) |
| `Floor` | (0, 0, 0) | (12, 5.4, 1) | (0.16, 0.17, 0.20) |
| `Horizon` | (0, 2.7, 0) | (12, 0.06, 1) | (0.32, 0.34, 0.40) |

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Editor/Tests/SceneLayoutRoomTests.cs`의 `RigBeltScrollView_AdoptsExistingRootSprite` 아래에 추가:

```csharp
        // ── 방 배경 ─────────────────────────────────────

        [Test]
        public void BuildRoomVisual_CreatesWallFloorAndHorizon()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);

            Transform wall = room.transform.Find("BackWall");
            Transform floor = room.transform.Find("Floor");
            Transform horizon = room.transform.Find("Horizon");

            Assert.That(wall, Is.Not.Null, "BackWall 없음");
            Assert.That(floor, Is.Not.Null, "Floor 없음");
            Assert.That(horizon, Is.Not.Null, "Horizon 없음");

            int wallOrder = wall.GetComponent<SpriteRenderer>().sortingOrder;
            int floorOrder = floor.GetComponent<SpriteRenderer>().sortingOrder;
            int horizonOrder = horizon.GetComponent<SpriteRenderer>().sortingOrder;

            Assert.That(wallOrder, Is.LessThan(floorOrder), "벽이 바닥보다 뒤여야 한다");
            Assert.That(floorOrder, Is.LessThan(horizonOrder), "경계선이 바닥보다 앞이어야 한다");

            // 캐릭터는 최대 z=3에서도 -300이다. 배경 셋 다 그보다 뒤여야 한다.
            Assert.That(horizonOrder, Is.LessThan(-300));
        }

        /// <summary>벽 아랫변과 바닥 윗변이 어긋나면 그 틈으로 배경이 뚫려 보인다.</summary>
        [Test]
        public void BuildRoomVisual_WallSitsExactlyOnFloorEdge()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);

            Transform floor = room.transform.Find("Floor");
            Transform wall = room.transform.Find("BackWall");
            Transform horizon = room.transform.Find("Horizon");

            float floorTop = floor.localPosition.y + floor.localScale.y * 0.5f;
            float wallBottom = wall.localPosition.y - wall.localScale.y * 0.5f;

            Assert.That(floorTop, Is.EqualTo(2.7f).Within(0.0001f), "바닥 윗변 = RoomHalfZ × DepthToScreen");
            Assert.That(wallBottom, Is.EqualTo(floorTop).Within(0.0001f));
            Assert.That(horizon.localPosition.y, Is.EqualTo(floorTop).Within(0.0001f));
        }

        [Test]
        public void BuildRoomVisual_TwiceMakesNoDuplicates()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);
            SceneLayoutBuilder.BuildRoomVisual(room);

            Assert.That(room.transform.childCount, Is.EqualTo(3));
        }
```

- [ ] **Step 2: 실패를 확인한다**

Test Runner에서 `SceneLayoutRoomTests` 실행.
예상: **컴파일 에러** — `BuildRoomVisual`이 private.

- [ ] **Step 3: 상수를 추가한다**

`Assets/Editor/SceneLayoutBuilder.cs` — `WallThickness` 상수 아래에:

```csharp
        /// <summary>뒷벽을 그리는 높이. 위가 조금 잘리는 편이 방이 위로 이어져 보인다.</summary>
        private const float WallVisualHeight = 4f;
```

- [ ] **Step 4: `BuildRoomVisual`을 교체한다**

기존 `BuildRoomVisual` 메서드 전체를 아래로 교체:

```csharp
        /// <summary>
        /// 방 배경. 바닥 판 하나로는 점프한 높이가 "화면에서 위로 갔다"로만 보인다.
        /// 뒷벽과 경계선을 세워 <b>높이를 잴 기준면</b>을 만든다.
        ///
        /// 논리 좌표계의 사각형은 진짜 원근이면 사다리꼴이 되지만, X에는 원근을 안 먹인다 —
        /// 판정이 모든 z에서 x ∈ [-RoomHalfX, RoomHalfX]인 직사각형이라 그림만 좁히면 어긋난다.
        ///
        /// 테스트가 씬을 건드리지 않고 임시 오브젝트로 부를 수 있게 public이다.
        /// </summary>
        public static void BuildRoomVisual(GameObject room)
        {
            // 바닥 윗변. 벽과 경계선이 전부 이 높이에 맞물린다.
            float floorTop = RoomHalfZ * DepthToScreen;
            float width = RoomHalfX * 2f;

            // 캐릭터 정렬은 -z*100이라 최저 z(-3)에서도 -300이다. 배경은 전부 그보다 뒤로 보낸다.
            MakePanel(room, "BackWall",
                      new Vector3(0f, floorTop + WallVisualHeight * 0.5f, 0f),
                      new Vector3(width, WallVisualHeight, 1f),
                      new Color(0.10f, 0.11f, 0.14f, 1f), -10001);

            MakePanel(room, "Floor",
                      Vector3.zero,
                      new Vector3(width, RoomHalfZ * 2f * DepthToScreen, 1f),
                      new Color(0.16f, 0.17f, 0.20f, 1f), -10000);

            // 바닥과 벽이 꺾이는 선. 점프 높이가 이 선 대비로 읽힌다.
            MakePanel(room, "Horizon",
                      new Vector3(0f, floorTop, 0f),
                      new Vector3(width, 0.06f, 1f),
                      new Color(0.32f, 0.34f, 0.40f, 1f), -9999);
        }

        private static void MakePanel(GameObject room, string name, Vector3 localPos,
                                      Vector3 scale, Color color, int order)
        {
            Transform t = room.transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "room panel");
                t = go.transform;
                t.SetParent(room.transform, false);
            }

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(t.gameObject);

            sr.sprite = FindSprite("Square");
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = order;

            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            t.localScale = scale;

            EditorUtility.SetDirty(t.gameObject);
        }
```

- [ ] **Step 5: 카메라를 올린다**

`SetupCamera` 안의 위치 한 줄을 교체:

```csharp
            // 아래 여백을 줄이고 뒷벽을 더 보여준다. size 5 기준 세로 -3.7 ~ 6.3 —
            // 바닥 아랫변(-2.7)과 벽 윗변(6.7) 사이가 화면에 거의 다 들어온다.
            cam.transform.position = new Vector3(0f, 1.3f, -10f);
```

- [ ] **Step 6: 통과를 확인한다**

Test Runner에서 `SceneLayoutRoomTests` 실행.
예상: 7개 전부 PASS (Task 4의 4개 + 이번 3개).

- [ ] **Step 7: 커밋**

```bash
git add Assets/Editor/SceneLayoutBuilder.cs Assets/Editor/Tests/SceneLayoutRoomTests.cs
git commit -m "feat: 방 뒷벽과 바닥 경계선 추가, 카메라 높이 조정"
```

---

### Task 6: 애니 바인딩 경로 갱신 + 빌더 재실행 + 눈 확인

**Files:**
- Modify: `Assets/Editor/AnimationBuilder.cs:24` (`SpritePath` 상수)
- Modify: `Assets/Editor/ArtImportBuilder.cs:44` (`SpritePath` 상수)
- Modify: `Assets/Scenes/SampleScene.unity` (빌더 실행 결과)
- Modify: `Assets/Prefabs/Ally.prefab`, `Assets/Data/Animation/*.anim` (빌더 실행 결과)

**Interfaces:**
- Consumes: Task 4가 만든 `root → View → Sprite` 하이어라키
- Produces: 없음 (마무리 태스크)

**왜 필요한가:** 애니 클립의 커브 바인딩은 **경로 문자열**이다. Sprite가 `View/Sprite`로 내려갔으므로 `"Sprite"`로 구워진 클립은 아무 트랜스폼도 못 찾아 조용히 아무 일도 안 한다.

- [ ] **Step 1: `AnimationBuilder`의 경로 상수를 고친다**

`Assets/Editor/AnimationBuilder.cs`:

```csharp
        /// <summary>
        /// 클립이 물리는 자식. SceneLayoutBuilder가 만든 깊이 배율 노드 아래에 있다.
        /// 배율은 View가, 스쿼시 · 스트레치는 Sprite가 나눠 갖는다.
        /// </summary>
        private const string SpritePath = "View/Sprite";
```

- [ ] **Step 2: `ArtImportBuilder`의 경로 상수를 고친다**

`Assets/Editor/ArtImportBuilder.cs:44`:

```csharp
        private const string SpritePath = "View/Sprite";
```

- [ ] **Step 3: 빌더를 순서대로 돌린다**

Unity 메뉴에서 **이 순서로** 실행한다. 반대로 하면 클립이 옛 경로로 구워진다.

1. `Prototype > 씬 벨트스크롤 배치로 정리`
   → 콘솔에 `[SceneLayoutBuilder] 완료 — BeltScrollView 배선 N개` 확인
2. `Prototype > 애니메이션 - 플레이스홀더 굽기 + 배선`
   → 콘솔에 `[AnimationBuilder] 컨트롤러 1 + 스킬 클립 N장` 확인

콘솔에 에러·경고가 있으면 멈추고 원인을 잡는다.

- [ ] **Step 4: 전체 테스트를 돌린다**

Test Runner `EditMode` 전체 실행.
예상: 이번에 추가한 16개(4 + 5 + 7) 포함, 기존 테스트까지 전부 PASS.

`EnemyPrefabBuilderTests`가 `Sprite` 자식 위치를 검사하고 있다면 여기서 깨진다.
깨지면 그 테스트의 기대 경로를 `View/Sprite`로 맞춘다 — 프리팹 구조가 바뀐 게 맞기 때문이다.

- [ ] **Step 5: 씬을 재생해 눈으로 확인한다**

`Assets/Scenes/SampleScene.unity`를 열고 Play. 아래 5개를 순서대로 본다.

1. **뒷벽과 경계선이 보이는가** — 화면 위쪽에 바닥보다 어두운 판, 그 아래 밝은 가로선 한 줄
2. **점프가 읽히는가** — 캐릭터가 뛰면 경계선을 넘어 벽면 앞으로 올라간다
3. **깊이가 읽히는가** — 뒤쪽(z=+2.4) 적이 앞쪽(z=-2.4) 적보다 눈에 띄게 작다
4. **발이 안 뜨는가** — 뒤쪽 캐릭터의 발이 자기 그림자 위에 정확히 붙어 있다
5. **애니가 살아 있는가** — 서 있을 때 미세한 바운스, 점프할 때 세로로 늘어남

4번이 어긋나면 `spriteOffsetY * scale` 곱이 빠진 것이다.
5번이 죽어 있으면 애니 바인딩 경로가 아직 옛것이다 — Step 3의 2번을 다시 돌린다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Editor/AnimationBuilder.cs Assets/Editor/ArtImportBuilder.cs
git add Assets/Scenes/SampleScene.unity Assets/Prefabs Assets/Data/Animation
git commit -m "chore: 애니 바인딩 경로를 View/Sprite로 옮기고 빌더 재실행"
```

---

### Task 7: 머리 위에 붙는 표시 — 오프셋에만 배율

**Files:**
- Modify: `Assets/Scripts/Vfx/ChargeGauge.cs`
- Modify: `Assets/Scripts/Vfx/EnemyStateLabel.cs` (⚠️ 아래 주의)
- Modify: `Assets/Editor/Tests/EnemyStateLabelTests.cs` (⚠️ 아래 주의)
- Test: `Assets/Editor/Tests/ChargeGaugePositionTests.cs` (신규)

**Interfaces:**
- Consumes: `BeltScroll.ScaleAt(float)` (Task 1)
- Produces: `public static Vector3 ChargeGauge.GaugePosition(Vector3 ground, float height, float headOffset)`
  — `EnemyStateLabel.LabelPosition`과 같은 모양으로 맞춘다

**문제:** 두 표시 다 `BeltScroll.ToView(ground, height + headOffset)`으로 머리 위에 붙는다.
뒤에 선 적은 몸이 0.82배로 줄어 머리도 그만큼 내려오는데 `headOffset`이 고정이라
게이지·라벨만 제자리에 남아 붕 뜬다. **위젯 크기는 안 건드린다** — 게이지와 글자는
어느 깊이에서나 같은 크기로 읽혀야 한다. 앵커 높이만 배율을 따라간다.

> ⚠️ **`EnemyStateLabel.cs`와 `EnemyStateLabelTests.cs`는 다른 세션이 지금 쓰고 있는 파일이다.**
> "시작 전 — 작업 트리 정리"의 커밋이 끝난 뒤에 Step 3~5를 한다.
> ChargeGauge 쪽(Step 1~2)은 겹치지 않으므로 먼저 해도 된다.

- [ ] **Step 1: ChargeGauge 위치 계산을 함수로 빼고 테스트를 쓴다**

`Assets/Scripts/Vfx/ChargeGauge.cs` — `Draw` 메서드 위에 추가:

```csharp
        /// <summary>
        /// 게이지가 놓일 자리. 뒤에 선 적은 몸이 줄어 머리도 내려오므로
        /// 오프셋에도 같은 깊이 배율을 먹인다. 게이지 자체의 크기(width)는 안 건드린다 —
        /// 어느 깊이에서나 같은 크기로 읽혀야 한다.
        /// </summary>
        public static Vector3 GaugePosition(Vector3 ground, float height, float headOffset)
            => BeltScroll.ToView(ground, height + headOffset * BeltScroll.ScaleAt(ground.z));
```

`Assets/Editor/Tests/ChargeGaugePositionTests.cs` 신규 생성:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 머리 위 표시는 몸이 줄어든 만큼 같이 내려와야 한다.
    /// 안 그러면 뒤쪽 적의 게이지만 허공에 뜬다.
    /// </summary>
    public class ChargeGaugePositionTests
    {
        [SetUp]
        public void SetUp()
        {
            BeltScroll.DepthToScreen = 0.9f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void TearDown()
        {
            BeltScroll.DepthToScreen = 0.5f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [Test]
        public void AtOrigin_OffsetIsUnscaled()
        {
            Vector3 p = ChargeGauge.GaugePosition(Vector3.zero, height: 0f, headOffset: 1.6f);

            Assert.That(p.y, Is.EqualTo(1.6f).Within(0.0001f));
        }

        /// <summary>z=3에서 배율 0.82. 바닥은 2.7로 접히고 오프셋만 줄어야 한다.</summary>
        [Test]
        public void AtBack_OffsetShrinksButFloorFoldDoesNot()
        {
            var ground = new Vector3(0f, 0f, 3f);

            Vector3 p = ChargeGauge.GaugePosition(ground, height: 0f, headOffset: 1.6f);

            Assert.That(p.y, Is.EqualTo(2.7f + 1.6f * 0.82f).Within(0.001f));
        }

        /// <summary>점프 높이는 배율을 안 받는다 — 논리적인 높이 그대로다.</summary>
        [Test]
        public void JumpHeight_IsNotScaled()
        {
            var ground = new Vector3(0f, 0f, 3f);

            Vector3 low = ChargeGauge.GaugePosition(ground, height: 0f, headOffset: 1.6f);
            Vector3 high = ChargeGauge.GaugePosition(ground, height: 2f, headOffset: 1.6f);

            Assert.That(high.y - low.y, Is.EqualTo(2f).Within(0.0001f));
        }
    }
}
```

Test Runner에서 실행 → 컴파일 에러(`GaugePosition` 없음)를 먼저 확인한 뒤 위 코드를 넣고 3개 PASS를 확인한다.

- [ ] **Step 2: `Draw`가 새 함수를 쓰게 한다**

`Assets/Scripts/Vfx/ChargeGauge.cs`의 `Draw` 안에서 위치 계산 한 줄을 교체:

```csharp
            sr.transform.position = GaugePosition(ground, phys.Height, headOffset);
```

Test Runner에서 `ChargeGaugePositionTests` 재실행 → 3개 PASS.

```bash
git add Assets/Scripts/Vfx/ChargeGauge.cs Assets/Editor/Tests/ChargeGaugePositionTests.cs
git commit -m "fix: 차지 게이지 머리 오프셋에 깊이 배율 적용"
```

- [ ] **Step 3: `EnemyStateLabelTests`의 기대값을 고친다**

⚠️ "시작 전 — 작업 트리 정리"의 커밋이 끝난 뒤에 한다.

`Assets/Editor/Tests/EnemyStateLabelTests.cs` — `SetUp`/`TearDown`에 배율을 고정하고
`LabelFoldsDepthIntoScreenHeight`의 기대값을 바꾼다:

```csharp
        [SetUp]
        public void SetUp()
        {
            // DepthToScreen · DepthScalePerUnit은 static이고 BeltScrollView가 매 프레임 덮어쓴다.
            // 테스트가 값을 고정했다가 원래대로 돌려놓는다.
            saved = BeltScroll.DepthToScreen;
            savedScale = BeltScroll.DepthScalePerUnit;
            BeltScroll.DepthToScreen = 0.9f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void TearDown()
        {
            BeltScroll.DepthToScreen = saved;
            BeltScroll.DepthScalePerUnit = savedScale;
        }
```

필드 선언에 `private float savedScale;`을 더한다.

`LabelFoldsDepthIntoScreenHeight`를 교체:

```csharp
        [Test]
        public void LabelFoldsDepthIntoScreenHeight()
        {
            // 깊이 z=2는 화면 세로 1.8로 접힌다(0.9 배율).
            // 머리 오프셋은 몸이 줄어든 만큼(z=2 → 0.88배) 같이 내려온다.
            Vector3 p = EnemyStateLabel.LabelPosition(new Vector3(5f, 0f, 2f), height: 0f, headOffset: 1.9f);

            Assert.That(p.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(p.y, Is.EqualTo(1.8f + 1.9f * 0.88f).Within(0.001f));
        }
```

나머지 3개(`LabelRisesWithJumpHeight`, `LabelSitsAboveTheHead`, `LabelClearsTheChargeGauge`)는
그대로 통과한다 — 각각 높이 차이, 대소 비교만 보기 때문이다.

- [ ] **Step 4: 실패를 확인한다**

Test Runner에서 `EnemyStateLabelTests` 실행.
예상: `LabelFoldsDepthIntoScreenHeight` FAIL — 기대 3.472, 실제 3.7 (오프셋에 배율이 아직 없다).

- [ ] **Step 5: `LabelPosition`을 고친다**

`Assets/Scripts/Vfx/EnemyStateLabel.cs`:

```csharp
        /// <summary>
        /// 라벨이 놓일 자리.
        ///
        /// 논리 좌표가 아니라 <see cref="BeltScroll.ToView"/>를 거친 <b>그리는 위치</b>다.
        /// 루트 transform은 깊이가 화면 세로로 접히기 전 값(x, 0, z)을 들고 있어서,
        /// 그대로 쓰면 라벨이 발밑 훨씬 아래에 찍힌다.
        ///
        /// 머리 오프셋에는 깊이 배율을 먹인다 — 뒤에 선 적은 몸이 줄어 머리도 내려온다.
        /// 글자 크기는 안 건드린다. 어느 깊이에서나 같게 읽혀야 한다.
        /// </summary>
        public static Vector3 LabelPosition(Vector3 ground, float height, float headOffset)
            => BeltScroll.ToView(ground, height + headOffset * BeltScroll.ScaleAt(ground.z));
```

Test Runner에서 `EnemyStateLabelTests` 실행 → 4개 PASS.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Vfx/EnemyStateLabel.cs Assets/Editor/Tests/EnemyStateLabelTests.cs
git commit -m "fix: 적 상태 라벨 머리 오프셋에 깊이 배율 적용"
```

---

## 완료 기준

- [ ] EditMode 테스트 19개 신규(Task 1: 4, Task 2: 5, Task 4·5: 7, Task 7: 3) + 기존 전부 PASS
- [ ] SampleScene에 `Room/BackWall`, `Room/Horizon`이 있고 화면에 보인다
- [ ] 캐릭터가 `root → View → Sprite` 구조이고 `BeltScrollView.depthRoot`가 배선돼 있다
- [ ] 뒤쪽 적이 앞쪽 적보다 작게 그려진다
- [ ] Idle 바운스 · Jump 스트레치가 살아 있다
- [ ] 뒤쪽 적의 차지 게이지 · 상태 라벨이 머리 위에 붙어 있다 (붕 뜨지 않는다)
- [ ] 논리 좌표 · 콜라이더 · 히트박스는 한 줄도 안 바뀌었다 (`git diff`로 확인)
