using Backend.Domain.Enums;

namespace Backend.Application.Chats.Commands.SendMessage;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    private static readonly string[] ValidInputModes = Enum.GetNames<InputModeEnum>()
        .Select(n => n.ToLower())
        .ToArray();

    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ChatId).GreaterThan(0);
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.InputMode)
            .Must(m => ValidInputModes.Contains(m.ToLower()))
            .WithMessage($"InputMode must be one of: {string.Join(", ", ValidInputModes)}");
    }
}
