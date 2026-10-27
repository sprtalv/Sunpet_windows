namespace SunPet;
public static class Profile
{
    public const string AppId = "FengGe.Windows";
    public const string SettingsFolder = "FengGe-Windows";
    public const string Title = "FengGe · 好事桌宠";
    public const string DisplayName = "峰哥 · 好事桌宠";
    public const string Role = Roles.Feng;
    public const int ExpectedClips = 11;
    public const double DefaultSize = 260, MinSize = 160, MaxSize = 400;
    public static readonly int[] Sizes = [190, 260, 340];
    public static readonly int[] CompanionIds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    public static bool VerticalBounce => true;
}

