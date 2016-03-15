using System;
using System.Threading.Tasks;
using System.Web;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.ComponentModel;
using SteamBot.SteamGroups;
using SteamKit2;
using SteamKit2.Internal;
using System.Globalization;
using ExempleBot;
using System.Timers;
using System.Net.Sockets;
using SteamTrade;
using SteamTrade.TradeOffer;
using SteamBot.Networking;
using System.Collections.Specialized;
using ChatterBotAPI;
using SteamBot.Networking.TCP_server;

namespace SteamBot
{
    public class Bot : IDisposable
    {
        #region Bot delegates
        public delegate UserHandler UserHandlerCreator(Bot bot, SteamID id);
        #endregion

        #region Private readonly variables
        private readonly SteamUser.LogOnDetails logOnDetails;
        private readonly string schemaLang;
        private readonly Dictionary<SteamID, UserHandler> userHandlers;
        private readonly Log.LogLevel consoleLogLevel;
        private readonly Log.LogLevel fileLogLevel;
        private readonly UserHandlerCreator createHandler;
        private readonly bool isProccess;
        private readonly BackgroundWorker botThread;
        #endregion

        #region Private variables
        private Task<Inventory> myInventoryTask;
        private TradeManager tradeManager;
        private TradeOfferManager tradeOfferManager;
        private int tradePollingInterval;
        private string myUserNonce;
        private string myUniqueId;
        private bool cookiesAreInvalid = true;
        private List<SteamID> friends;
        private bool disposed = false;
        private string consoleInput;
        #endregion

        #region Public readonly variables
        /// <summary>
        /// Userhandler class bot is running.
        /// </summary>
        public readonly string BotControlClass;
        /// <summary>
        /// The display name of bot to steam.
        /// </summary>
        public readonly string DisplayName;
        /// <summary>
        /// An array of admins for bot.
        /// </summary>
        public readonly IEnumerable<SteamID> Admins;
        public readonly SteamClient SteamClient;
        public readonly SteamUser SteamUser;
        public readonly SteamFriends SteamFriends;
        public readonly SteamTrading SteamTrade;
        public readonly SteamGameCoordinator SteamGameCoordinator;
        public readonly SteamNotifications SteamNotifications;
        /// <summary>
        /// The amount of time the bot will trade for.
        /// </summary>
        public readonly int MaximumTradeTime;
        /// <summary>
        /// The amount of time the bot will wait between user interactions with trade.
        /// </summary>
        public readonly int MaximumActionGap;
        /// <summary>
        /// The api key of bot.
        /// </summary>
        public readonly string ApiKey;
        public readonly SteamWeb SteamWeb;
        /// <summary>
        /// The prefix shown before bot's display name.
        /// </summary>
        public readonly string DisplayNamePrefix;
        /// <summary>
        /// The instance of the Logger for the bot.
        /// </summary>
        public readonly Log Log;

        public readonly List<string> MySteamID;

        public readonly string TCPPassword;

        public readonly int BotGameUsage;

        public readonly string ApiKeyBackPack;

        public readonly SteamMarketPrices sm;

        public readonly Thread StartListening;
        #endregion

        #region Public variables
        public TCPServer socket;
        public string AuthCode;
        public ChatterBotSession chatbotSession;
        public bool IsRunning;
        public List<long> listIDTradeOffer;
        public string logFile;
        public SteamAuth.SteamGuardAccount SteamGuardAccount;

        public Database DB { get; private set; }
        public bool requireSteamGuard { get; private set; }

        /// <summary>
        /// Is bot fully Logged in.
        /// Set only when bot did successfully Log in.
        /// </summary>
        public bool IsLoggedIn { get; private set; }

        /// <summary>
        /// The current trade the bot is in.
        /// </summary>
        public Trade CurrentTrade { get; private set; }

        /// <summary>
        /// The current game bot is in.
        /// Default: 0 = No game.
        /// </summary>
        public int CurrentGame { get; private set; }

        public GenericInventory mySteamInventory;

        public GenericInventory otherSteamInventory;
        #endregion

        public IEnumerable<SteamID> FriendsList
        {
            get
            {
                CreateFriendsListIfNecessary();
                return friends;
            }
        }

        public Inventory MyInventory
        {
            get
            {
                myInventoryTask.Wait();
                return myInventoryTask.Result;
            }
        }

        /// <summary>
        /// Compatibility sanity.
        /// </summary>
        [Obsolete("Refactored to be Log instead of log")]
        public Log log { get { return Log; } }

        public Bot(Configuration.BotInfo config, string apiKeyBackPack, string apiKey, UserHandlerCreator handlerCreator, Database database, string adminSteamID, bool debug = false, bool process = false)
        {
            socket = new TCPServer(this);
            ChatterBotFactory factory = new ChatterBotFactory();
            ChatterBot chatbot = factory.Create(ChatterBotType.PANDORABOTS, "b0dafd24ee35a477");
            chatbotSession = chatbot.CreateSession();
            userHandlers = new Dictionary<SteamID, UserHandler>();
            logOnDetails = new SteamUser.LogOnDetails
            {
                Username = config.Username,
                Password = config.Password
            };
            DisplayName  = config.DisplayName;
            MaximumTradeTime = config.MaximumTradeTime;
            MaximumActionGap = config.MaximumActionGap;
            DisplayNamePrefix = config.DisplayNamePrefix;
            tradePollingInterval = config.TradePollingInterval <= 100 ? 800 : config.TradePollingInterval;
            schemaLang = config.SchemaLang != null && config.SchemaLang.Length == 2 ? config.SchemaLang.ToLower() : "en";
            Admins = config.Admins;
            TCPPassword = config.TCPPassword;
            BotGameUsage = config.BotGameUsage;
            DB = database;

            MySteamID = new List<string>();
            foreach (string str in adminSteamID.Split(','))
                MySteamID.Add(str);

            ApiKey = !String.IsNullOrEmpty(config.ApiKey) ? config.ApiKey : apiKey;
            ApiKeyBackPack = apiKeyBackPack;
            isProccess = process;
            try
            {
                if( config.LogLevel != null )
                {
                    consoleLogLevel = (Log.LogLevel)Enum.Parse(typeof(Log.LogLevel), config.LogLevel, true);
                    Console.WriteLine(@"(Console) LogLevel configuration parameter used in bot {0} is depreciated and may be removed in future versions. Please use ConsoleLogLevel instead.", DisplayName);
                }
                else consoleLogLevel = (Log.LogLevel)Enum.Parse(typeof(Log.LogLevel), config.ConsoleLogLevel, true);
            }
            catch (ArgumentException)
            {
                Console.WriteLine(@"(Console) ConsoleLogLevel invalid or unspecified for bot {0}. Defaulting to ""Info""", DisplayName);
                consoleLogLevel = Log.LogLevel.Info;
            }

            try
            {
                fileLogLevel = (Log.LogLevel)Enum.Parse(typeof(Log.LogLevel), config.FileLogLevel, true);
            }
            catch (ArgumentException)
            {
                Console.WriteLine(@"(Console) FileLogLevel invalid or unspecified for bot {0}. Defaulting to ""Info""", DisplayName);
                fileLogLevel = Log.LogLevel.Info;
            }

            logFile = config.LogFile;
            Log = new Log(logFile, DisplayName, consoleLogLevel, fileLogLevel);
            createHandler = handlerCreator;
            BotControlClass = config.BotControlClass;
            SteamWeb = new SteamWeb();

            // Hacking around https
            ServicePointManager.ServerCertificateValidationCallback += SteamWeb.ValidateRemoteCertificate;

            Log.Debug ("Initializing Steam Bot...");
            SteamClient = new SteamClient();
            SteamClient.AddHandler(new SteamNotifications());
            SteamTrade = SteamClient.GetHandler<SteamTrading>();
            SteamUser = SteamClient.GetHandler<SteamUser>();
            SteamFriends = SteamClient.GetHandler<SteamFriends>();
            SteamGameCoordinator = SteamClient.GetHandler<SteamGameCoordinator>();
            SteamNotifications = SteamClient.GetHandler<SteamNotifications>();

            botThread = new BackgroundWorker { WorkerSupportsCancellation = true };
            botThread.DoWork += BackgroundWorkerOnDoWork;
            botThread.RunWorkerCompleted += BackgroundWorkerOnRunWorkerCompleted;
            botThread.RunWorkerAsync();

            System.Timers.Timer timerTradeOfferCheck = new System.Timers.Timer();
            timerTradeOfferCheck.Elapsed += new ElapsedEventHandler(DoTradeOfferChecks);
            timerTradeOfferCheck.Interval = 1000;
            timerTradeOfferCheck.Enabled = true;
            timerTradeOfferCheck.AutoReset = true;

            listIDTradeOffer = new List<long>();

            ThreadStart threadDelegate = new ThreadStart(socket.StartServer);
            StartListening = new Thread(threadDelegate);
            StartListening.Start();

            string[] rows = new string[1];
            rows[0] = "TradeOfferID";
            List<Dictionary<string, string>> tradeoffers = database.SELECT(rows, "tradeoffers");
            if (tradeoffers != null)
            {
                foreach (Dictionary<string, string> tradeoffer in tradeoffers)
                {
                    listIDTradeOffer.Add(Convert.ToInt64(tradeoffer["TradeOfferID"]));
                    Log.Warn("Found a new trade offer ID in the database ! (" + tradeoffer["TradeOfferID"] + ")");
                }
            }

            sm = new SteamMarketPrices();
            sm.ScanMarket(ApiKeyBackPack, BotGameUsage);
            if(sm.data.response.success == 0)
            {
                string[] row = new string[4];
                row[0] = "itemName";
                row[1] = "last_updated";
                row[2] = "value";
                row[3] = "quantity";
                List<Dictionary<string, string>> items = database.SELECT(row, "smitems");
                if (items != null)
                {
                    sm.data.response.items = new List<SteamMarketPrices.Item>();
                    foreach (Dictionary<string, string> item in items)
                    {
                        sm.data.response.items.Add(new SteamMarketPrices.Item(item["itemName"], Int32.Parse(item["last_updated"]), Int32.Parse(item["quantity"]), Double.Parse(item["value"])));
                    }
                }
                Log.Warn("Found " + items.Count + " backup items in database !");
            }

            System.Timers.Timer refreshMarket = new System.Timers.Timer(400000);
            refreshMarket.Elapsed += UpdateMarketItems;
            refreshMarket.AutoReset = true;
            refreshMarket.Enabled = true;
        }

        private void UpdateMarketItems(object sender, System.Timers.ElapsedEventArgs e)
        {
            sm.ScanMarket(ApiKeyBackPack, BotGameUsage);
        }
        
        private void SaveMarketInDB(List<SteamMarketPrices.Item> list)
        {
            if (list == null || list.Count == 0)
                return;

            Log.Warn("Saving steam market items into database...");
            Log.Error("This may take severals minutes !");
            DB.QUERY("TRUNCATE smitems");
            int itemCount = 0;
            foreach (SteamMarketPrices.Item item in list)
            {
                string[] rows = new string[4];
                rows[0] = "itemName";
                rows[1] = "last_updated";
                rows[2] = "value";
                rows[3] = "quantity";

                string[] values = new string[4];
                values[0] = item.name;
                values[1] = item.last_updated.ToString();
                values[2] = item.value.ToString();
                values[3] = item.quantity.ToString();

                DB.INSERT("smitems", rows, values);
                itemCount++;
                Log.Success(values[0] + " saved !");
                Log.Warn(itemCount + "/" + list.Count);
            }
            Log.Success("Done !");
        }

        public void OnTCPMessageReceived(TcpClient sender, String msg)
        {
            //THIS SUCKS !
            //Need to create custom TCP paquets and sh*t but I don't feel enough motived enough.

            if (!msg.Contains(TCPPassword))
                return;

            msg = msg.Replace(TCPPassword, "");

            if (msg.StartsWith(Networking.Constants.REQUEST_CONNECTION))
            {
                socket.Send(sender, TCPPassword+"I added you sucessfully to my clients list !");
                Client client = socket.Clients.Find(x => x.clientSocket == sender);
                if (client != null)
                    client.Name = msg.Replace(Networking.Constants.REQUEST_CONNECTION, "");
            }
            else if (msg.StartsWith(Networking.Constants.TRADEOFFER_CREATE_TF2))
            {
                #region TF2 Create Trade Offer
                msg = msg.Replace(Networking.Constants.TRADEOFFER_CREATE_TF2, "");

                string[] steamidItemGetItemGive = msg.Split('|');
                ulong OtherSID = new SteamID(steamidItemGetItemGive[0]).ConvertToUInt64();

                Dictionary<int, int> requiredItem = new Dictionary<int, int>();
                Dictionary<int, int> itemToGive = new Dictionary<int, int>();

                Dictionary<int, int> backUp = null;
                Dictionary<int, int> backUp2 = null;

                List<Inventory.Item> usedItems = new List<Inventory.Item>();

                TradeOffer offer = NewTradeOffer(OtherSID);

                if (steamidItemGetItemGive.Length >= 1)
                {
                    string[] ItemGet = steamidItemGetItemGive[1].Split(',');
                    for (int i = 0; i < ItemGet.Length; i++)
                    {
                        string[] dicEntry = ItemGet[i].Split('x');
                        requiredItem.Add(Int32.Parse(dicEntry[1]), Int32.Parse(dicEntry[0]));
                    }

                    backUp = new Dictionary<int, int>(requiredItem);

                    Inventory invetory = Inventory.FetchInventory(OtherSID, ApiKey, SteamWeb);

                    foreach (Inventory.Item item in invetory.Items)
                    {
                        if (requiredItem.ContainsKey(item.Defindex) && !usedItems.Contains(item))
                        {
                            requiredItem[item.Defindex] = requiredItem[item.Defindex] - 1;
                            offer.Items.AddTheirItem(item.AppId, item.ContextId, (long)item.Id);
                            if (requiredItem[item.Defindex] == 0)
                                requiredItem.Remove(item.Defindex);

                            usedItems.Add(item);
                        }
                    }
                }

                usedItems.Clear();

                if (steamidItemGetItemGive.Length >= 2 && steamidItemGetItemGive[2] != "")
                {
                    string[] ItemGive = steamidItemGetItemGive[2].Split(',');
                    for (int i = 0; i < ItemGive.Length; i++)
                    {
                        string[] dicEntry = ItemGive[i].Split('x');
                        itemToGive.Add(Int32.Parse(dicEntry[1]), Int32.Parse(dicEntry[0]));
                    }

                    backUp2 = new Dictionary<int, int>(itemToGive);

                    GetInventory();
                    foreach (Inventory.Item item in MyInventory.Items)
                    {
                        if (itemToGive.ContainsKey(item.Defindex) && !usedItems.Contains(item))
                        {
                            itemToGive[item.Defindex] = itemToGive[item.Defindex] - 1;
                            offer.Items.AddMyItem(item.AppId, item.ContextId, (long)item.Id);
                            if (itemToGive[item.Defindex] == 0)
                                itemToGive.Remove(item.Defindex);

                            usedItems.Add(item);
                        }
                    }
                }

                if (requiredItem.Count != 0)
                {
                    Log.Warn(OtherSID + " don't have enough items for the trade request !");
                }
                else if (itemToGive.Count != 0)
                {
                    int i = 0;
                }
                else if (offer.Items.NewVersion)
                {
                    string newOfferId;
                    if (offer.Send(out newOfferId))
                    {
                        Log.Success("Trade offer sent : Offer ID " + newOfferId);
                        Console.WriteLine("Trade offer sent : Offer ID " + newOfferId);

                        listIDTradeOffer.Add(Int32.Parse(newOfferId));

                        string[] rowsTO = new string[2];
                        rowsTO[0] = "SteamID";
                        rowsTO[1] = "TradeOfferID";

                        string[] valuesTO = new string[2];
                        valuesTO[0] = OtherSID.ToString();
                        valuesTO[1] = newOfferId.ToString();

                        DB.INSERT("tradeoffers", rowsTO, valuesTO);

                        if (backUp != null)
                        {
                            foreach (KeyValuePair<int, int> item in backUp)
                            {
                                for (int i = 0; i < item.Value; i++)
                                {
                                    string[] rowsI = new string[3];
                                    rowsI[0] = "itemName";
                                    rowsI[1] = "tradeoffersid";
                                    rowsI[2] = "togive";

                                    string[] valuesI = new string[3];
                                    valuesI[0] = item.Key.ToString();
                                    valuesI[1] = newOfferId.ToString();
                                    valuesI[2] = "0";

                                    DB.INSERT("items", rowsI, valuesI);
                                }
                            }
                        }

                        if (backUp2 != null)
                        {
                            foreach (KeyValuePair<int, int> item in backUp2)
                            {
                                for (int i = 0; i < item.Value; i++)
                                {
                                    string[] rowsI = new string[3];
                                    rowsI[0] = "itemName";
                                    rowsI[1] = "tradeoffersid";
                                    rowsI[2] = "togive";

                                    string[] valuesI = new string[3];
                                    valuesI[0] = item.Key.ToString();
                                    valuesI[1] = newOfferId.ToString();
                                    valuesI[2] = "1";

                                    DB.INSERT("items", rowsI, valuesI);
                                }
                            }
                        }
                    }
                }
                #endregion
            }
            else if (msg.StartsWith(Networking.Constants.TRADEOFFER_CREATE))
            {
                #region Create Trade Offer
                msg = msg.Replace(Networking.Constants.TRADEOFFER_CREATE, "");

                string[] steamidItemGetItemGive = msg.Split('/');
                ulong OtherSID = new SteamID(steamidItemGetItemGive[0]).ConvertToUInt64();

                Dictionary<string, int> requiredItem = new Dictionary<string, int>();
                Dictionary<string, int> itemToGive = new Dictionary<string, int>();

                Dictionary<string, int> backUp = null;
                Dictionary<string, int> backUp2 = null;

                List<GenericInventory.Item> usedItems = new List<GenericInventory.Item>();

                TradeOffer offer = NewTradeOffer(OtherSID);

                if (steamidItemGetItemGive.Length >= 1)
                {
                    string[] ItemGet = steamidItemGetItemGive[1].Split(',');
                    for (int i = 0; i < ItemGet.Length - 1; i++)
                    {
                        string[] dicEntry = ItemGet[i].Split('x');
                        requiredItem.Add(dicEntry[1], Int32.Parse(dicEntry[0]));
                    }

                    backUp = new Dictionary<string, int>(requiredItem);

                    GenericInventory otherInventory = new GenericInventory(SteamWeb);

                    List<long> contextId = new List<long>();
                    contextId.Add(2);
                    otherInventory.load(BotGameUsage, contextId, OtherSID);

                    foreach (KeyValuePair<ulong, GenericInventory.Item> item in otherInventory.items)
                    {
                        SteamTrade.GenericInventory.ItemDescription description = otherInventory.getDescription(item.Key);
                        //SendChatMessage(description.name + " in game " + otherInventory.items[item.Key].appid);

                        if (requiredItem.ContainsKey(description.name) && !usedItems.Contains(item.Value))
                        {
                            requiredItem[description.name] = requiredItem[description.name] - 1;
                            offer.Items.AddTheirItem(item.Value.appid, item.Value.contextid, (long)item.Value.assetid);
                            if (requiredItem[description.name] == 0)
                                requiredItem.Remove(description.name);

                            usedItems.Add(item.Value);
                        }
                    }
                }

                usedItems.Clear();

                if (steamidItemGetItemGive.Length >= 2 && steamidItemGetItemGive[2] != "")
                {
                    string[] ItemGive = steamidItemGetItemGive[2].Split(',');
                    for (int i = 0; i < ItemGive.Length - 1; i++)
                    {
                        string[] dicEntry = ItemGive[i].Split('x');
                        itemToGive.Add(dicEntry[1], Int32.Parse(dicEntry[0]));
                    }

                    backUp2 = new Dictionary<string, int>(itemToGive);

                    GenericInventory mySteamInventory = new GenericInventory(SteamWeb);

                    List<long> contextId = new List<long>();
                    contextId.Add(2);
                    mySteamInventory.load(BotGameUsage, contextId, SteamClient.SteamID);

                    foreach (KeyValuePair<ulong, GenericInventory.Item> item in mySteamInventory.items)
                    {
                        SteamTrade.GenericInventory.ItemDescription description = mySteamInventory.getDescription(item.Key);
                        //SendChatMessage(description.name + " in game " + otherInventory.items[item.Key].appid);

                        if (itemToGive.ContainsKey(description.name) && !usedItems.Contains(item.Value))
                        {
                            itemToGive[description.name] = itemToGive[description.name] - 1;
                            offer.Items.AddMyItem(item.Value.appid, item.Value.contextid, (long)item.Value.assetid);
                            if (itemToGive[description.name] == 0)
                                itemToGive.Remove(description.name);

                            usedItems.Add(item.Value);
                        }
                    }
                }

                if (requiredItem.Count != 0)
                {
                    Log.Warn(OtherSID + " don't have enough items for the trade request !");
                }
                else if (itemToGive.Count != 0)
                {
                    Log.Warn("Bot don't have enough items for the trade request !");
                }
                else if (offer.Items.NewVersion)
                {
                    string newOfferId;
                    if (offer.Send(out newOfferId))
                    {
                        Log.Success("Trade offer sent : Offer ID " + newOfferId);
                        Console.WriteLine("Trade offer sent : Offer ID " + newOfferId);

                        listIDTradeOffer.Add(Int32.Parse(newOfferId));

                        string[] rowsTO = new string[2];
                        rowsTO[0] = "SteamID";
                        rowsTO[1] = "TradeOfferID";

                        string[] valuesTO = new string[2];
                        valuesTO[0] = OtherSID.ToString();
                        valuesTO[1] = newOfferId.ToString();

                        DB.INSERT("tradeoffers", rowsTO, valuesTO);

                        if (backUp != null)
                        {
                            foreach (KeyValuePair<string, int> item in backUp)
                            {
                                for (int i = 0; i < item.Value; i++)
                                {
                                    string[] rowsI = new string[3];
                                    rowsI[0] = "itemName";
                                    rowsI[1] = "tradeoffersid";
                                    rowsI[2] = "togive";

                                    string[] valuesI = new string[3];
                                    valuesI[0] = item.Key.ToString();
                                    valuesI[1] = newOfferId.ToString();
                                    valuesI[2] = "0";

                                    DB.INSERT("items", rowsI, valuesI);
                                }
                            }
                        }

                        if (backUp2 != null)
                        {
                            foreach (KeyValuePair<string, int> item in backUp2)
                            {
                                for (int i = 0; i < item.Value; i++)
                                {
                                    string[] rowsI = new string[3];
                                    rowsI[0] = "itemName";
                                    rowsI[1] = "tradeoffersid";
                                    rowsI[2] = "togive";

                                    string[] valuesI = new string[3];
                                    valuesI[0] = item.Key.ToString();
                                    valuesI[1] = newOfferId.ToString();
                                    valuesI[2] = "1";

                                    DB.INSERT("items", rowsI, valuesI);
                                }
                            }
                        }
                    }
                }
                #endregion
            }
            else if (msg.StartsWith(Networking.Constants.STEAMGROUP_POST_ANOUNCEMENT))
            {
                #region Steamgroup annoncement
                msg = msg.Replace(Networking.Constants.STEAMGROUP_POST_ANOUNCEMENT, "");

                string[] groupIDHeadlineBody = msg.Split('/');

                var data = new NameValueCollection();
                data.Add("sessionID", SteamWeb.SessionId);
                data.Add("action", "post");
                data.Add("headline", groupIDHeadlineBody[1]);
                data.Add("body", groupIDHeadlineBody[2]);

                string link = "https://steamcommunity.com/gid/" + groupIDHeadlineBody[0] + "/announcements";

                SteamWeb.Fetch(link, "POST", data, false);
                #endregion
            }
            else if (msg.StartsWith(Networking.Constants.SPEAK_WITH_ME))
            {
                #region speak module
                msg = msg.Replace(Networking.Constants.SPEAK_WITH_ME, "");
                string[] clientMsg = msg.Split('|');
                string botAnswer = chatbotSession.Think(clientMsg[1]);
                socket.Send(sender, clientMsg[0]+"|"+botAnswer);
                #endregion
            }
            else if (msg.StartsWith(Networking.Constants.INVENTORY_SCAN))
            {
                #region inventory scan
                msg = msg.Replace(Networking.Constants.INVENTORY_SCAN + "|", "");
                List<long> contextId = new List<long>();
                contextId.Add(2);
                otherSteamInventory.load(BotGameUsage, contextId, new SteamID(msg));

                if (otherSteamInventory.items.Count == 0)
                {
                    switch (BotGameUsage)
                    {
                        case 440: socket.Send(sender, "No item found for TF2. Your inventory is set to private ?"); break;
                        case 730: Console.WriteLine("No item found for CS:GO. Your inventory is set to private ?"); break;
                        default: Console.WriteLine("No item found for game id " + BotGameUsage + ". Your inventory is set to private ?"); break;
                    }
                }
                else
                {
                    socket.Send(sender, "SCAN_BEGIN");
                    socket.Send(sender, ""+otherSteamInventory.items.Count);
                    foreach (KeyValuePair<ulong, GenericInventory.Item> item in otherSteamInventory.items)
                    {
                        SteamTrade.GenericInventory.ItemDescription description = otherSteamInventory.getDescription(item.Key);
                        if(description.tradable)
                            socket.Send(sender, description.name);
                    }
                    socket.Send(sender, "SCAN_END");
                }
                #endregion
            }
            else if (msg.StartsWith(Networking.Constants.SEND_MESSAGE))
            {
                #region sendMsg
                string[] msgTypeTargetMsg = msg.Split('|');
                SteamID steamID = new SteamID(msgTypeTargetMsg[1].Replace(" ", ""));
                SteamFriends.SendChatMessage(steamID, EChatEntryType.ChatMsg, msgTypeTargetMsg[2]);
                #endregion
            }
        }

        private void DoTradeOfferChecks(object sender, ElapsedEventArgs e)
        {
            if (DB != null && DB.isConnected)
                ReadTradeOfferStatus();
            else
                Log.Error("Database not connected ! Can't do trade offer checks !");
        }

        public void ReadTradeOfferStatus()
        {
            for (int i = 0; i < listIDTradeOffer.Count; i++)
            {
                if (tradeManager == null)
                    return;

                string id = listIDTradeOffer[i].ToString();
                TradeOffer offer = null;
                bool success = TryGetTradeOffer(id, out offer);
                if (success)
                {
                    if (listIDTradeOffer.Count == 0)
                        return;

                    if (offer.OfferState == TradeOfferState.TradeOfferStateAccepted)
                    {
                        Log.Success("Trade Offer of " + offer.PartnerSteamId + " accepted !");

                        /*Inventory invetory = Inventory.FetchInventory(offer.PartnerSteamId, ApiKey, SteamWeb);
                        SteamTrade.TradeOffer.TradeOffer.TradeStatusUser myItems = offer.Items.TheirMissingItems;
                        SteamTrade.TradeOffer.TradeOffer.TradeStatusUser theirItems = offer.Items.MyMissingItems;
                        var item = invetory.GetItem((ulong)myItems.Assets[0].AssetId);*/

                        string msg = TCPPassword + Networking.Constants.TRADEOFFER_ENDED;

                        msg += "|"+offer.PartnerSteamId+"|";

                        string[] rows = new string[1];

                        rows[0] = "itemName";

                        //My items to receive
                        List<Dictionary<string, string>> items = DB.SELECT(rows, "items", "WHERE tradeoffersid='" + offer.TradeOfferId + "' AND togive='0'");
                        if (items != null)
                        {
                            foreach (Dictionary<string, string> tradeoffer in items)
                            {
                                msg += tradeoffer["itemName"]+",";
                            }
                        }

                        msg += "|";

                        //His items to get
                        items = DB.SELECT(rows, "items", "WHERE tradeoffersid='" + offer.TradeOfferId + "'" + " AND togive='1'");
                        if (items != null)
                        {
                            foreach (Dictionary<string, string> tradeoffer in items)
                            {
                                msg += tradeoffer["itemName"]+",";
                            }
                        }

                        msg += "|";

                        rows[0] = "tradeValue";

                        //My items to receive
                        List<Dictionary<string, string>> values = DB.SELECT(rows, "tradeoffers", "WHERE TradeOfferID='" + offer.TradeOfferId + "'");
                        if (values != null)
                        {
                            foreach (Dictionary<string, string> value in values)
                            {
                                msg += value["tradeValue"] + "|";
                            }
                        }

                        Log.Warn("Sending to TCP clients : " + msg);
                        socket.SendToAll(msg);

                        /*Log.Warn("Removing trade offer id from database");
                        DB.DELETE("TradeOfferID", id, "tradeoffers");*/
                        listIDTradeOffer.Remove(Int32.Parse(id));
                    }
                    else
                    {
                        if (offer.OfferState != TradeOfferState.TradeOfferStateNeedsConfirmation && offer.OfferState != TradeOfferState.TradeOfferStateActive)
                        {
                            if (offer.OfferState == TradeOfferState.TradeOfferStateCanceled)
                                Log.Error("Trade offer " + id + " has been cancelled ! Removing...");
                            else if (offer.OfferState == TradeOfferState.TradeOfferStateCountered)
                                Log.Error("Trade offer " + id + " has been countered ! Removing...");
                            else if (offer.OfferState == TradeOfferState.TradeOfferStateDeclined)
                                Log.Error("Trade offer " + id + " has been declined ! Removing...");
                            else if (offer.OfferState == TradeOfferState.TradeOfferStateExpired)
                                Log.Error("Trade offer " + id + " expired ! Removing...");
                            else if (offer.OfferState == TradeOfferState.TradeOfferStateInvalid)
                                Log.Error("Trade offer " + id + " is invalid ! Removing...");
                            else if (offer.OfferState == TradeOfferState.TradeOfferStateInvalidItems)
                                Log.Error("Removing trade offer id from database because of invalid items !");

                            DB.DELETE("TradeOfferID", id.ToString(), "tradeoffers");
                            listIDTradeOffer.Remove(Int32.Parse(id));
                        }
                    }
                }
            }
        }

        ~Bot()
        {
            Dispose(false);
        }

        private void CreateFriendsListIfNecessary()
        {
            if (friends != null)
                return;

            friends = new List<SteamID>();
            for (int i = 0; i < SteamFriends.GetFriendCount(); i++)
                friends.Add(SteamFriends.GetFriendByIndex(i));
        }

        /// <summary>
        /// Occurs when the bot needs the SteamGuard authentication code.
        /// </summary>
        /// <remarks>
        /// Return the code in <see cref="SteamGuardRequiredEventArgs.SteamGuard"/>
        /// </remarks>
        public event EventHandler<SteamGuardRequiredEventArgs> OnSteamGuardRequired;

        /// <summary>
        /// Starts the callback thread and connects to Steam via SteamKit2.
        /// </summary>
        /// <remarks>
        /// THIS NEVER RETURNS.
        /// </remarks>
        /// <returns><c>true</c>. See remarks</returns>
        public bool StartBot()
        {
            IsRunning = true;
            Log.Info("Connecting...");
            if (!botThread.IsBusy)
                botThread.RunWorkerAsync();
            SteamClient.Connect();
            Log.Success("Done Loading Bot!");
            return true; // never get here
        }

        /// <summary>
        /// Disconnect from the Steam network and stop the callback
        /// thread.
        /// </summary>
        public void StopBot()
        {
            IsRunning = false;
            Log.Debug("Trying to shut down bot thread.");
            SteamClient.Disconnect();
            botThread.CancelAsync();
            while (botThread.IsBusy)
                Thread.Yield();
            userHandlers.Clear();
            //SaveMarketInDB(sm.data.response.items);
        }

        /// <summary>
        /// Creates a new trade with the given partner.
        /// </summary>
        /// <returns>
        /// <c>true</c>, if trade was opened,
        /// <c>false</c> if there is another trade that must be closed first.
        /// </returns>
        public bool OpenTrade (SteamID other)
        {
            if (CurrentTrade != null || CheckCookies() == false)
                return false;
            SteamTrade.Trade(other);
            return true;
        }

        /// <summary>
        /// Closes the current active trade.
        /// </summary>
        public void CloseTrade() 
        {
            if (CurrentTrade == null)
                return;
            UnsubscribeTrade (GetUserHandler (CurrentTrade.OtherSID), CurrentTrade);
            tradeManager.StopTrade ();
            CurrentTrade = null;
        }

        void OnTradeTimeout(object sender, EventArgs args) 
        {
            // ignore event params and just null out the trade.
            GetUserHandler(CurrentTrade.OtherSID).OnTradeTimeout();
        }

        /// <summary>
        /// Create a new trade offer with the specified partner
        /// </summary>
        /// <param name="other">SteamId of the partner</param>
        /// <returns></returns>
        public TradeOffer NewTradeOffer(SteamID other)
        {
            return tradeOfferManager.NewOffer(other);
        }

        /// <summary>
        /// Try to get a specific trade offer using the offerid
        /// </summary>
        /// <param name="offerId"></param>
        /// <param name="tradeOffer"></param>
        /// <returns></returns>
        public bool TryGetTradeOffer(string offerId, out TradeOffer tradeOffer)
        {
            return tradeOfferManager.GetOffer(offerId, out tradeOffer);
        }

        public void HandleInput(string input)
        {
            consoleInput = input;
        }

        public void HandleBotCommand(string command)
        {
            try
            {
                if (command == "linkauth")
                {
                    LinkMobileAuth();
                }
                else if (command == "getauth")
                {
                    try
                    {
                        Log.Success("Generated Steam Guard code: " + SteamGuardAccount.GenerateSteamGuardCode());
                    }
                    catch (NullReferenceException)
                    {
                        Log.Error("Unable to generate Steam Guard code.");
                    }
                }
                else if (command == "unlinkauth")
                {
                    if (SteamGuardAccount == null)
                    {
                        Log.Error("Mobile authenticator is not active on this bot.");
                    }
                    else if (SteamGuardAccount.DeactivateAuthenticator())
                    {
                        Log.Success("Deactivated authenticator on this account.");
                    }
                    else
                    {
                        Log.Error("Failed to deactivate authenticator on this account.");
                    }
                }
                else
                {
                    GetUserHandler(SteamClient.SteamID).OnBotCommand(command);
                }
            }
            catch (ObjectDisposedException e)
            {
                // Writing to console because odds are the error was caused by a disposed Log.
                Console.WriteLine(string.Format("Exception caught in BotCommand Thread: {0}", e));
                if (!this.IsRunning)
                {
                    Console.WriteLine("The Bot is no longer running and could not write to the Log. Try Starting this bot first.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Exception caught in BotCommand Thread: {0}", e));
            }
        }

        bool HandleTradeSessionStart (SteamID other)
        {
            if (CurrentTrade != null)
                return false;
            try
            {
                tradeManager.InitializeTrade(SteamUser.SteamID, other);
                CurrentTrade = tradeManager.CreateTrade(SteamUser.SteamID, other);
                CurrentTrade.OnClose += CloseTrade;
                SubscribeTrade(CurrentTrade, GetUserHandler(other));
                tradeManager.StartTradeThread(CurrentTrade);
                return true;
            }
            catch (SteamTrade.Exceptions.InventoryFetchException)
            {
                // we shouldn't get here because the inv checks are also
                // done in the TradeProposedCallback handler.
                /*string response = String.Empty;
                if (ie.FailingSteamId.ConvertToUInt64() == other.ConvertToUInt64())
                {
                    response = "Trade failed. Could not correctly fetch your backpack. Either the inventory is inaccessible or your backpack is private.";
                }
                else 
                {
                    response = "Trade failed. Could not correctly fetch my backpack.";
                }
                
                SteamFriends.SendChatMessage(other, 
                                             EChatEntryType.ChatMsg,
                                             response);

                Log.Info ("Bot sent other: {0}", response);
                
                CurrentTrade = null;*/
                return false;
            }
        }

        public void SetGamePlaying(int id)
        {
            var gamePlaying = new SteamKit2.ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
            if (id != 0)
                gamePlaying.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
                {
                    game_id = new GameID(id),
                });
            SteamClient.Send(gamePlaying);
            CurrentGame = id;
        }

        public bool SendCommandToServer(int serverID, string cmd)
        {
            if (serverID < socket.Clients.Count-1 || serverID > socket.Clients.Count-1)
                return false;

            socket.Send(socket.Clients[serverID].clientSocket, "EXEC|" + cmd);
            return true;
        }

        void HandleSteamMessage(ICallbackMsg msg)
        {
            Log.Debug(msg.ToString());
            #region Login
            msg.Handle<SteamClient.ConnectedCallback>(callback =>
            {
                Log.Debug("Connection Callback: {0}", callback.Result);

                if (callback.Result == EResult.OK)
                {
                    UserLogOn();
                }
                else
                {
                    Log.Error("Failed to connect to Steam Community, trying again...");
                    SteamClient.Connect();
                }

            });

            msg.Handle<SteamUser.LoggedOnCallback>(callback =>
            {
                Log.Debug("Logged On Callback: {0}", callback.Result);

                if (callback.Result == EResult.OK)
                {
                    myUserNonce = callback.WebAPIUserNonce;
                }
                else
                {
                    Log.Error("Login Error: {0}", callback.Result);
                }

                if (callback.Result == EResult.AccountLogonDeniedNeedTwoFactorCode)
                {
                    var mobileAuthCode = GetMobileAuthCode();
                    if (string.IsNullOrEmpty(mobileAuthCode))
                    {
                        Log.Error("Failed to generate 2FA code. Make sure you have linked the authenticator via SteamBot.");
                    }
                    else
                    {
                        logOnDetails.TwoFactorCode = mobileAuthCode;
                        Log.Success("Generated 2FA code.");
                    }
                }
                else if (callback.Result == EResult.TwoFactorCodeMismatch)
                {
                    SteamAuth.TimeAligner.AlignTime();
                    logOnDetails.TwoFactorCode = SteamGuardAccount.GenerateSteamGuardCode();
                    Log.Success("Regenerated 2FA code.");
                }
                else if (callback.Result == EResult.AccountLogonDenied)
                {
                    Log.Interface("This account is SteamGuard enabled. Enter the code via the `auth' command.");

                    // try to get the steamguard auth code from the event callback
                    var eva = new SteamGuardRequiredEventArgs();
                    FireOnSteamGuardRequired(eva);
                    if (!String.IsNullOrEmpty(eva.SteamGuard))
                        logOnDetails.AuthCode = eva.SteamGuard;
                    else
                        logOnDetails.AuthCode = Console.ReadLine();
                }
                else if (callback.Result == EResult.InvalidLoginAuthCode)
                {
                    Log.Interface("The given SteamGuard code was invalid. Try again using the `auth' command.");
                    logOnDetails.AuthCode = Console.ReadLine();
                }
            });

            msg.Handle<SteamUser.LoginKeyCallback>(callback =>
            {
                myUniqueId = callback.UniqueID.ToString();

                UserWebLogOn();

                if (Trade.CurrentSchema == null)
                {
                    Log.Info("Downloading Schema...");
                    Trade.CurrentSchema = Schema.FetchSchema(ApiKey, schemaLang);
                    Log.Success("Schema Downloaded!");
                }

                SteamFriends.SetPersonaName(DisplayNamePrefix + DisplayName);
                SteamFriends.SetPersonaState(EPersonaState.Online);

                Log.Success("Steam Bot Logged In Completely!");

                GetUserHandler(SteamClient.SteamID).OnLoginCompleted();
            });

            msg.Handle<SteamUser.WebAPIUserNonceCallback>(webCallback =>
            {
                Log.Debug("Received new WebAPIUserNonce.");

                if (webCallback.Result == EResult.OK)
                {
                    myUserNonce = webCallback.Nonce;
                    UserWebLogOn();
                }
                else
                {
                    Log.Error("WebAPIUserNonce Error: " + webCallback.Result);
                }
            });

            msg.Handle<SteamUser.UpdateMachineAuthCallback>(
                authCallback => OnUpdateMachineAuthCallback(authCallback)
            );
            #endregion

            #region Friends
            msg.Handle<SteamFriends.FriendsListCallback>(callback =>
            {
                foreach (SteamFriends.FriendsListCallback.Friend friend in callback.FriendList)
                {
                    switch (friend.SteamID.AccountType)
                    {
                        case EAccountType.Clan:
                            if (friend.Relationship == EFriendRelationship.RequestRecipient)
                            {
                                if (GetUserHandler(friend.SteamID).OnGroupAdd())
                                {
                                    AcceptGroupInvite(friend.SteamID);
                                }
                                else
                                {
                                    DeclineGroupInvite(friend.SteamID);
                                }
                            }
                            break;
                        default:
                            CreateFriendsListIfNecessary();
                            if (friend.Relationship == EFriendRelationship.None)
                            {
                                friends.Remove(friend.SteamID);
                                GetUserHandler(friend.SteamID).OnFriendRemove();
                                RemoveUserHandler(friend.SteamID);
                            }
                            else if (friend.Relationship == EFriendRelationship.RequestRecipient)
                            {
                                if (GetUserHandler(friend.SteamID).OnFriendAdd())
                                {
                                    if (!friends.Contains(friend.SteamID))
                                    {
                                        friends.Add(friend.SteamID);
                                    }
                                    else
                                    {
                                        Log.Error("Friend was added who was already in friends list: " + friend.SteamID);
                                    }
                                    SteamFriends.AddFriend(friend.SteamID);
                                }
                                else
                                {
                                    SteamFriends.RemoveFriend(friend.SteamID);
                                    RemoveUserHandler(friend.SteamID);
                                }
                            }
                            break;
                    }
                }
            });


            msg.Handle<SteamFriends.FriendMsgCallback>(callback =>
            {
                EChatEntryType type = callback.EntryType;

                if (callback.EntryType == EChatEntryType.ChatMsg)
                {
                    Log.Info("Chat Message from {0}: {1}",
                                         SteamFriends.GetFriendPersonaName(callback.Sender),
                                         callback.Message
                                         );
                    GetUserHandler(callback.Sender).OnMessageHandler(callback.Message, type);
                }
            });
            #endregion

            #region Group Chat
            msg.Handle<SteamFriends.ChatMsgCallback>(callback =>
            {
                GetUserHandler(callback.ChatterID).OnChatRoomMessage(callback.ChatRoomID, callback.ChatterID, callback.Message);
            });
            #endregion

            #region Trading
            msg.Handle<SteamTrading.SessionStartCallback>(callback =>
            {
                bool started = HandleTradeSessionStart(callback.OtherClient);

                if (!started)
                    Log.Error("Could not start the trade session.");
                else
                    Log.Debug("SteamTrading.SessionStartCallback handled successfully. Trade Opened.");
            });

            msg.Handle<SteamTrading.TradeProposedCallback>(callback =>
            {
                if (CheckCookies() == false)
                {
                    SteamTrade.RespondToTrade(callback.TradeID, false);
                    return;
                }

                try
                {
                    tradeManager.InitializeTrade(SteamUser.SteamID, callback.OtherClient);
                }
                catch (WebException we)
                {
                    SteamFriends.SendChatMessage(callback.OtherClient,
                             EChatEntryType.ChatMsg,
                             "Trade error: " + we.Message);

                    SteamTrade.RespondToTrade(callback.TradeID, false);
                    return;
                }
                catch (Exception)
                {
                    SteamFriends.SendChatMessage(callback.OtherClient,
                             EChatEntryType.ChatMsg,
                             "Trade declined. Could not correctly fetch your backpack.");

                    SteamTrade.RespondToTrade(callback.TradeID, false);
                    return;
                }

                //if (tradeManager.OtherInventory.IsPrivate)
                //{
                //    SteamFriends.SendChatMessage(callback.OtherClient, 
                //                                 EChatEntryType.ChatMsg,
                //                                 "Trade declined. Your backpack cannot be private.");

                //    SteamTrade.RespondToTrade (callback.TradeID, false);
                //    return;
                //}

                if (CurrentTrade == null && GetUserHandler(callback.OtherClient).OnTradeRequest())
                    SteamTrade.RespondToTrade(callback.TradeID, true);
                else
                    SteamTrade.RespondToTrade(callback.TradeID, false);
            });

            msg.Handle<SteamTrading.TradeResultCallback>(callback =>
            {
                if (callback.Response == EEconTradeResponse.Accepted)
                {
                    Log.Debug("Trade Status: {0}", callback.Response);
                    Log.Info("Trade Accepted!");
                    GetUserHandler(callback.OtherClient).OnTradeRequestReply(true, callback.Response.ToString());
                }
                else
                {
                    Log.Warn("Trade failed: {0}", callback.Response);
                    CloseTrade();
                    GetUserHandler(callback.OtherClient).OnTradeRequestReply(false, callback.Response.ToString());
                }

            });
            #endregion

            #region Disconnect
            msg.Handle<SteamUser.LoggedOffCallback>(callback =>
            {
                IsLoggedIn = false;
                Log.Warn("Logged off Steam.  Reason: {0}", callback.Result);
            });

            msg.Handle<SteamClient.DisconnectedCallback>(callback =>
            {
                if (IsLoggedIn)
                {
                    IsLoggedIn = false;
                    CloseTrade();
                    Log.Warn("Disconnected from Steam Network!");
                }

                SteamClient.Connect();
            });
            #endregion

            #region Notifications
            msg.Handle<SteamBot.SteamNotifications.NotificationCallback>(callback =>
            {
                //currently only appears to be of trade offer
                if (callback.Notifications.Count != 0)
                {
                    foreach (var notification in callback.Notifications)
                    {
                        Log.Info(notification.UserNotificationType + " notification");
                    }
                }

                // Get offers only if cookies are valid
                if (CheckCookies())
                    tradeOfferManager.GetOffers();
            });

            msg.Handle<SteamBot.SteamNotifications.CommentNotificationCallback>(callback =>
            {
                //various types of comment notifications on profile/activity feed etc
                //Log.Info("received CommentNotificationCallback");
                //Log.Info("New Commments " + callback.CommentNotifications.CountNewComments);
                //Log.Info("New Commments Owners " + callback.CommentNotifications.CountNewCommentsOwner);
                //Log.Info("New Commments Subscriptions" + callback.CommentNotifications.CountNewCommentsSubscriptions);
            });
            #endregion
        }

        public string WaitForInput()
        {
            consoleInput = null;
            while (true)
            {
                if (consoleInput != null)
                {
                    return consoleInput;
                }
                Thread.Sleep(5);
            }
        }

        /// <summary>
        /// Link a mobile authenticator to bot account, using SteamTradeOffersBot as the authenticator.
        /// Called from bot manager console. Usage: "exec [index] linkauth"
        /// If successful, 2FA will be required upon the next login.
        /// Use "exec [index] getauth" if you need to get a Steam Guard code for the account.
        /// To deactivate the authenticator, use "exec [index] unlinkauth".
        /// </summary>
        void LinkMobileAuth()
        {
            new Thread(() =>
            {
                var login = new SteamAuth.UserLogin(logOnDetails.Username, logOnDetails.Password);
                var loginResult = login.DoLogin();
                if (loginResult == SteamAuth.LoginResult.NeedEmail)
                {
                    while (loginResult == SteamAuth.LoginResult.NeedEmail)
                    {
                        Log.Interface("Enter Steam Guard code from email (type \"input [index] [code]\"):");
                        var emailCode = WaitForInput();
                        login.EmailCode = emailCode;
                        loginResult = login.DoLogin();
                    }
                }
                if (loginResult == SteamAuth.LoginResult.LoginOkay)
                {
                    Log.Info("Linking mobile authenticator...");
                    var authLinker = new SteamAuth.AuthenticatorLinker(login.Session);
                    var addAuthResult = authLinker.AddAuthenticator();
                    if (addAuthResult == SteamAuth.AuthenticatorLinker.LinkResult.MustProvidePhoneNumber)
                    {
                        while (addAuthResult == SteamAuth.AuthenticatorLinker.LinkResult.MustProvidePhoneNumber)
                        {
                            Log.Interface("Enter phone number with country code, e.g. +1XXXXXXXXXXX (type \"input [index] [number]\"):");
                            var phoneNumber = WaitForInput();
                            authLinker.PhoneNumber = phoneNumber;
                            addAuthResult = authLinker.AddAuthenticator();
                        }
                    }
                    if (addAuthResult == SteamAuth.AuthenticatorLinker.LinkResult.AwaitingFinalization)
                    {
                        SteamGuardAccount = authLinker.LinkedAccount;
                        try
                        {
                            var authFile = Path.Combine("authfiles", String.Format("{0}.auth", logOnDetails.Username));
                            Directory.CreateDirectory(Path.Combine(System.Windows.Forms.Application.StartupPath, "authfiles"));
                            File.WriteAllText(authFile, Newtonsoft.Json.JsonConvert.SerializeObject(SteamGuardAccount));
                            Log.Interface("Enter SMS code (type \"input [index] [code]\"):");
                            var smsCode = WaitForInput();
                            var authResult = authLinker.FinalizeAddAuthenticator(smsCode);
                            if (authResult == SteamAuth.AuthenticatorLinker.FinalizeResult.Success)
                            {
                                Log.Success("Linked authenticator.");
                            }
                            else
                            {
                                Log.Error("Error linking authenticator: " + authResult);
                            }
                        }
                        catch (IOException)
                        {
                            Log.Error("Failed to save auth file. Aborting authentication.");
                        }
                    }
                    else
                    {
                        Log.Error("Error adding authenticator: " + addAuthResult);
                    }
                }
                else
                {
                    if (loginResult == SteamAuth.LoginResult.Need2FA)
                    {
                        Log.Error("Mobile authenticator has already been linked!");
                    }
                    else
                    {
                        Log.Error("Error performing mobile login: " + loginResult);
                    }
                }
            }).Start();
        }

        string GetMobileAuthCode()
        {
            var authFile = Path.Combine("authfiles", String.Format("{0}.auth", logOnDetails.Username));
            if (File.Exists(authFile))
            {
                SteamGuardAccount = Newtonsoft.Json.JsonConvert.DeserializeObject<SteamAuth.SteamGuardAccount>(File.ReadAllText(authFile));
                return SteamGuardAccount.GenerateSteamGuardCode();
            }
            return string.Empty;
        }

        void UserLogOn()
        {
            // get sentry file which has the machine hw info saved 
            // from when a steam guard code was entered
            Directory.CreateDirectory(System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, "sentryfiles"));
            FileInfo fi = new FileInfo(System.IO.Path.Combine("sentryfiles",String.Format("{0}.sentryfile", logOnDetails.Username)));

            if (fi.Exists && fi.Length > 0)
                logOnDetails.SentryFileHash = SHAHash(File.ReadAllBytes(fi.FullName));
            else
                logOnDetails.SentryFileHash = null;

            SteamUser.LogOn(logOnDetails);
        }

        void UserWebLogOn()
        {
            do
            {
                IsLoggedIn = SteamWeb.Authenticate(myUniqueId, SteamClient, myUserNonce);

                if(!IsLoggedIn)
                {
                    Log.Warn("Authentication failed, retrying in 2s...");
                    Thread.Sleep(2000);
                }
            } while(!IsLoggedIn);

            Log.Success("User Authenticated!");

            tradeManager = new TradeManager(ApiKey, SteamWeb);
            tradeManager.SetTradeTimeLimits(MaximumTradeTime, MaximumActionGap, tradePollingInterval);
            tradeManager.OnTimeout += OnTradeTimeout;
            tradeOfferManager = new TradeOfferManager(ApiKey, SteamWeb);
            SubscribeTradeOffer(tradeOfferManager);
            cookiesAreInvalid = false;
            // Success, check trade offers which we have received while we were offline
            tradeOfferManager.GetOffers();
        }

        /// <summary>
        /// Checks if sessionId and token cookies are still valid.
        /// Sets cookie flag if they are invalid.
        /// </summary>
        /// <returns>true if cookies are valid; otherwise false</returns>
        bool CheckCookies()
        {
            // We still haven't re-authenticated
            if (cookiesAreInvalid)
                return false;

            try
            {
                if (!SteamWeb.VerifyCookies())
                {
                    // Cookies are no longer valid
                    Log.Warn("Cookies are invalid. Need to re-authenticate.");
                    cookiesAreInvalid = true;
                    SteamUser.RequestWebAPIUserNonce();
                    return false;
                }
            }
            catch
            {
                // Even if exception is caught, we should still continue.
                Log.Warn("Cookie check failed. http://steamcommunity.com is possibly down.");
            }

            return true;
        }

        UserHandler GetUserHandler(SteamID sid)
        {
            if (!userHandlers.ContainsKey(sid))
                userHandlers[sid] = createHandler(this, sid);
            return userHandlers[sid];
        }

        void RemoveUserHandler(SteamID sid)
        {
            if (userHandlers.ContainsKey(sid))
                userHandlers.Remove(sid);
        }

        static byte [] SHAHash (byte[] input)
        {
            SHA1Managed sha = new SHA1Managed();
            
            byte[] output = sha.ComputeHash( input );
            
            sha.Clear();
            
            return output;
        }

        void OnUpdateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback machineAuth)
        {
            byte[] hash = SHAHash (machineAuth.Data);

            Directory.CreateDirectory(System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, "sentryfiles"));

            File.WriteAllBytes (System.IO.Path.Combine("sentryfiles", String.Format("{0}.sentryfile", logOnDetails.Username)), machineAuth.Data);
            
            var authResponse = new SteamUser.MachineAuthDetails
            {
                BytesWritten = machineAuth.BytesToWrite,
                FileName = machineAuth.FileName,
                FileSize = machineAuth.BytesToWrite,
                Offset = machineAuth.Offset,
                
                SentryFileHash = hash, // should be the sha1 hash of the sentry file we just wrote
                
                OneTimePassword = machineAuth.OneTimePassword, // not sure on this one yet, since we've had no examples of steam using OTPs
                
                LastError = 0, // result from win32 GetLastError
                Result = EResult.OK, // if everything went okay, otherwise ~who knows~
                JobID = machineAuth.JobID, // so we respond to the correct server job
            };
            
            // send off our response
            SteamUser.SendMachineAuthResponse (authResponse);
        }

        /// <summary>
        /// Gets the bot's inventory and stores it in MyInventory.
        /// </summary>
        /// <example> This sample shows how to find items in the bot's inventory from a user handler.
        /// <code>
        /// Bot.GetInventory(); // Get the inventory first
        /// foreach (var item in Bot.MyInventory.Items)
        /// {
        ///     if (item.Defindex == 5021)
        ///     {
        ///         // Bot has a key in its inventory
        ///     }
        /// }
        /// </code>
        /// </example>
        public void GetInventory()
        {
            myInventoryTask = Task.Factory.StartNew((Func<Inventory>) FetchBotsInventory);
        }

        public void TradeOfferRouter(TradeOffer offer)
        {
            if (offer.OfferState == TradeOfferState.TradeOfferStateActive)
            {
                GetUserHandler(offer.PartnerSteamId).OnNewTradeOffer(offer);
            }
        }
        public void SubscribeTradeOffer(TradeOfferManager tradeOfferManager)
        {
            tradeOfferManager.OnNewTradeOffer += TradeOfferRouter;
        }

        //todo: should unsubscribe eventually...
        public void UnsubscribeTradeOffer(TradeOfferManager tradeOfferManager)
        {
            tradeOfferManager.OnNewTradeOffer -= TradeOfferRouter;
        }

        /// <summary>
        /// Subscribes all listeners of this to the trade.
        /// </summary>
        public void SubscribeTrade (Trade trade, UserHandler handler)
        {
            trade.OnSuccess += handler.OnTradeSuccess;
            trade.OnClose += handler.OnTradeClose;
            trade.OnError += handler.OnTradeError;
            trade.OnStatusError += handler.OnStatusError;
            //trade.OnTimeout += OnTradeTimeout;
            trade.OnAfterInit += handler.OnTradeInit;
            trade.OnUserAddItem += handler.OnTradeAddItem;
            trade.OnUserRemoveItem += handler.OnTradeRemoveItem;
            trade.OnMessage += handler.OnTradeMessageHandler;
            trade.OnUserSetReady += handler.OnTradeReadyHandler;
            trade.OnUserAccept += handler.OnTradeAcceptHandler;
        }
        
        /// <summary>
        /// Unsubscribes all listeners of this from the current trade.
        /// </summary>
        public void UnsubscribeTrade (UserHandler handler, Trade trade)
        {
            trade.OnSuccess -= handler.OnTradeSuccess;
            trade.OnClose -= handler.OnTradeClose;
            trade.OnError -= handler.OnTradeError;
            trade.OnStatusError -= handler.OnStatusError;
            //Trade.OnTimeout -= OnTradeTimeout;
            trade.OnAfterInit -= handler.OnTradeInit;
            trade.OnUserAddItem -= handler.OnTradeAddItem;
            trade.OnUserRemoveItem -= handler.OnTradeRemoveItem;
            trade.OnMessage -= handler.OnTradeMessageHandler;
            trade.OnUserSetReady -= handler.OnTradeReadyHandler;
            trade.OnUserAccept -= handler.OnTradeAcceptHandler;
        }

        /// <summary>
        /// Fetch the Bot's inventory and log a warning if it's private
        /// </summary>
        private Inventory FetchBotsInventory()
        {
            var inventory = Inventory.FetchInventory(SteamUser.SteamID, ApiKey, SteamWeb);
            if(inventory.IsPrivate)
            {
                Log.Warn("The bot's backpack is private! If your bot adds any items it will fail! Your bot's backpack should be Public.");
            }
            return inventory;
        }

        public void AcceptAllMobileTradeConfirmations()
        {
            if (SteamGuardAccount == null)
            {
                Log.Warn("Bot account does not have 2FA enabled.");
            }
            else
            {
                SteamGuardAccount.Session.SteamLogin = SteamWeb.Token;
                SteamGuardAccount.Session.SteamLoginSecure = SteamWeb.TokenSecure;
                try
                {
                    foreach (var confirmation in SteamGuardAccount.FetchConfirmations())
                    {
                        if (SteamGuardAccount.AcceptConfirmation(confirmation))
                        {
                            Log.Success("Confirmed {0}. (Confirmation ID #{1})", confirmation.ConfirmationDescription, confirmation.ConfirmationID);
                        }
                    }
                }
                catch (SteamAuth.SteamGuardAccount.WGTokenInvalidException)
                {
                    Log.Error("Invalid session when trying to fetch trade confirmations.");
                }
            }
        }

        #region Background Worker Methods

        private void BackgroundWorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            if (runWorkerCompletedEventArgs.Error != null)
            {
                Exception ex = runWorkerCompletedEventArgs.Error;

                Log.Error("Unhandled exceptions in bot {0} callback thread: {1} {2}",
                      DisplayName,
                      Environment.NewLine,
                      ex);

                Log.Info("This bot died. Stopping it..");
                //backgroundWorker.RunWorkerAsync();
                //Thread.Sleep(10000);
                StopBot();
                //StartBot();
            }
        }

        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            ICallbackMsg msg;

            while (!botThread.CancellationPending)
            {
                try
                {
                    msg = SteamClient.WaitForCallback(true);
                    HandleSteamMessage(msg);
                }
                catch (WebException e)
                {
                    Log.Error("URI: {0} >> {1}", (e.Response != null && e.Response.ResponseUri != null ? e.Response.ResponseUri.ToString() : "unknown"), e.ToString());
                    System.Threading.Thread.Sleep(45000);//Steam is down, retry in 45 seconds.
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                    Log.Warn("Restarting bot...");
                }
            }
        }

        #endregion Background Worker Methods

        private void FireOnSteamGuardRequired(SteamGuardRequiredEventArgs e)
        {
            // Set to null in case this is another attempt
            this.AuthCode = null;

            EventHandler<SteamGuardRequiredEventArgs> handler = OnSteamGuardRequired;
            if (handler != null)
                handler(this, e);
            else
            {
                while (true)
                {
                    if (this.AuthCode != null)
                    {
                        e.SteamGuard = this.AuthCode;
                        break;
                    }

                    Thread.Sleep(5);
                }
            }
        }

        #region Group Methods

        /// <summary>
        /// Accepts the invite to a Steam Group
        /// </summary>
        /// <param name="group">SteamID of the group to accept the invite from.</param>
        private void AcceptGroupInvite(SteamID group)
        {
            var AcceptInvite = new ClientMsg<CMsgGroupInviteAction>((int)EMsg.ClientAcknowledgeClanInvite);

            AcceptInvite.Body.GroupID = group.ConvertToUInt64();
            AcceptInvite.Body.AcceptInvite = true;

            this.SteamClient.Send(AcceptInvite);
            
        }

        /// <summary>
        /// Declines the invite to a Steam Group
        /// </summary>
        /// <param name="group">SteamID of the group to decline the invite from.</param>
        private void DeclineGroupInvite(SteamID group)
        {
            var DeclineInvite = new ClientMsg<CMsgGroupInviteAction>((int)EMsg.ClientAcknowledgeClanInvite);

            DeclineInvite.Body.GroupID = group.ConvertToUInt64();
            DeclineInvite.Body.AcceptInvite = false;

            this.SteamClient.Send(DeclineInvite);
        }

        /// <summary>
        /// Invites a use to the specified Steam Group
        /// </summary>
        /// <param name="user">SteamID of the user to invite.</param>
        /// <param name="groupId">SteamID of the group to invite the user to.</param>
        public void InviteUserToGroup(SteamID user, SteamID groupId)
        {
            var InviteUser = new ClientMsg<CMsgInviteUserToGroup>((int)EMsg.ClientInviteUserToClan);

            InviteUser.Body.GroupID = groupId.ConvertToUInt64();
            InviteUser.Body.Invitee = user.ConvertToUInt64();
            InviteUser.Body.UnknownInfo = true;

            this.SteamClient.Send(InviteUser);
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;
            StopBot();
            if (disposing)
                Log.Dispose();
            disposed = true;
        }
    }
}
