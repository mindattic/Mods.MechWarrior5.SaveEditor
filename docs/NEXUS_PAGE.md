# NexusMods pages: MW5 Save Editor

One program, two pages with the same text and the same file:
- MechWarrior 5: Mercenaries: https://www.nexusmods.com/mechwarrior5mercenaries/mods/1666
- MechWarrior 5: Clans: https://www.nexusmods.com/mechwarrior5clans/mods/70

## What to paste (identical on both pages)

- **Brief overview / summary:** [nexus/summary.txt](nexus/summary.txt)
- **Description:** [nexus/description.bbcode](nexus/description.bbcode), with the description editor's **Import
  description** (BBCode). Text only: no screenshots or other images (removed for the quarantine review, M5SE-T124).

NexusMods' API can't edit descriptions, so paste both by hand after a release changes them.

## Page fields

- **Name:** MW5 Save Editor - Mercenaries and Clans
- **Category:** Utilities. **Tags:** Utilities, Save Games, Mechs, Cheats and God items, User Interface
- **Images:** the square icon (`assets/logo.png`, 512×512) is the primary image; the header (`assets/header.png`,
  1920×1080) goes in the media settings. No screenshot gallery.
- **AI disclosure:** the code was written with an AI model (Claude); tick "contains AI-generated content" and say so.
- **Version:** whole numbers only; each release bumps the major (see AGENTS.md).

## Releases

A published GitHub release makes `.github/workflows/publish.yml` upload the release's own zip to both pages'
file slots (`nexus.json`: `modUid` and `fileId` per page), so the file on NexusMods is byte for byte the GitHub one.
