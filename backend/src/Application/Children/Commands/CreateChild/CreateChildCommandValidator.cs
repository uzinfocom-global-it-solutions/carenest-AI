namespace Backend.Application.Children.Commands.CreateChild;

public class CreateChildCommandValidator : AbstractValidator<CreateChildCommand>
{
    public CreateChildCommandValidator()
    {
        RuleFor(x => x.FamilyId).GreaterThan(0);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AgeYears).InclusiveBetween(0, 17);
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}
