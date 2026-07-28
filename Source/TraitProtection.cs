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
        private readonly HashSet<TraitDef> forcedDefs;
        private readonly bool protectGay;

        private TraitProtection(HashSet<TraitDef> forcedDefs, bool protectGay)
        {
            this.forcedDefs = forcedDefs;
            this.protectGay = protectGay;
        }

        public static TraitProtection Build(Pawn pawn, PawnGenerationRequest? request)
        {
            var defs = new HashSet<TraitDef>();

            void AddAll(Dictionary<TraitDef, int> source)
            {
                if (source == null) return;
                foreach (KeyValuePair<TraitDef, int> kvp in source) defs.Add(kvp.Key);
            }

            AddAll(TraitVarianceApplier.CaptureForcedTraits(pawn));
            AddAll(TraitVarianceApplier.CaptureBackstoryForcedTraits(pawn));
            if (request.HasValue)
                AddAll(TraitVarianceApplier.CaptureRequestForcedTraits(request.Value));

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
            if (trait == null) return true;

            // Mandatory: TraitSet.RemoveTrait calls pawn.genes.RemoveGene(trait.sourceGene) when this
            // is set, so removing a gene-sourced trait would delete the gene that granted it.
            if (trait.sourceGene != null) return true;

            // ScenForced is a misleading name: Trait's constructor assigns `scenForced = forced`, so
            // this is true for anything vanilla built with forced:true — kindDef-forced traits,
            // request-forced traits and ScenPart_ForcedTrait alike. It is false for backstory-forced
            // traits (vanilla builds those as a plain `new Trait(def, degree)`), which is exactly why
            // the forcedDefs set below is still needed rather than being redundant with this check.
            if (trait.ScenForced) return true;
            if (forcedDefs.Contains(trait.def)) return true;
            if (protectGay && trait.def == TraitDefOf.Gay) return true;

            return false;
        }
    }
}
