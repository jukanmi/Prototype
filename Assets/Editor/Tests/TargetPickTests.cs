using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 자동 조준이 <b>가장 가까운 적 / 가장 먼 적</b> 둘로만 갈리는지.
    ///
    /// 반경 훑기나 부채꼴 검사를 쓰면 유저는 카드를 놓기 전에 어디로 나갈지 알 수 없다.
    /// 규칙이 둘뿐이라는 게 이 시스템의 약속이고, 여기가 그 약속을 지키는 자리다.
    /// </summary>
    public class TargetPickTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp() => BattleRegistry.Clear();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
            BattleRegistry.Clear();
        }

        [Test]
        public void Nearest_PicksClosest()
        {
            Entity near = Enemy("near", new Vector3(2f, 0f, 0f));
            Enemy("far", new Vector3(20f, 0f, 0f));

            Assert.That(BattleRegistry.PickEnemy(Vector3.zero, TargetPick.Nearest),
                        Is.SameAs(near));
        }

        [Test]
        public void Farthest_PicksMostDistant()
        {
            Enemy("near", new Vector3(2f, 0f, 0f));
            Entity far = Enemy("far", new Vector3(20f, 0f, 0f));

            Assert.That(BattleRegistry.PickEnemy(Vector3.zero, TargetPick.Farthest),
                        Is.SameAs(far),
                        "밀치기는 벽까지 밀 거리가 필요하다 — 코앞의 적을 고르면 한 뼘 가서 끝난다");
        }

        [Test]
        public void SingleEnemy_BothRulesAgree()
        {
            Entity only = Enemy("only", new Vector3(5f, 0f, 0f));

            Assert.That(BattleRegistry.PickEnemy(Vector3.zero, TargetPick.Nearest), Is.SameAs(only));
            Assert.That(BattleRegistry.PickEnemy(Vector3.zero, TargetPick.Farthest), Is.SameAs(only),
                        "적이 하나면 가장 가까운 적이 곧 가장 먼 적이다");
        }

        [Test]
        public void NoEnemy_ReturnsNull()
        {
            Assert.That(BattleRegistry.PickEnemy(Vector3.zero, TargetPick.Nearest), Is.Null);
            Assert.That(BattleRegistry.PickEnemy(Vector3.zero, TargetPick.Farthest), Is.Null);
        }

        /// <summary>
        /// 훈련장 견본 손패가 기대는 상한. 손패는 4칸이고 그 이상은 들어가지 않는다 —
        /// 넘치면 실행 큐가 4장을 넘겨 한 싸이클의 정의가 무너진다.
        /// </summary>
        [Test]
        public void Hand_Add_StopsAtFour()
        {
            var hand = new Hand();
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();

            try
            {
                for (int i = 0; i < Hand.Size; i++)
                    Assert.That(hand.Add(new ComboCard(skill)), Is.True, $"{i + 1}번째 카드는 들어가야 한다");

                Assert.That(hand.Add(new ComboCard(skill)), Is.False, "5번째는 거부한다");
                Assert.That(hand.Count, Is.EqualTo(Hand.Size));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void Hand_Add_RejectsNull()
        {
            var hand = new Hand();

            Assert.That(hand.Add(null), Is.False);
            Assert.That(hand.Count, Is.Zero);
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>
        /// 등록만 된 최소 적. <see cref="Enemy"/>가 아니라 <see cref="Entity"/>를 쓰는 이유는
        /// Enemy.Awake가 EnemyData · 상태 색까지 끌어와 EditMode에서 부담이 크기 때문이다.
        /// 레지스트리는 Entity만 알면 된다.
        /// </summary>
        private Entity Enemy(string name, Vector3 position)
        {
            var go = new GameObject(name, typeof(Rigidbody), typeof(Physics), typeof(Combat), typeof(Entity));
            go.transform.position = position;
            spawned.Add(go);

            var entity = go.GetComponent<Entity>();
            BattleRegistry.RegisterEnemy(entity);
            return entity;
        }
    }
}
