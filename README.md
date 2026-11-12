# toter-becherspiel

Version 1.0.0

Hütchenspiel für QBCore. Ein Spieler stellt mit dem Item Becherset einen Tisch in die Welt, andere stellen sich davor und setzen Bargeld darauf, unter welchem von drei Bechern die Kugel liegt. Der Host zahlt Gewinne aus seiner Tasche und kassiert die Einsätze der Verlierer. Standardmäßig ist jeder Tisch gezinkt (Config.ImmerGezinkt), der Host bestimmt nach den Wetten, wo die Kugel liegt.

## Abhängigkeiten

- qb-core (oder Anbindung nach ANSCHLUESSE.md)

## Installation

Im Ordner `resources/[toter]` des Servers:

```
git clone -b release --depth 1 https://github.com/toter99/toter-becherspiel.git toter-becherspiel
```

In der `server.cfg`:

```
ensure toter-becherspiel
```

## Update

```
cd resources/[toter]/toter-becherspiel
git pull
```

Danach neue SQL-Dateien einspielen und die Ressource neu starten.

## Mehr

Beschreibung, Einstellungen und Anbindung an andere Server: https://github.com/toter99/toter-becherspiel

## Lizenz

Siehe `LICENSE`. Erstellt von toter.
