namespace SunPet;
public static class Profile
{
    public const string AppId = "SunPet.Windows";
    public const string SettingsFolder = "SunPet-Windows";
    public const string Title = "SunPet · 太对了";
    public const string DisplayName = "孙宇晨 · 太对了";
    public const string Role = Roles.Sun;
    public const int ExpectedClips = 28;
    public const double DefaultSize = 180, MinSize = 120, MaxSize = 260;
    public static readonly int[] Sizes = [130, 180, 240];
    public static readonly int[] CompanionIds = [1, 2, 4, 11, 13, 14];
    public static bool VerticalBounce => false;
}
