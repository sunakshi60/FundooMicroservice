FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT_PATH
WORKDIR /src

COPY . .
RUN dotnet publish ${PROJECT_PATH} -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .

CMD ["sh", "-c", "dotnet $(ls *.runtimeconfig.json | head -n 1 | sed 's/.runtimeconfig.json/.dll/')"]
