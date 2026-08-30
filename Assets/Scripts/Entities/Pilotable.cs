using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// "이 몸은 유저가 몰 수 있다"는 표식 + 그 몸에 딸린 조작 수치.
    ///
    /// <b>왜 몸에 있나.</b> 조종사(<see cref="PlayerPilot"/>)는 씬에 하나뿐이고 몸을 갈아탄다.
    /// 그런데 여기 담긴 값은 전부 <b>캐릭터 성능</b>이지 플레이어 성능이 아니다 —
    /// 대시 쿨은 그 캐릭터의 능력이고, 선입력 창은 그 캐릭터의 콤보 모션 길이에 묶인다.
    /// 조종사에 두면 전사로 대시하고 교대한 마법사가 그 쿨을 물려받는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Entity))]
    public class Pilotable : MonoBehaviour
    {
        [Tooltip("대시를 다시 쓸 수 있을 때까지의 시간.")]
        [SerializeField] private float dashCooldown = 0.6f;

        [Tooltip("평타 선입력이 살아 있는 시간. 공격 모션 중에 누른 입력을 이만큼 기억했다가 다음 타로 이어 준다.\n\n" +
                 "너무 짧으면 프레임을 맞춰야 연타가 되고, 너무 길면 한 번 누른 게 두 타로 샌다.\n" +
                 "1타 캔슬 시점(cancelStart)보다 확실히 길게 잡을 것.")]
        [SerializeField] private float attackBufferWindow = 0.25f;

        private float dashTimer;

        public float AttackBufferWindow => attackBufferWindow;

        public bool DashReady => dashTimer <= 0f;

        public void StartDashCooldown() => dashTimer = dashCooldown;

        /// <summary>
        /// 쿨을 흘린다. <b>조종 중인 몸만</b> 흐른다 — 벤치에 내려간 몸은 조종사가 안 부르므로
        /// 쿨이 얼어 있다. 예전에 <c>SetActive(false)</c>로 Update가 통째로 멈추던 것과 같은 동작이다.
        /// </summary>
        public void TickCooldowns(float dt)
        {
            if (dashTimer > 0f) dashTimer -= dt;
        }

        /// <summary>
        /// 표에서 조작 수치를 받는다. <b>0은 "건드리지 않는다"</b>는 뜻이다 —
        /// 표에 안 적힌 값까지 덮으면 프리팹 설정이 조용히 지워진다.
        ///
        /// 여기 든 두 값이 <see cref="PlayerData"/>에서 가장 중요한 항목이다.
        /// 주인공을 고른다는 말의 실질이 대시 쿨과 선입력 창이기 때문이다.
        /// </summary>
        public void ApplyData(PlayerData data)
        {
            if (data == null) return;

            if (data.dashCooldown > 0f) dashCooldown = data.dashCooldown;
            if (data.attackBufferWindow > 0f) attackBufferWindow = data.attackBufferWindow;
        }
    }
}
