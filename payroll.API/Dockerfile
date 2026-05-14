# Stage 1:  image app 
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Stage 2: Build environment (10.0)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["payroll.API.csproj", "./"]
RUN dotnet restore "./payroll.API.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "payroll.API.csproj" -c Release -o /app/build

# Stage 3: Publish app
FROM build AS publish
RUN dotnet publish "payroll.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: published files to image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "payroll.API.dll"]