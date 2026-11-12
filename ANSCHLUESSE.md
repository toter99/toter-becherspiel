# Anschlüsse

Das Becherspiel greift nie direkt auf andere Ressourcen zu. Alles, was es von außen braucht (Geld, Namen, Gang, Inventar, Meldungen), steckt in zwei Dateien:

| Datei | Wofür |
|---|---|
| `Server/Bridge.cs` | Framework, Name, Gang, Geld, Item |
| `Client/Bridge.cs` | Meldungen am Bildschirm |
| `Shared/Config.cs` | Namen von Ressourcen, Konto, Gang-Regeln |

Standard ist **QBCore**. Für ein anderes Setup werden nur diese Funktionen angepasst, der Rest der Ressource ruft sie immer mit denselben Namen und Rückgabewerten auf. Danach neu bauen (siehe README). Eine Datenbank braucht das Spiel nicht.

## Server (`Server/Bridge.cs`)

| Funktion | Muss liefern | Standard (QBCore) |
|---|---|---|
| `Core()` | Framework-Objekt | `exports['qb-core']:GetCoreObject()`, Name in `Config.CoreRessource` |
| `QbSpieler(player)` | Spielerobjekt oder `null` | `Core().Functions.GetPlayer(source)` |
| `Name(player)` | Name, den die anderen am Tisch sehen | Vor- und Nachname aus `charinfo` |
| `GangVon(player)` | Gang-Name, `""` oder `"none"` = keine Gang | `PlayerData.gang.name` |
| `Melden(player, text, typ)` | Meldung am Bildschirm, `typ` = `success`, `error` oder `primary` | Client-Event `QBCore:Notify` |
| `Kontostand(player)` | Geld auf `Config.Konto` | `PlayerData.money[Config.Konto]` |
| `Bezahlen(player, betrag, grund)` | Einsatz abziehen, `true` nur bei Erfolg | `Functions.RemoveMoney` |
| `Auszahlen(player, betrag, grund)` | Gewinn oder Erstattung gutschreiben | `Functions.AddMoney` |
| `ItemAnlegen(name, label, gewicht, text)` | Becherset beim Inventar bekannt machen | `Core().Functions.AddItem` |
| `ItemBenutzbar(name, aktion)` | Benutzen abfangen und `aktion(serverId, item)` aufrufen | `Core().Functions.CreateUseableItem` |
| `HatItem(player, name)` | `true`, wenn der Spieler das Item dabeihat | `Functions.GetItemByName` |

## Client (`Client/Bridge.cs`)

| Funktion | Muss tun | Standard |
|---|---|---|
| `Melden(text, typ, dauer)` | Meldung am Bildschirm | Event `QBCore:Notify` |

## Einstellungen mit Bezug zu anderen Ressourcen (`Shared/Config.cs`)

| Einstellung | Bedeutung | Standard |
|---|---|---|
| `CoreRessource` | Framework-Ressource | `qb-core` |
| `Konto` | Konto für Einsätze und Gewinne | `cash` |
| `NurGangsAufstellen` | nur Gang-Mitglieder stellen einen Tisch auf | `true` |
| `NurGangsMitspielen` | nur Gang-Mitglieder dürfen setzen | `false` |
| `Gangs` | nur diese Gangs (Namen wie im Framework), leer = jede Gang | leer |
| `SetItem` | Name des Items | `becherset` |

## Beispiele für andere Setups

Die Beispiele zeigen, an welcher Stelle angepasst wird. Prüft die genauen Export-Namen in der Doku eurer Ressource, die Versionen unterscheiden sich.

**Qbox (`qbx_core`):** Qbox bringt eine Kompatibilität für QBCore mit, oft reicht `CoreRessource = "qb-core"` so wie es ist. Sonst in `QbSpieler` `exports.qbx_core:GetPlayer(source)` verwenden, `PlayerData.gang` heißt gleich.

**ESX:** `Core()` gibt `exports['es_extended']:getSharedObject()` zurück, `QbSpieler` wird `ESX.GetPlayerFromId(source)`. Dann:
- `Name`: `xPlayer.getName()`
- `GangVon`: ESX hat keine Gangs. Entweder den Job nehmen (`xPlayer.job.name`, und in `Config.Gangs` die Gang-Jobs eintragen) oder die Gang aus eurem Gang-Skript abfragen
- `Kontostand`: `xPlayer.getAccount('money').money`
- `Bezahlen`: erst den Kontostand prüfen, dann `xPlayer.removeAccountMoney('money', betrag)`
- `Auszahlen`: `xPlayer.addAccountMoney('money', betrag)`
- Item über `xPlayer.getInventoryItem(name).count`, benutzbar über `ESX.RegisterUsableItem`
- Meldungen über `esx:showNotification`

**Bargeld als Item** (ox_inventory, qs-inventory): `Kontostand`, `Bezahlen` und `Auszahlen` über deren Exports, etwa `exports.ox_inventory:GetItemCount(source, 'money')`, `RemoveItem(source, 'money', betrag)` und `AddItem(source, 'money', betrag)`.

**ox_inventory:** `ItemAnlegen` leer lassen und das Becherset in `ox_inventory/data/items.lua` eintragen, das Bild nach `ox_inventory/web/images`. Benutzen über die Item-Option `server.export`, `HatItem` über `GetItemCount`.

**Gangs aus einem eigenen Skript:** `GangVon` gibt den Gang-Namen zurück, den das Skript liefert. Wer keiner Gang angehört, bekommt `""`. Soll jeder spielen dürfen, `NurGangsAufstellen = false`.

**Meldungen mit ox_lib:** In beiden `Melden` statt `QBCore:Notify` das Event `ox_lib:notify` mit `{ title, description, type }` senden.

## Sicherheit

Alles, was mit Geld zu tun hat, prüft und rechnet der Server: Einsatz abbuchen, Mindesteinsatz, Gewinne, Erstattungen beim Stoppen, Abbauen oder Verlassen. Der Client schickt nur Wünsche (welcher Becher, welcher Betrag). Der Server prüft Abstand zum Tisch, Gang, Item und begrenzt, wie oft ein Spieler Events schicken darf. Wer `Bezahlen` anpasst, muss weiter `false` liefern, wenn das Geld nicht reicht.
