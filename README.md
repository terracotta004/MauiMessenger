# MauiMessenger

This is a 90% ChatGPT Codex-written repository.

[![CI](https://github.com/terracotta004/MauiMessenger/actions/workflows/dotnet.yml/badge.svg)](https://github.com/terracotta004/MauiMessenger/actions/workflows/dotnet.yml)

On July 16, 2026, I started adding Codex co-author attribution to commit messages.

Add an .env file with these contents to set up the database:

```text
POSTGRES_DB=mauimessenger
POSTGRES_USER=mauimessenger
POSTGRES_PASSWORD=mauimessenger
POSTGRES_PORT=5432
```

Bring up the postgresql database:

```bash
docker compose up -d
docker compose ps
docker logs -f mauimessenger-db
```

Test the database / database user:

```bash
docker exec -it mauimessenger-db psql \
  -U mauimessenger \
  -d mauimessenger \
  -c "SELECT current_database(), current_user;"
```

Configure the default connection string for the API. 
Change the host to where your databse is running — 
on localhost, or on a different host.

macOS/Linux:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=15432;Database=mauimessenger;Username=mauimessenger;Password=mauimessenger"
```

Windows PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=15432;Database=mauimessenger;Username=mauimessenger;Password=mauimessenger"
```

If you are running the database on another computer than your development system, 
use this command to port forward. Replace 'ben@pi-ubuntu' with the right user and system.

```bash
ssh -N -L 15432:127.0.0.1:5432 ben@pi-ubuntu
```

Now, run `migrate-macos.ps1` or `migrate-windows.ps1` in PowerShell to prepare your database (located in project root):

Now run the API + Client.Web in your chosen IDE (Rider, Visual Studio, Visual Studio Code). 

Rider Multi-Launch Configuration: https://www.jetbrains.com/help/rider/Run_Debug_Multiple.html#multi_launch
