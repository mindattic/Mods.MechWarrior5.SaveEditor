# Screenshots for the README and the NexusMods page

Every screenshot is generated from the real app, off-screen, at 1920×1080, on sandbox copies of real
saves, so they always show the current UI:

```
dotnet test tests/MechWarrior5.SaveEditor.Headless.Tests --filter Category=Screenshots
```

It overwrites the files in place, keeping their names:

- `assets/mercs-01..10-*.png` and `assets/clans-01..10-*.png`: the README gallery.
- `assets/nexus/01..06-*.png`: the NexusMods page's six images. The description in
  [NEXUS_PAGE.md](NEXUS_PAGE.md) links them straight from GitHub (`main`), so pushing new ones updates
  the page's description by itself; only the page's media gallery needs the files re-uploaded.

| # | File | What it shows |
| --- | --- | --- |
| 1 | `01-requisition-tick-shift-click-add.png` | Mercenaries Mechs page with several requisition rows ticked. |
| 2 | `02-mech-lab-hardpoint-aware-refits.png` | The Mech Lab on its Weapons tab. |
| 3 | `03-clans-resources-kerenskies-honor-salvage-crew-mxp.png` | Clans Resources: Kerenskies, Merits, Honor, salvage, crew and MXP. |
| 4 | `04-clans-mechs-repair-duplicate-rename.png` | Clans Mechs with damaged mechs ticked and the Repair, Duplicate and Rename footer. |
| 5 | `05-pick-saves-by-name-not-guid.png` | The Mercenaries start page: saves listed by in-game name, date and money. |
| 6 | `06-two-themes-mercs-amber-clans-ice.png` | One page split down the middle: Mercs amber on the left, Clans ice on the right. |

The square icon (`assets/logo.png`, 512×512) and the header (`assets/header.png`, 1920×1080) are drawn
by hand, not generated: the icon is the page's primary image, and the header goes in its media
settings.

A separate, explicit render of every screen and dialog (`--filter Category=Audit`, written to
`MW5_AUDIT_DIR`) is for checking the UI against its rules, not for publishing.
