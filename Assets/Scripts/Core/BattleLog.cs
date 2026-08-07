using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Prototype
{
    [Flags]
    public enum LogCategory
    {
        None = 0,
        State = 1 << 0,   // 상태머신 전이
        Combat = 1 << 1,  // 타격 · 피격 · 사망
        Physics = 1 << 2, // 착지 · 벽 접촉 · 넉백
        Skill = 1 << 3,   // 스킬 발동 · 다단 히트 · 효과
        Deck = 1 << 4,    // 드로우 · 셔플 · 소멸
        Bullet = 1 << 5,  // 불릿타임 진입 / 해제
        Combo = 1 << 6,   // 슬롯 배치 · 큐 실행 · 재타겟
        Predict = 1 << 7, // 예측 · 헛침 경고
        Qte = 1 << 8,     // 콤보 QTE 판정

        All = ~0,
    }

    /// <summary>
    /// 콘솔 디버그 출력. 카테고리별로 껐다 켤 수 있다.
    /// 릴리즈 빌드에서는 호출 자체가 사라진다 — 인자 계산 비용도 남지 않는다.
    /// </summary>
    public static class BattleLog
    {
        /// <summary>켜 둘 카테고리. <see cref="BattleLogSettings"/>로 인스펙터에서 바꾼다.</summary>
        public static LogCategory Mask = LogCategory.All;

        /// <summary>프레임 번호를 앞에 붙인다. 한 프레임에 몰린 이벤트를 구분할 때.</summary>
        public static bool ShowFrame = true;

        public static bool IsEnabled(LogCategory c) => (Mask & c) != 0;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), HideInCallstack]
        public static void Log(LogCategory category, string message, UnityEngine.Object context = null)
        {
            if ((Mask & category) == 0) return;
            Debug.Log(Format(category, message), context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), HideInCallstack]
        public static void Warn(LogCategory category, string message, UnityEngine.Object context = null)
        {
            if ((Mask & category) == 0) return;
            Debug.LogWarning(Format(category, message), context);
        }

        private static string Format(LogCategory category, string message)
        {
            string tag = $"<b><color={ColorOf(category)}>[{category}]</color></b>";
            return ShowFrame ? $"{tag} <color=#808080>f{Time.frameCount}</color> {message}" : $"{tag} {message}";
        }

        private static string ColorOf(LogCategory c)
        {
            switch (c)
            {
                case LogCategory.State: return "#7FDBFF";
                case LogCategory.Combat: return "#FF6B6B";
                case LogCategory.Physics: return "#B0B0B0";
                case LogCategory.Skill: return "#FFD166";
                case LogCategory.Deck: return "#C792EA";
                case LogCategory.Bullet: return "#00E5FF";
                case LogCategory.Combo: return "#8AFF80";
                case LogCategory.Predict: return "#F78C6C";
                case LogCategory.Qte: return "#FF6FCF";
                default: return "white";
            }
        }

        /// <summary>로그용 짧은 이름. null이면 "(null)".</summary>
        public static string Name(UnityEngine.Object o) => o != null ? o.name : "(null)";

        /// <summary>상태 인스턴스의 타입 이름. "IdleState" 처럼 찍힌다.</summary>
        public static string StateName(IState s) => s != null ? s.GetType().Name : "(none)";
    }
}
