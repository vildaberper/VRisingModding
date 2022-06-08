# PP Rising

**Client and server mod.**

> This means you **have** to install it both on the client and the server for it to work. I recommend installing [ServerLaunchFix](https://v-rising.thunderstore.io/package/Mythic/ServerLaunchFix/) if you're launching in-game.

PP Rising is very much in development, but the end goal is to have a collection of QoL-features that makes sense to me.

## Quick Stack

Compulsively count to nearby containers. Prioritizes containers based on fill ratio (higher first).

### Config

- `Enable` [default `true`]: Enable quick stacking.
- `Distance` [default `20`]: Maximum distance to container.
- `NameIgnore` [default `nostack`]: Ignore containers with name including this.
- `SortOnStack` [default `true`]: Sort the container after stacking to it.
- `Cooldown` [default `1`]: Quick stack cooldown in seconds.
- Control `Quick Stack` [default `Insert`]: Hotkey for quick stacking.

# Changelog

- `0.0.3` _2022-06-08_

  - Minor code cleanup.
  - Updated docs.
  - Updated csproj (plugin version should now be correct in logs and config).
  - Update items that are stacked (compare inventory before & after). Should work with all items with debuffs.
  - Add quick stack cooldown.

- `0.0.2` _2022-06-06_

  - Minor code cleanup.
  - Updated config descriptions to reflect what is server-side.
  - Updated docs & manifest.

- `0.0.1` _2022-06-05_

  - Initial release.
