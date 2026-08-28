using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 표식을 모아 한 자리에서 그린다. <c>[BattleVfx]</c>에 얹혀 씬 배선 없이 돈다.
    ///
    /// <b>가장자리로 물리는 일은 여기서만 한다.</b> 소스는 "누가 어디에 있다"만 말하고,
    /// 화면 밖인지 · 어느 쪽으로 눕힐지는 전부 <see cref="MarkerRules"/>가 답한다.
    /// 소스마다 그 판정을 따로 두면 한 종류만 가장자리를 안 지키는 날이 온다.
    ///
    /// 소스는 <b>같은 오브젝트에서 찾는다</b>. <see cref="VfxRunner"/>가 컴포넌트를 얹는
    /// 방식이 그대로 배선이 된다.
    /// </summary>
    public class MarkerLayer : MonoBehaviour
    {
        private readonly List<MarkerRequest> requests = new List<MarkerRequest>();
        private readonly List<WorldMarker> markers = new List<WorldMarker>();

        private IMarkerSource[] sources;

        /// <summary>이번 프레임에 실제로 그려진 표식 수. 디버그 · 테스트가 읽는다.</summary>
        public int DrawnCount { get; private set; }

        private void LateUpdate()
        {
            // 소스는 늦게 찾는다. VfxRunner가 컴포넌트를 순서대로 얹으므로
            // Awake 시점에는 아직 다 붙지 않았을 수 있다.
            if (sources == null) sources = GetComponents<IMarkerSource>();

            requests.Clear();

            for (int i = 0; i < sources.Length; i++)
                sources[i]?.Collect(requests);

            Draw();
        }

        private void Draw()
        {
            float cameraX = EntranceDirector.CameraX;
            float halfWidth = EntranceDirector.HalfWidth;

            for (int i = 0; i < requests.Count; i++)
            {
                MarkerRequest req = requests[i];

                // 눕히기를 요청한 표식만 화면 밖 방향으로 돌린다.
                if (req.tilt)
                    req.angle = MarkerRules.PointerAngle(
                        MarkerRules.OffscreenSide(req.ground.x, cameraX, halfWidth));

                float drawX = MarkerRules.ClampToEdge(req.ground.x, cameraX, halfWidth);

                Marker(i).Draw(in req, drawX);
            }

            // 남은 표식은 숨긴다. 파괴하지 않는다 — 인원이 매 프레임 오르내리므로
            // 만들고 지우기를 반복하면 그대로 GC 부담이 된다.
            for (int i = requests.Count; i < markers.Count; i++)
                markers[i].Hide();

            DrawnCount = requests.Count;
        }

        private WorldMarker Marker(int index)
        {
            while (markers.Count <= index)
                markers.Add(WorldMarker.Create(transform));

            return markers[index];
        }
    }
}
