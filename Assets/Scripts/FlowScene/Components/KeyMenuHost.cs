using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// <see cref="KeyMenu"/>를 창 오브젝트에 매단다 — 창이 켜지면 커서를 다시 잡고, 켜져 있는 동안 매 프레임 돌린다.
    /// 창이 꺼지면(<c>SetActive(false)</c>) 저절로 멈춘다. 상점 창처럼 MonoBehaviour가 아닌 창이 쓴다.
    /// </summary>
    public sealed class KeyMenuHost : MonoBehaviour
    {
        private KeyMenu menu;

        /// <summary>창의 칸을 다 더한 뒤에 부른다 — 붙이는 순간 커서를 첫 칸에 둔다.</summary>
        public static void Attach(GameObject window, KeyMenu menu)
        {
            var host = window.AddComponent<KeyMenuHost>();
            host.menu = menu;
            menu.Open();
        }

        private void OnEnable() => menu?.Open();

        private void Update() => menu?.Tick();
    }
}
