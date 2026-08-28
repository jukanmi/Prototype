using UnityEngine;

namespace Prototype
{
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
}
