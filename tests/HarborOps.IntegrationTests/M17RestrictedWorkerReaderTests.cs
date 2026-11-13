using System.Security.Cryptography;
using HarborOps.Api.Audit;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Observability;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17RestrictedWorkerReaderTests
{
    [Fact]
    public async Task RestrictedWorkerCanLinkOnlyOwnedReceiptAndCannotReadM16AuditOrModifyHistory()
    {
        var admin=TestDatabase.ConnectionString();
        using var factory=new ReferenceDataHttpTests.ApiFactory(admin);
        var tenant=await ReferenceDataHttpTests.Setup(factory);
        using var adminClient=tenant.Admin;
        using var manager=tenant.Manager;
        using var analyst=tenant.Analyst;
        var source=await G9Support.NewSource(factory,tenant.A,"g17restricted");
        var receipt=await G9Support.Accept(factory,source,G9Support.EquipmentPayload(
            "g17-restricted-"+Guid.NewGuid().ToString("N")[..10],"SYN-BOX-UNKNOWN"));
        await using var scoped=factory.Services.CreateAsyncScope();
        var db=scoped.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var original=await db.TrackingInboxReceipts.AsNoTracking()
            .SingleAsync(x=>x.Id==receipt);
        var adminStore=new TrackingWorkStore(db,new TrackingWorkerSettings());
        var claim=await adminStore.ClaimOneAsync(tenant.A,CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(receipt,claim!.ReceiptId);

        var workerSecret="g17-worker-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var apiSecret="g17-api-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["AuditRoles:WorkerPassword"]=workerSecret,
            ["AuditRoles:ApiPassword"]=apiSecret
        }).Build();
        await AuditRoleProvisioner.ProvisionAsync(db,config,CancellationToken.None);
        var workerConnection=new NpgsqlConnectionStringBuilder(admin){
            Username="harborops_worker",Password=workerSecret
        }.ConnectionString;
        await using var workerDb=new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(workerConnection).Options);
        var restricted=new TrackingWorkStore(workerDb,new TrackingWorkerSettings());
        var read=await restricted.ReadReceiptCorrelationAsync(claim,CancellationToken.None);
        Assert.Equal(original.CorrelationId,read);
        var foreign=claim with{OrganizationId=tenant.B};
        Assert.Null(await restricted.ReadReceiptCorrelationAsync(foreign,CancellationToken.None));
        var digest=M17CorrelationDigest.FromServerCorrelation(read,
            RandomNumberGenerator.GetBytes(32));
        Assert.NotNull(digest);
        Assert.Equal(64,digest.Length);
        Assert.NotEqual(read,digest);

        await using var connection=new NpgsqlConnection(workerConnection);
        await connection.OpenAsync();
        await using(var deniedAudit=new NpgsqlCommand(
            "SELECT correlation_id FROM audit_entries LIMIT 1",connection)){
            var error=await Assert.ThrowsAsync<PostgresException>(()=>
                deniedAudit.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,error.SqlState);
        }
        await using(var deniedMutation=new NpgsqlCommand(
            "UPDATE tracking_inbox_receipts SET correlation_id='forged' WHERE id=@id",
            connection)){
            deniedMutation.Parameters.AddWithValue("id",receipt);
            var error=await Assert.ThrowsAsync<PostgresException>(()=>
                deniedMutation.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,error.SqlState);
        }
        var unchanged=await db.TrackingInboxReceipts.AsNoTracking()
            .SingleAsync(x=>x.Id==receipt);
        Assert.Equal(original.CorrelationId,unchanged.CorrelationId);
        Assert.Equal(original.OrganizationId,unchanged.OrganizationId);
    }
}
