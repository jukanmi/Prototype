using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 보스 패턴 하나의 정의. 판정 · 이동 · 연출 · 버프를 전부 데이터로 들고 있다.
    /// 언제 쓸지(사거리 · HP · 쿨)는 여기 없다 — 그건 판단이라 <see cref="BossBrainAsset"/>이 갖는다.
    /// </summary>
    [Serializable]
    public struct BossPattern
    {
        [Tooltip("인스펙터와 로그에서 이 패턴을 부르는 이름.")]
        public string label;

        [Header("타이밍")]
        [Tooltip("예고. 제자리에서 타겟을 노려본다 — 플레이어가 피할 창.")]
        public float telegraph;
        [Tooltip("발동. 이 구간 안에서 히트박스가 hitCount번 켜진다.")]
        public float active;
        [Tooltip("후딜. 반격당하는 구간.")]
        public float recovery;

        [Header("판정")]
        [Tooltip("발동 구간에서 히트박스를 켜는 횟수. 연타는 2 이상.")]
        public int hitCount;
        [Tooltip("한 번 켜져 있는 시간.")]
        public float hitDuration;
        [Tooltip("데미지 = 공격력 x 이 배율.")]
        public float damageScale;
        [Tooltip("이 패턴이 쓰는 히트박스. 비우면 평타 히트박스를 쓴다.")]
        public Attack hitbox;
        public HitData hit;

        [Header("이동")]
        [Tooltip("발동 중 전진 속도. 0이면 제자리에서 휘두른다.")]
        public float advanceSpeed;
        [Tooltip("적중·벽 접촉이 발동을 끊는가. 돌진 계열만 켠다.")]
        public bool cancelOnContact;

        [Header("연출")]
        [Tooltip("예고 중 재생할 Animator 상태 이름. 비우면 안 바꾼다.")]
        public string telegraphClip;
        [Tooltip("발동 중 재생할 Animator 상태 이름. 비우면 안 바꾼다.")]
        public string activeClip;

        [Header("버프 (격노 등)")]
        [Tooltip("발동 시 공격력에 곱한다. 1 이하면 버프 없음.")]
        public float attackPowerScale;
        [Tooltip("발동 시 이동속도에 곱한다. 1 이하면 버프 없음.")]
        public float moveSpeedScale;
        [Tooltip("발동 시 평타 쿨에 곱한다. 1 미만이면 빨라진다. 0이면 안 건드린다.")]
        public float attackIntervalScale;

        [Header("사용 제한")]
        [Tooltip("전투당 한 번만. 격노처럼 되돌릴 수 없는 패턴에 쓴다.")]
        public bool once;
    }

    /// <summary>
    /// 보스의 특수 행동 <b>실행</b>. 여러 패턴을 하나의 컴포넌트가 들고,
    /// 브레인이 지목한 인덱스를 <see cref="EnemySpecialSequence"/>에 태워 돌린다.
    ///
    /// 패턴마다 컴포넌트를 만들지 않는 이유는 <see cref="IEnemySpecialAction"/>에 적어 뒀다 —
    /// 인스펙터의 컴포넌트 순서가 패턴 번호가 되면 순서를 바꾸는 것만으로 기술이 뒤바뀐다.
    /// </summary>
    [RequireComponent(typeof(Entity))]
    public class BossPatternAction : MonoBehaviour, IEnemySpecialAction
    {
        [SerializeField] private BossPattern[] patterns;

        private Entity owner;
        private EntityAnimator animator;
        private EnemyControl control;
        private EnemySpecialSequence sequence;

        private Entity target;
        private int current = -1;

        /// <summary>1회성 패턴을 이미 썼는지. 브레인이 무상태라 여기서 기억해야 한다.</summary>
        private bool[] used;

        // ── 발동 구간의 히트박스 점멸 상태 ──
        private int hitsFired;
        private bool hitboxOpen;
        private float hitboxCloseAt;
        private float phaseTime;

        private bool wired;

        public int Count => patterns != null ? patterns.Length : 0;
        public bool IsRunning => sequence != null && sequence.IsRunning;

        public EnemySpecialPhase Phase => sequence != null ? sequence.Phase : EnemySpecialPhase.Idle;

        /// <summary>지금 돌고 있는 패턴 번호. 없으면 -1. 디버그 HUD가 읽는다.</summary>
        public int CurrentIndex => IsRunning ? current : -1;

        /// <summary>패턴 표. 프리팹 배선 검사용 읽기 전용 창구.</summary>
        public BossPattern[] Patterns => patterns;

        /// <summary>
        /// 패턴 표를 통째로 갈아끼운다. <b>에디터 빌더가 프리팹을 조립할 때만</b> 쓴다 —
        /// 전투 중에 부르면 돌고 있는 패턴의 인덱스가 표와 어긋난다.
        ///
        /// <see cref="Enemy.ConfigureBasicProjectile"/>과 같은 자리의 창구다.
        /// </summary>
        public void SetPatterns(BossPattern[] value)
        {
            patterns = value;
            used = new bool[Count];
        }

        private void Awake()
        {
            owner = GetComponent<Entity>();
            animator = GetComponent<EntityAnimator>();
            control = GetComponent<EnemyControl>();
            sequence = new EnemySpecialSequence();
            used = new bool[Count];
        }

        public bool TryStart(int index, Entity patternTarget)
        {
            if (sequence == null || sequence.IsRunning) return false;
            if (patternTarget == null) return false;
            if (index < 0 || index >= Count) return false;
            if (used[index]) return false;

            BossPattern p = patterns[index];

            // 소모는 끝날 때가 아니라 <b>시작할 때</b> 기록한다. 발동 도중 경직으로 끊겨도
            // 버프는 이미 걸렸을 수 있어서, 끝까지 갔을 때만 세면 격노가 두 번 걸린다.
            if (p.once) used[index] = true;

            current = index;
            target = patternTarget;
            hitsFired = 0;
            hitboxOpen = false;
            phaseTime = 0f;

            sequence.Begin(p.telegraph, p.active, p.recovery);
            PlayClip(p.telegraphClip);
            owner.SetTelegraph(sequence.ShouldShowTelegraph);

            BattleLog.Log(LogCategory.State,
                $"{name} 패턴 '{Label(index)}' 예고 시작 → {patternTarget.name}", this);
            return true;
        }

        public void Tick(float dt)
        {
            if (sequence == null || !sequence.IsRunning) return;

            EnemySpecialPhase before = sequence.Phase;
            sequence.Tick(dt, AimDirection());
            EnemySpecialPhase after = sequence.Phase;

            if (before != after) HandleTransition(after);

            // 발동 TelegraphLead초 전부터만 번쩍인다. 예고 단계 전체를 켜면 패턴마다
            // "!에서 타격까지"가 달라져 패링 타이밍을 익힐 수 없다.
            owner.SetTelegraph(sequence.ShouldShowTelegraph);

            // 단계가 바뀌면 sequence의 타이머가 0으로 되감긴다. 문턱 판정이 쓰는 시각도
            // 같은 값을 봐야 하므로 매 프레임 여기서 받아 온다.
            phaseTime = sequence.PhaseTime;

            switch (after)
            {
                case EnemySpecialPhase.Telegraph:
                    // 예고 중에는 계속 따라 돈다. 여기까지가 유도다.
                    owner.Physics.Face(AimDirection());
                    owner.Physics.Move(Vector3.zero, 0f);
                    break;

                case EnemySpecialPhase.Active:
                    TickActive();
                    break;
            }
        }

        public void Cancel()
        {
            if (sequence == null || !sequence.IsRunning) return;

            sequence.Cancel();
            Teardown();
            owner.SetTelegraph(false);
            current = -1;
            target = null;

            BattleLog.Log(LogCategory.State, $"{name} 패턴 취소", this);
        }

        // ── 발동 구간 ───────────────────────────────────

        /// <summary>
        /// 히트박스를 <c>hitCount</c>번 나눠 켠다. 매번 <see cref="Attack.Begin"/>을 새로 부르므로
        /// 이미 맞은 목록이 비워져 <b>같은 대상이 연타를 다 맞는다</b> — 그게 연타의 정의다.
        /// </summary>
        private void TickActive()
        {
            BossPattern p = patterns[current];

            // 전진. Dash는 속도를 매 프레임 덮어쓴다. 감속에 먹히지 않게 계속 밀어 준다.
            if (p.advanceSpeed > 0f) owner.Physics.Dash(sequence.LockedDirection, p.advanceSpeed);
            else owner.Physics.Move(Vector3.zero, 0f);

            Attack box = Hitbox(p);
            if (box == null) return;

            // 닫기가 먼저다. 순서가 뒤집히면 방금 연 히트박스를 같은 프레임에 도로 닫는다.
            if (hitboxOpen && phaseTime >= hitboxCloseAt)
            {
                box.End();
                hitboxOpen = false;
            }

            int total = Mathf.Max(1, p.hitCount);
            while (hitsFired < total && phaseTime >= HitTime(p.active, hitsFired, total))
            {
                box.Begin(BuildHit(p));
                hitboxOpen = true;
                hitboxCloseAt = phaseTime + Mathf.Max(0.01f, p.hitDuration);
                hitsFired++;
            }
        }

        /// <summary>
        /// i번째 타격이 발동 구간 안에서 나가는 시각. 구간을 균등하게 나눈다.
        ///
        /// 순수 함수라 테스트가 직접 부른다 — 다단히트가 한 프레임에 몰려 세 번이
        /// 한 번처럼 보이는 버그는 씬에서 눈으로 잡기 가장 어려운 종류다.
        /// </summary>
        public static float HitTime(float activeDuration, int index, int total)
            => total <= 1 ? 0f : Mathf.Max(0f, activeDuration) * index / total;

        private void HandleTransition(EnemySpecialPhase next)
        {
            BossPattern p = patterns[current];

            switch (next)
            {
                case EnemySpecialPhase.Active:
                    if (p.cancelOnContact) Wire(p);
                    ApplyBuffs(p);
                    PlayClip(p.activeClip);
                    break;

                case EnemySpecialPhase.Recovery:
                    Teardown();
                    owner.Physics.ResetInertia();
                    owner.Physics.Move(Vector3.zero, 0f);
                    break;

                case EnemySpecialPhase.Idle:
                    Teardown();
                    current = -1;
                    target = null;
                    break;
            }
        }

        /// <summary>
        /// 격노 계열. 되돌리지 않으므로 <c>once</c>와 짝지어 쓴다 —
        /// 매번 곱하면 몇 번 만에 손댈 수 없는 수치가 된다.
        /// </summary>
        private void ApplyBuffs(in BossPattern p)
        {
            if (p.attackPowerScale > 1f)
                owner.Stats.Set(StatType.AttackPower,
                                owner.Stats.GetValue(StatType.AttackPower) * p.attackPowerScale);

            if (p.moveSpeedScale > 1f)
                owner.Stats.Set(StatType.MoveSpeed,
                                owner.Stats.GetValue(StatType.MoveSpeed) * p.moveSpeedScale);

            if (p.attackIntervalScale > 0f && control != null)
                control.ScaleAttackInterval(p.attackIntervalScale);
        }

        private HitData BuildHit(in BossPattern p)
        {
            HitData h = p.hit;
            float scale = p.damageScale > 0f ? p.damageScale : 1f;
            h.damageData.damage = owner.Stats.GetValue(StatType.AttackPower, h.damageData.damage) * scale;
            return h;
        }

        /// <summary>?. 가 안전하도록 유니티의 가짜 null을 진짜 null로 정규화해서 돌려준다.</summary>
        private Attack Hitbox(in BossPattern p)
        {
            if (p.hitbox != null) return p.hitbox;
            if (owner != null && owner.BasicAttack != null) return owner.BasicAttack;
            return null;
        }

        private Vector3 AimDirection()
        {
            if (target == null) return owner.Physics.Facing;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.0001f ? to : owner.Physics.Facing;
        }

        private void PlayClip(string stateName)
        {
            if (animator != null) animator.PlayState(stateName);
        }

        private string Label(int index)
        {
            string label = patterns[index].label;
            return string.IsNullOrEmpty(label) ? index.ToString() : label;
        }

        // ── 구독 관리 ───────────────────────────────────
        // 전진 패턴에만 붙인다. 제자리 패턴에 붙이면 첫 타 적중이 자기 공격을 끊는다.

        private void Wire(in BossPattern p)
        {
            if (wired) return;
            wired = true;

            Attack box = Hitbox(p);
            if (box != null) box.OnHit += HandleHit;
            owner.Physics.OnWallHit += HandleWall;
        }

        private void Teardown()
        {
            Attack box = current >= 0 && current < Count ? Hitbox(patterns[current]) : null;

            if (wired)
            {
                wired = false;

                if (box != null) box.OnHit -= HandleHit;
                if (owner != null && owner.Physics != null) owner.Physics.OnWallHit -= HandleWall;
            }

            if (box != null) box.End();
            hitboxOpen = false;
        }

        private void HandleHit(Combat victim) => sequence.HitOrWall();
        private void HandleWall(Physics.WallHit wall) => sequence.HitOrWall();

        private void OnDisable()
        {
            Cancel();
        }
    }
}
