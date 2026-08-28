using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지정 좌표 반경 안의 살아 있는 적을 센다 · 모은다.
    ///
    /// <see cref="TargetSelector"/>의 조준 HUD("몇 명 맞는가")와 <see cref="KnockbackIndicator"/>의
    /// 화살표 대상 목록이 <b>같은 함수</b>를 봐야 한다 — 세는 쪽과 그리는 쪽이 따로 계산하면
    /// "경고는 없는데 화살표도 없다" 같은 어긋남이 생긴다.
    ///
    /// 콤보가 실제로 이어지는지(상태 사슬 · 다운 무적)는 여기서 보지 않는다 — 그건 수치로
    /// 보장하는 영역이라 별도 시뮬레이션이 필요 없다. 이 클래스는 순수 공간 질의만 한다.
    /// </summary>
    public class EnemyRadiusProbe : MonoBehaviour
    {
        private static readonly Collider[] Buffer = new Collider[64];
        // 한 콜라이더가 여러 개 잡히는 걸 막는 중복 필터. 매 프레임 도는 경로라 재사용한다.
        private static readonly HashSet<Combat> Seen = new HashSet<Combat>();

        /// <summary>
        /// 지정 좌표 반경 안의 살아 있는 적 수.
        /// 0이면 모으기 · 장판이 헛치는 것이므로 UI에서 미리 경고한다.
        /// </summary>
        public int CountEnemiesInRadius(Vector3 center, float radius) => EnemiesInRadius(center, radius, null);

        /// <summary>반경 안의 살아 있는 적을 <paramref name="outList"/>에 담고 그 수를 낸다.</summary>
        public int EnemiesInRadius(Vector3 center, float radius, List<Combat> outList)
        {
            outList?.Clear();

            int count = UnityEngine.Physics.OverlapSphereNonAlloc(center, radius, Buffer);
            int alive = 0;
            Seen.Clear();

            for (int i = 0; i < count; i++)
            {
                Combat c = Buffer[i] != null ? Buffer[i].GetComponentInParent<Combat>() : null;
                if (c == null || c.IsDead || !Seen.Add(c)) continue;
                if (c.GetComponent<Enemy>() == null) continue;

                outList?.Add(c);
                alive++;
            }

            return alive;
        }
    }
}
