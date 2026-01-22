FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar arquivos de projeto Duplicatas
COPY ["Duplicatas/Duplicatas.Worker/Duplicatas.Worker.csproj", "Duplicatas/Duplicatas.Worker/"]
COPY ["Duplicatas/Duplicatas.Application/Duplicatas.Application.csproj", "Duplicatas/Duplicatas.Application/"]
COPY ["Duplicatas/Duplicatas.Domain/Duplicatas.Domain.csproj", "Duplicatas/Duplicatas.Domain/"]
COPY ["Duplicatas/Duplicatas.Infra/Duplicatas.Infra.csproj", "Duplicatas/Duplicatas.Infra/"]

# Copiar arquivos de projeto CustomerPlatform (dependências)
COPY ["CustomerPlatform/src/CustomerPlatform.Application/CustomerPlatform.Application.csproj", "CustomerPlatform/src/CustomerPlatform.Application/"]
COPY ["CustomerPlatform/src/CustomerPlatform.Domain/CustomerPlatform.Domain.csproj", "CustomerPlatform/src/CustomerPlatform.Domain/"]
COPY ["CustomerPlatform/src/CustomerPlatform.Infrastructure/CustomerPlatform.Infrastructure.csproj", "CustomerPlatform/src/CustomerPlatform.Infrastructure/"]

# Restaurar dependências
RUN dotnet restore "Duplicatas/Duplicatas.Worker/Duplicatas.Worker.csproj"

# Copiar todo o código fonte Duplicatas
COPY Duplicatas/Duplicatas.Worker/ Duplicatas/Duplicatas.Worker/
COPY Duplicatas/Duplicatas.Application/ Duplicatas/Duplicatas.Application/
COPY Duplicatas/Duplicatas.Domain/ Duplicatas/Duplicatas.Domain/
COPY Duplicatas/Duplicatas.Infra/ Duplicatas/Duplicatas.Infra/

# Copiar código fonte CustomerPlatform
COPY CustomerPlatform/src/CustomerPlatform.Application/ CustomerPlatform/src/CustomerPlatform.Application/
COPY CustomerPlatform/src/CustomerPlatform.Domain/ CustomerPlatform/src/CustomerPlatform.Domain/
COPY CustomerPlatform/src/CustomerPlatform.Infrastructure/ CustomerPlatform/src/CustomerPlatform.Infrastructure/

# Build
WORKDIR "/src/Duplicatas/Duplicatas.Worker"
RUN dotnet build "Duplicatas.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Duplicatas.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Duplicatas.Worker.dll"]
