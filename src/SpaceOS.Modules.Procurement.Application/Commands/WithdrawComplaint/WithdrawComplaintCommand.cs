using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.WithdrawComplaint;

public sealed record WithdrawComplaintCommand(
    Guid ComplaintId,
    string WithdrawnBy,
    string Reason) : IRequest<Result>;
