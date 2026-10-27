using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Workflow_pg_fresh_review_query_preserves_warning_and_refuses_complete_row_coverage()
    {
        var seed=await SeedGraphAsync();var item=await WorkflowStoredCase(seed.OrganizationA,seed.ActorA);
        var now=DateTimeOffset.UtcNow;var warningCode="line_coverage_incomplete";var warningText="Native row coverage needs review — صفوف تحتاج مراجعة";
        await using(var write=_database.CreateContext()){
            item=await write.ReconciliationCases.SingleAsync(x=>x.Id==item.Id);
            foreach(var role in new[]{ReconciliationDocumentRole.Invoice,ReconciliationDocumentRole.PurchaseOrder}){
                var doc=new Document(Guid.NewGuid(),seed.OrganizationA,seed.ActorA,role+".pdf",Guid.NewGuid()+".pdf","application/pdf",100,new string(role==ReconciliationDocumentRole.Invoice?'a':'b',64),now);
                var extraction=doc.QueueExtraction(Guid.NewGuid(),1,"local","local-1.6.0","2.0",Guid.NewGuid().ToString("N"),now);extraction.Start(now);
                var values=new List<(string Key,FieldValueType Type,string Value,int Occurrence)>{
                    (role==ReconciliationDocumentRole.Invoice?"invoice_number":"purchase_order_number",FieldValueType.Identifier,role==ReconciliationDocumentRole.Invoice?"INV-1":"PO-1",0),
                    ("supplier_trn",FieldValueType.Identifier,"000000000000701",0),("currency",FieldValueType.Currency,"AED",0),("total",FieldValueType.Decimal,"1260",0),
                    ("document_total_basis",FieldValueType.String,"gross",0),("vat",FieldValueType.Decimal,"60",0),("tax_currency",FieldValueType.Currency,"AED",0),("tax_basis",FieldValueType.String,"net",0),
                    ("line_item_code",FieldValueType.Identifier,"ITEM-1",1),("line_description",FieldValueType.String,"Service",1),("line_quantity",FieldValueType.Decimal,"2",1),
                    ("line_unit",FieldValueType.String,"EA",1),("line_unit_price",FieldValueType.Decimal,"600",1),("line_price_basis",FieldValueType.String,"1 EA",1),
                    ("line_price_amount_basis",FieldValueType.String,"net",1),("line_total",FieldValueType.Decimal,"1260",1),("line_total_basis",FieldValueType.String,"gross",1),("line_tax",FieldValueType.Decimal,"60",1)};
                if(role==ReconciliationDocumentRole.Invoice)values.Add(("purchase_order_number",FieldValueType.Identifier,"PO-1",0));
                extraction.AddPage(Guid.NewGuid(),1,string.Join("\n",values.Select(x=>x.Value)),0);var offset=0;
                foreach(var v in values){var field=extraction.AddField(Guid.NewGuid(),v.Key,v.Occurrence,v.Type,v.Value,.95m,now,v.Value);var end=offset+System.Text.Encoding.UTF8.GetByteCount(v.Value);extraction.AddEvidence(field,Guid.NewGuid(),1,v.Value,offset,end);offset=end+1;}
                if(role==ReconciliationDocumentRole.Invoice)extraction.AddWarning(Guid.NewGuid(),warningCode,ExtractionWarningSeverity.Info,warningText,1);
                extraction.RecordProviderMetrics(1,1,null,null,null);extraction.Succeed(role==ReconciliationDocumentRole.Invoice?"invoice":"purchase_order","en",now,"fixture","independent-warning");doc.RecordExtractionSuccess(extraction,now);
                var review=doc.OpenReview(Guid.NewGuid(),seed.ActorA,now);var sequence=0;foreach(var field in extraction.Fields)review.AddAction(Guid.NewGuid(),seed.ActorA,++sequence,ReviewActionType.Confirmed,field.FieldKey,field.OccurrenceIndex,field.Id,null,null,null,now);review.Complete(extraction.Fields,now);
                item.SetDraftDocument(role,doc.Id,now);item.LinkReviewedDocument(Guid.NewGuid(),role,doc.Id,review.Id,extraction.Id,doc.Sha256,now);write.Documents.Add(doc);
            }
            await write.SaveChangesAsync();
        }
        await using var services=CitedServices(seed.OrganizationA,seed.ActorA,"http://127.0.0.1:1");
        await using(var evaluator=services.CreateAsyncScope()){
            var doc=await evaluator.ServiceProvider.GetRequiredService<IDocumentRepository>().GetForReviewAsync(seed.OrganizationA,item.DraftInvoiceDocumentId!.Value,default);
            var warning=Assert.Single(doc!.ExtractionRuns.Single().Warnings);Assert.Equal(warningCode,warning.Code);Assert.Equal(warningText,warning.SafeSummary);
            await evaluator.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>().HandleAsync(item.Id,item.ConcurrencyToken,new(RuleSetVersion:"invoice-po-1.0",OrderScope:"FullOrder"),default);
        }
        await using var fresh=services.CreateAsyncScope();var run=Assert.Single((await fresh.ServiceProvider.GetRequiredService<GetReconciliationCaseHandler>().HandleAsync(item.Id,default)).Runs);
        var source=Assert.Single(run.Sources!.Where(x=>x.Role=="Invoice"));Assert.Equal("Succeeded",source.Outcome);Assert.Equal("Unverified",source.RowCoverageStatus);
        using var warnings=JsonDocument.Parse(source.WarningsJson!);var saved=Assert.Single(warnings.RootElement.EnumerateArray());Assert.Equal(warningCode,saved.GetProperty("Code").GetString());Assert.Equal(warningText,saved.GetProperty("SafeSummary").GetString());
        Assert.Equal("CouldNotCheck",Assert.Single(run.Checks!.Where(x=>x.FieldKey=="source_coverage")).Status);
        Assert.DoesNotContain(run.Checks!,x=>x.Occurrence is >0 && x.Status=="NoDifference" && x.SignedDifference is not null);
        Assert.Equal("Incomplete",run.Coverage!.Status);
    }

    private async Task<ReconciliationCase> WorkflowStoredCase(Guid org,Guid actor)
    {
        var item=new ReconciliationCase(Guid.NewGuid(),org,actor,"Saved invoice pair",DateTimeOffset.UtcNow,workflowKind:ReconciliationWorkflowKind.InvoicePurchaseOrder);
        await using var db=_database.CreateContext();db.ReconciliationCases.Add(item);await db.SaveChangesAsync();return item;
    }
    private static void WorkflowAudit(AsyncServiceScope scope,Guid org,Guid actor)=>scope.ServiceProvider.GetRequiredService<SecurityAuditContext>().Bind(org,actor,"System","workflow-test");
    private static Task<ReconciliationCaseView> WorkflowSave(AsyncServiceScope scope,Guid item,string role,Guid? doc,Guid version,string key)=>
        scope.ServiceProvider.GetRequiredService<SetReconciliationDraftDocumentHandler>().HandleAsync(item,role,version,new(doc,key),default);

    [Fact]
    public async Task Workflow_pg_save_reopen_exact_replay_and_actor_org_key_isolation()
    {
        var seed=await SeedGraphAsync();var item=await WorkflowStoredCase(seed.OrganizationA,seed.ActorA);
        await using var services=CitedServices(seed.OrganizationA,seed.ActorA,"http://127.0.0.1:1");
        ReconciliationCaseView first;
        await using(var save=services.CreateAsyncScope()){WorkflowAudit(save,seed.OrganizationA,seed.ActorA);first=await WorkflowSave(save,item.Id,"Invoice",seed.DocumentA,item.ConcurrencyToken,"shared");}
        await using(var reopen=services.CreateAsyncScope()){
            WorkflowAudit(reopen,seed.OrganizationA,seed.ActorA);
            var view=await reopen.ServiceProvider.GetRequiredService<GetReconciliationCaseHandler>().HandleAsync(item.Id,default);
            Assert.Equal("InvoicePurchaseOrder",view.WorkflowKind);Assert.Equal(seed.DocumentA,Assert.Single(view.DraftDocuments!).DocumentId);Assert.Empty(view.Documents);Assert.Empty(view.Runs);
            var replay=await WorkflowSave(reopen,item.Id,"Invoice",seed.DocumentA,item.ConcurrencyToken,"shared");Assert.Equal(first.ConcurrencyToken,replay.ConcurrencyToken);
            await Assert.ThrowsAsync<ReconciliationConflictException>(()=>WorkflowSave(reopen,item.Id,"Invoice",null,item.ConcurrencyToken,"shared"));
        }
        Assert.Equal(1,await ScalarAsync<long>("SELECT count(*) FROM reconciliation_draft_requests WHERE reconciliation_case_id=@id",("id",item.Id)));
        var before=await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE target_id=@id",("id",item.Id));Assert.True(before>0);
        await using(var replay=services.CreateAsyncScope()){WorkflowAudit(replay,seed.OrganizationA,seed.ActorA);await WorkflowSave(replay,item.Id,"Invoice",seed.DocumentA,item.ConcurrencyToken,"shared");}
        Assert.Equal(before,await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE target_id=@id",("id",item.Id)));
        await using(var db=_database.CreateContext()){
            var actor=new Actor(Guid.NewGuid(),seed.OrganizationA,ActorKind.System,"Second actor",DateTimeOffset.UtcNow);db.Actors.Add(actor);await db.SaveChangesAsync();
            await using var second=CitedServices(seed.OrganizationA,actor.Id,"http://127.0.0.1:1");await using var scope=second.CreateAsyncScope();await WorkflowSave(scope,item.Id,"Invoice",seed.DocumentA,first.ConcurrencyToken,"shared");
        }
        var foreign=await WorkflowStoredCase(seed.OrganizationB,seed.ActorB);await using var other=CitedServices(seed.OrganizationB,seed.ActorB,"http://127.0.0.1:1");await using var otherScope=other.CreateAsyncScope();
        await WorkflowSave(otherScope,foreign.Id,"Invoice",seed.DocumentB,foreign.ConcurrencyToken,"shared");
        Assert.Equal(3,await ScalarAsync<long>("SELECT count(*) FROM reconciliation_draft_requests WHERE request_key='shared'"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Workflow_pg_concurrent_new_intents_only_one_commits_receipt_and_draft(bool noOp)
    {
        var seed=await SeedGraphAsync();var item=await WorkflowStoredCase(seed.OrganizationA,seed.ActorA);
        await using var services=CitedServices(seed.OrganizationA,seed.ActorA,"http://127.0.0.1:1");await using var one=services.CreateAsyncScope();await using var two=services.CreateAsyncScope();
        foreach(var scope in new[]{one,two}){WorkflowAudit(scope,seed.OrganizationA,seed.ActorA);await scope.ServiceProvider.GetRequiredService<IReconciliationRepository>().GetAsync(seed.OrganizationA,item.Id,default);}
        async Task<bool> Save(AsyncServiceScope scope,string key){try{await WorkflowSave(scope,item.Id,"Invoice",noOp?null:seed.DocumentA,item.ConcurrencyToken,key);return true;}catch(ReconciliationPreconditionException){return false;}}
        Assert.Single((await Task.WhenAll(Save(one,"one"),Save(two,"two"))).Where(x=>x));
        Assert.Equal(1,await ScalarAsync<long>("SELECT count(*) FROM reconciliation_draft_requests WHERE reconciliation_case_id=@id",("id",item.Id)));
        if(noOp)Assert.True(await ScalarAsync<bool>("SELECT draft_invoice_document_id IS NULL FROM reconciliation_cases WHERE id=@id",("id",item.Id)));
        else Assert.Equal(seed.DocumentA,await ScalarAsync<Guid>("SELECT draft_invoice_document_id FROM reconciliation_cases WHERE id=@id",("id",item.Id)));
    }

    [Fact]
    public async Task Workflow_pg_queued_draft_blocks_source_deletion_by_exact_FK_until_explicit_clear()
    {
        var seed=await SeedGraphAsync();var item=await WorkflowStoredCase(seed.OrganizationA,seed.ActorA);
        var doc=new Document(Guid.NewGuid(),seed.OrganizationA,seed.ActorA,"Unprocessed.pdf",Guid.NewGuid()+".pdf","application/pdf",100,new string('a',64),DateTimeOffset.UtcNow);
        await using(var db=_database.CreateContext()){db.Documents.Add(doc);await db.SaveChangesAsync();Assert.Empty(doc.ExtractionRuns);}
        await using var services=CitedServices(seed.OrganizationA,seed.ActorA,"http://127.0.0.1:1");ReconciliationCaseView saved;
        await using(var save=services.CreateAsyncScope())saved=await WorkflowSave(save,item.Id,"Invoice",doc.Id,item.ConcurrencyToken,"retain-source");
        // Satisfy the pre-existing purge-record guard so this assertion reaches the new draft FK.
        await using(var db=_database.CreateContext()){db.DocumentDeletions.Add(new DocumentDeletion(Guid.NewGuid(),seed.OrganizationA,doc.Id,doc.StorageKey,doc.Sha256,doc.SizeBytes,doc.ConcurrencyToken,Guid.NewGuid(),DateTimeOffset.UtcNow));await db.SaveChangesAsync();}
        await using(var connection=await _database.OpenConnectionAsync()){
            await using var command=new Npgsql.NpgsqlCommand("DELETE FROM documents WHERE id=@id",connection);command.Parameters.AddWithValue("id",doc.Id);
            var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>command.ExecuteNonQueryAsync());Assert.Equal("23503",error.SqlState);Assert.Equal("FK_reconciliation_cases_documents_organization_id_draft_invoic~",error.ConstraintName);
        }
        await using(var clear=services.CreateAsyncScope())await WorkflowSave(clear,item.Id,"Invoice",null,saved.ConcurrencyToken,"clear-source");
        await ExecuteAsync("DELETE FROM documents WHERE id=@id",("id",doc.Id));
        Assert.Equal(1,await ScalarAsync<long>("SELECT count(*) FROM reconciliation_draft_requests WHERE document_id=@id",("id",doc.Id)));
        await using var retry=services.CreateAsyncScope();await Assert.ThrowsAsync<ReconciliationNotFoundException>(()=>WorkflowSave(retry,item.Id,"Invoice",doc.Id,item.ConcurrencyToken,"retain-source"));
    }

    [Fact]
    public async Task Workflow_pg_restricted_grants_immutable_receipts_and_SQL_recorded_ABA_refusal()
    {
        var seed=await SeedGraphAsync();var fixture=await SupplierPairStoredFixture(seed.OrganizationA,seed.ActorA,"1260","1260");
        var sources=fixture.DocumentLinks.ToArray();var item=await WorkflowStoredCase(seed.OrganizationA,seed.ActorA);await using var role=await ApiRoleAsync();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Database"]=role.Connection,["AiService:BaseUrl"]="http://127.0.0.1:1",["AiService:WorkerToken"]="test-only-token"}).Build();
        await using var services=new ServiceCollection().AddInfrastructure(config).AddSingleton<IExecutionContext>(new CitedExecution(seed.OrganizationA,seed.ActorA)).BuildServiceProvider();
        var original=item.ConcurrencyToken;var invoice=sources.Single(x=>x.Role==ReconciliationDocumentRole.Invoice).DocumentId;
        Guid recordedVersion;ReconciliationCaseView view;
        await using(var scope=services.CreateAsyncScope()){WorkflowAudit(scope,seed.OrganizationA,seed.ActorA);view=await WorkflowSave(scope,item.Id,"Invoice",invoice,original,"A");}
        await using(var db=role.Db()){recordedVersion=(await db.ReconciliationCases.SingleAsync(x=>x.Id==item.Id)).DraftInvoiceVersion!.Value;}
        await using(var scope=services.CreateAsyncScope()){WorkflowAudit(scope,seed.OrganizationA,seed.ActorA);view=await WorkflowSave(scope,item.Id,"Invoice",seed.DocumentA,view.ConcurrencyToken,"B");}
        await using(var db=role.Db()){
            var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reconciliation_cases SET draft_invoice_document_id={invoice},draft_invoice_version={recordedVersion} WHERE id={item.Id}"));Assert.Equal("55000",error.SqlState);
            await ApiDatabasePolicy.VerifyAsync(db,role.Role,true,default);
        }
        await using(var scope=services.CreateAsyncScope()){WorkflowAudit(scope,seed.OrganizationA,seed.ActorA);view=await WorkflowSave(scope,item.Id,"Invoice",invoice,view.ConcurrencyToken,"A2");
            var error=await Assert.ThrowsAsync<ReconciliationConflictException>(()=>WorkflowSave(scope,item.Id,"Invoice",invoice,original,"A"));Assert.Equal("draft_intent_superseded",error.Code);}
        await AssertSqlStateAsync("55000","UPDATE reconciliation_draft_requests SET payload_hash=repeat('b',64) WHERE reconciliation_case_id=@id",("id",item.Id));
        await AssertSqlStateAsync("55000","DELETE FROM reconciliation_draft_requests WHERE reconciliation_case_id=@id",("id",item.Id));
        await AssertSqlStateAsync("55000","UPDATE reconciliation_cases SET workflow_kind='Legacy' WHERE id=@id",("id",item.Id));
        await AssertSqlStateAsync("23503","UPDATE reconciliation_cases SET draft_purchase_order_document_id=@doc,draft_purchase_order_version=gen_random_uuid() WHERE id=@id",("doc",seed.DocumentB),("id",item.Id));
        Assert.Equal(invoice,await ScalarAsync<Guid>("SELECT draft_invoice_document_id FROM reconciliation_cases WHERE id=@id",("id",item.Id)));
    }

    [Fact]
    public async Task Workflow_pg_draft_replacement_keeps_saved_pair_run_and_only_clears_its_pin()
    {
        var seed=await SeedGraphAsync();var fixture=await SupplierPairStoredFixture(seed.OrganizationA,seed.ActorA,"1260","1261");var item=await WorkflowStoredCase(seed.OrganizationA,seed.ActorA);
        await using(var db=_database.CreateContext()){
            item=await db.ReconciliationCases.SingleAsync(x=>x.Id==item.Id);
            foreach(var source in fixture.DocumentLinks){item.SetDraftDocument(source.Role,source.DocumentId,DateTimeOffset.UtcNow);item.LinkReviewedDocument(Guid.NewGuid(),source.Role,source.DocumentId,source.ReviewSessionId,source.ExtractionRunId,source.Sha256,DateTimeOffset.UtcNow);}await db.SaveChangesAsync();}
        await using var services=CitedServices(seed.OrganizationA,seed.ActorA,"http://127.0.0.1:1");
        await using(var evaluate=services.CreateAsyncScope()){await evaluate.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>().HandleAsync(item.Id,item.ConcurrencyToken,new(RuleSetVersion:"invoice-po-1.0",OrderScope:"FullOrder"),default);}
        ReconciliationCaseView before;await using(var read=services.CreateAsyncScope()){before=await read.ServiceProvider.GetRequiredService<GetReconciliationCaseHandler>().HandleAsync(item.Id,default);}
        await using(var replace=services.CreateAsyncScope()){WorkflowAudit(replace,seed.OrganizationA,seed.ActorA);await WorkflowSave(replace,item.Id,"Invoice",seed.DocumentA,before.ConcurrencyToken,"replacement");}
        await using var afterScope=services.CreateAsyncScope();var after=await afterScope.ServiceProvider.GetRequiredService<GetReconciliationCaseHandler>().HandleAsync(item.Id,default);
        Assert.Equal("PurchaseOrder",Assert.Single(after.Documents).Role);Assert.Equal(Assert.Single(before.Runs).Id,Assert.Single(after.Runs).Id);
        var old=Assert.Single(before.Runs);var retained=Assert.Single(after.Runs);
        Assert.Equal(old.EvaluationActorId,retained.EvaluationActorId);Assert.Equal(old.EvaluatedAt,retained.EvaluatedAt);Assert.Equal(old.OrderScope,retained.OrderScope);Assert.Equal(old.Coverage,retained.Coverage);
        Assert.Equal(old.Checks!.OrderBy(x=>x.Id),retained.Checks!.OrderBy(x=>x.Id));Assert.Equal(old.Fields.OrderBy(x=>x.Id),retained.Fields.OrderBy(x=>x.Id));Assert.Equal(old.Sources!.OrderBy(x=>x.Id),retained.Sources!.OrderBy(x=>x.Id));
    }

    [Fact]
    public async Task Workflow_pg_schema_model_grants_and_guarded_downgrade_are_current()
    {
        await using var db = _database.CreateContext();
        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        // Verify the applied chain rather than a count frozen before role/policy migrations.
        Assert.Equal(db.Database.GetMigrations().ToArray(), appliedMigrations);
        Assert.Contains("20261019190302_UseStaffAndAdminRoles", appliedMigrations);
        Assert.Contains("20261020111445_AddFactualInvoicePairPolicy", appliedMigrations);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());Assert.True(await TableExistsAsync("reconciliation_draft_requests"));
        Assert.True(await ScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM pg_indexes WHERE indexname='ux_reconciliation_draft_requests_intent' AND indexdef LIKE '%UNIQUE%')"));
        Assert.Equal(2,await ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE conrelid='reconciliation_draft_requests'::regclass AND contype='f' AND confdeltype='r'"));
        Assert.Equal(0,await ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE conrelid='reconciliation_draft_requests'::regclass AND confrelid='documents'::regclass"));
        var seed=await SeedGraphAsync();await WorkflowStoredCase(seed.OrganizationA,seed.ActorA);
        var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync("20261006121845_AddInvoicePurchaseOrderChecks"));Assert.Equal("55000",error.SqlState);Assert.True(await TableExistsAsync("reconciliation_draft_requests"));
    }
}
