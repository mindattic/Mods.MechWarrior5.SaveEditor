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

`nexus.json` is a third kind of publish config, beside the RogueTrader and GTFO ones. It has **one
package and two NexusMods pages** (`targets`): MechWarrior 5: Mercenaries (mod 1666) and MechWarrior 5:
Clans (mod 70). Every release goes to both.

1. **Build:** `pwsh tools/package.ps1` writes a self-contained single-file `MW5SaveEditor.exe` to
   `obj/package/MW5SaveEditor/` and zips it to `dist/MW5SaveEditor-<version>.zip`. That staged folder is
   the payload to hash.
2. **Hash:** the payload has no per-publish files, so pass no `-ExcludeNames`.
3. **Version:** the canonical version is `<Version>` in `Directory.Build.props`. 1.0.0 is the first file,
   uploaded by hand; bump the leading integer from there.
4. **Changelog:** keep a `## Changelog` section in `README.md`, newest first.
5. **Release:** cut a GitHub release with the zip attached. `.github/workflows/publish.yml` uploads it to
   every target, using `NEXUSMODS_API_KEY`.

While any target's `fileId` is `0`, stop and say which page still needs its first file uploaded by
hand. Then read its file ID from `https://api.nexusmods.com/v1/games/<gameDomain>/mods/<modId>/files.json`,
or from the page's Manage files, then the file's edit menu, then API info.
