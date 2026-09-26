using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

namespace Prototype
{
    // ══ GameplayModal ═══════════════════════════════════════════

    /// <summary>
    /// 게임플레이를 <b>멈춰 세워야 하는</b> 전면 UI가 떠 있는가.
    ///
    /// 스테이지 진행(웨이브 시계 · 아레나 정비 시계)과 승패 판정이 각자 개별 UI를 알게 두면,
    /// 모달이 하나 늘 때마다 세 군데를 똑같이 고쳐야 하고 한 군데를 빠뜨리면
    /// "카드를 고르는 사이에 등 뒤에서 웨이브가 쏟아지는" 식으로만 드러난다.
    /// 그래서 물어보는 창구를 하나로 둔다.
    /// </summary>
    public static class GameplayModal
    {
        private static int cancelConsumedFrame = -1;

        public static bool IsOpen => LevelUpSession.IsOpen || DeckBuilderUI.IsOpen;

        /// <summary>
        /// 이번 프레임의 ESC를 모달이 이미 썼는가.
        ///
        /// <see cref="IsOpen"/>만으로는 모자란다 — 스크립트 실행 순서는 정해져 있지 않아서,
        /// 모달이 ESC로 닫힌 프레임에 스테이지 쪽 Update가 <b>나중에</b> 돌면 이미 닫힌 창을 보고
        /// "모달 없음 + ESC 눌림"으로 읽어 런을 통째로 버리고 메인 메뉴로 나간다.
        /// </summary>
        public static bool CancelConsumedThisFrame => cancelConsumedFrame == Time.frameCount;

        /// <summary>모달이 ESC로 닫혔다. 닫기 <b>직전</b>에 부른다.</summary>
        public static void ConsumeCancel() => cancelConsumedFrame = Time.frameCount;
    }
}
