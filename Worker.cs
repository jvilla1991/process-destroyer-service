using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace proccess_destroyer_service
{
    public class Worker : BackgroundService
    {
        private readonly string processFilePath = @"C:\Program Files\Repos\process-destroyer-service\processes.txt";
        private readonly string logFilePath = @"C:\Program Files\Repos\process-destroyer-service\destroyer-log.log";
        private FileStream? lockFS;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log("Service started.");
            LockProcessFile();

            while (!stoppingToken.IsCancellationRequested)
            {
                var targetProcesses = LoadTargetProcesses();
                KillTargetProcesses(targetProcesses);

                await Task.Delay(1000 * 30, stoppingToken);
            }
        }

        private void KillTargetProcesses(string[] targetProcesses)
        {
            foreach (string processName in targetProcesses)
            {
                try
                {
                    var processes = Process.GetProcessesByName(processName);
                    foreach (var process in processes)
                    {
                        string message = $"[{DateTime.Now}] Killing process: {process.ProcessName} (PID: {process.Id})";
                        Log(message);
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    Log($"[{DateTime.Now}] Error: {ex.Message}");
                }
            }
        }

        private string[] LoadTargetProcesses()
        {
            try
            {
                if (File.Exists(processFilePath))
                {
                    return File.ReadAllLines(processFilePath)
                               .Where(line => !string.IsNullOrWhiteSpace(line))
                               .Select(line => line.Trim())
                               .ToArray();
                }
                else
                {
                    Log($"[WARNING] {processFilePath} not found! Creating a default one...");
                    File.WriteAllText(processFilePath, "LeagueClient\nLeague of Legends\n");
                    return new string[] { "LeagueClient", "League of Legends" };
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading process list: {ex.Message}");
                return new string[0];
            }
        }

        private void LockProcessFile()
        {
            try
            {
                lockFS = new FileStream(processFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Log($"Locked {processFilePath} for reading. Preventing modifications.");
            }
            catch (Exception ex)
            {
                Log($"Failed to lock {processFilePath}: {ex.Message}");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.Write("Enter service shutdown password: ");
            string? inputPassword = Console.ReadLine();

            const string ShutdownPassword = "SuperSecret123";

            if (inputPassword != ShutdownPassword)
            {
                Console.WriteLine("Ah, ah, ah. You didn't say the magic word.");
                return;
            }
            const string serviceStoppingMsg = "Correct password entered. Stopping service...";
            Console.WriteLine(serviceStoppingMsg);
            Log(serviceStoppingMsg);
            await base.StopAsync(cancellationToken);
        }

        private void UnlockProcessFile()
        {
            try
            {
                lockFS?.Close();
                lockFS = null;
                Log($"{processFilePath} is now unlocked for editing.");
            }
            catch (Exception ex)
            {
                Log($"Error unlocking {processFilePath}: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"[{timestamp}] {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGING ERROR] Failed to write to log file: {ex.Message}");
            }
        }
    }
}
