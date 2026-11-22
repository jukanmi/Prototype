// 전투 칸 클리어 보상 — 바닥에 떨어지는 금화 하나와 상자 하나. 상자를 열면 스킬 두루마리 셋 중 하나를 고른다.
// 자리 계산(ClearLootRules)은 순수 함수, 오브젝트를 짓고 줍는 일(ClearLoot)은 그 위에 얹힌다.

using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ ClearLootRules ═══════════════════════════════════════════

    /// <summary>
    /// 보상이 떨어지는 자리. 순수 함수라 씬 없이 검증한다(<c>ClearLootRulesTests</c>).
    ///
    /// <b>출구선 안쪽에만 놓는다.</b> 출구선(<see cref="StageOutcomeRules.ExitLine"/>)을 넘는 순간 칸이 끝나므로,
    /// 줍다가 그 선을 밟을 자리에 놓이면 주우러 가는 길이 곧 나가는 길이 된다.
    /// </summary>
    public static class ClearLootRules
    {
        /// <summary>금화와 상자 사이의 반 간격.</summary>
        public const float PairHalfGap = 1.1f;

        /// <summary>출구선에서 이만큼 안쪽까지만 놓는다.</summary>
        public const float ExitClearance = 1.6f;

        /// <summary>벽에서 이만큼 떨어뜨린다.</summary>
        public const float WallClearance = 1f;

        /// <summary>
        /// 두루마리 사이 간격. 이름표("[F] 원거리 사격" ≈ 3)가 옆과 안 겹치는 폭이다.
        /// 상호작용 반경(<see cref="SkillRadius"/>)의 두 배보다도 넓어 둘이 같이 안 잡힌다.
        /// </summary>
        public const float SkillGap = 3.2f;

        /// <summary>두루마리 줄이 상자에서 떨어지는 깊이. 화면 아래(카메라 쪽)로 내려 상자와 안 겹친다.</summary>
        public const float SkillRowDepth = 1.4f;

        public const float PickRadius = 1.3f;
        public const float SkillRadius = 0.8f;

        /// <summary>금화와 상자 한가운데 — <paramref name="at"/>(보통 방 한가운데)를 방 안 · 출구선 안쪽으로 민 자리.</summary>
        public static Vector3 Center(Vector3 at, RoomRect room, float exitLine)
        {
            float x = Fit(at.x,
                          room.MinX + WallClearance + PairHalfGap,
                          Mathf.Min(room.MaxX - WallClearance, exitLine - ExitClearance) - PairHalfGap);

            float z = Fit(at.z, room.MinZ + WallClearance, room.MaxZ - WallClearance);
            return new Vector3(x, at.y, z);
        }

        public static Vector3 GoldAt(Vector3 center) => center + Vector3.left * PairHalfGap;

        public static Vector3 ChestAt(Vector3 center) => center + Vector3.right * PairHalfGap;

        /// <summary>
        /// 상자에서 튀어나온 두루마리 <paramref name="count"/>개가 앉을 자리. 상자 앞(카메라 쪽)에 가로로 늘어선다.
        /// 앞이 벽에 막히면 뒤로 놓는다. 줄 전체를 방 · 출구선 안으로 민다.
        /// </summary>
        public static Vector3[] SkillRow(Vector3 chest, RoomRect room, float exitLine, int count)
        {
            var row = new Vector3[Mathf.Max(0, count)];
            if (row.Length == 0) return row;

            float span = SkillGap * (row.Length - 1) * 0.5f;
            float left = room.MinX + WallClearance;
            float right = Mathf.Min(room.MaxX - WallClearance, exitLine - ExitClearance);
            float mid = Fit(chest.x, left + span, right - span);

            float front = chest.z - SkillRowDepth;
            float z = front >= room.MinZ + WallClearance * 0.5f ? front : chest.z + SkillRowDepth;

            for (int i = 0; i < row.Length; i++)
                row[i] = new Vector3(mid - span + SkillGap * i, chest.y, z);

            return row;
        }

        /// <summary>구간 안으로 민다. 구간이 뒤집혔으면(방이 너무 좁다) 가운데로.</summary>
        private static float Fit(float value, float min, float max)
            => min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
    }

    // ══ ClearLoot ═══════════════════════════════════════════

    /// <summary>
    /// 전투 칸을 깨면 <see cref="BattleSceneController"/>가 부른다 — 금화와 상자를 바닥에 놓는다.
    ///
    /// <list type="bullet">
    /// <item><b>금화</b> — 주우면 그 칸의 클리어 골드(<see cref="GoldRules.StageClearReward"/>)가 들어온다.</item>
    /// <item><b>상자</b> — 열면 스킬 두루마리 셋이 튀어나온다(<see cref="CardOfferRules"/>로 뽑는다).
    /// 하나를 주우면 런 덱과 지금 손패에 들어가고 나머지 둘은 사라진다.</item>
    /// </list>
    ///
    /// 줍지 않고 출구로 가도 된다 — 그 보상은 버린 것이다.
    /// </summary>
    public static class ClearLoot
    {
        private static readonly Color GoldTextColor = new Color(1f, 0.86f, 0.35f);
        private static readonly Color SkillTextColor = new Color(0.75f, 0.92f, 1f);

        private const float CoinSize = 0.55f;
        private const float ChestSize = 1.15f;
        private const float ScrollSize = 0.8f;
        private const float FlySeconds = 0.45f;

        /// <param name="spot">보상이 놓일 한가운데 — 끝나는 방의 한가운데를 준다.</param>
        /// <param name="gold">금화 하나에 든 골드. 0 이하면 금화를 안 놓는다.</param>
        /// <param name="chest">상자를 놓는가(일반 전투 · 정예).</param>
        /// <param name="elite">정예 칸 — 빨간 상자.</param>
        public static void Drop(Vector3 spot, RoomRect room, float exitLine, int gold, bool chest, bool elite)
        {
            LootArt art = LootArt.Get();
            Vector3 center = ClearLootRules.Center(spot, room, exitLine);

            if (gold > 0) DropGold(ClearLootRules.GoldAt(center), gold, art);

            if (chest)
            {
                Vector3 at = ClearLootRules.ChestAt(center);
                Sprite closed = art != null ? art.ChestOf(elite, false) : null;

                LootPickup.Create(elite ? "Loot_EliteChest" : "Loot_Chest", at, closed, Color.white, ChestSize, 0f,
                                  "상자", ClearLootRules.PickRadius,
                                  box => OpenChest(box, room, exitLine, elite));
            }

            Debug.Log($"[Loot] 보상 — 금화 {gold} · 상자 {(chest ? (elite ? "정예" : "일반") : "없음")} @ {center}");
        }

        private static void DropGold(Vector3 at, int gold, LootArt art)
        {
            Sprite round = art != null ? art.coin : null;
            Color rim = art != null ? art.coinColor : new Color(1f, 0.85f, 0.35f);
            Color face = art != null ? art.coinFaceColor : new Color(0.93f, 0.66f, 0.18f);

            LootPickup coin = LootPickup.Create("Loot_Gold", at, round, rim, CoinSize, 0.25f,
                                                $"금화 {gold}", ClearLootRules.PickRadius,
                                                picked => PickGold(picked, gold));

            // 테두리 안에 한 겹 작은 원 — 납작한 원이 동전으로 읽힌다.
            coin.AddLayer("Face", round, face, 0.68f, true);
            coin.Spinning();
        }

        private static void PickGold(LootPickup coin, int gold)
        {
            RunProgression.Current.AddGold(gold);
            LootPickup.Popup(coin.transform.position, $"+{gold} 골드", GoldTextColor);

            Debug.Log($"[Loot] 금화 — 골드 +{gold} (누적 {RunProgression.Current.Gold})");
            Object.Destroy(coin.gameObject);
        }

        // ── 상자 ────────────────────────────────────────

        private static void OpenChest(LootPickup box, RoomRect room, float exitLine, bool elite)
        {
            box.Disarm();

            LootArt art = LootArt.Get();
            Sprite open = art != null ? art.ChestOf(elite, true) : null;
            if (open != null) box.Sprite.sprite = open;

            List<CardOffer> offers = CardOfferRules.Build(SkillPool(), () => Random.value);
            if (offers.Count == 0)
            {
                // 표 배선이 빠졌다. 조용히 빈 상자가 되면 원인을 못 찾는다.
                BattleLog.Warn(LogCategory.State,
                    "상자에 넣을 스킬이 없다 — Assets/Data/Resources/SkillCatalog.asset을 확인할 것", box);
                LootPickup.Popup(box.transform.position, "텅 비었다", Color.gray);
                return;
            }

            Vector3 from = box.transform.position;
            Vector3[] seats = ClearLootRules.SkillRow(from, room, exitLine, offers.Count);
            var group = new List<LootPickup>(offers.Count);

            for (int i = 0; i < offers.Count; i++)
            {
                SkillData skill = offers[i].data;
                Color glow = SkillCutinUI.RoleColor(skill.role);
                glow.a = 0.55f;

                LootPickup scroll = LootPickup.Create($"Loot_Skill_{i}", seats[i],
                                                      art != null ? art.ScrollOf(skill.role) : null, Color.white,
                                                      ScrollSize, 0.35f, skill.skillName, ClearLootRules.SkillRadius,
                                                      picked => PickSkill(picked, group));
                scroll.Skill = skill;
                scroll.AddLayer("Glow", art != null ? art.coin : null, glow, 1.25f, false);
                scroll.LaunchFrom(from, FlySeconds + 0.08f * i);

                group.Add(scroll);
            }

            BattleLog.Log(LogCategory.State, $"<b>상자</b> — 두루마리 {offers.Count}개", box);
        }

        /// <summary>
        /// 뽑을 스킬 — <b>파티</b>가 정한다(시전할 동료가 없는 스킬은 빠진다).
        /// 지금 든 카드에서 긁으면 0장으로 시작하는 테스트 모드에서 뽑을 것이 없다.
        /// </summary>
        private static List<SkillData> SkillPool()
        {
            BulletTimeController bulletTime = Object.FindAnyObjectByType<BulletTimeController>();
            return bulletTime != null
                ? bulletTime.AvailableSkillPool()
                : SkillCatalog.Pool(null, RunProgression.Current.Cards);
        }

        private static void PickSkill(LootPickup picked, List<LootPickup> group)
        {
            SkillData skill = picked.Skill;

            // 런 덱에 넣고, 들어간 <b>그 카드</b>를 지금 손패에 바로 꽂는다 — 다음 판까지 기다리면 고른 보람이 없다.
            ComboCard granted = RunProgression.Current.Grant(new CardOffer { data = skill });
            BulletTimeController bulletTime = Object.FindAnyObjectByType<BulletTimeController>();
            if (granted != null && bulletTime != null) bulletTime.GrantCard(granted);

            LootPickup.Popup(picked.transform.position, $"{skill.skillName} 획득", SkillTextColor);
            BattleLog.Log(LogCategory.State,
                $"스킬 선택 — {skill.skillName} | 런 덱 {RunProgression.Current.Cards.Count}종", picked);

            foreach (LootPickup other in group)
                if (other != null) Object.Destroy(other.gameObject);

            group.Clear();
        }
    }
}
