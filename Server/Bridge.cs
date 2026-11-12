using System;
using System.Collections.Generic;
using CitizenFX.Core;
using ToterBecher.Shared;

namespace ToterBecher.Server
{
    // ======================================================================================
    //  ANSCHLÜSSE (Server)
    //  Alles, was toter-becherspiel von anderen Ressourcen braucht. Standard ist QBCore.
    //  Für einen anderen Server nur diese Funktionen anpassen. Beispiele für andere Setups: ANSCHLUESSE.md
    // ======================================================================================
    public partial class Main
    {
        private dynamic core;

        private dynamic Core()
        {
            if (core == null) core = Exports[Config.CoreRessource].GetCoreObject();
            return core;
        }

        // Spielerobjekt des Frameworks oder null.
        private dynamic QbSpieler(Player player)
        {
            if (player == null) return null;
            try { return Core().Functions.GetPlayer(int.Parse(player.Handle)); }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Spieler nicht lesbar: {e.Message}");
                return null;
            }
        }

        // Name, den die anderen am Tisch sehen. Standard: Vor- und Nachname des Charakters.
        private string Name(Player player)
        {
            try
            {
                var xp = QbSpieler(player);
                return $"{(string)xp.PlayerData.charinfo.firstname} {(string)xp.PlayerData.charinfo.lastname}";
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Name lesen: {e.Message}");
                return player?.Name ?? "Unbekannt";
            }
        }

        // Gang des Spielers ("none" oder leer = keine). Standard: QBCore PlayerData.gang.name.
        private string GangVon(Player player)
        {
            try { return (string)QbSpieler(player)?.PlayerData.gang.name ?? ""; }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Gang lesen: {e.Message}");
                return "";
            }
        }

        // Meldung am Bildschirm, wenn das Becherspiel-Fenster nicht offen ist. typ: "success", "error", "primary".
        private void Melden(Player player, string text, string typ = "primary")
        {
            TriggerClientEvent(player, "QBCore:Notify", text, typ, 5000);
        }

        // Kontostand auf Config.Konto.
        private int Kontostand(Player player)
        {
            try
            {
                object geld = QbSpieler(player)?.PlayerData.money;
                object wert;
                if (geld is IDictionary<string, object> d && d.TryGetValue(Config.Konto, out wert)) return Convert.ToInt32(wert);
            }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Kontostand: {e.Message}"); }
            return 0;
        }

        // Betrag abziehen. true, wenn genug Geld da war.
        private bool Bezahlen(Player player, int betrag, string grund)
        {
            if (betrag <= 0) return true;
            try
            {
                var xp = QbSpieler(player);
                if (xp == null) return false;
                object ok = xp.Functions.RemoveMoney(Config.Konto, betrag, grund);
                return ok is bool b && b;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Bezahlen: {e.Message}");
                return false;
            }
        }

        // Betrag gutschreiben (Gewinn, Erstattung).
        private void Auszahlen(Player player, int betrag, string grund)
        {
            if (betrag <= 0) return;
            try { QbSpieler(player)?.Functions.AddMoney(Config.Konto, betrag, grund); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] Auszahlen an {player?.Name}: {e.Message}"); }
        }

        // ---------------------------------------------------------------- Inventar

        // Item beim Framework bekannt machen. Steht es schon in der Item-Liste des Inventars, passiert nichts.
        private void ItemAnlegen(string name, string label, int gewicht, string beschreibung)
        {
            try
            {
                Core().Functions.AddItem(name, new Dictionary<string, object>
                {
                    { "name", name }, { "label", label }, { "weight", gewicht }, { "type", "item" },
                    { "image", name + ".png" }, { "unique", false }, { "useable", true }, { "shouldClose", true },
                    { "combinable", null }, { "description", beschreibung }
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Item {name} nicht angelegt, bitte in die Item-Liste des Inventars eintragen: {e.Message}");
            }
        }

        // Aufruf, wenn ein Spieler das Item im Inventar benutzt: (Server-ID, Item-Daten).
        private void ItemBenutzbar(string name, Action<int, object> beimBenutzen)
        {
            try { Core().Functions.CreateUseableItem(name, beimBenutzen); }
            catch (Exception e) { Debug.WriteLine($"[{Config.Ressource}] {name} benutzbar machen: {e.Message}"); }
        }

        // true, wenn der Spieler das Item mindestens einmal dabeihat.
        private bool HatItem(Player player, string name)
        {
            try { return QbSpieler(player)?.Functions.GetItemByName(name) != null; }
            catch (Exception e)
            {
                Debug.WriteLine($"[{Config.Ressource}] Item prüfen: {e.Message}");
                return false;
            }
        }
    }
}
