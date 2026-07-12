using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public class SupplierComplaintConfiguration : IEntityTypeConfiguration<SupplierComplaint>
{
    public void Configure(EntityTypeBuilder<SupplierComplaint> builder)
    {
        builder.ToTable("SupplierComplaints");
        builder.HasKey(x => x.Id);

        // Basic properties
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ComplaintNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SupplierId).IsRequired();
        builder.Property(x => x.DeliveryId).IsRequired();
        builder.Property(x => x.PurchaseOrderId);

        // Complaint content
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(5000).IsRequired();
        builder.Property(x => x.AffectedQuantity).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.ClaimedAmount).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        // QA result (owned value object)
        builder.OwnsOne(x => x.QaResult, qa =>
        {
            qa.Property(q => q.Status).HasColumnName("QualityStatus");
            qa.Property(q => q.AcceptedQuantity).HasColumnName("QaAcceptedQty").HasPrecision(10, 2);
            qa.Property(q => q.RejectedQuantity).HasColumnName("QaRejectedQty").HasPrecision(10, 2);
            qa.Property(q => q.DefectDescription).HasColumnName("QaDefectDescription").HasMaxLength(2000);
            qa.Property(q => q.DefectPhotoPaths).HasColumnName("QaDefectPhotoPaths").HasColumnType("jsonb");
        });

        builder.Property(x => x.EvidencePaths).HasColumnName("EvidencePaths").HasColumnType("jsonb");

        // FSM
        builder.Property(x => x.Status).IsRequired();

        // Audit
        builder.Property(x => x.CreatedBy).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // Owned entity: ComplaintResponse
        builder.OwnsOne(x => x.SupplierResponse, response =>
        {
            response.Property(r => r.Type).HasColumnName("ResponseType");
            response.Property(r => r.ResponseText).HasColumnName("ResponseText").HasMaxLength(3000);
            response.Property(r => r.OfferedAmount).HasColumnName("OfferedAmount").HasPrecision(12, 2);
            response.Property(r => r.CounterProposal).HasColumnName("CounterProposal").HasMaxLength(2000);
            response.Property(r => r.AttachmentPaths).HasColumnName("ResponseAttachmentPaths").HasColumnType("jsonb");
            response.Property(r => r.RespondedBy).HasColumnName("RespondedBy").HasMaxLength(255);
            response.Property(r => r.RespondedAt).HasColumnName("RespondedAt");
        });

        // Owned entity: ComplaintResolution
        builder.OwnsOne(x => x.Resolution, resolution =>
        {
            resolution.Property(r => r.Type).HasColumnName("ResolutionType");
            resolution.Property(r => r.Summary).HasColumnName("ResolutionSummary").HasMaxLength(2000);
            resolution.Property(r => r.FinalAmount).HasColumnName("FinalAmount").HasPrecision(12, 2);
            resolution.Property(r => r.Action).HasColumnName("ResolutionAction");
            resolution.Property(r => r.ResolvedBy).HasColumnName("ResolvedBy").HasMaxLength(255);
            resolution.Property(r => r.ResolvedAt).HasColumnName("ResolvedAt");
        });

        // Indexes
        builder.HasIndex(x => new { x.TenantId, x.ComplaintNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SupplierId });
        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasFilter("\"Status\" NOT IN (5, 6, 7)"); // Open complaints only
        builder.HasIndex(x => x.DeliveryId);

        // Foreign keys (logical, not enforced in DB)
        // RLS will handle security
    }
}
