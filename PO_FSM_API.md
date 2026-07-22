# Purchase Order FSM Transition Endpoints

**Implementáció:** 2026-07-22 (WORLDS-PROC-PO-FSM)
**Modul:** SpaceOS.Modules.Procurement
**Aggregátum:** `PurchaseOrder` (`src/SpaceOS.Modules.Procurement.Domain/Aggregates/PurchaseOrder.cs`)

---

## 1. Domain FSM (egyetlen igazságforrás)

```
Draft ──Submit()──▶ Submitted ──Confirm()──▶ Confirmed ──MarkShipped()──▶ Shipped ──RecordDelivery()──▶ Delivered
  │                     │                        │                          │
  └─────────────────────┴────────────Cancel()─────┴──────────────────────────┘
                                                                     (Cancel() tiltva Delivered-ből és
                                                                      már Cancelled-ből)
```

Az összes átmenet-szabályt kizárólag a `PurchaseOrder` aggregátum saját metódusai
(`Submit`, `Confirm`, `MarkShipped`, `RecordDelivery`, `Cancel`) döntik el —
`InvalidOperationException`-t dobnak illegális állapotból hívva. Az endpoint/handler
réteg **nem** duplikál FSM-táblát: a handlerek csak try/catch-csel fordítják az
aggregátum saját döntését `Result.Conflict`-ra (409).

## 2. HTTP végpontok

Csoport: `/api/procurement/orders` (`Endpoints/ProcurementEndpoints.cs`), auth: `ManufacturerOnly`
(bármely hitelesített felhasználó — a PurchaseOrder-aggregátumhoz nincs finomabb RBAC-fogalom,
ellentétben a requisition-jóváhagyás SoD-szabályával).

| Verb | Path | Body | Válasz (siker) | Hibák |
|---|---|---|---|---|
| POST | `/api/procurement/orders/{id}/submit` | — | 200 `OrderStatusResponse` (Status=`Submitted`) | 400 (rossz guid), 401, 404, 409 |
| POST | `/api/procurement/orders/{id}/confirm` | — | 200 `OrderStatusResponse` (Status=`Confirmed`) | 400, 401, 404, 409 |
| POST | `/api/procurement/orders/{id}/ship` | — | 200 `OrderStatusResponse` (Status=`Shipped`) | 400, 401, 404, 409 |
| POST | `/api/procurement/orders/{id}/deliver` | `{receivedQuantity, notes?, recordedBy?}` | 200 `OrderStatusResponse` (Status=`Delivered`) | 400, 401, 404, 409 |
| POST | `/api/procurement/orders/{id}/cancel` | — | 200 `OrderStatusResponse` (Status=`Cancelled`) | 400, 401, 404, 409 |

`OrderStatusResponse` (`Application/Queries/GetOrderStatus/GetOrderStatusQuery.cs`) — ugyanaz
a DTO-alak, mint a már meglévő `GET /api/procurement/orders/{id}`:

```json
{
  "id": "guid",
  "tenantId": "guid",
  "supplierId": "guid",
  "materialType": "string",
  "quantity": 100.0,
  "unitPrice": 5000.0,
  "currency": "HUF",
  "status": "Submitted",
  "expectedDelivery": null,
  "createdAt": "2026-07-22T00:00:00Z"
}
```

`status` mindig a valódi wire-kulcs (`Draft`/`Submitted`/`Confirmed`/`Shipped`/`Delivered`/`Cancelled`)
— a portál-oldali `Approved`/`Shipping` UI-elnevezés **csak megjelenítési alias** lehet, nem írhatja
át ezt a mezőt.

**Malformed guid → 400:** a route-paraméter `string id`-ként érkezik és manuálisan
`Guid.TryParse`-olt (ugyanaz a minta, mint a meglévő `GET /orders/{id}`-nél) — nem
`{id:guid}` route-constraint, mert az ASP.NET Core minimal API-ban egy nem-illő
constraint 404-et adna, nem 400-at.

**Tenant-izoláció:** ismeretlen ID és más tenant PO-ja egyaránt 404 — nincs
cross-tenant existence-leak (ugyanaz a minta, mint `GetOrderStatusQueryHandler`-ben).

## 3. Idempotencia

Az idempotencia-mechanizmus **a domain-guard maga**: minden `PurchaseOrder`-metódus
kizárólag a megfelelő forrásállapotból hívható, egyébként `InvalidOperationException`-t
dob. Ha ugyanaz a transition-kérés kétszer fut le:

1. Első hívás: legális átmenet → állapot mutálódik, esemény (ha van) egyszer raise-elődik,
   `SaveChangesAsync` egyszer perzisztál → 200.
2. Második hívás: az aggregátum már a cél-állapotban van → a metódus guard-ja dob →
   a handler `catch (InvalidOperationException)` ágon 409-et ad vissza, **mielőtt**
   bármilyen `SaveChangesAsync` lefutna.

Ez pontosan ugyanaz a minta, mint a meglévő delivery-folyamaté: a `RecordDelivery()`
csak `Shipped`-ből hívható, tehát egy megismételt delivery-kérés a második hívásnál
elakad a guard-on, mielőtt új `Delivery`-sor vagy `ProcurementOutboxMessage`
(`InventoryInboundRequested`, idempotencia-kulcs = friss `Delivery.Id`) létrejönne —
lásd `tests/.../Handlers/PurchaseOrderTransitionHandlerTests.cs` (`*_CalledTwice_*`
tesztek) a bizonyítékért.

### Gap, amit ez a task fedezett fel és javított

A `RecordDeliveryCommandHandler` korábban **feltétel nélkül** hívta `order.MarkShipped()`-et
minden delivery-kérésnél (mert korábban nem volt önálló "ship" végpont — a Confirmed→Shipped
és Shipped→Delivered átmenetet egyetlen hívás végezte el). Amint megjelenik az önálló
`POST /orders/{id}/ship` végpont, egy már explicit módon Shipped-re állított rendelésen a
delivery-hívás `MarkShipped()`-je hibázna (`Cannot mark order as shipped in status Shipped`).
Javítás: a handler csak akkor hívja `MarkShipped()`-et, ha a rendelés még `Confirmed`
állapotban van — ez nem új FSM-szabály, csak annak elkerülése, hogy egy már megtörtént
átmenetet újra megpróbáljunk elvégezni. A `RecordDelivery()` Shipped→Delivered legalitását
továbbra is kizárólag maga az aggregátum dönti el. Lásd
`Deliver_AfterExplicitShipEndpoint_ShouldStillSucceed` teszt.

Emellett a handler korábban egyáltalán nem fogott `InvalidOperationException`-t —
egy illegális állapotú (pl. Draft) rendelésre indított delivery-kérés kezeletlen
kivételt dobott (valószínűleg 500-at eredményezve HTTP-szinten) ahelyett, hogy 409-et
adott volna. Ezt is javítottuk (lásd `Deliver_FromDraft_ShouldReturnConflictNotThrow`).

## 4. Cancel-ág — nincs Stop-clause eszkaláció szükséges

A task Stop-klózja szerint, ha a cancel-szemantika ellentmond a domainnek, nem szabad
állapotot bővíteni, hanem ADR-jelöltet és UI-disabled gap-et kell hagyni. Ez **nem
állt fenn**: a `PurchaseOrder.Cancel()` már készen, tisztán támogatja a
Draft/Submitted/Confirmed/Shipped → Cancelled ágat, és helyesen tiltja Delivered-ből
és már Cancelled-ből. Nincs kompenzáló hatás, amit el kellene végezni (inventory-foglalás
csak `RecordDelivery`-nél történik, Cancel előtte mindig biztonságos) — így a cancel
végpont ugyanazzal a try/catch-mintával, fabrikálás nélkül exponálható volt.

## 5. Alkalmazási parancsok

| Command | Handler | Aggregátum-hívás |
|---|---|---|
| `SubmitPurchaseOrderCommand` | `SubmitPurchaseOrderCommandHandler` | `order.Submit()` |
| `ConfirmPurchaseOrderCommand` | `ConfirmPurchaseOrderCommandHandler` | `order.Confirm()` |
| `MarkPurchaseOrderShippedCommand` | `MarkPurchaseOrderShippedCommandHandler` | `order.MarkShipped()` |
| `CancelPurchaseOrderCommand` | `CancelPurchaseOrderCommandHandler` | `order.Cancel()` |
| *(Delivered)* | `RecordDeliveryCommandHandler` (**meglévő, újrahasznosított**) | `order.MarkShipped()` (feltételes) + `order.RecordDelivery()` |

Egyik új command sincs FluentValidation-validátorral ellátva (a meglévő ID-alapú
requisition-parancsok — `ApprovePurchaseRequisitionCommand`, `RejectPurchaseRequisitionCommand`
— mintáját követve): a legalitást teljes egészében az aggregátum dönti el, a
validátor csak Guid.Empty-t szűrne, amit a route-szintű `Guid.TryParse` már kizár.

## 6. Tesztek

- `tests/.../Domain/PurchaseOrderTests.cs` — teljes 6×5-ös (állapot × akció) legális/illegális
  mátrix (`TransitionMatrix_ShouldMatchAggregateGuards`), plusz explicit
  "duplikált hívás nem duplikál eseményt" tesztek Submit-re és RecordDelivery-re.
- `tests/.../Handlers/PurchaseOrderTransitionHandlerTests.cs` — handler-szintű
  Submit/Confirm/Ship/Cancel + a Deliver-újrahasznosítás (ship-endpoint utáni delivery,
  ismételt delivery, illegális állapotú delivery).
- `tests/.../Api/PurchaseOrderTransitionEndpointsTests.cs` — TestServer, mind az 5
  végpontra: 200+DTO, 400 (rossz guid, mind az 5 route-ra), 401 (mind az 5 route-ra),
  404, 409.

## 7. Tudatosan ki nem terjesztett scope

- Nincs `Reason`/actor mező a submit/confirm/ship/cancel parancsokon — a domain
  metódusok nem fogadnak ilyen paramétert, és a task tiltja az állapot/modell bővítését.
- Nincs `ProcurementAuditLog`-írás ezekhez az átmenetekhez — a `CreatePurchaseOrderCommandHandler`
  sem ír ilyet ma; külön scope-döntés kellene hozzá.
- Nincs 403-teszt a PO-átmenetekhez: a `PurchaseOrder`-hez ma nincs finomabb RBAC-fogalom
  (nincs "approver szerep" analógia, mint a requisition SoD-nál) — a 403 csak ott
  szerepel, ahol a domain ténylegesen ismer ilyen esetet.
