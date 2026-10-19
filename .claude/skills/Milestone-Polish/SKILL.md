---
name: milestone-polish
description: Create a plan for polishing the current milestone choosing 10 issues from the open `backlog` GitHub issues
---

Choose about 10 items from the open `backlog`-labelled GitHub issues
(`gh issue list --state open --label backlog --json number,title,labels`, per
`docs/agents/issue-tracker.md`). Ask me for criterias but provide me with a default.
Device a plan to polish the current Milestone from these items, citing each by its `#N`. Check if
the chosen items are already done: an issue's state is `gh issue view N --json state`. A `BL-NNN`
cited anywhere is a closed historical item (`git log --grep=BL-NNN`), never a candidate.
Ignore items from future Milestones.
/grill-with-docs if something is unclear.
use /new-plan to scaffold the plan
