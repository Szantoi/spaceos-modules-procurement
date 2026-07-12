using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.AcceptComplaintResponse;

public sealed record AcceptComplaintResponseCommand(
    Guid ComplaintId,
    string AcceptedBy) : IRequest<Result>;
