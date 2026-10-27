using System;
using SunPet;
internal static class TestProgram
{
    [STAThread] private static int Main()
    {
        try { foreach (var line in Verification.Core()) Console.WriteLine(line); Console.WriteLine("PASS"); return 0; }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + e); return 1; }
    }
}
