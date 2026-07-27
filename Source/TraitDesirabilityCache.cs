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
            foreach (TraitDef def in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                for (int degree = 0; degree < (def.degreeDatas?.Count ?? 1); degree++)
                {
                    float score = ScoreTrait(def, degree);
                    Scores[(def, degree)] = score;
                }
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

        private static float ScoreTrait(TraitDef def, int degree)
        {
            TraitDegreeData data = def.degreeDatas != null && degree < def.degreeDatas.Count
                ? def.degreeDatas[degree]
                : null;
            if (data == null) return 0f;

            float skillCategory = 0f;
            if (data.skillGains != null)
            {
                float sum = data.skillGains.Values.Sum();
                skillCategory = Mathf.Clamp(sum / Constants.SkillOffsetReferenceMagnitude, -1f, 1f);
            }

            float statCategory = 0f;
            if (data.statOffsets != null || data.statFactors != null)
            {
                float sum = 0f;
                if (data.statOffsets != null)
                    sum += data.statOffsets.Sum(m => m.value);
                if (data.statFactors != null)
                    sum += data.statFactors.Sum(m => m.value - 1f);
                statCategory = Mathf.Clamp(sum / Constants.StatReferenceMagnitude, -1f, 1f);
            }

            float workTagCategory = 0f;
            if (data.disabledWorkTags != WorkTags.None)
            {
                int disabledCount = System.Enum.GetValues(typeof(WorkTags))
                    .Cast<WorkTags>()
                    .Count(tag => tag != WorkTags.None && (data.disabledWorkTags & tag) != 0);
                workTagCategory = Mathf.Clamp(-disabledCount * Constants.WorkTagDisablePenalty, -1f, 1f);
            }

            float socialCategory = 0f;
            if (data.socialFightChanceFactor != 1f)
            {
                socialCategory += (data.socialFightChanceFactor - 1f);
            }
            socialCategory = Mathf.Clamp(socialCategory / Constants.SocialReferenceMagnitude, -1f, 1f);

            return skillCategory + statCategory + workTagCategory + socialCategory;
        }
    }
}
