using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    [StaticConstructorOnStartup]
    public static class TraitDesirabilityCache
    {
        private static readonly Dictionary<(TraitDef, int), float> Scores = new Dictionary<(TraitDef, int), float>();
        private static readonly Dictionary<TraitDef, List<int>> DegreesByTrait = new Dictionary<TraitDef, List<int>>();
        private static readonly List<int> DegreeZeroOnly = new List<int> { 0 };

        public static float ObservedMinScore { get; private set; }
        public static float ObservedMaxScore { get; private set; }
        public static float MeanScore { get; private set; }
        public static float StdDevScore { get; private set; }

        static TraitDesirabilityCache()
        {
            Rebuild();
        }

        public static void Rebuild()
        {
            Scores.Clear();
            DegreesByTrait.Clear();

            foreach (TraitDef def in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                var degrees = new List<int>();
                if (def.degreeDatas == null || def.degreeDatas.Count == 0)
                {
                    // Degree-less trait: a single implicit degree-0 state.
                    Scores[(def, 0)] = ScoreTraitData(def, null);
                    degrees.Add(0);
                }
                else
                {
                    foreach (TraitDegreeData data in def.degreeDatas)
                    {
                        // Keyed by the trait's REAL degree value (e.g. -2/-1/1/2), not its list
                        // index — the list index and the degree value are unrelated for most
                        // multi-degree vanilla traits, and downstream sampling/granting must use
                        // the real value so `new Trait(def, degree, ...)` resolves correctly.
                        Scores[(def, data.degree)] = ScoreTraitData(def, data);
                        degrees.Add(data.degree);
                    }
                }
                DegreesByTrait[def] = degrees;
            }

            if (Scores.Count == 0)
            {
                ObservedMinScore = -1f;
                ObservedMaxScore = 1f;
                MeanScore = 0f;
                StdDevScore = 1f;
                return;
            }

            var values = Scores.Values.ToList();
            MeanScore = values.Average();
            float variance = values.Select(v => (v - MeanScore) * (v - MeanScore)).Average();
            StdDevScore = Mathf.Sqrt(variance);

            float rawMin = MeanScore - Constants.ZMultiplier * StdDevScore;
            float rawMax = MeanScore + Constants.ZMultiplier * StdDevScore;
            float trueMin = values.Min();
            float trueMax = values.Max();

            ObservedMinScore = Mathf.Max(rawMin, trueMin);
            ObservedMaxScore = Mathf.Min(rawMax, trueMax);
        }

        public static float ScoreOf(TraitDef def, int degree)
        {
            return Scores.TryGetValue((def, degree), out float score) ? score : 0f;
        }

        // All valid degree values for this trait (a single-entry [0] for degree-less traits).
        // TraitVarianceApplier samples (def, degree) pairs together using this, rather than
        // always defaulting to degree 0 — a hardcoded 0 doesn't exist for many multi-degree
        // vanilla traits (e.g. Industriousness: -2/-1/1/2), which would otherwise cause vanilla
        // trait-lookup errors and mis-scored sampling.
        public static IReadOnlyList<int> DegreesFor(TraitDef def)
        {
            return DegreesByTrait.TryGetValue(def, out var degrees) ? degrees : DegreeZeroOnly;
        }

        // `def` is needed alongside `data` because `disabledWorkTags` lives on TraitDef itself in
        // this RimWorld version, not on TraitDegreeData — verified against decompiled
        // Assembly-CSharp.dll (Global Constraints). Consequence: the work-tag category is
        // identical across every degree of a multi-degree trait (there is no per-degree
        // work-tag-disable concept in vanilla), unlike the other three categories, which do vary
        // by degree via `data`.
        private static float ScoreTraitData(TraitDef def, TraitDegreeData data)
        {
            float skillCategory = 0f;
            if (data?.skillGains != null)
            {
                // SkillGain is a {SkillDef skill, int amount} pair, not a dictionary — verified
                // against decompiled source (Global Constraints).
                float sum = data.skillGains.Sum(sg => sg.amount);
                skillCategory = Mathf.Clamp(sum / Constants.SkillOffsetReferenceMagnitude, -1f, 1f);
            }

            float statCategory = 0f;
            if (data != null && (data.statOffsets != null || data.statFactors != null))
            {
                float sum = 0f;
                if (data.statOffsets != null)
                    sum += data.statOffsets.Sum(m => m.value);
                if (data.statFactors != null)
                    sum += data.statFactors.Sum(m => m.value - 1f);
                statCategory = Mathf.Clamp(sum / Constants.StatReferenceMagnitude, -1f, 1f);
            }

            float workTagCategory = 0f;
            if (def.disabledWorkTags != WorkTags.None)
            {
                int disabledCount = System.Enum.GetValues(typeof(WorkTags))
                    .Cast<WorkTags>()
                    .Count(tag => tag != WorkTags.None && (def.disabledWorkTags & tag) != 0);
                workTagCategory = Mathf.Clamp(-disabledCount * Constants.WorkTagDisablePenalty, -1f, 1f);
            }

            float socialCategory = 0f;
            if (data != null && data.socialFightChanceFactor != 1f)
            {
                socialCategory += (data.socialFightChanceFactor - 1f);
            }
            socialCategory = Mathf.Clamp(socialCategory / Constants.SocialReferenceMagnitude, -1f, 1f);

            return skillCategory + statCategory + workTagCategory + socialCategory;
        }
    }
}
