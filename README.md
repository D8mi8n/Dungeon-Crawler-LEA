# LEA – Die vergessene Krypta

Ein spielbarer 2D-Dungeon-Crawler auf Basis der vorhandenen Dungeon-Karte und Skelett-Animationen. Entwickelt mit **Unity 6000.3.11f1**.

## Spielen

Die Windows-Version liegt in `Builds/Windows`. **LEA.exe** startet das Spiel. Der gesamte Ordner muss zusammenbleiben; zum Weitergeben den vollständigen Ordner kopieren oder die ZIP-Datei verwenden.

Im Unity Editor die Szene **Assets/Scenes/LEA.unity** öffnen und Play drücken. Im Hauptmenü „Krypta betreten“ oder Eingabe wählen.

| Eingabe | Aktion |
| --- | --- |
| WASD / Pfeiltasten | Bewegen und Blickrichtung bestimmen |
| Umschalt | Sprinten |
| Leertaste / linke Maustaste | In Blickrichtung angreifen |
| E | Nahe Siegeltruhe öffnen / Nordtor benutzen |
| Esc | Pause / fortsetzen |
| Eingabe | Im Hauptmenü starten, nach Sieg oder Niederlage neu starten |

## Spielziel

Besiege die drei Wächter und öffne ihre Siegeltruhen. Jedes Siegel stellt bis zu zwei Lebenspunkte wieder her. Sammle alle drei Seelensiegel und benutze das Nordtor. Zusätzliche Gegner und Münzen zählen zur Abschlussstatistik. Heiltränke werden beim Berühren aufgenommen, sobald Lebenspunkte fehlen.

Gegner kündigen ihre Angriffe farblich an. Nach einem Treffer ist der Spieler kurz unverwundbar. Angriffe treffen in Blickrichtung und werden von Wänden blockiert. Die Fallen warnen gelb, bevor sie rot aktiv werden. Pause friert das Spiel ein; beim Wechsel in ein anderes Fenster wird automatisch pausiert.

## Umfang

- Bestehende Dungeon-Karte mit geschlossenem Spielbereich
- Spielerbewegung, Sprint, Nahkampf, Schaden, Heilung und Tod
- Normale Gegner und drei Wächter mit Angriffsankündigung
- Drei Siegeltruhen, Heiltränke, Fallen und verriegelter Ausgang
- Deutsches Hauptmenü, HUD, Pause, Sieg und Niederlage
- GUI mit den vorhandenen Pixel-Art-Sprites aus `Assets/Sprites/PNG_UI`: Pergamentfenster, grüne Schaltflächen mit Hover-/Klickzustand, Charakterrahmen, Aktionsleiste und Symbole
- Vollständiger Neustart mit zurückgesetzten Gegnern, Truhen und Werten
- Kurze Soundeffekte und gespeicherte Stummschaltung
- Lokale Windows-Version ohne zusätzliche Installation

## Projekt und Builds

Die ursprünglichen Szenen `Dungeon`, `TestMap` und `TestDamian` bleiben als Ausgangsmaterial erhalten. Die fertige Spielszene heißt **LEA** und ist die einzige aktivierte Build-Szene.

Die GUI-Sprites sind am Objekt **LEA Spielsteuerung → DungeonHUD** im Inspector zugewiesen. **LEA → GUI-Sprites zuweisen** aktualisiert diese Referenzen in der fertigen Szene. Die Oberfläche skaliert für andere Fenstergrößen; die Ränder werden in neun Teilen gezeichnet, damit die Pixelrahmen erhalten bleiben.

Über **LEA → Windows-Version bauen** wird die Windows-Version aus der Spielszene erstellt. Das Editor-Werkzeug kann die Spielszene auch über **LEA → Spielszene vorbereiten** aus `Dungeon` und den vorhandenen Grafiken neu erzeugen. **Achtung:** Beim Vorbereiten werden manuelle Änderungen an `LEA.unity` und den erzeugten LEA-Animator-Controllern ersetzt. Eigene Varianten vorher unter einem anderen Namen speichern.

Wichtige Dateien:

- `Assets/Scripts/DungeonGame.cs`: Spielzustand, Punkte, Siegel, Sound und Neustart
- `Assets/Scripts/PlayerController.cs`: Spieler und Nahkampf
- `Assets/Scripts/EnemyController.cs`: Gegnerverhalten
- `Assets/Scripts/DungeonInteractable.cs`: Truhen, Heiltränke, Münzen und Tor
- `Assets/Scripts/DungeonHUD.cs`: Oberfläche
- `Assets/Editor/DungeonBuild.cs`: reproduzierbare Szenenerstellung und Windows-Build
- `Assets/Editor/DungeonValidation.cs`: automatisierter Test des Spielablaufs

Der automatisierte Test läuft mit Unity im Batch-Modus und `-executeMethod DungeonValidation.Run` **ohne** `-quit`. Er beendet Unity selbst mit einem passenden Exitcode. Das Ergebnis liegt in `Artifacts/validation-results.txt`.

Grafiken und Animationen stammen aus dem vorhandenen Projekt. Bestehende Nutzungsbedingungen dieser Assets gelten weiterhin.
