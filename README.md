# Nova the Squirrel Crowd Control Pack

This repository contains a Crowd Control effect pack for
[Nova the Squirrel](https://github.com/NovaSquirrel/NovaTheSquirrel), built as a
BizHawk connector demonstration for the NES.

## Contents

- `NovaTheSquirrel.cs` - the Crowd Control pack source.

The pack provides health and timed ability effects, game-state-aware retries,
effect conflict handling, and clean timed-effect cancellation.

## Getting the Game

Download `nova.nes` from the official
[Nova the Squirrel v1.0.6a release](https://github.com/NovaSquirrel/NovaTheSquirrel/releases/tag/v1.0.6a).
The ROM is also available as a
[direct download](https://github.com/NovaSquirrel/NovaTheSquirrel/releases/download/v1.0.6a/nova.nes).

## Development

Use the public [Crowd Control SDK](https://github.com/WarpWorld/CrowdControl.SDK/)
to develop, build, and test this pack. Follow the current setup and usage
instructions provided by the SDK repository.

The pack recognizes the official release ROM by its MD5 checksum. Use a
supported BizHawk installation with the Crowd Control external tool to run the
demo.

## Licensing

The Crowd Control pack source (`NovaTheSquirrel.cs`) is available under the
[MIT License](LICENSE).

Nova the Squirrel is not included in this repository and is not covered by the
MIT License. The game has its own licensing terms: its source code is
GPL-3.0-or-later, while its assets are licensed separately and are subject to
additional restrictions. See the
[Nova the Squirrel repository](https://github.com/NovaSquirrel/NovaTheSquirrel)
and its [license file](https://github.com/NovaSquirrel/NovaTheSquirrel/blob/master/LICENSE.txt)
for the authoritative terms.
