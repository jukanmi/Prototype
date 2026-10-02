// 진입 연출 파이프라인 — 명세 → 규칙 → 감독 → 재생, 그리고 그동안 막는 가드.
//   EntranceSpec      무엇을 어떻게 등장시킬지
//   EntranceRules     명세를 실제 좌표 · 시간으로 푸는 표
//   EntranceDirector  전체를 모는 쪽
//   EntrancePlayer    한 번의 연출을 실제로 재생
//   EntranceGuard     연출 중 조작 · 전투를 막는다
// 다섯이 한 흐름이라 따로 읽을 일이 없다.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ EntrancePlayer ═══════════════════════════════════════════

    /// <summary>
    /// 몸을 화면 밖과 착지점 사이로 실제로 밀어 넣는 구동기. 한 번에 하나만 돈다.
    ///
    /// <b>걷지 않고 밀어 넣는다.</b> 방은 사방이 콜라이더로 막혀 있어서
    /// (<see cref="WaveSpawnPlanner.SpawnInset"/> 주석) 바깥에서 <see cref="Physics.Move"/>로는
    /// 영영 못 들어온다. <see cref="EntranceGuard"/>가 몸통 콜라이더를 꺼 둔 상태이므로
    /// 벽을 통과해 들어오는 것이 맞다.
    ///
    /// <b>LateUpdate에서 자리를 덮어쓴다.</b> 같은 프레임의 Physics.FixedUpdate가
    /// 중력을 먹여 y를 끌어내리는데, 여기서 매 프레임 되돌리지 않으면 화면 밖에서
    /// 바닥 없는 허공을 떨어지며 들어온다 — 시작점은 방 밖이라 발판이 없다.
    /// <see cref="BeltScrollView"/>보다 먼저 돌아야 스프라이트가 한 프레임 늦지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EntrancePlayer : MonoBehaviour
    {
        private Entity body;
        private Physics physics;
        private EntranceGuard guard;

        private Vector3 startWorld;
        private Vector3 landingWorld;
        private Vector3 landingGround;
        private Vector3 facing;
        private float height;

        private float duration;
        private float elapsed;
        private bool unscaled;
        private bool lockedControl;
        private Action onArrive;

        public bool IsRunning { get; private set; }

        /// <summary>연출을 시작한다. 이미 돌고 있으면 앞엣것을 취소하고 갈아탄다.</summary>
        public void Begin(Entity owner, in EntranceSpec spec)
        {
            if (owner == null) return;

            if (IsRunning) Cancel();

            body = owner;
            physics = owner.Physics;

            if (physics == null)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{BattleLog.Name(owner)}에 Physics가 없다 — 등장 연출을 건너뛴다", this);
                spec.onArrive?.Invoke();
                return;
            }

            landingGround = spec.landing;
            facing = spec.facing;
            height = Mathf.Max(0f, spec.height);
            duration = EntranceRules.ClampSeconds(spec.seconds);
            unscaled = spec.unscaled;
            onArrive = spec.onArrive;
            elapsed = 0f;

            // 두 끝점의 <b>월드</b> 좌표를 여기서 확정한다. 발판 높이가 자리마다 다를 수 있어
            // 지상 좌표만으로는 보간이 바닥을 뚫거나 뜬다. Teleport가 그 계산을 이미 갖고 있다.
            physics.Teleport(landingGround, height);
            landingWorld = physics.Transform.position;

            physics.Teleport(spec.start, height);
            startWorld = physics.Transform.position;

            // 지면 아래는 Teleport 로 못 잡는다(음수 높이를 0으로 물린다). 잡은 뒤에 내린다.
            startWorld.y -= Mathf.Max(0f, spec.startDepth);

            // 억제가 <b>먼저</b>다. 조준·판정을 켠 채로 한 프레임이라도 화면 밖에 서 있으면
            // 그 프레임에 맞거나, 그쪽으로 스킬이 나간다.
            if (spec.guard) guard = EntranceGuard.Arm(body);

            lockedControl = spec.lockControl;
            if (lockedControl) body.IsEntering = true;

            // 진행 방향을 보고 들어온다. 등을 보이고 날아오면 무엇이 오는지 안 읽힌다.
            Vector3 travel = landingGround - spec.start;
            physics.Face(facing.sqrMagnitude > 0.0001f ? facing : travel);

            IsRunning = true;
            enabled = true;
        }

        /// <summary>
        /// <b>지금 즉시</b> 착지시킨다. 남은 시간을 건너뛰고 도착 처리를 그대로 밟는다 —
        /// 콜백도 부른다.
        ///
        /// 연출이 끝나기를 기다릴 수 없는 입력이 있다. U키 카드가 그렇다:
        /// "0.35초 뒤에 다시 누르라"는 답이 될 수 없으므로, 부르는 쪽이 여기서 앞당긴다.
        /// </summary>
        public void Finish()
        {
            if (!IsRunning) return;
            Arrive();
        }

        /// <summary>
        /// 착지시키지 않고 멈춘다. <paramref name="restore"/>가 false면 억제·잠금을 그대로 둔다 —
        /// 곧바로 다음 연출이 이어 걸릴 때 한 프레임 판정이 새는 것을 막는다.
        /// <see cref="EntranceSpec.onArrive"/>는 부르지 않는다.
        /// </summary>
        public void Cancel(bool restore = true)
        {
            if (!IsRunning) return;

            IsRunning = false;
            enabled = false;
            onArrive = null;

            if (!restore) return;

            Unwind();
        }

        private void LateUpdate()
        {
            if (!IsRunning) return;

            elapsed += unscaled ? Time.unscaledDeltaTime : TimeControl.DeltaTime;

            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            if (t >= 1f)
            {
                Arrive();
                return;
            }

            // 중력이 이번 프레임에 먹인 하강을 되돌린다. 상승은 건드리지 않는다(StopFall 규칙).
            physics.StopFall();

            Vector3 p = EntranceRules.Sample(startWorld, landingWorld, t);

            physics.Transform.position = p;
            if (physics.Rigidbody != null) physics.Rigidbody.position = p;
        }

        private void Arrive()
        {
            IsRunning = false;
            enabled = false;

            // 마지막 한 번은 Teleport다. 접지·관성·낙하속도·PhysicsState를 통째로 정리하는 건
            // 이쪽뿐이라, 직접 쓴 좌표로 끝내면 넉백 속도나 Aerial 상태를 물고 착지한다.
            physics.Teleport(landingGround, height);

            if (facing.sqrMagnitude > 0.0001f) physics.Face(facing);

            Unwind();

            // 뒷정리가 <b>전부 끝난 다음</b> 부른다 — 콜백이 그 자리에서 다음 연출을
            // 이어 걸거나 몸을 내려도(SetActive(false)) 안전해야 한다.
            Action callback = onArrive;
            onArrive = null;
            callback?.Invoke();
        }

        /// <summary>잠금과 억제를 되돌린다. 두 번 불려도 안전하다.</summary>
        private void Unwind()
        {
            if (lockedControl && body != null) body.IsEntering = false;
            lockedControl = false;

            if (guard != null) guard.Release();
            guard = null;
        }

        /// <summary>
        /// 연출 도중 몸이 꺼지거나 파괴돼도 잠금을 남기지 않는다.
        /// 남기면 다시 섰을 때 <b>영영 조작이 안 되는 몸</b>이 된다.
        /// </summary>
        private void OnDisable()
        {
            if (!IsRunning) return;

            IsRunning = false;
            onArrive = null;
            Unwind();
        }
    }

    // ══ EntranceGuard ═══════════════════════════════════════════

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
