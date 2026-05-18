namespace Backend.Application.Calendar.Commands.CreateCalendarEvent;

public class CreateCalendarEventCommandValidator : AbstractValidator<CreateCalendarEventCommand>
{
    public CreateCalendarEventCommandValidator()
    {
        RuleFor(x => x.FamilyId).GreaterThan(0);
        RuleFor(x => x.RequestingUserId).NotEmpty();
        RuleFor(x => x.Event.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Event.Description).MaximumLength(2000)
            .When(x => x.Event.Description != null);
        RuleFor(x => x.Event.LocationLabel).MaximumLength(200)
            .When(x => x.Event.LocationLabel != null);
        RuleFor(x => x.Event.LocationKey).MaximumLength(100)
            .When(x => x.Event.LocationKey != null);
    }
}
