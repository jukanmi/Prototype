using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 큐에 쌓인 슬롯을 순서대로 실행한다.
    /// Control을 건드리지 않고 <b>상태머신을 강탈</b>하는 방식(결정 로그 ②).
    /// AllyControl의 BT는 IsCommanded 동안 정지한다.
    /// </summary>
    public class ComboExecutor : MonoBehaviour
    {
        [Tooltip("슬롯 사이 여유. 0이면 후딜이 끝나는 즉시 다음 슬롯.")]
        [SerializeField] private float slotGap = 0.05f;
        [Tooltip("스킬이 끝나지 않을 때 강제로 넘기는 상한.")]
        [SerializeField] private float slotTimeout = 5f;

        private Coroutine running;

        /// <summary>
        /// 슬롯 실행 직전에 재생할 인트로. null이면 컷인 없이 곧바로 스킬로 간다.
        /// 씬에서는 Awake가 자식에서 찾아 꽂고, 테스트는 세터로 페이크를 넣는다.
        /// </summary>
        public ISkillCutin Cutin { get; set; }

        private void Awake()
        {
            if (Cutin == null) Cutin = GetComponentInChildren<ISkillCutin>(true);
        }

        public bool IsRunning => running != null;

        public event Action OnExecuteStarted;
        public event Action OnExecuteFinished;
        /// <summary>실행이 끝난 카드. Discard로 옮기는 쪽이 구독한다.</summary>
        public event Action<ComboCard> OnSlotConsumed;

        public void Execute(Queue<ComboSlot> queue)
        {
            if (queue == null || queue.Count == 0) return;
            if (IsRunning) return;

            running = StartCoroutine(Run(queue));
        }

        public void Abort()
        {
            if (running != null) StopCoroutine(running);
            running = null;

            // StopCoroutine으로 잘린 코루틴은 finally가 돌지 않는다.
            // 컷인이 내려놓은 TimeControl.Scale을 여기서 되돌리지 않으면 게임이 영구 정지한다.
            Cutin?.Cancel();
        }

        /// <summary>
        /// 이 슬롯을 실제로 발동할 수 있는지. 컷인을 띄울지도 이 판정을 따른다 —
        /// 발동하지 않을 슬롯에 인트로만 뜨면 유령 연출이 된다.
        /// </summary>
        public static bool CanRunSlot(in ComboSlot slot)
        {
            if (slot.Data == null) return false;
            if (slot.caster == null) return false;
            return !slot.caster.Combat.IsDead;
        }

        /// <summary>
        /// 컷인을 시작하고 대기용 열거자를 돌려준다. 컷인이 없으면 null.
        ///
        /// <b>이터레이터가 아니다</b> — 이 메서드를 부르는 순간 <see cref="ISkillCutin.Play"/>가
        /// 실행돼야 한다. 이터레이터로 만들면 호출자가 펌프하기 전까지 아무 일도 일어나지 않아,
        /// 스케줄러 없이 도는 에디트모드 테스트에서 컷인이 통째로 사라진다.
        /// </summary>
        private IEnumerator PlayCutin(in ComboSlot slot)
        {
            return Cutin?.Play(slot.caster, slot.Data);
        }

        /// <summary>
        /// 큐를 순서대로 소화한다. <see cref="Execute"/>가 코루틴으로 돌린다.
        /// public인 이유는 에디트모드 테스트가 스케줄러 없이 직접 펌프하기 위해서다
        /// (<see cref="RecentHitEnemyHUD.TickExpiry"/>와 같은 취지).
        /// </summary>
        public IEnumerator Run(Queue<ComboSlot> queue)
        {
            BattleLog.Log(LogCategory.Combo, $"<b>콤보 실행 시작</b> — {queue.Count}슬롯", this);
            OnExecuteStarted?.Invoke();

            // 차징 슬롯은 시작만 하고 큐를 막지 않는다. 전부 끝난 뒤 순서대로 터뜨린다.
            var pending = new List<PendingCharge>();

            int index = 0;
            while (queue.Count > 0)
            {
                ComboSlot slot = queue.Dequeue();
                BattleLog.Log(LogCategory.Combo,
                    $"── 슬롯 {index++}: {(slot.Data != null ? slot.Data.skillName : "(비어있음)")} / {BattleLog.Name(slot.caster)}", this);

                if (!CanRunSlot(in slot))
                {
                    BattleLog.Warn(LogCategory.Combo,
                        $"슬롯 건너뜀 — data {(slot.Data == null ? "없음" : slot.Data.skillName)} / caster {BattleLog.Name(slot.caster)}", this);

                    // 발동 못 해도 카드는 버린 더미로 보낸다. 안 그러면 덱에서 증발한다.
                    OnSlotConsumed?.Invoke(slot.card);

                    // 발동하지 않은 슬롯 때문에 콤보가 멈칫할 이유가 없어 slotGap은 건너뛴다.
                    continue;
                }

                // 대상이 이미 죽었으면 사망 위치 기준 최근접 적으로 재타겟한다(결정 로그 ⑧).
                // 컷인 <b>앞</b>에서 확정해야 한다 — 재타겟 실패로 취소될 슬롯에 컷인부터 뜨면
                // 화면을 0.55초 얼렸다가 아무 일도 없이 넘어가는 유령 연출이 된다.
                // 차징 슬롯도 이 루프를 그대로 지나므로 동일하게 재타겟을 받는다.
                if (slot.target.type == TargetingType.EnemyUnit &&
                    (slot.target.unit == null || slot.target.unit.Combat.IsDead))
                {
                    Entity deadTarget = slot.target.unit;
                    Vector3 deadPos = deadTarget != null ? deadTarget.transform.position : slot.target.point;
                    Entity retargeted = Retarget(deadPos);

                    if (retargeted == null)
                    {
                        BattleLog.Warn(LogCategory.Combo, $"재타겟 실패 — 살아 있는 적이 없다. {slot.Data.skillName} 취소", this);

                        // 발동 못 해도 카드는 버린 더미로 보낸다. 안 그러면 덱에서 증발한다.
                        OnSlotConsumed?.Invoke(slot.card);

                        // 발동하지 않은 슬롯 때문에 콤보가 멈칫할 이유가 없어 slotGap은 건너뛴다.
                        continue;
                    }

                    BattleLog.Log(LogCategory.Combo,
                        $"대상 사망 → 최근접 재타겟: {BattleLog.Name(retargeted)} (사망 위치 {deadPos})", this);
                    slot.target = TargetInfo.Unit(retargeted);
                }

                // 컷인은 스킬보다 먼저다. 여기서 시간이 멈추고, 끝나야 다시 흐른다.
                IEnumerator intro = PlayCutin(in slot);
                if (intro != null) yield return intro;

                if (slot.Data.IsCharge)
                {
                    StartCharge(slot, pending);
                    OnSlotConsumed?.Invoke(slot.card);
                    continue;   // 대기하지 않는다 — 뒤 슬롯이 그대로 이어진다
                }

                yield return RunSlot(slot);

                OnSlotConsumed?.Invoke(slot.card);

                if (slotGap > 0f)
                    yield return WaitScaled(slotGap);
            }

            if (pending.Count > 0)
                yield return ReleaseCharges(pending);

            BattleLog.Log(LogCategory.Combo, "<b>콤보 실행 종료</b>", this);

            running = null;
            OnExecuteFinished?.Invoke();
        }

        /// <summary>모으기를 시작한 차징 슬롯. 큐가 빌 때까지 붙잡아 둔다.</summary>
        private struct PendingCharge
        {
            public Ally caster;
            public ChargeSkillState state;
            public SkillData data;
        }

        private void StartCharge(ComboSlot slot, List<PendingCharge> pending)
        {
            SkillData data = slot.Data;
            Ally caster = slot.caster;

            if (caster == null || caster.Combat.IsDead)
            {
                BattleLog.Warn(LogCategory.Combo, $"차징 건너뜀 — 시전자 없음 ({data.skillName})", this);
                return;
            }

            var ctx = new SkillContext
            {
                data = data,
                caster = caster,
                target = slot.target.unit,
                targetInfo = slot.target,
                isBulletTime = true,
                comboIndex = 0,
            };

            // 모으는 동안에도 BT는 멈춰 있어야 한다. 해제까지 지휘 상태를 유지한다.
            caster.IsCommanded = true;

            IState state = data.CreateState(in ctx);
            caster.StateMachine.ForceChangeState(state);

            if (state is ChargeSkillState charge)
            {
                pending.Add(new PendingCharge { caster = caster, state = charge, data = data });
                BattleLog.Log(LogCategory.Combo,
                    $"  └ <color=#FFD166>차징 시작</color> {data.skillName} — 남은 슬롯이 끝나면 터진다", this);
            }
            else
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"{data.skillName}은 Charge 유형인데 ChargeSkillState가 아니다", this);
            }
        }

        /// <summary>배치 순서대로 차징을 해제하고 전부 끝날 때까지 기다린다.</summary>
        private IEnumerator ReleaseCharges(List<PendingCharge> pending)
        {
            BattleLog.Log(LogCategory.Combo, $"<b>차징 해제</b> — {pending.Count}건", this);

            for (int i = 0; i < pending.Count; i++)
            {
                PendingCharge p = pending[i];

                if (p.caster == null || p.caster.Combat.IsDead)
                {
                    BattleLog.Warn(LogCategory.Combo, $"{p.data.skillName} 차징 무산 — 시전자 사망", this);
                    continue;
                }

                // 사망 등으로 상태를 빼앗겼으면 터뜨릴 게 없다.
                if (p.caster.StateMachine.CurState != p.state)
                {
                    BattleLog.Warn(LogCategory.Combo, $"{p.data.skillName} 차징 무산 — 상태가 바뀌었다", this);
                    continue;
                }

                p.state.Release();

                float elapsed = 0f;
                while (elapsed < slotTimeout)
                {
                    if (p.caster.Combat.IsDead) break;
                    if (p.state.IsFinished) break;
                    if (p.caster.StateMachine.CurState != p.state) break;

                    elapsed += TimeControl.DeltaTime;
                    yield return null;
                }

                p.caster.IsCommanded = false;

                if (p.caster.StateMachine.CurState == p.state && !p.caster.Combat.IsDead)
                    p.caster.StateMachine.ForceChangeState(p.caster.IdleState);

                if (slotGap > 0f)
                    yield return WaitScaled(slotGap);
            }
        }

        private IEnumerator RunSlot(ComboSlot slot)
        {
            SkillData data = slot.Data;
            Ally caster = slot.caster;

            if (data == null || caster == null || caster.Combat.IsDead)
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"RunSlot 진입 직전 무효화 — data {(data == null ? "없음" : data.skillName)} / caster {BattleLog.Name(caster)}", this);
                yield break;
            }

            // 대상 확정(재타겟 포함)은 이제 호출자(Run 루프)가 컷인보다 먼저 끝낸다 —
            // RunSlot에 들어온 시점에는 slot.target이 이미 살아 있는 대상을 가리킨다.
            TargetInfo info = slot.target;
            Entity target = info.unit;

            var ctx = new SkillContext
            {
                data = data,
                caster = caster,
                target = target,
                targetInfo = info,
                isBulletTime = true,
                comboIndex = 0,
            };

            caster.IsCommanded = true;

            IState state = data.CreateState(in ctx);
            caster.StateMachine.ForceChangeState(state);

            float elapsed = 0f;
            var skillState = state as SkillState;

            while (elapsed < slotTimeout)
            {
                if (caster.Combat.IsDead) break;
                if (skillState != null && skillState.IsFinished) break;
                if (caster.StateMachine.CurState != state) break;   // 사망 등으로 관통당함

                elapsed += TimeControl.DeltaTime;
                yield return null;
            }

            caster.IsCommanded = false;

            if (elapsed >= slotTimeout)
                BattleLog.Warn(LogCategory.Combo, $"{data.skillName} 타임아웃 {slotTimeout:0.#}s — 강제로 다음 슬롯", this);

            if (caster.StateMachine.CurState == state && !caster.Combat.IsDead)
                caster.StateMachine.ForceChangeState(caster.IdleState);
        }

        /// <summary>사망 위치에서 가장 가까운 살아 있는 적을 새 대상으로 잡는다.</summary>
        private Entity Retarget(Vector3 deadPos)
        {
            return BattleRegistry.NearestEnemy(deadPos);
        }

        private IEnumerator WaitScaled(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += TimeControl.DeltaTime;
                yield return null;
            }
        }
    }
}
