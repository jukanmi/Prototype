using UnityEngine;

namespace Prototype
{
    // ══ GroundRules ═══════════════════════════════════════════

    /// <summary>지면(XZ) 위의 방향 계산. 순수 함수라 씬 없이 쓴다.</summary>
    public static class GroundRules
    {
        /// <summary>방향을 지면(XZ)에 눌러 단위 벡터로. 거의 0이면 <see cref="Vector3.forward"/>.</summary>
        public static Vector3 FlatDirection(Vector3 dir)
        {
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }
    }
}
