// 스폰 주변 장치 — 배치 · 스폰 가드 · 예고 표시 · 적 레이어 · 공격 토큰 풀.
// 스폰 자체는 EnemySpawnService(프리팹 고정)가 한다. 여기는 그 앞뒤를 받친다.
//
// SpawnPlacement 가 여기 있는 이유는 <b>방과 아레나가 함께 쓰기 때문</b>이다.
// 계산은 둘로 갈라져 있지만(WaveSpawnPlanner · ArenaSpawnPlanner) 결과는 한 벌이다.
// 계획서: docs/Stage_Encounter_Unification_Plan.md (3.2)

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

    // ══ SpawnBurrow ═══════════════════════════════════════════

    /// <summary>
    /// 솟아오르는 동안 몸을 배경 뒤에 두었다가, 지면에 가까워지면 앞으로 꺼낸다.
    ///
    /// <see cref="EnemySpawnGuard"/>와 같은 모양이다. 저쪽이 벽 진입선을 보는 자리에서
    /// 이쪽은 <b>높이</b>를 본다. 억제 자체는 <see cref="EntranceGuard"/>가 하고 있고
    /// (<see cref="EntrancePlayer"/>가 걸었다), 여기는 <b>언제 보이기 시작하는가</b>만 정한다.
    ///
    /// 판정은 여기서 안 켠다 — 솟는 동안은 여전히 연출 구간이라, 다 올라와야 맞고 때린다.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class SpawnBurrow : MonoBehaviour
    {
        private Enemy enemy;
        private float revealY;
        private bool armed;

        /// <summary>
        /// 솟기 시작한 <b>직후</b>에 <see cref="EnemySpawnService"/>가 부른다.
        /// <paramref name="groundY"/>는 다 올라왔을 때 설 높이다.
        /// </summary>
        public void Arm(float groundY)
        {
            enemy = GetComponent<Enemy>();
            revealY = BurrowRules.RevealY(groundY);
            armed = true;
        }

        private void Update()
        {
            if (!armed) return;

            if (transform.position.y >= revealY)
            {
                GetComponent<EntranceGuard>()?.Reveal();
                Done();
                return;
            }

            // 연출이 중간에 잘렸는데(취소 · 스테이지 종료) 아직 안 나타났으면 여기서 꺼낸다.
            // 안 꺼내면 화면에 없는 적이 살아 있어서 웨이브가 영영 안 끝난다.
            if (enemy != null && !EntranceDirector.IsPlaying(enemy))
            {
                GetComponent<EntranceGuard>()?.Reveal();
                Done();
            }
        }

        private void Done()
        {
            armed = false;
            Destroy(this);
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
}
