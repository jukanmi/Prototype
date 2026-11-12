using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    public enum TimeDomain { Battle, Cast }
    /// <summary>
    /// 게임플레이 전용 시간 배율. 결정 로그 ⑥ — Time.timeScale은 쓰지 않는다.
    /// UI와 애니메이션까지 멈추면 불릿타임 중 손패 조작이 불가능하기 때문.
    /// </summary>
    public static class TimeControl
    {
        /// <summary>0이면 완전 정지. BulletTimeController가 갱신한다.</summary>
        public static float Scale { get; set; } = 1f;

        /// <summary>엔티티 · 상태머신 · 물리에 주입할 dt.</summary>
        public static float DeltaTime => Time.deltaTime * Scale;

        public static float ScaleFor(TimeDomain domain) => domain == TimeDomain.Cast ? 1f : Scale;
        public static float DeltaFor(TimeDomain domain) => Time.deltaTime * ScaleFor(domain);
        public static float FixedDeltaFor(TimeDomain domain) => Time.fixedDeltaTime * ScaleFor(domain);

        /// <summary>UI · 조준 · 입력용 dt. 배율 영향을 받지 않는다.</summary>
        public static float UnscaledDeltaTime => Time.deltaTime;

        public static bool IsFrozen => Scale <= 0.0001f;

        public static bool AssistResolutionActive { get; private set; }
        public static int AssistResolutionVersion { get; private set; }

        public static void BeginAssistResolution()
        {
            AssistResolutionVersion++;
            AssistResolutionActive = true;
        }

        public static void EndAssistResolution() => AssistResolutionActive = false;

        public static void Reset()
        {
            EndAssistResolution();
            Scale = 1f;
        }

        /// <summary>
        /// Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.
        /// 불릿타임 중에 정지하면 Scale이 0인 채로 남아 다음 플레이가 멈춘 상태로 시작한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }

    // ══ GameplayModal ═══════════════════════════════════════════════
    // "지금 게임을 멈추는 창이 떠 있는가". 여는 쪽(LevelUpSession · DeckBuilderUI · Run 층)이
    // 자기 열림 여부를 등록하고, 멈춰야 하는 쪽(StageDirector · BattleSceneController)은 IsOpen만 본다.
    public static class GameplayModal
    {
        private static readonly List<Func<bool>> sources = new List<Func<bool>>();
        private static int cancelConsumedFrame = -1;

        /// <summary>모달 하나의 "지금 열려 있나". 같은 함수를 두 번 등록해도 한 번만 남는다.</summary>
        public static void Register(Func<bool> isOpen)
        {
            if (isOpen != null && !sources.Contains(isOpen)) sources.Add(isOpen);
        }

        public static bool IsOpen
        {
            get
            {
                for (int i = 0; i < sources.Count; i++)
                    if (sources[i]()) return true;
                return false;
            }
        }

        public static bool CancelConsumedThisFrame => cancelConsumedFrame == Time.frameCount;

        public static void ConsumeCancel() => cancelConsumedFrame = Time.frameCount;
    }
}
