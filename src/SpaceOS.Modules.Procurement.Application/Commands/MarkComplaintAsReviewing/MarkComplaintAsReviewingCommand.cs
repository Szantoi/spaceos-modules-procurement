using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.MarkComplaintAsReviewing;

public sealed record MarkComplaintAsReviewingCommand(
    Guid ComplaintId,
    string ReviewedBy) : IRequest<Result>;
