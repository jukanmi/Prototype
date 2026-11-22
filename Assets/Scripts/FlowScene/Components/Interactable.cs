using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Prototype
{
    /// <summary>
    /// 씬에 놓는 상호작용 지점. 다가가서 상호작용 키(기본 F)를 누르면 인스펙터에 꽂은
    /// <see cref="onInteract"/>를 부른다 — 출발 · 제단 · 상점 · 이벤트, 무엇을 여는지는 씬이 정한다.
    ///
    /// 판정 · 키 안내 · 입력을 스스로 한다. 씬 컨트롤러가 찾아다니며 관리하지 않으므로
    /// 조작하는 몸(<see cref="BattleRegistry.Controlled"/>)과 입력(<see cref="PlayerInputController"/>)만 있으면
    /// 어느 씬에서든 돈다. 여럿이 가까우면 제일 가까운 하나만 안내를 띄운다.
    ///
    /// 같은 오브젝트에 <see cref="Npc"/>가 있으면 대사부터 보이고, 끝까지 읽은 뒤에 부른다(ESC로 닫으면 안 부른다).
    /// 그림이 생기기 전까지는 머리 위 이름표(<see cref="TextMesh"/>)로 자리를 보인다.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [SerializeField] private string label = "";

        [Tooltip("바닥(XZ) 기준 반경. 이 안에 들어오면 키 안내가 뜬다.")]
        [SerializeField] private float radius = 1.5f;

        [Tooltip("상호작용 키를 눌렀을 때 할 일. 같은 오브젝트에 Npc가 있으면 대사를 다 읽은 뒤에 부른다.")]
        [SerializeField] private UnityEvent onInteract = new UnityEvent();

        [Tooltip("이름표 높이. 사람 머리 위는 2.2, 바닥에 놓인 물건은 낮게.")]
        [SerializeField] private float tagHeight = 2.2f;

        private static readonly List<Interactable> all = new List<Interactable>();

        /// <summary>이름표의 정렬 순서. 바닥 그림(깊이 × 100)이 닿지 않는 높이다.</summary>
        public const int TagSortingOrder = short.MaxValue;

        private TextMesh nameTag;

        /// <summary>이름표 · 대화창에 쓰는 이름. 비우면 오브젝트 이름.</summary>
        public string Label => string.IsNullOrEmpty(label) ? name : label;

        /// <summary>눌렀을 때 할 일. 코드로 지을 때 여기에 붙인다.</summary>
        public UnityEvent OnInteract => onInteract;

        /// <summary>
        /// 마지막으로 눌린 지점. 결과 쪽(이벤트 대화 등)이 "누가 불렀나"를 여기서 본다 —
        /// 인스펙터의 UnityEvent는 부른 쪽을 넘기지 않는다.
        /// </summary>
        public static Interactable Current { get; private set; }

        /// <summary>지금 키 안내가 떠 있는가(눌릴 차례인가). 툴팁 같은 덧붙임이 이걸 보고 뜬다.</summary>
        public bool IsFocused { get; private set; }

        /// <summary>테스트 · 코드로 지을 때의 이음매.</summary>
        public void Configure(string text, float range, float labelHeight = 2.2f)
        {
            label = text;
            radius = range;
            tagHeight = labelHeight;
        }

        private void OnEnable()
        {
            all.Add(this);
            if (nameTag != null) nameTag.gameObject.SetActive(true);
        }

        /// <summary>꺼진 지점은 이름표도 감춘다 — 열어 버린 상자에 "[상자]"가 남지 않게.</summary>
        private void OnDisable()
        {
            all.Remove(this);
            IsFocused = false;
            if (nameTag != null) nameTag.gameObject.SetActive(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            Current = null;
        }

        private void Start() => nameTag = NameTag(transform, TagText(false), tagHeight);

        private void Update()
        {
            PlayerInputController input = PlayerInputController.Instance;
            Entity body = BattleRegistry.Controlled;

            // 다른 창(일시정지 · 대화 · 상점 등)이 떠 있으면 쉰다 — 창들은 떠 있는 동안 조작을 잠근다.
            bool ready = input != null && !input.GameplaySuspended && !GameplayModal.IsOpen && body != null
                         && Nearest(body.transform.position) == this;
            IsFocused = ready;

            string text = TagText(ready);
            if (nameTag.text != text) nameTag.text = text;

            if (ready && input.InteractPressed) Interact();
        }

        /// <summary><see cref="Npc"/>가 있으면 대사부터 보이고 끝까지 읽으면, 없으면 바로 <see cref="onInteract"/>.</summary>
        public void Interact()
        {
            Current = this;

            Npc npc = GetComponent<Npc>();
            if (npc != null) DialogueWindow.Open(Label, npc.Lines, onInteract.Invoke, npc);
            else onInteract.Invoke();
        }

        /// <summary>이름표 글. 말을 걸 수 있으면 키를 앞에 붙인다.</summary>
        private string TagText(bool ready)
        {
            if (!ready) return $"[{Label}]";

            string key = InputHints.Key(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Interact);
            return $"[{key}] {Label}";
        }

        /// <summary>
        /// <paramref name="position"/>에서 반경 안에 있는 가장 가까운 지점. 없으면 null.
        /// 높이(Y)는 안 본다 — 점프 중에도 안내가 깜빡이지 않게.
        /// </summary>
        public static Interactable Nearest(Vector3 position)
        {
            Interactable best = null;
            float bestSq = float.MaxValue;

            foreach (Interactable i in all)
            {
                Vector3 d = i.transform.position - position;
                float sq = d.x * d.x + d.z * d.z;
                if (sq > i.radius * i.radius || sq >= bestSq) continue;

                best = i;
                bestSq = sq;
            }

            return best;
        }

        /// <summary>머리 위 이름표.</summary>
        private static TextMesh NameTag(Transform owner, string content, float height)
        {
            var go = new GameObject("NameTag");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);

            // 카메라가 위에서 내려다보므로 같은 각도로 눕힌다. 안 눕히면 납작하게 보인다.
            go.transform.rotation = BeltScroll.Billboard;
            go.AddComponent<FaceCamera>();

            // 폰트 · 재질을 안 꽂으면 TextMesh 는 아무것도 안 그린다. UiKit 과 같은 내장 폰트를 쓴다.
            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MeshRenderer tagRenderer = go.GetComponent<MeshRenderer>();
            tagRenderer.sharedMaterial = text.font.material;
            // 그림은 깊이(z)로 정렬 순서를 받는다. 글은 0이라 뒤에 선 상자 같은 그림에 가려졌다 — 늘 맨 위에 그린다.
            tagRenderer.sortingOrder = TagSortingOrder;
            text.text = content;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.08f;
            text.fontSize = 48;
            text.color = new Color(1f, 0.86f, 0.45f);
            return text;
        }

        /// <summary>
        /// 이름표를 매 프레임 카메라 정면으로 다시 세운다. 몸에 <c>Physics</c>가 있으면 루트가 바라보는 쪽으로
        /// 돌아가는데(기본 +X, 90°), 이름표는 그 자식이라 한 번만 세워 두면 같이 돌아 버린다.
        /// </summary>
        private sealed class FaceCamera : MonoBehaviour
        {
            private void LateUpdate() => transform.rotation = BeltScroll.Billboard;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.86f, 0.45f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
