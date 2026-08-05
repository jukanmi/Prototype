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

        [MenuItem("Prototype/스킬 에셋 16장 굽기")]
        public static void BuildSkills()
        {
            EnsureFolder(SkillFolder);

            int created = 0, updated = 0;

            foreach (Row row in Table)
            {
                string path = $"{SkillFolder}/SK_{row.id}.asset";
                SkillData asset = AssetDatabase.LoadAssetAtPath<SkillData>(path);

                bool isNew = asset == null;
                if (isNew)
                {
                    asset = ScriptableObject.CreateInstance<SkillData>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                Fill(asset, row);
                EditorUtility.SetDirty(asset);

                if (isNew) created++; else updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkillTableBuilder] 스킬 에셋 {Table.Length}장 완료 — 신규 {created} / 갱신 {updated} → {SkillFolder}");
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

        [MenuItem("Prototype/동료 4명에게 카드 장착 (덱 16장)")]
        public static void EquipParty()
        {
            SkillData[] skills = LoadSkills();
            if (skills.Length == 0)
            {
                Debug.LogError("[SkillTableBuilder] 스킬 에셋이 없다. '스킬 에셋 16장 굽기'부터 실행할 것.");
                return;
            }

            Ally[] allies = Object.FindObjectsByType<Ally>(FindObjectsInactive.Include);
            if (allies.Length == 0)
            {
                Debug.LogError("[SkillTableBuilder] 씬에 Ally가 없다.");
                return;
            }

            int total = 0;

            foreach (Ally ally in allies)
            {
                var so = new SerializedObject(ally);
                SerializedProperty equipped = so.FindProperty("equipped");
                equipped.ClearArray();

                int i = 0;
                foreach (SkillData s in skills)
                {
                    if (s.role != ally.Role) continue;
                    if (i >= Ally.EquipSlots) break;

                    equipped.InsertArrayElementAtIndex(i);
                    SerializedProperty card = equipped.GetArrayElementAtIndex(i);
                    card.FindPropertyRelative("data").objectReferenceValue = s;
                    // 시동기가 안 잡히는 패 꼬임을 막는 보정(스킬테이블.md §5).
                    card.FindPropertyRelative("drawWeight").floatValue = s.IsStarterType ? 2f : 1f;
                    i++;
                }

                // 고유기가 비어 있으면 공격기를 임시로 물려 둔다.
                SerializedProperty self = so.FindProperty("selfSkill");
                if (self.objectReferenceValue == null)
                {
                    foreach (SkillData s in skills)
                    {
                        if (s.role != ally.Role || s.attackType != AttackType.Strike) continue;
                        self.objectReferenceValue = s;
                        break;
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ally);
                total += i;

                Debug.Log($"[SkillTableBuilder] {ally.name} ({ally.Role}) 장착 {i}장", ally);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[SkillTableBuilder] 덱 총 {total}장 (목표 {Deck.Size})");
        }

        [MenuItem("Prototype/스킬 굽기 + 장착 한번에")]
        public static void BuildAndEquip()
        {
            BuildSkills();
            EquipParty();
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
