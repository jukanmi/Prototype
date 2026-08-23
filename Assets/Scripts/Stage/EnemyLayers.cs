using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 런타임에 소환한 적의 <b>레이어 배선</b>.
    ///
    /// 왜 필요한가: <c>Assets/Prefabs/Enemy_*.prefab</c>은 레이어가 Default(0)인 채로 저장돼 있다.
    /// 씬에 놓인 적은 <c>SceneLayoutBuilder</c>가 구워 줄 때 EnemyHurtbox · EnemyHitbox 로 바꾸지만,
    /// 그 빌더는 <b>씬만</b> 손본다. 프리팹을 그대로 Instantiate 하면 Default 인 채로 나오고,
    /// <see cref="Attack"/>은 충돌 매트릭스(<c>DynamicsManager</c>)를 그대로 읽으므로
    /// 결과는 <b>서로를 통과하는 적</b>이다 — 때려도 안 맞고 맞아도 안 아프다.
    /// 증상이 "가끔 안 맞는다"가 아니라 "그 웨이브만 통째로 무해하다"라서 더 헷갈린다.
    ///
    /// 레이어 번호는 3D전환_TODO.md §2 · <c>SceneLayoutBuilder</c>와 같은 이름을 쓴다.
    /// 이름으로 찾는 이유는 번호가 프로젝트 설정에 있고 코드가 그 사본을 들면 언젠가 어긋나기 때문이다.
    /// </summary>
    public static class EnemyLayers
    {
        public const string HurtboxLayer = "EnemyHurtbox";
        public const string HitboxLayer = "EnemyHitbox";

        /// <summary>
        /// 적 하나를 규약대로 맞춘다. 몸통은 피격 레이어, <see cref="Attack"/>이 붙은 자식은
        /// 전부 타격 레이어다 — 평타 히트박스와 돌진 히트박스 둘 다 해당한다.
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

            // 비활성 자식까지 본다. 돌진 히트박스는 꺼져 있는 것이 정상이다.
            foreach (Attack attack in root.GetComponentsInChildren<Attack>(true))
                attack.gameObject.layer = hit;
        }
    }
}
