using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 보스의 가드 게이지. 평소에는 슈퍼아머(맞아도 안 밀림)이고, 게이지가 0이 되면
    /// 정해진 시간 동안 무방비(가드 브레이크)가 된다. 안 맞고 버티면 자연 회복한다.
    ///
    /// <see cref="Combat"/>이 소유한다. 튜닝값은 프리팹에 저장된 그쪽 인스펙터 필드에서 받는다.
    /// 여기는 <b>수치와 시간만</b> 안다 — 브레이크 순간 경직을 지우거나 이벤트를 쏘는 건 Combat 몫이다.
    /// </summary>
    public sealed class GuardMeter
    {
        private float max;
        private float breakDuration;
        private readonly float defaultDamage;
        private readonly float regenDelay;
        private readonly float regen;

        /// <summary>가드브레이크로 무방비인 남은 시간.</summary>
        private float breakTimer;

        /// <summary>안 맞고 버틴 시간. 이게 다 차면 가드가 자연 회복을 시작한다.</summary>
        private float idleTimer;

        public GuardMeter(float max, float breakDuration, float defaultDamage, float regenDelay, float regen)
        {
            this.max = max;
            this.breakDuration = breakDuration;
            this.defaultDamage = defaultDamage;
            this.regenDelay = regenDelay;
            this.regen = regen;

            Energy = new Energy(EnergyType.Guard, max);
        }

        public Energy Energy { get; }

        /// <summary>가드 시스템을 쓰는 개체인가. 잡몹은 false다.</summary>
        public bool Enabled => max > 0f;

        /// <summary>가드가 깨져 무방비인지. 이 구간에만 경직 · 넉백 · 공중 콤보가 통한다.</summary>
        public bool IsBroken => breakTimer > 0f;

        public float BreakDuration => breakDuration;

        /// <summary>최대치와 브레이크 시간을 덮어쓰고 게이지를 채운다.</summary>
        public void Configure(float newMax, float newBreakDuration)
        {
            max = Mathf.Max(0f, newMax);
            breakDuration = Mathf.Max(0f, newBreakDuration);

            breakTimer = 0f;
            idleTimer = 0f;
            Energy.SetMax(max, refill: true);
        }

        /// <summary>
        /// 브레이크 타이머와 자연 회복. 스케일된 dt로 부른다.
        /// </summary>
        /// <returns>이번 틱에 브레이크가 끝나 가드가 돌아왔으면 true.</returns>
        public bool Tick(float dt)
        {
            if (!Enabled) return false;

            if (breakTimer > 0f)
            {
                breakTimer -= dt;
                if (breakTimer > 0f) return false;

                // 브레이크가 끝나면 가드는 가득 찬 상태로 돌아온다 — 다시 벽이 된다.
                breakTimer = 0f;
                Energy.Recover(Energy.MaxValue);
                return true;
            }

            if (Energy.IsFull) return false;

            // 맞는 동안은 회복하지 않는다. 찔끔찔끔 때리다 말면 처음부터 다시다.
            if (idleTimer > 0f)
            {
                idleTimer -= dt;
                if (idleTimer > 0f) return false;

                // 지연을 넘긴 만큼만 회복에 쓴다. 남은 dt를 버리면 프레임이 길 때
                // (불릿타임 복귀 · 에디터 스텝) 회복이 한 프레임씩 밀린다.
                dt = -idleTimer;
                idleTimer = 0f;
            }

            Energy.Recover(regen * dt);
            return false;
        }

        /// <summary>
        /// 이 타격이 깎는 가드. 브레이크 중에는 더 깎지 않는다 — 이미 바닥이고, 회복 시점은 타이머가 정한다.
        /// </summary>
        /// <param name="loss">실제로 깎인 양(= 몇 대분). 안 깎였으면 0.</param>
        /// <returns>이 타격으로 가드가 깨졌으면 true. 무방비 구간이 여기서 열린다.</returns>
        public bool Drain(in HitData hit, out float loss)
        {
            loss = 0f;
            if (!Enabled || IsBroken) return false;

            loss = LossOf(in hit);
            if (loss <= 0f) return false;

            Energy.Lose(loss);
            idleTimer = regenDelay;

            if (!Energy.IsEmpty) return false;

            breakTimer = breakDuration;
            idleTimer = 0f;
            return true;
        }

        /// <summary>
        /// 이 타격이 깎을 가드량. 명시값이 있으면 그것을(= 몇 대분인지), 없으면 한 대로 친다.
        ///
        /// <b>데미지가 0인 타격은 가드를 깎지 않는다</b> — 패링 반격처럼 "기회"만 주는 판정이
        /// 벽을 대신 허물면 안 된다. 가드만 깎는 타격을 만들려면 <c>guardDamage</c>를 명시하면 된다.
        /// 데미지에 비례시키지 않는 이유: 연타로 깨는 맛을 살리기 위해서다.
        /// </summary>
        private float LossOf(in HitData hit)
        {
            if (hit.guardDamage > 0f) return hit.guardDamage;

            return hit.damageData.damage > 0f ? defaultDamage : 0f;
        }
    }
}
