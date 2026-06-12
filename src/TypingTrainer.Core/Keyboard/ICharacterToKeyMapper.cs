namespace TypingTrainer.Core.Keyboard;

public interface ICharacterToKeyMapper
{
    CharacterKeyMapping? Map(char character);
}
