using Backend.Application.Common.Interfaces;
using Backend.Application.Recommendations.Models;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Recommendations.Commands.SubmitFeedback;

public record SubmitFeedbackCommand(
    int RecommendationId,
    string UserId,
    string FeedbackType) : IRequest<RecommendationFeedbackResult>;

public class SubmitFeedbackCommandHandler
    : IRequestHandler<SubmitFeedbackCommand, RecommendationFeedbackResult>
{
    private readonly IApplicationDbContext _context;

    public SubmitFeedbackCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<RecommendationFeedbackResult> Handle(
        SubmitFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FeedbackTypeEnum>(request.FeedbackType, ignoreCase: true, out var type))
            throw new ValidationException([new("feedbackType", $"Invalid feedback type '{request.FeedbackType}'.")]);

        var feedback = new RecommendationFeedback
        {
            RecommendationId = request.RecommendationId,
            UserId = request.UserId,
            FeedbackType = type,
        };

        _context.RecommendationFeedbacks.Add(feedback);
        await _context.SaveChangesAsync(cancellationToken);

        return new RecommendationFeedbackResult(
            feedback.Id, request.RecommendationId, request.UserId, type, feedback.CreatedAt);
    }
}
