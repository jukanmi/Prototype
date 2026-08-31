using Prototype.YG;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 파티를 조립한다. <c>BattleInput</c> 프리팹 루트에 붙는다.
    ///
    /// <b>동료마다 제 프리팹을 인스턴스화한다.</b> 예전에는 프리팹 안에 <see cref="Ally"/> 더미
    /// 슬롯 4칸을 미리 구워 두고 거기에 표만 꽂았다. 그 구조는 실행 순서 문제를 확실히 없애 주는
    /// 대신, <b>동료 넷이 전부 같은 몸</b>이어야 한다는 값을 치렀다 — 체형 · 콜라이더 · 애니메이션
    /// 계층이 다른 동료를 만들 자리가 없었고, 씬 뷰에서 누가 누구인지도 안 보였다.
    ///
    /// 지금은 <see cref="PartyMemberData.prefab"/> · <see cref="PlayerData.prefab"/>이 몸을 정하고
    /// 여기가 그것을 만든다. 비어 있으면 <see cref="defaultAllyPrefab"/> ·
    /// <see cref="defaultPlayerPrefab"/>으로 떨어지므로 기존 표는 하나도 안 깨진다.
    ///
    /// <b>몸은 꺼진 채로 태어난다.</b> <c>Instantiate</c>는 활성 오브젝트의 <c>Awake</c>를
    /// 그 자리에서 부른다 — 만들고 나서 표를 꽂으면 <see cref="Ally.ApplyData"/>가 이미 지나간
    /// 뒤라 아무것도 안 먹는다. 그래서 <c>~Staging</c>(비활성 임시 부모) 밑에서 만들고,
    /// 표를 꽂은 다음 <see cref="partyRoot"/>로 옮기며 깨운다. 이 순서가 이 클래스의 핵심이다.
    ///
    /// <b>실행 순서 -200을 유지한다.</b> 프리팹화로 없어진 것이 아니다:
    /// <list type="bullet">
    /// <item><see cref="Awake"/>가 만든 몸이 <c>TagSwapController</c> · <c>BulletTimeController</c> ·
    /// <c>CameraFollow</c> · <c>TargetSelector</c> · <c>DebugComboHUD</c>(전부 0)의 <c>Awake</c>보다
    /// <b>먼저</b> 존재해야 한다. 그 다섯의 <c>FindAnyObjectByType&lt;Player&gt;</c> 폴백이
    /// 살아 있는 이유이자, 그것이 안전한 이유다.</item>
    /// <item><see cref="Start"/>의 체력 복원이 <c>TagSwapController.Start</c>(0)의
    /// 로스터 초기화보다 먼저 끝나야 한다.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PartyAssembler : MonoBehaviour
    {
        [Header("기본 몸 — 표에 프리팹이 없을 때 쓴다")]
        [Tooltip("PlayerData.prefab 이 비었을 때 쓸 주인공 프리팹.")]
        [SerializeField] private Player defaultPlayerPrefab;

        [Tooltip("PartyMemberData.prefab 이 비었을 때 쓸 동료 프리팹.")]
        [SerializeField] private Ally defaultAllyPrefab;

        [Tooltip("만든 몸들을 담는 컨테이너. 이 노드와 루트는 항상 원점 · 무회전 · 배율 1이어야 한다.")]
        [SerializeField] private Transform partyRoot;

        [Header("배선")]
        [Tooltip("비우면 같은 오브젝트에서 찾는다. 만든 주인공을 여기에 밀어 넣는다.")]
        [SerializeField] private TagSwapController swap;

        [Tooltip("비우면 자식에서 찾는다. 만든 주인공을 여기에 밀어 넣는다 — 덱이 파티 카드를 걷는다.")]
        [SerializeField] private BulletTimeController bulletTime;

        [Header("단독 실행")]
        [Tooltip("Boot 씬을 거치지 않고 스테이지 씬만 Play 할 때 쓸 파티.\n\n" +
                 "Boot 를 거쳐 들어오면 GameManager(런의 주인)가 고른 조합이 이긴다 — " +
                 "여기 값은 무시된다. BulletTimeController 의 standaloneStartupMode 와 같은 규칙이다.")]
        [SerializeField] private PartyLoadout standaloneLoadout;

        /// <summary>실제로 쓰인 조합. 파티 선택 UI · 디버그 HUD가 읽는다.</summary>
        public PartyLoadout Loadout { get; private set; }

        /// <summary>이번 스테이지에 실제로 선 주인공. <see cref="Awake"/> 이후에만 유효하다.</summary>
        public Player Hero { get; private set; }

        /// <summary>이번 스테이지에 실제로 선 동료 4칸. 안 채워진 칸은 <c>null</c>이다.</summary>
        public Ally[] Members { get; private set; } = new Ally[PartyLoadout.MaxMembers];

        /// <summary>몸을 꺼진 채로 만들기 위한 임시 부모. <see cref="BuildParty"/> 끝에 버린다.</summary>
        private Transform stage;

        private void Awake()
        {
            if (swap == null) swap = GetComponent<TagSwapController>();
            if (bulletTime == null) bulletTime = GetComponentInChildren<BulletTimeController>(true);
            if (partyRoot == null) partyRoot = transform;

            EnforceContainerTransform();

            Loadout = PartyAssembleRules.Pick(
                GameManager.Instance != null ? GameManager.Instance.Loadout : null,
                standaloneLoadout);

            if (Loadout == null)
                BattleLog.Warn(LogCategory.State,
                    "PartyLoadout 이 없다 — 기본 프리팹으로 주인공만 선다. " +
                    "BattleInput 의 PartyAssembler 에 standaloneLoadout 을 꽂을 것.", this);

            BuildParty();
            PlaceParty();
        }

        /// <summary>
        /// 지난 스테이지에서 물고 온 체력을 되돌린다.
        ///
        /// <b>Awake가 아니라 Start인 이유</b>는 <see cref="Combat"/>가 자기 <c>Awake</c>에서
        /// <see cref="Energy"/>를 만들기 때문이다. 그 전에 비율을 넣으면 곧바로 프리팹의
        /// <c>maxHealth</c>로 덮인다.
        ///
        /// 이 컴포넌트는 실행 순서 -200이라 <c>TagSwapController.Start</c>(0)보다도 먼저 돈다 —
        /// 첫 몸이 무대에 서기 전에 체력이 확정된다.
        /// </summary>
        private void Start()
        {
            PartyState state = RunProgression.Current.Party;

            // 첫 스테이지다. 기록이 없는 것과 "만피로 기록됐다"는 구분되어야 하므로
            // 여기서 끝내되, 무슨 일이 있었는지는 남긴다.
            if (!state.HasSnapshot)
            {
                BattleLog.Log(LogCategory.State,
                    "파티 상태 기록이 없다 — 첫 스테이지이거나 지난 전환에서 못 찍었다. 전원 만피로 시작한다.", this);
                return;
            }

            RestoreHealth(Hero, state.HeroHpRatio);

            int restored = 0;
            foreach (Ally a in Members)
            {
                if (a == null || a.Data == null) continue;

                RestoreHealth(a, state.HpRatioOf(a.Data));
                restored++;
            }

            BattleLog.Log(LogCategory.State,
                $"파티 상태 복원 — 동료 {restored}명 · 전사 {state.DeadCount}명 · " +
                $"주인공 체력 {state.HeroHpRatio:P0}", this);
        }

        private static void RestoreHealth(Entity body, float ratio)
        {
            if (body == null || body.Combat == null || body.Combat.Health == null) return;
            if (ratio >= 1f) return;   // 기록이 없거나 만피다. 건드릴 것이 없다.

            body.Combat.Health.SetRatio(ratio);
        }

        // ── 조립 ────────────────────────────────────────

        /// <summary>
        /// 로드아웃대로 몸을 만든다.
        ///
        /// <b>빈 칸과 전사한 동료는 아예 안 만든다.</b> 슬롯 시절에는 미리 구워진 몸을
        /// 지워야 했고, 그때 <c>SetActive(false)</c>를 <c>Destroy</c>보다 <b>먼저</b> 해야 한다는
        /// 함정이 있었다 — 안 그러면 그 몸이 한 프레임 동안 <see cref="BattleRegistry"/>에 올라
        /// 있지도 않을 동료를 적 AI가 타겟으로 잡았다. 만들지 않으면 그 함정 자체가 없다.
        ///
        /// 죽은 동료를 빈 칸과 똑같이 다루는 이유는 그대로다 — 죽은 상태로 세우려면
        /// 사망 연출 · 레지스트리 · 상태머신이 한 번씩 더 돌아야 하는데, 3인 파티 경로
        /// (<c>TagSwapRules</c>의 null 칸 건너뛰기 · <c>CollectPartyCards</c>의 skip ·
        /// <c>AvailableRoles</c>에서 빠짐 · <c>DeckRules</c>의 목표 축소)가 이미 전부 성립한다.
        /// </summary>
        private void BuildParty()
        {
            OpenStage();

            Hero = BuildHero();
            Members = BuildMembers();

            if (Hero != null) Hero.SetParty(Members);
            else BattleLog.Warn(LogCategory.State,
                "주인공 프리팹이 없다 — 태그 로스터가 통째로 안 만들어진다. " +
                "PartyAssembler 의 defaultPlayerPrefab 을 꽂거나 PlayerData 에 프리팹을 넣을 것.", this);

            CloseStage();
            PushReferences();
        }

        private Player BuildHero()
        {
            PlayerData data = Loadout != null ? Loadout.hero : null;

            GameObject source = Resolve(data != null ? data.prefab : null,
                                        defaultPlayerPrefab != null ? defaultPlayerPrefab.gameObject : null);
            if (source == null) return null;

            var hero = Spawn<Player>(source, "Player");
            if (hero == null) return null;

            // 표가 없어도 정상이다 — 주인공이 하나뿐인 동안은 프리팹 고정값과 같은 값이라
            // 로드아웃에 안 꽂아도 지금까지와 똑같이 돈다.
            if (data != null)
            {
                hero.SetData(data);
                hero.name = !string.IsNullOrEmpty(data.heroId) ? data.heroId : data.Label;
                ApplyLook(hero.transform, data.animatorController, data.bodyScale, data.spriteTint);
            }

            AllyLayers.Apply(hero.gameObject);
            Wake(hero.gameObject);

            return hero;
        }

        private Ally[] BuildMembers()
        {
            var party = new Ally[PartyLoadout.MaxMembers];
            PartyMemberData[] picked = PartyAssembleRules.Resolve(Loadout, party.Length);

            for (int i = 0; i < party.Length; i++)
            {
                PartyMemberData member = picked[i];
                if (member == null) continue;

                if (RunProgression.Current.Party.IsDead(member))
                {
                    BattleLog.Log(LogCategory.State,
                        $"{member.Label} — 이번 런에서 전사했다. 몸을 만들지 않는다.", this);
                    continue;
                }

                if (!PartyAssembleRules.IsValid(member, out string reason))
                    BattleLog.Warn(LogCategory.Deck, $"파티 저작 오류 — {reason}", this);

                GameObject source = Resolve(member.prefab,
                                            defaultAllyPrefab != null ? defaultAllyPrefab.gameObject : null);
                if (source == null)
                {
                    BattleLog.Warn(LogCategory.State,
                        $"{member.Label} 의 몸을 만들 프리팹이 없다 — 이 칸을 비운다. " +
                        "PartyMemberData.prefab 을 채우거나 defaultAllyPrefab 을 꽂을 것.", this);
                    continue;
                }

                Ally ally = Spawn<Ally>(source, member.memberId);
                if (ally == null) continue;

                ally.SetData(member);
                ally.name = !string.IsNullOrEmpty(member.memberId) ? member.memberId : member.Label;
                ApplyLook(ally.transform, member.animatorController, member.bodyScale, member.spriteTint);

                // 레이어는 프리팹이 아니라 여기가 보장한다. 동료마다 프리팹이 갈리면서
                // "그 동료만 적을 통과한다"가 생길 자리가 넷으로 늘었다 — 한 곳에서 칠한다.
                AllyLayers.Apply(ally.gameObject);
                Wake(ally.gameObject);

                party[i] = ally;
            }

            return party;
        }

        /// <summary>표의 프리팹이 우선, 없으면 기본 프리팹. 둘 다 없으면 null.</summary>
        private static GameObject Resolve(GameObject custom, GameObject fallback)
            => custom != null ? custom : fallback;

        // ── 생성 · 기상 ─────────────────────────────────

        /// <summary>
        /// 꺼진 임시 부모를 연다. 여기 밑에서 태어난 몸은 <c>activeInHierarchy</c>가 false라
        /// Unity 가 <c>Awake</c>를 아예 안 부른다 — 표를 꽂을 틈이 그 사이다.
        /// </summary>
        private void OpenStage()
        {
            var go = new GameObject("~Staging");
            go.SetActive(false);
            go.transform.SetParent(partyRoot, false);
            stage = go.transform;
        }

        private void CloseStage()
        {
            if (stage == null) return;

            // 몸은 전부 Wake 에서 partyRoot 로 옮겨 갔다. 남은 것은 빈 껍데기뿐이다.
            Destroy(stage.gameObject);
            stage = null;
        }

        /// <summary>
        /// 프리팹 하나를 <b>꺼진 채로</b> 만든다. 루트에 <typeparamref name="T"/>가 없으면
        /// 만든 것을 도로 버리고 null 을 돌려준다 — 저작 실수가 조용히 넘어가면
        /// 증상은 "그 동료만 아무것도 안 한다"로만 나온다.
        /// </summary>
        private T Spawn<T>(GameObject source, string label) where T : Component
        {
            GameObject go = Instantiate(source, stage);

            var body = go.GetComponent<T>();
            if (body == null)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{source.name} 의 루트에 {typeof(T).Name} 이(가) 없다 — " +
                    $"'{label}' 자리를 비운다.", this);

                Destroy(go);
                return null;
            }

            go.name = !string.IsNullOrEmpty(label) ? label : source.name;
            return body;
        }

        /// <summary>
        /// 몸을 <see cref="partyRoot"/>로 옮기며 깨운다. 이 줄이 <c>Awake</c> ·
        /// <c>OnEnable</c>을 부르므로, <b>표는 이 앞에서 전부 꽂혀 있어야 한다.</b>
        ///
        /// 프리팹이 꺼진 채로 저장돼 있어도 여기서 켠다 — 그런 프리팹은 영영 안 깨어나고,
        /// 화면에는 "그 동료만 안 나온다"로만 보인다.
        /// </summary>
        private void Wake(GameObject body)
        {
            body.transform.SetParent(partyRoot, false);
            body.transform.localPosition = Vector3.zero;
            body.SetActive(true);
        }

        // ── 외형 ────────────────────────────────────────

        /// <summary>
        /// 공용 프리팹 하나를 색과 크기로 구분하던 시절의 통로. 제 프리팹을 가진 동료는
        /// 이 값들이 기본값(1 · 흰색 · null)이라 여기가 전부 통과한다.
        /// </summary>
        private static void ApplyLook(Transform body, RuntimeAnimatorController controller,
                                      float bodyScale, Color tint)
        {
            // 애니메이터는 반드시 Awake 전에 꽂는다. EntityAnimator.Awake 가 컨트롤러를
            // AnimatorOverrideController 로 감싸므로 그 뒤에 바꾸면 스킬 클립 교체가 통째로 어긋난다.
            if (controller != null)
            {
                var anim = body.GetComponent<EntityAnimator>();
                if (anim != null) anim.SetController(controller);
            }

            ApplyBodyScale(body, bodyScale);
            ApplyTint(body, tint);
        }

        /// <summary>
        /// 몸 크기. <see cref="BeltScrollView"/>에 넘긴다 — <b>트랜스폼에 직접 쓰면 안 된다.</b>
        ///
        /// 예전에는 여기서 <c>View</c>와 <c>Shadow</c>의 <c>localScale</c>에 직접 곱했다.
        /// 그 두 노드의 배율은 <see cref="BeltScrollView"/>가 깊이 배율로 <b>매 프레임 통째로
        /// 덮어쓰는</b> 자리라, 값은 프리팹 인스펙터에만 남고 재생하는 순간 사라졌다 —
        /// <c>bodyScale</c>이 오래도록 아무 효과가 없었던 이유이고, 에러가 아니라
        /// "왜 다 똑같이 보이지"로만 드러나서 오래 안 잡혔다.
        ///
        /// 루트를 안 키우는 원칙은 그대로다. 루트 콜라이더 치수를
        /// <see cref="Entity.HurtboxSize"/>가 읽어 <b>스킬 사거리 배수</b>로 쓰기 때문에
        /// (<c>SkillData.castRangeScale</c>), 루트를 키우면 몸집이 큰 동료의 스킬만 사거리가 늘어난다.
        /// </summary>
        private static void ApplyBodyScale(Transform body, float scale)
        {
            if (body == null || Mathf.Approximately(scale, 1f) || scale <= 0f) return;

            var view = body.GetComponent<BeltScrollView>();
            if (view != null)
            {
                view.MultiplyBodyScale(scale);
                return;
            }

            BattleLog.Warn(LogCategory.State,
                $"{body.name}: BeltScrollView 가 없어 몸 배율 {scale:0.##}를 적용하지 못했다.", body);
        }

        /// <summary>
        /// 직업 색. 흰색은 "칠하지 않는다"는 뜻이다.
        ///
        /// <b>알파는 건드리지 않는다</b> — 등장 · 사망 연출이 알파를 애니메이션으로 굴리고
        /// (<c>Ally_Dead</c> 클립의 <c>m_Color.a</c>), 여기서 덮으면 그 연출이 첫 프레임에 잘린다.
        /// </summary>
        private static void ApplyTint(Transform body, Color tint)
        {
            if (body == null || tint == Color.white) return;

            Transform sprite = body.Find(PartyMemberData.SpritePath);
            var sr = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;
            if (sr == null) return;

            sr.color = new Color(tint.r, tint.g, tint.b, sr.color.a);
        }

        // ── 주입 ────────────────────────────────────────

        /// <summary>
        /// 만든 주인공을 그를 필요로 하는 곳에 <b>밀어 넣는다</b>.
        ///
        /// 슬롯 시절에는 이 참조들이 <c>BattleInputBuilder</c>가 프리팹에 구워 둔 것이었다.
        /// 몸이 런타임 생성물이 되면서 그 참조가 사라졌으므로 여기가 대신 채운다 —
        /// 받는 쪽의 <c>FindAnyObjectByType</c> 폴백은 <b>안전망으로 남긴다</b>.
        /// 파티 없이 도는 스킬 실험 씬이 그 폴백으로 살아 있다.
        /// </summary>
        private void PushReferences()
        {
            if (Hero == null) return;

            if (swap != null) swap.SetHero(Hero);
            else BattleLog.Warn(LogCategory.State,
                "TagSwapController 가 없다 — 태그 로스터가 안 만들어진다.", this);

            if (bulletTime != null)
            {
                bulletTime.SetHero(Hero);
                bulletTime.GetComponent<DebugComboHUD>()?.SetHero(Hero);
            }
            else BattleLog.Warn(LogCategory.State,
                "BulletTimeController 가 없다 — 덱이 파티 카드를 못 걷는다.", this);
        }

        // ── 배치 ────────────────────────────────────────

        /// <summary>
        /// 파티를 이 스테이지의 자리로 옮긴다.
        ///
        /// 실제 착지는 <see cref="TagSwapController"/>가 <c>Start</c>에서 한다 —
        /// <see cref="Physics.Teleport"/>가 접지 · 관성 · 낙하속도를 함께 정리하는데
        /// 그건 <c>Physics.Awake</c>가 끝난 뒤에야 부를 수 있기 때문이다.
        ///
        /// 여기서는 트랜스폼만 미리 옮긴다. 안 그러면 첫 프레임에 몸이 원점에 한 번 찍히고,
        /// 카메라가 그 자리를 잡아 화면이 눈에 띄게 튄다.
        /// </summary>
        private void PlaceParty()
        {
            PartySpawnPoint.Resolve(out Vector3 ground, out Vector3 facing);

            if (Hero != null) Hero.transform.position = ground;

            foreach (Ally a in Members)
                if (a != null) a.transform.position = ground;

            if (swap != null) swap.SeedSeat(ground, facing);
            else BattleLog.Warn(LogCategory.State,
                "TagSwapController 가 없어 시작 자리를 넘기지 못했다 — 파티가 원점에서 시작한다.", this);
        }

        // ── 컨테이너 불변식 ─────────────────────────────

        /// <summary>
        /// 루트와 <see cref="partyRoot"/>는 <b>원점 · 무회전 · 배율 1</b>이어야 한다.
        ///
        /// 이게 "물리 간섭 없는 컨테이너"의 실제 내용이다. 어긋나면 증상이 전부 딴 데서 난다:
        /// <list type="bullet">
        /// <item>위치가 어긋나면 <see cref="Prototype.YG.BattleSceneController"/>의 출구 판정이
        /// 월드 x 를 읽으므로 <b>스테이지가 안 넘어간다</b>.</item>
        /// <item>배율이 어긋나면 자식 히트박스가 통째로 커지고 작아진다 — 데미지 판정이 조용히 틀어진다.</item>
        /// </list>
        /// 조용히 고치고 한 번 경고한다. 막는 쪽은 EditMode 테스트(<c>PartyPrefabTests</c>)다.
        /// </summary>
        private void EnforceContainerTransform()
        {
            Fix(transform, "BattleInput 루트");
            if (partyRoot != null && partyRoot != transform) Fix(partyRoot, "Party 컨테이너");
        }

        private static void Fix(Transform t, string label)
        {
            bool moved = t.position != Vector3.zero
                      || t.rotation != Quaternion.identity
                      || t.localScale != Vector3.one;

            if (!moved) return;

            BattleLog.Warn(LogCategory.State,
                $"{label}의 트랜스폼이 원점이 아니다 " +
                $"(pos {t.position}, scale {t.localScale}) — 원점으로 되돌린다. " +
                "이 컨테이너가 움직이면 출구 판정과 히트박스 크기가 함께 어긋난다.", t);

            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
        }
    }
}
