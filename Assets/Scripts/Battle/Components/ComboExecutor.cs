using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 큐에 쌓인 슬롯을 순서대로 실행한다.
    /// Control을 건드리지 않고 <b>상태머신을 강탈</b>하는 방식(결정 로그 ②).
    /// 시전자는 몸에 조종사가 없으므로 스킬이 나갈 때까지 스스로 움직이지 않는다.
    /// </summary>
    public class ComboExecutor : MonoBehaviour
    {
        [Tooltip("슬롯 사이 여유. 0이면 후딜이 끝나는 즉시 다음 슬롯.")]
        [SerializeField] private float slotGap = 0.05f;
        [Tooltip("스킬이 끝나지 않을 때 강제로 넘기는 상한.")]
        [SerializeField] private float slotTimeout = 5f;

        private Coroutine running;
        private bool executing;
        public CastDirector Director { get; set; }

        /// <summary>
        /// 슬롯 실행 직전에 재생할 인트로. null이면 컷인 없이 곧바로 스킬로 간다.
        /// 씬에서는 Awake가 자식에서 찾아 꽂고, 테스트는 세터로 페이크를 넣는다.
        /// </summary>
        public ISkillCutin Cutin { get; set; }

        private void Awake()
        {
            if (Cutin == null) Cutin = GetComponentInChildren<ISkillCutin>(true);
        }

        public bool IsRunning => executing;

        /// <summary>
        /// 슬롯 사이 여유. 예측기가 체공 시간을 셀 때 같은 값을 봐야
        /// "예측은 공중인데 실제로는 이미 다운"이 안 생긴다.
        /// </summary>
        public float SlotGap => slotGap;

        public event Action OnExecuteStarted;
        public event Action OnExecuteFinished;
        /// <summary>실행이 끝난 카드. Discard로 옮기는 쪽이 구독한다.</summary>
        public event Action<ComboCard> OnSlotConsumed;

        public void Execute(Queue<ComboSlot> queue)
        {
            if (queue == null || queue.Count == 0) return;
            if (IsRunning) return;

            executing = true;
            running = StartCoroutine(Run(queue));
        }

        public void Abort()
        {
            if (Director != null && Director.IsResolving) Director.Abort();
            if (running != null) StopCoroutine(running);
            running = null;
            executing = false;

            // StopCoroutine으로 잘린 코루틴은 finally가 돌지 않는다.
            // 컷인이 내려놓은 TimeControl.Scale을 여기서 되돌리지 않으면 게임이 영구 정지한다.
            Cutin?.Cancel();
        }

        private void OnDisable() => Abort();

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
            if (Director != null)
            {
                OnExecuteStarted?.Invoke();
                var cards = new List<ComboCard>();
                var requests = new List<CastRequest>();
                while (queue.Count > 0)
                {
                    ComboSlot slot = queue.Dequeue();
                    cards.Add(slot.card);
                    requests.Add(new CastRequest(slot.caster, slot.Data, slot.target,
                        slot.damageScale > 0f ? slot.damageScale : 1f, true, slot.targetEntity));
                }
                yield return Director.Resolve(requests, i => OnSlotConsumed?.Invoke(cards[i]));
                running = null;
                executing = false;
                OnExecuteFinished?.Invoke();
                yield break;
            }
            BattleLog.Log(LogCategory.Combo, $"<b>콤보 실행 시작</b> — {queue.Count}슬롯", this);
            OnExecuteStarted?.Invoke();

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

                // 재타겟 경로는 사라졌다. 조준이 좌표만 남기므로 대상이 그사이 죽어도
                // 시전 순간 SkillState.ResolveTarget이 그 좌표에서 다시 고른다(결정 로그 ⑧).

                // 컷인은 스킬보다 먼저다. 여기서 시간이 멈추고, 끝나야 다시 흐른다.
                IEnumerator intro = PlayCutin(in slot);
                if (intro != null) yield return intro;

                yield return RunSlot(slot);

                OnSlotConsumed?.Invoke(slot.card);

                if (slotGap > 0f)
                    yield return WaitScaled(slotGap);
            }

            BattleLog.Log(LogCategory.Combo, "<b>콤보 실행 종료</b>", this);

            running = null;
            executing = false;
            OnExecuteFinished?.Invoke();
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

            // 꺼진 몸에 상태를 걸면 Update가 안 돌아 IsFinished가 영영 안 서고,
            // slotTimeout(초 단위)만큼 콤보가 통째로 멎는다. 조용히 넘기지 않고 소리를 낸다.
            if (!caster.gameObject.activeInHierarchy)
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"{data.skillName} 건너뜀 — {BattleLog.Name(caster)}가 꺼져 있다", this);
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
                damageScale = slot.damageScale,
            };

            caster.IsCommanded = true;

            IState state = data.CreateState(in ctx);
            caster.BeginSkill(state);

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

            caster.EndSkill(state);
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
