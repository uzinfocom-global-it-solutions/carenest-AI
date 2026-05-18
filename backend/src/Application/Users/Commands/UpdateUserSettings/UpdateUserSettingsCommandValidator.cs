namespace Backend.Application.Users.Commands.UpdateUserSettings;

public class UpdateUserSettingsCommandValidator : AbstractValidator<UpdateUserSettingsCommand>
{
    public UpdateUserSettingsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Settings.PreferredLanguage)
            .Length(2, 10).When(x => x.Settings.PreferredLanguage != null);

        RuleFor(x => x.Settings.Timezone)
            .Length(2, 64).When(x => x.Settings.Timezone != null);

        RuleFor(x => x.Settings)
            .Must(s => !s.QuietHoursStart.HasValue || s.QuietHoursEnd.HasValue)
            .WithMessage("Both quiet hours start and end must be set together.");

        RuleFor(x => x.Settings)
            .Must(s => !s.QuietHoursEnd.HasValue || s.QuietHoursStart.HasValue)
            .WithMessage("Both quiet hours start and end must be set together.");
    }
}
