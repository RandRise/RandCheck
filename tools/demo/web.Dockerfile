FROM node:24.21.0-alpine@sha256:be80f76cf40ec8e42b9bec49f60a55e0660f30af58d3e5a25530785b30ea67e2
WORKDIR /app
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci
COPY apps/web .
COPY apps/web/next.config.mjs ./next.base.mjs
COPY tools/demo/next.config.mjs ./next.config.mjs
ENV NEXT_TELEMETRY_DISABLED=1
# Development identity is intentionally permitted only by the existing dev server.
CMD ["npm", "run", "dev", "--", "--hostname", "0.0.0.0", "--port", "3000"]
