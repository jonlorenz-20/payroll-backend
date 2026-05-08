# Stage 1: Base image na tatakbuhan ng app (Pinalitan ng 10.0)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Stage 2: Build environment (Pinalitan ng 10.0)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["payroll.API.csproj", "./"]
RUN dotnet restore "./payroll.API.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "payroll.API.csproj" -c Release -o /app/build

# Stage 3: Publish ang app
FROM build AS publish
RUN dotnet publish "payroll.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Ilipat ang published files sa Base image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "payroll.API.dll"]