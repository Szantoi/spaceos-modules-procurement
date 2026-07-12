using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence;

public class ProcurementDbContext : DbContext
{
    public ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : base(options) { }

    // Core v1 entities
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();

    // v2 entities
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<InvoiceMatchEntity> InvoiceMatches => Set<InvoiceMatchEntity>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<MatchPolicyEntity> MatchPolicies => Set<MatchPolicyEntity>();
    public DbSet<ProcurementOutboxMessage> OutboxMessages => Set<ProcurementOutboxMessage>();
    public DbSet<ProcurementInboxMessage> InboxMessages => Set<ProcurementInboxMessage>();
    public DbSet<ProcurementAuditLog> AuditLogs => Set<ProcurementAuditLog>();

    // Supplier complaint flow
    public DbSet<SupplierComplaint> SupplierComplaints => Set<SupplierComplaint>();

    // Subcontracting
    public DbSet<SubcontractOrder> SubcontractOrders => Set<SubcontractOrder>();

    // ASN tracking (Week 3)
    public DbSet<AsnShipment> AsnShipments => Set<AsnShipment>();
    public DbSet<ReceiptQueue> ReceiptQueues => Set<ReceiptQueue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("spaceos_procurement");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcurementDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
