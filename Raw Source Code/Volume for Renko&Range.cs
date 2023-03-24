/*
--------------------------------------------------------------------------------------------------------------------------------
                      Volume for Renko/Range
VolumeRenkoRange applies tick volume logic on non-time based charts.

It's possible because we have the Open/Close Time of Bar, so:
Volume logic = Number of price updates (ticks) that come during the formation of a bar (between of OpenTime and CloseTime).

Uses Ticks Data to make the calculation of volume, just like Candles.

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
    [Indicator(AutoRescale = true, AccessRights = AccessRights.None)]
    public class VolumeRenkoRange : Indicator
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
        [Parameter("Load From:", DefaultValue = LoadFromData.Today, Group = "==== Volume for Renko&Range ====")]
        public LoadFromData LoadFromInput { get; set; }
        
        [Parameter("Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== Volume for Renko&Range ====")]
        public string StringDate { get; set; }
        
        [Parameter("Nº Bars to Show:", DefaultValue = -1, MinValue = -1, Group = "==== Volume for Renko&Range ====")]
        public int Lookback { get; set; }
                
        // ==============
        [Output("Main", LineColor="aqua", PlotType = PlotType.Histogram, Thickness = 5)]
        public IndicatorDataSeries Result { get; set; }
        
        [Output("Bullish Volume", LineColor = "green", PlotType = PlotType.Histogram, Thickness = 5)]
        public IndicatorDataSeries BullVolume { get; set; }

        [Output("Bearish Volume", LineColor = "red", PlotType = PlotType.Histogram, Thickness = 5)]
        public IndicatorDataSeries BearVolume { get; set; }
        
        private Bars _TicksOHLC;
        private DateTime FromDateTime;
        private int CurrentVol = 0;
        private bool TextsRemoved = false;
        
        private VerticalAlignment V_Align = VerticalAlignment.Center;
        private HorizontalAlignment H_Align = HorizontalAlignment.Center;
        // ==============
        
        protected override void Initialize()
        {
        
            // First Ticks Data and BarOpened/ObjectsRemoved events 
            _TicksOHLC = MarketData.GetBars(TimeFrame.Tick);
            Bars.BarOpened += ResetCurrentVol;
            Chart.ObjectsRemoved += SetTextsRemoved;
            
            if (LoadFromInput == LoadFromData.Custom)
            {
                // ==== Get datetime to load from: dd/mm/yyyy ====               
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out FromDateTime))
                {
                    if (FromDateTime > Server.Time.Date) {   
                        // for Log
                        FromDateTime = Server.Time.Date;
                        Print($"Invalid DateTime '{StringDate}'. Using '{FromDateTime}'");
                        // for Screen
                        IndicatorArea.DrawStaticText("txt_2", $"Invalid DateTime '{StringDate}'. \n Using '{FromDateTime.Day}/{FromDateTime.Month}/{FromDateTime.Year}'", VerticalAlignment.Top, HorizontalAlignment.Right, Color.Red);
                    }
                }
                else {
                    // for Log
                    FromDateTime = Server.Time.Date;
                    Print($"Invalid DateTime '{StringDate}'. Using '{FromDateTime}'");
                    // for Screen
                    IndicatorArea.DrawStaticText("txt_2", $"Invalid DateTime '{StringDate}'. \n Using '{FromDateTime.Day}/{FromDateTime.Month}/{FromDateTime.Year}'", VerticalAlignment.Top, HorizontalAlignment.Right, Color.Red);
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
            // ==== Removing Messages ====
            if (!IsLastBar)
            {
              if (!TextsRemoved)
                    IndicatorArea.RemoveAllObjects();
            }
            else
                CurrentVol += 1;
            
            if (index < (Bars.OpenTimes.GetIndexByTime(Server.Time)-Lookback) && (Lookback != -1 && Lookback > 0))
                return;
            // ==============
            var CurrentTimeBar = Bars.OpenTimes[index];
            var PreviousTimeBar = Bars.OpenTimes[index - 1];
            bool isBullish = (Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1]);
            bool currentIsBullish = (Bars.ClosePrices[index] > Bars.OpenPrices[index]);
            // ==============
            
            Result[index - 1] = GetVolume(PreviousTimeBar, CurrentTimeBar);
            Result[index] = CurrentVol;
            
            // ==== HISTORICAL BULL/BEAR VOLUME ====
            if (isBullish)
                BullVolume[index - 1] = Result[index - 1];
            else
                BearVolume[index - 1 ] = Result[index - 1];
            
            // ==== CURRENT BULL/BEAR VOLUME ====
            if (currentIsBullish)
            {
                BullVolume[index] = Result[index];
                BearVolume[index] = 0;
            }
            else
            {
                BearVolume[index] = Result[index];
                BullVolume[index] = 0;
            }
        }
        
        // ========= Functions Area ==========
        private int GetVolume(DateTime startTime, DateTime endTime)
        {
            int volume = 0;
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
                
                volume += 1;
            }

            return volume;
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
        private void ResetCurrentVol(BarOpenedEventArgs obj)
        {
            CurrentVol = 0;
        }
        // ========= ========== ==========
        private void DrawOnScreen(string Msg)
        {
            IndicatorArea.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
        }
        // ========= ========== ==========
        private void SetTextsRemoved(ChartObjectsRemovedEventArgs obj)
        {
            TextsRemoved = true;
        }
    }
}