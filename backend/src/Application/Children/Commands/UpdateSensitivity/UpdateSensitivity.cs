using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Children.Commands.UpdateSensitivity;

public record UpdateSensitivityCommand(
    int ChildId,
    ChildSensitivityDto Sensitivity,
    string RequestingUserId) : IRequest;

public class UpdateSensitivityCommandHandler : IRequestHandler<UpdateSensitivityCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public UpdateSensitivityCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task Handle(UpdateSensitivityCommand request, CancellationToken cancellationToken)
    {
        var child = await _context.Children.FindAsync([request.ChildId], cancellationToken)
            ?? throw new NotFoundException(nameof(Child), request.ChildId);

        await _authorization.AssertMemberAsync(child.FamilyId, request.RequestingUserId, cancellationToken);

        var sensitivity = await _context.ChildSensitivities
            .FirstOrDefaultAsync(s => s.ChildId == request.ChildId, cancellationToken)
            ?? throw new NotFoundException(nameof(ChildSensitivity), request.ChildId);

        var dto = request.Sensitivity;
        sensitivity.HeatSensitive = dto.HeatSensitive;
        sensitivity.ColdSensitive = dto.ColdSensitive;
        sensitivity.AirQualitySensitive = dto.AirQualitySensitive;
        sensitivity.PollenSensitive = dto.PollenSensitive;
        sensitivity.UvSensitive = dto.UvSensitive;
        sensitivity.SkinSensitive = dto.SkinSensitive;
        sensitivity.ActivitySensitive = dto.ActivitySensitive;
        sensitivity.RespiratorySensitive = dto.RespiratorySensitive;
        sensitivity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(request.RequestingUserId, "child.sensitivity_updated",
            "child_sensitivity", request.ChildId, null, cancellationToken);
    }
}
