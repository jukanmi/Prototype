// 진입 연출 파이프라인 — 명세 → 규칙 → 감독 → 재생, 그리고 그동안 막는 가드.
//   EntranceSpec      무엇을 어떻게 등장시킬지
//   EntranceRules     명세를 실제 좌표 · 시간으로 푸는 표
//   EntranceDirector  전체를 모는 쪽
//   EntrancePlayer    한 번의 연출을 실제로 재생
//   EntranceGuard     연출 중 조작 · 전투를 막는다
// 다섯이 한 흐름이라 따로 읽을 일이 없다.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ EntranceSpec ═══════════════════════════════════════════

    /// <summary>
    /// 등장 · 퇴장 한 번의 주문서. <see cref="EntranceDirector.Play"/>가 받는 유일한 인자다.
    ///
    /// <b>방향을 구분하지 않는다.</b> 등장은 화면 밖 → 착지점, 퇴장은 착지점 → 화면 밖일 뿐
    /// 하는 일이 같다. 두 메서드로 쪼개면 잠금 · 억제 · 뒷정리가 두 벌이 되고,
    /// 반드시 한쪽만 고쳐지는 날이 온다.
    /// </summary>
    public struct EntranceSpec
    {
        /// <summary>출발 지상 좌표. 등장이면 화면 밖이다.</summary>
        public Vector3 start;

        /// <summary>도착 지상 좌표. 퇴장이면 이쪽이 화면 밖이다.</summary>
        public Vector3 landing;

        /// <summary>도착했을 때 볼 방향. <see cref="Vector3.zero"/>면 진행 방향을 그대로 쓴다.</summary>
        public Vector3 facing;

        /// <summary>
        /// 바닥에서 뜬 높이. 양 끝점 모두 이 높이로 잡히므로 <b>수평으로</b> 날아온다.
        ///
        /// 공중에서 태그 교대를 하면 새 몸도 같은 높이에서 이어받아야 한다 —
        /// 지상 좌표만 넘기면 새 몸만 바닥에 서서 콤보가 그 자리에서 끊긴다.
        /// </summary>
        public float height;

        /// <summary>
        /// 출발점만 <b>지면 아래로</b> 더 내리는 깊이. 0이면 아무것도 안 바뀐다.
        ///
        /// <see cref="height"/>가 양 끝점에 똑같이 걸리는 값이라 "아래에서 위로"를 표현할 수가 없다.
        /// <c>Physics.Teleport</c>도 음수 높이를 0으로 물리므로 그쪽으로도 안 된다.
        /// 그래서 출발 <b>월드</b> 좌표를 잡은 다음 이만큼만 내린다 —
        /// 땅속에서 솟아오르는 등장(<see cref="EntranceDirector.PlanBurrow"/>)이 쓰는 유일한 칸이다.
        /// </summary>
        public float startDepth;

        /// <summary>소요 시간. 0이면 <see cref="EntranceRules.DefaultSeconds"/>.</summary>
        public float seconds;

        /// <summary>
        /// 스케일 안 된 시간으로 굴릴 것인가.
        ///
        /// <b>컷인과 겹치는 등장은 반드시 true다.</b> 컷인이 <c>TimeControl.Scale</c>을
        /// 0.15로 내리므로, 게임 시간으로 굴리면 6.7배 느려져 컷인이 끝나도 아직 날아오는 중이다.
        /// </summary>
        public bool unscaled;

        /// <summary>
        /// 오는 동안 판정 · 조준을 끄고 배경 뒤로 숨길 것인가(<see cref="EntranceGuard"/>).
        ///
        /// 끄지 않으면 양쪽이 다 샌다 — 화면 밖의 몸이 맞거나, 화면 밖으로 광역기가 나간다.
        /// </summary>
        public bool guard;

        /// <summary>
        /// 오는 동안 AI · 조종 입력을 잠글 것인가(<see cref="Entity.IsEntering"/>).
        /// 끄면 날아오는 도중에 몸이 제 판단으로 걸어 나간다.
        /// </summary>
        public bool lockControl;

        /// <summary>
        /// 도착한 <b>뒤에</b> 할 일. 카메라 인계 · 진입 걷기 시작 · <c>SetActive(false)</c>가 여기 실린다.
        ///
        /// 도착 처리(텔레포트 · 방향 · 잠금 해제 · 억제 복구)가 <b>전부 끝난 다음</b> 불린다 —
        /// 그래야 콜백이 그 자리에서 다음 연출을 이어 걸어도 안전하다.
        /// </summary>
        public Action onArrive;

        /// <summary>
        /// 흔히 쓰는 조합. 판정을 끄고 조종을 잠근 채, <b>게임 시간</b>으로 들어온다.
        ///
        /// 기본이 게임 시간인 이유는 몸이 세상의 일부이기 때문이다 — 시간이 멈춘 화면에서
        /// 적 하나만 움직이면 정지가 통째로 깨진다. 컷인과 겹치는 시전자 등장만
        /// <see cref="unscaled"/>를 직접 켠다.
        /// </summary>
        public static EntranceSpec Default(Vector3 start, Vector3 landing, float seconds = 0f)
            => new EntranceSpec
            {
                start = start,
                landing = landing,
                facing = Vector3.zero,
                seconds = seconds,
                unscaled = false,
                guard = true,
                lockControl = true,
            };
    }

    // ══ EntranceRules ═══════════════════════════════════════════

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
        /// 진입 중인 적은 조우 클리어 인구조사에 잡히므로(<see cref="EncounterClearRules"/>),
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

    // ══ EntranceDirector ═══════════════════════════════════════════

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
        /// <b>땅속에서 솟아오르는</b> 주문서. 출발점과 착지점이 같고, 출발만 지면 아래다.
        ///
        /// 카메라를 안 읽는다 — 화면 밖에서 오는 것이 아니라 제자리에서 올라오기 때문이다.
        /// 그래서 <see cref="PlanEntry"/>와 달리 순수 함수고, 테스트가 직접 부를 수 있다.
        ///
        /// 보는 방향을 <b>여기서 정해 준다.</b> 출발과 착지가 같아 진행 방향이 0이라,
        /// 안 정하면 몸이 프리팹에 저장된 방향 그대로 등을 보이고 솟는다.
        /// 방 안쪽(원점 쪽)을 보게 한다.
        /// </summary>
        public static EntranceSpec PlanBurrow(Vector3 landing, float seconds = 0f)
        {
            EntranceSpec spec = EntranceSpec.Default(landing, landing,
                                                     seconds <= 0f ? BurrowRules.Seconds : seconds);

            spec.startDepth = BurrowRules.Depth;
            spec.facing = new Vector3(landing.x >= 0f ? -1f : 1f, 0f, 0f);

            return spec;
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

    // ══ EntrancePlayer ═══════════════════════════════════════════

    /// <summary>
    /// 몸을 화면 밖과 착지점 사이로 실제로 밀어 넣는 구동기. 한 번에 하나만 돈다.
    ///
    /// <b>걷지 않고 밀어 넣는다.</b> 방은 사방이 콜라이더로 막혀 있어서
    /// (<see cref="WaveSpawnPlanner.SpawnInset"/> 주석) 바깥에서 <see cref="Physics.Move"/>로는
    /// 영영 못 들어온다. <see cref="EntranceGuard"/>가 몸통 콜라이더를 꺼 둔 상태이므로
    /// 벽을 통과해 들어오는 것이 맞다.
    ///
    /// <b>LateUpdate에서 자리를 덮어쓴다.</b> 같은 프레임의 Physics.FixedUpdate가
    /// 중력을 먹여 y를 끌어내리는데, 여기서 매 프레임 되돌리지 않으면 화면 밖에서
    /// 바닥 없는 허공을 떨어지며 들어온다 — 시작점은 방 밖이라 발판이 없다.
    /// <see cref="BeltScrollView"/>보다 먼저 돌아야 스프라이트가 한 프레임 늦지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EntrancePlayer : MonoBehaviour
    {
        private Entity body;
        private Physics physics;
        private EntranceGuard guard;

        private Vector3 startWorld;
        private Vector3 landingWorld;
        private Vector3 landingGround;
        private Vector3 facing;
        private float height;

        private float duration;
        private float elapsed;
        private bool unscaled;
        private bool lockedControl;
        private Action onArrive;

        public bool IsRunning { get; private set; }

        /// <summary>연출을 시작한다. 이미 돌고 있으면 앞엣것을 취소하고 갈아탄다.</summary>
        public void Begin(Entity owner, in EntranceSpec spec)
        {
            if (owner == null) return;

            if (IsRunning) Cancel();

            body = owner;
            physics = owner.Physics;

            if (physics == null)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{BattleLog.Name(owner)}에 Physics가 없다 — 등장 연출을 건너뛴다", this);
                spec.onArrive?.Invoke();
                return;
            }

            landingGround = spec.landing;
            facing = spec.facing;
            height = Mathf.Max(0f, spec.height);
            duration = EntranceRules.ClampSeconds(spec.seconds);
            unscaled = spec.unscaled;
            onArrive = spec.onArrive;
            elapsed = 0f;

            // 두 끝점의 <b>월드</b> 좌표를 여기서 확정한다. 발판 높이가 자리마다 다를 수 있어
            // 지상 좌표만으로는 보간이 바닥을 뚫거나 뜬다. Teleport가 그 계산을 이미 갖고 있다.
            physics.Teleport(landingGround, height);
            landingWorld = physics.Transform.position;

            physics.Teleport(spec.start, height);
            startWorld = physics.Transform.position;

            // 지면 아래는 Teleport 로 못 잡는다(음수 높이를 0으로 물린다). 잡은 뒤에 내린다.
            startWorld.y -= Mathf.Max(0f, spec.startDepth);

            // 억제가 <b>먼저</b>다. 조준·판정을 켠 채로 한 프레임이라도 화면 밖에 서 있으면
            // 그 프레임에 맞거나, 그쪽으로 스킬이 나간다.
            if (spec.guard) guard = EntranceGuard.Arm(body);

            lockedControl = spec.lockControl;
            if (lockedControl) body.IsEntering = true;

            // 진행 방향을 보고 들어온다. 등을 보이고 날아오면 무엇이 오는지 안 읽힌다.
            Vector3 travel = landingGround - spec.start;
            physics.Face(facing.sqrMagnitude > 0.0001f ? facing : travel);

            IsRunning = true;
            enabled = true;
        }

        /// <summary>
        /// <b>지금 즉시</b> 착지시킨다. 남은 시간을 건너뛰고 도착 처리를 그대로 밟는다 —
        /// 콜백도 부른다.
        ///
        /// 연출이 끝나기를 기다릴 수 없는 입력이 있다. U키 카드가 그렇다:
        /// "0.35초 뒤에 다시 누르라"는 답이 될 수 없으므로, 부르는 쪽이 여기서 앞당긴다.
        /// </summary>
        public void Finish()
        {
            if (!IsRunning) return;
            Arrive();
        }

        /// <summary>
        /// 착지시키지 않고 멈춘다. <paramref name="restore"/>가 false면 억제·잠금을 그대로 둔다 —
        /// 곧바로 다음 연출이 이어 걸릴 때 한 프레임 판정이 새는 것을 막는다.
        /// <see cref="EntranceSpec.onArrive"/>는 부르지 않는다.
        /// </summary>
        public void Cancel(bool restore = true)
        {
            if (!IsRunning) return;

            IsRunning = false;
            enabled = false;
            onArrive = null;

            if (!restore) return;

            Unwind();
        }

        private void LateUpdate()
        {
            if (!IsRunning) return;

            elapsed += unscaled ? Time.unscaledDeltaTime : TimeControl.DeltaTime;

            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            if (t >= 1f)
            {
                Arrive();
                return;
            }

            // 중력이 이번 프레임에 먹인 하강을 되돌린다. 상승은 건드리지 않는다(StopFall 규칙).
            physics.StopFall();

            Vector3 p = EntranceRules.Sample(startWorld, landingWorld, t);

            physics.Transform.position = p;
            if (physics.Rigidbody != null) physics.Rigidbody.position = p;
        }

        private void Arrive()
        {
            IsRunning = false;
            enabled = false;

            // 마지막 한 번은 Teleport다. 접지·관성·낙하속도·PhysicsState를 통째로 정리하는 건
            // 이쪽뿐이라, 직접 쓴 좌표로 끝내면 넉백 속도나 Aerial 상태를 물고 착지한다.
            physics.Teleport(landingGround, height);

            if (facing.sqrMagnitude > 0.0001f) physics.Face(facing);

            Unwind();

            // 뒷정리가 <b>전부 끝난 다음</b> 부른다 — 콜백이 그 자리에서 다음 연출을
            // 이어 걸거나 몸을 내려도(SetActive(false)) 안전해야 한다.
            Action callback = onArrive;
            onArrive = null;
            callback?.Invoke();
        }

        /// <summary>잠금과 억제를 되돌린다. 두 번 불려도 안전하다.</summary>
        private void Unwind()
        {
            if (lockedControl && body != null) body.IsEntering = false;
            lockedControl = false;

            if (guard != null) guard.Release();
            guard = null;
        }

        /// <summary>
        /// 연출 도중 몸이 꺼지거나 파괴돼도 잠금을 남기지 않는다.
        /// 남기면 다시 섰을 때 <b>영영 조작이 안 되는 몸</b>이 된다.
        /// </summary>
        private void OnDisable()
        {
            if (!IsRunning) return;

            IsRunning = false;
            onArrive = null;
            Unwind();
        }
    }

    // ══ EntranceGuard ═══════════════════════════════════════════

    /// <summary>
    /// 화면 밖을 오가는 동안 <b>판정을 전부 끄고 배경 뒤로 숨긴다.</b> 진영을 가리지 않는다.
    ///
    /// 원래 <see cref="EnemySpawnGuard"/> 안에 적 전용으로만 있던 부분이다.
    /// 동료도 교대 · 시전으로 화면 밖을 오가게 되면서 같은 억제가 양쪽에 필요해졌고,
    /// 두 벌로 두면 <b>반드시 한쪽만 고쳐진다</b> — 그래서 여기 한 벌만 둔다.
    ///
    /// <b>끄는 이유는 양쪽에 다 있다.</b>
    /// <list type="bullet">
    /// <item>화면 밖의 몸이 맞는다 — 플레이어는 보이지도 않는 것을 때리고 있다.</item>
    /// <item>화면 밖의 몸을 향해 스킬이 나간다 — 조준이 화면 밖으로 새어 헛돈다.</item>
    /// </list>
    ///
    /// <b>붙잡은 수를 센다.</b> 아레나 스폰처럼 벽 연출(<see cref="EnemySpawnGuard"/>)과
    /// 비행(<see cref="EntrancePlayer"/>)이 겹쳐 두 주인이 동시에 억제를 걸 수 있는데,
    /// 먼저 끝난 쪽이 복구해 버리면 남은 연출이 판정을 켠 채로 돈다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EntranceGuard : MonoBehaviour
    {
        /// <summary>
        /// 숨는 동안 정렬 순서에 더하는 값.
        ///
        /// 배경(바닥 · 뒷벽)이 -10000 언저리를 쓰고 캐릭터는 -z·100이라 최저 -300이다.
        /// 그보다 확실히 뒤로 보내려면 한 자릿수 더 큰 음수여야 한다.
        /// </summary>
        public const int HiddenSortingOffset = -30000;

        private Entity body;
        private BeltScrollView view;
        private Collider[] bodyColliders;

        /// <summary>억제를 붙잡고 있는 주인 수. 0이 되는 순간 복구한다.</summary>
        private int holds;

        public bool IsArmed => holds > 0;

        /// <summary>
        /// 억제를 건다. 이미 걸려 있으면 <b>수만 하나 올린다</b> —
        /// 두 주인이 겹쳐도 마지막 하나가 놓을 때까지 유지된다.
        /// </summary>
        public static EntranceGuard Arm(Entity target, bool hide = true)
        {
            if (target == null) return null;

            var guard = target.GetComponent<EntranceGuard>();
            if (guard == null) guard = target.gameObject.AddComponent<EntranceGuard>();

            guard.Resolve(target);
            guard.holds++;

            // 조준 후보에서 뺀다. 판정만 끄면 스킬이 여전히 그쪽으로 나가 헛돈다.
            target.IsTargetable = false;

            // 몸통 콜라이더가 곧 피격 판정이다. 끄면 벽도 통과하는데,
            // 방 밖에서 들어오는 중이니 그게 맞다 — 벽에 걸리면 영영 못 들어온다.
            guard.SetBodyColliders(false);

            if (hide && guard.view != null) guard.view.SortingOffset = HiddenSortingOffset;

            return guard;
        }

        /// <summary>
        /// 정렬만 앞으로 되돌린다. 판정은 아직 꺼진 채다.
        /// 벽에서 걸어 나오는 적이 진입선을 넘는 순간 쓴다 — 몸은 보이지만 아직 연출 구간이다.
        /// </summary>
        public void Reveal()
        {
            if (view != null) view.SortingOffset = 0;
        }

        /// <summary>
        /// 붙잡은 것을 하나 놓는다. 마지막 하나였으면 전부 되돌린다.
        /// <b>두 번 불려도 안전하다.</b>
        /// </summary>
        public void Release()
        {
            if (holds <= 0) return;
            if (--holds > 0) return;

            Restore();
        }

        private void Restore()
        {
            holds = 0;

            Reveal();
            SetBodyColliders(true);

            if (body != null) body.IsTargetable = true;
        }

        private void Awake() => Resolve(GetComponent<Entity>());

        private void Resolve(Entity target)
        {
            if (body == null) body = target != null ? target : GetComponent<Entity>();
            if (view == null) view = GetComponent<BeltScrollView>();
            if (bodyColliders == null) bodyColliders = GetComponents<Collider>();
        }

        /// <summary>
        /// 몸통 콜라이더만 만진다. 자식(<see cref="Attack"/> 히트박스)은 건드리지 않는다 —
        /// 그쪽은 평소에도 꺼져 있고 휘두를 때만 켜지는데, 여기서 강제로 켜면
        /// 연출 직후에 판정이 한 프레임 새어 나간다.
        /// </summary>
        private void SetBodyColliders(bool on)
        {
            if (bodyColliders == null) return;

            for (int i = 0; i < bodyColliders.Length; i++)
            {
                Collider c = bodyColliders[i];
                if (c == null || c.isTrigger) continue;   // 트리거는 히트박스다
                c.enabled = on;
            }
        }

        /// <summary>
        /// 파괴 · 씬 언로드로 잘려도 "영영 안 잡히는 몸"을 조준 목록에 남기지 않는다.
        /// <see cref="EnemySpawnGuard"/>가 갖고 있던 안전망을 그대로 옮겼다.
        /// </summary>
        private void OnDisable()
        {
            if (holds > 0) Restore();
        }
    }
}
