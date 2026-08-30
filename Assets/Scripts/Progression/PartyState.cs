using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스테이지를 넘어가는 파티의 <b>몸 상태</b> — 잔여 체력과 생사.
    ///
    /// <b>왜 필요한가.</b> 스테이지가 바뀌면 씬이 통째로 새로 로드되고, <see cref="Combat"/>가
    /// 프리팹의 <c>maxHealth</c>로 <see cref="Energy"/>를 새로 만든다. 아무것도 안 하면
    /// <b>매 스테이지 전원이 풀피로 부활</b>한다 — 로그라이크식 소모전이 성립하지 않는다.
    ///
    /// <see cref="MonoBehaviour"/>가 아니다. <see cref="RunProgression"/>이 소유하고
    /// <c>GameManager</c>가 Boot 씬에서 들고 있으므로 씬을 넘어 살아남는다 —
    /// 런 덱이 살아남는 것과 정확히 같은 이유다.
    ///
    /// <b>슬롯 번호가 아니라 <see cref="PartyMemberData"/> 에셋을 키로 쓴다.</b>
    /// 편성 순서가 바뀌어도 따라오고, 죽은 동료의 슬롯이 사라진 뒤에도 기록이 남는다.
    /// </summary>
    public class PartyState
    {
        /// <summary>
        /// 주인공이 쓰러진 채로 스테이지를 넘겼을 때 되살아나는 체력 비율.
        ///
        /// <b>주인공만 예외다.</b> 동료의 사망은 런 끝까지 영구지만, 주인공은 태그 로스터 0번이자
        /// <c>FindAnyObjectByType&lt;Player&gt;()</c>가 다섯 군데에서 찾는 앵커라 없앨 수가 없다.
        /// 동료가 살아남아 스테이지를 클리어했는데 주인공만 영영 못 일어나면
        /// 그 다음 스테이지를 시작할 방법이 없다.
        /// </summary>
        public const float HeroReviveRatio = 0.3f;

        public readonly struct MemberState
        {
            public readonly float HpRatio;
            public readonly bool Dead;

            public MemberState(float hpRatio, bool dead)
            {
                HpRatio = hpRatio;
                Dead = dead;
            }
        }

        private readonly Dictionary<PartyMemberData, MemberState> members =
            new Dictionary<PartyMemberData, MemberState>();

        /// <summary>주인공의 잔여 체력 비율. 쓰러졌으면 <see cref="HeroReviveRatio"/>로 낮춰 담는다.</summary>
        public float HeroHpRatio { get; private set; } = 1f;

        /// <summary>한 번이라도 기록했는가. 로그와 디버그용이다.</summary>
        public bool HasSnapshot { get; private set; }

        // ── 읽기 ────────────────────────────────────────

        /// <summary>이 동료가 이번 런에서 이미 죽었는가. 기록이 없으면 살아 있다.</summary>
        public bool IsDead(PartyMemberData member)
            => member != null && members.TryGetValue(member, out MemberState s) && s.Dead;

        /// <summary>
        /// 이 동료가 물고 갈 체력 비율. 기록이 없으면 1(첫 스테이지).
        /// 죽은 동료는 물어볼 일이 없지만, 물어보면 0이다.
        /// </summary>
        public float HpRatioOf(PartyMemberData member)
        {
            if (member == null || !members.TryGetValue(member, out MemberState s)) return 1f;
            return s.Dead ? 0f : s.HpRatio;
        }

        public int DeadCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<PartyMemberData, MemberState> kv in members)
                    if (kv.Value.Dead) n++;

                return n;
            }
        }

        // ── 기록 ────────────────────────────────────────

        /// <summary>
        /// 지금 파티의 상태를 찍는다. <b>스테이지를 넘어가기 직전에</b> 한 번 부른다.
        ///
        /// <b>기존 기록을 지우지 않는다.</b> 이미 죽어 슬롯이 사라진 동료는
        /// <see cref="Player.Party"/>에 <c>null</c>로만 남아 누구였는지 알 수가 없다 —
        /// 통째로 갈아엎으면 그 사망 기록이 사라지고 다음 스테이지에서 되살아난다.
        ///
        /// <b>패배 후 재시작이 저절로 맞는 것도 이 시점 선택 덕분이다.</b> 기록은 클리어할 때만
        /// 남으므로, 지고 나서 같은 스테이지를 다시 하면 <b>그 스테이지를 시작할 때의 기록</b>이
        /// 그대로 다시 읽힌다. 되돌리는 코드가 따로 필요 없다.
        /// </summary>
        public void Capture(Player player)
        {
            if (player == null) return;

            HasSnapshot = true;

            HeroHpRatio = player.Combat != null && player.Combat.IsDead
                ? HeroReviveRatio
                : Ratio(player);

            int recorded = 0;
            int skipped = 0;

            foreach (Ally a in player.Party)
            {
                // null 칸은 "빈 편성"이거나 "이미 죽어 지워진 슬롯"이다. 둘 다 기록할 것이 없고,
                // 후자는 여기서 건드리면 안 되는 기존 기록을 이미 갖고 있다.
                if (a == null) continue;

                // 표가 없으면 키가 없어서 기록할 수가 없다 — 그 동료만 조용히 만피로
                // 되살아난다. 로드아웃 배선이 빠졌다는 신호이므로 조용히 넘기지 않는다.
                if (a.Data == null)
                {
                    skipped++;
                    continue;
                }

                bool dead = a.Combat != null && a.Combat.IsDead;
                Record(a.Data, Ratio(a), dead);
                recorded++;
            }

            if (skipped > 0)
                BattleLog.Warn(LogCategory.State,
                    $"동료 {skipped}명이 PartyMemberData 를 안 물고 있어 상태를 못 찍었다 — " +
                    "그 동료만 다음 스테이지에서 만피로 시작한다. PartyLoadout 배선을 확인할 것.", player);

            BattleLog.Log(LogCategory.State,
                $"파티 상태 기록 — 동료 {recorded}명 · 전사 누적 {DeadCount}명 · " +
                $"주인공 체력 {HeroHpRatio:P0}", player);
        }

        /// <summary>
        /// 한 명의 상태를 직접 적는다. <see cref="Capture"/>가 쓰는 통로이자
        /// <b>테스트가 들어오는 이음매</b>다.
        ///
        /// 에디트모드에서는 <c>Awake</c>가 안 돌아 <c>Combat.Die()</c>가 물리에서 터진다 —
        /// 즉 "죽은 동료"를 만들 방법이 없다(<see cref="TagSwapRules"/>가 술어를 받는 것과 같은 이유).
        /// 영구 사망 규칙이 이 시스템의 핵심인데 그것만 검증 못 하면 안 되므로 여기를 열어 둔다.
        ///
        /// <b>죽음은 되돌아가지 않는다.</b> 한 번 죽었다고 적힌 동료는 이후 어떤 기록으로도
        /// 되살아나지 않는다 — 되살아날 수 있으면 그건 A안(영구 사망)이 아니다.
        /// </summary>
        public void Record(PartyMemberData member, float hpRatio, bool dead)
        {
            if (member == null) return;

            HasSnapshot = true;

            if (IsDead(member)) return;

            members[member] = dead
                ? new MemberState(0f, true)
                : new MemberState(Mathf.Clamp(hpRatio, 0.01f, 1f), false);
        }

        /// <summary>
        /// 살아 있는 몸의 체력 비율. <b>0을 돌려주지 않는다</b> — 산 캐릭터를 0으로 복원하면
        /// 체력은 비었는데 <see cref="CombatState"/>는 살아 있는, 아무도 못 죽이는 몸이 된다.
        /// </summary>
        private static float Ratio(Entity e)
        {
            if (e == null || e.Combat == null || e.Combat.Health == null) return 1f;
            return Mathf.Clamp(e.Combat.Health.Ratio, 0.01f, 1f);
        }

        /// <summary>새 런. <see cref="RunProgression.Reset"/>이 부른다.</summary>
        public void Clear()
        {
            members.Clear();
            HeroHpRatio = 1f;
            HasSnapshot = false;
        }
    }
}
