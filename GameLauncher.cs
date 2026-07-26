using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

public class GameLauncher
{
    public static void LaunchAndMonitor(string appName)
    {
        // 1. Find the game manifest
        var games = EpicSyncManager.FindInstalledGames();
        var game = games.Find(g => g.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase));

        if (game == null)
        {
            // The game is not installed locally. Check if we can find it in the online account library cache
            var epicApi = new EpicApiManager();
            var library = epicApi.FetchLibrary();
            game = library.Find(g => g.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase));

            if (game == null)
            {
                Console.WriteLine("Game '" + appName + "' not found in Epic Games library.");
                return;
            }

            Console.WriteLine(game.DisplayName + " is not installed. Launching installation in Epic Games Store...");
            
            // Construct the EGS installation/launch URI
            string installUri = "";
            if (!string.IsNullOrEmpty(game.CatalogNamespace) && !string.IsNullOrEmpty(game.CatalogItemId))
            {
                installUri = string.Format("com.epicgames.launcher://apps/{0}%3A{1}%3A{2}?action=launch",
                    Uri.EscapeDataString(game.CatalogNamespace),
                    Uri.EscapeDataString(game.CatalogItemId),
                    Uri.EscapeDataString(game.AppName));
            }
            else
            {
                installUri = string.Format("com.epicgames.launcher://apps/{0}?action=launch",
                    Uri.EscapeDataString(game.AppName));
            }

            try
            {
                Process.Start(installUri);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to launch installation URI: " + ex.Message);
            }
            return;
        }

        Console.WriteLine("Launching " + game.DisplayName + "...");

        // 2. Scan the game directory to find all possible exe names
        var gameExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(game.InstallLocation))
            {
                foreach (var file in Directory.GetFiles(game.InstallLocation, "*.exe", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    // Skip Epic-related helper executables that might be in the folder
                    if (name.Equals("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("EpicWebHelper", StringComparison.OrdinalIgnoreCase))
                        continue;

                    gameExes.Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Warning: Could not scan game directory for executables: " + ex.Message);
        }

        // Add the launch executable just in case
        string launchExeName = Path.GetFileNameWithoutExtension(game.LaunchExecutable);
        if (!string.IsNullOrEmpty(launchExeName))
        {
            gameExes.Add(launchExeName);
        }

        // 3. Check if Epic Games Launcher is already running
        bool egsWasRunning = Process.GetProcessesByName("EpicGamesLauncher").Length > 0;

        // 4. Construct EGS URI
        string uri = "";
        if (!string.IsNullOrEmpty(game.CatalogNamespace) && !string.IsNullOrEmpty(game.CatalogItemId))
        {
            uri = string.Format("com.epicgames.launcher://apps/{0}%3A{1}%3A{2}?action=launch&silent=true",
                Uri.EscapeDataString(game.CatalogNamespace),
                Uri.EscapeDataString(game.CatalogItemId),
                Uri.EscapeDataString(game.AppName));
        }
        else
        {
            uri = string.Format("com.epicgames.launcher://apps/{0}?action=launch&silent=true",
                Uri.EscapeDataString(game.AppName));
        }

        // 5. Start the EGS launcher/game
        string launcherPath = EpicSyncManager.GetEpicLauncherExePath();
        if (!egsWasRunning && !string.IsNullOrEmpty(launcherPath) && File.Exists(launcherPath))
        {
            // If EGS was not running, launch EGS directly in silent mode with the game URI
            try
            {
                Process.Start(launcherPath, "\"" + uri + "\" -silent");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to launch EGS via executable: " + ex.Message + ". Falling back to URI handler.");
                Process.Start(uri);
            }
        }
        else
        {
            try
            {
                Process.Start(uri);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to launch URI: " + ex.Message);
                return;
            }
        }

        // 6. Wait for the game to start (monitor processes matching gameExes)
        Console.WriteLine("Waiting for game processes to start...");
        DateTime startTime = DateTime.Now;
        TimeSpan startTimeout = TimeSpan.FromMinutes(2);
        bool gameStarted = false;

        while (DateTime.Now - startTime < startTimeout)
        {
            if (IsAnyProcessRunning(gameExes))
            {
                gameStarted = true;
                break;
            }
            Thread.Sleep(1000);
        }

        if (!gameStarted)
        {
            Console.WriteLine("Timeout: Game process did not start within 2 minutes.");
            return;
        }

        Console.WriteLine("Game started. Monitoring running processes...");

        // 7. Loop while the game is running
        int consecutiveEmptyPolls = 0;
        while (true)
        {
            Thread.Sleep(1000);

            if (IsAnyProcessRunning(gameExes))
            {
                consecutiveEmptyPolls = 0;
            }
            else
            {
                consecutiveEmptyPolls++;
                if (consecutiveEmptyPolls >= 3) // Wait 3 seconds to confirm exit
                {
                    break;
                }
            }
        }

        Console.WriteLine("Game has closed.");

        // 8. If EGS wasn't running before, terminate it to save resources
        if (!egsWasRunning)
        {
            // Give the game process a couple of seconds to fully clean up
            Thread.Sleep(2000);
            
            Console.WriteLine("Closing Epic Games Launcher to free system resources...");
            TerminateEgsProcesses();
        }
    }

    private static bool IsAnyProcessRunning(HashSet<string> processNames)
    {
        if (processNames == null || processNames.Count == 0) return false;
        
        foreach (var name in processNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length > 0)
                {
                    foreach (var p in processes)
                    {
                        p.Dispose();
                    }
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    public static void TerminateEgsProcesses()
    {
        string[] targetProcesses = { "EpicGamesLauncher", "EpicWebHelper" };
        foreach (var name in targetProcesses)
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var p in processes)
            {
                try
                {
                    p.Kill();
                    Console.WriteLine("Terminated process: " + name + " (PID: " + p.Id + ")");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to terminate process " + name + ": " + ex.Message);
                }
            }
        }
    }
}
