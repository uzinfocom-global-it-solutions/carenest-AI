using Backend.Application.Common.Interfaces;
using Backend.Application.Families.Models;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Families.Commands.CreateFamily;

public record CreateFamilyCommand(
    string Name,
    string CreatedByUserId,
    string? LocationKey,
    double? Latitude,
    double? Longitude) : IRequest<FamilyResult>;

public class CreateFamilyCommandHandler : IRequestHandler<CreateFamilyCommand, FamilyResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _audit;

    public CreateFamilyCommandHandler(IApplicationDbContext context, IAuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<FamilyResult> Handle(CreateFamilyCommand request, CancellationToken cancellationToken)
    {
        var family = new Family
        {
            Name = request.Name,
            CreatedByUserId = request.CreatedByUserId,
            DefaultLocationKey = request.LocationKey,
            DefaultLatitude = request.Latitude,
            DefaultLongitude = request.Longitude,
        };

        _context.Families.Add(family);
        await _context.SaveChangesAsync(cancellationToken);

        _context.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = family.Id,
            UserId = request.CreatedByUserId,
            Role = RoleEnum.Parent,
            Status = FamilyMemberStatusEnum.Active,
        });
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.CreatedByUserId, "family.created", "family", family.Id,
            new { family.Name }, cancellationToken);

        return new FamilyResult(
            family.Id, family.Name, family.CreatedByUserId,
            family.DefaultLocationKey, family.DefaultLatitude, family.DefaultLongitude,
            family.CreatedAt);
    }
}
