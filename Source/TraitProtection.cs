using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Decides which of a pawn's traits must never be removed. Every rule here is required for
    // correctness, not preference — see the spec's "Protected traits" table.
    //
    // Deliberately NOT protected: freely-rolled sexuality traits. An earlier version of this mod
    // believed vanilla guarantees every pawn exactly one of Gay/Bisexual/Asexual. That is false.
    // PawnGenerator.TryGenerateSexualityTraitFor builds a weighted table whose first entry is a null
    // candidate carrying the summed commonality of every other trait in the game; when that entry
    // wins, the `result.First != null` guard grants nothing. Heterosexual is the common, correct
    // outcome, so removing a rolled sexuality trait breaks no invariant. The one genuinely forced
    // case — a same-gender love/ex-love partner implying Gay — IS protected below.
    public class TraitProtection
    {
        // Maps every forced TraitDef to a human-readable label for which of the four forced-trait
        // sources put it there. The set of keys is exactly what the old plain HashSet<TraitDef> held,
        // so protection decisions are unchanged; the labels exist purely so the verbose trait trace
        // can say *why* a trait survived rather than just that it did.
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

            // First source to claim a def wins the label. Order matches the capture order below, so a
            // def forced by two sources reads as the one checked first — the label is diagnostic, not
            // load-bearing, and any of its sources is a true answer to "why is this protected".
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

            // Vanilla hard-grants Gay when the pawn already has a same-gender love or ex-love partner
            // (PawnGenerator.GenerateTraits, ~line 1521). Removing it would leave the relationship in
            // an incoherent state, so this specific case is protected while a freely-rolled Gay is not.
            bool sameGenderPartner =
                LovePartnerRelationUtility.HasAnyLovePartnerOfTheSameGender(pawn)
                || LovePartnerRelationUtility.HasAnyExLovePartnerOfTheSameGender(pawn);

            return new TraitProtection(defs, sameGenderPartner);
        }

        public bool IsProtected(Trait trait)
        {
            return ProtectionReason(trait) != null;
        }

        // The single source of truth for protection — IsProtected is a null check over this. Returns
        // null for a removable trait, otherwise the reason it is protected, in the same check order
        // the old boolean-only version used.
        public string ProtectionReason(Trait trait)
        {
            if (trait == null) return "null trait";

            // Mandatory: TraitSet.RemoveTrait calls pawn.genes.RemoveGene(trait.sourceGene) when this
            // is set, so removing a gene-sourced trait would delete the gene that granted it.
            if (trait.sourceGene != null) return $"sourceGene {trait.sourceGene.def.defName}";

            // ScenForced is a misleading name: Trait's constructor assigns `scenForced = forced`, so
            // this is true for anything vanilla built with forced:true — kindDef-forced traits,
            // request-forced traits and ScenPart_ForcedTrait alike. It is false for backstory-forced
            // traits (vanilla builds those as a plain `new Trait(def, degree)`), which is exactly why
            // the forcedSources map below is still needed rather than being redundant with this check.
            if (trait.ScenForced) return "ScenForced (built with forced:true)";
            if (forcedSources.TryGetValue(trait.def, out string source)) return source;
            if (protectGay && trait.def == TraitDefOf.Gay) return "same-gender love partner";

            return null;
        }
    }
}
