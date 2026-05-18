using System.Collections.Generic;
using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Children.Commands.CreateChild;

public record CreateChildCommand(
    int FamilyId,
    string DisplayName,
    int AgeYears,
    string RequestingUserId,
    string? Gender = null) : IRequest<ChildResult>;

public class CreateChildCommandHandler : IRequestHandler<CreateChildCommand, ChildResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public CreateChildCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<ChildResult> Handle(CreateChildCommand request, CancellationToken cancellationToken)
    {
        await _authorization.AssertMemberAsync(request.FamilyId, request.RequestingUserId, cancellationToken);

        var detectedGender = request.Gender ?? GenderDetector.Detect(request.DisplayName);

        var child = new Child
        {
            FamilyId = request.FamilyId,
            DisplayName = request.DisplayName,
            AgeYears = request.AgeYears,
            AgeGroup = Child.ResolveAgeGroup(request.AgeYears),
            Gender = detectedGender,
        };

        _context.Children.Add(child);
        await _context.SaveChangesAsync(cancellationToken);

        _context.ChildSensitivities.Add(new ChildSensitivity { ChildId = child.Id });
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "child.created", "child", child.Id,
            new { request.DisplayName, request.AgeYears, ageGroup = child.AgeGroup.ToString() },
            cancellationToken);

        return new ChildResult(
            child.Id, child.FamilyId, child.DisplayName,
            child.AgeYears, child.AgeGroup, child.CreatedAt, child.Gender);
    }
}

internal static class GenderDetector
{
    private static readonly HashSet<string> _femaleEn = new(StringComparer.OrdinalIgnoreCase)
    {
        "alice","anna","annie","aria","aurora","bella","chloe","claire","diana",
        "elena","eliza","elizabeth","ella","emily","emma","eva","grace","hannah",
        "isabella","isla","julia","kate","katelyn","katherine","kelly","layla","lea",
        "leah","lily","lisa","lucy","luna","mia","mila","nora","olivia","sarah",
        "sara","sofia","sophia","stella","victoria","violet","zoe",
        "daria","dasha","kira","lara","lena","masha","natasha","oksana","olga","rita","sonya","tanya","vera","vika","yulia","zoya"
    };

    public static string? Detect(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var lower = name.Trim().ToLowerInvariant();
        var first = lower.Split(' ')[0];

        if (_femaleEn.Contains(first)) return "Female";

        if (first.EndsWith('а') || first.EndsWith('я')) return "Female";

        if (first.Length > 2 && !first.EndsWith('а') && !first.EndsWith('я')
            && (char.IsLetter(first[^1]) && !"аеёиоуыэюя".Contains(first[^1])))
            return "Male";

        return null; // uncertain
    }
}
