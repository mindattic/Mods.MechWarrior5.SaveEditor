# Third-party notices

## JJ's MW5 Merc Save Editor — inspiration

Many thanks to JJ, whose [MW5 Merc Save Editor](https://www.nexusmods.com/mechwarrior5mercenaries/mods/1466)
inspired this editor's scope: stock loadouts, repairs, and bulk inventory and pilot editing. **No code from
that project is used.** This editor's save engine, game logic and interface were written independently.

The game reference data in `src/Mech5.Mercs.SaveEditor.Core/Data/stock_loadouts.json.gz`, `items.json` and the
variant, chassis, tonnage and hero-name columns of `mechs.json` were derived from the data catalogs
shipped with JJ's editor (v1.21.0), so its license notice is reproduced here as that license requires:

```
MIT License

Copyright (c) 2026 JJ (jonayetjubaer-cmyk)
Copyright (c) 2026 Jonaex LTD

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Sarna.net — 'Mech images

'Mech pictures are fetched at runtime from [Sarna.net](https://www.sarna.net) (BattleTech wiki) through its
public MediaWiki API, cached on the local machine, and never bundled with or redistributed by this
editor. Image rights belong to their respective owners; each picture links back to its Sarna page.

## Rajdhani font

The bundled Rajdhani typeface (`shared/src/Mech5.Shared.Wpf/Assets/Fonts`, from the Mods.Mech5.Shared submodule) is © Indian Type Foundry,
licensed under the SIL Open Font License 1.1 (see `OFL.txt` in that folder).

## MechWarrior / BattleTech

MechWarrior, BattleTech and 'Mech are trademarks of their respective owners. This is an unofficial
fan-made tool and is not affiliated with or endorsed by Piranha Games, Microsoft or Catalyst Game Labs.
