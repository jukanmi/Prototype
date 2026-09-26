using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 보스. 거리 · 남은 체력 · 쿨을 보고 <see cref="BossPatternAction"/>의 패턴 하나를 고른다.
    /// 무상태 — 쿨 타이머는 EnemyControl이, 1회성 소모 여부는 실행기가 들고 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Brain/Boss", fileName = "Brain_Boss")]
    public class BossBrainAsset : EnemyBrainAsset
    {
        /// <summary>패턴 하나를 언제 쓸지의 조건. 실행 방법은 <see cref="BossPattern"/>이 갖는다.</summary>
        [Serializable]
        public struct PatternRule
        {
            [Tooltip("인스펙터에서 알아보기 위한 이름. 판단에는 쓰이지 않는다.")]
            public string label;

            [Tooltip("BossPatternAction의 패턴 번호.")]
            public int index;

            [Tooltip("이 거리 이상에서만 쓴다.")]
            public float minRange;
            [Tooltip("이 거리 이하에서만 쓴다.")]
            public float maxRange;

            [Tooltip("쓴 뒤 재사용 대기시간. 0이면 EnemyControl의 기본 쿨을 쓴다.")]
            public float cooldown;

            [Tooltip("남은 체력이 이 비율 이하일 때만 쓴다. 0이면 제한 없음.")]
            [Range(0f, 1f)] public float maxHealthRatio;
        }

        [Tooltip("위에서부터 검사한다. 조건을 만족하는 첫 규칙이 나간다.")]
        public PatternRule[] patterns;

        public override EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            if (ctx.target == null) return EnemyIntent.None;

            if (ctx.p.leashRange > 0f && ctx.distance > ctx.p.leashRange)
                return EnemyIntent.None;

            // distance를 이미 받았으므로 normalized(sqrt 재계산) 대신 나눈다.
            Vector3 dir = ctx.distance > 0.0001f ? ctx.toTarget / ctx.distance : Vector3.zero;

            int pick = Choose(in ctx);
            if (pick >= 0)
                return EnemyIntent.Special(patterns[pick].index, dir, patterns[pick].cooldown);

            // 쓸 패턴이 없으면 보통 적처럼 붙어서 때린다.
            if (ctx.distance <= ctx.p.attackRange)
                return ctx.attackReady ? EnemyIntent.Attack(dir) : EnemyIntent.None;

            return EnemyIntent.Move(dir);
        }

        /// <summary>
        /// 조건을 만족하는 첫 규칙의 <b>배열 위치</b>. 없으면 -1.
        ///
        /// 무작위 가중치는 넣지 않았다. 다른 브레인 3종이 전부 결정적이라 재현이 쉽고,
        /// 패턴마다 쿨이 달라서 실제 순서는 저절로 섞인다. 단조로워 보이면 그때 얹는다.
        /// </summary>
        private int Choose(in EnemyBrainContext ctx)
        {
            if (patterns == null) return -1;

            for (int i = 0; i < patterns.Length; i++)
            {
                PatternRule r = patterns[i];

                if (!ctx.IsSpecialReady(r.index)) continue;
                if (ctx.distance < r.minRange || ctx.distance > r.maxRange) continue;

                // 0은 "제한 없음"이다. 이 프로젝트의 leashRange · specialRange와 같은 규약 —
                // 안 그러면 인스펙터에서 손대지 않은 규칙이 전부 조용히 죽는다.
                if (r.maxHealthRatio > 0f && ctx.healthRatio > r.maxHealthRatio) continue;

                return i;
            }

            return -1;
        }
    }
}
