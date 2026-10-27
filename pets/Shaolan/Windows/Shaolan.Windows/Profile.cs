namespace SunPet;
public static class Profile
{
    public const string AppId = "Shaolan.Windows";
    public const string SettingsFolder = "Shaolan-Windows";
    public const string Title = "Shaolan · 薄肌桌宠";
    public const string DisplayName = "邵艾伦 · 薄肌桌宠";
    public const string Role = Roles.Shao;
    public const int ExpectedClips = 37;
    public const double DefaultSize = 260, MinSize = 160, MaxSize = 400;
    public static readonly int[] Sizes = [190, 260, 340];
    public static readonly int[] CompanionIds = [];
    public static bool VerticalBounce => true;
}


