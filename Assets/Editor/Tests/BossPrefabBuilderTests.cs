using System.Collections.Generic;
using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 보스 애셋이 실제로 쓸 수 있는 상태로 나오는지 검증한다.
    /// 프리팹 배선은 컴파일로 안 잡히는 실수가 가장 많이 나는 곳이다.
    /// </summary>
    public class BossPrefabBuilderTests
    {
        private const string PrefabPath = "Assets/Prefabs/Enemy_Boss.prefab";
        private const string DataPath = "Assets/Data/Enemy/Enemy_Boss.asset";
        private const string BrainPath = "Assets/Data/Enemy/Brain_Boss.asset";
        private const string ControllerPath = "Assets/Data/Animation/BossAnimator.controller";
        private const string BasePrefabPath = "Assets/Prefabs/Enemy_Melee.prefab";
        private const string SheetFolder = "Assets/Art/Character/BossWarrior";

        [OneTimeSetUp]
        public void BuildOnce()
        {
            BossArtImportBuilder.BuildAll();
            BossPrefabBuilder.Build();
        }

        private static GameObject Prefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(go, Is.Not.Null, $"{PrefabPath} 가 생성되지 않았다");
            return go;
        }

        // ── 패턴 표 자체의 규약 ──────────────────────────
        // 아트 빌더와 프리팹 빌더가 이 표 하나를 나눠 읽는다. 표가 깨지면 둘 다 조용히 어긋난다.

        [Test]
        public void Table_EveryPatternHasPositiveDurations()
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                Assert.That(e.active, Is.GreaterThan(0f), $"{e.label}: 발동 길이가 0이면 클립 fps를 나눌 수 없다");
                Assert.That(e.telegraph, Is.GreaterThan(0f), $"{e.label}: 예고가 없으면 피할 방법이 없다");
                Assert.That(e.recovery, Is.GreaterThan(0f), $"{e.label}: 후딜이 없으면 반격할 창이 없다");
                Assert.That(e.ActiveFps, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Table_EveryPatternHitsAtLeastOnce()
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                Assert.That(e.hitCount, Is.GreaterThanOrEqualTo(1), $"{e.label}: 타격 횟수");
                Assert.That(e.hitDuration, Is.GreaterThan(0f), $"{e.label}: 판정 지속");
                Assert.That(e.damageScale, Is.GreaterThan(0f), $"{e.label}: 데미지 배율");
            }
        }

        /// <summary>연타는 마지막 타격까지 발동 구간 안에 들어와야 한다.</summary>
        [Test]
        public void Table_MultiHitPatternsFitInsideActiveWindow()
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                if (e.hitCount <= 1) continue;

                float last = BossPatternAction.HitTime(e.active, e.hitCount - 1, e.hitCount);
                Assert.That(last + e.hitDuration, Is.LessThanOrEqualTo(e.active + 0.0001f),
                            $"{e.label}: 마지막 타격이 발동 구간을 넘어간다");
            }
        }

        [Test]
        public void Table_EveryRuleHasUsableRange()
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                Assert.That(e.maxRange, Is.GreaterThan(e.minRange), $"{e.label}: 사거리 구간이 비어 있다");
                Assert.That(e.cooldown, Is.GreaterThan(0f), $"{e.label}: 쿨이 0이면 매 프레임 다시 나간다");
                Assert.That(e.maxHealthRatio, Is.InRange(0f, 1f), $"{e.label}: 체력 조건");
            }
        }

        /// <summary>
        /// 전진하는 패턴이 접촉으로 안 끊기면 벽에 처박은 채로 발동 시간 내내 밀고 있는다.
        /// </summary>
        [Test]
        public void Table_AdvancingPatternsCancelOnContact()
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
                if (e.advanceSpeed > 0f)
                    Assert.That(e.cancelOnContact, Is.True, $"{e.label}: 전진하는데 접촉 취소가 꺼져 있다");
        }

        /// <summary>1회성 버프 패턴은 되돌리는 경로가 없다. 조건 없이 반복되면 수치가 발산한다.</summary>
        [Test]
        public void Table_BuffPatternsAreOnceAndGated()
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                bool buffs = e.attackPowerScale > 1f || e.moveSpeedScale > 1f
                             || (e.attackIntervalScale > 0f && e.attackIntervalScale < 1f);
                if (!buffs) continue;

                Assert.That(e.once, Is.True, $"{e.label}: 버프 패턴인데 1회성이 아니다");
                Assert.That(e.maxHealthRatio, Is.GreaterThan(0f), $"{e.label}: 버프 패턴에 체력 조건이 없다");
            }
        }

        [Test]
        public void Table_AnimatorStateNamesAreUnique()
        {
            var seen = new HashSet<string>();

            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                Assert.That(seen.Add(e.state), Is.True, $"발동 상태 이름 중복: {e.state}");
                Assert.That(seen.Add(e.windupState), Is.True, $"예고 상태 이름 중복: {e.windupState}");
            }
        }

        [Test]
        public void Table_EverySheetExists()
        {
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>($"{SheetFolder}/{e.sheet}.png"),
                            Is.Not.Null, $"{e.label}: 시트 {e.sheet}.png 가 없다");
        }

        // ── 슬라이스 · 피벗 ─────────────────────────────

        /// <summary>
        /// 시트 안 캐릭터는 셀 바닥에 붙어 있지 않다. 피벗을 (0.5, 0)으로 두면
        /// BeltScrollView가 그 여백까지 바닥으로 쳐서 보스가 공중에 뜬 채 걸어 다닌다.
        /// </summary>
        [Test]
        public void Sprites_PivotSitsAtTheFeet_NotCellBottom()
        {
            Sprite first = FirstSprite($"{SheetFolder}/Idle.png");

            Assert.That(first, Is.Not.Null, "Idle.png 슬라이스 결과가 없다");
            Assert.That(first.pivot.y, Is.GreaterThan(1f), "피벗이 셀 바닥에 붙어 있다 — 보스가 뜬다");
            Assert.That(first.pivot.y / first.rect.height,
                        Is.EqualTo(BossArtImportBuilder.FootPivotY).Within(0.001f));
            Assert.That(first.pivot.x / first.rect.width, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Sprites_UseBossPixelsPerUnit()
        {
            Sprite first = FirstSprite($"{SheetFolder}/Idle.png");

            Assert.That(first.pixelsPerUnit, Is.EqualTo(BossArtImportBuilder.PixelsPerUnit).Within(0.001f));
        }

        /// <summary>보스가 동료만 하면 보스로 안 읽힌다. 동료는 약 1.36유닛이다.</summary>
        [Test]
        public void Boss_IsTallerThanAnAlly()
        {
            Assert.That(BossArtImportBuilder.StandingHeight, Is.GreaterThan(1.8f));
        }

        private static Sprite FirstSprite(string sheetPath)
        {
            Sprite best = null;
            int bestIndex = int.MaxValue;

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (!(o is Sprite s)) continue;

                int at = s.name.LastIndexOf('_');
                int index = at >= 0 && int.TryParse(s.name.Substring(at + 1), out int n) ? n : 0;
                if (index >= bestIndex) continue;

                bestIndex = index;
                best = s;
            }

            return best;
        }

        // ── 애니메이터 ──────────────────────────────────

        [Test]
        public void Controller_HasEveryStateTheCodeAsksFor()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, "BossAnimator.controller 가 없다");

            var states = new HashSet<string>();
            foreach (ChildAnimatorState s in controller.layers[0].stateMachine.states)
                states.Add(s.state.name);

            // EntityAnimator가 상태 클래스 이름으로 찾는 것들.
            foreach (string required in new[] { "Idle", "Move", "Attack", "Hit", "Dead" })
                Assert.That(states, Contains.Item(required), $"상태 '{required}' 가 없다");

            // BossPatternAction이 이름으로 재생하는 것들.
            foreach (BossPatternTable.Entry e in BossPatternTable.All)
            {
                Assert.That(states, Contains.Item(e.state), $"{e.label}: 발동 상태가 없다");
                Assert.That(states, Contains.Item(e.windupState), $"{e.label}: 예고 상태가 없다");
            }
        }

        /// <summary>발동 클립이 발동 구간보다 길면 칼이 다 나가기 전에 판정이 끝나 있다.</summary>
        [Test]
        public void Controller_ActiveClipMatchesActiveWindow()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            foreach (ChildAnimatorState s in controller.layers[0].stateMachine.states)
            {
                BossPatternTable.Entry? match = null;
                foreach (BossPatternTable.Entry e in BossPatternTable.All)
                    if (e.state == s.state.name) match = e;

                if (match == null) continue;

                var clip = s.state.motion as AnimationClip;
                Assert.That(clip, Is.Not.Null, $"{s.state.name}: 클립이 안 물려 있다");
                Assert.That(clip.length, Is.EqualTo(match.Value.active).Within(0.02f),
                            $"{s.state.name}: 클립 길이와 발동 길이가 다르다");
            }
        }

        // ── 프리팹 ──────────────────────────────────────

        [Test]
        public void Prefab_HasCoreComponents()
        {
            GameObject go = Prefab();

            Assert.That(go.GetComponent<Enemy>(), Is.Not.Null, "Enemy 없음");
            Assert.That(go.GetComponent<EnemyControl>(), Is.Not.Null, "EnemyControl 없음");
            Assert.That(go.GetComponent<Physics>(), Is.Not.Null, "Physics 없음");
            Assert.That(go.GetComponent<Combat>(), Is.Not.Null, "Combat 없음");
            Assert.That(go.GetComponent<BossPatternAction>(), Is.Not.Null, "패턴 실행기 없음");
        }

        /// <summary>한 Entity에 특수 실행기는 하나뿐이다. 둘이면 인덱스가 어느 쪽 것인지 알 수 없다.</summary>
        [Test]
        public void Prefab_HasExactlyOneSpecialAction()
        {
            GameObject go = Prefab();

            Assert.That(go.GetComponent<EnemyChargeAction>(), Is.Null, "돌진 실행기가 남아 있다");
            Assert.That(go.GetComponents<IEnemySpecialAction>().Length, Is.EqualTo(1));
        }

        [Test]
        public void Prefab_HasNoMissingScript()
        {
            foreach (Component c in Prefab().GetComponentsInChildren<Component>(true))
                Assert.That(c, Is.Not.Null, "missing script가 있다");
        }

        [Test]
        public void Prefab_PatternCountMatchesTable()
        {
            var action = Prefab().GetComponent<BossPatternAction>();

            Assert.That(action.Count, Is.EqualTo(BossPatternTable.All.Length));
        }

        /// <summary>히트박스가 안 물리면 평타 히트박스로 떨어져 광역기가 근접기가 된다.</summary>
        [Test]
        public void Prefab_EveryPatternHasItsOwnHitboxWired()
        {
            BossPattern[] patterns = Prefab().GetComponent<BossPatternAction>().Patterns;

            for (int i = 0; i < patterns.Length; i++)
            {
                Assert.That(patterns[i].hitbox, Is.Not.Null, $"{patterns[i].label}: 히트박스 미배선");
                Assert.That(patterns[i].hitbox.GetComponent<Collider>(), Is.Not.Null);
            }
        }

        [Test]
        public void Prefab_WidePatternsUseTheWideHitbox()
        {
            GameObject go = Prefab();
            BossPattern[] patterns = go.GetComponent<BossPatternAction>().Patterns;
            Attack basic = go.GetComponent<Enemy>().BasicAttack;

            for (int i = 0; i < patterns.Length; i++)
            {
                bool wide = BossPatternTable.All[i].wideHitbox;
                bool usesBasic = patterns[i].hitbox == basic;

                Assert.That(usesBasic, Is.EqualTo(!wide),
                            $"{patterns[i].label}: 광역 여부와 히트박스가 안 맞는다");
            }
        }

        [Test]
        public void Prefab_HasSeparateWideHitbox()
        {
            GameObject go = Prefab();
            Transform wide = go.transform.Find(BossPrefabBuilder.WideHitboxName);
            Attack basic = go.GetComponent<Enemy>().BasicAttack;

            Assert.That(wide, Is.Not.Null, "광역 히트박스 자식이 없다");
            Assert.That(wide.gameObject, Is.Not.SameAs(basic.gameObject));
            Assert.That(wide.GetComponent<Attack>(), Is.Not.Null);

            Vector3 wideSize = wide.GetComponent<BoxCollider>().size;
            Vector3 basicSize = basic.GetComponent<BoxCollider>().size;
            Assert.That(wideSize.x, Is.GreaterThan(basicSize.x), "광역이 근접보다 좁다");
        }

        // ── 지오메트리 규약 ─────────────────────────────
        // Physics.Apply가 LookRotation(Facing)으로 루트를 돌린다. 정면은 로컬 +Z다.

        [Test]
        public void Prefab_PutsHitboxesInFront()
        {
            GameObject go = Prefab();

            foreach (Attack a in go.GetComponentsInChildren<Attack>(true))
            {
                Transform t = a.transform;
                Assert.That(t.localPosition.z, Is.GreaterThan(0.5f), $"{t.name}: 정면(+Z)에 있어야 한다");
                Assert.That(Mathf.Abs(t.localPosition.x), Is.LessThan(0.01f),
                            $"{t.name}: X로 밀면 회전 후 깊이축으로 빠진다");
            }
        }

        [Test]
        public void Prefab_StandsOnGround()
        {
            var capsule = Prefab().GetComponent<CapsuleCollider>();

            Assert.That(capsule, Is.Not.Null);
            Assert.That(capsule.center.y, Is.EqualTo(capsule.height * 0.5f).Within(0.001f),
                        "발이 바닥(y=0)에 닿아야 한다");
            Assert.That(capsule.height, Is.EqualTo(BossArtImportBuilder.StandingHeight).Within(0.01f));
        }

        /// <summary>루트가 Y축으로 도는데 스프라이트가 붙어 있으면 옆면이 보여 사라진다.</summary>
        [Test]
        public void Prefab_KeepsSpriteOffTheRotatingRoot()
        {
            GameObject go = Prefab();

            Assert.That(go.GetComponent<SpriteRenderer>(), Is.Null);
            Assert.That(go.transform.Find("View/Sprite"), Is.Not.Null);
        }

        /// <summary>기준 프리팹의 캡슐 메시를 안 걷어내면 스프라이트 안에 회색 캡슐이 겹쳐 보인다.</summary>
        [Test]
        public void Prefab_HasNoPlaceholderMesh()
        {
            GameObject go = Prefab();

            Assert.That(go.transform.Find("Body"), Is.Null, "플레이스홀더 몸통이 남아 있다");
            Assert.That(go.transform.Find("FacingMarker"), Is.Null);
        }

        [Test]
        public void Prefab_SpriteIsUntintedAndFlips()
        {
            GameObject go = Prefab();
            var sr = go.transform.Find("View/Sprite").GetComponent<SpriteRenderer>();
            var belt = go.GetComponent<BeltScrollView>();

            Assert.That(sr.sprite, Is.Not.Null, "첫 프레임이 안 꽂혀 있다");
            Assert.That(sr.color, Is.EqualTo(Color.white), "기준 프리팹의 변종색이 남아 시트가 물든다");

            var so = new SerializedObject(belt);
            Assert.That(so.FindProperty("flipToFacing").boolValue, Is.True, "옆에서 본 시트인데 반전이 꺼져 있다");
            Assert.That(so.FindProperty("spriteOffsetY").floatValue, Is.EqualTo(0f).Within(0.001f),
                        "피벗이 발밑이라 더 올리면 뜬다");
        }

        [Test]
        public void Prefab_UsesBossAnimator()
        {
            GameObject go = Prefab();
            var animator = go.GetComponent<Animator>();

            Assert.That(animator, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), Is.EqualTo(ControllerPath));
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                        "컬링되면 화면 밖에서 상태가 안 돌아 판정과 어긋난다");
            Assert.That(go.GetComponent<EntityAnimator>(), Is.Not.Null);
        }

        // ── 데이터 · 브레인 ─────────────────────────────

        [Test]
        public void Data_IsWiredToBrainAndPrefab()
        {
            EnemyData data = Prefab().GetComponent<Enemy>().Data;

            Assert.That(data, Is.Not.Null, "EnemyData 미배정");
            Assert.That(data.brain, Is.TypeOf<BossBrainAsset>());
            Assert.That(data.prefab, Is.Not.Null, "데이터 → 프리팹 역참조가 없다");
            Assert.That(Prefab().GetComponent<EnemyControl>().Brain, Is.Not.Null, "폴백 브레인 미배정");
        }

        /// <summary>일반 적보다 확실히 단단해야 보스로 읽힌다. 돌진 멧돼지가 60이다.</summary>
        [Test]
        public void Data_IsBossGrade()
        {
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);

            Assert.That(data.hp, Is.GreaterThan(200f));
            Assert.That(data.atk, Is.GreaterThan(7f));
            Assert.That(data.basicProjectile, Is.Null, "보스는 근접이다");
        }

        /// <summary>
        /// 규칙의 index가 패턴 배열 밖을 가리키면 EnemyControl이 경고만 찍고 아무것도 안 한다 —
        /// 보스가 평타만 치는 상태로 조용히 굴러간다.
        /// </summary>
        [Test]
        public void Brain_RulesPointAtRealPatterns()
        {
            var brain = AssetDatabase.LoadAssetAtPath<BossBrainAsset>(BrainPath);
            int count = Prefab().GetComponent<BossPatternAction>().Count;

            Assert.That(brain, Is.Not.Null);
            Assert.That(brain.patterns.Length, Is.EqualTo(BossPatternTable.All.Length));

            foreach (BossBrainAsset.PatternRule r in brain.patterns)
                Assert.That(r.index, Is.InRange(0, count - 1), $"{r.label}: 없는 패턴을 가리킨다");
        }

        [Test]
        public void Brain_CoversEveryPatternExactlyOnce()
        {
            var brain = AssetDatabase.LoadAssetAtPath<BossBrainAsset>(BrainPath);
            var seen = new HashSet<int>();

            foreach (BossBrainAsset.PatternRule r in brain.patterns)
                Assert.That(seen.Add(r.index), Is.True, $"패턴 {r.index}를 가리키는 규칙이 둘 이상이다");

            Assert.That(seen.Count, Is.EqualTo(BossPatternTable.All.Length), "안 쓰이는 패턴이 있다");
        }

        /// <summary>브레인이 못 고르는 거리 구간이 있으면 보스가 그 자리에서 평타만 친다.</summary>
        [Test]
        public void Brain_CoversTheWholeApproachRange()
        {
            var brain = AssetDatabase.LoadAssetAtPath<BossBrainAsset>(BrainPath);

            for (float d = 0f; d <= 9f; d += 0.5f)
            {
                bool covered = false;
                foreach (BossBrainAsset.PatternRule r in brain.patterns)
                    if (d >= r.minRange && d <= r.maxRange) covered = true;

                Assert.That(covered, Is.True, $"거리 {d}에서 쓸 수 있는 패턴이 하나도 없다");
            }
        }

        // ── 안전장치 ────────────────────────────────────

        /// <summary>기준 프리팹을 갈아엎으면 SampleScene의 참조가 통째로 날아간다.</summary>
        [Test]
        public void BasePrefab_IsLeftAlone()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);

            Assert.That(basePrefab, Is.Not.Null);
            Assert.That(basePrefab.name, Is.EqualTo("Enemy_Melee"));
            Assert.That(basePrefab.GetComponent<BossPatternAction>(), Is.Null, "기준 프리팹에 보스 패턴이 붙었다");
        }

        /// <summary>두 번 돌려도 경로·GUID가 그대로여야 한다. 아니면 씬 참조가 매번 끊긴다.</summary>
        [Test]
        public void Build_IsIdempotent()
        {
            string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
            string dataGuid = AssetDatabase.AssetPathToGUID(DataPath);
            string brainGuid = AssetDatabase.AssetPathToGUID(BrainPath);
            int dataCount = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemy" }).Length;

            BossPrefabBuilder.Build();

            Assert.That(AssetDatabase.AssetPathToGUID(PrefabPath), Is.EqualTo(prefabGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(DataPath), Is.EqualTo(dataGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(BrainPath), Is.EqualTo(brainGuid));
            Assert.That(AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemy" }).Length,
                        Is.EqualTo(dataCount), "재실행이 데이터 애셋을 늘렸다");
        }
    }
}
