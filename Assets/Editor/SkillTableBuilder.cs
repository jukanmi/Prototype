using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 스킬테이블.md 24종 중 <b>정석 4슬롯 체인</b>에 필요한 16종을 SkillData 에셋으로 굽는다.
    /// 직업당 모으기 · 띄우기 · 공격기 · 밀치기 1장씩 → 어느 직업 조합으로도 체인이 성립한다.
    /// 몇 번을 돌려도 같은 결과가 나온다(이름이 같으면 덮어쓴다).
    /// </summary>
    public static class SkillTableBuilder
    {
        private const string SkillFolder = "Assets/Data/Skills";

        /// <summary>한 줄이 스킬 하나. 수치는 전부 프로토타입 임시값이다(스킬테이블.md §7 미결정).</summary>
        private struct Row
        {
            public string id;
            public string skillName;
            public Role role;
            public AttackType type;
            public CombatState require;
            public CombatState result;
            public TargetingType targeting;
            public float radius;
            public float castTime;
            public float recovery;
            public int hits;
            public string note;
        }

        private static readonly Row[] Table =
        {
            // ── 탱커 ─────────────────────────────────────
            Make("TK01", "사슬 견인",      Role.Tanker,  AttackType.Gather,   TargetingType.GroundPoint, 4.5f, 0.25f, 0.25f, 1, "텔포 후 자기 주변 흡입"),
            Make("TK02", "방패 올려치기",  Role.Tanker,  AttackType.Launcher, TargetingType.None,        2.5f, 0.20f, 0.25f, 1, "시동기"),
            Make("TK04", "사슬 감아치기",  Role.Tanker,  AttackType.Strike,   TargetingType.Direction,   3.0f, 0.15f, 0.20f, 3, "다단 3히트"),
            Make("TK05", "대지 강타",      Role.Tanker,  AttackType.Push,     TargetingType.None,        3.0f, 0.30f, 0.35f, 1, "스파이크 — 현재는 일반 밀치기로 동작"),

            // ── 전사 ─────────────────────────────────────
            Make("WR01", "소용돌이 베기",  Role.Warrior, AttackType.Gather,   TargetingType.None,        3.5f, 0.20f, 0.25f, 1, "PDF SK-01"),
            Make("WR02", "올려베기",       Role.Warrior, AttackType.Launcher, TargetingType.None,        2.5f, 0.15f, 0.20f, 1, "PDF SK-02 · 시동기"),
            Make("WR03", "돌진 베기",      Role.Warrior, AttackType.Strike,   TargetingType.Direction,   3.0f, 0.15f, 0.25f, 1, "PDF SK-03 · 이동 포함"),
            Make("WR05", "회전 강타",      Role.Warrior, AttackType.Push,     TargetingType.Direction,   3.0f, 0.20f, 0.30f, 1, ""),

            // ── 궁수 ─────────────────────────────────────
            Make("AR03", "그물 사격",      Role.Archer,  AttackType.Gather,   TargetingType.GroundPoint, 4.0f, 0.30f, 0.20f, 1, "원격 흡입"),
            Make("AR04", "상승 화살",      Role.Archer,  AttackType.Launcher, TargetingType.EnemyUnit,   2.0f, 0.25f, 0.20f, 1, "시동기"),
            Make("AR01", "연속 사격",      Role.Archer,  AttackType.Strike,   TargetingType.EnemyUnit,   2.0f, 0.15f, 0.20f, 3, "PDF SK-04 · 다단"),
            Make("AR02", "강력 사격",      Role.Archer,  AttackType.Push,     TargetingType.EnemyUnit,   2.0f, 0.35f, 0.30f, 1, "PDF SK-05"),

            // ── 마법사 ───────────────────────────────────
            Make("WZ01", "중력장",         Role.Wizard,  AttackType.Gather,   TargetingType.GroundPoint, 5.0f, 0.35f, 0.25f, 1, "지정 지점 흡입"),
            Make("WZ02", "융기",           Role.Wizard,  AttackType.Launcher, TargetingType.GroundPoint, 3.0f, 0.30f, 0.25f, 1, "시동기 · 지면 솟음"),
            Make("WZ03", "마력탄 연사",    Role.Wizard,  AttackType.Strike,   TargetingType.EnemyUnit,   2.5f, 0.15f, 0.20f, 3, "다단"),
            Make("WZ05", "충격파",         Role.Wizard,  AttackType.Push,     TargetingType.Direction,   3.5f, 0.25f, 0.30f, 1, ""),
        };

        /// <summary>
        /// 선행 · 결과 상태는 공격 유형에서 기계적으로 나온다.
        /// 산개=Neutral · 밀집=LightHit · 공중=AerialHit · 벽바운드=WallBound.
        /// </summary>
        private static Row Make(string id, string name, Role role, AttackType type,
                                TargetingType targeting, float radius,
                                float castTime, float recovery, int hits, string note)
        {
            CombatState require, result;
            switch (type)
            {
                case AttackType.Gather:
                    require = CombatState.Neutral; result = CombatState.LightHit; break;
                case AttackType.Launcher:
                    require = CombatState.LightHit; result = CombatState.AerialHit; break;
                case AttackType.Push:
                    require = CombatState.AerialHit; result = CombatState.WallBound; break;
                default: // Strike — 공중 유지. 돌진 계열은 지상에서도 나가야 하므로 조건 없음.
                    require = CombatState.Neutral; result = CombatState.AerialHit; break;
            }

            return new Row
            {
                id = id, skillName = name, role = role, type = type,
                require = require, result = result,
                targeting = targeting, radius = radius,
                castTime = castTime, recovery = recovery, hits = hits, note = note,
            };
        }

        /// <summary>
        /// <b>폐기됨.</b> 초기 16장 프로토타입 표다. 지금 기준은 기획표(<see cref="ReferenceSkillBuilder"/>)이고,
        /// 이걸 돌리면 의도적으로 지운 옛 에셋이 되살아난다. 참고용으로만 남긴다.
        /// </summary>
        private static void BuildSkills(bool overwriteExisting)
        {
            EnsureFolder(SkillFolder);

            int created = 0, overwritten = 0, kept = 0;

            foreach (Row row in Table)
            {
                string path = $"{SkillFolder}/SK_{row.id}.asset";
                SkillData asset = AssetDatabase.LoadAssetAtPath<SkillData>(path);

                // 이미 있는 것은 손으로 만든 것으로 본다. 건드리지 않는다.
                if (asset != null && !overwriteExisting)
                {
                    kept++;
                    continue;
                }

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<SkillData>();
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else
                {
                    overwritten++;
                }

                Fill(asset, row);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkillTableBuilder] 신규 {created} / 덮어씀 {overwritten} / 유지 {kept} → {SkillFolder}");
        }

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

        private static void Fill(SkillData asset, Row row)
        {
            asset.skillName = row.skillName;
            asset.description = string.IsNullOrEmpty(row.note) ? row.id : $"{row.id} — {row.note}";
            asset.role = row.role;
            asset.attackType = row.type;
            asset.requireState = row.require;
            asset.resultState = row.result;
            asset.targeting = row.targeting;
            asset.radius = row.radius;
            asset.castTime = row.castTime;
            asset.hitInterval = 0.3f;   // 프로토타입 고정값
            asset.recoveryTime = row.recovery;
            asset.cooldown = 5f;
            asset.manaCost = 20f;

            asset.hitDataList = BuildHits(row);
            asset.effects = BuildEffects(row);
        }

        private static List<HitData> BuildHits(Row row)
        {
            var list = new List<HitData>(row.hits);

            for (int i = 0; i < row.hits; i++)
                list.Add(BuildHit(row));

            return list;
        }

        private static HitData BuildHit(Row row)
        {
            var hit = new HitData
            {
                targetState = row.require,
                canOtg = false,
                snapZ = row.type == AttackType.Gather,
            };

            switch (row.type)
            {
                case AttackType.Gather:
                    // 실제 흡입은 PullEffect가 반경으로 처리한다.
                    // 여기 한 줄은 ComboPredictor가 상태 전이를 읽기 위한 것이라 데미지가 0이다.
                    hit.damageData = new DamageData(0f);
                    hit.nextState = CombatState.LightHit;
                    hit.mode = KnockbackMode.TowardCaster;
                    hit.knockbackForce = 0f;
                    hit.hitStunDuration = 0.4f;
                    break;

                case AttackType.Launcher:
                    hit.damageData = new DamageData(8f);
                    hit.nextState = CombatState.AerialHit;
                    hit.mode = KnockbackMode.Up;
                    hit.launchForce = 12f;
                    hit.hitStunDuration = 0.6f;
                    break;

                case AttackType.Push:
                    // 벽바운드는 넉백 후 벽에 닿아야 걸린다(CombatStateRules.OnWallContact).
                    // 따라서 여기서 요청하는 상태는 Knockback이다.
                    hit.damageData = new DamageData(14f);
                    hit.nextState = CombatState.Knockback;
                    hit.mode = KnockbackMode.AwayFromCaster;
                    hit.knockbackForce = 16f;
                    hit.hitStunDuration = 0.5f;
                    break;

                default: // Strike
                    hit.damageData = new DamageData(6f);
                    hit.nextState = CombatState.AerialHit;
                    hit.mode = KnockbackMode.Fixed;
                    hit.fixedDir = Vector3.forward;
                    hit.knockbackForce = 2f;
                    hit.hitStunDuration = 0.25f;
                    break;
            }

            return hit;
        }

        private static List<ISkillEffect> BuildEffects(Row row)
        {
            var list = new List<ISkillEffect>();

            // 모으기만 별도 효과가 필요하다 — 반경 안 다수 대상 + 방향 계산(스킬테이블.md §6-1).
            if (row.type == AttackType.Gather)
                list.Add(new PullEffect());

            // 돌진 계열은 시전자를 밀어 준다.
            if (row.id == "WR03")
                list.Add(new ChargeEffect());

            return list;
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

                if (s.targeting == TargetingType.GroundPoint && s.radius <= 0f)
                    problems += Warn(s, $"{tag}: GroundPoint인데 radius가 0이다. 항상 헛친다.");
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

        /// <summary>기획표로 굽고 빈 슬롯을 채운 뒤 검증까지. 전부 비파괴다.</summary>
        [MenuItem("Prototype/기획표 - 굽고 장착하고 검증")]
        public static void BuildAndEquip()
        {
            ReferenceSkillBuilder.Build();
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
