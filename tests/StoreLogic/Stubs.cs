namespace UnityEngine
{
    public static class Time
    {
        public static float fixedTime;
        public const float fixedDeltaTime = 1f / 60f;
        public static float realtimeSinceStartup;
    }

    public struct Vector3
    {
        public Vector3(float x, float y, float z) { }
    }
}

namespace Multiplayer
{
    public static class App { public static AppSate state; }

    public enum AppSate
    {
        Startup, Menu, LoadLevel, PlayLevel, ClientWaitServerLoad,
        ClientLobby, ServerLobby, ClientLoadLobby, ServerLoadLobby,
    }
}

public enum GameState { Inactive, LoadingLevel, PlayingLevel, Paused }
public enum WorkshopItemSource { BuiltIn }
public class Game
{
    public static Game instance;
    public GameState state;
    public int currentLevelNumber;
}

namespace HSRTimer
{
    public class SettingsModel
    {
        public bool AutoReset;
        public string MarkersPath;
        public bool MarkersDebugLogging;
    }
    public class ConfigService
    {
        public static ConfigService Instance;
        public SettingsModel Settings;
        public TestTags EnabledTags;
    }
    public class TestTags
    {
        public System.Collections.Generic.List<string> Tags;
    }
    public class LocalizationService { public string Get(string key) => key; }
    public static class Plugin
    {
        public static readonly TestLogger Logger = new TestLogger();
    }
    public sealed class TestLogger
    {
        public void LogInfo(string message) => System.Console.WriteLine(message);
        public void LogWarning(string message) => System.Console.WriteLine(message);
    }
}

namespace HSRTimer
{
    // Supply the Unity component state needed by the real boundary methods.
    public partial class TimerCore
    {
        private float _lastFixedTime = float.NaN;
        private bool _segmentStartLatched;
        private ulong _segmentStartTicks;
        private double _segmentStartPause;

        public static TimerCore Instance { get; private set; }
        public static RunState State { get; private set; }
        public static string LastBoundary { get; private set; }
        private static void LogBoundary(string entry) => LastBoundary = entry;

        internal static void SetUpBoundaryStep(RunState state, bool fixedUpdateAlreadyRan)
        {
            State = state;
            Instance = new TimerCore();
            if (fixedUpdateAlreadyRan)
                Instance._lastFixedTime = UnityEngine.Time.fixedTime;
        }

        internal (bool latched, ulong ticks, double pause) StartBoundary
            => (_segmentStartLatched, _segmentStartTicks, _segmentStartPause);
    }
}
