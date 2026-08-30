using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Prototype.EditorTools;

namespace Prototype.Tests
{
    /// <summary>
    /// <c>BattleInput.prefab</c>의 <b>파티 계층</b>을 본다.
    ///
    /// 여기 있는 대부분은 컴파일러도 다른 테스트도 안 잡아 준다 — 틀리면 씬을 켜 봐야 알고,
    /// 그중 몇 개(레이어 · 컨테이너 배율)는 켜 봐도 증상이 엉뚱한 데서 나온다.
    /// </summary>
    public class PartyPrefabTests
    {
        private static GameObject Host()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(BattleInputBuilder.PrefabPath);
            Assert.That(go, Is.Not.Null,
                $"{BattleInputBuilder.PrefabPath} 를 못 찾았다. " +
                "메뉴 'Prototype ▸ 전투 - 입력 호스트 프리팹 만들기' 를 실행할 것.");
            return go;
        }

        // ── 컨테이너 불변식 ─────────────────────────────

        /// <summary>
        /// 루트가 원점을 벗어나면 <see cref="Prototype.YG.BattleSceneController"/>의 출구 판정이
        /// 월드 x 를 읽으므로 <b>스테이지가 안 넘어간다</b>. 증상이 파티 구조와 전혀 안 닮았다.
        /// </summary>
        [Test]
        public void Root_IsAtOriginWithIdentityTransform()
        {
            GameObject host = Host();

            Assert.That(host.transform.localPosition, Is.EqualTo(Vector3.zero),
                "BattleInput 루트가 원점이 아니다.");
            Assert.That(host.transform.localRotation, Is.EqualTo(Quaternion.identity),
                "BattleInput 루트가 회전해 있다.");
            Assert.That(host.transform.localScale, Is.EqualTo(Vector3.one),
                "BattleInput 루트의 배율이 1이 아니다 — 자식 히트박스가 통째로 커지고 작아진다.");
        }

        [Test]
        public void PartyRoot_IsAtOriginWithIdentityTransform()
        {
            Transform party = Host().transform.Find(BattleInputBuilder.PartyRootName);
            Assert.That(party, Is.Not.Null, $"'{BattleInputBuilder.PartyRootName}' 컨테이너가 없다.");

            Assert.That(party.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(party.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(party.localScale, Is.EqualTo(Vector3.one));
        }

        /// <summary>
        /// 컨테이너는 물리에 <b>일절 개입하지 않는다</b>. 루트에 콜라이더가 붙으면
        /// 아무도 없는 원점에 몸통이 하나 서 있는 셈이 된다.
        /// </summary>
        [Test]
        public void Container_HasNoPhysicsComponents()
        {
            GameObject host = Host();

            Assert.That(host.GetComponent<Rigidbody>(), Is.Null,
                "BattleInput 루트에 Rigidbody 가 붙어 있다.");
            Assert.That(host.GetComponent<Collider>(), Is.Null,
                "BattleInput 루트에 Collider 가 붙어 있다.");

            Transform party = host.transform.Find(BattleInputBuilder.PartyRootName);
            if (party == null) return;

            Assert.That(party.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(party.GetComponent<Collider>(), Is.Null);
        }

        // ── 파티 계층 ───────────────────────────────────

        [Test]
        public void Host_HasPartyAssembler()
        {
            Assert.That(Host().GetComponent<PartyAssembler>(), Is.Not.Null,
                "PartyAssembler 가 없다 — 로드아웃이 아무 데서도 안 읽힌다.");
        }

        [Test]
        public void Party_ContainsOnePlayerAndFourAllySlots()
        {
            GameObject host = Host();

            Assert.That(host.GetComponentsInChildren<Player>(true).Length, Is.EqualTo(1),
                "파티에 Player 가 정확히 하나가 아니다 — 태그 로스터 0번 슬롯이다.");

            Assert.That(host.GetComponentsInChildren<Ally>(true).Length,
                Is.EqualTo(PartyLoadout.MaxMembers),
                $"동료 슬롯이 {PartyLoadout.MaxMembers}칸이 아니다.");
        }

        /// <summary>
        /// 슬롯 배선이 비면 <see cref="PartyAssembler"/>가 조용히 아무것도 안 한다 —
        /// 프리팹 기본값 파티가 그대로 서고, 로드아웃은 무시된다.
        /// </summary>
        [Test]
        public void Assembler_HasEveryReferenceWired()
        {
            var assembler = Host().GetComponent<PartyAssembler>();
            Assert.That(assembler, Is.Not.Null);

            var so = new SerializedObject(assembler);

            Assert.That(so.FindProperty("player").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.player 가 비어 있다.");
            Assert.That(so.FindProperty("partyRoot").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.partyRoot 가 비어 있다.");
            Assert.That(so.FindProperty("swap").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.swap 이 비어 있다 — 시작 자리가 안 넘어가 파티가 원점에서 시작한다.");

            SerializedProperty slots = so.FindProperty("slots");
            Assert.That(slots.arraySize, Is.EqualTo(PartyLoadout.MaxMembers),
                "PartyAssembler.slots 의 칸 수가 다르다.");

            for (int i = 0; i < slots.arraySize; i++)
                Assert.That(slots.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null,
                    $"PartyAssembler.slots[{i}] 가 비어 있다.");
        }

        /// <summary>덱 · 콤보 · 컷인이 전부 여기 안에 있어야 씬에서 배선이 사라진다.</summary>
        [Test]
        public void Host_OwnsCombatManager()
        {
            GameObject host = Host();

            Assert.That(host.GetComponentInChildren<BulletTimeController>(true), Is.Not.Null,
                "BattleInput 안에 BulletTimeController 가 없다 — 덱이 씬 소유로 남아 있다.");
            Assert.That(host.GetComponentInChildren<ComboExecutor>(true), Is.Not.Null,
                "BattleInput 안에 ComboExecutor 가 없다.");
            Assert.That(host.GetComponentInChildren<TargetSelector>(true), Is.Not.Null,
                "BattleInput 안에 TargetSelector 가 없다.");
        }

        /// <summary>
        /// 덱을 짜는 쪽이 파티를 못 찾으면 손패가 통째로 빈다.
        /// 슬롯 넷은 프리팹 안에만 있어 <c>FindAnyObjectByType</c> 폴백으로는 못 잡는 경로가 있다.
        /// </summary>
        [Test]
        public void BulletTime_KnowsPlayerAndSwap()
        {
            var bullet = Host().GetComponentInChildren<BulletTimeController>(true);
            Assert.That(bullet, Is.Not.Null);

            var so = new SerializedObject(bullet);

            Assert.That(so.FindProperty("player").objectReferenceValue, Is.Not.Null,
                "BulletTimeController.player 가 비어 있다 — 덱이 파티 카드를 못 걷는다.");
            Assert.That(so.FindProperty("swap").objectReferenceValue, Is.Not.Null,
                "BulletTimeController.swap 이 비어 있다 — U키가 벤치에 앉은 동료를 못 불러온다.");
        }

        [Test]
        public void TagSwap_KnowsPlayer()
        {
            var swap = Host().GetComponent<TagSwapController>();
            Assert.That(swap, Is.Not.Null);

            Assert.That(new SerializedObject(swap).FindProperty("player").objectReferenceValue,
                Is.Not.Null, "TagSwapController.player 가 비어 있다 — 로스터가 안 만들어진다.");
        }

        // ── 몸 ──────────────────────────────────────────

        [Test]
        public void PartyBodies_AreAtLocalOrigin()
        {
            // 자리는 파티가 하나만 쓴다(TagSwapController.partySeat). 슬롯마다 좌표를 두면
            // 프리팹과 실제 자리가 어긋나 "교대할 때만 순간이동" 처럼 보인다.
            foreach (Entity e in Host().GetComponentsInChildren<Entity>(true))
                Assert.That(e.transform.localPosition, Is.EqualTo(Vector3.zero),
                    $"{e.name} 이(가) 프리팹 안에서 원점이 아니다.");
        }

        /// <summary>
        /// 레이어가 틀리면 <see cref="Attack"/>이 충돌 매트릭스를 그대로 읽어
        /// <b>아군이 적을 통과한다</b> — 스킬이 나가고 이펙트도 뜨는데 데미지만 없다.
        /// 런타임에는 <see cref="AllyLayers"/>가 보장하지만, 프리팹이 맞으면 한 겹 더 안전하다.
        /// </summary>
        [Test]
        public void PartyBodies_UseAllyLayers()
        {
            int hurt = LayerMask.NameToLayer(AllyLayers.HurtboxLayer);
            int hit = LayerMask.NameToLayer(AllyLayers.HitboxLayer);

            Assert.That(hurt, Is.GreaterThanOrEqualTo(0),
                $"프로젝트에 '{AllyLayers.HurtboxLayer}' 레이어가 없다.");
            Assert.That(hit, Is.GreaterThanOrEqualTo(0),
                $"프로젝트에 '{AllyLayers.HitboxLayer}' 레이어가 없다.");

            foreach (Entity e in Host().GetComponentsInChildren<Entity>(true))
            {
                Assert.That(e.gameObject.layer, Is.EqualTo(hurt),
                    $"{e.name} 의 몸통 레이어가 {AllyLayers.HurtboxLayer} 가 아니다.");

                foreach (Attack a in e.GetComponentsInChildren<Attack>(true))
                    Assert.That(a.gameObject.layer, Is.EqualTo(hit),
                        $"{e.name}/{a.name} 의 레이어가 {AllyLayers.HitboxLayer} 가 아니다.");
            }
        }
    }
}
