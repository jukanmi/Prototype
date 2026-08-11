using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 플레이어 캐릭터의 <b>몸</b>. 태그 로스터의 0번 슬롯이고, 나머지 슬롯인
    /// <see cref="Ally"/>와 같은 자격으로 필드에 서고 내려간다.
    ///
    /// 예전에는 여기가 입력과 지휘까지 들고 있었지만 전부 떼어 냈다 —
    /// 태그로 내려가면 <c>SetActive(false)</c>가 되는데 입력이 붙어 있으면
    /// 되돌아올 키까지 함께 죽기 때문이다. 지금 그 일은
    /// <see cref="BattleCommander"/>와 <see cref="TagSwapController"/>가 한다.
    ///
    /// 파티 명단이 아직 여기 남아 있는 것은 덱 구성(<see cref="BulletTimeController"/>)이
    /// 이 배열을 보기 때문이다.
    /// </summary>
    public class Player : Entity
    {
        [Tooltip("동료 4명. 태그 로스터는 이 앞에 플레이어를 붙여 5칸이 된다.")]
        [SerializeField] private Ally[] party = new Ally[4];

        public Ally[] Party => party;

        /// <summary>
        /// <see cref="Ally"/>와 같은 이유로 등록이 Start가 아니라 여기다 —
        /// 태그로 내려간 몸은 꺼져 있으므로 적의 후보에서 빠져야 한다.
        /// </summary>
        private void OnEnable()
        {
            BattleRegistry.RegisterAlly(this);
        }

        private void OnDisable()
        {
            BattleRegistry.Unregister(this);
            ReleaseBody();
        }
    }
}
