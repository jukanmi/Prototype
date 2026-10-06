// 런 지도의 모양 = 애셋 하나. 층 목록과 층마다 뽑을 후보.
// 모델과 생성 규칙은 RunMap.cs에 있다. 이 파일은 유니티가 애셋 타입마다 자기 이름의 파일을 요구해서 따로 있다.
// 계획서: docs/Run_Map_Plan.md (2.2)

using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지도를 굴릴 재료. 값을 바꾸는 데 코드 수정이 필요 없다.
    ///
    /// <b>층 수는 <see cref="floors"/>의 길이다.</b> 일렬 진행(디버그 경로)은 층마다 칸이 하나인 레시피일 뿐이라
    /// 모델을 따로 두지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "RunMap_", menuName = "Prototype/런 지도 레시피", order = 40)]
    public class RunMapRecipe : ScriptableObject
    {
        [Tooltip("위에서 아래로 0층 → 보스 층. 마지막 층은 보스 한 칸이어야 한다.")]
        public FloorRule[] floors = new FloorRule[0];

        public IReadOnlyList<FloorRule> Floors => floors;

        public int FloorCount => floors != null ? floors.Length : 0;

        /// <summary>레시피의 저작 실수 전부. 비어 있어야 런을 열 수 있다.</summary>
        public List<RunMapIssue> Issues() => RunMapRules.RecipeIssues(floors);

        /// <summary><see cref="RunMapGenerator.TryGenerate"/>를 이 레시피로 부른다.</summary>
        public bool TryGenerate(int seed, out RunMap map, out List<RunMapIssue> issues)
            => RunMapGenerator.TryGenerate(floors, seed, out map, out issues);
    }
}
