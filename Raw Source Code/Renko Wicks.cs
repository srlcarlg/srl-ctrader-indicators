/*
--------------------------------------------------------------------------------------------------------------------------------
                      Renko Wicks
Renko Wicks applies tick volume logic on non-time based charts.

Uses Ticks Data like my other indicator 'Volume for Renko/Range', with a similar logic but focused on the renko price, so:
BullWick = Minimum price existing during the formation of a bar (between of OpenTime and CloseTime)
BearWick = Maximum price existing during the formation of a bar (between OpenTime and CloseTime)

This uses Ticks Data to correctly calculate Wicks, just like Candles or others Time-Based Charts.

For Better Performance, Recompile it on cTrader with .NET 6.0 instead .NET 4.x.

AUTHOR: srlcarlg
----------------------------------------------------------------------------------------------------------------------------
*/
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class RenkoWicks : Indicator
    {
        public enum LoadFromData
        {
            Today,
            Yesterday,
            One_Week,
            Two_Week,
            Monthly,
            Custom
        }
        [Parameter("Load From:", DefaultValue = LoadFromData.Today, Group = "==== Renko Wicks ====")]
        public LoadFromData LoadFromInput { get; set; }
        
        [Parameter("Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== Renko Wicks ====")]
        public string StringDate { get; set; }
        
        [Parameter("Nº Bars to Show:", DefaultValue = -1, MinValue = -1, Group = "==== Renko Wicks ====")]
        public int Lookback { get; set; }
        
        [Parameter("Wicks Thickness:", DefaultValue = 1, MaxValue = 5, Group = "==== Renko Wicks ====")]
        public int Thickness { get; set; }
        
        // ==============
        private Bars _TicksOHLC;
        private DateTime FromDateTime;
        private VerticalAlignment V_Align = VerticalAlignment.Top;
        private HorizontalAlignment H_Align = HorizontalAlignment.Center;
        
        private IndicatorDataSeries AllWicks;
        
        private bool WrongTF = false;
        private Color BullColor;
        private Color BearColor;
        private List<double> currentPriceWicks = new List<double>();
        private List<ChartTrendLine> TrendLinesWicks = new List<ChartTrendLine>();
        // ==============
        
        protected override void Initialize()
        {
            // ===== Verify Timeframe =====
            string currentTimeframe = Chart.TimeFrame.ToString();
            if (!currentTimeframe.Contains("Renko"))
            {
                DrawOnScreen($"Renko Wicks \n WORKS ONLY IN RENKO CHART!");
                WrongTF = true;
                return;
            }
        
            AllWicks = CreateDataSeries();        
        
            // First Ticks Data and BarOpened/ObjectsRemoved events
            _TicksOHLC = MarketData.GetBars(TimeFrame.Tick);
            Bars.BarOpened += ResetCurrentWick;
            Chart.ColorsChanged += SetTrendLinesColor;
            
            BullColor = Chart.ColorSettings.BullOutlineColor;
            BearColor = Chart.ColorSettings.BearOutlineColor;
            
            if (LoadFromInput == LoadFromData.Custom)
            {
                // ==== Get datetime to load from: dd/mm/yyyy ====                
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None,  out FromDateTime))
                {
                    if (FromDateTime > Server.Time.Date)  {
                        // for Log
                        FromDateTime = Server.Time.Date;
                        Print($"Invalid DateTime '{StringDate}'. Using '{FromDateTime}'");
                    }
                }
                else {
                    // for Log
                    FromDateTime = Server.Time.Date;
                    Print($"Invalid DateTime '{StringDate}'. Using '{FromDateTime}'");
                }
                
            }
            else
            {
                DateTime LastBarTime = Bars.LastBar.OpenTime.Date;
                if (LoadFromInput == LoadFromData.Today)
                    FromDateTime = LastBarTime.Date;
                else if (LoadFromInput == LoadFromData.Yesterday)
                    FromDateTime = LastBarTime.AddDays(-1);
                else if (LoadFromInput == LoadFromData.One_Week)
                    FromDateTime = LastBarTime.AddDays(-5);
                else if (LoadFromInput == LoadFromData.Two_Week)
                    FromDateTime = LastBarTime.AddDays(-10);
                else if (LoadFromInput == LoadFromData.Monthly)
                    FromDateTime = LastBarTime.AddMonths(-1);
            }
           
            // ==== Check if existing ticks data on the chart really needs more data ====
            var FirstTickTime = _TicksOHLC.OpenTimes.FirstOrDefault();  
            if (FirstTickTime >= FromDateTime) {   
                LoadMoreTicks(FromDateTime);
                DrawOnScreen("Data Collection Finished \n Calculating...");
            }
            else {
                Print($"Using existing tick data from '{FirstTickTime}'");  
                DrawOnScreen($"Using existing tick data from '{FirstTickTime}' \n Calculating...");
            }
        }
        
        public override void Calculate(int index)
        { 
            if (WrongTF)
                return;
                
            // ==== Removing Messages ====
            if (!IsLastBar)
                DrawOnScreen("");
            else
                currentPriceWicks.Add(Bars.ClosePrices[index]);
            
            if (index < (Bars.OpenTimes.GetIndexByTime(Server.Time)-Lookback) && (Lookback != -1 && Lookback > 0))
                return;
                
            // ==============
            var CurrentTimeBar = Bars.OpenTimes[index];
            var PreviousTimeBar = Bars.OpenTimes[index - 1];
            var PrevOpen = Bars.OpenPrices[index - 1];
            
            bool isBullish = (Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1]);
            bool currentIsBullish = (Bars.ClosePrices[index] > Bars.OpenPrices[index]);
            bool Gap = Bars.OpenTimes[index - 1] == Bars.OpenTimes[index - 2];
            // ==============
            
            AllWicks[index - 1] = GetWicks(PreviousTimeBar, CurrentTimeBar, isBullish);
            
            // ==== HISTORICAL BULL WICK ====           
            if (isBullish)
            {
                if (AllWicks[index - 1] < PrevOpen && !Gap)
                {
                    var trendBull = Chart.DrawTrendLine("BullWick_" + (index - 1), PreviousTimeBar, AllWicks[index - 1], PreviousTimeBar, Bars.OpenPrices[index - 1], Chart.ColorSettings.BullOutlineColor);
                    trendBull.Thickness = Thickness;
                    trendBull.Comment = "BullWick";
                    TrendLinesWicks.Add(trendBull);
                }
            }
            // ==== HISTORICAL BEAR WICK ====
            else
            {
                if (AllWicks[index - 1] > PrevOpen && !Gap)
                {
                    var trendBear = Chart.DrawTrendLine("BearWick_" + (index - 1) , PreviousTimeBar, AllWicks[index - 1], PreviousTimeBar, Bars.OpenPrices[index - 1], Chart.ColorSettings.BearOutlineColor);
                    trendBear.Thickness = Thickness;
                    trendBear.Comment = "BearWick";
                    TrendLinesWicks.Add(trendBear);
                }
            }
            
            // ==== CURRENT BULL WICK ====
            if (currentIsBullish)
            {
                if (currentPriceWicks.Count == 0)
                    return;
                    
                AllWicks[index] = currentPriceWicks.Min();
                var currentTrendBull = Chart.DrawTrendLine("currentPriceLines", CurrentTimeBar, AllWicks[index], CurrentTimeBar, currentPriceWicks.Max(), Chart.ColorSettings.BullOutlineColor);
                currentTrendBull.Thickness = Thickness;
            }
            // ==== CURRENT BEAR WICK ====
            else
            {
                if (currentPriceWicks.Count == 0)
                    return;
                    
                AllWicks[index] = currentPriceWicks.Max();
                var currentTrendBear = Chart.DrawTrendLine("currentPriceLines", CurrentTimeBar, AllWicks[index], CurrentTimeBar, currentPriceWicks.Min(), Chart.ColorSettings.BearOutlineColor);
                currentTrendBear.Thickness = Thickness;
            }
        }
        
        // ========= Functions Area ==========
        private double GetWicks(DateTime startTime, DateTime endTime, bool isBullish)
        {
            double min = Int32.MaxValue;
            double max = 0;
                        
            for (int tickIndex = 0; tickIndex < _TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = _TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime)
                {
                    if (tickBar.OpenTime > endTime)
                        break;
                    else
                        continue;
                }

                if (isBullish && tickBar.Close < min)
                    min = tickBar.Close;       
                else if (!isBullish && tickBar.Close > max)
                    max = tickBar.Close;
            }
            
            return isBullish ? min : max;
        }
        // ========= ========== ==========
        private void LoadMoreTicks(DateTime FromDateTime)
        {
            bool msg = false;
            
            while (_TicksOHLC.OpenTimes.FirstOrDefault() > FromDateTime)
            {
                if (!msg) {
                    Print($"Loading from '{_TicksOHLC.OpenTimes.Reverse().Last()}' to '{FromDateTime}'...");
                    msg = true;
                }

                int loadedCount = _TicksOHLC.LoadMoreHistory();
                Print("Loaded {0} Ticks, Current Tick Date: {1}", loadedCount, _TicksOHLC.OpenTimes.FirstOrDefault());
                if (loadedCount == 0)
                    break;
            }
            Print("Data Collection Finished, First Tick from: {0}", _TicksOHLC.OpenTimes.FirstOrDefault());
        }
        // ========= ========== ==========
        private void ResetCurrentWick(BarOpenedEventArgs obj)
        {
            currentPriceWicks.Clear();
            Chart.RemoveObject("currentPriceLines");
        }
        // ========= ========== ==========
        private void DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
        }
        // ========= ========== ==========
        private void SetTrendLinesColor(ChartColorEventArgs obj)
        {
            if (obj.Chart.ColorSettings.BullOutlineColor != BullColor) 
            {
                for (int wickIndex = 0; wickIndex < TrendLinesWicks.Count; wickIndex++)
                {
                    if (TrendLinesWicks[wickIndex].Comment == "BullWick")
                        TrendLinesWicks[wickIndex].Color = obj.Chart.ColorSettings.BullOutlineColor;
                }
                BullColor = obj.Chart.ColorSettings.BullOutlineColor;
            }
            
            if (obj.Chart.ColorSettings.BearOutlineColor != BearColor)
            {
                for (int wickIndex = 0; wickIndex < TrendLinesWicks.Count; wickIndex++)
                {
                    if (TrendLinesWicks[wickIndex].Comment == "BearWick")
                        TrendLinesWicks[wickIndex].Color = obj.Chart.ColorSettings.BearOutlineColor;
                }
                BearColor = obj.Chart.ColorSettings.BearOutlineColor;
            }
        }
    }
}