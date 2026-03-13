# 4 Pics 1 Word (PF_Project_4P1W)

Monorepo containing:
- `auth-api` (ASP.NET Core Web API)
- `resource-api` (ASP.NET Core Web API)
- `web-app` (React player + admin CMS)

## Quick Start (Local)

### auth-api
1. `cd auth-api`
2. `dotnet restore`
3. `dotnet run`

### resource-api
1. `cd resource-api`
2. `dotnet restore`
3. `dotnet run`

### web-app
1. `cd web-app`
2. `npm install`
3. `npm run dev`

## Environment Variables

Create `.env` files per service as needed:
- `auth-api/.env`
- `resource-api/.env`
- `web-app/.env`

## Iterations (High-Level)
1. Foundations & Auth
2. Packs & Randomization (Player)
3. Puzzles & Core Gameplay
4. CMS: Images + Tags
5. CMS: Puzzles + Packs
6. Polish, QA, and Launch

## Notes
- File storage is local for dev; image URLs returned by APIs.
- Admin-only CMS routes/components are protected by role `admin`.
