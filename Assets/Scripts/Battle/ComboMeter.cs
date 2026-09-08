// 전투 계산 조각들 — 콤보 미터 · 슬롯 · 쿨타임 추적 · 태그 교대 규칙 · 적 반경 탐침.
// 전부 BulletTimeController와 TagSwapController가 쓰는 순수 계산이다.

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ ComboMeter ═══════════════════════════════════════════

    /// <summary>
    /// "지금 콤보를 몇 대, 얼마나 넣었나". <b>순수 계산만</b> 한다 —
    /// 유니티에 의존하지 않아 EditMode에서 그대로 돌릴 수 있다
    /// (<see cref="BasicComboRules"/>와 같은 취지).
    ///
    /// 콤보의 경계는 <b>시간</b>이 정한다. 마지막 타격 뒤 <see cref="Window"/>만큼 아무것도
    /// 안 맞으면 그 콤보는 끝난 것으로 보고, 다음 타격이 1타부터 다시 센다.
    /// 상태 사슬(<see cref="CombatState"/>)로 끊지 않는 이유: 다운·기상까지 이어지는
    /// 정상적인 마무리도 사슬 위에서는 "끊긴 것"으로 보여서, 정작 완주한 콤보가 안 잡힌다.
    /// </summary>
    public class ComboMeter
    {
        /// <summary>이 시간 동안 새 타격이 없으면 콤보가 끝난다(초).</summary>
        public float Window { get; set; } = 2.5f;

        /// <summary>콤보가 끝난 뒤 화면에 남겨 두는 시간(초). 마지막 숫자를 읽을 여유다.</summary>
        public float LingerDuration { get; set; } = 1.5f;

        /// <summary>지금까지 맞힌 횟수.</summary>
        public int Hits { get; private set; }

        /// <summary>누적 피해. 방어력 · 보호막까지 적용된 실제 수치가 들어온다.</summary>
        public float Damage { get; private set; }

        /// <summary>첫 타부터 마지막 타까지 걸린 시간(초). 마지막 타 이후 공백은 안 센다.</summary>
        public float Duration { get; private set; }

        /// <summary>마지막 타격 이후 흐른 시간(초).</summary>
        public float SinceLastHit { get; private set; }

        /// <summary>아직 이어지는 중인지. <see cref="Window"/> 안에 있으면 참.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>초당 피해. 한 대만 맞혔으면 시간이 0이라 0을 낸다 — 무한대를 그리지 않는다.</summary>
        public float Dps => Duration > 0.0001f ? Damage / Duration : 0f;

        /// <summary>
        /// 화면에 보일지. 이어지는 중이거나, 끝난 뒤 <see cref="LingerDuration"/> 안이면 보인다.
        /// 한 대도 안 맞혔으면 보이지 않는다.
        /// </summary>
        public bool IsVisible => Hits > 0 && (IsRunning || SinceLastHit < Window + LingerDuration);

        /// <summary>
        /// 사라지는 중의 불투명도(0~1). 콤보가 끝난 순간부터 <see cref="LingerDuration"/>에 걸쳐 0으로 간다.
        /// </summary>
        public float Alpha
        {
            get
            {
                if (Hits <= 0) return 0f;
                if (IsRunning) return 1f;
                if (LingerDuration <= 0f) return 0f;

                float faded = SinceLastHit - Window;
                return Mathf.Clamp01(1f - faded / LingerDuration);
            }
        }

        /// <summary>한 대 들어갔다. <paramref name="damage"/>는 실제로 깎인 양.</summary>
        public void AddHit(float damage)
        {
            if (IsRunning)
            {
                // 첫 타 시점부터의 누적. 이어지는 동안의 공백만 길이에 들어간다.
                Duration += SinceLastHit;
            }
            else
            {
                // 새 콤보. 남아 있던 이전 기록을 여기서 지운다 —
                // Tick에서 지우면 마지막 숫자가 화면에서 사라지는 순간과 엉킨다.
                Hits = 0;
                Damage = 0f;
                Duration = 0f;
                IsRunning = true;
            }

            Hits++;
            Damage += Mathf.Max(0f, damage);
            SinceLastHit = 0f;
        }

        /// <summary>
        /// 시간을 흘린다. <b>스케일된 dt</b>를 넣어야 한다 —
        /// 불릿타임에 머문 시간이 콤보 길이에 들어가면 DPS가 통째로 거짓이 된다.
        /// </summary>
        public void Tick(float dt)
        {
            if (Hits <= 0 || dt <= 0f) return;

            SinceLastHit += dt;

            if (IsRunning && SinceLastHit >= Window)
                IsRunning = false;
        }

        /// <summary>전부 지운다. 씬 전환 · 전투 종료에서 부른다.</summary>
        public void Reset()
        {
            Hits = 0;
            Damage = 0f;
            Duration = 0f;
            SinceLastHit = 0f;
            IsRunning = false;
        }
    }

    // ══ ComboSlot ═══════════════════════════════════════════

    /// <summary>
    /// 카드 한 장의 실행 단위. 카드 · 조준값 · 시전자를 함께 묶는다.
    /// <see cref="Hand"/>가 이 구조체를 그대로 들고 있어 손패 = 실행 순서가 된다.
    /// </summary>
    [Serializable]
    public struct ComboSlot
    {
        public ComboCard card;

        /// <summary>유저가 찍었거나 자동으로 채워진 조준값.</summary>
        public TargetInfo target;

        /// <summary>유저가 직접 조준했는지. false면 발동 직전에 자동 조준으로 채운다.</summary>
        public bool aimed;

        /// <summary>이 카드를 실행할 동료. 발동 직전에 카드의 직업으로 결정된다.</summary>
        public Ally caster;

        public bool IsEmpty => card == null;
        public SkillData Data => card != null ? card.Data : null;
    }

    // ══ SkillCooldownTracker ═══════════════════════════════════════════

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

    // ══ TagSwapRules ═══════════════════════════════════════════

    /// <summary>
    /// 태그 교대 순서를 정하는 규칙. <see cref="TagSwapController"/>에서 떼어 낸 순수 계산이다.
    ///
    /// 핵심 두 메서드가 <see cref="Entity"/>가 아니라 <b>술어</b>를 받는 이유는 EditMode 테스트
    /// 때문이다. 에디트모드에서는 <c>Awake</c>가 돌지 않아 <c>Combat.Die()</c>가
    /// <c>physics</c>에서 터진다 — 즉 "죽은 캐릭터"를 만들 방법이 없다. 술어로 받으면
    /// 순회 규칙만 씬 없이 검증할 수 있고, 로스터를 읽는 부분은 얇은 어댑터로 남는다.
    /// </summary>
    public static class TagSwapRules
    {
        /// <summary>
        /// <paramref name="current"/> <b>다음</b> 칸부터 한 바퀴 돌며 세울 수 있는 첫 칸.
        ///
        /// 세울 수 있는 칸이 <paramref name="current"/> 하나뿐이면 그대로 돌려준다 —
        /// "바꿀 사람이 없다"와 "아무도 없다"는 부르는 쪽이 다르게 다뤄야 하기 때문이다.
        /// 아무도 없으면 -1.
        /// </summary>
        public static int Next(int count, Func<int, bool> selectable, int current)
        {
            if (count <= 0 || selectable == null) return -1;

            // current가 범위 밖(-1 포함)이면 0번 앞에서 시작한 것으로 본다.
            int from = current >= 0 && current < count ? current : -1;

            for (int step = 1; step <= count; step++)
            {
                int i = Wrap(from + step, count);
                if (selectable(i)) return i;
            }

            // 한 바퀴를 다 돌아도 못 찾았다 = 자기 자신뿐이거나 아무도 없다.
            return from >= 0 && selectable(from) ? from : -1;
        }

        /// <summary>처음으로 세울 수 있는 칸. 시작 시 한 번 쓴다.</summary>
        public static int First(int count, Func<int, bool> selectable)
        {
            if (selectable == null) return -1;

            for (int i = 0; i < count; i++)
                if (selectable(i)) return i;

            return -1;
        }

        // ── 로스터 어댑터 ───────────────────────────────────

        public static int NextAlive(IReadOnlyList<Entity> roster, int current)
            => roster == null ? -1 : Next(roster.Count, i => IsSelectable(roster, i), current);

        public static int FirstAlive(IReadOnlyList<Entity> roster)
            => roster == null ? -1 : First(roster.Count, i => IsSelectable(roster, i));

        /// <summary>
        /// 그 칸을 필드에 세울 수 있는가. 빈 칸과 사망을 거른다.
        /// <b>활성 여부는 보지 않는다</b> — 지금 꺼져 있는 몸을 고르는 게 교대의 목적이다.
        /// </summary>
        public static bool IsSelectable(IReadOnlyList<Entity> roster, int index)
        {
            if (roster == null || index < 0 || index >= roster.Count) return false;

            Entity e = roster[index];
            return e != null && e.Combat != null && !e.Combat.IsDead;
        }

        private static int Wrap(int index, int count) => ((index % count) + count) % count;
    }

    // ══ EnemyRadiusProbe ═══════════════════════════════════════════

    /// <summary>
    /// 지정 좌표 반경 안의 살아 있는 적을 센다 · 모은다.
    ///
    /// <see cref="TargetSelector"/>의 조준 HUD("몇 명 맞는가")와 <see cref="KnockbackIndicator"/>의
    /// 화살표 대상 목록이 <b>같은 함수</b>를 봐야 한다 — 세는 쪽과 그리는 쪽이 따로 계산하면
    /// "경고는 없는데 화살표도 없다" 같은 어긋남이 생긴다.
    ///
    /// 콤보가 실제로 이어지는지(상태 사슬 · 다운 무적)는 여기서 보지 않는다 — 그건 수치로
    /// 보장하는 영역이라 별도 시뮬레이션이 필요 없다. 이 클래스는 순수 공간 질의만 한다.
    /// </summary>
    public class EnemyRadiusProbe : MonoBehaviour
    {
        private static readonly Collider[] Buffer = new Collider[64];
        // 한 콜라이더가 여러 개 잡히는 걸 막는 중복 필터. 매 프레임 도는 경로라 재사용한다.
        private static readonly HashSet<Combat> Seen = new HashSet<Combat>();

        /// <summary>
        /// 지정 좌표 반경 안의 살아 있는 적 수.
        /// 0이면 모으기 · 장판이 헛치는 것이므로 UI에서 미리 경고한다.
        /// </summary>
        public int CountEnemiesInRadius(Vector3 center, float radius) => EnemiesInRadius(center, radius, null);

        /// <summary>반경 안의 살아 있는 적을 <paramref name="outList"/>에 담고 그 수를 낸다.</summary>
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
    }
}
