using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 화면 밖을 오가는 동안 <b>판정을 전부 끄고 배경 뒤로 숨긴다.</b> 진영을 가리지 않는다.
    ///
    /// 원래 <see cref="EnemySpawnGuard"/> 안에 적 전용으로만 있던 부분이다.
    /// 동료도 교대 · 시전으로 화면 밖을 오가게 되면서 같은 억제가 양쪽에 필요해졌고,
    /// 두 벌로 두면 <b>반드시 한쪽만 고쳐진다</b> — 그래서 여기 한 벌만 둔다.
    ///
    /// <b>끄는 이유는 양쪽에 다 있다.</b>
    /// <list type="bullet">
    /// <item>화면 밖의 몸이 맞는다 — 플레이어는 보이지도 않는 것을 때리고 있다.</item>
    /// <item>화면 밖의 몸을 향해 스킬이 나간다 — 조준이 화면 밖으로 새어 헛돈다.</item>
    /// </list>
    ///
    /// <b>붙잡은 수를 센다.</b> 아레나 스폰처럼 벽 연출(<see cref="EnemySpawnGuard"/>)과
    /// 비행(<see cref="EntrancePlayer"/>)이 겹쳐 두 주인이 동시에 억제를 걸 수 있는데,
    /// 먼저 끝난 쪽이 복구해 버리면 남은 연출이 판정을 켠 채로 돈다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EntranceGuard : MonoBehaviour
    {
        /// <summary>
        /// 숨는 동안 정렬 순서에 더하는 값.
        ///
        /// 배경(바닥 · 뒷벽)이 -10000 언저리를 쓰고 캐릭터는 -z·100이라 최저 -300이다.
        /// 그보다 확실히 뒤로 보내려면 한 자릿수 더 큰 음수여야 한다.
        /// </summary>
        public const int HiddenSortingOffset = -30000;

        private Entity body;
        private BeltScrollView view;
        private Collider[] bodyColliders;

        /// <summary>억제를 붙잡고 있는 주인 수. 0이 되는 순간 복구한다.</summary>
        private int holds;

        public bool IsArmed => holds > 0;

        /// <summary>
        /// 억제를 건다. 이미 걸려 있으면 <b>수만 하나 올린다</b> —
        /// 두 주인이 겹쳐도 마지막 하나가 놓을 때까지 유지된다.
        /// </summary>
        public static EntranceGuard Arm(Entity target, bool hide = true)
        {
            if (target == null) return null;

            var guard = target.GetComponent<EntranceGuard>();
            if (guard == null) guard = target.gameObject.AddComponent<EntranceGuard>();

            guard.Resolve(target);
            guard.holds++;

            // 조준 후보에서 뺀다. 판정만 끄면 스킬이 여전히 그쪽으로 나가 헛돈다.
            target.IsTargetable = false;

            // 몸통 콜라이더가 곧 피격 판정이다. 끄면 벽도 통과하는데,
            // 방 밖에서 들어오는 중이니 그게 맞다 — 벽에 걸리면 영영 못 들어온다.
            guard.SetBodyColliders(false);

            if (hide && guard.view != null) guard.view.SortingOffset = HiddenSortingOffset;

            return guard;
        }

        /// <summary>
        /// 정렬만 앞으로 되돌린다. 판정은 아직 꺼진 채다.
        /// 벽에서 걸어 나오는 적이 진입선을 넘는 순간 쓴다 — 몸은 보이지만 아직 연출 구간이다.
        /// </summary>
        public void Reveal()
        {
            if (view != null) view.SortingOffset = 0;
        }

        /// <summary>
        /// 붙잡은 것을 하나 놓는다. 마지막 하나였으면 전부 되돌린다.
        /// <b>두 번 불려도 안전하다.</b>
        /// </summary>
        public void Release()
        {
            if (holds <= 0) return;
            if (--holds > 0) return;

            Restore();
        }

        private void Restore()
        {
            holds = 0;

            Reveal();
            SetBodyColliders(true);

            if (body != null) body.IsTargetable = true;
        }

        private void Awake() => Resolve(GetComponent<Entity>());

        private void Resolve(Entity target)
        {
            if (body == null) body = target != null ? target : GetComponent<Entity>();
            if (view == null) view = GetComponent<BeltScrollView>();
            if (bodyColliders == null) bodyColliders = GetComponents<Collider>();
        }

        /// <summary>
        /// 몸통 콜라이더만 만진다. 자식(<see cref="Attack"/> 히트박스)은 건드리지 않는다 —
        /// 그쪽은 평소에도 꺼져 있고 휘두를 때만 켜지는데, 여기서 강제로 켜면
        /// 연출 직후에 판정이 한 프레임 새어 나간다.
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

        /// <summary>
        /// 파괴 · 씬 언로드로 잘려도 "영영 안 잡히는 몸"을 조준 목록에 남기지 않는다.
        /// <see cref="EnemySpawnGuard"/>가 갖고 있던 안전망을 그대로 옮겼다.
        /// </summary>
        private void OnDisable()
        {
            if (holds > 0) Restore();
        }
    }
}
