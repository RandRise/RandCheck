using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCitedChunkEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This SQL-owned pgvector table deliberately has no EF entity/type mapping.
            // Parameters are real[] explicitly cast to vector by CitedEmbeddingStore.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS vector;
                CREATE UNIQUE INDEX ux_document_chunks_embedding_source
                  ON document_chunks (organization_id, index_id, id, content_sha256);
                CREATE TABLE cited_chunk_embeddings (
                    organization_id uuid NOT NULL,
                    index_id uuid NOT NULL,
                    chunk_id uuid NOT NULL,
                    input_sha256 varchar(64) NOT NULL,
                    provider varchar(100) NOT NULL,
                    model varchar(100) NOT NULL,
                    model_version varchar(100) NOT NULL,
                    dimension integer NOT NULL CHECK (dimension=1536),
                    distance varchar(16) NOT NULL CHECK (distance='cosine'),
                    reservation_id uuid NOT NULL,
                    outcome_artifact_id uuid NOT NULL REFERENCES cited_generation_artifacts(id) ON DELETE RESTRICT,
                    response_sha256 varchar(64) NOT NULL CHECK (response_sha256 ~ '^[0-9a-f]{64}$'),
                    embedding vector(1536) NOT NULL CHECK (vector_norm(embedding)>0),
                    created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                    CONSTRAINT pk_cited_chunk_embeddings PRIMARY KEY
                      (organization_id, chunk_id, provider, model, model_version, dimension, distance),
                    CONSTRAINT fk_embedding_canonical_chunk FOREIGN KEY (organization_id,index_id,chunk_id,input_sha256)
                      REFERENCES document_chunks(organization_id,index_id,id,content_sha256) ON DELETE RESTRICT,
                    CONSTRAINT fk_embedding_spend FOREIGN KEY (organization_id,reservation_id)
                      REFERENCES cited_spend_reservations(organization_id,id) ON DELETE RESTRICT,
                    CONSTRAINT ck_embedding_model CHECK
                      (btrim(provider)<>'' AND btrim(model)<>'' AND btrim(model_version)<>'')
                );
                CREATE INDEX ix_cited_chunk_embeddings_corpus ON cited_chunk_embeddings
                  (organization_id,index_id,provider,model,model_version,dimension,distance);
                CREATE FUNCTION guard_cited_chunk_embedding() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'Stored embeddings are immutable' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM cited_spend_reservations s
                        JOIN cited_generation_artifacts a ON a.organization_id=s.organization_id
                          AND a.reservation_id=s.id AND a.actor_id=s.actor_id AND a.kind='Outcome'
                        WHERE s.organization_id=NEW.organization_id AND s.id=NEW.reservation_id
                          AND s.workload='Embedding' AND s.state='Settled' AND s.maximum_output_tokens=0
                          AND s.provider=NEW.provider AND s.model=NEW.model AND s.model_version=NEW.model_version
                          AND a.id=NEW.outcome_artifact_id
                    ) THEN
                        RAISE EXCEPTION 'Embedding requires its settled outcome' USING ERRCODE='23514';
                    END IF;
                    PERFORM id FROM cited_document_indexes WHERE organization_id=NEW.organization_id
                      AND id=NEW.index_id AND status='Ready' FOR SHARE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Embedding source is ineligible' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER cited_chunk_embedding_guard BEFORE INSERT OR UPDATE OR DELETE
                  ON cited_chunk_embeddings FOR EACH ROW EXECUTE FUNCTION guard_cited_chunk_embedding();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM cited_chunk_embeddings) THEN
                        RAISE EXCEPTION 'Stored embeddings prevent downgrade' USING ERRCODE='23514';
                    END IF;
                END $$;
                DROP TABLE cited_chunk_embeddings;
                DROP FUNCTION guard_cited_chunk_embedding();
                DROP INDEX ux_document_chunks_embedding_source;
                -- The extension may be shared with other tables; leave it installed.
                """);
        }
    }
}
