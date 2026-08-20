using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>머리 위 게이지 한 줄. 무엇에서 왔는지는 이미 잊은, 그리기용 값만 남은 형태.</summary>
    public readonly struct StatusView
    {
        public readonly string label;
        public readonly Color color;
        public readonly float remain;
        public readonly float duration;

        public StatusView(string label, Color color, float remain, float duration)
        {
            this.label = label;
            this.color = color;
            this.remain = remain;
            this.duration = duration;
        }

        /// <summary>남은 비율 0~1. 게이지 폭이 이걸 따른다.</summary>
        public float Ratio => duration > 0.0001f ? Mathf.Clamp01(remain / duration) : 0f;
    }

    /// <summary>
    /// 지속 상태를 화면 표현(글자 · 색)으로 옮기는 표. <see cref="CombatStateVisuals"/>의 짝이다.
    ///
    /// 경직 계열은 여기서 새로 만들지 않고 <see cref="CombatStateVisuals"/>를 그대로 부른다 —
    /// 머리 위 글자(<see cref="EnemyStateLabel"/>)와 그 아래 게이지가 다른 이름 · 다른 색으로
    /// 같은 상태를 가리키면 이중 부호화가 오히려 방해가 된다.
    ///
    /// 유니티 객체를 만지지 않으므로 EditMode에서 그대로 검증된다.
    /// </summary>
    public static class StatusEffectVisuals
    {
        /// <summary>한 캐릭터에 동시에 그릴 줄 수의 상한. 넘치면 머리 위가 탑이 된다.</summary>
        public const int MaxRows = 4;

        /// <summary>패링 성공 무적. 짧지만 "지금은 뭘 맞아도 안 아프다"가 보여야 반격이 읽힌다.</summary>
        public const string InvulnerableLabel = "무적";

        private static readonly Color InvulnerableColor = new Color(0.298f, 0.788f, 0.941f); // #4CC9F0
        private static readonly Color ShieldColor = new Color(0.361f, 0.855f, 0.804f);       // #5CDACD
        private static readonly Color DamageCutColor = new Color(0.678f, 0.780f, 1f);        // #ADC7FF
        private static readonly Color LifestealColor = new Color(0.804f, 0.294f, 0.427f);    // #CD4B6D

        public static string Label(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Shield: return "보호막";
                case StatusKind.DamageCut: return "피해감소";
                case StatusKind.Lifesteal: return "흡혈";
                default: return kind.ToString();
            }
        }

        public static Color StatusColor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Shield: return ShieldColor;
                case StatusKind.DamageCut: return DamageCutColor;
                case StatusKind.Lifesteal: return LifestealColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 이 캐릭터에게 지금 그려야 할 줄을 모은다. <b>표시 대상의 유일한 정의</b>다 —
        /// 화면에 그리는 쪽(<see cref="StatusEffectBar"/>)은 순회와 배치만 한다.
        ///
        /// 순서는 아래(머리에 가까운 쪽)부터: 경직 → 무적 → 버프. 경직이 가장 자주 바뀌고
        /// 가장 급하게 읽어야 하는 값이라 몸에서 제일 가깝다.
        ///
        /// 시체에는 아무것도 그리지 않는다 — <see cref="CombatStateVisuals.ShouldShow"/>와 같은 규칙.
        /// </summary>
        public static int Collect(Combat combat, List<StatusView> into)
        {
            if (into == null) return 0;

            into.Clear();
            if (combat == null || combat.IsDead) return 0;

            // 경직 · 다운 · 기상. 착지로만 풀리는 상태(공중피격 · 넉백)는 타이머가 0이라
            // 여기서 저절로 빠진다 — 남은 시간이 없는 것에 게이지를 그릴 수는 없다.
            if (CombatStateVisuals.ShouldShow(combat.CombatState) && combat.StunRemaining > 0f)
                into.Add(new StatusView(CombatStateVisuals.Label(combat.CombatState),
                                        CombatStateVisuals.StateColor(combat.CombatState),
                                        combat.StunRemaining, combat.StunDuration));

            if (combat.ParryInvulnRemaining > 0f)
                into.Add(new StatusView(InvulnerableLabel, InvulnerableColor,
                                        combat.ParryInvulnRemaining, combat.ParryInvulnDuration));

            IReadOnlyList<StatusEffects.Entry> active = combat.Statuses.Active;
            for (int i = 0; i < active.Count && into.Count < MaxRows; i++)
            {
                StatusEffects.Entry e = active[i];
                into.Add(new StatusView(Label(e.kind), StatusColor(e.kind), e.remain, e.duration));
            }

            // 경직 + 무적 + 버프가 한꺼번에 걸리면 상한을 넘을 수 있다. 위(나중에 걸린 버프)부터 자른다.
            if (into.Count > MaxRows) into.RemoveRange(MaxRows, into.Count - MaxRows);

            return into.Count;
        }
    }
}
