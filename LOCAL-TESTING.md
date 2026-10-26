# Local testing

A PostgreSQL database is optional while debugging locally.

If neither `Database:ConnectionString` nor `DATABASE_URL` is configured, the bot automatically uses in-memory storage. Reaction-role mappings, HoYoverse configuration, code snapshots and event-warning state then survive only until the process stops.

Keep the Fluxer credentials in .NET User Secrets, for example:

```bash
dotnet user-secrets set "Fluxer:Token" "YOUR_TOKEN" --project "Norma Bot/Norma Bot.csproj"
dotnet user-secrets set "Fluxer:ApiUrl" "YOUR_API_URL" --project "Norma Bot/Norma Bot.csproj"
```

Useful test flow:

1. Start the bot from Visual Studio.
2. Create a reaction-role message with `!setuproles #channel Message text`.
3. Add a mapping with `!addreactionrole MESSAGE_ID 😀 @Role`.
4. React/unreact and verify the role is added/removed.
5. Configure HoYoverse notifications with `!setuphoyo #channel @GenshinRole @StarRailRole @ZenlessRole`.
6. Run `!checkhoyo` to execute a poll immediately instead of waiting for the hourly loop.

The first successful code poll seeds the active-code snapshot and intentionally does not announce every code that was already active before the bot started tracking them. Later newly appearing codes are announced.
