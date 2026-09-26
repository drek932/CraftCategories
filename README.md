# CraftCategories

**Automatically organize crafting mods in The Long Dark.**

CraftCategories automatically detects mods that add custom crafting recipes and creates a dedicated category for each of them.

No integration required from other mod authors.

https://github.com/user-attachments/assets/9996b235-9b8e-43dc-a88d-9b75e8a36824

## Features

- **Automatic mod detection** — no compatibility list or manual setup.
- **Dedicated categories** — every crafting mod gets its own category.
- **Automatic icons** — category icons are taken from the corresponding mod.
- **Fully scalable** — supports any number of crafting mods with a scrollable category list.
- **Per-mod recipe visibility** — choose where a mod's recipes appear:
  - Mod category only
  - Game categories only
  - Both
- **Individual recipe control** — hide specific recipes without disabling the entire mod.
- **Ingredient-aware display** — optionally show recipes in game categories when their required ingredients are available.
- **Category positioning** — customize the order of mod categories.
- **ModSettings integration** — extensive configuration without editing files.

## For Mod Authors

Nothing is required.

CraftCategories works independently of the crafting mods it organizes.

You don't need to:

- add CraftCategories as a dependency;
- modify your recipes;
- add compatibility code;
- create a configuration file;
- maintain a compatibility patch.

Just create your crafting mod. CraftCategories handles the organization automatically.

## Installation

### Requirements

- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [ModSettings](https://github.com/DigitalzombieTLD/ModSettings)

### Install

1. Download the latest [release](https://github.com/drek932/CraftCategories/releases).
2. Extract the contents into your `Mods` folder.
3. Launch the game.

CraftCategories will automatically detect compatible crafting mods.

## Configuration

All settings are available through ModSettings.

You can configure individual mods and recipes without modifying the original crafting mods.

## Language

- English
- Russian

The interface follows the game's language settings.

## Compatibility

Tested with:

- The Long Dark 2.54
- The Long Dark 2.55
- MelonLoader 0.7.2

CraftCategories does not rely on a manually maintained list of supported crafting mods.

## License

[MIT](LICENSE)
