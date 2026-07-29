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

        [Header("보상")]
        public int exp = 10;
        public int gold = 5;

        public GameObject prefab;
    }
}
