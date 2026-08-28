using System;
using UnityEngine;

namespace Prototype
{
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
}
