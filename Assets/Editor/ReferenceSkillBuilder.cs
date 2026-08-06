using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 기획 표(스킬 타입 × 4직업)를 그대로 SkillData 에셋으로 굽는다.
    /// 직업당 6칸(스킬1 기본공격 2 · 스킬2 유틸기 2 · 스킬3 필살기 2), 탱커만 5칸.
    ///
    /// 표에 없는 것은 만들지 않는다. 지속 장판과 채널링은 아직 시스템이 없어
    /// 즉발 · 다단으로 근사했고, 해당 스킬 설명에 그렇게 적어 뒀다.
    ///
    /// <b>이미 있는 에셋은 절대 건드리지 않는다.</b> 손으로 고친 값이 날아가지 않게.
    /// </summary>
    public static class ReferenceSkillBuilder
    {
        private const string Folder = "Assets/Data/Skills";

        // ── 한 스킬을 기술하는 값 ──────────────────────────

        private class Spec
        {
            public string id;
            public string name;
            public string note;          // 참고한 원본 + 근사 여부
            public Role role;
            public AttackType type;
            public CombatState require = CombatState.Neutral;
            public TargetingType aim = TargetingType.None;
            public float radius = 3f;

            public float castTime = 0.2f;
            public float hitInterval = 0.3f;
            public float recovery = 0.25f;

            public List<HitData> hits = new List<HitData>();
            public List<ISkillEffect> effects = new List<ISkillEffect>();

            public bool projectile;
            public float projSpeed = 18f;
            public float projRange = 11f;
            public int projPierce;

            public float maxChargeTime = 2.5f;
            public float chargeDamageMul = 2.5f;
            public float chargeRadiusMul = 1.6f;
        }

        // ── HitData 조립 헬퍼 ─────────────────────────────

        private static HitData Damage(float dmg, float stun = 0.3f) => new HitData
        {
            damageData = new DamageData(dmg),
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            knockbackForce = 2f,
            hitStunDuration = stun,
        };

        private static HitData Gather(float dmg = 4f) => new HitData
        {
            damageData = new DamageData(dmg),
            nextState = CombatState.LightHit,
            mode = KnockbackMode.TowardCaster,
            knockbackForce = 10f,
            hitStunDuration = 0.4f,
            snapZ = true,          // 벨트스크롤에서 Z가 어긋나면 후속타가 전부 빗나간다
        };

        private static HitData Launch(float dmg = 8f, float force = 12f) => new HitData
        {
            damageData = new DamageData(dmg),
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            launchForce = force,
            hitStunDuration = 0.6f,
        };

        /// <summary>밀치기. nextState는 Knockback이다 — 벽바운드는 벽에 닿아야 생긴다.</summary>
        private static HitData Push(float dmg = 14f, float force = 18f) => new HitData
        {
            damageData = new DamageData(dmg),
            nextState = CombatState.Knockback,
            mode = KnockbackMode.AwayFromCaster,
            knockbackForce = force,
            hitStunDuration = 0.5f,
        };

        private static HitData Aerial(float dmg = 6f) => new HitData
        {
            damageData = new DamageData(dmg),
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            knockbackForce = 1.5f,
            hitStunDuration = 0.35f,
        };

        // ── 표 ────────────────────────────────────────────

        private static List<Spec> BuildTable()
        {
            var t = new List<Spec>();

            // ══ 탱커 ══ 스킬3 두 번째 칸은 표에 비어 있어 만들지 않는다.
            // 피해감소 계열(수호 보호막 · 수호 결계)은 있으나 없으나 차이가 없어 표에서 뺐다.
            t.Add(new Spec {
                id = "TK1A", name = "소용돌이 베기", note = "몸을 회전시켜 주변 적을 끌어모은다",
                role = Role.Tanker, type = AttackType.Gather, aim = TargetingType.None, radius = 3.5f,
                castTime = 0.2f, recovery = 0.25f,
                hits = { Gather() },
                effects = { new PullEffect() },
            });
            t.Add(new Spec {
                id = "TK1B", name = "대지 강타", note = "라인하르트 돌진 — 돌진해 땅을 내려찍어 밀어낸다",
                role = Role.Tanker, type = AttackType.Push, aim = TargetingType.Direction,
                castTime = 0.2f, recovery = 0.3f,
                hits = { Push(12f, 20f) },
                effects = { new ChargeEffect() },
            });
            t.Add(new Spec {
                id = "TK2A", name = "사슬 견인", note = "세트 E — 찍은 지점으로 텔포 후 주변 흡입",
                role = Role.Tanker, type = AttackType.Gather, aim = TargetingType.GroundPoint, radius = 4.5f,
                castTime = 0.25f, recovery = 0.25f,
                hits = { Gather() },
                effects = { new PullEffect() },
            });
            t.Add(new Spec {
                id = "TK3A", name = "방패 올려치기", note = "말파이트 R — 돌진 후 광역 에어본",
                role = Role.Tanker, type = AttackType.Launcher, require = CombatState.LightHit,
                aim = TargetingType.GroundPoint, radius = 4f,
                castTime = 0.35f, recovery = 0.35f,
                hits = { Launch(12f, 14f) },
                effects = { new ChargeEffect() },
            });

            // ══ 근거리 딜러 (Warrior) ══
            t.Add(new Spec {
                id = "WR1A", name = "돌진 베기", note = "리븐 E — 짧은 대쉬 + 보호막",
                role = Role.Warrior, type = AttackType.Strike, aim = TargetingType.Direction,
                castTime = 0.12f, recovery = 0.2f,
                hits = { Damage(7f) },
                effects = { new ChargeEffect(), new ShieldEffect() },
            });
            t.Add(new Spec {
                id = "WR1B", name = "올려베기", note = "요네 Q 3타 — 앞 2타는 평타, 3타만 띄운다",
                role = Role.Warrior, type = AttackType.Launcher, require = CombatState.LightHit,
                aim = TargetingType.None,
                castTime = 0.12f, hitInterval = 0.18f, recovery = 0.25f,
                hits = { Damage(5f, 0.2f), Damage(5f, 0.2f), Launch(9f, 13f) },
            });
            t.Add(new Spec {
                id = "WR3A", name = "회전 강타", note = "트리스타나 R — 대상을 밀어낸다. 벽에 닿으면 벽바운드",
                role = Role.Warrior, type = AttackType.Push, require = CombatState.AerialHit,
                aim = TargetingType.EnemyUnit,
                castTime = 0.25f, recovery = 0.3f,
                hits = { Push() },
            });
            t.Add(new Spec {
                id = "WR3B", name = "사슬 감아치기", note = "누누 R — 콤보 끝까지 모았다가 터진다",
                role = Role.Warrior, type = AttackType.Charge, aim = TargetingType.None, radius = 5f,
                castTime = 0.15f, recovery = 0.4f,
                hits = { Launch(14f, 10f) },
                effects = { new PullEffect() },
                maxChargeTime = 3f, chargeDamageMul = 3f, chargeRadiusMul = 2f,
            });

            // ══ 궁수 ══ 전부 투사체.
            t.Add(new Spec {
                id = "AR1A", name = "연속 사격", note = "애쉬 Q — 3연사",
                role = Role.Archer, type = AttackType.Strike, aim = TargetingType.EnemyUnit,
                castTime = 0.15f, hitInterval = 0.15f, recovery = 0.2f,
                hits = { Aerial(5f), Aerial(5f), Aerial(5f) },
                projectile = true, projSpeed = 22f, projRange = 10f,
            });
            t.Add(new Spec {
                id = "AR2A", name = "상승 화살", note = "시동기 — 화살로 쳐올려 띄운다",
                role = Role.Archer, type = AttackType.Launcher, require = CombatState.LightHit,
                aim = TargetingType.GroundPoint, radius = 4f,
                castTime = 0.3f, recovery = 0.25f,
                hits = { Launch(6f, 12f) },
            });
            t.Add(new Spec {
                id = "AR2B", name = "강력 사격", note = "베인 E — 밀어내고 벽에 닿으면 벽바운드",
                role = Role.Archer, type = AttackType.Push, require = CombatState.AerialHit,
                aim = TargetingType.EnemyUnit,
                castTime = 0.3f, recovery = 0.3f,
                hits = { Push(13f, 20f) },
                projectile = true, projSpeed = 20f, projRange = 12f,
            });
            t.Add(new Spec {
                id = "AR3A", name = "그물사격", note = "그물을 쏴 지정 지점의 적을 끌어모은다",
                role = Role.Archer, type = AttackType.Gather, aim = TargetingType.GroundPoint, radius = 4f,
                castTime = 0.3f, recovery = 0.2f,
                hits = { Gather() },
                effects = { new PullEffect() },
            });

            // ══ 마법사 ══
            t.Add(new Spec {
                id = "WZ1A", name = "마력탄 연사", note = "이즈리얼 Q — 기본 스킬샷",
                role = Role.Wizard, type = AttackType.Strike, aim = TargetingType.Direction,
                castTime = 0.15f, recovery = 0.2f,
                hits = { Damage(9f) },
                projectile = true, projSpeed = 24f, projRange = 12f,
            });
            t.Add(new Spec {
                id = "WZ2A", name = "융기", note = "시동기 — 지면을 솟구쳐 띄운다",
                role = Role.Wizard, type = AttackType.Launcher, require = CombatState.LightHit,
                aim = TargetingType.GroundPoint, radius = 4.5f,
                castTime = 0.35f, recovery = 0.3f,
                hits = { Launch(7f, 12f) },
            });
            t.Add(new Spec {
                id = "WZ2B", name = "중력장", note = "흐웨이 EE — 지정 지점으로 끌어모은다",
                role = Role.Wizard, type = AttackType.Gather, aim = TargetingType.GroundPoint, radius = 5f,
                castTime = 0.35f, recovery = 0.25f,
                hits = { Gather(5f) },
                effects = { new PullEffect() },
            });
            t.Add(new Spec {
                id = "WZ3A", name = "충격파", note = "앞으로 충격파를 뿜어 밀어낸다. 벽에 닿으면 벽바운드",
                role = Role.Wizard, type = AttackType.Push, require = CombatState.AerialHit,
                aim = TargetingType.Direction, radius = 3.5f,
                castTime = 0.3f, recovery = 0.3f,
                hits = { Push(14f, 18f) },
            });

            return t;
        }

        // ── 굽기 ──────────────────────────────────────────

        [MenuItem("Prototype/기획표 - 스킬 굽기 (빠진 것만)")]
        public static void Build()
        {
            EnsureFolder(Folder);

            int created = 0, kept = 0;
            List<Spec> table = BuildTable();

            foreach (Spec s in table)
            {
                string path = $"{Folder}/SK_{s.id}.asset";

                if (AssetDatabase.LoadAssetAtPath<SkillData>(path) != null)
                {
                    kept++;
                    continue;   // 손으로 고쳤을 수 있다. 건드리지 않는다.
                }

                var asset = ScriptableObject.CreateInstance<SkillData>();
                Fill(asset, s);
                AssetDatabase.CreateAsset(asset, path);
                EditorUtility.SetDirty(asset);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[기획표] 스킬 {table.Count}칸 — 신규 {created} / 기존 유지 {kept} → {Folder}");
        }

        private static void Fill(SkillData a, Spec s)
        {
            a.skillName = s.name;
            a.description = $"{s.id} — {s.note}";
            a.role = s.role;
            a.attackType = s.type;
            a.requireState = s.require;
            a.targeting = s.aim;
            a.radius = s.radius;

            a.castTime = s.castTime;
            a.hitInterval = s.hitInterval;
            a.recoveryTime = s.recovery;
            a.cooldown = 5f;
            a.manaCost = 20f;

            a.hitDataList = new List<HitData>(s.hits);
            a.effects = new List<ISkillEffect>(s.effects);

            a.maxChargeTime = s.maxChargeTime;
            a.maxChargeDamageMul = s.chargeDamageMul;
            a.maxChargeRadiusMul = s.chargeRadiusMul;

            // 결과 상태는 HitData를 실제로 돌려서 뽑는다 — 예측과 어긋날 여지를 없앤다.
            a.resultState = Simulate(s);

            if (!s.projectile) return;

            a.projectile = AssetDatabase
                .LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectile.prefab")
                ?.GetComponent<Projectile>();

            a.projectileSpeed = s.projSpeed;
            a.projectileRange = s.projRange;
            a.projectilePierce = s.projPierce;

            if (a.projectile == null)
                Debug.LogWarning($"[기획표] {s.name}: 투사체 프리팹이 없다. " +
                                 "'투사체 - 프리팹 만들고 원거리에 물리기'를 먼저 실행할 것.");
        }

        /// <summary>실전투와 같은 규칙으로 결과 상태를 계산한다.</summary>
        private static CombatState Simulate(Spec s)
        {
            CombatState cur = s.require;
            for (int i = 0; i < s.hits.Count; i++)
            {
                HitData h = s.hits[i];
                cur = CombatStateRules.Next(cur, in h, i);
            }

            // 밀치기는 Knockback으로 날아가 벽에 닿아야 WallBound가 된다.
            // 표시용 결과는 의도한 종착지를 적어 준다.
            return s.type == AttackType.Push && cur == CombatState.Knockback
                ? CombatState.WallBound
                : cur;
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
