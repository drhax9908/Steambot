using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using SteamKit2;
using ExempleBot;
using SteamTrade;

namespace SteamBot
{
    /// <summary>
    /// A class that manages SteamBot processes.
    /// </summary>
    public class BotManager : IDisposable
    {
        public List<RunningBot> botProcs { get; private set; }
        private Log mainLog;
        private bool useSeparateProcesses;
        private bool disposed;
        private bool canQuery;
        public Database DB { get; private set; }

        public BotManager()
        {
            useSeparateProcesses = false;
            new List<Bot>();
            botProcs = new List<RunningBot>();
        }

        ~BotManager()
        {
            Dispose(false);
        }

        public Configuration ConfigObject { get; private set; }

        /// <summary>
        /// Loads a configuration file to use when creating bots.
        /// </summary>
        /// <param name="configFile"><c>false</c> if there was problems loading the config file.</param>
        public bool LoadConfiguration(string configFile)
        {
            configFile = (AppDomain.CurrentDomain.BaseDirectory + configFile);

            try
            {
                FileStream file = File.OpenRead(configFile);
            }
            catch(Exception e)
            {
                return false;
            }

            try
            {
                ConfigObject = Configuration.LoadConfiguration(configFile);
            }
            catch (JsonReaderException)
            {
                // handle basic json formatting screwups
                ConfigObject = null;
            }

            if (ConfigObject == null)
                return false;

            DB = new Database(ConfigObject.DatabaseHost, ConfigObject.DatabaseUserName, ConfigObject.DatabasePassword, ConfigObject.DatabasePort, ConfigObject.DatabaseName);
            InitialiseDatabase();

            useSeparateProcesses = ConfigObject.UseSeparateProcesses;

            mainLog = new Log(ConfigObject.MainLog, null, Log.LogLevel.Debug, Log.LogLevel.Debug);

            for (int i = 0; i < ConfigObject.Bots.Length; i++)
            {
                Configuration.BotInfo info = ConfigObject.Bots[i];
                if (ConfigObject.AutoStartAllBots || info.AutoStart)
                {
                    mainLog.Info("Launching Bot " + info.DisplayName + "...");
                }

                var v = new RunningBot(useSeparateProcesses, i, ConfigObject, DB);
                botProcs.Add(v);
            }

            return true;
        }

        private void InitialiseDatabase()
        {
            if (DB.connection.State == System.Data.ConnectionState.Open)
            {
                string database = "CREATE DATABASE IF NOT EXISTS " + ConfigObject.DatabaseName;
                canQuery = DB.QUERY(database);

                DB.connection.Close();
                DB = new Database(ConfigObject.DatabaseHost, ConfigObject.DatabaseUserName, ConfigObject.DatabasePassword, ConfigObject.DatabasePort, ConfigObject.DatabaseName);

                string table1 = "CREATE TABLE IF NOT EXISTS `tradeoffers` (" +
                                "`ID` int(11) NOT NULL AUTO_INCREMENT PRIMARY KEY," +
                                "`steamID` varchar(125) NOT NULL," +
                                " `TradeOfferID` bigint(255) NOT NULL," +
                                " `tradeValue` varchar(125) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1";
                canQuery = DB.QUERY(table1);
                string table2 = "CREATE TABLE IF NOT EXISTS `items` (" +
                                "`ID` int(11) NOT NULL AUTO_INCREMENT PRIMARY KEY," +
                                "`itemName` varchar(125) NOT NULL," +
                                "`ToGive` int(15) NOT NULL," +
                                " `tradeoffersid` bigint(255) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1";
                canQuery = DB.QUERY(table2);
                string table3 = "CREATE TABLE IF NOT EXISTS `smitems` (" +
                                 "`ID` int(11) NOT NULL AUTO_INCREMENT PRIMARY KEY," +
                                 "`itemName` varchar(125) NOT NULL," +
                                 "`last_updated` int(35) NOT NULL," +
                                 "`value` int(15) NOT NULL," +
                                 " `quantity` int(15) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=latin1";
                canQuery = DB.QUERY(table3);
            }
            else
            {
                Console.WriteLine("Could NOT connect to database !");
            }
        }

        /// <summary>
        /// Starts the bots that have been configured.
        /// </summary>
        /// <returns><c>false</c> if there was something wrong with the configuration or logging.</returns>
        public bool StartBots()
        {
            if (ConfigObject == null || mainLog == null)
                return false;

            foreach (var runningBot in botProcs)
            {
                runningBot.Start();

                Thread.Sleep(2000);
            }

            return true;
        }

        /// <summary>
        /// Kills all running bot processes and cleans up loose ends
        /// </summary>
        public void StopBots()
        {
            if (mainLog != null)
            {
                mainLog.Debug("Shutting down all bot processes.");
                StopBotsProcesses();
                mainLog.Dispose();
                mainLog = null;
            }
            else
            {
                StopBotsProcesses();
            }
            if(DB != null)
                DB.connection.Close();
        }

        public void StopBotsProcesses()
        {
            foreach (var botProc in botProcs)
            {
                botProc.Stop();
            }
        }

        /// <summary>
        /// Kills a single bot process given that bots index in the configuration.
        /// </summary>
        /// <param name="index">A zero-based index.</param>
        public void StopBot(int index)
        {
            mainLog.Debug(String.Format("Killing bot process {0}.", index));
            if (index < botProcs.Count)
            {
                botProcs[index].Stop();
            }
        }

        /// <summary>
        /// Stops a bot given that bots configured username.
        /// </summary>
        /// <param name="botUserName">The bot's username.</param>
        public void StopBot(string botUserName)
        {
            mainLog.Debug(String.Format("Killing bot with username {0}.", botUserName));

            var res = from b in botProcs
                      where b.BotConfig.Username.Equals(botUserName, StringComparison.CurrentCultureIgnoreCase)
                      select b;

            foreach (var bot in res)
            {
                bot.Stop();
            }
        }

        /// <summary>
        /// Starts a bot in a new process given that bot's index in the configuration.
        /// </summary>
        /// <param name="index">A zero-based index.</param>
        public void StartBot(int index)
        {
            mainLog.Debug(String.Format("Starting bot at index {0}.", index));

            if (index < ConfigObject.Bots.Length)
            {
                botProcs[index].Start();
            }
        }

        /// <summary>
        /// Starts a bot given that bots configured username.
        /// </summary>
        /// <param name="botUserName">The bot's username.</param>
        public void StartBot(string botUserName)
        {
            mainLog.Debug(String.Format("Starting bot with username {0}.", botUserName));

            var res = from b in botProcs
                      where b.BotConfig.Username.Equals(botUserName, StringComparison.CurrentCultureIgnoreCase)
                      select b;

            foreach (var bot in res)
            {
                bot.Start();
            }
            
        }

        /// <summary>
        /// Sets the SteamGuard auth code on the given bot
        /// </summary>
        /// <param name="index">The bot's index</param>
        /// <param name="AuthCode">The auth code</param>
        public void AuthBot(int index, string AuthCode)
        {
            if (index < botProcs.Count)
            {
                if (botProcs[index].UsingProcesses)
                {
                    //  Write out auth code to the bot process' stdin
                    StreamWriter BotStdIn = botProcs[index].BotProcess.StandardInput;

                    BotStdIn.WriteLine("auth " + AuthCode);
                    BotStdIn.Flush();
                }
                else
                {
                    botProcs[index].TheBot.AuthCode = AuthCode;
                }
            }
        }

        /// <summary>
        /// Sends the BotManager command to the target Bot
        /// </summary>
        /// <param name="index">The target bot's index</param>
        /// <param name="command">The command to be 
        /// uted</param>
        public void SendCommand(int index, string command)
        {
            mainLog.Debug(String.Format("Sending command \"{0}\" to Bot at index {1}", command, index));
            if (index < botProcs.Count)
            {
                if (botProcs[index].IsRunning)
                {
                    if (botProcs[index].UsingProcesses)
                    {
                        //  Write out the exec command to the bot process' stdin
                        StreamWriter BotStdIn = botProcs[index].BotProcess.StandardInput;

                        BotStdIn.WriteLine("exec " + command);
                        BotStdIn.Flush();
                    }
                    else
                    {
                        botProcs[index].TheBot.HandleBotCommand(command);
                    }
                }
                else
                {
                    mainLog.Warn(String.Format("Bot at index {0} is not running. Use the 'Start' command first", index));
                }
            }
            else
            {
                mainLog.Warn(String.Format("Invalid Bot index: {0}", index));
            }
        }

        /// <summary>
        /// Sends the BotManager input to the target Bot
        /// </summary>
        /// <param name="index">The target bot's index</param>
        /// <param name="command">The input to give the bot</param>
        public void SendInput(int index, string input)
        {
            mainLog.Debug(String.Format("Sending input \"{0}\" to Bot at index {1}", input, index));
            if (index < botProcs.Count)
            {
                if (botProcs[index].IsRunning)
                {
                    if (botProcs[index].UsingProcesses)
                    {
                        //  Write out the exec command to the bot process' stdin
                        StreamWriter BotStdIn = botProcs[index].BotProcess.StandardInput;

                        BotStdIn.WriteLine("input " + input);
                        BotStdIn.Flush();
                    }
                    else
                    {
                        botProcs[index].TheBot.HandleInput(input);
                    }
                }
                else
                {
                    mainLog.Warn(String.Format("Bot at index {0} is not running. Use the 'Start' command first", index));
                }
            }
            else
            {
                mainLog.Warn(String.Format("Invalid Bot index: {0}", index));
            }
        }
        

        /// <summary>
        /// A method to return an instance of the <c>bot.BotControlClass</c>.
        /// </summary>
        /// <param name="bot">The bot.</param>
        /// <param name="sid">The steamId.</param>
        /// <returns>A <see cref="UserHandler"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the control class type does not exist.</exception>
        public static UserHandler UserHandlerCreator(Bot bot, SteamID sid)
        {
            Type controlClass = Type.GetType(bot.BotControlClass);

            if (controlClass == null)
                throw new ArgumentException("Configured control class type was null. You probably named it wrong in your configuration file.", "bot");

            return (UserHandler)Activator.CreateInstance(
                    controlClass, new object[] { bot, sid });
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;
            StopBots();
            if (disposing)
            {
                foreach (IDisposable bot in botProcs)
                    bot.Dispose();
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region Nested RunningBot class

        /// <summary>
        /// Nested class that holds the information about a spawned bot process.
        /// </summary>
        public class RunningBot : IDisposable
        {
            private const string BotExecutable = "SteamBot.exe";
            private readonly Configuration config;
            private bool disposed;
            private Database database;

            /// <summary>
            /// Creates a new instance of <see cref="RunningBot"/> class.
            /// </summary>
            /// <param name="useProcesses">
            /// <c>true</c> indicates that this bot is ran in a thread;
            /// <c>false</c> indicates it is ran in a separate process.
            /// </param>
            /// <param name="index">The index of the bot in the configuration.</param>
            /// <param name="config">The bots configuration object.</param>
            public RunningBot(bool useProcesses, int index, Configuration config, Database database)
            {
                this.config = config;
                UsingProcesses = useProcesses;
                BotConfigIndex = index;
                BotConfig = config.Bots[BotConfigIndex];
                this.config = config;
                this.database = database;
            }

            ~RunningBot()
            {
                Dispose(false);
            }

            public bool UsingProcesses { get; private set; }

            public int BotConfigIndex { get; private set; }

            public Configuration.BotInfo BotConfig { get; private set; }

            public Process BotProcess { get; set; }

            // will not be null in threaded mode. will be null in process mode.
            public Bot TheBot { get; set; }

            public bool IsRunning = false;

            public void Stop()
            {
                if (IsRunning && UsingProcesses)
                {
                    if (!BotProcess.HasExited)
                    {
                        BotProcess.Kill();
                        IsRunning = false;
                    }
                }
                else if (TheBot != null && TheBot.IsRunning)
                {
                    TheBot.StopBot();
                    IsRunning = false;
                }
            }

            public void Start()
            {
                if (UsingProcesses)
                {
                    if (!IsRunning)
                    {
                        SpawnSteamBotProcess(BotConfigIndex);
                        IsRunning = true;
                    }
                }
                else if (TheBot == null)
                {
                    SpawnBotThread(BotConfig);
                    IsRunning = true;
                }
                else if (!TheBot.IsRunning)
                {
                    SpawnBotThread(BotConfig);
                    IsRunning = true;
                }
            }

            private void SpawnSteamBotProcess(int botIndex)
            {
                // we don't do any of the standard output redirection below. 
                // we could but we lose the nice console colors that the Log class uses.

                Process botProc = new Process();
                botProc.StartInfo.FileName = BotExecutable;
                botProc.StartInfo.Arguments = @"-bot " + botIndex;

                // Set UseShellExecute to false for redirection.
                botProc.StartInfo.UseShellExecute = false;

                // Redirect the standard output.  
                // This stream is read asynchronously using an event handler.
                botProc.StartInfo.RedirectStandardOutput = false;

                // Redirect standard input to allow manager commands to be read properly
                botProc.StartInfo.RedirectStandardInput = true;

                // Set our event handler to asynchronously read the output.
                //botProc.OutputDataReceived += new DataReceivedEventHandler(BotStdOutHandler);

                botProc.Start();

                BotProcess = botProc;

                // Start the asynchronous read of the bot output stream.
                //botProc.BeginOutputReadLine();
            }


            private void SpawnBotThread(Configuration.BotInfo botConfig)
            {
                // the bot object itself is threaded so we just build it and start it.
                Bot b = new Bot(botConfig,
                                config.ApiKeyBackPack,
                                config.ApiKey,
                                UserHandlerCreator,
                                database,
                                config.MySteamID,
                                true);
                TheBot = b;
                TheBot.StartBot();
            }

            //private static void BotStdOutHandler(object sender, DataReceivedEventArgs e)
            //{
            //    if (!String.IsNullOrEmpty(e.Data))
            //    {
            //        Console.WriteLine(e.Data);
            //    }
            //}

            private void Dispose(bool disposing)
            {
                if (disposed)
                    return;
                Stop();
                if (disposing && TheBot != null)
                    TheBot.Dispose();
                disposed = true;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        #endregion Nested RunningBot class
    }
}
