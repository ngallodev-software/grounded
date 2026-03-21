FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Grounded.Api/Grounded.Api.csproj Grounded.Api/
RUN dotnet restore Grounded.Api/Grounded.Api.csproj
# Copy prompts and eval into the project directory so csproj Content items (relative to .csproj) find them
COPY prompts/ Grounded.Api/prompts/
COPY eval/ Grounded.Api/eval/
COPY Grounded.Api/ Grounded.Api/
RUN dotnet publish Grounded.Api/Grounded.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5252
EXPOSE 5252
ENTRYPOINT ["dotnet", "Grounded.Api.dll"]
