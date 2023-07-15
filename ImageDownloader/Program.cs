using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ImageDownloader
{
    class Program
    {
        private static int totalCount;
        private static int parallelism;
        private static string savePath;
        private static bool isCancelled;
        private static int downloadedCount;
        private static object lockObj = new object();

        public delegate void ProgressEventHandler(int current, int total);
        public static event ProgressEventHandler ProgressEvent;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKeyPress);
            ProgressEvent += PrintProgress;

            // Read input from JSON file (optional)
            ReadInputFromJson();

            // Read input from console (if JSON file is not used or some fields are missing)
            ReadInputFromConsole();

            // Create save directory if it doesn't exist
            Directory.CreateDirectory(savePath);

            // Start downloading images
            Console.WriteLine($"Downloading {totalCount} images ({parallelism} parallel downloads at most)\n");

            List<Task> downloadTasks = new List<Task>();
            for (int i = 1; i <= totalCount; i++)
            {
                if (isCancelled)
                    break;

                downloadTasks.Add(DownloadImageAsync(i));

                if (downloadTasks.Count >= parallelism || i == totalCount)
                {
                    Task.WaitAny(downloadTasks.ToArray());
                    downloadTasks.RemoveAll(t => t.IsCompleted);
                }
            }

            Console.WriteLine("\nDownload completed.");
        }

        static async Task DownloadImageAsync(int index)
        {
            string imageUrl = $"https://picsum.photos/200/300?random={Guid.NewGuid()}";
            string fileName = $"{index}.png";
            string filePath = Path.Combine(savePath, fileName);

            try
            {
                using (WebClient webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync(imageUrl, filePath);

                    lock (lockObj)
                    {
                        downloadedCount++;
                        ProgressEvent?.Invoke(downloadedCount, totalCount);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError downloading image {index}: {ex.Message}");
            }
        }

        static void ReadInputFromJson()
        {
            try
            {
                string inputJson = File.ReadAllText("Input.json");
                InputData inputData = Newtonsoft.Json.JsonConvert.DeserializeObject<InputData>(inputJson);

                totalCount = inputData.Count;
                parallelism = inputData.Parallelism;
                savePath = inputData.SavePath;
            }
            catch (Exception)
            {
                // JSON file cannot be read or deserialized, continue with console input
            }
        }

        static void ReadInputFromConsole()
        {
            Console.Write("Enter the number of images to download: ");
            totalCount = GetIntegerInput();

            Console.Write("Enter the maximum parallel download limit: ");
            parallelism = GetIntegerInput();

            Console.Write("Enter the save path (default: ./outputs): ");
            savePath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(savePath))
                savePath = "./outputs";
        }

        static int GetIntegerInput()
        {
            int number;
            while (!int.TryParse(Console.ReadLine(), out number) || number <= 0)
            {
                Console.Write("Invalid input. Please enter a positive integer: ");
            }
            return number;
        }

        static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            isCancelled = true;
            Console.WriteLine("\n\nDownload cancelled. Cleaning up...");
            CleanUp();
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            if (!isCancelled)
            {
                Console.WriteLine("\n\nDownload interrupted. Cleaning up...");
                CleanUp();
            }
        }

        static void CleanUp()
        {
            // Delete downloaded images
            for (int i = 1; i <= downloadedCount; i++)
            {
                string fileName = $"{i}.png";
                string filePath = Path.Combine(savePath, fileName);
                File.Delete(filePath);
            }

            // Delete save directory if empty
            if (Directory.GetFiles(savePath).Length == 0)
                Directory.Delete(savePath);

            Console.WriteLine("Cleanup completed.");
        }

        static void PrintProgress(int current, int total)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"Progress: {current}/{total}");
        }
    }

    class InputData
    {
        public int Count { get; set; }
        public int Parallelism { get; set; }
        public string SavePath { get; set; }
    }
}
