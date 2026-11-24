namespace UnityEngine
{
    public enum KeyCode { Backspace, R, Home, Tab }
    public struct Color
    {
        public Color(float r, float g, float b, float a) {}
        public static Color white => new Color(1, 1, 1, 1);
    }

    public class TextAsset { public string text; }

    public static class Resources
    {
        public static T Load<T>(string path) where T : class => null;
    }

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
public enum WorkshopItemSource { BuiltIn, EditorPick, Subscription, NotSpecified }
public class Game
{
    public static Game instance;
    public GameState state;
    public int currentLevelNumber;
    public string[] levels, editorPickLevels;
    public int levelCount;
}

namespace HSRTimer
{
    public static class PresetStore { public const string DefaultPresetName = "default"; }
    public static class GradientText
    {
        public static UnityEngine.Color ParseColor(string value, UnityEngine.Color fallback) => fallback;
        public static string ToHex(UnityEngine.Color color) => "FFFFFFFF";
    }
    public class ConfigService
    {
        public static ConfigService Instance;
        public SettingsModel Settings;
        public LocalizationService Localization;
        public TestTags EnabledTags;
    }
    public class TestTags
    {
        public System.Collections.Generic.List<string> Tags;
    }
    public class LocalizationService { public string Get(string key) => key; public string CurrentCode => "en"; }
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

namespace BepInEx
{
    public sealed class BepInPlugin : System.Attribute { public string GUID { get; set; } }
}
namespace BepInEx.Unity.IL2CPP
{
    public class BasePlugin {}
}

namespace HumanAPI
{
    public class WorkshopLevelMetadata
    {
        public WorkshopItemSource levelType;
        public ulong workshopId;
        public string title;
        public T TryCast<T>() where T : class => this as T;
    }
    public class BuiltinLevelMetadata : WorkshopLevelMetadata { public string internalName; }
    public class WorkshopRepository
    {
        public static WorkshopRepository instance;
        public WorkshopLevelMetadata[] levelRepo;
    }
}

namespace HSRTimer
{
    public static class LevelIdentity
    {
        public static string EnglishLevelName(string term, string fallback)
            => term == "LEVEL/Intro" ? "Mansion" : fallback;
    }
}
