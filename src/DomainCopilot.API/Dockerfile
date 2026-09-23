FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/DomainCopilot.API/DomainCopilot.API.csproj", "src/DomainCopilot.API/"]
COPY ["src/DomainCopilot.Application/DomainCopilot.Application.csproj", "src/DomainCopilot.Application/"]
COPY ["src/DomainCopilot.Domain/DomainCopilot.Domain.csproj", "src/DomainCopilot.Domain/"]
COPY ["src/DomainCopilot.Infrastructure/DomainCopilot.Infrastructure.csproj", "src/DomainCopilot.Infrastructure/"]

RUN dotnet restore "src/DomainCopilot.API/DomainCopilot.API.csproj"

COPY . .
WORKDIR "/src/src/DomainCopilot.API"

RUN dotnet publish "DomainCopilot.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DomainCopilot.API.dll"]
