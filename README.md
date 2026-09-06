<img src="packaging/icon/icon-128.png" alt="" width="96" align="left" />

# Camera Control

Drive Blackmagic ATEM switchers and PTZ cameras from a game controller.

One operator steers whichever camera is on preview, changes the preview selection, cuts, toggles
keyers and runs macros, without taking their eyes off the stage.

<br clear="left" />

![The main window](docs/images/window.png)

This is a .NET port of [Cgf.CameraControl.Main](https://github.com/sensslen/Cgf.CameraControl.Main),
which is TypeScript and headless. It reads the same configuration files.

## What it does

- **Steers the preview camera.** The left stick pans and tilts, the right stick zooms and focuses.
  Change preview and the previous camera is stopped rather than left drifting.
- **Runs the switcher.** Cut, auto, keyer toggles, macros, and a macro toggle that picks one of two
  macros from the live state of a keyer or an auxiliary output.
- **Lights the tally.** Cameras that carry a tally light follow the preview and program buses.
- **Shows what is happening.** Every interface with the mixer and the cameras it drives underneath
  it, and a log you can filter by component.
- **Runs without a controller.** Clicking an interface opens a control window: two mouse pads for the
  two sticks, and the keyboard for everything else.
- **Rumbles** on a transition, when one of your cameras goes on air, and when a connection drops.

## Install

| Platform | |
| --- | --- |
| Windows | `winget install Sensslen.CameraControl`, or the installer from [Releases](../../releases) |
| macOS | the `.dmg` from [Releases](../../releases), then drag to Applications |
| Linux | `flatpak install ./CameraControl-<version>-linux-x64.flatpak` |

Every binary is NativeAOT compiled, so there is no .NET runtime to install.

Releases are signed where the project has certificates for it. Without them a Windows installer shows
a SmartScreen warning, and a macOS build is ad-hoc signed rather than notarized, so macOS asks before
opening it: **System Settings → Privacy & Security → Open Anyway**, or clear the download flag once
with `xattr -dr com.apple.quarantine "/Applications/Camera Control.app"`. Every release that went out
that way says so in its notes.

## Configure

Point the application at a `config.json` with **File → Import**, and it reopens the same file next
time. There is a working example in [samples/offline.json](samples/offline.json) that needs no
hardware: it uses a passthrough mixer, so a controller and the interface bindings can be checked on
a laptop.

```json
{
  "cams": [
    { "type": "Websocket.PtzLanc", "instance": 1, "ip": "10.0.0.11", "showTallyLight": false }
  ],
  "videoMixers": [
    { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.240", "mixEffectBlock": 0 }
  ],
  "interfaces": [
    {
      "type": "gamepad",
      "instance": 1,
      "videoMixer": 1,
      "connectionChange": { "type": "direct", "default": { "up": 1, "right": 2, "down": 3, "left": 4 } },
      "specialFunction": { "default": { "down": { "type": "key", "index": 1 } } },
      "cameraMap": { "1": 1 }
    }
  ]
}
```

A problem with one entry is reported against that entry and the rest still load, so a typo in one
camera does not take the desk down with it.

### Cameras

| `type` | Talks to | Needs |
| --- | --- | --- |
| `Websocket.PtzLanc` | the websocket LANC controller firmware | `ip` |
| `Signalr.PtzLanc` | the SignalR LANC controller service | `connectionUrl`, `connectionPort` |
| `viscaoverip` | a VISCA camera over UDP | `ip`, and `port` if it is not 52381 |

All three take `panTiltInvert`. A VISCA camera also takes `tallyMode`, naming the vendor whose tally
payload it understands: `avonic`, `ptzoptics`, or `none`, which is the default. `avonic` carries a
red and a green lamp, and CineTreak cameras answer to it too; `ptzoptics` has one lamp, so preview
leaves it dark.
Nothing in the VISCA specification covers a tally lamp, so a camera sent the wrong vendor's payload
does something unrelated rather than nothing.

[Camera compatibility](docs/CameraCompatibility.md) lists the cameras this has actually been run
against and what a VISCA camera has to accept to work at all.

### Controller bindings

| Control | Does |
| --- | --- |
| Left stick | pan and tilt |
| Right stick | zoom and focus |
| D-pad | change the preview selection |
| A / B / X / Y | special function bound to down / right / left / up |
| Right shoulder | cut |
| Right trigger | auto |
| Left shoulder | hold for the `alt` bindings |
| Left trigger | hold for the `altLower` bindings |

SDL normalises every controller to this one layout, so `gamepad` is the only interface type that
matters. The old `logitech/F310`, `logitech/F710` and `logitech/Rumblepad2` strings still load, but
they are labels now rather than behaviour.

### The control window

Clicking an interface opens it. The two pads are the two sticks: drag inside one and let go to stop.
The keyboard drives the same interface, whether or not a controller is bound to it.

| Key | Does |
| --- | --- |
| `W` `A` `S` `D` | pan and tilt |
| `I` `K` / `J` `L` | zoom / focus |
| Arrow keys | change the preview selection |
| `1` `2` `3` `4` | the special functions bound to down, right, left and up |
| `Enter` / `Space` | cut / auto |
| `Shift` / `Ctrl` | hold for the `alt` and `altLower` bindings |

An interface of type `keyboard` has no controller behind it at all and takes the same configuration
as a `gamepad`, minus `serialNumber` and `deadzone`. One with a controller behind it accepts both at
once: the pad and the window are two sets of hands on one interface rather than two interfaces
fighting over the same cameras.

Two controllers on one machine are told apart by `serialNumber`. That is an absolute filter, never a
preference: an interface whose serial is not present stays unbound and says so, because putting an
operator on somebody else's mixer is worse than binding nothing.

### Languages

English plus ten more, picked from the operating system on first run and changeable under
**Language**. English is the source text; every translation is machine made and none has been
reviewed by a native speaker.

## Build

Needs the .NET 10 SDK. Nothing else.

```bash
dotnet build
dotnet format --verify-no-changes
```

The test projects are Microsoft.Testing.Platform executables, so run them directly rather than
through `dotnet test`:

```bash
for suite in tests/*/bin/Debug/net10.0/*.Tests; do "$suite"; done
```

### Layout

| Project | |
| --- | --- |
| `Cgf.CameraControl.Core` | the domain: factories, configuration, the mixer and camera contracts |
| `Cgf.CameraControl.Atem` | ATEM support over AtemSharp |
| `Cgf.CameraControl.Cameras.WebsocketPtzLanc` | the websocket PTZ LANC camera |
| `Cgf.CameraControl.Cameras.SignalrPtzLanc` | the SignalR PTZ LANC camera |
| `Cgf.CameraControl.Cameras.ViscaOverIp` | the VISCA over IP camera |
| `Cgf.CameraControl.Input.Sdl` | the controller logic and the SDL device layer |
| `Cgf.CameraControl.Licenses.Generator` | compiles the licence report into the app |
| `Cgf.CameraControl.App` | the Avalonia window and the composition root |

Only the app references Avalonia and only `Input.Sdl` references SDL, so everything else is testable
without a display or a controller.

### The `--aot-probe` switch

Two things in this application fail under trimming *without failing the build*: AtemSharp finds its
command types by reflecting over its own assembly, and the translations are embedded resources
because NativeAOT does not load satellite assemblies. Either one can leave a binary that starts,
connects, and quietly does the wrong thing.

`CgfCameraControl --aot-probe` decodes a real ATEM command, serializes a SignalR camera update
through the real hub protocol, and resolves every language, then exits non-zero if any of them has
stopped working. The release workflow runs it against every published binary before anything is
uploaded.

### Third-party licences

**Help → Third-party licences** lists every package the application ships. Opening a row shows what
it is under and the full text of that licence: what the package published for itself where it
published anything, and the text of the SPDX identifier it declared otherwise. Report and texts are
committed and compiled into the binary by `Cgf.CameraControl.Licenses.Generator`, so showing them
costs nothing at run time and they cannot drift from what is actually shipped. A licence with no text
fails the build rather than leaving an empty page. Regenerate all of it after changing a package
reference:

```bash
dotnet tool restore
python packaging/licenses/fetch-texts.py
```

That script also refuses a licence the project has not accepted, and ignores what builds the binary
rather than travelling in it: the diagnostics support package ships in Debug alone, and the NativeAOT
compiler and the trimmer carry the host's runtime identifier in their package id, so a report naming
them would differ between a developer machine and CI. CI runs the same script and fails when what is
committed is out of date.

### The icon

Generated, not drawn by hand, so the SVG and every raster come from the same numbers:

```bash
python packaging/icon/generate.py
```

## Licence

MIT. See [LICENSE](LICENSE).
