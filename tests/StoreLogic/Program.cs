using System;
using System.IO;
using System.Linq;
using HSRTimer;
using Multiplayer;
using UnityEngine;

static void Equal<T>(T expected, T actual, string message)
{
    if (!Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}

Game.instance = new Game
{
    levels = new[] { "Intro", "Train", "Carry", "Mountain", "Demolition", "Castle", "Water",
        "PowerPlant", "Aztec", "Dark", "Steam", "Ice", "Intro_Reprise", "Credits" },
    levelCount = 12,
    editorPickLevels = new[] { "Thermal", "Factory", null },
};
(bool Found, ulong Index, WorkshopItemSource Source) RetryTarget(string input)
{
    bool found = RetryTargetResolver.TryResolve(input, out ulong index, out var source);
    return (found, index, source);
}
Equal((true, 0UL, WorkshopItemSource.BuiltIn), RetryTarget("0"), "Retry ID zero");
Equal(RetryTarget("Mansion"), RetryTarget(" 0 "), "Mansion title matches zero");
Equal(RetryTarget(" traIN "), RetryTarget("001"), "Existing names match numeric ID");
Equal((true, 0UL, WorkshopItemSource.EditorPick), RetryTarget(" 100 "), "Extra ID offset");
Equal(RetryTarget("Factory"), RetryTarget("101"), "Extra title and ID match");
Equal((true, 12UL, WorkshopItemSource.BuiltIn), RetryTarget("12"), "Reprise outside selectable count");
Equal(RetryTarget("12"), RetryTarget("Reprise"), "Reprise alias");
Equal(RetryTarget("12"), RetryTarget("Intro_Reprise"), "Reprise internal scene alias");
foreach (string value in new[] { null, "", "  ", "13", "99", "102", "199", "200", "-1", "-0",
    "18446744073709551615", "18446744073709551616", "999999999999999999999999999" })
    Equal(false, RetryTarget(value).Found, $"Invalid retry input '{value}'");
Game.instance.levels[12] = "Credits";
Equal(false, RetryTarget("12").Found, "Special case cannot launch unrelated scene");
Game.instance = null;
Equal(false, RetryTarget("0").Found, "Unavailable runtime catalog");

long initialTick = GameClock.CurrentTick;
for (int step = 1; step <= 180; step++)
{
    Time.fixedTime = step * Time.fixedDeltaTime;
    if (step % 2 == 0)
        GameClock.Advance();
    long hookTick = GameClock.CurrentTick;
    GameClock.Advance();
    Equal(initialTick + step, hookTick, "Boundary hook and FixedUpdate share one tick");
    Equal(hookTick, GameClock.CurrentTick, "Repeated observation cannot advance the clock");
}
Equal(1000L, GameClock.ToMs(GameClock.Seconds(60, 0)), "Sixty playable ticks");
Equal(1250L, GameClock.ToMs(GameClock.Seconds(60, 0.25)), "Pause supplement");
Equal(60L, GameClock.MsToFrames(1000), "Persisted milliseconds convert to frames");
Equal(true, SegmentLogic.IsSegmentStart(GameState.LoadingLevel, GameState.PlayingLevel, AppSate.LoadLevel), "Load to play");
Equal(false, SegmentLogic.ShouldAccumulateFixed(GameState.PlayingLevel, AppSate.ClientWaitServerLoad, true), "Client wait");
Equal(true, SegmentLogic.ShouldAccumulatePause(GameState.Paused, true), "Active paused run");
Equal(false, SegmentLogic.IsAutoReset(GameState.PlayingLevel, GameState.Inactive, AppSate.PlayLevel, AppSate.LoadLevel, true, true), "Retry retains run");

var run = new RunState();
run.BeginSegment(20, 0.25, 0, WorkshopItemSource.BuiltIn, 0);
run.PendingEndTicks = 80;
run.PendingEndPause = 0.25;
run.EndSegment(run.PendingEndTicks.Value, run.PendingEndPause, true);
Equal<ulong?>(60, run.LastSegmentTicks, "Segment snapshots boundary tick difference");
run.BeginSegment(80, 0.25, 1, WorkshopItemSource.BuiltIn, 0);
Equal<ulong?>(null, run.PendingEndTicks, "New segment drops the old endpoint");

Game.instance = new Game { state = GameState.PlayingLevel };
App.state = AppSate.PlayLevel;
foreach (bool timerRanFirst in new[] { false, true })
{
    Time.fixedTime += Time.fixedDeltaTime;
    var boundaryRun = new RunState
    {
        InSegment = true,
        TimingActive = true,
        PlayableTicks = timerRanFirst ? 101UL : 100UL,
        PauseAccum = 0.25,
    };
    TimerCore.SetUpBoundaryStep(boundaryRun, timerRanFirst);
    Equal(timerRanFirst, TimerCore.HasProcessedCurrentPhysicsStep, "Physics callback order");
    TimerCore.RecordLevelPass();
    Equal<ulong?>(101, boundaryRun.PendingEndTicks, "Both callback orders include the pass step exactly once");
    Equal(0.25, boundaryRun.PendingEndPause, "Pause total frozen at pass");
    Equal(true, boundaryRun.LevelPassed, "Authoritative pass flag");

    Time.fixedTime += Time.fixedDeltaTime;
    TimerCore.RecordLevelPass();
    Equal<ulong?>(101, boundaryRun.PendingEndTicks, "Duplicate pass cannot move the end tick");
}

var waiting = new RunState { InSegment = true, TimingActive = true, PlayableTicks = 100 };
App.state = AppSate.ClientWaitServerLoad;
TimerCore.SetUpBoundaryStep(waiting, fixedUpdateAlreadyRan: false);
TimerCore.RecordLevelPass();
Equal<ulong?>(100, waiting.PendingEndTicks, "A non-playable boundary cannot add a step");
var retry = new RunState { InSegment = true, TimingActive = true, Retrying = true, PlayableTicks = 40, PauseAccum = 0.5 };
TimerCore.SetUpBoundaryStep(retry, fixedUpdateAlreadyRan: false);
TimerCore.RecordLevelPass();
Equal<ulong?>(null, retry.PendingEndTicks, "Abandoned retry attempt has no finish");
TimerCore.RecordSegmentStart();
Equal((true, 40UL, 0.5), TimerCore.Instance.StartBoundary, "AfterLoad snapshot captures ticks and pause");

string testRoot = Path.Combine(Path.GetTempPath(), "hsrtimer-store-" + Guid.NewGuid());
string documents = Path.Combine(testRoot, "OneDrive", "Documents");
Directory.CreateDirectory(documents);
StoreDataPolicy.InitialiseDataRoot(documents, Path.Combine(testRoot, "LocalAppData"));
string directory = PersistenceService.PluginDir;
Equal(Path.Combine(documents, StoreDataPolicy.DataDirectory), directory, "Writable Windows-provided Documents wins");
Equal(0, Directory.GetFiles(directory).Length, "Write probe leaves no file");
Equal(true, StoreDataPolicy.PbWritesEnabled, "Store PB writes are enabled");
Equal(Path.Combine(directory, "markers"), StoreDataPolicy.ResolveRecordPath(directory, Path.Combine("..", "HSRTimer"), "markers"), "Parent paths cannot reach legacy marker data");
Equal(Path.Combine(directory, "markers"), StoreDataPolicy.ResolveRecordPath(directory, Path.GetTempPath(), "markers"), "Absolute paths cannot reach legacy marker data");
Equal(Path.Combine(directory, "custom"), StoreDataPolicy.ResolveRecordPath(directory, "custom", "markers"), "Store marker path can remain inside the isolated directory");
Directory.CreateDirectory(directory);

try
{
    string fallback = Path.Combine(testRoot, "LocalAppData", StoreDataPolicy.DataDirectory);
    Directory.CreateDirectory(fallback);
    string original = Path.Combine(fallback, "settings.ini");
    File.WriteAllText(original, "[hud]\nsize = 99\n");
    string previousPb = Path.Combine(fallback, "subsegment", "pb", "IL", "Mansion", "Any", "meta.json");
    Directory.CreateDirectory(Path.GetDirectoryName(previousPb));
    File.WriteAllText(previousPb, "original PB");
    string warning = null;
    Equal(directory, StoreDataPolicy.SelectDataRoot(documents, fallback, message => warning = message), "Primary wins even with existing fallback data");
    Equal<string>(null, warning, "No fallback warning on primary success");
    string settings = PersistenceService.PathFor("settings.ini");
    Equal(0, System.Linq.Enumerable.Count(PersistenceService.Read(settings)), "Missing primary settings do not read from fallback");
    var section = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IDictionary<string, string>>
    {
        ["hud"] = new System.Collections.Generic.Dictionary<string, string> { ["size"] = "22" }
    };
    PersistenceService.Write(settings, section);
    Equal("22", System.Linq.Enumerable.Single(PersistenceService.Read(settings)).Value, "Save and reload settings in Documents");
    string blocked = Path.Combine(testRoot, "blocked");
    Directory.CreateDirectory(blocked);
    File.WriteAllText(Path.Combine(blocked, StoreDataPolicy.DataDirectory), "file prevents creation of the data directory");
    Equal(fallback, StoreDataPolicy.SelectDataRoot(blocked, fallback, message => warning = message), "Unwritable primary selects fallback");
    Equal(true, warning.Contains(blocked) && warning.Contains(fallback), "Fallback logs both paths and failure");
    string missing = Path.Combine(testRoot, "MissingDocuments");
    Equal(fallback, StoreDataPolicy.SelectDataRoot(missing, fallback, message => warning = message), "Missing Documents selects fallback");
    Equal(false, Directory.Exists(missing), "Missing Documents is not created");
    Equal(fallback, StoreDataPolicy.SelectDataRoot(null, fallback, message => warning = message), "Unresolved known folder selects fallback");
    string protectedDocuments = Path.Combine(testRoot, "WindowsApps", "FakePackage", "Documents");
    Directory.CreateDirectory(protectedDocuments);
    Equal(fallback, StoreDataPolicy.SelectDataRoot(protectedDocuments, fallback, message => warning = message), "WindowsApps is never writable");
    Equal(0, Directory.GetFileSystemEntries(protectedDocuments).Length, "Nothing was written to WindowsApps");
    Equal("[hud]\nsize = 99\n", File.ReadAllText(original), "Fallback settings preserved across both selections");
    Equal(directory, PersistenceService.PluginDir, "Process data root remains fixed");
    if (!OperatingSystem.IsWindows())
    {
        string packageTarget = Path.Combine(testRoot, "package-target");
        Directory.CreateDirectory(packageTarget);
        string redirectedPackage = Path.Combine(testRoot, "package-link");
        Directory.CreateSymbolicLink(redirectedPackage, packageTarget);
        string redirectedConfig = redirectedPackage;
        Equal(fallback, StoreDataPolicy.SelectDataRoot(redirectedConfig, fallback, message => warning = message),
            "A redirected package ancestor triggers fallback");
        Equal(true, warning.Contains("reparse point") && warning.Contains(redirectedPackage),
            "The rejected ancestor is logged");
        Equal(0, Directory.GetFileSystemEntries(packageTarget).Length, "Fallback does not write through the redirect");
    }

    string meta = Path.Combine(directory, "meta.json");
    string samples = Path.Combine(directory, "sample.jsonl");
    File.WriteAllText(meta, SubsegmentFileStore.MakeMetaJson("IL", "Mansion", null, "Any", new[] { "Mansion" }, 1250, 1));
    Equal(true, SubsegmentFileStore.TryLoadMeta(meta, out var loadedMeta), "Metadata round trip");
    Equal(1250L, loadedMeta.total_ms, "Metadata timing units");
    Equal("Mansion", loadedMeta.level_ids[0], "Metadata array");
    string sampleJson = SubsegmentFileStore.MakeSampleJson(new[] { new SubsegmentSample { seq = 1, t_ms = 1250, px = 2.5f } });
    Equal(false, sampleJson.Contains("Position"), "Runtime-only property excluded");
    File.WriteAllText(samples, sampleJson + "{\"seq\":2,\"t_ms\":1300,\"px\":3.5,\"plane_radius\":0.4}\n{\n");
    Equal(true, SubsegmentFileStore.TryLoadSamples(samples, out var loaded), "Sample round trip and legacy import");
    Equal(2, loaded.Count, "Malformed JSONL skipped without discarding good rows");
    Equal(1300L, loaded[1].t_ms, "Legacy sample time");
    Equal(3.5f, loaded[1].px, "Legacy sample position");

    string category = SubsegmentFileStore.CategoryKey(new[] { "Jumpless", "Checkpoint" });
    Equal("Checkpoint+Jumpless", category, "Order-independent tags");
    Equal("Any", SubsegmentFileStore.CategoryKey(Array.Empty<string>()), "Untagged category");
    string il = Path.Combine(directory, "subsegment", "pb", "IL", "Mansion", "Any");
    string tagged = Path.Combine(directory, "subsegment", "pb", "IL", "Mansion", category);
    bool SaveIl(string target, string tag, long time, string filename = "sample.jsonl", bool keepBackup = true)
        => StoreRecordWriter.TrySaveBetter(directory, target, time,
            SubsegmentFileStore.MakeMetaJson("IL", "Mansion", null, tag, null, time, 1),
            new System.Collections.Generic.Dictionary<string, string> { [filename] = sampleJson }, keepBackup);
    long ReadTime(string dir)
    {
        Equal(true, SubsegmentFileStore.TryReadTotalMs(Path.Combine(dir, "meta.json"), out long time), "Read persisted PB");
        return time;
    }
    Equal(true, SaveIl(il, "Any", 1250), "First IL save");
    Equal(false, SaveIl(il, "Any", 1250), "Tie keeps the prior record");
    Equal(false, SaveIl(il, "Any", 1500), "Slower run keeps the prior record");
    Equal(false, SaveIl(il, "Any", 1100, Path.Combine("..", "forbidden")), "Staging write fails safely");
    using (var lease = new FileStream(il + ".lock", FileMode.Open, FileAccess.Write, FileShare.None))
        Equal(false, SaveIl(il, "Any", 1050), "Another writer cannot change the active PB");
    Equal(1250L, ReadTime(il), "Failed write preserves active PB");
    Equal(true, SaveIl(il, "Any", 1000), "Faster run updates");
    Equal(1000L, ReadTime(il), "Updated record reloaded");
    Equal(1250L, ReadTime(il + ".previous"), "Previous complete PB is retained");
    Equal(true, SaveIl(il, "Any", 980), "ON keeps previous and archives older backup");
    var archived = Directory.GetDirectories(Path.GetDirectoryName(il), "Any.backup-*").Single();
    Equal(1250L, ReadTime(archived), "Archived ON backup contains prior PB");
    Equal(true, SaveIl(il, "Any", 970, keepBackup: false), "OFF updates existing IL PB");
    Equal(1000L, ReadTime(il + ".previous"), "OFF keeps existing previous backup");
    Equal(1250L, ReadTime(archived), "OFF keeps existing archived backup");
    Equal(1, Directory.GetDirectories(Path.GetDirectoryName(il), "Any.backup-*").Length, "OFF creates no archived backup");
    Equal(false, Directory.Exists(il + ".rollback"), "Successful OFF update removes temporary rollback");

    string offIl = Path.Combine(Path.GetDirectoryName(il), "NoBackup");
    var settingsModel = new SettingsModel();
    Equal(false, settingsModel.AutomaticPbBackup, "New config defaults OFF");
    var iniPath = PersistenceService.PathFor("settings.ini");
    File.WriteAllText(iniPath, "[settings]\nauto_reset = true\n");
    settingsModel.AutomaticPbBackup = true;
    settingsModel.Load();
    Equal(false, settingsModel.AutomaticPbBackup, "Legacy settings without key reset to OFF");
    settingsModel.RetryLevelOverride = "Train";
    settingsModel.Save();
    settingsModel = new SettingsModel();
    settingsModel.Load();
    Equal("Train", settingsModel.RetryLevelOverride, "Legacy retry name still loads");
    settingsModel.RetryLevelOverrideEnable = true;
    settingsModel.RetryLevelOverride = "001";
    settingsModel.Save();
    settingsModel = new SettingsModel();
    settingsModel.Load();
    Equal("001", settingsModel.RetryLevelOverride, "Retry ID saved and reloaded as entered");
    Equal(true, settingsModel.RetryLevelOverrideEnable, "Existing retry setting preserved");
    foreach (bool enabled in new[] { true, false })
    {
        settingsModel.AutomaticPbBackup = enabled;
        settingsModel.Save();
        settingsModel = new SettingsModel();
        settingsModel.Load();
        Equal(enabled, settingsModel.AutomaticPbBackup, "Backup preference persists after reload");
    }
    Equal(true, SaveIl(offIl, "NoBackup", 1500, keepBackup: settingsModel.AutomaticPbBackup), "First OFF PB save");
    Equal(true, SaveIl(offIl, "NoBackup", 1400, keepBackup: false), "Improved OFF PB save");
    Equal(false, Directory.Exists(offIl + ".previous"), "OFF creates no previous backup");
    Directory.Move(offIl, offIl + ".rollback");
    Equal(1400L, ReadTime(StoreRecordWriter.ReadBundleDirectory(offIl)), "Interrupted OFF swap remains readable");
    Equal(false, SaveIl(offIl, "NoBackup", 1450, keepBackup: false), "OFF recovery prevents slower replacement");
    Equal(1400L, ReadTime(offIl), "OFF restores interrupted swap");

    Equal(true, SaveIl(tagged, category, 1400), "Tagged record saves independently");
    Equal(970L, ReadTime(il), "Untagged record unaffected by the tagged run");

    string ml = Path.Combine(directory, "subsegment", "pb", "ML", "Aztec", "Any");
    var levelFiles = new System.Collections.Generic.Dictionary<string, string>
    {
        [Path.Combine("levels", "B0.jsonl")] = sampleJson,
        [Path.Combine("levels", "B1.jsonl")] = sampleJson,
    };
    string MultiMeta(long time) => SubsegmentFileStore.MakeMetaJson("ML", null, "Aztec", "Any", new[] { "B0", "B1" }, time, 2);
    Equal(true, StoreRecordWriter.TrySaveBetter(directory, ml, 2000, MultiMeta(2000), levelFiles, keepBackup: true), "First campaign save");
    Directory.Move(ml, ml + ".previous");
    Equal(2000L, ReadTime(StoreRecordWriter.ReadBundleDirectory(ml)), "Interrupted swap can be read after restart");
    Equal(true, StoreRecordWriter.TrySaveBetter(directory, ml, 1900, MultiMeta(1900), levelFiles, keepBackup: true), "Campaign recovery and update");
    Equal(1900L, ReadTime(ml), "Updated campaign record");
    Equal(2000L, ReadTime(ml + ".previous"), "Campaign backup intact");
    Equal(true, StoreRecordWriter.TrySaveBetter(directory, ml, 1800, MultiMeta(1800), levelFiles, keepBackup: false),
        "Campaign PB updates with backup OFF");
    Equal(1800L, ReadTime(ml), "Campaign OFF active record readable");
    Equal(2000L, ReadTime(ml + ".previous"), "OFF does not alter campaign backup");
    Equal(false, Directory.Exists(ml + ".rollback"), "Successful campaign save cleans rollback");

    string corrupt = Path.Combine(tagged, "meta.json");
    File.WriteAllText(corrupt, "{");
    Equal(false, SaveIl(tagged, category, 900), "Unreadable metadata blocks replacement");
    Equal("{", File.ReadAllText(corrupt), "Unreadable metadata preserved");
    File.WriteAllText(Path.Combine(il, "sample.jsonl"), "{");
    Equal(false, SaveIl(il, "Any", 900), "Corrupt samples block replacement");
    Equal("{", File.ReadAllText(Path.Combine(il, "sample.jsonl")), "Corrupt samples preserved");

    string ini = Path.Combine(directory, "settings.ini");
    StoreRecordWriter.WriteFile(directory, ini, "size=18", keepBackup: false);
    StoreRecordWriter.WriteFile(directory, ini, "size=22", keepBackup: false);
    Equal("size=22", File.ReadAllText(ini), "Atomic settings replacement");
    if (!OperatingSystem.IsWindows())
    {
        string outside = Path.Combine(directory, "existing-records");
        Directory.CreateDirectory(outside);
        string isolated = Path.Combine(directory, "store");
        Directory.CreateDirectory(isolated);
        Directory.CreateSymbolicLink(Path.Combine(isolated, "link"), outside);
        Equal(false, StoreRecordWriter.TrySaveBetter(isolated, Path.Combine(isolated, "link", "Any"),
            900, MultiMeta(900), levelFiles, keepBackup: false), "Linked path cannot redirect a Store write");
        Equal(0, Directory.GetFileSystemEntries(outside).Length, "Linked directory untouched");
    }

    var markers = MarkerStore.CreateEmpty("Mansion", "BuiltIn", 0, "Any");
    markers.pb = new MarkerPb { total_ms = 2000 };
    markers.pbTimes.Add(new MarkerPbEntry { id = "finish", t_ms = 1900 });
    Equal(true, MarkerStore.Save(markers, onlyIfImproved: true), "First marker PB");
    markers.pb.total_ms = 2200;
    Equal(false, MarkerStore.Save(markers, onlyIfImproved: true), "Slower marker PB rejected");
    markers.pb.total_ms = 1800;
    Equal(true, MarkerStore.Save(markers, onlyIfImproved: true), "Faster marker PB replaces");
    Equal(true, MarkerStore.TryLoad("Mansion", "Any", out var markerReload), "Marker file reloaded");
    Equal(1800L, markerReload.pb.total_ms, "Marker PB after reload");
    string markerPath = MarkerStore.SetPath("Mansion", "Any");
    Equal(0, Directory.GetFiles(Path.GetDirectoryName(markerPath), "*.bak-*").Length, "OFF marker PB creates no backup");
    ConfigService.Instance = new ConfigService { Settings = new SettingsModel { AutomaticPbBackup = true } };
    markers.pb.total_ms = 1700;
    Equal(true, MarkerStore.Save(markers, onlyIfImproved: true), "ON marker PB saves");
    Equal(1, Directory.GetFiles(Path.GetDirectoryName(markerPath), "*.bak-*").Length, "ON marker PB backup retained");
    ConfigService.Instance.Settings.AutomaticPbBackup = false;
    markers.pb.total_ms = 1600;
    Equal(true, MarkerStore.Save(markers, onlyIfImproved: true), "OFF marker PB saves after ON");
    Equal(1, Directory.GetFiles(Path.GetDirectoryName(markerPath), "*.bak-*").Length, "OFF preserves existing marker backup");
    Equal(true, MarkerStore.Save(markers), "Ordinary marker edit saves");
    Equal(2, Directory.GetFiles(Path.GetDirectoryName(markerPath), "*.bak-*").Length, "Ordinary marker edit keeps backup behavior");
    ConfigService.Instance = null;
    File.WriteAllText(markerPath, "{");
    Equal(false, MarkerStore.Save(markers), "Malformed marker file cannot be replaced");
    Equal("{", File.ReadAllText(markerPath), "Malformed marker file retained");
    Equal("original PB", File.ReadAllText(previousPb), "Documents saves and fallback probes leave existing LocalAppData PB intact");
}
finally
{
    Directory.Delete(testRoot, recursive: true);
}

const string storeRepository = "https://github.com/TOFU-meister/HSRTimer-MicrosoftStore";
Equal(storeRepository, PluginInfo.PLUGIN_REPOSITORY_URL, "Store repository identity");
string Feed(params string[] urls) => "<feed xmlns='http://www.w3.org/2005/Atom'>" +
    string.Concat(System.Linq.Enumerable.Select(urls, url => $"<entry><link href='{url}' /></entry>")) + "</feed>";
string upstreamRelease = PluginInfo.UpstreamRepositoryUrl + "/releases/tag/v9.0.0";
string storeRelease = storeRepository + "/releases/tag/v1.0.0";
Equal(false, AtomFeed.TryParseLatest(Feed(upstreamRelease), out _, out string releaseError, storeRepository), "Reject upstream-only feed");
Equal(AtomFeed.NoStableRelease, releaseError, "No eligible Store release result");
Equal(false, AtomFeed.TryParseLatest(Feed(storeRelease + "-preview.1"), out _, out releaseError, storeRepository), "Skip prereleases");
Equal(AtomFeed.NoStableRelease, releaseError, "No stable Store release result");
Equal(false, AtomFeed.TryParseLatest(Feed(), out _, out releaseError, storeRepository), "Empty Atom feed");
Equal(AtomFeed.NoStableRelease, releaseError, "Empty feed is distinct from malformed response");
Equal(false, AtomFeed.TryParseLatest("<html />", out _, out releaseError, storeRepository), "Reject non-feed response");
Equal(true, AtomFeed.TryParseLatest(Feed(upstreamRelease, storeRelease), out var storeUpdate, out _, storeRepository), "Select only dedicated Store release");
Equal(storeRelease, storeUpdate.Url, "Store release link");

string projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "../../../../../src/HSRTimer/HSRTimer.csproj"));
var storeVersionNode = System.Xml.Linq.XDocument.Load(projectPath).Root.Elements("PropertyGroup")
    .Single(group => (string)group.Element("TargetFramework") == "net6.0").Element("Version");
Equal((string)storeVersionNode, PluginInfo.RuntimeVersion, "Store runtime and project versions agree");
Equal(true, System.Text.RegularExpressions.Regex.IsMatch(PluginInfo.RuntimeVersion,
    @"^0\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-pre$"), "Store version format");
var tabs = new SettingsPanelTabRegistry();
int saves = 0, draws = 0, deactivations = 0;
string tabLanguage = null;
Action saveTab = () => saves++;
bool RegisterTab(string guid, Func<string> title, Action draw, Action save) =>
    tabs.RegisterCallbacks(guid, title, draw, () => deactivations++, save,
        () => true, () => new[] { "en", "zh-Hans" }, code => tabLanguage = code);
Equal(false, RegisterTab("invalid", () => "Invalid", null, saveTab), "invalid callback rejected");
Equal(true, RegisterTab("overlay", () => "Input Overlay", () => draws++, saveTab), "callback registration");
Equal(false, RegisterTab("overlay", () => "duplicate", () => {}, saveTab), "duplicate rejected");
Equal(1, tabs.Count, "one overlay tab");
Equal("en", tabLanguage, "language on registration");
tabs.NotifyLanguageChanged("zh-Hans");
Equal("zh-Hans", tabLanguage, "language update");
tabs.NotifyLanguageChanged("unsupported");
Equal("en", tabLanguage, "language fallback");
tabs.TabAt(0).Draw();
Equal(1, draws, "draw callback");
Equal(true, tabs.CapturesInput(0), "input callback");
tabs.Deactivate(0);
Equal(1, deactivations, "deactivation callback");
Equal(true, RegisterTab("fault", () => throw new Exception("title"), () => {},
    () => throw new Exception("save")), "faulting title does not partially register");
Equal("fault", tabs.TabTitle(tabs.TabAt(1)), "title fallback");
int trailingSaves = 0;
tabs.SettingsSaved += () => trailingSaves++;
tabs.NotifySettingsSaved();
Equal(1, saves, "save once despite duplicate registration");
Equal(1, trailingSaves, "faulty subscriber isolated");

Equal(true, VersionUtil.TryParse(PluginInfo.RuntimeVersion, out var currentPre), "Recognise running prerelease");
Equal("pre", currentPre.Pre, "Keep prerelease suffix");
Equal(true, VersionUtil.TryParse("0.1.0-pre.1", out var previousPre), "Previous development version");
Equal(true, VersionUtil.Compare(currentPre, previousPre) > 0, "Current version advances past prior build");
Equal(true, VersionUtil.TryParse("v0.1.3", out var stable), "Recognise stable counterpart");
Equal(true, VersionUtil.Compare(stable, currentPre) > 0, "Stable release sorts after the running prerelease");
Equal(false, AtomFeed.TryParseLatest(Feed(storeRepository + "/releases/tag/v" + PluginInfo.RuntimeVersion),
    out _, out releaseError, storeRepository), "The test release is not a stable update candidate");

string BuildVersion(string revision) => "<VersionNumber><majorVersion>1</majorVersion><minorVersion>0</minorVersion>"
    + "<build>96</build><revision>" + revision + "</revision></VersionNumber>";
Equal(true, StoreGameVersion.TryFormat(BuildVersion("1419"), out string gameVersion), "Read game resource XML");
Equal("v1096141", gameVersion, "Match the reference timer's display");
Equal(true, StoreGameVersion.TryFormat(BuildVersion("1"), out gameVersion), "Short resource version");
Equal("v10961", gameVersion, "Short version remains intact");
Equal(false, StoreGameVersion.TryFormat(BuildVersion("invalid"), out _), "Reject nonnumeric version components");
Equal(false, StoreGameVersion.TryFormat("<VersionNumber />", out _), "Reject missing version components");
Equal(false, StoreGameVersion.TryFormat("<VersionNumber>", out _), "Reject malformed version XML");

void FontClick(string text)
{
    UnityEngine.GUILayout.Click = text;
    UnityEngine.GUILayout.Labels.Clear();
    StoreFontDiagnostics.Draw(new UnityEngine.GUIStyle(), new UnityEngine.GUIStyle());
}

Equal(false, StoreFontDiagnostics.IsApplied, "IMGUI remains default");
FontClick("Font Diagnostics");
FontClick("List Unity OS fonts");
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("MissingMethodException")),
    "OS enumeration exception stays in diagnostics");
UnityEngine.Font.FailOsList = false;
FontClick("List Unity OS fonts");
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("OS font 1/2: Arial")),
    "successfully enumerated OS names appear sorted");
PanelTextInput.Query = "sEgO";
FontClick(null);
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("2 retrieved, 1 shown")), "case-insensitive partial search");
FontClick("Segoe UI");
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("OS font 2/2: Segoe UI")), "direct OS selection");
PanelTextInput.Query = "";
var originalOutput = Console.Out;
var probeOutput = new StringWriter();
try
{
    Console.SetOut(probeOutput);
    FontClick("Inspect interop and internal-call availability");
    Il2CppInterop.Runtime.IL2CPP.EmptyFont = true;
    FontClick("Inspect interop and internal-call availability");
    Il2CppInterop.Runtime.IL2CPP.HasFontClass = false;
    FontClick("Inspect interop and internal-call availability");
}
finally { Console.SetOut(originalOutput); }
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("registration results recorded")), "read-only API report");
Equal(true, probeOutput.ToString().Contains("Game metadata: .ctor(System.String), token=0x06000001"),
    "runtime declaration can coexist with a missing CLR constructor");
Equal(true, probeOutput.ToString().Contains("no exact signature match for .ctor()"), "report unmatched native signature");
Equal(true, probeOutput.ToString().Contains("absence inconclusive"), "empty query cannot assert absence");
Equal(true, probeOutput.ToString().Contains("query unavailable"), "unavailable metadata query is not a missing method");
Equal(true, probeOutput.ToString().Contains("Internal_CreateDynamicFont: unresolved"), "registration is reported separately");
FontClick("Probe Font(string) with selected name");
FontClick(null);
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("public Font(string) constructor is unavailable")),
    "independent string constructor fails safely when missing");
FontClick("Probe selected OS font");
FontClick(null);
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("MissingMethodException")),
    "optional single-family overload safely missing");
var firstDuplicateName = new UnityEngine.Font { name = "Same name" };
var secondDuplicateName = new UnityEngine.Font { name = "Same name" };
UnityEngine.Resources.LoadedFonts = new[] { UnityEngine.GUI.skin.font, firstDuplicateName,
    secondDuplicateName, firstDuplicateName, new UnityEngine.Font(), new UnityEngine.Font() };
FontClick("Scan game fonts");
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("5 unique live candidates")),
    "same native object deduplicated and same names retained");
FontClick($"Same name #{secondDuplicateName.GetInstanceID()} — Game Asset / Loaded Unity asset / Dynamic");
FontClick("I checked the candidate visually");
FontClick("Apply to Timer HUD for this session");
Equal(true, ReferenceEquals(secondDuplicateName, StoreFontDiagnostics.ActiveFont()), "candidate selected by instance ID");
FontClick("Scan game fonts");
FontClick("Inspect glyph and atlas APIs");
Equal(true, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("not exposed by interop")),
    "optional character query safely missing");
FontClick("Apply to Timer HUD for this session");
Equal(false, StoreFontDiagnostics.IsApplied, "draw return alone does not confirm glyphs");
FontClick("I checked the candidate visually");
FontClick("Apply to Timer HUD for this session");
Equal(true, ReferenceEquals(UnityEngine.GUI.skin.font, StoreFontDiagnostics.ActiveFont()),
    "borrowed font can be applied");
FontClick("Restore IMGUI / release probe fonts");
Equal(false, UnityEngine.GUI.skin.font.Destroyed, "borrowed font is never destroyed");
FontClick("Probe upstream fallback order");
var ownedFont = UnityEngine.Font.LastCreated;
UnityEngine.GUI.FailCandidateDraw = true;
FontClick(null);
FontClick("I checked the candidate visually");
FontClick("Apply to Timer HUD for this session");
Equal(false, StoreFontDiagnostics.IsApplied, "failed preview cannot be applied");
UnityEngine.GUI.FailCandidateDraw = false;
FontClick("Preview size 18 (click to cycle)");
FontClick("I checked the candidate visually");
FontClick("Apply to Timer HUD for this session");
Equal(true, ReferenceEquals(ownedFont, StoreFontDiagnostics.ActiveFont()), "owned font can be applied");
Equal(22, StoreFontDiagnostics.ActiveSize, "preview size applies only for the session");
StoreFontDiagnostics.HudFailed(new InvalidOperationException("simulated draw failure"));
Equal(false, StoreFontDiagnostics.IsApplied, "HUD failure resets to standard font");
FontClick("I checked the candidate visually");
FontClick("Apply to Timer HUD for this session");
Equal(true, StoreFontDiagnostics.IsApplied, "a fresh preview can be confirmed after a HUD exception");
ownedFont.Destroyed = true;
Equal(true, StoreFontDiagnostics.ActiveFont() == null && !StoreFontDiagnostics.IsApplied,
    "destroyed font resets to standard font");
FontClick("Probe upstream fallback order");
var newOwnedFont = UnityEngine.Font.LastCreated;
StoreFontDiagnostics.Release();
Equal(true, newOwnedFont.Destroyed, "release destroys the live owned font");

string bundlePath = System.IO.Path.GetTempFileName();
try
{
    void ExpectBundleFailure(Action action, string description)
    {
        try { action(); }
        catch (System.IO.InvalidDataException) { return; }
        throw new Exception(description);
    }

    StoreFontBundle LoadBundle(Type api = null)
        => StoreFontBundle.Load(bundlePath, api ?? typeof(TestFontBundle), () => new Il2CppSystem.Type());

    System.IO.File.WriteAllText(bundlePath, "not a UnityFS file");
    ExpectBundleFailure(() => LoadBundle(), "bad header must be rejected");
    Equal(0, TestFontBundle.Opened, "invalid files never reach Unity");
    using (var oversized = System.IO.File.OpenWrite(bundlePath))
        oversized.SetLength(StoreFontBundle.MaxFileBytes + 1);
    ExpectBundleFailure(() => LoadBundle(), "size limit checked before opening");
    Equal(0, TestFontBundle.Opened, "oversized files never reach Unity");
    System.IO.File.WriteAllText(bundlePath, "UnityFS\0");
    try { LoadBundle(typeof(object)); throw new Exception("Missing wrapper should fail."); }
    catch (MissingMethodException) {}
    Equal(0, TestFontBundle.Opened, "partial API never opens a bundle");
    ExpectBundleFailure(() => LoadBundle(), "Unity rejecting a valid header is handled");
    var invalidNative = TestFontBundle.Next = new() { InvalidNativeObject = true };
    try { LoadBundle(); throw new Exception("Invalid native wrapper accepted."); }
    catch (System.IO.InvalidDataException error)
    {
        Equal(true, error.Message.Contains("Unity overloaded null"), "Unity null is distinguished from CLR null");
    }
    Equal(0, invalidNative.Unloads, "invalid object is not unloaded");
    var enumerationFailure = TestFontBundle.Next = new() { FailEnumeration = true };
    try { LoadBundle(); throw new Exception("Enumeration failure ignored."); }
    catch (System.IO.InvalidDataException error)
    {
        Equal(true, StoreFontBundle.Describe(error).Contains("enumeration test error"), "preserve inner exception");
    }
    Equal(1, enumerationFailure.Unloads, "failed enumeration unloads bundle");
    var control = TestFontBundle.Next = new() { Font = null, Texture = new UnityEngine.Texture2D() };
    using (var loadedTexture = StoreFontBundle.Load(bundlePath, typeof(TestFontBundle),
        () => new Il2CppSystem.Type(), StoreFontBundle.Route.FileExplicit))
    {
        Equal(1, loadedTexture.TextureCount, "Texture2D-only control accepted");
        Equal("explicit", TestFontBundle.LastRoute, "three-argument file route selected");
    }
    using (StoreFontBundle.Load(bundlePath, typeof(TestFontBundle),
        () => new Il2CppSystem.Type(), StoreFontBundle.Route.Memory))
        Equal("memory", TestFontBundle.LastRoute, "memory route selected");
    try
    {
        StoreFontBundle.Load(bundlePath, typeof(TestFontBundle), () => new Il2CppSystem.Type(), StoreFontBundle.Route.MemoryExplicit);
        throw new Exception("Memory reflection failure ignored.");
    }
    catch (System.IO.InvalidDataException error)
    {
        Equal(true, StoreFontBundle.Describe(error).Contains("native test error"), "memory exception chain recorded");
    }
    var noFonts = TestFontBundle.Next = new() { Font = null };
    ExpectBundleFailure(() => LoadBundle(), "bundle without a font rejected");
    Equal(1, noFonts.Unloads, "empty bundle unloaded");
    var tooMany = TestFontBundle.Next = new() { Names = new string[33] };
    ExpectBundleFailure(() => LoadBundle(), "asset count limit");
    Equal(1, tooMany.Unloads, "oversized bundle unloaded");
    var sourceBundle = TestFontBundle.Next = new();
    var loaded = LoadBundle();
    Equal(true, ReferenceEquals(loaded.Fonts[0], sourceBundle.Font), "bundle font acquired");
    loaded.Dispose();
    loaded.Dispose();
    Equal(1, sourceBundle.Unloads, "bundle ownership released once");
    Equal(true, sourceBundle.Font.Destroyed, "bundle-owned asset released");
    StoreFontDiagnostics.Release();
    var original = TestFontBundle.Next = new();
    var initial = LoadBundle();
    Equal(true, original.Font.hideFlags.HasFlag(UnityEngine.HideFlags.DontUnloadUnusedAsset), "only owned Font protected");
    Equal(UnityEngine.HideFlags.None, original.Font.material.hideFlags, "material ownership not assumed");
    Equal(UnityEngine.HideFlags.None, UnityEngine.GUI.skin.font.hideFlags, "borrowed skin untouched");
    StoreFontDiagnostics.RegisterBundle(bundlePath, StoreFontBundle.Route.File, initial, selectNew: true);
    FontClick(null);
    FontClick("I checked the candidate visually");
    FontClick("Apply to Timer HUD for this session");
    Equal(true, ReferenceEquals(original.Font, StoreFontDiagnostics.ActiveFont()), "external Font applied");
    int attempts = 0;
    StoreFontBundle Recover(string path, StoreFontBundle.Route route)
    {
        Equal(bundlePath, path, "recovery uses original path");
        Equal(StoreFontBundle.Route.File, route, "recovery uses original API");
        attempts++;
        TestFontBundle.Next = new();
        return LoadBundle();
    }

    StoreFontDiagnostics.MaintainBundles(1000, Recover);
    Equal(0, attempts, "healthy bundle skips reload");
    original.Font.material.mainTexture.InvalidNativeObject = true;
    original.Font.material.mainTexture = new UnityEngine.Texture(); // atlas rebuilt by Unity
    StoreFontDiagnostics.MaintainBundles(1500, Recover);
    Equal(0, attempts, "valid replacement atlas is accepted");
    bool detached = false, detachedBeforeUnload = false;
    StoreFontDiagnostics.DetachHudStyles = () => detached = true;
    original.BeforeUnload = () => detachedBeforeUnload = detached && !StoreFontDiagnostics.IsApplied;
    original.Font.Destroyed = true;
    StoreFontDiagnostics.MaintainBundles(2000, Recover);
    Equal(true, detachedBeforeUnload, "HUD style detachment precedes owned unload");
    Equal(false, StoreFontDiagnostics.IsApplied, "invalidated Font falls back while waiting");
    Equal(1, original.Unloads, "stale live bundle unloaded before recovery");
    FontClick(null);
    Equal(false, UnityEngine.GUILayout.Labels.Exists(x => x.Contains("Font unloaded")), "stale candidate removed");
    StoreFontDiagnostics.MaintainBundles(3999, Recover);
    Equal(0, attempts, "retry waits for cooldown");
    StoreFontDiagnostics.MaintainBundles(4499, Recover);
    var replacement = TestFontBundle.Next;
    Equal(1, attempts, "Font reloaded once");
    Equal(true, ReferenceEquals(replacement.Font, StoreFontDiagnostics.ActiveFont()), "same asset automatically reapplied");
    replacement.InvalidNativeObject = true;
    StoreFontDiagnostics.MaintainBundles(5000, Recover);
    Equal(0, replacement.Unloads, "no unload call on invalid bundle");
    StoreFontDiagnostics.MaintainBundles(7000, Recover);
    var second = TestFontBundle.Next;
    Equal(2, attempts, "bundle-only invalidation recovered");
    second.Font.material.mainTexture.InvalidNativeObject = true;
    StoreFontDiagnostics.MaintainBundles(7500, Recover);
    StoreFontDiagnostics.MaintainBundles(9500, Recover);
    Equal(3, attempts, "invalid atlas recovered");
    TestFontBundle.Next.Font.material.InvalidNativeObject = true;
    StoreFontDiagnostics.MaintainBundles(10000, Recover);
    StoreFontDiagnostics.MaintainBundles(12000, Recover);
    Equal(3, attempts, "unstable successive recoveries bounded");
    Equal(false, StoreFontDiagnostics.IsApplied, "remains on IMGUI after exhaustion");

    TestFontBundle.Next = new();
    StoreFontDiagnostics.RegisterBundle(bundlePath, StoreFontBundle.Route.File, LoadBundle(), selectNew: true);
    TestFontBundle.Next.Font.material.InvalidNativeObject = true;
    StoreFontDiagnostics.MaintainBundles(12500, Recover);
    int failures = 0;
    StoreFontBundle FailRecovery(string path, StoreFontBundle.Route route)
    {
        failures++;
        throw new System.IO.IOException("simulated missing file");
    }
    for (long time = 14500; time <= 20500; time += 2000)
        StoreFontDiagnostics.MaintainBundles(time, FailRecovery);
    Equal(3, failures, "consecutive failed loads are bounded");

    TestFontBundle.Next = new();
    StoreFontDiagnostics.RegisterBundle(bundlePath, StoreFontBundle.Route.File, LoadBundle(), selectNew: true);
    TestFontBundle.Next.Font.Destroyed = true;
    StoreFontDiagnostics.MaintainBundles(21000, Recover);
    FontClick("Restore IMGUI / release probe fonts");
    StoreFontDiagnostics.MaintainBundles(26000, Recover);
    Equal(3, attempts, "explicit restore cancels pending recovery");
    StoreFontDiagnostics.DetachHudStyles = null;

    var fixtures = new System.Collections.Generic.List<(TestFontBundle Native, StoreFontBundle Loaded, string Path)>();
    for (int i = 0; i < 5; i++)
    {
        var native = TestFontBundle.Next = new();
        native.Font.name = "Same embedded name";
        string path = bundlePath + $"-{i}.bundle";
        var fixture = LoadBundle();
        fixtures.Add((native, fixture, path));
        if (i == 4)
        {
            try
            {
                StoreFontDiagnostics.RegisterBundle(path, StoreFontBundle.Route.File, fixture, selectNew: true);
                throw new Exception("Fifth font bundle was accepted.");
            }
            catch (InvalidOperationException) { }
            Equal(1, native.Unloads, "rejected fifth bundle released");
            break;
        }
        StoreFontDiagnostics.RegisterBundle(path, StoreFontBundle.Route.File, fixture, selectNew: true);
    }

    var first = fixtures[0];
    var secondFixture = fixtures[1];
    StoreFontDiagnostics.RegisterBundle(first.Path, StoreFontBundle.Route.File, first.Loaded, selectNew: true);
    Equal(0, first.Native.Unloads, "same loaded instance registered idempotently");
    var duplicate = TestFontBundle.Next = new();
    try
    {
        StoreFontDiagnostics.RegisterBundle(first.Path, StoreFontBundle.Route.File, LoadBundle(), selectNew: true);
        throw new Exception("Duplicate path was accepted.");
    }
    catch (InvalidOperationException) { }
    Equal(1, duplicate.Unloads, "rejected duplicate instance released");

    void SelectFixture(int i)
    {
        var fixture = fixtures[i];
        FontClick($"Same embedded name #{fixture.Native.Font.GetInstanceID()} — AssetBundle / {System.IO.Path.GetFileName(fixture.Path)} / Dynamic");
        FontClick(null);
    }

    SelectFixture(0);
    FontClick("I checked the candidate visually");
    FontClick("Apply to Timer HUD for this session");
    Equal(true, ReferenceEquals(first.Native.Font, StoreFontDiagnostics.ActiveFont()), "selected first of same-named fonts");
    SelectFixture(1);
    Equal(true, ReferenceEquals(first.Native.Font, StoreFontDiagnostics.ActiveFont()), "preview selection leaves HUD unchanged");
    FontClick("I checked the candidate visually");
    FontClick("Apply to Timer HUD for this session");
    Equal(true, ReferenceEquals(secondFixture.Native.Font, StoreFontDiagnostics.ActiveFont()), "HUD switched to second bundle");
    Equal(secondFixture.Native.Font.GetInstanceID(), StoreFontDiagnostics.PerformanceSnapshot().FontId.Value, "performance snapshot follows active font");
    Equal(UnityEngine.FontStyle.Normal, StoreFontDiagnostics.ActiveStyle, "applied font preserves its own weight by default");
    FontClick("HUD style on next apply: Normal (font's own weight)");
    Equal(UnityEngine.FontStyle.Normal, StoreFontDiagnostics.ActiveStyle, "changing comparison mode alone leaves HUD unchanged");
    FontClick("Apply to Timer HUD for this session");
    Equal(UnityEngine.FontStyle.Bold, StoreFontDiagnostics.ActiveStyle, "legacy HUD weight can be reapplied explicitly");
    FontClick("HUD style on next apply: Legacy HUD (Bold)");
    FontClick("Apply to Timer HUD for this session");
    Equal(UnityEngine.FontStyle.Normal, StoreFontDiagnostics.ActiveStyle, "reapply returns to the font's own weight");
    FontClick("Record preview and HUD style comparison");
    Equal(true, StoreFontDiagnostics.CaptureHudStyle, "comparison asks HUD to report its styles");
    StoreFontDiagnostics.RecordHudStyles("HUD after", new UnityEngine.GUIStyle(), new UnityEngine.GUIStyle());
    Equal(false, StoreFontDiagnostics.CaptureHudStyle, "capture ends after one HUD update");
    Equal(4, StoreFontDiagnostics.PerformanceSnapshot().Bundles, "performance snapshot counts four bundles");
    FontClick("Restore IMGUI / release probe fonts");
    Equal(false, StoreFontDiagnostics.IsApplied, "explicit release restores IMGUI after switching");
    Equal(0, StoreFontDiagnostics.PerformanceSnapshot().Bundles, "no bundles held after release");
    foreach (var fixture in fixtures)
        Equal(1, fixture.Native.Unloads, "each loaded fixture released once");

}
finally { System.IO.File.Delete(bundlePath); }

var frameWindow = new StoreFrameWindow(100);
for (int i = 0; i < 98; i++) frameWindow.Add(0.01f);
frameWindow.Add(0.04f);
frameWindow.Add(0.05f);
frameWindow.Add(float.NaN);
var frames = frameWindow.Summarize();
Equal(100, frames.Frames, "valid frame count");
Equal(40, (int)Math.Round(frames.P99Ms), "nearest-rank p99");
Equal(10, (int)Math.Round(frames.P95Ms), "nearest-rank p95");
Equal(20, (int)Math.Round(frames.LowFps), "1% low is reciprocal of slow-tail mean");
frameWindow.Add(0.06f);
Equal(101, frameWindow.Summarize().Frames, "full-window frame count survives percentile buffer wrap");
Equal(100, frameWindow.Summarize().PercentileFrames, "percentile capacity bounded");
frameWindow.Reset();
Equal(0, frameWindow.Summarize().Frames, "frame reset");
Equal(false, StorePerformance.Enabled, "performance capture defaults off");
StorePerformance.Frame(true);
Equal(0, StorePerformance.RetainedSamples, "off does not accumulate");
StorePerformance.Start(0);
UnityEngine.Time.unscaledDeltaTime = 0.016f;
for (int i = 1; i <= 365; i++)
{
    UnityEngine.Time.realtimeSinceStartup = i * 5;
    StorePerformance.Frame(true);
}
Equal(360, StorePerformance.RetainedSamples, "CSV rows bounded");
StorePerformance.Start(0);
Equal(0, StorePerformance.RetainedSamples, "restart clears prior rows");
UnityEngine.GUILayout.Click = "Stop performance capture";
StorePerformance.Draw(new UnityEngine.GUIStyle(), new UnityEngine.GUIStyle());
Equal(false, StorePerformance.Enabled, "explicit stop disables capture");

Console.WriteLine("Store logic checks passed.");
