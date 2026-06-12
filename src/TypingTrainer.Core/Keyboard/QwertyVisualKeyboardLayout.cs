namespace TypingTrainer.Core.Keyboard;

public static class QwertyVisualKeyboardLayout
{
    public static VisualKeyboardLayout Create()
    {
        return new VisualKeyboardLayout(
            "QWERTY",
            [
                new VisualKeyboardRow(
                [
                    Character("Backquote", "`", "~", "`", FingerAssignment.LeftPinky),
                    Character("Digit1", "1", "!", "1", FingerAssignment.LeftPinky),
                    Character("Digit2", "2", "@", "2", FingerAssignment.LeftRing),
                    Character("Digit3", "3", "#", "3", FingerAssignment.LeftMiddle),
                    Character("Digit4", "4", "$", "4", FingerAssignment.LeftIndex),
                    Character("Digit5", "5", "%", "5", FingerAssignment.LeftIndex),
                    Character("Digit6", "6", "^", "6", FingerAssignment.RightIndex),
                    Character("Digit7", "7", "&", "7", FingerAssignment.RightIndex),
                    Character("Digit8", "8", "*", "8", FingerAssignment.RightMiddle),
                    Character("Digit9", "9", "(", "9", FingerAssignment.RightRing),
                    Character("Digit0", "0", ")", "0", FingerAssignment.RightPinky),
                    Character("Minus", "-", "_", "-", FingerAssignment.RightPinky),
                    Character("Equal", "=", "+", "=", FingerAssignment.RightPinky),
                    Special("Backspace", "Backspace", KeyRole.Backspace, FingerAssignment.RightPinky, 2.0)
                ]),
                new VisualKeyboardRow(
                [
                    Special("Tab", "Tab", KeyRole.Tab, FingerAssignment.LeftPinky, 1.5),
                    Character("KeyQ", "Q", null, "q", FingerAssignment.LeftPinky),
                    Character("KeyW", "W", null, "w", FingerAssignment.LeftRing),
                    Character("KeyE", "E", null, "e", FingerAssignment.LeftMiddle),
                    Character("KeyR", "R", null, "r", FingerAssignment.LeftIndex),
                    Character("KeyT", "T", null, "t", FingerAssignment.LeftIndex),
                    Character("KeyY", "Y", null, "y", FingerAssignment.RightIndex),
                    Character("KeyU", "U", null, "u", FingerAssignment.RightIndex),
                    Character("KeyI", "I", null, "i", FingerAssignment.RightMiddle),
                    Character("KeyO", "O", null, "o", FingerAssignment.RightRing),
                    Character("KeyP", "P", null, "p", FingerAssignment.RightPinky),
                    Character("BracketLeft", "[", "{", "[", FingerAssignment.RightPinky),
                    Character("BracketRight", "]", "}", "]", FingerAssignment.RightPinky),
                    Character("Backslash", "\\", "|", "\\", FingerAssignment.RightPinky, 1.5)
                ]),
                new VisualKeyboardRow(
                [
                    Special("CapsLock", "Caps Lock", KeyRole.CapsLock, FingerAssignment.LeftPinky, 1.75),
                    Character("KeyA", "A", null, "a", FingerAssignment.LeftPinky),
                    Character("KeyS", "S", null, "s", FingerAssignment.LeftRing),
                    Character("KeyD", "D", null, "d", FingerAssignment.LeftMiddle),
                    Character("KeyF", "F", null, "f", FingerAssignment.LeftIndex),
                    Character("KeyG", "G", null, "g", FingerAssignment.LeftIndex),
                    Character("KeyH", "H", null, "h", FingerAssignment.RightIndex),
                    Character("KeyJ", "J", null, "j", FingerAssignment.RightIndex),
                    Character("KeyK", "K", null, "k", FingerAssignment.RightMiddle),
                    Character("KeyL", "L", null, "l", FingerAssignment.RightRing),
                    Character("Semicolon", ";", ":", ";", FingerAssignment.RightPinky),
                    Character("Quote", "'", "\"", "'", FingerAssignment.RightPinky),
                    Special("Enter", "Enter", KeyRole.Enter, FingerAssignment.RightPinky, 2.25)
                ]),
                new VisualKeyboardRow(
                [
                    Special("LeftShift", "Shift", KeyRole.Modifier, FingerAssignment.LeftPinky, 2.25),
                    Character("KeyZ", "Z", null, "z", FingerAssignment.LeftPinky),
                    Character("KeyX", "X", null, "x", FingerAssignment.LeftRing),
                    Character("KeyC", "C", null, "c", FingerAssignment.LeftMiddle),
                    Character("KeyV", "V", null, "v", FingerAssignment.LeftIndex),
                    Character("KeyB", "B", null, "b", FingerAssignment.LeftIndex),
                    Character("KeyN", "N", null, "n", FingerAssignment.RightIndex),
                    Character("KeyM", "M", null, "m", FingerAssignment.RightIndex),
                    Character("Comma", ",", "<", ",", FingerAssignment.RightMiddle),
                    Character("Period", ".", ">", ".", FingerAssignment.RightRing),
                    Character("Slash", "/", "?", "/", FingerAssignment.RightPinky),
                    Special("RightShift", "Shift", KeyRole.Modifier, FingerAssignment.RightPinky, 2.75)
                ]),
                new VisualKeyboardRow(
                [
                    Special("LeftCtrl", "Ctrl", KeyRole.Control, FingerAssignment.LeftPinky, 1.5),
                    Special("LeftAlt", "Alt", KeyRole.Alt, FingerAssignment.LeftThumb, 1.5),
                    new VisualKeyboardKey("Space", "Space", null, " ", KeyRole.Space, FingerAssignment.LeftThumb, 9.0),
                    Special("RightAlt", "Alt", KeyRole.Alt, FingerAssignment.RightThumb, 1.5),
                    Special("RightCtrl", "Ctrl", KeyRole.Control, FingerAssignment.RightPinky, 1.5)
                ])
            ]);
    }

    private static VisualKeyboardKey Character(
        string id,
        string primaryLabel,
        string? secondaryLabel,
        string output,
        FingerAssignment finger,
        double widthUnits = 1.0)
    {
        return new VisualKeyboardKey(
            id,
            primaryLabel,
            secondaryLabel,
            output,
            KeyRole.Character,
            finger,
            widthUnits);
    }

    private static VisualKeyboardKey Special(
        string id,
        string label,
        KeyRole role,
        FingerAssignment finger,
        double widthUnits)
    {
        return new VisualKeyboardKey(
            id,
            label,
            null,
            null,
            role,
            finger,
            widthUnits);
    }
}
