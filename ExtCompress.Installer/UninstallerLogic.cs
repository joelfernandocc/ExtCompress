using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace ExtCompress.Installer
{
    public static class UninstallerLogic
    {
        public static void Uninstall()
        {
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\ExtCompressPack", false);
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\ExtCompressLightning", false);
                Registry.ClassesRoot.DeleteSubKeyTree(@"SystemFileAssociations\.extc\shell\ExtCompressUnpack", false);
                Registry.ClassesRoot.DeleteSubKeyTree(".extc", false);
                Registry.ClassesRoot.DeleteSubKeyTree("ExtCompress.Archive", false);
                Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ExtCompress", false);

                string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ExtCompress");
                RemoveFromPath(installDir);

                // Schedule deletion of the installation directory
                DeleteDirectorySecurely(installDir);
            }
            catch (Exception)
            {
                // Ignore or log
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

        public static void DeleteDirectorySecurely(string directoryPath)
        {
            var exeName = Process.GetCurrentProcess().MainModule?.FileName;
            if (exeName == null) return;
            
            // Launch cmd to wait briefly and then delete the directory
            string command = $"/c ping 127.0.0.1 -n 4 > nul & rmdir /s /q \"{directoryPath}\"";
            
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            };
            Process.Start(startInfo);
        }
    }
}
