namespace Multiverse.Hub
{
    /// <summary>
    /// Satu-satunya sumber kebenaran untuk nama/path scene resmi.
    /// Dipakai oleh HubManager (runtime), SceneAutoRegister, SceneScaffolder
    /// dan ProjectValidator (editor) agar tidak ada string yang saling berbeda.
    /// </summary>
    public static class SceneCatalog
    {
        public const string HubSceneName = "MainMenu";
        public const string HubScenePath = "Assets/Hub/Scenes/MainMenu.unity";

        public sealed class GameEntry
        {
            public readonly string Folder;      // nama folder di Assets/Games/
            public readonly string DisplayName; // judul tampil di Hub
            public readonly string Pic;
            public readonly string SceneName;

            public GameEntry(string folder, string displayName, string pic)
            {
                Folder = folder;
                DisplayName = displayName;
                Pic = pic;
                SceneName = folder + "_Main";
            }

            public string ScenePath => "Assets/Games/" + Folder + "/Scenes/" + SceneName + ".unity";
            public string FolderPath => "Assets/Games/" + Folder;
        }

        // Urutan = urutan index di Build Settings setelah MainMenu (index 0).
        public static readonly GameEntry[] Games =
        {
            new GameEntry("CyberNeonDodger", "Cyber Neon Dodger", "Novendra (Venator)"),
            new GameEntry("DragonHunter", "Dragon Hunter", "Muhammad Hanif (Velmiree)"),
            new GameEntry("CozyFruitCatcher", "Cozy Fruit Catcher", "Azizah"),
            new GameEntry("DeepSeaExplorer", "Deep Sea Explorer", "Kaishan"),
            new GameEntry("SkyGlider", "Sky Glider", "Zidnii"),
        };
    }
}
