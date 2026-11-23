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
Equal(true, VersionUtil.TryParse("v0.1.2", out var stable), "Recognise stable counterpart");
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

Console.WriteLine("Store logic checks passed.");
