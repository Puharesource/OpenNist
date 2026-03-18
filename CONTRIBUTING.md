# Contributing

## Commit messages

This repository uses the Conventional Commits format for all regular commits:

```text
<type>(optional-scope): <summary>
```

Examples:

- `feat(core): add shared binary reader abstraction`
- `fix(nist): validate record separators`
- `docs: document package roadmap`
- `chore(repo): tighten analyzer settings`

Allowed commit types:

- `build`
- `chore`
- `ci`
- `docs`
- `feat`
- `fix`
- `perf`
- `refactor`
- `revert`
- `style`
- `test`

Notes:

- Scope is optional but recommended when a change is clearly tied to one package or repo area.
- Use `!` for breaking changes, for example `feat(core)!: rename image payload abstraction`.
- Merge commits and `git revert` commits are exempt from the validation rule.

## Local setup

The repository includes a versioned Git hook in `.githooks/commit-msg` and a commit message template in `.gitmessage`.

To enable them in a local clone:

```bash
git config core.hooksPath .githooks
git config commit.template .gitmessage
chmod +x .githooks/commit-msg
```
