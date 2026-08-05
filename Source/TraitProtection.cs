using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Decides which of a pawn's traits must never be removed.
    //
    // Freely-rolled sexuality traits are not protected (heterosexual is the un-traited outcome in vanilla).
    // The one genuinely forced case — a same-gender love/ex-love partner implying Gay — is protected below.
    public class TraitProtection
    {
        // Maps every forced TraitDef to a diagnostic label indicating which forced-trait source put it there.
        private readonly Dictionary<TraitDef, string> forcedSources;
        private readonly bool protectGay;

        private TraitProtection(Dictionary<TraitDef, string> forcedSources, bool protectGay)
        {
            this.forcedSources = forcedSources;
            this.protectGay = protectGay;
        }

        public static TraitProtection Build(Pawn pawn, PawnGenerationRequest? request)
        {
            var defs = new Dictionary<TraitDef, string>();

            // First source to claim a def wins the label.
            void AddAll(Dictionary<TraitDef, int> source, string label)
            {
                if (source == null) return;
                foreach (KeyValuePair<TraitDef, int> kvp in source)
                    if (!defs.ContainsKey(kvp.Key)) defs[kvp.Key] = label;
            }

            // CaptureForcedTraits merges kindDef.forcedTraits and gene forcedTraits into one map, so
            // split them back apart here for the label using the same gene lookup GrowthUpPatch uses.
            foreach (KeyValuePair<TraitDef, int> kvp in TraitVarianceApplier.CaptureForcedTraits(pawn))
                if (!defs.ContainsKey(kvp.Key))
                    defs[kvp.Key] = TraitVarianceApplier.FindForcingGene(pawn, kvp.Key) != null
                        ? "gene forcedTraits"
                        : "kindDef forcedTraits";

            AddAll(TraitVarianceApplier.CaptureBackstoryForcedTraits(pawn), "backstory forcedTraits");
            if (request.HasValue)
                AddAll(TraitVarianceApplier.CaptureRequestForcedTraits(request.Value), "request ForcedTraits");

            // Vanilla hard-grants Gay when the pawn already has a same-gender love or ex-love partner.
            // Removing it would leave the relationship in an incoherent state.
            bool sameGenderPartner =
                LovePartnerRelationUtility.HasAnyLovePartnerOfTheSameGender(pawn)
                || LovePartnerRelationUtility.HasAnyExLovePartnerOfTheSameGender(pawn);

            return new TraitProtection(defs, sameGenderPartner);
        }

        public bool IsProtected(Trait trait)
        {
            return ProtectionReason(trait) != null;
        }

        // Returns null for a removable trait, or the diagnostic reason string if protected.
        public string ProtectionReason(Trait trait)
        {
            if (trait == null) return "null trait";

            // Mandatory: TraitSet.RemoveTrait calls pawn.genes.RemoveGene(trait.sourceGene) when this
            // is set, so removing a gene-sourced trait would delete the gene that granted it.
            if (trait.sourceGene != null) return $"sourceGene {trait.sourceGene.def.defName}";

            // ScenForced is set for anything vanilla built with forced:true (kindDef, request, or ScenPart_ForcedTrait).
            // Backstory-forced traits do not set ScenForced, which is why forcedSources is also checked below.
            if (trait.ScenForced) return "ScenForced (built with forced:true)";
            if (forcedSources.TryGetValue(trait.def, out string source)) return source;
            if (protectGay && trait.def == TraitDefOf.Gay) return "same-gender love partner";

            return null;
        }
    }
}
