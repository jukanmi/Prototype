using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전투 상태를 화면 표현(색 · 글자)으로 옮기는 표. <b>한 벌만</b> 유지한다.
    ///
    /// <see cref="EnemyStateTint"/>(몸 색)와 <see cref="EnemyStateLabel"/>(머리 위 글자)이
    /// 같은 함수를 부른다. 두 벌로 갈리면 색과 글자가 어긋나 이중 부호화가 의미를 잃는다.
    ///
    /// 유니티 객체를 만지지 않으므로 EditMode에서 그대로 검증된다.
    /// </summary>
    public static class CombatStateVisuals
    {
        /// <summary>
        /// 기본 색을 상태색 쪽으로 끌어당기는 정도. 1이면 완전히 덮는다.
        /// 1로 두지 않는 이유는 적 고유색(고블린 빨강 · 궁수 파랑 · 멧돼지 주황)의
        /// 흔적을 남겨, 물든 뒤에도 무슨 적이었는지 알아볼 수 있게 하기 위해서다.
        /// </summary>
        public const float TintStrength = 0.8f;

        /// <summary>
        /// 예고(선딜) 표시의 세기. 경직(0.8)보다 <b>세게</b> 덮어 완전한 흰색으로 번쩍인다.
        ///
        /// 경직도 흰색 계열이라 세기까지 같으면 "때렸다"와 "맞기 직전이다"가 구분되지 않는다.
        /// 예고는 고유색을 지워 버릴 만큼 하얗고, 경직은 적 색이 비쳐 보인다.
        /// </summary>
        public const float TelegraphStrength = 1f;

        // 색과 글자를 함께 쓴다. 색만으로는 색약자가 구분하지 못하고,
        // 글자만으로는 난전에서 안 읽힌다.
        private static readonly Color LightHitColor = new Color(1f, 1f, 1f);           // #FFFFFF
        private static readonly Color AerialHitColor = new Color(0.349f, 0.761f, 1f);  // #59C2FF
        private static readonly Color KnockbackColor = new Color(1f, 0.549f, 0.259f);  // #FF8C42
        private static readonly Color WallBoundColor = new Color(0.886f, 0.290f, 1f);  // #E24AFF
        private static readonly Color DownColor = new Color(0.478f, 0.478f, 0.522f);   // #7A7A85
        private static readonly Color GetupColor = new Color(1f, 0.820f, 0.400f);      // #FFD166

        /// <summary>공격 예고. 이 색으로 번쩍이는 순간이 곧 "지금 대시하면 패링된다"는 신호다.</summary>
        private static readonly Color TelegraphColor = new Color(1f, 1f, 1f);          // #FFFFFF

        /// <summary>예고 중에 머리 위에 띄울 글자. 색만으로는 색약자가 구분하지 못한다.</summary>
        public const string TelegraphLabel = "!";

        /// <summary>화면에 드러낼 상태인지. 평상시(Neutral)와 사망은 표시하지 않는다.</summary>
        public static bool ShouldShow(CombatState state)
            => state != CombatState.Neutral && state != CombatState.Dead;

        /// <summary>머리 위에 띄울 글자. 표시 대상이 아니면 빈 문자열.</summary>
        public static string Label(CombatState state)
        {
            switch (state)
            {
                case CombatState.LightHit: return "경직";
                case CombatState.AerialHit: return "공중";
                case CombatState.Knockback: return "넉백";
                case CombatState.WallBound: return "벽꽂";
                case CombatState.Down: return "다운";
                case CombatState.Getup: return "기상";
                default: return string.Empty;
            }
        }

        /// <summary>상태를 대표하는 색. 표시 대상이 아니면 흰색(중립값).</summary>
        public static Color StateColor(CombatState state)
        {
            switch (state)
            {
                case CombatState.LightHit: return LightHitColor;
                case CombatState.AerialHit: return AerialHitColor;
                case CombatState.Knockback: return KnockbackColor;
                case CombatState.WallBound: return WallBoundColor;
                case CombatState.Down: return DownColor;
                case CombatState.Getup: return GetupColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 기본 색을 상태색 쪽으로 섞는다.
        ///
        /// 알파는 항상 기본 색의 것을 쓴다 — 사망 페이드가 알파를 깎는데
        /// 여기서 덮으면 죽는 연출이 도중에 끊긴다.
        /// </summary>
        public static Color Tint(Color baseColor, CombatState state)
            => Tint(baseColor, state, telegraphing: false);

        /// <summary>
        /// 예고까지 반영한 색. <b>전투 상태가 예고를 이긴다</b> —
        /// 예고 중에 맞으면 특수 행동이 취소되므로(EnemyControl), 그 순간 화면도 피격을 보여야 한다.
        /// </summary>
        public static Color Tint(Color baseColor, CombatState state, bool telegraphing)
        {
            if (ShouldShow(state))
                return Mix(baseColor, StateColor(state), TintStrength);

            if (telegraphing && state != CombatState.Dead)
                return Mix(baseColor, TelegraphColor, TelegraphStrength);

            return baseColor;
        }

        /// <summary>
        /// 알파는 항상 기본 색의 것을 쓴다 — 사망 페이드가 알파를 깎는데
        /// 여기서 덮으면 죽는 연출이 도중에 끊긴다.
        /// </summary>
        private static Color Mix(Color baseColor, Color target, float strength)
        {
            Color mixed = Color.Lerp(baseColor, target, strength);
            mixed.a = baseColor.a;
            return mixed;
        }
    }
}
