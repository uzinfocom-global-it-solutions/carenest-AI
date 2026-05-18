using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Children.Commands.UpdateChild;

public record UpdateChildCommand(
    int ChildId,
    string DisplayName,
    int AgeYears,
    string RequestingUserId) : IRequest<ChildResult>;

public class UpdateChildCommandHandler : IRequestHandler<UpdateChildCommand, ChildResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public UpdateChildCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<ChildResult> Handle(UpdateChildCommand request, CancellationToken cancellationToken)
    {
        var child = await _context.Children.FindAsync([request.ChildId], cancellationToken)
            ?? throw new NotFoundException(nameof(Child), request.ChildId);

        await _authorization.AssertMemberAsync(child.FamilyId, request.RequestingUserId, cancellationToken);

        var previousName = child.DisplayName;
        var previousAge = child.AgeYears;

        child.DisplayName = request.DisplayName;
        child.AgeYears = request.AgeYears;
        child.AgeGroup = Child.ResolveAgeGroup(request.AgeYears);
        child.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "child.updated", "child", child.Id,
            new
            {
                previousName,
                newName = child.DisplayName,
                previousAge,
                newAge = child.AgeYears,
                ageGroup = child.AgeGroup.ToString(),
            }, cancellationToken);

        return new ChildResult(child.Id, child.FamilyId, child.DisplayName,
            child.AgeYears, child.AgeGroup, child.CreatedAt, child.Gender);
    }
}
