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
        private readonly string baseDirectory;
        private readonly string processFilePath;
        private readonly string logFilePath;
        private FileStream? lockFS;

        public Worker()
        {
            baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            processFilePath = Path.Combine(baseDirectory, "processes.txt");
            logFilePath = Path.Combine(baseDirectory, "destroyer-log.log");

            EnsureRequiredFiles();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log("Service started.");

            try
            {
                lockFS = new FileStream(processFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Log($"Locked {processFilePath} for reading. Preventing modifications.");
            }
            catch (Exception ex)
            {
                Log($"Failed to lock {processFilePath}: {ex.Message}");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var targetProcesses = File.ReadAllLines(processFilePath)
                                       .Where(line => !string.IsNullOrWhiteSpace(line))
                                       .Select(line => line.Trim())
                                       .ToArray();

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
                        Log($"[{DateTime.Now}] Killing process: {process.ProcessName} (PID: {process.Id})");
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    Log($"[{DateTime.Now}] Error: {ex.Message}");
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Log("Service stopping... unlocking process list file.");

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

            await base.StopAsync(cancellationToken);
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

        private void EnsureRequiredFiles()
        {
            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Close();
                Log("Created log file.");
            }
            else
            {
                File.WriteAllText(logFilePath, String.Empty);
                Log("Clearing Old Log Entries.");
            }

            if (!File.Exists(processFilePath))
            {
                File.WriteAllText(processFilePath, "LeagueClient\nLeague of Legends\nnotepad\n");
                Log("Created default processes file");
            }
        }
    }
}
