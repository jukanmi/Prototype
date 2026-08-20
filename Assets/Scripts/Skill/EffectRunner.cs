using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지속시간이 있는 효과를 되돌려 주는 최소 스케줄러.
    /// 불릿타임 배율을 따르므로 시간 정지 중에는 지속시간도 흐르지 않는다.
    ///
    /// <b>캐릭터에게 걸리는 상태는 여기 넣지 않는다.</b> 그건 <see cref="StatusEffects"/>가
    /// 지속시간과 원복을 같이 들고 있어야 머리 위 게이지에 남은 시간이 뜬다 —
    /// 여기 맡기면 걸렸다는 사실이 익명 콜백 안에만 남는다.
    /// 대상 없이 시간만 재면 되는 효과(장판 수명 등)를 위해 남겨 둔다.
    /// </summary>
    public class EffectRunner : MonoBehaviour
    {
        private struct Pending
        {
            public float remain;
            public Action revert;
        }

        private static EffectRunner instance;
        private readonly List<Pending> pending = new List<Pending>();

        public static EffectRunner Instance
        {
            get
            {
                if (instance != null) return instance;

                var go = new GameObject("[EffectRunner]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<EffectRunner>();
                return instance;
            }
        }

        /// <summary>duration 뒤에 revert를 한 번 호출한다.</summary>
        public void Schedule(float duration, Action revert)
        {
            if (revert == null) return;

            if (duration <= 0f)
            {
                revert();
                return;
            }

            pending.Add(new Pending { remain = duration, revert = revert });
        }

        private void Update()
        {
            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending p = pending[i];
                p.remain -= dt;

                if (p.remain > 0f)
                {
                    pending[i] = p;
                    continue;
                }

                pending.RemoveAt(i);
                p.revert?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
