# Snowglobe ❄

**Eine kleine Schneekugel für deinen Desktop.** Leise fallen Flocken über deine Monitore, bleiben auf Fenstern und Icons liegen und häufen sich zu weichen Schneewehen. Du arbeitest ganz normal darunter weiter. Und wenn es zu viel wird, greifst du zur Schaufel oder schickst den kleinen Schneeroboter los. ☕

## Download & Start

1. **[Snowglobe.exe herunterladen](https://github.com/JRSDev-at/snowglobe/releases/latest/download/Snowglobe.exe)** (eine einzige Datei, keine Installation)
2. Am besten in einen eigenen Ordner legen, zum Beispiel `%LOCALAPPDATA%\Snowglobe\`, und per Doppelklick starten.
3. Es öffnet sich kein Fenster: Unten rechts im Tray erscheint eine **Schneekugel**. Über sie (Rechtsklick) steuerst du alles. Mit **„Mit Windows starten“** schneit es nach jedem Login automatisch.

**Windows warnt beim ersten Start?** Die EXE ist nicht digital signiert, deshalb zeigt SmartScreen „Der Computer wurde durch Windows geschützt“. Auf **„Weitere Informationen“ → „Trotzdem ausführen“** klicken. Wer sichergehen will: Zu jedem Release gibt es eine `Snowglobe.exe.sha256` mit der Prüfsumme, und der komplette Quellcode liegt hier im Repo.

**Voraussetzungen:** Windows 10 oder 11. Sonst nichts, .NET Framework 4 und DirectX sind bereits in Windows enthalten.

**Updates** kommen automatisch: Snowglobe schaut gelegentlich nach einer neuen Version, prüft sie und startet sich kurz neu. Abschalten lässt sich das im Tray-Menü unter *Updates*.

## Was es kann

- Schnee fällt über alle Monitore, mit Wind, sanftem Schaukeln und Tiefenwirkung (kleine Flocken sind fern und langsam, große nah und schneller).
- Er bleibt **auf Fensteroberkanten** (auch auf Popups und Menüs, passend zu den abgerundeten Ecken), **auf Desktop-Icons** und **am Bildschirmboden** liegen und rutscht natürlich ab.
- Wird ein Fenster verschoben, minimiert oder geschlossen, **bröckelt sein Schnee ab**, in verschieden großen Klumpen. Bewegt es sich nur nach oben oder unten, **fährt der Schnee mit**.
- Klappt ein Fenster oder Popup **über liegendem Schnee** auf, wird der Schnee **auf seine Oberkante geschoben**.
- Bei Vollbild-Apps (Videos, Spiele, Präsentationen) blendet sich der Schnee auf dem betroffenen Monitor aus.
- Durchklickbar, nicht in der Taskleiste, nicht in Alt+Tab und standardmäßig **unsichtbar in Screenshots und Bildschirmfreigaben**.
- Sparsam: etwa 6–7 % eines CPU-Kerns auf vier Full-HD-Monitoren.

### Schaufel
Tray-Menü → **Schaufel bereitstellen**: Unten rechts am Hauptmonitor steckt eine Schaufel im Schnee.
- **Linksklick** nimmt sie in die Hand, mit etwas Schneegestöber.
- In der Hand ist sie **sofort aktiv, ohne Taste**: Fährst du mit der Maus durch den Schnee, schiebt sie ihn vor sich her. **Hochziehen** nimmt eine Ladung aufs Blatt, eine **schwungvolle Bewegung** wirft sie davon, auch auf andere Fenster.
- Die Schaufel bewegt sich mit: Sie schabt über den Boden, kippt beim Hochziehen nach hinten, schwingt beim Tragen nach und schnellt beim Wurf hoch.
- **Kurzer Linksklick** sticht sie an dieser Stelle in den Schnee, auch auf ein Fenster. Verschwindet das Fenster, fällt sie auf die nächste Fläche darunter.
- **Rechtsklick** bringt sie zurück an ihren Platz. Solange du sie hältst, gehen Linksklicks nicht an andere Programme.

### Schneeroboter
Ein kleiner Räumroboter, der liegenden Schnee **an den Bildschirmrand schiebt**. Dort bleibt nur ein **kleiner Hügel**, der Rest wird **aus dem Bild hinausgeschoben**, damit nichts verdeckt wird. Von Fenstern schiebt er den Schnee über die Kanten.
- **Anklicken:** Er räumt sofort die Fläche, auf der er steht.
- **Hinschicken (Strg+Alt+R)**, dann auf eine Stelle klicken: Er fährt hin und räumt dort, auch auf anderen Monitoren.
- **Automatik:** Er räumt selbstständig, wo am meisten Schnee liegt, **auf allen Monitoren**.
- **Monitorwechsel:** Neben ihm wächst ein Rohr aus dem Boden, er fährt hinein und kommt auf dem anderen Monitor aus einem Rohr wieder heraus.

### Tray-Menü

| Menüpunkt | |
|---|---|
| Schnee: Leicht / Mittel / Stark | Intensität |
| Schaufel bereitstellen | siehe oben |
| Schneeroboter ▸ Hinschicken / Anzeigen / Automatik | siehe oben |
| Schnee abschütteln | Alles Liegengebliebene fällt herunter |
| Im Hintergrund (hinter Fenstern) | Schnee nur auf dem Desktop, hinter allen Fenstern |
| Pausieren | Auch per Doppelklick aufs Tray-Icon |
| In Screenshots/Bildschirmfreigabe ausblenden | Standard: an (in Teams & Co. sieht niemand den Schnee) |
| Mit Windows starten | Autostart beim Login |
| Updates ▸ Automatisch aktualisieren / Jetzt suchen | |

Einstellungen liegen unter `HKCU\Software\Snowglobe`. **Deinstallieren:** Im Tray „Mit Windows starten“ abhaken, beenden und die EXE löschen.

---

## Für Entwickler

### Bauen
Kein SDK nötig, der C#-Compiler von .NET Framework 4 ist in Windows enthalten:

```bat
build.cmd
```

Ergebnis: `bin\Snowglobe.exe`.

Debug-Schalter:
- `--selftest`: prüft Popup, Schaufel, Roboter und Röhre rechnerisch → `selftest.txt`
- `--perf`: schreibt Zeitmessungen nach `perf.txt`
- `--nogpu`: erzwingt die Ausgabe per `UpdateLayeredWindow`
- `--capture`: für einen Lauf in Screenshots sichtbar
- `--fast`: Schnee bleibt 8× schneller liegen
- `--auto`: Roboter-Automatik für einen Lauf an
- `--dump`: Monitore und Icon-Positionen → `snowglobe-dump.txt`

### Release veröffentlichen
1. In `src/Snowglobe.cs` die Versionsnummer erhöhen, z. B. `[assembly: AssemblyVersion("1.4.0.0")]`.
2. Auf `main` pushen.
3. Die GitHub Action (`.github/workflows/release.yml`) baut die EXE, führt den Selbsttest aus und erstellt das Release `v1.4.0` mit EXE und Prüfsumme. Ohne neue Versionsnummer passiert nichts.

Installierte Snowglobes prüfen eine Minute nach dem Start und danach alle 6 Stunden `releases/latest`, laden die neue EXE, prüfen **SHA-256** und Version, ersetzen die laufende Datei und starten neu.

### Technik
- **Ausgabe über die GPU:** pro Monitor ein durchklickbares Fenster mit DirectComposition-Swapchain (Direct3D 11, vorab multipliziertes Alpha). Gezeichnet wird auf der CPU; pro Frame werden nur die geänderten Rechtecke in einen Ring aus 3 Staging-Texturen kopiert und von der GPU in die Swapchain übertragen. Fallback: `UpdateLayeredWindow`.
- Flocken sind vorgerenderte Sprites mit Subpixel-Positionen (4×4) und einem dezenten Schatten.
- Schneehaufen sind Höhenprofile pro Pixelspalte mit Böschungswinkel und langsamem Tauen. Schaufel und Roboter nutzen dieselbe Schneepflug-Funktion.
- Fenster werden etwa 10× pro Sekunde per `EnumWindows` in Z-Reihenfolge gelesen, Desktop-Icons per `LVM_GETITEMRECT` aus der Explorer-ListView.
- Klicks auf Roboter und Schaufel erkennt ein Low-Level-Maushook auf einem eigenen Thread. Er ist nur aktiv, solange Roboter oder Schaufel sichtbar sind, und lässt Mausbewegungen immer durch.

## Lizenz
Copyright © 2026 JRSDev-at. Snowglobe ist freie Software unter der [GNU General Public License v3.0](LICENSE) (oder einer späteren Version): Du darfst es nutzen, verändern und weitergeben. Veränderte Versionen müssen ebenfalls unter der GPL und mit Quellcode weitergegeben werden. Ohne jede Gewährleistung.
