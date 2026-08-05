using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 살아 있는 전투 참가자 목록. 타게팅 · 재타겟 · 광역 판정이 전부 여기서 후보를 얻는다.
    /// FindObjectsOfType을 매 프레임 돌리지 않기 위한 최소한의 캐시.
    /// </summary>
    public static class BattleRegistry
    {
        private static readonly List<Entity> allies = new List<Entity>();
        private static readonly List<Entity> enemies = new List<Entity>();

        /// <summary>
        /// Enter Play Mode Options가 Domain Reload를 끄고 있어 static이 살아남는다.
        /// 리셋하지 않으면 지난 세션의 파괴된 Entity가 목록에 그대로 쌓인다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        /// <summary>플레이어와 동료.</summary>
        public static IReadOnlyList<Entity> Allies => allies;
        public static IReadOnlyList<Entity> Enemies => enemies;

        public static void RegisterAlly(Entity e)
        {
            if (e != null && !allies.Contains(e)) allies.Add(e);
        }

        public static void RegisterEnemy(Entity e)
        {
            if (e != null && !enemies.Contains(e)) enemies.Add(e);
        }

        public static void Unregister(Entity e)
        {
            allies.Remove(e);
            enemies.Remove(e);
        }

        public static void Clear()
        {
            allies.Clear();
            enemies.Clear();
        }

        public static int AliveEnemyCount()
        {
            int n = 0;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && !enemies[i].Combat.IsDead) n++;
            return n;
        }

        public static bool AllAlliesDead()
        {
            for (int i = 0; i < allies.Count; i++)
                if (allies[i] != null && !allies[i].Combat.IsDead) return false;
            return true;
        }

        /// <summary>지정 좌표에서 가장 가까운 살아 있는 적. 대상 사망 시 재타겟에 쓴다(결정 로그 ⑧).</summary>
        public static Entity NearestEnemy(Vector3 position, Entity exclude = null)
        {
            Entity best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                Entity e = enemies[i];
                if (e == null || e == exclude || e.Combat.IsDead) continue;

                float sqr = (e.transform.position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }
    }
}
