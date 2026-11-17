# Snowglobe ❄

Leichter Schneefall direkt auf dem Windows-Desktop, mit Schaufel, Schneeroboter und automatischen Updates.

- Schnee fällt über alle Monitore, mit Wind, sanftem Schaukeln und Tiefenwirkung (kleine Flocken sind fern und langsam, große nah und schneller).
- Er bleibt **auf Fensteroberkanten** (auch auf Popups und Menüs, passend zu den abgerundeten Ecken), **auf Desktop-Icons** und **am Bildschirmboden** liegen und rutscht natürlich ab.
- Bei Popups mit unsichtbarem Rand wird die tatsächlich sichtbare Kante am Bildschirm nachgemessen, damit kein Schnee in der Luft schwebt.
- Verdeckte Stellen werden ausgeblendet. Wird ein Fenster seitlich verschoben, minimiert oder geschlossen, **bröckelt sein Schnee ab**: oben zuerst, in verschieden großen Klumpen. Bewegt es sich nur nach oben oder unten, **fährt der Schnee mit**.
- Klappt ein Fenster oder Popup **über liegendem Schnee** auf, wird der Schnee **auf seine Oberkante nach oben geschoben**.
- Bei Vollbild-Apps (Videos, Spiele, Präsentationen) blendet sich der Schnee auf dem betroffenen Monitor automatisch aus.
- Durchklickbar, erscheint nicht in der Taskleiste und nicht in Alt+Tab.

## Schaufel
Tray-Menü → **Schaufel bereitstellen**: Unten rechts am Hauptmonitor steckt eine Schaufel im Schnee.
- **Linksklick** (großzügige Klickfläche) nimmt sie in die Hand, mit etwas Schneegestöber.
- In der Hand ist sie **sofort aktiv, ohne Taste**: Fährst du mit der Maus durch den Schnee, schiebt sie ihn wie ein Schneepflug. **Hochziehen** nimmt eine Ladung aufs Blatt, eine **schwungvolle Bewegung** wirft sie in diese Richtung ab, auch auf andere Fenster. (Mit gedrückter Taste ziehen und loslassen wirft ebenfalls.)
- Die Schaufel bewegt sich mit: Sie schabt beim Schieben über den Boden, kippt beim Hochziehen nach hinten, schwingt beim Tragen nach und schnellt beim Wurf hoch.
- **Kurzer Linksklick** (ohne Ziehen) sticht die Schaufel an dieser Stelle in den Schnee, auch auf ein Fenster. Verschwindet das Fenster, fällt sie auf die nächste Fläche darunter.
- **Rechtsklick** bringt sie zurück an ihren Platz unten rechts. Solange du sie hältst, gehen Linksklicks nicht an andere Programme.

## Schneeroboter
Ein kleiner Räumroboter. Seine Aufgabe: liegenden Schnee **an den Bildschirmrand schieben**. Am Boden schiebt er die linke Hälfte nach links und die rechte nach rechts. Am Rand bleibt nur ein **kleiner Hügel** (höchstens 28 px), alles darüber schiebt er **aus dem Bild hinaus**, damit nichts verdeckt wird. Auch alte, zu große Randhaufen stutzt er so zurecht. Von Fenstern schiebt er den Schnee über die Kanten.
- **Anklicken:** Er räumt sofort die Fläche, auf der er steht.
- **Hinschicken (Strg+Alt+R)**, dann auf eine Stelle klicken: Er fährt hin und räumt dort, auch auf anderen Monitoren.
- **Automatik:** Er räumt selbstständig die Fläche mit dem meisten Schnee, **auf allen Monitoren**.
- **Monitorwechsel:** Neben ihm wächst ein Rohr aus dem Boden, er fährt hinein, und auf dem anderen Monitor kommt er aus einem Rohr wieder heraus.

## Bedienung

Tray-Icon (Schneekugel) → Rechtsklick:

| Menüpunkt | |
|---|---|
| Schnee: Leicht / Mittel / Stark | Intensität |
| Schaufel bereitstellen | siehe oben |
| Schneeroboter ▸ Hinschicken / Anzeigen / Automatik | siehe oben |
| Schnee abschütteln | Alles Liegengebliebene fällt herunter |
| Im Hintergrund (hinter Fenstern) | Schnee nur auf dem Desktop, hinter allen Fenstern. Ist ein Monitor komplett von einem maximierten Fenster verdeckt, pausiert er dort. |
| Pausieren | Auch per Doppelklick aufs Tray-Icon |
| In Screenshots/Bildschirmfreigabe ausblenden | Standard: an (Kolleg:innen in Teams sehen keinen Schnee) |
| Mit Windows starten | Eintrag unter `HKCU\...\Run` |
| Updates ▸ Automatisch aktualisieren / Jetzt suchen | siehe unten |

Einstellungen liegen unter `HKCU\Software\Snowglobe`.

## Updates

Snowglobe prüft eine Minute nach dem Start und danach alle 6 Stunden die GitHub-Releases von `JRSDev-at/snowglobe`. Bei einer neueren Version wird `Snowglobe.exe` heruntergeladen, die **SHA-256-Prüfsumme** und die Versionsnummer werden geprüft, die laufende Datei wird ersetzt und das Programm neu gestartet.

**Voraussetzung:** Das Repository muss **öffentlich** sein, sonst kommen andere nicht an die Releases.

**Ein Update veröffentlichen:**
1. In `src/Snowglobe.cs` die Versionsnummer erhöhen: `[assembly: AssemblyVersion("1.3.0.0")]`.
2. Auf `main` pushen.
3. Die GitHub Action (`.github/workflows/release.yml`) baut die EXE, führt den Selbsttest aus und erstellt das Release `v1.3.0` mit EXE und Prüfsumme. Ohne neue Versionsnummer passiert nichts.

## Bauen

Kein SDK nötig, der C#-Compiler von .NET Framework 4 ist in Windows enthalten:

```bat
build.cmd
```

Ergebnis: `bin\Snowglobe.exe` (eine Datei).

Debug-Schalter:
- `--selftest`: prüft Popup, Schaufel, Roboter und Röhre rechnerisch → `selftest.txt`
- `--perf`: schreibt Zeitmessungen nach `perf.txt`
- `--nogpu`: erzwingt die alte Ausgabe per `UpdateLayeredWindow`
- `--capture`: für einen Lauf in Screenshots sichtbar
- `--fast`: Schnee bleibt 8× schneller liegen
- `--auto`: Roboter-Automatik für einen Lauf an
- `--dump`: Monitore und Icon-Positionen → `snowglobe-dump.txt`

## Technik

- **Ausgabe über die GPU:** pro Monitor ein durchklickbares Fenster mit DirectComposition-Swapchain (Direct3D 11, vorab multipliziertes Alpha). Gezeichnet wird auf der CPU in einen Puffer. Pro Frame werden nur die geänderten Rechtecke in einen Ring aus 3 Staging-Texturen kopiert, die GPU kopiert sie dann in die Swapchain. Klappt das nicht, fällt das Programm automatisch auf `UpdateLayeredWindow` zurück.
- Gemessen auf 4 × 1080p: ca. 6–7 % **eines** CPU-Kerns (vorher 22 %).
- Flocken sind vorgerenderte Sprites mit Subpixel-Positionen (4×4) und einem dezenten Schatten.
- Schneehaufen sind Höhenprofile pro Pixelspalte mit Böschungswinkel und langsamem Tauen. Schaufel und Roboter nutzen dieselbe Schneepflug-Funktion.
- Fenster werden etwa 10× pro Sekunde per `EnumWindows` in Z-Reihenfolge gelesen. Desktop-Icons kommen per `LVM_GETITEMRECT` aus der Explorer-ListView.
- Klicks auf Roboter und Schaufel erkennt ein Low-Level-Maushook auf einem **eigenen Thread**. Er ist nur aktiv, solange Roboter oder Schaufel sichtbar sind, und lässt Mausbewegungen immer durch.
