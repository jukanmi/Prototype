using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 발판 등록과 질의. <b>가장 중요한 규칙은 "비었을 때"다</b> —
    /// 발판을 아직 안 깐 씬이 판정 도입만으로 바닥을 잃으면 안 된다.
    /// </summary>
    public class GroundRegistryTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp() => GroundRegistry.Clear();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            GroundRegistry.Clear();
        }

        /// <summary>크기를 직접 적은 발판. Renderer 없이도 선다.</summary>
        private GroundPlate NewPlate(Vector3 center, float halfX, float halfZ)
        {
            var go = new GameObject("Plate");
            spawned.Add(go);
            go.transform.position = center;

            var plate = go.AddComponent<GroundPlate>();

            var so = new UnityEditor.SerializedObject(plate);
            so.FindProperty("halfExtents").vector2Value = new Vector2(halfX, halfZ);
            so.ApplyModifiedPropertiesWithoutUndo();

            return plate;
        }

        // ── 비었을 때 ───────────────────────────────────

        /// <summary>
        /// <b>회귀 방지의 핵심.</b> 발판을 안 깐 씬(테스트 씬 · 훈련장)은 예전처럼
        /// 무한 평면이어야 한다. 여기서 false가 나오면 그 씬의 유닛이 전부 허공에 뜬다.
        /// </summary>
        [Test]
        public void Empty_SupportsEverywhere()
        {
            Assert.That(GroundRegistry.HasPlates, Is.False);
            Assert.That(GroundRegistry.Supports(new Vector3(999f, 0f, -999f)), Is.True);
        }

        [Test]
        public void Empty_HeightFallsBackToTheGivenValue()
        {
            float y = GroundRegistry.HeightAt(new Vector3(999f, 0f, 0f), fallback: -1.25f);

            Assert.That(y, Is.EqualTo(-1.25f).Within(0.0001f));
        }

        // ── 등록 ────────────────────────────────────────

        [Test]
        public void Plate_RegistersItselfOnEnable()
        {
            NewPlate(Vector3.zero, 6f, 3f);

            Assert.That(GroundRegistry.Count, Is.EqualTo(1));
            Assert.That(GroundRegistry.HasPlates, Is.True);
        }

        [Test]
        public void Plate_UnregistersOnDisable()
        {
            GroundPlate p = NewPlate(Vector3.zero, 6f, 3f);

            p.enabled = false;

            Assert.That(GroundRegistry.Count, Is.Zero);
        }

        // ── 질의 ────────────────────────────────────────

        [Test]
        public void InsideThePlate_IsSupported()
        {
            NewPlate(Vector3.zero, 6f, 3f);

            Assert.That(GroundRegistry.Supports(new Vector3(5.9f, 0f, 2.9f)), Is.True);
        }

        /// <summary>판을 깔면 그 밖은 딛을 것이 없다. 낙차는 여기서 시작한다.</summary>
        [Test]
        public void OutsideThePlate_IsNotSupported()
        {
            NewPlate(Vector3.zero, 6f, 3f);

            Assert.That(GroundRegistry.Supports(new Vector3(6.1f, 0f, 0f)), Is.False);
            Assert.That(GroundRegistry.Supports(new Vector3(0f, 0f, 3.1f)), Is.False);
        }

        [Test]
        public void OutsideThePlate_HeightFallsBack()
        {
            NewPlate(Vector3.zero, 6f, 3f);

            float inside = GroundRegistry.HeightAt(new Vector3(0f, 0f, 0f), fallback: -99f);
            float outside = GroundRegistry.HeightAt(new Vector3(20f, 0f, 0f), fallback: -99f);

            Assert.That(inside, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(outside, Is.EqualTo(-99f).Within(0.0001f));
        }

        /// <summary>통로는 판을 이어 붙여 만든다. 이음매에서 구멍이 나면 안 된다.</summary>
        [Test]
        public void AdjacentPlates_CoverTheSeam()
        {
            NewPlate(new Vector3(-6f, 0f, 0f), 6f, 3f);   // x [-12 .. 0]
            NewPlate(new Vector3(6f, 0f, 0f), 6f, 3f);    // x [  0 .. 12]

            Assert.That(GroundRegistry.Supports(new Vector3(0f, 0f, 0f)), Is.True, "이음매");
            Assert.That(GroundRegistry.Supports(new Vector3(-11.9f, 0f, 0f)), Is.True);
            Assert.That(GroundRegistry.Supports(new Vector3(11.9f, 0f, 0f)), Is.True);
            Assert.That(GroundRegistry.Supports(new Vector3(12.1f, 0f, 0f)), Is.False, "통로 끝");
        }

        [Test]
        public void HigherPlate_WinsWhenStacked()
        {
            NewPlate(Vector3.zero, 6f, 3f);
            NewPlate(new Vector3(0f, 1.5f, 0f), 2f, 2f);

            float y = GroundRegistry.HeightAt(new Vector3(0f, 5f, 0f), fallback: -99f);

            Assert.That(y, Is.EqualTo(1.5f).Within(0.0001f));
        }
    }
}
