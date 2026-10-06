// 월드 마커 한 흐름 — 요청 → 규칙 → 풀 → 그리기.
//   MarkerRequest  무엇을 어디에 그릴지
//   MarkerRules    그 요청을 실제 수치로 푸는 표
//   MarkerLayer    마커를 재활용하는 풀
//   WorldMarker    한 개를 실제로 그리는 컴포넌트
// 네 조각을 따로 읽을 일이 없어 한 파일에 둔다.
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ WorldMarker ═══════════════════════════════════════════

    /// <summary>
    /// 표식 한 개의 몸통. <see cref="MarkerLayer"/>가 풀에서 꺼내 쓰고 남는 것은 숨긴다.
    ///
    /// 그리는 규칙은 <see cref="EnemyStateLabel"/> · <see cref="ChargeGauge"/>와 같다 —
    /// 논리 좌표가 아니라 <see cref="BeltScroll.ToView"/>를 거친 그리는 위치를 쓰고,
    /// 머리 오프셋에는 깊이 배율을 먹인다(뒤에 선 캐릭터는 몸이 줄어 머리도 내려온다).
    /// </summary>
    public class WorldMarker : MonoBehaviour
    {
        private const int FontSize = 54;

        private TextMesh textMesh;
        private MeshRenderer meshRenderer;

        /// <summary>
        /// 표식이 놓일 자리.
        ///
        /// <see cref="ControlledCharacterArrow.ArrowPosition"/>과 같은 계산이다 —
        /// 그쪽은 기존 테스트가 부르고 있어 시그니처를 그대로 두었고, 여기가 그것을 위임받는다.
        /// </summary>
        public static Vector3 Position(Vector3 ground, float height, float headOffset, float bob = 0f)
            => BeltScroll.ToView(ground, height)
             + BeltScroll.ScreenUp * ((headOffset + bob) * BeltScroll.ScaleAt(ground.z));

        public static WorldMarker Create(Transform parent)
        {
            var go = new GameObject("WorldMarker");
            go.transform.SetParent(parent, false);
            return go.AddComponent<WorldMarker>();
        }

        public void Draw(in MarkerRequest req, float drawX)
        {
            EnsureRenderer();

            // 물린 X로 그리되 깊이 배율은 <b>원래 깊이</b>로 계산한다.
            // 가장자리로 밀었다고 크기가 변하면 "멀어졌다"로 잘못 읽힌다.
            var ground = new Vector3(drawX, req.ground.y, req.ground.z);

            textMesh.transform.position = Position(ground, req.height, req.headOffset, req.bob);

            // 빌보드 위에 각도를 얹는다. 카메라를 마주 본 평면에서 도는 것이라
            // 기울어진 카메라에서도 화면 기준으로 정확히 눕는다.
            textMesh.transform.rotation = BeltScroll.Billboard * Quaternion.Euler(0f, 0f, req.angle);

            textMesh.text = req.glyph;
            textMesh.color = req.color;
            textMesh.characterSize = req.size;

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋 적용
            meshRenderer.sortingOrder = Mathf.RoundToInt(-req.ground.z * 100f) + req.sortingOffset;
            meshRenderer.enabled = true;
        }

        public void Hide()
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
        }

        private void EnsureRenderer()
        {
            if (textMesh != null && meshRenderer != null) return;

            textMesh = gameObject.GetComponent<TextMesh>();
            if (textMesh == null) textMesh = gameObject.AddComponent<TextMesh>();

            textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.fontSize = FontSize;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;

            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = textMesh.font.material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }
    }

    // ══ MarkerLayer ═══════════════════════════════════════════

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
