# Contributing

This repository uses Conventional Commits, a versioned Git hook, and a small modular project layout so changes can stay focused and reviewable.

## Local setup

Enable the repository Git hook and commit template:

```bash
git config core.hooksPath .githooks
git config commit.template .gitmessage
chmod +x .githooks/commit-msg
```

Install the required SDK and restore dependencies:

```bash
dotnet restore OpenNist.slnx
```

If you are working on the web app:

```bash
cd src/web/open-nist-site
bun install
```

## Build and test

Build the .NET solution:

```bash
dotnet build OpenNist.slnx
```

Run the .NET test suite:

```bash
dotnet test --project tests/OpenNist.Tests/OpenNist.Tests.csproj
```

Run the web app checks:

```bash
cd src/web/open-nist-site
bun run format:check
bun run lint
bunx tsc -b && bunx vite build
```

If you touch the browser-hosted .NET layer, resync the published WebAssembly assets before validating the web app:

```bash
cd src/web/open-nist-site
bun run wasm:sync:debug
```

## Commit messages

This repository uses Conventional Commits for regular commits:

```text
<type>(optional-scope): <summary>
```

Examples:

- `feat(nist): add fixture-based round-trip coverage`
- `fix(wsq): preserve comment parsing on inspect`
- `perf(nfiq): reduce managed allocation in feature extraction`
- `docs(repo): add quickstart and troubleshooting guides`

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

- Scope is optional, but recommended when the change is clearly tied to one package or area.
- Use `!` for breaking changes, for example `feat(nist)!: rename binary record model`.
- Merge commits and `git revert` commits are exempt from the validation rule.

## Project conventions

- Prefer focused changes over broad refactors.
- Preserve exact output where the repository already has byte-for-byte regression tests.
- For NFIQ work, keep score calculations unchanged unless the change is intentionally behavioral and covered by updated tests.
- For WSQ and NIST work, avoid changing wire-format output unless that change is explicitly intended and validated.

## Documentation

User-facing documentation lives in [docs/README.md](docs/README.md). When you add or change public behavior, update the relevant quickstart, how-to, reference, or troubleshooting page in the same change.
