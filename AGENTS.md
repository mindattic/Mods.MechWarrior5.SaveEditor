# Mods.MechWarrior5.SaveEditor

Read [`../AGENTS.md`](../AGENTS.md) and the shared `mindattic-agent-standard/` first. Project-specific
context only:

- **Names.** Repo, local folder and ticket key are all `Mods.MechWarrior5.SaveEditor` (ticket code `M5SE`;
  renamed from `MW5-SaveEditor` on 2026-11-09, old IDs still resolve). Never push to the `wmtorode`
  remote: it is the original upstream.
- **One app, two games.** `src/MechWarrior5.SaveEditor.App` hosts a Mercenaries shell and a Clans shell,
  switched by the title-bar game tabs (`ShellViewModel`).
  - Mercenaries logic: `src/MechWarrior5.Mercs.SaveEditor.Core`.
  - Clans logic: `src/MechWarrior5.Clans.SaveEditor.Core`.
  - Clans pages: `src/MechWarrior5.SaveEditor.App/Clans/`.
- **Shared code.** Shared code lives in `shared/`, a git subtree of `mindattic/Mods.MechWarrior5.Shared`
  (ticket code `MM5S`), the library for future MechWarrior projects. Put game-neutral code there. Pull
  with `git subtree pull --prefix shared https://github.com/mindattic/Mods.MechWarrior5.Shared.git main
  --squash`, and send local changes back with `git subtree push` (a cross-repo action).
- **Saves are sacred.** Never overwrite an unstamped (original) game save. Always work on copies of
  real saves:
  - Mercenaries: `%LOCALAPPDATA%\MW5Mercs\Saved\SaveGames`.
  - Clans: `%LOCALAPPDATA%\MW5Clans\Saved\SaveGames`.
- **Verification.** This CLI has no desktop, so check UI by rendering pages offscreen with a
  RenderTargetBitmap harness.

## Publishing (`/publish`)

`nexus.json` is a third kind of publish config, beside the RogueTrader and GTFO ones:

1. **Build:** `pwsh tools/package.ps1` writes a self-contained single-file `MW5SaveEditor.exe` to
   `obj/package/MW5SaveEditor/` and zips it to `dist/MW5SaveEditor-<version>.zip`. That staged folder is
   the payload to hash.
2. **Hash:** the payload has no per-publish files, so pass no `-ExcludeNames`.
3. **Version:** the canonical version is `<Version>` in `Directory.Build.props`. It starts at 0.0.0, so the
   first publish makes it 1.0.0, then 2.0.0 and so on.
4. **Changelog:** keep a `## Changelog` section in `README.md`, newest first.
5. **Release:** cut a GitHub release with the zip attached. `.github/workflows/publish.yml` then uploads
   it to NexusMods with `NEXUSMODS_API_KEY`.

While `modId` or `fileId` in `nexus.json` is `0`, stop and say so: the page is created by hand first
(see `docs/NEXUS_PAGE.md`, "One-time setup").
