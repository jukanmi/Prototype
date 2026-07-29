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

            int index = 0;
            while (queue.Count > 0)
            {
                ComboSlot slot = queue.Dequeue();
                BattleLog.Log(LogCategory.Combo,
                    $"── 슬롯 {index++}: {(slot.Data != null ? slot.Data.skillName : "(비어있음)")} / {BattleLog.Name(slot.caster)}", this);

                yield return RunSlot(slot);

                OnSlotConsumed?.Invoke(slot.card);

                if (slotGap > 0f)
                    yield return WaitScaled(slotGap);
            }

            BattleLog.Log(LogCategory.Combo, "<b>콤보 실행 종료</b>", this);

            running = null;
            OnExecuteFinished?.Invoke();
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

            TargetInfo info = slot.target;
            Entity target = info.unit;

            // 대상이 이미 죽었으면 사망 위치 기준 최근접 적으로 재타겟한다(결정 로그 ⑧).
            if (info.type == TargetingType.EnemyUnit && (target == null || target.Combat.IsDead))
            {
                Vector3 deadPos = target != null ? target.transform.position : info.point;
                target = Retarget(deadPos);

                if (target == null)
                {
                    BattleLog.Warn(LogCategory.Combo, $"재타겟 실패 — 살아 있는 적이 없다. {data.skillName} 취소", this);
                    yield break;
                }

                BattleLog.Log(LogCategory.Combo,
                    $"대상 사망 → 최근접 재타겟: {BattleLog.Name(target)} (사망 위치 {deadPos})", this);
                info = TargetInfo.Unit(target);
            }

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
