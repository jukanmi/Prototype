using UnityEngine;

namespace Prototype
{
    /// <summary>적 한 종류의 수치 테이블.</summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Data", fileName = "Enemy_")]
    public class EnemyData : ScriptableObject
    {
        public string enemyId = "Enemy_001";
        public string enemyName = "고블린";

        [Header("전투")]
        public float hp = 40f;
        public float atk = 5f;

        [Header("행동")]
        [Tooltip("판단 로직. 비우면 프리팹 EnemyControl의 폴백 브레인을 쓴다.")]
        public EnemyBrainAsset brain;

        public float moveSpeed = 4f;
        public float attackRange = 1.8f;
        public float attackInterval = 1.5f;
        public float retargetInterval = 0.5f;

        [Tooltip("타겟이 이보다 멀면 추격을 포기한다. 0이면 무제한.")]
        public float leashRange = 0f;

        [Tooltip("원거리 전용. 이보다 가까우면 물러난다. 0이면 물러나지 않는다.")]
        public float preferredMinRange = 0f;

        [Header("특수 행동 (돌진)")]
        [Tooltip("돌진이 닿는 최대 거리. 0이면 돌진하지 않는다.")]
        public float specialRange = 0f;

        [Tooltip("돌진 재사용 대기시간.")]
        public float specialInterval = 4f;

        [Header("평타 — 원거리")]
        [Tooltip("넣으면 평타가 투사체가 된다. 비우면 근접 평타 그대로.")]
        public Projectile basicProjectile;
        public float projectileSpeed = 16f;
        public float projectileRange = 9f;
        public int projectilePierce = 0;

        [Header("보상")]
        public int exp = 10;
        public int gold = 5;

        public GameObject prefab;
    }
}
