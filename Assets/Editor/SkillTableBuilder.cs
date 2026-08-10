using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// SkillData 에셋 <b>주변</b> 작업만 한다 — 빈 에셋 생성, 씬 동료에게 장착, 검증.
    ///
    /// 수치를 코드로 굽는 표는 걷어냈다. 밸런스는 인스펙터에서 직접 만지는 쪽이 빠른데,
    /// 표를 두면 손으로 고친 값을 매번 표에 되옮겨야 하고 지운 스킬이 되살아난다.
    /// <b>에셋이 유일한 원본이다.</b>
    /// </summary>
    public static class SkillTableBuilder
    {
        private const string SkillFolder = "Assets/Data/Skills";

        /// <summary>
        /// GroundPoint 스킬의 최소 반경. 이보다 좁으면 좌표를 정확히 찍어도 대부분 헛친다 —
        /// 조준이 좌표 기반으로 바뀐 뒤로는 반경이 곧 관용 오차다.
        /// </summary>
        private const float MinGroundRadius = 3f;

        /// <summary>
        /// 빈 SkillData를 하나 만든다. 표에 없는 스킬을 직접 추가할 때.
        /// 선택한 폴더에 생기고, 곧바로 이름을 고칠 수 있게 선택 상태로 둔다.
        /// </summary>
        [MenuItem("Prototype/스킬 - 빈 에셋 하나 만들기")]
        public static void CreateBlankSkill()
        {
            EnsureFolder(SkillFolder);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{SkillFolder}/SK_New.asset");
            var asset = ScriptableObject.CreateInstance<SkillData>();

            asset.skillName = "새 스킬";
            asset.hitDataList = new List<HitData> { new HitData
            {
                damageData = new DamageData(10f),
                nextState = CombatState.LightHit,
                mode = KnockbackMode.Fixed,
                fixedDir = Vector3.forward,
                hitStunDuration = 0.3f,
            } };

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[SkillTableBuilder] 빈 스킬 생성 → {path}", asset);
        }

        // ── 씬의 동료에게 장착 ────────────────────────────

        /// <summary>
        /// <b>빈 슬롯만</b> 채운다. 이미 꽂혀 있는 카드와 drawWeight는 그대로 둔다.
        /// 장착 구성을 직접 짜는 중에 덮어써 버리면 곤란하다.
        /// </summary>
        [MenuItem("Prototype/장착 - 빈 슬롯만 채우기")]
        public static void EquipParty() => EquipParty(false);

        /// <summary>장착 구성을 전부 버리고 폴더 순서대로 다시 채운다.</summary>
        [MenuItem("Prototype/장착 - 전부 다시 채우기 (수동 구성 삭제)")]
        public static void ReEquipParty()
        {
            bool ok = EditorUtility.DisplayDialog(
                "장착 다시 채우기",
                "동료 4명의 장착 카드를 전부 비우고 폴더 순서대로 다시 채운다.\n\n" +
                "직접 고른 장착 구성과 drawWeight가 사라진다.\n\n계속할까?",
                "다시 채운다", "취소");

            if (ok) EquipParty(true);
        }

        /// <summary>
        /// 빈 슬롯(카드가 null인 자리)만 정리한다. 꽂혀 있는 카드는 그대로.
        /// SerializedProperty로 배열을 늘렸다가 생긴 잔해를 치우는 용도.
        /// </summary>
        [MenuItem("Prototype/장착 - 빈 슬롯 잔해 정리")]
        public static void PruneEmptySlots()
        {
            int removed = 0;

            foreach (Ally ally in Object.FindObjectsByType<Ally>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(ally, "prune");

                for (int i = ally.Equipped.Count - 1; i >= 0; i--)
                {
                    ComboCard c = ally.Equipped[i];
                    if (c != null && c.Data != null) continue;

                    ally.RemoveSkill(c);
                    removed++;
                }

                EditorUtility.SetDirty(ally);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[SkillTableBuilder] 빈 슬롯 {removed}개 제거");
        }

        private static void EquipParty(bool clearFirst)
        {
            SkillData[] skills = LoadSkills();
            if (skills.Length == 0)
            {
                Debug.LogError("[SkillTableBuilder] 스킬 에셋이 없다. '스킬 - 빠진 에셋만 만들기'부터 실행할 것.");
                return;
            }

            Ally[] allies = Object.FindObjectsByType<Ally>(FindObjectsInactive.Include);
            if (allies.Length == 0)
            {
                Debug.LogError("[SkillTableBuilder] 씬에 Ally가 없다.");
                return;
            }

            int total = 0, added = 0;

            foreach (Ally ally in allies)
            {
                Undo.RecordObject(ally, "equip");

                // SerializedProperty 배열 삽입은 ComboCard 같은 참조형에서 빈 원소를 만든다.
                // 런타임 API를 그대로 쓰는 편이 확실하다.
                if (clearFirst)
                    while (ally.Equipped.Count > 0)
                        ally.RemoveSkill(ally.Equipped[ally.Equipped.Count - 1]);

                var already = new HashSet<SkillData>();
                foreach (ComboCard c in ally.Equipped)
                    if (c != null && c.Data != null) already.Add(c.Data);

                foreach (SkillData s in skills)
                {
                    if (ally.Equipped.Count >= Ally.EquipSlots) break;
                    if (s.role != ally.Role || already.Contains(s)) continue;

                    // 시동기가 안 잡히는 패 꼬임을 막는 보정(스킬테이블.md §5).
                    if (!ally.EquipSkill(new ComboCard(s, s.IsStarterType ? 2f : 1f))) continue;

                    already.Add(s);
                    added++;
                }

                // 고유기가 비어 있으면 공격기를 임시로 물려 둔다.
                var so = new SerializedObject(ally);
                SerializedProperty self = so.FindProperty("selfSkill");
                if (self.objectReferenceValue == null)
                {
                    foreach (SkillData s in skills)
                    {
                        if (s.role != ally.Role || s.attackType != AttackType.Strike) continue;
                        self.objectReferenceValue = s;
                        break;
                    }
                    so.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(ally);
                total += ally.Equipped.Count;

                Debug.Log($"[SkillTableBuilder] {ally.name} ({ally.Role}) 장착 {ally.Equipped.Count}/{Ally.EquipSlots}장", ally);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[SkillTableBuilder] 덱 총 {total}장 (목표 {Deck.Size}) — 이번에 추가 {added}장");
        }

        // ── 손으로 만든 스킬 검증 ─────────────────────────

        /// <summary>
        /// 인스펙터에서 직접 잡은 값이 콤보 규칙과 어긋나는지 본다.
        /// 여기서 걸리는 것들은 실행해도 에러가 안 나고 조용히 콤보만 끊긴다.
        /// </summary>
        [MenuItem("Prototype/스킬 - 검증")]
        public static void ValidateSkills()
        {
            SkillData[] skills = LoadSkills();
            if (skills.Length == 0) { Debug.LogError("[검증] 스킬 에셋이 없다."); return; }

            int problems = 0;
            var byRole = new Dictionary<Role, HashSet<AttackType>>();

            foreach (SkillData s in skills)
            {
                if (!byRole.TryGetValue(s.role, out var types))
                    byRole[s.role] = types = new HashSet<AttackType>();
                types.Add(s.attackType);

                string tag = $"{s.name} ({s.skillName})";

                if (string.IsNullOrWhiteSpace(s.skillName))
                    problems += Warn(s, $"{tag}: skillName이 비어 있다. HUD와 로그에 '?'로 뜬다.");

                if (s.hitDataList == null || s.hitDataList.Count == 0)
                {
                    problems += Warn(s, $"{tag}: hitDataList가 비었다. ComboPredictor가 상태 전이를 못 읽어 " +
                                        "슬롯이 항상 '기본'으로 뜨고 실제 판정도 안 나간다.");
                    continue;
                }

                // 선행 상태에서 이 스킬을 돌리면 실제로 무엇이 나오는지 — 실전투와 같은 규칙.
                CombatState sim = s.requireState;
                for (int h = 0; h < s.hitDataList.Count; h++)
                {
                    HitData hit = s.hitDataList[h];
                    sim = CombatStateRules.Next(sim, in hit, h);
                }

                // 밀치기가 WallBound를 선언하는 건 정상이다 — 벽에 닿아야 만들어진다.
                bool pushToWall = s.attackType == AttackType.Push
                               && s.resultState == CombatState.WallBound
                               && sim == CombatState.Knockback;

                if (sim != s.resultState && !pushToWall)
                    problems += Warn(s, $"{tag}: resultState는 {s.resultState}인데 HitData를 돌리면 {sim}이 나온다. " +
                                        "예측 표시와 실제 결과가 어긋난다.");

                switch (s.attackType)
                {
                    case AttackType.Launcher:
                        if (s.hitDataList[0].mode != KnockbackMode.Up || s.hitDataList[0].launchForce <= 0f)
                            problems += Warn(s, $"{tag}: 띄우기인데 mode가 Up이 아니거나 launchForce가 0이다. 안 뜬다.");
                        break;

                    case AttackType.Push:
                        if (s.hitDataList[0].nextState == CombatState.WallBound)
                            problems += Warn(s, $"{tag}: HitData.nextState가 WallBound다. " +
                                                "벽바운드는 Knockback으로 날아가 벽에 닿아야 생긴다(OnWallContact). " +
                                                "여기는 Knockback으로 둘 것.");
                        if (s.hitDataList[0].knockbackForce <= 0f)
                            problems += Warn(s, $"{tag}: 밀치기인데 knockbackForce가 0이다. 벽까지 못 간다.");
                        break;

                    case AttackType.Gather:
                        bool hasPull = s.effects != null && s.effects.Exists(e => e is PullEffect);
                        if (!hasPull)
                            problems += Warn(s, $"{tag}: 모으기인데 PullEffect가 없다. 히트박스에 닿은 하나만 끌려온다.");
                        break;
                }

                if (s.targeting == TargetingType.EnemyUnit)
                    problems += Warn(s, $"{tag}: EnemyUnit 조준은 사용 중단됐다. GroundPoint로 바꿔라.");

                if (s.targeting == TargetingType.GroundPoint && s.radius < MinGroundRadius)
                    problems += Warn(s, $"{tag}: GroundPoint인데 radius가 {s.radius:0.##}다. 최소 {MinGroundRadius:0.##} 이상이어야 한다.");
            }

            // 정석 4슬롯 체인이 직업별로 성립하는지.
            AttackType[] chain = { AttackType.Gather, AttackType.Launcher, AttackType.Strike, AttackType.Push };
            foreach (var kv in byRole)
                foreach (AttackType need in chain)
                    if (!kv.Value.Contains(need))
                        problems += Warn(null, $"[{kv.Key}] {need} 스킬이 없다. 이 직업만으로는 정석 체인이 안 된다.");

            if (skills.Length != Deck.Size)
                Debug.Log($"[검증] 스킬 {skills.Length}장 (덱 목표 {Deck.Size}장)");

            if (problems == 0)
                Debug.Log($"<b>[검증] 통과</b> — 스킬 {skills.Length}장, 문제 없음");
            else
                Debug.LogWarning($"<b>[검증] 문제 {problems}건</b> — 위 경고 확인");
        }

        private static int Warn(Object ctx, string message)
        {
            Debug.LogWarning("[검증] " + message, ctx);
            return 1;
        }

        /// <summary>빈 슬롯을 채운 뒤 검증까지. 전부 비파괴다.</summary>
        [MenuItem("Prototype/장착 - 빈 슬롯 채우고 검증")]
        public static void EquipAndValidate()
        {
            EquipParty(false);
            ValidateSkills();
        }

        private static SkillData[] LoadSkills()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillData", new[] { SkillFolder });
            var list = new List<SkillData>(guids.Length);

            foreach (string g in guids)
            {
                var s = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g));
                if (s != null) list.Add(s);
            }

            return list.ToArray();
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
