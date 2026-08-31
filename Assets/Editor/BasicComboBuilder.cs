using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 평타 3연타(약 · 중 · 강)에 쓸 클립을 굽는다.
    ///
    /// <b>프리팹은 안 건드린다.</b> 단계 표(<see cref="BasicAttackStage"/>)는 Player · Ally
    /// 프리팹에 이미 들어가 있고, 거기가 원본이다 — 손으로 튜닝한 값을 생성기가 되돌리면
    /// 인스펙터에서 고친 손맛이 메뉴 한 번에 조용히 사라진다. 값을 바꿀 일은 인스펙터에서 한다.
    /// 프리팹에 단계가 들어 있는지는 <c>BasicComboPrefabTests</c>가 지킨다.
    ///
    /// <b><see cref="ArtImportBuilder"/>의 "전부 임포트" 메뉴를 쓰지 않는 이유</b>:
    /// 그쪽은 (1) 프로젝트 <b>바깥</b> 폴더에서 시트를 복사해 오고,
    /// (2) 이미 삭제된 <c>AllyAnimator.controller</c>를 다시 만들어 <c>Ally.prefab</c>을
    /// 거기로 옮겨 붙인다 — 지금 돌리면 애니메이터 배선이 갈라진다.
    /// 여기서는 <b>이미 프로젝트 안에 슬라이스돼 있는</b> 시트로 클립만 굽고,
    /// 컨트롤러는 손대지 않는다(단계별 클립은 런타임 오버라이드로 꽂힌다 —
    /// <see cref="EntityAnimator.PlayBasicAttackStage"/>).
    ///
    /// 멱등이다. 두 번 돌려도 클립 내용만 갱신된다.
    /// </summary>
    public static class BasicComboBuilder
    {
        /// <summary>경로가 아니라 컴포넌트로 찾는다 — 프리팹을 옮겨도 안 끊긴다.</summary>
        internal static string[] TargetPrefabs
            => new[] { PrefabLocator.PlayerPath, PrefabLocator.AllyPath };

        /// <summary>단계 수. 에셋 시트가 3장이라 3타다.</summary>
        internal const int StageCount = 3;

        internal const string Stage1Clip = "Ally_Attack";    // 이미 있다(기본 평타)
        internal const string Stage2Clip = "Ally_Attack2";
        internal const string Stage3Clip = "Ally_Attack3";

        /// <summary>
        /// 3타가 쓰는 <c>_AttackCombo2hit</c> 시트의 프레임 구간. 10프레임이 두 스윙으로
        /// 나뉘어 있고(0~4 내려베기 · 5~9 되돌려베기) <b>앞 스윙만</b> 쓴다.
        /// 히트박스는 3타에 한 번(0.18~0.34초)만 열리므로 그림도 한 번이어야 한다.
        /// </summary>
        internal const int Stage3From = 0;
        internal const int Stage3To = 4;

        [MenuItem("Prototype/평타 - 3연타 클립 굽기")]
        public static void Build()
        {
            BakeClips();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BasicComboBuilder] {Stage2Clip} · {Stage3Clip} 을 다시 구웠다. " +
                      "단계 표는 프리팹 인스펙터에서 고칠 것.");
        }

        /// <summary>
        /// 2·3타 클립을 굽는다. 1타는 기존 <c>Ally_Attack</c>을 그대로 쓴다.
        ///
        /// <b>3타는 시트의 앞 스윙만 쓴다</b>(<see cref="Stage3To"/>). <c>_AttackCombo2hit</c>은
        /// 이름 그대로 10프레임에 <b>두 번</b> 휘두르는 그림이라, 통째로 구우면 판정은
        /// <see cref="BasicAttackStage"/> 한 벌(3타)인데 화면은 네 번 휘두른다 —
        /// 찍기 · 횡베기 · 찍기 · 횡베기. 그림이 <see cref="HitData"/>를 따라가야 한다.
        ///
        /// 최종 길이는 <see cref="EntityAnimator.PlayBasicAttackStage"/>가 단계 total(0.70초)에
        /// 맞춰 늘리므로 여기 fps는 프레임 <b>분배</b>만 정한다. 균등 분배라 값 자체는 화면에 안 보인다.
        /// </summary>
        private static void BakeClips()
        {
            ArtImportBuilder.BuildSpriteClip(new ArtImportBuilder.ClipSpec("Attack2", "_Attack2", 14f, false));
            ArtImportBuilder.BuildSpriteClip(
                new ArtImportBuilder.ClipSpec("Attack3", "_AttackCombo2hit", 11f, false, Stage3From, Stage3To));
        }
    }
}
