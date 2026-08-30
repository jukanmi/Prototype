using Prototype.YG;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 파티를 조립한다. <c>BattleInput</c> 프리팹 루트에 붙는다.
    ///
    /// <b>동료를 만들지 않는다 — 이미 있는 슬롯에 데이터를 꽂는다.</b>
    /// 이게 이 설계에서 가장 중요한 선택이고, 이유는 실행 순서다:
    /// <see cref="Player"/>를 <c>FindAnyObjectByType</c>으로 찾는 곳이 다섯 군데 있고
    /// (<see cref="TagSwapController"/> · <see cref="BulletTimeController"/> ·
    /// <see cref="TargetSelector"/> · <see cref="CameraFollow"/> · <see cref="DebugComboHUD"/>)
    /// 전부 <c>Awake</c>에서 돈다. 런타임에 <c>Instantiate</c>로 만들면 그 다섯이 통째로
    /// "생성 전에 찾는" 순서 버그가 되고, 증상은 "가끔 카메라가 안 따라감 / 덱이 빔"이라
    /// 재현이 안 된다. 슬롯을 프리팹 안에 실체로 두면 그 다섯 줄을 한 줄도 안 건드려도 된다.
    ///
    /// 그래서 여기가 하는 일은 <b>주입 · 정리 · 배치</b> 셋뿐이다.
    ///
    /// <b>실행 순서 -200.</b> <see cref="Ally"/>(0) · <see cref="EntityAnimator"/>(0)보다 먼저
    /// 돌아야 한다 — 전자는 <c>Awake</c>에서 <c>data</c>를 읽고, 후자는 <c>Awake</c>에서
    /// 애니메이터 컨트롤러를 <c>AnimatorOverrideController</c>로 감싸 버리기 때문이다.
    /// <see cref="PlayerPilot"/>(-100)보다도 앞이다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PartyAssembler : MonoBehaviour
    {
        [Header("슬롯 — 프리팹 안의 몸들")]
        [Tooltip("태그 로스터 0번. 항상 있어야 한다.")]
        [SerializeField] private Player player;

        [Tooltip("동료 슬롯 4칸. PartyLoadout 이 안 채운 칸은 Awake 에서 지운다.")]
        [SerializeField] private Ally[] slots = new Ally[PartyLoadout.MaxMembers];

        [Tooltip("몸들을 담는 컨테이너. 이 노드와 루트는 항상 원점 · 무회전 · 배율 1이어야 한다.")]
        [SerializeField] private Transform partyRoot;

        [Header("배선")]
        [Tooltip("비우면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private TagSwapController swap;

        [Header("단독 실행")]
        [Tooltip("Boot 씬을 거치지 않고 스테이지 씬만 Play 할 때 쓸 파티.\n\n" +
                 "Boot 를 거쳐 들어오면 GameManager(런의 주인)가 고른 조합이 이긴다 — " +
                 "여기 값은 무시된다. BulletTimeController 의 standaloneStartupMode 와 같은 규칙이다.")]
        [SerializeField] private PartyLoadout standaloneLoadout;

        /// <summary>실제로 쓰인 조합. 파티 선택 UI · 디버그 HUD가 읽는다.</summary>
        public PartyLoadout Loadout { get; private set; }

        private void Awake()
        {
            if (swap == null) swap = GetComponent<TagSwapController>();

            EnforceContainerTransform();

            Loadout = PartyAssembleRules.Pick(
                GameManager.Instance != null ? GameManager.Instance.Loadout : null,
                standaloneLoadout);

            if (Loadout == null)
                BattleLog.Warn(LogCategory.State,
                    "PartyLoadout 이 없다 — 슬롯이 프리팹 기본값 그대로 선다. " +
                    "BattleInput 의 PartyAssembler 에 standaloneLoadout 을 꽂을 것.", this);

            BuildParty();
            PlaceParty();
        }

        // ── 조립 ────────────────────────────────────────

        /// <summary>
        /// 슬롯에 데이터를 꽂고, 로드아웃이 안 채운 칸은 지운다.
        ///
        /// 지우는 순서가 중요하다 — <c>SetActive(false)</c>를 <b>먼저</b> 한다.
        /// 그냥 <c>Destroy</c>만 하면 그 몸은 이번 프레임에 <c>Awake</c> · <c>OnEnable</c>을
        /// 마치고 나서 파괴된다. <c>Ally.OnEnable</c>이 <see cref="BattleRegistry"/>에
        /// 등록하므로, 한 프레임 동안 <b>있지도 않을 동료를 적 AI가 타겟으로 잡는다.</b>
        /// 비활성화가 먼저면 Unity 는 그 몸의 Awake 를 아예 부르지 않는다.
        /// </summary>
        private void BuildParty()
        {
            if (slots == null) return;

            PartyMemberData[] members = PartyAssembleRules.Resolve(Loadout, slots.Length);
            var party = new Ally[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                Ally slot = slots[i];
                if (slot == null) continue;

                PartyMemberData member = members[i];

                if (member == null)
                {
                    // 빈 칸. TagSwapRules 가 null 칸을 건너뛰므로 3인 파티가 그대로 돈다.
                    slot.gameObject.SetActive(false);
                    Destroy(slot.gameObject);
                    slots[i] = null;
                    continue;
                }

                if (!PartyAssembleRules.IsValid(member, out string reason))
                    BattleLog.Warn(LogCategory.Deck,
                        $"파티 저작 오류 — {reason}", slot);

                Inject(slot, member);
                party[i] = slot;
            }

            if (player != null)
            {
                player.SetParty(party);
                AllyLayers.Apply(player.gameObject);
            }
            else
            {
                BattleLog.Warn(LogCategory.State,
                    "PartyAssembler 에 Player 가 안 꽂혀 있다 — 태그 로스터가 통째로 안 만들어진다.", this);
            }
        }

        /// <summary>동료 하나에 표를 꽂는다. 실제 적용은 <see cref="Ally.ApplyData"/>가 Awake 에서 한다.</summary>
        private void Inject(Ally slot, PartyMemberData member)
        {
            slot.SetData(member);
            slot.name = !string.IsNullOrEmpty(member.memberId) ? member.memberId : member.Label;

            // 애니메이터만 여기서 직접 꽂는다. EntityAnimator.Awake 가 컨트롤러를
            // AnimatorOverrideController 로 감싸므로 그 뒤에 바꾸면 스킬 클립 교체가 통째로 어긋난다.
            if (member.animatorController != null)
            {
                var anim = slot.GetComponent<EntityAnimator>();
                if (anim != null) anim.SetController(member.animatorController);
            }

            ApplyBodyScale(slot.transform, member.bodyScale);
            ApplyTint(slot.transform, member.spriteTint);

            // 레이어는 프리팹이 아니라 여기가 보장한다. 지금까지는 SceneLayoutBuilder 가
            // 씬을 구울 때 칠했는데, 파티가 씬에서 빠지면 그 빌더는 파티를 못 본다.
            AllyLayers.Apply(slot.gameObject);
        }

        /// <summary>
        /// 몸 크기. 씬에서 <c>View</c> · <c>Shadow</c>를 각각 키우던 값을 하나로 모은 것이다.
        /// 루트를 키우지 않는 이유는 루트에 콜라이더(피격 범위)가 붙어 있고,
        /// 그 치수를 <see cref="Entity.HurtboxSize"/>가 읽어 <b>스킬 사거리 배수</b>로 쓰기 때문이다
        /// (<c>SkillData.castRangeScale</c>) — 루트를 키우면 몸집이 큰 동료의 스킬만 사거리가 늘어난다.
        ///
        /// <b>한 번만 불러야 한다.</b> 현재 배율에 곱하므로 두 번 부르면 제곱이 된다.
        /// </summary>
        private static void ApplyBodyScale(Transform body, float scale)
        {
            if (body == null || Mathf.Approximately(scale, 1f) || scale <= 0f) return;

            Scale(body.Find("View"), scale);
            Scale(body.Find("Shadow"), scale);
        }

        private static void Scale(Transform t, float scale)
        {
            if (t != null) t.localScale *= scale;
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

            if (player != null) player.transform.position = ground;

            if (slots != null)
                foreach (Ally a in slots)
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
