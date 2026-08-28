using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 대기 좌표의 <b>단일 출처</b>. <see cref="StandbyRules"/>를 실제 로스터에 붙인 얇은 어댑터다.
    ///
    /// <b>값을 캐시하지 않는다.</b> 카메라가 움직이면 화면 밖도 함께 움직이므로,
    /// 물어보는 순간의 카메라로 매번 계산하는 것이 맞다. 대신 <b>연출은 시작하는 순간에
    /// 한 번만 물어본다</b> — 매 프레임 다시 물으면 카메라를 따라 목적지가 흘러 궤적이 휜다.
    ///
    /// <see cref="TagSwapController"/>가 소유한다. 로스터 · 조작 중인 칸 · 앵커를
    /// 이미 그쪽이 알고 있어서, 여기에 또 한 벌을 두면 반드시 어긋난다.
    /// </summary>
    public class PartyStandby
    {
        private readonly IReadOnlyList<Entity> roster;

        public PartyStandby(IReadOnlyList<Entity> bodies)
        {
            roster = bodies;
        }

        public int Count => roster != null ? roster.Count : 0;

        /// <summary>이 몸이 로스터 몇 번 칸인가. 없으면 -1.</summary>
        public int IndexOf(Entity body)
        {
            if (body == null || roster == null) return -1;

            for (int i = 0; i < roster.Count; i++)
                if (ReferenceEquals(roster[i], body)) return i;

            return -1;
        }

        /// <summary>
        /// 이 칸의 대기 좌표. 로스터에 없는 번호를 물어도 답은 나온다 —
        /// 규칙이 순번만으로 정해지므로 빈 칸도 자기 자리를 갖는다.
        /// </summary>
        public Vector3 PointFor(int index)
            => StandbyRules.Slot(index, EntranceDirector.CameraX, EntranceDirector.HalfWidth);

        /// <summary>이 몸이 물러날 자리. 로스터 밖의 몸이면 false.</summary>
        public bool TryPointFor(Entity body, out Vector3 point)
        {
            int index = IndexOf(body);

            if (index < 0)
            {
                point = Vector3.zero;
                return false;
            }

            point = PointFor(index);
            return true;
        }

        /// <summary>
        /// 지금 화면 밖에서 대기 중인 몸들. <paramref name="currentIndex"/>(조작 중)와
        /// 빈 칸은 빼고, <b>죽은 몸은 넣는다</b> — 표식이 회색으로 알려 줘야 할 정보다.
        ///
        /// 리스트를 받아 채운다. 매 프레임 표식이 부르는 자리라 새 리스트를 만들면
        /// 그대로 GC 부담이 된다(<see cref="BattleRegistry"/>와 같은 취지).
        /// </summary>
        public void Collect(List<StandbySeat> into, int currentIndex)
        {
            if (into == null) return;
            into.Clear();

            if (roster == null) return;

            // 카메라는 한 번만 읽는다. 칸마다 다시 읽으면 같은 프레임 안에서도
            // 값이 갈릴 수 있고, 무엇보다 그럴 이유가 없다.
            float cameraX = EntranceDirector.CameraX;
            float halfWidth = EntranceDirector.HalfWidth;

            for (int i = 0; i < roster.Count; i++)
            {
                if (i == currentIndex) continue;

                Entity body = roster[i];
                if (body == null) continue;

                bool alive = body.Combat != null && !body.Combat.IsDead;

                into.Add(new StandbySeat(i, body, StandbyRules.Slot(i, cameraX, halfWidth), alive));
            }
        }
    }

    /// <summary>대기 중인 한 명. 표식이 이걸 그대로 그린다.</summary>
    public readonly struct StandbySeat
    {
        /// <summary>로스터 칸. 표식의 좌우 · 깊이가 여기서 정해지므로 그 사람의 <b>고정된 자리</b>다.</summary>
        public readonly int Index;

        public readonly Entity Body;

        /// <summary>화면 밖 대기 좌표(지상).</summary>
        public readonly Vector3 Point;

        /// <summary>살아 있는가. 죽었어도 목록에는 남는다 — 표식이 회색으로 알려 준다.</summary>
        public readonly bool Alive;

        public StandbySeat(int index, Entity body, Vector3 point, bool alive)
        {
            Index = index;
            Body = body;
            Point = point;
            Alive = alive;
        }
    }
}
