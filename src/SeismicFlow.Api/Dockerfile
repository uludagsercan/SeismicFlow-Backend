FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["SeismicFlow.Api/SeismicFlow.Api.csproj", "SeismicFlow.Api/"]
COPY ["SeismicFlow.Application/SeismicFlow.Application.csproj", "SeismicFlow.Application/"]
COPY ["Domain/SeismicFlow.Domain.csproj", "Domain/"]
COPY ["SeismicFlow.Shared/SeismicFlow.Shared.csproj", "SeismicFlow.Shared/"]
COPY ["Infrastructure/SeismicFlow.Infrastructure.Persistence.csproj", "Infrastructure/"]
COPY ["SeismicFlow.Infrastructure.External/SeismicFlow.Infrastructure.External.csproj", "SeismicFlow.Infrastructure.External/"]

RUN dotnet restore "SeismicFlow.Api/SeismicFlow.Api.csproj"

COPY . .

RUN dotnet build "SeismicFlow.Api/SeismicFlow.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SeismicFlow.Api/SeismicFlow.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SeismicFlow.Api.dll"]