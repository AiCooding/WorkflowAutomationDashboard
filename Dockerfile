# Stage 1: Build Angular
FROM node:22-alpine AS angular-build
WORKDIR /app/ui
COPY src/workflow-dashboard-ui/package*.json ./
RUN npm ci
COPY src/workflow-dashboard-ui/ ./
RUN npm run build -- --configuration production

# Stage 2: Build .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /app
# Copy project files first for restore layer caching (skip the .slnx — it
# references the Angular .esproj which would pull a JS SDK we don't need here)
COPY src/WorkflowDashboard.Api/*.csproj ./src/WorkflowDashboard.Api/
COPY src/WorkflowDashboard.Shared/*.csproj ./src/WorkflowDashboard.Shared/
RUN dotnet restore src/WorkflowDashboard.Api/WorkflowDashboard.Api.csproj
COPY src/WorkflowDashboard.Api/ ./src/WorkflowDashboard.Api/
COPY src/WorkflowDashboard.Shared/ ./src/WorkflowDashboard.Shared/
RUN dotnet publish src/WorkflowDashboard.Api/WorkflowDashboard.Api.csproj -c Release -o /publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=dotnet-build /publish ./
COPY --from=angular-build /app/ui/dist/workflow-dashboard-ui/browser ./wwwroot
EXPOSE 5080
ENV ASPNETCORE_URLS=http://+:5080
ENV ConnectionStrings__WorkflowDb="Data Source=/data/workflow.db"
ENV Specs__RootDir=/app/docs/features
VOLUME /data
ENTRYPOINT ["dotnet", "WorkflowDashboard.Api.dll"]
