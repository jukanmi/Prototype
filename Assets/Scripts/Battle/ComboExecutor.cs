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

        /// <summary>
        /// 슬롯 실행 직전에 재생할 인트로. null이면 컷인 없이 곧바로 스킬로 간다.
        /// 씬에서는 Awake가 자식에서 찾아 꽂고, 테스트는 세터로 페이크를 넣는다.
        /// </summary>
        public ISkillCutin Cutin { get; set; }

        /// <summary>
        /// 시전자를 화면에 세웠다 내리는 무대. null이면 무대 연출 없이 그대로 시전한다.
        ///
        /// <see cref="Cutin"/>과 달리 자식에서 찾지 않는다 — 무대를 구현하는
        /// <see cref="TagSwapController"/>는 전투 상시 오브젝트에 붙어 있고,
        /// 그쪽이 <c>Start</c>에서 자기를 꽂아 준다. 의존 방향을 그쪽으로 몰아
        /// Executor가 태그 시스템을 모르게 유지한다.
        /// </summary>
        public ICasterStage Stage { get; set; }

        private void Awake()
        {
            if (Cutin == null) Cutin = GetComponentInChildren<ISkillCutin>(true);
        }

        public bool IsRunning => running != null;

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

            running = StartCoroutine(Run(queue));
        }

        public void Abort()
        {
            if (running != null) StopCoroutine(running);
            running = null;

            // StopCoroutine으로 잘린 코루틴은 finally가 돌지 않는다.
            // 컷인이 내려놓은 TimeControl.Scale을 여기서 되돌리지 않으면 게임이 영구 정지한다.
            Cutin?.Cancel();

            // 같은 이유로 무대도 여기서 비운다. 안 그러면 불려 나온 시전자가
            // 실시간 전투에 그대로 남고 조작 캐릭터는 숨은 채로 굳는다.
            Stage?.Clear();
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

                // 재타겟 경로는 사라졌다. 조준이 좌표만 남기므로 대상이 그사이 죽어도
                // 시전 순간 SkillState.ResolveTarget이 그 좌표에서 다시 고른다(결정 로그 ⑧).

                // 무대에 세우는 것이 가장 먼저다. 컷인에 시전자가 보여야 하고,
                // 무엇보다 비활성 오브젝트에 ForceChangeState를 걸면 Update가 안 돌아
                // 스킬이 영영 안 끝나고 slotTimeout까지 큐가 멈춘다.
                Stage?.Enter(slot.caster);

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

                // "끝났다"고만 알린다. 실제로 내려가는 건 다음 Enter나 Clear다 —
                // 여기서 곧바로 내리면 같은 시전자가 이어질 때 한 프레임 깜빡이고,
                // 그 사이 OnDisable → ReleaseBody가 돌아 다음 슬롯이 통째로 날아간다.
                Stage?.Exit(slot.caster);

                OnSlotConsumed?.Invoke(slot.card);

                if (slotGap > 0f)
                    yield return WaitScaled(slotGap);
            }

            if (pending.Count > 0)
                yield return ReleaseCharges(pending);

            BattleLog.Log(LogCategory.Combo, "<b>콤보 실행 종료</b>", this);

            // 무대를 비우고 조작 캐릭터를 되돌린다. 페이즈가 RealTime으로 넘어가기 전에
            // 끝나야 유저가 조작을 되찾은 순간 몰 몸이 이미 서 있다.
            Stage?.Clear();

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

            // RunSlot과 같은 이유 — 꺼진 몸은 모으기가 진행되지 않는다.
            if (!caster.gameObject.activeInHierarchy)
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"{data.skillName} 차징 건너뜀 — {BattleLog.Name(caster)}가 꺼져 있다. ICasterStage 배선을 확인하라", this);
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

                // 차징 시전자는 모으는 내내 무대에 붙잡혀 있었다. 여기서야 놓아 준다.
                Stage?.Exit(p.caster);

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

            // 꺼진 몸에 상태를 걸면 Update가 안 돌아 IsFinished가 영영 안 서고,
            // slotTimeout(초 단위)만큼 콤보가 통째로 멎는다. 무대 배선이 빠졌을 때의 증상이라
            // 조용히 넘기지 않고 소리를 낸다.
            if (!caster.gameObject.activeInHierarchy)
            {
                BattleLog.Warn(LogCategory.Combo,
                    $"{data.skillName} 건너뜀 — {BattleLog.Name(caster)}가 꺼져 있다. ICasterStage 배선을 확인하라", this);
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
