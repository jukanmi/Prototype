using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype
{
    /// <summary>비전투 칸의 종류. 씬 하나에 하나를 고른다.</summary>
    public enum NodeSceneKind
    {
        /// <summary>들어오자마자 회복하고 결과만 보여 준다.</summary>
        Rest = 0,

        /// <summary>들어오자마자 카드 상점 창을 띄운다.</summary>
        Shop = 1,

        /// <summary>들어오자마자 이벤트 하나를 뽑아 창을 띄운다.</summary>
        Event = 2,
    }

    /// <summary>
    /// 휴식 · 상점 · 이벤트 씬의 지휘자. <see cref="BattleSceneController"/>의 비전투판이다.
    ///
    /// <b>아직 그림이 없어서 씬에는 이 컴포넌트 하나뿐이다.</b> 들어오자마자 창을 코드로 짓고,
    /// [나가기] · [계속]이 이 칸을 끝낸다. 나중에 상점 그림이 생기면 씬에 배경을 깔고
    /// 창은 그 위에 그대로 띄우면 된다 — 흐름은 안 바뀐다.
    ///
    /// 칸을 끝내는 곳은 <see cref="Leave"/> 한 군데다 — 지도로 돌아간다.
    ///
    /// 계획서: docs/Run_Map_Plan.md (2.7 · 0단계)
    /// </summary>
    public class NodeSceneController : MonoBehaviour
    {
        [Tooltip("이 씬이 어떤 칸인가.")]
        [SerializeField] private NodeSceneKind kind = NodeSceneKind.Rest;

        [Tooltip("이벤트 칸이 뽑을 후보. 들어올 때마다 하나를 무작위로 고른다. 다른 종류에서는 안 읽는다.")]
        [SerializeField] private RunEventAsset[] events = new RunEventAsset[0];

        private bool leaving;

        public NodeSceneKind Kind => kind;
        public IReadOnlyList<RunEventAsset> Events => events;

        private string SceneName => gameObject.scene.name;

        /// <summary>
        /// 이 런의 편성. 몸이 없는 씬에서 체력을 바꿀 때 기록이 없는 동료까지 닿으려면 필요하다.
        /// 단독 실행이면 null — 주인공 기록만 바뀐다.
        /// </summary>
        private static IReadOnlyList<PartyMemberData> Roster
            => GameManager.Instance != null && GameManager.Instance.Loadout != null
                ? GameManager.Instance.Loadout.Members
                : null;

        private void Start()
        {
            UiKit.EnsureEventSystem();

            var canvasGo = new GameObject("NodeCanvas");
            canvasGo.transform.SetParent(transform, false);
            UiKit.BuildCanvas(canvasGo, UiLayer.NodeWindow);

            if (GameManager.Instance == null)
                Debug.LogWarning("[Node] GameManager 없음. 씬 단독 실행 — 나가면 이 씬을 다시 연다.");

            switch (kind)
            {
                case NodeSceneKind.Rest:
                    // 들어온 순간 적용한다. 창은 결과를 보여 줄 뿐이고, 창을 닫기 전에 나가도 회복은 이미 끝났다.
                    RunProgression.Current.Party.ChangeHp(NodeRules.RestHealRatio, Roster);
                    NodeWindows.Rest(canvasGo.transform, NodeRules.RestHealRatio, Leave);
                    break;

                case NodeSceneKind.Shop:
                    new ShopWindow(canvasGo.transform, Roster, Leave);
                    break;

                default:
                    new EventWindow(canvasGo.transform, PickEvent(), Roster, Leave);
                    break;
            }

            Debug.Log($"[Node] {kind} 칸 진입 ({SceneName}) — 골드 {RunProgression.Current.Gold}");
        }

        /// <summary>빈 칸을 뺀 후보 중 하나. 후보가 없으면 null — 창이 그 사실을 적고 [계속]만 낸다.</summary>
        private RunEventAsset PickEvent()
        {
            var pool = new List<RunEventAsset>();
            if (events != null)
                foreach (RunEventAsset e in events)
                    if (e != null) pool.Add(e);

            if (pool.Count == 0)
            {
                Debug.LogError($"[Node] {name}: 이벤트 후보가 비어 있다.", this);
                return null;
            }

            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// 이 칸을 끝내고 지도로 돌아간다. <b>접수가 먼저다</b> — 전환이 거절되면 버튼만 다시 누를 수 있게 남긴다
        /// (<see cref="BattleSceneController"/>의 전환 주석과 같은 규칙).
        ///
        /// 비전투 칸은 보스 층에 올 수 없으므로(레시피 검사) "갈 곳이 없는" 경우가 없다.
        /// </summary>
        private void Leave()
        {
            if (leaving) return;

            GameManager gm = GameManager.Instance;

            if (gm == null || SceneLoader.Instance == null)
            {
                leaving = true;
                SceneManager.LoadScene(SceneName);
                return;
            }

            if (gm.CompleteNodeAndReturnToMap(SceneName, () => AudioManager.Instance?.PlayMenuBgm()))
            {
                leaving = true;
                return;
            }

            // 들어가 있는 칸이 없다 — 지도를 안 거치고 이 씬이 떴다. 갇히지 않게 메인 메뉴로 보낸다.
            if (gm.CurrentNode == null && gm.ReturnToMainMenu(SceneName, () => AudioManager.Instance?.PlayMenuBgm()))
            {
                Debug.LogWarning($"[Node] 들어가 있는 지도 칸이 없다({SceneName}) — 메인 메뉴로 돌아간다.");
                leaving = true;
            }
        }
    }
}
