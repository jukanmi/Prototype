using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 기본 투사체 프리팹을 만들고 원거리 스킬에 물린다.
    /// 아트가 없으니 도형 스프라이트로 때운다 — 구조만 맞으면 나중에 갈아끼우면 된다.
    /// </summary>
    public static class ProjectileBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Projectile.prefab";

        [MenuItem("Prototype/투사체 - 프리팹 만들고 원거리에 물리기")]
        public static void BuildAndAssign()
        {
            Projectile prefab = BuildPrefab();
            int skills = AssignToRangedSkills(prefab);
            int basics = AssignBasicAttacks(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[ProjectileBuilder] 스킬 {skills}개 · 평타 {basics}명에 배정", prefab);
        }

        /// <summary>
        /// 궁수 · 마법사는 평타도 날아간다. 근접처럼 붙어야 때리는 게 말이 안 된다.
        /// 사거리는 스킬보다 짧게 잡아 스킬의 우위를 남긴다.
        /// </summary>
        private static int AssignBasicAttacks(Projectile prefab)
        {
            if (prefab == null) return 0;

            int count = 0;

            // 씬이 아니라 표에 쓴다. 파티는 BattleInput 프리팹 안으로 들어갔고,
            // 씬 인스턴스에 쓰면 없앤 오버라이드가 씬마다 되살아난다.
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(PartyMemberData)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var member = AssetDatabase.LoadAssetAtPath<PartyMemberData>(path);
                if (member == null) continue;
                if (member.role != Role.Archer && member.role != Role.Wizard) continue;

                member.basicProjectile = prefab;
                member.projectileSpeed = member.role == Role.Archer ? 20f : 15f;
                member.projectileRange = member.role == Role.Archer ? 9f : 8f;
                member.projectilePierce = 0;

                EditorUtility.SetDirty(member);
                count++;

                Debug.Log($"[ProjectileBuilder] {member.Label} ({member.role}) 평타 → 투사체", member);
            }

            if (count == 0)
                Debug.LogWarning("[ProjectileBuilder] PartyMemberData 가 하나도 없다. " +
                                 "'Prototype ▸ 파티 - 1단계: 씬에서 표 추출'을 먼저 돌릴 것.");

            return count;
        }

        /// <summary>
        /// 루트 = 논리 좌표 + 판정. 자식 Sprite/Shadow = 화면에 접어서 그리는 것.
        /// 캐릭터와 같은 구조라 BeltScroll 변환이 그대로 먹는다.
        /// </summary>
        private static Projectile BuildPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing.GetComponent<Projectile>();

            var root = new GameObject("Projectile");

            // 판정 — 트리거만 쓴다. 이동은 코드가 한다.
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.35f;
            col.isTrigger = true;

            // Attack이 OnTriggerEnter를 받으려면 한쪽에 Rigidbody가 있어야 한다.
            // 투사체 쪽에 키네마틱으로 붙인다 — 유니티 물리가 위치를 건드리면 안 되므로.
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            root.AddComponent<Attack>();
            var proj = root.AddComponent<Projectile>();

            var sprite = MakeChild(root, "Sprite", "Circle", new Color(1f, 0.85f, 0.35f, 1f), 0.4f, 100);
            var shadow = MakeChild(root, "Shadow", "Circle", new Color(0f, 0f, 0f, 0.3f), 0.3f, 99);
            shadow.localScale = new Vector3(0.3f, 0.12f, 1f);

            var so = new SerializedObject(proj);
            so.FindProperty("sprite").objectReferenceValue = sprite;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.ApplyModifiedProperties();

            EnsureFolder(PrefabFolder);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return saved.GetComponent<Projectile>();
        }

        private static Transform MakeChild(GameObject parent, string name, string spriteName,
                                           Color color, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = FindSprite(spriteName);
            sr.color = color;
            sr.sortingOrder = order;

            go.transform.localScale = Vector3.one * scale;
            return go.transform;
        }

        /// <summary>
        /// 궁수 · 마법사 스킬 중 <b>장판이 아닌 것</b>만 투사체로 본다.
        ///
        /// 조준 방식으로는 더 이상 가를 수 없다 — 대상 지정이 전부 GroundPoint로 넘어와서
        /// 연속 사격도, 중력장도 똑같이 GroundPoint다.
        /// 실제 차이는 <b>기준점에서 터지는 광역 효과를 들고 있는지</b>다.
        /// </summary>
        private static int AssignToRangedSkills(Projectile prefab)
        {
            if (prefab == null) return 0;

            int count = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;

                bool ranged = (data.role == Role.Archer || data.role == Role.Wizard)
                           && data.attackType != AttackType.Gather
                           && !IsGroundField(data);

                if (!ranged || data.projectile != null) continue;

                data.projectile = prefab;

                // 다단 히트는 연사라 빠르고 짧게, 밀치기는 무겁고 멀리.
                switch (data.attackType)
                {
                    case AttackType.Push:
                        data.projectileSpeed = 18f;
                        data.projectileRange = 14f;
                        data.projectilePierce = 0;
                        break;
                    case AttackType.Strike:
                        data.projectileSpeed = 22f;
                        data.projectileRange = 10f;
                        data.projectilePierce = 0;
                        break;
                    default:
                        data.projectileSpeed = 16f;
                        data.projectileRange = 11f;
                        data.projectilePierce = 0;
                        break;
                }

                EditorUtility.SetDirty(data);
                count++;

                Debug.Log($"[ProjectileBuilder] {data.skillName} ({data.role}/{data.attackType}) → 투사체", data);
            }

            return count;
        }

        /// <summary>
        /// 기준점에서 터지는 장판인지. 끌어당기거나 띄우는 효과는 시전 좌표를 중심으로 돌기 때문에
        /// 날아갈 투사체가 따로 없다.
        /// </summary>
        private static bool IsGroundField(SkillData data)
        {
            if (data.effects == null) return false;

            return data.effects.Exists(e => e is PullEffect || e is AirborneEffect);
        }

        private static Sprite FindSprite(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null) return s;
            }
            return null;
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
