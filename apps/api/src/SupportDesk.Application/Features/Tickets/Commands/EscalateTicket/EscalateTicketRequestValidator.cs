using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .Must(reason => reason is not null && reason.Trim().Length is >= 5 and <= 500)
            .WithMessage("Reason must contain 5 to 500 characters after trimming.");

        RuleFor(request => request.EscalatedBy)
            .NotEmpty()
            .Must(actor => actor is not null && actor.Trim().Length is >= 1 and <= 100)
            .WithMessage("EscalatedBy is required (maximum 100 characters).");
    }
}
