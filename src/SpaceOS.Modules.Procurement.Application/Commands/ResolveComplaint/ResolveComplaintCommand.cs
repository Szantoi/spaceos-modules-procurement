using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Application.Commands.ResolveComplaint;

public sealed record ResolveComplaintCommand(
    Guid ComplaintId,
    ResolutionType ResolutionType,
    ResolutionAction ResolutionAction,
    decimal? ResolutionValue,
    string? ResolutionValueCurrency,
    string? ResolutionNotes,
    string ResolvedBy) : IRequest<Result>;
