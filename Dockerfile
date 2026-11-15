# syntax=docker/dockerfile:1
# BASELINE: one image with the backend (.NET API), the frontend (SvelteKit) and config/.
#
#   docker build -t baseline .                 # build the app image
#   docker build --target test .               # run backend tests + frontend checks only
#   docker run -p 3000:3000 -p 5074:5074 baseline
#
# Frontend: http://localhost:3000   Backend API: http://localhost:5074

ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=24

# ---------- 1. Backend: restore, build, publish ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS backend-build
WORKDIR /src
# Project files first so the NuGet restore is cached between code changes.
COPY baseline.slnx ./
COPY backend/backend.csproj backend/
COPY backend.Tests/backend.Tests.csproj backend.Tests/
RUN dotnet restore baseline.slnx
COPY backend/ backend/
COPY backend.Tests/ backend.Tests/
COPY config/ config/
RUN dotnet publish backend/backend.csproj -c Release -o /out/backend --no-restore

# ---------- 2. Frontend: install, build ----------
FROM node:${NODE_VERSION}-bookworm-slim AS frontend-build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json frontend/.npmrc ./
RUN npm ci
COPY frontend/ ./
# The browser calls the backend at this address. It is baked in at build time.
ARG API_URL=http://localhost:5074
ENV VITE_API_URL=${API_URL}
RUN npm run build

# Runtime-only node_modules (no dev tools) for the Node server.
FROM node:${NODE_VERSION}-bookworm-slim AS frontend-deps
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json frontend/.npmrc ./
RUN npm ci --omit=dev --ignore-scripts

# ---------- 3. Tests (optional target: docker build --target test .) ----------
FROM frontend-build AS frontend-test
RUN npm run check && npm run lint && touch /frontend-ok

FROM backend-build AS test
# Pulling a file from frontend-test makes this target run the frontend checks too.
COPY --from=frontend-test /frontend-ok /tmp/frontend-ok
RUN dotnet test baseline.slnx -c Release --no-restore

# ---------- 4. Final image: backend + frontend in one container ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
# Node runtime for the SvelteKit server (one binary, no npm needed at runtime).
COPY --from=frontend-build /usr/local/bin/node /usr/local/bin/node

WORKDIR /app
COPY --from=backend-build /out/backend ./backend
COPY config/ ./config
COPY --from=frontend-build /src/frontend/build ./frontend/build
COPY --from=frontend-build /src/frontend/package.json ./frontend/package.json
COPY --from=frontend-deps /src/frontend/node_modules ./frontend/node_modules

# Starts both servers; if either one stops, the container stops.
COPY <<'EOF' /app/start.sh
#!/bin/bash
set -e
cd /app/backend && dotnet backend.dll "$@" &
cd /app/frontend && node build &
wait -n
exit $?
EOF

RUN chmod +x /app/start.sh && mkdir -p /app/data && chown -R $APP_UID /app/data

ENV ASPNETCORE_URLS=http://+:5074 \
    ASPNETCORE_ENVIRONMENT=Production \
    Health__ConfigDirectory=/app/config \
    ConnectionStrings__Default="Data Source=/app/data/baseline.db" \
    Cors__AllowedOrigins__0=http://localhost:3000 \
    PORT=3000 \
    HOST=0.0.0.0

# SQLite demo database lives here; mount a volume to keep it between runs.
VOLUME /app/data
EXPOSE 3000 5074
USER $APP_UID
ENTRYPOINT ["/app/start.sh"]
