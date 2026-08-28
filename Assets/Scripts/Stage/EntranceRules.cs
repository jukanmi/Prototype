using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 몸이 <b>화면 밖 어디에서</b> 들어오고 나가는가. <b>순수 함수</b>다.
    ///
    /// 벨트스크롤이라 계산이 한 축으로 접힌다 — 카메라는 X로만 움직이고
    /// 깊이(Z)는 언제나 화면 안이므로, "화면 밖"은 <b>X 하나로 결정된다</b>.
    /// 보이는 구간은 <c>[cameraX - halfWidth, cameraX + halfWidth]</c>이고,
    /// 이 폭은 <see cref="CameraFrameRules.HalfWidth"/>가 카메라와 똑같이 계산한다.
    ///
    /// 씬도 카메라도 모르는 자리에 둔 이유는 <see cref="CameraFrameRules"/>와 같다 —
    /// 등장 버그는 "뭔가 이상한데"로만 보이지 숫자가 안 보인다.
    /// </summary>
    public static class EntranceRules
    {
        /// <summary>화면 끝에서 더 밀어내는 여유. 몸통 반지름과 깊이 배율을 감안한 값이다.</summary>
        public const float DefaultMargin = 1.5f;

        /// <summary>기본 소요 시간. 컷인(0.98초) 안에 완전히 묻히는 길이로 잡았다.</summary>
        public const float DefaultSeconds = 0.35f;

        /// <summary>
        /// 소요 시간의 상한. <b>안전핀이다.</b>
        ///
        /// 진입 중인 적은 라운드 클리어 인구조사에 잡히므로(<see cref="RoundClearRules"/>),
        /// 연출이 늘어지면 라운드가 그만큼 안 끝난다.
        /// <see cref="SpawnEntry.DefaultWalkTimeout"/>과 같은 취지다.
        /// </summary>
        public const float MaxSeconds = 1.5f;

        public const float MinSeconds = 0.05f;

        /// <summary>
        /// 카메라가 없는 씬(테스트 · 스킬 시험장)에서 쓰는 반폭.
        /// 방 반경보다 넓어야 "밖"이 정말 밖이 된다.
        /// </summary>
        public const float FallbackHalfWidth = WaveSpawnPlanner.RoomHalfX + 2f;

        /// <summary>이 X가 지금 화면 안인가.</summary>
        public static bool IsOnScreen(float x, float cameraX, float halfWidth)
            => x >= cameraX - halfWidth && x <= cameraX + halfWidth;

        /// <summary>
        /// 어느 쪽 가장자리에서 들어올 것인가. +1이 오른쪽, -1이 왼쪽이다.
        ///
        /// <b>가까운 쪽에서 온다.</b> 먼 쪽에서 오면 화면을 가로질러야 해서
        /// 같은 시간에 훨씬 빨리 움직여야 하고, 그러면 착지에서 급정거로 보인다.
        /// </summary>
        public static int SideOf(float landingX, float cameraX)
            => landingX >= cameraX ? 1 : -1;

        /// <summary>
        /// <paramref name="side"/> 쪽 화면 밖 X.
        ///
        /// 화면 끝과 착지점 <b>둘 다</b>보다 바깥이어야 한다. 화면 끝만 보면 착지점이 이미
        /// 화면 밖인 경우(아레나 벽 뒤, 좁은 구간)에 시작점이 착지점보다 <b>안쪽</b>이 되어
        /// 몸이 거꾸로 들어온다.
        /// </summary>
        public static float OffscreenX(float landingX, int side, float cameraX, float halfWidth,
                                       float margin = DefaultMargin)
        {
            float m = Mathf.Max(0f, margin);

            return side >= 0
                ? Mathf.Max(cameraX + halfWidth, landingX) + m
                : Mathf.Min(cameraX - halfWidth, landingX) - m;
        }

        /// <summary>
        /// 화면 밖 시작점(또는 퇴장 목적지). 깊이와 높이는 착지점 그대로다 —
        /// 옆으로만 벗어나면 화면에서 사라진다.
        /// </summary>
        public static Vector3 OffscreenPoint(Vector3 landing, int side, float cameraX, float halfWidth,
                                             float margin = DefaultMargin)
            => new Vector3(OffscreenX(landing.x, side, cameraX, halfWidth, margin), landing.y, landing.z);

        /// <summary>가까운 가장자리를 스스로 골라 잡은 화면 밖 지점.</summary>
        public static Vector3 OffscreenPoint(Vector3 landing, float cameraX, float halfWidth,
                                             float margin = DefaultMargin)
            => OffscreenPoint(landing, SideOf(landing.x, cameraX), cameraX, halfWidth, margin);

        /// <summary>
        /// 가감속 곡선. <b>빠르게 나와 감속하며 선다</b>(ease-out cubic).
        ///
        /// 반대로(가속) 하면 화면 밖에서 굼뜨게 기어 나오다 착지에서 튄다 —
        /// "튀어나온다"는 인상은 <b>첫 프레임의 속도</b>가 만든다.
        /// </summary>
        public static float Ease(float t)
        {
            float x = 1f - Mathf.Clamp01(t);
            return 1f - x * x * x;
        }

        /// <summary>진행률 <paramref name="t"/>에서의 위치.</summary>
        public static Vector3 Sample(Vector3 start, Vector3 landing, float t)
            => Vector3.LerpUnclamped(start, landing, Ease(t));

        /// <summary>소요 시간을 안전 범위로 물린다.</summary>
        public static float ClampSeconds(float seconds)
            => Mathf.Clamp(seconds <= 0f ? DefaultSeconds : seconds, MinSeconds, MaxSeconds);
    }
}
