FROM node:24.21.0-alpine@sha256:be80f76cf40ec8e42b9bec49f60a55e0660f30af58d3e5a25530785b30ea67e2
WORKDIR /app
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci
COPY apps/web .
WORKDIR /docs
COPY docs/package.json docs/package-lock.json ./
RUN npm ci
COPY docs .
COPY tools/ci /checks
ENV NEXT_TELEMETRY_DISABLED=1 AUTH_MODE=Locked
CMD ["sh", "/checks/web.sh"]
