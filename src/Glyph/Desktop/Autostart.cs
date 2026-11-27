using Microsoft.Win32;

namespace Glyph.Desktop;

/// "Start with Windows". Done with a Task Scheduler task that runs when you sign in: Windows starts those
/// straight away, while programs in the Run key wait until the desktop has settled and every other startup
/// program has had its turn (half a minute to two minutes on a busy laptop). The Run key is only the fallback.
internal static class Autostart
{
    const string TaskName = "Glyph";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string HomeKey = @"Software\Glyph";

    /// `onlyMine` (uninstalling): turn it off only if it starts this copy of Glyph, not another one.
    public static void Apply(bool on, bool onlyMine = false)
    {
        var exe = Paths.Exe;
        bool viaTask = false;
        try
        {
            if (on) { RegisterTask(exe); viaTask = true; }
            else RemoveTask(onlyMine ? exe : null);
        }
        catch (Exception e) { Log.Write("autostart task: " + e.Message); }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            bool ours = key.GetValue("Glyph") is string v && v.Contains(onlyMine ? exe : Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase);
            if (on && !viaTask) key.SetValue("Glyph", $"\"{exe}\" --autostart");
            else if (ours) key.DeleteValue("Glyph", false);
        }
        catch (Exception e) { Log.Write("autostart: " + e.Message); }
    }

    /// Remembers where this Glyph keeps its settings, so the installer can offer to bring them along.
    public static void RememberHome()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(HomeKey);
            if (key.GetValue("Home") as string != Paths.Home) key.SetValue("Home", Paths.Home);
        }
        catch { }
    }

    static dynamic Folder()
    {
        dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!;
        svc.Connect();
        return svc.GetFolder("\\");
    }

    static void RegisterTask(string exe)
    {
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        string X(string s) => System.Security.SecurityElement.Escape(s);
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Starts Glyph when you sign in. Turn it off in Glyph's settings: General, Start with Windows.</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{X(user)}</UserId></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>{X(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
                <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd></IdleSettings>
              </Settings>
              <Actions Context="Author"><Exec><Command>"{X(exe)}"</Command><Arguments>--autostart</Arguments><WorkingDirectory>{X(Path.GetDirectoryName(exe)!)}</WorkingDirectory></Exec></Actions>
            </Task>
            """;
        var folder = Folder();
        try
        {
            string old = folder.GetTask(TaskName).Xml;
            if (old.Contains($"\"{X(exe)}\"", StringComparison.OrdinalIgnoreCase) && old.Contains("--autostart") && old.Contains("PT0S")) return;   // already right
        }
        catch { }   // no task yet
        folder.RegisterTask(TaskName, xml, 6 /* create or update */, null, null, 3 /* the signed-in user */, null);
        Log.Write("start with Windows: on (sign-in task)");
    }

    static void RemoveTask(string? onlyFor)
    {
        var folder = Folder();
        string xml;
        try { xml = folder.GetTask(TaskName).Xml; } catch { return; }
        if (onlyFor != null && !xml.Contains(System.Security.SecurityElement.Escape(onlyFor)!, StringComparison.OrdinalIgnoreCase)) return;
        folder.DeleteTask(TaskName, 0);
        Log.Write("start with Windows: off");
    }
}
