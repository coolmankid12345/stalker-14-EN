<!-- If you have any questions, please contact our discord https://discord.gg/SnUSV76zR3 -->

## What I changed

Fixed stash and band portal crashes caused by the `TryLoadMap` API migration in the upstream merge. Added proper error handling when arena maps fail to load and fixed band portals permanently blocking re-entry instead of using a cooldown.

Added English translations for all 57 Stalker-specific access levels. Previously, examining access-restricted portals displayed raw locale keys (e.g., `id-card-access-level-freedom`) instead of proper names. Also fixed the Guide access level which had hardcoded Russian text instead of a locale key.

## Changelog

author: @teecoding

- fix: Stash portals no longer crash the server when the arena map fails to load
- fix: Band portals now use a proper cooldown instead of permanently blocking re-entry
- fix: Access level names on portals and ID cards now display in English instead of raw locale keys
- fix: Guide access level no longer shows hardcoded Russian text

<!-- Put X — [X]: -->
## Make sure you check and agree to the following
- [X] Yes, I ran my code and tested that the changes worked
- [X] Yes, I checked that there were no errors in the console output of the client and server after my changes
- [X] I agree that by submitting a PR I agree to the terms of the [license](https://github.com/stalker14-project/stalker-14/edit/master/LICENSE.TXT).
- [X] I have checked and confirm that all images and audio files that I have added to the PR belong to me or are under an open license
