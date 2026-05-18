using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Children.Queries.GetChild;

public record GetChildQuery(int ChildId, string RequestingUserId) : IRequest<ChildResult>;

public class GetChildQueryHandler : IRequestHandler<GetChildQuery, ChildResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;

    public GetChildQueryHandler(IApplicationDbContext context, IFamilyAuthorization authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<ChildResult> Handle(GetChildQuery request, CancellationToken cancellationToken)
    {
        var child = await _context.Children.FindAsync([request.ChildId], cancellationToken)
            ?? throw new NotFoundException(nameof(Child), request.ChildId);

        await _authorization.AssertMemberAsync(child.FamilyId, request.RequestingUserId, cancellationToken);

        return new ChildResult(child.Id, child.FamilyId, child.DisplayName,
            child.AgeYears, child.AgeGroup, child.CreatedAt, child.Gender);
    }
}
