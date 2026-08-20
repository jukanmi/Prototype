using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 실시간 카드 사용 쿨타임 장부. 두 겹으로 잠근다.
    ///
    /// <b>공용 쿨</b>은 어느 카드를 썼든 U키 전체를 잠근다. 연사 속도를 정하는 건 이쪽이다 —
    /// 손패 4장이 서로 다른 스킬이라 스킬 쿨만으로는 연타가 그대로 통과한다.
    ///
    /// <b>스킬 쿨</b>은 같은 스킬이 다시 손패에 돌아왔을 때를 막는다.
    /// <b>에셋 1개 = 스킬 1개</b>이므로 <see cref="SkillData"/> 참조를 그대로 키로 쓴다.
    ///
    /// MonoBehaviour가 아니다 — 순수 계산이라 씬 없이 테스트할 수 있다.
    /// 시간을 스스로 읽지 않는다. 얼마나 흘렀는지는 <see cref="Tick"/>에 넣어 주는 쪽이 정한다
    /// (불릿타임 정지 중에는 흐르지 않아야 하므로).
    /// </summary>
    public class SkillCooldownTracker
    {
        private readonly Dictionary<SkillData, float> remaining = new Dictionary<SkillData, float>();

        /// <summary>순회 중 Dictionary를 고치면 터진다. 지울 키를 잠시 담아 둔다.</summary>
        private readonly List<SkillData> scratch = new List<SkillData>();

        private float global;

        /// <summary>쿨타임이 도는 스킬 수. 공용 쿨은 여기 안 센다.</summary>
        public int Count => remaining.Count;

        /// <summary>카드 종류와 무관하게 잠겨 있는 남은 시간(초).</summary>
        public float GlobalRemaining => Mathf.Max(0f, global);

        /// <summary>
        /// 그 스킬을 지금 쓸 수 있기까지 남은 시간(초).
        /// 공용 쿨과 스킬 쿨 중 <b>긴 쪽</b>이다 — 둘 다 풀려야 나간다.
        /// </summary>
        public float Remaining(SkillData data)
        {
            float own = data != null && remaining.TryGetValue(data, out float left) ? left : 0f;
            return Mathf.Max(0f, Mathf.Max(own, global));
        }

        /// <summary>공용 쿨을 뺀, 그 스킬 자신의 쿨만. 로그에서 어느 쪽이 막았는지 가릴 때 쓴다.</summary>
        public float OwnRemaining(SkillData data)
            => data != null && remaining.TryGetValue(data, out float left) ? Mathf.Max(0f, left) : 0f;

        public bool IsCooling(SkillData data) => Remaining(data) > 0f;

        /// <summary>
        /// 스킬 쿨을 건다. 이미 돌고 있으면 <b>긴 쪽을 남긴다</b> —
        /// 짧은 쿨의 재사용이 긴 쿨을 깎아 내리는 일을 막는다.
        /// </summary>
        public void Start(SkillData data, float seconds)
        {
            if (data == null || seconds <= 0f) return;

            if (remaining.TryGetValue(data, out float left) && left >= seconds) return;

            remaining[data] = seconds;
        }

        /// <summary>공용 쿨을 건다. 스킬 쿨과 같은 이유로 긴 쪽을 남긴다.</summary>
        public void StartGlobal(float seconds)
        {
            if (seconds <= 0f) return;

            global = Mathf.Max(global, seconds);
        }

        /// <summary>흐른 시간만큼 깎는다. 0 이하가 된 항목은 장부에서 지운다.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            if (global > 0f) global -= dt;

            if (remaining.Count == 0) return;

            scratch.Clear();
            foreach (KeyValuePair<SkillData, float> kv in remaining)
                scratch.Add(kv.Key);

            for (int i = 0; i < scratch.Count; i++)
            {
                SkillData key = scratch[i];
                float left = remaining[key] - dt;

                if (left <= 0f) remaining.Remove(key);
                else remaining[key] = left;
            }
        }

        public void Clear()
        {
            remaining.Clear();
            global = 0f;
        }
    }
}
