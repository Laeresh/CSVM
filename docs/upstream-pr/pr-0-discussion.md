# Pre-PR question to upstream (issue or discussion)

**Status: POSTED as [TerranMechworks/mech3ax#3](https://github.com/TerranMechworks/mech3ax/issues/3)**
(2026-07-21). The text below is what went out; it predates the AI-disclosure decision, so see
"Follow-up comment" at the bottom for the disclosure to add to the thread.

**Why:** the CS `gamez` PR is the large one, and how much polish it deserves depends entirely
on whether upstream *wants* CS back in the unified architecture. Asking first costs nothing.
Post this before or alongside the anim PR; don't wait on it to open the anim PR.

**Suggested title:** Was dropping Crimson Skies `gamez` support in 0.7.0 a bandwidth call or an
architectural one?

---

Hi, I've been working on Crimson Skies support in a fork, and before I open a larger PR I'd
like to check what you'd actually want.

`7f592ec` removed CS `gamez.zbd` support ahead of the RC/MW/PM common-infra refactor. I've
ported it forward onto the current unified `GameZ`/`Node` API (not a revert, no
`GameZDataCs`/`NodeCs` revival; CS's lod/window/display/object3d node data reuses the shared
readers unchanged), and all nine archives of a retail install round-trip byte-identically,
including `planes.zbd`. That last one also closes a 72-byte `planes.zbd` diff that turned out
to be a general `Ascii` suffix asymmetry rather than anything CS-specific.

The port does need a little new API surface, all optional and absent for MW/PM/RC, a
`GameZMetadata.model_slots` list (CS interleaves live models with free slots, so this keeps
`models` a dense `Vec<Model>` instead of forcing `Vec<Option<Model>>` on all four games), a
handful of optional CS fields, and one change visible to the other games' JSON:
`Partition.x`/`z` widened `u8` → `i16`, because CS stores partial/bogus partition values like
x == −1 that `u8` can't hold (MW/PM/RC values themselves are unaffected).

So the question is which of these the removal was:

1. **Bandwidth**, CS was a fourth game to carry through a big refactor and something had to
   give. In that case I'd be happy to open the PR and iterate on it.
2. **Architectural**, CS genuinely doesn't belong in the shared abstractions, and folding it
   back in would cost more than it's worth to you. That's a completely reasonable answer; I'd
   just keep the fork rather than push a PR you'd have to carry.

Either way I'm also opening a separate, much simpler PR adding `cam_anim.zbd`/`mis_anim.zbd`
support, which is purely a gap-fill and shouldn't conflict with anything.

Thanks for mech3ax, the round-trip test harness in particular made all of this tractable.

---

## Follow-up comment (post to issue #3)

The issue went out before we settled on disclosing AI assistance, so this belongs in the
thread, ideally before either PR lands in front of a reviewer.

---

One thing I should have said upfront, sorry: this work was done with the help of Claude Code
(Anthropic's agentic coding tool). The reverse engineering, the implementation and the
verification were all AI-assisted, and the commits carry a `Co-Authored-By: Claude` trailer.

I've reviewed the result and I'm putting my name to it, and the round-trip evidence is
mechanical rather than a judgement call, your own `test.py` harness reports `--- ALL OK ---`
byte-identical across the whole retail install for both tracks. But you should know how it was
produced when you decide how closely to read it, and it's entirely fair if that changes your
answer about whether you want CS back in the tree at all.
