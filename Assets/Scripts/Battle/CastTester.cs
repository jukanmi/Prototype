using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>Attach to the battle input host for card-independent Inspector smoke tests.</summary>
    public sealed class CastTester : MonoBehaviour
    {
        [SerializeField] private CastDirector director;
        [SerializeField] private Ally caster;
        [SerializeField] private SkillData skill;
        private CastDirector ResolveDirector()
        {
            if (director == null) director = FindAnyObjectByType<CastDirector>();
            return director;
        }

        [ContextMenu("Assist/Automatic card")]
        public void Automatic()
        {
            if (!Application.isPlaying || ResolveDirector() == null) return;
            Ally selected = caster;
            if (selected == null && director.Assists.Hero != null)
                foreach (Ally ally in director.Assists.Hero.Party)
                    if (ally != null && ally.Equipped.Count > 0) { selected = ally; break; }
            SkillData data = skill != null ? skill : selected != null && selected.Equipped.Count > 0 ? selected.Equipped[0].Data : null;
            if (selected != null && data != null)
                director.Cast(new CastRequest(selected, data, selected.AutoTarget(data)));
        }

        [ContextMenu("Assist/Queue three automatic cards")]
        public void QueueThree() { for (int i = 0; i < 3; i++) Automatic(); }

        [ContextMenu("Assist/Bullet time equipped party cards")]
        public void BulletTime()
        {
            if (!Application.isPlaying || ResolveDirector() == null || director.IsResolving || director.Assists.Hero == null) return;
            var requests = new List<CastRequest>();
            foreach (Ally ally in director.Assists.Hero.Party)
            {
                if (ally == null || ally.Equipped.Count == 0 || ally.Equipped[0].Data == null) continue;
                SkillData data = ally.Equipped[0].Data;
                requests.Add(new CastRequest(ally, data, ally.AutoTarget(data), 1f, true));
            }
            StartCoroutine(Run(requests));
        }

        private IEnumerator Run(List<CastRequest> requests) { yield return director.Resolve(requests, null); }

        [ContextMenu("Assist/Recall all")]
        public void Recall() { if (Application.isPlaying) ResolveDirector()?.Abort(); }
    }
}
