using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCitedQuestionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_review_actions_organization_id_review_session_id_extraction~",
                table: "review_actions");

            migrationBuilder.DropIndex(
                name: "IX_field_evidence_organization_id_extraction_run_id_page_number",
                table: "field_evidence");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_review_actions_organization_id_review_session_id_extraction~",
                table: "review_actions",
                columns: new[] { "organization_id", "review_session_id", "extraction_run_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_field_evidence_organization_id_extraction_run_id_page_numbe~",
                table: "field_evidence",
                columns: new[] { "organization_id", "extraction_run_id", "page_number", "id" });

            migrationBuilder.CreateTable(
                name: "cited_document_indexes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    document_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    profile_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    profile_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_document_indexes", x => x.id);
                    table.UniqueConstraint("AK_cited_document_indexes_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_cited_document_indexes_organization_id_id_review_session_id~", x => new { x.organization_id, x.id, x.review_session_id, x.extraction_run_id });
                    table.CheckConstraint("ck_cited_indexes_sha", "document_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_cited_indexes_status", "status IN ('Building','Ready','Superseded','Quarantined')");
                    table.ForeignKey(
                        name: "FK_cited_document_indexes_review_sessions_organization_id_docu~",
                        columns: x => new { x.organization_id, x.document_id, x.review_session_id },
                        principalTable: "review_sessions",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cited_document_indexes_review_sessions_organization_id_revi~",
                        columns: x => new { x.organization_id, x.review_session_id, x.extraction_run_id },
                        principalTable: "review_sessions",
                        principalColumns: new[] { "organization_id", "id", "extraction_run_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cited_retrieval_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    request_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retrieval_duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_retrieval_runs", x => x.id);
                    table.UniqueConstraint("AK_cited_retrieval_runs_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_cited_runs_completion", "(status = 'Running' AND completed_at_utc IS NULL AND failure_code IS NULL) OR (status = 'Completed' AND completed_at_utc IS NOT NULL AND failure_code IS NULL) OR (status = 'Failed' AND completed_at_utc IS NOT NULL AND failure_code IS NOT NULL)");
                    table.CheckConstraint("ck_cited_runs_duration", "(retrieval_duration_ms IS NULL OR retrieval_duration_ms >= 0) AND (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc)");
                    table.CheckConstraint("ck_cited_runs_policy", "jsonb_typeof(policy_json) = 'object'");
                    table.CheckConstraint("ck_cited_runs_question", "btrim(question) <> ''");
                    table.CheckConstraint("ck_cited_runs_status", "status IN ('Running','Completed','Failed')");
                    table.ForeignKey(
                        name: "FK_cited_retrieval_runs_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    review_action_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.id);
                    table.UniqueConstraint("AK_document_chunks_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_document_chunks_organization_id_id_extraction_run_id", x => new { x.organization_id, x.id, x.extraction_run_id });
                    table.UniqueConstraint("AK_document_chunks_organization_id_index_id_id", x => new { x.organization_id, x.index_id, x.id });
                    table.CheckConstraint("ck_document_chunks_content", "octet_length(content) BETWEEN 1 AND 65536 AND btrim(content) <> ''");
                    table.CheckConstraint("ck_document_chunks_hash", "content_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_document_chunks_kind", "(kind = 'PageText' AND review_action_id IS NULL) OR (kind = 'ReviewedFact' AND review_action_id IS NOT NULL)");
                    table.CheckConstraint("ck_document_chunks_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "FK_document_chunks_cited_document_indexes_organization_id_inde~",
                        columns: x => new { x.organization_id, x.index_id, x.review_session_id, x.extraction_run_id },
                        principalTable: "cited_document_indexes",
                        principalColumns: new[] { "organization_id", "id", "review_session_id", "extraction_run_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_chunks_review_actions_organization_id_review_sessi~",
                        columns: x => new { x.organization_id, x.review_session_id, x.extraction_run_id, x.review_action_id },
                        principalTable: "review_actions",
                        principalColumns: new[] { "organization_id", "review_session_id", "extraction_run_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cited_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retrieval_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generation_duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    estimated_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    cost_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_answers", x => x.id);
                    table.UniqueConstraint("AK_cited_answers_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_cited_answers_organization_id_id_retrieval_run_id", x => new { x.organization_id, x.id, x.retrieval_run_id });
                    table.CheckConstraint("ck_cited_answers_cost", "(estimated_cost IS NULL AND cost_currency IS NULL) OR (estimated_cost IS NOT NULL AND estimated_cost >= 0 AND cost_currency IS NOT NULL AND cost_currency ~ '^[A-Z]{3}$')");
                    table.CheckConstraint("ck_cited_answers_duration", "generation_duration_ms >= 0");
                    table.CheckConstraint("ck_cited_answers_reason", "(status = 'Answered' AND reason_code IS NULL) OR (status IN ('PartiallyAnswered','InsufficientEvidence') AND reason_code IS NOT NULL AND reason_code IN ('no_relevant_evidence','incomplete_evidence','conflicting_evidence','unsupported_scope','unsafe_source'))");
                    table.CheckConstraint("ck_cited_answers_status", "status IN ('Answered','PartiallyAnswered','InsufficientEvidence')");
                    table.ForeignKey(
                        name: "FK_cited_answers_cited_retrieval_runs_organization_id_retrieva~",
                        columns: x => new { x.organization_id, x.retrieval_run_id },
                        principalTable: "cited_retrieval_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retrieval_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_sources", x => x.id);
                    table.UniqueConstraint("AK_retrieval_sources_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_retrieval_sources_organization_id_retrieval_run_id_index_id", x => new { x.organization_id, x.retrieval_run_id, x.index_id });
                    table.ForeignKey(
                        name: "FK_retrieval_sources_cited_document_indexes_organization_id_in~",
                        columns: x => new { x.organization_id, x.index_id },
                        principalTable: "cited_document_indexes",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retrieval_sources_cited_retrieval_runs_organization_id_retr~",
                        columns: x => new { x.organization_id, x.retrieval_run_id },
                        principalTable: "cited_retrieval_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chunk_evidence_spans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    byte_start = table.Column<int>(type: "integer", nullable: false),
                    byte_end = table.Column<int>(type: "integer", nullable: false),
                    quote = table.Column<string>(type: "text", nullable: false),
                    field_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bounding_polygon = table.Column<string>(type: "jsonb", nullable: true),
                    coordinate_unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_evidence_spans", x => x.id);
                    table.UniqueConstraint("AK_chunk_evidence_spans_organization_id_chunk_id_id", x => new { x.organization_id, x.chunk_id, x.id });
                    table.UniqueConstraint("AK_chunk_evidence_spans_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_chunk_spans_polygon", "(bounding_polygon IS NULL AND coordinate_unit IS NULL) OR (bounding_polygon IS NOT NULL AND coordinate_unit IS NOT NULL AND coordinate_unit = 'normalized')");
                    table.CheckConstraint("ck_chunk_spans_range", "page_number > 0 AND byte_start >= 0 AND byte_end > byte_start AND octet_length(quote) = byte_end - byte_start");
                    table.ForeignKey(
                        name: "FK_chunk_evidence_spans_document_chunks_organization_id_chunk_~",
                        columns: x => new { x.organization_id, x.chunk_id, x.extraction_run_id },
                        principalTable: "document_chunks",
                        principalColumns: new[] { "organization_id", "id", "extraction_run_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chunk_evidence_spans_extraction_pages_organization_id_extra~",
                        columns: x => new { x.organization_id, x.extraction_run_id, x.page_number },
                        principalTable: "extraction_pages",
                        principalColumns: new[] { "organization_id", "extraction_run_id", "page_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chunk_evidence_spans_field_evidence_organization_id_extract~",
                        columns: x => new { x.organization_id, x.extraction_run_id, x.page_number, x.field_evidence_id },
                        principalTable: "field_evidence",
                        principalColumns: new[] { "organization_id", "extraction_run_id", "page_number", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "answer_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retrieval_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answer_claims", x => x.id);
                    table.UniqueConstraint("AK_answer_claims_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_answer_claims_organization_id_retrieval_run_id_id", x => new { x.organization_id, x.retrieval_run_id, x.id });
                    table.CheckConstraint("ck_answer_claims_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_answer_claims_text", "btrim(text) <> ''");
                    table.ForeignKey(
                        name: "FK_answer_claims_cited_answers_organization_id_answer_id_retri~",
                        columns: x => new { x.organization_id, x.answer_id, x.retrieval_run_id },
                        principalTable: "cited_answers",
                        principalColumns: new[] { "organization_id", "id", "retrieval_run_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_hits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retrieval_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    lexical_score = table.Column<double>(type: "double precision", nullable: true),
                    vector_score = table.Column<double>(type: "double precision", nullable: true),
                    fused_score = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_hits", x => x.id);
                    table.UniqueConstraint("AK_retrieval_hits_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_retrieval_hits_organization_id_retrieval_run_id_id_chunk_id", x => new { x.organization_id, x.retrieval_run_id, x.id, x.chunk_id });
                    table.CheckConstraint("ck_hits_fused_score", "fused_score IS NULL OR (fused_score > '-Infinity'::float8 AND fused_score < 'Infinity'::float8)");
                    table.CheckConstraint("ck_hits_lexical_score", "lexical_score IS NULL OR (lexical_score > '-Infinity'::float8 AND lexical_score < 'Infinity'::float8)");
                    table.CheckConstraint("ck_hits_vector_score", "vector_score IS NULL OR (vector_score > '-Infinity'::float8 AND vector_score < 'Infinity'::float8)");
                    table.CheckConstraint("ck_retrieval_hits_rank", "rank BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_retrieval_hits_cited_retrieval_runs_organization_id_retriev~",
                        columns: x => new { x.organization_id, x.retrieval_run_id },
                        principalTable: "cited_retrieval_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retrieval_hits_document_chunks_organization_id_index_id_chu~",
                        columns: x => new { x.organization_id, x.index_id, x.chunk_id },
                        principalTable: "document_chunks",
                        principalColumns: new[] { "organization_id", "index_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retrieval_hits_retrieval_sources_organization_id_retrieval_~",
                        columns: x => new { x.organization_id, x.retrieval_run_id, x.index_id },
                        principalTable: "retrieval_sources",
                        principalColumns: new[] { "organization_id", "retrieval_run_id", "index_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "answer_citations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retrieval_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    span_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answer_citations", x => x.id);
                    table.UniqueConstraint("AK_answer_citations_organization_id_id", x => new { x.organization_id, x.id });
                    table.ForeignKey(
                        name: "FK_answer_citations_answer_claims_organization_id_retrieval_ru~",
                        columns: x => new { x.organization_id, x.retrieval_run_id, x.claim_id },
                        principalTable: "answer_claims",
                        principalColumns: new[] { "organization_id", "retrieval_run_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_answer_citations_chunk_evidence_spans_organization_id_chunk~",
                        columns: x => new { x.organization_id, x.chunk_id, x.span_id },
                        principalTable: "chunk_evidence_spans",
                        principalColumns: new[] { "organization_id", "chunk_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_answer_citations_retrieval_hits_organization_id_retrieval_r~",
                        columns: x => new { x.organization_id, x.retrieval_run_id, x.hit_id, x.chunk_id },
                        principalTable: "retrieval_hits",
                        principalColumns: new[] { "organization_id", "retrieval_run_id", "id", "chunk_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_answer_citations_organization_id_chunk_id_span_id",
                table: "answer_citations",
                columns: new[] { "organization_id", "chunk_id", "span_id" });

            migrationBuilder.CreateIndex(
                name: "IX_answer_citations_organization_id_retrieval_run_id_claim_id",
                table: "answer_citations",
                columns: new[] { "organization_id", "retrieval_run_id", "claim_id" });

            migrationBuilder.CreateIndex(
                name: "IX_answer_citations_organization_id_retrieval_run_id_hit_id_ch~",
                table: "answer_citations",
                columns: new[] { "organization_id", "retrieval_run_id", "hit_id", "chunk_id" });

            migrationBuilder.CreateIndex(
                name: "ux_answer_citations_review",
                table: "answer_citations",
                columns: new[] { "organization_id", "claim_id", "hit_id" },
                unique: true,
                filter: "span_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_answer_citations_span",
                table: "answer_citations",
                columns: new[] { "organization_id", "claim_id", "hit_id", "span_id" },
                unique: true,
                filter: "span_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_answer_claims_organization_id_answer_id_retrieval_run_id",
                table: "answer_claims",
                columns: new[] { "organization_id", "answer_id", "retrieval_run_id" });

            migrationBuilder.CreateIndex(
                name: "ux_answer_claims_ordinal",
                table: "answer_claims",
                columns: new[] { "organization_id", "answer_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chunk_evidence_spans_organization_id_chunk_id_extraction_ru~",
                table: "chunk_evidence_spans",
                columns: new[] { "organization_id", "chunk_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_evidence_spans_organization_id_extraction_run_id_page~",
                table: "chunk_evidence_spans",
                columns: new[] { "organization_id", "extraction_run_id", "page_number", "field_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cited_answers_organization_id_retrieval_run_id",
                table: "cited_answers",
                columns: new[] { "organization_id", "retrieval_run_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cited_document_indexes_organization_id_document_id_review_s~",
                table: "cited_document_indexes",
                columns: new[] { "organization_id", "document_id", "review_session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cited_document_indexes_organization_id_review_session_id_ex~",
                table: "cited_document_indexes",
                columns: new[] { "organization_id", "review_session_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cited_indexes_eligible",
                table: "cited_document_indexes",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_cited_indexes_review_profile",
                table: "cited_document_indexes",
                columns: new[] { "organization_id", "review_session_id", "profile_name", "profile_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cited_runs_history",
                table: "cited_retrieval_runs",
                columns: new[] { "organization_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_cited_runs_request",
                table: "cited_retrieval_runs",
                columns: new[] { "organization_id", "actor_id", "request_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_organization_id_index_id_review_session_id_~",
                table: "document_chunks",
                columns: new[] { "organization_id", "index_id", "review_session_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_organization_id_review_session_id_extractio~",
                table: "document_chunks",
                columns: new[] { "organization_id", "review_session_id", "extraction_run_id", "review_action_id" });

            migrationBuilder.CreateIndex(
                name: "ux_document_chunks_index_ordinal",
                table: "document_chunks",
                columns: new[] { "organization_id", "index_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_hits_organization_id_index_id_chunk_id",
                table: "retrieval_hits",
                columns: new[] { "organization_id", "index_id", "chunk_id" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_hits_organization_id_retrieval_run_id_index_id",
                table: "retrieval_hits",
                columns: new[] { "organization_id", "retrieval_run_id", "index_id" });

            migrationBuilder.CreateIndex(
                name: "ux_retrieval_hits_chunk",
                table: "retrieval_hits",
                columns: new[] { "organization_id", "retrieval_run_id", "chunk_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_retrieval_hits_rank",
                table: "retrieval_hits",
                columns: new[] { "organization_id", "retrieval_run_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_sources_organization_id_index_id",
                table: "retrieval_sources",
                columns: new[] { "organization_id", "index_id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION verify_cited_page_quote() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE page_text text;
                BEGIN
                    SELECT canonical_text INTO page_text FROM extraction_pages
                    WHERE organization_id = NEW.organization_id
                      AND extraction_run_id = NEW.extraction_run_id AND page_number = NEW.page_number;
                    -- Missing pages are rejected by the tenant-aware foreign key.
                    IF page_text IS NOT NULL AND convert_to(NEW.quote, 'UTF8') IS DISTINCT FROM
                        substring(convert_to(page_text, 'UTF8') FROM NEW.byte_start + 1 FOR NEW.byte_end - NEW.byte_start)
                    THEN
                        RAISE EXCEPTION 'Citation quote does not match the canonical page span'
                            USING ERRCODE = '23514', CONSTRAINT = 'ck_cited_span_exact_quote';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER verify_cited_page_quote BEFORE INSERT OR UPDATE ON chunk_evidence_spans
                    FOR EACH ROW EXECUTE FUNCTION verify_cited_page_quote();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER verify_cited_page_quote ON chunk_evidence_spans; DROP FUNCTION verify_cited_page_quote();");
            migrationBuilder.DropTable(
                name: "answer_citations");

            migrationBuilder.DropTable(
                name: "answer_claims");

            migrationBuilder.DropTable(
                name: "chunk_evidence_spans");

            migrationBuilder.DropTable(
                name: "retrieval_hits");

            migrationBuilder.DropTable(
                name: "cited_answers");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "retrieval_sources");

            migrationBuilder.DropTable(
                name: "cited_document_indexes");

            migrationBuilder.DropTable(
                name: "cited_retrieval_runs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_review_actions_organization_id_review_session_id_extraction~",
                table: "review_actions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_field_evidence_organization_id_extraction_run_id_page_numbe~",
                table: "field_evidence");

            migrationBuilder.CreateIndex(
                name: "IX_review_actions_organization_id_review_session_id_extraction~",
                table: "review_actions",
                columns: new[] { "organization_id", "review_session_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_field_evidence_organization_id_extraction_run_id_page_number",
                table: "field_evidence",
                columns: new[] { "organization_id", "extraction_run_id", "page_number" });
        }
    }
}
