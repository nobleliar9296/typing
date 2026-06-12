namespace TypingTrainer.Core.Keyboard;

public static class KeyboardLayoutRepository
{
    public static KeyboardLayout Qwerty { get; } = new(
        "QWERTY",
        [
            Stage("home anchors", "fj"),
            Stage("home row core", "fjdk"),
            Stage("home row expanded", "fjdksl"),
            Stage("home row full", "fjdksla;"),
            Stage("easy top row", "ruei"),
            Stage("top row expanded", "woqp"),
            Stage("bottom row core", "vmc,"),
            Stage("bottom row expanded", "x.z/"),
            Stage("remaining letters", "tyghbn"),
            Stage("capitalization", "ABCDEFGHIJKLMNOPQRSTUVWXYZ"),
            Stage("numbers", "0123456789"),
            Stage("punctuation", "'\"-:?!()")
        ]);

    private static KeyboardLayoutStage Stage(string name, string characters)
    {
        return new KeyboardLayoutStage(name, characters.ToHashSet());
    }
}
