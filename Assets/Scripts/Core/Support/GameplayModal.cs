// "지금 게임을 멈추는 창이 떠 있는가". 여는 쪽(LevelUpSession · DeckBuilderUI · Run 층)이
// 자기 열림 여부를 등록하고, 멈춰야 하는 쪽(StageDirector · BattleSceneController)은 IsOpen만 본다.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
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
