using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 타격을 <b>없던 일로</b> 만드는 시간 창 셋 — 대시 패링 창, 패링 성공 무적, 교대 등장 무적.
    ///
    /// <see cref="Combat"/>이 소유한다. 여기는 "지금 열려 있나"와 "앞에서 왔나"만 안다.
    /// 흘려 낸 뒤의 반격 · 로그 · 이벤트는 Combat 몫이다.
    /// </summary>
    public sealed class DefenseWindows
    {
        private readonly float parryWindow;
        private readonly float parryAngle;
        private readonly float parrySuccessInvuln;
        private readonly float summonInvuln;

        /// <summary>패링 판정이 열려 있는 남은 시간. 대시가 연다.</summary>
        private float parryTimer;

        /// <summary>패링 성공으로 얻은 무적의 남은 시간. 이 구간은 방향을 보지 않는다.</summary>
        private float parryInvulnTimer;

        /// <summary>교대로 막 서면서 받은 무적의 남은 시간. 이 구간도 방향을 보지 않는다.</summary>
        private float summonInvulnTimer;

        public DefenseWindows(float parryWindow, float parryAngle, float parrySuccessInvuln, float summonInvuln)
        {
            this.parryWindow = parryWindow;
            this.parryAngle = parryAngle;
            this.parrySuccessInvuln = parrySuccessInvuln;
            this.summonInvuln = summonInvuln;
        }

        public bool IsParrying => parryTimer > 0f;
        public bool IsParryInvulnerable => parryInvulnTimer > 0f;
        public bool IsSummonInvulnerable => summonInvulnTimer > 0f;

        public float ParryWindow => parryWindow;
        public float ParryInvulnRemaining => Mathf.Max(0f, parryInvulnTimer);
        public float ParryInvulnDuration => parrySuccessInvuln;
        public float SummonInvulnRemaining => Mathf.Max(0f, summonInvulnTimer);
        public float SummonInvulnDuration => summonInvuln;

        public void Tick(float dt)
        {
            if (parryTimer > 0f) parryTimer -= dt;
            if (parryInvulnTimer > 0f) parryInvulnTimer -= dt;
            if (summonInvulnTimer > 0f) summonInvulnTimer -= dt;
        }

        /// <summary>대시가 패링 창을 연다.</summary>
        public void OpenParry() => parryTimer = parryWindow;

        /// <summary>교대 등장 무적을 건다. 길이가 0이면 false — 무적 없이 그대로 선다.</summary>
        public bool GrantSummon()
        {
            if (summonInvuln <= 0f) return false;

            summonInvulnTimer = summonInvuln;
            return true;
        }

        /// <summary>
        /// 패링 창이 열려 있고 타격이 전방 부채꼴에서 왔으면 막는다.
        /// 막으면 창을 닫고 무적으로 갈아탄다 — 반격이 돌아와도 두 번 성립하지 않는다.
        /// </summary>
        public bool TryParry(Vector3 facing, Vector3 toAttacker)
        {
            if (!IsParrying) return false;
            if (!CombatStateRules.IsFrontal(facing, toAttacker, parryAngle)) return false;

            parryTimer = 0f;
            parryInvulnTimer = parrySuccessInvuln;
            return true;
        }

        /// <summary>셋 다 닫는다. 태그로 내려가는 몸이 쓴다.</summary>
        public void Clear()
        {
            parryTimer = 0f;
            parryInvulnTimer = 0f;
            summonInvulnTimer = 0f;
        }
    }
}
