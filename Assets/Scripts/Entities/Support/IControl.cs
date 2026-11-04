// 몸을 모는 쪽(조작 · AI)의 계약. Entity는 이것만 알고,
// 구현(Control · EnemyControl · AssistControlModule …)은 Agent 층(Control 폴더)에 있다.

namespace Prototype
{
    public interface IControl
    {
        /// <summary>
        /// <c>MonoBehaviour.enabled</c> 그대로 — 인스펙터에서 꺼 둔 AI는 건너뛴다.
        /// MonoBehaviour 구현은 따로 쓸 것 없이 이 속성을 물려받아 만족한다.
        /// </summary>
        bool enabled { get; }

        void Tick(float dt);
    }
}
