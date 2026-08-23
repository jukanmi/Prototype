namespace Prototype.EditorTools
{
    /// <summary>
    /// 보스 패턴 5종의 <b>단일 정의</b>. 아트 빌더와 프리팹 빌더가 같은 표를 읽는다.
    ///
    /// 한 곳에 모은 이유는 클립 길이와 판정 길이가 맞물려 있기 때문이다 —
    /// 발동 구간이 0.3초인데 클립이 0.6초짜리면 칼이 다 나가기 전에 판정이 끝나 있고,
    /// 두 숫자를 따로 들고 있으면 한쪽만 고쳐도 아무 에러 없이 어긋난다.
    /// 여기서는 fps를 발동 길이에서 <b>계산</b>하므로 어긋날 수가 없다.
    ///
    /// 표를 늘리면 패턴이 늘어난다. 브레인 규칙까지 같은 줄에 있어서
    /// "언제 쓰는가"와 "어떻게 나가는가"를 한눈에 맞춰 볼 수 있다.
    /// </summary>
    internal static class BossPatternTable
    {
        /// <summary>공격 시트 한 장의 프레임 수. 4장 모두 같다(600 / 150).</summary>
        internal const int AttackFrames = 4;

        /// <summary>
        /// 패턴이 쓰는 히트박스. 프리팹에 종류별로 하나씩 있고 패턴이 골라 쓴다.
        ///
        /// bool 두 개로 나누지 않는 이유: 둘 다 켜진 저작이 가능해지고, 그때 어느 쪽이
        /// 이기는지는 코드를 읽어야만 알 수 있다.
        /// </summary>
        internal enum HitboxKind
        {
            /// <summary>평타 상자. 발밑 근접.</summary>
            Basic,
            /// <summary>광역 상자. 앞으로 넓다.</summary>
            Wide,
            /// <summary>구. 앞뒤 구분이 없다 — 둘레 전부를 때리는 패턴만 쓴다.</summary>
            Radial,
        }

        internal struct Entry
        {
            public string label;         // 사람이 부르는 이름
            public string sheet;         // 원본 시트 파일명(확장자 없이)
            public string state;         // 발동 중 Animator 상태
            public string windupState;   // 예고 중 Animator 상태
            public string chargeState;   // 차징 중 Animator 상태. 비우면 windupState를 쓴다

            // ── 차징 ──
            // chargeTime이 0이면 차징 패턴이 아니다 — 예고부터 시작한다.
            public float chargeTime;

            // ── 타이밍 ──
            public float telegraph;
            public float active;
            public float recovery;

            // ── 판정 ──
            public int hitCount;
            public float hitDuration;
            public float damageScale;
            public HitboxKind hitboxKind;  // 기본값 Basic = 평타 상자

            // ── 이동 ──
            public float advanceSpeed;
            public bool cancelOnContact;

            // ── 버프 ──
            public bool once;
            public float attackPowerScale;
            public float moveSpeedScale;
            public float attackIntervalScale;

            // ── 브레인 규칙 (언제 고를지) ──
            public float minRange;
            public float maxRange;
            public float cooldown;
            public float maxHealthRatio;  // 0이면 제한 없음

            /// <summary>
            /// 발동 클립의 재생 속도. 프레임 0은 예고가 붙들고 있으므로 발동은 1~3만 쓴다.
            /// 길이가 0이면 나눌 수 없어 넉넉한 값으로 떨어뜨린다.
            /// </summary>
            public float ActiveFps => active > 0f ? (AttackFrames - 1) / active : 12f;
        }

        /// <summary>
        /// 브레인은 <b>위에서부터</b> 검사해 첫 번째로 맞는 것을 쓴다. 즉 이 순서가 곧 우선순위다.
        /// 격노가 맨 위인 이유: 체력이 반토막 난 순간 다른 패턴보다 먼저 나가야 페이즈 전환으로 읽힌다.
        ///
        /// <b>배열 위치가 곧 패턴 번호다.</b> BossPrefabBuilder가 실행기 배열과 브레인 규칙을
        /// 이 순서 그대로 만들므로, 줄 순서를 바꾸면 우선순위와 번호가 함께 바뀐다.
        /// </summary>
        internal static readonly Entry[] All =
        {
            // ── 0. 격노 — 전투당 한 번. 충격파를 깔면서 스스로를 강화한다.
            new Entry
            {
                label = "격노", sheet = "Attack4",
                state = "Pattern_Rage", windupState = "Pattern_Rage_Windup",
                telegraph = 0.60f, active = 0.40f, recovery = 0.60f,
                hitCount = 1, hitDuration = 0.22f, damageScale = 0.8f, hitboxKind = HitboxKind.Wide,
                once = true,
                attackPowerScale = 1.35f, moveSpeedScale = 1.2f, attackIntervalScale = 0.7f,
                minRange = 0f, maxRange = 6f, cooldown = 999f, maxHealthRatio = 0.5f,
            },

            // ── 1. 횡베기 — 유일한 차징기. 2.4초 동안 모으고, 몸을 돌려 <b>둘레 전부</b>를 벤다.
            //
            //      때려도 밀리지 않는다. 차징은 시작하면 2.4초 뒤에 정확히 터진다 —
            //      맞을 때마다 늦춰지면 "언제 터지나"가 매 판 달라져 회피 타이밍을 못 배운다.
            //      끊는 수단은 가드브레이크 하나다(EnemyControl이 Cancel을 부른다).
            //
            //      앞뒤 구분이 없으므로(HitboxKind.Radial) 뒤로 돌아가는 회피가 통하지 않는다 —
            //      반경 밖으로 빠지거나, 모으는 동안 끊거나 둘 중 하나다. 차징기에 상자 판정을 주면
            //      "모으는 걸 보고 등 뒤로 걸어가면 끝"이 되어 2.4초가 무의미해진다.
            //
            //      격노(체력 50%) 바로 아래에 둔다. 체력 60% 아래부터 나오므로
            //      후반 페이즈의 골격이 "격노 → 차징기 압박"으로 읽힌다.
            //      시트를 격노와 공유한다 — 원호 궤적이 그려진 유일한 시트다(Attack4).
            new Entry
            {
                label = "횡베기", sheet = "Attack4",
                state = "Pattern_Sweep", windupState = "Pattern_Sweep_Windup",
                chargeState = "Pattern_Sweep_Hold",
                chargeTime = 2.4f,
                telegraph = 0.35f, active = 0.35f, recovery = 1.1f,
                hitCount = 1, hitDuration = 0.24f, damageScale = 3.5f, hitboxKind = HitboxKind.Radial,
                minRange = 0f, maxRange = 6f, cooldown = 14f, maxHealthRatio = 0.6f,
            },

            // ── 2. 대지가르기 — 예고가 가장 길다. 이 패턴은 "피하라고" 있는 것이다.
            //      (아래 두 줄과 함께, 주석의 번호는 배열 위치와 같아야 한다)
            new Entry
            {
                label = "대지가르기", sheet = "Attack2",
                state = "Pattern_Slam", windupState = "Pattern_Slam_Windup",
                telegraph = 0.75f, active = 0.30f, recovery = 0.85f,
                hitCount = 1, hitDuration = 0.20f, damageScale = 2.0f, hitboxKind = HitboxKind.Wide,
                minRange = 0f, maxRange = 4.5f, cooldown = 6f,
            },

            // ── 3. 돌진베기 — 유일하게 전진한다. 닿거나 벽에 박으면 그 자리에서 후딜.
            //      최대 이동거리 = advanceSpeed x active = 6.75m.
            new Entry
            {
                label = "돌진베기", sheet = "Attack3",
                state = "Pattern_Dash", windupState = "Pattern_Dash_Windup",
                telegraph = 0.55f, active = 0.45f, recovery = 0.70f,
                hitCount = 1, hitDuration = 0.45f, damageScale = 1.4f, hitboxKind = HitboxKind.Wide,
                advanceSpeed = 15f, cancelOnContact = true,
                minRange = 3f, maxRange = 9f, cooldown = 5f,
            },

            // ── 4. 삼연참 — 붙었을 때의 주력. 한 타는 약하지만 세 번 다 맞으면 아프다.
            new Entry
            {
                label = "삼연참", sheet = "Attack1",
                state = "Pattern_Combo", windupState = "Pattern_Combo_Windup",
                telegraph = 0.35f, active = 0.60f, recovery = 0.55f,
                hitCount = 3, hitDuration = 0.12f, damageScale = 0.7f, hitboxKind = HitboxKind.Basic,
                minRange = 0f, maxRange = 3.2f, cooldown = 3.5f,
            },
        };
    }
}
