# Snowglobe ❄

Leichter Schneefall direkt auf dem Windows-Desktop.

- Schnee fällt über alle Monitore, mit Wind, sanftem Schaukeln und Tiefenwirkung (kleine Flocken sind fern und langsam, große nah und schneller).
- Er bleibt **auf Fensteroberkanten** (nicht maximierte Fenster), **auf Desktop-Icons** und **am Bildschirmboden** liegen und rutscht natürlich ab.
- Verdeckte Stellen werden ausgeblendet. Wird ein Fenster verschoben, minimiert oder geschlossen, **fällt sein Schnee herunter**.
- Bei Vollbild-Apps (Videos, Spiele, Präsentationen) blendet sich der Schnee auf dem betroffenen Monitor automatisch aus.
- Durchklickbar, erscheint nicht in der Taskleiste und nicht in Alt+Tab.

## Bedienung

Tray-Icon (Schneekugel) → Rechtsklick:

| Menüpunkt | |
|---|---|
| Schnee: Leicht / Mittel / Stark | Intensität |
| Schnee abschütteln | Alles Liegengebliebene fällt herunter |
| Pausieren | Auch per Doppelklick aufs Tray-Icon |
| In Screenshots/Bildschirmfreigabe ausblenden | Standard: an (Kolleg:innen in Teams sehen keinen Schnee) |
| Mit Windows starten | Eintrag unter `HKCU\...\Run` |
| Beenden | |

Einstellungen liegen unter `HKCU\Software\Snowglobe`.

## Bauen

Kein SDK nötig, der C#-Compiler von .NET Framework 4 ist in Windows enthalten:

```bat
build.cmd
```

Ergebnis: `bin\Snowglobe.exe` (eine Datei, ca. 40 KB).

Debug-Schalter: `--capture` (für einen Lauf in Screenshots sichtbar), `--fast` (Schnee bleibt 8× schneller liegen), `--dump` (schreibt Monitore und Icon-Positionen in `snowglobe-dump.txt`).

## Technik

- Ein Layered Window pro Monitor (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST`), per `UpdateLayeredWindow` mit eigenem Alpha pro Pixel. Läuft mit ca. 30 FPS, Per-Monitor-DPI-aware.
- Flocken sind vorgerenderte Sprites mit Subpixel-Positionen (4×4) und einem dezenten Schatten, damit sie auch auf hellen Fenstern sichtbar sind.
- Schneehaufen sind Höhenprofile pro Pixelspalte mit Böschungswinkel und langsamem Tauen.
- Fenster werden etwa 10× pro Sekunde per `EnumWindows` in Z-Reihenfolge gelesen, versteckte Fenster (DWM cloaked) werden ignoriert. Desktop-Icons kommen per `LVM_GETITEMRECT` aus der Explorer-ListView.
- Es wird nur dort gelöscht, wo im letzten Frame gezeichnet wurde (Dirty Rects).
