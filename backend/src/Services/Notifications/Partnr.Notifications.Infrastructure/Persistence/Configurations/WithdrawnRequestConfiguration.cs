using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Partnr.Notifications.Domain.Entities;

namespace Partnr.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class WithdrawnRequestConfiguration : IEntityTypeConfiguration<WithdrawnRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawnRequest> builder)
    {
        builder.ToTable("withdrawn_requests");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
    }
}
