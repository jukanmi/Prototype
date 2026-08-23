using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동시 공격권. <b>한 화면에 적이 여섯이어도 동시에 휘두르는 적은 두셋뿐</b>이게 만든다.
    ///
    /// 이게 없으면 벨트스크롤이 성립하지 않는다. 적 AI는 각자 사거리만 보고 판단하므로,
    /// 여섯이 둘러싸면 여섯이 같은 프레임에 휘두르고 그 사이에는 <b>피할 수 있는 틈이 없다</b> —
    /// 난이도가 아니라 설계 실패다. 토큰을 쥔 적만 공격을 시도하고 나머지는 사거리 안에서
    /// 기다리게 하면, 몰려 있는 그림은 그대로 두고 들어오는 타격만 일정하게 유지된다.
    ///
    /// <b>임대(lease)</b>로 준다. 반납을 놓치는 경로가 반드시 생기기 때문이다 —
    /// 공격 도중에 죽거나, 씬이 내려가거나, 상태머신이 공격을 거절하거나. 반납이 한 번
    /// 새면 그 토큰은 영영 안 돌아오고, 증상은 "어느 순간부터 적이 아무도 안 때린다"라
    /// 원인을 짚기가 대단히 어렵다. 만료가 그 경로를 전부 덮는다.
    ///
    /// 순수 C#이다 — 시간은 부르는 쪽이 넘긴다.
    /// </summary>
    public sealed class AttackTokenPool
    {
        /// <summary>기본 허용치. 스테이지가 <see cref="SetCapacity"/>로 덮는다.</summary>
        public const int DefaultCapacity = 2;

        /// <summary>평타용 임대 기간. 평타 한 싸이클(선딜+후딜)보다 넉넉하다.</summary>
        public const float BasicLease = 2.5f;

        /// <summary>특수 행동용 임대 기간. 돌진은 예고+발동+후딜로 2초에 가깝다.</summary>
        public const float SpecialLease = 4f;

        /// <summary>
        /// 쥔 주체 → 만료 시각.
        ///
        /// 키가 <c>object</c>인 이유는 이 풀이 <b>순수 C#</b>이기 때문이다 —
        /// 유니티의 인스턴스 ID(<c>EntityId</c>)는 앞으로 int로 표현되지 않을 예정이라
        /// 숫자로 굳혀 두면 언젠가 통째로 갈아엎어야 한다. 참조 하나면 충분한 일이다.
        /// </summary>
        private readonly Dictionary<object, float> leases = new Dictionary<object, float>();

        /// <summary>만료 정리용. 매 프레임 도는 자리라 할당을 남기지 않는다.</summary>
        private readonly List<object> expired = new List<object>();

        public int Capacity { get; private set; } = DefaultCapacity;

        /// <summary>0 이하는 받지 않는다 — 아무도 공격하지 못하는 방이 조용히 만들어진다.</summary>
        public void SetCapacity(int value) => Capacity = Mathf.Max(1, value);

        /// <summary>
        /// 공격권을 얻는다. <b>이미 쥔 쪽은 언제나 성공</b>이다 —
        /// 연타 도중에 정원이 줄어들었다고 휘두르던 팔이 멈추면 그게 더 이상하다.
        /// </summary>
        /// <param name="holder">쥐는 주체. 보통 적을 모는 컴포넌트 자신이다.</param>
        /// <param name="now">지금 시각(초).</param>
        /// <param name="lease">이 시간이 지나면 자동 반납된다.</param>
        public bool TryAcquire(object holder, float now, float lease)
        {
            Prune(now);

            if (holder == null) return false;

            if (leases.ContainsKey(holder))
            {
                leases[holder] = now + Mathf.Max(0.1f, lease);
                return true;
            }

            if (leases.Count >= Capacity) return false;

            leases[holder] = now + Mathf.Max(0.1f, lease);
            return true;
        }

        /// <summary>공격이 끝났다. 쥔 적 없는 주체를 반납해도 아무 일도 일어나지 않는다.</summary>
        public void Release(object holder)
        {
            if (holder != null) leases.Remove(holder);
        }

        /// <summary>지금 이 주체가 공격권을 쥐고 있는가.</summary>
        public bool Holds(object holder, float now)
            => holder != null && leases.TryGetValue(holder, out float until) && until > now;

        /// <summary>만료되지 않은 공격권의 수. 디버그 HUD와 테스트가 읽는다.</summary>
        public int ActiveCount(float now)
        {
            Prune(now);
            return leases.Count;
        }

        /// <summary>전부 반납. 웨이브가 바뀌거나 씬이 내려갈 때.</summary>
        public void Clear() => leases.Clear();

        /// <summary>정원까지 기본값으로 되돌린다. 씬 경계에서 부른다.</summary>
        public void ResetAll()
        {
            Clear();
            Capacity = DefaultCapacity;
        }

        private void Prune(float now)
        {
            if (leases.Count == 0) return;

            expired.Clear();

            foreach (KeyValuePair<object, float> pair in leases)
                if (pair.Value <= now) expired.Add(pair.Key);

            for (int i = 0; i < expired.Count; i++)
                leases.Remove(expired[i]);
        }
    }

    /// <summary>
    /// 전투 한 판이 공유하는 공격권 한 벌. <see cref="BattleRegistry"/>와 같은 자리에 있는
    /// 전역 상태이고, 같은 이유로 <b>씬 경계에서 반드시 비운다</b> —
    /// Domain Reload 가 꺼져 있으면 지난 판의 임대가 그대로 살아남아
    /// 새 스테이지 첫 몇 초 동안 아무도 공격하지 않는다.
    /// </summary>
    public static class EnemyAttackTokens
    {
        public static AttackTokenPool Pool { get; } = new AttackTokenPool();

        /// <summary>지금 시각. 불릿타임에 얼면 안 되므로 스케일 안 된 시간을 쓴다.</summary>
        public static float Now => Time.unscaledTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pool.ResetAll();
    }
}
