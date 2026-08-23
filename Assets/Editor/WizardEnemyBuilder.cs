using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 마법사 원거리 적을 찍어낸다 — 프리팹 1종 + 난이도별 데이터 2종.
    ///
    /// 지금까지의 적은 전부 <b>렌더러 한 장</b>짜리 시트였다. 마법사는 파츠 16장을 뼈로 묶은
    /// 스켈레탈 리그라 그 규약이 두 군데서 깨진다:
    ///
    /// <list type="bullet">
    /// <item>Animator가 <b>루트에 못 온다.</b> 클립의 바인딩 경로가 <c>Skeletal/...</c>로
    /// 리그 루트 기준이라, 다른 트랜스폼에 얹으면 커브가 아무 데도 안 붙는다.
    /// 그래서 Animator는 마법사 프리팹에 둔 채로 <see cref="EntityAnimator"/>만 그걸 가리킨다.</item>
    /// <item>좌우 반전을 <c>flipX</c>로 못 한다. 16장이 각자 제자리에서 뒤집혀 리그가 분해된다.
    /// <see cref="BeltScrollView"/>의 facingRoot(=Sprite 노드 scale.x 부호)로 통째로 거울을 놓는다.</item>
    /// </list>
    ///
    /// 기존 <c>Enemy_Ranged.prefab</c>을 <b>기준</b>으로 복제해 컴포넌트 구성 · 히트박스 · 물리
    /// 수치를 물려받고, 그림과 애니메이터만 갈아끼운다 — 원거리 적이 두 벌로 갈리지 않게.
    ///
    /// 몇 번을 돌려도 같은 경로를 갱신할 뿐 애셋이 늘지 않는다.
    /// </summary>
    public static class WizardEnemyBuilder
    {
        private const string WizardPrefabPath = "Assets/Art/Character/Wizard - 2D Character/Wizard.prefab";
        private const string WizardClipFolder = "Assets/Art/Character/Wizard - 2D Character/Animations";

        private const string BasePrefabPath = "Assets/Prefabs/Enemy_Ranged.prefab";
        private const string ProjectilePath = "Assets/Prefabs/Projectile.prefab";

        private const string ClipFolder = "Assets/Data/Animation";
        private const string ControllerPath = ClipFolder + "/WizardAnimator.controller";
        private const string SkillSlotPath = ClipFolder + "/" + EntityAnimator.SkillSlotClip + ".anim";

        private const string PrefabFolder = "Assets/Prefabs";
        private const string DataFolder = "Assets/Data/Enemy";

        /// <summary>브레인은 기존 원거리 것을 그대로 쓴다 — 무상태라 종류마다 나눌 이유가 없다.</summary>
        private const string BrainPath = DataFolder + "/Brain_Ranged.asset";

        public const string PrefabPath = PrefabFolder + "/Enemy_WizardRanged.prefab";
        public const string SoftDataPath = DataFolder + "/Enemy_WizardSoft.asset";
        public const string HardDataPath = DataFolder + "/Enemy_WizardHard.asset";

        /// <summary>
        /// 마법사 그림의 배율. 원본은 발밑 기준 4.1유닛이다.
        ///
        /// 고블린 시트와 <b>바운즈로 맞추면 안 된다</b> — 그쪽은 투명 여백이 커서 2.9유닛 중
        /// 실제로 그려지는 건 절반쯤이다. 여백 없이 꽉 찬 마법사를 같은 바운즈로 키우면
        /// 화면에서 혼자 머리 하나가 더 크다. 눈에 보이는 키를 기준으로 맞춘 값이 이것이다 —
        /// 고블린보다 조금 크고, 가장 큰 동료와 비슷하다.
        /// </summary>
        private const float WizardScale = 0.4f;

        /// <summary>리그 루트가 붙는 자리. <see cref="SceneLayoutBuilder"/>의 깊이 노드 아래다.</summary>
        private const string SpritePath = "View/Sprite";

        // ── 애니메이터 상태 ↔ 마법사 클립 ────────────────

        /// <summary>
        /// 왼쪽이 <see cref="EntityAnimator"/>가 찾는 상태 이름(=상태 클래스 이름에서 State를 뗀 것),
        /// 오른쪽이 마법사 패키지의 원본 클립 파일이다.
        ///
        /// 클립을 <c>Wizard_{상태}</c>로 <b>복사해서</b> 쓰는 이유: EntityAnimator는 클립 길이를
        /// 이름(<c>{직업}_{상태}</c>)으로 찾아 재생 속도를 코드가 정한 상태 길이에 맞춘다.
        /// 원본 이름(Run · Hurt · Die) 그대로면 그 조회가 전부 빗나가 평타 모션과 히트박스가 따로 논다.
        /// </summary>
        private static readonly (string state, string source)[] ClipMap =
        {
            ("Idle",         "Idle"),
            ("Move",         "Run"),
            ("Jump",         "Jump"),
            ("Attack",       "Attack"),
            ("AerialAttack", "Attack"),
            ("Hit",          "Hurt"),
            ("AerialHit",    "Hurt"),
            ("Down",         "Die"),
            ("Getup",        "Hurt"),
            ("Dead",         "Die"),
        };

        // ── 난이도별 수치 ───────────────────────────────

        /// <summary>한 난이도를 만드는 데 필요한 것 전부.</summary>
        private struct Variant
        {
            public string assetPath;
            public string id;
            public string displayName;

            public float hp;
            public float atk;
            public float moveSpeed;

            public float attackInterval;
            public float leashRange;
            public float preferredMinRange;

            public float projectileSpeed;
            public float projectileRange;

            public int exp;
            public int gold;

            /// <summary>
            /// 쏘기로 마음먹는 거리. 투사체 사거리의 80%(<see cref="Entity.BasicAttackReach"/>)를
            /// 넘기면 안 된다 — 넘기면 닿지도 않을 거리에서 쏘고 쿨만 태운다.
            /// </summary>
            public float AttackRange => projectileRange * 0.8f;
        }

        /// <summary>
        /// 견습생. 느리게 한 발씩 쏘고 금방 물러선다 — 원거리 적을 어떻게 잡는지 배우는 자리다.
        /// </summary>
        private static readonly Variant Soft = new Variant
        {
            assetPath = SoftDataPath, id = "Enemy_WizardSoft", displayName = "마법사 견습생",
            hp = 22f, atk = 3f, moveSpeed = 2.6f,
            attackInterval = 2.4f, leashRange = 14f, preferredMinRange = 3f,
            projectileSpeed = 10f, projectileRange = 7.5f,
            exp = 12, gold = 6,
        };

        /// <summary>
        /// 대마법사. 방 반대편에서도 닿고, 붙으면 더 멀리 물러난다 —
        /// 근접 적과 섞어 두면 "붙을 수가 없는" 압박이 된다.
        /// </summary>
        private static readonly Variant Hard = new Variant
        {
            assetPath = HardDataPath, id = "Enemy_WizardHard", displayName = "대마법사",
            hp = 34f, atk = 7f, moveSpeed = 3.2f,
            attackInterval = 1.2f, leashRange = 18f, preferredMinRange = 5f,
            projectileSpeed = 16f, projectileRange = 11f,
            exp = 24, gold = 14,
        };

        private static readonly Variant[] Variants = { Soft, Hard };

        // ── 진입점 ──────────────────────────────────────

        [MenuItem("Prototype/적 - 마법사 원거리 프리팹 만들기")]
        public static void Build()
        {
            if (BuildSilently()) Debug.Log($"[WizardEnemyBuilder] 완료 — {PrefabPath}");
        }

        /// <summary>
        /// 대화상자 없이 굽는다. 스테이지 빌더가 씬을 만들기 직전에 부르고, 테스트도 이걸 쓴다.
        /// </summary>
        /// <returns>전부 성공했으면 참. 실패는 이미 로그로 남겼다.</returns>
        internal static bool BuildSilently()
        {
            var wizard = AssetDatabase.LoadAssetAtPath<GameObject>(WizardPrefabPath);
            if (wizard == null)
            {
                Debug.LogError($"[WizardEnemyBuilder] 마법사 아트가 없다: {WizardPrefabPath}");
                return false;
            }

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"[WizardEnemyBuilder] 기준 프리팹이 없다: {BasePrefabPath}");
                return false;
            }

            EnsureFolder(ClipFolder);
            EnsureFolder(DataFolder);

            AnimatorController controller = BuildController();
            if (controller == null) return false;

            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            Projectile shot = projectile != null ? projectile.GetComponent<Projectile>() : null;
            if (shot == null)
                Debug.LogWarning($"[WizardEnemyBuilder] {ProjectilePath}가 없다. 마법사가 근접으로 때린다.");

            var brain = AssetDatabase.LoadAssetAtPath<EnemyBrainAsset>(BrainPath);
            if (brain == null)
                Debug.LogWarning($"[WizardEnemyBuilder] {BrainPath}가 없다. 프리팹의 폴백 브레인을 쓴다.");

            // 데이터가 먼저다. 프리팹의 Enemy.data를 채워야 씬에 그냥 끌어다 놔도 마법사로 동작한다 —
            // 기준 프리팹에서 물려받은 고블린 데이터가 남아 있으면 궁수 수치로 싸운다.
            var built = new EnemyData[Variants.Length];
            for (int i = 0; i < Variants.Length; i++)
                built[i] = EnsureData(Variants[i], brain, shot);

            GameObject prefab = BuildPrefab(basePrefab, wizard, controller, built[0]);
            if (prefab == null) return false;

            // 데이터 → 프리팹 역참조. 스포너가 데이터만 들고 소환할 수 있게.
            foreach (EnemyData data in built)
            {
                data.prefab = prefab;
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        // ── 애니메이션 ──────────────────────────────────

        /// <summary>
        /// 마법사 전용 컨트롤러. 공용 <c>EntityAnimator.controller</c>를 못 쓴다 —
        /// 그쪽 클립은 <c>View/Sprite</c>의 스케일을 흔드는 플레이스홀더라
        /// 리그가 있는 캐릭터에서는 아무 데도 안 붙는다.
        /// </summary>
        private static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 여러 번 돌려도 같은 결과가 나오게 기존 상태를 비운다.
            foreach (ChildAnimatorState st in sm.states)
                sm.RemoveState(st.state);

            foreach ((string state, string source) in ClipMap)
            {
                AnimationClip clip = CopyClip(source, state);
                if (clip == null) return null;

                AddState(sm, state, clip);
            }

            // 스킬 자리. 적은 스킬을 안 쓰지만 슬롯이 없으면 EntityAnimator의 오버라이드 사전에
            // 키가 사라져, 나중에 마법사에게 스킬을 물릴 때 조용히 아무 일도 안 하게 된다.
            var slot = AssetDatabase.LoadAssetAtPath<AnimationClip>(SkillSlotPath);
            if (slot != null) AddState(sm, "Skill", slot);
            else Debug.LogWarning($"[WizardEnemyBuilder] {SkillSlotPath}가 없어 Skill 상태를 건너뛴다.");

            sm.defaultState = FindState(sm, "Idle");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            AnimatorState state = sm.AddState(name);
            state.motion = clip;

            // AnimationBuilder와 같은 이유로 끈다: Dead 클립은 알파를 안 건드리는데
            // DeadState가 코드로 페이드아웃하므로, 켜 두면 시체가 영영 안 사라진다.
            state.writeDefaultValues = false;
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (ChildAnimatorState st in sm.states)
                if (st.state.name == name) return st.state;

            return null;
        }

        /// <summary>
        /// 마법사 클립을 <c>Wizard_{상태}</c>라는 이름으로 복사한다.
        ///
        /// 원본 폴더를 건드리지 않는다 — 에셋 스토어 패키지라 다시 임포트하면 덮어써진다.
        /// 바인딩 경로는 리그 루트 기준이라 복사해도 그대로 붙는다.
        /// </summary>
        private static AnimationClip CopyClip(string source, string state)
        {
            string from = $"{WizardClipFolder}/{source}.anim";
            string to = $"{ClipFolder}/Wizard_{state}.anim";

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(from) == null)
            {
                Debug.LogError($"[WizardEnemyBuilder] 원본 클립이 없다: {from}");
                return null;
            }

            // CopyAsset은 목적지가 있으면 실패한다. 덮어쓰려면 먼저 지워야 한다.
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(to) != null)
                AssetDatabase.DeleteAsset(to);

            if (!AssetDatabase.CopyAsset(from, to))
            {
                Debug.LogError($"[WizardEnemyBuilder] 클립 복사 실패: {from} → {to}");
                return null;
            }

            AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(to);
            if (clip == null)
            {
                Debug.LogError($"[WizardEnemyBuilder] 복사한 클립을 못 읽는다: {to}");
                return null;
            }

            // 파일명만 바뀌고 애셋 이름은 원본 그대로다. EntityAnimator가 보는 건 애셋 이름이다.
            string wanted = $"Wizard_{state}";
            if (clip.name != wanted)
            {
                clip.name = wanted;
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }

        // ── 프리팹 ──────────────────────────────────────

        private static GameObject BuildPrefab(GameObject basePrefab, GameObject wizardArt,
                                              AnimatorController controller, EnemyData data)
        {
            // 프리팹 애셋을 직접 편집하지 않는다. 사본을 조립해서 통째로 저장한다.
            var root = Object.Instantiate(basePrefab);

            try
            {
                root.name = "Enemy_WizardRanged";
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;

                Transform sprite = root.transform.Find(SpritePath);
                if (sprite == null)
                {
                    Debug.LogError($"[WizardEnemyBuilder] 기준 프리팹에 {SpritePath}가 없다.");
                    return null;
                }

                Animator animator = SwapArt(root, sprite, wizardArt, controller);
                WireView(root, sprite);
                WireAnimator(root, animator);
                WireData(root, data);
                AssignLayers(root);

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 고블린 시트를 걷어내고 그 자리에 마법사 리그를 꽂는다.
        ///
        /// 마법사는 <b>프리팹 인스턴스로</b> 넣는다(중첩 프리팹). 아트를 고치면 적에게 바로 반영되고,
        /// 여기서 사본을 떠 두면 그 연결이 끊긴다.
        /// </summary>
        private static Animator SwapArt(GameObject root, Transform sprite, GameObject wizardArt,
                                        AnimatorController controller)
        {
            // 몸이 두 벌로 겹쳐 그려지지 않게 옛 렌더러를 걷어낸다.
            var old = sprite.GetComponent<SpriteRenderer>();
            if (old != null) Object.DestroyImmediate(old);

            // 리그의 Animator가 주인이다. 루트에 남은 공용 Animator는 EntityAnimator의
            // GetComponentInChildren 폴백이 잘못 집는 자리라 같이 걷어낸다.
            var rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null) Object.DestroyImmediate(rootAnimator);

            // 재실행 대비 — 이미 꽂혀 있던 리그를 지운다.
            for (int i = sprite.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(sprite.GetChild(i).gameObject);

            var art = (GameObject)PrefabUtility.InstantiatePrefab(wizardArt);
            art.transform.SetParent(sprite, false);
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.identity;
            art.transform.localScale = Vector3.one * WizardScale;

            var animator = art.GetComponent<Animator>();
            if (animator == null) animator = art.GetComponentInChildren<Animator>(true);

            if (animator != null) animator.runtimeAnimatorController = controller;
            else Debug.LogError("[WizardEnemyBuilder] 마법사 아트에 Animator가 없다.");

            return animator;
        }

        /// <summary>
        /// 2.5D 표현 배선. 반전은 <b>Sprite 노드의 scale.x</b>가 맡는다 —
        /// 파츠 16장에 flipX를 걸면 각자 제자리에서 뒤집혀 리그가 분해된다.
        /// </summary>
        private static void WireView(GameObject root, Transform sprite)
        {
            // 깊이 노드 · 그림자 · 정렬 목록은 공용 규약대로 다시 맞춘다.
            // 파츠가 여럿이라도 원래 sortingOrder 순서로 담기므로 앞뒤가 지켜진다.
            SceneLayoutBuilder.RigBeltScrollView(root);

            var view = root.GetComponent<BeltScrollView>();
            if (view == null) return;

            var so = new SerializedObject(view);
            so.FindProperty("flipToFacing").boolValue = true;
            so.FindProperty("facingRoot").objectReferenceValue = sprite;

            // flipX 경로를 확실히 죽인다. 남겨 두면 파츠 한 장만 뒤집힌다.
            so.FindProperty("facingRenderer").objectReferenceValue = null;

            // 피벗이 발밑이라 올려 줄 필요가 없다.
            so.FindProperty("spriteOffsetY").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(view);
        }

        /// <summary>
        /// <see cref="EntityAnimator"/>가 리그의 Animator를 보게 한다.
        /// 직렬화 필드를 비우면 자식 순서에 기대는 폴백을 타므로 명시적으로 꽂는다.
        /// </summary>
        private static void WireAnimator(GameObject root, Animator animator)
        {
            var entityAnimator = root.GetComponent<EntityAnimator>();
            if (entityAnimator == null) entityAnimator = root.AddComponent<EntityAnimator>();

            var so = new SerializedObject(entityAnimator);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(entityAnimator);
        }

        /// <summary>
        /// 기본 수치 테이블. 씬에서 인스턴스마다 갈아끼우는 자리이기도 하다 —
        /// 프리팹은 하나고 난이도는 데이터로 나뉜다.
        /// </summary>
        private static void WireData(GameObject root, EnemyData data)
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy == null || data == null) return;

            // private [SerializeField]는 SerializedObject로만 안전하게 건드린다.
            var so = new SerializedObject(enemy);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(enemy);
        }

        /// <summary>
        /// 히트박스 레이어. 프리팹에 박아 둬야 씬에 새로 꽂아도 충돌 매트릭스가 맞는다 —
        /// 기준이 된 <c>Enemy_Ranged.prefab</c>은 Default로 남아 있어 씬 빌더가 매번 고쳐 주고 있다.
        /// </summary>
        private static void AssignLayers(GameObject root)
        {
            var entity = root.GetComponent<Entity>();
            if (entity != null) SceneLayoutBuilder.AssignLayers(entity);
        }

        // ── 데이터 ──────────────────────────────────────

        private static EnemyData EnsureData(Variant v, EnemyBrainAsset brain, Projectile shot)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(v.assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, v.assetPath);
            }

            data.enemyId = v.id;
            data.enemyName = v.displayName;
            data.hp = v.hp;
            data.atk = v.atk;
            data.brain = brain;
            data.moveSpeed = v.moveSpeed;
            data.attackRange = v.AttackRange;
            data.attackInterval = v.attackInterval;
            data.retargetInterval = 0.5f;
            data.leashRange = v.leashRange;
            data.preferredMinRange = v.preferredMinRange;
            data.specialRange = 0f;
            data.specialInterval = 0f;

            data.basicProjectile = shot;
            data.projectileSpeed = v.projectileSpeed;
            data.projectileRange = v.projectileRange;
            data.projectilePierce = 0;

            data.exp = v.exp;
            data.gold = v.gold;

            EditorUtility.SetDirty(data);
            return data;
        }

        // ── 유틸 ────────────────────────────────────────

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string cur = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
