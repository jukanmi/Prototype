using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 보스 프리팹이 <b>실제로</b> 가드를 들고 있는지.
    ///
    /// <see cref="BossGuardTests"/>는 <c>Combat.SetGuard</c>로 값을 손수 넣고 규칙을 검증한다 —
    /// 규칙은 늘 통과했는데 정작 씬에 서는 보스는 <c>maxGuard 0</c>이라 슈퍼아머가 없었다.
    /// 프로덕션 코드에 <c>SetGuard</c> 호출자가 하나도 없어서, 프리팹 값이 곧 유일한 원본이다.
    ///
    /// 어떻게 날아갔나: <c>maxGuard</c>의 클래스 기본값이 0이고, 보스 프리팹은
    /// 가드가 없는 <c>Enemy_Melee</c>에서 복제된다. 빌더가 가드를 안 쓰던 동안
    /// 재생성 한 번에 10 → 0으로 떨어졌고, 나머지 가드 수치(회복 · 브레이크 시간)만
    /// 남아 조용히 무의미해졌다. 그래서 여기서 <b>프리팹 파일</b>을 본다.
    /// </summary>
    public class BossGuardPrefabTests
    {
        private const string BossPrefabPath = "Assets/Prefabs/Enemy_Boss.prefab";
        private const string DummyPrefabPath = "Assets/Prefabs/Enemy_Dummy.prefab";

        [Test]
        public void BossPrefab_IsSuperArmoredByDefault()
        {
            Combat boss = LoadCombat(BossPrefabPath);

            Assert.That(boss.HasGuard, Is.True,
                        "보스가 가드를 안 들고 있다 — 평상시 슈퍼아머가 통째로 꺼진다. " +
                        "Prototype ▸ 보스 - 프리팹 + 데이터 만들기 를 다시 돌릴 것");

            Assert.That(SerializedFloat(boss, "maxGuard"),
                        Is.EqualTo(BossPrefabBuilder.MaxGuard).Within(0.01f),
                        "프리팹 값이 빌더 상수와 어긋난다 — 둘 중 하나가 손으로 바뀌었다");
        }

        /// <summary>
        /// 가드 수치가 서로 말이 되는지. 하나만 바뀌면 "몇 대 맞고 깨지나"가 조용히 달라진다.
        /// </summary>
        [Test]
        public void BossGuard_BreaksInTenBasicHits()
        {
            Combat boss = LoadCombat(BossPrefabPath);

            float hits = SerializedFloat(boss, "maxGuard") / SerializedFloat(boss, "defaultGuardDamage");

            Assert.That(hits, Is.EqualTo(10f).Within(0.01f),
                        "평타 열 대에 깨지는 게 설계값이다. maxGuard와 defaultGuardDamage를 같이 옮길 것");
        }

        /// <summary>
        /// 허수아비는 보스에서 몸을 떠 오지만 <b>가드만은 안 물려받는다</b>.
        /// 슈퍼아머가 켜져 있으면 경직 · 넉백 · 공중 콤보가 전부 무시돼 훈련장이 아무것도 못 보여 준다.
        /// </summary>
        [Test]
        public void DummyPrefab_HasNoGuard()
        {
            Combat dummy = LoadCombat(DummyPrefabPath);

            Assert.That(dummy.HasGuard, Is.False,
                        "허수아비에 가드가 켜졌다 — 슈퍼아머라 콤보가 통째로 흘러 훈련장이 무의미해진다");
        }

        /// <summary>무게는 반대로 <b>반드시</b> 물려받아야 한다. 여기서 잰 넉백이 보스전에서 재현되는 근거다.</summary>
        [Test]
        public void DummyPrefab_KeepsBossWeight()
        {
            Physics boss = LoadPrefab(BossPrefabPath).GetComponent<Physics>();
            Physics dummy = LoadPrefab(DummyPrefabPath).GetComponent<Physics>();

            Assert.That(dummy.ImpulseDamping, Is.EqualTo(boss.ImpulseDamping).Within(0.001f),
                        "넉백 감쇠가 다르면 밀리는 거리가 달라진다");
            Assert.That(dummy.Gravity, Is.EqualTo(boss.Gravity).Within(0.001f),
                        "중력이 다르면 체공 시간이 달라진다");
            Assert.That(dummy.FallGravityScale, Is.EqualTo(boss.FallGravityScale).Within(0.001f),
                        "낙하 배율이 다르면 공중 콤보 창이 달라진다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static GameObject LoadPrefab(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(go, Is.Not.Null, $"프리팹이 없다: {path}");
            return go;
        }

        private static Combat LoadCombat(string path)
        {
            var combat = LoadPrefab(path).GetComponent<Combat>();
            Assert.That(combat, Is.Not.Null, $"{path}에 Combat이 없다");
            return combat;
        }

        /// <summary>
        /// 직렬화된 값을 그대로 읽는다. <c>Combat.Guard</c>를 쓰지 않는 이유:
        /// 그 Energy는 첫 접근에 <c>maxGuard</c>로 만들어져 <b>인스턴스에 눌러앉는다</b>.
        /// 프리팹을 이미 한 번 읽어 둔 에디터 세션에서는 파일을 고쳐도 옛 최대치가 그대로 나온다 —
        /// 테스트가 캐시를 검사하게 두면 통과·실패가 세션 순서에 따라 갈린다.
        /// </summary>
        private static float SerializedFloat(Object target, string field)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);

            Assert.That(p, Is.Not.Null, $"{target.name}에 {field} 필드가 없다");
            return p.floatValue;
        }
    }
}
