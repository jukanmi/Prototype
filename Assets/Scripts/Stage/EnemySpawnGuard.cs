using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벽에서 걸어 나오는 동안 <b>판정을 전부 끄고 벽 뒤에 숨긴다.</b>
    /// 진입이 끝나면 스스로 원상복구하고 사라진다.
    ///
    /// <b>억제 자체는 <see cref="EntranceGuard"/>가 한다.</b> 동료도 교대·시전으로 화면 밖을
    /// 오가게 되면서 같은 억제가 양쪽에 필요해졌고, 두 벌로 두면 반드시 한쪽만 고쳐진다.
    /// 여기 남은 것은 <b>벽 진입선 판정</b> — 언제 벽 앞으로 나와야 하는가뿐이다.
    ///
    /// 시각 처리는 <b>정렬 순서</b>로 한다. 진입 중에는 배경 벽보다 뒤에 그려지다가,
    /// 진입선을 넘는 순간 앞으로 올라온다. 페이드인보다 이쪽이 벨트스크롤 감각에 훨씬 잘 맞는다.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemySpawnGuard : MonoBehaviour
    {
        /// <summary>
        /// 진입 중 정렬 순서에 더하는 값.
        /// 실제 값은 <see cref="EntranceGuard.HiddenSortingOffset"/>이 갖고 있다 —
        /// 이 이름으로 참조하던 코드와 테스트가 있어 창구만 남긴다.
        /// </summary>
        public const int HiddenSortingOffset = EntranceGuard.HiddenSortingOffset;

        private Enemy enemy;
        private EnemyControl control;
        private EntranceGuard guard;

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

            guard = EntranceGuard.Arm(enemy);
        }

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (enemy == null) enemy = GetComponent<Enemy>();
            if (control == null) control = GetComponent<EnemyControl>();
        }

        private void Update()
        {
            if (!armed) return;

            // 진입선을 넘었으면 벽 앞으로 나온다. 판정은 아직 안 켠다 —
            // 목표 셀에 닿을 때까지는 여전히 연출 구간이다.
            if (!crossed && ArenaSpawnPlanner.HasCrossedEntryLine(wall, transform.position, entryLine))
            {
                crossed = true;
                guard?.Reveal();
            }

            // 화면 밖에서 날아 들어오는 중이면 걷기가 아직 시작도 안 했다.
            // 이걸 안 보면 비행 첫 프레임에 control.IsEntering이 false라 그대로 풀려 버린다.
            if (enemy != null && enemy.IsEntering) return;

            // 진입이 끝나는 시점의 유일한 판단 근거. AI가 몸을 가져가는 순간과 같아야 한다.
            if (control != null && control.IsEntering) return;

            Release();
        }

        /// <summary>판정을 되돌리고 물러난다. 두 번 불려도 안전하다.</summary>
        public void Release()
        {
            if (!armed) return;
            armed = false;

            if (guard != null) guard.Release();
            guard = null;

            BattleLog.Log(LogCategory.State, $"{name} 진입 완료 — 판정 복구", this);

            Destroy(this);
        }

        /// <summary>
        /// 파괴·씬 언로드로 잘려도 조준 목록에 "영영 안 잡히는 적"을 남기지 않는다.
        /// <see cref="EntranceGuard"/>도 자기 <c>OnDisable</c>에서 같은 일을 하지만,
        /// 붙잡은 수를 여기서 놓아 줘야 남은 주인이 없을 때 실제로 복구된다.
        /// </summary>
        private void OnDisable()
        {
            if (!armed) return;
            armed = false;

            if (guard != null) guard.Release();
            guard = null;
        }
    }
}
