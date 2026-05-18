using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;

namespace Backend.Application.Children.Queries.ListChildrenByFamily;

public record ListChildrenByFamilyQuery(
    int FamilyId,
    string RequestingUserId) : IRequest<IReadOnlyList<ChildResult>>;

public class ListChildrenByFamilyQueryHandler
    : IRequestHandler<ListChildrenByFamilyQuery, IReadOnlyList<ChildResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;

    public ListChildrenByFamilyQueryHandler(IApplicationDbContext context, IFamilyAuthorization authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<ChildResult>> Handle(
        ListChildrenByFamilyQuery request,
        CancellationToken cancellationToken)
    {
        await _authorization.AssertMemberAsync(request.FamilyId, request.RequestingUserId, cancellationToken);

        return await _context.Children
            .Where(c => c.FamilyId == request.FamilyId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ChildResult(c.Id, c.FamilyId, c.DisplayName,
                c.AgeYears, c.AgeGroup, c.CreatedAt, c.Gender))
            .ToListAsync(cancellationToken);
    }
}
