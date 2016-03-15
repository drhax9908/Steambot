using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using SteamTrade.TradeWebAPI;
using System.Threading;
using SteamTrade.TradeOffer;
using System;
using System.Timers;
using ChatterBotAPI;
using System.Threading.Tasks;
using SteamBot.Networking.TCP_server;

namespace SteamBot
{
    public class ArkarrUserHandler : UserHandler
    {
        private double cent;

        public ArkarrUserHandler(Bot bot, SteamID sid) : base(bot, sid)
        {
            bot.mySteamInventory = new GenericInventory(bot.SteamWeb);
            bot.otherSteamInventory = new GenericInventory(bot.SteamWeb);
        }

        public override bool OnGroupAdd()
        {
            return false;
        }

        public override bool OnFriendAdd () 
        {
            return true;
        }

        public override void OnLoginCompleted()
        {
            Log.Success("SteamBot Version 1.8 started !");
            Log.Success("Visit the Alliedmodders page daily !");
            Log.Success("Important updates may happen !");
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message)
        {
            Log.Info(Bot.SteamFriends.GetFriendPersonaName(sender) + ": " + message);
            base.OnChatRoomMessage(chatID, sender, message);
        }

        public override void OnFriendRemove () {}

        public override void OnTradeAwaitingConfirmation(long tradeOfferID)
        {
            Log.Warn("Trade ended awaiting confirmation");
            SendChatMessage("Please complete the confirmation to finish the trade");

            RegistreTradeOfferInDatabase(tradeOfferID);
        }
        
        public override void OnMessage (string message, EChatEntryType type) 
        {
            if (message == "LIST" &&  Bot.MySteamID.Contains(OtherSID.ToString()))
            {
                List<long> contextId = new List<long>();
                contextId.Add(2);
                Bot.mySteamInventory.load(Bot.BotGameUsage, contextId, Bot.SteamClient.SteamID);

                SendChatMessage("My inventory (" + Bot.BotGameUsage + ") contain " + Bot.mySteamInventory.items.Count + " item(s) !");
                foreach (KeyValuePair<ulong, GenericInventory.Item> item in Bot.mySteamInventory.items)
                {
                    SteamTrade.GenericInventory.ItemDescription description = Bot.mySteamInventory.getDescription(item.Key);
                    SendChatMessage(description.name + " in game " + Bot.mySteamInventory.items[item.Key].appid);
                }
            }
            else if (message == "TCP" && Bot.MySteamID.Contains(OtherSID.ToString()))
            {
                ThreadState state = Bot.StartListening.ThreadState;
                SendChatMessage(state.ToString());
            }
            else if (message.StartsWith("EXEC") && Bot.MySteamID.Contains(OtherSID.ToString()))
            {
                message = message.Replace("EXEC", "");
                string[] cmd = message.Split(' ');
                if (cmd.Length >= 2)
                {
                    string command = "";
                    for(int i = 2; i < cmd.Length; i++)
                        command += cmd[i] + " ";
                    command.Remove(command.Length - 1);

                    if (Bot.SendCommandToServer(Int32.Parse(cmd[1]), command))
                        SendChatMessage("Executed '" + command + "' on server id '" + cmd[1] + "' !");
                    else
                        SendChatMessage("Wrong server ID, use 'SERVERLIST' to get the server ID.");
                }
                else
                {
                    SendChatMessage("Usage : EXEC [serverid] [command]");
                    SendChatMessage("Exemple : EXEC 0 sm_test 1 2 3");
                }
            }
            else if (message == "SERVERLIST" && Bot.MySteamID.Contains(OtherSID.ToString()))
            {
                List<string> existingServer = new List<string>();
                SendChatMessage("Number of connection : " + Bot.socket.Clients.Count);
                SendChatMessage("Server ID   Server name");
                for(int i = 0; i < Bot.socket.Clients.Count; i++)
                {
                    if (!existingServer.Contains(Bot.socket.Clients[i].Name) && Bot.socket.Clients[i].clientSocket.Connected)
                    {
                        SendChatMessage(i + "             " + Bot.socket.Clients[i].Name);
                        existingServer.Add(Bot.socket.Clients[i].Name);
                    }
                }
                SendChatMessage("-----------------------");
            }
            else
            {
                SendChatMessage(Bot.chatbotSession.Think(message));
            }
        }

        public override bool OnTradeRequest()
        {
            //Bot.SteamFriends.SendChatMessage(new SteamID("STEAM_0:1:XXXX"), EChatEntryType.ChatMsg, OtherSID + " started a trade !");
            return true;
        }
        
        public override void OnTradeError (string error) 
        {
            SendChatMessage("Crap ! A error ! Please, send me another trade request, would you ?");
            Log.Warn(error);
        }
        
        public override void OnTradeTimeout () 
        {
            SendChatMessage("Sorry dear, but you left me alone, so I cancelled the trade.");
            Log.Info("User was kicked because he was AFK.");
        }
        
        public override void OnTradeInit()
        {
            SendTradeMessage("Heyo ! Welcome to the trade form ! Soooo... should we begin ?");
        }
        
        public override void OnTradeAddItem (Schema.Item schemaItem, Inventory.Item inventoryItem) {}
        
        public override void OnTradeRemoveItem (Schema.Item schemaItem, Inventory.Item inventoryItem) {}
        
        public override void OnTradeMessage (string message)
        {
            if (message == "ALL" && Bot.MySteamID.Contains(OtherSID.ToString()))
            {
                List<long> contextId = new List<long>();
                contextId.Add(2);
                Bot.mySteamInventory.load(Bot.BotGameUsage, contextId, Bot.SteamClient.SteamID);

                Trade.SendMessage("Welcome " + Bot.MySteamID + " ! This is what I have.");
                foreach (GenericInventory.Item item in Bot.mySteamInventory.items.Values)
                    Trade.AddItem(item);
            }
        }
        
        public override void OnTradeReady (bool ready) 
        {
            if (!ready)
            {
                Trade.SetReady (false);
            }
            else
            {
                if (Validate())
                {
                    Trade.SetReady(true);
                    SendTradeMessage("You are about to trade for {0}$ !", cent);
                }
            }
        }

        public override void OnTradeSuccess()
        {
            Log.Success("Trade Complete.");
            //TO DO !!
        }

        private void RegistreTradeOfferInDatabase(long tradeOfferID)
        {
            Bot.listIDTradeOffer.Add(tradeOfferID);

            string[] rowsTO = new string[3];
            rowsTO[0] = "SteamID";
            rowsTO[1] = "TradeOfferID";
            rowsTO[2] = "tradeValue";

            string[] valuesTO = new string[3];
            valuesTO[0] = OtherSID.ToString();
            valuesTO[1] = tradeOfferID.ToString();
            valuesTO[2] = cent.ToString();

            Bot.DB.INSERT("tradeoffers", rowsTO, valuesTO);

            foreach (TradeUserAssets asset in Trade.OtherOfferedItems)
            {
                SteamTrade.GenericInventory.ItemDescription description = Bot.otherSteamInventory.getDescription(asset.assetid);

                string[] rowsI = new string[3];
                rowsI[0] = "itemName";
                rowsI[1] = "tradeoffersid";
                rowsI[2] = "togive";

                string[] valuesI = new string[3];
                valuesI[0] = description.name;
                valuesI[1] = tradeOfferID.ToString();
                valuesI[2] = "0";

                Bot.DB.INSERT("items", rowsI, valuesI);
            }

            foreach (TradeUserAssets asset in Trade.MyOfferedItems)
            {
                SteamTrade.GenericInventory.ItemDescription description = Bot.mySteamInventory.getDescription(asset.assetid);

                string[] rowsI = new string[3];
                rowsI[0] = "itemName";
                rowsI[1] = "tradeoffersid";
                rowsI[2] = "togive";

                string[] valuesI = new string[3];
                valuesI[0] = description.name;
                valuesI[1] = tradeOfferID.ToString();
                valuesI[2] = "1";

                Bot.DB.INSERT("items", rowsI, valuesI);
            }
        }

        public override void OnTradeAccept() 
        {
            if (Validate() || IsAdmin)
            {
                //Even if it is successful, AcceptTrade can fail on
                //trades with a lot of items so we use a try-catch
                try {
                    if (Trade.AcceptTrade())
                        Log.Success("Trade Accepted!");
                }
                catch {
                    Log.Warn ("The trade might have failed, but we can't be sure.");
                }
            }
        }

        public bool Validate ()
        {            
            cent = 0.0;
            
            List<string> errors = new List<string> ();

            List<long> contextId = new List<long>();
            contextId.Add(2);
            Bot.otherSteamInventory.load(Bot.BotGameUsage, contextId, OtherSID);

            if (Bot.sm.data.response.items != null && Bot.sm.data.response.items.Count != 0)
            {
                IEnumerable<TradeUserAssets> listOfferedItems = Trade.OtherOfferedItems;
                foreach (TradeUserAssets tradeItem in listOfferedItems)
                {
                    if (Bot.otherSteamInventory.items.ContainsKey(tradeItem.assetid))
                    {
                        SteamTrade.GenericInventory.ItemDescription description = Bot.otherSteamInventory.getDescription(tradeItem.assetid);
                        SteamMarketPrices.Item itemInfo = Bot.sm.data.response.items.Find(i => i.name == description.name);
                        if (itemInfo != null)
                            cent += (itemInfo.value / 100.0);
                    }
                }
            }
            else
            {
                errors.Add("[X] I'm busy right now, come back in 5 minutes !");
            }
            
            //if (AmountAdded == TF2Value.Zero)
               // errors.Add ("[X] You must put up at least 1 scrap or 1 key !");
            
            // send the errors
            if (errors.Count != 0)
            {
                SendTradeMessage("Hum... sorry. I can't accept that trade offer, check the messages bellow :");
                foreach (string error in errors)
                    SendTradeMessage(error);
            }

            return errors.Count == 0;
        }
        
    }
 
}

