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

        /// <summary>
        /// <b>몸이 구워져 있으면 안 된다.</b> 동료마다 제 프리팹을 쓰게 되면서
        /// 여기 남은 몸은 전부 유령이 된다 — 로드아웃이 만든 진짜 파티와 나란히 서서
        /// 동료가 다섯 명이 되고, 덱이 20장이 되고, 교대 순환에 이름 없는 칸이 낀다.
        /// </summary>
        [Test]
        public void Party_IsAnEmptyContainer()
        {
            GameObject host = Host();

            Transform party = host.transform.Find(BattleInputBuilder.PartyRootName);
            Assert.That(party, Is.Not.Null, $"'{BattleInputBuilder.PartyRootName}' 컨테이너가 없다.");

            Assert.That(host.GetComponentsInChildren<Player>(true), Is.Empty,
                "BattleInput 안에 Player 가 구워져 있다 — 몸은 PartyAssembler 가 런타임에 만든다. " +
                "'Prototype ▸ 전투 - 입력 호스트 프리팹 만들기'를 다시 돌릴 것.");

            Assert.That(host.GetComponentsInChildren<Ally>(true), Is.Empty,
                "BattleInput 안에 Ally 가 구워져 있다 — 로드아웃이 만든 파티와 겹쳐 " +
                "동료가 다섯 명이 되고 덱이 20장이 된다.");
        }

        /// <summary>
        /// 배선이 비면 <see cref="PartyAssembler"/>가 조용히 아무것도 안 한다 —
        /// 몸이 하나도 안 서고, 화면에는 "아무도 안 나온다"로만 보인다.
        ///
        /// <b>기본 프리팹 둘이 핵심</b>이다. 표(<see cref="PartyMemberData.prefab"/>)가
        /// 비어 있는 것은 정상이고, 그때 여기로 떨어진다.
        /// </summary>
        [Test]
        public void Assembler_HasEveryReferenceWired()
        {
            var assembler = Host().GetComponent<PartyAssembler>();
            Assert.That(assembler, Is.Not.Null);

            var so = new SerializedObject(assembler);

            Assert.That(so.FindProperty("defaultPlayerPrefab").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.defaultPlayerPrefab 이 비어 있다 — " +
                "PlayerData 에 프리팹이 없으면 주인공이 아예 안 선다.");
            Assert.That(so.FindProperty("defaultAllyPrefab").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.defaultAllyPrefab 이 비어 있다 — " +
                "PartyMemberData 에 프리팹이 없는 동료가 통째로 빠진다.");
            Assert.That(so.FindProperty("partyRoot").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.partyRoot 가 비어 있다.");
            Assert.That(so.FindProperty("swap").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.swap 이 비어 있다 — 시작 자리가 안 넘어가 파티가 원점에서 시작한다.");
            Assert.That(so.FindProperty("bulletTime").objectReferenceValue, Is.Not.Null,
                "PartyAssembler.bulletTime 이 비어 있다 — 덱이 파티 카드를 못 걷어 손패가 빈다.");
        }

        /// <summary>
        /// 기본 프리팹의 루트에 컴포넌트가 없으면 <see cref="PartyAssembler"/>가 만든 몸을
        /// 그 자리에서 도로 버린다. 빌더가 <c>GetComponent</c>로 꽂으므로 보통은 맞지만,
        /// 손으로 다른 프리팹을 꽂았을 때 잡히는 건 여기뿐이다.
        /// </summary>
        [Test]
        public void Assembler_DefaultPrefabsCarryTheirComponents()
        {
            var so = new SerializedObject(Host().GetComponent<PartyAssembler>());

            Assert.That(so.FindProperty("defaultPlayerPrefab").objectReferenceValue,
                Is.InstanceOf<Player>(), "defaultPlayerPrefab 이 Player 가 아니다.");
            Assert.That(so.FindProperty("defaultAllyPrefab").objectReferenceValue,
                Is.InstanceOf<Ally>(), "defaultAllyPrefab 이 Ally 가 아니다.");
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
        /// <c>player</c> 는 이제 여기서 검사하지 않는다 — 주인공이 런타임 생성물이라
        /// 프리팹에 꽂을 인스턴스가 없고, <see cref="PartyAssembler"/>가
        /// <c>Awake</c>(-200)에서 <c>SetHero</c>로 밀어 넣는다.
        /// 그 주입이 성립하는지는 <see cref="Assembler_HasEveryReferenceWired"/>가 본다.
        /// </summary>
        [Test]
        public void BulletTime_KnowsSwap()
        {
            var bullet = Host().GetComponentInChildren<BulletTimeController>(true);
            Assert.That(bullet, Is.Not.Null);

            Assert.That(new SerializedObject(bullet).FindProperty("swap").objectReferenceValue,
                Is.Not.Null,
                "BulletTimeController.swap 이 비어 있다 — U키가 벤치에 앉은 동료를 못 불러온다.");
        }

        // ── 카메라 앵커 ─────────────────────────────────

        /// <summary>
        /// 앵커는 <c>Party</c>의 <b>형제</b>여야 한다. Party 안에 넣으면
        /// <c>PartyRoot_IsAtOriginWithIdentityTransform</c>의 컨테이너 불변식에 걸린다 —
        /// 앵커는 움직이는 것이 일이기 때문이다.
        /// </summary>
        [Test]
        public void Host_HasCameraAnchorOutsideParty()
        {
            GameObject host = Host();

            var anchor = host.GetComponentInChildren<CameraAnchor>(true);
            Assert.That(anchor, Is.Not.Null,
                "CameraAnchor 가 없다 — 카메라가 몸 트랜스폼을 직접 물게 된다.");

            Assert.That(anchor.transform.parent, Is.EqualTo(host.transform),
                "CameraAnchor 가 BattleInput 루트의 직계 자식이 아니다.");

            Transform party = host.transform.Find(BattleInputBuilder.PartyRootName);
            Assert.That(anchor.transform.IsChildOf(party), Is.False,
                "CameraAnchor 가 Party 컨테이너 안에 있다 — 그 노드는 원점에 고정이다.");
        }

        /// <summary>
        /// 배선이 비면 <see cref="TagSwapController"/>가 폴백으로 몸 트랜스폼을 직접 꽂는다.
        /// 게임은 돌지만 앵커를 만든 이유가 통째로 사라지고, 아무 에러도 안 난다.
        /// </summary>
        [Test]
        public void TagSwap_KnowsCameraAnchor()
        {
            var swap = Host().GetComponent<TagSwapController>();

            Assert.That(new SerializedObject(swap).FindProperty("cameraAnchor").objectReferenceValue,
                Is.Not.Null, "TagSwapController.cameraAnchor 가 비어 있다.");
        }

        // ── 파티 체력 HUD ───────────────────────────────

        /// <summary>
        /// 로스터의 주인과 <b>같은 오브젝트</b>에 있어야 한다. 자식이나 다른 오브젝트에 두면
        /// 찾는 코드가 필요해지고, 그 순간 "가끔 HUD가 안 뜬다"가 생길 자리가 열린다.
        /// </summary>
        [Test]
        public void Host_HasPartyHealthHud()
        {
            GameObject host = Host();

            var hud = host.GetComponent<PartyHealthHUD>();
            Assert.That(hud, Is.Not.Null,
                "PartyHealthHUD 가 없다 — 파티 체력이 화면에 안 뜬다.");

            Assert.That(new SerializedObject(hud).FindProperty("swap").objectReferenceValue,
                Is.Not.Null, "PartyHealthHUD.swap 이 비어 있다 — 로스터를 못 읽는다.");
        }

    }
}
