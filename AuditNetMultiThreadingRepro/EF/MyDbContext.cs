using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Audit.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuditNetMultiThreadingRepro
{
    public class MyDbContext : DbContext
    {
        public DbSet<AuditLogEntry> AuditLogEntries { get; set; }
        public DbSet<Product> Products { get; set; }

        private static DbContextHelper _helper = new DbContextHelper();
        private readonly IAuditDbContext _auditContext;

        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
            _auditContext = new DefaultAuditContext(this);
            _helper.SetConfig(_auditContext);
        }


        public IDbContextTransaction BeginTransaction()
        {
            return Database.BeginTransaction(IsolationLevel.Serializable);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return Database.BeginTransactionAsync(IsolationLevel.Serializable);
        }

        public override int SaveChanges()
        {
            return _helper.SaveChanges(_auditContext, () => base.SaveChanges());
        }

        public override async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _helper.SaveChangesAsync(_auditContext,
                () => base.SaveChangesAsync(cancellationToken));
        }
    }
}