# LEA – Die vergessene Krypta

Ein 2D-Dungeon-Crawler in Pixel-Art-Optik mit drei spielbaren Leveln, Nahkampf, bewachten Siegeltruhen und animierten Fallen. Das Projekt verwendet die vorhandenen Umgebungssprites, Charakteranimationen und Prefabs.

**Engine:** Unity 6000.3.11f1 · **Darstellung:** URP 2D · **Zielplattform:** Windows 64-Bit · **Sprache:** Deutsch

## Projekt starten

1. Den Ordner `Dungeon Crawler LEA` in Unity Hub als Projekt hinzufügen und mit **Unity 6000.3.11f1** öffnen.
2. Den Import der Assets und Pakete abwarten.
3. `Assets/Scenes/LEA.unity` öffnen und **Play** drücken.
4. Im Hauptmenü ein Level auswählen und mit **„Krypta betreten“** oder **Eingabe** starten. Der Startknopf startet das jeweils ausgewählte Level.

Auch `LEA_Zisterne.unity` und `LEA_Grabkammern.unity` können direkt im Editor geöffnet werden. Alle drei Szenen sind in den Build-Einstellungen aktiviert.

Eine lokal erstellte Windows-Version wird über `Builds/Windows/LEA.exe` gestartet. Zum Weitergeben muss der gesamte Windows-Ordner zusammenbleiben. `Builds/` ist von Git ausgeschlossen und deshalb nach einem frischen Klonen möglicherweise nicht vorhanden.

## Level

| Szene | Level | Umgebung und Besonderheiten |
| --- | --- | --- |
| `LEA.unity` | Krypta | Kompakte Stein- und Wasserkulisse mit Säulen, Statuen und Kerzen; feste Kamera und Bodenfallen. |
| `LEA_Zisterne.unity` | Zisterne | Wasserbecken, Stege, Wasserpfeiler und beleuchtete Kammern; mitlaufende Kamera und Bodenfallen. |
| `LEA_Grabkammern.unity` | Grabkammern | Ausgedehnte Hallen und Durchgänge mit Statuen, Schädeln, Kerzen und Fackeln; mitlaufende Kamera, Bodenfallen und drei Drachenfallen. |

Die Level sind im Hauptmenü frei auswählbar. Nach einem Sieg führt **„Nächstes Level“** von der Krypta in die Zisterne und anschließend in die Grabkammern. Nach dem letzten Level kann dieses erneut gespielt werden.

Alle drei Level verwenden dasselbe Ausgangstor auf Basis von `Door.prefab`. Es zeigt den geschlossenen beziehungsweise geöffneten Zustand passend zur Anzahl der eingesammelten Siegel.

## Steuerung

| Eingabe | Aktion |
| --- | --- |
| WASD / Pfeiltasten | Bewegen und Blickrichtung bestimmen |
| Umschalt | Sprinten |
| Leertaste | In Blickrichtung angreifen |
| Linke Maustaste | Zur Mausposition ausrichten und angreifen; Ausrichtung in vier Richtungen |
| E | Nahe Siegeltruhe öffnen oder Ausgangstor benutzen |
| Esc | Pause / fortsetzen |
| Eingabe | Im Hauptmenü starten; nach einem Sieg zum nächsten Level; nach Niederlage oder dem letzten Level neu starten |

Jeder Angriff benötigt einen neuen Tastendruck oder Mausklick. Während eines Angriffs bleibt der Spieler stehen. Die Pause kann auch über den HUD-Knopf geöffnet werden. Beim Wechsel in ein anderes Fenster pausiert das laufende Spiel automatisch.

## Spielziel und Regeln

Besiege die drei Wächter und öffne ihre Siegeltruhen mit **E**. Jede Truhe bleibt gesperrt, solange ihr Wächter lebt. Ein eingesammeltes Seelensiegel heilt bis zu zwei Lebenspunkte. Sobald alle drei Siegel gesammelt sind, öffnet sich das Ausgangstor sichtbar. Benutze es in der Nähe mit **E**, um das Level abzuschließen.

Weitere Gegner müssen für den Sieg nicht besiegt werden. Besiegte Gegner und gesammelte Münzen zählen zur Abschlussstatistik; dort wird auch die benötigte Zeit angezeigt. Heiltränke werden in der Nähe automatisch aufgenommen, wenn Lebenspunkte fehlen.

Gegner kündigen ihre Angriffe farblich an und zeigen ihre Lebensbalken über dem Kopf. Wände auf der Kollisionsschicht `DungeonWalls` blockieren Nahkampftreffer und Interaktionen. Nach einem Treffer ist der Spieler kurz unverwundbar und blinkt. Bei null Lebenspunkten endet der Versuch.

**„Neu starten“** lädt das aktuelle Level erneut. Leben, Siegel, Münzen, Gegner, Truhen und Laufzeit werden zurückgesetzt. Auch beim Wechsel zum nächsten Level beginnt ein neuer Durchlauf mit vollen Lebenspunkten. Es gibt keinen gespeicherten Spielfortschritt; lediglich die Toneinstellung bleibt gespeichert.

## Fallen

**Bodenfallen** verwenden `DungeonTrap.cs`. Auf eine Ruhephase folgt eine gelbe Warnung, anschließend eine rote Schadensphase. In der aktiven Phase verursachen sie bei ausreichender Nähe einen Schadenspunkt.

**Drachenfallen** sind in der Grabkammer an den drei Objekten `Dragon_Trap_S`, `Dragon_Trap_S (1)` und `Dragon_Trap_S (2)` eingerichtet. `DragonTrapDamage.cs` ordnet jedem Bild der bestehenden Flammenanimation einen eigenen Schadensbereich zu:

- Die Hitbox verlängert und verkürzt sich mit der sichtbaren Flamme.
- Phasen ohne aktive Flamme verursachen keinen Schaden; einzelne Funken bleiben ungefährlich.
- Ein Treffer verursacht einen Schadenspunkt und berücksichtigt die vorhandene Unverwundbarkeit des Spielers.
- Die gedrehte Falle richtet ihren Schadensbereich automatisch entsprechend aus.
- Der feste Collider am Drachenkopf bleibt erhalten. Für die Flamme entsteht beim Spielstart ein separater Trigger.

Die Zuordnung von Sprite, Position und Größe sowie der Schaden sind am jeweiligen `DragonTrapDamage`-Baustein im Inspector hinterlegt. Die vorhandenen Fallenanimationen und die Spieleranimationen werden dafür nicht verändert.

## Oberfläche und Grafik

Die Umgebung verwendet Sprites aus `Assets/Sprites/PNG` und vorhandene Prefabs aus `Assets/Prefabs`, darunter Böden, Stege, Säulen, Türen, Statuen, Kerzen, Fackeln und Fallen.

Die Oberfläche verwendet die Pixel-Art-Sprites aus `Assets/Sprites/PNG_UI` und die Schrift **Pixelify Sans**. Sie umfasst Hauptmenü, Levelauswahl, Spiel-HUD, Pause sowie Sieg- und Niederlagenbildschirm.

- Das HUD zeigt einen Lebensbalken, ein festes Spielerporträt, Siegel, Münzen, besiegte Gegner und den Pauseknopf.
- Das Porträt bleibt beim Laufen und Angreifen in gleichbleibender Größe und Position.
- Der Lebensbalken hat keine zusätzliche Zahlenanzeige oder Überschrift.
- Das Siegelsymbol verwendet `Circle_menu_34` aus `Circle_menu.png`.
- Interaktionshinweise erscheinen in der Nähe von Truhen und Toren. Die Steuerungsübersicht steht im Hauptmenü.

Die GUI-Sprites sind am Objekt **LEA Spielsteuerung → DungeonHUD** zugewiesen. Die Oberfläche skaliert ausgehend von 1280 × 720 auf die Fenstergröße; die Rahmen werden in neun Teilen gezeichnet.

## Windows-Version erstellen

1. Unity 6000.3.11f1 mit Windows-Build-Unterstützung verwenden.
2. Alle geänderten Szenen und Assets speichern.
3. **LEA → Windows-Version bauen** ausführen.
4. Die Ausgabe unter `Builds/Windows/LEA.exe` starten und den vollständigen Ordner für die Weitergabe verwenden.

Der Build verwendet die gespeicherten Szenen in der Reihenfolge **LEA → LEA_Zisterne → LEA_Grabkammern**. Die lokal vorhandene Windows-Version und `Builds/LEA-Windows.zip` stammen noch von vor der Ergänzung der Drachenfallen. Für den aktuellen Projektstand ist ein neuer Build nötig. Eine vorhandene ZIP-Datei wird durch den Build-Befehl nicht automatisch aktualisiert.

### Bestehende Szenen weiterbearbeiten

Die gespeicherten Szenen unter `Assets/Scenes` enthalten den aktuellen, manuell überarbeiteten Levelstand. Für normale Änderungen diese Szenen direkt bearbeiten und speichern.

Die älteren Erzeugungswerkzeuge sind weiterhin im Editor-Menü vorhanden:

- **LEA → Spielszene vorbereiten** würde `LEA.unity` und die erzeugten LEA-Animator-Controller ersetzen. Es benötigt außerdem `Assets/Scenes/Dungeon.unity`, die im aktuellen Projekt nicht mehr vorhanden ist.
- **LEA → Zwei zusätzliche Level erzeugen** ersetzt Zisterne und Grabkammern durch die Generatorlayouts. Dabei gehen die manuellen Erweiterungen und die dort eingerichteten Drachenfallen verloren.
- **LEA → GUI-Sprites zuweisen** setzt die vorgesehenen UI-Referenzen in `LEA.unity` neu. Es aktualisiert nicht automatisch die beiden anderen Szenen.

Für den normalen Build der drei vorhandenen Szenen ist keine Neuerzeugung erforderlich.

## Wichtige Dateien

| Datei / Ordner | Aufgabe |
| --- | --- |
| `Assets/Scenes/` | Die drei spielbaren Level |
| `Assets/Scripts/DungeonGame.cs` | Spielzustände, Levelwechsel, Siegel, Statistik, Sound und Neustart |
| `Assets/Scripts/PlayerController.cs` | Bewegung, Nahkampf, Schaden und Spieleranimationen |
| `Assets/Scripts/EnemyController.cs` | Gegnerverhalten, Wächter und Lebensbalken |
| `Assets/Scripts/DungeonInteractable.cs` | Siegeltruhen, Heiltränke, Münzen und Ausgangsinteraktion |
| `Assets/Scripts/DungeonExitGate.cs` | Anzeige des geschlossenen oder geöffneten Ausgangstors |
| `Assets/Scripts/DungeonTrap.cs` | Bodenfallen mit Warn- und Schadensphase |
| `Assets/Scripts/DragonTrapDamage.cs` | Animationsabhängige Flammenhitboxen der drei Drachenfallen |
| `Assets/Scripts/SpriteAnimationLoop.cs` | Spriteanimationen von Umgebungsobjekten |
| `Assets/Scripts/DungeonCamera.cs` | Mitlaufende Kamera der größeren Level |
| `Assets/Scripts/DungeonHUD.cs` | Menüs, HUD, Pixel-Schrift und festes Spielerporträt |
| `Assets/Editor/DungeonBuild.cs` | Windows-Build und ursprüngliche Szenen-/GUI-Einrichtung |
| `Assets/Editor/DungeonLevelBuild.cs` | Ursprüngliche Generatorlayouts für die Zusatzlevel |
| `Assets/Editor/DungeonGateBuild.cs` | Einrichtung der gemeinsamen Ausgangstore |
| `Assets/Editor/DungeonValidation.cs` | Automatisierte Prüfung des Spielablaufs und der Erreichbarkeit |
| `Assets/Resources/Fonts/` | Pixelify Sans Regular und Bold |

## Prüfung

Der vorhandene Prüfer kann mit Unity im Batch-Modus über `-executeMethod DungeonValidation.Run` gestartet werden, **ohne `-quit`**. Er beendet Unity selbst mit dem passenden Exitcode und schreibt den Bericht nach `Artifacts/validation-results.txt`. Dafür eine separate Projektkopie verwenden oder den Editor für dieses Projekt vorher schließen.

Der Prüfer kontrolliert unter anderem Start, Pause, Kampf, Schaden, Siegel, Ausgangstore, Neustart, Levelwechsel und die Erreichbarkeit von Truhen und Ausgängen. Seine Ergebnisse gelten jeweils für die tatsächlich geprüften Szenen. Die neuen Drachenfallen sind nicht Teil dieses allgemeinen Prüfers; ihre kurzen und langen Flammenphasen, Treffer und die gedrehte Ausrichtung zusätzlich im Play-Modus prüfen.

## Assets und Lizenzen

Grafiken, Prefabs und Charakteranimationen stammen aus dem vorhandenen Projekt; deren jeweilige Nutzungsbedingungen gelten weiterhin.

**Pixelify Sans** von Stefie Justprince / Typecalism Foundryline wird unter der **SIL Open Font License 1.1** verwendet. Die vollständige Lizenz liegt unter [Assets/StreamingAssets/Licenses/PixelifySans-OFL.txt](Assets/StreamingAssets/Licenses/PixelifySans-OFL.txt) und wird mit dem Spiel ausgeliefert. Hinweise zur Schrift sind in [Assets/Resources/Fonts/README.md](Assets/Resources/Fonts/README.md) enthalten.
