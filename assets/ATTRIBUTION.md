# Asset attribution

Track the source/license of every non-original asset here, at the time it's added. Don't skip this even for placeholder art -- it's much easier to record accurately now than to reconstruct later once a folder has a dozen unlabeled sheets in it.

## `sprites/items/roguelikeitems.png`

By Joe Williamson ([@JoeCreates](https://twitter.com/JoeCreates)). Licensed **Creative Commons Attribution-ShareAlike (CC BY-SA)**. Attribution text is embedded directly in the bottom of the sheet image itself.

**CC BY-SA requires attribution and, if this project is ever distributed, that derivative works using this asset be shared under the same license.** If MadRealm is released publicly with this asset still in use, credit Joe Williamson in the game's credits screen and/or README, and check the ShareAlike implications for any modified version of this specific sheet before distributing it.

## `sprites/characters/characters.png`

Converted from `16x24rpgdb.gif` (originally an animated "reveal" preview GIF -- the final frame, which is the complete sheet, was extracted; see `docs/godot-csharp-gotchas.md` #5 for why the GIF itself couldn't be imported directly into Godot). **Source/author/license not yet recorded** -- this was dropped into the project directory without an accompanying license file or source link. Track down the actual source before this project is ever distributed publicly; don't ship it on the assumption it's free-use without confirming.

## `sprites/tiles/overworld.png`

Not yet integrated into any scene (see `docs/art-integration-plan.md`). **Source/author/license not yet recorded** -- same caveat as `characters.png`.

## `sprites/items/potions_overlay.png`

Not yet integrated into any scene. **Source/author/license not yet recorded** -- same caveat.

---

**Standing rule:** any new asset added to this project should get an entry here in the same commit, even if the license is initially "unknown, needs checking" -- an honest "unknown" is recoverable; a silently-missing entry usually isn't, once nobody remembers where a file came from.
