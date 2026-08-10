using Prototype;
using UnityEngine;

namespace Prototype.YG
{
    /// <summary>
    /// 스테이지 오른쪽 끝의 트리거. 플레이어가 닿으면 다음 스테이지로 전환한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class StageExitTrigger : MonoBehaviour
    {
        private BattleSceneController controller;
        private bool fired;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            controller = FindAnyObjectByType<BattleSceneController>();

            if (controller == null)
                Debug.LogWarning("[StageExitTrigger] BattleSceneController를 찾지 못했다.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fired || controller == null) return;
            if (other.GetComponentInParent<Player>() == null) return;

            fired = true;
            controller.AdvanceToNextStage();
        }
    }
}
