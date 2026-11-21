using UnityEngine;
using UnityEngine.Rendering;

namespace Prototype
{
    /// <summary>Transfers the existing character renderer's presentation to an articulated rig.
    /// The hidden renderer remains available to tint, death fades and skill afterimages.</summary>
    [ExecuteAlways, DefaultExecutionOrder(100)]
    public sealed class LayeredCharacterVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer source;
        [SerializeField] private Transform rig;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private SpriteRenderer[] parts;

        private void OnEnable() => Sync();
        private void LateUpdate() => Sync();
        private void OnDisable()
        {
            if (source != null) source.forceRenderingOff = false;
            if (rig != null) rig.gameObject.SetActive(false);
        }

        public void Sync()
        {
            if (source == null || rig == null) return;
            source.forceRenderingOff = true;
            rig.gameObject.SetActive(source.enabled);
            Vector3 scale = rig.localScale;
            scale.x = Mathf.Abs(scale.x) * (source.flipX ? -1f : 1f);
            scale.y = Mathf.Abs(scale.y) * (source.flipY ? -1f : 1f);
            rig.localScale = scale;
            if (sortingGroup != null)
            {
                sortingGroup.sortingLayerID = source.sortingLayerID;
                sortingGroup.sortingOrder = source.sortingOrder;
            }
            if (parts == null) return;
            foreach (var part in parts)
            {
                if (part == null) continue;
                part.color = source.color;
                part.enabled = source.enabled;
            }
        }
    }
}
