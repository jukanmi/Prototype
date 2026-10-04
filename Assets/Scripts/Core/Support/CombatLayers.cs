using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전투 몸의 <b>레이어 배선</b>. 몸통은 피격 레이어, <see cref="Attack"/>이 붙은 자식은 전부 타격 레이어.
    ///
    /// 왜 필요한가: 프리팹은 레이어가 Default(0)인 채로 저장돼 있다. 그대로 Instantiate 하면
    /// <see cref="Attack"/>이 충돌 매트릭스(<c>DynamicsManager</c>)를 그대로 읽으므로 결과는
    /// <b>서로를 통과하는 몸</b>이다 — 스킬이 나가고 이펙트도 뜨는데 데미지만 없다.
    /// 증상이 "가끔 안 맞는다"가 아니라 "통째로 무해하다"라 오히려 놓치기 쉽다.
    ///
    /// 소환하는 쪽(<see cref="EnemySpawnService"/> · <see cref="PartyAssembler"/>)이 몸마다 부른다.
    /// 레이어는 이름으로 찾는다 — 번호는 프로젝트 설정에 있고, 코드가 그 사본을 들면 언젠가 어긋난다.
    /// </summary>
    public static class CombatLayers
    {
        public const string AllyHurtbox = "AllyHurtbox";
        public const string AllyHitbox = "AllyHitbox";
        public const string EnemyHurtbox = "EnemyHurtbox";
        public const string EnemyHitbox = "EnemyHitbox";

        public static string HurtboxOf(Faction faction) => faction == Faction.Ally ? AllyHurtbox : EnemyHurtbox;
        public static string HitboxOf(Faction faction) => faction == Faction.Ally ? AllyHitbox : EnemyHitbox;

        /// <summary>
        /// 몸 하나를 규약대로 맞춘다. 비활성 자식까지 본다 — 스킬 · 돌진 히트박스는 꺼져 있는 것이 정상이다.
        /// </summary>
        public static void Apply(GameObject root, Faction faction)
        {
            if (root == null) return;

            string hurtName = HurtboxOf(faction);
            string hitName = HitboxOf(faction);
            int hurt = LayerMask.NameToLayer(hurtName);
            int hit = LayerMask.NameToLayer(hitName);

            if (hurt < 0 || hit < 0)
            {
                BattleLog.Warn(LogCategory.Combat,
                    $"레이어 '{hurtName}' 또는 '{hitName}'가 프로젝트에 없다. " +
                    "'Prototype ▸ 씬 벨트스크롤 배치로 정리'를 한 번 돌려 레이어를 만들 것.", root);
                return;
            }

            root.layer = hurt;

            foreach (Attack attack in root.GetComponentsInChildren<Attack>(true))
                attack.gameObject.layer = hit;
        }
    }
}
