using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Autobahn;

public sealed class Preferences
{
    private readonly string path;
    public string Language { get; private set; } = "en";
    public Preferences(string? directory = null)
    {
        path = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityAutobahn"), "settings.json");
        try
        {
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<SavedPreferences>(File.ReadAllText(path));
                if (saved?.Language is "en" or "fr" or "de") Language = saved.Language;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public bool SaveLanguage(string language)
    {
        if (language is not ("en" or "fr" or "de")) throw new ArgumentOutOfRangeException(nameof(language));
        Language = language;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new SavedPreferences(language)));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private sealed record SavedPreferences(string Language);
}

public static class Lang
{
    public static string Code { get; private set; } = "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Code switch { "fr" => "fr-FR", "de" => "de-DE", _ => "en-GB" });
    public static void Set(string code) => Code = code is "fr" or "de" ? code : "en";
    public static string T(string key)
    {
        var text = Strings[key];
        return Code switch { "fr" => text.Fr, "de" => text.De, _ => text.En };
    }
    public static IEnumerable<string> Keys => Strings.Keys;

    private static readonly Dictionary<string, (string En, string Fr, string De)> Strings = new()
    {
        ["subtitle"] = ("Your Antigravity copilot", "Votre copilote Antigravity", "Dein Antigravity-Copilot"),
        ["hide"] = ("Hide to tray", "Masquer dans ^", "Im Tray ausblenden"),
        ["enable"] = ("Enable auto-submit", "Activer l’auto-submit", "Auto-Submit aktivieren"),
        ["pause"] = ("Pause auto-submit", "Mettre en pause", "Auto-Submit pausieren"),
        ["stateOn"] = ("ON THE ROAD  ·  AUTO-SUBMIT ON", "EN ROUTE  ·  AUTO-SUBMIT ACTIF", "UNTERWEGS  ·  AUTO-SUBMIT AKTIV"),
        ["stateOff"] = ("RED LIGHT  ·  AUTO-SUBMIT PAUSED", "AU FEU ROUGE  ·  AUTO-SUBMIT EN PAUSE", "ROTE AMPEL  ·  AUTO-SUBMIT PAUSIERT"),
        ["session"] = ("THIS SESSION", "CETTE SESSION", "DIESE SITZUNG"),
        ["sent"] = ("Submits sent", "Submits envoyés", "Gesendete Submits"),
        ["total"] = ("ON THE CLOCK", "AU COMPTEUR", "GESAMTZÄHLER"),
        ["sinceStart"] = ("Since your first trip", "Depuis le premier trajet", "Seit der ersten Fahrt"),
        ["last"] = ("LAST SUBMIT", "DERNIER SUBMIT", "LETZTER SUBMIT"),
        ["roadAwaits"] = ("The road is waiting", "La route vous attend", "Die Straße wartet"),
        ["journal"] = ("Activity log", "Journal de bord", "Fahrtenbuch"),
        ["files"] = ("Open log files", "Ouvrir les fichiers", "Logdateien öffnen"),
        ["footer"] = ("Ctrl + Alt + F8  ·  Enable / pause       Ctrl + Alt + F9  ·  Show window", "Ctrl + Alt + F8  ·  Activer / pause       Ctrl + Alt + F9  ·  Réafficher la fenêtre", "Strg + Alt + F8  ·  Start / Pause       Strg + Alt + F9  ·  Fenster anzeigen"),
        ["restore"] = ("Show Autobahn", "Réafficher Autobahn", "Autobahn anzeigen"),
        ["quit"] = ("Quit", "Quitter", "Beenden"),
        ["trayOn"] = ("Autobahn · on the road", "Autobahn · en route", "Autobahn · unterwegs"),
        ["trayOff"] = ("Autobahn · paused", "Autobahn · en pause", "Autobahn · pausiert"),
        ["ready"] = ("Background detection is ready.", "La détection en arrière-plan est prête.", "Die Erkennung im Hintergrund ist bereit."),
        ["openApp"] = ("Open Antigravity to start your trip.", "Ouvrez Antigravity pour commencer le trajet.", "Öffne Antigravity, um loszufahren."),
        ["detected"] = ("Submit spotted · keeping watch.", "Submit repéré · surveillance en cours.", "Submit erkannt · Überwachung läuft."),
        ["watching"] = ("Watching for the next Submit button.", "À l’affût du prochain bouton Submit.", "Warte auf die nächste Submit-Schaltfläche."),
        ["restoreApp"] = ("If detection stalls, restore Antigravity.", "Si la détection tarde, réaffichez Antigravity.", "Falls die Erkennung stockt, zeige Antigravity an."),
        ["updating"] = ("Antigravity is updating · one moment…", "Antigravity se met à jour · un instant…", "Antigravity wird aktualisiert · einen Moment…"),
        ["noClick"] = ("The button is visible, but its click action is unavailable.", "Le bouton est visible, mais son clic n’est pas accessible.", "Die Schaltfläche ist sichtbar, lässt sich aber nicht auslösen."),
        ["waitingApp"] = ("Waiting for Antigravity. Restore its window if needed.", "La détection attend Antigravity. Réaffichez sa fenêtre si nécessaire.", "Warte auf Antigravity. Zeige bei Bedarf das Fenster an."),
        ["pausedReady"] = ("Ready to go whenever you are.", "Prêt à repartir quand vous le souhaitez.", "Bereit, sobald du es bist."),
        ["historyError"] = ("History could not be read or saved.", "L’historique n’a pas pu être lu ou enregistré.", "Der Verlauf konnte nicht gelesen oder gespeichert werden."),
        ["openError"] = ("Could not open the folder: ", "Impossible d’ouvrir le dossier : ", "Ordner konnte nicht geöffnet werden: "),
        ["clickError"] = ("Auto-submit paused. Check Antigravity. ", "Auto-submit mis en pause. Vérifiez Antigravity. ", "Auto-Submit pausiert. Prüfe Antigravity. "),
        ["hotkeyToggle"] = ("Ctrl+Alt+F8 is already in use. Use the buttons or tray menu.", "Ctrl+Alt+F8 est déjà utilisé. Les boutons et le menu restent disponibles.", "Strg+Alt+F8 ist bereits belegt. Nutze die Schaltflächen oder das Tray-Menü."),
        ["hotkeyRestore"] = ("Ctrl+Alt+F9 is already in use. Click the tray icon to return.", "Ctrl+Alt+F9 est déjà utilisé. Cliquez sur l’icône près de l’horloge pour revenir.", "Strg+Alt+F9 ist bereits belegt. Klicke zum Zurückkehren auf das Tray-Symbol."),
        ["hideTitle"] = ("Autobahn is still within reach", "Autobahn reste à portée de main", "Autobahn bleibt erreichbar"),
        ["hideHint"] = ("Click the blue icon near the clock, reopen the EXE, or press Ctrl+Alt+F9 to return.", "Cliquez sur l’icône bleue près de l’horloge, relancez l’EXE ou utilisez Ctrl+Alt+F9 pour revenir.", "Klicke auf das blaue Symbol neben der Uhr, starte die EXE erneut oder drücke Strg+Alt+F9."),
        ["alreadyOpen"] = ("Autobahn is already running. Click its icon near the Windows clock to show it.", "Autobahn est déjà ouvert. Cliquez sur son icône près de l’horloge Windows pour le réafficher.", "Autobahn läuft bereits. Klicke auf das Symbol neben der Windows-Uhr, um es anzuzeigen."),
        ["language"] = ("Language", "Langue", "Sprache"),
        ["languageError"] = ("The language changed, but could not be saved for next time.", "La langue a changé, mais n’a pas pu être enregistrée pour le prochain lancement.", "Die Sprache wurde geändert, konnte aber nicht für den nächsten Start gespeichert werden."),
        ["sceneOn"] = ("The road is clear.", "La voie est libre.", "Die Straße ist frei."),
        ["sceneOff"] = ("You are in control.", "Vous avez le contrôle.", "Du hast die Kontrolle."),
        ["sceneOnDetail"] = ("Autobahn takes care of the Submit buttons.", "Autobahn s’occupe des boutons Submit.", "Autobahn übernimmt die Submit-Schaltflächen."),
        ["sceneOffDetail"] = ("At the red light, no new Submits are sent.", "Au feu rouge, aucun nouveau Submit n’est envoyé.", "Bei Rot werden keine neuen Submits gesendet."),
        ["onRoad"] = ("ON THE ROAD", "EN ROUTE", "UNTERWEGS"),
        ["redLight"] = ("RED LIGHT", "FEU ROUGE", "ROTE AMPEL"),
        ["carOn"] = ("Car driving, auto-submit enabled", "Voiture en route, auto-submit actif", "Auto fährt, Auto-Submit aktiv"),
        ["carOff"] = ("Car at red light, auto-submit paused", "Voiture au feu rouge, auto-submit en pause", "Auto an roter Ampel, Auto-Submit pausiert"),
        ["feed"] = ("Activity log, newest events first", "Journal d’activité, événements récents en premier", "Fahrtenbuch, neueste Ereignisse zuerst"),
        ["submitTitle"] = ("Submit sent", "Submit envoyé", "Submit gesendet"),
        ["submitDetail"] = ("Sent to Antigravity.", "Envoyé à Antigravity.", "An Antigravity gesendet."),
        ["enabledTitle"] = ("Let’s go!", "C’est parti !", "Los geht’s!"),
        ["enabledDetail"] = ("Submit button detection is active.", "La détection des boutons Submit est active.", "Die Erkennung von Submit-Schaltflächen ist aktiv."),
        ["pausedTitle"] = ("Paused at the red light", "Pause au feu rouge", "Pause an der roten Ampel"),
        ["pausedDetail"] = ("Auto-submit is off. You are in control.", "L’auto-submit est désactivé. Vous gardez la main.", "Auto-Submit ist aus. Du behältst die Kontrolle."),
        ["errorTitle"] = ("Something to check", "Un point à vérifier", "Bitte prüfen"),
        ["startedTitle"] = ("Ready to hit the road", "Prêt à prendre la route", "Bereit für die Fahrt"),
        ["startedDetail"] = ("Enable auto-submit whenever you are ready.", "Activez l’auto-submit quand vous le souhaitez.", "Aktiviere Auto-Submit, sobald du bereit bist."),
        ["stoppedTitle"] = ("Trip finished", "Trajet terminé", "Fahrt beendet"),
        ["stoppedDetail"] = ("Autobahn was closed.", "Autobahn a été fermé.", "Autobahn wurde geschlossen."),
        ["infoTitle"] = ("Good to know", "À savoir", "Gut zu wissen")
    };
}
