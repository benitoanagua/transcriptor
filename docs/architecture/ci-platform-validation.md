# CI platform validation

Transcriptor currently supports Windows as its end-user desktop platform. The Linux CI job is an architectural guardrail, not a claim that the desktop application is available on Linux.

## Validation lanes

| Lane | Runner | Scope | What it proves |
| --- | --- | --- | --- |
| Portable layer (Linux) | Ubuntu | `Transcriptor.UI`, `Transcriptor.Transcription`, the headless `Transcriptor.Dev` host, and the portable test suite | Shared projects compile and their automated/headless checks run without Windows-only target frameworks or the Windows platform adapter. |
| Windows (full) | Windows | All production, platform, setup, development, and test projects | The currently supported Windows desktop product builds and its Windows-specific tests pass. |

## Why keep both lanes?

- The portable lane catches accidental OS coupling early, before another desktop platform is implemented.
- The Windows lane remains authoritative for the supported product and Windows integrations.
- Both lanes build the same repository and shared projects. They are not separate product implementations or competing development tracks.
- Working on Windows with WSL is compatible with this arrangement: Linux CI provides a clean Linux environment rather than relying on the developer's local WSL configuration.

## Platform support policy

A passing portable Linux build does **not** mean that Transcriptor's desktop application is supported on Linux. Product support requires a platform adapter, packaging/distribution, runtime validation, and platform-specific tests for that OS.

When another desktop platform is introduced, add its adapter and full-platform CI lane while retaining the portable lane. Do not fork shared domain, transcription, or UI logic merely to satisfy a platform.

## Naming

Use **Portable layer (Linux)** for the Linux job and **Windows (full)** for the currently supported desktop product. Avoid names such as "Linux supported" or "Linux desktop" until end-user Linux support is actually delivered.
