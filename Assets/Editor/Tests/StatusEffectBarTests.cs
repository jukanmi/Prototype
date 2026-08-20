using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 무엇을 그릴지(<see cref="StatusEffectVisuals.Collect"/>)와 어디에 그릴지
    /// (<see cref="StatusEffectBar.RowPosition"/>)를 검증한다.
    ///
    /// 순회 · 풀링은 BattleRegistry에 의존하는데 그 목록은 Start에서 채워지고
    /// EditMode는 Start를 돌리지 않는다 — EnemyStateLabelTests와 같은 이유로 뺀다.
    /// </summary>
    public class StatusEffectBarTests
    {
        // StatusEffectBar의 기본값과 같은 값. 인스펙터 값이 아니라 이 배치를 검증한다.
        private const float HeadOffset = 2.25f;
        private const float RowStep = 0.23f;   // rowHeight 0.18 + rowGap 0.05
        private const float Width = 1.3f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<StatusView> rows = new List<StatusView>();

        private float savedDepth;
        private float savedShear;
        private float savedScale;

        [SetUp]
        public void SetUp()
        {
            // 전부 static이고 BeltScrollView가 매 프레임 덮어쓴다.
            // 씬 빌더와 같은 값으로 고정했다가 원래대로 돌려놓는다.
            savedDepth = BeltScroll.DepthToScreen;
            savedShear = BeltScroll.DepthToScreenX;
            savedScale = BeltScroll.DepthScalePerUnit;
            BeltScroll.DepthToScreen = 0.9f;
            BeltScroll.DepthToScreenX = 0.45f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void TearDown()
        {
            BeltScroll.DepthToScreen = savedDepth;
            BeltScroll.DepthToScreenX = savedShear;
            BeltScroll.DepthScalePerUnit = savedScale;

            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 배치 ────────────────────────────────────────

        /// <summary>머리 위 글자(1.9)와 겹치면 둘 다 못 읽는다.</summary>
        [Test]
        public void FirstRowClearsTheStateLabel()
        {
            Vector3 ground = Vector3.zero;

            Vector3 label = EnemyStateLabel.LabelPosition(ground, height: 0f, headOffset: 1.9f);
            Vector3 row = StatusEffectBar.RowPosition(ground, 0f, HeadOffset, 0, RowStep, Width);

            Assert.That(row.y, Is.GreaterThan(label.y));
        }

        [Test]
        public void RowsStackUpward()
        {
            Vector3 ground = Vector3.zero;

            Vector3 first = StatusEffectBar.RowPosition(ground, 0f, HeadOffset, 0, RowStep, Width);
            Vector3 second = StatusEffectBar.RowPosition(ground, 0f, HeadOffset, 1, RowStep, Width);

            Assert.That(second.y - first.y, Is.EqualTo(RowStep).Within(0.0001f));
        }

        /// <summary>게이지는 왼쪽 끝을 기준으로 늘어난다. 그 끝이 몸 기둥의 왼쪽 절반만큼 밖이다.</summary>
        [Test]
        public void RowIsCenteredOnTheBodyColumn()
        {
            var ground = new Vector3(-2f, 0f, 2.5f);

            Vector3 body = BeltScroll.ToView(ground, 0f);
            Vector3 row = StatusEffectBar.RowPosition(ground, 0f, HeadOffset, 0, RowStep, Width);

            Assert.That(row.x + Width * 0.5f, Is.EqualTo(body.x).Within(0.0001f));
        }

        [Test]
        public void RowFollowsDepthAndJumpHeight()
        {
            // 깊이 z=2는 화면 세로 1.8로 접히고(0.9 배율), 머리 오프셋은 몸이 줄어든 만큼(0.88배) 내려온다.
            Vector3 row = StatusEffectBar.RowPosition(new Vector3(5f, 0f, 2f), 0f, HeadOffset, 0, RowStep, Width);

            Assert.That(row.y, Is.EqualTo(1.8f + HeadOffset * 0.88f).Within(0.001f));

            Vector3 low = StatusEffectBar.RowPosition(Vector3.zero, 0f, HeadOffset, 0, RowStep, Width);
            Vector3 high = StatusEffectBar.RowPosition(Vector3.zero, 3f, HeadOffset, 0, RowStep, Width);

            Assert.That(high.y - low.y, Is.EqualTo(3f).Within(0.0001f), "띄워진 적의 게이지도 같이 올라간다");
        }

        // ── 무엇을 그릴지 ────────────────────────────────

        [Test]
        public void Neutral_ShowsNothing()
        {
            Combat c = NewCombat("Idle", Vector3.zero);

            Assert.That(StatusEffectVisuals.Collect(c, rows), Is.Zero);
        }

        [Test]
        public void Stun_ShowsItsRemainingTime()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.Hit(in Poke, attacker);
            StatusEffectVisuals.Collect(victim, rows);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].label, Is.EqualTo(CombatStateVisuals.Label(CombatState.LightHit)),
                        "머리 위 글자와 같은 표를 써야 이름이 갈리지 않는다");
            Assert.That(rows[0].color, Is.EqualTo(CombatStateVisuals.StateColor(CombatState.LightHit)));
            Assert.That(rows[0].remain, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(rows[0].Ratio, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void StunGauge_DrainsAsItRecovers()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.Hit(in Poke, attacker);
            victim.Tick(0.15f);
            StatusEffectVisuals.Collect(victim, rows);

            Assert.That(rows[0].Ratio, Is.EqualTo(0.5f).Within(0.001f));
        }

        /// <summary>다운은 경직과 다른 길이를 쓴다. 분모가 안 바뀌면 게이지가 100%를 넘거나 처음부터 반쯤 차 있다.</summary>
        [Test]
        public void DownGauge_UsesTheDownDurationAsItsDenominator()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.Hit(in Launcher, attacker);
            victim.Tick(0.5f);   // 경직이 끝나고 지면에 있으면 착지 처리 → 다운
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Down), "선행 조건: 다운이어야 한다");

            StatusEffectVisuals.Collect(victim, rows);

            Assert.That(rows[0].label, Is.EqualTo(CombatStateVisuals.Label(CombatState.Down)));
            Assert.That(rows[0].Ratio, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Buffs_AreListedAboveTheStunRow()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.Statuses.Apply(StatusKind.Shield, 5f);
            victim.Hit(in Poke, attacker);

            StatusEffectVisuals.Collect(victim, rows);

            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].label, Is.EqualTo(CombatStateVisuals.Label(CombatState.LightHit)),
                        "가장 급한 값이 몸에서 제일 가깝다");
            Assert.That(rows[1].label, Is.EqualTo(StatusEffectVisuals.Label(StatusKind.Shield)));
            Assert.That(rows[1].remain, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void ParrySuccess_ShowsTheInvulnerabilityWindow()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.BeginParryWindow();
            victim.Hit(in Poke, attacker);
            Assert.That(victim.IsParryInvulnerable, Is.True, "선행 조건: 무적으로 갈아탔다");

            StatusEffectVisuals.Collect(victim, rows);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].label, Is.EqualTo(StatusEffectVisuals.InvulnerableLabel));
            Assert.That(rows[0].Ratio, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void NeverDrawsMoreRowsThanTheCap()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.Statuses.Apply(StatusKind.Shield, 5f);
            victim.Statuses.Apply(StatusKind.DamageCut, 5f);
            victim.Statuses.Apply(StatusKind.Lifesteal, 5f);
            victim.Hit(in Poke, attacker);

            Assert.That(StatusEffectVisuals.Collect(victim, rows),
                        Is.LessThanOrEqualTo(StatusEffectVisuals.MaxRows));
        }

        [Test]
        public void Corpses_ShowNothing()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            victim.Statuses.Apply(StatusKind.Shield, 5f);
            victim.TakeDamage(new DamageData(9999f));

            Assert.That(victim.IsDead, Is.True);
            Assert.That(StatusEffectVisuals.Collect(victim, rows), Is.Zero);
        }

        // ── 헬퍼 ────────────────────────────────────────

        /// <summary>Physics.Facing의 기본값이 Vector3.right이므로 +X가 정면이다.</summary>
        private static readonly Vector3 InFront = new Vector3(3f, 0f, 0f);

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

        /// <summary>띄우는 한 대. 경직이 끝나면 다운으로 넘어간다.</summary>
        private static readonly HitData Launcher = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            hitStunDuration = 0.3f,
        };

        private Combat NewCombat(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            spawned.Add(go);

            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }
    }
}
