using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ SpawnPlacement ═══════════════════════════════════════════

    /// <summary>
    /// 적 한 기가 <b>언제 어디서 어떻게</b> 나오는지. 소환 창구가 받는 유일한 좌표 묶음이다.
    ///
    /// <b>방과 아레나가 같은 구조체를 쓴다.</b> 예전에는 방이 <c>SpawnPlacement</c>,
    /// 아레나가 <c>ArenaSpawnPlan</c>으로 갈라져 있었다. 소환 창구도 두 갈래였고,
    /// 그래서 땅속 등장 같은 새 연출이 한쪽에만 붙었다.
    ///
    /// 뒤의 넷은 <b>벽에서 나오는 배치만</b> 채운다. 방 안에서 나오는 배치는
    /// <see cref="wall"/>이 <see cref="SpawnWall.None"/>이고 나머지는 안 읽힌다.
    /// </summary>
    public struct SpawnPlacement
    {
        /// <summary>몸이 나타나는 자리.</summary>
        public Vector3 spawnPoint;

        /// <summary>걸어 들어가 자리 잡는 지점. 제자리에서 나오는 배치는 등장 자리와 같다.</summary>
        public Vector3 entryPoint;

        /// <summary>도착한 뒤 AI가 깨어나기까지 서 있는 시간.</summary>
        public float holdSeconds;

        /// <summary>조우 시작 기준 등장 시각. <b>연출이 시작되는 시각</b>이지 참전 시각이 아니다.</summary>
        public float appearAt;

        // ── 예고 ────────────────────────────────────────

        /// <summary>
        /// 예고 표식이 뜨는 시각. <b>음수면 예고가 없다</b>(<see cref="HasTelegraph"/>).
        ///
        /// 벽에서 나오거나 땅속에서 솟는 적만 예고를 받는다. 걸어 들어오거나 날아오는 적은
        /// 오는 모습 자체가 예고라, 표식을 더 얹으면 화면만 시끄러워진다.
        /// </summary>
        public float telegraphAt;

        /// <summary>예고 표식이 뜨는 자리.</summary>
        public Vector3 telegraphPoint;

        // ── 벽 진입 전용 ────────────────────────────────

        /// <summary>어느 벽에서 나오는가. <see cref="SpawnWall.None"/>이면 벽이 아니다.</summary>
        public SpawnWall wall;

        /// <summary>
        /// 이 선을 넘는 순간 정렬 순서를 앞으로 올린다 — 벽 뒤에 있다가 걸어 나오는 그림이 된다.
        /// 벽이 좌우면 X, 앞뒤면 Z 기준이다.
        /// </summary>
        public float entryLine;

        /// <summary>벽에서 걸어 나오는 배치인가.</summary>
        public bool FromWall => wall != SpawnWall.None;

        /// <summary>예고 표식이 붙는 배치인가.</summary>
        public bool HasTelegraph => telegraphAt >= 0f;
    }

    // ══ SpawnRouteRules ═══════════════════════════════════════════

    /// <summary>소환 창구가 고르는 세 갈래.</summary>
    public enum SpawnRoute
    {
        /// <summary>화면 밖에서 날아와 방 가장자리에 선 다음 걸어 들어간다.</summary>
        Edge = 0,

        /// <summary>발밑 땅속에서 솟아오른다.</summary>
        Ground = 1,

        /// <summary>벽 뒤에서 걸어 나온다. 아레나가 이것이다.</summary>
        Wall = 2,
    }

    /// <summary>
    /// 어느 갈래로 내보낼 것인가. <b>순수 함수</b>라 씬 없이 테스트가 부를 수 있다.
    ///
    /// 갈래를 고르는 것은 저작한 모션 <b>하나가 아니다.</b> 벽 진입은 배치가 실제로
    /// 벽을 들고 있어야 성립한다 — 시작 좌표가 아레나 경계 <b>밖</b>이고, 벽을 통과해
    /// 들어오는 동안 몸통 판정을 꺼 둬야 하기 때문이다.
    ///
    /// 모션만 보고 갈랐더니 실제로 이런 일이 있었다. 통합 뒤 디렉터가 아레나도 같은 창구로
    /// 보내게 됐는데, 창구는 벽 진입을 "방에서는 성립 안 하는 것"으로만 알고 있어서
    /// 가장자리 갈래로 떨어뜨렸다. 몸이 <b>벽 뒤에 놓인 채 판정이 켜졌고</b>,
    /// 그대로 벽에 막혀 아레나에 못 들어왔다. 화면으로는 "저 적이 벽에 끼었다"로만 보인다.
    /// </summary>
    public static class SpawnRouteRules
    {
        /// <summary>
        /// <paramref name="placementHasWall"/>은 <see cref="SpawnPlacement.FromWall"/>이다.
        /// 좌표가 벽을 들고 있으면 <b>모션이 무엇이든</b> 벽 갈래여야 한다 —
        /// 시작 자리가 이미 벽 바깥이라 다른 갈래로는 들어올 방법이 없다.
        /// </summary>
        public static SpawnRoute For(SpawnMotion motion, bool placementHasWall)
        {
            if (placementHasWall) return SpawnRoute.Wall;
            if (motion == SpawnMotion.Burrow) return SpawnRoute.Ground;

            return SpawnRoute.Edge;
        }

        /// <summary>
        /// 저작한 모션과 배치가 어긋났는가. 벽 진입으로 저작됐는데 좌표에 벽이 없는 경우다 —
        /// 웨이브 방의 줄에 벽 진입을 찍으면 이렇게 된다.
        ///
        /// 소환을 건너뛰지는 않는다. 안 나오면 그 조우가 영영 전멸하지 않는다.
        /// </summary>
        public static bool IsMismatch(SpawnMotion motion, bool placementHasWall)
            => motion == SpawnMotion.FromWall && !placementHasWall;
    }

    // ══ SpawnCensus ═══════════════════════════════════════════

    /// <summary>
    /// 낳은 몸을 세어 <see cref="EncounterCensus"/>의 상태 칸을 채운다.
    ///
    /// <b>판정과 세기를 갈라 둔 자리다.</b> 판정(<see cref="EncounterClearRules"/>)은
    /// 씬을 전혀 모르는 순수 함수라 테스트가 직접 부를 수 있어야 하고, 씬을 읽는 절반은
    /// 여기 하나만 둔다. 방과 아레나가 같은 함수를 쓰므로 세는 기준이 갈릴 자리가 없다.
    /// </summary>
    public static class SpawnCensus
    {
        /// <summary>
        /// <paramref name="bodies"/>를 훑어 진입 중 · 전투 중 · 사망 연출 중으로 나눠 센다.
        /// <c>spawned</c>도 같이 채운다 — 한 기도 안 나온 조우를 클리어로 치지 않기 위해서다.
        ///
        /// 파괴된 몸(<c>null</c>)은 목록에 남아도 세지 않는다. 다만 <c>spawned</c>에는
        /// 들어간다 — "나오긴 했다"는 사실은 사라지면 안 된다.
        /// </summary>
        public static void CountBodies(List<Enemy> bodies, ref EncounterCensus census)
        {
            if (bodies == null) return;

            census.spawned += bodies.Count;

            for (int i = 0; i < bodies.Count; i++)
            {
                Enemy e = bodies[i];
                if (e == null) continue;

                if (e.Combat.IsDead) { census.dying++; continue; }

                // 진입 중은 판정이 꺼져 있어 때릴 수도 맞을 수도 없다. 그래도 위협이다.
                var control = e.GetComponent<EnemyControl>();
                if (control != null && control.IsEntering) census.entering++;
                else census.fighting++;
            }
        }
    }

    // ══ BurrowRules ═══════════════════════════════════════════

    /// <summary>
    /// 땅속에서 솟아오르는 등장의 숫자와 판단. <b>순수 함수</b>다.
    ///
    /// 이 등장이 다른 것들과 다른 점은 <b>예고가 선택이 아니라는 것</b>이다.
    /// 벽에서 걸어 나오거나 화면 밖에서 날아오는 적은 오는 모습 자체가 예고지만,
    /// 발밑에서 솟는 적은 표식이 없으면 반응할 <i>정보</i>가 아예 없다.
    /// 그러면 유저는 "바닥을 계속 보고 있어야 하는 게임"으로 학습하고,
    /// 그 순간 벨트스크롤의 전방 시야 규칙이 통째로 무너진다.
    /// </summary>
    public static class BurrowRules
    {
        /// <summary>출발 지점이 지면 아래로 내려가는 깊이. 몸 하나가 완전히 잠기는 정도다.</summary>
        public const float Depth = 1.8f;

        /// <summary>솟는 데 걸리는 시간. 화면 밖 비행보다 길다 — 올라오는 것이 보여야 한다.</summary>
        public const float Seconds = 0.5f;

        /// <summary>
        /// 지면에서 이만큼 아래에 닿으면 몸을 앞으로 꺼낸다.
        ///
        /// 스프라이트를 바닥으로 <b>가리지는 않는다</b> — 이 프로젝트의 정렬은 순서값 하나라
        /// 반만 가리는 방법이 없다. 그래서 완전히 숨김과 완전히 보임 사이의 전환점만 고른다.
        /// 값을 키우면 일찍 나타나 솟는 과정이 길게 보이고, 줄이면 늦게 튀어나온다.
        /// </summary>
        public const float RevealDepth = 0.9f;

        /// <summary>예고 표식 크기. 발밑에 깔리는 납작한 자국이다.</summary>
        public static Vector2 TelegraphSize => new Vector2(1.3f, 0.7f);

        /// <summary>꺼내기 전에 올라오는 거리. 0이면 처음부터 보이고, 깊이와 같으면 다 올라와서 튀어나온다.</summary>
        public static float RiseBeforeReveal => Depth - RevealDepth;

        /// <summary>
        /// 지면이 <paramref name="groundY"/>일 때 몸을 꺼낼 높이.
        ///
        /// 기준이 <b>지면</b>이지 출발점이 아니다. 출발점으로 재면 <see cref="SpawnBurrow"/>가
        /// 그 값을 언제 읽느냐에 답이 달라진다 — <c>EntrancePlayer.Begin</c>은 두 끝점을
        /// 계산만 하고 몸은 <b>착지점에 둔 채</b> 돌아오므로, 솟기 직전에 위치를 읽으면
        /// 지면 높이가 잡힌다. 그걸 출발점으로 착각하면 기준선이 한 몸 위로 올라가
        /// <b>영영 안 나타나는 적</b>이 된다.
        /// </summary>
        public static float RevealY(float groundY) => groundY - RevealDepth;

        /// <summary>예고가 뜨는 시각(웨이브 시작 기준). 앞이 모자라면 0으로 물린다.</summary>
        public static float TelegraphAt(float appearAt)
            => Mathf.Max(0f, appearAt - ArenaSpawnPlanner.TelegraphLead);

        /// <summary>예고가 떠 있는 시간. 등장 시각이 이르면 그만큼 짧아진다.</summary>
        public static float TelegraphSeconds(float appearAt)
            => Mathf.Max(0f, appearAt) - TelegraphAt(appearAt);

        /// <summary>
        /// 예고를 제대로 낼 시간이 있는가. 거짓이면 <b>저작 실수</b>다 —
        /// 웨이브가 열리자마자 솟는 적은 아무리 표식을 띄워도 읽을 시간이 없다.
        /// 저작 검증(<see cref="BoardProblem.BurrowTooEarly"/>)이 이 값을 읽는다.
        /// </summary>
        public static bool HasRoomForTelegraph(float appearAt)
            => appearAt >= ArenaSpawnPlanner.TelegraphLead;
    }

    // ══ EnemyLayers ═══════════════════════════════════════════

    /// <summary>
    /// 런타임에 소환한 적의 <b>레이어 배선</b>.
    ///
    /// 왜 필요한가: <c>Assets/Prefabs/Enemy_*.prefab</c>은 레이어가 Default(0)인 채로 저장돼 있다.
    /// 씬에 놓인 적은 <c>SceneLayoutBuilder</c>가 구워 줄 때 EnemyHurtbox · EnemyHitbox 로 바꾸지만,
    /// 그 빌더는 <b>씬만</b> 손본다. 프리팹을 그대로 Instantiate 하면 Default 인 채로 나오고,
    /// <see cref="Attack"/>은 충돌 매트릭스(<c>DynamicsManager</c>)를 그대로 읽으므로
    /// 결과는 <b>서로를 통과하는 적</b>이다 — 때려도 안 맞고 맞아도 안 아프다.
    /// 증상이 "가끔 안 맞는다"가 아니라 "그 웨이브만 통째로 무해하다"라서 더 헷갈린다.
    ///
    /// 레이어 번호는 3D전환_TODO.md §2 · <c>SceneLayoutBuilder</c>와 같은 이름을 쓴다.
    /// 이름으로 찾는 이유는 번호가 프로젝트 설정에 있고 코드가 그 사본을 들면 언젠가 어긋나기 때문이다.
    /// </summary>
    public static class EnemyLayers
    {
        public const string HurtboxLayer = "EnemyHurtbox";
        public const string HitboxLayer = "EnemyHitbox";

        /// <summary>
        /// 적 하나를 규약대로 맞춘다. 몸통은 피격 레이어, <see cref="Attack"/>이 붙은 자식은
        /// 전부 타격 레이어다 — 평타 히트박스와 돌진 히트박스 둘 다 해당한다.
        /// </summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;

            int hurt = LayerMask.NameToLayer(HurtboxLayer);
            int hit = LayerMask.NameToLayer(HitboxLayer);

            if (hurt < 0 || hit < 0)
            {
                BattleLog.Warn(LogCategory.Combat,
                    $"레이어 '{HurtboxLayer}' 또는 '{HitboxLayer}'가 프로젝트에 없다. " +
                    "'Prototype ▸ 씬 벨트스크롤 배치로 정리'를 한 번 돌려 레이어를 만들 것.", root);
                return;
            }

            root.layer = hurt;

            // 비활성 자식까지 본다. 돌진 히트박스는 꺼져 있는 것이 정상이다.
            foreach (Attack attack in root.GetComponentsInChildren<Attack>(true))
                attack.gameObject.layer = hit;
        }
    }

    // ══ AttackTokenPool ═══════════════════════════════════════════

    /// <summary>
    /// 동시 공격권. <b>한 화면에 적이 여섯이어도 동시에 휘두르는 적은 두셋뿐</b>이게 만든다.
    ///
    /// 이게 없으면 벨트스크롤이 성립하지 않는다. 적 AI는 각자 사거리만 보고 판단하므로,
    /// 여섯이 둘러싸면 여섯이 같은 프레임에 휘두르고 그 사이에는 <b>피할 수 있는 틈이 없다</b> —
    /// 난이도가 아니라 설계 실패다. 토큰을 쥔 적만 공격을 시도하고 나머지는 사거리 안에서
    /// 기다리게 하면, 몰려 있는 그림은 그대로 두고 들어오는 타격만 일정하게 유지된다.
    ///
    /// <b>임대(lease)</b>로 준다. 반납을 놓치는 경로가 반드시 생기기 때문이다 —
    /// 공격 도중에 죽거나, 씬이 내려가거나, 상태머신이 공격을 거절하거나. 반납이 한 번
    /// 새면 그 토큰은 영영 안 돌아오고, 증상은 "어느 순간부터 적이 아무도 안 때린다"라
    /// 원인을 짚기가 대단히 어렵다. 만료가 그 경로를 전부 덮는다.
    ///
    /// 순수 C#이다 — 시간은 부르는 쪽이 넘긴다.
    /// </summary>
    public sealed class AttackTokenPool
    {
        /// <summary>기본 허용치. 스테이지가 <see cref="SetCapacity"/>로 덮는다.</summary>
        public const int DefaultCapacity = 2;

        /// <summary>평타용 임대 기간. 평타 한 싸이클(선딜+후딜)보다 넉넉하다.</summary>
        public const float BasicLease = 2.5f;

        /// <summary>특수 행동용 임대 기간. 돌진은 예고+발동+후딜로 2초에 가깝다.</summary>
        public const float SpecialLease = 4f;

        /// <summary>
        /// 쥔 주체 → 만료 시각.
        ///
        /// 키가 <c>object</c>인 이유는 이 풀이 <b>순수 C#</b>이기 때문이다 —
        /// 유니티의 인스턴스 ID(<c>EntityId</c>)는 앞으로 int로 표현되지 않을 예정이라
        /// 숫자로 굳혀 두면 언젠가 통째로 갈아엎어야 한다. 참조 하나면 충분한 일이다.
        /// </summary>
        private readonly Dictionary<object, float> leases = new Dictionary<object, float>();

        /// <summary>만료 정리용. 매 프레임 도는 자리라 할당을 남기지 않는다.</summary>
        private readonly List<object> expired = new List<object>();

        public int Capacity { get; private set; } = DefaultCapacity;

        /// <summary>0 이하는 받지 않는다 — 아무도 공격하지 못하는 방이 조용히 만들어진다.</summary>
        public void SetCapacity(int value) => Capacity = Mathf.Max(1, value);

        /// <summary>
        /// 공격권을 얻는다. <b>이미 쥔 쪽은 언제나 성공</b>이다 —
        /// 연타 도중에 정원이 줄어들었다고 휘두르던 팔이 멈추면 그게 더 이상하다.
        /// </summary>
        /// <param name="holder">쥐는 주체. 보통 적을 모는 컴포넌트 자신이다.</param>
        /// <param name="now">지금 시각(초).</param>
        /// <param name="lease">이 시간이 지나면 자동 반납된다.</param>
        public bool TryAcquire(object holder, float now, float lease)
        {
            Prune(now);

            if (holder == null) return false;

            if (leases.ContainsKey(holder))
            {
                leases[holder] = now + Mathf.Max(0.1f, lease);
                return true;
            }

            if (leases.Count >= Capacity) return false;

            leases[holder] = now + Mathf.Max(0.1f, lease);
            return true;
        }

        /// <summary>공격이 끝났다. 쥔 적 없는 주체를 반납해도 아무 일도 일어나지 않는다.</summary>
        public void Release(object holder)
        {
            if (holder != null) leases.Remove(holder);
        }

        /// <summary>지금 이 주체가 공격권을 쥐고 있는가.</summary>
        public bool Holds(object holder, float now)
            => holder != null && leases.TryGetValue(holder, out float until) && until > now;

        /// <summary>만료되지 않은 공격권의 수. 디버그 HUD와 테스트가 읽는다.</summary>
        public int ActiveCount(float now)
        {
            Prune(now);
            return leases.Count;
        }

        /// <summary>전부 반납. 웨이브가 바뀌거나 씬이 내려갈 때.</summary>
        public void Clear() => leases.Clear();

        /// <summary>정원까지 기본값으로 되돌린다. 씬 경계에서 부른다.</summary>
        public void ResetAll()
        {
            Clear();
            Capacity = DefaultCapacity;
        }

        private void Prune(float now)
        {
            if (leases.Count == 0) return;

            expired.Clear();

            foreach (KeyValuePair<object, float> pair in leases)
                if (pair.Value <= now) expired.Add(pair.Key);

            for (int i = 0; i < expired.Count; i++)
                leases.Remove(expired[i]);
        }
    }

    /// <summary>
    /// 전투 한 판이 공유하는 공격권 한 벌. <see cref="BattleRegistry"/>와 같은 자리에 있는
    /// 전역 상태이고, 같은 이유로 <b>씬 경계에서 반드시 비운다</b> —
    /// Domain Reload 가 꺼져 있으면 지난 판의 임대가 그대로 살아남아
    /// 새 스테이지 첫 몇 초 동안 아무도 공격하지 않는다.
    /// </summary>
    public static class EnemyAttackTokens
    {
        public static AttackTokenPool Pool { get; } = new AttackTokenPool();

        /// <summary>지금 시각. 불릿타임에 얼면 안 되므로 스케일 안 된 시간을 쓴다.</summary>
        public static float Now => Time.unscaledTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pool.ResetAll();
    }
}
