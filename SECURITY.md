# Security Policy

## Supported Versions

ShadowGuard is currently maintained on the `main` branch. Security fixes are applied to the latest source version in this repository.

## Security Model

ShadowGuard is designed as a local supply-chain security analysis tool.

Current security boundaries:

- It scans local project dependency manifest files.
- It does not execute dependencies from the scanned project.
- It does not modify the scanned project source code.
- It only writes reports or SBOM files when the user explicitly exports them.
- Plugin files are read from the local `plugins/` directory and interpreted as JSON rule definitions.

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

- Add automated security scanning in CI.
- Add schema validation for plugin JSON files.
- Add regex timeout handling for plugin rules using regular expressions.
- Add scan diagnostics instead of silently ignoring parser failures.
- Add signed release artifacts and checksums.
