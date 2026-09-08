// 머리 위에 붙는 표시 넷 — 적 상태 라벨 · 적 틴트 · 차지 게이지 · 조종 캐릭터 화살표.
// 전부 대상 하나를 매 프레임 따라다니며 그린다.
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ EnemyStateLabel ═══════════════════════════════════════════

    /// <summary>
    /// 적 머리 위에 전투 상태를 글자로 띄운다.
    ///
    /// <see cref="EnemyStateTint"/>의 색과 짝을 이룬다 — 색만으로는 난전에서 안 읽히고,
    /// 색약자는 아예 구분하지 못한다.
    ///
    /// <see cref="ChargeGauge"/>와 같은 자리(<c>[BattleVfx]</c>)에 붙는다 — 씬 배선 0.
    /// </summary>
    public class EnemyStateLabel : MonoBehaviour
    {
        [Tooltip("머리 위로 띄우는 높이. 차징 게이지(1.6)보다 위여야 겹치지 않는다.")]
        [SerializeField] private float headOffset = 1.9f;

        [Tooltip("정렬 오프셋. 차징 게이지(300)보다 앞이다.")]
        [SerializeField] private int sortingOffset = 320;

        [Tooltip("월드 단위 글자 크기.")]
        [SerializeField] private float characterSize = 0.06f;

        [SerializeField] private int fontSize = 48;

        /// <summary>TextMesh와 그 MeshRenderer를 같이 들고 다닌다 — 매 프레임 GetComponent하지 않게.</summary>
        private struct Slot
        {
            public TextMesh text;
            public MeshRenderer renderer;
        }

        private readonly List<Slot> pool = new List<Slot>();

        /// <summary>
        /// 라벨이 놓일 자리.
        ///
        /// 논리 좌표가 아니라 <see cref="BeltScroll.ToView"/>를 거친 <b>그리는 위치</b>다.
        /// 루트 transform은 깊이가 화면 세로로 접히기 전 값(x, 0, z)을 들고 있어서,
        /// 그대로 쓰면 라벨이 발밑 훨씬 아래에 찍힌다.
        ///
        /// 머리 오프셋에는 깊이 배율을 먹인다 — 뒤에 선 적은 몸이 줄어 머리도 내려온다.
        /// 글자 크기는 안 건드린다. 어느 깊이에서나 같게 읽혀야 한다.
        /// </summary>
        public static Vector3 LabelPosition(Vector3 ground, float height, float headOffset)
            => BeltScroll.ToView(ground, height)
             + BeltScroll.ScreenUp * (headOffset * BeltScroll.ScaleAt(ground.z));

        // 캐릭터 위치는 LateUpdate에 확정된다(BeltScrollView). 그 뒤에 읽는다.
        // TimeControl을 보지 않으므로 불릿타임 중에도 보인다 — 조준하는 동안 상태가 보여야 한다.
        private void LateUpdate()
        {
            int used = Draw(BattleRegistry.Enemies);

            // 남는 슬롯은 끈다. 파괴하지 않는다 — 다음 타격에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                pool[i].renderer.enabled = false;
        }

        private int Draw(IReadOnlyList<Entity> list)
        {
            if (list == null) return 0;

            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null) continue;

                CombatState state = e.Combat.CombatState;

                if (CombatStateVisuals.ShouldShow(state))
                {
                    Draw(Take(n), e, CombatStateVisuals.Label(state), CombatStateVisuals.StateColor(state));
                    n++;
                    continue;
                }

                if (e.Combat.IsDead) continue;

                // 아래 셋은 상태가 아니라 행동이라 CombatState에 없다. 몸 색(EnemyStateTint)과
                // 짝을 이뤄 글자로도 띄운다 — 색만으로는 색약자가 구분하지 못한다.
                if (e.Combat.IsGuardBroken)
                {
                    // 브레이크는 몸 색을 건드리지 않으므로 글자가 유일한 표시다.
                    Draw(Take(n), e, CombatStateVisuals.GuardBreakLabel, new Color(0.886f, 0.290f, 1f));
                    n++;
                    continue;
                }

                if (e.Combat.IsSuperArmored)
                {
                    Draw(Take(n), e, CombatStateVisuals.SuperArmorLabel, new Color(1f, 0.820f, 0.400f));
                    n++;
                    continue;
                }

                if (e.IsTelegraphing)
                {
                    Draw(Take(n), e, CombatStateVisuals.TelegraphLabel, Color.white);
                    n++;
                }
            }

            return n;
        }

        private void Draw(Slot slot, Entity e, string label, Color color)
        {
            Physics phys = e.Physics;
            Vector3 ground = phys.GroundPosition;

            slot.text.text = label;
            slot.text.color = color;

            slot.text.transform.position = LabelPosition(ground, phys.Height, headOffset);
            slot.text.transform.rotation = BeltScroll.Billboard;   // 빌보드

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋만 준다.
            slot.renderer.sortingOrder = Mathf.RoundToInt(-ground.z * 100f) + sortingOffset;
            slot.renderer.enabled = true;
        }

        private Slot Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("EnemyStateLabel");
                go.transform.SetParent(transform, false);

                var text = go.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
                text.characterSize = characterSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;

                var renderer = go.GetComponent<MeshRenderer>();

                // TextMesh는 폰트를 꽂아도 렌더러 머티리얼을 스스로 맞추지 않는다.
                // 이걸 빼면 글자가 분홍 사각형으로 나온다.
                renderer.sharedMaterial = text.font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                pool.Add(new Slot { text = text, renderer = renderer });
            }

            return pool[index];
        }
    }

    // ══ EnemyStateTint ═══════════════════════════════════════════

    /// <summary>
    /// 적의 몸 색을 전투 상태와 <b>공격 예고</b>에 맞춰 물들인다.
    /// 스킬에 걸린 적을 난전에서 골라내고, 맞기 전에 "지금 온다"를 읽게 하기 위한 것이다.
    ///
    /// <b>Update가 없다</b> — 상태가 바뀌는 순간에만 일한다.
    /// <see cref="Enemy"/>가 스스로 붙이므로 프리팹 · 씬 배선이 없다.
    /// </summary>
    [RequireComponent(typeof(Combat))]
    public class EnemyStateTint : MonoBehaviour
    {
        [Tooltip("물들일 몸 렌더러. 비우면 스스로 찾는다. 그림자를 넣으면 안 된다.")]
        [SerializeField] private SpriteRenderer body;

        private Combat combat;
        private Entity owner;

        /// <summary>물들이기 전의 색. 적 종류를 구분하는 고유색이다.</summary>
        public Color BaseColor { get; private set; } = Color.white;

        public SpriteRenderer Body => body;

        private void Awake()
        {
            combat = GetComponent<Combat>();
            owner = GetComponent<Entity>();

            if (body == null) body = ResolveBody();
            if (body != null) BaseColor = body.color;
        }

        private void OnEnable()
        {
            if (combat != null)
            {
                combat.OnCombatStateChanged += HandleStateChanged;
                combat.OnGuardBreakChanged += HandleOverlayChanged;
                combat.OnDebuffsChanged += HandleDebuffsChanged;
            }

            if (owner != null) owner.OnTelegraphChanged += HandleOverlayChanged;
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.OnCombatStateChanged -= HandleStateChanged;
                combat.OnGuardBreakChanged -= HandleOverlayChanged;
                combat.OnDebuffsChanged -= HandleDebuffsChanged;
            }

            if (owner != null) owner.OnTelegraphChanged -= HandleOverlayChanged;
        }

        /// <summary>
        /// 아머는 <b>기본 상태</b>라 이벤트가 뜨지 않는다 — 보스는 첫 프레임부터 금색이어야 한다.
        /// 상태 변경만 기다리면 가드가 한 번 깨질 때까지 색이 안 붙는다.
        /// </summary>
        private void Start() => Apply(combat != null ? combat.CombatState : CombatState.Neutral);

        private void HandleStateChanged(CombatState prev, CombatState next) => Apply(next);

        /// <summary>예고 · 가드브레이크는 둘 다 겹침 표시라 같은 계산을 다시 돌리면 된다.</summary>
        private void HandleOverlayChanged(bool on)
            => Apply(combat != null ? combat.CombatState : CombatState.Neutral);

        /// <summary>디버프도 겹침 표시다. 시그니처만 달라 어댑터를 하나 더 둔다.</summary>
        private void HandleDebuffsChanged(Debuff mask)
            => Apply(combat != null ? combat.CombatState : CombatState.Neutral);

        /// <summary>
        /// 상태와 예고를 색에 반영한다.
        /// Neutral · Dead로 돌아오고 예고도 꺼지면 <see cref="CombatStateVisuals.Tint"/>가
        /// 기본 색을 그대로 돌려주므로 원복은 따로 처리하지 않는다.
        /// </summary>
        public void Apply(CombatState state)
        {
            if (body == null) return;

            body.color = CombatStateVisuals.Tint(BaseColor, state, ResolveOverlay());
        }

        /// <summary>
        /// 겹쳐 그릴 표시. 빙결 &gt; 스턴 &gt; 아머 &gt; 예고 순이다.
        ///
        /// 디버프가 아머를 이기는 이유: 얼어붙은 보스가 금색이면 "때려도 안 밀린다"는
        /// 거짓말이 된다 — 굳은 몸은 오히려 밀린다.
        /// 아머가 예고를 이기는 이유: 아머 중에는 어차피 못 끊으므로
        /// "지금 패링하면 된다"는 신호를 주면 역시 거짓말이 된다.
        ///
        /// 가드브레이크는 색을 주지 않는다(<see cref="CombatStateVisuals.GuardBreakLabel"/> 참고).
        /// </summary>
        private CombatOverlay ResolveOverlay()
        {
            if (combat != null && combat.HasDebuff(Debuff.Freeze)) return CombatOverlay.Frozen;
            if (combat != null && combat.HasDebuff(Debuff.Stun)) return CombatOverlay.Stunned;
            if (combat != null && combat.IsSuperArmored) return CombatOverlay.SuperArmor;
            if (owner != null && owner.IsTelegraphing) return CombatOverlay.Telegraph;

            return CombatOverlay.None;
        }

        /// <summary>
        /// 몸 렌더러를 찾는다.
        ///
        /// <c>GetComponentInChildren</c>은 쓰지 않는다 — 자식 순서에 따라 그림자를 집을 수 있고,
        /// 그러면 몸은 그대로인데 발밑 타원만 물든다.
        /// </summary>
        private SpriteRenderer ResolveBody()
        {
            var view = GetComponent<BeltScrollView>();
            if (view != null && view.SpriteRoot != null)
            {
                SpriteRenderer sr = view.SpriteRoot.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }

            // BeltScrollView가 없는 변종 프리팹은 루트에 몸이 붙어 있다.
            return GetComponent<SpriteRenderer>();
        }
    }

    // ══ ChargeGauge ═══════════════════════════════════════════

    /// <summary>
    /// 모으는 중인 캐릭터 머리 위에 차징 게이지를 띄운다.
    ///
    /// <see cref="ChargeSkillState"/>는 불릿타임 큐가 다 끝난 뒤에 터진다. 그동안 화면에는
    /// 제자리에 선 동료만 보여서 "왜 안 나가지"로 읽혔다. 남은 양이 보이면
    /// 차징기를 앞 슬롯에 둘수록 세진다는 규칙이 눈으로 확인된다.
    ///
    /// <see cref="RangeIndicator"/>와 같은 자리에 붙는다 — 씬 배선 0.
    /// </summary>
    public class ChargeGauge : MonoBehaviour
    {
        [Tooltip("머리 위로 띄우는 높이.")]
        [SerializeField] private float headOffset = 1.6f;

        [Tooltip("게이지 가로 폭(월드 단위). 시트 원본 비율은 유지한다.")]
        [SerializeField] private float width = 1.1f;

        [Tooltip("정렬 오프셋. 캐릭터보다 확실히 앞이어야 한다.")]
        [SerializeField] private int sortingOffset = 300;

        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

        // 조준과 마찬가지로 시간이 멈춘 동안에도 보여야 한다.
        // 캐릭터 위치는 LateUpdate에 확정되므로(BeltScrollView) 그 뒤에 읽는다.
        private void LateUpdate()
        {
            VfxLibrary lib = VfxLibrary.Get();
            VfxClip clip = lib != null ? lib.chargeGauge : null;

            int used = 0;
            if (clip != null && clip.IsValid)
            {
                used += DrawAll(BattleRegistry.Allies, clip, used);
                used += DrawAll(BattleRegistry.Enemies, clip, used);
            }

            // 남는 렌더러는 끈다. 파괴하지 않는다 — 다음 차징에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        private int DrawAll(IReadOnlyList<Entity> list, VfxClip clip, int start)
        {
            if (list == null) return 0;

            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null || e.Combat.IsDead) continue;

                // 동료 스킬(상태머신)이든 보스 패턴(컴포넌트)이든 Entity가 한 창구로 합쳐 준다.
                IChargeState charge = e.ChargeState;
                if (charge == null || !charge.IsCharging) continue;

                Draw(Take(start + n), e, clip, charge.ChargeRatio);
                n++;
            }

            return n;
        }

        /// <summary>
        /// 게이지가 놓일 자리. 뒤에 선 적은 몸이 줄어 머리도 내려오므로
        /// 오프셋에도 같은 깊이 배율을 먹인다. 게이지 자체의 크기(width)는 안 건드린다 —
        /// 어느 깊이에서나 같은 크기로 읽혀야 한다.
        /// </summary>
        public static Vector3 GaugePosition(Vector3 ground, float height, float headOffset)
            => BeltScroll.ToView(ground, height)
             + BeltScroll.ScreenUp * (headOffset * BeltScroll.ScaleAt(ground.z));

        private void Draw(SpriteRenderer sr, Entity e, VfxClip clip, float ratio)
        {
            // 시트는 프레임 0이 가득 찬 상태다. 남은 양이 아니라 "채운 양"으로 뒤집어 읽는다.
            sr.sprite = clip.FrameAt(1f - Mathf.Clamp01(ratio));
            if (sr.sprite == null) return;

            Physics phys = e.Physics;
            Vector3 ground = phys.GroundPosition;

            sr.transform.position = GaugePosition(ground, phys.Height, headOffset);
            sr.transform.rotation = BeltScroll.Billboard;

            float w = sr.sprite.bounds.size.x;
            float s = w > 0.0001f ? width / w : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋만 준다.
            sr.sortingOrder = Mathf.RoundToInt(-ground.z * 100f) + sortingOffset;
            sr.color = Color.white;
            sr.enabled = true;
        }

        private SpriteRenderer Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("ChargeGauge");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.enabled = false;

                pool.Add(sr);
            }

            return pool[index];
        }
    }

    // ══ ControlledCharacterArrow ═══════════════════════════════════════════

    /// <summary>
    /// 플레이어가 현재 조작 중인 캐릭터(플레이어 또는 태그 교대된 동료) 머리 위에
    /// 화살표(<c>▼</c>)를 띄워 난전 중에도 내 위치를 즉시 식별할 수 있게 한다.
    ///
    /// <b>그리는 일은 <see cref="MarkerLayer"/>가 한다.</b> 예전에는 이 컴포넌트가
    /// TextMesh를 직접 만들고 매 프레임 그렸는데, 대기 동료 표식이 같은 몸통을 또 한 벌
    /// 만들게 되면서 빌보드 · 깊이 배율 · 정렬 계산이 두 곳에 생겼다. 지금은 여기가
    /// "누구 머리 위에 무엇을"만 답한다.
    ///
    /// <see cref="TagSwapController.Current"/>를 추적하며, 교대 시 새 몸으로 즉시 옮겨간다.
    /// </summary>
    public class ControlledCharacterArrow : MonoBehaviour, IMarkerSource
    {
        [Tooltip("머리 위로 띄우는 기본 높이. 상태 게이지(StatusEffectBar, 2.25)보다 위여야 겹치지 않는다.")]
        [SerializeField] private float headOffset = 2.4f;

        [Tooltip("상하 부유(플로팅) 진폭.")]
        [SerializeField] private float bobHeight = 0.12f;

        [Tooltip("상하 부유 속도.")]
        [SerializeField] private float bobSpeed = 5f;

        [Tooltip("정렬 오프셋. 상태 게이지(340)보다 앞이다.")]
        [SerializeField] private int sortingOffset = 360;

        [Tooltip("월드 단위 글자 크기.")]
        [SerializeField] private float characterSize = 0.08f;

        [Tooltip("화살표 기본 색상.")]
        [SerializeField] private Color arrowColor = new Color(1f, 0.86f, 0.35f, 1f);

        private TagSwapController cachedSwap;

        /// <summary>
        /// 화살표가 놓일 자리.
        ///
        /// 계산 본체는 <see cref="WorldMarker.Position"/>으로 옮겼다. 이 시그니처를 남겨 두는 이유는
        /// 기존 테스트가 여기를 부르고 있어서다 — 규칙이 바뀌지 않았음을 그쪽이 계속 지켜 준다.
        /// </summary>
        public static Vector3 ArrowPosition(Vector3 ground, float height, float headOffset, float bobOffset = 0f)
            => WorldMarker.Position(ground, height, headOffset, bobOffset);

        /// <summary>시간에 따른 상하 부유 오프셋 계산 (순수 함수).</summary>
        public static float BobOffset(float unscaledTime, float speed, float height)
            => Mathf.Sin(unscaledTime * speed) * height;

        public void Collect(List<MarkerRequest> into)
        {
            Entity target = ResolveTarget();
            if (target == null) return;

            Physics phys = target.Physics;
            if (phys == null) return;

            into.Add(new MarkerRequest
            {
                ground = phys.GroundPosition,
                height = phys.Height,
                headOffset = headOffset,
                glyph = "▼",
                color = arrowColor,
                sortingOffset = sortingOffset,
                size = characterSize,
                bob = BobOffset(Time.unscaledTime, bobSpeed, bobHeight),

                // 방향이 곧 뜻인 글리프다. 등장 연출로 화면 밖에 있을 때
                // 가장자리에서 "저쪽에서 오는 중"을 가리킨다.
                tilt = true,
            });
        }

        private Entity ResolveTarget()
        {
            if (cachedSwap == null)
                cachedSwap = FindAnyObjectByType<TagSwapController>();

            if (cachedSwap != null && cachedSwap.Current != null)
                return Usable(cachedSwap.Current);

            // 태그 컨트롤러가 없는 씬을 위한 폴백: 아군 중 조종사가 물고 있는 몸
            var allies = BattleRegistry.Allies;
            if (allies != null)
            {
                for (int i = 0; i < allies.Count; i++)
                {
                    Entity e = allies[i];
                    if (e != null && e.IsPiloted && Usable(e) != null) return e;
                }
            }

            return null;
        }

        /// <summary>
        /// 그려도 되는 몸인가. 꺼져 있거나 죽었으면 표식도 없다.
        ///
        /// <b>등장 연출 중에도 그린다</b> — 그때가 오히려 "내가 어디 있는지"가 가장 안 보이는
        /// 구간이고, 화면 밖이면 <see cref="MarkerLayer"/>가 가장자리에 물려 준다.
        /// </summary>
        private static Entity Usable(Entity e)
        {
            if (e == null || !e.gameObject.activeSelf) return null;
            if (e.Combat == null || e.Combat.IsDead) return null;

            return e;
        }
    }
}
