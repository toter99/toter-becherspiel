const $ = (id) => document.getElementById(id);
const PLATZ_X = { 1: "20%", 2: "50%", 3: "80%" };

const zustand = {
  stufe: null,
  istHost: false,
  min: 10,
  max: 5000,
  taste: "F6",
  platzHat: [0, 1, 2, 3],
  gewaehlt: 0,
  wettenOffen: false,
  gesetzt: false,
  uhr: null,
  laeufe: [],
  einsatz: 10,
  fest: false,
  ich: 0,
};

const geld = (n) => "$" + Number(n).toLocaleString("de-DE");
const becherEl = (id) => document.querySelector(`.bs-becher[data-id="${id}"]`);
const becherAuf = (platz) => becherEl(zustand.platzHat[platz]);

function spaeter(fn, ms) {
  const t = setTimeout(fn, ms);
  zustand.laeufe.push(t);
}

function laeufeStoppen() {
  zustand.laeufe.forEach(clearTimeout);
  zustand.laeufe = [];
}

function status(text) {
  $("status").textContent = text;
}

// ---------------------------------------------------------------- Aufstellen

function aufbauZeigen(stufen, zinkenErlaubt) {
  const box = $("stufen");
  box.replaceChildren();
  stufen.forEach((s, i) => {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "bs-stufe" + (i === 0 ? " is-active" : "");
    b.innerHTML = `${s.name}<small>${s.tausche}× tauschen</small>`;
    b.onclick = () => {
      box.querySelectorAll(".bs-stufe").forEach((x) => x.classList.remove("is-active"));
      b.classList.add("is-active");
      zustand.stufe = s.id;
    };
    box.append(b);
  });
  zustand.stufe = stufen[0] ? stufen[0].id : null;
  $("zinken").checked = false;
  $("zinken-zeile").hidden = !zinkenErlaubt;
  Nui.hide($("tisch"));
  Nui.show($("aufbau"));
}

$("aufstellen").onclick = () => {
  Nui.hide($("aufbau"));
  Nui.post("aufstellen", { stufe: zustand.stufe, gezinkt: $("zinken").checked });
};

// ---------------------------------------------------------------- Becher

function becherSetzen(dauer) {
  for (let platz = 1; platz <= 3; platz++) {
    const el = becherAuf(platz);
    el.style.transitionDuration = dauer ? dauer + "ms" : "0ms";
    el.style.setProperty("--x", PLATZ_X[platz]);
    el.querySelector(".bs-nummer").textContent = platz;
  }
}

function becherZuruecksetzen() {
  zustand.platzHat = [0, 1, 2, 3];
  document.querySelectorAll(".bs-becher").forEach((el) => el.classList.remove("is-hoch", "mit-kugel", "is-gewaehlt", "is-gewinner"));
  becherSetzen(0);
}

function tauschen(a, b, dauer) {
  const p = zustand.platzHat;
  [p[a], p[b]] = [p[b], p[a]];
  becherSetzen(dauer);
}

document.querySelectorAll(".bs-becher").forEach((el) => {
  el.onclick = () => {
    if (!zustand.wettenOffen || zustand.gesetzt) return;
    const platz = zustand.platzHat.indexOf(Number(el.dataset.id));
    zustand.gewaehlt = platz;
    document.querySelectorAll(".bs-becher").forEach((x) => x.classList.toggle("is-gewaehlt", x === el));
    setzenPruefen();
    Nui.post("auswahl", { becher: platz });
  };
});

// ---------------------------------------------------------------- Wetten


const CHIPS = [50, 100, 500, 1000];

function mindest() {
  return Math.max(zustand.min, zustand.einsatz || 0);
}

function einsatzAnwenden(betrag, fest) {
  zustand.einsatz = betrag || zustand.min;
  zustand.fest = !!fest;
  const warFest = $("betrag").disabled;
  $("betrag").disabled = zustand.fest;
  $("betrag").min = mindest();
  $("betrag").placeholder = `Einsatz ab ${geld(mindest())}`;
  const jetzt = parseInt($("betrag").value, 10);
  if (zustand.fest) $("betrag").value = zustand.einsatz;
  else if (warFest || !(jetzt >= mindest())) $("betrag").value = mindest();
  chipsBauen();
  document.querySelectorAll("#einsatz-art .bs-stufe").forEach((b) => b.classList.toggle("is-active", (b.dataset.art === "fest") === zustand.fest));
  if (document.activeElement !== $("fest-betrag")) $("fest-betrag").value = zustand.einsatz;
  infoZeile();
  setzenPruefen();
}

function chipsBauen() {
  const box = $("chips");
  box.replaceChildren();
  const werte = [...new Set([mindest(), ...CHIPS.filter((n) => n > mindest())])].slice(0, 4);
  box.hidden = zustand.fest || !werte.length;
  werte.forEach((n) => {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "bs-chip";
    b.textContent = geld(n);
    b.onclick = () => {
      $("betrag").value = n;
      setzenPruefen();
    };
    box.append(b);
  });
}

function infoZeile() {
  const d = zustand.info || {};
  const teile = [];
  if (!zustand.istHost) teile.push(`Tisch von ${d.hostName}`);
  teile.push(d.stufe);
  if (d.gezinkt) teile.push("gezinkt");
  teile.push(zustand.fest ? `Einsatz fest ${geld(zustand.einsatz)}` : `Mindesteinsatz ${geld(mindest())}`);
  $("tisch-info").textContent = teile.join(" · ");
}

function hostKnoepfe(laeuft) {
  $("starten").hidden = laeuft;
  $("starten").disabled = false;
  $("stoppen").hidden = !laeuft;
  $("stoppen").disabled = false;
  document.querySelector(".bs-einsatz-wahl").classList.toggle("is-gesperrt", laeuft);
}

function einsatzSenden(fest) {
  const n = parseInt($("fest-betrag").value, 10);
  if (!(n >= zustand.min)) {
    Nui.toast("Becherspiel", `Der Einsatz ist mindestens ${geld(zustand.min)}.`, "danger");
    $("fest-betrag").value = zustand.einsatz;
    return;
  }
  Nui.post("einsatz", { betrag: n, fest });
}

document.querySelectorAll("#einsatz-art .bs-stufe").forEach((b) => {
  b.onclick = () => einsatzSenden(b.dataset.art === "fest");
});
$("fest-betrag").addEventListener("change", () => einsatzSenden(zustand.fest));

// Einsätze aller Spieler über den Bechern (Platz = Becher-Nummer beim Setzen).
function wettenZeigen(liste) {
  for (let platz = 1; platz <= 3; platz++) {
    const box = document.querySelector(`.bs-gesetzt[data-platz="${platz}"]`);
    box.replaceChildren();
    const hier = liste.filter((w) => w.becher === platz)
      .sort((a, b) => (b.gesetzt !== false) - (a.gesetzt !== false) || b.betrag - a.betrag);
    if (!hier.length) continue;
    const drin = hier.filter((w) => w.gesetzt !== false);
    const summe = document.createElement("b");
    summe.textContent = drin.length ? geld(drin.reduce((s, w) => s + w.betrag, 0)) : "\u00a0";
    box.append(summe);
    hier.slice(0, 3).forEach((w) => {
      const z = document.createElement("span");
      const gesetzt = w.gesetzt !== false;
      z.textContent = gesetzt ? `${w.name} · ${geld(w.betrag)}` : `${w.name} · wählt`;
      z.classList.add(gesetzt ? "is-drin" : "is-wahl");
      if (w.id === zustand.ich) z.classList.add("is-ich");
      box.append(z);
    });
    if (hier.length > 3) {
      const mehr = document.createElement("small");
      mehr.textContent = `+${hier.length - 3} weitere`;
      box.append(mehr);
    }
  }
}

function setzenPruefen() {
  const n = parseInt($("betrag").value, 10);
  $("setzen").disabled = zustand.gesetzt || !zustand.gewaehlt || !(n >= mindest());
}

$("betrag").addEventListener("input", setzenPruefen);
$("setzen").onclick = () => {
  const betrag = parseInt($("betrag").value, 10);
  if (!zustand.gewaehlt || !betrag) return;
  $("setzen").disabled = true;
  Nui.post("wetten", { becher: zustand.gewaehlt, betrag });
};

function uhr(sekunden) {
  clearInterval(zustand.uhr);
  let rest = sekunden;
  const el = $("zeit");
  el.hidden = false;
  const zeigen = () => {
    el.textContent = rest;
    el.classList.toggle("is-knapp", rest <= 5);
  };
  zeigen();
  zustand.uhr = setInterval(() => {
    rest--;
    if (rest <= 0) {
      clearInterval(zustand.uhr);
      el.hidden = true;
      return;
    }
    zeigen();
  }, 1000);
}

function uhrStopp() {
  clearInterval(zustand.uhr);
  $("zeit").hidden = true;
}

// ---------------------------------------------------------------- Host

$("starten").onclick = () => {
  $("starten").disabled = true;
  Nui.post("starten");
};
$("stoppen").onclick = () => {
  $("stoppen").disabled = true;
  Nui.post("stoppen");
};
$("abbauen").onclick = () => {
  Nui.hide($("tisch"));
  Nui.post("abbauen");
};

function leuteZeigen(liste) {
  const ul = $("leute-liste");
  ul.replaceChildren();
  $("leute-zahl").textContent = liste.length;
  if (!liste.length) {
    const li = document.createElement("li");
    li.className = "bs-leer";
    li.textContent = "Noch niemand da";
    ul.append(li);
    return;
  }
  liste.forEach((p) => {
    const li = document.createElement("li");
    li.className = "tu-list-item" + (p.becher ? " is-gesetzt" : "");
    const n = document.createElement("span");
    n.textContent = p.name;
    li.append(n);
    if (p.becher) {
      const m = document.createElement("span");
      m.className = "tu-list-meta";
      m.textContent = `Becher ${p.becher} · ${geld(p.betrag)}`;
      li.append(m);
    }
    ul.append(li);
  });
}

function zinkenZeigen(wetten, ehrlich, sekunden) {
  $("ehrlich").textContent = ehrlich;
  const ul = $("einsaetze");
  ul.replaceChildren();
  if (!wetten.length) {
    const li = document.createElement("li");
    li.className = "bs-leer";
    li.textContent = "Niemand hat gesetzt.";
    ul.append(li);
  }
  wetten.forEach((w) => {
    const li = document.createElement("li");
    li.className = "tu-list-item";
    const n = document.createElement("span");
    n.textContent = w.name;
    const m = document.createElement("span");
    m.className = "tu-list-meta";
    m.textContent = `Becher ${w.becher} · ${geld(w.betrag)}`;
    li.append(n, m);
    ul.append(li);
  });
  const wahl = $("wahl");
  wahl.replaceChildren();
  [1, 2, 3].forEach((b) => {
    const k = document.createElement("button");
    k.type = "button";
    k.className = "tu-btn" + (b === ehrlich ? " is-primary" : "");
    k.textContent = `Becher ${b}`;
    k.onclick = () => {
      Nui.hide($("zinken-fenster"));
      Nui.post("zinken", { becher: b });
    };
    wahl.append(k);
  });
  let rest = sekunden;
  $("zinken-zeit").textContent = `${rest} s`;
  const t = setInterval(() => {
    rest--;
    $("zinken-zeit").textContent = `${Math.max(rest, 0)} s`;
    if (rest <= 0 || $("zinken-fenster").hidden) {
      clearInterval(t);
      Nui.hide($("zinken-fenster"));
    }
  }, 1000);
  Nui.show($("zinken-fenster"));
}

// ---------------------------------------------------------------- Nachrichten vom Client

Nui.on("aufbau", (d) => aufbauZeigen(d.stufen || [], d.zinken));

Nui.on("tisch", ({ daten: d }) => {
  laeufeStoppen();
  uhrStopp();
  becherZuruecksetzen();
  zustand.istHost = !!d.istHost;
  zustand.min = d.min;
  zustand.taste = d.taste || "F6";
  zustand.wettenOffen = false;
  zustand.gesetzt = false;
  zustand.gewaehlt = 0;

  $("tisch-titel").textContent = zustand.istHost ? "Dein Becherspiel" : "Becherspiel";
  zustand.info = d;
  $("betrag").min = d.min;
  Nui.hide($("wette"));
  Nui.hide($("ergebnis"));
  Nui.hide($("zinken-fenster"));
  $("host").hidden = !zustand.istHost;
  $("leute").hidden = !zustand.istHost;
  hostKnoepfe(d.zustand !== "frei");
  zustand.ich = d.ich || 0;
  einsatzAnwenden(d.einsatz, d.fest);
  wettenZeigen([]);
  if (zustand.istHost) leuteZeigen([]);
  status(d.zustand === "frei" ? (zustand.istHost ? "Starte eine Runde, sobald jemand mitspielt." : "Warte auf die nächste Runde.") : "Die Runde läuft schon, du bist bei der nächsten dabei.");
  $("fuss").innerHTML = `<kbd>${zustand.taste}</kbd> Kamera frei · <kbd>Esc</kbd> ${zustand.istHost ? "Fenster zu, Tisch bleibt stehen" : "Tisch verlassen"}`;
  document.body.classList.remove("is-kamera");
  Nui.hide($("kamera-hinweis"));
  Nui.hide($("aufbau"));
  Nui.show($("tisch"));
});

Nui.on("zeigen", ({ ball, dauer }) => {
  laeufeStoppen();
  becherZuruecksetzen();
  Nui.hide($("ergebnis"));
  Nui.hide($("wette"));
  zustand.wettenOffen = false;
  zustand.gesetzt = false;
  zustand.gewaehlt = 0;
  hostKnoepfe(true);
  wettenZeigen([]);
  status("Merk dir, wo die Kugel liegt.");
  const el = becherAuf(ball);
  el.classList.add("is-hoch", "mit-kugel");
  spaeter(() => el.classList.remove("is-hoch"), Math.max(dauer - 400, 0));
  spaeter(() => el.classList.remove("mit-kugel"), Math.max(dauer - 100, 0));
});

Nui.on("mischen", ({ tausche, dauer }) => {
  status("Augen auf den Becher.");
  tausche.forEach(([a, b], i) => spaeter(() => tauschen(a, b, dauer), i * dauer));
});

Nui.on("wettenAuf", ({ sekunden, istHost }) => {
  becherSetzen(0);
  uhr(sekunden);
  if (istHost) {
    status("Die Wetten laufen.");
    return;
  }
  zustand.wettenOffen = true;
  zustand.gesetzt = false;
  zustand.gewaehlt = 0;
  $("buehne").classList.add("kann-waehlen");
  $("betrag").value = zustand.fest ? zustand.einsatz : mindest();
  setzenPruefen();
  status(zustand.fest ? `Wo ist die Kugel? Becher anklicken und ${geld(zustand.einsatz)} setzen.` : `Wo ist die Kugel? Becher anklicken, ab ${geld(mindest())} setzen.`);
  Nui.show($("wette"));
});

Nui.on("wettenUhr", ({ sekunden }) => {
  uhr(sekunden);
  status(zustand.istHost || zustand.gesetzt ? "Alle haben gesetzt, gleich wird aufgedeckt." : "Alle anderen haben gesetzt, schnell noch setzen.");
});

Nui.on("wetteOk", ({ becher, betrag }) => {
  zustand.gesetzt = true;
  setzenPruefen();
  status(`Gesetzt: ${geld(betrag)} auf Becher ${becher}.`);
});

Nui.on("wettenZu", () => {
  zustand.wettenOffen = false;
  uhrStopp();
  $("buehne").classList.remove("kann-waehlen");
  Nui.hide($("wette"));
  status("Nichts geht mehr.");
});

Nui.on("zinken", ({ wetten, ehrlich, sekunden }) => zinkenZeigen(wetten || [], ehrlich, sekunden));

Nui.on("aufdecken", ({ becher, ergebnisse, istHost }) => {
  Nui.hide($("zinken-fenster"));
  const el = becherAuf(becher);
  el.classList.add("is-hoch", "mit-kugel", "is-gewinner");
  status(`Die Kugel lag unter Becher ${becher}.`);
  hostKnoepfe(false);

  const liste = $("ergebnis-liste");
  liste.replaceChildren();
  const titel = $("ergebnis-titel");
  titel.className = "bs-ergebnis-titel";
  if (istHost) {
    if (!ergebnisse.length) return;
    const summe = ergebnisse.reduce((s, e) => s + (e.gewonnen ? -e.betrag : e.betrag), 0);
    titel.textContent = summe >= 0 ? `Du machst ${geld(summe)} Gewinn` : `Du zahlst ${geld(-summe)} drauf`;
    titel.classList.add(summe >= 0 ? "is-gewonnen" : "is-verloren");
    ergebnisse.forEach((e) => {
      const li = document.createElement("li");
      const n = document.createElement("span");
      n.textContent = e.name;
      const b = document.createElement("span");
      b.className = e.gewonnen ? "minus" : "plus";
      b.textContent = (e.gewonnen ? "-" : "+") + geld(e.betrag);
      li.append(n, b);
      liste.append(li);
    });
  } else {
    const mein = ergebnisse[0];
    if (!mein) return;
    titel.textContent = mein.gewonnen ? `Gewonnen! +${geld(mein.betrag)}` : `Verloren. -${geld(mein.betrag)}`;
    titel.classList.add(mein.gewonnen ? "is-gewonnen" : "is-verloren");
  }
  spaeter(() => Nui.show($("ergebnis")), 600);
});

Nui.on("spieler", ({ liste }) => leuteZeigen(liste || []));

Nui.on("einsatz", ({ betrag, fest }) => einsatzAnwenden(betrag, fest));
Nui.on("wettenListe", ({ liste }) => wettenZeigen(liste || []));

Nui.on("gestoppt", ({ istHost }) => {
  laeufeStoppen();
  uhrStopp();
  becherZuruecksetzen();
  const hatteGesetzt = zustand.gesetzt;
  zustand.wettenOffen = false;
  zustand.gesetzt = false;
  zustand.gewaehlt = 0;
  $("buehne").classList.remove("kann-waehlen");
  Nui.hide($("wette"));
  Nui.hide($("zinken-fenster"));
  Nui.hide($("ergebnis"));
  hostKnoepfe(false);
  wettenZeigen([]);
  status(istHost ? "Runde gestoppt, alle Einsätze sind zurück." : hatteGesetzt ? "Der Host hat die Runde gestoppt. Dein Einsatz ist zurück." : "Der Host hat die Runde gestoppt.");
});

Nui.on("hinweis", ({ text, typ }) => {
  const art = typ === "error" ? "danger" : typ === "success" ? "ok" : "";
  Nui.toast("Becherspiel", text, art);
});

Nui.on("kamera", ({ maus }) => {
  document.body.classList.toggle("is-kamera", !maus);
  $("kamera-hinweis").innerHTML = `<kbd>${zustand.taste}</kbd> zurück zur Maus`;
  $("kamera-hinweis").hidden = maus;
});

Nui.on("schliessen", () => {
  laeufeStoppen();
  uhrStopp();
  document.body.classList.remove("is-kamera");
  document.querySelectorAll("[data-nui-root]").forEach((el) => (el.hidden = true));
});

// ---------------------------------------------------------------- Vorschau im Browser

if (!Nui.inGame) {
  const senden = (action, d = {}) => window.postMessage({ action, ...d }, "*");
  const ansicht = new URLSearchParams(location.search).get("ansicht") || "aufbau";
  const tisch = (istHost) =>
    senden("tisch", { daten: { istHost, ich: 7, hostName: "Marco Rossi", stufe: "Mittel", gezinkt: istHost, zustand: "frei", min: 10, taste: "F6" } });
  const leute = [{ name: "Lena Vogt" }, { name: "Can Yilmaz" }, { name: "Jonas Becker" }];

  setTimeout(() => {
    if (ansicht === "aufbau") {
      senden("aufbau", { stufen: [{ id: "leicht", name: "Leicht", tausche: 5 }, { id: "mittel", name: "Mittel", tausche: 9 }, { id: "schwer", name: "Schwer", tausche: 14 }], zinken: false });
    } else if (ansicht === "spieler") {
      tisch(false);
      senden("zeigen", { ball: 2, dauer: 1200 });
      setTimeout(() => senden("mischen", { tausche: [[1, 2], [2, 3], [1, 3]], dauer: 300 }), 1300);
      setTimeout(() => senden("wettenAuf", { sekunden: 15, istHost: false }), 2400);
      setTimeout(() => {
        document.querySelectorAll(".bs-becher")[0].click();
        $("betrag").value = 250;
        setzenPruefen();
      }, 2600);
    } else if (ansicht === "host") {
      tisch(true);
      senden("spieler", { liste: leute });
      senden("hinweis", { text: "Can Yilmaz ist dazugekommen." });
    } else if (ansicht === "hostwetten") {
      tisch(true);
      senden("einsatz", { betrag: 100, fest: false });
      senden("zeigen", { ball: 2, dauer: 50 });
      setTimeout(() => senden("wettenAuf", { sekunden: 15, istHost: true }), 100);
      setTimeout(() => senden("wettenListe", { liste: [{ id: 2, name: "Lena Vogt", becher: 1, betrag: 500 }, { id: 3, name: "Can Yilmaz", becher: 3, betrag: 100 }, { id: 4, name: "Jonas Becker", becher: 1, betrag: 250 }] }), 150);
      senden("spieler", { liste: [{ name: "Lena Vogt", becher: 1, betrag: 500 }, { name: "Can Yilmaz", becher: 3, betrag: 500 }, { name: "Jonas Becker" }] });
    } else if (ansicht === "festspieler") {
      tisch(false);
      senden("einsatz", { betrag: 250, fest: false });
      senden("wettenAuf", { sekunden: 15, istHost: false });
      senden("wettenListe", { liste: [{ id: 2, name: "Lena Vogt", becher: 1, betrag: 500, gesetzt: true }, { id: 7, name: "Max Muster", becher: 2, betrag: 0, gesetzt: false }, { id: 4, name: "Jonas Becker", becher: 1, betrag: 0, gesetzt: false }, { id: 5, name: "Anni Kowalski", becher: 3, betrag: 1200, gesetzt: true }] });
    } else if (ansicht === "zinken") {
      tisch(true);
      senden("spieler", { liste: leute });
      senden("wettenZu");
      senden("zinken", { wetten: [{ name: "Lena Vogt", becher: 1, betrag: 500 }, { name: "Can Yilmaz", becher: 3, betrag: 120 }], ehrlich: 1, sekunden: 10 });
    } else if (ansicht === "ergebnis") {
      tisch(false);
      senden("aufdecken", { becher: 3, ergebnisse: [{ id: 2, name: "Lena Vogt", gewonnen: true, betrag: 250 }], istHost: false });
    } else if (ansicht === "hostergebnis") {
      tisch(true);
      senden("spieler", { liste: leute });
      senden("aufdecken", { becher: 2, ergebnisse: [{ name: "Lena Vogt", gewonnen: false, betrag: 500 }, { name: "Can Yilmaz", gewonnen: true, betrag: 120 }], istHost: true });
    } else if (ansicht === "kamera") {
      tisch(false);
      senden("kamera", { maus: false });
    }
  }, 50);
}
