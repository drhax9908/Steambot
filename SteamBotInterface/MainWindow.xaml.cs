using ExempleBot;
using NDesk.Options;
using SteamBot;
using SteamKit2;
using SteamTrade;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SteamBotInterface
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static OptionSet opts = new OptionSet()
        {
            {"bot=", "launch a configured bot given that bots index in the configuration array.", 
                b => botIndex = Convert.ToInt32(b) } ,
                { "help", "shows this help text", p => showHelp = (p != null) }
        };

        private readonly Log Log;
        private static bool showHelp;
        private static int botIndex = -1;
        private static BotManager manager;
        private static bool isclosing = false;
        private int wpfBotIndex = -1;
        private Thread console;

        public MainWindow()
        {
            InitializeComponent();
            //AttachConsole.CreateConsole();

            ThreadStart threadDelegate = new ThreadStart(Start);
            console = new Thread(threadDelegate);
            console.Start();

            // Create a timer with a two second interval.
            System.Timers.Timer reloadGUI = new System.Timers.Timer(2000);
            reloadGUI.Elapsed += ReloadGUINow;
            reloadGUI.AutoReset = true;
            reloadGUI.Enabled = true;
        }

        private void ReloadGUINow(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (wpfBotIndex != -1)
                UpdateWindow(wpfBotIndex);
        }

        [STAThread]
        public void Start()
        {
            BotManagerMode();
        }

        #region WPF

        public void UpdateWindow(int botIndex)
        {
            botIndex--;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, (SendOrPostCallback)delegate
            {
                Bot bot = manager.botProcs[botIndex].TheBot;
                lblBotName.Content = bot.DisplayNamePrefix + " " + bot.DisplayName+"'s status";
                lblStatus.Foreground = bot.IsRunning ? Brushes.Green : Brushes.Red;
                lblStatus.Content = bot.IsRunning ? "Running !" : "Stopped !";
                lblAdmins.Content = "";
                foreach(string admin in bot.MySteamID)
                    lblAdmins.Content += admin + " ";
                lblLogFile.Content = bot.logFile;
                lblControlClass.Content = bot.BotControlClass;

                lblApiKey.Content = manager.ConfigObject.ApiKey;
                lblMainLogFile.Content = manager.ConfigObject.MainLog;
                lblRequireSteamGaurd.Foreground = bot.requireSteamGuard ? Brushes.Red : Brushes.Green;
                lblRequireSteamGaurd.Content = bot.requireSteamGuard ? "Auth required !" : "Logged in !";

                btnPreviousBot.IsEnabled = botIndex == 0 ? false : true;
                btnNextBot.IsEnabled = botIndex + 1 == manager.botProcs.Count ? false : true;

                lblDatabaseName.Foreground = bot.DB.isConnected ? Brushes.Green : Brushes.Red;
                lblDatabaseName.Content = bot.DB.isConnected ? "Connected !" : "Disconnected ! (Trade offer not saved !)";

            }, null);
        }

        private void WindowInterface_Closed(object sender, EventArgs e)
        {
            isclosing = true;
            manager.StopBots();
            console.Interrupt();
            console.Abort();
        }

        private void btnPreviousBot_Click(object sender, RoutedEventArgs e)
        {
            wpfBotIndex--;
            UpdateWindow(wpfBotIndex);
        }

        private void btnNextBot_Click(object sender, RoutedEventArgs e)
        {
            wpfBotIndex++;
            UpdateWindow(wpfBotIndex);
        }

        #endregion WPF

        #region SteamBot Operational Modes

        // This mode is to run a single Bot until it's terminated.
        private void BotMode(int botIndex)
        {
            if (!File.Exists("settings.json"))
            {
                Log.Error("No settings.json file found.");
                return;
            }

            Configuration configObject;
            try
            {
                configObject = Configuration.LoadConfiguration("settings.json");
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                // handle basic json formatting screwups
                Console.WriteLine("settings.json file is corrupt or improperly formatted.");
                return;
            }

            if (botIndex >= configObject.Bots.Length)
            {
                Console.WriteLine("Invalid bot index.");
                return;
            }

            Bot b = new Bot(configObject.Bots[botIndex], configObject.ApiKeyBackPack, configObject.ApiKey, BotManager.UserHandlerCreator, manager.DB, configObject.MySteamID, true, true);
            Console.Title = "Bot Manager";
            b.StartBot();

            string AuthSet = "auth";
            string ExecCommand = "exec";

            // this loop is needed to keep the botmode console alive.
            // instead of just sleeping, this loop will handle console input
            while (true)
            {
                string inputText = Console.ReadLine();

                if (String.IsNullOrEmpty(inputText))
                    continue;

                // Small parse for console input
                var c = inputText.Trim();

                var cs = c.Split(' ');

                if (cs.Length > 1)
                {
                    if (cs[0].Equals(AuthSet, StringComparison.CurrentCultureIgnoreCase))
                    {
                        b.AuthCode = cs[1].Trim();
                    }
                    else if (cs[0].Equals(ExecCommand, StringComparison.CurrentCultureIgnoreCase))
                    {
                        b.HandleBotCommand(c.Remove(0, cs[0].Length + 1));
                    }
                }
            }
        }

        // This mode is to manage child bot processes and take use command line inputs
        private void BotManagerMode()
        {
            manager = new BotManager();

            var loadedOk = manager.LoadConfiguration("settings.json");

            if (!loadedOk)
            {
                Console.WriteLine("Configuration file Does not exist or is corrupt. Please rename 'settings-template.json' to 'settings.json' and modify the settings to match your environment");
            }
            else
            {
                if (manager.ConfigObject.UseSeparateProcesses)
                    SetConsoleCtrlHandler(ConsoleCtrlCheck, true);

                if (manager.ConfigObject.AutoStartAllBots)
                {
                    var startedOk = manager.StartBots();

                    if (!startedOk)
                    {
                        Console.WriteLine("Error starting the bots because either the configuration was bad or because the log file was not opened.");
                    }
                }
                else
                {
                    foreach (var botInfo in manager.ConfigObject.Bots)
                    {
                        if (botInfo.AutoStart)
                        {
                            // auto start this particual bot...
                            manager.StartBot(botInfo.Username);
                        }
                    }
                }

                wpfBotIndex = manager.botProcs.Count;
                if (wpfBotIndex != 0)
                    UpdateWindow(wpfBotIndex);

                Console.WriteLine("Type help for bot manager commands. ");
                Console.Write("botmgr > ");

                var bmi = new BotManagerInterpreter(manager);

                // command interpreter loop.
                do
                {
                    try
                    {
                        string inputText = Console.ReadLine();

                        if (String.IsNullOrEmpty(inputText))
                            continue;

                        bmi.CommandInterpreter(inputText);

                        Console.Write("botmgr > ");
                    }
                    catch (Exception e) { isclosing = true; }                    

                } while (!isclosing);
            }
        }

        #endregion Bot Modes

        private bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            // Put your own handler here
            switch (ctrlType)
            {
                case CtrlTypes.CTRL_C_EVENT:
                case CtrlTypes.CTRL_BREAK_EVENT:
                case CtrlTypes.CTRL_CLOSE_EVENT:
                case CtrlTypes.CTRL_LOGOFF_EVENT:
                case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                    if (manager != null)
                    {
                        manager.StopBots();
                    }
                    isclosing = true;
                    break;
            }

            return true;
        }

        #region Console Control Handler Imports

        // Declare the SetConsoleCtrlHandler function
        // as external and receiving a delegate.
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        #endregion
    }
}