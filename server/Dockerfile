# The CSVM master server as a container. Built from the repository root, because the server
# compiles the game's wire file (CSVM/src/Net/MasterProtocol.cs) as well as its own:
#
#   docker build -f server/Dockerfile -t csvm-master .
#
# server/docker-compose.yml does this for you.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props .editorconfig ./
COPY CSVM/src/Net/MasterProtocol.cs CSVM/src/Net/MasterProtocol.cs
COPY server/MasterServer server/MasterServer
RUN dotnet publish server/MasterServer/MasterServer.csproj -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# The image's own non-root user. Plain HTTP on 8080; TLS is the reverse proxy's.
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "csvm-master.dll"]
