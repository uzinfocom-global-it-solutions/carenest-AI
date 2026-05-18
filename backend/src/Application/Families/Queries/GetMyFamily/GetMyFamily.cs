using Backend.Application.Common.Interfaces;
using Backend.Application.Families.Models;
using Backend.Domain.Enums;

namespace Backend.Application.Families.Queries.GetMyFamily;

public record GetMyFamilyQuery(string RequestingUserId) : IRequest<FamilyResult?>;

public class GetMyFamilyQueryHandler : IRequestHandler<GetMyFamilyQuery, FamilyResult?>
{
    private readonly IApplicationDbContext _context;

    public GetMyFamilyQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<FamilyResult?> Handle(GetMyFamilyQuery request, CancellationToken cancellationToken)
    {
        var membership = await _context.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == request.RequestingUserId
                     && m.Status == FamilyMemberStatusEnum.Active)
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Family)
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null) return null;

        return new FamilyResult(
            membership.Id,
            membership.Name,
            membership.CreatedByUserId,
            membership.DefaultLocationKey,
            membership.DefaultLatitude,
            membership.DefaultLongitude,
            membership.CreatedAt);
    }
}
