/*
--------------------------------------------------------------------------------------------------------------------------------
                        Order Flow Ticks
Order Flow Ticks brings the main concepts of Order Flow (aka Footprint) for cTrader.
Using ideas from my previous creations (Volume for Renko/Range, TPO Profile) made this possible.

Comparing with Footprint, we have the features:
* Normal Mode = Volume Profile of Bar
* Buy vs Sell Divided Mode = Bid/Ask Footprint
* Buy vs Sell Profile Mode = Same but Profile
* Delta Divided Mode = Delta Footprint
* Delta Profile Mode = Same but Profile

All parameters are self-explanatory.
Also works on Ticks/Renko/Range Charts

For Better Performance, Recompile it on cTrader with .NET 6.0 instead .NET 4.x.

AUTHOR: srlcarlg 
 
== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class OrderFlowTicks : Indicator
    {
        public enum LoadFromData
        {
            Existing_on_Chart,
            Today,
            Yesterday,
            One_Week,
            Two_Week,
            Monthly,
            Custom
        }

        [Parameter("Load From:", DefaultValue = LoadFromData.Existing_on_Chart, Group = "==== Tick Volume Settings ====")]
        public LoadFromData LoadFromInput { get; set; }

        [Parameter("Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== Tick Volume Settings ====")]
        public string StringDate { get; set; }
        
        
        [Parameter("Nº Bars to Show:", DefaultValue = -1, MinValue = -1, Group = "==== Order Flow Ticks ====")]
        public int Lookback { get; set; }
        
        public enum ModeVOLData
        {
            Normal,
            Buy_Sell,
            Delta,
        }

        [Parameter("VOL Mode:", DefaultValue = ModeVOLData.Delta, Group = "==== Order Flow Ticks ====")]
        public ModeVOLData ModeVOLInput { get; set; }
        public enum DeltaVisualData
        {
            Divided,
            Profile,
        }
        [Parameter("Buy&Sell/Delta Mode:", DefaultValue = DeltaVisualData.Profile, Group = "==== Order Flow Ticks ====")]
        public DeltaVisualData DeltaVisualInput { get; set; } 
                
        public enum ConfigRowData
        {
            Predefined,
            Custom,
        }
        [Parameter("Row Config:", DefaultValue = ConfigRowData.Predefined, Group = "==== Order Flow Ticks ====")]
        public ConfigRowData ConfigRowInput { get; set; }
        
        [Parameter("Custom Row Height:", DefaultValue = 0.2, MinValue = 0.2, Group = "==== Order Flow Ticks ====")]
        public double CustomHeight { get; set; }
        
        
        [Parameter("Fill Histogram?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool FillHist { get; set; }
        
        [Parameter("Show Numbers?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool ShowNumbers { get; set; }
        
        [Parameter("Show Results?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool ShowResults { get; set; }
        
        [Parameter("[Renko] Show Wicks?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool ShowWicks { get; set; }
        
        
        public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }
        [Parameter("Operator Buy/Sell (only)", DefaultValue = OperatorBuySell_Data.Sum, Group = "==== Results/Numbers ====")]
        public OperatorBuySell_Data OperatorBuySell_Input { get; set; } 
        
        public enum ResultsType_Data
        {
            Percentage,
            Value,
            Both
        }
        [Parameter("Results Type:", DefaultValue = ResultsType_Data.Percentage, Group = "==== Results/Numbers ====")]
        public ResultsType_Data ResultsType_Input { get; set; } 
        
        public enum ResultsColoringData
        {
            bySide,
            Fixed,
        }
        [Parameter("Results Coloring:", DefaultValue = ResultsColoringData.bySide, Group = "==== Results/Numbers ====")]
        public ResultsColoringData ResultsColoringInput { get; set; } 
        
        [Parameter("Fixed Color Rt/Nb:", DefaultValue = Colors.White, Group = "==== Results/Numbers ====")]
        public Colors RawColorRtNb { get; set; }


        [Parameter("Enable Filter?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool EnableFilter { get; set; }
        
        [Parameter("MA Filter Type:", DefaultValue = MovingAverageType.Exponential, Group = "==== Large Result Filter ====")]
        public MovingAverageType MAtype { get; set; }

        [Parameter("MA Filter Period:", DefaultValue = 5, MinValue = 1, Group = "==== Large Result Filter ====")]
        public int MAperiod { get; set; }
        
        [Parameter("Large R. Ratio", DefaultValue = 1.5, MinValue = 1, MaxValue = 2, Group = "==== Large Result Filter ====")]
        public double Filter_Ratio { get; set; }
        
        [Parameter("Large R. Color", DefaultValue = Colors.Gold, Group = "==== Large Result Filter ====")]
        public Colors RawColorLargeR { get; set; }
        
        [Parameter("Coloring Bar?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool ColoringBars { get; set; }
        
        [Parameter("[Delta] Coloring Cumulative?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool ColoringCD { get; set; }
        
        
        [Parameter("Color Volume:", DefaultValue = Colors.SkyBlue, Group = "==== Volume ====")]
        public Colors RawColorHist { get; set; }

        [Parameter("Color Largest Volume:", DefaultValue = Colors.Gold, Group = "==== Volume ====")]
        public Colors RawColorLVOL { get; set; }
                     
                     
        [Parameter("Color Buy:", DefaultValue = Colors.DeepSkyBlue, Group = "==== Buy ====")]
        public Colors RawColorBuy { get; set; }
        
        [Parameter("Color Largest Buy:", DefaultValue = Colors.Gold, Group = "==== Buy ====")]
        public Colors RawColorBuy_LVOL { get; set; }
        
        
        [Parameter("Color Sell:", DefaultValue = Colors.Crimson, Group = "==== Sell ====")]
        public Colors RawColorSell { get; set; }
        
        [Parameter("Color Largest Sell:", DefaultValue = Colors.Goldenrod, Group = "==== Sell ====")]
        public Colors RawColorSell_LVOL { get; set; }
                
        
        [Parameter("Opacity Histogram:", DefaultValue = 70, MinValue = 5, MaxValue = 100, Group = "==== Opacity ====")]
        public int OpacityHist { get; set; }
        
        [Parameter("Opacity Rt/Nb", DefaultValue = 80, MinValue = 5, MaxValue = 100, Group = "==== Opacity ====")]
        public int OpacityNumbers { get; set; }
        

        [Parameter("Font Size Numbers:", DefaultValue = 8, MinValue = 1, MaxValue = 80, Group = "==== Font Size ====")]
        public int FontSizeNumbers { get; set; } 
        
        [Parameter("Font Size Results:", DefaultValue = 10, MinValue = 1, MaxValue = 80, Group = "==== Font Size ====")]
        public int FontSizeResults { get; set; } 
        
        
        public enum ConfigInfoC
        {
            Top_Right,
            Top_Left,
            Bottom_Right,
            Bottom_Left,
        }
        [Parameter("Info Corner Position:", DefaultValue = ConfigInfoC.Bottom_Left, Group = "==== Others ====")]
        public ConfigInfoC ConfigInfoC_Input { get; set; }
        
        [Parameter("Info Corner Color:", DefaultValue = Colors.Snow, Group = "==== Others ====")]
        public Colors RawColorInfoC { get; set; }
                
        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }


        private VerticalAlignment V_Align = VerticalAlignment.Top;
        private HorizontalAlignment H_Align = HorizontalAlignment.Center;
        private bool Wrong = false;
        private DateTime FromDateTime;
        
        private List<double> allSegmentsPrices = new List<double>();        
        private IDictionary<double, int> allVolumesRank = new Dictionary<double, int>();
        private IDictionary<double, int> allVolumesR_Up = new Dictionary<double, int>();
        private IDictionary<double, int> allVolumesR_Down = new Dictionary<double, int>();
        private IDictionary<double, int> allDeltaRank = new Dictionary<double, int>();
        private IDictionary<double, int> CumulDeltaRank = new Dictionary<double, int>();
        
        private IDictionary<int, ChartRectangle> currentBar_HistsD = new Dictionary<int, ChartRectangle>();
        private IDictionary<int, ChartText> currentBar_NumbersD = new Dictionary<int, ChartText>();
        
        private double HeightPips = 4;
        private double rowHeight = 0;
        
        private bool isLive = false;
        
        private Color VolumeColor;
        private Color BuyColor;
        private Color SellColor;
        
        private Color Volume_LVOLColor;
        private Color Buy_LVOLColor;
        private Color Sell_LVOLColor;
        
        private Color RtNb_FixedColor;
        
        private int cleanedIndex;  
        
        private Bars _TicksOHLC;
        private bool NewBar = false;
        private bool finishedCalc = false;
        private bool lockCalc = false;
        
        private IndicatorDataSeries CumulDeltaSeries, DynamicSeries;
        
        private MovingAverage MACumulDelta, MADynamic;
        
        protected override void Initialize()
        {
            // ========== Predefined Config ==========
            if (ConfigRowInput == ConfigRowData.Predefined && (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Monthly))
            {
                if (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Minute4)
                    SetHeightPips(0.3, 5);
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                    SetHeightPips(1, 10);
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame < TimeFrame.Minute30)
                        SetHeightPips(2, 15);
                    
                    if (Chart.TimeFrame >= TimeFrame.Minute30 && Chart.TimeFrame <= TimeFrame.Hour)
                        SetHeightPips(4, 30);
                    else if (Chart.TimeFrame >= TimeFrame.Hour4 && Chart.TimeFrame <= TimeFrame.Hour8)
                        SetHeightPips(6, 50);
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Day3)
                    SetHeightPips(15, 180);
                else if (Chart.TimeFrame >= TimeFrame.Weekly && Chart.TimeFrame <= TimeFrame.Monthly)
                    SetHeightPips(50, 380);
            }
            else
            {   if (ConfigRowInput == ConfigRowData.Predefined)
                {
                    string Msg = "'Predefined Config' is designed only for Standard Timeframe (Minutes, Hours, Days, Weekly, Monthly)\n\n use 'Custom Config' to others Chart Timeframes (Renko/Range/Ticks).";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                HeightPips = CustomHeight;
            }
            
            void SetHeightPips(double digits5, double digits2)
            {
                if (Symbol.Digits == 5)
                    HeightPips = digits5;
                else if (Symbol.Digits == 2)
                {
                    HeightPips = digits2;
                    if (Symbol.PipSize == 0.1)
                        HeightPips /= 2;
                }
            }
            if (EnableFilter) 
            {
                DynamicSeries = CreateDataSeries();
                CumulDeltaSeries = CreateDataSeries();
                
                MADynamic = Indicators.MovingAverage(DynamicSeries, MAperiod, MAtype);
                MACumulDelta = Indicators.MovingAverage(CumulDeltaSeries, MAperiod, MAtype);
            }
            // First Ticks Data
            _TicksOHLC = MarketData.GetBars(TimeFrame.Tick);
            
            string currentTimeframe = Chart.TimeFrame.ToString();      
            if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick"))
                Bars.BarOpened += SetNewBar;
            
            if (LoadFromInput != LoadFromData.Existing_on_Chart)
                VolumeInitialize();
                
            // Ex: 4 pips to Volume calculation(rowHeight)
            rowHeight = (Symbol.PipSize) * HeightPips;
            
            // ===== Colors with Opacity =====
            int histOpacity = (int)(2.55 * OpacityHist);
            Color rawHist = Color.FromName(RawColorHist.ToString());
            VolumeColor = Color.FromArgb(histOpacity, rawHist.R, rawHist.G, rawHist.B);
            
            Color rawBuy = Color.FromName(RawColorBuy.ToString());
            BuyColor = Color.FromArgb(histOpacity, rawBuy.R, rawBuy.G, rawBuy.B);
            
            Color rawSell = Color.FromName(RawColorSell.ToString());
            SellColor = Color.FromArgb(histOpacity, rawSell.R, rawSell.G, rawSell.B);

            // Largest Volume
            Color rawHistLVOL = Color.FromName(RawColorLVOL.ToString());
            Volume_LVOLColor = Color.FromArgb(histOpacity, rawHistLVOL.R, rawHistLVOL.G, rawHistLVOL.B);
            
            Color rawBuyLVOL = Color.FromName(RawColorBuy_LVOL.ToString());
            Buy_LVOLColor = Color.FromArgb(histOpacity, rawBuyLVOL.R, rawBuyLVOL.G, rawBuyLVOL.B);
            
            Color rawSellLVOL = Color.FromName(RawColorSell_LVOL.ToString());
            Sell_LVOLColor = Color.FromArgb(histOpacity, rawSellLVOL.R, rawSellLVOL.G, rawSellLVOL.B);
            
            // Fixed Rt/Nb Color
            int NumbersOpacity = (int)(2.55 * OpacityNumbers);
            Color rawFixed = Color.FromName(RawColorRtNb.ToString());
            RtNb_FixedColor = Color.FromArgb(NumbersOpacity, rawFixed.R, rawFixed.G, rawFixed.B);
            
            // === Info Corner ===
            Color rawColor = Color.FromName(RawColorInfoC.ToString());
            Color InfoColor = Color.FromArgb((int)(2.55 * 70), rawColor.R, rawColor.G, rawColor.B);
            string strMode = ConfigRowInput == ConfigRowData.Predefined ? "Predefined" : "Custom";
            string strVisual = (ModeVOLInput == ModeVOLData.Buy_Sell || ModeVOLInput == ModeVOLData.Delta) ? $"{DeltaVisualInput}" : "";
            string VolInfo = $"{strVisual} \n" + 
                             $"VOL {ModeVOLInput} \n" + 
                             $"{strMode} Row \n" +
                             $"Row Height: {HeightPips} pip(s) \n";
            
            VerticalAlignment v_align = VerticalAlignment.Bottom;
            HorizontalAlignment h_align = HorizontalAlignment.Left;
            if (ConfigInfoC_Input == ConfigInfoC.Bottom_Right)
                h_align = HorizontalAlignment.Right;
            else if (ConfigInfoC_Input == ConfigInfoC.Top_Left)
                v_align = VerticalAlignment.Top;
            else if (ConfigInfoC_Input == ConfigInfoC.Top_Right)
            {
                v_align = VerticalAlignment.Top;
                h_align = HorizontalAlignment.Right;
            }
            Chart.DrawStaticText("Vol Info", VolInfo, v_align, h_align, InfoColor);
            
            DrawOnScreen("Calculating...");
            Second_DrawOnScreen("Taking too long? \nSet Nº Bars to Show");
        }

        public override void Calculate(int index)
        {
            if (Wrong)
                return;
            
            // ==== Removing Messages ====
            if (!IsLastBar) {
                DrawOnScreen(""); 
                Second_DrawOnScreen("");
            }

            if (index < (Bars.OpenTimes.GetIndexByTime(Server.Time)-Lookback) && (Lookback != -1 && Lookback > 0))
                return;
                
            int indexStart = index;
                                    
            // === Clean Dicts/others ===
            if (index == indexStart && index != cleanedIndex || (index-1) == indexStart && (index-1) != cleanedIndex) 
            {
                allSegmentsPrices.Clear();
                allVolumesRank.Clear();
                allVolumesR_Up.Clear();
                allVolumesR_Down.Clear();
                allDeltaRank.Clear();
                cleanedIndex = index == indexStart ? index : (index-1); 
            }
            
            // Historical data 
            if (!IsLastBar)
            {
                if (!isLive)
                    VP(index, indexStart); 
                else
                    NewBar=true;
            }
            else
            {                                    
               isLive = true;
               
               if (NewBar)
               {
                    string currentTimeframe = Chart.TimeFrame.ToString();      
                    if ((currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range")) && ModeVOLInput == ModeVOLData.Normal && !finishedCalc)
                    {
                        finishedCalc=true; Repaint(index-1); lockCalc = true;
                        return;
                    }
                   if (ModeVOLInput == ModeVOLData.Delta)
                   {
                       foreach (int key in currentBar_HistsD.Keys) 
                       {
                            try {
                            Chart.RemoveObject(currentBar_HistsD[key].Name);
                            } catch {}
                        }
                        foreach (int key in currentBar_NumbersD.Keys) 
                        {
                            try {
                            Chart.RemoveObject(currentBar_NumbersD[key].Name);
                            } catch {};
                        }    
                        currentBar_HistsD.Clear();
                        currentBar_NumbersD.Clear();
                    }
                    Repaint(index-1);
                    NewBar = false;
                    if (ModeVOLInput == ModeVOLData.Delta)
                        currentBar_HistsD.Clear(); currentBar_NumbersD.Clear();
                    return;
               }
               
               // "Repaint" of Numbers/Histograms Delta because of unknown High/Low 
               if (ModeVOLInput == ModeVOLData.Delta)
               {
                   foreach (int key in currentBar_HistsD.Keys) 
                   {
                        try {
                        Chart.RemoveObject(currentBar_HistsD[key].Name);
                        } catch {}
                    }
                    foreach (int key in currentBar_NumbersD.Keys) 
                    {
                        try {
                        Chart.RemoveObject(currentBar_NumbersD[key].Name);
                        } catch {};
                    }    
                    currentBar_HistsD.Clear();
                    currentBar_NumbersD.Clear();
                }
                Repaint(index);
            }
            
            void Repaint(int ind)
            {
                allSegmentsPrices.Clear();
                allVolumesRank.Clear();
                allVolumesR_Up.Clear();
                allVolumesR_Down.Clear();
                allDeltaRank.Clear();
                VP(ind, ind); 
            }
        }
        
        private void VP(int index, int iStart)
        {

            // ======= Highest and Lowest =======
            double highest = Bars.HighPrices[index], lowest = Bars.LowPrices[index], open = Bars.OpenPrices[index];
            
            if (Chart.TimeFrame.ToString().Contains("Renko") && ShowWicks) 
            {
                var CurrentTimeBar = Bars.OpenTimes[index];
                var NextTimeBar = Bars.OpenTimes[index + 1];
                bool isBullish = (Bars.ClosePrices[index] > Bars.OpenPrices[index]);
                
                if (isBullish)
                    lowest = GetWicks(CurrentTimeBar, NextTimeBar, isBullish);
                else
                    highest = GetWicks(CurrentTimeBar, NextTimeBar, isBullish);
            }
            
            List<double> currentSegments = new List<double>();        
            double prev_segment = open;    
            while (prev_segment >= (lowest-rowHeight))
            {
                currentSegments.Add(prev_segment);
                prev_segment = Math.Abs(prev_segment - rowHeight);
            }
            prev_segment = open;    
            while (prev_segment <= (highest+rowHeight))
            {
                currentSegments.Add(prev_segment);
                prev_segment = Math.Abs(prev_segment + rowHeight);
            }
            allSegmentsPrices = currentSegments.OrderBy(x => x).ToList();
            
            // ======= Volume on Tick =======
            VolP_Tick(index);  
            
            // ======= Drawing =======
            if (allSegmentsPrices.Count == 0)
                return;

            double prev_segment_loop = 0;
            for (int i = 0; i < allSegmentsPrices.Count; i++)
            {
                if (prev_segment_loop == 0)
                    prev_segment_loop = allSegmentsPrices[i];
                    
                double priceKey = allSegmentsPrices[i];
                if (!allVolumesRank.ContainsKey(priceKey))
                    continue;

                int largestVOL = allVolumesRank.Values.Max();
                
                double priceLVOL = 0;
                for (int k = 0; k < allVolumesRank.Count; k++)
                {
                    if (allVolumesRank.ElementAt(k).Value == largestVOL)
                    {
                        priceLVOL = allVolumesRank.ElementAt(k).Key;
                        break;
                    }
                }
                                
                // =======  HISTOGRAMs + Texts  =======
                /*
                Indeed, the value of X-Axis is simply a rule of three, 
                where the maximum value of the respective side (One/Buy/Sell) will be the maxLength (in Milliseconds),
                from there the math adjusts the histograms.
                    
                    MaxValue    maxLength(ms)
                       x             ?(ms)
                
                The values 1.50 and 3 are the manually set values like the size of the Bar body in any timeframe (Candle, Ticks, Renko, Range)
                */
                
                double lowerSegment = prev_segment_loop;
                double upperSegment = allSegmentsPrices[i];
                
                string currentTimeframe = Chart.TimeFrame.ToString();   

                // All Volume 
                double maxLength = 0;
                if (!IsLastBar)
                    maxLength = Bars[iStart + 1].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                else
                {
                    maxLength = Bars[iStart].OpenTime.Subtract(Bars[iStart-1].OpenTime).TotalMilliseconds;
                    if ((currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range")) && ModeVOLInput == ModeVOLData.Normal && finishedCalc && !lockCalc)
                       maxLength = Bars[iStart + 1].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds; 
                }
                
                double proportion = allVolumesRank[priceKey] * (maxLength - (maxLength/1.50));
                double dynLength = proportion / largestVOL;
                
                // Bull / Up
                double proportion_Up = allVolumesR_Up[priceKey] * (maxLength - (maxLength/1.50));
                double dynLength_Up = proportion_Up / allVolumesR_Up.Values.Max();
                // Bear / Down
                double maxLength_Left = Bars[iStart].OpenTime.Subtract(Bars[iStart-1].OpenTime).TotalMilliseconds;
                double proportion_Down = allVolumesR_Down[priceKey] * (maxLength_Left - (maxLength_Left/1.50));
                double dynLength_Down = proportion_Down / allVolumesR_Down.Values.Max();
                // Delta
                double proportion_Delta = allDeltaRank[priceKey] * (maxLength - (maxLength/1.50));
                double dynLength_Delta = proportion_Delta / allDeltaRank.Values.Max();
                
                if (allDeltaRank[priceKey] < 0 && DeltaVisualInput == DeltaVisualData.Divided && ModeVOLInput == ModeVOLData.Delta)
                {
                    // Negative Delta
                    proportion_Delta = allDeltaRank[priceKey] * (maxLength_Left - (maxLength_Left/1.50));
                    dynLength_Delta = proportion_Delta / allDeltaRank.Values.Where(n => n < 0).Min();  
                }
                

                if (DeltaVisualInput == DeltaVisualData.Profile && ModeVOLInput == ModeVOLData.Buy_Sell)
                {
                    // Buy vs Sell = Pseudo Delta
                    int buy_Volume = allVolumesR_Up.Values.Max();
                    int sell_Volume = allVolumesR_Down.Values.Max();
                    int sideVolMax = buy_Volume > sell_Volume ? buy_Volume : sell_Volume;
                    
                    proportion_Up = allVolumesR_Up[priceKey] * (maxLength - (maxLength/1.20));
                    dynLength_Up = proportion_Up / sideVolMax;
                    proportion_Down = allVolumesR_Down[priceKey] * (maxLength - (maxLength/1.50));
                    dynLength_Down = proportion_Down / sideVolMax;
                    
                }
                else if (DeltaVisualInput == DeltaVisualData.Profile && ModeVOLInput == ModeVOLData.Delta)
                {   
                    int Positive_Delta = allDeltaRank.Values.Max();
                    IEnumerable<int> allNegative = allDeltaRank.Values.Where(n => n < 0);  
                    int Negative_Delta = 0;
                    try {Negative_Delta = Math.Abs(allNegative.Min());} catch {}
                    
                    int deltaMax = Positive_Delta > Negative_Delta ? Positive_Delta : Negative_Delta;

                    dynLength_Delta = proportion_Delta / deltaMax;
                }
                                
                                
                if (ModeVOLInput == ModeVOLData.Normal)
                {
                    Color dynColor = allVolumesRank[priceKey] != largestVOL ? VolumeColor : Volume_LVOLColor;
                    ChartRectangle volHist;
                    if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                        volHist = Chart.DrawRectangle($"{iStart}_{i}", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, dynColor);
                    else
                        volHist = Chart.DrawRectangle($"{iStart}_{i}", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength*2), upperSegment, dynColor);
                    
                    if (FillHist)
                        volHist.IsFilled = true;
                    
                    if (ShowNumbers)
                    {
                        ChartText C = Chart.DrawText($"{iStart}_{i}Center", $"{allVolumesRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtNb_FixedColor);
                        C.HorizontalAlignment = HorizontalAlignment.Center;
                        C.FontSize = FontSizeNumbers;
                    }
                    if (ShowResults)
                    {
                        ChartText Center;
                        Color dynResColor = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : VolumeColor;
                        Center = Chart.DrawText($"{iStart}SumCenter", $"\n{allVolumesRank.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynResColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        
                        if (EnableFilter) 
                        {
                            DynamicSeries[index] = allVolumesRank.Values.Sum();
                                
                            // =========== Dynamic Series Filter ===========
                            double DynamicFilter = DynamicSeries[index] / MADynamic.Result[index];
                            double DynamicLarge = DynamicFilter >= Filter_Ratio ? DynamicSeries[index] : 0;
                            
                            Color dynBarColor = DynamicLarge >= 2 ? Color.FromName(RawColorLargeR.ToString()) : dynResColor;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == Color.FromName(RawColorLargeR.ToString()))
                                Chart.SetBarFillColor(index, Color.FromName(RawColorLargeR.ToString()));
                        }   
                    }
                    
                }
                else if (ModeVOLInput == ModeVOLData.Buy_Sell)
                {
                    Color dynColorBuy = allVolumesR_Up[priceKey] != allVolumesR_Up.Values.Max() ? BuyColor : Buy_LVOLColor;
                    Color dynColorSell = allVolumesR_Down[priceKey] != allVolumesR_Down.Values.Max() ? SellColor : Sell_LVOLColor;
                    
                    ChartRectangle buyHist;
                    ChartRectangle sellHist;
                    if (DeltaVisualInput == DeltaVisualData.Divided)
                    {
                        buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Up), upperSegment, dynColorBuy);
                        sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Down), upperSegment, dynColorSell);
                    }
                    else
                    {
                        if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range")) {
                            sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Down), upperSegment, SellColor);
                            buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Up), upperSegment, BuyColor);
                        }
                        else {
                            sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength_Down*2), upperSegment, SellColor);
                            buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength_Up*2), upperSegment, BuyColor);
                        }
                    }
                    
                    if (FillHist)
                    {
                        buyHist.IsFilled = true;
                        sellHist.IsFilled = true;
                    }
                    
                    if (ShowNumbers)
                    {
                        ChartText L = Chart.DrawText($"{iStart}_{i}SellNumber", $"{allVolumesR_Down[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtNb_FixedColor);
                        ChartText R = Chart.DrawText($"{iStart}_{i}BuyNumber", $"{allVolumesR_Up[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtNb_FixedColor);
                        
                        if (DeltaVisualInput == DeltaVisualData.Divided) {
                            L.HorizontalAlignment = HorizontalAlignment.Left;
                            R.HorizontalAlignment = HorizontalAlignment.Right;
                        }
                        else {
                            L.HorizontalAlignment = HorizontalAlignment.Right;
                            R.HorizontalAlignment = HorizontalAlignment.Left;
                        }
                        L.FontSize = FontSizeNumbers;
                        R.FontSize = FontSizeNumbers;
                    }
                    
                    if (ShowResults)
                    {
                        Color dynColorLeft = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : SellColor;
                        Color dynColorRight = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : BuyColor;
                        
                        int volBuy = allVolumesR_Up.Values.Sum();
                        int volSell = allVolumesR_Down.Values.Sum();
                        
                        Color compare = volBuy > volSell ? BuyColor : volBuy < volSell ? SellColor : RtNb_FixedColor;
                        Color dynColorCenter = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : compare;
                                                                        
                        int percentBuy = (volBuy * 100) / (volBuy + volSell);
                        int percentSell = (volSell * 100) / (volBuy + volSell);
                        
                        var selected = ResultsType_Input;
                        string dynStrBuy = selected == ResultsType_Data.Percentage ? $"\n{percentBuy}%" : selected == ResultsType_Data.Value ? $"\n{volBuy}" : $"\n{percentBuy}%\n({volBuy})";
                        string dynStrSell = selected == ResultsType_Data.Percentage ? $"\n{percentSell}%" : selected == ResultsType_Data.Value ? $"\n{volSell}" : $"\n{percentSell}%\n({volSell})";
                        string dynSpaceSum = (selected == ResultsType_Data.Percentage || selected == ResultsType_Data.Value) ? $"\n\n" : $"\n\n\n";
                        
                        ChartText Left, Right, Center;
                        Left = Chart.DrawText($"{iStart}SellSum", $"{dynStrSell}", Bars.OpenTimes[iStart], lowest, dynColorLeft);
                        Right = Chart.DrawText($"{iStart}BuySum", $"{dynStrBuy}", Bars.OpenTimes[iStart], lowest, dynColorRight);
                        if (OperatorBuySell_Input == OperatorBuySell_Data.Sum)
                            Center = Chart.DrawText($"{iStart}SumCenter", $"{dynSpaceSum}{allVolumesR_Up.Values.Sum() + allVolumesR_Down.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynColorCenter);
                        else
                            Center = Chart.DrawText($"{iStart}SumCenter", $"{dynSpaceSum}{allVolumesR_Up.Values.Sum() - allVolumesR_Down.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynColorCenter);
                        
                        if (DeltaVisualInput == DeltaVisualData.Divided) {
                            Left.HorizontalAlignment = HorizontalAlignment.Left;
                            Right.HorizontalAlignment = HorizontalAlignment.Right;
                        }
                        else {
                            Left.HorizontalAlignment = HorizontalAlignment.Right;
                            Right.HorizontalAlignment = HorizontalAlignment.Left;
                        }
                         
                        Left.FontSize = FontSizeResults;
                        Right.FontSize = FontSizeResults;
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults;

                        if (EnableFilter) 
                        {
                            if (OperatorBuySell_Input == OperatorBuySell_Data.Sum)
                                DynamicSeries[index] = allVolumesR_Up.Values.Sum() + allVolumesR_Down.Values.Sum();
                            else
                                DynamicSeries[index] = allVolumesR_Up.Values.Sum() - allVolumesR_Down.Values.Sum();
                                
                            // =========== Dynamic Series Filter ===========
                            double DynamicFilter = DynamicSeries[index] / MADynamic.Result[index];
                            double DynamicLarge = DynamicFilter >= Filter_Ratio ? DynamicSeries[index] : 0;
                            
                            Color dynBarColor = DynamicLarge >= 2 ? Color.FromName(RawColorLargeR.ToString()) : dynColorCenter;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == Color.FromName(RawColorLargeR.ToString()))
                                Chart.SetBarFillColor(index, Color.FromName(RawColorLargeR.ToString()));
                        }   
                    }
                        
                }
                else
                {
                    IEnumerable<int> allNegative = allDeltaRank.Values.Where(n => n < 0);  
                    int Negative_Delta = 0;
                    try {Negative_Delta = allNegative.Min();} catch {}
                        
                    Color dynColorBuy = allDeltaRank[priceKey] != allDeltaRank.Values.Max() ? BuyColor : Buy_LVOLColor;
                    Color dynColorSell = allDeltaRank[priceKey] != Negative_Delta ? SellColor : Sell_LVOLColor;

                    ChartRectangle deltaHist;
                    if (DeltaVisualInput == DeltaVisualData.Divided)
                    {
                        try {
                            if (allDeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}BuyDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, dynColorBuy);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}SellDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Delta), upperSegment, dynColorSell);
                        } catch {
                           if (allDeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}BuyDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, dynColorBuy);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}SellDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, dynColorSell);
                        }
                    }
                    else
                    {
                        try {
                        if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range")) 
                        {
                            if (allDeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, BuyColor);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Delta), upperSegment, SellColor);
                        }
                        else
                        {
                            if (allDeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength_Delta*2), upperSegment, BuyColor);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(-dynLength_Delta*2), upperSegment, SellColor);
                        }
                        } catch {
                            deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)), upperSegment, RtNb_FixedColor);
                        }
                        
                    }
                    if (FillHist)
                        deltaHist.IsFilled = true;
                        
                    if (IsLastBar)
                    {
                        if (!currentBar_HistsD.ContainsKey(i))
                            currentBar_HistsD.Add(i, deltaHist);
                        else
                            currentBar_HistsD[i] = deltaHist;
                    }
                    
                    if (ShowNumbers)
                    {
                        ChartText Numbers;
                        if (allDeltaRank[priceKey] > 0)
                        {
                            Numbers = Chart.DrawText($"{iStart}_{i}BuyNumber", $"{allDeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtNb_FixedColor);
                            if (DeltaVisualInput == DeltaVisualData.Divided)
                                Numbers.HorizontalAlignment = HorizontalAlignment.Right;
                            else
                                Numbers.HorizontalAlignment = HorizontalAlignment.Center;
                                
                            Numbers.FontSize = FontSizeNumbers;
                        }
                        else if (allDeltaRank[priceKey] < 0)
                        {
                            Numbers = Chart.DrawText($"{iStart}_{i}SellNumber", $"{allDeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtNb_FixedColor);
                            if (DeltaVisualInput == DeltaVisualData.Divided)
                                Numbers.HorizontalAlignment = HorizontalAlignment.Left;
                            else
                                Numbers.HorizontalAlignment = HorizontalAlignment.Center;
                            Numbers.FontSize = FontSizeNumbers;
                        }
                        else
                        {
                            Numbers = Chart.DrawText($"{iStart}_{i}NoNumber", $"{allDeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtNb_FixedColor);
                            Numbers.HorizontalAlignment = HorizontalAlignment.Center;
                            Numbers.FontSize = FontSizeNumbers;
                        }
                        
                        if (IsLastBar)
                        {
                            if (!currentBar_NumbersD.ContainsKey(i))
                                currentBar_NumbersD.Add(i, Numbers);
                            else
                                currentBar_NumbersD[i] = Numbers;
                        }
                    }

                    // =======  Results  =======  
                    if (ShowResults)
                    {
                        Color dynColorLeft = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : SellColor;
                        Color dynColorRight = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : BuyColor;
                        
                        Color compareSumD = allDeltaRank.Values.Sum() > 0 ? BuyColor : allDeltaRank.Values.Sum() < 0 ? SellColor : RtNb_FixedColor;
                        Color dynColorCenter = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : compareSumD;
                        
                        int deltaBuy = allDeltaRank.Values.Where(n => n > 0).Sum();
                        int deltaSell = allDeltaRank.Values.Where(n => n < 0).Sum();
                        
                        int percentBuy = 0;
                        int percentSell = 0;
                        try {percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell));} catch {};
                        try {percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell));} catch {}
                        
                        var selected = ResultsType_Input;
                        string dynStrBuy = selected == ResultsType_Data.Percentage ? $"\n{percentBuy}%" : selected == ResultsType_Data.Value ? $"\n{deltaBuy}" : $"\n{percentBuy}%\n({deltaBuy})";
                        string dynStrSell = selected == ResultsType_Data.Percentage ? $"\n{percentSell}%" : selected == ResultsType_Data.Value ? $"\n{deltaSell}" : $"\n{percentSell}%\n({deltaSell})";
                        string dynSpaceSum = (selected == ResultsType_Data.Percentage || selected == ResultsType_Data.Value) ? $"\n\n" : $"\n\n\n";
                        
                        ChartText Left, Right, Center;
                        Left = Chart.DrawText($"{iStart}SellDeltaSum", $"{dynStrSell}", Bars.OpenTimes[iStart], lowest, dynColorLeft);
                        Right = Chart.DrawText($"{iStart}BuyDeltaSum", $"{dynStrBuy}", Bars.OpenTimes[iStart], lowest, dynColorRight);
                        Center = Chart.DrawText($"{iStart}SumDeltaCenter", $"{dynSpaceSum}{allDeltaRank.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynColorCenter);
                        
                        Left.HorizontalAlignment = HorizontalAlignment.Left;
                        Left.FontSize = FontSizeResults;
                        Right.HorizontalAlignment = HorizontalAlignment.Right;
                        Right.FontSize = FontSizeResults;
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults;
                        

                        if (!CumulDeltaRank.ContainsKey(index))
                            CumulDeltaRank.Add(index, allDeltaRank.Values.Sum());
                        else
                            CumulDeltaRank[index] = allDeltaRank.Values.Sum();

                        int CumulDelta = CumulDeltaRank.Keys.Count <= 1 ? CumulDeltaRank[index] : (CumulDeltaRank[index] + CumulDeltaRank[index-1]);
                        int prevCumulDelta = CumulDeltaRank.Keys.Count <= 2 ? CumulDeltaRank[index] : (CumulDeltaRank[index-1] + CumulDeltaRank[index-2]);
                        
                        Color compareCD = CumulDelta > prevCumulDelta ? BuyColor : CumulDelta < prevCumulDelta ? SellColor : RtNb_FixedColor;
                        Color dynColorCD = ResultsColoringInput == ResultsColoringData.Fixed ? RtNb_FixedColor : compareCD;

                        ChartText CD = Chart.DrawText($"{iStart}CD", $"\n{CumulDelta}\n", Bars.OpenTimes[iStart], highest, dynColorCD);
                        CD.HorizontalAlignment = HorizontalAlignment.Center;
                        CD.VerticalAlignment = VerticalAlignment.Top;
                        CD.FontSize = FontSizeResults;
                        
                        if (EnableFilter) 
                        {
                            CumulDeltaSeries[index] = Math.Abs(CumulDeltaRank[index]);
                            DynamicSeries[index] = Math.Abs(allDeltaRank.Values.Sum());
                            
                            // =========== Dynamic Series Filter ===========
                            double DynamicFilter = DynamicSeries[index] / MADynamic.Result[index];
                            double DynamicLarge = DynamicFilter >= Filter_Ratio ? DynamicSeries[index] : 0;
                            
                            Color dynBarColor = DynamicLarge >= 2 ? Color.FromName(RawColorLargeR.ToString()) : dynColorCenter;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == Color.FromName(RawColorLargeR.ToString()))
                                Chart.SetBarFillColor(index, Color.FromName(RawColorLargeR.ToString()));
                            
                            if (ColoringCD) {
                                // =========== Cumul Delta Filter ===========
                                double CumulDeltaFilter = CumulDeltaSeries[index] / MACumulDelta.Result[index];
                                double CumulDeltaLarge = CumulDeltaFilter > Filter_Ratio ? CumulDeltaSeries[index] : 0;
                                Color dynCDColor = CumulDeltaLarge > 2 ? Color.FromName(RawColorLargeR.ToString()) : dynColorCD;
                                CD.Color = dynCDColor;
                            }
                        }                        
                    }
                }                   

                prev_segment_loop = allSegmentsPrices[i];
            }
        }     
        // ====== Functions Area ======       
        private void VolP_Tick(int index)
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index+1];
            
            if (IsLastBar)
                endTime = _TicksOHLC.Last().OpenTime;
                            
            double prevTick = 0;
            
            for (int tickIndex = 0; tickIndex < _TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar;
                tickBar = _TicksOHLC[tickIndex]; 
                
                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime)
                {
                    if (tickBar.OpenTime > endTime)
                        break;
                    else
                        continue;
                }
                
                RankVol(tickBar.Close);
                prevTick = tickBar.Close;
            }
            // ========= ========== ==========
            void RankVol(double tickPrice)
            {
                double prev_segmentValue = 0.0;
                for (int i = 0; i < allSegmentsPrices.Count; i++)
                {
                    if (prev_segmentValue != 0 && tickPrice >= prev_segmentValue && tickPrice <= allSegmentsPrices[i])
                    {
                        double priceKey = allSegmentsPrices[i];
    
                        if (allVolumesRank.ContainsKey(priceKey))
                        {
                            allVolumesRank[priceKey] += 1;
                            
                            if (tickPrice > prevTick && prevTick != 0)
                                allVolumesR_Up[priceKey] += 1;
                            else if (tickPrice < prevTick && prevTick != 0)
                                allVolumesR_Down[priceKey] += 1;
                            else if (tickPrice == prevTick && prevTick != 0)
                            {
                                allVolumesR_Up[priceKey] += 1;
                                allVolumesR_Down[priceKey] += 1;
                            }
                            
                            allDeltaRank[priceKey] += (allVolumesR_Up[priceKey] - allVolumesR_Down[priceKey]);
                        }
                        else
                        {
                            allVolumesRank.Add(priceKey, 1);
                            
                            if (!allVolumesR_Up.ContainsKey(priceKey))
                                allVolumesR_Up.Add(priceKey, 1);
                            else
                                allVolumesR_Up[priceKey] += 1;
                                
                            if (!allVolumesR_Down.ContainsKey(priceKey))
                                allVolumesR_Down.Add(priceKey, 1);
                            else
                                allVolumesR_Down[priceKey] += 1;
                                
                            if (!allDeltaRank.ContainsKey(priceKey))
                                allDeltaRank.Add(priceKey, (allVolumesR_Up[priceKey] - allVolumesR_Down[priceKey]));
                            else
                                allDeltaRank[priceKey] += (allVolumesR_Up[priceKey] - allVolumesR_Down[priceKey]);
                        }
                        
                        break;            
                    }
                    prev_segmentValue = allSegmentsPrices[i];
                }
            }
        }
        
        private double GetWicks(DateTime startTime, DateTime endTime, bool isBullish)
        {
            double min = Int32.MaxValue;
            double max = 0;
            
            if (IsLastBar)
                endTime = _TicksOHLC.Last().OpenTime;
                        
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
        
        private void DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.LightBlue);
        }
        private void Second_DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt2", $"{Msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }
        private void SetNewBar(BarOpenedEventArgs obj)
        {
            NewBar = true;
        }
        // ************************** VOLUME RENKO/RANGE **************************
        /*
            Original source code by srlcarlg (me) (https://ctrader.com/algos/indicators/show/3045)
            Uses Ticks Data to make the calculation of volume, just like Candles.
        */
        private void VolumeInitialize()
        {
            if (LoadFromInput == LoadFromData.Custom)
            {
                // ==== Get datetime to load from: dd/mm/yyyy ====
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out FromDateTime))
                {
                    if (FromDateTime > Server.Time.Date)
                    {
                        // for Log
                        FromDateTime = Server.Time.Date;
                        Print($"Invalid DateTime '{StringDate}'. Using '{FromDateTime}'");
                    }
                }
                else
                {
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
            DateTime FirstTickTime = _TicksOHLC.OpenTimes.Reverse().LastOrDefault();
            if (FirstTickTime >= FromDateTime)
            {
                LoadMoreTicks(FromDateTime);
                DrawOnScreen("Data Collection Finished \n Calculating...");
            }
            else
            {
                Print($"Using existing tick data from '{FirstTickTime}'");
                DrawOnScreen($"Using existing tick data from '{FirstTickTime}' \n Calculating...");
            }
        }
        private void LoadMoreTicks(DateTime FromDateTime)
        {
            bool msg = false;

            while (_TicksOHLC.OpenTimes.Reverse().LastOrDefault() > FromDateTime)
            {
                if (!msg)
                {
                    Print($"Loading from '{_TicksOHLC.OpenTimes.Reverse().Last()}' to '{FromDateTime}'...");
                    msg = true;
                }

                int loadedCount = _TicksOHLC.LoadMoreHistory();
                Print("Loaded {0} Ticks, Current Tick Date: {1}", loadedCount, _TicksOHLC.OpenTimes.Reverse().LastOrDefault());
                if (loadedCount == 0)
                    break;
            }
            Print("Data Collection Finished, First Tick from: {0}", _TicksOHLC.OpenTimes.Reverse().LastOrDefault());
        }
    }
}