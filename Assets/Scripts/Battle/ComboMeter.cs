using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// "지금 콤보를 몇 대, 얼마나 넣었나". <b>순수 계산만</b> 한다 —
    /// 유니티에 의존하지 않아 EditMode에서 그대로 돌릴 수 있다
    /// (<see cref="BasicComboRules"/>와 같은 취지).
    ///
    /// 콤보의 경계는 <b>시간</b>이 정한다. 마지막 타격 뒤 <see cref="Window"/>만큼 아무것도
    /// 안 맞으면 그 콤보는 끝난 것으로 보고, 다음 타격이 1타부터 다시 센다.
    /// 상태 사슬(<see cref="CombatState"/>)로 끊지 않는 이유: 다운·기상까지 이어지는
    /// 정상적인 마무리도 사슬 위에서는 "끊긴 것"으로 보여서, 정작 완주한 콤보가 안 잡힌다.
    /// </summary>
    public class ComboMeter
    {
        /// <summary>이 시간 동안 새 타격이 없으면 콤보가 끝난다(초).</summary>
        public float Window { get; set; } = 2.5f;

        /// <summary>콤보가 끝난 뒤 화면에 남겨 두는 시간(초). 마지막 숫자를 읽을 여유다.</summary>
        public float LingerDuration { get; set; } = 1.5f;

        /// <summary>지금까지 맞힌 횟수.</summary>
        public int Hits { get; private set; }

        /// <summary>누적 피해. 방어력 · 보호막까지 적용된 실제 수치가 들어온다.</summary>
        public float Damage { get; private set; }

        /// <summary>첫 타부터 마지막 타까지 걸린 시간(초). 마지막 타 이후 공백은 안 센다.</summary>
        public float Duration { get; private set; }

        /// <summary>마지막 타격 이후 흐른 시간(초).</summary>
        public float SinceLastHit { get; private set; }

        /// <summary>아직 이어지는 중인지. <see cref="Window"/> 안에 있으면 참.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>초당 피해. 한 대만 맞혔으면 시간이 0이라 0을 낸다 — 무한대를 그리지 않는다.</summary>
        public float Dps => Duration > 0.0001f ? Damage / Duration : 0f;

        /// <summary>
        /// 화면에 보일지. 이어지는 중이거나, 끝난 뒤 <see cref="LingerDuration"/> 안이면 보인다.
        /// 한 대도 안 맞혔으면 보이지 않는다.
        /// </summary>
        public bool IsVisible => Hits > 0 && (IsRunning || SinceLastHit < Window + LingerDuration);

        /// <summary>
        /// 사라지는 중의 불투명도(0~1). 콤보가 끝난 순간부터 <see cref="LingerDuration"/>에 걸쳐 0으로 간다.
        /// </summary>
        public float Alpha
        {
            get
            {
                if (Hits <= 0) return 0f;
                if (IsRunning) return 1f;
                if (LingerDuration <= 0f) return 0f;

                float faded = SinceLastHit - Window;
                return Mathf.Clamp01(1f - faded / LingerDuration);
            }
        }

        /// <summary>한 대 들어갔다. <paramref name="damage"/>는 실제로 깎인 양.</summary>
        public void AddHit(float damage)
        {
            if (IsRunning)
            {
                // 첫 타 시점부터의 누적. 이어지는 동안의 공백만 길이에 들어간다.
                Duration += SinceLastHit;
            }
            else
            {
                // 새 콤보. 남아 있던 이전 기록을 여기서 지운다 —
                // Tick에서 지우면 마지막 숫자가 화면에서 사라지는 순간과 엉킨다.
                Hits = 0;
                Damage = 0f;
                Duration = 0f;
                IsRunning = true;
            }

            Hits++;
            Damage += Mathf.Max(0f, damage);
            SinceLastHit = 0f;
        }

        /// <summary>
        /// 시간을 흘린다. <b>스케일된 dt</b>를 넣어야 한다 —
        /// 불릿타임에 머문 시간이 콤보 길이에 들어가면 DPS가 통째로 거짓이 된다.
        /// </summary>
        public void Tick(float dt)
        {
            if (Hits <= 0 || dt <= 0f) return;

            SinceLastHit += dt;

            if (IsRunning && SinceLastHit >= Window)
                IsRunning = false;
        }

        /// <summary>전부 지운다. 씬 전환 · 전투 종료에서 부른다.</summary>
        public void Reset()
        {
            Hits = 0;
            Damage = 0f;
            Duration = 0f;
            SinceLastHit = 0f;
            IsRunning = false;
        }
    }
}
