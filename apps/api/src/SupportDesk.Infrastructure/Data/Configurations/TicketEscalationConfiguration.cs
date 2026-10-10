using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>Relational mapping for the append-only ticket escalation audit history.</summary>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", table =>
            table.HasCheckConstraint(
                "CK_TicketEscalations_OneLevel",
                "[ToPriority] = [FromPriority] + 1"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.FromPriority).HasConversion<int>().IsRequired();
        builder.Property(e => e.ToPriority).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.EscalatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<Ticket>()
            .WithMany(ticket => ticket.Escalations)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports WHERE TicketId = ? ORDER BY EscalatedAtUtc DESC, Id DESC.
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc, e.Id })
            .HasDatabaseName("IX_TicketEscalations_Ticket_Time_Id");
    }
}
