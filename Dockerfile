FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BMoni.sln ./
COPY src/BMoni.Api/BMoni.Api.csproj src/BMoni.Api/
COPY src/BMoni.Application/BMoni.Application.csproj src/BMoni.Application/
COPY src/BMoni.Domain/BMoni.Domain.csproj src/BMoni.Domain/
COPY src/BMoni.Infrastructure/BMoni.Infrastructure.csproj src/BMoni.Infrastructure/
RUN dotnet restore src/BMoni.Api/BMoni.Api.csproj
COPY . .
RUN dotnet publish src/BMoni.Api/BMoni.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "BMoni.Api.dll"]
