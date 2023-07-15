using System.Net;

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

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
            ProgressEvent += PrintProgress;

            ReadInput();

            Directory.CreateDirectory(savePath);

            Console.WriteLine($"Downloading {totalCount} images ({parallelism} parallel downloads at most)\n");

            List<Task> downloadTasks = new List<Task>();
            for (int i = 1; i <= totalCount; i++)
            {
                if (isCancelled)
                    break;

                downloadTasks.Add(DownloadImageAsync(i));

                if (downloadTasks.Count >= parallelism || i == totalCount)
                {
                    await Task.WhenAny(downloadTasks);
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

        static void ReadInput()
        {
            Console.Write("Enter the number of images to download: ");
            totalCount = GetPositiveIntegerInput();

            Console.Write("Enter the maximum parallel download limit: ");
            parallelism = GetPositiveIntegerInput();

            Console.Write("Enter the save path (default: ./outputs): ");
            savePath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(savePath))
                savePath = "./outputs";
        }

        static int GetPositiveIntegerInput()
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
            for (int i = 1; i <= downloadedCount; i++)
            {
                string fileName = $"{i}.png";
                string filePath = Path.Combine(savePath, fileName);
                File.Delete(filePath);
            }

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
}
