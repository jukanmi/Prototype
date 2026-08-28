using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 등장 · 퇴장의 <b>유일한 창구</b>. 적 스폰도 동료 교대도 여기로 들어온다.
    ///
    /// 창구를 하나로 두는 이유는 <see cref="EnemySpawnService"/>와 같다 —
    /// 절차가 두 벌이 되면 한쪽만 억제 배선이 빠지는 식으로 조용히 갈라지고,
    /// 증상은 "저 적만 이상하다"로만 보인다.
    ///
    /// 화면 밖 좌표는 <b>여기서 카메라를 읽어</b> 채운다. 계산 자체는
    /// <see cref="EntranceRules"/>가 순수 함수로 갖고 있고, 씬을 아는 부분만 이쪽이다.
    /// </summary>
    public static class EntranceDirector
    {
        /// <summary>지금 카메라가 보고 있는 중심 X. 카메라가 없으면 0.</summary>
        public static float CameraX
        {
            get
            {
                Camera cam = BeltScroll.Cam;
                return cam != null ? cam.transform.position.x : 0f;
            }
        }

        /// <summary>
        /// 카메라가 한쪽으로 보는 폭. <b>매번 읽는다</b> —
        /// 에디터에서 게임 뷰 크기를 바꾸면 그대로 달라지고, 캐싱해 두면
        /// 그때부터 "화면 밖"이 화면 안이 된다(CameraFollow가 종횡비를 매 프레임 다시 읽는 것과 같은 이유).
        /// </summary>
        public static float HalfWidth
        {
            get
            {
                Camera cam = BeltScroll.Cam;
                if (cam == null || !cam.orthographic) return EntranceRules.FallbackHalfWidth;

                return Mathf.Max(EntranceRules.FallbackHalfWidth,
                                 CameraFrameRules.HalfWidth(cam.orthographicSize, cam.aspect));
            }
        }

        /// <summary>
        /// 화면 밖에서 <paramref name="landing"/>으로 들어오는 주문서.
        ///
        /// 시작점은 <b>지금 이 순간의 카메라</b>로 한 번만 계산한다. 매 프레임 다시 잡으면
        /// 통로에서 카메라를 따라 출발점이 흘러 궤적이 휜다.
        /// </summary>
        public static EntranceSpec PlanEntry(Vector3 landing, float seconds = 0f)
        {
            Vector3 start = EntranceRules.OffscreenPoint(landing, CameraX, HalfWidth);
            return EntranceSpec.Default(start, landing, seconds);
        }

        /// <summary>
        /// <paramref name="from"/>에서 가까운 화면 밖으로 나가는 주문서.
        /// 착지점이 화면 밖이라는 것만 다르고 하는 일은 같다.
        /// </summary>
        public static EntranceSpec PlanExit(Vector3 from, float seconds = 0f)
        {
            Vector3 target = EntranceRules.OffscreenPoint(from, CameraX, HalfWidth);
            return EntranceSpec.Default(from, target, seconds);
        }

        /// <summary>
        /// 연출 하나를 건다. 이미 돌고 있던 연출은 <b>취소하고 갈아탄다</b> —
        /// 퇴장 중인 몸이 다시 불려 나오는 경로가 여럿이라(연속 슬롯 · 사망 자동교대 ·
        /// 카드 즉시 사용), 겹치면 몸이 두 목표 사이에서 떤다.
        /// </summary>
        public static EntrancePlayer Play(Entity body, in EntranceSpec spec)
        {
            if (body == null) return null;

            EntrancePlayer player = Resolve(body);
            if (player == null) return null;

            player.Begin(body, in spec);
            return player;
        }

        /// <summary>지금 이 몸에 연출이 돌고 있는가.</summary>
        public static bool IsPlaying(Entity body)
        {
            if (body == null) return false;

            var player = body.GetComponent<EntrancePlayer>();
            return player != null && player.IsRunning;
        }

        /// <summary>
        /// 돌고 있는 연출을 <b>지금 끝낸다</b>. 남은 시간을 건너뛰고 착지시키며 콜백도 부른다.
        /// 없으면 아무 일도 안 한다.
        ///
        /// 기다릴 수 없는 입력이 앞당길 때 쓴다 — U키 카드가 그렇다.
        /// </summary>
        public static void Finish(Entity body)
        {
            if (body == null) return;

            var player = body.GetComponent<EntrancePlayer>();
            if (player != null) player.Finish();
        }

        /// <summary>
        /// 돌고 있는 연출을 중단한다. 착지시키지 않고, <see cref="EntranceSpec.onArrive"/>도 안 부른다.
        /// 없으면 아무 일도 안 한다.
        /// </summary>
        public static void Cancel(Entity body, bool restore = true)
        {
            if (body == null) return;

            var player = body.GetComponent<EntrancePlayer>();
            if (player != null) player.Cancel(restore);
        }

        /// <summary>
        /// 구동기를 얻는다. <b>다 쓴 뒤에도 파괴하지 않고 재사용한다</b> —
        /// <c>Destroy</c>는 프레임 끝까지 미뤄지므로, 도착 콜백이 그 자리에서 다음 연출을
        /// 걸면 파괴 대기 중인 컴포넌트에 다시 <c>AddComponent</c>를 하게 된다.
        /// 쉬는 동안에는 <c>enabled = false</c>라 LateUpdate 비용도 없다.
        /// </summary>
        private static EntrancePlayer Resolve(Entity body)
        {
            var player = body.GetComponent<EntrancePlayer>();
            if (player != null) return player;

            return body.gameObject.AddComponent<EntrancePlayer>();
        }
    }
}
