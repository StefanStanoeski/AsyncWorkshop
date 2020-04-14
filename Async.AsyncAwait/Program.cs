using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Async.AsyncAwait
{
    class Program
    {
        const int _maxProcessedDocsAtGivenTime = 4;
        static int _totalProcessedDocs = 0;
        const int _targetProcessedDocsCount = 10;

        static SemaphoreSlim _semaphoreLimitingDocsProcessedAtTheSameTime = new SemaphoreSlim(_maxProcessedDocsAtGivenTime);

        static string _pathToFolder = "D:\\AsyncFileStorage";

        static List<string> _filePathsInTargetFolder = new List<string>();
        static BufferBlock<string> _filePathsToProcess = new BufferBlock<string>();
        static Dictionary<string, string> _processedFilesMappedToFileName = new Dictionary<string, string>();

        private static object _lock = new object();

        static async Task Main(string[] args)
        {
            Console.WriteLine($"Starting FileProcessor, please start inserting files into {_pathToFolder} directory.");

            var fileReaderTask = new Task(WatchForFiles);

            fileReaderTask.Start();

            while(_totalProcessedDocs < _targetProcessedDocsCount)
            {
                await RunFileProcessingAsync();
            }

            fileReaderTask.Wait();

            Console.WriteLine("File paths contain the following data:");

            foreach (var item in _processedFilesMappedToFileName)
            {
                Console.WriteLine($"{item.Key}: {item.Value}");
            }

            Console.ReadKey();
        }

        static async Task RunFileProcessingAsync()
        {
            for (int i = 0; i < _maxProcessedDocsAtGivenTime; i++)
            {
                await ProcessFileAsync();
            }
        }

        static void WatchForFiles()
        {
            while (true)
            {
                Monitor.Enter(_lock);

                if (_totalProcessedDocs == _targetProcessedDocsCount)
                {
                    Monitor.Exit(_lock);

                    break;
                }

                var filePaths = Directory.GetFiles(_pathToFolder).ToList();

                var newFilePaths = filePaths.Except(_filePathsInTargetFolder).ToList();

                if (newFilePaths.Any())
                {
                    Console.WriteLine($"Found {newFilePaths.Count} new file paths.");

                    _filePathsInTargetFolder.AddRange(newFilePaths);

                    foreach (var path in newFilePaths)
                    {
                        _filePathsToProcess.Post(path);
                    }
                }

                Monitor.Exit(_lock);
            }
        }

        static async Task ProcessFileAsync()
        {
            while (true)
            {
                Monitor.Enter(_lock);

                if (_totalProcessedDocs == _targetProcessedDocsCount)
                {
                    Monitor.Exit(_lock);

                    break;
                }

                if (_filePathsToProcess.Count > 0)
                {
                    _semaphoreLimitingDocsProcessedAtTheSameTime.Wait();

                    await ReadFileAsync();

                    _semaphoreLimitingDocsProcessedAtTheSameTime.Release();
                }

                Monitor.Exit(_lock);
            }
        }

        private static async Task ReadFileAsync()
        {
            var filePath = await _filePathsToProcess.ReceiveAsync();

            var fileName = filePath.Split("\\").Last();

            Console.WriteLine($"Processing file with name: {fileName}.");

            var fileContent = File.ReadAllText(filePath);

            _processedFilesMappedToFileName.Add(fileName, fileContent);

            _totalProcessedDocs++;

            Console.WriteLine($"Processing for {fileName} finished. Processed {_totalProcessedDocs} documents so far.");
        }
    }
}
