# Snowglobe ❄

Leichter Schneefall direkt auf dem Windows-Desktop, mit Schaufel und Schneeroboter.

- Schnee fällt über alle Monitore, mit Wind, sanftem Schaukeln und Tiefenwirkung (kleine Flocken sind fern und langsam, große nah und schneller).
- Er bleibt **auf Fensteroberkanten** (auch auf Popups und Menüs), **auf Desktop-Icons** und **am Bildschirmboden** liegen und rutscht natürlich ab.
- Verdeckte Stellen werden ausgeblendet. Wird ein Fenster seitlich verschoben, minimiert oder geschlossen, **fällt sein Schnee herunter**. Bewegt es sich nur nach oben oder unten (z. B. ein aufklappendes Popup), **fährt der Schnee mit**.
- Klappt ein Fenster oder Popup **über liegendem Schnee** auf, wird der Schnee **auf seine Oberkante nach oben geschoben**. Schließt es, fällt er wieder herunter.
- Bei Vollbild-Apps (Videos, Spiele, Präsentationen) blendet sich der Schnee auf dem betroffenen Monitor automatisch aus.
- Durchklickbar, erscheint nicht in der Taskleiste und nicht in Alt+Tab.

## Werkzeuge

### Schaufel (Strg+Alt+S)
Der Mauszeiger wird zur Schaufel:
- **Ziehen** mit gedrückter linker Maustaste schiebt Schnee wie ein Schneepflug.
- **Hochziehen** nimmt eine Ladung Schnee auf das Blatt, **Loslassen** wirft sie in Bewegungsrichtung ab, auch auf andere Fenster.
- **Rechtsklick** oder nochmal **Strg+Alt+S** beendet die Schaufel. Solange sie aktiv ist, gehen Linksklicks nicht an andere Programme.

### Schneeroboter
Ein kleiner Räumroboter mit Schild fährt auf dem Boden oder auf Fenstern herum.
- **Roboter anklicken:** Er räumt den Schnee seiner Fläche zu einem Haufen an seiner Position. Auf Fenstern schiebt er ihn über die Kante.
- **Hinschicken (Strg+Alt+R)** und danach auf eine Stelle klicken: Er springt hin und häuft dort an. Auf einen anderen Monitor fällt er von oben herein.
- **Automatik:** Er räumt selbstständig die Fläche mit dem meisten Schnee. Der Boden-Haufen landet links unten.
- Fällt sein Fenster weg, fällt er mit herunter.

## Bedienung

Tray-Icon (Schneekugel) → Rechtsklick:

| Menüpunkt | |
|---|---|
| Schnee: Leicht / Mittel / Stark | Intensität |
| Schaufel | siehe oben |
| Schneeroboter ▸ Hinschicken / Anzeigen / Automatik | siehe oben |
| Schnee abschütteln | Alles Liegengebliebene fällt herunter |
| Im Hintergrund (hinter Fenstern) | Schnee nur auf dem Desktop, hinter allen Fenstern. Ist ein Monitor komplett von einem maximierten Fenster verdeckt, pausiert er dort und spart CPU. Werkzeuge holen ihn kurz nach vorne. |
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

Ergebnis: `bin\Snowglobe.exe` (eine Datei).

Debug-Schalter:
- `--capture`: für einen Lauf in Screenshots sichtbar
- `--fast`: Schnee bleibt 8× schneller liegen
- `--auto`: Roboter-Automatik für einen Lauf an
- `--dump`: schreibt Monitore und Icon-Positionen in `snowglobe-dump.txt`

## Technik

- Ein Layered Window pro Monitor (`WS_EX_LAYERED | WS_EX_TRANSPARENT`), per `UpdateLayeredWindow` mit eigenem Alpha pro Pixel. Läuft mit ca. 30 FPS, Per-Monitor-DPI-aware. Im Hintergrund-Modus wird das Fenster direkt über dem Desktop-Fenster einsortiert.
- Flocken sind vorgerenderte Sprites mit Subpixel-Positionen (4×4) und einem dezenten Schatten.
- Schneehaufen sind Höhenprofile pro Pixelspalte mit Böschungswinkel und langsamem Tauen. Schaufel und Roboter nutzen dieselbe Schneepflug-Funktion.
- Fenster werden etwa 10× pro Sekunde per `EnumWindows` in Z-Reihenfolge gelesen. Desktop-Icons kommen per `LVM_GETITEMRECT` aus der Explorer-ListView.
- Klicks auf Roboter und Schaufel erkennt ein Low-Level-Maushook auf einem **eigenen Thread**, damit der Mauszeiger nie auf das Rendering wartet. Er läuft nur, solange Roboter oder Werkzeug aktiv sind, und lässt Mausbewegungen immer unangetastet durch.
