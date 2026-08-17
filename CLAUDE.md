# Project Notes for Claude Code

## Memory

Capture memories proactively as they happen — don't wait until asked, and don't batch them at the end of a session. Concrete triggers, all seen in real sessions on this project:

- A root cause turns out to be a non-obvious, hard-to-rediscover fact (a third-party package bug, a framework-level gap, a stale-build-artifact gotcha) — save it the moment it's confirmed, not after the whole task wraps up.
- The user gives explicit feedback or pushback on approach (e.g. "that's a hack, find the real cause", "audit the whole system before patching one instance") — save it right then, not in a retrospective batch.
- A fact about the Core/NetCore shared-framework governance process, or another cross-project convention, gets used or refined — these decay slowly and are worth keeping current.

If the user has to ask "did you save anything about this?", treat that as a signal memory-writing is already overdue, not as the trigger to start.
