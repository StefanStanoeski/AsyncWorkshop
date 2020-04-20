using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Async.ConcurrentCollections
{
    class Program
    {
        const int _maxProcessedDocsAtGivenTime = 4;
        static int _totalProcessedDocs = 0;
        const int _targetProcessedDocsCount = 10;

        static SemaphoreSlim _semaphoreLimitingDocsProcessedAtTheSameTime = new SemaphoreSlim(_maxProcessedDocsAtGivenTime);

        static string _pathToFolder = "D:\\AsyncFileStorage";

        static List<string> _filePathsInTargetFolder = new List<string>();

        static ConcurrentQueue<string> _filePathsToProcess = new ConcurrentQueue<string>();

        static ConcurrentDictionary<string, string> _processedFilesMappedToFileName = new ConcurrentDictionary<string, string>();

        private static object _lock = new object();

        static void Main(string[] args)
        {
            Console.WriteLine($"Starting FileProcessor, please start inserting files into {_pathToFolder} directory.");

            var fileWatcherTask = new Task(WatchForFiles);
            fileWatcherTask.Start();

            RunFileProcessing();

            fileWatcherTask.Wait();

            Console.WriteLine("File paths contain the following data:");

            foreach (var item in _processedFilesMappedToFileName)
            {
                Console.WriteLine($"{item.Key}: {item.Value}");
            }

            Console.ReadKey();
        }

        static void RunFileProcessing()
        {
            Parallel.For(_totalProcessedDocs, _targetProcessedDocsCount, new ParallelOptions { MaxDegreeOfParallelism = _maxProcessedDocsAtGivenTime }, (_totalProcessedDocs) =>
            {
                ProcessFile();
            });
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
                        _filePathsToProcess.Enqueue(path);
                    }
                }

                Monitor.Exit(_lock);
            }
        }

        static void ProcessFile()
        {
            while (true)
            {
                Monitor.Enter(_lock);

                if (_totalProcessedDocs == _targetProcessedDocsCount)
                {
                    Monitor.Exit(_lock);

                    break;
                }

                if (_filePathsToProcess.Any())
                {
                    _semaphoreLimitingDocsProcessedAtTheSameTime.Wait();

                    _filePathsToProcess.TryDequeue(out string filePath);

                    var fileName = filePath.Split("\\").Last();

                    Console.WriteLine($"Processing file with name: {fileName}.");

                    var fileContent = File.ReadAllText(filePath);

                    _processedFilesMappedToFileName.TryAdd(fileName, fileContent);

                    _totalProcessedDocs++;

                    Console.WriteLine($"Processing for {fileName} finished. Processed {_totalProcessedDocs} documents so far.");

                    _semaphoreLimitingDocsProcessedAtTheSameTime.Release();
                }

                Monitor.Exit(_lock);
            }
        }
    }
}
