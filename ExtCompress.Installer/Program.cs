using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ExtCompress.Installer;

static class Program
{
    static void Main(string[] args)
    {
        if (!IsAdministrator())
        {
            var exeName = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exeName)) return;

            ProcessStartInfo startInfo = new ProcessStartInfo(exeName)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(" ", args.Select(a => $"\"{a}\""))
            };
            try
            {
                Process.Start(startInfo);
            }
            catch
            {
                Console.WriteLine("This installer requires administrative privileges.");
                Thread.Sleep(3000);
            }
            return;
        }

        Console.Clear();
        Console.WriteLine(@"
  ______      _    _____                                              
 |  ____|    | |  / ____|                                             
 | |__  __  _| |_| |     ___  _ __ ___  _ __  _ __ ___  ___ ___ 
 |  __| \ \/ / __| |    / _ \| '_ ` _ \| '_ \| '__/ _ \/ __/ __|
 | |____ >  <| |_| |___| (_) | | | | | | |_) | | |  __/\__ \__ \
 |______/_/\_\\__|\_____\___/|_| |_| |_| .__/|_|  \___||___/___/
                                       | |                      
                                       |_|                      

ExtCompress By Soluciones Digitales Camargo
");

        bool uninstall = args.Length > 0 && args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
        bool silent = args.Any(a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase));

        if (uninstall)
        {
            Uninstall(false, silent);
        }
        else
        {
            Install(silent);
        }

        if (!silent)
        {
            Console.WriteLine("\nTask completed successfully. Press any key to exit.");
            Console.ReadKey();
        }
    }

    private static void Install(bool silent = false)
    {
        string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ExtCompress");
        bool isUpdate = Directory.Exists(installDir) && File.Exists(Path.Combine(installDir, "ExtCompress.exe"));

        if (!silent)
        {
            Console.WriteLine(LocalizationManager.Get("Install_CertPrompt"));
            Console.Write("> ");
            var keyInput = Console.ReadKey();
            Console.WriteLine();
            if (keyInput.Key == ConsoleKey.S)
            {
                InstallCertificate();
            }
            else
            {
                Console.WriteLine(LocalizationManager.Get("Install_CertSkipped"));
            }
        }

        if (isUpdate)
        {
            if (!silent) Console.Write(LocalizationManager.Get("Install_UpdateDetected"));
            Uninstall(true, silent);
            if (!silent) Console.Write(LocalizationManager.Get("Install_UpdatePurged"));
        }
        else
        {
            if (!silent) Console.Write(LocalizationManager.Get("Install_Starting"));
        }

        RunTaskWithProgress(LocalizationManager.Get("Install_CreatingDir"), () =>
        {
            if (!Directory.Exists(installDir))
            {
                Directory.CreateDirectory(installDir);
            }
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Install_CopyingExe"), () =>
        {
            try
            {
                using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("ExtCompress.exe");
                if (stream != null)
                {
                    using var fileStream = new FileStream(Path.Combine(installDir, "ExtCompress.exe"), FileMode.Create, FileAccess.Write);
                    stream.CopyTo(fileStream);
                }
                else
                {
                    Console.WriteLine("\n[ERROR CRÍTICO] El motor ExtCompress.exe no está embebido en el instalador.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[ERROR] Al extraer motor: " + ex.Message);
            }
            
            // Copy the installer itself to act as the uninstaller
            string currentInstaller = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(currentInstaller) && File.Exists(currentInstaller))
            {
                File.Copy(currentInstaller, Path.Combine(installDir, "ExtCompressUninstall.exe"), true);
            }
            Thread.Sleep(300);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Install_ZombieKeys"), () =>
        {
            try { Registry.ClassesRoot.DeleteSubKeyTree(@"*\shell\ExtCompressCompress", false); } catch {}
            try { Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\ExtCompressCompress", false); } catch {}
            Thread.Sleep(50);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Install_AddingContext"), () =>
        {
            string[] targets = { @"Directory\shell", @"*\shell" };
            foreach (var target in targets)
            {
                using var key = Registry.ClassesRoot.CreateSubKey($@"{target}\ExtCompress");
                key.SetValue("", "ExtCompress");
                key.SetValue("Icon", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\",0");
                using var cmdKey = key.CreateSubKey("command");
                cmdKey.SetValue("", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\" /compress \"%1\" /out \"%1.extc\"");
                
                using var keyFast = Registry.ClassesRoot.CreateSubKey($@"{target}\ExtCompressLightning");
                keyFast.SetValue("", "ExtCompress Fast");
                keyFast.SetValue("Icon", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\",0");
                using var cmdKeyFast = keyFast.CreateSubKey("command");
                cmdKeyFast.SetValue("", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\" /lightning \"%1\" /out \"%1.extc\"");
            }
            Thread.Sleep(100);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Install_AddingContext"), () =>
        {
            using var extKey = Registry.ClassesRoot.CreateSubKey(".extc");
            extKey.SetValue("", "ExtCompress.Archive");

            extKey.SetValue("Content Type", "application/x-extcompress");
            extKey.SetValue("PerceivedType", "compressed");

            using var classKey = Registry.ClassesRoot.CreateSubKey("ExtCompress.Archive");
            classKey.SetValue("", "ExtCompress Archive");
            using var iconKey = classKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue("", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\",0");

            using var shellKey = classKey.CreateSubKey(@"shell\open\command");
            shellKey.SetValue("", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\" \"%1\"");
            
            using var unpackKey = classKey.CreateSubKey(@"shell\ExtCompressUnpack");
            unpackKey.SetValue("", "Extraer aquí (ExtCompress)");
            unpackKey.SetValue("Icon", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\",0");
            using var cmdUnpackKey = unpackKey.CreateSubKey("command");
            cmdUnpackKey.SetValue("", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\" \"%1\"");
            
            Thread.Sleep(100);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Uninstall_UpdatingPath"), () =>
        {
            const string name = "PATH";
            string pathvar = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) ?? "";
            var paths = pathvar.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!paths.Any(p => string.Equals(p.TrimEnd('\\'), installDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(installDir);
                Environment.SetEnvironmentVariable(name, string.Join(";", paths), EnvironmentVariableTarget.Machine);
            }
            Thread.Sleep(200);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Install_AddRemove"), () =>
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ExtCompress");
            key.SetValue("DisplayName", "ExtCompress");
            key.SetValue("DisplayIcon", $"\"{Path.Combine(installDir, "ExtCompress.exe")}\",0");
            key.SetValue("DisplayVersion", "1.0.0.0");
            key.SetValue("Publisher", "Soluciones Digitales Camargo");
            key.SetValue("UninstallString", $"\"{Path.Combine(installDir, "ExtCompressUninstall.exe")}\" /uninstall");
            Thread.Sleep(100);
        }, silent);

        if (!silent) Console.WriteLine(LocalizationManager.Get("Install_Completed"));
    }

    private static void InstallCertificate()
    {
        try
        {
            string certBase64 = "MIIDHjCCAgagAwIBAgIQd3FIH0JF+qFF/G1u4GjupTANBgkqhkiG9w0BAQsFADAnMSUwIwYDVQQDDBxTb2x1Y2lvbmVzIERpZ2l0YWxlcyBDYW1hcmdvMB4XDTI2MDcxOTAyMTkzNVoXDTI3MDcxOTAyMzkzNVowJzElMCMGA1UEAwwcU29sdWNpb25lcyBEaWdpdGFsZXMgQ2FtYXJnbzCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBALVbBV8iMhvJTM5z7AXzaR4/KWzcqYM46lM5eVZOm0zziR67em4JmQIJNRtNVyFg0i/+5j98j/ypHy+To70Lcdkxw6o3lQ7PCk8YjGsPIKOUxDS+pnYCIhoQKPMtoLEQheva+Qe7oVXFB+RX+DWMxW1RGFK+VRpvX03gLgN1K8fsh1lOevg4xOFF0APs+5jPKgB7GP+PsVLrVz3PMEQFWMbK4+4kQqNXaIrH5GDCeSr5RywZlR1O8cNYT9yoB9AVd9lfnjHE2mJ3iUBtpTsRet3eFzbyWuzuUO7qU+OBFOBJ+ye8q2mIv3oSLG7ndHyMZmoA8saK8i92Zg/NfDS6IZ0CAwEAAaNGMEQwDgYDVR0PAQH/BAQDAgeAMBMGA1UdJQQMMAoGCCsGAQUFBwMDMB0GA1UdDgQWBBSf/ktJWV9B+KhLWk0LOPlA2MilfTANBgkqhkiG9w0BAQsFAAOCAQEAWXykrSk4enMURSFJFP3Lh0kBtaGHQo2YL8Wrl1VCv+otYeOvE+L1kIPIIYbMYH9phfBZhej5hy6xyo5S7iWlUlydN0irO9rn6uYZJ/4g8F4WN2CZuTsEGwU+edbD8QAjO1TpzkTqpKVq5tlni62n+Dj1RqwGUnvkv8bWonYU5C5l1odjvWof2LiJHFW/cUO81vFVjHrX7Arer7lIMVoDX2Bhe/SFPCcyslVPdYAlOS+/cibPIQKkLMeN+mghO6X/fR1UK/fQVN0NipcZUZfTNmjSPGcPz11DxvsiOMhxpJVwJ4ANB406yQx5ctLgDFrqFj/FrdsBRRa6VSvk4iXsuw==";
            byte[] certBytes = Convert.FromBase64String(certBase64);
            using (X509Certificate2 cert = new X509Certificate2(certBytes))
            {
                using (X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(cert);
                    store.Close();
                }
            }
            Console.WriteLine(LocalizationManager.Get("Install_CertSuccess"));
        }
        catch (Exception ex)
        {
            Console.WriteLine(LocalizationManager.Get("Install_CertError", ex.Message));
        }
    }

    private static void Uninstall(bool isUpdate = false, bool silent = false)
    {
        if (!isUpdate && !silent) Console.Write(LocalizationManager.Get("Uninstall_Starting"));

        RunTaskWithProgress(LocalizationManager.Get("Uninstall_RemovingContext"), () =>
        {
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\ExtCompress", false);
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\ExtCompressLightning", false);
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\ExtCompressPack", false);
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\ExtCompressCompress", false);
            
            Registry.ClassesRoot.DeleteSubKeyTree(@"*\shell\ExtCompress", false);
            Registry.ClassesRoot.DeleteSubKeyTree(@"*\shell\ExtCompressLightning", false);
            Registry.ClassesRoot.DeleteSubKeyTree(@"*\shell\ExtCompressCompress", false);

            Registry.ClassesRoot.DeleteSubKeyTree(@"ExtCompress.Archive\shell\ExtCompressUnpack", false);
            Registry.ClassesRoot.DeleteSubKeyTree(@"SystemFileAssociations\.extc\shell\ExtCompressUnpack", false);
            Thread.Sleep(150);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Uninstall_RemovingContext"), () =>
        {
            Registry.ClassesRoot.DeleteSubKeyTree(".extc", false);
            Registry.ClassesRoot.DeleteSubKeyTree("ExtCompress.Archive", false);
            Thread.Sleep(150);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Uninstall_RemovingAddRemove"), () =>
        {
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ExtCompress", false);
            Thread.Sleep(150);
        }, silent);

        string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ExtCompress");
        
        RunTaskWithProgress(LocalizationManager.Get("Uninstall_UpdatingPath"), () =>
        {
            RemoveFromPath(installDir);
            Thread.Sleep(200);
        }, silent);

        RunTaskWithProgress(LocalizationManager.Get("Uninstall_RemovingEngine"), () =>
        {
            string exeDest = Path.Combine(installDir, "ExtCompress.exe");
            if (File.Exists(exeDest))
            {
                try { File.Delete(exeDest); } catch { }
            }
            
            // Nota: No podemos borrar ExtCompressUninstall.exe directamente si nos estamos ejecutando desde él,
            // pero el instalador puro normalmente usa un archivo CMD temporal para autoborrarse. 
            // Para simplicidad en esta versión CLI, informamos al usuario.
            Thread.Sleep(300);
        }, silent);

        if (!isUpdate && !silent)
        {
            Console.WriteLine(LocalizationManager.Get("Uninstall_Removed"));
            Console.WriteLine(LocalizationManager.Get("Uninstall_Note"));
            Console.WriteLine(LocalizationManager.Get("Uninstall_Completed"));
        }
    }

    private static void RemoveFromPath(string installDir)
    {
        const string name = "PATH";
        string pathvar = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) ?? "";
        var paths = pathvar.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        bool removed = false;
        for (int i = paths.Count - 1; i >= 0; i--)
        {
            if (string.Equals(paths[i].TrimEnd('\\'), installDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                paths.RemoveAt(i);
                removed = true;
            }
        }
        
        if (removed)
        {
            Environment.SetEnvironmentVariable(name, string.Join(";", paths), EnvironmentVariableTarget.Machine);
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RunTaskWithProgress(string taskName, Action action, bool silent = false)
    {
        if (silent)
        {
            try { action(); } catch {}
            return;
        }

        Task task = Task.Run(action);
        int totalBars = 20;
        int progress = 0;
        
        while (!task.IsCompleted)
        {
            int percent = (int)((progress / (float)totalBars) * 100);
            string bars = new string('=', progress) + new string(' ', totalBars - progress);
            Console.Write($"\r[{bars}] {percent,3}% {taskName}");
            Thread.Sleep(30);
            if (progress < totalBars - 1) progress++;
        }
        
        try
        {
            task.GetAwaiter().GetResult();
            string finalBars = new string('=', totalBars);
            Console.Write($"\r[{finalBars}] 100% {taskName}");
        }
        catch (Exception ex)
        {
            string finalBars = new string('x', totalBars);
            Console.Write($"\r[{finalBars}] ERR  {taskName} - {ex.Message}");
        }
        Console.WriteLine();
    }
}