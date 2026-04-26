# Clean Code Standard

This file defines the engineering baseline for architecture and implementation in this repository.

## Core Principles
- Prefer clarity over cleverness.
- Keep the design and code as simple as possible.
- Make code easy to read, test, change, and reason about.
- Optimize for maintainability first, while explicitly weighing CPU and memory impact where relevant.
- Use modern, stable language and ecosystem practices when they improve the result.

## Structure and Design
- Apply separation of concerns.
- Keep business logic, orchestration, infrastructure, and I/O separated where practical.
- Follow the single responsibility principle.
- Design for high cohesion and low coupling.
- Use clear module and component boundaries.
- Hide internal details and expose only what other parts need.
- Encapsulate change so a change in one concern has minimal impact elsewhere.
- Prefer composition over inheritance unless inheritance is clearly simpler and safer.
- Use dependency inversion and interface segregation where they reduce coupling and improve testability.
- Keep one source of truth for each important concept.
- Apply KISS by default.
- Apply YAGNI aggressively; do not design for hypothetical future needs.

## Naming and Readability
- Use intention-revealing names.
- Choose names that reflect domain meaning rather than implementation trivia.
- Keep control flow obvious and readable.
- Avoid boolean soup, deep nesting, and surprising shortcuts.
- Prefer explicitness when it improves understanding.
- Keep functions, classes, modules, and components focused and reasonably small.

## Logic and State
- Minimize side effects and make them explicit when necessary.
- Prefer predictable data flow.
- Minimize mutable shared state.
- Keep invalid states hard to represent where applicable.
- Make error handling explicit, predictable, and easy to follow.
- Fail fast when that improves correctness and debuggability.

## Duplication and Abstraction
- Remove harmful duplication, but do not force abstraction too early.
- Avoid unnecessary indirection.
- Add abstraction only when it is justified by current complexity, reuse, or clarity.
- Keep interfaces small and focused.

## Testability
- Design for testability from the start.
- Keep units isolated enough to be tested directly.
- Avoid designs that require broad integration setup for simple behavioral tests.
- Treat unit tests as mandatory for relevant new or changed logic.
- Add automated acceptance or end-to-end tests when they are feasible and valuable.

## Performance and Resource Use
- Consider CPU time and memory usage in design decisions.
- Do not optimize blindly, but do not ignore obvious waste.
- Make meaningful performance tradeoffs explicit in the design.
- Prefer simple solutions unless scale or runtime characteristics justify extra complexity.

## Quality Practices
- Leave the code cleaner than you found it where practical.
- Keep formatting and conventions consistent.
- Avoid hidden magic and surprising behavior.
- Prefer explicit contracts between components.
- Document only what is needed to understand intent, constraints, or non-obvious decisions.
