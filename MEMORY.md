# PROCUREMENT Memory

Utolsó frissítés: 2026-07-22 (WORLDS-PROC-PO-FSM)

## Aktuális állapot
- WORLDS-PROC-PO-FSM kész: `PurchaseOrder` FSM (Draft→Submitted→Confirmed→Shipped→Delivered
  + Cancel-ág) mind portál-elérhető HTTP-végponttal (`/api/procurement/orders/{id}/{submit,
  confirm,ship,deliver,cancel}`). Részletek: `PO_FSM_API.md`.

## Fontos kontextus
- `PurchaseOrder` aggregátum EGYETLEN igazságforrás az átmenetekre — kivétel-alapú guard
  (`InvalidOperationException`), nem `Result`-alapú (ellentétben `PurchaseRequisition`-nal).
  Handler-szinten try/catch fordítja `Result.Conflict`-ra (409) — ez NEM FSM-duplikáció.
- `RecordDeliveryCommandHandler` (delivery) újrahasznosítva változatlanul, csak egy
  idempotencia-biztonsági guard-dal bővítve: a belső `MarkShipped()`-hívás csak akkor fut,
  ha a rendelés még `Confirmed` (a dedikált ship-végpont miatt lehet már `Shipped`).
- Idempotencia mindenhol a domain-state-guard maga: ismételt kérés → guard dob → 409,
  nincs dupla esemény/outbox-sor/inventory-booking.
- `CreatePurchaseOrderCommandHandler` MA IS azonnal Submit()-tel is lezárja az új PO-t
  (sosem marad Draft-ban ezen az úton) — Draft állapotú PO csak a
  `ConvertRequisitionToPurchaseOrderCommandHandler` (PR→PO) útján jön létre. Ez a
  Submit-endpoint valódi belépési pontja.

## Következő lépések
- Frontend (külön task) fogja tükrözni ezt az FSM-et — wire-kulcsok (`Confirmed`/`Shipped`)
  nem írhatók át UI-elnevezéssel.

## Megoldott problémák
- RecordDeliveryCommandHandler kezeletlen `InvalidOperationException`-je illegális
  állapotú delivery-kérésnél (pl. Draft-ból) — most 409-et ad, nem 500-at.

## Session tapasztalatok
- 162→237 zöld teszt (75 új: domain-mátrix, handler, TestServer-endpoint).
