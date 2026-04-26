---
name: unit-testing
description: Add or improve unit tests for relevant new or changed logic so that behavior is validated at the right level of isolation for the current stack.
---

# Unit Testing

Use this skill when acting as the developer and the current change introduces or modifies behavior.

## Goals
- Validate behavior at unit level.
- Keep tests fast, focused, and readable.

## Required Inputs
- Changed implementation
- Requirements and design context

## Required Output
- Unit tests for relevant new or changed behavior

## Rules
- Test behavior, not internal trivia.
- Keep tests small and intention-revealing.
- Cover nominal paths, error paths, and meaningful edge cases.
- Prefer deterministic tests.
- Use the most natural unit boundary for the stack: function, class, module, component, or equivalent.
