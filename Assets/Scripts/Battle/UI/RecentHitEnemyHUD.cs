using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 아군이 가장 최근에 유효 타격한 적 한 명의 체력을 하단에 표시한다.
    ///
    /// <b>화면은 프리팹이 쥔다</b> — 이 컴포넌트는 <c>CombatManager</c> 프리팹의
    /// <c>RecentHitEnemyCanvas</c> 자식에 붙어 있다. 자리 · 색을 바꾸려면 프리팹을 연다.
    /// </summary>
    public class RecentHitEnemyHUD : MonoBehaviour
    {
        private const float VisibleDuration = 3f;

        [Header("배선")]
        [Tooltip("켜고 끄는 대상. 이름과 체력 바를 담은 판.")]
        [SerializeField] private GameObject panel;

        [Tooltip("적 이름.")]
        [SerializeField] private Text nameLabel;

        [Tooltip("체력 바의 채워지는 부분. 오른쪽 앵커를 움직여 폭을 만든다.")]
        [SerializeField] private RectTransform healthFill;

        private Combat currentTarget;
        private float expiresAt;

        /// <summary>배선이 빈 채로 돌 때 경고를 한 번만 낸다.</summary>
        private bool warned;

        public Combat CurrentTarget => currentTarget;
        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>체력 바가 실제로 그려지는 비율. 0~1.</summary>
        public float HealthFillRatio => healthFill != null ? healthFill.anchorMax.x : 0f;

        private void Awake()
        {
            if (panel == null || nameLabel == null || healthFill == null)
                Warn();

            // 프리팹은 판이 보이는 채로 저장돼 있다(그래야 에디터에서 배치를 본다).
            SetVisible(false);
        }

        private void Warn()
        {
            if (warned) return;

            warned = true;
            Debug.LogWarning(
                "[RecentHitEnemyHUD] 배선이 비어 있다 — 적 체력 바가 안 뜬다. " +
                "CombatManager 프리팹의 RecentHitEnemyCanvas 배선을 확인할 것.", this);
        }

        private void OnEnable() => Combat.OnAnyHitLanded += Track;

        private void OnDisable()
        {
            Combat.OnAnyHitLanded -= Track;
            UnsubscribeTarget();
        }

        private void Update() => TickExpiry(Time.unscaledTime);

        /// <summary>
        /// 표시 시간 만료 검사. 현재 시각을 인자로 받는다 —
        /// 에디트모드 테스트가 3초를 실제로 기다리지 않고 앞당길 수 있어야 한다.
        /// </summary>
        public void TickExpiry(float unscaledNow)
        {
            if (currentTarget != null && unscaledNow >= expiresAt)
                Clear();
        }

        public void Track(Combat attacker, Combat target)
        {
            if (attacker?.Owner?.Faction != Faction.Ally ||
                target?.Owner?.Faction != Faction.Enemy || target.IsDead) return;

            if (currentTarget != target)
            {
                UnsubscribeTarget();
                currentTarget = target;
                currentTarget.OnDead += Clear;
                currentTarget.Health.OnChanged += HandleHealthChanged;
            }

            expiresAt = Time.unscaledTime + VisibleDuration;
            Refresh();
            SetVisible(true);
        }

        private void HandleHealthChanged(Energy _) => Refresh();

        private void Refresh()
        {
            if (currentTarget == null || currentTarget.IsDead)
            {
                Clear();
                return;
            }

            if (nameLabel != null) nameLabel.text = currentTarget.name;
            SetFillRatio(currentTarget.Health.Ratio);
        }

        /// <summary>
        /// 오른쪽 앵커를 움직여 폭을 줄인다.
        /// Image.fillAmount는 스프라이트가 있을 때만 동작한다 — 이 HUD는 스프라이트 없이
        /// 코드로만 만들기 때문에 sprite가 null이고, 그 경우 Image는 type/fillAmount를
        /// 무시하고 RectTransform 전체를 채운다. 그래서 폭을 직접 그린다.
        /// </summary>
        private void SetFillRatio(float ratio)
        {
            if (healthFill == null) return;
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        private void Clear()
        {
            UnsubscribeTarget();
            currentTarget = null;
            SetVisible(false);
        }

        private void UnsubscribeTarget()
        {
            if (currentTarget == null) return;
            currentTarget.OnDead -= Clear;
            currentTarget.Health.OnChanged -= HandleHealthChanged;
        }

        private void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }
    }
}
