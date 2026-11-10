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
- **Shared code.** Game-neutral code (the Unreal save engine, pak reading, the editor stamp, and the WPF
  theme, dialogs and image service) lives in `shared/` as ordinary projects of this repo: edit, ticket
  (`M5SE`) and commit it like the rest. It used to be a git subtree of `mindattic/Mods.MechWarrior5.Shared`
  (ticket code `MM5S`); that repo was folded in and archived on 2026-11-10 because this app was its only
  user (M5SE-T90). There is no subtree to pull or push. If a second MechWarrior tool ever needs it, split
  it back out with `git subtree split --prefix shared`.
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
   `obj/package/MW5SaveEditor/` and zips it to `dist/MechWarrior5.SaveEditor-<version>.zip`. That staged folder is
   the payload to hash.
2. **Hash:** the payload has no per-publish files, so pass no `-ExcludeNames`.
3. **Version:** the canonical version is `<Version>` in `Directory.Build.props`. Whole-number versions only,
   like every MindAttic app: each release bumps the major (1.0.0, 2.0.0, 3.0.0...), never a minor or patch.
   The owner lifted the 1.0.0 hold on 2026-11-10 and set 2.0.0 for the UI rewrite release.
4. **Changelog:** keep a `## Changelog` section in `README.md`, newest first.
5. **Release:** cut a GitHub release with the zip attached; the **exe zip is downloaded from GitHub only**. NexusMods
   quarantines executables, so `.github/workflows/publish.yml` uploads a pointer instead to every target, using
   `NEXUSMODS_API_KEY`: `MechWarrior5.SaveEditor-<version>-GitHub.zip`, holding one self-contained page
   built from `tools/nexus-pointer.html` with a link to `releases/latest`. On each page the "Mod manager download"
   button is removed and a GitHub mirror points at `releases/latest` (both set by hand).

While any target's `fileId` is `0`, stop and say which page still needs its first file uploaded by
hand. Then put that file's upload-API **File ID** in `fileId`: on the page, Manage files, the file's menu,
**Advanced**, then the **File ID** under **API** (e.g. Mercenaries 8353341, Clans 8353350), and the **Unique Mod ID** beside it in `modUid` (Mercenaries
13310103651970, Clans 29785598197830): the changelog step needs it. The key in `NEXUSMODS_API_KEY` must be the
**MindAttic** NexusMods account's (it owns both pages); the `nexus-files` workflow prints which account a key is. It is neither the per-page
`file_id` nor the `uid` that the v1 files.json lists (the read-only `nexus-files` workflow shows those); both are
rejected by the upload API.
