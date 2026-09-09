# Colonial Marines Universe

**Colonial Marines Universe (CMU)** is a multiplayer sandbox game set in the *Alien* universe.
It is a fork of RMC14, which is based on Colonial Marines 13 (CM13), and is built in C# on the RobustToolbox engine.

This repository contains the game's code, maps, and resources. CMU builds on the work of its upstream projects
and contributors, with asset credits and licensing recorded in the corresponding metadata files.

## Community

Join the [CMU Discord](https://discord.gg/colonialmarines) to get involved, discuss the game, and coordinate contributions.
For bugs, feature requests, and tasks needing help, visit this repository's **Issues** tab.

## Contributing

Contributions are welcome across code, maps, art, sound, documentation, and testing.

1. Check existing issues before reporting a bug or starting work. Discuss larger changes in an issue or on Discord.
2. Read the [contribution guidelines](CONTRIBUTING.md) for code conventions and pull request guidance.
3. Keep your changes focused and test the behavior they affect.
4. Open a pull request using the [pull request template](.github/PULL_REQUEST_TEMPLATE.md).
   Explain what changed, why, and how you tested it. Include screenshots or video for visible changes where useful.

When adding or modifying assets, preserve their attribution and license metadata. See [License](#license) below.

## Local development

### Requirements

- Git, including submodule support.
- The .NET SDK selected by [global.json](global.json).
- Python 3 for the repository's build setup scripts.

## Credits

CMU builds on RMC14, CM13, Space Station 14, and RobustToolbox. Thanks to the developers, artists, mappers,
and other contributors whose work makes these projects possible. Individual asset credits are recorded in metadata.

## License

### Code

The **May 1, 2026** licensing cutoff was introduced in
[commit 08ae7902c0c4][license-cutoff] ("Revise license information"):

- The vast majority of code implemented **before May 1, 2026** is licensed under the
  [MIT License][historical-mit].
- All code implemented **on or after May 1, 2026** is licensed under the
  [GNU Affero General Public License v3.0 (AGPL-3.0)](LICENSE), unless noted otherwise.

The repository's `LICENSE.TXT` was subsequently changed from MIT to AGPL-3.0 on **May 13, 2026**, in
[commit f41c414ca0ec][license-file-change]. The cutoff above comes from the earlier README change.

License exceptions must be clearly declared in the affected file and its header, along with the reason for the exception.

### Assets

Most art assets are licensed under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/), unless stated otherwise.
Each asset's metadata records its license and copyright information; consult that metadata for the terms that apply.
See this [example asset metadata](Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

[license-cutoff]: https://github.com/AU-14/ColonialMarinesUniverse/commit/08ae7902c0c40c3f9d564612ec3e343de83bdb56
[historical-mit]: https://github.com/AU-14/ColonialMarinesUniverse/blob/bfec8c5d74f7637a7c4b44e365884dccf0217a28/LICENSE.TXT
[license-file-change]: https://github.com/AU-14/ColonialMarinesUniverse/commit/f41c414ca0ec7e0ec3f9f4900527206b280aad6f
