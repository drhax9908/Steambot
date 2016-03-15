using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SteamTrade
{
    public class SteamMarketPrices
    {
        private SteamWeb steamWeb;
        public RootObject data {get; set;}

        public SteamMarketPrices()
        {
            steamWeb = new SteamWeb();
        }

        public bool ScanMarket(string backpackAPIKey, int appID)
        {
            switch(appID)
            {
                case 440: Console.WriteLine("Scanning TF2 Steam market..."); break;
                case 730: Console.WriteLine("Scanning CS:GO Steam market..."); break;
                default: Console.WriteLine("Scanning Steam market for game id "+appID+"..."); break;
            }
            string response = steamWeb.Fetch("http://backpack.tf/api/IGetMarketPrices/v1/?key=" + backpackAPIKey + "&appid=" + appID, "POST");
            RootObject tmpData = Parse(response);
            if (tmpData.response.success == 0)
            {
                if (data == null) data = tmpData;
                Console.WriteLine(">>> ERROR :");
                Console.WriteLine(tmpData.response.message);
                Console.WriteLine(">>> Prices NOT updated.");
                return false;
            }
            data = tmpData;
            Console.WriteLine("Item list updated !");
            return true;
        }

        private RootObject Parse(string jsonString)
        {
            dynamic jsonObject = JsonConvert.DeserializeObject(jsonString);
            RootObject parsed = null;
            if (jsonObject.response.success == 0)
            {
                //The response object is not valid and has a message...
                parsed = new RootObject()
                {
                    response = new Response()
                    {
                        success = jsonObject.response.success,
                        message = jsonObject.response.message
                    }
                };
            }
            else
            {
                parsed = new RootObject()
                {
                    response = new Response()
                    {
                        success = jsonObject.response.success,
                        current_time = jsonObject.response.current_time,
                        items = ParseItems(jsonObject.response.items)
                    }
                };
            }
            return parsed;
        }

        private List<Item> ParseItems(dynamic items)
        {
            List<Item> itemList = new List<Item>();
            foreach (var item in items)
            {
                itemList.Add(new Item()
                {
                    name = item.Name,
                    last_updated = item.Value.last_updated,
                    quantity = item.Value.quantity,
                    value = item.Value.value
                });
            }
            return itemList;
        }

        public class Item
        {
            public Item(){}
            public Item(string name, int lastUpdated, int quantity, double value)
            {
                this.name = name;
                this.last_updated = last_updated;
                this.quantity = quantity;
                this.value = value;
            }

            public string name
            {
                get;
                set;
            }
            public int last_updated { get; set; }
            public int quantity { get; set; }
            public double value { get; set; }
        }

        public class Response
        {
            public int success { get; set; }
            public string message { get; set; }
            public int current_time { get; set; }
            public List<Item> items { get; set; }
        }

        public class RootObject
        {
            public Response response { get; set; }
        }
    }
}
