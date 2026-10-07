using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    [RequireComponent(typeof(AssistManager))]
    public sealed class CastDirector : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float preparationSeconds = 0.25f;
        [SerializeField, Min(0f)] private float impactGapSeconds = 0.3f;
        [SerializeField, Min(0f)] private float maxReactionWaitSeconds = 1f;
        private AssistManager assists;
        private BulletTimeController bulletTime;
        private int generation;
        private Action<int> consumeSlot;
        private readonly HashSet<int> consumedSlots = new HashSet<int>();
        private int slotCount;
        private bool aborting;
        private Ally activeCaster;
        private readonly HashSet<Entity> slotVictims = new HashSet<Entity>();
        public bool IsResolving { get; private set; }
        public AssistManager Assists => assists != null ? assists : assists = GetComponent<AssistManager>();

        public void Configure(Player hero, BulletTimeController controller)
        {
            bulletTime = controller;
            Assists.Initialize(hero);
        }

        public CastTicket Cast(CastRequest request, Action<CastTicket> completed = null)
            => Assists.Queue(request, completed);

        public void BeginSelection() => Assists.BeginSelection();
        public void EndSelection() => Assists.EndSelection();

        public IEnumerator Resolve(IReadOnlyList<CastRequest> requests, Action<int> consumed)
        {
            if (IsResolving) yield break;
            IsResolving = true;
            consumeSlot = consumed;
            consumedSlots.Clear();
            slotCount = requests.Count;
            int run = generation;
            // Only selection freezes the battlefield. Confirmation resumes live combat.
            TimeControl.Reset();
            Assists.BeginSelection();
            // Bind each card to its chosen enemy before any earlier card moves that enemy.
            var prepared = new List<CastRequest>(requests.Count);
            foreach (CastRequest request in requests) prepared.Add(RefreshChainRequest(request));
            Assists.PrepareFormation(prepared);
            while (run == generation && !Assists.FormationReady) yield return null;
            float preparation = 0f;
            while (run == generation && preparation < preparationSeconds)
            { preparation += TimeControl.DeltaFor(TimeDomain.Cast); yield return null; }
            for (int i = 0; i < prepared.Count && run == generation; i++)
            {
                CastRequest request = RefreshChainRequest(prepared[i]);
                if (run != generation) yield break;
                int index = i;
                activeCaster = request.Caster;
                slotVictims.Clear();
                CastTicket ticket = Cast(request, _ => Consume(index));
                if (ticket == null) { Consume(index); continue; }
                while (run == generation && !ticket.IsDone) yield return null;
                // Finish this hand slot, including detached attacks, before starting the next.
                float effectWait = 0f;
                while (run == generation && HasCastEffects && effectWait < 10f)
                { effectWait += TimeControl.DeltaFor(TimeDomain.Cast); yield return null; }
                if (run != generation) yield break;
                if (HasCastEffects)
                {
                    Debug.LogWarning("Assist effects exceeded the 10 second completion budget; cancelling remaining effects.", this);
                    Projectile.CancelCastEffects();
                    Installation.CancelCastEffects();
                }
                // Give this impact time to visibly move its victims before the next card aims.
                float reactionWait = 0f;
                float reactionLimit = Mathf.Max(impactGapSeconds, maxReactionWaitSeconds);
                while (run == generation && (reactionWait < impactGapSeconds ||
                    (reactionWait < reactionLimit && HasMovingVictims)))
                { reactionWait += TimeControl.DeltaFor(TimeDomain.Cast); yield return null; }
            }
            if (run != generation) yield break;
            Assists.ReleaseFormation();
            while (run == generation && !Assists.FormationGone) yield return null;
            if (run != generation) yield break;
            IsResolving = false;
            activeCaster = null;
            slotVictims.Clear();
            consumeSlot = null;
            slotCount = 0;
            TimeControl.Reset();
            Assists.EndSelection();
        }

        private static bool HasCastEffects => Projectile.HasCastEffects || Installation.HasCastEffects;

        private bool HasMovingVictims
        {
            get
            {
                foreach (Entity enemy in slotVictims)
                    if (enemy != null && !enemy.Combat.IsDead &&
                        enemy.Physics.HorizontalVelocity.sqrMagnitude > 0.0225f) return true;
                return false;
            }
        }

        public static CastRequest RefreshChainRequest(CastRequest request)
        {
            if (!request.IsValid) return request;
            Entity target = request.TargetEntity;
            if (target == null || target.Combat.IsDead || !target.IsTargetable || !target.gameObject.activeInHierarchy)
                target = request.Target.type == TargetingType.GroundPoint
                    ? BattleRegistry.NearestEnemy(request.Target.point)
                    : BattleRegistry.PickEnemy(request.Caster.transform.position, request.Skill.targetPick);
            if (target == null) return request;
            TargetInfo aim = request.Target;
            if (request.Skill.targeting == TargetingType.GroundPoint)
                aim = TargetInfo.Ground(target.Physics.GroundPosition);
            else if (request.Skill.targeting == TargetingType.Direction)
                aim = TargetInfo.Dir(target.Physics.GroundPosition - request.Caster.Physics.GroundPosition);
            return new CastRequest(request.Caster, request.Skill, aim, request.DamageScale, true, target);
        }

        private void Consume(int index)
        {
            if (consumedSlots.Add(index)) consumeSlot?.Invoke(index);
        }

        public void Abort()
        {
            if (aborting) return;
            aborting = true;
            generation++;
            IsResolving = false;
            activeCaster = null;
            slotVictims.Clear();
            Assists.RecallAll();
            Projectile.CancelCastEffects();
            Installation.CancelCastEffects();
            for (int i = 0; i < slotCount; i++) Consume(i);
            consumeSlot = null;
            slotCount = 0;
            TimeControl.Reset();
            bulletTime?.CancelAssistSequence();
            aborting = false;
        }

        public static void RecallScene()
        {
            foreach (var director in FindObjectsByType<CastDirector>()) director.Abort();
        }

        private void HandleHitLanded(Combat attacker, Combat victim)
        {
            if (IsResolving && attacker != null && attacker.Owner == activeCaster &&
                victim != null && victim.Owner != null && victim.Owner.Faction == Faction.Enemy)
                slotVictims.Add(victim.Owner);
        }

        private void OnEnable() => Combat.OnAnyHitLanded += HandleHitLanded;
        private void OnDisable()
        {
            Combat.OnAnyHitLanded -= HandleHitLanded;
            Abort();
        }
    }
}
