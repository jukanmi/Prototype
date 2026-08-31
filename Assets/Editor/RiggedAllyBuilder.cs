using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 외부 아트팩의 <b>몸과 애니메이션</b>을 동료 프리팹에 이식한다.
    /// <see cref="Rigs"/>에 줄을 늘리면 그대로 늘어난다.
    ///
    /// 아트팩은 두 형태로 온다. 여기서 갈리는 것은 <b>Animator 가 어느 노드에 앉느냐</b>
    /// 하나뿐이고, 그건 클립의 커브 경로가 정한다:
    /// <list type="bullet">
    /// <item><b>뼈대 리그</b>(마법사) — 프리팹으로 오고 클립이 <c>Skeletal/bone_1/...</c>을
    /// 찍는다. 모델 프리팹을 <c>Model</c>로 넣고 Animator 를 거기 둔다.</item>
    /// <item><b>스프라이트 시트</b>(전사) — 시트와 클립만 오고 클립 경로가 <b>빈 문자열</b>이다.
    /// 동료 프리팹에 이미 있는 <c>View/Sprite</c>를 <c>Model</c> 아래로 옮기고
    /// Animator 를 그 <c>Sprite</c>에 둔다.</item>
    /// </list>
    ///
    /// <b>왜 그냥 프리팹을 끌어다 놓으면 안 되나.</b> 세 가지가 안 맞는다:
    /// <list type="number">
    /// <item><b>상태 이름</b> — <see cref="EntityAnimator"/>는 상태 클래스 이름
    /// (<c>IdleState</c> → <c>"Idle"</c>)으로 Animator 상태를 직접 재생한다. 아트팩의
    /// 컨트롤러는 <c>Run</c> · <c>Hurt</c> · <c>Die</c> 같은 제 이름을 쓰므로 하나도 안 걸린다.
    /// 걸리지 않으면 <b>에러 없이 그냥 안 움직인다</b>(<c>Play</c>가 <c>HasState</c>로 막는다).
    /// 그래서 게임 이름표를 붙인 컨트롤러를 여기서 새로 만든다.</item>
    ///
    /// <item><b>클립 경로</b> — 아트팩 클립은 <c>Skeletal/bone_1/...</c> 처럼 <b>모델 루트 기준</b>
    /// 경로로 뼈를 찍는다. 그래서 Animator 는 반드시 <c>Skeletal</c>을 <b>직계 자식</b>으로 두는
    /// 노드에 있어야 한다 — 동료 루트에 두면 경로가 한 칸씩 밀려 전부 미아가 된다.
    /// 동료 루트의 Animator 를 떼고 모델 노드의 것을 쓰는 이유다.</item>
    ///
    /// <item><b>좌우 반전</b> — <see cref="BeltScrollView"/>는 한 장짜리 스프라이트를
    /// <c>flipX</c>로 뒤집어 왔다. 시트는 그대로 두면 되지만, 리그는 렌더러가 열 몇 개라
    /// <c>flipX</c>를 걸면 부위마다 제자리에서 뒤집혀 <b>몸이 흩어진다</b>.
    /// 리그만 노드 배율로 뒤집도록 <c>facingRoot</c>에 모델을 꽂는다.</item>
    /// </list>
    ///
    /// <b>원본 아트는 건드리지 않는다.</b> 클립도 시트도 컨트롤러도 읽기만 하고,
    /// 만든 컨트롤러는 <see cref="ControllerFolder"/>에 따로 둔다. 리그 모델은
    /// <b>중첩 프리팹 인스턴스</b>로 넣으므로 아트팩이 갱신되면 그대로 따라 들어온다.
    ///
    /// 몇 번을 돌려도 같은 결과다 — 기존 <c>Model</c> 노드를 지우고 다시 넣는다.
    /// </summary>
    public static class RiggedAllyBuilder
    {
        public const string ControllerFolder = "Assets/Data/Animation/Rigged";

        /// <summary>모델이 들어가는 노드 이름. 재실행 때 이 이름으로 찾아 갈아끼운다.</summary>
        public const string ModelName = "Model";

        /// <summary>동료 프리팹에서 몸이 사는 노드. 깊이 배율을 받는 자리이기도 하다.</summary>
        private const string ViewName = "View";

        /// <summary>공용 시트 시절의 스프라이트 노드. 리그가 들어오면 자리를 내준다.</summary>
        private const string LegacySpriteName = "Sprite";

        private const string ShadowName = "Shadow";

        /// <summary>
        /// 게임 상태 하나와 그 자리에 놓을 아트팩 클립.
        ///
        /// <b>남는 상태는 비워 두는 것이 정상이다.</b> 아트팩에 없는 동작
        /// (기상 · 공중 평타 같은 것)은 가장 가까운 클립을 빌려 쓰거나 아예 안 넣는다.
        /// 안 넣으면 그 상태에서 직전 모션이 이어진다 — 어색하긴 해도 멈추지는 않는다.
        /// </summary>
        private struct Motion
        {
            public string state;
            public string clip;

            public Motion(string state, string clip) { this.state = state; this.clip = clip; }
        }

        private struct Rig
        {
            public string id;
            public string allyPrefab;

            /// <summary>
            /// 뼈대 리그 프리팹. <b>비우면 시트 방식</b>이다 — 아트팩이 프리팹 없이
            /// 스프라이트 시트와 클립만 주는 경우이고, 그때는 동료 프리팹에 이미 있는
            /// <c>View/Sprite</c>를 그대로 쓴다.
            ///
            /// 둘의 차이는 <b>Animator 가 어느 노드에 앉느냐</b> 하나로 좁혀진다.
            /// 클립의 커브 경로가 그것을 정한다:
            /// <list type="bullet">
            /// <item>리그 클립은 <c>Skeletal/bone_1/...</c> 처럼 모델 루트 기준이라
            /// Animator 가 <c>Model</c> 에 앉는다.</item>
            /// <item>시트 클립은 경로가 <b>빈 문자열</b>이다 — 스프라이트를 자기 자신의
            /// <c>SpriteRenderer</c>에 찍으므로 Animator 가 <c>Sprite</c> 에 앉아야 한다.</item>
            /// </list>
            /// </summary>
            public string modelPrefab;

            /// <summary>클립이 든 폴더. 이름으로 찾는다.</summary>
            public string clipFolder;

            /// <summary>
            /// 아트 단위 보정 배율. 아트팩은 보통 100 PPU 인데 이 프로젝트의 공용 시트는
            /// 28 PPU 라, 그대로 넣으면 그 동료만 딴 세상 크기로 선다.
            ///
            /// <b><c>Model</c> 노드에 건다.</b> 그 위의 <c>View</c>(depthRoot)는
            /// <see cref="BeltScrollView"/>가 깊이 배율로 매 프레임 덮어쓰는 자리라
            /// 거기 넣으면 재생하는 순간 사라진다.
            ///
            /// 저작용 배수(<see cref="PartyMemberData.bodyScale"/>)와는 다른 층이다 —
            /// 그쪽은 <c>BeltScrollView.bodyScale</c>에 실리고, 둘은 곱해진다.
            /// </summary>
            public float scale;

            /// <summary>
            /// 발이 바닥에 닿아 보이도록 화면 위로 올리는 양(<see cref="BeltScrollView"/>).
            /// 스프라이트 피벗이 <b>발밑</b>이면 0, <b>가운데</b>면 반 칸만큼 올려야 한다 —
            /// 안 올리면 몸이 바닥에 반쯤 잠긴다.
            /// </summary>
            public float spriteOffsetY;

            public Motion[] motions;
        }

        private static readonly Rig[] Rigs =
        {
            new Rig
            {
                id = "Wiz",
                allyPrefab  = "Assets/Prefabs/Player/Allies/Ally_Wiz.prefab",
                modelPrefab = "Assets/Art/Character/Wizard - 2D Character/Wizard.prefab",
                clipFolder  = "Assets/Art/Character/Wizard - 2D Character/Animations",
                // 실측값이다. 아트팩은 100 PPU 인데 공용 시트는 28 PPU 라 그대로 넣으면
                // 마법사만 여섯 배로 선다. 0.33 에서 키가 탱커보다 한 뼘 큰 정도가 된다.
                scale = 0.33f,
                motions = new[]
                {
                    new Motion("Idle",         "Idle"),
                    new Motion("Move",         "Run"),
                    new Motion("Jump",         "Jump"),
                    new Motion("Attack",       "Attack"),
                    new Motion("AerialAttack", "Attack"),
                    new Motion("Hit",          "Hurt"),
                    new Motion("AerialHit",    "Hurt"),
                    new Motion("Down",         "Die"),
                    new Motion("Getup",        "Hurt"),
                    new Motion("Dead",         "Die"),

                    // 스킬은 23종인데 리그 클립은 한 벌이다. 평타로 통일한다.
                    // 스킬별 모션 교체(EntityAnimator.SwapSkillClip)는 이 몸에 안 걸린다 —
                    // 그 통로가 바꿔 끼우는 클립이 전부 공용 시트용이라, 걸리면 오히려
                    // 리그가 아무 데도 안 붙은 클립을 물고 굳는다.
                    new Motion("Skill",        "Attack"),
                },
            },

            new Rig
            {
                id = "War",
                allyPrefab  = "Assets/Prefabs/Player/Allies/Ally_War.prefab",

                // 시트 방식이다 — 이 아트팩은 프리팹도 뼈대도 없이 시트와 클립만 준다.
                modelPrefab = null,
                clipFolder  = "Assets/Art/Character/Warrior free set/Aniamtion",

                // 한 칸이 69×44 px · 100 PPU = 0.69×0.44 유닛이다.
                //
                // 공용 도트 동료(탱커 ≈1.4 유닛)에 맞추면 1.15인데, 화면에서 보면
                // 그 크기가 너무 작다는 판단이라 4배로 올린 값이다.
                // 즉 이 동료는 <b>의도적으로 다른 동료보다 훨씬 크다</b>.
                scale = 4.6f,

                // 슬라이스 피벗이 <b>가운데</b>(alignment 0)라 반 칸을 올려야 발이 바닥에 닿는다.
                // 반 칸은 0.22 유닛이지만 캐릭터 발이 칸 바닥에 딱 붙어 있지 않아 실측 계수는 0.155다.
                // <b>배율을 바꾸면 여기도 같은 비율로 따라가야 한다</b> — 그래서 식으로 남긴다.
                spriteOffsetY = 0.155f * 4.6f,

                motions = new[]
                {
                    new Motion("Idle",         "Idle"),
                    new Motion("Move",         "Run"),
                    new Motion("Jump",         "jump"),
                    new Motion("Attack",       "Attack"),
                    new Motion("AerialAttack", "Dash-Attack"),
                    new Motion("Hit",          "Hurt"),
                    new Motion("AerialHit",    "Fall"),
                    new Motion("Down",         "Death"),
                    new Motion("Getup",        "Hurt"),
                    new Motion("Dead",         "Death"),
                    new Motion("Skill",        "Attack"),
                },
            },
        };

        [MenuItem("Prototype/파티 - 동료 리그 모델 이식", priority = 32)]
        public static void Build()
        {
            EnsureFolder(ControllerFolder);

            int done = 0;
            foreach (Rig rig in Rigs)
                if (BuildOne(rig)) done++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RiggedAllyBuilder] 리그 {done}/{Rigs.Length}종 이식 완료.");
        }

        private static bool BuildOne(Rig rig)
        {
            // 시트 방식은 모델 프리팹이 없는 것이 정상이다. 경로를 적어 놓고 못 찾은 것만 오류다.
            GameObject modelPrefab = null;
            if (!string.IsNullOrEmpty(rig.modelPrefab))
            {
                modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rig.modelPrefab);
                if (modelPrefab == null)
                {
                    Debug.LogError($"[RiggedAllyBuilder] 모델 프리팹이 없다: {rig.modelPrefab}");
                    return false;
                }
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(rig.allyPrefab) == null)
            {
                Debug.LogError($"[RiggedAllyBuilder] 동료 프리팹이 없다: {rig.allyPrefab}\n" +
                               "'Prototype ▸ 파티 - 동료별 프리팹 만들기'를 먼저 돌릴 것.");
                return false;
            }

            AnimatorController controller = BuildController(rig);
            if (controller == null) return false;

            return Graft(rig, modelPrefab, controller);
        }

        // ── 컨트롤러 ────────────────────────────────────

        /// <summary>
        /// 게임의 상태 이름으로 컨트롤러를 짓는다. 클립은 아트팩 것을 <b>참조만</b> 한다 —
        /// 복사하면 아트팩이 갱신돼도 안 따라오고, 원본을 고치면 아트팩을 오염시킨다.
        /// </summary>
        private static AnimatorController BuildController(Rig rig)
        {
            string path = $"{ControllerFolder}/Ally_{rig.id}.controller";

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 여러 번 돌려도 같은 결과가 나오게 기존 상태를 비운다.
            foreach (ChildAnimatorState st in sm.states)
                sm.RemoveState(st.state);

            AnimatorState idle = null;
            int wired = 0;

            foreach (Motion m in rig.motions)
            {
                AnimationClip clip = LoadClip(rig.clipFolder, m.clip);
                if (clip == null)
                {
                    Debug.LogWarning($"[RiggedAllyBuilder] {rig.id}: 클립 '{m.clip}' 을 못 찾았다 — " +
                                     $"'{m.state}' 상태를 건너뛴다.");
                    continue;
                }

                AnimatorState state = sm.AddState(m.state);
                state.motion = clip;

                // 클립이 안 건드리는 값을 매 프레임 되돌리지 않는다.
                // 리그는 뼈가 많아 이 값이 켜져 있으면 안 쓰는 상태가 포즈를 통째로 리셋한다.
                state.writeDefaultValues = false;

                if (m.state == "Idle") idle = state;
                wired++;
            }

            if (idle != null) sm.defaultState = idle;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[RiggedAllyBuilder] {path} — 상태 {wired}개 배선", controller);

            return controller;
        }

        /// <summary>
        /// 폴더 안에서 이름이 정확히 일치하는 클립. <c>LoadAssetAtPath</c>로 바로 잡지 않는 이유는
        /// <c>.anim</c> 하나에 서브에셋이 여럿 든 경우가 있어서다.
        /// </summary>
        private static AnimationClip LoadClip(string folder, string name)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:AnimationClip {name}", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c && c.name == name) return c;
            }

            return null;
        }

        // ── 이식 ────────────────────────────────────────

        private static bool Graft(Rig rig, GameObject modelPrefab, AnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(rig.allyPrefab);
            if (root == null)
            {
                Debug.LogError($"[RiggedAllyBuilder] 프리팹을 못 열었다: {rig.allyPrefab}");
                return false;
            }

            try
            {
                Transform view = root.transform.Find(ViewName);
                if (view == null)
                {
                    Debug.LogError($"[RiggedAllyBuilder] {rig.id}: '{ViewName}' 노드가 없다.");
                    return false;
                }

                RescueSprite(view);

                GameObject model = modelPrefab != null
                    ? BuildRigModel(view, modelPrefab)
                    : BuildSheetModel(view, rig.id);

                if (model == null) return false;

                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * rig.scale;

                // 시트는 자기 SpriteRenderer 에 찍으므로 Animator 가 그 노드에 앉아야 한다.
                // 리그는 모델 루트 기준 경로라 Model 에 앉는다.
                Transform sheet = model.transform.Find(LegacySpriteName);
                GameObject animatorHost = sheet != null ? sheet.gameObject : model;

                Animator animator = WireAnimator(root, animatorHost, controller);
                WireView(root, model, sheet, rig.spriteOffsetY, animator);

                PrefabUtility.SaveAsPrefabAsset(root, rig.allyPrefab);

                Debug.Log($"[RiggedAllyBuilder] {rig.allyPrefab} — " +
                          $"{(modelPrefab != null ? modelPrefab.name : "시트")} 이식 · " +
                          $"배율 {rig.scale:0.##} · 발밑 보정 {rig.spriteOffsetY:0.##}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 지난 실행이 만든 <c>Model</c>을 치운다. <b>그 아래 있던 <c>Sprite</c>는 먼저 꺼낸다</b> —
        /// 시트 방식은 그 노드를 재사용하므로, 통째로 지우면 두 번째 실행부터 몸이 사라진다.
        /// </summary>
        private static void RescueSprite(Transform view)
        {
            Transform model = view.Find(ModelName);
            if (model == null) return;

            Transform sprite = model.Find(LegacySpriteName);
            if (sprite != null) sprite.SetParent(view, false);

            Object.DestroyImmediate(model.gameObject);
        }

        /// <summary>뼈대 리그. 공용 시트 스프라이트는 자리를 내주고 사라진다.</summary>
        private static GameObject BuildRigModel(Transform view, GameObject modelPrefab)
        {
            Transform legacy = view.Find(LegacySpriteName);
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, view);
            model.name = ModelName;

            return model;
        }

        /// <summary>
        /// 시트. 동료 프리팹에 이미 있는 <c>View/Sprite</c>를 <c>Model</c> 아래로 옮긴다.
        ///
        /// <b>왜 Sprite 에 직접 배율을 안 거나.</b> 그 노드의 <c>localScale</c>은 공용 시트
        /// 클립이 상수 커브로 1을 찍는 자리다(<c>ArtImportBuilder.BuildSpriteClip</c>) —
        /// 언젠가 공용 컨트롤러가 다시 물리면 배율이 조용히 지워진다.
        /// <c>Model</c>은 아무 클립도 안 건드리는 자리라 그 사고가 없다.
        /// </summary>
        private static GameObject BuildSheetModel(Transform view, string id)
        {
            Transform sprite = view.Find(LegacySpriteName);
            if (sprite == null)
            {
                Debug.LogError($"[RiggedAllyBuilder] {id}: 시트 방식인데 " +
                               $"'{ViewName}/{LegacySpriteName}' 가 없다.");
                return null;
            }

            var model = new GameObject(ModelName);
            model.transform.SetParent(view, false);
            sprite.SetParent(model.transform, false);

            return model;
        }

        private static void Strip(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>
        /// Animator 를 <b>모델 노드</b>로 옮긴다. 클립 경로가 모델 루트 기준이라
        /// 여기가 아니면 뼈를 하나도 못 찾는다.
        ///
        /// 동료 루트의 Animator 는 <b>떼어 낸다</b>. 남겨 두면
        /// <c>EntityAnimator.Awake</c>의 <c>GetComponentInChildren</c> 폴백이 루트 것을 먼저 잡아,
        /// 배선을 잘못 건드린 순간 조용히 빈 컨트롤러로 돌아간다.
        /// </summary>
        private static Animator WireAnimator(GameObject root, GameObject host, AnimatorController controller)
        {
            var animator = host.GetComponent<Animator>();
            if (animator == null) animator = host.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // 컬링되면 화면 밖에서 상태가 안 돌아 판정과 어긋난다.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null) Object.DestroyImmediate(rootAnimator);

            Wire(root.GetComponent<EntityAnimator>(),
                 so => so.FindProperty("animator").objectReferenceValue = animator);

            return animator;
        }

        /// <summary>
        /// <see cref="BeltScrollView"/>를 리그에 맞춘다. 바뀌는 것은 셋이다:
        /// <list type="bullet">
        /// <item><c>facingRoot</c> — 좌우 반전을 <c>flipX</c>가 아니라 노드 배율로.</item>
        /// <item><c>sprite</c> — 모델 노드. <c>depthRoot</c>가 있으면 위치는 그쪽이 잡으므로
        /// 이 값은 "몸이 여기 있다"는 표시로만 쓰인다.</item>
        /// <item><c>sortedRenderers</c> — 그림자를 맨 앞에 두고 리그 부위를 <b>원래 순서대로</b>
        /// 잇는다. <c>ApplySorting</c>이 <c>order + i</c>로 칠하므로 배열 순서가 곧 앞뒤다 —
        /// 뒤죽박죽이면 지팡이가 얼굴 앞으로 나온다.</item>
        /// </list>
        /// </summary>
        private static void WireView(GameObject root, GameObject model, Transform sheet,
                                     float spriteOffsetY, Animator animator)
        {
            var view = root.GetComponent<BeltScrollView>();
            if (view == null)
            {
                Debug.LogWarning($"[RiggedAllyBuilder] {root.name}: BeltScrollView 가 없다 — 배선 생략.");
                return;
            }

            var ordered = new List<SpriteRenderer>();

            Transform shadow = root.transform.Find(ShadowName);
            var shadowRenderer = shadow != null ? shadow.GetComponent<SpriteRenderer>() : null;
            if (shadowRenderer != null) ordered.Add(shadowRenderer);   // 0번 = 맨 뒤

            var parts = new List<SpriteRenderer>(model.GetComponentsInChildren<SpriteRenderer>(true));
            parts.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            ordered.AddRange(parts);

            // 시트는 렌더러가 하나라 flipX 가 맞다. 리그는 부위마다 제자리에서 뒤집혀
            // 몸이 흩어지므로 노드 배율로 뒤집는다.
            bool isSheet = sheet != null;
            var sheetRenderer = isSheet ? sheet.GetComponent<SpriteRenderer>() : null;

            Wire(view, so =>
            {
                so.FindProperty("sprite").objectReferenceValue = isSheet ? sheet : model.transform;
                so.FindProperty("flipToFacing").boolValue = true;
                so.FindProperty("spriteOffsetY").floatValue = spriteOffsetY;

                so.FindProperty("facingRoot").objectReferenceValue = isSheet ? null : model.transform;
                so.FindProperty("facingRenderer").objectReferenceValue = sheetRenderer;

                SerializedProperty arr = so.FindProperty("sortedRenderers");
                arr.arraySize = ordered.Count;
                for (int i = 0; i < ordered.Count; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
            });

            Debug.Log($"[RiggedAllyBuilder] {root.name}: 정렬 렌더러 {ordered.Count}개 " +
                      $"(그림자 {(shadowRenderer != null ? 1 : 0)} + " +
                      $"{(isSheet ? "시트" : "리그")} {parts.Count}) · " +
                      $"반전 {(isSheet ? "flipX" : "노드 배율")}", animator);
        }

        // ── 공용 ────────────────────────────────────────

        private static void Wire(Object target, System.Action<SerializedObject> apply)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            apply(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
