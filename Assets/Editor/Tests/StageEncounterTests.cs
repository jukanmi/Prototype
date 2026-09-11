using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 목록 한 칸이 <b>스스로 접는 값</b>들. 저작이 성립하지 않는 조합을 런타임이 어떻게 읽는가.
    ///
    /// 접는 이유는 늘 같다 — <b>스테이지가 그 자리에서 멈추는 것보다 낫다.</b>
    /// 저작값 자체는 손대지 않으므로, 보드의 저작 검증이 같은 자리를 따로 짚어 준다.
    ///
    /// 계획서: docs/Stage_Encounter_Unification_Plan.md (2항)
    /// </summary>
    public class StageEncounterTests
    {
        /// <summary>
        /// 자리 없는 조우에 진입선 조건이 박혀 있으면 <b>넘을 선이 없어 영영 안 열린다.</b>
        /// 앞 조우 뒤로 접는다.
        /// </summary>
        [Test]
        public void CrossLineWithoutASite_FoldsToAfterPrevious()
        {
            var encounter = new StageEncounter
            {
                content = null,
                trigger = EncounterTrigger.CrossLine,
                site = null,
            };

            Assert.That(encounter.trigger, Is.EqualTo(EncounterTrigger.CrossLine), "저작값은 남아야 한다");
            Assert.That(encounter.Trigger, Is.EqualTo(EncounterTrigger.AfterPrevious));
            Assert.That(encounter.WaitsForPrevious, Is.True);
        }

        /// <summary>자리가 있으면 진입선 조건이 그대로 선다. 아레나가 이 경로로 돈다.</summary>
        [Test]
        public void CrossLineWithASite_StaysCrossLine()
        {
            var host = new GameObject("자리");

            try
            {
                var encounter = new StageEncounter
                {
                    trigger = EncounterTrigger.CrossLine,
                    site = host.AddComponent<EncounterSite>(),
                };

                Assert.That(encounter.HasSite, Is.True);
                Assert.That(encounter.Trigger, Is.EqualTo(EncounterTrigger.CrossLine));
                Assert.That(encounter.WaitsForPrevious, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>음수로 저작된 틈은 0으로 본다.</summary>
        [Test]
        public void NegativeGap_FoldsToZero()
        {
            var encounter = new StageEncounter { gapSeconds = -3f };

            Assert.That(encounter.GapSeconds, Is.Zero);
        }

        [Test]
        public void AuthoredGap_IsKeptAsIs()
        {
            var encounter = new StageEncounter { gapSeconds = 2.5f };

            Assert.That(encounter.GapSeconds, Is.EqualTo(2.5f).Within(0.0001f));
        }

        /// <summary>
        /// 기본 생성한 칸도 굴러가야 한다. 인스펙터에서 목록 크기를 늘리면 이 상태가 된다.
        /// 즉시가 0번이라 새 칸은 "바로 열리는 빈 조우"이고, 빈 칸은 디렉터가 걷어 낸다.
        /// </summary>
        [Test]
        public void DefaultSlot_IsImmediateAndEmpty()
        {
            var encounter = new StageEncounter();

            Assert.That((int)EncounterTrigger.Immediate, Is.EqualTo(0));
            Assert.That(encounter.Trigger, Is.EqualTo(EncounterTrigger.Immediate));
            Assert.That(encounter.content, Is.Null);
            Assert.That(encounter.HasSite, Is.False);
            Assert.That(encounter.GapSeconds, Is.Zero);
        }

        /// <summary>
        /// 시작 조건은 애셋에 <b>정수로</b> 저장된다. 중간에 끼우면 이미 저작된 조우의
        /// 조건이 통째로 밀린다 — 화면으로는 "아레나가 걸어가기도 전에 열린다"로 보인다.
        /// </summary>
        [Test]
        public void TriggerOrdinals_AreStable()
        {
            Assert.That((int)EncounterTrigger.Immediate, Is.EqualTo(0));
            Assert.That((int)EncounterTrigger.AfterPrevious, Is.EqualTo(1));
            Assert.That((int)EncounterTrigger.CrossLine, Is.EqualTo(2));
        }
    }
}
