using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벽에서 걸어 나오는 동안 <b>판정을 전부 끄고 벽 뒤에 숨긴다.</b>
    /// 진입이 끝나면 스스로 원상복구하고 사라진다.
    ///
    /// <b>판정을 꺼야 하는 이유가 양쪽 다 있다.</b>
    /// <list type="bullet">
    /// <item>벽 안쪽에 서 있는 적이 원거리 공격을 날리면, 플레이어는 보이지도 않는 곳에서 맞는다.</item>
    /// <item>플레이어가 벽 쪽으로 광역기를 쓰면 아직 안 나온 적이 통째로 잡힌다 —
    /// 예고를 보고 미리 깔아 두는 것이 최적해가 되어 벽 연출이 무의미해진다.</item>
    /// </list>
    ///
    /// 시각 처리는 <b>정렬 순서</b>로 한다. 진입 중에는 배경 벽보다 뒤에 그려지다가,
    /// 진입선을 넘는 순간 앞으로 올라온다. 페이드인보다 이쪽이 벨트스크롤 감각에 훨씬 잘 맞는다.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemySpawnGuard : MonoBehaviour
    {
        /// <summary>
        /// 진입 중 정렬 순서에 더하는 값.
        ///
        /// 배경(바닥 · 뒷벽)이 -10000 언저리를 쓰고 캐릭터는 -z·100 이라 최저 -300이다.
        /// 그보다 확실히 뒤로 보내려면 한 자릿수 더 큰 음수여야 한다.
        /// </summary>
        public const int HiddenSortingOffset = -30000;

        private Enemy enemy;
        private EnemyControl control;
        private BeltScrollView view;
        private Collider[] bodyColliders;

        private SpawnWall wall;
        private float entryLine;
        private bool crossed;
        private bool armed;

        /// <summary>진입 연출이 아직 도는 중인가. 라운드 클리어 판정이 이 값을 센다.</summary>
        public bool IsEntering => armed;

        /// <summary>
        /// 소환 직후 <see cref="EnemySpawnService"/>가 부른다.
        /// <see cref="EnemyControl.BeginSpawnEntry"/>보다 <b>먼저</b> 불러야 한다 —
        /// 첫 프레임에 판정이 켜져 있으면 벽 안쪽에서 한 대 맞을 수 있다.
        /// </summary>
        public void Arm(SpawnWall fromWall, float wallEntryLine)
        {
            wall = fromWall;
            entryLine = wallEntryLine;
            crossed = false;
            armed = true;

            Resolve();

            // 조준 후보에서 뺀다. 판정만 끄면 스킬이 여전히 그쪽으로 나가 헛돈다.
            enemy.IsTargetable = false;

            // 몸통 콜라이더가 곧 피격 판정이다. 끄면 벽도 통과하는데, 벽에서 나오는 중이니 맞다.
            SetBodyColliders(false);

            if (view != null) view.SortingOffset = HiddenSortingOffset;
        }

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (enemy == null) enemy = GetComponent<Enemy>();
            if (control == null) control = GetComponent<EnemyControl>();
            if (view == null) view = GetComponent<BeltScrollView>();
            if (bodyColliders == null) bodyColliders = GetComponents<Collider>();
        }

        private void Update()
        {
            if (!armed) return;

            // 진입선을 넘었으면 벽 앞으로 나온다. 판정은 아직 안 켠다 —
            // 목표 셀에 닿을 때까지는 여전히 연출 구간이다.
            if (!crossed && ArenaSpawnPlanner.HasCrossedEntryLine(wall, transform.position, entryLine))
            {
                crossed = true;
                if (view != null) view.SortingOffset = 0;
            }

            // 진입이 끝나는 시점의 유일한 판단 근거. AI가 몸을 가져가는 순간과 같아야 한다.
            if (control != null && control.IsEntering) return;

            Release();
        }

        /// <summary>판정을 되돌리고 물러난다. 두 번 불려도 안전하다.</summary>
        public void Release()
        {
            if (!armed) return;
            armed = false;

            if (view != null) view.SortingOffset = 0;

            SetBodyColliders(true);
            if (enemy != null) enemy.IsTargetable = true;

            BattleLog.Log(LogCategory.State, $"{name} 진입 완료 — 판정 복구", this);

            Destroy(this);
        }

        /// <summary>
        /// 몸통 콜라이더만 만진다. 자식(<see cref="Attack"/> 히트박스)은 건드리지 않는다 —
        /// 그쪽은 평소에도 꺼져 있고 휘두를 때만 켜지는데, 여기서 강제로 켜면
        /// 진입 직후에 판정이 한 프레임 새어 나간다.
        /// </summary>
        private void SetBodyColliders(bool on)
        {
            if (bodyColliders == null) return;

            for (int i = 0; i < bodyColliders.Length; i++)
            {
                Collider c = bodyColliders[i];
                if (c == null || c.isTrigger) continue;   // 트리거는 히트박스다
                c.enabled = on;
            }
        }

        private void OnDisable()
        {
            // 파괴·씬 언로드로 잘려도 조준 목록에 "영영 안 잡히는 적"을 남기지 않는다.
            if (armed && enemy != null) enemy.IsTargetable = true;
        }
    }
}
