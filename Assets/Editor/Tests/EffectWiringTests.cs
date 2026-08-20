using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 이펙트 배선은 <b>조용히</b> 깨진다. 시트를 다시 슬라이스하면 서브에셋 fileId가 바뀌고,
    /// 클립이 물고 있던 프레임이 null 배열로 남는다 — 에러 없이 아무것도 안 뜬다.
    /// <see cref="VfxClip.IsValid"/>는 배열 길이만 보므로 null 원소도 잡아 준다.
    ///
    /// 아트가 아직 안 들어온 슬롯은 통과시킨다. 비어 있는 것은 폴백이지 버그가 아니다
    /// (<see cref="VfxLibrary"/> 주석). 반대로 <b>채워졌는데 깨진 것</b>은 전부 실패다.
    /// </summary>
    public class EffectWiringTests
    {
        private const string LibraryPath = "Assets/Data/Resources/VfxLibrary.asset";

        [Test]
        public void VfxLibrary_ExistsInResources()
        {
            var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            Assert.That(lib, Is.Not.Null,
                $"{LibraryPath} 가 없다. VfxLibrary.Get()이 Resources에서 이 이름으로 찾는다.");
        }

        [Test]
        public void EveryVfxClipAsset_HasUsableFrames()
        {
            VfxClip[] clips = LoadAll<VfxClip>();
            Assert.That(clips, Is.Not.Empty, "VfxClip 에셋이 하나도 없다.");

            foreach (VfxClip clip in clips)
            {
                Assert.That(clip.IsValid, Is.True, $"{clip.name}: 프레임이 비었다.");
                Assert.That(clip.frames.Any(f => f == null), Is.False,
                    $"{clip.name}: 프레임에 null이 섞였다 — 시트를 다시 슬라이스하면서 참조가 끊긴 것이다.");
                Assert.That(clip.fps, Is.GreaterThan(0f), $"{clip.name}: fps가 0 이하다.");
                Assert.That(clip.Duration, Is.GreaterThan(0f), $"{clip.name}: 재생 시간이 0이다.");
            }
        }

        /// <summary>
        /// 한 클립의 프레임은 <b>같은 시트의 같은 행</b>에서 와야 한다.
        /// 행이 섞이면 프레임마다 색이 튄다 — 이 팩은 세로 9칸이 전부 색 변형이라
        /// 통짜 슬라이스 + 이름순 정렬로 만들면 반드시 이렇게 된다.
        /// </summary>
        [Test]
        public void ClipFrames_ComeFromOneSheetRow()
        {
            foreach (VfxClip clip in LoadAll<VfxClip>())
            {
                if (!clip.IsValid) continue;

                string[] rows = clip.frames
                    .Where(f => f != null)
                    .Select(RowKeyOf)
                    .Where(k => k != null)
                    .Distinct()
                    .ToArray();

                // 행 표기가 없는 옛 시트(Rings)는 검사 대상이 아니다.
                if (rows.Length == 0) continue;

                Assert.That(rows.Length, Is.EqualTo(1),
                    $"{clip.name}: 프레임이 여러 행에서 왔다 ({string.Join(", ", rows)}). 색이 프레임마다 튄다.");
            }
        }

        /// <summary>프레임 순서가 열 번호와 같아야 한다. 문자열 정렬은 _10을 _2 앞에 놓는다.</summary>
        [Test]
        public void ClipFrames_AreInColumnOrder()
        {
            foreach (VfxClip clip in LoadAll<VfxClip>())
            {
                if (!clip.IsValid) continue;

                // 행 표기가 있는 시트(Effect 팩)만 검사한다. 옛 시트는 손으로 순서를 잡았다 —
                // UI_ChargeGauge는 일부러 가득 참 → 빈 상태로 거꾸로 넣는다.
                Sprite[] frames = clip.frames.Where(f => f != null && RowKeyOf(f) != null).ToArray();
                if (frames.Length != clip.frames.Length || frames.Length == 0) continue;

                int[] cols = frames.Select(ColumnOf).ToArray();

                for (int i = 1; i < cols.Length; i++)
                    Assert.That(cols[i], Is.GreaterThan(cols[i - 1]),
                        $"{clip.name}: 프레임 순서가 어긋났다 (…{cols[i - 1]}, {cols[i]}…).");
            }
        }

        /// <summary>
        /// 스킬이 전용 시트를 물었다면 그 시트가 살아 있어야 한다.
        /// 비어 있는 것은 전역 폴백이므로 통과다.
        /// </summary>
        [Test]
        public void SkillClipSlots_AreEitherEmptyOrValid()
        {
            SkillData[] skills = LoadAll<SkillData>();
            Assert.That(skills, Is.Not.Empty, "SkillData 에셋이 하나도 없다.");

            foreach (SkillData s in skills)
            {
                AssertSlot(s, "hitClip", s.vfx.hitClip);
                AssertSlot(s, "castClip", s.vfx.castClip);
            }
        }

        private static void AssertSlot(SkillData skill, string field, VfxClip clip)
        {
            if (clip == null) return;

            Assert.That(clip.IsValid, Is.True, $"{skill.name}.{field}: {clip.name}의 프레임이 비었다.");
            Assert.That(clip.frames.Any(f => f == null), Is.False,
                $"{skill.name}.{field}: {clip.name}의 프레임에 null이 섞였다.");
        }

        /// <summary>"03_r5_07" → "03_r5". 행 표기가 없으면 null.</summary>
        private static string RowKeyOf(Sprite sprite)
        {
            int i = sprite.name.LastIndexOf("_r", System.StringComparison.Ordinal);
            if (i < 0) return null;

            int end = sprite.name.IndexOf('_', i + 2);
            return end < 0 ? null : sprite.name.Substring(0, end);
        }

        /// <summary>이름 끝의 열 번호. 없으면 -1.</summary>
        private static int ColumnOf(Sprite sprite)
        {
            int i = sprite.name.LastIndexOf('_');
            return i >= 0 && int.TryParse(sprite.name.Substring(i + 1), out int col) ? col : -1;
        }

        private static T[] LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToArray();
    }
}
