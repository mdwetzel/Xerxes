using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace IRC_Bot
{
    /// <summary>
    /// Represents a bot that recognizes and responds to various commands in 
    /// an IRC chat room
    /// </summary>
    public class Bot
    {
        private readonly bool invisible;
        private readonly string nickname;
        private readonly string realName;
        private readonly string username;

        public Bot(string nickname = "antieffortbot", bool invisible = true)
        {
            this.nickname = nickname;
            this.invisible = invisible;

            username = "C# Bot";
            realName = "A C# bot!";
        }

        public string Nickname
        {
            get { return nickname; }
        }

        public string Username
        {
            get { return username; }
        }

        public string RealName
        {
            get { return realName; }
        }

        public bool Invisible
        {
            get { return invisible; }
        }
    }

    public enum MessageType
    {
        Channel,
        User,
        Command
    }

    /// <summary>
    /// Represents an IRC server to connect to
    /// </summary>
    public class Server
    {
        #region Delegates

        public delegate void Join(string data);

        public delegate void Message(string data);

        public delegate void Part(string data);

        #endregion

        private const string Channel = "#antieffortbot";
        private readonly Bot bot = new Bot("AEBOT", false);
        private readonly TcpClient client = new TcpClient();
        private readonly string host;
        private readonly int port;
        private readonly Stopwatch stopWatch = new Stopwatch();
        private readonly Stopwatch waitWatch = new Stopwatch();
        private string botnick = "AntieffortBot";
        private Thread pingSender;
        private NetworkStream stream;
        private StreamReader streamReader;
        private StreamWriter streamWriter;

        public Server(string host, int port)
        {
            this.host = host;
            this.port = port;

            Joined += ServerJoined;
            Messaged += ServerMessaged;
            Parted += ServerParted;

            stopWatch.Start();
            waitWatch.Start();
        }

        public event Join Joined;

        public event Message Messaged;

        public event Part Parted;

        public void Connect(string host, int port)
        {
            client.Connect(host, port);
            stream = client.GetStream();
            streamReader = new StreamReader(stream);
            streamWriter = new StreamWriter(stream);

            Send(String.Format("USER {0} {1} * :{2}", bot.Username, bot.Invisible, bot.RealName));
            Send(String.Format("NICK {0}", bot.Nickname));
            Send(String.Format("JOIN {0}", Channel));

            pingSender = new Thread(Ping);
            pingSender.Start();

            Work();
        }

        public void Connect()
        {
            Connect(host, port);
        }

        private void ServerParted(string data)
        {
            Send(string.Format("PRIVMSG {0} :Oh, shit. He decided to move onto more promising channels.", Channel));
        }

        private void Work()
        {
            while (true)
            {
                try
                {
                    string ircCommand;
                    while ((ircCommand = streamReader.ReadLine()) != null)
                    {
                        string[] parts = ircCommand.Split(' ');

                        string command = parts[1];

                        PrintCommand(command);

                        switch (command)
                        {
                            case "JOIN":
                                if (Joined != null)
                                {
                                    Joined(ircCommand);
                                }
                                break;
                            case "PART":
                                if (Parted != null)
                                    Parted(ircCommand);
                                break;
                            case "PRIVMSG":
                                if (Messaged != null)
                                {
                                    Messaged(ircCommand);
                                }
                                break;
                        }
                        Console.WriteLine("{0} - {1}", DateTime.Now.ToString("T"), ircCommand);
                    }
                }
                catch (IndexOutOfRangeException)
                {
                }
            }
        }

        private static void PrintCommand(string command)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.WriteLine(command);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private void ServerMessaged(string data)
        {
            //string nick = data.Substring(1, data.IndexOf("!") - 1);

            string pattern = @"(?<=PRIVMSG).+(?=:)";

            Match to = Regex.Match(data, pattern);

            pattern = string.Format(@"(?<=PRIVMSG {0}|{1} :).+", Channel, "AEBOT");

            to = Regex.Match(data, pattern);

            string message = null;

            var commands = new List<string> {"weather", "time", "uptime", "date"};

            if (to.Value.StartsWith("!bot"))
            {
                if (to.Value.Contains(" "))
                {
                    string[] parts = to.Value.Split(' ');
                    if (parts.Length > 0)
                    {
                        string innerComm = parts[1].ToLower();
                        if (string.IsNullOrWhiteSpace(innerComm))
                        {
                            message = string.Format("How could I possibly understand that?");
                        }
                        else
                        {
                            if (commands.Contains(innerComm))
                            {
                                switch (innerComm)
                                {
                                    case "weather":

                                        break;
                                    case "time":
                                        message = string.Format("The time is now {0} {1}.", DateTime.Now.ToString("T"),
                                                                TimeZone.CurrentTimeZone.DaylightName);
                                        break;
                                    case "uptime":
                                        TimeSpan t = stopWatch.Elapsed;

                                        message = t.Minutes > 0
                                                      ? string.Format(
                                                          "I've been here for {0} minutes and {1} seconds.", t.Minutes,
                                                          t.Seconds)
                                                      : string.Format("I've been here for {0} seconds.", t.Seconds);
                                        break;
                                }
                            }
                            else
                            {
                                message = "That was not a recognized command. You fool!";
                            }
                        }
                    }
                }
                else
                {
                    message = "Yes?";
                }

                Send(string.Format("PRIVMSG {0} :{1}", Channel, message));
            }
        }

        public void Send(string data)
        {
            streamWriter.WriteLine(data);
            streamWriter.Flush();
        }

        private void ServerJoined(string data)
        {
            string nick = data.Substring(1, data.IndexOf('!') - 1);

            Send(nick != bot.Nickname
                     ? string.Format("PRIVMSG {0} :Greetings, {1}, and welcome to hel...er, I mean, {0}.", Channel, nick)
                     : string.Format("PRIVMSG {0} :Oh, my. You should be so happy. I'm here.", Channel));
        }

        private void Ping()
        {
            while (true)
            {
                Send(string.Format("PING :{0}", host));
                Thread.Sleep(15000);
            }
        }
    }

    internal class Program
    {
        private static void Main()
        {
            Server serv = new Server("avarice.az.us.synirc.net", 6666);

            serv.Connect();
        }
    }
}