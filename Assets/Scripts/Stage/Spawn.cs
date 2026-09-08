// 스폰 주변 장치 — 스폰 가드 · 예고 표시 · 적 레이어 · 공격 토큰 풀.
// 스폰 자체는 EnemySpawnService(프리팹 고정)가 한다. 여기는 그 앞뒤를 받친다.

using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ EnemySpawnGuard ═══════════════════════════════════════════

    /// <summary>
    /// 벽에서 걸어 나오는 동안 <b>판정을 전부 끄고 벽 뒤에 숨긴다.</b>
    /// 진입이 끝나면 스스로 원상복구하고 사라진다.
    ///
    /// <b>억제 자체는 <see cref="EntranceGuard"/>가 한다.</b> 동료도 교대·시전으로 화면 밖을
    /// 오가게 되면서 같은 억제가 양쪽에 필요해졌고, 두 벌로 두면 반드시 한쪽만 고쳐진다.
    /// 여기 남은 것은 <b>벽 진입선 판정</b> — 언제 벽 앞으로 나와야 하는가뿐이다.
    ///
    /// 시각 처리는 <b>정렬 순서</b>로 한다. 진입 중에는 배경 벽보다 뒤에 그려지다가,
    /// 진입선을 넘는 순간 앞으로 올라온다. 페이드인보다 이쪽이 벨트스크롤 감각에 훨씬 잘 맞는다.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemySpawnGuard : MonoBehaviour
    {
        /// <summary>
        /// 진입 중 정렬 순서에 더하는 값.
        /// 실제 값은 <see cref="EntranceGuard.HiddenSortingOffset"/>이 갖고 있다 —
        /// 이 이름으로 참조하던 코드와 테스트가 있어 창구만 남긴다.
        /// </summary>
        public const int HiddenSortingOffset = EntranceGuard.HiddenSortingOffset;

        private Enemy enemy;
        private EnemyControl control;
        private EntranceGuard guard;

        private SpawnWall wall;
        private float entryLine;
        private bool crossed;
        private bool armed;

        /// <summary>진입 연출이 아직 도는 중인가. 라운드 클리어 판정이 이 값을 센다.</summary>
        public bool IsEntering => armed;

        /// <summary>
        /// 소환 직후 <see cref="EnemySpawnService"/>가 부른다.
        /// <see cref="EnemyControl.BeginSpawnEntry"/>보다 <b>먼저</b> 불러야 한다 —
        /// 첫 프레임에 판정이 켜져 있으면 벽 안쪽에서 한 대 맞을 수 있다.
        /// </summary>
        public void Arm(SpawnWall fromWall, float wallEntryLine)
        {
            wall = fromWall;
            entryLine = wallEntryLine;
            crossed = false;
            armed = true;

            Resolve();

            guard = EntranceGuard.Arm(enemy);
        }

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (enemy == null) enemy = GetComponent<Enemy>();
            if (control == null) control = GetComponent<EnemyControl>();
        }

        private void Update()
        {
            if (!armed) return;

            // 진입선을 넘었으면 벽 앞으로 나온다. 판정은 아직 안 켠다 —
            // 목표 셀에 닿을 때까지는 여전히 연출 구간이다.
            if (!crossed && ArenaSpawnPlanner.HasCrossedEntryLine(wall, transform.position, entryLine))
            {
                crossed = true;
                guard?.Reveal();
            }

            // 화면 밖에서 날아 들어오는 중이면 걷기가 아직 시작도 안 했다.
            // 이걸 안 보면 비행 첫 프레임에 control.IsEntering이 false라 그대로 풀려 버린다.
            if (enemy != null && enemy.IsEntering) return;

            // 진입이 끝나는 시점의 유일한 판단 근거. AI가 몸을 가져가는 순간과 같아야 한다.
            if (control != null && control.IsEntering) return;

            Release();
        }

        /// <summary>판정을 되돌리고 물러난다. 두 번 불려도 안전하다.</summary>
        public void Release()
        {
            if (!armed) return;
            armed = false;

            if (guard != null) guard.Release();
            guard = null;

            BattleLog.Log(LogCategory.State, $"{name} 진입 완료 — 판정 복구", this);

            Destroy(this);
        }

        /// <summary>
        /// 파괴·씬 언로드로 잘려도 조준 목록에 "영영 안 잡히는 적"을 남기지 않는다.
        /// <see cref="EntranceGuard"/>도 자기 <c>OnDisable</c>에서 같은 일을 하지만,
        /// 붙잡은 수를 여기서 놓아 줘야 남은 주인이 없을 때 실제로 복구된다.
        /// </summary>
        private void OnDisable()
        {
            if (!armed) return;
            armed = false;

            if (guard != null) guard.Release();
            guard = null;
        }
    }

    // ══ SpawnTelegraph ═══════════════════════════════════════════

    /// <summary>
    /// 스폰 예고 표식. 몹이 나오기 <see cref="ArenaSpawnPlanner.TelegraphLead"/>초 전에
    /// 해당 벽에 붉게 번쩍이다 사라진다.
    ///
    /// <b>플레이어가 뒤를 잡히는 건 실력 부족이어야지 정보 부족이면 안 된다.</b>
    /// 후방 스폰은 예고가 없으면 그냥 부당하게 느껴지고, 유저는 "뒤를 계속 보고 있어야 하는
    /// 게임"이라고 학습해 버린다 — 그 순간 벨트스크롤의 전방 시야 규칙이 통째로 무너진다.
    ///
    /// 스스로 수명을 세고 사라진다. 부르는 쪽은 띄우기만 하면 된다.
    /// </summary>
    public class SpawnTelegraph : MonoBehaviour
    {
        private const float BlinkHz = 6f;

        private static readonly Color MarkColor = new Color(1f, 0.25f, 0.2f, 0.85f);

        /// <summary>배경(뒷벽 -10001, 바닥 -10000)보다는 앞, 캐릭터(-300 언저리)보다는 뒤.</summary>
        private const int SortingOrder = -5000;

        private SpriteRenderer mark;
        private float life;
        private float age;

        /// <summary>
        /// 표식 하나를 띄운다. <paramref name="seconds"/>가 지나면 스스로 사라진다.
        /// </summary>
        /// <param name="worldPoint">벽면 위 지상 좌표. 화면 위치는 벨트스크롤 투영을 거친다.</param>
        public static SpawnTelegraph Show(Vector3 worldPoint, float seconds, Vector2 size)
        {
            var go = new GameObject("SpawnTelegraph");
            go.transform.position = BeltScroll.ToView(worldPoint);
            go.transform.rotation = BeltScroll.Billboard;

            var telegraph = go.AddComponent<SpawnTelegraph>();
            telegraph.life = Mathf.Max(0.05f, seconds);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = MarkColor;
            sr.sortingOrder = SortingOrder;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            telegraph.mark = sr;
            return telegraph;
        }

        private void Update()
        {
            // 예고는 연출이다. 불릿타임에 얼면 "곧 나온다"는 정보가 멎어 버린다.
            age += Time.unscaledDeltaTime;

            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }

            if (mark == null) return;

            // 끝으로 갈수록 빨리 깜빡인다 — 남은 시간이 그대로 읽힌다.
            float urgency = Mathf.Clamp01(age / life);
            float blink = Mathf.PingPong(age * BlinkHz * (0.6f + urgency), 1f);

            Color c = MarkColor;
            c.a = Mathf.Lerp(0.25f, 0.95f, blink);
            mark.color = c;
        }

        /// <summary>
        /// 1x1 흰 스프라이트. 프로젝트에 전용 애셋을 만들지 않으려고 코드로 굽는다 —
        /// 한 장을 모두가 공유하므로 표식이 몇 개 떠도 텍스처는 하나다.
        /// </summary>
        private static Sprite cached;

        private static Sprite SolidSprite()
        {
            if (cached != null) return cached;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            cached = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            cached.hideFlags = HideFlags.HideAndDontSave;
            return cached;
        }
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
