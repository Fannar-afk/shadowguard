# Changelog

## Unreleased

### Added

- Added MIT License.
- Added security policy in `SECURITY.md`.
- Added third-party notices in `THIRD_PARTY_NOTICES.md`.
- Added demo workspace case study in `case-studies/demo-workspace-scan.md`.
- Added Windows GitHub Actions CI workflow.
- Added Windows installer script under `package/`.
- Added CI artifact uploads for the Windows installer and portable win-x64 build.
- Added lightweight verification project `ShadowGuard.Tests` for core behavior smoke checks.
- Added `CONTRIBUTING.md` with build and contribution guidance.

### Changed

- Rewrote `README.md` as normal project documentation with installation, usage, plugin, build, packaging, architecture, development, security, and license sections.

### Notes

The lightweight verification project intentionally avoids external test framework packages so that this update does not introduce additional third-party package license considerations.