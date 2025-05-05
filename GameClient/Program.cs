// Program.cs — Console entry point for CoreClient
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using City2.Core.Networking;

namespace City2.Client.ConsoleClient
{
    public class Config
    {
        public string ServerAddress { get; set; } = "57.128.228.75";
        public int ServerPort { get; set; } = 9050;

        public static Config Load(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<Config>(json, options) ?? new Config();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading config: " + e.Message);
                return new Config();
            }
        }
    }

    class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine(message);
        public void Error(string message) => Console.Error.WriteLine(message);
    }

    class Program
    {
        static void Main(string[] args)
        {
            string configPath = "config.json";
            Config config = Config.Load(configPath);
            Console.WriteLine($"Loaded config: {config.ServerAddress}:{config.ServerPort}");

            var client = new CoreClient(config.ServerAddress, config.ServerPort, new ConsoleLogger());
            var clientThread = new Thread(client.RunEventLoop);
            clientThread.Start();

            Console.WriteLine("Type 'login <username>', 'logout', 'chat <message>', or 'pos <x> <y>' to interact.");

            while (true)
            {
                string? input = Console.ReadLine();
                if (input == null) continue;

                var parts = input.Split(' ', 2);
                string cmd = parts[0].ToLower();

                switch (cmd)
                {
                    case "login":
                        if (parts.Length > 1) client.Login(parts[1]);
                        else Console.WriteLine("Usage: login <username>");
                        break;
                    case "logout":
                        client.Logout();
                        break;
                    case "chat":
                        if (parts.Length > 1) client.SendChat(parts[1]);
                        else Console.WriteLine("Usage: chat <message>");
                        break;
                    case "pos":
                        var coords = parts.Length > 1 ? parts[1].Split(' ') : Array.Empty<string>();
                        if (coords.Length == 2 && float.TryParse(coords[0], out float x) && float.TryParse(coords[1], out float y))
                        {
                            client.SendPosition(x, y);
                        }
                        else
                        {
                            Console.WriteLine("Usage: pos <x> <y>");
                        }
                        break;
                }
            }
        }
    }
}
