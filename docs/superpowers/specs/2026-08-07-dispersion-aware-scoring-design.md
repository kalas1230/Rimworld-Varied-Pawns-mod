# Dispersion-aware scoring, real-unit noise sliders, and an outcome curve

Status: **design approved, not implemented.**
Supersedes nothing. Touches Rule 1, Rule 5, Rule 7 and Rule 8 — see "Rules and permissions".

---

## 1. The problem, and the measurement that established it

`CalculateCompositeScore` reads six fields. `skillNoise` and `passionNoise` are not among them, so
no percentage anywhere in this project responds to them. That was a known and documented scope
limit. What was **not** known is how large the resulting error is.

Best-of-N is a **maximum** statistic, and maxima reward dispersion. The stated reason for using
Best-of-N rather than a mean is that the player cherry-picks — rerolls starts, refuses quest pawns,
picks from captures. A cherry-picking metric that cannot see spread is inconsistent with its own
justification.

Measured (`zzz-Do-Not-Commit/noise_bestofn_mc.py`, two independent seeds agreeing to 0.1pp):

| Wildcard | reported | with dispersion | understated by |
|---|---|---|---|
| N=1 | −4.7% | −1.4% | 3.2pp |
| N=5 | +10.1% | +27.9% | 17.9pp |
| N=25 | +19.4% | **+44.3%** | 24.9pp |
| N=50 | +22.3% | **+49.0%** | 26.6pp |

**Wildcard is outside the ±35% envelope the mod advertises.** Rule 1 passes today only because the
metric enforcing it is blind to the axis that breaks it. Every other preset moves ≤1.5pp — this is
Wildcard-specific, because it is the only profile whose noise sits far off the pack.

### 1.1 The finding that shaped the fix

**`skillNoise` is nearly free on the envelope; `passionNoise` is the entire story.** Dropping
Wildcard's `skillNoise` from 0.85 to **0.00** moves N=50 by 0.3pp measured on the quadrature grid
(+48.1% → +47.8%), and by 0.14pp measured by Monte Carlo (+48.98% → +48.84%). Both methods, same
conclusion — but note the two figures come from **different instruments**, and the rest of §1 is
sourced from the MC. Quote whichever you use, and say which.

The mechanism: skill noise is drawn per-skill, so the pawn's average over 12 skills carries only
`variance/12`, and it is then censored by `Clamp(0, 20)`. The passion budget is a single per-pawn
draw with no averaging, so it reaches Best-of-N power in full.

This is why the remediation below leaves Wildcard's within-pawn skill chaos completely intact.

---

## 2. Decisions taken

| # | Question | Decision |
|---|---|---|
| 1 | Wildcard breaches ±35% once the score can see dispersion | **Retune Wildcard.** Rule 1 keeps meaning exactly what it says; variance presets are not exempted from it. |
| 2 | Which header figures become dispersion-aware | **Both.** Originally decided as Best-of-25 only; **re-taken** once the stated rationale was shown to be false. See the correction below. |
| 3 | What the header curve plots | **The realised outcome distribution.** Noise sliders visibly move it; censoring shows as a pile at the left. Still a single line. |
| 4 | Noise slider units | **Real units, single handle, typical spread on the handle**, with the extreme and the resulting outcome band as derived readouts. |

> [!WARNING]
> **Correction to Decision 2's rationale — the premise it was decided on is false.**
>
> Decision 2 was taken on the argument *"the noise is mean-zero, so the typical-pawn figure
> genuinely does not change."* That is true only where the composite is **linear** in the shift.
> `Clamp(0, 20)` puts a kink in it, and past that kink the function is convex, so by Jensen
> `E[clamp(baseline + T·mag)] > clamp(baseline)` whenever the clamp binds.
>
> **This is the same failure `PawnVarianceSettings.cs:1523-1532` already documents for the quality
> axis** — the codebase learned this lesson and the first draft of this spec did not apply it.
>
> Measured, N=1, each profile against its own mean-only score:
>
> | Faithful | Elite | Sovereign | Specialist | Scavenger | Desperate | Distinct | **Wildcard (retuned)** |
> |---|---|---|---|---|---|---|---|
> | +0.001% | +0.000% | +0.000% | +0.001% | +0.008% | +0.020% | +0.201% | **+1.708%** |
>
> So the rationale holds for seven of eight presets and fails for the one whose band sits under the
> floor. In displayed terms the retuned Wildcard's typical figure is **−13.02% mean-only versus
> −11.53% dispersion-aware, a 1.49pp gap.**
>
> **Resolved: the typical figure becomes dispersion-aware too.** Keeping it mean-only would
> under-report censored profiles — Wildcard today, and any custom profile a player pushes below the
> floor. The model already produces the corrected value, so it costs nothing.
>
> **Two distinct quantities are involved; do not conflate them.**
>
> | Readout | Was | Becomes |
> |---|---|---|
> | Editor Row 3, at the quality slider's own `q` | `composite(q)` | `μ(q)` — the model's conditional mean at that `q` |
> | Envelope table's N=1 column | `E[composite(q)]` over the Beta | the dispersion-aware N=1 |
>
> They differ only where the clamp binds, and by construction they agree everywhere it does not —
> which is why seven of eight presets move by ≤0.2%.

A two-handle noise range was considered and **rejected**. It would be a third kind of range in a UI
that carefully teaches two (targets vs hard limits, with every tooltip stating which). More
importantly, the only thing two handles buy over one is *asymmetry*, and asymmetric noise breaks
`E[shift] = Lerp(min, max, q)` — the exact identity that makes `CalculateCompositeScore` reading
`Lerp` correct rather than approximate, and that keeps `envelope_check.py` in step with what pawns
actually get.

---

## 3. Architecture — one computation, three consumers

New file `Source/DispersionModel.cs`, mirrored by a new section in `docs/tools/envelope_check.py`.

Conditional on the quality roll `q`:

- **Skill axis.** Each of the 12 skills is `clamp(5 + baseline(q) + T·mag, 0, 20) / 20`, with `T`
  triangular on [−1,1] (`u1 + u2 − 1`, variance 1/6). Take `E` and `Var` by quadrature over `T`.
  The pawn's *average* over 12 iid skills is then `Normal(E, Var/12)` by CLT.
- **Passion axis.** Budget is `Lerp(min,max,q) + Clamp(Gaussian(0,σ), ±4σ)`, floored at 1 when
  `passionCountMin > 0`, capped at capacity. `E` and `Var` by quadrature over the standard normal.
- **Combine.** The two axes are independent given `q`, so `composite | q ~ Normal(μ(q), σ(q))`,
  with `μ` and `σ²` combining under the existing weights.

Then `F(x) = Σ_q Φ((x − μ(q)) / σ(q)) · w_q` over the Beta density of `q`.

> **The `[0,1]` integration bound IS the clamp — do not "fix" it.** The Normal approximation has
> unbounded support, but the true composite is `Clamp01`'d. For `Y = clamp(X,0,1)`,
> `E[max of N] = ∫₀¹ (1 − F_Y(x)^N) dx` and `F_Y = F_X` on `[0,1)`, so integrating the *unclamped*
> Gaussian CDF over `[0,1]` is exactly correct for the clamped variable. Extending the integral
> beyond `[0,1]`, or pre-clamping `F`, would both be wrong. This matters: Wildcard's
> `μ ≈ 0.25, σ ≈ 0.09` puts real mass below zero, so the bound is load-bearing, not cosmetic.

> **Guard `σ(q) = 0`.** At zero noise the conditional distribution is degenerate and
> `Φ((x−μ)/σ)` divides by zero. Fall back to a step function (`x ≥ μ ? 1 : 0`) below ~1e-12. This
> is not hypothetical — **the zero-noise self-check in §8.2 exercises exactly this path**, so
> omitting the guard crashes the one test that validates the whole model.

> **`Φ` has no built-in in Unity's Mono — `System.Math` has no `Erf`, and there is none anywhere in
> `Source/` today.** Confirmed: `PawnVarianceMod.csproj:3` targets `net472`, and .NET Framework
> never shipped `Math.Erf` (it arrived in .NET Core). The C# side needs an explicit error-function approximation, and **both
> implementations must agree to well under the gate tolerance.** Python's `math.erf` is
> near-machine-precision, so the C# approximation sets the floor. Abramowitz–Stegun 7.1.26 (max
> abs error 1.5e-7) is sufficient and allocation-free. Specify it, pin it, and have the in-game
> gate exercise it — an `Erf` mismatch would be a two-implementation divergence in the one function
> both sides call hundreds of thousands of times.

That single `F` feeds all three consumers:

| Consumer | Derivation |
|---|---|
| Best-of-25 header anchor, and Rule 1 enforcement | `E[max of N] = Σ (1 − F(x)^N) dx`. **Rule 1 is checked at all four N (1, 5, 25, 50) via this model**, unchanged in scope — Decision 2 restricts only what the *header displays*, not what the envelope enforces. Narrowing Rule 1 to N=25 would be a Rule 5 change and is not proposed. |
| Header curve | the analytic mixture density `f(x) = Σ_q (w_q/σ(q))·φ((x−μ(q))/σ(q))` — **not** finite differences of `F`, which render visibly noisier for no saving, since the same `μ(q), σ(q)` are already in hand |
| `envelope_check.py` | the same two, mirrored |

**Deterministic by requirement, not by preference.** The in-game gate cross-checks the C# against
`envelope_check.py` at 0.5pp on the displayed quantity. That only works if both sides are
reproducible, so Monte Carlo cannot be in the shipped path.

### 3.1 Grid resolution

Ship **q=256, x=512, triangular=65, gaussian=65** (131k evaluations for `F`).

Measured drift against a 4× finer grid (q=1024, x=2048, 257, 257), worst case across all 8 presets
and all four N: **0.000pp**. Even q=128/x=256 drifts only 0.001pp. Discretisation error largely
cancels because every displayed figure is a *ratio* to Faithful.

### 3.2 Accuracy of the Normal-per-q approximation

Validated against the Monte Carlo:

| | grid vs MC, worst |
|---|---|
| all presets except Wildcard | ≤0.0005 absolute |
| Wildcard at `passionNoise` 0.85 | 0.0048 (≈1.5pp displayed) |
| **Wildcard at `passionNoise` 0.50** | **0.08pp** |

The 1.5pp case is a configuration this spec removes. Heavy clamping — the ±4σ window, the floor at
1, the capacity cap — is what breaks the normal fit, and the retune reduces all three. Post-retune
the model error sits an order of magnitude inside the 0.5pp gate tolerance.

**This approximation must be stated in `HANDOVER.md`.** It is a real limitation, not a rounding
detail: a future profile with extreme `passionNoise` will drift from the MC again.

---

## 4. Scoring changes

`CalculateCompositeScore` **stays exactly as it is, and remains the mean-band function.** It is
still what `E[shift] = Lerp` guarantees exactness for, and still what the zero-noise self-check
measures against. What changes is *which function the readouts call*, not this one's contents.

`CalculateBestOfNScoreCore` gains a dispersion-aware sibling. Per Decision 2 (as re-taken), **both**
header figures now read from the model:

| Readout | Source |
|---|---|
| Editor Row 3, "typical pawn" | `μ(q)` at the slider's `q` |
| Editor Row 3b, Best-of-25 anchor | `E[max of 25]` |
| Rule 1 enforcement | `E[max of N]`, all four N |

**At zero noise every one of these reproduces today's figure to 2e-04**, so this is a strict
generalisation rather than a replacement — verified as self-check 1 of the tool.

Every preset's figures shift (Faithful's N=50 goes 0.3400 → 0.3727; the typical figure moves ≤0.2%
for seven presets and +1.71% for the retuned Wildcard), so **every pasted table regenerates.**

Every preset's Best-of-N figure rises somewhat (Faithful's own N=50 goes 0.3400 → 0.3727), so
`EnvelopeFigures.g.cs` and **every pasted table in `HANDOVER.md`** regenerate together. Rule 6.

---

## 5. Wildcard retune

| field | from | to | why |
|---|---|---|---|
| `passionNoise` | 0.85 | **0.50** | The only noise scalar that reaches Best-of-N power. |
| `passionMajorBias` | 0.6 | **0.35** | Nerfs through the pip *exchange rate*, not through spread. |
| `skillShiftMax` | 4.2 | **2.0** | Lowers the cherry-picked ceiling without touching noise. |

Unchanged: `skillNoise` 0.85, `skillShiftMin` −5.0, `passionCountMin/Max` 2.2/10.8,
`traitCountMin/Max` 0/8, `averageQuality` 0.37.

Result: **−11.5% / +5.1% / +14.7% / +17.7%** at N = 1/5/25/50. 17.3pp of envelope margin.

Two identity properties both survive, and they are the point of the retune:

- **Dispersion 1.71× Faithful** (realised per-pawn composite sd 0.0899 vs 0.0526). Wildcard remains
  decisively the widest preset.
- **Below Faithful at N=1, crossing above as N rises** — the documented signature of a variance
  preset, in fact strengthened (−11.5% against the shipped −4.7%).

`averageQuality` was considered as a lever and **rejected**: it is marginally the most
dispersion-efficient nerf, but it works by making every Wildcard pawn weaker rather than by changing
anything about how the preset behaves.

Lowering `passionMajorBias` is a nerf that buys flavour: Wildcard now scatters more shallow passions
instead of fewer deep ones, which reads more "wildcard" than the Major-heavy 0.6.

> **One tension to record.** `HANDOVER.md`'s left-censoring section says that if the goal is spread,
> the levers are `skillNoise` and the *upper* handle — and this retune lowers the upper handle. That
> guidance is still right in general; it is being traded against deliberately here, and the cost is
> already priced into the measured 1.71× rather than assumed away. The band narrows 9.2 → 7.0 levels
> while `skillNoise` is untouched, so the within-pawn scatter that the guidance is really protecting
> is unaffected. **Do not read this as licence to keep lowering the handle.**

> **Rule 7 trigger.** Changing `passionMajorBias` moves `R` for this profile:
> `R = (20/18)·(1.5/0.8)·eff(bias)`, so `R(0.6) = 1.99` becomes `R(0.35) = 1.91` skill levels per
> passion pip. Capacity `12·(1 + 0.5·bias)` shifts 15.6 → 14.1 pips — still non-binding, since the
> widest realised budget sits well under it. Recompute and repaste, per the recalculate-trigger
> list. This is exactly the case Rule 7 was corrected to cover: a per-profile field that moves `R`
> while touching none of the three global constants.

---

## 6. Noise sliders in real units

Both fields store the **standard deviation** directly, which is what the handle displays.

| field | was | becomes | range |
|---|---|---|---|
| `skillNoise` (0–1) | scalar | `skillSpread`, levels | 0 – 2.45 (`MaxMagnitude/√6`) |
| `passionNoise` (0–1) | scalar | `passionSpread`, pips | 0 – 4.0 |

> [!CAUTION]
> **The required conversion at the call site, stated explicitly because omitting it is the exact
> defect shape this project keeps shipping.**
>
> `SkillVarianceApplier.cs:71` applies `(TriangularSample()*2−1) * magnitude`, whose sd is
> `magnitude/√6`. So storing the **sd** means the applier must read:
>
> ```csharp
> float magnitude = v.skillSpread * Mathf.Sqrt(6f);   // NOT  = v.skillSpread
> ```
>
> The naive edit — replacing `Lerp(0, 6, skillNoise)` with a bare `v.skillSpread` — looks obviously
> correct and silently divides every profile's skill noise by 2.449. Wildcard's per-skill sd would
> land at 0.85 instead of 2.08, and **nothing would catch it**: the composite does not read
> `skillSpread` at N=1, `envelope_check.py` computes the sd from the same field so it would agree
> with itself, and the in-game gate compares the two implementations rather than either against
> reality. Only `Roll pawns and dump distribution` would show it.
>
> `passionSpread` needs **no** conversion — `PassionVarianceApplier.cs:62` already uses the value
> as the Gaussian's σ directly. **The two fields are not symmetric. Do not "tidy" them into one
> shared helper.**

> [!CAUTION]
> **A second, independent truncation bug in the same rename.** `VarianceProfile.cs:97-98` currently
> runs `Mathf.Clamp01()` on both fields, on every profile load, every editor edit, and every
> `SettingsTransfer` import. Under real units that silently clips the retuned Wildcard
> (`skillSpread` 2.08, `passionSpread` 2.00) and Distinct (`passionSpread` 1.40) **to 1.00**. It
> must become:
>
> ```csharp
> skillSpread   = Mathf.Clamp(skillSpread,   0f, Constants.MaxMagnitude / Mathf.Sqrt(6f));
> passionSpread = Mathf.Clamp(passionSpread, 0f, Constants.PassionBudgetSpreadMax);
> ```
>
> This is a *different* defect from the `√6` one above and is not fixed by fixing that. Same shape,
> same silence: nothing in the gate would catch either.

**The Python side renames too, and one of the three places is load-bearing.**

| `docs/tools/envelope_check.py` | What breaks if missed |
|---|---|
| `:71` — the field-name list `parse_profiles` pulls from `VarianceProfile.cs` | **Parse failure or silently absent fields.** This is the critical one. |
| `:198-199` — `make_spread` still applies the old `Lerp(lo, hi, noise)` | Dispersion table reports wrong sd |
| `:378-383` — dispersion table column headers | Cosmetic, but stale labels |

Also update the `Scribe` defaults at `VarianceProfile.cs:125-126` (`0.2f` / `0.25f`) to the rescaled
equivalents (`0.489898f` / `1.0f`), or every loaded profile silently picks up near-zero spread. The
field initialisers at `:28` / `:35` (`0.35f` / `0.25f`) are a separate pair and need the same
treatment (`0.857321f` / `1.0f`).

Preset rescale — `skillNoise × √6`, `passionNoise × 4`. **The table below is rounded for reading; the
committed literals must carry six decimals.** `EnvelopeFigures.g.cs` stores scores at six decimals,
and `0.49 × √6 = 1.20025` against a true `1.2000` is enough to rewrite the file — which would break
the rename's own "no number moves" check. Exact values are in the plan's Task 5 Step 1.

| preset | `skillSpread` (lv) | `passionSpread` (pips) |
|---|---|---|
| Faithful | 0.49 | 1.00 |
| Distinct | 0.86 | 1.40 |
| **Wildcard** | **2.08** | **2.00** |
| Desperate | 0.61 | 1.00 |
| Elite | 0.54 | 1.00 |
| Sovereign | 0.59 | 1.00 |
| Specialist | 0.61 | 1.00 |
| Scavenger | 0.61 | 1.00 |

Each slider carries two derived lines — the extreme, and the resulting outcome band in range form:

Worked example, the retuned Wildcard at its own median quality (`q ≈ 0.354`):

```
Skill spread     typical ±2.1 lv  [=====o===]
                 extreme ±5.1 lv per skill
                 most skills land 0.4 – 4.6

Passion spread   typical ±2.0 pips [====o====]
                 extreme ±8.0 pips
                 budget usually 3.2 – 7.2
```

The outcome-band line is computed from the spread **and** the profile's own band, so it updates when
either moves. That makes the interaction between the two visible — something no control shows today.

**The extreme line is not decoration.** Triangular noise has `sd = bound/√6 ≈ 0.41 × bound`, so a
label showing only the bound overstates the typical pawn by ~2.5×, and one showing only the typical
hides that a single skill can move 5 levels.

The mod is unreleased, so the field rename and rescale ship without a migration shim. A saved config
from before the change resets.

---

## 7. The curve

`DrawDistributionCurve` plots the realised outcome density — the analytic Gaussian mixture from §3,
not finite differences of `F` — instead of the Beta density of `q`. Consequences, all intended:

- moving either noise slider visibly widens or narrows the curve;
- left-censoring appears as a pile against the left edge, which is currently invisible everywhere in
  the UI and is the failure mode that cost this project a full retune;
- it remains a **single line**, so the rejected "second ghosted Best-of-N curve" stays rejected;
- it is still never greyed on read-only presets — it is a readout, not a control.

The axis label changes from quality to realised power, and the tooltip must say so.

---

## 8. Verification

### 8.1 The gap this closes

The C#/Python mirror catches the two implementations *diverging*. It cannot catch an error they
**share** — that is the documented blind spot behind the carried right-edge CDF slip, where the
0.5pp tolerance provably would not notice both sides drifting together.

Promoting the Monte Carlo to `docs/tools/dispersion_mc.py` narrows it. Sampling is an independent
*numerical method*, not a second copy of the same quadrature, so it catches shared **quadrature**
errors that neither mirror can.

> [!CAUTION]
> **It does NOT make the verification independent, and an earlier draft of this spec overclaimed
> that it did.** `envelope_check.py`'s `make_composite`, the MC's `make_realised`, and the grid's
> `moments()` all three substitute a flat `AssumedVanillaSkillBaseline = 5` for each skill's real
> vanilla-rolled level. The measured reality is ~3.37 (`HANDOVER.md`, left-censoring section). So
> the grid and the MC are two implementations of the **same simplified random variable**, not two
> views of the real one.
>
> Concretely: the MC would catch a mis-derived variance or a bad order-statistic estimator. It
> would **not** catch an error in the shared modelling assumption — which is exactly the class the
> left-censoring defect belonged to. **`Roll pawns and dump distribution` is the only instrument
> that sees real vanilla baselines**, and §8.3 keeps it as a manual spot-check outside the
> 0.5pp pipeline. That is a deliberate limit, not an oversight, but do not describe the
> verification story as "independent" without this qualifier.

### 8.2 What runs, and when

| Check | Trigger | Expectation |
|---|---|---|
| `envelope_check.py` | any scoring change (Rule 6) | PASS Rule 1 and Rule 2 at N = 1, 5, 25, 50 |
| Self-check: zero-noise grid vs analytic | every tool run | ≤1e-3 |
| In-game `Verify Best-of-N` | any scoring change | 32/32; both sides deterministic, so exact agreement |
| `dispersion_mc.py` | when the model changes, not per-commit | grid within 0.5pp of MC |
| `Roll pawns and dump distribution` | after the Wildcard retune | median and per-pawn sd confirm dispersion held |

The in-game gate must be **extended** to cover the dispersed score. A gate that still checks only
the old mean-band integral would pass while the number on screen came from untested code.

### 8.3 In-game measurement the retune requires

Run `Roll pawns and dump distribution` at 1000 pawns against Wildcard and confirm the realised
figures. Read the `ACTUALLY RESOLVED TO:` line — a race or faction override outranks the Active
Colony Profile, and that has already produced two invalid 1000-pawn runs on this project.

---

## 9. Rules and permissions

**Rule 8 sign-off: GRANTED by the owner for all five files**, on the list as it stood when this
spec was written. It covers the changes described here and nothing wider — a change that grows
beyond this spec needs its own permission.

The five files marked `DONE (REVIEWED)`:

| File | Why it is touched |
|---|---|
| `Source/PawnVarianceSettings.cs` | the new Best-of-N path and its cache |
| `Source/ProfileEditorTab.cs` | sliders and curve |
| `Source/VarianceProfile.cs` | field rename/rescale, `Clamp01` bounds at `:97-98`, `Scribe` defaults at `:125-126`, and the Wildcard values |
| **`Source/SkillVarianceApplier.cs`** | **`:57` must convert the stored sd back to a bound — the `√6` edit above** |
| **`Source/PassionVarianceApplier.cs`** | **`:62` reads the renamed `passionSpread` field** |

> The last two were missing from an earlier draft. **Both are `[x]` in the code-review status list**
> (`HANDOVER.md:1261` and `:1268`) — `PassionVarianceApplier.cs` was marked reviewed part-way
> through this design session, so a stale reading of that list will get this wrong. Re-read it
> before starting rather than trusting this table.

Unreviewed files also touched, so no Rule 8 gate but still uncounted work: `Source/DebugActions.cs`
(the new dump action, plus two **comments** at `:326` and `:721` that name the old fields).

> [!CAUTION]
> **Correction — `Source/SettingsTransfer.cs` was listed here as unreviewed. It is not.**
> `HANDOVER.md:1257` marks it `[x]` DONE (REVIEWED), so it sits under the Rule 8 gate and is **not**
> covered by the sign-off above, which was granted against the five-file list. Nothing here
> authorises editing it.
>
> It also does not need editing. It round-trips profiles through
> `VarianceProfileValues.ExposeData()` and contains **zero** references to `skillNoise` /
> `passionNoise`, so the `Scribe` rename in §6 carries import/export automatically. The claim that
> it needed "import/export of the renamed fields" was wrong on the facts as well as on the gate.
>
> If implementation turns up a genuine need to touch it, **stop and get owner permission first.**

One more file the earlier draft missed: **`Source/PawnVarianceSettings.cs:1362` declares
`PassionPipEfficiency` `private static`**, and the new `DispersionModel` calls it. It must widen to
`internal static` or the model does not compile (CS0122). That file *is* inside the sign-off, so this
is scope already granted — but it is real work, not a free call.

**Rule 5 consultation** — profile parameters and the meaning of the percentage bounds both move.
Covered by the decisions in §2, which were taken by the owner.

**Rule 1** is now enforced on the dispersion-aware figure. The `HANDOVER.md` section "What the
envelope does NOT measure — `skillNoise` and `passionNoise`" is largely obsolete and must be
rewritten: the envelope now measures it. The parts worth keeping are the mechanism (why per-skill
noise averages down) and the warning against reading dispersion out of the mean band.

**Rule 6** — `EnvelopeFigures.g.cs` and every pasted table regenerate.

---

## 10. Risks and carried limitations

| Risk | Mitigation |
|---|---|
| A more complex integral implemented twice. | The in-game gate, plus the MC as an independent method. **Honest limit: the gate's 0.5pp tolerance still cannot detect the two quadrature sides drifting together. Only `dispersion_mc.py` can, and only if it is actually run.** |
| The Normal-per-q approximation degrades for extreme `passionNoise`. | Measured: 0.08pp at the shipped value, 1.5pp at 0.85. Documented in `HANDOVER.md` so a future retune knows the failure mode. |
| **Per-frame cost during a slider drag.** The single-slot cache has a ~0% hit rate while a slider is *moving* — the key changes every frame — so each dragged frame pays the full 131k evaluations, each with an `Erf` call, single-threaded in Mono. | **Mitigation is mandatory, not conditional:** drop to q=64/x=128 while the drag is live and recompute at full resolution on release. Rationale for not profiling first — the measured accuracy cost of the cheap grid is **0.001pp**, i.e. free, so there is no trade to evaluate. Both reviewers independently flagged likely stutter; neither benchmarked it, and **the 5–15ms estimate remains unverified**. Taking free insurance beats either building on a guess or shipping on one. |
| **A custom profile with high `passionSpread` drifts from reality while the gate stays green.** The gate compares C# against Python, both running the *same* Normal-per-q approximation, so they agree to ~0.000pp while both sit ~1.5pp from what pawns actually roll. | Real, and it outlives the retune: `dispersion_mc.py` runs when the model changes, never against player-authored profiles. Accepted — the error is ≤1.5pp at the extreme and the score is display-only. **Must be written into `HANDOVER.md`, not just here**, since the shipped presets no longer exercise the failing case and a future reader will not rediscover it. |
| The curve changing meaning confuses an existing mental model. | No existing users — the mod is unreleased. Axis label and tooltip both change. |
| The right-edge CDF slip is inherited by the new integral. | Carried deliberately, per the settled decision. It cancels in the ratio to Faithful exactly as before. Do not fix it here. |

---

## 11. Implementation phasing

Large enough to be worth sequencing. Every phase boundary leaves the build green and the gates
passing — which is exactly why 2 and 3 are fused rather than split.

| Phase | Content | Done when |
|---|---|---|
| 1 | `DispersionModel.cs` + the `envelope_check.py` mirror + `dispersion_mc.py`. No UI, no retune. | Zero-noise self-check ≤1e-3; grid within 0.5pp of MC on all 8 presets. Gates untouched, still green. |
| **2+3** | **One atomic commit.** Wire the dispersed score into the Best-of-25 anchor and Rule 1, extend the in-game gate, retune Wildcard, regenerate `EnvelopeFigures.g.cs` and every table. | Gate 32/32 in game; `envelope_check.py` PASSes with Wildcard at ≈+17.7%; `Roll pawns and dump distribution` confirms dispersion held. |
| 4 | Slider units and readouts, and the outcome curve. | Visual check in game; preset rescale leaves every figure unmoved. |

> **Why 2 and 3 are atomic and not two commits.** Switching the metric on *before* retuning puts
> Wildcard at ≈+48%, and `envelope_check.py:401` returns exit code `1` on any Rule 1 breach. That
> boundary therefore cannot be green by construction — an earlier draft of this spec asked for a
> PASS and predicted a breach in the same sentence. Committing them together is the honest fix; the
> alternative is a commit that cannot satisfy the project's own gate.
>
> **The ordering within that commit still matters.** Land the metric first, then tune against it.
> Retuning Wildcard while the score is still blind to dispersion means tuning against the wrong
> number, which is the mistake this whole spec exists to correct.

## 12. Out of scope

- Making `CalculateCompositeScore` itself dispersion-aware (decision 2 keeps it mean-only).
- Fixing the shared right-edge CDF slip — settled as permanently carried.
- Asymmetric noise, and any second range-control kind.
- The trait axis. Trait count still does not enter the composite; Rule 3 is untouched.
- Retuning any preset other than Wildcard. Every other profile moves ≤1.5pp under the new metric
  and stays comfortably inside the envelope.
