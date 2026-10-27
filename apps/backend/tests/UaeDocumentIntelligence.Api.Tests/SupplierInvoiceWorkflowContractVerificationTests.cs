using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class ReconciliationApiContractTests
{
    private sealed class WorkflowApiRequests : IReconciliationDraftRequestStore
    {
        public List<ReconciliationDraftRequest> Items {get;} = [];
        public Task<ReconciliationDraftRequest?> FindAsync(Guid org, Guid actor, string key, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(x=>x.OrganizationId==org && x.ActorId==actor && x.RequestKey==key));
        public Task AddAsync(ReconciliationDraftRequest x, CancellationToken ct) {Items.Add(x); return Task.CompletedTask;}
    }
    private sealed class WorkflowApiDocuments(params Document[] values) : IDocumentRepository
    {
        public Task<Document?> GetAsync(Guid org, Guid id, CancellationToken ct)=>Task.FromResult(values.SingleOrDefault(x=>x.OrganizationId==org && x.Id==id));
        public Task<Document?> GetForReviewAsync(Guid org, Guid id, CancellationToken ct)=>GetAsync(org,id,ct);
        public Task AddAsync(Document d,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<Document>> ListAsync(Guid org,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Document>>(values.Where(x=>x.OrganizationId==org).ToList());
        public Task<int> NextRunNumberAsync(Guid org,Guid id,CancellationToken ct)=>Task.FromResult(1);
        public Task<DocumentMetrics> GetMetricsAsync(Guid org,CancellationToken ct)=>Task.FromResult(new DocumentMetrics(0,0,0,null));
        public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
        public Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken ct)=>Task.FromResult(ReviewSaveOutcome.Saved);
    }
    private WebApplicationFactory<Program> WorkflowApiFactory(FakeRepository repository, WorkflowApiRequests requests, params Document[] docs) =>
        WithRepository(repository).WithWebHostBuilder(builder=>builder.ConfigureServices(services=>{
            services.RemoveAll<IReconciliationDraftRequestStore>(); services.AddSingleton<IReconciliationDraftRequestStore>(requests);
            services.RemoveAll<IDocumentRepository>(); services.AddSingleton<IDocumentRepository>(new WorkflowApiDocuments(docs));
        }));
    private static Document WorkflowApiDocument(Guid? org=null)=>new(Guid.NewGuid(),org??OrganizationId,ActorId,"Queued invoice.pdf","test.pdf","application/pdf",100,new string('a',64),Now);
    private static ReconciliationCase WorkflowApiCase()=>new(Guid.NewGuid(),OrganizationId,ActorId,"Saved pair",Now,workflowKind:ReconciliationWorkflowKind.InvoicePurchaseOrder);
    private static async Task<HttpResponseMessage> DraftPut(HttpClient client,Guid id,string role,Guid? doc,string key,string? etag){
        using var request=new HttpRequestMessage(HttpMethod.Put,$"/api/reconciliation-cases/{id}/draft-documents/{role}"){Content=JsonContent.Create(new{documentId=doc,requestKey=key})};
        if(etag is not null)request.Headers.TryAddWithoutValidation("If-Match",etag);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Workflow_api_create_defaults_legacy_but_explicit_pair_returns_draft_contract_and_strong_etag()
    {
        using var factory=WorkflowApiFactory(new FakeRepository(),new WorkflowApiRequests());using var client=factory.CreateClient();
        using var old=await client.PostAsJsonAsync("/api/reconciliation-cases",new{name="Legacy"});
        var oldBody=await old.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("Legacy",oldBody.GetProperty("workflowKind").GetString());Assert.Empty(oldBody.GetProperty("draftDocuments").EnumerateArray());
        using var pair=await client.PostAsJsonAsync("/api/reconciliation-cases",new{name="Pair",workflowKind="InvoicePurchaseOrder",requestKey="pair-create"});
        Assert.Equal(HttpStatusCode.Created,pair.StatusCode);var body=await pair.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvoicePurchaseOrder",body.GetProperty("workflowKind").GetString());Assert.Equal($"\"{body.GetProperty("concurrencyToken").GetGuid():D}\"",pair.Headers.ETag?.Tag);
        Assert.Empty(body.GetProperty("documents").EnumerateArray());Assert.Empty(body.GetProperty("runs").EnumerateArray());
    }
    [Theory]
    [InlineData(null,428)] [InlineData("W/\"00000000-0000-4000-8000-000000000001\"",400)] [InlineData("*",400)]
    public async Task Workflow_api_draft_requires_strong_specific_etag(string? header,int status)
    {
        var item=WorkflowApiCase();var doc=WorkflowApiDocument();var receipts=new WorkflowApiRequests();
        using var factory=WorkflowApiFactory(new FakeRepository(item),receipts,doc);using var client=factory.CreateClient();
        using var response=await DraftPut(client,item.Id,"Invoice",doc.Id,"strong",header);
        Assert.Equal(status,(int)response.StatusCode);Assert.Empty(receipts.Items);Assert.Null(item.DraftInvoiceDocumentId);
    }
    [Fact]
    public async Task Workflow_api_draft_replay_returns_current_etag_and_changed_payload_conflicts()
    {
        var item=WorkflowApiCase();var doc=WorkflowApiDocument();var receipts=new WorkflowApiRequests();var original=$"\"{item.ConcurrencyToken:D}\"";
        using var factory=WorkflowApiFactory(new FakeRepository(item),receipts,doc);using var client=factory.CreateClient();
        using var first=await DraftPut(client,item.Id,"Invoice",doc.Id,"same",original);Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        using var replay=await DraftPut(client,item.Id,"Invoice",doc.Id,"same",original);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.Equal(first.Headers.ETag?.Tag,replay.Headers.ETag?.Tag);
        var view=await replay.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(doc.Id,Assert.Single(view.GetProperty("draftDocuments").EnumerateArray()).GetProperty("documentId").GetGuid());
        Assert.Single(receipts.Items);using var changed=await DraftPut(client,item.Id,"Invoice",null,"same",original);
        Assert.Equal(HttpStatusCode.Conflict,changed.StatusCode);Assert.Equal("request_key_reused",(await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
    [Fact]
    public async Task Workflow_api_foreign_source_and_stale_new_intent_are_safe_refusals()
    {
        var item=WorkflowApiCase();var foreign=WorkflowApiDocument(Guid.NewGuid());var own=WorkflowApiDocument();var receipts=new WorkflowApiRequests();
        using var factory=WorkflowApiFactory(new FakeRepository(item),receipts,foreign,own);using var client=factory.CreateClient();
        using var foreignResponse=await DraftPut(client,item.Id,"Invoice",foreign.Id,"foreign",$"\"{item.ConcurrencyToken:D}\"");Assert.Equal(HttpStatusCode.NotFound,foreignResponse.StatusCode);
        using var stale=await DraftPut(client,item.Id,"Invoice",own.Id,"stale",$"\"{Guid.NewGuid():D}\"");Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
        Assert.Empty(receipts.Items);Assert.Null(item.DraftInvoiceDocumentId);
    }
    [Theory]
    [InlineData("invoice")] [InlineData("Quotation")] [InlineData("0")]
    public async Task Workflow_api_role_names_are_bounded(string role)
    {
        var item=WorkflowApiCase();var receipts=new WorkflowApiRequests();using var factory=WorkflowApiFactory(new FakeRepository(item),receipts);using var client=factory.CreateClient();
        using var response=await DraftPut(client,item.Id,role,null,"role",$"\"{item.ConcurrencyToken:D}\"");Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Empty(receipts.Items);
    }
    [Theory]
    [InlineData("Unknown")] [InlineData("0")] [InlineData("invoicepurchaseorder")]
    public async Task Workflow_api_invalid_workflow_creation_or_list_rejected(string workflow)
    {
        using var factory=WorkflowApiFactory(new FakeRepository(),new WorkflowApiRequests());using var client=factory.CreateClient();
        using var create=await client.PostAsJsonAsync("/api/reconciliation-cases",new{name="Invalid",workflowKind=workflow});Assert.Equal(HttpStatusCode.BadRequest,create.StatusCode);
        using var list=await client.GetAsync($"/api/reconciliation-cases?workflowKind={workflow}");Assert.Equal(HttpStatusCode.BadRequest,list.StatusCode);
    }
}

public sealed partial class EnterpriseSecurityTests
{
    private sealed class WorkflowSecurityRepository(ReconciliationCase item):IReconciliationRepository
    {
        public int Saves {get;private set;}
        public Task<ReconciliationCase?> GetAsync(Guid org,Guid id,CancellationToken ct)=>Task.FromResult(org==item.OrganizationId&&id==item.Id?item:null);
        public Task<ReconciliationCase?> GetByCreationRequestAsync(Guid org,Guid actor,string key,CancellationToken ct)=>Task.FromResult<ReconciliationCase?>(null);
        public Task AddAsync(ReconciliationCase value,CancellationToken ct)=>throw new NotSupportedException();
        public Task<ReconciliationSaveOutcome> SaveChangesAsync(CancellationToken ct){Saves++;return Task.FromResult(ReconciliationSaveOutcome.Saved);}
        public Task<IReadOnlyList<ReconciliationCaseSummary>> ListAsync(Guid org,int offset,int limit,CancellationToken ct,ReconciliationCaseFilter? filter=null)=>Task.FromResult<IReadOnlyList<ReconciliationCaseSummary>>([]);
    }
    private sealed class WorkflowSecurityReceipts:IReconciliationDraftRequestStore
    {
        public int Lookups{get;private set;} public ReconciliationDraftRequest? Receipt{get;private set;}
        public Task<ReconciliationDraftRequest?> FindAsync(Guid org,Guid actor,string key,CancellationToken ct){Lookups++;return Task.FromResult(Receipt is not null && Receipt.OrganizationId==org&&Receipt.ActorId==actor&&Receipt.RequestKey==key?Receipt:null);}
        public Task AddAsync(ReconciliationDraftRequest receipt,CancellationToken ct){Receipt=receipt;return Task.CompletedTask;}
    }
    [Theory]
    [InlineData(OrganizationAccessRole.Staff,true)]
    [InlineData(OrganizationAccessRole.Admin,true)]
    [InlineData((OrganizationAccessRole)0,false)]
    [InlineData((OrganizationAccessRole)999,false)]
    public async Task Workflow_api_current_role_controls_draft_write(OrganizationAccessRole role,bool allowed)
    {
        var actors=new ActorStore();actors.Actor=actors.Actor! with{Role=role};
        var item=new ReconciliationCase(Guid.NewGuid(),actors.Actor.OrganizationId,actors.Actor.ActorId,"Private pair",DateTimeOffset.UtcNow,workflowKind:ReconciliationWorkflowKind.InvoicePurchaseOrder);
        var repository=new WorkflowSecurityRepository(item);var receipts=new WorkflowSecurityReceipts();
        using var factory=Host(actors).WithWebHostBuilder(builder=>builder.ConfigureServices(services=>{services.Replace(ServiceDescriptor.Singleton<IReconciliationRepository>(repository));services.Replace(ServiceDescriptor.Singleton<IReconciliationDraftRequestStore>(receipts));}));
        using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",Token(role:"DocumentIntelligence.Operator"));
        using var request=new HttpRequestMessage(HttpMethod.Put,$"/api/reconciliation-cases/{item.Id}/draft-documents/Invoice"){Content=JsonContent.Create(new{documentId=(Guid?)null,requestKey="role-intent"})};
        request.Headers.TryAddWithoutValidation("If-Match",$"\"{item.ConcurrencyToken:D}\"");using var result=await client.SendAsync(request);
        Assert.Equal(allowed?HttpStatusCode.OK:HttpStatusCode.Forbidden,result.StatusCode);Assert.Equal(allowed?1:0,repository.Saves);Assert.Equal(allowed?1:0,receipts.Lookups);
    }
    [Fact]
    public async Task Workflow_api_exact_key_replay_cannot_bypass_revocation_or_downgrade()
    {
        var actors=new ActorStore();actors.Actor=actors.Actor! with{Role=OrganizationAccessRole.Staff};
        var item=new ReconciliationCase(Guid.NewGuid(),actors.Actor.OrganizationId,actors.Actor.ActorId,"Private pair",DateTimeOffset.UtcNow,workflowKind:ReconciliationWorkflowKind.InvoicePurchaseOrder);
        var original=item.ConcurrencyToken;var repository=new WorkflowSecurityRepository(item);var receipts=new WorkflowSecurityReceipts();
        using var factory=Host(actors).WithWebHostBuilder(builder=>builder.ConfigureServices(services=>{services.Replace(ServiceDescriptor.Singleton<IReconciliationRepository>(repository));services.Replace(ServiceDescriptor.Singleton<IReconciliationDraftRequestStore>(receipts));}));
        using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",Token(role:"DocumentIntelligence.Operator"));
        async Task<HttpResponseMessage> Save(){using var request=new HttpRequestMessage(HttpMethod.Put,$"/api/reconciliation-cases/{item.Id}/draft-documents/Invoice"){Content=JsonContent.Create(new{documentId=(Guid?)null,requestKey="authorized"})};request.Headers.TryAddWithoutValidation("If-Match",$"\"{original:D}\"");return await client.SendAsync(request);}
        using var first=await Save();Assert.Equal(HttpStatusCode.OK,first.StatusCode);using var replay=await Save();Assert.Equal(HttpStatusCode.OK,replay.StatusCode);var lookups=receipts.Lookups;
        actors.Actor=actors.Actor with{Role=(OrganizationAccessRole)0};using var viewer=await Save();Assert.Equal(HttpStatusCode.Forbidden,viewer.StatusCode);
        actors.Actor=null;using var revoked=await Save();Assert.Equal(HttpStatusCode.Forbidden,revoked.StatusCode);Assert.Equal(lookups,receipts.Lookups);Assert.Equal(1,repository.Saves);
    }
}
