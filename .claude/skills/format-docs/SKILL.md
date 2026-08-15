---
name: format-docs
description: Author, restructure, or audit CSVM's `docs/formats/` reference. Use when adding a format decode, improving a format family's readability, splitting an independently useful format subtopic, or checking format documentation against its navigation, evidence, and reader-path contract.
---

# Format Docs

Read `PROJECT_CONTEXT.md`, `docs/formats/README.md`, and the target family before editing. Read `docs/formats/gotchas.md` before describing a reader, transform, or shader rule.

## Modes

- **Author:** add or materially extend a family page alongside a decoder change.
- **Restructure:** improve navigation and progressive disclosure without changing a technical claim.
- **Audit:** compare pages against the contract; report gaps without editing unless asked.

## Contract

Read [the page contract](references/page-contract.md). Keep each existing `family.md` as its canonical landing page. Create an independent child topic under `family/`, never solely because of length; assess a page above 25–30 KB. Keep every child’s parent link and the root index nested link in sync.

Use the reader path where applicable: At a glance, conceptual model, reference, reader rules and edge cases, Evidence & limits. Preserve claims and local-capture citations. State what is true now; remove change-history narration and past-state comparisons. Retain dates only when they identify evidence needed to assess a claim. Put proof beside disputed claims and a compact evidence map at the end of a landing page.

## Finish

Check links and heading order. Report semantic changes separately from structural edits. Never add game assets, bulk values, or executable disassembly.
