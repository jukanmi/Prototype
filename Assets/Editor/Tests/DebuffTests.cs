using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 디버프(스턴 · 빙결)의 계약.
    ///
    /// 존재 이유는 <see cref="WallStun_SurvivesAFollowUpJab"/> 하나다 — 벽에 처박아 건 1.2초가
    /// 0.2초 뒤 들어온 평타의 <c>hitStunDuration</c> 0.3초에 통째로 지워졌다. 경직과 디버프가
    /// 같은 타이머를 쓰고 있었기 때문이고, 그래서 축을 갈랐다.
    ///
    /// 시간은 <see cref="StatusEffects"/>가 재고, 몸은 <see cref="StunState"/> · <see cref="FrozenState"/>가
    /// 붙잡는다. 여기서 두 층이 어긋나지 않는지 같이 본다.
    /// </summary>
    public class DebuffTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 회귀: 벽스턴이 평타에 지워지지 않는다 ────────

        [Test]
        public void WallStun_SurvivesAFollowUpJab()
        {
            Wall(new Vector3(2f, 0f, 0f));          // 벽면은 x = 1.5

            Combat attacker = NewBody("Attacker");
            Combat victim = NewBody("Victim");

            attacker.Attack(victim, in WallPush);

            Assert.That(victim.HasDebuff(Debuff.Stun), Is.True,
                        "선행 조건: 벽까지 1.5m인데 3m를 밀었으므로 벽 스턴이 걸려야 한다");

            float stunned = victim.Statuses.Remaining(StatusKind.Stun);

            // 곧바로 평타. 예전에는 이 한 대가 1.2초를 0.3초로 잘라 냈다.
            attacker.Attack(victim, in Jab);

            Assert.That(victim.Statuses.Remaining(StatusKind.Stun), Is.EqualTo(stunned).Within(0.0001f),
                        "평타의 경직이 벽 스턴을 덮어쓰면 안 된다");

            victim.Tick(0.4f);   // 평타 경직(0.3초)은 이미 끝났을 시간
            victim.Tick(0.1f);   // 경직이 풀린 다음 프레임에 몸이 굳는다

            Assert.That(victim.Statuses.Remaining(StatusKind.Stun), Is.GreaterThan(0f), "아직 스턴이 남아 있어야 한다");
            Assert.That(victim.Owner.StateMachine.CurState, Is.SameAs(victim.Owner.StunState));
            Assert.That(victim.Owner.IsBusy, Is.True, "굳은 몸은 입력 · AI 게이트에 걸려야 한다");
        }

        // ── 상태머신과의 정합 ───────────────────────────

        [Test]
        public void Debuff_GrabsTheBodyImmediately()
        {
            Combat c = NewBody("Victim");

            c.ApplyDebuff(Debuff.Stun, 1f);

            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.StunState),
                        "경직이 없으면 거는 그 자리에서 굳는다");
        }

        [Test]
        public void Freeze_OutranksStun()
        {
            Combat c = NewBody("Victim");

            c.ApplyDebuff(Debuff.Stun, 1f);
            c.ApplyDebuff(Debuff.Freeze, 1f);

            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.FrozenState),
                        "둘 다 걸렸으면 화면에 나갈 그림은 하나여야 한다");
        }

        [Test]
        public void Debuff_ExpiresAndReturnsToIdle()
        {
            Combat c = NewBody("Victim");
            c.ApplyDebuff(Debuff.Stun, 0.5f);

            c.Tick(0.6f);

            Assert.That(c.HasDebuff(Debuff.ActionBlocking), Is.False);
            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.IdleState),
                        "스스로는 중단 불가라 정합 루프가 Force로 꺼내 줘야 한다");
            Assert.That(c.Owner.IsBusy, Is.False);
        }

        [Test]
        public void Debuff_DoesNotTouchCombatState()
        {
            Combat c = NewBody("Victim");

            c.ApplyDebuff(Debuff.Freeze, 1f);

            Assert.That(c.CombatState, Is.EqualTo(CombatState.Neutral),
                        "디버프와 피격 경직은 직교다 — 콤보 연계 어휘를 건드리면 안 된다");
        }

        // ── 피격 규칙 ───────────────────────────────────

        [Test]
        public void Frozen_StillTakesDamageAndKnockback()
        {
            Combat attacker = NewBody("Attacker");
            Combat victim = NewBody("Victim");

            victim.ApplyDebuff(Debuff.Freeze, 3f);
            float hp = victim.Health.CurValue;

            attacker.Attack(victim, in WallPush);

            Assert.That(victim.Health.CurValue, Is.LessThan(hp), "굳었다고 데미지까지 사라지면 안 된다");
            Assert.That(victim.Physics.HorizontalVelocity.magnitude, Is.GreaterThan(0.1f),
                        "넉백이 먹혀야 얼린 적을 벽으로 밀어붙이는 콤보가 산다");
        }

        [Test]
        public void Freeze_SurvivesBeingHit()
        {
            Combat attacker = NewBody("Attacker");
            Combat victim = NewBody("Victim");

            victim.ApplyDebuff(Debuff.Freeze, 3f);
            attacker.Attack(victim, in Jab);

            Assert.That(victim.HasDebuff(Debuff.Freeze), Is.True, "빙결은 때려도 안 풀린다");
            Assert.That(victim.Owner.StateMachine.CurState, Is.SameAs(victim.Owner.FrozenState),
                        "피격 반응이 빙결을 밀어내면 한 대에 풀린 것과 같아진다");
        }

        [Test]
        public void HitData_CarriesTheDebuff()
        {
            Combat attacker = NewBody("Attacker");
            Combat victim = NewBody("Victim");

            attacker.Attack(victim, in FreezingJab);

            Assert.That(victim.Statuses.Remaining(StatusKind.Freeze), Is.EqualTo(2.5f).Within(0.0001f),
                        "저작은 새 Effect 클래스가 아니라 HitData 필드 두 개로 끝난다");
        }

        // ── 시간 규칙 ───────────────────────────────────

        [Test]
        public void LongerDebuff_WinsOverAShorterReapply()
        {
            Combat c = NewBody("Victim");

            c.ApplyDebuff(Debuff.Stun, 1.2f);
            c.ApplyDebuff(Debuff.Stun, 0.5f);

            Assert.That(c.Statuses.Remaining(StatusKind.Stun), Is.EqualTo(1.2f).Within(0.0001f),
                        "짧은 재시전이 이미 걸린 긴 스턴을 잘라 내면 안 된다");
        }

        /// <summary>
        /// 불릿타임(dt = 0)에는 안 닳는다. <b>의도된 동작</b>이라 이름으로 못 박아 둔다 —
        /// 불릿타임은 유저가 카드를 고르는 시간이라, 실시간으로 녹으면 오래 고민한 쪽이 벌을 받는다.
        /// </summary>
        [Test]
        public void DebuffClock_DoesNotRunWhileTimeIsFrozen()
        {
            Combat c = NewBody("Victim");
            c.ApplyDebuff(Debuff.Stun, 1f);

            c.Tick(0f);

            Assert.That(c.Statuses.Remaining(StatusKind.Stun), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Corpse_TakesNoDebuff()
        {
            Combat c = NewBody("Victim");
            c.TakeDamage(new DamageData(99999f));
            Assert.That(c.IsDead, Is.True, "선행 조건: 죽어 있어야 한다");

            c.ApplyDebuff(Debuff.Stun, 1f);

            Assert.That(c.HasDebuff(Debuff.Stun), Is.False, "시체를 굳힐 이유가 없다");
        }

        // ── 벤치(교대) ──────────────────────────────────

        [Test]
        public void BenchedBody_LosesDebuffsButKeepsBuffs()
        {
            Combat c = NewBody("Ally");
            c.Statuses.Apply(StatusKind.Shield, 10f);
            c.ApplyDebuff(Debuff.Freeze, 5f);

            c.ClearDebuffs();   // Entity.ReleaseBody가 부르는 그 경로

            Assert.That(c.HasDebuff(Debuff.All), Is.False,
                        "꺼진 몸은 Tick이 멈춘다 — 물고 내려가면 벤치에 숨어 디버프를 피하거나 영영 얼어 있다");
            Assert.That(c.Statuses.Has(StatusKind.Shield), Is.True,
                        "버프는 남는다 — 필드 밖에서 보호막이 녹을 이유는 없다");
        }

        /// <summary>
        /// 가드 브레이크도 <see cref="Combat.ClearHitStun"/>을 부른다. 거기서 디버프까지 지우면
        /// 가드를 깨는 순간 방금 건 스턴이 사라져, 가장 무방비여야 할 구간이 오히려 안전해진다.
        /// </summary>
        [Test]
        public void ClearHitStun_LeavesDebuffsAlone()
        {
            Combat c = NewBody("Victim");
            c.ApplyDebuff(Debuff.Stun, 2f);

            c.ClearHitStun();

            Assert.That(c.HasDebuff(Debuff.Stun), Is.True);
        }

        // ── 표시 ────────────────────────────────────────

        [Test]
        public void Debuffs_AreDrawnBelowBuffs()
        {
            Combat c = NewBody("Victim");
            c.Statuses.Apply(StatusKind.Shield, 5f);
            c.ApplyDebuff(Debuff.Stun, 1f);          // 나중에 걸렸지만 더 급한 정보다

            var rows = new List<StatusView>();
            StatusEffectVisuals.Collect(c, rows);

            int stun = rows.FindIndex(r => r.label == StatusEffectVisuals.Label(StatusKind.Stun));
            int shield = rows.FindIndex(r => r.label == StatusEffectVisuals.Label(StatusKind.Shield));

            Assert.That(stun, Is.GreaterThanOrEqualTo(0), "스턴 행이 그려져야 한다");
            Assert.That(stun, Is.LessThan(shield), "디버프가 버프보다 몸에 가까운 쪽에 온다");
        }

        [Test]
        public void FrozenBody_TintsOverSuperArmor()
        {
            Color body = Color.red;

            Color frozen = CombatStateVisuals.Tint(body, CombatState.Neutral, CombatOverlay.Frozen);
            Color armored = CombatStateVisuals.Tint(body, CombatState.Neutral, CombatOverlay.SuperArmor);

            Assert.That(frozen, Is.Not.EqualTo(body), "얼면 색이 바뀌어야 한다");
            Assert.That(frozen, Is.Not.EqualTo(armored), "얼어붙은 보스가 아머 금색이면 거짓말이다");
        }

        // ── 도구 ────────────────────────────────────────

        private Combat NewBody(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }

        private void Wall(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            go.transform.position = position;
            go.transform.localScale = new Vector3(1f, 4f, 40f);
            spawned.Add(go);

            // 에디트모드에는 물리 스텝이 돌지 않는다. 방금 옮긴 콜라이더를 레이가 보려면 직접 맞춰 준다.
            UnityEngine.Physics.SyncTransforms();
        }

        /// <summary>벽 쪽으로 3m 미는 타격. 시전자 정면(+X)이 기본이라 벽을 향한다.</summary>
        private static readonly HitData WallPush = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            pushDistance = 3f,
            hitStunDuration = 0.3f,
        };

        /// <summary>밀지 않는 평타. 벽 스턴 조건(pushDistance)을 안 갖는다.</summary>
        private static readonly HitData Jab = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        /// <summary>저작으로 빙결을 거는 타격.</summary>
        private static readonly HitData FreezingJab = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
            debuff = Debuff.Freeze,
            debuffDuration = 2.5f,
        };
    }
}
