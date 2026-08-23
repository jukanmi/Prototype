using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// <b>경계 소유자.</b> 지금 이 스테이지의 유효 구간이 어디인지를 아는 단 하나의 자리다.
    ///
    /// 카메라도 스폰 계획도 여기서 경계를 얻는다. 라운드마다 바뀌는 것은 <b>이 안에 든
    /// 경계값 하나뿐</b>이고, 읽는 쪽에는 "아레나인가 통로인가" 하는 분기가 없다.
    ///
    /// 두 벌을 들고 있다는 점이 중요하다.
    /// <list type="bullet">
    /// <item><see cref="CameraMin"/>/<see cref="CameraMax"/> — <b>보간된</b> 값. 전환이 부드럽게 보이도록.</item>
    /// <item><see cref="UnitMin"/>/<see cref="UnitMax"/> — <b>원본</b> 아레나 경계. 즉시 바뀐다.
    /// 유닛 배치·스폰 계획이 이쪽을 쓴다. 보간 중인 카메라 경계로 자리를 잡으면
    /// 전환 0.3초 동안 소환된 적이 화면 밖 엉뚱한 자리에 선다.</item>
    /// </list>
    /// </summary>
    public class StageBounds : MonoBehaviour
    {
        /// <summary>
        /// 씬에 하나. 카메라가 <c>FindAnyObjectByType</c>을 매 프레임 돌지 않게 열어 둔다.
        /// 없으면 null — 경계가 없는 씬(SampleScene · 훈련장)이 그렇고, 그 씬들은 그냥 안 쓴다.
        /// </summary>
        public static StageBounds Instance { get; private set; }

        [Tooltip("경계 전환에 쓰는 시간. 0.3초 근처가 적당하다 — 길면 늘어지고 짧으면 뚝 끊긴다.")]
        [SerializeField] private float blendSeconds = BoundsBlend.DefaultDuration;

        private BoundsBlend blend;

        /// <summary>카메라가 봐도 되는 구간(보간된 값).</summary>
        public float CameraMin => blend.Min;
        public float CameraMax => blend.Max;

        /// <summary>유닛 배치·스폰이 쓰는 원본 구간. 보간하지 않는다.</summary>
        public float UnitMin { get; private set; }
        public float UnitMax { get; private set; }

        public bool IsBlending => blend.IsBlending;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 경계를 갈아 끼운다. 카메라는 <paramref name="blendSeconds"/>에 걸쳐 따라오고,
        /// 유닛 경계는 즉시 바뀐다.
        /// </summary>
        public void SwitchTo(float min, float max)
        {
            UnitMin = min;
            UnitMax = max;
            blend.To(min, max, blendSeconds);
        }

        /// <summary>보간 없이 맞춘다. 스테이지가 처음 올라올 때 한 번.</summary>
        public void Snap(float min, float max)
        {
            UnitMin = min;
            UnitMax = max;
            blend.Snap(min, max);
        }

        /// <summary>
        /// <b>LateUpdate</b>에서 굴린다 — 카메라도 LateUpdate에서 읽으므로,
        /// 여기가 Update면 카메라가 한 프레임 늦은 경계를 보고 전환 내내 미세하게 떤다.
        /// 스크립트 실행 순서에 기대지 않도록 카메라 쪽에서 한 번 더 밀어 준다
        /// (<see cref="TickBlend"/>).
        /// </summary>
        private void LateUpdate() => TickBlend();

        /// <summary>
        /// 보간을 한 프레임 진행한다. 같은 프레임에 두 번 불려도 안전하도록
        /// 프레임 번호를 기억한다 — 카메라가 자기 LateUpdate에서 먼저 부를 수 있다.
        ///
        /// <b>스케일 안 된 시간</b>을 쓴다. 라운드가 끝나는 순간은 마지막 적이 죽는 순간이라
        /// 불릿타임이 걸려 있기 쉬운데, 게임 시간으로 돌리면 그 동안 카메라가 굳는다.
        /// </summary>
        public void TickBlend()
        {
            if (tickedFrame == Time.frameCount) return;
            tickedFrame = Time.frameCount;

            blend.Tick(Time.unscaledDeltaTime);
        }

        private int tickedFrame = -1;
    }
}
