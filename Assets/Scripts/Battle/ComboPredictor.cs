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
        private readonly List<CombatState> predicted = new List<CombatState>();
        private static readonly Collider[] Buffer = new Collider[64];

        public IReadOnlyList<CombatState> Predicted => predicted;

        /// <summary>슬롯 0~N을 순서대로 시뮬레이션해 각 슬롯 <b>실행 후</b>의 상태를 낸다.</summary>
        public List<CombatState> Simulate(IReadOnlyList<ComboSlot> slots)
        {
            predicted.Clear();
            if (slots == null) return predicted;

            CombatState cur = CombatState.Neutral;

            // 차징은 자리에서 모으기만 하고 큐가 끝난 뒤 터진다(ComboExecutor).
            // 그래서 상태 사슬에서도 맨 뒤로 미뤄야 예측이 실제와 맞는다.
            var deferred = new List<int>();

            for (int i = 0; i < slots.Count; i++)
            {
                SkillData data = slots[i].Data;
                if (data == null)
                {
                    predicted.Add(cur);
                    continue;
                }

                if (data.IsCharge)
                {
                    // 지금은 상태를 바꾸지 않는다. 표시는 나중에 채운다.
                    deferred.Add(i);
                    predicted.Add(cur);

                    BattleLog.Log(LogCategory.Predict,
                        $"슬롯 {i} {data.skillName}: <color=#FFD166>차징</color> — 콤보 끝에 발동");
                    continue;
                }

                cur = Apply(data, cur);
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
        public int CountEnemiesInRadius(Vector3 center, float radius)
        {
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(center, radius, Buffer);
            int alive = 0;
            var seen = new HashSet<Combat>();

            for (int i = 0; i < count; i++)
            {
                Combat c = Buffer[i] != null ? Buffer[i].GetComponentInParent<Combat>() : null;
                if (c == null || c.IsDead || !seen.Add(c)) continue;
                if (c.GetComponent<Enemy>() == null) continue;

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
