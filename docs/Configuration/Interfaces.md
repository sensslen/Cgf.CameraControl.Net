# Interfaces

Each `interfaces` entry defines an operator's mixer, camera map and controls.
`gamepad` and `keyboard` have separate schemas: `keys` on a gamepad or `deadzone` on a keyboard
is an error. To share a mixer, configure separate interfaces with the same `videoMixer`.

## What both kinds carry

| Key | Default | Means |
| --- | --- | --- |
| `videoMixer` | required | the `instance` of the mixer this drives |
| `cameraMap` | required | mixer input to camera `instance` |
| `connectionChange` | required | how a direction moves the preview selection |
| `functions` | `{}` | named things a binding can run |
| `enableChangingProgram` | `true` | whether this interface may put anything to air |

`enableChangingProgram: false` disables cut, auto and the keyboard program row without disabling
camera framing.

## `gamepad`

```json
{
  "type": "gamepad",
  "instance": 1,
  "videoMixer": 1,
  "serialNumber": "83234F94",
  "deadzone": 0.05,
  "rumble": true,
  "cameraMap": { "1": 1, "2": 2 },
  "connectionChange": { "type": "direct", "default": { "up": 1, "right": 2 } },
  "functions": { "wide": { "type": "key", "index": 1 } },
  "pad": { "default": { "down": "wide" } }
}
```

| Key | Default | Means |
| --- | --- | --- |
| `backend` | `sdl` | `sdl` for SDL's controller mappings; `hid` for direct, serial-addressed F310 input |
| `serialNumber` | absent | claim only the pad reporting this serial; absent takes the first free pad |
| `deadzone` | `0.05` | stick travel ignored around centre, so the cameras do not drift |
| `rumble` | `true` | whether the pad is allowed to buzz |
| `pad` | `{}` | which named function each face button runs |

Accepted type names are `gamepad`, `logitech/F310`, `logitech/F710` and `logitech/Rumblepad2`.
All use the same SDL layout. HID accepts only `gamepad` and `logitech/F310`.
A / B / X / Y run the `pad` functions bound to down / right / left / up.
`serialNumber` is an exact filter. An absent serial leaves the interface unbound, never assigned
to another operator's controller.

### Choosing a gamepad backend

| Backend | Support |
| --- | --- |
| `sdl` | default; SDL controller mappings |
| `hid` | HidSharp; Logitech F310 **D mode only**; non-empty `serialNumber` required; no rumble |

HID opens the device by serial, independent of enumeration order, USB port or restart.
Duplicate serial claims are rejected. Missing devices stay disconnected and retry until present.
Unsupported report layouts are logged, not decoded.

The F310 byte layout follows
[`sensslen/node-gamepad`'s F310 definition](https://github.com/sensslen/node-gamepad/blob/master/src/controllers/logitech/gamepadf310.json).
Axis values 127 and 128 are neutral.

On macOS, SDL IOKit omits serial numbers and device paths even when the OS exposes them.
Use HID for serial-addressed F310s. The application disables SDL's Apple GameController backend
so IOKit can detect generic D-mode devices. Otherwise, SDL suppresses IOKit devices for which
Apple's `supportsHIDDevice` returns true. On the tested Mac, it returns true for both F310s while
`GCController.controllers` is empty, leaving neither SDL backend exposing them.

Change the F310 X/D switch **before plugging it in**. Replug after changing it while connected.

HidSharp supports Windows, macOS and Linux without separate HIDAPI installation.
Linux may need a udev rule granting access to the pad's `hidraw` node.
HID hardware verification covers macOS F310s only. Never configure one pad through both backends.

The left stick pans and tilts, the right stick zooms and focuses, the direction pad moves the
preview selection through `connectionChange`, the right shoulder cuts and the right trigger autos.
The left shoulder holds `alt` and the left trigger holds `altLower`.

A pad has four face buttons and no more. The way past four is the modifier sets:

```json
"pad": {
  "default":  { "up": "wide", "down": "tight" },
  "alt":      { "up": "sweep" },
  "altLower": { "up": "reset" }
}
```

A modifier overrides only its bound buttons; others use `default`.

The gamepad drawing is read-only. Symbols match the physical pad: face symbols, d-pad arrows and
L1/L2/R1/R2. Its legend follows the active modifier. Preview/program directions use their bus
colors, triggers fill with travel, and unbound controls are faded.

![A gamepad interface, with R1, R2, triangle and right held and L2 half pulled](../images/interface-gamepad.png)

## `keyboard`

Keep the window active and select the keyboard interface. Configured keys work even when a list,
button or scroll area has focus; unbound keys retain normal navigation.
Switching interfaces, entering edit mode or leaving the window releases held keys and stops
movement. These are not global shortcuts and need no macOS Input Monitoring permission.

![A keyboard interface: the input rows, cut and auto, the functions, the pan and tilt pad and the zoom and focus sliders](../images/interface-keyboard.png)

The panel has program/preview input rows, cut/auto between them and configured functions to the
right. Buttons show their bound keys and light while held. Mouse controls provide pan/tilt and
zoom/focus sliders; releasing a drag recenters the control and stops movement.

```json
{
  "type": "keyboard",
  "instance": 2,
  "videoMixer": 1,
  "cameraMap": { "1": 1, "2": 2, "3": 3 },
  "connectionChange": { "type": "direct", "default": { "up": 1, "right": 2 } },
  "functions": {
    "wide":  { "type": "key", "index": 1 },
    "sweep": { "type": "macroLoop", "indexes": [1, 2] }
  },
  "keys": {
    "pan":   { "left": "A", "right": "D" },
    "tilt":  { "up": "W", "down": "S" },
    "zoom":  { "in": "I", "out": "K" },
    "focus": { "far": "L", "near": "J" },
    "connectionChange": { "up": "Up", "down": "Down", "left": "Left", "right": "Right" },
    "input":    { "D1": 1, "D2": 2, "D3": 3 },
    "function": { "F1": "wide", "F2": "sweep" },
    "cut":  "Enter",
    "auto": "Space"
  }
}
```

| Key in `keys` | Means |
| --- | --- |
| `pan`, `tilt`, `zoom`, `focus` | a key per direction; held is full speed, released is stopped |
| `connectionChange` | keys that step the selection, as the direction pad does |
| `input` | a key that selects a mixer input outright, which is the room a pad has not got |
| `function` | a key to the name of a function in `functions` |
| `cut`, `auto` | the transition keys |

All keys are optional; only bound actions appear as buttons. One bound axis end allows movement
in that direction; mouse controls work even with no axis keys. `input` selects inputs directly,
beyond the gamepad's four directions. Keyboard bindings have no modifier sets.

## Functions

Functions are bound by name. Names label keyboard buttons and gamepad controls.

```json
"functions": {
  "wide":  { "type": "key", "index": 1 },
  "sweep": { "type": "macroLoop", "indexes": [1, 2, 3] },
  "iso":   { "type": "connectionChange", "index": 4 },
  "lower": {
    "type": "macroToggle",
    "indexOn": 23,
    "indexOff": 24,
    "condition": { "type": "key", "key": 0 }
  }
}
```

| `type` | Does |
| --- | --- |
| `key` | toggle the switcher's upstream key `index` |
| `connectionChange` | select input `index` |
| `macroLoop` | run the next macro in `indexes` each press, wrapping round |
| `macroToggle` | run `indexOn` or `indexOff` depending on `condition` |

A `macroToggle` condition is either `{ "type": "key", "key": n }`, which asks whether upstream key
`n` is set, or `{ "type": "aux_selection", "aux": n, "selection": m }`, which asks whether auxiliary
`n` is showing input `m`.

An undefined function binding fails at load with the available names.

## `connectionChange`

Two preview-selection schemes:

```json
"connectionChange": {
  "type": "direct",
  "default":  { "up": 1, "right": 2, "down": 3, "left": 4 },
  "alt":      { "up": 5, "right": 7 },
  "altLower": { "up": 9 }
}
```

`direct` binds each direction to a fixed input, with a set per modifier. A modifier set that is
absent falls back to `default`.

```json
"connectionChange": {
  "type": "directional",
  "directions": {
    "1": { "right": 2 },
    "2": { "left": 1, "right": 3 },
    "3": { "left": 2 }
  }
}
```

`directional` steps to a neighbor. A selection with no entry falls back to the lowest numbered one.

## Key names

`keys` uses Avalonia's own key names, matched without regard to case. A name that is not one of them
is reported against the entry that carries it.

| Kind | Names |
| --- | --- |
| Letters | `A` to `Z` |
| Number row | `D0` to `D9` |
| Numeric keypad | `NumPad0` to `NumPad9`, `Add`, `Subtract`, `Multiply`, `Divide`, `Decimal` |
| Function keys | `F1` to `F24` |
| Arrows | `Up`, `Down`, `Left`, `Right` |
| Editing | `Enter`, `Space`, `Tab`, `Back`, `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, `Escape` |
| Modifiers as keys | `LeftShift`, `RightShift`, `LeftCtrl`, `RightCtrl`, `LeftAlt`, `RightAlt` |
| Punctuation | `OemComma`, `OemPeriod`, `OemMinus`, `OemPlus`, `OemQuestion`, `OemSemicolon`, `OemQuotes`, `OemOpenBrackets`, `OemCloseBrackets`, `OemPipe`, `OemTilde` |

`Enter` and `Return` are the same key; the panel writes it `Return`.
