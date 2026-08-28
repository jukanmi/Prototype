using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Prototype;
using PPhysics = Prototype.Physics;

namespace PrototypeEditor
{
    /// <summary>
    /// 테스트용 씬 · 프리팹 · 스킬 에셋을 한 번에 만든다.
    /// 메뉴: Prototype ▸ 테스트 씬 만들기
    ///
    /// 프리팹은 스프라이트 대신 3D 프리미티브로 그린다.
    /// 논리 좌표(X 좌우 / Z 깊이 / Y 높이)를 눈으로 직접 보기 위해서다.
    /// 스프라이트 작업이 붙으면 BeltScrollView로 교체하면 된다.
    /// </summary>
    public static class TestSceneBuilder
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string MaterialDir = "Assets/Prefabs/Materials";
        private const string SkillDir = "Assets/Skills";
        private const string DataDir = "Assets/Data";
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = SceneDir + "/TestBattle.unity";
        private const string WallLayer = "Wall";

        [MenuItem("Prototype/테스트 씬 만들기", priority = 0)]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "테스트 씬 만들기",
                    "스킬 에셋 20개, 프리팹 6개, 씬 1개(TestBattle)를 생성한다.\n" +
                    "같은 이름의 기존 에셋은 덮어쓴다. 계속할까?",
                    "만들기", "취소"))
                return;

            EnsureFolders();
            EnsureLayer(WallLayer);

            Dictionary<string, Material> mats = CreateMaterials();
            SkillSet skills = CreateSkills();
            EnemyData enemyData = CreateEnemyData();

            GameObject playerPrefab = BuildPlayerPrefab(mats["Player"]);
            Dictionary<Role, GameObject> allyPrefabs = BuildAllyPrefabs(mats, skills);
            GameObject enemyPrefab = BuildEnemyPrefab(mats["Enemy"], enemyData);

            BuildScene(playerPrefab, allyPrefabs, enemyPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<b>[TestSceneBuilder]</b> 완료 — " + ScenePath + " 를 열고 Play 를 누를 것.");
        }

        // ── 폴더 / 레이어 ────────────────────────────────

        private static void EnsureFolders()
        {
            foreach (string dir in new[] { PrefabDir, MaterialDir, SkillDir, DataDir, SceneDir })
            {
                if (Directory.Exists(dir)) continue;

                Directory.CreateDirectory(dir);
                AssetDatabase.ImportAsset(dir);
            }
        }

        /// <summary>Physics.wallMask 가 가리킬 레이어를 만든다.</summary>
        private static void EnsureLayer(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0) return;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // 0~7 은 빌트인. 8번부터 빈 칸을 찾는다.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return;
            }

            Debug.LogWarning($"[TestSceneBuilder] 빈 레이어 슬롯이 없다. '{layerName}' 레이어를 직접 만들 것.");
        }

        // ── 머티리얼 ─────────────────────────────────────

        private static Dictionary<string, Material> CreateMaterials()
        {
            var colors = new Dictionary<string, Color>
            {
                { "Player", new Color(0.30f, 0.55f, 0.95f) },
                { "Tanker", new Color(0.85f, 0.75f, 0.30f) },
                { "Warrior", new Color(0.90f, 0.45f, 0.25f) },
                { "Archer", new Color(0.40f, 0.85f, 0.55f) },
                { "Wizard", new Color(0.70f, 0.45f, 0.90f) },
                { "Enemy", new Color(0.85f, 0.25f, 0.25f) },
                { "Floor", new Color(0.22f, 0.22f, 0.26f) },
                { "Wall", new Color(0.35f, 0.32f, 0.30f) },
            };

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var result = new Dictionary<string, Material>();
            foreach (var kv in colors)
            {
                string path = $"{MaterialDir}/M_{kv.Key}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }

                mat.shader = shader;
                mat.color = kv.Value;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", kv.Value);

                EditorUtility.SetDirty(mat);
                result[kv.Key] = mat;
            }

            return result;
        }

        // ── 스킬 에셋 ────────────────────────────────────

        private class SkillSet
        {
            public readonly Dictionary<Role, List<SkillData>> Combo = new Dictionary<Role, List<SkillData>>();
        }

        private static SkillData MakeSkill(
            string file, string skillName, Role role, AttackType type,
            CombatState require, CombatState result, TargetingType targeting,
            List<HitData> hits, List<ISkillEffect> effects,
            float radius = 3f, float castTime = 0.15f, float hitInterval = 0.3f, float recovery = 0.2f)
        {
            string path = $"{SkillDir}/{file}.asset";
            var so = AssetDatabase.LoadAssetAtPath<SkillData>(path);

            if (so == null)
            {
                so = ScriptableObject.CreateInstance<SkillData>();
                AssetDatabase.CreateAsset(so, path);
            }

            so.skillName = skillName;
            so.role = role;
            so.attackType = type;
            so.requireState = require;
            so.resultState = result;
            so.targeting = targeting;
            so.radius = radius;
            so.castTime = castTime;
            so.hitInterval = hitInterval;
            so.recoveryTime = recovery;
            so.hitDataList = hits ?? new List<HitData>();
            so.effects = effects ?? new List<ISkillEffect>();
            so.cooldown = 5f;
            so.manaCost = 20f;

            EditorUtility.SetDirty(so);
            return so;
        }

        private static HitData Hit(float dmg, CombatState next, KnockbackMode mode,
            float push = 0f, float airborne = 0f, float stun = 0.3f,
            bool snapZ = false, bool otg = false)
        {
            return new HitData
            {
                damageData = new DamageData(dmg),
                targetState = CombatState.Neutral,
                nextState = next,
                canOtg = otg,
                mode = mode,
                fixedDir = Vector3.forward,
                pushDistance = push,
                airborneHeight = airborne,
                hitStunDuration = stun,
                snapZ = snapZ,
            };
        }

        private static SkillSet CreateSkills()
        {
            var s = new SkillSet();

            // ── 탱커 ──────────────────────────────────
            s.Combo[Role.Tanker] = new List<SkillData>
            {
                MakeSkill("SK_T1_사슬끌어당기기", "사슬 끌어당기기", Role.Tanker, AttackType.Gather,
                    CombatState.Neutral, CombatState.LightHit, TargetingType.GroundPoint,
                    new List<HitData>(),                       // 판정은 PullEffect가 낸다
                    new List<ISkillEffect> { new PullEffect() }, radius: 4.5f),

                MakeSkill("SK_T2_방패올려치기", "방패 올려치기", Role.Tanker, AttackType.Launcher,
                    CombatState.LightHit, CombatState.AerialHit, TargetingType.GroundPoint,
                    new List<HitData>(),
                    new List<ISkillEffect> { new AirborneEffect() }),

                MakeSkill("SK_T3_강철돌진", "강철 돌진", Role.Tanker, AttackType.Charge,
                    CombatState.Neutral, CombatState.LightHit, TargetingType.Direction,
                    new List<HitData> { Hit(12f, CombatState.LightHit, KnockbackMode.Fixed, push: 0.625f) },
                    new List<ISkillEffect> { new ChargeEffect() }),

                // AttackType.DamageCut 이 사라져 분류만 Strike로 옮겼다. 효과는 그대로 방어형이다.
                MakeSkill("SK_T4_도발방벽", "도발 방벽", Role.Tanker, AttackType.Strike,
                    CombatState.Neutral, CombatState.Neutral, TargetingType.None,
                    new List<HitData>(),
                    new List<ISkillEffect> { new DamageCutEffect(), new TauntEffect(), new ShieldEffect() }),
            };

            // ── 전사 (기획서 SK-01 ~ SK-03) ──────────────
            s.Combo[Role.Warrior] = new List<SkillData>
            {
                MakeSkill("SK_W1_소용돌이베기", "소용돌이 베기", Role.Warrior, AttackType.Gather,
                    CombatState.Neutral, CombatState.LightHit, TargetingType.GroundPoint,
                    new List<HitData>(),
                    new List<ISkillEffect> { new PullEffect() }, radius: 4f),

                MakeSkill("SK_W2_올려베기", "올려베기", Role.Warrior, AttackType.Launcher,
                    CombatState.LightHit, CombatState.AerialHit, TargetingType.GroundPoint,
                    new List<HitData>(),
                    new List<ISkillEffect> { new AirborneEffect() }),

                MakeSkill("SK_W3_돌진베기", "돌진 베기", Role.Warrior, AttackType.Strike,
                    CombatState.Neutral, CombatState.LightHit, TargetingType.Direction,
                    new List<HitData> { Hit(15f, CombatState.LightHit, KnockbackMode.Fixed, push: 0.5f) },
                    new List<ISkillEffect> { new ChargeEffect() }),

                MakeSkill("SK_W4_연참", "연참", Role.Warrior, AttackType.Strike,
                    CombatState.AerialHit, CombatState.AerialHit, TargetingType.GroundPoint,
                    new List<HitData>
                    {
                        Hit(6f, CombatState.AerialHit, KnockbackMode.Up, airborne: 0.2667f, stun: 0.35f),
                        Hit(6f, CombatState.AerialHit, KnockbackMode.Up, airborne: 0.15f, stun: 0.35f),
                        Hit(8f, CombatState.AerialHit, KnockbackMode.Up, airborne: 0.15f, stun: 0.4f),
                    }, null, hitInterval: 0.25f),
            };

            // ── 궁수 (기획서 SK-04 ~ SK-05) ──────────────
            s.Combo[Role.Archer] = new List<SkillData>
            {
                MakeSkill("SK_A1_연속사격", "연속 사격", Role.Archer, AttackType.Strike,
                    CombatState.AerialHit, CombatState.AerialHit, TargetingType.GroundPoint,
                    new List<HitData>
                    {
                        Hit(5f, CombatState.AerialHit, KnockbackMode.Up, airborne: 0.15f, stun: 0.3f),
                        Hit(5f, CombatState.AerialHit, KnockbackMode.Up, airborne: 0.1042f, stun: 0.3f),
                        Hit(5f, CombatState.AerialHit, KnockbackMode.Up, airborne: 0.1042f, stun: 0.3f),
                        Hit(7f, CombatState.AerialHit, KnockbackMode.Up, airborne: 0.0667f, stun: 0.35f),
                    }, null, hitInterval: 0.2f),

                MakeSkill("SK_A2_강력사격", "강력 사격", Role.Archer, AttackType.Push,
                    CombatState.AerialHit, CombatState.Knockback, TargetingType.GroundPoint,
                    new List<HitData> { Hit(18f, CombatState.Knockback, KnockbackMode.AwayFromCaster, push: 2.75f, stun: 0.6f) },
                    null),

                MakeSkill("SK_A3_상승화살", "상승 화살", Role.Archer, AttackType.Launcher,
                    CombatState.LightHit, CombatState.AerialHit, TargetingType.GroundPoint,
                    new List<HitData> { Hit(6f, CombatState.AerialHit, KnockbackMode.Up, airborne: 2.4f, stun: 0.6f) },
                    null, radius: 4f),

                MakeSkill("SK_A4_사출화살", "사출 화살", Role.Archer, AttackType.Launcher,
                    CombatState.LightHit, CombatState.AerialHit, TargetingType.GroundPoint,
                    new List<HitData>(),
                    new List<ISkillEffect> { new AirborneEffect() }),
            };

            // ── 마법사 ────────────────────────────────
            s.Combo[Role.Wizard] = new List<SkillData>
            {
                MakeSkill("SK_M1_중력장", "중력장", Role.Wizard, AttackType.Gather,
                    CombatState.Neutral, CombatState.LightHit, TargetingType.GroundPoint,
                    new List<HitData>(),
                    new List<ISkillEffect> { new PullEffect() }, radius: 5f),

                MakeSkill("SK_M2_융기", "융기", Role.Wizard, AttackType.Launcher,
                    CombatState.LightHit, CombatState.AerialHit, TargetingType.GroundPoint,
                    new List<HitData>(),
                    new List<ISkillEffect> { new AirborneEffect() }, radius: 3.5f),

                MakeSkill("SK_M3_마력파동", "마력 파동", Role.Wizard, AttackType.Push,
                    CombatState.AerialHit, CombatState.Knockback, TargetingType.GroundPoint,
                    new List<HitData> { Hit(14f, CombatState.Knockback, KnockbackMode.AwayFromCaster, push: 2.5f, stun: 0.6f) },
                    null, radius: 4f),

                MakeSkill("SK_M4_흡혈저주", "흡혈 저주", Role.Wizard, AttackType.Strike,
                    CombatState.Neutral, CombatState.LightHit, TargetingType.GroundPoint,
                    new List<HitData> { Hit(10f, CombatState.LightHit, KnockbackMode.Fixed, push: 0.25f) },
                    new List<ISkillEffect> { new LifestealEffect() }),
            };

            return s;
        }

        private static EnemyData CreateEnemyData()
        {
            string path = $"{DataDir}/Enemy_001_고블린.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.enemyId = "Enemy_001";
            data.enemyName = "고블린";
            data.hp = 60f;
            data.atk = 5f;
            data.exp = 10;
            data.gold = 5;

            EditorUtility.SetDirty(data);
            return data;
        }

        // ── 프리팹 ───────────────────────────────────────

        /// <summary>캐릭터 뼈대. 콜라이더는 바닥(y=0) 위로만 올라가도록 오프셋을 준다.</summary>
        private static GameObject BuildCharacterBase(string name, Material bodyMat, float bodyScale = 1f)
        {
            var root = new GameObject(name);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f * bodyScale, 0f);
            body.transform.localScale = Vector3.one * bodyScale;
            Object.DestroyImmediate(body.GetComponent<Collider>());   // 몸통 시각용. 판정은 루트가 갖는다.
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // 정면 표시용 마커. 히트박스 방향을 눈으로 확인하려고 붙인다.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1f * bodyScale, 0.55f * bodyScale);
            nose.transform.localScale = new Vector3(0.2f, 0.2f, 0.4f) * bodyScale;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.freezeRotation = true;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f * bodyScale, 0f);
            capsule.height = 2f * bodyScale;
            capsule.radius = 0.5f * bodyScale;

            var phys = root.AddComponent<PPhysics>();
            SetSerialized(phys, so =>
            {
                so.FindProperty("groundY").floatValue = 0f;
                so.FindProperty("wallMask").intValue = 1 << LayerMask.NameToLayer(WallLayer);
            });

            root.AddComponent<Combat>();
            return root;
        }

        private static Attack AddHitbox(GameObject root, string name, Vector3 localPos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            return go.AddComponent<Attack>();
        }

        private static GameObject BuildPlayerPrefab(Material mat)
        {
            GameObject root = BuildCharacterBase("Player", mat, 1.1f);

            Attack basic = AddHitbox(root, "BasicHitbox", new Vector3(0f, 1.1f, 1.2f), new Vector3(1.6f, 1.6f, 1.6f));
            Attack skill = AddHitbox(root, "SkillHitbox", new Vector3(0f, 1.1f, 1.6f), new Vector3(2.6f, 2.2f, 2.6f));

            var combat = root.GetComponent<Combat>();
            SetSerialized(combat, so => so.FindProperty("maxHealth").floatValue = 150f);

            var player = root.AddComponent<Player>();
            root.AddComponent<Pilotable>();

            SetSerialized(player, so =>
            {
                so.FindProperty("basicAttack").objectReferenceValue = basic;
                so.FindProperty("skillAttack").objectReferenceValue = skill;
                so.FindProperty("party").arraySize = 4;
            });

            return SavePrefab(root, $"{PrefabDir}/Player.prefab");
        }

        private static Dictionary<Role, GameObject> BuildAllyPrefabs(
            Dictionary<string, Material> mats, SkillSet skills)
        {
            var result = new Dictionary<Role, GameObject>();

            foreach (Role role in new[] { Role.Tanker, Role.Warrior, Role.Archer, Role.Wizard })
            {
                GameObject root = BuildCharacterBase($"Ally_{role}", mats[role.ToString()]);

                Attack basic = AddHitbox(root, "BasicHitbox", new Vector3(0f, 1f, 1.1f), new Vector3(1.4f, 1.5f, 1.5f));
                Attack skill = AddHitbox(root, "SkillHitbox", new Vector3(0f, 1f, 1.5f), new Vector3(2.4f, 2f, 2.4f));

                SetSerialized(root.GetComponent<Combat>(), so => so.FindProperty("maxHealth").floatValue = 100f);

                var ally = root.AddComponent<Ally>();

                // 태그 몸은 Control을 둘 다 달고 있고 TagSwapController가 하나를 고른다.
                // 조종사는 씬에 하나뿐이고 몸 밖에 있다. 몸에는 "몰 수 있다"는 표식만 붙인다.
                root.AddComponent<Pilotable>();

                List<SkillData> combo = skills.Combo[role];
                SetSerialized(ally, so =>
                {
                    so.FindProperty("basicAttack").objectReferenceValue = basic;
                    so.FindProperty("skillAttack").objectReferenceValue = skill;
                    so.FindProperty("role").enumValueIndex = (int)role;

                    SerializedProperty equipped = so.FindProperty("equipped");
                    equipped.arraySize = combo.Count;
                    for (int i = 0; i < combo.Count; i++)
                    {
                        SerializedProperty card = equipped.GetArrayElementAtIndex(i);
                        card.FindPropertyRelative("data").objectReferenceValue = combo[i];

                        // 시동기(띄우기)는 패 꼬임 방지로 드로우 가중치를 올린다.
                        card.FindPropertyRelative("drawWeight").floatValue =
                            combo[i].attackType == AttackType.Launcher ? 2.5f : 1f;
                    }
                });

                result[role] = SavePrefab(root, $"{PrefabDir}/Ally_{role}.prefab");
            }

            return result;
        }

        private static GameObject BuildEnemyPrefab(Material mat, EnemyData data)
        {
            GameObject root = BuildCharacterBase("Enemy_고블린", mat, 0.9f);

            Attack basic = AddHitbox(root, "BasicHitbox", new Vector3(0f, 0.9f, 1f), new Vector3(1.3f, 1.4f, 1.4f));

            SetSerialized(root.GetComponent<Combat>(), so => so.FindProperty("maxHealth").floatValue = 60f);

            var enemy = root.AddComponent<Enemy>();
            root.AddComponent<EnemyControl>();

            SetSerialized(enemy, so =>
            {
                so.FindProperty("basicAttack").objectReferenceValue = basic;
                so.FindProperty("data").objectReferenceValue = data;
            });

            return SavePrefab(root, $"{PrefabDir}/Enemy_고블린.prefab");
        }

        // ── 씬 ───────────────────────────────────────────

        private static void BuildScene(
            GameObject playerPrefab, Dictionary<Role, GameObject> allyPrefabs, GameObject enemyPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            Camera cam = BuildCamera();

            // ── 캐릭터 배치 ──
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(-6f, 0f, 0f);

            var allies = new Dictionary<Role, GameObject>();
            Vector3[] allyPos =
            {
                new Vector3(-8f, 0f, 2f),
                new Vector3(-8f, 0f, -2f),
                new Vector3(-10f, 0f, 1f),
                new Vector3(-10f, 0f, -1f),
            };

            int idx = 0;
            foreach (Role role in new[] { Role.Tanker, Role.Warrior, Role.Archer, Role.Wizard })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(allyPrefabs[role]);
                go.transform.position = allyPos[idx++];
                allies[role] = go;
            }

            Vector3[] enemyPos =
            {
                new Vector3(4f, 0f, 0f),
                new Vector3(6f, 0f, 2.5f),
                new Vector3(6f, 0f, -2.5f),
                new Vector3(9f, 0f, 0f),
            };

            for (int i = 0; i < enemyPos.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                go.name = $"Enemy_{i + 1:00}";
                go.transform.position = enemyPos[i];
            }

            // ── 전투 매니저 ──
            var systemGo = new GameObject("BattleSystem");
            var bullet = systemGo.AddComponent<BulletTimeController>();
            var executor = systemGo.AddComponent<ComboExecutor>();
            var radiusProbe = systemGo.AddComponent<EnemyRadiusProbe>();
            var selector = systemGo.AddComponent<TargetSelector>();
            var hud = systemGo.AddComponent<DebugComboHUD>();
            var boardUi = systemGo.AddComponent<ComboBoardUI>();
            systemGo.AddComponent<RecentHitEnemyHUD>();
            systemGo.AddComponent<ComboDamageHUD>();
            systemGo.AddComponent<BattleLogSettings>();

            // 라운드 클리어 후 레벨업 화면. 이 씬에는 라운드 디렉터가 없어 스스로 열리지는 않지만,
            // 붙어 있어야 배선이 두 벌로 갈리지 않는다.
            var levelUp = systemGo.AddComponent<LevelUpSession>();
            SetSerialized(levelUp, so => so.FindProperty("bulletTime").objectReferenceValue = bullet);

            SetSerialized(bullet, so =>
            {
                so.FindProperty("player").objectReferenceValue = player.GetComponent<Player>();
                so.FindProperty("executor").objectReferenceValue = executor;
                so.FindProperty("targetSelector").objectReferenceValue = selector;
            });

            SetSerialized(selector, so =>
            {
                so.FindProperty("cam").objectReferenceValue = cam;
                so.FindProperty("radiusProbe").objectReferenceValue = radiusProbe;
                so.FindProperty("groundY").floatValue = 0f;
            });

            SetSerialized(hud, so =>
            {
                so.FindProperty("bulletTime").objectReferenceValue = bullet;
                so.FindProperty("targetSelector").objectReferenceValue = selector;
                so.FindProperty("player").objectReferenceValue = player.GetComponent<Player>();
            });

            SetSerialized(boardUi, so =>
            {
                so.FindProperty("targetSelector").objectReferenceValue = selector;
            });

            // 플레이어 ↔ 파티 연결. 1~4 키 순서가 이 배열 순서다.
            SetSerialized(player.GetComponent<Player>(), so =>
            {
                so.FindProperty("bulletTime").objectReferenceValue = bullet;

                SerializedProperty party = so.FindProperty("party");
                party.arraySize = 4;
                party.GetArrayElementAtIndex(0).objectReferenceValue = allies[Role.Tanker].GetComponent<Ally>();
                party.GetArrayElementAtIndex(1).objectReferenceValue = allies[Role.Warrior].GetComponent<Ally>();
                party.GetArrayElementAtIndex(2).objectReferenceValue = allies[Role.Archer].GetComponent<Ally>();
                party.GetArrayElementAtIndex(3).objectReferenceValue = allies[Role.Wizard].GetComponent<Ally>();
            });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildEnvironment()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/M_Floor.mat");
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/M_Wall.mat");

            // 바닥은 순수 시각물. 콜라이더를 붙이지 않는다 —
            // 높이(Y)는 Physics가 코드로 계산하므로 유니티 물리와 싸우면 안 된다.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor (visual only)";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(36f, 0.1f, 14f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // 벽은 진짜 콜라이더. 넉백 → 벽 바운드 전이를 여기서 확인한다.
            int wallLayer = LayerMask.NameToLayer(WallLayer);
            var walls = new GameObject("Walls");

            (string name, Vector3 pos, Vector3 scale)[] defs =
            {
                ("Wall_Right", new Vector3(17f, 1.5f, 0f), new Vector3(1f, 3f, 14f)),
                ("Wall_Left", new Vector3(-17f, 1.5f, 0f), new Vector3(1f, 3f, 14f)),
                ("Wall_Back", new Vector3(0f, 1.5f, 7f), new Vector3(36f, 3f, 1f)),
                ("Wall_Front", new Vector3(0f, 1.5f, -7f), new Vector3(36f, 3f, 1f)),
            };

            foreach (var d in defs)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = d.name;
                w.transform.SetParent(walls.transform);
                w.transform.position = d.pos;
                w.transform.localScale = d.scale;
                w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

                if (wallLayer >= 0) w.layer = wallLayer;
            }
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // 벨트스크롤을 비스듬히 내려다본다. XZ 평면과 Y 높이가 동시에 보이게.
            go.transform.position = new Vector3(0f, 12f, -18f);
            go.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

            go.AddComponent<AudioListener>();
            return cam;
        }

        // ── 유틸 ─────────────────────────────────────────

        private static void SetSerialized(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
