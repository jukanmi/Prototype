using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 아군의 <b>레이어 배선</b>. <see cref="EnemyLayers"/>의 아군판이고, 존재 이유도 같다.
    ///
    /// <b>왜 이제 필요한가.</b> 지금까지 아군 레이어는 <c>SceneLayoutBuilder.AssignLayers</c>가
    /// <b>씬을 구울 때</b> 칠했다. 파티가 씬에 놓여 있었기 때문이다.
    /// 파티가 <see cref="PartyAssembler"/>의 프리팹 안으로 들어가면 그 빌더는 파티를 못 본다 —
    /// 아무도 안 칠하면 <c>Ally</c>가 Default(0) 레이어로 서고, <see cref="Attack"/>은
    /// 충돌 매트릭스를 그대로 읽으므로 결과는 <b>적을 통과하는 아군</b>이다.
    ///
    /// 증상이 "가끔 안 맞는다"가 아니라 "때려도 아무 일도 안 일어난다"라 오히려 놓치기 쉽다 —
    /// 스킬이 나가고 이펙트도 뜨는데 데미지만 없다.
    /// </summary>
    public static class AllyLayers
    {
        public const string HurtboxLayer = "AllyHurtbox";
        public const string HitboxLayer = "AllyHitbox";

        /// <summary>
        /// 아군 하나를 규약대로 맞춘다. 몸통은 피격 레이어, <see cref="Attack"/>이 붙은 자식은
        /// 전부 타격 레이어 — 평타 히트박스와 <c>SkillHitbox</c> 둘 다 해당한다.
        ///
        /// 비활성 자식까지 본다. 스킬 히트박스는 꺼져 있는 것이 정상이다.
        /// </summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;

            int hurt = LayerMask.NameToLayer(HurtboxLayer);
            int hit = LayerMask.NameToLayer(HitboxLayer);

            if (hurt < 0 || hit < 0)
            {
                BattleLog.Warn(LogCategory.Combat,
                    $"레이어 '{HurtboxLayer}' 또는 '{HitboxLayer}'가 프로젝트에 없다. " +
                    "'Prototype ▸ 씬 벨트스크롤 배치로 정리'를 한 번 돌려 레이어를 만들 것.", root);
                return;
            }

            root.layer = hurt;

            foreach (Attack attack in root.GetComponentsInChildren<Attack>(true))
                attack.gameObject.layer = hit;
        }
    }
}
