using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using SDL3;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;

/// The F310's D-mode report: four byte axes, a four-bit hat, twelve buttons and
/// two vendor bytes. HidSharp prefixes unnumbered reports with a zero report ID.
/// Layout matches sensslen/node-gamepad's controllers/logitech/gamepadf310.json.
public sealed class F310ReportMapper(GamepadInputMapper mapper)
{
    private static readonly SDL.GamepadButton[] Buttons =
    [
        SDL.GamepadButton.West, SDL.GamepadButton.South,
        SDL.GamepadButton.East, SDL.GamepadButton.North,
        SDL.GamepadButton.LeftShoulder, SDL.GamepadButton.RightShoulder,
    ];

    private int _buttons;
    private int _hat = 8;

    public void Apply(ReadOnlySpan<byte> report)
    {
        if (report.Length != 9 || report[0] != 0)
        {
            throw new InvalidDataException("Expected a nine-byte unnumbered Logitech F310 D-mode report.");
        }

        mapper.Axis(SDL.GamepadAxis.LeftX, Axis(report[1]));
        mapper.Axis(SDL.GamepadAxis.LeftY, Axis(report[2]));
        mapper.Axis(SDL.GamepadAxis.RightX, Axis(report[3]));
        mapper.Axis(SDL.GamepadAxis.RightY, Axis(report[4]));

        var buttons = (report[5] >> 4) | (report[6] << 4);
        for (var bit = 0; bit < Buttons.Length; bit++)
        {
            if (((buttons ^ _buttons) & (1 << bit)) != 0)
            {
                mapper.Button(Buttons[bit], (buttons & (1 << bit)) != 0);
            }
        }

        if (((buttons ^ _buttons) & 0x40) != 0)
        {
            mapper.Axis(SDL.GamepadAxis.LeftTrigger, (buttons & 0x40) != 0 ? short.MaxValue : (short)0);
        }
        if (((buttons ^ _buttons) & 0x80) != 0)
        {
            mapper.Axis(SDL.GamepadAxis.RightTrigger, (buttons & 0x80) != 0 ? short.MaxValue : (short)0);
        }

        var hat = report[5] & 15;
        if (hat != _hat)
        {
            Hat(SDL.GamepadButton.DPadLeft, hat is 5 or 6 or 7, _hat is 5 or 6 or 7);
            Hat(SDL.GamepadButton.DPadUp, hat is 0 or 1 or 7, _hat is 0 or 1 or 7);
            Hat(SDL.GamepadButton.DPadRight, hat is 1 or 2 or 3, _hat is 1 or 2 or 3);
            Hat(SDL.GamepadButton.DPadDown, hat is 3 or 4 or 5, _hat is 3 or 4 or 5);
        }
        _buttons = buttons;
        _hat = hat;
    }

    public void Reset()
    {
        _buttons = 0;
        _hat = 8;
        mapper.Reset();
    }

    private void Hat(SDL.GamepadButton button, bool pressed, bool previous)
    {
        if (pressed != previous)
        {
            mapper.Button(button, pressed);
        }
    }

    private static short Axis(byte value) => value switch
    {
        < 127 => (short)((value - 127) * 32767 / 127),
        > 128 => (short)((value - 128) * 32767 / 127),
        _ => 0,
    };
}
