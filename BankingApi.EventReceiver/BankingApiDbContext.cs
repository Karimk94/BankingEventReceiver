using Microsoft.EntityFrameworkCore;

namespace BankingApi.EventReceiver
{
    public class BankingApiDbContext : DbContext
    {
        public DbSet<BankAccount> BankAccounts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer("Server=(local); Database=BankingApiTest; Integrated Security=SSPI; MultipleActiveResultSets = true; TrustServerCertificate=True;");
    }
}
