using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 슬롯 배치 시점의 예측.
    /// 시간이 멈춰 있어 적 위치가 고정되고 유저가 찍은 좌표도 확정돼 있으므로
    /// 반경 안의 적 수를 정확히 셀 수 있다 — 모으기 헛침도 사전에 경고 가능하다.
    /// 전이 규칙은 <see cref="CombatStateRules"/>를 그대로 재사용한다.
    /// </summary>
    public class ComboPredictor : MonoBehaviour
    {
        /// <summary>시전자를 못 찾았을 때 쓸 중력. Physics의 직렬화 기본값과 같다.</summary>
        private const float FallbackGravity = 30f;
        /// <summary>ComboExecutor를 못 찾았을 때 쓸 슬롯 간격.</summary>
        private const float FallbackSlotGap = 0.05f;

        [Tooltip("슬롯 간격을 읽어 올 실행기. 비우면 씬에서 찾는다.")]
        [SerializeField] private ComboExecutor executor;

        private readonly List<CombatState> predicted = new List<CombatState>();
        /// <summary>슬롯별 "이 카드는 무적에 흘린다" 표식. predicted와 같은 길이를 유지한다.</summary>
        private readonly List<bool> blocked = new List<bool>();
        private static readonly Collider[] Buffer = new Collider[64];
        // 한 콜라이더가 여러 개 잡히는 걸 막는 중복 필터. 매 프레임 도는 경로라 재사용한다.
        private static readonly HashSet<Combat> Seen = new HashSet<Combat>();

        public IReadOnlyList<CombatState> Predicted => predicted;

        /// <summary>
        /// 슬롯 <paramref name="idx"/>가 다운(또는 기상) 무적에 통째로 흘리는지.
        /// 다운 1.2s + 기상 0.4s = 1.6초 무적인데 슬롯 간격은 0.05초다 —
        /// 한 번 눕히면 뒤가 전멸하므로 <b>놓기 전에</b> 알려야 한다.
        /// </summary>
        public bool IsBlockedByDown(int idx) => idx >= 0 && idx < blocked.Count && blocked[idx];

        /// <summary>실행기와 같은 슬롯 간격. 체공 시간을 셀 때 쓴다.</summary>
        private float SlotGap
        {
            get
            {
                if (executor == null) executor = FindAnyObjectByType<ComboExecutor>();
                return executor != null ? executor.SlotGap : FallbackSlotGap;
            }
        }

        /// <summary>슬롯 0~N을 순서대로 시뮬레이션해 각 슬롯 <b>실행 후</b>의 상태를 낸다.</summary>
        public List<CombatState> Simulate(IReadOnlyList<ComboSlot> slots)
        {
            predicted.Clear();
            blocked.Clear();
            if (slots == null) return predicted;

            CombatState cur = CombatState.Neutral;

            // 남은 체공 시간. 띄우는 타격이 채우고, 슬롯이 돌 때마다 닳는다.
            // 0이 되면 대상이 바닥에 닿는다 — 그 순간 다운이고, 다운은 1.6초 무적이라
            // 뒤이은 슬롯이 통째로 무효가 된다. 놓기 전에 보여 줘야 "콤보 설계 미스"로 읽힌다.
            float airTimeLeft = 0f;
            float gap = SlotGap;

            // 차징은 자리에서 모으기만 하고 큐가 끝난 뒤 터진다(ComboExecutor).
            // 그래서 상태 사슬에서도 맨 뒤로 미뤄야 예측이 실제와 맞는다.
            var deferred = new List<int>();

            for (int i = 0; i < slots.Count; i++)
            {
                SkillData data = slots[i].Data;
                if (data == null)
                {
                    predicted.Add(cur);
                    blocked.Add(false);
                    continue;
                }

                if (data.IsCharge)
                {
                    // 지금은 상태를 바꾸지 않는다. 표시는 나중에 채운다.
                    deferred.Add(i);
                    predicted.Add(cur);
                    blocked.Add(false);

                    BattleLog.Log(LogCategory.Predict,
                        $"슬롯 {i} {data.skillName}: <color=#FFD166>차징</color> — 콤보 끝에 발동");
                    continue;
                }

                // 이 슬롯이 시작되기 전에 이미 착지했는가.
                airTimeLeft -= data.TotalDuration + gap;
                if (airTimeLeft <= 0f)
                {
                    CombatState landed = CombatStateRules.OnGroundContact(cur);
                    if (landed != cur)
                        BattleLog.Log(LogCategory.Predict,
                            $"슬롯 {i} 전에 착지 — {cur} → <color=#FF8080>{landed}</color>");
                    cur = landed;
                }

                bool whiffed = IsInvincibleTo(cur, data);
                blocked.Add(whiffed);

                cur = Apply(data, cur);

                // 무적에 흘린 슬롯은 띄우지도 못한다 — 체공을 벌어 주는 건 실제로 맞은 타격뿐이다.
                if (!whiffed)
                    airTimeLeft = Mathf.Max(airTimeLeft, HangTime(data, slots[i].caster));

                predicted.Add(cur);

                bool chained = i == 0 || CombatStateRules.CanChain(predicted[i - 1], data.requireState);
                BattleLog.Log(LogCategory.Predict,
                    $"슬롯 {i} {data.skillName}: 선행 {data.requireState} → 예측 <b>{cur}</b> " +
                    $"[{(chained ? "<color=#8AFF80>강화</color>" : "<color=#808080>기본</color>")}]");
            }

            // 미뤄 둔 차징을 배치 순서대로 마지막에 적용한다.
            foreach (int i in deferred)
            {
                SkillData data = slots[i].Data;
                cur = Apply(data, cur);
                predicted[i] = cur;

                BattleLog.Log(LogCategory.Predict,
                    $"슬롯 {i} {data.skillName} 차징 발동: 선행 {data.requireState} → 예측 <b>{cur}</b>");
            }

            return predicted;
        }

        /// <summary>
        /// 지금 상태의 대상에게 이 스킬이 <b>한 대도 못 넣는지</b>.
        /// 다운은 바닥쓸기(canOtg)만 관통하고, 기상은 무엇도 통과하지 못한다
        /// (<see cref="CombatStateRules.Next"/>와 같은 기준).
        /// </summary>
        private static bool IsInvincibleTo(CombatState cur, SkillData data)
        {
            if (cur == CombatState.Getup) return true;
            if (cur != CombatState.Down) return false;

            if (data.hitDataList != null)
                for (int h = 0; h < data.hitDataList.Count; h++)
                    if (data.hitDataList[h].canOtg) return false;

            return true;
        }

        /// <summary>
        /// 이 스킬이 벌어 주는 체공 시간. 가장 센 띄우기 기준으로 <c>2v / g</c>다 —
        /// 올라갔다 내려오는 왕복 시간. 안 띄우는 스킬이면 0.
        /// </summary>
        private static float HangTime(SkillData data, Ally caster)
        {
            if (data == null || data.hitDataList == null) return 0f;

            // 공중 전용 값도 같이 본다. 첫 타로 띄운 뒤 후속타가 airLaunchForce로 더 밀어 올리면
            // 실제 체공은 그쪽 기준이다 — launchForce만 보면 창을 실제보다 좁게 잡는다.
            float launch = 0f;
            for (int h = 0; h < data.hitDataList.Count; h++)
                launch = Mathf.Max(launch,
                                   Mathf.Max(data.hitDataList[h].launchForce,
                                             data.hitDataList[h].airLaunchForce));

            if (launch <= 0f) return 0f;

            float gravity = caster != null && caster.Physics != null ? caster.Physics.Gravity : FallbackGravity;
            return gravity > 0.0001f ? 2f * launch / gravity : 0f;
        }

        /// <summary>실전투와 같은 규칙 · 같은 HitData로 돌린다.</summary>
        private static CombatState Apply(SkillData data, CombatState cur)
        {
            for (int h = 0; h < data.hitDataList.Count; h++)
            {
                HitData hit = data.hitDataList[h];
                cur = CombatStateRules.Next(cur, in hit, h);
            }
            return cur;
        }

        /// <summary>슬롯 idx가 선행 조건을 충족하는지 — 강화 표시 여부.</summary>
        public bool IsChained(IReadOnlyList<ComboSlot> slots, int idx)
        {
            if (slots == null || idx <= 0 || idx >= slots.Count) return idx == 0;

            SkillData data = slots[idx].Data;
            if (data == null) return false;

            CombatState prev = idx - 1 < predicted.Count ? predicted[idx - 1] : CombatState.Neutral;
            return CombatStateRules.CanChain(prev, data.requireState);
        }

        /// <summary>
        /// 지정 좌표 반경 안의 살아 있는 적 수.
        /// 0이면 모으기가 헛치는 것이므로 UI에서 미리 경고한다.
        /// </summary>
        public int CountEnemiesInRadius(Vector3 center, float radius) => EnemiesInRadius(center, radius, null);

        /// <summary>
        /// 반경 안의 살아 있는 적을 <paramref name="outList"/>에 담고 그 수를 낸다.
        /// 세는 쪽(<see cref="CountEnemiesInRadius"/>)과 그리는 쪽(<see cref="KnockbackIndicator"/>)이
        /// 같은 목록을 봐야 "경고는 없는데 화살표도 없다" 같은 어긋남이 안 생긴다.
        /// </summary>
        public int EnemiesInRadius(Vector3 center, float radius, List<Combat> outList)
        {
            outList?.Clear();

            int count = UnityEngine.Physics.OverlapSphereNonAlloc(center, radius, Buffer);
            int alive = 0;
            Seen.Clear();

            for (int i = 0; i < count; i++)
            {
                Combat c = Buffer[i] != null ? Buffer[i].GetComponentInParent<Combat>() : null;
                if (c == null || c.IsDead || !Seen.Add(c)) continue;
                if (c.GetComponent<Enemy>() == null) continue;

                outList?.Add(c);
                alive++;
            }

            return alive;
        }

        /// <summary>
        /// 배치 예정 카드가 헛칠지 미리 본다.
        /// 반경으로 때리는 스킬만 판단한다 — 근접은 시전자 히트박스라 radius를 쓰지 않는다.
        /// </summary>
        public bool WillWhiff(SkillData data, in TargetInfo target)
        {
            if (data == null) return true;
            if (!data.UsesRadius) return false;

            return CountEnemiesInRadius(target.point, data.radius) == 0;
        }
    }
}
