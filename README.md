# Stashie
This is a cleaned and customized version of Stashie based on [Stashie by Arecurius0](https://github.com/Arecurius0/Stashie) and [Stashie by DetectiveSquirrel](https://github.com/DetectiveSquirrel/Stashie).

## Foreword

This version has been cleaned up and customized to my liking. I will not be maintaining this or adding new features — unless I need it for my own use.

Feel free to fork this and make your own changes.

## Changes

- Removed all the unnecessary features that were broken or not needed for my use.
- Implemented ItemLibraryFilter and added JSON support for the Stashie configuration (SquirrelDetective).
- Coroutines have been replaced with SyncTasks<>.
- Implemented [InputHumanizer](https://github.com/sychotixdev/InputHumanizer) and [InputHumanizerLib](https://github.com/sychotixdev/InputHumanizerLib) for humanized input (sychotixdev).

## Installation
1. Download the source code and [InputHumanizer](https://github.com/sychotixdev/InputHumanizer), and place it in `Plugins/Source`.
2. Download [InputHumanizerLib](https://github.com/sychotixdev/InputHumanizerLib) and build the project. Place the built DLL in the root folder.
