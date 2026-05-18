namespace Backend.Application.Children.Commands.UpdateChild;

public class UpdateChildCommandValidator : AbstractValidator<UpdateChildCommand>
{
    public UpdateChildCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AgeYears).InclusiveBetween(0, 25);
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}
