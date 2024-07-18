using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using Newtonsoft.Json.Linq;
using RestSharp;
using Serilog;
using Trady.Core.Infrastructure;
using Trady.Core;
using System.Collections;
using Newtonsoft.Json;

namespace SuperExBot
{
    public static class Program
    {
        public static DateTime StartUnixTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static string ApiKey = "", ApiSecret = "", Symbol = "", IslemTipi = "", Quantity = "";
        public static int Interval = 0, dEma1 = 0, dEma2 = 0;

        public static List<Candlestick> CandlestickList { get; set; }
        public static Root myDeserializedClass { get; set; }
        public static OrderBook OrdersBooks { get; set; }
        public static IslemSonucu Sonuclar { get; set; }
        public static void Main(string[] args)
        {
            if (System.Diagnostics.Process.GetProcessesByName("SuperExBot").Length == 1)
            {
                ApiInformationLoading();

                CandlestickList = new List<Candlestick>();
                CandlestickList.Clear();

                QueryKline(ApiKey, Symbol, Interval, 1500);

                var candlestick = CandlestickList.ToList();

                var candlesticks = candlestick.Where(x => Program.StartUnixTime.AddMilliseconds(x.OpenTime).ToUniversalTime() < DateTime.Now.ToUniversalTime()).ToArray();

                //if (candlesticks.Count() <= 12)
                //    return;

                List<IOhlcv> tradyCandles = candlesticks.Select(candle => new Candle(Program.StartUnixTime.AddMilliseconds(candle.OpenTime).ToUniversalTime(), candle.Open, candle.High, candle.Low, candle.Close, candle.Volume)).Cast<IOhlcv>().ToList();

                List<decimal?> Ema1 = new List<decimal?>();
                List<decimal?> Ema2 = new List<decimal?>();
                List<string> BuyList = new List<string>();

                Ema1 = Logs.Analysis.EmaHesaplama(tradyCandles, dEma1);
                Ema2 = Logs.Analysis.EmaHesaplama(tradyCandles, dEma2);

                List<bool?> longCond = new List<bool?>();
                List<bool?> shortCond = new List<bool?>();
                List<bool?> longCondition = new List<bool?>();
                List<bool?> shortCondition = new List<bool?>();
                List<int?> CondIni = new List<int?>();

                longCondition.Add(false);
                shortCondition.Add(false);

                longCond.Add(false);
                shortCond.Add(false);

                int dContIni = 0;
                CondIni.Add(0);

                string strDurum = "-";

                longCond = CrossoverDegerler(Ema1, Ema2);
                shortCond = CrossunderDegerler(Ema1, Ema2);

                for (int i = 1; i < longCond.Count; i++)
                {
                    dContIni = longCond[i].Value ? 1 : shortCond[i].Value ? -1 : CondIni[i - 1].Value;
                    CondIni.Add(dContIni);
                }

                for (int i = 1; i < CondIni.Count; i++)
                {
                    bool blnLongs = longCond[i].Value && CondIni[i - 1].Value == -1;
                    bool blnShorts = shortCond[i].Value && CondIni[i - 1].Value == 1;

                    longCondition.Add(blnLongs);
                    shortCondition.Add(blnShorts);
                }

                for (int i = 0; i < longCondition.Count; i++)
                {
                    bool blnBuy = longCondition[i].Value;
                    bool blnSell = shortCondition[i].Value;
                    string strDate = tradyCandles[i].DateTime.ToString();
                    if (blnBuy)
                    {
                        strDurum = "BUY";// - " + strDate;
                        BuyList.Add(strDurum);
                    }
                    else if (blnSell)
                    {
                        strDurum = "SELL";// - " + strDate;
                        BuyList.Add(strDurum);
                    }
                    else
                    {
                        strDurum = "-";
                        BuyList.Add(strDurum);
                    }
                }

                string strStatus = BuyList[BuyList.Count - 1].ToString();

                string[] strCoinMinQty = tradyCandles[tradyCandles.Count - 1].Close.ToString().Split(',');
                //string strCoinMiktarAra = strCoinMinQty.Length > 1 ? strCoinMinQty[1].Length : strCoinMinQty[0].Length;
                int dRound = strCoinMinQty.Length > 1 ? strCoinMinQty[1].Length : strCoinMinQty[0].Length;

                if (IslemTipi == "1")
                {
                    CancelAllOrder(ApiKey, Symbol);

                    for (int c = 0; c < 2; c++)
                    {
                        if (c == 0)
                        {
                            string strPadLeft = "0";
                            string strIslem = "0," + strPadLeft.PadRight(dRound, '0');
                            int a = 5;

                            decimal flQuantity = Convert.ToDecimal(Quantity) / tradyCandles[tradyCandles.Count - 1].Close;

                            GetOrderBook(ApiKey, Symbol);

                            decimal flOrders = Convert.ToDecimal(OrdersBooks.data.bids[0][0].ToString());

                            for (int i = 5; i < 10; i++)
                            {
                                decimal flIslem = 0;

                                if (i != 5)
                                {
                                    string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                                    flIslem = Convert.ToDecimal(strIslems);
                                }
                                else
                                {
                                    string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                                    flIslem = Convert.ToDecimal(strIslems);
                                }

                                strIslem = flIslem.ToString();

                                decimal flCikarim = Convert.ToDecimal(strIslem);
                                decimal flSonuc = flOrders - flCikarim;
                                a++;

                                SubmitOrder(ApiKey, Symbol, 1, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                            }
                        }
                        else
                        {
                            string strPadLeft = "0";
                            string strIslem = "0," + strPadLeft.PadRight(dRound, '0');
                            int a = 5;

                            decimal flQuantity = Convert.ToDecimal(Quantity) / tradyCandles[tradyCandles.Count - 1].Close;

                            GetOrderBook(ApiKey, Symbol);

                            decimal flOrders = Convert.ToDecimal(OrdersBooks.data.asks[0][0].ToString());

                            for (int i = 5; i < 10; i++)
                            {
                                decimal flIslem = 0;

                                if (i != 5)
                                {
                                    string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                                    flIslem = Convert.ToDecimal(strIslems);
                                }
                                else
                                {
                                    string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                                    flIslem = Convert.ToDecimal(strIslems);
                                }

                                strIslem = flIslem.ToString();

                                decimal flCikarim = Convert.ToDecimal(strIslem);
                                decimal flSonuc = flOrders + flCikarim;
                                a++;

                                SubmitOrder(ApiKey, Symbol, 2, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                            }
                        }
                    }
                }
                else if (IslemTipi == "2")
                {
                    //strStatus = tradyCandles[tradyCandles.Count - 1].Volume >= tradyCandles[tradyCandles.Count - 2].Volume ? "BUY" : "SELL";
                    bool blnWhile = true;
                    while (blnWhile)
                    {
                        try
                        {
                            Console.WriteLine("Mevcut Order Verileri Silindi");
                            CancelAllOrder(ApiKey, Symbol);

                            string strPadLeft = "0";
                            string strIslem = "0," + strPadLeft.PadRight(dRound, '0');
                            int a = 1;

                            Console.WriteLine("Order Verileri Getirildi");
                            GetOrderBook(ApiKey, Symbol);
                            decimal flOrders = Convert.ToDecimal(OrdersBooks.data.asks[0][0].ToString());
                            decimal flQuantity = Convert.ToDecimal(Quantity) / flOrders;
                            flQuantity = Math.Round(flQuantity, 0);
                            decimal flIslem = 0;


                            string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                            flIslem = Convert.ToDecimal(strIslems);

                            strIslem = flIslem.ToString();

                            decimal flCikarim = Convert.ToDecimal(strIslem);
                            decimal flSonuc = flOrders - flCikarim;
                            Console.WriteLine("Order Girildi - Adet : " + flQuantity + " - Fiyat : " + flSonuc.ToString());
                            SubmitOrder(ApiKey, Symbol, 2, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                            Thread.Sleep(8);
                            Console.WriteLine("Order Tamamlandı - Adet : " + flQuantity + " - Fiyat : " + flSonuc.ToString());
                            SubmitOrder(ApiKey, Symbol, 1, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                            Thread.Sleep(8);
                            Console.Clear();
                        }
                        catch (Exception ex)
                        {
                            blnWhile = false;
                            Log.Error("Çalışma sırasında beklenmedik hata oluştu. Detay:\n{Exception}", ex.Message.ToString());
                        }
                    }
                    #region Formül Kapandı
                    //for (int c = 0; c < 2; c++)
                    //{
                    //    if (c == 0)
                    //    {
                    //        string strPadLeft = "0";
                    //        string strIslem = "0," + strPadLeft.PadRight(dRound, '0');
                    //        int a = 5;

                    //        decimal flQuantity = Convert.ToDecimal(Quantity) / tradyCandles[tradyCandles.Count - 1].Close;

                    //        GetOrderBook(ApiKey, Symbol);
                    //        decimal flOrders = Convert.ToDecimal(OrdersBooks.data.bids[0][0].ToString());

                    //        for (int i = 5; i < 10; i++)
                    //        {
                    //            decimal flIslem = 0;

                    //            if (i != 5)
                    //            {
                    //                string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                    //                flIslem = Convert.ToDecimal(strIslems);
                    //            }
                    //            else
                    //            {
                    //                string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                    //                flIslem = Convert.ToDecimal(strIslems);
                    //            }

                    //            strIslem = flIslem.ToString();

                    //            decimal flCikarim = Convert.ToDecimal(strIslem);
                    //            decimal flSonuc = flOrders - flCikarim;
                    //            a++;

                    //            SubmitOrder(ApiKey, Symbol, 1, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                    //        }
                    //    }
                    //    else
                    //    {
                    //        string strPadLeft = "0";
                    //        string strIslem = "0," + strPadLeft.PadRight(dRound, '0');
                    //        int a = 1;

                    //        decimal flQuantity = Convert.ToDecimal(Quantity) / tradyCandles[tradyCandles.Count - 1].Close;

                    //        GetOrderBook(ApiKey, Symbol);
                    //        decimal flOrders = Convert.ToDecimal(OrdersBooks.data.asks[0][0].ToString());

                    //        decimal flIslem = 0;

                    //        //if (i != 5)
                    //        //{
                    //        string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                    //        flIslem = Convert.ToDecimal(strIslems);
                    //        //}
                    //        //else
                    //        //{
                    //        //    string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                    //        //    flIslem = Convert.ToDecimal(strIslems);
                    //        //}

                    //        strIslem = flIslem.ToString();

                    //        decimal flCikarim = Convert.ToDecimal(strIslem);
                    //        decimal flSonuc = flOrders - flCikarim;
                    //        //a++;

                    //        SubmitOrder(ApiKey, Symbol, 2, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                    //        Thread.Sleep(20);
                    //        SubmitOrder(ApiKey, Symbol, 1, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                    //    }
                    //}

                    #endregion
                }
                else if(IslemTipi == "3")
                {
                    bool blnWhile = true;
                    while (blnWhile)
                    {
                        try
                        {
                            Console.WriteLine("Mevcut Order Verileri Silindi");
                            CancelAllOrder(ApiKey, Symbol);

                            string strPadLeft = "0";
                            string strIslem = "0," + strPadLeft.PadRight(dRound, '0');
                            int a = 1;

                            Console.WriteLine("Order Verileri Getirildi");
                            GetOrderBook(ApiKey, Symbol);
                            decimal flOrders = Convert.ToDecimal(OrdersBooks.data.bids[0][0].ToString());
                            decimal flQuantity = Convert.ToDecimal(Quantity) / flOrders;
                            flQuantity = Math.Round(flQuantity, 0);
                            decimal flIslem = 0;


                            string strIslems = "0," + strPadLeft.PadRight(dRound - 1, '0') + a.ToString();
                            flIslem = Convert.ToDecimal(strIslems);

                            strIslem = flIslem.ToString();

                            decimal flCikarim = Convert.ToDecimal(strIslem);
                            decimal flSonuc = flOrders + flCikarim;
                            Console.WriteLine("Order Girildi - Adet : " + flQuantity + " - Fiyat : " + flSonuc.ToString());
                            SubmitOrder(ApiKey, Symbol, 1, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                            Thread.Sleep(8);
                            Console.WriteLine("Order Tamamlandı - Adet : " + flQuantity + " - Fiyat : " + flSonuc.ToString());
                            SubmitOrder(ApiKey, Symbol, 2, Convert.ToInt32(flQuantity).ToString(), Math.Round(flSonuc, dRound).ToString());
                            Thread.Sleep(8);
                            Console.Clear();
                        }
                        catch (Exception ex)
                        {
                            blnWhile = false;
                            Log.Error("Çalışma sırasında beklenmedik hata oluştu. Detay:\n{Exception}", ex.Message.ToString());
                        }
                    }
                }
            }
        }
        public static void CancelAllOrder(string strApiKey, string strSembol)
        {
            try
            {
                string strSorgu = @"https://api.superex.com/spot/spot/order/repeal-all?symbol={0}";
                strSorgu = string.Format(strSorgu, strSembol);

                string strOrjSorgu = strSorgu;

                bool blnDongu = false;

                while (!blnDongu)
                {
                    var options = new RestClientOptions()
                    {
                        MaxTimeout = -1,
                    };
                    var client = new RestClient(options);
                    var request = new RestRequest(strSorgu, Method.Post);
                    request.AddHeader("x-api-key", strApiKey);
                    request.AddHeader("accept-language", "en");
                    request.AddHeader("Content-Type", "application/json");
                    RestResponse response = client.Execute(request);

                    if (response.Content != null)
                    {
                        Sonuclar = JsonConvert.DeserializeObject<IslemSonucu>(response.Content.ToString());

                        if(Sonuclar.data != null || Sonuclar.msg.ToString() == "Order Cancelled" || Sonuclar.msg.ToString() == "There are no cancellable orders")
                        {
                            blnDongu = true;
                        }
                        else
                        {
                            blnDongu = false;
                        }
                        
                    }
                    else
                    {
                        blnDongu = false;
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(strSembol + " Verileri Alınamadı.Detay: {Exception}", ex.Message.ToString());
                Console.WriteLine(strSembol + " Verileri Alınamadı.Detay:\n" + ex.Message.ToString());
            }
        }
        public static void SubmitOrder(string strApiKey, string strSembol,int dTradeType, string strOrderNumber, string strPrice)
        {
            try
            {
                string strSorgu = @"https://api.superex.com/spot/spot/order";
                //strSorgu = string.Format(strSorgu, strSembol);

                string strOrjSorgu = strSorgu;

                bool blnDongu = false;

                while (!blnDongu)
                {
                    var Data = new SubmitOrder()
                    {
                        orderPriceType = 1,
                        symbol = strSembol,
                        tradeType = dTradeType.ToString(),
                        orderNumber = strOrderNumber.Replace(',', '.'),
                        price = strPrice.Replace(',','.'),
                        total = ""
                    };

                    var options = new RestClientOptions()
                    {
                        MaxTimeout = -1,
                    };
                    var client = new RestClient(options);
                    var request = new RestRequest(strSorgu, Method.Post);
                    request.AddHeader("x-api-key", strApiKey);
                    request.AddHeader("accept-language", "en");
                    request.AddHeader("Content-Type", "application/json");
                    var body = JsonConvert.SerializeObject(Data);
                    request.AddParameter("application/json", body, ParameterType.RequestBody);
                    RestResponse response = client.Execute(request);

                    if (response.Content != null)
                    {
                        Sonuclar = JsonConvert.DeserializeObject<IslemSonucu>(response.Content.ToString());

                        if (!string.IsNullOrEmpty(Sonuclar.data.ToString()))
                        {
                            blnDongu = true;
                        }
                        else
                        {
                            blnDongu = false;
                        }

                    }
                    else
                    {
                        blnDongu = false;
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(strSembol + " Verileri Alınamadı.Detay: {Exception}", ex.Message.ToString());
                Console.WriteLine(strSembol + " Verileri Alınamadı.Detay:\n" + ex.Message.ToString());
            }
        }
        public static void QueryKline(string strApiKey, string strSembol, int dInterval, int dLimit = 1500)
        {
            try
            {
                string strSorgu = @"https://api.superex.com/spot/public/v3/market/candles?symbol={0}&timeType={1}&limit={2}";
                strSorgu = string.Format(strSorgu, strSembol, dInterval, dLimit);

                string strOrjSorgu = strSorgu;

                bool blnDongu = false;

                while (!blnDongu)
                {
                    var options = new RestClientOptions()
                    {
                        MaxTimeout = -1,
                    };
                    var client = new RestClient(options);
                    var request = new RestRequest(strSorgu, Method.Get);
                    request.AddHeader("x-api-key", strApiKey);
                    request.AddHeader("accept-language", "en");
                    RestResponse response = client.Execute(request);

                    if (response.Content != null)
                    {
                        myDeserializedClass = JsonConvert.DeserializeObject<Root>(response.Content.ToString());
                        
                        CandlestickList.Clear();

                        foreach (var item in myDeserializedClass.data)
                        {
                            string[] ItemList = item.Split(',');

                            var CandData = new Candlestick
                            {
                                OpenTime = Convert.ToInt64(ItemList[0].ToString()),
                                High = Math.Round(Convert.ToDecimal(ItemList[1].ToString().Replace('.', ',')), 8),
                                Open = Math.Round(Convert.ToDecimal(ItemList[2].ToString().Replace('.', ',')), 8),
                                Low = Math.Round(Convert.ToDecimal(ItemList[3].ToString().Replace('.', ',')), 8),
                                Close = Math.Round(Convert.ToDecimal(ItemList[4].ToString().Replace('.', ',')), 8),
                                Volume = Convert.ToDecimal(ItemList[5].ToString().Replace('.', ',')),
                            };

                            CandlestickList.Add(CandData);
                        }

                        blnDongu = true;
                    }
                    else
                    {
                        blnDongu = false;
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(strSembol + " Verileri Alınamadı.Detay: {Exception}", ex.Message.ToString());
                Console.WriteLine(strSembol + " Verileri Alınamadı.Detay:\n" + ex.Message.ToString());
            }
        }
        public static void GetOrderBook(string strApiKey, string strSembol)
        {
            try
            {
                string strSorgu = @"https://api.superex.com/spot/public/v3/orderbook?depth=5&level=1&market_pair={0}";
                strSorgu = string.Format(strSorgu, strSembol);

                string strOrjSorgu = strSorgu;

                bool blnDongu = false;

                while (!blnDongu)
                {
                    var options = new RestClientOptions()
                    {
                        MaxTimeout = -1,
                    };
                    var client = new RestClient(options);
                    var request = new RestRequest(strSorgu, Method.Get);
                    request.AddHeader("x-api-key", strApiKey);
                    request.AddHeader("accept-language", "en");
                    RestResponse response = client.Execute(request);

                    if (response.Content != null)
                    {
                        OrdersBooks = JsonConvert.DeserializeObject<OrderBook>(response.Content.ToString());

                        blnDongu = true;
                    }
                    else
                    {
                        blnDongu = false;
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(strSembol + " Verileri Alınamadı.Detay: {Exception}", ex.Message.ToString());
                Console.WriteLine(strSembol + " Verileri Alınamadı.Detay:\n" + ex.Message.ToString());
            }
        }
        public static List<bool?> CrossoverDegerler(List<decimal?> Src, List<decimal?> ShorDegerler)
        {
            List<bool?> Croslar = new List<bool?>();
            Croslar.Add(false);
            int dHesapla = 0;
            dHesapla = Src.Count - ShorDegerler.Count;
            Src.RemoveRange(0, dHesapla);

            for (int i = 1; i < Src.Count; i++)
            {
                if ((Src[i].Value > ShorDegerler[i].Value) && (Src[i - 1].Value < ShorDegerler[i].Value))
                {
                    Croslar.Add(true);
                }
                else
                {
                    Croslar.Add(false);
                }
            }

            return Croslar;
        }
        public static List<bool?> CrossunderDegerler(List<decimal?> Src, List<decimal?> LongDegerler)
        {
            List<bool?> Croslar = new List<bool?>();
            Croslar.Add(false);
            int dHesapla = 0;
            dHesapla = Src.Count - LongDegerler.Count;
            Src.RemoveRange(0, dHesapla);

            for (int i = 1; i < Src.Count; i++)
            {
                if ((Src[i].Value < LongDegerler[i].Value) && (Src[i - 1].Value > LongDegerler[i].Value))
                {
                    Croslar.Add(true);
                }
                else
                {
                    Croslar.Add(false);
                }
            }

            return Croslar;
        }
        public static void ApiInformationLoading()
        {
            string strAppPath = System.IO.Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            string strPath = strAppPath + "\\parameters.json";
            var json = System.IO.File.ReadAllText(strPath);
            var jObject = JObject.Parse(json);

            ApiKey = jObject["ApiKey"].ToString();
            ApiSecret = jObject["Secret"].ToString();
            Symbol = jObject["Symbol"].ToString();
            Interval = Convert.ToInt32(jObject["TimeFrame"].ToString());
            IslemTipi = jObject["IslemTipi"].ToString();
            Quantity = jObject["Quantity"].ToString();
            dEma1 = Convert.ToInt32(jObject["Ema1"].ToString());
            dEma2 = Convert.ToInt32(jObject["Ema2"].ToString());
        }
    }
    public class Candlestick
    {
        public long OpenTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }
    public class Root
    {
        public int code { get; set; }
        public List<string> data { get; set; }
        public string msg { get; set; }
    }
    public class IslemSonucu
    {
        public int code { get; set; }
        public object data { get; set; }
        public string msg { get; set; }
    }
    public class SubmitOrder
    {
        public int orderPriceType { get; set; }
        public string symbol { get; set; }
        public string tradeType { get; set; }
        public string orderNumber { get; set; }
        public string price { get; set; }
        public string total { get; set; }
    }

    public class OrderData
    {
        public List<List<double>> asks { get; set; }
        public List<List<double>> bids { get; set; }
        public string ticker_id { get; set; }
        public string timestamp { get; set; }
    }
    public class OrderBook
    {
        public int code { get; set; }
        public OrderData data { get; set; }
        public string msg { get; set; }
    }
}