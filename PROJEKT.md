# toter-becherspiel

## Zweck
Hütchenspiel für QBCore. Ein Spieler stellt mit dem Item Becherset einen Tisch in die Welt, andere stellen sich davor und setzen Bargeld darauf, unter welchem von drei Bechern die Kugel liegt. Der Host zahlt Gewinne aus seiner Tasche und kassiert die Einsätze der Verlierer. Standardmäßig ist jeder Tisch gezinkt (Config.ImmerGezinkt), der Host bestimmt nach den Wetten, wo die Kugel liegt.

## Abhängigkeiten
- qb-core (oder Anbindung nach ANSCHLUESSE.md)

## Status
Version 1.0.0 (erstes öffentliches Release, github.com/toter99/toter-becherspiel, MIT). Interne Stände bis 2.0.1 sind darin aufgegangen, Änderungen ab jetzt in CHANGELOG.md. Bis 1.5.1 im Spiel getestet, Gang-Sperre und Spam-Schutz kamen danach. Autor und Projektseite in Config.Autor/Projektseite, Hinweis beim Start in der Server-Konsole, kleiner Vermerk im Fenster.

- **Aufstellen:** Item `becherset`, nur Gang-Mitglieder (Config.NurGangsAufstellen, Config.Gangs), Vorschau mit Drehen per Mausrad oder Pfeiltasten. Becher und Kugel liegen auf der echten Tischplatte.
- **Runde:** Zeigen, Mischen, bis 60 Sekunden setzen (5, sobald alle gesetzt haben), gezinkt: Host wählt in 10 Sekunden den Becher, sonst gilt der ehrliche.
- **Einsatz:** pro Runde fest oder Mindesteinsatz, nach oben praktisch offen (Config.EinsatzTechnischMax nur gegen Rechenüberlauf). Mindestbetrag steht beim Spieler im Feld.
- **Anzeige:** über den Bechern sehen alle, wer wählt (gestrichelt) und wer was gesetzt hat (grün), der Host zusätzlich rechts in seiner Liste.
- **Stoppen:** Host bricht eine Runde ab, ohne aufzudecken, alle Einsätze gehen zurück.
- **Sicherheit:** Geld nur auf dem Server, Abstand zum Tisch beim Setzen geprüft, jedes Server-Event pro Spieler gedrosselt (Gedrosselt), Gewinne mit long gerechnet.
- **Technik:** C# nach der Lua-Fassung "cupgame", Anschlüsse in Server/Bridge.cs und Client/Bridge.cs (ANSCHLUESSE.md), keine Datenbank, keine Chat-Befehle.

## Ablauf
1. Host: Becherset im Inventar benutzen, Schwierigkeit wählen, optional gezinkt, Tisch mit Pfeiltasten drehen und mit E aufstellen.
2. Spieler stellen sich auf die Seite mit dem grünen Kreis und drücken E (höchstens 6 pro Tisch).
3. Host startet die Runde: Kugel wird kurz gezeigt, die Becher werden gemischt, dann bis zu 60 Sekunden Wetten (5 Sekunden, sobald alle gesetzt haben).
4. Ehrlicher Tisch: Server deckt die echte Position auf. Gezinkt: Host wählt in 10 Sekunden (Config.ZinkenMs) den Becher, sonst gilt die echte Position.
5. Abrechnung: Verlierer zuerst (Einsatz an den Host), dann Gewinner (Einsatz zurück plus gleich viel vom Host). Reicht das Geld des Hosts nicht, wird nach Einsatzhöhe aufgeteilt.

Wetten beziehen sich auf die Position (1 links, 2 Mitte, 3 rechts), nicht auf einen bestimmten Becher.

## Bedienung
Keine Chat-Befehle.

| Was | Wirkung |
|---|---|
| Item `becherset` benutzen | Tisch aufstellen, oder den eigenen Tisch wieder öffnen |
| E am Tisch | mitspielen, beim eigenen Tisch das Fenster öffnen |
| Knopf "Tisch abbauen" | eigenen Tisch abbauen, offene Einsätze gehen zurück |
| F6 (Tastenbelegung `becherspiel_kamera`) | am Tisch zwischen Maus und freier Kamera wechseln |

Esc schließt beim Host nur das Fenster, der Tisch bleibt stehen. Spieler verlassen mit Esc den Tisch. Das Set bleibt beim Aufstellen im Inventar, der Server prüft beim Aufstellen, ob es da ist.

## Schnittstellen
Server-Events (`toter-becherspiel:server:`, alle gedrosselt): `tische`, `aufstellen(stufe, gezinkt, x, y, z, richtung, hoehen, becherHoehe)`, `abbauen`, `wiederOeffnen`, `beitreten(hostId)`, `verlassen(hostId)`, `starten`, `stoppen`, `einsatz(betrag, fest)`, `auswahl(hostId, becher)`, `wetten(hostId, becher, betrag)`, `zinken(becher)`.

Client-Events (`toter-becherspiel:client:`): `set` (Item benutzt), `tische`, `tisch(hostId, info|null)`, `offen`, `zu`, `zeigen`, `mischen`, `einsatz`, `wettenAuf`, `wettenUhr`, `wetten`, `wetteOk`, `wettenZu`, `gestoppt`, `zinken`, `aufdecken`, `hinweis`, `spieler`.

Der Server prüft alles selbst: Abstand und Seite beim Beitreten, Wettphase, Einsatzgrenzen, Geld, ob der Absender der Host ist.

## Anschlüsse
`Server/Bridge.cs` (Name, Gang, Kontostand, Bezahlen, Auszahlen, Melden, Item anlegen, benutzbar machen, prüfen) und `Client/Bridge.cs` (Melden). Allgemeine Beschreibung mit Beispielen für Qbox, ESX, ox_inventory, ox_lib: ANSCHLUESSE.md.

## Entscheidungen
- GetGameTimer() ist auf dem Server long, im Client int. Auf dem Server immer long verwenden.
- Tisch-Props erzeugt der Server (OneSync), Becher und Kugel jeder Client selbst. Die Höhen rechnet der Client beim Aufstellen über GetModelDimensions aus und schickt sie mit.
- Andere Spieler erfahren nicht, ob ein Tisch gezinkt ist. In der alten Fassung stand das in `/cupgame list`.
- Gewinne 1:1. Den Multiplikator aus der alten Config gibt es nicht mehr.
- Skalieren von Props ist entfallen, weil es serverseitig nicht greift und die Kollision nicht mitskaliert.
- Wer neu verbindet, bekommt die stehenden Tische vom Server (in der alten Fassung sah man nur Tische, die nach dem eigenen Login aufgestellt wurden).
- Mindestabstand zwischen Tischen, Tisch nur direkt beim Spieler, 30 Sekunden Pause zwischen zwei Tischen.
- Kein Lua, wenige Kommentare, Texte auf Deutsch.

## Werte aus der alten Fassung
Tisch: `prop_hat_box_05` mit `prop_apple_box_02` darauf, Becher `prop_plastic_cup_02` umgedreht, Kugel `prop_poolball_9`, Becherabstand 0.25, Extrahöhe 0.15, Sichtweite 100, Tisch 0.5 m vor dem Spieler.

## Offen
- Ingame testen: Becherhöhe, Drehung der umgedrehten Becher (sonst in `Client/Tisch.cs` 180 bei SetEntityRotation von Pitch auf Roll), Seite des grünen Kreises (`VorneUmdrehen`).
- `prop_hat_box_05` verschwindet aus kürzerer Entfernung als die Platte (Draw-Distanz des Modells). Eventuell ein Außen-Prop als Fuß, z. B. `prop_barrel_02a`.
- Verlässt ein Spieler mitten in der Wettphase das Spiel, wird sein Einsatz beim Trennen erstattet. Ist QBCore zu dem Zeitpunkt schon mit dem Spieler fertig, geht das ins Leere.
- Ideen: `ox_target` statt E, eigene rote Becher als Prop, Set beim Aufstellen aus dem Inventar nehmen und beim Abbauen zurückgeben.
