# Security Policy

## Supported Versions

ShadowGuard is currently maintained on the `main` branch. Security fixes are applied to the latest source version in this repository.

## Security Model

ShadowGuard is designed as a local supply-chain security analysis tool.

Current security boundaries:

- It scans local project dependency manifest files.
- It does not execute dependencies from the scanned project.
- It does not modify the scanned project source code.
- It writes reports, SBOM files, validation results, or vulnerability query results only when the user explicitly exports them or passes an output path to the CLI.
- Plugin files are read from the local `plugins/` directory and interpreted as JSON rule definitions.
- Plugin regex matching uses a timeout to reduce the risk of inefficient or malicious regular expressions blocking scans.
- OSV vulnerability lookup is opt-in and only runs when the user explicitly passes `--vuln` to the CLI.
- GitHub Security Advisory identifiers are currently surfaced through OSV `GHSA-*` aliases; the project does not call the GitHub Advisory GraphQL API directly.

## Network Behavior

Default desktop scanning and default CLI scanning are local-first and do not require network access. Network access is used only for explicit online vulnerability lookup through OSV.

## Reporting a Vulnerability

If you find a vulnerability, please open a private security advisory if available, or create an issue with enough detail to reproduce the problem.

Please include:

- Affected version or commit SHA.
- Operating system and .NET SDK version.
- Steps to reproduce.
- Expected behavior and actual behavior.
- Any sample manifest or plugin file required to reproduce the issue.

## Recommended Hardening Work

The following work items are recommended for future versions:

- Add signed release artifacts and checksums.
- Add official CycloneDX JSON Schema based validation.
- Add scan diagnostics instead of silently ignoring parser failures.
- Add direct GitHub Advisory GraphQL integration for authenticated environments.
- Add desktop UI support for vulnerability query results.
