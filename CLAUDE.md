# SpaceOS.Modules.Procurement — CLAUDE.md

## SESSION STARTUP/SHUTDOWN RITUAL

**Minden session elején:**
```bash
# 0. Datahaven státusz regisztráció — jelezd hogy dolgozol
curl -X POST https://datahaven.joinerytech.hu/api/terminal/status \
  -H "Authorization: Bearer dev-token-spaceos-dashboard-2026" \
  -H "Content-Type: application/json" \
  -d '{
    "terminal": "procurement",
    "status": "working",
    "currentTask": "Session started - checking inbox"
  }'

# 1. Inbox ellenőrzés
ls /opt/spaceos/docs/mailbox/procurement/inbox/
grep -l "status: UNREAD" /opt/spaceos/docs/mailbox/procurement/inbox/*.md 2>/dev/null
```

**Session végén (DONE/BLOCKED outbox után):**
```bash
# Datahaven státusz regisztráció — jelezd hogy befejeztél
curl -X POST https://datahaven.joinerytech.hu/api/terminal/status \
  -H "Authorization: Bearer dev-token-spaceos-dashboard-2026" \
  -H "Content-Type: application/json" \
  -d '{"terminal":"procurement","status":"idle"}'
```

**Datahaven Dashboard:** https://datahaven.joinerytech.hu (token: `dev-token-spaceos-dashboard-2026`)
- Dashboard (`/`) — Procurement státusz (WORKING/IDLE), inbox/outbox metrikák
- Kanban (`/kanban`) — Procurement swimlane a Delivery track-en
- Teljes API: `docs/WORKFLOW.md` — "Datahaven Dashboard" szakasz

---

## JELENLEGI ÁLLAPOT (2026-04-17)

| | |
|---|---|
| **Terminál** | procurement · Port: **5006** · Mailbox: `/opt/spaceos/docs/mailbox/procurement/` |
| **Aktuális commit** | `7e9f10f` (PROCUREMENT-006: POST+GET /api/procurement/suppliers) |
| **Tesztek** | **51/51 pass** |
| **VPS** | LIVE ✅ · migration `20260417000004_AddSupplierCreatedAt` alkalmazva |

### ⚠️ MigrateAsync() NINCS Program.cs-ben
A Procurement modulban **nem fut automatikusan** az EF migration deploy-kor.
Új migration esetén manuális SQL szükséges — jelezd a root terminálnak INFRA feladatként.

### TenantGucKey
```
TenantGucKey = "app.current_tenant_id"
```

### InternalEndpoints.cs — OpenConnectionAsync minta (KÖTELEZŐ)
```csharp
if (dbContext.Database.IsRelational())
    await dbContext.Database.OpenConnectionAsync(ct);
try {
    if (dbContext.Database.IsRelational())
        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant_id', {0}, false)",
            tenantGuid.ToString());
    counts = await repo.DeleteAllByTenantAsync(tenantGuid, ct);
} finally {
    if (dbContext.Database.IsRelational())
        await dbContext.Database.CloseConnectionAsync();
}
```

### ⚠️ /healthz endpoint hiányzik (tech debt)
A service fut és válaszol, de `/healthz` 404-et ad. Ha szükséges, add hozzá.

---

## Stack
- .NET 8, Clean Architecture + DDD + CQRS
- PostgreSQL 16 schema: `spaceos_procurement`
- EF Core 8 + Npgsql 8.0.11

## Approved packages
MediatR 12.4.1 · FluentValidation 12.1.1 · Ardalis.Result 10.1.0 · Ardalis.Specification 8.0.0
EF Core 8.0.11 · Npgsql 8.0.11 · xUnit v3 · Moq 4.20.72 · FluentAssertions 6.12.2

## Pipeline: INBOX → CODE → BUILD → TEST → OUTBOX

### Kötelező lépések
1. `ls /opt/spaceos/docs/mailbox/procurement/inbox/` → UNREAD inbox olvasása
2. `dotnet build` → **0 error, 0 warning**
3. `dotnet test` → **minden zöld**
4. Outbox: `mailbox/procurement/outbox/YYYY-MM-DD_NNN_<slug>-done.md` · `status: UNREAD`

## Layer dependency rule
```
Domain ← Application ← Infrastructure ← Api
                                       ← Tests
```

## Security
- Minden endpoint: `[Authorize(Policy = "ManufacturerOnly")]`
- TenantId JWT-ből, nem request bodyból
- RLS: Supplier, PurchaseOrder, Delivery táblák tenant alapján védve
