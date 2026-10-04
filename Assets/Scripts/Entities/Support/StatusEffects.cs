using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 한 캐릭터에 걸린 <b>지속시간 있는 상태</b>의 목록. <see cref="Combat"/> 하나당 한 벌.
    ///
    /// 남은 시간을 화면에 띄우려면 그 시간을 아는 곳이 있어야 하는데, 예전에는
    /// <see cref="EffectRunner"/>에 넘긴 익명 콜백 안에만 있었다 — 밖에서는 지금 보호막이
    /// 걸려 있는지조차 알 수 없었다. 그래서 <b>되돌리기까지 여기로 옮겼다.</b>
    /// 타이머를 두 벌 굴리면 화면과 실제가 반드시 어긋난다.
    ///
    /// <see cref="Combat.Tick"/>이 굴린다. 따라서 불릿타임 배율을 따르고, 태그로 내려간
    /// 몸(<c>SetActive(false)</c>)에서는 같이 멈춘다 — 필드 밖에 있는 동안 버프만 녹아
    /// 없어지지 않는다.
    ///
    /// 유니티 객체를 만지지 않으므로 EditMode에서 그대로 검증된다.
    /// </summary>
    public class StatusEffects
    {
        public struct Entry
        {
            public StatusKind kind;

            /// <summary>남은 시간.</summary>
            public float remain;

            /// <summary>걸릴 때의 전체 길이. 게이지가 비율을 그릴 때 분모로 쓴다.</summary>
            public float duration;

            /// <summary>풀릴 때 실행할 원복. 효과를 건 쪽이 같이 넘긴다.</summary>
            public Action revert;

            public float Ratio => duration > 0.0001f ? Mathf.Clamp01(remain / duration) : 0f;
        }

        private readonly List<Entry> active = new List<Entry>();

        /// <summary>지금 걸려 있는 것들. 표시 순서는 걸린 순서다.</summary>
        public IReadOnlyList<Entry> Active => active;

        public int Count => active.Count;

        /// <summary>
        /// 지금 걸린 디버프 비트. <b>캐시하지 않는다</b> — 엔트리가 넷을 넘는 일이 없고,
        /// 캐시하면 무효화할 지점이 넷(Apply · Cancel · CancelAll · Tick)으로 늘어난다.
        /// 같은 값을 두 곳에서 관리하다 어긋나는 게 이 클래스가 생긴 이유다.
        /// </summary>
        public Debuff Debuffs
        {
            get
            {
                Debuff mask = Debuff.None;
                for (int i = 0; i < active.Count; i++) mask |= StatusRules.DebuffOf(active[i].kind);
                return mask;
            }
        }

        /// <summary>
        /// 목록이 실제로 바뀌었을 때 한 번. 몸 색을 칠하는 쪽이 매 프레임 묻지 않게 하려고 있다.
        /// 재진입을 피하려고 <see cref="CancelAll"/>은 원복을 다 돌린 뒤에 한 번만 쏜다.
        /// </summary>
        public event Action OnChanged;

        /// <summary>
        /// 상태를 건다. 같은 종류가 이미 걸려 있으면 <b>긴 쪽이 남는다</b> —
        /// 짧은 재시전이 이미 걸린 긴 버프를 잘라 내면 안 된다.
        ///
        /// 지속시간이 0 이하면 걸지 않고 그 자리에서 원복한다(<see cref="EffectRunner.Schedule"/>과 같은 규칙).
        /// </summary>
        public void Apply(StatusKind kind, float duration, Action revert = null)
        {
            if (duration <= 0f)
            {
                revert?.Invoke();
                return;
            }

            int i = IndexOf(kind);
            if (i < 0)
            {
                active.Add(new Entry { kind = kind, remain = duration, duration = duration, revert = revert });
                OnChanged?.Invoke();
                return;
            }

            Entry e = active[i];
            float before = e.remain;

            e.remain = Mathf.Max(e.remain, duration);

            // 게이지가 100%를 넘어 보이지 않도록 분모도 같이 늘린다.
            e.duration = Mathf.Max(e.duration, e.remain);

            // 원복은 새 것으로 갈아 끼운다. 옛 것은 실행하지 않는다 —
            // 같은 효과를 덧건 것이지 한 번 풀었다 다시 건 게 아니다.
            if (revert != null) e.revert = revert;

            active[i] = e;

            // 짧은 재시전은 아무것도 바꾸지 않는다. 그때까지 알리면 구독자가 헛돈다.
            if (e.remain > before) OnChanged?.Invoke();
        }

        /// <summary>
        /// 시간이 남아 있어도 지금 푼다. 원복은 실행된다.
        /// 보호막이 다 닳았을 때처럼 <b>시간이 아닌 이유로</b> 끝나는 경우가 있다.
        /// </summary>
        public void Cancel(StatusKind kind)
        {
            int i = IndexOf(kind);
            if (i < 0) return;

            Entry e = active[i];
            active.RemoveAt(i);
            e.revert?.Invoke();

            OnChanged?.Invoke();
        }

        /// <summary>
        /// 마스크에 걸리는 <b>디버프만</b> 푼다. 버프는 남긴다 —
        /// 태그로 내려간 몸이 필드 밖에서 보호막까지 잃을 이유는 없다.
        ///
        /// 이름을 <see cref="CancelAll"/>의 오버로드로 두지 않은 이유: 같은 이름이 서로 다른
        /// 범위를 지우면 호출부만 봐서는 무엇이 사라지는지 알 수 없다.
        /// </summary>
        /// <returns>실제로 푼 개수.</returns>
        public int Cancel(Debuff mask)
        {
            if (mask == Debuff.None) return 0;

            int removed = 0;

            // 원복이 다시 상태를 걸어도 방금 지운 칸을 덮어쓰지 않도록 역순으로 돈다(Tick과 같은 순서).
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if ((StatusRules.DebuffOf(active[i].kind) & mask) == 0) continue;

                Entry e = active[i];
                active.RemoveAt(i);
                e.revert?.Invoke();
                removed++;
            }

            if (removed > 0) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>전부 푼다. 사망이 부른다 — 시체에 버프가 남아 있을 이유가 없다.</summary>
        public void CancelAll()
        {
            if (active.Count == 0) return;

            // 원복이 다시 상태를 걸어도 꼬이지 않도록 목록을 먼저 비운다.
            var reverts = new List<Action>(active.Count);
            for (int i = 0; i < active.Count; i++) reverts.Add(active[i].revert);

            active.Clear();

            for (int i = 0; i < reverts.Count; i++) reverts[i]?.Invoke();

            OnChanged?.Invoke();
        }

        public bool Has(StatusKind kind) => IndexOf(kind) >= 0;

        /// <summary>남은 시간. 안 걸려 있으면 0.</summary>
        public float Remaining(StatusKind kind)
        {
            int i = IndexOf(kind);
            return i >= 0 ? active[i].remain : 0f;
        }

        /// <summary><see cref="Combat.Tick"/>이 스케일된 dt로 부른다.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || active.Count == 0) return;

            int expired = 0;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Entry e = active[i];
                e.remain -= dt;

                if (e.remain > 0f)
                {
                    active[i] = e;
                    continue;
                }

                // 목록에서 먼저 빼고 되돌린다 — 원복이 다시 상태를 걸어도 방금 지운 칸을
                // 덮어쓰지 않는다(EffectRunner와 같은 순서).
                active.RemoveAt(i);
                e.revert?.Invoke();
                expired++;
            }

            // 프레임당 한 번만 알린다. 셋이 같이 끝나도 몸 색은 한 번만 다시 칠하면 된다.
            if (expired > 0) OnChanged?.Invoke();
        }

        private int IndexOf(StatusKind kind)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].kind == kind) return i;

            return -1;
        }
    }
}
