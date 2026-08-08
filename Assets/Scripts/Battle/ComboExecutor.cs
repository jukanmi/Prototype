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
        }

        private IEnumerator Run(Queue<ComboSlot> queue)
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

                if (slot.Data != null && slot.Data.IsCharge)
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
                    $"슬롯 건너뜀 — data {(data == null ? "없음" : data.skillName)} / caster {BattleLog.Name(caster)}", this);
                yield break;
            }

            // 조준은 좌표만 박아 두고, 상대는 시전 순간 SkillState가 그 좌표에서 다시 고른다.
            // 그래서 대상이 그사이 죽어도 별도의 재타겟 경로가 필요 없다(결정 로그 ⑧).
            var ctx = new SkillContext
            {
                data = data,
                caster = caster,
                targetInfo = slot.target,
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
