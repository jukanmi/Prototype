using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 근접 · 원거리 · 돌진 적 3종을 프리팹으로 찍어낸다.
    ///
    /// 기존 <c>enemy.prefab</c>을 <b>기준</b>으로 복제해 컴포넌트 구성과 수치를 물려받고,
    /// 브레인 · 데이터 · 머티리얼을 갈아끼운다. 원본은 절대 건드리지 않는다 —
    /// 씬이 그 GUID를 참조하고 있다.
    ///
    /// 지오메트리는 <see cref="TestSceneBuilder"/>의 규약으로 맞춘다:
    /// <b>정면은 로컬 +Z</b>(Physics.Apply가 LookRotation으로 돌리는 축),
    /// 몸통은 center (0,1,0) / height 2, 히트박스는 (0, 0.9, +1). 손으로 만든 옛 프리팹처럼
    /// ±X에 히트박스를 두면 회전 후 깊이축으로 빠져 옆에 선 대상에게 영영 닿지 않는다.
    /// 몸통도 스프라이트 대신 캡슐 메쉬를 쓴다 — 루트가 Y축으로 도는데 스프라이트를 붙이면
    /// 옆면이 보여 사라진다.
    ///
    /// 몇 번을 돌려도 같은 경로를 갱신할 뿐 애셋이 늘지 않는다.
    /// </summary>
    public static class EnemyPrefabBuilder
    {
        private const string BasePrefabPath = "Assets/Prefabs/enemy.prefab";
        private const string ProjectilePath = "Assets/Prefabs/Projectile.prefab";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string MaterialFolder = "Assets/Prefabs/Materials";
        private const string DataFolder = "Assets/Data/Enemy";

        /// <summary>돌진 히트박스 자식 이름. 재실행 때 이 이름으로 찾아 갱신한다.</summary>
        private const string ChargeHitboxName = "ChargeHitbox";

        // ── 지오메트리 규약 (TestSceneBuilder와 동일) ──
        private static readonly Vector3 BodyCenter = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 BasicHitboxPos = new Vector3(0f, 0.9f, 1f);
        private static readonly Vector3 BasicHitboxSize = new Vector3(1.3f, 1.4f, 1.4f);
        private static readonly Vector3 ChargeHitboxPos = new Vector3(0f, 0.9f, 1.4f);
        private static readonly Vector3 ChargeHitboxSize = new Vector3(1.6f, 1.6f, 1.8f);

        /// <summary>한 종류를 만드는 데 필요한 것 전부. 표를 늘리면 적이 늘어난다.</summary>
        private struct Variant
        {
            public string id;                  // Melee / Ranged / Charger
            public string displayName;
            public Color color;

            public float hp;
            public float atk;
            public float moveSpeed;

            public float attackRange;
            public float attackInterval;
            public float leashRange;
            public float preferredMinRange;

            public bool ranged;
            public float projectileSpeed;
            public float projectileRange;

            public bool charger;
            public float specialRange;
            public float specialInterval;
        }

        private static readonly Variant[] Variants =
        {
            new Variant
            {
                id = "Melee", displayName = "고블린", color = new Color(0.90f, 0.30f, 0.28f),
                hp = 40f, atk = 5f, moveSpeed = 4f,
                attackRange = 1.8f, attackInterval = 1.5f, leashRange = 14f,
            },
            new Variant
            {
                id = "Ranged", displayName = "궁수 고블린", color = new Color(0.35f, 0.62f, 1f),
                hp = 26f, atk = 4f, moveSpeed = 3.4f,
                // 사거리는 투사체 사거리의 80%(BasicAttackReach)가 실제 기준이 된다.
                attackRange = 7.2f, attackInterval = 1.8f, leashRange = 16f, preferredMinRange = 4f,
                ranged = true, projectileSpeed = 14f, projectileRange = 9f,
            },
            new Variant
            {
                id = "Charger", displayName = "돌진 멧돼지", color = new Color(1f, 0.65f, 0.20f),
                hp = 60f, atk = 7f, moveSpeed = 3.6f,
                attackRange = 2f, attackInterval = 1.6f, leashRange = 18f,
                charger = true, specialRange = 7f, specialInterval = 4f,
            },
        };

        [MenuItem("Prototype/적 - 근접·원거리·돌진 프리팹 만들기")]
        public static void Build()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"[EnemyPrefabBuilder] 기준 프리팹이 없다: {BasePrefabPath}");
                return;
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(DataFolder);

            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            Projectile projectilePrefab = projectile != null ? projectile.GetComponent<Projectile>() : null;

            foreach (Variant v in Variants)
                BuildVariant(v, basePrefab, projectilePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyPrefabBuilder] 적 {Variants.Length}종 생성 완료 → {PrefabFolder}/Enemy_*.prefab");
        }

        private static void BuildVariant(Variant v, GameObject basePrefab, Projectile projectilePrefab)
        {
            EnemyBrainAsset brain = EnsureBrain(v);
            Material material = EnsureMaterial(v);
            EnemyData data = EnsureData(v, brain, v.ranged ? projectilePrefab : null);

            // 프리팹 애셋을 직접 편집하지 않는다. 사본을 조립해서 통째로 저장한다.
            var root = Object.Instantiate(basePrefab);
            root.name = $"Enemy_{v.id}";
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            BuildBody(root, material);
            Attack basicHitbox = EnsureBasicHitbox(root);
            WireEnemy(root, data, basicHitbox, v, projectilePrefab);
            WireControl(root, brain, v);

            if (v.charger) WireCharge(root);
            else RemoveCharge(root);

            string path = $"{PrefabFolder}/Enemy_{v.id}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            // 데이터 → 프리팹 역참조. 스포너가 데이터만 들고 소환할 수 있게.
            data.prefab = saved;
            EditorUtility.SetDirty(data);

            Debug.Log($"[EnemyPrefabBuilder] {path} 저장", saved);
        }

        // ── 애셋 ────────────────────────────────────────

        /// <summary>브레인 애셋. 근접은 기존 애셋을 재사용한다.</summary>
        private static EnemyBrainAsset EnsureBrain(Variant v)
        {
            string path = $"{DataFolder}/Brain_{v.id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyBrainAsset>(path);

            EnemyBrainAsset wanted = existing;
            System.Type type = v.ranged ? typeof(RangedBrainAsset)
                             : v.charger ? typeof(ChargerBrainAsset)
                             : typeof(MeleeBrainAsset);

            // 타입이 어긋난 애셋은 갈아끼운다. 판단 로직이 통째로 다르다.
            if (wanted == null || wanted.GetType() != type)
            {
                if (wanted != null) AssetDatabase.DeleteAsset(path);

                wanted = (EnemyBrainAsset)ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(wanted, path);
            }

            return wanted;
        }

        private static EnemyData EnsureData(Variant v, EnemyBrainAsset brain, Projectile projectilePrefab)
        {
            string path = $"{DataFolder}/Enemy_{v.id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.enemyId = $"Enemy_{v.id}";
            data.enemyName = v.displayName;
            data.hp = v.hp;
            data.atk = v.atk;
            data.brain = brain;
            data.moveSpeed = v.moveSpeed;
            data.attackRange = v.attackRange;
            data.attackInterval = v.attackInterval;
            data.retargetInterval = 0.5f;
            data.leashRange = v.leashRange;
            data.preferredMinRange = v.preferredMinRange;
            data.specialRange = v.specialRange;
            data.specialInterval = v.specialInterval;

            data.basicProjectile = projectilePrefab;
            data.projectileSpeed = v.projectileSpeed;
            data.projectileRange = v.projectileRange;
            data.projectilePierce = 0;

            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>몸통 메쉬용 머티리얼. 셰이더 선택은 TestSceneBuilder와 같게 맞춘다.</summary>
        private static Material EnsureMaterial(Variant v)
        {
            string path = $"{MaterialFolder}/M_Enemy_{v.id}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            mat.color = v.color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", v.color);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── 프리팹 조립 ─────────────────────────────────

        /// <summary>
        /// 몸통. 루트는 Facing 방향으로 Y축 회전하므로 스프라이트를 붙이면 옆면이 보여 사라진다.
        /// TestSceneBuilder처럼 캡슐 메쉬를 쓰고, 정면 확인용 마커를 같이 붙인다.
        /// </summary>
        private static void BuildBody(GameObject root, Material material)
        {
            var legacySprite = root.GetComponent<SpriteRenderer>();
            if (legacySprite != null) Object.DestroyImmediate(legacySprite);

            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = BodyCenter;      // 발이 바닥(y=0)에 닿고 몸통이 위로 선다
            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.isTrigger = false;

            MakeMesh(root, "Body", PrimitiveType.Capsule, BodyCenter, Vector3.one, material);
            MakeMesh(root, "FacingMarker", PrimitiveType.Cube,
                     new Vector3(0f, 1f, 0.55f), new Vector3(0.2f, 0.2f, 0.4f), material);
        }

        private static void MakeMesh(GameObject root, string name, PrimitiveType type,
                                     Vector3 localPos, Vector3 scale, Material material)
        {
            Transform found = root.transform.Find(name);
            if (found != null) Object.DestroyImmediate(found.gameObject);

            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.layer = root.layer;

            // 판정은 루트와 히트박스만 갖는다. 시각용 콜라이더가 남으면 넉백이 엉킨다.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// 기준 프리팹의 히트박스 자식에는 <see cref="Attack"/>이 빠져 있다.
        /// 그대로 두면 근접 적이 아무도 못 때린다 — 여기서 채워 넣고 규약 위치로 옮긴다.
        /// </summary>
        private static Attack EnsureBasicHitbox(GameObject root)
        {
            Transform child = root.transform.Find("Attack");
            GameObject go = child != null ? child.gameObject : NewChild(root, "Attack");

            // 옛 규약의 캡슐·마커 스프라이트를 걷어내고 규약대로 다시 만든다.
            foreach (Collider c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
            var marker = go.GetComponent<SpriteRenderer>();
            if (marker != null) Object.DestroyImmediate(marker);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = BasicHitboxSize;
            box.center = Vector3.zero;

            go.transform.localPosition = BasicHitboxPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            Attack hitbox = go.GetComponent<Attack>();
            if (hitbox == null) hitbox = go.AddComponent<Attack>();
            hitbox.Attacker = root.GetComponent<Combat>();

            EditorUtility.SetDirty(hitbox);
            return hitbox;
        }

        private static void WireEnemy(GameObject root, EnemyData data, Attack hitbox, Variant v,
                                      Projectile projectilePrefab)
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy == null) return;

            // private [SerializeField]는 SerializedObject로만 안전하게 건드린다.
            var so = new SerializedObject(enemy);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("basicAttack").objectReferenceValue = hitbox;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 데이터 주입 없이 프리팹만 씬에 끌어다 놔도 원거리로 동작해야 한다.
            if (v.ranged && projectilePrefab != null)
                enemy.ConfigureBasicProjectile(projectilePrefab, v.projectileSpeed, v.projectileRange, 0);

            EditorUtility.SetDirty(enemy);
        }

        private static void WireControl(GameObject root, EnemyBrainAsset brain, Variant v)
        {
            var control = root.GetComponent<EnemyControl>();
            if (control == null) return;

            var so = new SerializedObject(control);
            so.FindProperty("brain").objectReferenceValue = brain;
            so.FindProperty("attackInterval").floatValue = v.attackInterval;
            so.FindProperty("specialInterval").floatValue = v.specialInterval;

            SerializedProperty p = so.FindProperty("parameters");
            p.FindPropertyRelative("attackRange").floatValue = v.attackRange;
            p.FindPropertyRelative("leashRange").floatValue = v.leashRange;
            p.FindPropertyRelative("preferredMinRange").floatValue = v.preferredMinRange;
            p.FindPropertyRelative("specialRange").floatValue = v.specialRange;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(control);
        }

        /// <summary>
        /// 돌진 실행기와 전용 히트박스. 평타 히트박스를 공유하면
        /// 평타 적중이 돌진을 끊어 버린다(둘 다 같은 OnHit을 탄다).
        /// </summary>
        private static void WireCharge(GameObject root)
        {
            // ?? 는 유니티의 == 오버로드를 타지 않는다. 파괴/미부착 객체가 그대로 통과한다.
            var action = root.GetComponent<EnemyChargeAction>();
            if (action == null) action = root.AddComponent<EnemyChargeAction>();

            Transform found = root.transform.Find(ChargeHitboxName);
            GameObject go = found != null ? found.gameObject : NewChild(root, ChargeHitboxName);

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = ChargeHitboxSize;
            box.center = Vector3.zero;

            go.transform.localPosition = ChargeHitboxPos;   // 평타보다 한 칸 더 앞
            go.transform.localRotation = Quaternion.identity;

            Attack hitbox = go.GetComponent<Attack>();
            if (hitbox == null) hitbox = go.AddComponent<Attack>();
            hitbox.Attacker = root.GetComponent<Combat>();

            var so = new SerializedObject(action);
            so.FindProperty("chargeHitbox").objectReferenceValue = hitbox;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(action);
        }

        /// <summary>표에서 돌진을 끄면 이미 붙은 실행기도 걷어낸다 — 재실행이 상태를 남기지 않게.</summary>
        private static void RemoveCharge(GameObject root)
        {
            var action = root.GetComponent<EnemyChargeAction>();
            if (action != null) Object.DestroyImmediate(action);

            Transform found = root.transform.Find(ChargeHitboxName);
            if (found != null) Object.DestroyImmediate(found.gameObject);
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
