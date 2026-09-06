# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the actual label strings used in this repo's issue tracker.

| Label in mattpocock/skills | Label in our tracker | Meaning                                  |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label string from this table.

The internal tracker is markdown, not a labelled issue system, so there is nothing
there to apply a label *to*: record the role inline in the `backlog.md` entry as a
`Status: <role>` line. A public GitHub issue does carry real labels, and the strings
above are the ones to create and use there; the bug form applies `bug` by itself.
See `issue-tracker.md`.

Edit the right-hand column to match whatever vocabulary you actually use.
