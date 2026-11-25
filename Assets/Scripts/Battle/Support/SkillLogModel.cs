using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스킬 로그의 <b>계산</b>. 줄 목록 · 순서 번호 · 쉬는 시간 · 정리 진행을 쥔다.
    /// 그리기는 <see cref="SkillLogHUD"/>가 한다 — <see cref="ComboMeter"/> / <see cref="ComboDamageHUD"/>와 같은 분리.
    ///
    /// <code>
    /// 시전 ─ Add ─▶ <b>맨 위</b>에 새 줄, 기존 줄은 한 칸씩 아래로 미끄러진다 (번호 = 이번 묶음 안에서 몇 번째)
    /// 시전 · 아군 타격 ─ NotifyAttack ─▶ 쉬는 시간 0
    /// 쉬는 시간 ≥ IdleWindow ─▶ 정리: 위 줄부터 차례로 왼쪽으로 빠지며 흐려짐 → 다 빠지면 비우고 번호 1부터
    /// 정리 중 공격 ─▶ 정리 취소, 줄은 그대로 남고 번호도 이어서 센다
    /// 줄 수가 MaxRows를 넘음 ─▶ 맨 아래(가장 오래된) 줄이 제자리에서 흐려지며 빠진다
    /// 줄의 대상이 죽음 ─ NotifyTargetDead ─▶ DeathLinger 뒤 그 줄이 흐려지며 빠지고, 아래 줄들이 올라와 틈을 메운다
    /// </code>
    ///
    /// 시간은 부르는 쪽이 넘긴다(스케일된 dt). 0이면 아무것도 안 움직인다 — 총공격 선택처럼 시간이 멈춘 동안 로그가 남는다.
    /// </summary>
    public sealed class SkillLogModel
    {
        /// <summary>이만큼 공격이 없으면 로그가 정리된다(초).</summary>
        public const float IdleWindow = 3f;

        /// <summary>한 번에 남겨 두는 줄 수. 화면에 다 안 들어가도 된다 — 넘친 줄은 화면 밖에 남는다.</summary>
        public const int MaxRows = 36;

        /// <summary>대상이 죽은 뒤 그 줄이 남아 있는 시간(초). 막타를 읽을 여유.</summary>
        public const float DeathLinger = 1f;

        /// <summary>새 줄이 왼쪽에서 밀려 들어오는 시간.</summary>
        public const float AppearTime = 0.18f;

        /// <summary>빠지는 줄(넘침 · 대상 사망)이 흐려지는 시간.</summary>
        public const float EvictTime = 0.2f;

        /// <summary>정리 때 한 줄이 빠지는 시간.</summary>
        public const float ExitTime = 0.25f;

        /// <summary>정리 때 줄 사이 출발 간격. 위 줄부터 차례로 빠진다.</summary>
        public const float ExitStagger = 0.03f;

        /// <summary>칸이 바뀐 줄이 새 칸을 따라가는 빠르기(지수 감쇠 계수, 1/초).</summary>
        public const float FollowRate = 14f;

        public sealed class Entry
        {
            public Ally Caster;
            public SkillData Skill;

            /// <summary>이 스킬이 겨눈 적. 이 적이 죽으면 줄이 빠진다. 없으면 시전자의 첫 적중 상대로 채운다(HUD).</summary>
            public Entity Target;

            /// <summary>이번 묶음 안에서 몇 번째 스킬인가. 1부터.</summary>
            public int Order;

            /// <summary>목표 칸. 0이 맨 위(가장 최근). 빠지는 줄은 제 칸을 지킨다.</summary>
            public int Slot;

            /// <summary>지금 그려지는 칸(소수). <see cref="Slot"/>을 부드럽게 따라간다.</summary>
            public float Y;

            /// <summary>등장 진행 0→1(선형). 그리는 쪽이 곡선을 입힌다.</summary>
            public float Appear;

            /// <summary>빠지는 중이면 0→1. 아니면 -1.</summary>
            public float Evict = -1f;

            /// <summary>대상이 죽은 뒤 남은 시간. 아직 안 죽었으면 -1.</summary>
            public float DeathLeft = -1f;

            /// <summary>정리로 빠지는 진행 0→1.</summary>
            public float Exit;

            public bool Evicting => Evict >= 0f;

            /// <summary>대상이 죽어 빠질 차례를 기다리는 중인가.</summary>
            public bool TargetDead => DeathLeft >= 0f;

            /// <summary>그릴 때 곱할 불투명도.</summary>
            public float Alpha
                => Mathf.Clamp01(Appear / 0.5f) * (1f - Exit) * (Evicting ? 1f - Evict : 1f);
        }

        /// <summary>위(가장 최근)에서 아래(가장 오래된) 순서.</summary>
        private readonly List<Entry> entries = new List<Entry>();
        private int nextOrder = 1;
        private float idle;

        /// <summary>위(가장 최근)에서 아래 순서. 빠지는 중인 줄도 들어 있다.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        public int Count => entries.Count;

        /// <summary>정리(전체 퇴장) 중인가.</summary>
        public bool IsClearing { get; private set; }

        /// <summary>마지막 공격 뒤 흐른 시간.</summary>
        public float Idle => idle;

        /// <summary>다음에 붙을 번호.</summary>
        public int NextOrder => nextOrder;

        /// <summary>새 줄을 맨 위에. 쉬는 시간도 0으로 돌린다 — 시전은 그 자체로 공격이다.</summary>
        public Entry Add(Ally caster, SkillData skill, Entity target = null)
        {
            NotifyAttack();

            var e = new Entry
            {
                Caster = caster,
                Skill = skill,
                Target = target,
                Order = nextOrder++,
            };
            entries.Insert(0, e);

            Reslot();

            // 새 줄은 맨 위 칸에서 바로 시작한다(옆에서 밀려 들어올 뿐) — 아래로 미끄러지는 건 기존 줄이다.
            e.Y = e.Slot;
            return e;
        }

        /// <summary>
        /// 이 줄의 대상이 죽었다. <see cref="DeathLinger"/> 뒤 줄이 빠진다.
        /// 여러 번 불러도 시계는 처음 한 번만 건다.
        /// </summary>
        public void NotifyTargetDead(Entry e)
        {
            if (e == null || e.TargetDead || e.Evicting) return;
            e.DeathLeft = DeathLinger;
        }

        /// <summary>공격이 있었다. 정리 중이었다면 취소한다.</summary>
        public void NotifyAttack()
        {
            idle = 0f;
            if (!IsClearing) return;

            IsClearing = false;
            foreach (Entry e in entries) e.Exit = 0f;
        }

        /// <summary>통째로 비운다. 스테이지 재시작 · HUD 꺼짐이 부른다.</summary>
        public void Reset()
        {
            entries.Clear();
            nextOrder = 1;
            idle = 0f;
            IsClearing = false;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            if (entries.Count == 0)
            {
                idle = 0f;
                return;
            }

            idle += dt;
            if (!IsClearing && idle >= IdleWindow) IsClearing = true;

            float follow = 1f - Mathf.Exp(-FollowRate * dt);

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry e = entries[i];
                e.Appear = Mathf.Min(1f, e.Appear + dt / AppearTime);
                e.Y = Mathf.Abs(e.Slot - e.Y) < 0.001f ? e.Slot : Mathf.Lerp(e.Y, e.Slot, follow);

                if (e.Evicting)
                {
                    e.Evict = Mathf.Min(1f, e.Evict + dt / EvictTime);
                    if (e.Evict >= 1f) entries.RemoveAt(i);
                }
            }

            // 대상이 죽은 줄의 시계. 흐려짐 진행 <b>뒤</b>에 돈다 — 같은 프레임에 시작과 끝이 겹쳐 한 번에 사라지지 않게.
            // 다 되면 빠지기 시작하고, 아래 줄들이 틈을 메운다.
            bool reslot = false;
            foreach (Entry e in entries)
            {
                if (!e.TargetDead || e.Evicting) continue;

                e.DeathLeft = Mathf.Max(0f, e.DeathLeft - dt);
                if (e.DeathLeft > 0f) continue;

                e.Evict = 0f;
                reslot = true;
            }
            if (reslot) Reslot();

            if (!IsClearing) return;

            // 정리 진행은 3초를 넘긴 만큼이다 — 넘어선 프레임의 dt 전체를 얹으면 그 자리에서 다 사라진다.
            float clearElapsed = idle - IdleWindow;
            bool allGone = true;
            for (int i = 0; i < entries.Count; i++)
            {
                float t = (clearElapsed - i * ExitStagger) / ExitTime;
                entries[i].Exit = Mathf.Clamp01(t);
                if (entries[i].Exit < 1f) allGone = false;
            }

            if (allGone) Reset();
        }

        /// <summary>
        /// 칸을 다시 매긴다. 남는 줄은 위에서부터 0, 1, 2 … — 새 줄이 위에 끼면 나머지가 내려가고,
        /// 가운데 줄이 빠지면 아래 줄들이 올라온다. 빠지는 줄은 제 칸을 지킨 채 흐려진다.
        /// 넘치면 맨 아래(가장 오래된) 줄부터 뺀다.
        /// </summary>
        private void Reslot()
        {
            int slot = 0;
            foreach (Entry e in entries)
            {
                if (e.Evicting) continue;

                if (slot >= MaxRows)
                {
                    e.Evict = 0f;
                    continue;
                }

                e.Slot = slot++;
            }
        }

        // ── 칸 색 ────────────────────────────────────────

        /// <summary>
        /// 순서 칸의 색 — 그 스킬이 적에게 남기는 상태.
        /// 디버프(바인드 · 빙결 · 스턴)를 하나라도 거는 타가 있으면 그 색이 이긴다. 몸 색 겹침 표시와 같은 우선순위다 —
        /// 묶는 스킬은 "묶는다"가 가장 큰 정보다. 없으면 결과 상태(공중 · 넉백 · 벽꽝 …)의 색, 그것도 없으면 흐린 회색.
        /// </summary>
        public static Color ChipColor(SkillData skill)
        {
            if (skill == null) return UiTheme.TextOff;

            Debuff mask = Debuff.None;
            if (skill.hitDataList != null)
                foreach (HitData hit in skill.hitDataList)
                    mask |= hit.debuff;

            if (CombatStateVisuals.TryDebuffColor(mask, out Color debuff)) return debuff;

            return CombatStateVisuals.ShouldShow(skill.resultState)
                ? CombatStateVisuals.StateColor(skill.resultState)
                : UiTheme.TextOff;
        }

        /// <summary>칸 위 숫자 색. 밝은 칸(경직 흰색 · 스턴 노랑)에는 어두운 글자.</summary>
        public static Color NumberColor(Color chip)
            => UiTheme.Contrast(chip, UiTheme.TextOnLight) >= UiTheme.Contrast(chip, UiTheme.TextMain)
                ? UiTheme.TextOnLight
                : UiTheme.TextMain;

        /// <summary>스킬명. 비어 있으면 에셋 이름 — 빈 줄보다 낫다(컷인과 같은 규칙).</summary>
        public static string NameOf(SkillData skill)
            => skill == null ? string.Empty
                : string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName;
    }
}
