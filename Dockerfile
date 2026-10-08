FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["Norma Bot/Norma Bot.csproj", "Norma Bot/"]
RUN dotnet restore "Norma Bot/Norma Bot.csproj"

COPY . .

RUN dotnet publish "Norma Bot/Norma Bot.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Norma Bot.dll"]