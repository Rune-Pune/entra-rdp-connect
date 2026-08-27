# Contributing

Thanks for taking an interest. This is a small project with a narrow purpose, so a quick word on what fits and how to get a change in.

## What fits

The tool does one thing: get a Linux desktop onto an Entra ID-joined Windows PC without fighting the OAuth code. Changes that make that more reliable, work on more distributions, or support another VPN or browser are welcome.

Things that are deliberately out of scope: managing multiple saved connections, storing passwords, and anything that needs a service running in the background.

If you are unsure whether an idea fits, open an issue before writing code. It is a cheaper conversation than a closed pull request.

## Getting it running

```bash
git clone https://github.com/Rune-Pune/entra-rdp-connect.git
cd entra-rdp-connect
dotnet build && dotnet test
dotnet run --project src/EntraRdpConnect.App
```

You need the .NET 10 SDK. You do **not** need a working VPN or an Entra-joined machine to develop against — the whole connection chain runs in unit tests against fake adapters, which is much of the point of the architecture.

## Architecture, in one rule

Dependencies point inward. `Core` holds the domain, the application logic and the ports, and performs **no I/O whatsoever** — no processes, no files, no sockets, no user-facing text. Everything that touches the outside world is an adapter in `Infrastructure`. The CLI and the GUI are driving adapters, each with its own composition root.

The practical test: if you find yourself adding `System.Diagnostics.Process` or a translated sentence to `Core`, it belongs somewhere else.

Supporting a different VPN client or browser means implementing one interface and registering it in the composition root. Nothing else should need to change — if it does, that is worth a comment in the pull request.

## Text and translations

User-facing text lives in `src/EntraRdpConnect.App/Localization/`. English (`Strings.resx`) is the neutral language; other languages sit beside it.

- **Adding a string:** put it in `Strings.resx` *and* every translated file. A test fails and names the key if you miss one.
- **Adding a language:** copy `Strings.resx` to `Strings.<code>.resx`, translate the values, and add the code to `Localizer.Languages`.
- **Error codes:** validation errors and command failures are enums, not sentences. Add the enum value, then a matching `Error<Name>` or `Failure<Name>` string. A test enforces the pairing.

The CLI is English-only by design — it is a troubleshooting tool.

## Pull requests

`main` is protected: every change goes through a pull request, and CI has to be green before it can merge. That applies to the maintainer too, so you are not being held to a different standard.

- Branch off `main`, keep the change focused on one thing
- Run `dotnet build && dotnet test` before pushing — CI runs the same, plus a single-file publish smoke test
- **Write commit messages and code comments in English.** Explain *why*, not what the diff already shows
- Do not include machine names, user principal names, IP addresses, tenant IDs or personal paths — not in code, not in tests, not in commit messages. This repository is public and its history is permanent

New contributors' workflow runs need manual approval before CI starts, so there may be a short wait on your first pull request. That is a GitHub default, not a comment on your patch.

## Reporting bugs

Use the issue templates — they ask for the distribution, FreeRDP version and browser, which is almost always what the answer depends on. Redact host names and UPNs from any logs you paste.

Security issues go through the process in [SECURITY.md](SECURITY.md) instead, not a public issue.
