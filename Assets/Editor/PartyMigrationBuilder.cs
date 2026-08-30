using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 파티를 씬에서 프리팹으로 옮기는 1회성 도구.
    ///
    /// <b>왜 도구인가.</b> 옮길 값이 동료 하나당 51개, 씬 아홉 개에 흩어져 약 1,800개다.
    /// 손으로 옮기면 어긋난 한 곳이 "그 스테이지만 마법사 카드가 다르다"로만 드러나고,
    /// 그건 게임을 끝까지 돌려 봐야 알 수 있는 종류의 오류다.
    ///
    /// 두 단계로 나눈다:
    /// <list type="number">
    /// <item><b>추출</b> — 씬 하나를 읽어 <see cref="PartyMemberData"/> 넷과
    /// <see cref="PartyLoadout"/> 하나를 굽는다. 사람이 확인할 수 있는 형태로 먼저 만든다.</item>
    /// <item><b>마이그레이션</b> — 씬마다 파티를 걷어내고 <see cref="PartySpawnPoint"/>를 남긴다.</item>
    /// </list>
    /// 1단계 결과를 눈으로 대조한 뒤에 2단계를 돌릴 것. 순서를 붙이면 되돌릴 수 없다.
    /// </summary>
    public static class PartyMigrationBuilder
    {
        private const string DataFolder = "Assets/Data/Resources/" + PartyCatalog.ResourceFolder;
        private const string LoadoutPath = DataFolder + "/" + PartyCatalog.DefaultLoadoutName + ".asset";
        private const string AllyPrefabPath = "Assets/Prefabs/Ally.prefab";

        /// <summary>표를 뽑아낼 기준 씬. 아홉 개가 같은 값을 들고 있으므로 하나면 된다.</summary>
        private const string SourceScene = "Assets/Scenes/Level/Stage_01.unity";

        /// <summary>
        /// 파티를 걷어낼 씬들.
        ///
        /// <c>Skill_test</c>는 <b>일부러 뺐다</b> — 동료가 한 명뿐인 스킬 실험 씬이라
        /// 파티 구조와 무관하고, 여기 넣으면 그 씬의 실험 대상이 통째로 사라진다.
        /// </summary>
        private static readonly string[] TargetScenes =
        {
            "Assets/Scenes/Level/Stage_01.unity",
            "Assets/Scenes/Level/Stage_02.unity",
            "Assets/Scenes/Level/Stage_03.unity",
            "Assets/Scenes/Level/Stage_04.unity",
            "Assets/Scenes/Level/Stage_05.unity",
            "Assets/Scenes/Level/Stage_Boss.unity",
            "Assets/Scenes/Level/Stage_Mini.unity",
            "Assets/Scenes/Level/Stage_Training.unity",
            "Assets/Scenes/SampleScene.unity",
        };

        // ── 1단계 : 추출 ─────────────────────────────────

        [MenuItem("Prototype/파티 - 1단계: 씬에서 표 추출", priority = 40)]
        public static void Extract()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[PartyMigration] {SourceScene} 를 못 열었다.");
                return;
            }

            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(AllyPrefabPath);
            Ally prefabAlly = reference != null ? reference.GetComponent<Ally>() : null;

            // 호스트 프리팹 안이 아니라 씬에 직접 놓인 쪽을 읽는다. 프리팹을 먼저 구운 뒤에
            // 추출을 돌리면 둘 다 있고, 프리팹 쪽은 아직 표가 안 꽂혀 빈 값이다.
            Player player = FindLoose(Object.FindObjectsByType<Player>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));

            if (player == null)
            {
                Debug.LogError("[PartyMigration] 씬에 (호스트 프리팹 밖의) Player 가 없다. " +
                               "이미 마이그레이션된 씬이라면 추출할 것이 없다.");
                return;
            }

            EnsureFolder(DataFolder);

            var loadout = LoadOrCreate<PartyLoadout>(LoadoutPath);
            loadout.loadoutName = "기본 파티";
            loadout.members = new PartyMemberData[PartyLoadout.MaxMembers];

            var report = new List<string>();

            for (int i = 0; i < player.Party.Length && i < PartyLoadout.MaxMembers; i++)
            {
                Ally ally = player.Party[i];
                if (ally == null) continue;

                string path = $"{DataFolder}/Party_{ally.name}.asset";
                var data = LoadOrCreate<PartyMemberData>(path);

                Capture(data, ally, prefabAlly, report);

                EditorUtility.SetDirty(data);
                loadout.members[i] = data;
            }

            EditorUtility.SetDirty(loadout);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PartyMigration] 표 {loadout.FilledCount}개 + 로드아웃을 {DataFolder} 에 구웠다.\n" +
                      string.Join("\n", report), loadout);

            WarnAboutDroppedFields();
        }

        /// <summary>
        /// 씬 인스턴스 하나를 표로 옮긴다.
        ///
        /// <b>프리팹과 같은 값은 안 적는다.</b> 표의 빈 칸은 "프리팹 값을 그대로 둔다"는 뜻이고
        /// (<see cref="Ally.ApplyData"/>), 같은 값을 굳이 복사해 두면 나중에 프리팹을 고쳐도
        /// 표가 옛 값으로 되돌려 버린다.
        /// </summary>
        private static void Capture(PartyMemberData data, Ally ally, Ally prefabAlly, List<string> report)
        {
            data.memberId = ally.name;
            data.displayName = ally.name;
            data.role = ally.Role;
            data.portrait = ally.Portrait;

            data.equipped = new List<ComboCard>();
            foreach (ComboCard c in ally.Equipped)
                if (c != null) data.equipped.Add(c.Clone());

            // 원거리 평타 — 근접 동료는 비운 채로 둔다.
            data.basicProjectile = ally.BasicIsRanged ? GetProjectile(ally) : null;
            if (data.basicProjectile != null)
            {
                data.projectileSpeed = ally.BasicProjectileSpeed;
                data.projectileRange = ally.BasicProjectileRange;
                data.projectilePierce = ally.BasicProjectilePierce;
            }

            // 몸 크기 — Shadow 의 z 배율이 곧 그 값이다(프리팹 기본이 1).
            Transform shadow = ally.transform.Find("Shadow");
            data.bodyScale = shadow != null ? Mathf.Round(shadow.localScale.z * 1000f) / 1000f : 1f;

            // 직업 색 — ArtImportBuilder 가 씬 인스턴스에 칠하던 값. 알파는 안 가져온다
            // (사망 연출이 굴리는 값이라 표에 굳히면 안 된다).
            Transform sprite = ally.transform.Find(PartyMemberData.SpritePath);
            var sr = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;
            data.spriteTint = sr != null ? new Color(sr.color.r, sr.color.g, sr.color.b, 1f) : Color.white;

            // 애니메이터는 프리팹과 다를 때만 적는다. 지금은 넷 다 같은 컨트롤러를 쓴다.
            var animator = ally.GetComponentInChildren<Animator>(true);
            var prefabAnimator = prefabAlly != null ? prefabAlly.GetComponentInChildren<Animator>(true) : null;

            data.animatorController =
                animator != null && prefabAnimator != null &&
                animator.runtimeAnimatorController != prefabAnimator.runtimeAnimatorController
                    ? animator.runtimeAnimatorController
                    : null;

            // 체력 · 공격력 · 이동속도는 씬에서 안 건드리고 있었다. 0으로 둬 프리팹 값을 살린다.
            data.hp = 0f;
            data.atk = 0f;
            data.moveSpeed = 0f;

            string valid = PartyAssembleRules.IsValid(data, out string reason) ? "OK" : $"⚠ {reason}";
            report.Add($"  {data.memberId,-6} {data.role,-8} 카드 {data.equipped.Count}장 " +
                       $"크기 {data.bodyScale:0.##} " +
                       $"{(data.basicProjectile != null ? "원거리" : "근접")}  {valid}");
        }

        /// <summary>
        /// <see cref="Entity"/>가 투사체 프리팹을 밖으로 안 열어 준다. 표를 굽는 1회성 경로라
        /// 여기서만 직렬화 필드를 직접 읽는다 — 런타임 코드에는 넣지 않는다.
        /// </summary>
        private static Projectile GetProjectile(Ally ally)
        {
            var so = new SerializedObject(ally);
            return so.FindProperty("basicProjectile").objectReferenceValue as Projectile;
        }

        /// <summary>
        /// 옮기지 않고 <b>버리는</b> 값을 알린다. 조용히 사라지면 나중에
        /// "원래 뭐가 있었는데" 가 되고, 그때는 씬 히스토리를 뒤져야 한다.
        /// </summary>
        private static void WarnAboutDroppedFields()
        {
            Debug.LogWarning(
                "[PartyMigration] 다음 씬 오버라이드는 표로 옮기지 않는다:\n" +
                "  · selfSkill  — 스크립트에 존재하지 않는 죽은 필드다(대표 스킬의 잔해). " +
                "값은 언제나 equipped 안의 카드와 같았으므로 잃는 정보가 없다.\n" +
                "  · m_SortingOrder — 씬에서 동료 넷이 나란히 서 있던 시절의 겹침 정리값이다. " +
                "파티가 자리를 하나만 쓰는 지금은 BeltScrollView 가 깊이로 매 프레임 덮는다.\n" +
                "  · animator(=null) — EntityAnimator 의 배선을 일부러 끊어 자동탐색에 맡긴 흔적. " +
                "Ally 프리팹에서 제대로 꽂아 둘 것.\n" +
                "  · drawWeight — 표에 그대로 보존했다. 다만 ComboCard.DrawWeight 는 " +
                "지금 어떤 드로우 경로도 읽지 않는다(덱 맨 위에서 순서대로 가져간다).");
        }

        // ── 2단계 : 씬 마이그레이션 ──────────────────────

        [MenuItem("Prototype/파티 - 2단계: 씬 마이그레이션", priority = 41)]
        public static void MigrateScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<PartyLoadout>(LoadoutPath) == null)
            {
                Debug.LogError("[PartyMigration] 1단계(표 추출)를 먼저 돌릴 것. " +
                               $"{LoadoutPath} 가 없다.");
                return;
            }

            var report = new List<string>();

            foreach (string path in TargetScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    report.Add($"  {Path.GetFileName(path),-22} ✗ 열지 못했다");
                    continue;
                }

                report.Add("  " + Migrate(scene));
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PartyMigration] 씬 마이그레이션 완료\n" + string.Join("\n", report));

            Debug.LogWarning(
                "[PartyMigration] Skill_test 는 자동 대상이 아니다 — 손으로 정할 것.\n" +
                "  그 씬은 동료 한 명과 고정 손패 3장으로 스킬을 실험하는 자리라 파티 규약을 따르지 않는다.\n" +
                "  그런데 BattleInput 프리팹을 다시 구웠으므로 그 씬의 호스트 인스턴스에도 " +
                "파티 5명과 CombatManager 가 따라 들어간다 — 그대로 두면 덱이 두 벌 돌고 동료가 여섯 명이 된다.\n" +
                "  둘 중 하나를 고를 것:\n" +
                "   (A) 이 목록에 Skill_test 를 추가해 함께 마이그레이션한다. " +
                "고정 손패는 CarryFixedHand 가 옮기고, 대신 기본 파티 4명이 선다.\n" +
                "   (B) 씬에 놓인 Player · Ally · CombatManager 를 남기고 " +
                "호스트 안의 Party · CombatManager 를 그 씬에서만 비활성화한다.");
        }

        private static string Migrate(Scene scene)
        {
            string name = scene.name;

            // 자리부터 기록한다. 몸을 지운 뒤에는 물어볼 곳이 없다.
            Player player = FindLoose(Object.FindObjectsByType<Player>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));

            Vector3 spawn = player != null
                ? new Vector3(player.transform.position.x, 0f, player.transform.position.z)
                : PartySpawnPoint.Fallback;

            int removed = 0;

            // 순서 주의 — 동료를 먼저 지운다. Player 를 먼저 지우면 Party 배열을 잃는다.
            foreach (Ally a in Object.FindObjectsByType<Ally>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (IsInsideHost(a)) continue;
                Object.DestroyImmediate(a.gameObject);
                removed++;
            }

            if (player != null) { Object.DestroyImmediate(player.gameObject); removed++; }

            // CombatManager 는 BattleInput 안으로 들어갔다. 씬에 남으면 덱이 두 벌 돈다 —
            // 두 BulletTimeController 가 같은 RunProgression 에 씨를 뿌리고 서로를 덮는다.
            var host = Object.FindAnyObjectByType<PartyAssembler>();
            BulletTimeController hostDeck = host != null
                ? host.GetComponentInChildren<BulletTimeController>(true) : null;

            string carried = "";

            foreach (BulletTimeController b in Object.FindObjectsByType<BulletTimeController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (IsInsideHost(b)) continue;

                if (CarryFixedHand(b, hostDeck)) carried = " · 고정손패 이관";

                Object.DestroyImmediate(b.gameObject);
                removed++;
            }

            string spawnNote = EnsureSpawnPoint(scene, spawn);
            string hostNote = EnsureHost(scene);

            return $"{name,-22} 제거 {removed}개 · {spawnNote} · {hostNote}{carried}";
        }

        /// <summary>
        /// <b>고정 손패</b>를 씬의 덱에서 호스트의 덱으로 옮긴다.
        ///
        /// 이건 씬마다 다른 <b>의도된 저작값</b>이다 — 훈련장 · 시연 씬은 콤보 한 싸이클을
        /// 매번 똑같이 굴려야 값을 비교할 수 있어서 무작위 드로우를 끈다.
        /// 그냥 지우면 그 씬은 조용히 일반 덱으로 돌아가고, 같은 체인이 두 번 안 나오게 된다.
        ///
        /// 나머지 인스펙터 값은 옮기지 않는다 — 씬마다 다른 게 정상인 값은 이것뿐이고,
        /// 전부 복사하면 어떤 스테이지의 튜닝이 프리팹 오버라이드로 굳는다.
        /// </summary>
        private static bool CarryFixedHand(BulletTimeController from, BulletTimeController to)
        {
            if (from == null || to == null) return false;

            var src = new SerializedObject(from);
            if (!src.FindProperty("useFixedHand").boolValue) return false;

            SerializedProperty srcHand = src.FindProperty("fixedHand");

            var dst = new SerializedObject(to);
            dst.FindProperty("useFixedHand").boolValue = true;

            SerializedProperty dstHand = dst.FindProperty("fixedHand");
            dstHand.arraySize = srcHand.arraySize;
            for (int i = 0; i < srcHand.arraySize; i++)
                dstHand.GetArrayElementAtIndex(i).objectReferenceValue =
                    srcHand.GetArrayElementAtIndex(i).objectReferenceValue;

            dst.ApplyModifiedProperties();
            EditorUtility.SetDirty(to);

            return true;
        }

        /// <summary>
        /// 이 컴포넌트가 <b>BattleInput 프리팹 안</b>에 있는가.
        ///
        /// 이 씬을 두 번 돌려도 안전해야 한다. 프리팹 인스턴스가 이미 들어와 있는 씬에서
        /// 무턱대고 <c>Ally</c>를 전부 지우면 <b>방금 넣은 파티를 도로 지운다</b> —
        /// 그러고도 씬은 멀쩡해 보이고, 증상은 다음 Play 에서 "아무도 안 나온다"로만 나온다.
        /// </summary>
        private static bool IsInsideHost(Component c)
            => c != null && c.GetComponentInParent<PartyAssembler>(true) != null;

        /// <summary>호스트 프리팹 밖에 놓인 첫 인스턴스. 없으면 null.</summary>
        private static T FindLoose<T>(T[] all) where T : Component
        {
            foreach (T c in all)
                if (!IsInsideHost(c)) return c;

            return null;
        }

        private static string EnsureSpawnPoint(Scene scene, Vector3 ground)
        {
            PartySpawnPoint existing = Object.FindAnyObjectByType<PartySpawnPoint>();
            if (existing != null)
            {
                existing.transform.position = ground;
                return $"자리 갱신 {ground.x:0.#}";
            }

            var go = new GameObject("PartySpawnPoint");
            go.transform.position = ground;
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PartySpawnPoint>();

            return $"자리 생성 {ground.x:0.#}";
        }

        /// <summary>
        /// <c>BattleInput</c> 인스턴스가 있는지 보고, 있으면 원점으로 되돌린다.
        ///
        /// 이 프리팹은 이미 아홉 씬에 들어가 있으므로 보통 "확인만" 하고 끝난다.
        /// 프리팹 자산의 GUID 가 그대로라 <c>BattleInputBuilder</c>가 다시 구운 새 계층
        /// (파티 · CombatManager)이 씬 인스턴스에 자동으로 따라 들어온다.
        /// </summary>
        private static string EnsureHost(Scene scene)
        {
            var assembler = Object.FindAnyObjectByType<PartyAssembler>();
            if (assembler == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleInputBuilder.PrefabPath);
                if (prefab == null) return "호스트 없음 ✗ (프리팹을 먼저 구울 것)";

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return "호스트 추가";
            }

            Transform t = assembler.transform;
            bool moved = t.position != Vector3.zero
                      || t.rotation != Quaternion.identity
                      || t.localScale != Vector3.one;

            if (!moved) return "호스트 OK";

            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
            return "호스트 원점 복구";
        }

        // ── 유틸 ────────────────────────────────────────

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
