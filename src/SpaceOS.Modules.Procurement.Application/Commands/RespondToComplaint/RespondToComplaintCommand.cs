using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Application.Commands.RespondToComplaint;

public sealed record RespondToComplaintCommand(
    Guid ComplaintId,
    ResponseType ResponseType,
    string ResponseText,
    ResolutionAction ProposedAction,
    decimal? ProposedValue,
    string? ProposedValueCurrency,
    string ResponseProvidedBy) : IRequest<Result>;
