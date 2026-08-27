# Security Policy

## Reporting a vulnerability

Please report security issues privately, not as a public issue.

Use GitHub's [private vulnerability reporting](https://github.com/Rune-Pune/entra-rdp-connect/security/advisories/new) — it opens a channel visible only to the maintainer. Expect a first response within a week; this is a spare-time project, not a staffed product.

Please include what an attacker could achieve, the steps to reproduce, and your distribution and FreeRDP version. Redact host names, user principal names and tenant IDs from anything you paste.

## What is in scope

The parts of this project worth scrutiny:

- **The OAuth authorization code.** It is read out of browser history, written to the RDP process's stdin, and must never reach a log file. `ERC_RDP_DEBUG=1` masks it deliberately — a path that leaks it is a real finding.
- **The dedicated browser profile.** It holds a live signed-in session and is created with `0700`. Anything that widens those permissions, or leaves the profile behind after a run, matters.
- **The three `pkexec` call sites** — raising the VPN, installing packages, and appending to `/etc/hosts`. Arguments are quoted and package names are looked up in the app's own list rather than taken from the caller. A way to get arbitrary arguments or package names through is in scope.
- **The configuration file**, written `0600`. It stores no passwords by design; a change that starts storing a secret is a finding in itself.

## What is out of scope

- Vulnerabilities in FreeRDP, WireGuard, `uwg-quick`, browsers or the .NET runtime. Report those to the projects concerned
- Anything that requires an attacker to already have your user account on the machine — at that point the browser profile and config are theirs anyway
- The use of `/cert:ignore`. It is a deliberate trade-off: the Entra P2P certificate rotates every few hours, and the connection is already protected by the tunnel and by Entra/NLA. The reasoning is in the README, and an argument that it is wrong is welcome as a normal issue

## Supported versions

The latest release only. This is a single-maintainer project with no backport capacity.
