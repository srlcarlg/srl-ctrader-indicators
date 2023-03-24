/*
--------------------------------------------------------------------------------------------------------------------------------
                        Volume Profile
                        
All core features of TPO Profile but in VOLUME
It also has the features of Order Flow Ticks

=== Volume Modes ===
*Normal/Gradient Mode = Volume Profile with Fixed/Gradient Color
*Buy vs Sell Mode = The name explains itself
*Delta Mode = Volume Delta Profile
*Normal+Delta Mode = Volume + Delta

The Volume Calculation(in Bars Volume Source)
is exported, with adaptations, from the BEST VP I have see/used for MT4/MT5, 
of Russian FXcoder's https://gitlab.com/fxcoder-mql/vp (VP 10.1), author of the famous (Volume Profile + Range v6.0)
a BIG THANKS to HIM!

All parameters are self-explanatory.

For Better Performance, Recompile it on cTrader with .NET 6.0 instead .NET 4.x.


AUTHOR: srlcarlg 
 
== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
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
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class VolumeProfile : Indicator
    {
        [Parameter("Lookback:", DefaultValue = 5, MinValue = 1, MaxValue = 100, Group = "==== Volume Profile ====")]
        public int Lookback { get; set; }
        
        public enum VolumeSourceData
        {
            Ticks,
            Bars,
        }

        [Parameter("Volume Source:", DefaultValue = VolumeSourceData.Bars, Group = "==== Volume Profile ====")]
        public VolumeSourceData VolumeSourceInput { get; set; }
        
        public enum VolumeModeData
        {
            Normal,
            Gradient,
            Buy_Sell,
            Delta,
            Normal_Delta
        }

        [Parameter("Volume Mode:", DefaultValue = VolumeModeData.Gradient, Group = "==== Volume Profile ====")]
        public VolumeModeData VolumeModeInput { get; set; }
                
        public enum ConfigRowData
        {
            Predefined,
            Custom,
        }
        [Parameter("Config:", DefaultValue = ConfigRowData.Predefined, Group = "==== Volume Profile ====")]
        public ConfigRowData ConfigRowInput { get; set; }
        
        
        [Parameter("Custom Interval:", DefaultValue = "Daily", Group = "==== Custom Interval / Row Config ====")]
        public TimeFrame CustomInterval { get; set; }
        
        [Parameter("Custom Row Height:", DefaultValue = 1, MinValue = 0.2, Group = "==== Custom Interval / Row Config ====")]
        public double CustomHeight { get; set; }
        
        
        [Parameter("Bars Source:", DefaultValue = "Minute", Group = "==== Bars Volume Source ====")]
        public TimeFrame BarsVOLSource_TF { get; set; }
                
        public enum DistributionData
        {
            OHLC,
            High,
            Low,
            Close,
            Uniform_Distribution,
            Uniform_Presence,
            Parabolic_Distribution,
            Triangular_Distribution,
        }

        [Parameter("Bar Distribution(Bars only):", DefaultValue = DistributionData.OHLC, Group = "==== Bars Volume Source ====")]
        public DistributionData DistributionInput { get; set; }
        
        
        public enum LoadFromData
        {
            According_to_Lookback,
            Today,
            Yesterday,
            One_Week,
            Custom
        }

        [Parameter("Load From:", DefaultValue = LoadFromData.Today, Group = "==== Ticks Volume Source ====")]
        public LoadFromData LoadFromInput { get; set; }
        
        [Parameter("Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== Ticks Volume Source ====")]
        public string StringDate { get; set; }
        
        
        public enum HistWidthData
        {      
            _15,
            _30,
            _50,
            _70,
            _100
        }
        [Parameter("Histogram Width(%)", DefaultValue = HistWidthData._50, Group = "==== Histogram Settings ====")]
        public HistWidthData HistWidthInput { get; set; }
        
        public enum HistSideData
        {      
            Left,
            Right,
        }
        [Parameter("Histogram Side", DefaultValue = HistSideData.Left, Group = "==== Histogram Settings ====")]
        public HistSideData HistSideInput { get; set; }
        
        [Parameter("Fill Histogram?", DefaultValue = true, Group = "==== Histogram Settings ====")]
        public bool FillHist { get; set; }
                        
        [Parameter("Opacity:", DefaultValue = 60, MinValue = 5, MaxValue = 100, Group = "==== Histogram Settings ====")]
        public int OpacityHistInput { get; set; }
        
        [Parameter("Opacity inside VA:", DefaultValue = 80, MinValue = 5, MaxValue = 100, Group = "==== Histogram Settings ====")]
        public int OpacityHistVA_Input { get; set; }


        [Parameter("Normal Color:", DefaultValue = Colors.Gray, Group = "==== Colors Histogram ====")]
        public Colors RawColorHist { get; set; }
        
        [Parameter("Normal Color inside VA:", DefaultValue = Colors.DeepSkyBlue, Group = "==== Colors Histogram ====")]
        public Colors RawColorHist_VA { get; set; }

        [Parameter("Gradient Color Min. Vol:", DefaultValue = Colors.RoyalBlue, Group = "==== Colors Histogram ====")]
        public Colors RawColorGrandient_Min { get; set; }
        
        [Parameter("Gradient Color Max. Vol:", DefaultValue = Colors.OrangeRed, Group = "==== Colors Histogram ====")]
        public Colors RawColorGrandient_Max { get; set; }
                                
        [Parameter("Color Buy:", DefaultValue = Colors.DeepSkyBlue, Group = "==== Colors Histogram ====")]
        public Colors RawColorBuy { get; set; }
        
        [Parameter("Color Sell:", DefaultValue = Colors.Crimson, Group = "==== Colors Histogram ====")]
        public Colors RawColorSell { get; set; }
        
        
        [Parameter("Show Results(%)?", DefaultValue = true, Group = "==== Other settings ====")]
        public bool ShowResults { get; set; }
        
        [Parameter("Font Size Results:", DefaultValue = 10, MinValue = 1, MaxValue = 80, Group = "==== Other settings ====")]
        public int FontSizeResults { get; set; } 
               
        [Parameter("Show OHLC Bar?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ShowOHLC { get; set; }
        
        [Parameter("Show Value Area?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ShowVA { get; set; }
        
        [Parameter("Show POC Migration?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ShowMigration { get; set; }
        
        [Parameter("Keep POC? (no VA)", DefaultValue = true, Group = "==== Other settings ====")]
        public bool KeepPOC { get; set; }
                
        [Parameter("Extended POCs?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ExtendPOC { get; set; }
           
        [Parameter("Extended VAs?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ExtendVA { get; set; }


        public enum ConfigInfoC
        {
            Top_Right,
            Top_Left,
            Bottom_Right,
            Bottom_Left,
        }
        
        [Parameter("OHLC Bar Color:", DefaultValue = Colors.Gray, Group = "==== Others ====")]
        public Colors RawColorOHLC { get; set; }
        
        [Parameter("Info Corner Position:", DefaultValue = ConfigInfoC.Bottom_Left, Group = "==== Others ====")]
        public ConfigInfoC ConfigInfoC_Input { get; set; }
        
        [Parameter("Info Corner Color:", DefaultValue = Colors.Snow, Group = "==== Others ====")]
        public Colors RawColorInfoC { get; set; }
        
        
        [Parameter("Color POC:", DefaultValue = Colors.Gold, Group = "==== Point of Control ====")]
        public Colors RawColorPOC { get; set; }
        
        [Parameter("LineStyle POC:", DefaultValue = LineStyle.Solid, Group = "==== Point of Control ====")]
        public LineStyle LineStylePOC { get; set; }
        
        [Parameter("Thickness POC:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Point of Control ====")]
        public int ThicknessPOC { get; set; }
        
                
        [Parameter("Color VA:", DefaultValue = Colors.AliceBlue,  Group = "==== Value Area ====")]
        public Colors RawColorVA { get; set; }
        
        [Parameter("Opacity VA" , DefaultValue = 10, MinValue = 5, MaxValue = 100, Group = "==== Value Area ====")]
        public int OpacityVA { get; set; }
                
        [Parameter("LineStyle VA:", DefaultValue = LineStyle.Solid, Group = "==== Value Area ====")]
        public LineStyle LineStyleVA { get; set; }
               
        [Parameter("Thickness VA:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Value Area ====")]
        public int ThicknessVA { get; set; }
        
        [Parameter("Color VAH:", DefaultValue = Colors.PowderBlue , Group = "==== Value Area ====")]
        public Colors RawColorVAH { get; set; }
        
        [Parameter("Color VAL:", DefaultValue = Colors.PowderBlue, Group = "==== Value Area ====")]
        public Colors RawColorVAL { get; set; }
        
        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }
        
        [Output("POC Migration Line", LineStyle=LineStyle.LinesDots, LineColor = "PaleGoldenrod", Thickness = 1)]
        public IndicatorDataSeries POCMigration { get; set; }
                
        private VerticalAlignment V_Align = VerticalAlignment.Top;
        private HorizontalAlignment H_Align = HorizontalAlignment.Center;
        private bool Wrong = false;
                
        private List<double> allSegmentsPrices = new List<double>();        
        private IDictionary<double, double> allVolumesRank = new Dictionary<double, double>();
        private IDictionary<double, double> allVolumesR_Up = new Dictionary<double, double>();
        private IDictionary<double, double> allVolumesR_Down = new Dictionary<double, double>();
        private IDictionary<double, double> allDeltaRank = new Dictionary<double, double>();
        private IDictionary<double, double> CumulDeltaRank = new Dictionary<double, double>();
        
        private List<ChartTrendLine> allPOCsLines = new List<ChartTrendLine>();   
        private List<ChartTrendLine> allVALines = new List<ChartTrendLine>(); 
        
        private IDictionary<int, ChartRectangle> allRectangles = new Dictionary<int, ChartRectangle>();
        
        private double HeightPips = 4;
        private DateTime FromDateTime;
        private TimeFrame VOL_TF;
        private Bars LookBack_Bars;
        private Bars _TicksOHLC;
        private Bars VOL_Bars;
        
        private double rowHeight = 0;
        private double drawHeight = 0;
        
        private double[] priceVA_LHP = {0, 0, 0};

        private bool isLive = false;
        
        private Color HistColor;
        private Color HistColorVA;
        private int Hist_VA_Opacity;
        private Color VAColor;
        private Color BuyColor;
        private Color SellColor;

        private int cleanedIndex;
                
        private bool NewBar = false;
        
        private double prevPrice;
        
        protected override void Initialize()
        {
            // ========== Predefined Config ==========
            if (ConfigRowInput == ConfigRowData.Predefined && (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Day3))
            {
                if (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Minute4)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute) {
                        VOL_TF = TimeFrame.Hour; SetHeight();
                    }
                    else if (Chart.TimeFrame == TimeFrame.Minute2) {
                        VOL_TF = TimeFrame.Hour2; SetHeight();
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Minute4) {
                        VOL_TF = TimeFrame.Hour3; SetHeight();
                    }
                    void SetHeight() {
                        SetHeightPips(0.5, 8);
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute5) {
                        VOL_TF = TimeFrame.Hour4; SetHeight();
                    }
                    else if (Chart.TimeFrame == TimeFrame.Minute6) {
                        VOL_TF = TimeFrame.Hour6; SetHeight();
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Minute8) {
                        VOL_TF = TimeFrame.Hour8; SetHeight();
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Minute10) {
                        VOL_TF = TimeFrame.Hour12; SetHeight();
                    }
                    void SetHeight() {
                        SetHeightPips(0.5, 25);
                    }
                    
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour) {
                        VOL_TF = TimeFrame.Daily; SetHeightPips(2, 50);
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Hour8) {
                        VOL_TF = TimeFrame.Weekly; SetHeightPips(4, 140);
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Day3) {
                    VOL_TF = TimeFrame.Monthly; SetHeightPips(6, 220);
                }
            }
            else
            {   
                string[] timeBased = {"Minute", "Hour", "Daily", "Day"};
                if (ConfigRowInput == ConfigRowData.Predefined)
                {
                    string Msg = "'Predefined Config' is designed only for Standard Timeframe (Minutes, Hours, Days) \n Weekly and Monthly is not currently supported \n\n use 'Custom Config' to others Chart Timeframes (Renko/Range/Ticks).";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                } 
                if (!(timeBased.Any(CustomInterval.Name.ToString().Contains)))
                {   
                    string Msg = $"'Volume Interval' is designed ONLY for TIME \n (Minutes, Hours, Days) \n Weekly and Monthly is not currently supported";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                if (CustomInterval == Chart.TimeFrame || CustomInterval < Chart.TimeFrame)
                {
                    string comp = CustomInterval == Chart.TimeFrame ? "==" : "<";
                    string Msg = $"Volume Interval ({CustomInterval.ShortName}) {comp} Chart Timeframe ({Chart.TimeFrame.ShortName})\nWhy?";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                if (CustomInterval < TimeFrame.Minute15)
                {
                    string Msg = "The minimum 'Custom Interval' is 15 minutes";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                VOL_TF = CustomInterval;
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
            string[] timesBased = {"Minute", "Hour", "Daily", "Day"};
            if (!(timesBased.Any(BarsVOLSource_TF.Name.ToString().Contains)))
            {
                string Msg = $"'Bars Volume Source' is designed ONLY for TIME \n (Minutes, Hours, Days) \n Weekly and Monthly is not currently supported";
                Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                Wrong = true;
                return;
            }
            if ((VolumeModeInput == VolumeModeData.Buy_Sell || VolumeModeInput == VolumeModeData.Delta || VolumeModeInput == VolumeModeData.Normal_Delta) && BarsVOLSource_TF != TimeFrame.Minute)
            {
                string Msg = $"'Buy_Sell' and 'Delta' is designed ONLY for '1m Bars Volume Source'";
                Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                Wrong = true;
                return;
            }
            // ============================================================================

            LookBack_Bars = MarketData.GetBars(VOL_TF);      
            if (LookBack_Bars.ClosePrices.Count < Lookback)
            {
                while (LookBack_Bars.ClosePrices.Count < Lookback)
                {
                    int loadedCount = LookBack_Bars.LoadMoreHistory();
                    Print($"Loaded {loadedCount}, {VOL_TF.ShortName} LookBack Bars, Current Bar Date: {LookBack_Bars.OpenTimes.FirstOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
            }
            
            if (VolumeSourceInput == VolumeSourceData.Ticks)
                _TicksOHLC = MarketData.GetBars(TimeFrame.Tick);
            else
                VOL_Bars = MarketData.GetBars(BarsVOLSource_TF);
                
            if (VolumeSourceInput == VolumeSourceData.Bars)
            {
                if (VOL_Bars.OpenTimes.FirstOrDefault() > LookBack_Bars.OpenTimes[LookBack_Bars.ClosePrices.Count - Lookback])
                {
                    while (VOL_Bars.OpenTimes.FirstOrDefault() > LookBack_Bars.OpenTimes[LookBack_Bars.ClosePrices.Count - Lookback])
                    {
                        int loadedCount = VOL_Bars.LoadMoreHistory();
                        Print($"Loaded {loadedCount}, {BarsVOLSource_TF.ShortName} VOL Bars, Current Bar Date: {VOL_Bars.OpenTimes.FirstOrDefault()}");
                        if (loadedCount == 0)
                            break;
                    }
                }
                try {
                DateTime FirstVolDate = VOL_Bars.OpenTimes.FirstOrDefault();
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeStart", FirstVolDate, Color.Red);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText($"VolumeStartText", $"{BarsVOLSource_TF.ShortName} Volume Data \n ends here", FirstVolDate, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(FirstVolDate)], Color.Red);
                textInfo.FontSize = 8;
                } catch {};
            }
            else
                TickVolumeInitialize();
            
            // Ex: 4 pips to VOL Distribuition(rowHeight)
            rowHeight = (Symbol.PipSize) * HeightPips;
            drawHeight = (Symbol.PipSize) * (HeightPips/2);
            // ===== Colors with Opacity =====
            int Hist_Opacity = (int)(2.55 * OpacityHistInput);
            Color rawHist = Color.FromName(RawColorHist.ToString());
            HistColor = Color.FromArgb(Hist_Opacity, rawHist.R, rawHist.G, rawHist.B);
            
            Hist_VA_Opacity = (int)(2.55 * OpacityHistVA_Input);
            Color rawHist_VA = Color.FromName(RawColorHist_VA.ToString());
            HistColorVA = Color.FromArgb(Hist_VA_Opacity, rawHist_VA.R, rawHist_VA.G, rawHist_VA.B);
            
            int VA_Opacity = (int)(2.55 * OpacityVA);
            Color rawVA = Color.FromName(RawColorVA.ToString());
            VAColor = Color.FromArgb(VA_Opacity, rawVA.R, rawVA.G, rawVA.B);
            
            Color rawBuy = Color.FromName(RawColorBuy.ToString());
            BuyColor = Color.FromArgb(Hist_Opacity, rawBuy.R, rawBuy.G, rawBuy.B);
            
            Color rawSell = Color.FromName(RawColorSell.ToString());
            SellColor = Color.FromArgb(Hist_Opacity, rawSell.R, rawSell.G, rawSell.B);
            
            // === Info Corner ===
            Color rawColor = Color.FromName(RawColorInfoC.ToString());
            Color InfoColor = Color.FromArgb((int)(2.55 * 50), rawColor.R, rawColor.G, rawColor.B);
            string strMode = ConfigRowInput == ConfigRowData.Predefined ? "Predefined" : "Custom";
            string strVolSource = VolumeSourceInput == VolumeSourceData.Bars ? BarsVOLSource_TF.ShortName : $"Tick";
            string volInfo = $"{VolumeModeInput} \n" +
                             $"VOL Data: {strVolSource} \n" + 
                             $"VOL Interval: {VOL_TF.ShortName} \n" + 
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
            Chart.DrawStaticText("VOL Info", volInfo, v_align, h_align, InfoColor);
            
            DrawOnScreen("Calculating...");
            string nonTimeBased = !(timesBased.Any(Chart.TimeFrame.ToString().Contains)) ? "Ticks/Renko/Range with 100% Histogram Width \n sometimes is recommended" : "";
            string ticksInfo = $"Ticks Volume Source: \n 1) Naturally heavier \n 2) Large 'Lookback' or 'Tick Data' takes longer to calculate \n 3) Recommended for intraday only";
            string showTicksInfo = VolumeSourceInput == VolumeSourceData.Ticks ? ticksInfo : "";
            Second_DrawOnScreen($"Taking too long? You can: \n 1) Increase the rowHeight \n 2) Disable the Value Area (High Performance)\n\n {nonTimeBased} \n\n {showTicksInfo}");
            if (Application.UserTimeOffset.ToString() != "03:00:00")
                Third_DrawOnScreen("Set your UTC to UTC+3");

        }

        public override void Calculate(int index)
        {
            if (Wrong)
                return;
                
            // ==== Removing Messages ====
            if (!IsLastBar) {
                DrawOnScreen(""); Second_DrawOnScreen(""); Third_DrawOnScreen("");
            }
                
            Bars TF_Bars = LookBack_Bars;
            // Get Index of VOL Interval to continue only in Lookback
            int iVerify = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (TF_Bars.ClosePrices.Count - iVerify > Lookback)
                return;
            
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);
            
            // ====== Extended POC/VA ========
            if (ExtendPOC && allPOCsLines.Count != 0 && IsLastBar && allPOCsLines.LastOrDefault().Y2 != priceVA_LHP[2])
            {
                for (int tl=0; tl < allPOCsLines.Count; tl++)
                {
                    allPOCsLines[tl].Time2 = Bars.OpenTimes[index];
                    string dynDate = VOL_TF == TimeFrame.Daily ? allPOCsLines[tl].Time1.Date.AddDays(1).ToString().Replace("00:00:00", "") : allPOCsLines[tl].Time1.Date.ToString();
                    Chart.DrawText($"POC{allPOCsLines[tl].Time1}", $"{dynDate}", Bars.OpenTimes[index], allPOCsLines[tl].Y2+drawHeight, Color.FromName(RawColorPOC.ToString()));
                }
            }
            
            if (ExtendVA && allVALines.Count != 0 && IsLastBar && allVALines.LastOrDefault().Time2 != Bars.OpenTimes[index])
            {
                for (int tl=0; tl < allVALines.Count; tl++)
                    allVALines[tl].Time2 = Bars.OpenTimes[index];
            }

            // === Clean Dicts/others ===
            if (index == indexStart && index != cleanedIndex || (index-1) == indexStart && (index-1) != cleanedIndex) 
            {
                allSegmentsPrices.Clear();
                allVolumesRank.Clear();
                allVolumesR_Up.Clear();
                allVolumesR_Down.Clear();
                allDeltaRank.Clear();
                allRectangles.Clear();
                double[] VAforColor = {0, 0, 0};
                priceVA_LHP = VAforColor;
                cleanedIndex = index == indexStart ? index : (index-1);
            }
            // Historical data 
            if (!IsLastBar)
            {
                if (!isLive)
                    VP(indexStart, index); 
                else
                    NewBar=true;
            }
            else
            {
               if (NewBar)
               {
                    VP(indexStart, index); 
                    NewBar = false;
                    return;
               }
                isLive = true;
                // "Repaint" if the price moves half of rowHeight
                if (Bars.ClosePrices[index] >= (prevPrice+drawHeight) ||  Bars.ClosePrices[index] <= (prevPrice-drawHeight))
                {                        
                    for (int i=indexStart; i <= index; i++)
                    {
                        if (i == indexStart) {
                            allSegmentsPrices.Clear();
                            allVolumesRank.Clear();
                            allVolumesR_Up.Clear();
                            allVolumesR_Down.Clear();
                            allDeltaRank.Clear();
                            allRectangles.Clear();
                        }
                            
                        VP(indexStart, i); 
                    } 
                    prevPrice = Bars.ClosePrices[index];
                }
            }
        }
        
        private void VP(int iStart, int index)
        {
            // ======= Highest and Lowest =======
            Bars TF_Bars = LookBack_Bars;
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            
            double highest = TF_Bars.HighPrices[TF_idx], lowest = TF_Bars.LowPrices[TF_idx], open = TF_Bars.OpenPrices[TF_idx];        
            
            // ======= Chart Segmentation =======
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
            
            // ======= VP =======
            if (VolumeSourceInput == VolumeSourceData.Ticks)
                VolP_Tick(index);
            else
                VolP_Bars(index);

            // ======= Drawing =======
            if (allSegmentsPrices.Count == 0)
                return;

            for (int i = 0; i < allSegmentsPrices.Count; i++)
            {
                double priceKey = allSegmentsPrices[i];
                if (!allVolumesRank.ContainsKey(priceKey))
                    continue;
                    
                /*
                Indeed, the value of X-Axis is simply a rule of three, 
                where the maximum value will be the maxLength (in Milliseconds),
                from there the math adjusts the histograms.
                    
                    MaxValue    maxLength(ms)
                       x             ?(ms)
                
                The values 1.25 and 4 are the manually set values
                */
                
                double lowerSegment = allSegmentsPrices[i]-rowHeight;
                double upperSegment = allSegmentsPrices[i];
                
                double largestVOL = allVolumesRank.Values.Max();

                double maxLength = Bars[index].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                var selected = HistWidthInput;
                double maxWidth = selected == HistWidthData._15 ? 1.25 : selected == HistWidthData._30 ? 1.50 : selected == HistWidthData._50 ? 2 : 4;
                double proportion = allVolumesRank[priceKey] * (maxLength-(maxLength/maxWidth));      
                if (selected == HistWidthData._100)
                    proportion = allVolumesRank[priceKey] * maxLength;      

                double dynLength = proportion / largestVOL;
                
                Color dynColor = HistColor;
                if (VolumeModeInput == VolumeModeData.Gradient)
                {
                    Color rawMinColor = Color.FromName(RawColorGrandient_Min.ToString());
                    Color rawMaxColor = Color.FromName(RawColorGrandient_Max.ToString());
                    
                    double Intensity = ((allVolumesRank[priceKey] * 100) / largestVOL) / 100;
                    double stepR = (rawMaxColor.R - rawMinColor.R) * Intensity;
                    double stepG = (rawMaxColor.G - rawMinColor.G) * Intensity;
                    double stepB = (rawMaxColor.B - rawMinColor.B) * Intensity;
                    
                    int A = (int)(2.55 * OpacityHistInput);
                    int R = (int)Math.Round(rawMinColor.R + stepR);
                    int G = (int)Math.Round(rawMinColor.G + stepG);
                    int B = (int)Math.Round(rawMinColor.B + stepB);
                    
                    dynColor = Color.FromArgb(A, R, G, B);
                }
                
                if (VolumeModeInput == VolumeModeData.Normal || VolumeModeInput == VolumeModeData.Normal_Delta || VolumeModeInput == VolumeModeData.Gradient)
                {
                    ChartRectangle volHist;
                    volHist = Chart.DrawRectangle($"{iStart}_{i}_", Bars.OpenTimes[iStart], lowerSegment, Bars.OpenTimes[iStart].AddMilliseconds(dynLength), upperSegment, dynColor);
                    
                    if (allRectangles.ContainsKey(i))
                        allRectangles[i] = volHist;
                    else
                        allRectangles.Add(i, volHist);
                        
                    if (FillHist)
                        volHist.IsFilled = true;
                    if (HistSideInput == HistSideData.Right) {
                        volHist.Time1 = Bars.OpenTimes[index];
                        volHist.Time2 = Bars.OpenTimes[index].AddMilliseconds(-dynLength);
                    }
                }
                if (VolumeModeInput == VolumeModeData.Buy_Sell)
                {                    
                    // Buy vs Sell = Pseudo Delta
                    double buy_Volume = 0;
                    try {buy_Volume = allVolumesR_Up.Values.Max();} catch {};
                    double sell_Volume = 0;
                    try {sell_Volume = allVolumesR_Down.Values.Max();} catch {};
                    double sideVolMax = buy_Volume > sell_Volume ? buy_Volume : sell_Volume;
                    
                    double maxHalfWidth = selected == HistWidthData._15 ? 1.12 : selected == HistWidthData._30 ? 1.25 : selected == HistWidthData._50 ? 1.40 : 1.75;
                    
                    double proportion_Up = 0;
                    try {proportion_Up = allVolumesR_Up[priceKey] * (maxLength - (maxLength/maxHalfWidth));} catch {};
                    if (selected == HistWidthData._100)
                        try {proportion_Up = allVolumesR_Up[priceKey] * (maxLength - (maxLength/3));} catch {};
                    
                    double dynLength_Up = proportion_Up / sideVolMax;;
                    
                    double proportion_Down =0;
                    try {proportion_Down = allVolumesR_Down[priceKey] * (maxLength - (maxLength/maxWidth));} catch {};
                    if (selected == HistWidthData._100)
                        try {proportion_Down = allVolumesR_Down[priceKey] * maxLength;} catch {};
                    
                    double dynLength_Down = proportion_Down / sideVolMax;
                    
                    ChartRectangle buyHist, sellHist;
                    if (allVolumesR_Down.ContainsKey(priceKey) && allVolumesR_Up.ContainsKey(priceKey)) 
                    {
                        sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Down), upperSegment, SellColor);
                        buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Up), upperSegment, BuyColor);
                        if (FillHist) {
                        buyHist.IsFilled = true;sellHist.IsFilled = true;
                        }
                        if (HistSideInput == HistSideData.Right) {
                            sellHist.Time1 = Bars.OpenTimes[index];
                            sellHist.Time2 = Bars.OpenTimes[index].AddMilliseconds(-dynLength_Down);
                            buyHist.Time1 = Bars.OpenTimes[index];
                            buyHist.Time2 = Bars.OpenTimes[index].AddMilliseconds(-dynLength_Up);
                        }
                    }
                    if (ShowResults)
                    {
                        double volBuy = allVolumesR_Up.Values.Sum();
                        double volSell = allVolumesR_Down.Values.Sum();                                                                  
                        double percentBuy = (volBuy * 100) / (volBuy + volSell);
                        double percentSell = (volSell * 100) / (volBuy + volSell);
                        
                        ChartText Left, Right;
                        Left = Chart.DrawText($"{iStart}BuySum", $"{Math.Round(percentBuy)}%", Bars.OpenTimes[iStart], lowest, SellColor);    
                        Right = Chart.DrawText($"{iStart}SellSum", $"{Math.Round(percentSell)}%", Bars.OpenTimes[iStart], lowest, BuyColor);
                        Left.HorizontalAlignment = HorizontalAlignment.Left; Left.FontSize = FontSizeResults;
                        Right.HorizontalAlignment = HorizontalAlignment.Right; Right.FontSize = FontSizeResults;
                        if (HistSideInput == HistSideData.Right) {
                            Right.Time = Bars.OpenTimes[index];
                            Left.Time = Bars.OpenTimes[index];
                        }
                    }
                }
                else if (VolumeModeInput == VolumeModeData.Delta || VolumeModeInput == VolumeModeData.Normal_Delta)
                {
                    // Delta
                    double Positive_Delta = allDeltaRank.Values.Max();
                    IEnumerable<double> allNegative = allDeltaRank.Values.Where(n => n < 0);  
                    double Negative_Delta = 0;
                    try {Negative_Delta = Math.Abs(allNegative.Min());} catch {}
                    
                    double deltaMax = Positive_Delta > Negative_Delta ? Positive_Delta : Negative_Delta;
                    
                    double proportion_Delta = Math.Abs(allDeltaRank[priceKey]) * (maxLength - (maxLength/maxWidth));
                     if (selected == HistWidthData._100)
                        proportion_Delta = Math.Abs(allDeltaRank[priceKey]) * maxLength;
                    double dynLength_Delta = proportion_Delta / deltaMax;
                    
                    ChartRectangle deltaHist;
                    try {
                        if (allDeltaRank[priceKey] >= 0)
                            deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, BuyColor);
                        else
                            deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, SellColor);
                    } catch {
                        deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, HistColor);
                    }
                    if (FillHist)
                        deltaHist.IsFilled = true;
                    if (HistSideInput == HistSideData.Right) {
                        deltaHist.Time1 = Bars.OpenTimes[index];
                        deltaHist.Time2 = deltaHist.Time2 != Bars[iStart].OpenTime ? Bars.OpenTimes[index].AddMilliseconds(-dynLength_Delta) : Bars[iStart].OpenTime;
                    }
                    if (ShowResults) 
                    {
                        double deltaBuy = allDeltaRank.Values.Where(n => n > 0).Sum();
                        double deltaSell = allDeltaRank.Values.Where(n => n < 0).Sum();
                        double percentBuy = 0;
                        double percentSell = 0;
                        try {percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell));} catch {};
                        try {percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell));} catch {}
    
                        ChartText Left, Right;
                        Right = Chart.DrawText($"{iStart}BuyDeltaSum", $"{Math.Round(percentBuy)}%", Bars.OpenTimes[iStart], lowest, BuyColor);
                        Left = Chart.DrawText($"{iStart}SellDeltaSum", $"{Math.Round(percentSell)}%", Bars.OpenTimes[iStart], lowest, SellColor);
                        Left.HorizontalAlignment = HorizontalAlignment.Left; Left.FontSize = FontSizeResults;
                        Right.HorizontalAlignment = HorizontalAlignment.Right; Right.FontSize = FontSizeResults;
                        
                        if (HistSideInput == HistSideData.Right) {
                            Right.Time = Bars.OpenTimes[index];
                            Left.Time = Bars.OpenTimes[index];
                        }
                    }
                }
                // ============= Coloring Letters + VAL / VAH / POC =============
                if (ShowVA)
                {
                    double[] VAL_VAH_POC = VA_Calculation();
                    
                    if (ShowMigration){
                        if (IsLastBar && NewBar)
                            POCMigration[index] = VAL_VAH_POC[2]-rowHeight;
                        else if (!IsLastBar)
                            POCMigration[index] = VAL_VAH_POC[2]-rowHeight;
                    }
                    // ==========================
                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[2]-rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[2]-rowHeight, Color.FromName(RawColorPOC.ToString()));
                    ChartTrendLine vah = Chart.DrawTrendLine($"VAH_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[1]+rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[1]+rowHeight, Color.FromName(RawColorVAH.ToString()));
                    ChartTrendLine val = Chart.DrawTrendLine($"VAL_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[0], Bars.OpenTimes[index], VAL_VAH_POC[0], Color.FromName(RawColorVAL.ToString()));
                    
                    double[] VAforColor = {VAL_VAH_POC[0], VAL_VAH_POC[1], VAL_VAH_POC[2]};
                    priceVA_LHP = VAforColor;
                    
                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                    vah.LineStyle = LineStyleVA; vah.Thickness = ThicknessVA; vah.Comment = "VAH";
                    val.LineStyle = LineStyleVA; val.Thickness = ThicknessVA; val.Comment = "VAL"; 
                    
                    // ==== POC Lines ====
                    if (allPOCsLines.Contains(poc))
                    {
                        for (int tl=0; tl < allRectangles.Count; tl++)
                        {
                            if (allPOCsLines[tl].Time1 == poc.Time1) {
                                allPOCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        allPOCsLines.Add(poc);
                        
                    // ==== VAH / VAL Lines ====
                    if (allVALines.Contains(vah) || allVALines.Contains(val))
                    {
                        for (int tl=0; tl < allVALines.Count; tl++)
                        {
                            if (allVALines[tl].Comment == "VAH" && allVALines[tl].Time1 == vah.Time1)
                                allVALines[tl] = vah;
                            else if (allVALines[tl].Comment == "VAL" && allVALines[tl].Time1 == val.Time1)
                                allVALines[tl] = val;
                        }
                    }
                    else
                    {
                        if (!allVALines.Contains(vah))
                            allVALines.Add(vah);
                        if (!allVALines.Contains(val))
                            allVALines.Add(val);
                    }   
                    
                    if (VolumeModeInput == VolumeModeData.Normal)
                    {
                        Color rawPOC = Color.FromName(RawColorPOC.ToString());
                        Color opacifiedPOC = Color.FromArgb(Hist_VA_Opacity, rawPOC.R, rawPOC.G, rawPOC.B);
                        
                        Color rawVAH = Color.FromName(RawColorVAH.ToString());
                        Color opacifiedVAH = Color.FromArgb(Hist_VA_Opacity, rawVAH.R, rawVAH.G, rawVAH.B);
                        
                        Color rawVAL = Color.FromName(RawColorVAL.ToString());
                        Color opacifiedVAL = Color.FromArgb(Hist_VA_Opacity, rawVAL.R, rawVAL.G, rawVAL.B);
                        // =========== Coloring Retangles ============
                        foreach (int key in allRectangles.Keys)
                        {                            
                            if (allRectangles[key].Y1 > priceVA_LHP[0] && allRectangles[key].Y1 < priceVA_LHP[1])
                                allRectangles[key].Color = HistColorVA;
                            
                            if (allRectangles[key].Y1 == priceVA_LHP[2]-rowHeight)
                                allRectangles[key].Color = opacifiedPOC;
                            
                            if (allRectangles[key].Y1 == priceVA_LHP[1])
                                allRectangles[key].Color = opacifiedVAH;
                            else if (allRectangles[key].Y1 == priceVA_LHP[0])
                                allRectangles[key].Color = opacifiedVAL;
                        }
                    }
                    else
                    {
                        Color rawPOC = Color.FromName(RawColorPOC.ToString());
                        Color opacifiedPOC = Color.FromArgb(Hist_VA_Opacity, rawPOC.R, rawPOC.G, rawPOC.B);
                        
                        foreach (int key in allRectangles.Keys)
                        {                            
                            if (allRectangles[key].Y1 == priceVA_LHP[2]-rowHeight)
                                allRectangles[key].Color = opacifiedPOC;
                        } 
                    }
                }
                else if (!ShowVA && KeepPOC)
                {
                    double priceLVOL = 0;
                    for (int k = 0; k < allVolumesRank.Count; k++)
                    {
                        if (allVolumesRank.ElementAt(k).Value == largestVOL) {
                            priceLVOL = allVolumesRank.ElementAt(k).Key;
                            break;
                        }
                    }
    
                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], priceLVOL-rowHeight, Bars.OpenTimes[index], priceLVOL-rowHeight, Color.FromName(RawColorPOC.ToString()));
                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                    
                    if (ShowMigration){
                        if (IsLastBar && NewBar)
                            POCMigration[index] = priceLVOL-rowHeight;
                        else if (!IsLastBar)
                            POCMigration[index] = priceLVOL-rowHeight;
                    }
                    // ==== POC Lines ====
                    if (allPOCsLines.Contains(poc))
                    {
                        for (int tl=0; tl < allRectangles.Count; tl++)
                        {
                            if (allPOCsLines[tl].Time1 == poc.Time1) {
                                allPOCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        allPOCsLines.Add(poc);
                    
                    Color rawPOC = Color.FromName(RawColorPOC.ToString());
                    Color opacifiedPOC = Color.FromArgb(Hist_VA_Opacity, rawPOC.R, rawPOC.G, rawPOC.B);
                    // =========== Coloring Retangles ============
                    foreach (int key in allRectangles.Keys)
                    { 
                        if (allRectangles[key].Y1 == priceLVOL-rowHeight)
                            allRectangles[key].Color = opacifiedPOC;
                    }
                }  
            }
            // ====== Rectangle VA ======
            if (ShowVA && priceVA_LHP[0] != 0) 
            {
                ChartRectangle rectVA;
                rectVA = Chart.DrawRectangle($"{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], priceVA_LHP[0], Bars.OpenTimes[index], priceVA_LHP[1]+rowHeight, VAColor);
                rectVA.IsFilled = true;
            }

            if (!ShowOHLC)
                return;
            ChartText iconOpenSession =  Chart.DrawText($"Start{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], TF_Bars.OpenPrices[TF_idx], Color.FromName(RawColorOHLC.ToString()));
            ChartText iconCloseSession =  Chart.DrawText($"End{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], Bars.ClosePrices[index], Color.FromName(RawColorOHLC.ToString()));
            iconOpenSession.VerticalAlignment = VerticalAlignment.Center;
            iconOpenSession.HorizontalAlignment = HorizontalAlignment.Left;
            iconOpenSession.FontSize = 14;
            iconCloseSession.VerticalAlignment = VerticalAlignment.Center;
            iconCloseSession.HorizontalAlignment = HorizontalAlignment.Right;
            iconCloseSession.FontSize = 14;
            
            ChartTrendLine Session = Chart.DrawTrendLine($"Session{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], lowest, TF_Bars.OpenTimes[TF_idx], highest, Color.FromName(RawColorOHLC.ToString()));
            Session.Thickness = 3;
            
        }
        private void VolP_Bars(int index)
        {
            
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index+1];
                        
            if (IsLastBar)
                endTime = VOL_Bars.Last().OpenTime;
                
            for (int k = 0; k < VOL_Bars.Count; ++k)
            {
                Bar volBar;
                volBar = VOL_Bars[k]; 
                
                if (volBar.OpenTime < startTime || volBar.OpenTime > endTime)
                {
                    if (volBar.OpenTime > endTime)
                        break;
                    else
                        continue;
                }
                /* The Volume Calculation(in Bars Volume Source) is exported, with adaptations, from the BEST VP I have see/used for MT4/MT5, 
                    of Russian FXcoder's https://gitlab.com/fxcoder-mql/vp (VP 10.1), author of the famous (Volume Profile + Range v6.0)
                / I tried to reproduce as close to the original, 
                / I would say it was very good approximation in most core options, 
                / except the "Triangular", witch I had to interpret it my way, and it turned out different, of course.
                / "Parabolic" too but the result turned out good
                */
                bool isBullish = volBar.Close >= volBar.Open;
                if (DistributionInput == DistributionData.OHLC)
                {
                    // ========= Tick Simulation ================
                    // Bull bar 
                    if (volBar.Close >= volBar.Open)
                    {
                        // Average Tick Volume
                        double avgVol = volBar.TickVolume/(volBar.Open + volBar.High + volBar.Low + volBar.Close/4);
                        for (int i = 0; i < allSegmentsPrices.Count; i++)
                        {  
                            double priceKey = allSegmentsPrices[i];
                            if (allSegmentsPrices[i] <= volBar.Open && allSegmentsPrices[i] >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (allSegmentsPrices[i] <= volBar.High && allSegmentsPrices[i] >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (allSegmentsPrices[i] <= volBar.High && allSegmentsPrices[i] >= volBar.Close)
                                AddVolume(priceKey, avgVol, isBullish);
                        }
                    }
                    // Bear bar 
                    else
                    {
                        // Average Tick Volume
                        double avgVol = volBar.TickVolume/(volBar.Open + volBar.High + volBar.Low + volBar.Close/4);
                        for (int i = 0; i < allSegmentsPrices.Count; i++)
                        {  
                            double priceKey = allSegmentsPrices[i];
                            if (allSegmentsPrices[i] >= volBar.Open && allSegmentsPrices[i] <= volBar.High)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (allSegmentsPrices[i] <= volBar.High && allSegmentsPrices[i] >= volBar.Low) 
                                AddVolume(priceKey, avgVol, isBullish);
                            if (allSegmentsPrices[i] >= volBar.Low && allSegmentsPrices[i] <= volBar.Close) 
                                AddVolume(priceKey, avgVol, isBullish);
                        }
                    }
                }
                else if (DistributionInput == DistributionData.High || DistributionInput == DistributionData.Low || DistributionInput == DistributionData.Close)
                {
                    var selected = DistributionInput;
                    if (selected == DistributionData.High) 
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < allSegmentsPrices.Count; i++)
                        {
                            if (allSegmentsPrices[i] >= volBar.High && prevSegment <= volBar.High)
                                AddVolume(allSegmentsPrices[i], volBar.TickVolume, isBullish);
                            prevSegment = allSegmentsPrices[i];
                        }
                    }
                    else if (selected == DistributionData.Low) 
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < allSegmentsPrices.Count; i++)
                        {
                            if (allSegmentsPrices[i] >= volBar.Low && prevSegment <= volBar.Low)
                                AddVolume(allSegmentsPrices[i], volBar.TickVolume, isBullish);
                            prevSegment = allSegmentsPrices[i];
                        }
                    }
                    else
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < allSegmentsPrices.Count; i++)
                        {
                            if (allSegmentsPrices[i] >= volBar.Close && prevSegment <= volBar.Close)
                                AddVolume(allSegmentsPrices[i], volBar.TickVolume, isBullish);
                            prevSegment = allSegmentsPrices[i];
                        }
                    }
                }
                else if (DistributionInput == DistributionData.Uniform_Distribution)
                {    
                    double HL = Math.Abs(volBar.High - volBar.Low);
                    double uniVol = volBar.TickVolume/HL;
                    for (int i = 0; i < allSegmentsPrices.Count; i++)
                    {
                        if (allSegmentsPrices[i] >= volBar.Low && allSegmentsPrices[i] <= volBar.High)
                            AddVolume(allSegmentsPrices[i], uniVol, isBullish);
                    }
                }
                else if (DistributionInput == DistributionData.Uniform_Presence)
                {    
                    double uniP_Vol = 1;
                    for (int i = 0; i < allSegmentsPrices.Count; i++)
                    {
                        if (allSegmentsPrices[i] >= volBar.Low && allSegmentsPrices[i] <= volBar.High)
                            AddVolume(allSegmentsPrices[i], uniP_Vol, isBullish);
                    }
                }
                else if (DistributionInput == DistributionData.Parabolic_Distribution)
                {  
                    double HL = Math.Abs(volBar.High - volBar.Low);
                    double HL2 = HL / 2;
                    double hl2SQRT = Math.Sqrt(HL2);
                    double final = hl2SQRT / hl2SQRT;
                        
                    double parabolicVol = volBar.TickVolume / final;
                    
                    for (int i = 0; i < allSegmentsPrices.Count; i++)
                    {
                        if (allSegmentsPrices[i] >= volBar.Low && allSegmentsPrices[i] <= volBar.High)
                            AddVolume(allSegmentsPrices[i], parabolicVol, isBullish);
                    }
                }
                else if (DistributionInput == DistributionData.Triangular_Distribution)
                {    
                    double HL = Math.Abs(volBar.High - volBar.Low);
                    double HL2 = HL / 2;
                    double HL_minus = HL - HL2;
                    // =====================================
                    double oneStep = HL2 * HL2 / 2;
                    double secondStep = HL_minus * HL_minus / 2;
                    double final = oneStep + secondStep;
                    
                    double triangularVol = volBar.TickVolume / final;

                    for (int i = 0; i < allSegmentsPrices.Count; i++)
                    {
                        if (allSegmentsPrices[i] >= volBar.Low && allSegmentsPrices[i] <= volBar.High)
                            AddVolume(allSegmentsPrices[i], triangularVol, isBullish);
                    }
                }
            }
            
            void AddVolume(double priceKey, double vol, bool isBullish)
            {
                if (!allVolumesRank.ContainsKey(priceKey))
                    allVolumesRank.Add(priceKey, vol);
                else
                    allVolumesRank[priceKey] += vol;
                bool condition = (VolumeModeInput != VolumeModeData.Normal || VolumeModeInput != VolumeModeData.Gradient);
                if (condition)
                    Add_BuySell(priceKey, vol, isBullish);
            }
            void Add_BuySell(double priceKey, double vol, bool isBullish)
            {
                if (isBullish)
                {
                    if (!allVolumesR_Up.ContainsKey(priceKey))
                        allVolumesR_Up.Add(priceKey, vol);
                    else
                        allVolumesR_Up[priceKey] += vol;
                }
                else 
                {
                    if (!allVolumesR_Down.ContainsKey(priceKey))
                        allVolumesR_Down.Add(priceKey, vol);
                    else
                        allVolumesR_Down[priceKey] += vol;
                }  
                
                if (!allDeltaRank.ContainsKey(priceKey))
                {
                    if (allVolumesR_Up.ContainsKey(priceKey) && allVolumesR_Down.ContainsKey(priceKey))
                        allDeltaRank.Add(priceKey, (allVolumesR_Up[priceKey] - allVolumesR_Down[priceKey]));
                    else if (allVolumesR_Up.ContainsKey(priceKey) && !allVolumesR_Down.ContainsKey(priceKey))
                        allDeltaRank.Add(priceKey, (allVolumesR_Up[priceKey]));
                    else if (!allVolumesR_Up.ContainsKey(priceKey) && allVolumesR_Down.ContainsKey(priceKey))
                        allDeltaRank.Add(priceKey, (-allVolumesR_Down[priceKey]));
                    else
                        allDeltaRank.Add(priceKey, 0);
                }
                else
                {
                    if (allVolumesR_Up.ContainsKey(priceKey) && allVolumesR_Down.ContainsKey(priceKey))
                        allDeltaRank[priceKey] += (allVolumesR_Up[priceKey] - allVolumesR_Down[priceKey]);
                    else if (allVolumesR_Up.ContainsKey(priceKey) && !allVolumesR_Down.ContainsKey(priceKey))
                        allDeltaRank[priceKey] += (allVolumesR_Up[priceKey]);
                    else if (!allVolumesR_Up.ContainsKey(priceKey) && allVolumesR_Down.ContainsKey(priceKey))
                        allDeltaRank[priceKey] += (-allVolumesR_Down[priceKey]);
                    
                }
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
                    if (tickPrice >= prev_segmentValue && tickPrice <= allSegmentsPrices[i])
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
        private void DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.LightBlue);
        }
        private void Second_DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt2", $"{Msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }
        private void Third_DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt3", $"{Msg}", VerticalAlignment.Top, HorizontalAlignment.Right, Color.Yellow);
        }
        private void TickVolumeInitialize()
        {
            if (LoadFromInput == LoadFromData.Custom)
            {
                // ==== Get datetime to load from: dd/mm/yyyy ====
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out FromDateTime))
                {
                    if (FromDateTime > Server.Time.Date) {
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
                if (LoadFromInput != LoadFromData.According_to_Lookback) 
                {
                    DateTime LastBarTime = Bars.LastBar.OpenTime.Date;
                    if (LoadFromInput == LoadFromData.Today)
                        FromDateTime = LastBarTime.Date;
                    else if (LoadFromInput == LoadFromData.Yesterday)
                        FromDateTime = LastBarTime.AddDays(-1);
                    else if (LoadFromInput == LoadFromData.One_Week)
                        FromDateTime = LastBarTime.AddDays(-5);
                    
                    FromDateTime = FromDateTime.AddDays(-1).AddHours(21);
                }
                else
                    FromDateTime = LookBack_Bars.OpenTimes[LookBack_Bars.ClosePrices.Count - Lookback];
                    
            }

            // ==== Check if existing ticks data on the chart really needs more data ====
            DateTime FirstTickTime = _TicksOHLC.OpenTimes.FirstOrDefault();
            if (FirstTickTime >= FromDateTime) {
                LoadMoreTicks(FromDateTime);
                DrawOnScreen("Data Collection Finished \n Calculating...");
            }
            else {
                Print($"Using existing tick data from '{FirstTickTime}'");
                DrawOnScreen($"Using existing tick data from '{FirstTickTime}' \n Calculating...");
            }
            try {
            FirstTickTime = _TicksOHLC.OpenTimes.FirstOrDefault();
            ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeStart", FirstTickTime, Color.Red);
            lineInfo.LineStyle = LineStyle.Lines;
            ChartText textInfo = Chart.DrawText($"VolumeStartText", $"Tick Volume Data \n ends here", FirstTickTime, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(FirstTickTime)], Color.Red);
            textInfo.FontSize = 8;
            } catch {};
        }
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
        private double[] VA_Calculation()
        {
        /*  https://onlinelibrary.wiley.com/doi/pdf/10.1002/9781118659724.app1
            https://www.mypivots.com/dictionary/definition/40/calculating-market-profile-value-area 
            Same of TPO Profile(https://ctrader.com/algos/indicators/show/3074)  */

            double largestVOL = allVolumesRank.Values.Max();
            
            double totalvol = allVolumesRank.Values.Sum();
            double _70percent = Math.Round((70 * totalvol) / 100);
            
            double priceLVOL = 0;
            for (int k = 0; k < allVolumesRank.Count; k++)
            {
                if (allVolumesRank.ElementAt(k).Value == largestVOL)
                {
                    priceLVOL = allVolumesRank.ElementAt(k).Key;
                    break;
                }
            }
            double priceVAH = 0;
            double priceVAL = 0;
            
            double sumVA = largestVOL;
                        
            List<double> upKeys = new List<double>();
            List<double> downKeys = new List<double>();
            for (int i = 0; i < allSegmentsPrices.Count; i++)
            {
                double priceKey = allSegmentsPrices[i];
                
                if (allVolumesRank.ContainsKey(priceKey))
                {
                    if (priceKey < priceLVOL)
                        downKeys.Add(priceKey);
                    else if (priceKey > priceLVOL)
                        upKeys.Add(priceKey);
                }
            }
            upKeys.Sort();
            downKeys.Sort();
            downKeys.Reverse();
            
            double[] withoutVA = {priceLVOL-(rowHeight*2), priceLVOL+drawHeight, priceLVOL};
            if (upKeys.Count == 0 || downKeys.Count == 0)
                return withoutVA;

            double[] prev2UP = {0, 0};
            double[] prev2Down = {0, 0};
            
            bool lockAbove = false;
            double[] aboveKV = {0, 0};
            
            bool lockBelow = false;
            double[] belowKV = {0, 0};

            for (int i = 0; i < allVolumesRank.Keys.Count; i++)
            {            
                if (sumVA >= _70percent)
                    break;

                double sumUp = 0;
                double sumDown = 0;
                
                // ========= Above of POC =========
                double prevUPkey = upKeys.First();
                double keyUP = 0;
                foreach (double key in upKeys)
                {
                    if (upKeys.Count == 1 || prev2UP[0] != 0 && prev2UP[1] != 0 && key == upKeys.Last())
                    {
                        sumDown = allVolumesRank[key];
                        keyUP = key;
                        break;
                    }
                    if (lockAbove)
                    {
                        keyUP = aboveKV[0];
                        sumUp = aboveKV[1];
                        break;
                    }
                    if (prev2UP[0] == 0 && prev2UP[1] == 0 && key != prevUPkey 
                    || prev2UP[0] != 0 && prev2UP[1] != 0 && prevUPkey > aboveKV[0] && key > aboveKV[0])
                    {
                        double upVOL = allVolumesRank[key];
                        double up2VOL = allVolumesRank[prevUPkey];
                        
                        keyUP = key;
                        
                        double[] _2up = {prevUPkey, keyUP};
                        prev2UP = _2up;
                        
                        double[] _above = {keyUP, upVOL + up2VOL};
                        aboveKV = _above;
                        
                        sumUp = upVOL + up2VOL;
                        break;
                    }
                    prevUPkey = key;
                }

                // ========= Below of POC =========
                double prevDownkey = downKeys.First();
                double keyDw = 0;
                foreach (double key in downKeys)
                {
                    if (downKeys.Count == 1 || prev2Down[0] != 0 && prev2Down[1] != 0 && key == downKeys.Last())
                    {
                        sumDown = allVolumesRank[key];
                        keyDw = key;
                        break;
                    }
                    if (lockBelow)
                    {
                        keyDw = belowKV[0];
                        sumDown = belowKV[1];
                        break;
                    }
                    if (prev2Down[0] == 0 && prev2Down[1] == 0 && key != prevDownkey 
                    || prev2Down[0] != 0 && prev2Down[1] != 0 && prevDownkey < aboveKV[0] && key < belowKV[0])
                    {
                        double downVOL = allVolumesRank[key];
                        double down2VOL = allVolumesRank[prevDownkey];
                        
                        keyDw = key;
                        
                        double[] _2down = {prevDownkey, keyDw};
                        prev2Down = _2down;
                        
                        double[] _below = {keyDw, downVOL + down2VOL};
                        belowKV = _below;
                        
                        sumDown = downVOL + down2VOL;
                        break;
                    }
                    prevDownkey = key;
                }

                // ========= VA rating ========= 
                if (sumUp > sumDown)
                {
                    sumVA += sumUp;
                    priceVAH = keyUP;
                    priceVAL = keyDw;
                    
                    lockBelow = true;
                    lockAbove = false;
                }
                else if (sumDown > sumUp)
                {                    
                    sumVA += sumDown;
                    priceVAH = keyUP;
                    priceVAL = keyDw;
                    
                    lockBelow = false;
                    lockAbove = true;
                }
                else if (sumUp == sumDown)
                {
                    double[] _2up = {prevUPkey, keyUP};
                    prev2UP = _2up;
                    double[] _2down = {prevDownkey, keyDw};
                    prev2Down = _2down;
                    
                    sumVA += (sumUp + sumDown);
                    priceVAH = keyUP;
                    priceVAL = keyDw;
                    
                    lockBelow = false;
                    lockAbove = false;
                }
            }
            
            double[] VA = {priceVAL, priceVAH, priceLVOL}; 

            return VA;
        }
    }
}