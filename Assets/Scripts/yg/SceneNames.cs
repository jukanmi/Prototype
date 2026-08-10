namespace Prototype.YG
{
    /// <summary>
    /// 씬 이름 상수. 문자열 하드코딩으로 인한 오타를 막는다.
    /// </summary>
    public static class SceneNames
    {
        public const string Boot     = "Boot";
        public const string MainMenu = "MainMenu";

        /// <summary>
        /// 스테이지 진행 순서. <see cref="GameManager.CurrentStageIndex"/>가 이 배열의
        /// 인덱스와 대응한다. 인덱스 0이 런 시작 지점이며, 마지막 인덱스를 넘어가면 런 클리어로 취급한다.
        /// </summary>
        public static readonly string[] Stages = { "SampleScene", "Stage02" };
    }
}
