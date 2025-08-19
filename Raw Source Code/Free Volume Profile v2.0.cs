/*
--------------------------------------------------------------------------------------------------------------------------------
                        Volume Profile v2.0
                            revision 1

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

.NET 6.0+ is Required

What's new in v2.0?
-Added Params Panel for quickly switch between settings (volume modes, row height, interval, etc) and most importantly, more user-friendly.
-Refactor to only use Colors API.
-Should work with Mac OS users.

What's new in rev. 1?
-Fix => Indicator's objects being randomly removed right after switching Panel settings
-Fix => Lookback doesn't load more historical data
-Fix => Real-time profile differs from historical profile of the same period
-Row Height in Pips!
-Custom Format (k, M) for Big Numbers (n >= 1000)!
-Change histograms side to Right (Delta Profile) in Normal+Delta mode
-Bars Distribution => OHLC_No_Avg and Open
-Buy vs Sell => Sum, Subtract and Divide total values of each side
-Delta => Total, Min, Max, Subtract (min - max) Delta

Last update => 14/08/2025

AUTHOR: srlcarlg

== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using static cAlgo.FreeVolumeProfileV20;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class FreeVolumeProfileV20 : Indicator
    {
        public enum PanelAlignData
        {
            Top_Left,
            Top_Center,
            Top_Right,
            Center_Left,
            Center_Right,
            Bottom_Left,
            Bottom_Center,
            Bottom_Right,
        }
        [Parameter("Panel Position:", DefaultValue = PanelAlignData.Bottom_Right, Group = "==== Volume Profile v2.0 ====")]
        public PanelAlignData PanelAlignInput { get; set; }

        public enum VolumeSourceData
        {
            Ticks,
            Bars,
        }
        [Parameter("Volume Source:", DefaultValue = VolumeSourceData.Bars, Group = "==== Volume Profile v2.0 ====")]
        public VolumeSourceData VolumeSourceInput { get; set; }


        public enum ConfigRowData
        {
            Predefined,
            Custom,
        }
        [Parameter("Config:", DefaultValue = ConfigRowData.Predefined, Group = "==== Config ====")]
        public ConfigRowData ConfigRowInput { get; set; }

        [Parameter("Custom Interval:", DefaultValue = "Daily", Group = "==== Config ====")]
        public TimeFrame CustomInterval { get; set; }

        [Parameter("Custom Row Height:", DefaultValue = 1, MinValue = 0.2, Group = "==== Config ====")]
        public double CustomHeight { get; set; }


        [Parameter("[Bars]Source:", DefaultValue = "Minute", Group = "==== Volume Source ====")]
        public TimeFrame BarsVOLSource_TF { get; set; }

        public enum DistributionData
        {
            OHLC,
            OHLC_No_Avg,
            Open,
            High,
            Low,
            Close,
            Uniform_Distribution,
            Uniform_Presence,
            Parabolic_Distribution,
            Triangular_Distribution,
        }
        [Parameter("[Bars]Distribution:", DefaultValue = DistributionData.OHLC, Group = "==== Volume Source ====")]
        public DistributionData DistributionInput { get; set; }

        public enum LoadFromData
        {
            According_to_Lookback,
            Today,
            Yesterday,
            One_Week,
            Custom
        }
        [Parameter("[Ticks]Load From:", DefaultValue = LoadFromData.Today, Group = "==== Volume Source ====")]
        public LoadFromData LoadFromInput { get; set; }

        [Parameter("[Ticks]Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== Volume Source ====")]
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
        public HistSideData HistogramSideInput { get; set; }

        [Parameter("Fill Histogram?", DefaultValue = true, Group = "==== Histogram Settings ====")]
        public bool FillHist { get; set; }

        [Parameter("[Gradient] Opacity:", DefaultValue = 60, MinValue = 5, MaxValue = 100, Group = "==== Histogram Settings ====")]
        public int OpacityHistInput { get; set; }


        [Parameter("Extended VAs?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ExtendVA { get; set; }

        [Parameter("Show OHLC Bar?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ShowOHLC { get; set; }

        [Parameter("Show Results?", DefaultValue = true, Group = "==== Other settings ====")]
        public bool ShowResults { get; set; }

        [Parameter("Format Results?", DefaultValue = true, Group = "==== Other settings ====")]
        public bool FormatResults { get; set; }
        
        public enum FormatMaxDigits_Data
        {
            Zero,
            One,
            Two,
        }
        [Parameter("Format Max Digits:", DefaultValue = FormatMaxDigits_Data.One, Group = "==== Other settings ====")]
        public FormatMaxDigits_Data FormatMaxDigits_Input { get; set; }

        public enum BuySellResult_Data
        {
            Sum,
            Subtract,
            Divide
        }
        [Parameter("[Buy_Sell] Show Total:", DefaultValue = BuySellResult_Data.Divide, Group = "==== Other settings ====")]
        public BuySellResult_Data BuySellResult_Input { get; set; }

        [Parameter("Font Size Results:", DefaultValue = 10, MinValue = 1, MaxValue = 80, Group = "==== Other settings ====")]
        public int FontSizeResults { get; set; }


        [Parameter("Normal Color:", DefaultValue = "#99808080", Group = "==== Colors Histogram ====")]
        public Color HistColor  { get; set; }

        [Parameter("Normal Color inside VA:", DefaultValue = "#CC00BFFF", Group = "==== Colors Histogram ====")]
        public Color HistColorVA  { get; set; }

        [Parameter("Gradient Color Min. Vol:", DefaultValue = "RoyalBlue", Group = "==== Colors Histogram ====")]
        public Color ColorGrandient_Min { get; set; }

        [Parameter("Gradient Color Max. Vol:", DefaultValue = "OrangeRed", Group = "==== Colors Histogram ====")]
        public Color ColorGrandient_Max { get; set; }

        [Parameter("Color Buy:", DefaultValue = "#9900BFFF", Group = "==== Colors Histogram ====")]
        public Color BuyColor  { get; set; }

        [Parameter("Color Sell:", DefaultValue = "#99DC143C", Group = "==== Colors Histogram ====")]
        public Color SellColor  { get; set; }

        [Parameter("OHLC Bar Color:", DefaultValue = "Gray", Group = "==== Colors Histogram ====")]
        public Color ColorOHLC { get; set; }


        [Parameter("Color POC:", DefaultValue = "D0FFD700", Group = "==== Point of Control ====")]
        public Color ColorPOC { get; set; }

        [Parameter("LineStyle POC:", DefaultValue = LineStyle.Lines, Group = "==== Point of Control ====")]
        public LineStyle LineStylePOC { get; set; }

        [Parameter("Thickness POC:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Point of Control ====")]
        public int ThicknessPOC { get; set; }


        [Parameter("Color VA:", DefaultValue = "#19F0F8FF", Group = "==== Value Area ====")]
        public Color VAColor  { get; set; }

        [Parameter("Color VAH:", DefaultValue = "PowderBlue", Group = "==== Value Area ====")]
        public Color ColorVAH { get; set; }

        [Parameter("Color VAL:", DefaultValue = "PowderBlue", Group = "==== Value Area ====")]
        public Color ColorVAL { get; set; }

        [Parameter("Opacity VA", DefaultValue = 10, MinValue = 5, MaxValue = 100, Group = "==== Value Area ====")]
        public int OpacityVA { get; set; }

        [Parameter("LineStyle VA:", DefaultValue = LineStyle.LinesDots, Group = "==== Value Area ====")]
        public LineStyle LineStyleVA { get; set; }

        [Parameter("Thickness VA:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Value Area ====")]
        public int ThicknessVA { get; set; }


        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }

        private readonly VerticalAlignment V_Align = VerticalAlignment.Top;
        private readonly HorizontalAlignment H_Align = HorizontalAlignment.Center;

        private List<double> Segments = new();
        private readonly IDictionary<double, double> VolumesRank = new Dictionary<double, double>();
        private readonly IDictionary<double, double> VolumesRank_Up = new Dictionary<double, double>();
        private readonly IDictionary<double, double> VolumesRank_Down = new Dictionary<double, double>();
        private readonly IDictionary<double, double> DeltaRank = new Dictionary<double, double>();
        private double[] MinMaxDelta = { 0, 0 };

        private readonly List<ChartTrendLine> POCsLines = new();
        private readonly List<ChartTrendLine> VALines = new();

        private readonly IDictionary<int, ChartRectangle> RectanglesToColor = new Dictionary<int, ChartRectangle>();

        private DateTime FromDateTime;
        private TimeFrame LookBack_TF;
        private Bars LookBack_Bars;
        private Bars TicksOHLC;
        private Bars VOL_Bars;

        private double HeightPips = 4;
        private double rowHeight = 0;
        private double prevPrice;
        private double[] priceVA_LHP = { 0, 0, 0 };

        private bool isLive = false;
        private bool Wrong = false;
        private bool configHasChanged = false;

        private int cleanedIndex;

        // Moved from cTrader Input to Params Panel
        public int Lookback { get; set; } = 5;
        public enum VolumeModeData
        {
            Normal,
            Gradient,
            Buy_Sell,
            Delta,
            Normal_Delta
        }
        public VolumeModeData VolumeModeInput { get; set; } = VolumeModeData.Gradient;
        public bool ShowVA { get; set; } = false;
        public bool KeepPOC { get; set; } = true;
        public bool ExtendPOC { get; set; } = false;

        // Params Panel
        private Border ParamBorder;
        public class IndicatorParams
        {
            public int LookBack { get; set; }
            public VolumeModeData VolMode { get; set; }
            public double RowHeight { get; set; }
            public TimeFrame Interval { get; set; }
            public bool KeepPOC { get; set; }
            public bool ExtendedPOC { get; set; }
            public bool ShowVA { get; set; }
        }

        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "VP",
                Padding = 0,
                Height = 22,
                Margin = 2,
                BackgroundColor = btnColor
            };
            button.Click += HiddenEvent;
            panel.AddChild(button);
        }
        private void HiddenEvent(ButtonClickEventArgs obj)
        {
            if (ParamBorder.IsVisible)
                ParamBorder.IsVisible = false;
            else
                ParamBorder.IsVisible = true;
        }

        protected override void Initialize()
        {
            // ========== Predefined Config ==========
            if (ConfigRowInput == ConfigRowData.Predefined && (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Day3))
            {
                if (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Minute4)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute)
                        LookBack_TF = TimeFrame.Hour;
                    else if (Chart.TimeFrame == TimeFrame.Minute2)
                        LookBack_TF = TimeFrame.Hour2;
                    else if (Chart.TimeFrame <= TimeFrame.Minute4)
                        LookBack_TF = TimeFrame.Hour3;

                    SetHeightPips(0.5, 8);
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute5)
                        LookBack_TF = TimeFrame.Hour4;
                    else if (Chart.TimeFrame == TimeFrame.Minute6)
                        LookBack_TF = TimeFrame.Hour6;
                    else if (Chart.TimeFrame <= TimeFrame.Minute8)
                        LookBack_TF = TimeFrame.Hour8;
                    else if (Chart.TimeFrame <= TimeFrame.Minute10)
                        LookBack_TF = TimeFrame.Hour12;

                    SetHeightPips(0.5, 25);
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour) {
                        LookBack_TF = TimeFrame.Daily;
                        SetHeightPips(2, 50);
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Hour8) {
                        LookBack_TF = TimeFrame.Weekly;
                        SetHeightPips(4, 140);
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Day3)
                {
                    LookBack_TF = TimeFrame.Monthly;
                    SetHeightPips(6, 220);
                }
            }
            else
            {
                string[] timeBased = { "Minute", "Hour", "Daily", "Day" };
                if (ConfigRowInput == ConfigRowData.Predefined)
                {
                    string Msg = "'Predefined Config' is designed only for Standard Timeframe (Minutes, Hours, Days) \n Weekly and Monthly is not currently supported \n\n use 'Custom Config' to others Chart Timeframes (Renko/Range/Ticks).";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                if (!timeBased.Any(CustomInterval.Name.ToString().Contains))
                {
                    string Msg = $"Weekly and Monthly 'Interval' should have 'Bars Source' above Minutes";
                    if (BarsVOLSource_TF.Name.Contains("Minute")) {
                        Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                        Wrong = true;
                        return;
                    }
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
                LookBack_TF = CustomInterval;
                HeightPips = CustomHeight;
            }

            void SetHeightPips(double digits5, double digits2)
            {
                if (Symbol.Digits == 5 || Symbol.Digits == 1)
                    HeightPips = digits5;
                else
                    HeightPips = digits2;
            }
            string[] timesBased = { "Minute", "Hour", "Daily", "Day" };
            if (!timesBased.Any(BarsVOLSource_TF.Name.ToString().Contains))
            {
                string Msg = $"'Bars Volume Source' is designed ONLY for: \n (Minutes, Hours, Days) \n Weekly and Monthly is not currently supported";
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

            LookBack_Bars = MarketData.GetBars(LookBack_TF);
            if (LookBack_Bars.ClosePrices.Count < Lookback)
            {
                while (LookBack_Bars.ClosePrices.Count < Lookback)
                {
                    int loadedCount = LookBack_Bars.LoadMoreHistory();
                    Print($"Loaded {loadedCount}, {LookBack_TF.ShortName} LookBack Bars, Current Bar Date: {LookBack_Bars.OpenTimes.FirstOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
            }

            if (VolumeSourceInput == VolumeSourceData.Ticks)
                TicksOHLC = MarketData.GetBars(TimeFrame.Tick);
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
                try
                {
                    DateTime FirstVolDate = VOL_Bars.OpenTimes.FirstOrDefault();
                    ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeStart", FirstVolDate, Color.Red);
                    lineInfo.LineStyle = LineStyle.Lines;
                    ChartText textInfo = Chart.DrawText($"VolumeStartText", $"{BarsVOLSource_TF.ShortName} Volume Data \n ends here", FirstVolDate, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(FirstVolDate)], Color.Red);
                    textInfo.FontSize = 8;
                }
                catch { };
            }
            else
                TickVolumeInitialize();

            // Ex: 4 pips to VOL Distribuition(rowHeight)
            rowHeight = Symbol.PipSize * HeightPips;

            DrawOnScreen("Calculating...");
            string nonTimeBased = !timesBased.Any(Chart.TimeFrame.ToString().Contains) ? "Ticks/Renko/Range with 100% Histogram Width \n sometimes is recommended" : "";
            string ticksInfo = $"Ticks Volume Source: \n 1) Naturally heavier at 1 tick \n 2) Large 'Lookback' or 'Tick Data' takes longer to calculate \n 3) Recommended for intraday only";
            string showTicksInfo = VolumeSourceInput == VolumeSourceData.Ticks ? ticksInfo : "";
            Second_DrawOnScreen($"Taking too long? You can: \n 1) Increase the rowHeight \n 2) Disable the Value Area (High Performance)\n\n {nonTimeBased} \n\n {showTicksInfo}");
            if (Application.UserTimeOffset.ToString() != "03:00:00")
                Third_DrawOnScreen("Set your UTC to UTC+3");

            // PARAMS PANEL
            VerticalAlignment vAlign = VerticalAlignment.Bottom;
            HorizontalAlignment hAlign = HorizontalAlignment.Right;

            if (PanelAlignInput == PanelAlignData.Bottom_Left)
                hAlign = HorizontalAlignment.Left;
            else if (PanelAlignInput == PanelAlignData.Top_Left)
                vAlign = VerticalAlignment.Top;
            else if (PanelAlignInput == PanelAlignData.Top_Right) {
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Right;
            } else if (PanelAlignInput == PanelAlignData.Center_Right) {
                vAlign = VerticalAlignment.Center;
                hAlign = HorizontalAlignment.Right;
            } else if (PanelAlignInput == PanelAlignData.Center_Left) {
                vAlign = VerticalAlignment.Center;
                hAlign = HorizontalAlignment.Left;
            } else if (PanelAlignInput == PanelAlignData.Top_Center) {
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Center;
            } else if (PanelAlignInput == PanelAlignData.Bottom_Center) {
                vAlign = VerticalAlignment.Bottom;
                hAlign = HorizontalAlignment.Center;
            }

            IndicatorParams DefaultParams = new()
            {
                LookBack = Lookback,
                VolMode = VolumeModeInput,
                RowHeight = HeightPips,
                Interval = LookBack_TF,
                KeepPOC = KeepPOC,
                ExtendedPOC = ExtendPOC,
                ShowVA = ShowVA
            };

            ParamsPanel ParamPanel = new(this, DefaultParams);
            Border borderParam = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                Width = 225,
                Child = ParamPanel
            };
            Chart.AddControl(borderParam);
            ParamBorder = borderParam;

            var wrapPanel = new WrapPanel
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
            };
            AddHiddenButton(wrapPanel, Color.Gray);
            Chart.AddControl(wrapPanel);
        }

        public override void Calculate(int index)
        {
            if (Wrong)
                return;

            // ==== Removing Messages ====
            if (!IsLastBar)
            {
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
            if (ExtendPOC && POCsLines.Count != 0 && POCsLines.FirstOrDefault().Y2 != priceVA_LHP[2])
            {
                for (int tl = 0; tl < POCsLines.Count; tl++)
                {
                    POCsLines[tl].Time2 = Bars.OpenTimes[index];
                    string dynDate = LookBack_TF == TimeFrame.Daily ? POCsLines[tl].Time1.Date.AddDays(1).ToString().Replace("00:00:00", "") : POCsLines[tl].Time1.Date.ToString();
                    Chart.DrawText($"POC{POCsLines[tl].Time1}", $"{dynDate}", Bars.OpenTimes[index], POCsLines[tl].Y2 + (rowHeight / 2), ColorPOC);
                }
            }

            if (ExtendVA && VALines.Count != 0 && VALines.FirstOrDefault().Time2 != Bars.OpenTimes[index])
            {
                for (int tl = 0; tl < VALines.Count; tl++)
                    VALines[tl].Time2 = Bars.OpenTimes[index];
            }

            // === Clean Dicts/others ===
            if (index == indexStart && index != cleanedIndex || (index - 1) == indexStart && (index - 1) != cleanedIndex)
            {
                Segments.Clear();
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                double[] resetDelta = {0, 0};
                MinMaxDelta = resetDelta;
                RectanglesToColor.Clear();
                double[] VAforColor = { 0, 0, 0 };
                priceVA_LHP = VAforColor;
                cleanedIndex = index == indexStart ? index : (index - 1);
            }
            // Historical data
            if (!IsLastBar)
            {
                if (!isLive)
                    VolumeProfile(indexStart, index);
            }
            else
            {
                isLive = true;
                // "Repaint" if the price moves half of rowHeight
                if (Bars.ClosePrices[index] >= (prevPrice + (rowHeight / 2)) || Bars.ClosePrices[index] <= (prevPrice - (rowHeight / 2)) || configHasChanged)
                {
                    for (int i = indexStart; i <= index; i++)
                    {
                        if (i == indexStart)
                        {
                            Segments.Clear();
                            VolumesRank.Clear();
                            VolumesRank_Up.Clear();
                            VolumesRank_Down.Clear();
                            DeltaRank.Clear();
                            double[] resetDelta = {0, 0};
                            MinMaxDelta = resetDelta;
                            RectanglesToColor.Clear();
                        }

                        VolumeProfile(indexStart, i);
                    }
                    prevPrice = Bars.ClosePrices[index];
                    configHasChanged = false;
                }
            }
        }

        private void VolumeProfile(int iStart, int index)
        {
            // ======= Highest and Lowest =======
            Bars TF_Bars = LookBack_Bars;
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

            double highest = TF_Bars.HighPrices[TF_idx], lowest = TF_Bars.LowPrices[TF_idx], open = TF_Bars.OpenPrices[TF_idx];

            // ======= Chart Segmentation =======
            List<double> currentSegments = new();
            double prev_segment = open;
            while (prev_segment >= (lowest - rowHeight))
            {
                currentSegments.Add(prev_segment);
                prev_segment = Math.Abs(prev_segment - rowHeight);
            }
            prev_segment = open;
            while (prev_segment <= (highest + rowHeight))
            {
                currentSegments.Add(prev_segment);
                prev_segment = Math.Abs(prev_segment + rowHeight);
            }
            Segments = currentSegments.OrderBy(x => x).ToList();

            // ======= VP =======
            if (VolumeSourceInput == VolumeSourceData.Ticks)
                VP_Tick(index);
            else
                VP_Bars(index);

            // ======= Drawing =======
            if (Segments.Count == 0)
                return;

            for (int i = 0; i < Segments.Count; i++)
            {
                double priceKey = Segments[i];
                if (!VolumesRank.ContainsKey(priceKey))
                    continue;

                /*
                Indeed, the value of X-Axis is simply a rule of three,
                where the maximum value will be the maxLength (in Milliseconds),
                from there the math adjusts the histograms.

                    MaxValue    maxLength(ms)
                       x             ?(ms)

                The values 1.25 and 4 are the manually set values
                */

                double lowerSegment = Segments[i] - rowHeight;
                double upperSegment = Segments[i];

                double largestVOL = VolumesRank.Values.Max();

                double maxLength = Bars[index].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                var selected = HistWidthInput;
                double maxWidth = selected == HistWidthData._15 ? 1.25 : selected == HistWidthData._30 ? 1.50 : selected == HistWidthData._50 ? 2 : 4;
                double proportion = VolumesRank[priceKey] * (maxLength - (maxLength / maxWidth));
                if (selected == HistWidthData._100)
                    proportion = VolumesRank[priceKey] * maxLength;

                double dynLength = proportion / largestVOL;

                Color dynColor = HistColor;
                if (VolumeModeInput == VolumeModeData.Gradient)
                {

                    double Intensity = (VolumesRank[priceKey] * 100 / largestVOL) / 100;
                    double stepR = (ColorGrandient_Max.R - ColorGrandient_Min.R) * Intensity;
                    double stepG = (ColorGrandient_Max.G - ColorGrandient_Min.G) * Intensity;
                    double stepB = (ColorGrandient_Max.B - ColorGrandient_Min.B) * Intensity;

                    int A = (int)(2.55 * OpacityHistInput);
                    int R = (int)Math.Round(ColorGrandient_Min.R + stepR);
                    int G = (int)Math.Round(ColorGrandient_Min.G + stepG);
                    int B = (int)Math.Round(ColorGrandient_Min.B + stepB);

                    dynColor = Color.FromArgb(A, R, G, B);
                }

                if (VolumeModeInput == VolumeModeData.Normal || VolumeModeInput == VolumeModeData.Normal_Delta || VolumeModeInput == VolumeModeData.Gradient)
                {
                    ChartRectangle volHist;
                    volHist = Chart.DrawRectangle($"{iStart}_{i}_", Bars.OpenTimes[iStart], lowerSegment, Bars.OpenTimes[iStart].AddMilliseconds(dynLength), upperSegment, dynColor);

                    if (RectanglesToColor.ContainsKey(i))
                        RectanglesToColor[i] = volHist;
                    else
                        RectanglesToColor.Add(i, volHist);

                    if (FillHist)
                        volHist.IsFilled = true;
                    if (HistogramSideInput == HistSideData.Right)
                    {
                        volHist.Time1 = Bars.OpenTimes[index];
                        volHist.Time2 = Bars.OpenTimes[index].AddMilliseconds(-dynLength);
                    }

                    if (ShowResults)
                    {
                        ChartText Center;
                        double sum = Math.Round(VolumesRank.Values.Sum());
                        string strValue = FormatResults ? FormatBigNumber(sum) : $"{sum}";

                        Color centerColor = (VolumeModeInput == VolumeModeData.Normal || VolumeModeInput == VolumeModeData.Normal_Delta) ? ColorOHLC : ColorGrandient_Min;
                        Center = Chart.DrawText($"{iStart}NormalResult", $"\n{strValue}", Bars.OpenTimes[iStart], lowest, centerColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;

                        if (HistogramSideInput == HistSideData.Right) {
                            Center.Time = Bars.OpenTimes[index];
                        }
                    }
                }
                if (VolumeModeInput == VolumeModeData.Buy_Sell)
                {
                    // Buy vs Sell = Pseudo Delta
                    double buy_Volume = 0;
                    try { buy_Volume = VolumesRank_Up.Values.Max(); } catch { };
                    double sell_Volume = 0;
                    try { sell_Volume = VolumesRank_Down.Values.Max(); } catch { };
                    double sideVolMax = buy_Volume > sell_Volume ? buy_Volume : sell_Volume;

                    double maxHalfWidth = selected == HistWidthData._15 ? 1.12 : selected == HistWidthData._30 ? 1.25 : selected == HistWidthData._50 ? 1.40 : 1.75;

                    double proportion_Up = 0;
                    try { proportion_Up = VolumesRank_Up[priceKey] * (maxLength - (maxLength / maxHalfWidth)); } catch { };
                    if (selected == HistWidthData._100)
                        try { proportion_Up = VolumesRank_Up[priceKey] * (maxLength - (maxLength / 3)); } catch { };

                    double dynLength_Up = proportion_Up / sideVolMax; ;

                    double proportion_Down = 0;
                    try { proportion_Down = VolumesRank_Down[priceKey] * (maxLength - (maxLength / maxWidth)); } catch { };
                    if (selected == HistWidthData._100)
                        try { proportion_Down = VolumesRank_Down[priceKey] * maxLength; } catch { };

                    double dynLength_Down = proportion_Down / sideVolMax;

                    ChartRectangle buyHist, sellHist;
                    if (VolumesRank_Down.ContainsKey(priceKey) && VolumesRank_Up.ContainsKey(priceKey))
                    {
                        sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Down), upperSegment, SellColor);
                        buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Up), upperSegment, BuyColor);
                        if (FillHist)
                        {
                            buyHist.IsFilled = true; sellHist.IsFilled = true;
                        }
                        if (HistogramSideInput == HistSideData.Right)
                        {
                            sellHist.Time1 = Bars.OpenTimes[index];
                            sellHist.Time2 = Bars.OpenTimes[index].AddMilliseconds(-dynLength_Down);
                            buyHist.Time1 = Bars.OpenTimes[index];
                            buyHist.Time2 = Bars.OpenTimes[index].AddMilliseconds(-dynLength_Up);
                        }
                    }
                    if (ShowResults)
                    {
                        double volBuy = VolumesRank_Up.Values.Sum();
                        double volSell = VolumesRank_Down.Values.Sum();
                        double percentBuy = (volBuy * 100) / (volBuy + volSell);
                        double percentSell = (volSell * 100) / (volBuy + volSell);

                        ChartText Left, Right;
                        Left = Chart.DrawText($"{iStart}SellSum", $"{Math.Round(percentSell)}%", Bars.OpenTimes[iStart], lowest, SellColor);
                        Right = Chart.DrawText($"{iStart}BuySum", $"{Math.Round(percentBuy)}%", Bars.OpenTimes[iStart], lowest, BuyColor); 
                        Left.HorizontalAlignment = HorizontalAlignment.Left; Left.FontSize = FontSizeResults;
                        Right.HorizontalAlignment = HorizontalAlignment.Right; Right.FontSize = FontSizeResults;

                        ChartText Center;
                        double sum = Math.Round(volBuy + volSell);
                        double subtract = Math.Round(volBuy - volSell);
                        double divide = 0;
                        if (volBuy != 0 && volSell != 0)
                            divide = Math.Round(volBuy / volSell, 3);

                        Color centerColor = Math.Round(percentBuy) > Math.Round(percentSell) ? BuyColor : SellColor;
                        if (BuySellResult_Input == BuySellResult_Data.Sum)
                            Center = Chart.DrawText($"{iStart}BuySellResult", $"\n{(FormatResults ? FormatBigNumber(sum) : sum)}", Bars.OpenTimes[iStart], lowest, centerColor);
                        else if (BuySellResult_Input == BuySellResult_Data.Subtract) {
                            string subtractFmtd = subtract > 0 ? FormatBigNumber(subtract) : $"-{FormatBigNumber(Math.Abs(subtract))}";
                            Center = Chart.DrawText($"{iStart}BuySellResult", $"\n{(FormatResults ? subtractFmtd : subtract)}", Bars.OpenTimes[iStart], lowest, centerColor);
                        }
                        else
                            Center = Chart.DrawText($"{iStart}BuySellResult", $"\n{divide}", Bars.OpenTimes[iStart], lowest, centerColor);

                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;

                        if (HistogramSideInput == HistSideData.Right)
                        {
                            Right.Time = Bars.OpenTimes[index];
                            Left.Time = Bars.OpenTimes[index];
                            Center.Time = Bars.OpenTimes[index];
                        }
                    }
                }
                else if (VolumeModeInput == VolumeModeData.Delta || VolumeModeInput == VolumeModeData.Normal_Delta)
                {
                    // Delta
                    double Positive_Delta = DeltaRank.Values.Max();
                    IEnumerable<double> allNegative = DeltaRank.Values.Where(n => n < 0);
                    double Negative_Delta = 0;
                    try { Negative_Delta = Math.Abs(allNegative.Min()); } catch { }

                    double deltaMax = Positive_Delta > Negative_Delta ? Positive_Delta : Negative_Delta;

                    double proportion_Delta = Math.Abs(DeltaRank[priceKey]) * (maxLength - (maxLength / maxWidth));
                    if (selected == HistWidthData._100)
                        proportion_Delta = Math.Abs(DeltaRank[priceKey]) * maxLength;
                    double dynLength_Delta = proportion_Delta / deltaMax;

                    ChartRectangle deltaHist;
                    try
                    {
                        if (DeltaRank[priceKey] >= 0)
                            deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, BuyColor);
                        else
                            deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, SellColor);
                    }
                    catch
                    {
                        deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, HistColor);
                    }
                    if (FillHist)
                        deltaHist.IsFilled = true;
                    if (HistogramSideInput == HistSideData.Right || VolumeModeInput == VolumeModeData.Normal_Delta)
                    {
                        deltaHist.Time1 = Bars.OpenTimes[index];
                        deltaHist.Time2 = deltaHist.Time2 != Bars[iStart].OpenTime ? Bars.OpenTimes[index].AddMilliseconds(-dynLength_Delta) : Bars[iStart].OpenTime;
                    }

                    if (ShowResults)
                    {
                        double deltaBuy = DeltaRank.Values.Where(n => n > 0).Sum();
                        double deltaSell = DeltaRank.Values.Where(n => n < 0).Sum();
                        double percentBuy = 0;
                        double percentSell = 0;
                        try { percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { };
                        try { percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { }

                        ChartText Left, Right;
                        Right = Chart.DrawText($"{iStart}BuyDeltaSum", $"{Math.Round(percentBuy)}%", Bars.OpenTimes[iStart], lowest, BuyColor);
                        Left = Chart.DrawText($"{iStart}SellDeltaSum", $"{Math.Round(percentSell)}%", Bars.OpenTimes[iStart], lowest, SellColor);
                        Left.HorizontalAlignment = HorizontalAlignment.Left; Left.FontSize = FontSizeResults;
                        Right.HorizontalAlignment = HorizontalAlignment.Right; Right.FontSize = FontSizeResults;

                        ChartText Center, MinText, MaxText, SubText;
                        double totalDelta = Math.Round(DeltaRank.Values.Sum());
                        double minDelta = Math.Round(MinMaxDelta[0]);
                        double maxDelta = Math.Round(MinMaxDelta[1]);
                        double subDelta = Math.Round(minDelta - maxDelta);
                        
                        string totalDeltaFmtd = totalDelta > 0 ? FormatBigNumber(totalDelta) : $"-{FormatBigNumber(Math.Abs(totalDelta))}";
                        string minDeltaFmtd = minDelta > 0 ? FormatBigNumber(minDelta) : $"-{FormatBigNumber(Math.Abs(minDelta))}";
                        string maxDeltaFmtd = maxDelta > 0 ? FormatBigNumber(maxDelta) : $"-{FormatBigNumber(Math.Abs(maxDelta))}";
                        string subDeltaFmtd = subDelta > 0 ? FormatBigNumber(subDelta) : $"-{FormatBigNumber(Math.Abs(subDelta))}";
                        
                        Color centerColor = totalDelta > 0 ? BuyColor : SellColor;
                        Color subColor = subDelta > 0 ? BuyColor : SellColor;
                        Center = Chart.DrawText($"{iStart}DeltaResult", $"\n{(FormatResults ? totalDeltaFmtd : totalDelta)}", Bars.OpenTimes[iStart], lowest, centerColor);
                        MinText = Chart.DrawText($"{iStart}DeltaMinResult", $"\n\nMin: {(FormatResults ? minDeltaFmtd : minDelta)}", Bars.OpenTimes[iStart], lowest, SellColor);
                        MaxText = Chart.DrawText($"{iStart}DeltaMaxResult", $"\n\n\nMax: {(FormatResults ? maxDeltaFmtd : maxDelta)}", Bars.OpenTimes[iStart], lowest, BuyColor);
                        SubText = Chart.DrawText($"{iStart}DeltaSubResult", $"\n\n\n\nSub: {(FormatResults ? subDeltaFmtd : subDelta)}", Bars.OpenTimes[iStart], lowest, subColor);
                        
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        MinText.HorizontalAlignment = HorizontalAlignment.Center;
                        MaxText.HorizontalAlignment = HorizontalAlignment.Center;
                        SubText.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;
                        MinText.FontSize = FontSizeResults - 1;
                        MaxText.FontSize = FontSizeResults - 1;
                        SubText.FontSize = FontSizeResults - 1;
                        
                        if (HistogramSideInput == HistSideData.Right || VolumeModeInput == VolumeModeData.Normal_Delta)
                        {
                            Right.Time = Bars.OpenTimes[index];
                            Left.Time = Bars.OpenTimes[index];
                            Center.Time = Bars.OpenTimes[index];
                            MinText.Time = Bars.OpenTimes[index];
                            MaxText.Time = Bars.OpenTimes[index];
                            SubText.Time = Bars.OpenTimes[index];
                        }
                    }
                }
                // ============= Coloring Letters + VAL / VAH / POC =============
                if (ShowVA)
                {
                    double[] VAL_VAH_POC = VA_Calculation();

                    // ==========================
                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[2] - rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[2] - rowHeight, ColorPOC);
                    ChartTrendLine vah = Chart.DrawTrendLine($"VAH_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[1] + rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[1] + rowHeight, ColorVAH);
                    ChartTrendLine val = Chart.DrawTrendLine($"VAL_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[0], Bars.OpenTimes[index], VAL_VAH_POC[0], ColorVAL);

                    double[] VAforColor = { VAL_VAH_POC[0], VAL_VAH_POC[1], VAL_VAH_POC[2] };
                    priceVA_LHP = VAforColor;

                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                    vah.LineStyle = LineStyleVA; vah.Thickness = ThicknessVA; vah.Comment = "VAH";
                    val.LineStyle = LineStyleVA; val.Thickness = ThicknessVA; val.Comment = "VAL";

                    // ==== POC Lines ====
                    if (POCsLines.Contains(poc))
                    {
                        for (int tl = 0; tl < RectanglesToColor.Count; tl++)
                        {
                            if (POCsLines[tl].Time1 == poc.Time1)
                            {
                                POCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        POCsLines.Add(poc);

                    // ==== VAH / VAL Lines ====
                    if (VALines.Contains(vah) || VALines.Contains(val))
                    {
                        for (int tl = 0; tl < VALines.Count; tl++)
                        {
                            if (VALines[tl].Comment == "VAH" && VALines[tl].Time1 == vah.Time1)
                                VALines[tl] = vah;
                            else if (VALines[tl].Comment == "VAL" && VALines[tl].Time1 == val.Time1)
                                VALines[tl] = val;
                        }
                    }
                    else
                    {
                        if (!VALines.Contains(vah))
                            VALines.Add(vah);
                        if (!VALines.Contains(val))
                            VALines.Add(val);
                    }

                    if (VolumeModeInput == VolumeModeData.Normal)
                    {
                        // =========== Coloring Retangles ============
                        foreach (int key in RectanglesToColor.Keys)
                        {
                            if (RectanglesToColor[key].Y1 > priceVA_LHP[0] && RectanglesToColor[key].Y1 < priceVA_LHP[1])
                                RectanglesToColor[key].Color = HistColorVA;

                            if (RectanglesToColor[key].Y1 == priceVA_LHP[2] - rowHeight)
                                RectanglesToColor[key].Color = ColorPOC;

                            if (RectanglesToColor[key].Y1 == priceVA_LHP[1])
                                RectanglesToColor[key].Color = ColorVAH;
                            else if (RectanglesToColor[key].Y1 == priceVA_LHP[0])
                                RectanglesToColor[key].Color = ColorVAL;
                        }
                    }
                    else
                    {
                        foreach (int key in RectanglesToColor.Keys)
                        {
                            if (RectanglesToColor[key].Y1 == priceVA_LHP[2] - rowHeight)
                                RectanglesToColor[key].Color = ColorPOC;
                        }
                    }
                }
                else if (!ShowVA && KeepPOC)
                {
                    double priceLVOL = 0;
                    for (int k = 0; k < VolumesRank.Count; k++)
                    {
                        if (VolumesRank.ElementAt(k).Value == largestVOL)
                        {
                            priceLVOL = VolumesRank.ElementAt(k).Key;
                            break;
                        }
                    }

                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], priceLVOL - rowHeight, Bars.OpenTimes[index], priceLVOL - rowHeight, ColorPOC);
                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";

                    // ==== POC Lines ====
                    if (POCsLines.Contains(poc))
                    {
                        for (int tl = 0; tl < RectanglesToColor.Count; tl++)
                        {
                            if (POCsLines[tl].Time1 == poc.Time1)
                            {
                                POCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        POCsLines.Add(poc);

                    // =========== Coloring Retangles ============
                    foreach (int key in RectanglesToColor.Keys)
                    {
                        if (RectanglesToColor[key].Y1 == priceLVOL - rowHeight)
                            RectanglesToColor[key].Color = ColorPOC;
                    }
                }
            }
            // ====== Rectangle VA ======
            if (ShowVA && priceVA_LHP[0] != 0)
            {
                ChartRectangle rectVA;
                rectVA = Chart.DrawRectangle($"{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], priceVA_LHP[0], Bars.OpenTimes[index], priceVA_LHP[1] + rowHeight, VAColor);
                rectVA.IsFilled = true;
            }

            if (!ShowOHLC)
                return;
            ChartText iconOpenSession = Chart.DrawText($"Start{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], TF_Bars.OpenPrices[TF_idx], ColorOHLC);
            ChartText iconCloseSession = Chart.DrawText($"End{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], Bars.ClosePrices[index], ColorOHLC);
            iconOpenSession.VerticalAlignment = VerticalAlignment.Center;
            iconOpenSession.HorizontalAlignment = HorizontalAlignment.Left;
            iconOpenSession.FontSize = 14;
            iconCloseSession.VerticalAlignment = VerticalAlignment.Center;
            iconCloseSession.HorizontalAlignment = HorizontalAlignment.Right;
            iconCloseSession.FontSize = 14;

            ChartTrendLine Session = Chart.DrawTrendLine($"Session{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], lowest, TF_Bars.OpenTimes[TF_idx], highest, ColorOHLC);
            Session.Thickness = 3;
        }
        private void VP_Bars(int index)
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];
            
            // For real-time market
            // Run conditional only in the last bar of repaint loop
            if (IsLastBar && Bars.OpenTimes[index] == Bars.LastBar.OpenTime)
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
                if (DistributionInput == DistributionData.OHLC || DistributionInput == DistributionData.OHLC_No_Avg)
                {
                    bool isAvg = DistributionInput == DistributionData.OHLC;
                    // ========= Tick Simulation ================
                    // Bull/Buy/Up bar
                    if (volBar.Close >= volBar.Open)
                    {
                        // Average Tick Volume
                        double avgVol = isAvg ? volBar.TickVolume / (volBar.Open + volBar.High + volBar.Low + volBar.Close / 4) : volBar.TickVolume;
                        for (int i = 0; i < Segments.Count; i++)
                        {
                            double priceKey = Segments[i];
                            if (Segments[i] <= volBar.Open && Segments[i] >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (Segments[i] <= volBar.High && Segments[i] >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (Segments[i] <= volBar.High && Segments[i] >= volBar.Close)
                                AddVolume(priceKey, avgVol, isBullish);
                        }
                    }
                    // Bear/Sell/Down bar
                    else
                    {
                        // Average Tick Volume
                        double avgVol = isAvg ? volBar.TickVolume / (volBar.Open + volBar.High + volBar.Low + volBar.Close / 4) : volBar.TickVolume;
                        for (int i = 0; i < Segments.Count; i++)
                        {
                            double priceKey = Segments[i];
                            if (Segments[i] >= volBar.Open && Segments[i] <= volBar.High)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (Segments[i] <= volBar.High && Segments[i] >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (Segments[i] >= volBar.Low && Segments[i] <= volBar.Close)
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
                        for (int i = 0; i < Segments.Count; i++)
                        {
                            if (Segments[i] >= volBar.High && prevSegment <= volBar.High)
                                AddVolume(Segments[i], volBar.TickVolume, isBullish);
                            prevSegment = Segments[i];
                        }
                    }
                    else if (selected == DistributionData.Low)
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < Segments.Count; i++)
                        {
                            if (Segments[i] >= volBar.Low && prevSegment <= volBar.Low)
                                AddVolume(Segments[i], volBar.TickVolume, isBullish);
                            prevSegment = Segments[i];
                        }
                    }
                    else
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < Segments.Count; i++)
                        {
                            if (Segments[i] >= volBar.Close && prevSegment <= volBar.Close)
                                AddVolume(Segments[i], volBar.TickVolume, isBullish);
                            prevSegment = Segments[i];
                        }
                    }
                }
                else if (DistributionInput == DistributionData.Uniform_Distribution)
                {
                    double HL = Math.Abs(volBar.High - volBar.Low);
                    double uniVol = volBar.TickVolume / HL;
                    for (int i = 0; i < Segments.Count; i++)
                    {
                        if (Segments[i] >= volBar.Low && Segments[i] <= volBar.High)
                            AddVolume(Segments[i], uniVol, isBullish);
                    }
                }
                else if (DistributionInput == DistributionData.Uniform_Presence)
                {
                    double uniP_Vol = 1;
                    for (int i = 0; i < Segments.Count; i++)
                    {
                        if (Segments[i] >= volBar.Low && Segments[i] <= volBar.High)
                            AddVolume(Segments[i], uniP_Vol, isBullish);
                    }
                }
                else if (DistributionInput == DistributionData.Parabolic_Distribution)
                {
                    double HL = Math.Abs(volBar.High - volBar.Low);
                    double HL2 = HL / 2;
                    double hl2SQRT = Math.Sqrt(HL2);
                    double final = hl2SQRT / hl2SQRT;

                    double parabolicVol = volBar.TickVolume / final;

                    for (int i = 0; i < Segments.Count; i++)
                    {
                        if (Segments[i] >= volBar.Low && Segments[i] <= volBar.High)
                            AddVolume(Segments[i], parabolicVol, isBullish);
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

                    for (int i = 0; i < Segments.Count; i++)
                    {
                        if (Segments[i] >= volBar.Low && Segments[i] <= volBar.High)
                            AddVolume(Segments[i], triangularVol, isBullish);
                    }
                }
            }

            void AddVolume(double priceKey, double vol, bool isBullish)
            {
                if (!VolumesRank.ContainsKey(priceKey))
                    VolumesRank.Add(priceKey, vol);
                else
                    VolumesRank[priceKey] += vol;
                
                bool condition = VolumeModeInput != VolumeModeData.Normal || VolumeModeInput != VolumeModeData.Gradient;
                if (condition)
                    Add_BuySell(priceKey, vol, isBullish);
            }
            void Add_BuySell(double priceKey, double vol, bool isBullish)
            {
                if (isBullish)
                {
                    if (!VolumesRank_Up.ContainsKey(priceKey))
                        VolumesRank_Up.Add(priceKey, vol);
                    else
                        VolumesRank_Up[priceKey] += vol;
                }
                else
                {
                    if (!VolumesRank_Down.ContainsKey(priceKey))
                        VolumesRank_Down.Add(priceKey, vol);
                    else
                        VolumesRank_Down[priceKey] += vol;
                }

                double prevDelta = DeltaRank.Values.Sum();

                if (!DeltaRank.ContainsKey(priceKey))
                {
                    if (VolumesRank_Up.ContainsKey(priceKey) && VolumesRank_Down.ContainsKey(priceKey))
                        DeltaRank.Add(priceKey, (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]));
                    else if (VolumesRank_Up.ContainsKey(priceKey) && !VolumesRank_Down.ContainsKey(priceKey))
                        DeltaRank.Add(priceKey, (VolumesRank_Up[priceKey]));
                    else if (!VolumesRank_Up.ContainsKey(priceKey) && VolumesRank_Down.ContainsKey(priceKey))
                        DeltaRank.Add(priceKey, (-VolumesRank_Down[priceKey]));
                    else
                        DeltaRank.Add(priceKey, 0);
                }
                else
                {
                    if (VolumesRank_Up.ContainsKey(priceKey) && VolumesRank_Down.ContainsKey(priceKey))
                        DeltaRank[priceKey] += (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]);
                    else if (VolumesRank_Up.ContainsKey(priceKey) && !VolumesRank_Down.ContainsKey(priceKey))
                        DeltaRank[priceKey] += (VolumesRank_Up[priceKey]);
                    else if (!VolumesRank_Up.ContainsKey(priceKey) && VolumesRank_Down.ContainsKey(priceKey))
                        DeltaRank[priceKey] += (-VolumesRank_Down[priceKey]);

                }

                double currentDelta = DeltaRank.Values.Sum();
                if (prevDelta > currentDelta)
                    MinMaxDelta[0] = prevDelta; // Min
                if (prevDelta < currentDelta)
                    MinMaxDelta[1] = prevDelta; // Max before final delta
            }
        }

        // ====== Functions Area ======
        private void VP_Tick(int index)
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];

            // For real-time market
            // Run conditional only in the last bar of repaint loop
            if (IsLastBar && Bars.OpenTimes[index] == Bars.LastBar.OpenTime)
                endTime = TicksOHLC.Last().OpenTime;

            double prevTick = 0;

            for (int tickIndex = 0; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar;
                tickBar = TicksOHLC[tickIndex];

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
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (tickPrice >= prev_segmentValue && tickPrice <= Segments[i])
                    {
                        double priceKey = Segments[i];
                        double prevDelta = DeltaRank.Values.Sum();

                        if (VolumesRank.ContainsKey(priceKey))
                        {
                            VolumesRank[priceKey] += 1;

                            if (tickPrice > prevTick && prevTick != 0)
                                VolumesRank_Up[priceKey] += 1;
                            else if (tickPrice < prevTick && prevTick != 0)
                                VolumesRank_Down[priceKey] += 1;
                            else if (tickPrice == prevTick && prevTick != 0)
                            {
                                VolumesRank_Up[priceKey] += 1;
                                VolumesRank_Down[priceKey] += 1;
                            }

                            DeltaRank[priceKey] += (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]);
                        }
                        else
                        {
                            VolumesRank.Add(priceKey, 1);

                            if (!VolumesRank_Up.ContainsKey(priceKey))
                                VolumesRank_Up.Add(priceKey, 1);
                            else
                                VolumesRank_Up[priceKey] += 1;

                            if (!VolumesRank_Down.ContainsKey(priceKey))
                                VolumesRank_Down.Add(priceKey, 1);
                            else
                                VolumesRank_Down[priceKey] += 1;

                            if (!DeltaRank.ContainsKey(priceKey))
                                DeltaRank.Add(priceKey, (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]));
                            else
                                DeltaRank[priceKey] += (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]);
                        }
                        
                        double currentDelta = DeltaRank.Values.Sum();
                        if (prevDelta > currentDelta)
                            MinMaxDelta[0] = prevDelta; // Min
                        if (prevDelta < currentDelta)
                            MinMaxDelta[1] = prevDelta; // Max before final delta

                        break;
                    }
                    prev_segmentValue = Segments[i];
                }
            }
        }
        public string FormatBigNumber(double num)
        {
            /*
                MaxDigits = 2
                123        ->  123
                1234       ->  1.23k
                12345      ->  12.35k
                123456     ->  123.45k
                1234567    ->  1.23M
                12345678   ->  12.35M
                123456789  ->  123.56M
            */
            FormatMaxDigits_Data selected = FormatMaxDigits_Input;
            string digitsThousand = selected == FormatMaxDigits_Data.Two ? "0.##k" : selected == FormatMaxDigits_Data.One ? "0.#k" : "0.k";
            string digitsMillion = selected == FormatMaxDigits_Data.Two ? "0.##M" : selected == FormatMaxDigits_Data.One ? "0.#M" : "0.M";
            
            if (num >= 100000000) {
                return (num / 1000000D).ToString(digitsMillion);
            }
            if (num >= 1000000) {
                return (num / 1000000D).ToString(digitsMillion);
            }
            if (num >= 100000) {
                return (num / 1000D).ToString(digitsThousand);
            }
            if (num >= 10000) {
                return (num / 1000D).ToString(digitsThousand);
            }
            if (num >= 1000) {
                return (num / 1000D).ToString(digitsThousand);
            }

            return num.ToString("#,0");
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
            DateTime FirstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
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
            try
            {
                FirstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeStart", FirstTickTime, Color.Red);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText($"VolumeStartText", $"Tick Volume Data \n ends here", FirstTickTime, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(FirstTickTime)], Color.Red);
                textInfo.FontSize = 8;
            }
            catch { };
        }
        private void LoadMoreTicks(DateTime FromDateTime)
        {
            bool msg = false;

            while (TicksOHLC.OpenTimes.FirstOrDefault() > FromDateTime)
            {
                if (!msg)
                {
                    Print($"Loading from '{TicksOHLC.OpenTimes.First()}' to '{FromDateTime}'...");
                    msg = true;
                }

                int loadedCount = TicksOHLC.LoadMoreHistory();
                Print("Loaded {0} Ticks, Current Tick Date: {1}", loadedCount, TicksOHLC.OpenTimes.FirstOrDefault());
                if (loadedCount == 0)
                    break;
            }
            Print("Data Collection Finished, First Tick from: {0}", TicksOHLC.OpenTimes.FirstOrDefault());
        }
        // ========= ========== ==========
        private double[] VA_Calculation()
        {
            /*  https://onlinelibrary.wiley.com/doi/pdf/10.1002/9781118659724.app1
                https://www.mypivots.com/dictionary/definition/40/calculating-market-profile-value-area
                Same of TPO Profile(https://ctrader.com/algos/indicators/show/3074)  */

            double largestVOL = VolumesRank.Values.Max();

            double totalvol = VolumesRank.Values.Sum();
            double _70percent = Math.Round((70 * totalvol) / 100);

            double priceLVOL = 0;
            for (int k = 0; k < VolumesRank.Count; k++)
            {
                if (VolumesRank.ElementAt(k).Value == largestVOL)
                {
                    priceLVOL = VolumesRank.ElementAt(k).Key;
                    break;
                }
            }
            double priceVAH = 0;
            double priceVAL = 0;

            double sumVA = largestVOL;

            List<double> upKeys = new();
            List<double> downKeys = new();
            for (int i = 0; i < Segments.Count; i++)
            {
                double priceKey = Segments[i];

                if (VolumesRank.ContainsKey(priceKey))
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

            double[] withoutVA = { priceLVOL - (rowHeight * 2), priceLVOL + (rowHeight / 2), priceLVOL };
            if (upKeys.Count == 0 || downKeys.Count == 0)
                return withoutVA;

            double[] prev2UP = { 0, 0 };
            double[] prev2Down = { 0, 0 };

            bool lockAbove = false;
            double[] aboveKV = { 0, 0 };

            bool lockBelow = false;
            double[] belowKV = { 0, 0 };

            for (int i = 0; i < VolumesRank.Keys.Count; i++)
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
                        sumDown = VolumesRank[key];
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
                        double upVOL = VolumesRank[key];
                        double up2VOL = VolumesRank[prevUPkey];

                        keyUP = key;

                        double[] _2up = { prevUPkey, keyUP };
                        prev2UP = _2up;

                        double[] _above = { keyUP, upVOL + up2VOL };
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
                        sumDown = VolumesRank[key];
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
                        double downVOL = VolumesRank[key];
                        double down2VOL = VolumesRank[prevDownkey];

                        keyDw = key;

                        double[] _2down = { prevDownkey, keyDw };
                        prev2Down = _2down;

                        double[] _below = { keyDw, downVOL + down2VOL };
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
                    double[] _2up = { prevUPkey, keyUP };
                    prev2UP = _2up;
                    double[] _2down = { prevDownkey, keyDw };
                    prev2Down = _2down;

                    sumVA += (sumUp + sumDown);
                    priceVAH = keyUP;
                    priceVAL = keyDw;

                    lockBelow = false;
                    lockAbove = false;
                }
            }

            double[] VA = { priceVAL, priceVAH, priceLVOL };

            return VA;
        }

        public void ClearAndRecalculate()
        {            
            // The chart should already be clear
            // No objects
            
            int FirstIndex = Bars.OpenTimes.GetIndexByTime(LookBack_Bars.OpenTimes.FirstOrDefault());

            // Get Index of VOL Interval to continue only in Lookback
            int iVerify = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            while (LookBack_Bars.ClosePrices.Count - iVerify > Lookback) {
                FirstIndex++;
                iVerify = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            }

            int TF_idx = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(LookBack_Bars.OpenTimes[TF_idx]);

            // Historical data
            for (int index = indexStart; index < Bars.Count; index++)
            {
                TF_idx = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                indexStart = Bars.OpenTimes.GetIndexByTime(LookBack_Bars.OpenTimes[TF_idx]);

                if (index == indexStart && index != cleanedIndex || (index - 1) == indexStart && (index - 1) != cleanedIndex)
                {
                    Segments.Clear();
                    VolumesRank.Clear();
                    VolumesRank_Up.Clear();
                    VolumesRank_Down.Clear();
                    DeltaRank.Clear();
                    RectanglesToColor.Clear();
                    double[] VAforColor = { 0, 0, 0 };
                    priceVA_LHP = VAforColor;
                    cleanedIndex = index == indexStart ? index : (index - 1);
                }
                VolumeProfile(indexStart, index);
            }
               
            // Repaint current profile
            for (int i = indexStart; i <= Bars.Count; i++)
            {
                if (i == indexStart)
                {
                    Segments.Clear();
                    VolumesRank.Clear();
                    VolumesRank_Up.Clear();
                    VolumesRank_Down.Clear();
                    DeltaRank.Clear();
                    double[] resetDelta = {0, 0};
                    MinMaxDelta = resetDelta;
                    RectanglesToColor.Clear();
                }

                VolumeProfile(indexStart, i);
            }
            
            configHasChanged = true;
            if (ExtendPOC) {
                ExtendPOCNow();
            }
        }

        public void SetRowHeight(double number)
        {
            rowHeight = number;
        }
        public void SetLookback(int number)
        {
            Lookback = number;
        }
        public void SetInterval(TimeFrame newTF) {
            LookBack_TF = newTF;
            LookBack_Bars = MarketData.GetBars(LookBack_TF);
            if (LookBack_Bars.ClosePrices.Count < Lookback)
            {
                while (LookBack_Bars.ClosePrices.Count < Lookback)
                {
                    int loadedCount = LookBack_Bars.LoadMoreHistory();
                    Print($"Loaded {loadedCount}, {LookBack_TF.ShortName} LookBack Bars, Current Bar Date: {LookBack_Bars.OpenTimes.FirstOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
            }

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
                try
                {
                    DateTime FirstVolDate = VOL_Bars.OpenTimes.FirstOrDefault();
                    ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeStart", FirstVolDate, Color.Red);
                    lineInfo.LineStyle = LineStyle.Lines;
                    ChartText textInfo = Chart.DrawText($"VolumeStartText", $"{BarsVOLSource_TF.ShortName} Volume Data \n ends here", FirstVolDate, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(FirstVolDate)], Color.Red);
                    textInfo.FontSize = 8;
                }
                catch { };
            }
        }
        public void ExtendPOCNow() {
            if (ExtendPOC && POCsLines.Count != 0)
            {
                for (int tl = 0; tl < POCsLines.Count; tl++)
                {
                    POCsLines[tl].Time2 = Bars.LastBar.OpenTime;
                    string dynDate = LookBack_TF == TimeFrame.Daily ? POCsLines[tl].Time1.Date.AddDays(1).ToString().Replace("00:00:00", "") : POCsLines[tl].Time1.Date.ToString();
                    Chart.DrawText($"POC{POCsLines[tl].Time1}", $"{dynDate}", Bars.LastBar.OpenTime, POCsLines[tl].Y2 + (rowHeight / 2), ColorPOC);
                }
            }
        }
        public int GetLookback()
        {
            return Lookback;
        }
        public double GetRowHeight()
        {
            return rowHeight;
        }
        public TimeFrame GetInterval() {
            return LookBack_TF;
        }
    }
    public class ParamsPanel : CustomControl
    {
        // Any
        private const string LookBack_InputKey = "LookBackKey";
        private const string RowHeight_InputKey = "RowHeightKey";
        private const string Interval_InputKey = "IntervalKey";
        private const string KeepPOC_InputKey = "KeepPOCKey";
        private const string ExtendedPOC_InputKey = "ExtendedPOCKey";
        private const string ShowVA_InputKey = "ShowVAKey";

        private readonly IDictionary<string, TextBox> textInputMap = new Dictionary<string, TextBox>();
        private readonly IDictionary<string, CheckBox> checkBoxMap = new Dictionary<string, CheckBox>();
        private readonly IDictionary<string, ComboBox> comboBoxMap = new Dictionary<string, ComboBox>();
        private readonly FreeVolumeProfileV20 Outside;

        private Button ModeBtn;
        private readonly Color BtnColor;
        private readonly IndicatorParams FirstParams;

        public ParamsPanel(FreeVolumeProfileV20 indicator, IndicatorParams defaultParams)
        {
            BtnColor = Color.FromHex("#7F808080");
            Outside = indicator;
            FirstParams = defaultParams;
            AddChild(CreateTradingPanel());
        }

        private ControlBase CreateTradingPanel()
        {
            StackPanel mainPanel = new();

            ControlBase header = CreateHeader();
            mainPanel.AddChild(header);

            StackPanel contentPanel = CreateContentPanel();
            mainPanel.AddChild(contentPanel);

            return mainPanel;
        }

        private static ControlBase CreateHeader()
        {
            Border headerBorder = new()
            {
                BorderThickness = "0 0 0 1",
                Style = Styles.CreateCommonBorderStyle(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 280
            };
            Grid grid = new(0, 0);

            TextBlock header = new()
            {
                Text = "Volume Profile",
                Margin = "10 7",
                Style = Styles.CreateHeaderStyle(),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            grid.AddChild(header, 0, 0);

            headerBorder.Child = grid;
            return headerBorder;
        }

        private StackPanel CreateContentPanel()
        {
            StackPanel contentPanel = new()
            {
                Margin = 10
            };
            Grid grid = new(3, 5);
            grid.Columns[1].SetWidthInPixels(5);
            grid.Columns[3].SetWidthInPixels(5);

            Button button_prev = CreatePassButton("<");
            grid.AddChild(button_prev, 0, 0);

            Button VolumeModeButton = CreateModeInfo_Button(FirstParams.VolMode.ToString());
            grid.AddChild(VolumeModeButton, 0, 1, 1, 3);

            Button button_next = CreatePassButton(">");
            grid.AddChild(button_next, 0, 4);

            var Lookback_Input = CreateInputWithLabel("Lookback", FirstParams.LookBack.ToString(), LookBack_InputKey);
            grid.AddChild(Lookback_Input, 1, 0);
            var RowHeightInput = CreateInputWithLabel("Row(pips)", FirstParams.RowHeight.ToString("0.############################", CultureInfo.InvariantCulture), RowHeight_InputKey);
            grid.AddChild(RowHeightInput, 1, 2);
            var IntervalInput = CreateComboBoxWithLabel("Interval", Interval_InputKey);
            grid.AddChild(IntervalInput, 1, 4);

            var POCCheckbox = CreateCheckboxWithLabel("POC", FirstParams.KeepPOC, KeepPOC_InputKey);
            grid.AddChild(POCCheckbox, 2, 0);
            var ExtendCheckbox = CreateCheckboxWithLabel("Extend", FirstParams.ExtendedPOC, ExtendedPOC_InputKey);
            grid.AddChild(ExtendCheckbox, 2, 2);
            var WithVACheckbox = CreateCheckboxWithLabel("VA", FirstParams.ShowVA, ShowVA_InputKey);
            grid.AddChild(WithVACheckbox, 2, 4);

            contentPanel.AddChild(grid);
            return contentPanel;
        }

        private Button CreatePassButton(string label)
        {
            Button button = new()
            {
                Text = label,
                Padding = 0,
                Width = 30,
                Height = 20,
                Margin = 0,
                BackgroundColor = BtnColor,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            if (label == ">")
            {
                button.Click += NextModeEvent;
            }
            else
            {
                button.Click += PrevModeEvent;
            }
            return button;
        }

        private Button CreateModeInfo_Button(string label)
        {
            Button button = new()
            {
                Text = label,
                Padding = 0,
                Width = 70,
                Height = 30,
                Margin = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            button.Click += ResetParamsEvent;
            ModeBtn = button;

            return button;
        }

        private Panel CreateInputWithLabel(string label, string defaultValue, string inputKey)
        {
            StackPanel stackPanel = new()
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0"
            };

            TextBlock textBlock = new()
            {
                Text = label,
                TextAlignment = TextAlignment.Center
            };

            TextBox input = new()
            {
                Margin = "0 5 0 0",
                Text = defaultValue,
                Style = Styles.CreateInputStyle(),
                TextAlignment = TextAlignment.Center
            };

            input.TextChanged += TextChangedEvent;
            textInputMap.Add(inputKey, input);

            stackPanel.AddChild(textBlock);
            stackPanel.AddChild(input);

            return stackPanel;
        }
        private Panel CreateComboBoxWithLabel(string label, string inputKey)
        {
            StackPanel stackPanel = new()
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0"
            };

            TextBlock textBlock = new()
            {
                Text = label,
                TextAlignment = TextAlignment.Center
            };

            ComboBox comboBox = new()
            {
                Margin = "0 5 0 0",
                Style = Styles.CreateInputStyle(),
            };

            string[] allTF = {
                // Candles
                "h1", "h2", "h3", "h4", "h6", "h8", "h12", "D1", "D2", "D3", "W1", "Month1",
            };
            for (int i = 0; i < allTF.Length; i++)
            {
                comboBox.AddItem(allTF[i]);
            }
            comboBox.SelectedItem = FirstParams.Interval.ShortName;
            comboBox.SelectedItemChanged += ComboBoxSelectedEvent;
            comboBoxMap.Add(inputKey, comboBox);

            stackPanel.AddChild(textBlock);
            stackPanel.AddChild(comboBox);

            return stackPanel;
        }

        private ControlBase CreateCheckboxWithLabel(string label, bool defaultValue, string inputKey)
        {
            Border checkBoxBorder = new()
            {
                Margin = "0 10 0 0",
                BorderThickness = "0 1 0 1",
                Style = Styles.CreateCommonBorderStyle()
            };

            StackPanel stackPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Margin = "0 10 0 10"
            };

            CheckBox input = new()
            {
                Margin = "0 0 5 0",
                IsChecked = defaultValue
            };

            TextBlock textBlock = new()
            {
                Text = label,
            };

            input.Click += CheckBoxClickEvent;
            checkBoxMap.Add(inputKey, input);

            stackPanel.AddChild(input);
            stackPanel.AddChild(textBlock);

            checkBoxBorder.Child = stackPanel;
            return checkBoxBorder;
        }

        private void RecalculateOutsideWithMsg(bool reloadHistory = false) {
            string currentMode = ModeBtn.Text;
            ModeBtn.Text = $"{currentMode}\nCalculating...";
            
            Outside.Chart.RemoveAllObjects();
            
            Outside.BeginInvokeOnMainThread(() => {
                if (reloadHistory) {
                    Outside.SetInterval(Outside.GetInterval());
                }
                Outside.ClearAndRecalculate();
                ModeBtn.Text = currentMode;
            });
        }

        private void NextModeEvent(ButtonClickEventArgs obj)
        {
            if (Outside.VolumeModeInput == VolumeModeData.Normal)
            {
                Outside.VolumeModeInput = VolumeModeData.Gradient;
                ModeBtn.Text = "Gradient";
            } else if (Outside.VolumeModeInput == VolumeModeData.Gradient)
            {
                Outside.VolumeModeInput = VolumeModeData.Buy_Sell;
                ModeBtn.Text = "Buy_Sell";
            } else if (Outside.VolumeModeInput == VolumeModeData.Buy_Sell)
            {
                Outside.VolumeModeInput = VolumeModeData.Delta;
                ModeBtn.Text = "Delta";
            } else if (Outside.VolumeModeInput == VolumeModeData.Delta)
            {
                Outside.VolumeModeInput = VolumeModeData.Normal_Delta;
                ModeBtn.Text = "Normal+Delta";
            }

            RecalculateOutsideWithMsg();
        }
        private void PrevModeEvent(ButtonClickEventArgs obj)
        {
            if (Outside.VolumeModeInput == VolumeModeData.Normal_Delta)
            {
                Outside.VolumeModeInput = VolumeModeData.Delta;
                ModeBtn.Text = "Delta";
            } else if (Outside.VolumeModeInput == VolumeModeData.Delta)
            {
                Outside.VolumeModeInput = VolumeModeData.Buy_Sell;
                ModeBtn.Text = "Buy_Sell";
            } else if (Outside.VolumeModeInput == VolumeModeData.Buy_Sell)
            {
                Outside.VolumeModeInput = VolumeModeData.Gradient;
                ModeBtn.Text = "Gradient";
            } else if (Outside.VolumeModeInput == VolumeModeData.Gradient)
            {
                Outside.VolumeModeInput = VolumeModeData.Normal;
                ModeBtn.Text = "Normal";
            }

            RecalculateOutsideWithMsg();
        }
        private void ChangeParams(IndicatorParams indicatorParams)
        {
            foreach (var key in checkBoxMap.Keys)
            {
                switch (key)
                {
                    case "KeepPOCKey": checkBoxMap[key].IsChecked = indicatorParams.KeepPOC; break;
                    case "ExtendedPOCKey": checkBoxMap[key].IsChecked = indicatorParams.ExtendedPOC; break;
                    case "ShowVAKey": checkBoxMap[key].IsChecked = indicatorParams.ShowVA; break;
                }
            }
            foreach (var key in textInputMap.Keys)
            {
                switch (key)
                {
                    case "LookBackKey": textInputMap[key].Text = indicatorParams.LookBack.ToString(); break;
                    case "RowHeightKey": textInputMap[key].Text = indicatorParams.RowHeight.ToString("0.############################", CultureInfo.InvariantCulture); break;
                }
            }
            foreach (var key in comboBoxMap.Keys)
            {
                switch (key)
                {
                    case "IntervalKey": comboBoxMap[key].SelectedItem = indicatorParams.Interval.ToString(); break;
                }
            }
        }
        private void ResetParamsEvent(ButtonClickEventArgs obj)
        {
            ChangeParams(FirstParams);
        }
        private void TextChangedEvent(TextChangedEventArgs obj)
        {
            int lookBack = GetValueFromInput(LookBack_InputKey, -1);
            double rowPips = GetDoubleFromInput(RowHeight_InputKey, -1);
            
            if (rowPips != -1 && rowPips > 0) {
                double rowHeight = Outside.Symbol.PipSize * rowPips;

                if (rowHeight != Outside.GetRowHeight()) {
                    Outside.SetRowHeight(rowHeight);
                    RecalculateOutsideWithMsg();   
                }
            }
            if (lookBack > 0 && lookBack != Outside.GetLookback()) {
                Outside.SetLookback(lookBack);
                // Get more lookback/interval Bars if needed
                // No delay in Panel
                RecalculateOutsideWithMsg(true);
            }
        }
        private void ComboBoxSelectedEvent(ComboBoxSelectedItemChangedEventArgs obj)
        {
            foreach (var key in comboBoxMap.Keys)
            {
                switch (key)
                {
                    case "IntervalKey": {
                        string selected = comboBoxMap[key].SelectedItem;
                        if (selected != Outside.GetInterval().ShortName) {
                            // Chart/Panel delay is allowed
                            Outside.SetInterval(StringToTimeframe(selected));
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                }
            }
        }
        private void CheckBoxClickEvent(CheckBoxEventArgs obj)
        {
            foreach (var key in checkBoxMap.Keys)
            {
                switch (key)
                {
                    case "KeepPOCKey": {
                        bool selected = (bool)checkBoxMap[key].IsChecked;
                        if (selected != Outside.KeepPOC) {
                            Outside.KeepPOC = selected;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                    case "ExtendedPOCKey": {
                        bool selected = (bool)checkBoxMap[key].IsChecked;
                        if (selected != Outside.ExtendPOC) {
                            Outside.ExtendPOC = selected;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                    case "ShowVAKey": {
                        bool selected = (bool)checkBoxMap[key].IsChecked;
                        if (selected != Outside.ShowVA) {
                            Outside.ShowVA = selected;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                }
            }
        }
        private int GetValueFromInput(string inputKey, int defaultValue)
        {
            return int.TryParse(textInputMap[inputKey].Text, out int value) ? value : defaultValue;
        }
        private double GetDoubleFromInput(string inputKey, int defaultValue)
        {
            return double.TryParse(textInputMap[inputKey].Text, NumberStyles.Number, CultureInfo.InvariantCulture, out double value) ? value : defaultValue;
        }
        private static TimeFrame StringToTimeframe(string inputTF)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                // Candles
                case "h1": return TimeFrame.Hour;
                case "h2": return TimeFrame.Hour2;
                case "h3": return TimeFrame.Hour3;
                case "h4": return TimeFrame.Hour4;
                case "h6": return TimeFrame.Hour6;
                case "h8": return TimeFrame.Hour8;
                case "h12": return TimeFrame.Hour12;
                case "D1": return TimeFrame.Daily;
                case "D2": return TimeFrame.Day2;
                case "D3": return TimeFrame.Day3;
                case "W1": return TimeFrame.Weekly;
                case "Month1": return TimeFrame.Monthly;
                default:
                    break;
            }
            return ifWrong;
        }
    }

    // ====================== THEME ============================
    public static class Styles
    {
        public static Style CreatePanelBackgroundStyle()
        {
            Style style = new();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#292929"), 0.85m), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.85m), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }
        public static Style CreateCommonBorderStyle()
        {
            Style style = new();
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.12m), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#000000"), 0.12m), ControlState.LightTheme);
            return style;
        }
        public static Style CreateHeaderStyle()
        {
            Style style = new();
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#FFFFFF", 0.70m), ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#000000", 0.65m), ControlState.LightTheme);
            return style;
        }
        public static Style CreateInputStyle()
        {
            Style style = new(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#1A1A1A"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#111111"), ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#E7EBED"), ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#D6DADC"), ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.CornerRadius, 3);
            return style;
        }
        private static Color GetColorWithOpacity(Color baseColor, decimal opacity)
        {
            int alpha = (int)Math.Round(byte.MaxValue * opacity, MidpointRounding.AwayFromZero);
            return Color.FromArgb(alpha, baseColor);
        }
    }
}
