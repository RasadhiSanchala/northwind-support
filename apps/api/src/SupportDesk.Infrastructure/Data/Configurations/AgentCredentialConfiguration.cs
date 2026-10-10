using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class AgentCredentialConfiguration : IEntityTypeConfiguration<AgentCredential>
{
    public void Configure(EntityTypeBuilder<AgentCredential> builder)
    {
        builder.ToTable("AgentCredentials");
        builder.HasKey(x => x.AgentId);
        builder.Property(x => x.AgentId).ValueGeneratedNever();
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(300);
        builder.HasOne<Agent>().WithOne().HasForeignKey<AgentCredential>(x => x.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
