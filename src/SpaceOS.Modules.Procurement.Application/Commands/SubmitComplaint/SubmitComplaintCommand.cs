using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.SubmitComplaint;

public sealed record SubmitComplaintCommand(
    Guid ComplaintId,
    string SubmittedBy) : IRequest<Result>;
