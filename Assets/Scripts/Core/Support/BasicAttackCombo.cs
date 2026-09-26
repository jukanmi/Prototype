using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 평타 한 타의 정의. <see cref="Entity"/>가 배열로 들고 있고,
    /// <b>배열이 비어 있으면 지금까지의 단발 평타 그대로다</b> —
    /// 적 · 자율 동료 프리팹은 한 줄도 안 바뀐다.
    ///
    /// 완전한 <see cref="HitData"/>를 타마다 두지 않는 이유: 그러면 <c>canOtg</c> ·
    /// <c>targetState</c> · <c>snapZ</c>까지 타별로 어긋날 수 있다. 평타 3타가 서로 다른
    /// OTG 판정을 갖는 건 기능이 아니라 버그다. 기본 한 벌(<c>basicHit</c>)에
    /// <b>델타만</b> 얹는다.
    /// </summary>
    [Serializable]
    public struct BasicAttackStage
    {
        [Tooltip("인스펙터에서 알아보기 위한 이름. 판정에 쓰지 않는다.")]
        public string label;

        [Tooltip("이 타에 재생할 모션. 비우면 기본 평타 클립을 그대로 쓴다 —\n" +
                 "아트가 아직 없어도 로직은 돈다(세 타가 같은 그림으로 보일 뿐).")]
        public AnimationClip clip;

        [Tooltip("히트박스를 켜는 시점. 0 이하면 Entity의 기본 평타 값을 쓴다.")]
        public float windup;
        [Tooltip("히트박스를 끄는 시점. 0 이하면 기본값.")]
        public float activeEnd;
        [Tooltip("후딜 포함 이 타의 전체 길이. 0 이하면 기본값.")]
        public float total;

        [Tooltip("다음 타로 넘어갈 수 있는 최초 시점. 0 이하면 activeEnd —\n" +
                 "히트박스가 닫히자마자 후딜을 캔슬한다. 손맛을 정하는 유일한 숫자다.")]
        public float cancelStart;

        [Tooltip("이 타의 데미지 배율. 0 이하면 1.")]
        public float damageMultiplier;

        [Tooltip("켜면 아래 값이 basicHit의 반응(상태 · 넉백 · 띄우기)을 덮는다. 마무리 타만 켠다.")]
        public bool overrideReaction;
        public CombatState nextState;
        public KnockbackMode mode;
        [Tooltip("밀어낼 거리(유닛).")]
        public float pushDistance;
        [Tooltip("띄울 높이(유닛).")]
        public float airborneHeight;
        [Tooltip("공중에 뜬 대상에게 쓰는 띄우기 높이. 0이면 airborneHeight를 그대로 쓴다.")]
        public float aerialAirborneHeight;
        [Tooltip("이미 떠 있는 대상을 airborneHeight 위로는 올리지 않는다.")]
        public bool capAirborne;
        public float hitStunDuration;
    }

    /// <summary>0을 기본값으로 접고 난 뒤의 타이밍. 상태가 매 틱 이걸 본다.</summary>
    public readonly struct BasicAttackTiming
    {
        public readonly float windup;
        public readonly float activeEnd;
        public readonly float cancelStart;
        public readonly float total;

        public BasicAttackTiming(float windup, float activeEnd, float cancelStart, float total)
        {
            this.windup = windup;
            this.activeEnd = activeEnd;
            this.cancelStart = cancelStart;
            this.total = total;
        }
    }

    /// <summary>이번 틱에 콤보가 할 일.</summary>
    public enum BasicComboStep
    {
        /// <summary>이 타를 계속 돈다.</summary>
        Continue,
        /// <summary>다음 타로 넘어간다.</summary>
        Advance,
        /// <summary>평타를 마치고 Idle로 나간다.</summary>
        Finish,
    }

    /// <summary>
    /// 평타 콤보 판정. <b>전부 순수 함수</b>다 —
    /// EditMode에서는 <see cref="Entity"/>의 <c>Awake</c>가 돌지 않아 상태머신이 없으므로,
    /// 판정을 상태 안에 두면 테스트가 불가능하다.
    /// </summary>
    public static class BasicComboRules
    {
        /// <summary>0 이하를 "미지정"으로 보고 기본값으로 접는다(<see cref="SkillData.ApproachDistance"/>와 같은 규약).</summary>
        public static float Or(float value, float fallback) => value > 0f ? value : fallback;

        /// <summary>단계 하나의 타이밍을 기본 평타 값 위에 얹어 확정한다.</summary>
        public static BasicAttackTiming ResolveTiming(in BasicAttackStage stage,
                                                      float baseWindup, float baseActiveEnd, float baseTotal)
        {
            float windup = Or(stage.windup, baseWindup);
            float activeEnd = Or(stage.activeEnd, baseActiveEnd);
            float total = Or(stage.total, baseTotal);

            return new BasicAttackTiming(windup, activeEnd, Or(stage.cancelStart, activeEnd), total);
        }

        /// <summary>
        /// 기본 평타 타격에 이 단계의 델타를 얹는다.
        /// 배율은 공격력이 이미 실린 뒤에 곱해진다 — <see cref="Entity.BuildBasicHit()"/>가 먼저 돈다.
        /// </summary>
        public static HitData BuildStageHit(in HitData basic, in BasicAttackStage stage)
        {
            HitData h = basic;
            h.damageData.damage *= Or(stage.damageMultiplier, 1f);

            if (!stage.overrideReaction) return h;

            // 띄우기는 둘을 같이 덮어야 한다. airborneHeight만 넣으면 몸은 뜨는데
            // 상태가 LightHit로 남아 착지 → 다운 전이가 어긋난다.
            h.nextState = stage.nextState;
            h.mode = stage.mode;
            h.pushDistance = stage.pushDistance;
            h.airborneHeight = stage.airborneHeight;
            h.aerialAirborneHeight = stage.aerialAirborneHeight;
            h.capAirborne = stage.capAirborne;
            h.hitStunDuration = Or(stage.hitStunDuration, h.hitStunDuration);

            return h;
        }

        /// <summary>
        /// 이번 틱의 결정. 우선순위가 <b>여기서 한 번만</b> 정해진다 —
        /// 상태 쪽에 흩어 두면 "마지막 프레임에 Finish가 Advance를 이겨 콤보가 한 타에서 멈추는"
        /// 실수를 코드 리뷰로 잡아야 한다.
        /// </summary>
        public static BasicComboStep Decide(float timer, in BasicAttackTiming t,
                                            int stage, int stageCount, bool buffered)
        {
            bool last = stage >= stageCount - 1;

            // 다음 타가 있으면 후딜을 캔슬하고 넘어간다. Finish보다 먼저 본다.
            if (buffered && !last && timer >= t.cancelStart)
                return BasicComboStep.Advance;

            if (timer < t.total) return BasicComboStep.Continue;

            // 마무리는 캔슬도 이어치기도 없다. 예전에는 후딜이 끝나는 순간 선입력이 살아 있으면
            // 1타부터 다시 돌았는데, 선입력 창(0.25s)이 마무리 후딜(0.7s)보다 짧아도
            // 두들기는 동안에는 창이 계속 채워져 평타가 영영 안 끝났다 —
            // 상태를 못 벗어나니 Idle 전이도, 콤보를 끊는 어떤 판단도 돌지 않는다.
            // 3타를 다 쓰면 무조건 Idle로 나간다. 다시 치려면 새로 눌러야 한다.
            return BasicComboStep.Finish;
        }
    }
}
