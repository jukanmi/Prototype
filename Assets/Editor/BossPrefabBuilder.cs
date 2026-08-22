using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 보스 프리팹 · 데이터 · 브레인을 한 번에 찍는다.
    ///
    /// <c>Enemy_Melee.prefab</c>을 기준으로 복제해 컴포넌트 구성(Entity · Physics · Combat ·
    /// BeltScrollView · 그림자)을 물려받고, 몸통 · 히트박스 · 브레인 · 패턴을 갈아끼운다.
    /// 원본은 절대 건드리지 않는다 — 씬이 그 GUID를 참조하고 있다.
    ///
    /// 패턴 수치는 <see cref="BossPatternTable"/>이 단독으로 들고 있다.
    /// <see cref="BossArtImportBuilder"/>가 같은 표에서 클립 길이를 뽑으므로 둘이 어긋나지 않는다.
    ///
    /// 여러 번 돌려도 같은 경로를 갱신할 뿐 애셋이 늘지 않는다.
    /// </summary>
    public static class BossPrefabBuilder
    {
        private const string BasePrefabPath = "Assets/Prefabs/Enemy_Melee.prefab";
        private const string PrefabPath = "Assets/Prefabs/Enemy_Boss.prefab";
        private const string DataFolder = "Assets/Data/Enemy";
        private const string DataPath = DataFolder + "/Enemy_Boss.asset";
        private const string BrainPath = DataFolder + "/Brain_Boss.asset";
        private const string ControllerPath = "Assets/Data/Animation/BossAnimator.controller";
        private const string IdleSheet = "Assets/Art/Character/BossWarrior/Idle.png";

        private const string SpritePath = "View/Sprite";
        internal const string BasicHitboxName = "Attack";

        /// <summary>광역 히트박스 자식 이름. 재실행 때 이 이름으로 찾아 갱신한다.</summary>
        internal const string WideHitboxName = "WideHitbox";

        /// <summary>둘레 판정 히트박스 자식 이름. 횡베기처럼 앞뒤 구분이 없는 패턴이 쓴다.</summary>
        internal const string RadialHitboxName = "RadialHitbox";

        // ── 수치 ───────────────────────────────────────
        internal const float Hp = 400f;
        internal const float Atk = 12f;
        internal const float MoveSpeed = 3.2f;
        internal const float AttackRange = 2.6f;
        internal const float AttackInterval = 1.4f;

        /// <summary>보스방은 도망칠 곳이 없다. 0은 "추격을 포기하지 않는다"는 뜻이다.</summary>
        internal const float LeashRange = 0f;

        // ── 가드 (슈퍼아머 · 가드브레이크) ───────────────
        //
        // 여기 없으면 <b>보스가 평상시 슈퍼아머를 잃는다</b>. Combat.maxGuard의 클래스 기본값이
        // 0이고 HasGuard가 그걸 그대로 보므로, 기준 프리팹(Enemy_Melee)에서 떠 오는 순간
        // 가드 시스템이 통째로 꺼진다 — 실제로 한 번 그렇게 날아갔다.
        // 나머지 가드 수치만 남아 있으면 조용히 무의미해지므로 네 값을 한자리에 묶어 둔다.

        /// <summary>가드 게이지 최대치. <see cref="GuardDamage"/>로 나누면 "몇 대 맞고 깨지나"가 나온다 — 지금은 10대.</summary>
        internal const float MaxGuard = 60f;

        /// <summary>평타 한 대가 깎는 양. 스킬은 HitData.guardDamage로 이보다 크게 깎을 수 있다.</summary>
        internal const float GuardDamage = 6f;

        /// <summary>깨진 뒤 무방비로 있는 시간. 콤보를 넣을 수 있는 유일한 창.</summary>
        internal const float GuardBreakDuration = 4f;

        /// <summary>마지막 피격 후 이만큼 지나야 회복이 시작된다.</summary>
        internal const float GuardRegenDelay = 3f;

        /// <summary>초당 회복량. 연타를 끊으면 벽이 도로 선다(초당 게이지의 20%).</summary>
        internal const float GuardRegen = 12f;

        // 평타. 몸집에 맞게 동료보다 느리게 휘두른다.
        // BossArtImportBuilder의 Attack 클립 fps(7)가 이 길이에 맞춰져 있다.
        private const float BasicWindup = 0.18f;
        private const float BasicActiveEnd = 0.36f;
        private const float BasicTotal = 0.55f;

        // ── 지오메트리 ─────────────────────────────────
        // 정면은 로컬 +Z다(Physics.Apply가 LookRotation으로 돌리는 축).
        private static float BodyHeight => BossArtImportBuilder.StandingHeight;
        private const float BodyRadius = 0.7f;

        private static Vector3 BasicHitboxPos => new Vector3(0f, BodyHeight * 0.45f, 1.5f);
        private static readonly Vector3 BasicHitboxSize = new Vector3(1.9f, 1.8f, 1.8f);

        private static Vector3 WideHitboxPos => new Vector3(0f, BodyHeight * 0.45f, 1.9f);
        private static readonly Vector3 WideHitboxSize = new Vector3(3.6f, 2.4f, 3.2f);

        /// <summary>
        /// 둘레 판정. <b>몸 중심</b>에 놓는다 — 앞으로 밀면 등 뒤에 사각지대가 생겨
        /// "뒤로 돌아가면 안 맞는다"가 되고, 그러면 상자를 쓰는 것과 같아진다.
        /// </summary>
        private static Vector3 RadialHitboxPos => new Vector3(0f, BodyHeight * 0.45f, 0f);

        /// <summary>
        /// 둘레 판정 반경. 광역 상자의 정면 도달거리(1.9 + 1.6 = 3.5)보다 조금 짧다 —
        /// 사방을 덮는 대신 한 방향으로 뻗는 거리를 내주는 것이 이 패턴의 거래다.
        /// </summary>
        internal const float RadialHitboxRadius = 3.2f;

        [MenuItem("Prototype/보스 - 프리팹 + 데이터 만들기")]
        public static void Build()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"[BossPrefabBuilder] 기준 프리팹이 없다: {BasePrefabPath}");
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError(
                    $"[BossPrefabBuilder] {ControllerPath}가 없다. " +
                    "'Prototype ▸ 보스 - 아트 임포트 + 애니메이터'를 먼저 돌릴 것.");
                return;
            }

            EnsureFolder(DataFolder);

            BossBrainAsset brain = EnsureBrain();
            EnemyData data = EnsureData(brain);

            // 프리팹 애셋을 직접 편집하지 않는다. 사본을 조립해서 통째로 저장한다.
            var root = Object.Instantiate(basePrefab);
            root.name = "Enemy_Boss";
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            BuildBody(root);
            RigSprite(root, controller);

            Attack basicHitbox = EnsureHitbox(root, BasicHitboxName, BasicHitboxPos, BasicHitboxSize);
            Attack wideHitbox = EnsureHitbox(root, WideHitboxName, WideHitboxPos, WideHitboxSize);
            Attack radialHitbox = EnsureSphereHitbox(root, RadialHitboxName, RadialHitboxPos, RadialHitboxRadius);

            WireEnemy(root, data, basicHitbox);
            WireGuard(root);
            WireControl(root, brain);
            WirePatterns(root, basicHitbox, wideHitbox, radialHitbox);
            AssignLayers(root, basicHitbox, wideHitbox, radialHitbox);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // 데이터 → 프리팹 역참조. 스포너가 데이터만 들고 소환할 수 있게.
            data.prefab = saved;
            EditorUtility.SetDirty(data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossPrefabBuilder] {PrefabPath} 저장 — 패턴 {BossPatternTable.All.Length}종", saved);
        }

        // ── 애셋 ───────────────────────────────────────

        /// <summary>
        /// 브레인. 규칙 순서가 곧 우선순위라 표 순서를 그대로 옮긴다.
        /// index는 <see cref="WirePatterns"/>가 넣는 패턴 배열의 위치와 같아야 한다 —
        /// 둘 다 같은 표를 같은 순서로 도니까 정의상 일치한다.
        /// </summary>
        private static BossBrainAsset EnsureBrain()
        {
            var brain = AssetDatabase.LoadAssetAtPath<BossBrainAsset>(BrainPath);
            if (brain == null)
            {
                brain = ScriptableObject.CreateInstance<BossBrainAsset>();
                AssetDatabase.CreateAsset(brain, BrainPath);
            }

            var rules = new BossBrainAsset.PatternRule[BossPatternTable.All.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                BossPatternTable.Entry e = BossPatternTable.All[i];
                rules[i] = new BossBrainAsset.PatternRule
                {
                    label = e.label,
                    index = i,
                    minRange = e.minRange,
                    maxRange = e.maxRange,
                    cooldown = e.cooldown,
                    maxHealthRatio = e.maxHealthRatio,
                };
            }

            brain.patterns = rules;
            EditorUtility.SetDirty(brain);
            return brain;
        }

        private static EnemyData EnsureData(BossBrainAsset brain)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            data.enemyId = "Enemy_Boss";
            data.enemyName = "전사 보스";
            data.hp = Hp;
            data.atk = Atk;
            data.brain = brain;
            data.moveSpeed = MoveSpeed;
            data.attackRange = AttackRange;
            data.attackInterval = AttackInterval;
            data.retargetInterval = 0.5f;
            data.leashRange = LeashRange;
            data.preferredMinRange = 0f;

            // 사거리·쿨은 패턴마다 다르고 브레인 규칙이 들고 있다.
            // EnemyData의 이 두 칸은 돌진 멧돼지처럼 특수가 하나뿐인 적을 위한 것이다.
            data.specialRange = 0f;
            data.specialInterval = 4f;

            data.basicProjectile = null;
            data.exp = 200;
            data.gold = 120;

            EditorUtility.SetDirty(data);
            return data;
        }

        // ── 프리팹 조립 ─────────────────────────────────

        /// <summary>
        /// 몸통. 기준 프리팹이 물려준 캡슐을 보스 크기로 늘린다.
        /// EnemyPrefabBuilder가 붙이는 캡슐 <b>메시</b>(Body · FacingMarker)는 걷어낸다 —
        /// 보스는 스프라이트로 그리므로 몸 안에 회색 캡슐이 겹쳐 보인다.
        /// </summary>
        private static void BuildBody(GameObject root)
        {
            foreach (string name in new[] { "Body", "FacingMarker" })
            {
                Transform found = root.transform.Find(name);
                if (found != null) Object.DestroyImmediate(found.gameObject);
            }

            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();

            capsule.center = new Vector3(0f, BodyHeight * 0.5f, 0f);   // 발이 바닥(y=0)에 닿는다
            capsule.height = BodyHeight;
            capsule.radius = BodyRadius;
            capsule.isTrigger = false;

            // 캐릭터끼리 밀지 않고 벽만 벽으로 본다. 이게 틀리면 돌진이 아군 몸통에 부딪혀
            // 곧바로 후딜로 끊긴다(cancelOnContact가 OnWallHit를 듣기 때문).
            var physics = root.GetComponent<Prototype.Physics>();
            if (physics != null)
            {
                int wall = LayerMask.NameToLayer("Wall");
                if (wall >= 0)
                {
                    var pso = new SerializedObject(physics);
                    pso.FindProperty("wallMask").intValue = 1 << wall;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(physics);
                }
            }
        }

        /// <summary>
        /// 스프라이트 · 애니메이터 배선. 동료(<see cref="ArtImportBuilder"/>)와 같은 방식이다 —
        /// 피벗이 발밑이라 <c>spriteOffsetY</c>는 0이고, 옆에서 본 시트라 좌우 반전을 켠다.
        /// </summary>
        private static void RigSprite(GameObject root, AnimatorController controller)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // 컬링되면 화면 밖에서 상태가 안 돌아 판정과 어긋난다.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var view = root.GetComponent<EntityAnimator>();
            if (view == null) view = root.AddComponent<EntityAnimator>();

            var vso = new SerializedObject(view);
            vso.FindProperty("animator").objectReferenceValue = animator;
            vso.ApplyModifiedPropertiesWithoutUndo();

            Transform sprite = root.transform.Find(SpritePath);
            var sr = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;
            if (sr == null)
            {
                Debug.LogWarning($"[BossPrefabBuilder] {SpritePath}가 없어 스프라이트를 못 꽂는다.");
                return;
            }

            Sprite idle = FirstSprite(IdleSheet);
            if (idle != null) sr.sprite = idle;

            // 기준 프리팹은 변종색(빨강)으로 물들어 있다. 그대로 두면 시트가 통째로 붉어진다.
            sr.color = Color.white;
            sr.flipX = false;
            sprite.localScale = Vector3.one;

            var belt = root.GetComponent<BeltScrollView>();
            if (belt == null) return;

            var bso = new SerializedObject(belt);
            bso.FindProperty("spriteOffsetY").floatValue = 0f;   // 피벗이 발밑이라 더 올릴 필요가 없다
            bso.FindProperty("flipToFacing").boolValue = true;
            bso.FindProperty("facingRenderer").objectReferenceValue = sr;
            bso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(belt);
        }

        /// <summary>슬라이스된 시트의 첫 프레임. 인스펙터에서 보스를 알아볼 수 있게 꽂아 둔다.</summary>
        private static Sprite FirstSprite(string sheetPath)
        {
            Sprite best = null;
            int bestIndex = int.MaxValue;

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (!(o is Sprite s)) continue;

                int at = s.name.LastIndexOf('_');
                int index = at >= 0 && int.TryParse(s.name.Substring(at + 1), out int n) ? n : 0;
                if (index >= bestIndex) continue;

                bestIndex = index;
                best = s;
            }

            if (best == null)
                Debug.LogWarning($"[BossPrefabBuilder] {sheetPath}의 슬라이스 결과가 없다. 아트 빌더를 먼저 돌릴 것.");

            return best;
        }

        /// <summary>
        /// 히트박스 자식 하나. 기준 프리팹의 옛 캡슐 판정을 걷어내고 박스로 다시 만든다.
        /// </summary>
        private static Attack EnsureHitbox(GameObject root, string name, Vector3 localPos, Vector3 size)
        {
            GameObject go = EnsureHitboxObject(root, name, localPos);

            // 박스를 <b>먼저</b> 붙이고 옛 콜라이더를 지운다.
            // 순서가 반대면 Attack의 [RequireComponent(typeof(Collider))] 때문에
            // 마지막 콜라이더 제거가 거부되어 옛 캡슐 판정이 그대로 남는다.
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();

            KeepOnly(go, box);

            box.isTrigger = true;
            box.size = size;
            box.center = Vector3.zero;

            return FinishHitbox(go, root);
        }

        /// <summary>
        /// 둘레 판정용 구 히트박스. 상자와 달리 회전을 타지 않으므로
        /// 보스가 어디를 보고 있든 같은 범위를 덮는다 — 그게 이 히트박스의 존재 이유다.
        /// </summary>
        private static Attack EnsureSphereHitbox(GameObject root, string name, Vector3 localPos, float radius)
        {
            GameObject go = EnsureHitboxObject(root, name, localPos);

            var sphere = go.GetComponent<SphereCollider>();
            if (sphere == null) sphere = go.AddComponent<SphereCollider>();

            KeepOnly(go, sphere);

            sphere.isTrigger = true;
            sphere.radius = radius;
            sphere.center = Vector3.zero;

            return FinishHitbox(go, root);
        }

        /// <summary>히트박스 자식 하나. 재실행 때는 이름으로 찾아 자리만 다시 잡는다.</summary>
        private static GameObject EnsureHitboxObject(GameObject root, string name, Vector3 localPos)
        {
            Transform child = root.transform.Find(name);
            GameObject go = child != null ? child.gameObject : NewChild(root, name);

            var marker = go.GetComponent<SpriteRenderer>();
            if (marker != null) Object.DestroyImmediate(marker);

            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            return go;
        }

        /// <summary>
        /// 남겨 둘 콜라이더 하나만 남기고 나머지를 지운다.
        /// 옛 모양이 남아 있으면 판정이 둘로 겹쳐 같은 대상을 두 번 때린다.
        /// </summary>
        private static void KeepOnly(GameObject go, Collider keep)
        {
            foreach (Collider c in go.GetComponents<Collider>())
                if (c != keep) Object.DestroyImmediate(c);
        }

        private static Attack FinishHitbox(GameObject go, GameObject root)
        {
            Attack hitbox = go.GetComponent<Attack>();
            if (hitbox == null) hitbox = go.AddComponent<Attack>();
            hitbox.Attacker = root.GetComponent<Combat>();

            EditorUtility.SetDirty(hitbox);
            return hitbox;
        }

        private static void WireEnemy(GameObject root, EnemyData data, Attack basicHitbox)
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy == null) return;

            // private [SerializeField]는 SerializedObject로만 안전하게 건드린다.
            var so = new SerializedObject(enemy);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("basicAttack").objectReferenceValue = basicHitbox;
            so.FindProperty("basicAttackWindup").floatValue = BasicWindup;
            so.FindProperty("basicAttackActiveEnd").floatValue = BasicActiveEnd;
            so.FindProperty("basicAttackTotal").floatValue = BasicTotal;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(enemy);
        }

        /// <summary>
        /// 가드를 프리팹에 박는다. <b>보스를 보스로 만드는 값</b>이라 빌더가 직접 쓴다.
        ///
        /// 기준 프리팹(Enemy_Melee)에는 가드가 없어서, 이걸 안 쓰면 빌더를 돌릴 때마다
        /// <c>maxGuard</c>가 0으로 떨어지고 보스가 평상시 슈퍼아머를 조용히 잃는다.
        /// 손으로 인스펙터에 넣어 두는 방식이 실제로 한 번 그렇게 날아갔다.
        /// </summary>
        private static void WireGuard(GameObject root)
        {
            var combat = root.GetComponent<Combat>();
            if (combat == null) return;

            var so = new SerializedObject(combat);
            so.FindProperty("maxGuard").floatValue = MaxGuard;
            so.FindProperty("defaultGuardDamage").floatValue = GuardDamage;
            so.FindProperty("guardBreakDuration").floatValue = GuardBreakDuration;
            so.FindProperty("guardRegenDelay").floatValue = GuardRegenDelay;
            so.FindProperty("guardRegen").floatValue = GuardRegen;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(combat);
        }

        private static void WireControl(GameObject root, BossBrainAsset brain)
        {
            var control = root.GetComponent<EnemyControl>();
            if (control == null) return;

            var so = new SerializedObject(control);
            so.FindProperty("brain").objectReferenceValue = brain;
            so.FindProperty("attackInterval").floatValue = AttackInterval;
            so.FindProperty("specialInterval").floatValue = 4f;

            SerializedProperty p = so.FindProperty("parameters");
            p.FindPropertyRelative("attackRange").floatValue = AttackRange;
            p.FindPropertyRelative("leashRange").floatValue = LeashRange;
            p.FindPropertyRelative("preferredMinRange").floatValue = 0f;
            // 특수 사거리는 브레인 규칙이 패턴마다 따로 들고 있다. 여기 값은 안 쓰인다.
            p.FindPropertyRelative("specialRange").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(control);
        }

        /// <summary>
        /// 패턴 표 → 실행기. 브레인 규칙의 index와 같은 순서다.
        /// 돌진 멧돼지의 실행기가 붙어 있으면 걷어낸다 — 한 Entity에 특수 실행기는 하나뿐이다.
        /// </summary>
        private static void WirePatterns(GameObject root, Attack basicHitbox, Attack wideHitbox, Attack radialHitbox)
        {
            var charge = root.GetComponent<EnemyChargeAction>();
            if (charge != null) Object.DestroyImmediate(charge);

            var action = root.GetComponent<BossPatternAction>();
            if (action == null) action = root.AddComponent<BossPatternAction>();

            var built = new BossPattern[BossPatternTable.All.Length];
            for (int i = 0; i < built.Length; i++)
            {
                BossPatternTable.Entry e = BossPatternTable.All[i];
                built[i] = new BossPattern
                {
                    label = e.label,
                    chargeTime = e.chargeTime,
                    chargeHitDelay = e.chargeHitDelay,
                    chargeClip = e.chargeState,
                    telegraph = e.telegraph,
                    active = e.active,
                    recovery = e.recovery,
                    hitCount = e.hitCount,
                    hitDuration = e.hitDuration,
                    damageScale = e.damageScale,
                    hitbox = HitboxFor(e.hitboxKind, basicHitbox, wideHitbox, radialHitbox),
                    hit = BuildHit(e),
                    advanceSpeed = e.advanceSpeed,
                    cancelOnContact = e.cancelOnContact,
                    telegraphClip = e.windupState,
                    activeClip = e.state,
                    attackPowerScale = e.attackPowerScale,
                    moveSpeedScale = e.moveSpeedScale,
                    attackIntervalScale = e.attackIntervalScale,
                    once = e.once,
                };
            }

            action.SetPatterns(built);
            EditorUtility.SetDirty(action);
        }

        private static Attack HitboxFor(BossPatternTable.HitboxKind kind,
                                        Attack basic, Attack wide, Attack radial)
        {
            switch (kind)
            {
                case BossPatternTable.HitboxKind.Wide: return wide;
                case BossPatternTable.HitboxKind.Radial: return radial;
                default: return basic;
            }
        }

        /// <summary>
        /// 패턴이 실을 타격 정보. 데미지는 <see cref="BossPatternAction"/>이 공격력에서 다시 계산하므로
        /// 여기 damage는 스탯이 없을 때의 폴백일 뿐이다.
        ///
        /// 연타(hitCount &gt; 1)는 경직을 짧게 준다 — 길면 첫 타에 굳은 대상 위로
        /// 나머지가 지나가 버려 세 번 맞았다는 게 화면에서 안 읽힌다.
        /// </summary>
        private static HitData BuildHit(in BossPatternTable.Entry e)
        {
            bool multi = e.hitCount > 1;

            return new HitData
            {
                damageData = new DamageData(10f),
                targetState = CombatState.Neutral,
                nextState = multi ? CombatState.LightHit : CombatState.Knockback,
                mode = multi ? KnockbackMode.Fixed : KnockbackMode.AwayFromCaster,
                fixedDir = Vector3.forward,
                knockbackForce = multi ? 3f : 10f,
                launchForce = e.hitboxKind != BossPatternTable.HitboxKind.Basic
                              && !multi && e.advanceSpeed <= 0f ? 4f : 0f,
                hitStunDuration = multi ? 0.22f : 0.55f,
            };
        }

        /// <summary>
        /// 진영 레이어. 이게 빠지면 충돌 매트릭스가 히트박스를 걸러내지 못해
        /// 보스가 아무도 못 때리거나 아군 판정에 얻어맞는다.
        /// </summary>
        private static void AssignLayers(GameObject root, params Attack[] hitboxes)
        {
            int hurt = LayerMask.NameToLayer("EnemyHurtbox");
            int hit = LayerMask.NameToLayer("EnemyHitbox");

            if (hurt < 0 || hit < 0)
            {
                Debug.LogWarning(
                    "[BossPrefabBuilder] EnemyHurtbox/EnemyHitbox 레이어가 없다. " +
                    "'Prototype ▸ 씬 벨트스크롤 배치로 정리'를 한 번 돌리면 등록된다.");
                return;
            }

            root.layer = hurt;
            foreach (Attack a in hitboxes)
                if (a != null) a.gameObject.layer = hit;
        }

        // ── 유틸 ────────────────────────────────────────

        private static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = parent.layer;
            return go;
        }

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
