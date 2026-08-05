using UnityEngine;

namespace Prototype.YG
{
    /// <summary>
    /// 런(run) 단위 데이터만 소유한다. 씬 오브젝트는 절대 참조하지 않는다.
    /// 씬이 언로드되는 순간 그런 참조는 전부 무효가 되기 때문이다.
    /// Boot 씬에 상주하며 Boot 씬은 언로드되지 않으므로 DontDestroyOnLoad 는 쓰지 않는다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ── 런 데이터 (프로토타입 단계 최소 구성)
        public int  CurrentStageIndex { get; private set; }
        public int  TotalExp          { get; private set; }
        public bool IsRunActive       { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>메인화면 [시작] 클릭 시 호출. 런 상태를 초기값으로 되돌린다.</summary>
        public void StartNewRun()
        {
            CurrentStageIndex = 0;
            TotalExp          = 0;
            IsRunActive       = true;

            RestoreTime();

            Debug.Log("[GameManager] 새 런 시작");
        }

        /// <summary>ESC 이탈 또는 패배 시 호출. 런 데이터를 폐기한다.</summary>
        public void EndRun()
        {
            IsRunActive = false;

            RestoreTime();

            Debug.Log("[GameManager] 런 종료");
        }

        public void AddExp(int amount) => TotalExp += amount;
        public void AdvanceStage()     => CurrentStageIndex++;

        /// <summary>
        /// 이 프로젝트의 일시정지는 Time.timeScale 이 아니라 TimeControl.Scale 이다
        /// (불릿타임 중 UI · 조준은 계속 돌아야 하므로). 불릿타임 도중 이탈하면
        /// Scale 이 0 인 채로 남아 다음 런이 멈춘 상태로 시작하므로 여기서 되돌린다.
        /// Time.timeScale 은 외부 코드가 건드렸을 경우를 대비한 보험이다.
        /// </summary>
        private static void RestoreTime()
        {
            TimeControl.Reset();
            Time.timeScale = 1f;
        }
    }
}
