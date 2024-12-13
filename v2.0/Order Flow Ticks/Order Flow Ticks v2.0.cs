/*
--------------------------------------------------------------------------------------------------------------------------------
                        Order Flow Ticks v2.0
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

.NET 6.0+ is Required

What's new in v2.0?
-Added Params Panel for quickly switch between settings (volume modes, row height, etc) and most importantly, more user-friendly.
-Refactor to only use Colors API.
-Should work with Mac OS users.

Performance Tips!
- Set 'Nº Bars' to 20+ or more if switching settings is taking too long

AUTHOR: srlcarlg

== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using static cAlgo.OrderFlowTicksV20;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class OrderFlowTicksV20 : Indicator
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
        [Parameter("Panel Position:", DefaultValue = PanelAlignData.Bottom_Right, Group = "==== Order Flow Ticks v2.0 ====")]
        public PanelAlignData PanelAlignInput { get; set; }

        public enum ConfigRowData
        {
            Predefined,
            Custom,
        }
        [Parameter("Row Config:", DefaultValue = ConfigRowData.Predefined, Group = "==== Order Flow Ticks v2.0 ====")]
        public ConfigRowData ConfigRowInput { get; set; }

        [Parameter("Custom Row Height:", DefaultValue = 0.2, MinValue = 0.2, Group = "==== Order Flow Ticks v2.0 ====")]
        public double CustomHeightInput { get; set; }


        [Parameter("Fill Histogram?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool FillHist { get; set; }

        [Parameter("Show Numbers?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool ShowNumbers { get; set; }

        [Parameter("Show Results?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool ShowResults { get; set; }

        [Parameter("Show Side(total)?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool ShowSideTotalInput { get; set; }

        [Parameter("[Renko] Show Wicks?", DefaultValue = true, Group = "==== Visualization ====")]
        public bool ShowWicks { get; set; }


        [Parameter("Font Size Numbers:", DefaultValue = 8, MinValue = 1, MaxValue = 80, Group = "==== Font Size ====")]
        public int FontSizeNumbers { get; set; }

        [Parameter("Font Size Results:", DefaultValue = 10, MinValue = 1, MaxValue = 80, Group = "==== Font Size ====")]
        public int FontSizeResults { get; set; }


        public enum ResultsColoringData
        {
            bySide,
            Fixed,
        }
        [Parameter("Results Coloring:", DefaultValue = ResultsColoringData.bySide, Group = "==== Results/Numbers ====")]
        public ResultsColoringData ResultsColoringInput { get; set; }

        [Parameter("Fixed Color RT/NB:", DefaultValue = "#CCFFFFFF", Group = "==== Results/Numbers ====")]
        public Color RtnbFixedColor { get; set; }


        [Parameter("Enable Filter?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool EnableFilter { get; set; }

        [Parameter("MA Filter Type:", DefaultValue = MovingAverageType.Exponential, Group = "==== Large Result Filter ====")]
        public MovingAverageType MAtype { get; set; }

        [Parameter("MA Filter Period:", DefaultValue = 5, MinValue = 1, Group = "==== Large Result Filter ====")]
        public int MAperiod { get; set; }

        [Parameter("Large R. Ratio", DefaultValue = 1.5, MinValue = 1, MaxValue = 2, Group = "==== Large Result Filter ====")]
        public double Filter_Ratio { get; set; }

        [Parameter("Large R. Color", DefaultValue = "Gold", Group = "==== Large Result Filter ====")]
        public Color ColorLargeResult { get; set; }

        [Parameter("Coloring Bar?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool ColoringBars { get; set; }

        [Parameter("[Delta] Coloring Cumulative?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool ColoringCD { get; set; }


        [Parameter("Color Volume:", DefaultValue = "#B287CEEB", Group = "==== Volume ====")]
        public Color VolumeColor { get; set; }

        [Parameter("Color Largest Volume:", DefaultValue = "#B2FFD700", Group = "==== Volume ====")]
        public Color VolumeLargeColor { get; set; }


        [Parameter("Color Buy:", DefaultValue = "#B200BFFF", Group = "==== Buy ====")]
        public Color BuyColor { get; set; }

        [Parameter("Color Largest Buy:", DefaultValue = "#B2FFD700", Group = "==== Buy ====")]
        public Color BuyLargeColor { get; set; }


        [Parameter("Color Sell:", DefaultValue = "#B2DC143C", Group = "==== Sell ====")]
        public Color SellColor { get; set; }

        [Parameter("Color Largest Sell:", DefaultValue = "#B2DAA520", Group = "==== Sell ====")]
        public Color SellLargeColor { get; set; }

        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }

        private List<double> Segments = new();
        private readonly IDictionary<double, int> VolumesRank = new Dictionary<double, int>();
        private readonly IDictionary<double, int> VolumesRank_Up = new Dictionary<double, int>();
        private readonly IDictionary<double, int> VolumesRank_Down = new Dictionary<double, int>();
        private readonly IDictionary<double, int> DeltaRank = new Dictionary<double, int>();
        private readonly IDictionary<double, int> CumulDeltaRank = new Dictionary<double, int>();

        private double heightPips = 4;
        private double rowHeight = 0;

        private DateTime fromDateTime;
        private Bars TicksOHLC;

        private bool isWrong = false;
        private bool isLive = false;
        private bool isNewBar = false;
        private bool isCalcFinished = false;
        private bool isCalcLocked = false;

        // For Filter
        // DynamicSeries can be Normal, BuyxSell or Delta Volume
        private IndicatorDataSeries CumulDeltaSeries, DynamicSeries;
        private MovingAverage MACumulDelta, MADynamic;

        // Moved from cTrader Input to Params Panel
        public int Lookback { get; set; } = -1;
        public enum VolumeModeData
        {
            Normal,
            Buy_Sell,
            Delta,
        }
        public VolumeModeData VolumeModeInput { get; set; } = VolumeModeData.Delta;

        public enum VolumeViewData
        {
            Divided,
            Profile,
        }
        public VolumeViewData VolumeViewInput { get; set; } = VolumeViewData.Profile;

         public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }
        public OperatorBuySell_Data OperatorBuySellInput { get; set; } = OperatorBuySell_Data.Subtraction;

        public enum ResultsType_Data
        {
            Percentage,
            Value,
            Both
        }
        public ResultsType_Data ResultsTypeInput { get; set; } = ResultsType_Data.Percentage;

        public bool ColorOnlyLarguestInput { get; set; } = true;

        // Params Panel
        private Border ParamBorder;

        public class IndicatorParams
        {
            public double NBars { get; set; }
            public VolumeModeData VolMode { get; set; }
            public VolumeViewData VolView { get; set; }
            public double RowHeight { get; set; }
            public ResultsType_Data ResultType { get; set; }
            public OperatorBuySell_Data OperatorBuySell { get; set; }
            public bool OnlyLargestDivided { get; set; }
        }

        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "ODFT",
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
            // ====== Predefined Config ==========
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
            {
                if (ConfigRowInput == ConfigRowData.Predefined)
                {
                    string msg = "'Predefined Config' is designed only for Standard Timeframe (Minutes, Hours, Days, Weekly, Monthly)\n\n use 'Custom Config' to others Chart Timeframes (Renko/Range/Ticks).";
                    Chart.DrawStaticText("txt", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Orange);
                    isWrong = true;
                    return;
                }
                heightPips = CustomHeightInput;
            }

            void SetHeightPips(double digits5, double digits2)
            {
                if (Symbol.Digits == 5)
                    heightPips = digits5;
                else if (Symbol.Digits == 2)
                {
                    heightPips = digits2;
                    if (Symbol.PipSize == 0.1)
                        heightPips /= 2;
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
            TicksOHLC = MarketData.GetBars(TimeFrame.Tick);

            string currentTimeframe = Chart.TimeFrame.ToString();
            if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick"))
                Bars.BarOpened += SetNewBar;

            if (LoadFromInput != LoadFromData.Existing_on_Chart)
                VolumeInitialize();

            // Ex: 4 pips to Volume calculation(rowHeight)
            rowHeight = Symbol.PipSize * heightPips;

            DrawOnScreen("Calculating...");
            Second_DrawOnScreen("Taking too long? \nSet Nº Bars to Show");

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
                NBars = Lookback,
                VolMode = VolumeModeInput,
                VolView = VolumeViewInput,
                RowHeight = rowHeight,
                ResultType = ResultsTypeInput,
                OperatorBuySell = OperatorBuySellInput,
                OnlyLargestDivided = ColorOnlyLarguestInput
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
            if (isWrong)
                return;

            // Removing Messages
            if (!IsLastBar)
            {
                DrawOnScreen("");
                Second_DrawOnScreen("");
            }
            // LookBack
            if (index < (Bars.OpenTimes.GetIndexByTime(Server.Time) - Lookback) && (Lookback != -1 && Lookback > 0))
                return;

            // Historical data
            if (!IsLastBar)
            {
                if (!isLive)
                    CreateOrderFlow(index);
                else
                    isNewBar = true;
            }
            else
            {
                // Required for Non-Time based charts (Renko, Range, Ticks)
                isLive = true;
                if (isNewBar)
                {
                    string currentTimeframe = Chart.TimeFrame.ToString();
                    if (VolumeModeInput == VolumeModeData.Normal && !isCalcFinished &&
                    (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range")))
                    {
                        isCalcFinished = true;
                        CreateOrderFlow(index - 1);
                        isCalcLocked = true;
                        return;
                    }
                    CreateOrderFlow(index - 1);
                    isNewBar = false;
                    return;
                }

                CreateOrderFlow(index);
            }

            void CreateOrderFlow(int idx)
            {
                Segments.Clear();
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                OrderFlow(idx);
            }
        }

        private void OrderFlow(int iStart)
        {
            // ======= Highest and Lowest =======
            double highest = Bars.HighPrices[iStart], lowest = Bars.LowPrices[iStart], open = Bars.OpenPrices[iStart];

            if (Chart.TimeFrame.ToString().Contains("Renko") && ShowWicks)
            {
                var currentOpenTime = Bars.OpenTimes[iStart];
                var NextOpenTime = Bars.OpenTimes[iStart + 1];
                bool isBullish = (Bars.ClosePrices[iStart] > Bars.OpenPrices[iStart]);

                if (isBullish)
                    lowest = GetWicks(currentOpenTime, NextOpenTime, isBullish);
                else
                    highest = GetWicks(currentOpenTime, NextOpenTime, isBullish);
            }

            List<double> currentSegments = new();
            double prevSegment = open;
            while (prevSegment >= (lowest - rowHeight))
            {
                currentSegments.Add(prevSegment);
                prevSegment = Math.Abs(prevSegment - rowHeight);
            }
            prevSegment = open;
            while (prevSegment <= (highest + rowHeight))
            {
                currentSegments.Add(prevSegment);
                prevSegment = Math.Abs(prevSegment + rowHeight);
            }
            Segments = currentSegments.OrderBy(x => x).ToList();

            // ======= Volume on Tick =======
            VP_Tick(iStart);

            // ======= Drawing =======
            if (Segments.Count == 0)
                return;

            double loopPrevSegment = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                if (loopPrevSegment == 0)
                    loopPrevSegment = Segments[i];

                double priceKey = Segments[i];
                if (!VolumesRank.ContainsKey(priceKey))
                    continue;

                int largestVOL = VolumesRank.Values.Max();

                // =======  HISTOGRAMs + Texts  =======
                /*
                Indeed, the value of X-Axis is simply a rule of three,
                where the maximum value of the respective side (One/Buy/Sell) will be the maxLength (in Milliseconds),
                from there the math adjusts the histograms.

                    MaxValue    maxLength(ms)
                       x             ?(ms)

                The values 1.50 and 3 are the manually set values like the size of the Bar body in any timeframe (Candle, Ticks, Renko, Range)
                */

                double lowerSegment = loopPrevSegment;
                double upperSegment = Segments[i];

                string currentTimeframe = Chart.TimeFrame.ToString();

                // All Volume
                double maxLength = 0;
                if (!IsLastBar)
                    maxLength = Bars[iStart + 1].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                else
                {
                    maxLength = Bars[iStart].OpenTime.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                    if ((currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                    && VolumeModeInput == VolumeModeData.Normal && isCalcFinished && !isCalcLocked)
                        maxLength = Bars[iStart + 1].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                }

                double proportion = VolumesRank[priceKey] * (maxLength - (maxLength / 1.50));
                double dynLength = proportion / largestVOL;

                // Bull/Up/Buy
                double proportion_Up = VolumesRank_Up[priceKey] * (maxLength - (maxLength / 1.50));
                double dynLength_Up = proportion_Up / VolumesRank_Up.Values.Max();
                // Bear/Down/Sell
                double maxLength_Left = Bars[iStart].OpenTime.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                double proportion_Down = VolumesRank_Down[priceKey] * (maxLength_Left - (maxLength_Left / 1.50));
                double dynLength_Down = proportion_Down / VolumesRank_Down.Values.Max();
                // Delta
                double proportion_Delta = DeltaRank[priceKey] * (maxLength - (maxLength / 1.50));
                double dynLength_Delta = proportion_Delta / DeltaRank.Values.Max();

                if (DeltaRank[priceKey] < 0 && VolumeViewInput == VolumeViewData.Divided && VolumeModeInput == VolumeModeData.Delta)
                {
                    // Negative Delta
                    proportion_Delta = DeltaRank[priceKey] * (maxLength_Left - (maxLength_Left / 1.50));
                    dynLength_Delta = proportion_Delta / DeltaRank.Values.Where(n => n < 0).Min();
                }

                if (VolumeViewInput == VolumeViewData.Profile && VolumeModeInput == VolumeModeData.Buy_Sell)
                {
                    // Buy vs Sell = Pseudo Delta
                    int buyVolume = VolumesRank_Up.Values.Max();
                    int sellVolume = VolumesRank_Down.Values.Max();
                    int sideVolMax = buyVolume > sellVolume ? buyVolume : sellVolume;

                    proportion_Up = VolumesRank_Up[priceKey] * (maxLength - (maxLength / 1.20));
                    dynLength_Up = proportion_Up / sideVolMax;
                    proportion_Down = VolumesRank_Down[priceKey] * (maxLength - (maxLength / 1.50));
                    dynLength_Down = proportion_Down / sideVolMax;
                }
                else if (VolumeViewInput == VolumeViewData.Profile && VolumeModeInput == VolumeModeData.Delta)
                {
                    int Positive_Delta = DeltaRank.Values.Max();
                    IEnumerable<int> allNegative = DeltaRank.Values.Where(n => n < 0);
                    int Negative_Delta = 0;
                    try { Negative_Delta = Math.Abs(allNegative.Min()); } catch { }

                    int deltaMax = Positive_Delta > Negative_Delta ? Positive_Delta : Negative_Delta;

                    dynLength_Delta = proportion_Delta / deltaMax;
                }

                if (VolumeModeInput == VolumeModeData.Normal)
                {
                    Color dynColor = VolumesRank[priceKey] != largestVOL ? VolumeColor : VolumeLargeColor;
                    ChartRectangle volHist;
                    if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                        volHist = Chart.DrawRectangle($"{iStart}_{i}", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, dynColor);
                    else
                        volHist = Chart.DrawRectangle($"{iStart}_{i}", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength / 3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength / 3)).AddMilliseconds(dynLength * 2), upperSegment, dynColor);

                    if (FillHist)
                        volHist.IsFilled = true;

                    if (ShowNumbers)
                    {
                        ChartText C = Chart.DrawText($"{iStart}_{i}Center", $"{VolumesRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                        C.HorizontalAlignment = HorizontalAlignment.Center;
                        C.FontSize = FontSizeNumbers;
                    }
                    if (ShowResults)
                    {
                        ChartText Center;
                        Color dynResColor = ResultsColoringInput == ResultsColoringData.Fixed ? RtnbFixedColor : VolumeColor;
                        Center = Chart.DrawText($"{iStart}SumCenter", $"\n{VolumesRank.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynResColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;

                        if (EnableFilter)
                        {
                            DynamicSeries[iStart] = VolumesRank.Values.Sum();

                            // ====== Dynamic Series Filter ======
                            double DynamicFilter = DynamicSeries[iStart] / MADynamic.Result[iStart];
                            double DynamicLarge = DynamicFilter >= Filter_Ratio ? DynamicSeries[iStart] : 0;

                            Color dynBarColor = DynamicLarge >= 2 ? ColorLargeResult : dynResColor;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                        }
                    }

                }
                else if (VolumeModeInput == VolumeModeData.Buy_Sell)
                {
                    Color dynColorBuy = VolumesRank_Up[priceKey] != VolumesRank_Up.Values.Max() ? BuyColor : BuyLargeColor;
                    Color dynColorSell = VolumesRank_Down[priceKey] != VolumesRank_Down.Values.Max() ? SellColor : SellLargeColor;

                    ChartRectangle buyHist;
                    ChartRectangle sellHist;
                    if (VolumeViewInput == VolumeViewData.Divided)
                    {
                        buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Up), upperSegment, dynColorBuy);
                        sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Down), upperSegment, dynColorSell);
                    }
                    else
                    {
                        if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                        {
                            sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Down), upperSegment, SellColor);
                            buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Up), upperSegment, BuyColor);
                        }
                        else
                        {
                            sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength / 3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength / 3)).AddMilliseconds(dynLength_Down * 2), upperSegment, SellColor);
                            buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength / 3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength / 3)).AddMilliseconds(dynLength_Up * 2), upperSegment, BuyColor);
                        }
                    }

                    if (FillHist)
                    {
                        buyHist.IsFilled = true;
                        sellHist.IsFilled = true;
                    }

                    if (ShowNumbers)
                    {
                        ChartText L = Chart.DrawText($"{iStart}_{i}SellNumber", $"{VolumesRank_Down[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                        ChartText R = Chart.DrawText($"{iStart}_{i}BuyNumber", $"{VolumesRank_Up[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);

                        if (VolumeViewInput == VolumeViewData.Divided)
                        {
                            L.HorizontalAlignment = HorizontalAlignment.Left;
                            R.HorizontalAlignment = HorizontalAlignment.Right;
                        }
                        else
                        {
                            L.HorizontalAlignment = HorizontalAlignment.Right;
                            R.HorizontalAlignment = HorizontalAlignment.Left;
                        }
                        L.FontSize = FontSizeNumbers;
                        R.FontSize = FontSizeNumbers;
                    }

                    if (ShowResults)
                    {
                        var selected = ResultsTypeInput;

                        int volBuy = VolumesRank_Up.Values.Sum();
                        int volSell = VolumesRank_Down.Values.Sum();

                        Color compare = volBuy > volSell ? BuyColor : volBuy < volSell ? SellColor : RtnbFixedColor;
                        Color dynColorCenter = ResultsColoringInput == ResultsColoringData.Fixed ? RtnbFixedColor : compare;

                        if (ShowSideTotalInput)
                        {
                            Color dynColorLeft = ResultsColoringInput == ResultsColoringData.Fixed ? RtnbFixedColor : SellColor;
                            Color dynColorRight = ResultsColoringInput == ResultsColoringData.Fixed ? RtnbFixedColor : BuyColor;

                            int percentBuy = (volBuy * 100) / (volBuy + volSell);
                            int percentSell = (volSell * 100) / (volBuy + volSell);

                            string dynStrBuy = selected == ResultsType_Data.Percentage ? $"\n{percentBuy}%" : selected == ResultsType_Data.Value ? $"\n{volBuy}" : $"\n{percentBuy}%\n({volBuy})";
                            string dynStrSell = selected == ResultsType_Data.Percentage ? $"\n{percentSell}%" : selected == ResultsType_Data.Value ? $"\n{volSell}" : $"\n{percentSell}%\n({volSell})";

                            ChartText Left, Right;
                            Left = Chart.DrawText($"{iStart}SellSum", $"{dynStrSell}", Bars.OpenTimes[iStart], lowest, dynColorLeft);
                            Right = Chart.DrawText($"{iStart}BuySum", $"{dynStrBuy}", Bars.OpenTimes[iStart], lowest, dynColorRight);

                            if (VolumeViewInput == VolumeViewData.Divided)
                            {
                                Left.HorizontalAlignment = HorizontalAlignment.Left;
                                Right.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                            else
                            {
                                Left.HorizontalAlignment = HorizontalAlignment.Right;
                                Right.HorizontalAlignment = HorizontalAlignment.Left;
                            }

                            Left.FontSize = FontSizeResults;
                            Right.FontSize = FontSizeResults;
                        }

                        string dynSpaceSum = (selected == ResultsType_Data.Percentage || selected == ResultsType_Data.Value) ? $"\n\n" : $"\n\n\n";
                        ChartText Center;
                        if (OperatorBuySellInput == OperatorBuySell_Data.Sum)
                            Center = Chart.DrawText($"{iStart}SumCenter", $"{dynSpaceSum}{VolumesRank_Up.Values.Sum() + VolumesRank_Down.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynColorCenter);
                        else
                            Center = Chart.DrawText($"{iStart}SumCenter", $"{dynSpaceSum}{VolumesRank_Up.Values.Sum() - VolumesRank_Down.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynColorCenter);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults;

                        if (EnableFilter)
                        {
                            if (OperatorBuySellInput == OperatorBuySell_Data.Sum)
                                DynamicSeries[iStart] = VolumesRank_Up.Values.Sum() + VolumesRank_Down.Values.Sum();
                            else
                                DynamicSeries[iStart] = VolumesRank_Up.Values.Sum() - VolumesRank_Down.Values.Sum();

                            // ====== Dynamic Series Filter ======
                            double DynamicFilter = DynamicSeries[iStart] / MADynamic.Result[iStart];
                            double DynamicLarge = DynamicFilter >= Filter_Ratio ? DynamicSeries[iStart] : 0;

                            Color dynBarColor = DynamicLarge >= 2 ? ColorLargeResult : dynColorCenter;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                        }
                    }

                }
                else
                {
                    IEnumerable<int> allNegative = DeltaRank.Values.Where(n => n < 0);
                    int Negative_Delta = 0;
                    try { Negative_Delta = allNegative.Min(); } catch { }

                    Color dynColorBuy = DeltaRank[priceKey] != DeltaRank.Values.Max() ? BuyColor : BuyLargeColor;
                    Color dynColorSell = DeltaRank[priceKey] != Negative_Delta ? SellColor : SellLargeColor;

                    if (ColorOnlyLarguestInput)
                    {
                        if (DeltaRank[priceKey] == DeltaRank.Values.Max())
                            dynColorBuy = DeltaRank.Values.Max() > Math.Abs(Negative_Delta) ? VolumeLargeColor : BuyColor;
                        if (DeltaRank[priceKey] == Negative_Delta)
                            dynColorSell = DeltaRank.Values.Max() < Math.Abs(Negative_Delta) ? VolumeLargeColor : SellColor;
                    }

                    ChartRectangle deltaHist;
                    if (VolumeViewInput == VolumeViewData.Divided)
                    {
                        try
                        {
                            if (DeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, dynColorBuy);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Delta), upperSegment, dynColorSell);
                        }
                        catch
                        {
                            if (DeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, dynColorBuy);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, dynColorSell);
                        }
                    }
                    else
                    {
                        try
                        {
                            if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                            {
                                if (DeltaRank[priceKey] >= 0)
                                    deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, BuyColor);
                                else
                                    deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Delta), upperSegment, SellColor);
                            }
                            else
                            {
                                if (DeltaRank[priceKey] >= 0)
                                    deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength / 3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength / 3)).AddMilliseconds(dynLength_Delta * 2), upperSegment, BuyColor);
                                else
                                    deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength / 3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength / 3)).AddMilliseconds(-dynLength_Delta * 2), upperSegment, SellColor);
                            }
                        }
                        catch
                        {
                            deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength / 3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength / 3)), upperSegment, RtnbFixedColor);
                        }

                    }

                    if (FillHist)
                        deltaHist.IsFilled = true;

                    if (ShowNumbers)
                    {
                        ChartText nbText;
                        if (DeltaRank[priceKey] > 0)
                        {
                            nbText = Chart.DrawText($"{iStart}_{i}DynNumberDelta", $"{DeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                            if (VolumeViewInput == VolumeViewData.Divided)
                                nbText.HorizontalAlignment = HorizontalAlignment.Right;
                            else
                                nbText.HorizontalAlignment = HorizontalAlignment.Center;

                            nbText.FontSize = FontSizeNumbers;
                        }
                        else if (DeltaRank[priceKey] < 0)
                        {
                            nbText = Chart.DrawText($"{iStart}_{i}DynNumberDelta", $"{DeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                            if (VolumeViewInput == VolumeViewData.Divided)
                                nbText.HorizontalAlignment = HorizontalAlignment.Left;
                            else
                                nbText.HorizontalAlignment = HorizontalAlignment.Center;
                            nbText.FontSize = FontSizeNumbers;
                        }
                        else
                        {
                            nbText = Chart.DrawText($"{iStart}_{i}DynNumberDelta", $"{DeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                            nbText.HorizontalAlignment = HorizontalAlignment.Center;
                            nbText.FontSize = FontSizeNumbers;
                        }
                    }

                    // =======  Results  =======
                    if (ShowResults)
                    {
                        var selected = ResultsTypeInput;

                        Color dynColorLeft = ResultsColoringInput == ResultsColoringData.Fixed ? RtnbFixedColor : SellColor;
                        Color dynColorRight = ResultsColoringInput == ResultsColoringData.Fixed ? RtnbFixedColor : BuyColor;

                        Color compareSumD = DeltaRank.Values.Sum() > 0 ? BuyColor : DeltaRank.Values.Sum() < 0 ? SellColor : SellLargeColor;
                        Color dynColorCenter = ResultsColoringInput == ResultsColoringData.Fixed ? SellLargeColor : compareSumD;

                        if (ShowSideTotalInput)
                        {
                            int deltaBuy = DeltaRank.Values.Where(n => n > 0).Sum();
                            int deltaSell = DeltaRank.Values.Where(n => n < 0).Sum();

                            int percentBuy = 0;
                            int percentSell = 0;
                            try { percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { };
                            try { percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { }

                            string dynStrBuy = selected == ResultsType_Data.Percentage ? $"\n{percentBuy}%" : selected == ResultsType_Data.Value ? $"\n{deltaBuy}" : $"\n{percentBuy}%\n({deltaBuy})";
                            string dynStrSell = selected == ResultsType_Data.Percentage ? $"\n{percentSell}%" : selected == ResultsType_Data.Value ? $"\n{deltaSell}" : $"\n{percentSell}%\n({deltaSell})";

                            ChartText Left, Right;
                            Left = Chart.DrawText($"{iStart}SumDeltaSell", $"{dynStrSell}", Bars.OpenTimes[iStart], lowest, dynColorLeft);
                            Right = Chart.DrawText($"{iStart}SumDeltaBuy", $"{dynStrBuy}", Bars.OpenTimes[iStart], lowest, dynColorRight);
                            Left.HorizontalAlignment = HorizontalAlignment.Left;
                            Left.FontSize = FontSizeResults;
                            Right.HorizontalAlignment = HorizontalAlignment.Right;
                            Right.FontSize = FontSizeResults;
                        }

                        ChartText Center;
                        string dynSpaceSum = (selected == ResultsType_Data.Percentage || selected == ResultsType_Data.Value) ? $"\n\n" : $"\n\n\n";
                        Center = Chart.DrawText($"{iStart}SumDeltaCenter", $"{dynSpaceSum}{DeltaRank.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynColorCenter);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults;

                        if (!CumulDeltaRank.ContainsKey(iStart))
                            CumulDeltaRank.Add(iStart, DeltaRank.Values.Sum());
                        else
                            CumulDeltaRank[iStart] = DeltaRank.Values.Sum();

                        int CumulDelta = CumulDeltaRank.Keys.Count <= 1 ? CumulDeltaRank[iStart] : (CumulDeltaRank[iStart] + CumulDeltaRank[iStart - 1]);
                        int prevCumulDelta = CumulDeltaRank.Keys.Count <= 2 ? CumulDeltaRank[iStart] : (CumulDeltaRank[iStart - 1] + CumulDeltaRank[iStart - 2]);

                        Color compareCD = CumulDelta > prevCumulDelta ? BuyColor : CumulDelta < prevCumulDelta ? SellColor : SellLargeColor;
                        Color dynColorCD = ResultsColoringInput == ResultsColoringData.Fixed ? SellLargeColor : compareCD;

                        ChartText CD = Chart.DrawText($"{iStart}CD", $"\n{CumulDelta}\n", Bars.OpenTimes[iStart], highest, dynColorCD);
                        CD.HorizontalAlignment = HorizontalAlignment.Center;
                        CD.VerticalAlignment = VerticalAlignment.Top;
                        CD.FontSize = FontSizeResults;

                        if (EnableFilter)
                        {
                            CumulDeltaSeries[iStart] = Math.Abs(CumulDeltaRank[iStart]);
                            DynamicSeries[iStart] = Math.Abs(DeltaRank.Values.Sum());

                            // ====== Dynamic Series Filter ======
                            double DynamicFilter = DynamicSeries[iStart] / MADynamic.Result[iStart];
                            double DynamicLarge = DynamicFilter >= Filter_Ratio ? DynamicSeries[iStart] : 0;

                            Color dynBarColor = DynamicLarge >= 2 ? ColorLargeResult : dynColorCenter;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);

                            if (ColoringCD)
                            {
                                // ====== Cumul Delta Filter ======
                                double CumulDeltaFilter = CumulDeltaSeries[iStart] / MACumulDelta.Result[iStart];
                                double CumulDeltaLarge = CumulDeltaFilter > Filter_Ratio ? CumulDeltaSeries[iStart] : 0;
                                Color dynCDColor = CumulDeltaLarge > 2 ? ColorLargeResult : dynColorCD;
                                CD.Color = dynCDColor;
                            }
                        }
                    }
                }

                loopPrevSegment = Segments[i];
            }
        }
        // ====== Functions Area ======
        private void VP_Tick(int index)
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];

            if (IsLastBar)
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

                RankVolume(tickBar.Close);
                prevTick = tickBar.Close;
            }
            // ========= ========== ==========
            void RankVolume(double tickPrice)
            {
                double prevSegmentValue = 0.0;
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (prevSegmentValue != 0 && tickPrice >= prevSegmentValue && tickPrice <= Segments[i])
                    {
                        double priceKey = Segments[i];

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

                        break;
                    }
                    prevSegmentValue = Segments[i];
                }
            }
        }

        private double GetWicks(DateTime startTime, DateTime endTime, bool isBullish)
        {
            double min = Int32.MaxValue;
            double max = 0;

            if (IsLastBar)
                endTime = TicksOHLC.Last().OpenTime;

            for (int tickIndex = 0; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = TicksOHLC[tickIndex];

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

        private void DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
        }
        private void Second_DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt2", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }
        private void SetNewBar(BarOpenedEventArgs obj)
        {
            isNewBar = true;
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
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out fromDateTime))
                {
                    if (fromDateTime > Server.Time.Date)
                    {
                        // for Log
                        fromDateTime = Server.Time.Date;
                        Print($"Invalid DateTime '{StringDate}'. Using '{fromDateTime}'");
                    }
                }
                else
                {
                    // for Log
                    fromDateTime = Server.Time.Date;
                    Print($"Invalid DateTime '{StringDate}'. Using '{fromDateTime}'");
                }
            }
            else
            {
                DateTime LastBarTime = Bars.LastBar.OpenTime.Date;
                if (LoadFromInput == LoadFromData.Today)
                    fromDateTime = LastBarTime.Date;
                else if (LoadFromInput == LoadFromData.Yesterday)
                    fromDateTime = LastBarTime.AddDays(-1);
                else if (LoadFromInput == LoadFromData.One_Week)
                    fromDateTime = LastBarTime.AddDays(-5);
                else if (LoadFromInput == LoadFromData.Two_Week)
                    fromDateTime = LastBarTime.AddDays(-10);
                else if (LoadFromInput == LoadFromData.Monthly)
                    fromDateTime = LastBarTime.AddMonths(-1);
            }

            // ==== Check if existing ticks data on the chart really needs more data ====
            DateTime FirstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
            if (FirstTickTime >= fromDateTime)
            {
                LoadMoreTicks(fromDateTime);
                DrawOnScreen("Data Collection Finished \n Calculating...");
            }
            else
            {
                Print($"Using existing tick data from '{FirstTickTime}'");
                DrawOnScreen($"Using existing tick data from '{FirstTickTime}' \n Calculating...");
            }
        }
        private void LoadMoreTicks(DateTime fromDateTime)
        {
            bool msg = false;

            while (TicksOHLC.OpenTimes.FirstOrDefault() > fromDateTime)
            {
                if (!msg)
                {
                    Print($"Loading from '{TicksOHLC.OpenTimes.First()}' to '{fromDateTime}'...");
                    msg = true;
                }

                int loadedCount = TicksOHLC.LoadMoreHistory();
                Print("Loaded {0} Ticks, Current Tick Date: {1}", loadedCount, TicksOHLC.OpenTimes.FirstOrDefault());
                if (loadedCount == 0)
                    break;
            }
            Print("Data Collection Finished, First Tick from: {0}", TicksOHLC.OpenTimes.FirstOrDefault());
        }

        public void ClearAndRecalculate()
        {
            Chart.RemoveAllObjects();
            Chart.ResetBarColors();

            int index;
            if (Lookback != -1 && Lookback > 0)
                index = Bars.OpenTimes.GetIndexByTime(Server.Time)-Lookback;
            else
                index = Bars.OpenTimes.GetIndexByTime(TicksOHLC.OpenTimes.FirstOrDefault());

            // Historical data
            // index++ every crash to avoid DELTA VOLUME Crash (the given X key is not in dictionary) when re-calculating
            for (int i = index; i < Bars.Count; i++)
            {
                try {
                    CreateOrderflow(i);
                } catch {
                    index++;
                }
            }

            void CreateOrderflow(int i) {
                Segments.Clear();
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                OrderFlow(i);
            }
        }

        public void SetRowHeight(double number) {
            rowHeight = number;
        }
        public void SetLookback(int number) {
            Lookback = number;
        }
        public double GetRowHeight() {
            return rowHeight;
        }
        public double GetLookback() {
            return Lookback;
        }
    }

    public class ParamsPanel : CustomControl
    {
        // Any
        private const string ProfileOrDivided_Inputkey = "ProfileOrDividedKey";
        private const string BarsToShow_InputKey = "BarsToShowKey";
        private const string RowHeight_InputKey = "RowHeightKey";
        private const string ResultType_InputKey = "ResultTypeKey";
        // Delta
        private const string onlyLargestDivided_InputKey = "onlyLargestDividedKey";
        // Buy vs Sell
        private const string Operator_InputKey = "OperatorKey";

        private readonly IDictionary<string, TextBox> textInputMap = new Dictionary<string, TextBox>();
        private readonly IDictionary<string, CheckBox> checkBoxMap = new Dictionary<string, CheckBox>();
        private readonly IDictionary<string, ComboBox> comboBoxMap = new Dictionary<string, ComboBox>();
        private readonly OrderFlowTicksV20 Outside;

        private Button ModeBtn;
        private readonly Color BtnColor;
        private readonly IndicatorParams FirstParams;

        private Panel ResultTypePanel;
        private Panel VolumeViewPanel;
        private Panel OperatorPanel;
        private ControlBase OnlyLarguestPanel;
        private ControlBase EmptyDeltaPanel;
        private ControlBase EmptyBuySellPanel;

        public ParamsPanel(OrderFlowTicksV20 indicator, IndicatorParams defaultParams)
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
                Text = "Order Flow Ticks",
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

            var BarsToShow_Input = CreateInputWithLabel("Nº Bars", FirstParams.NBars.ToString(), BarsToShow_InputKey);
            grid.AddChild(BarsToShow_Input, 1, 0);

            var RowHeightInput = CreateInputWithLabel("Row Height", FirstParams.RowHeight.ToString("0.############################"), RowHeight_InputKey);
            grid.AddChild(RowHeightInput, 1, 2);

            ResultTypePanel = CreateComboBoxWithLabel("Result Type", ResultType_InputKey);
            VolumeViewPanel = CreateComboBoxWithLabel("Volume View", ProfileOrDivided_Inputkey);
            OnlyLarguestPanel = CreateCheckboxWithLabel("Only Larguest?", FirstParams.OnlyLargestDivided, onlyLargestDivided_InputKey);
            OnlyLarguestPanel.IsVisible = false;
            OperatorPanel = CreateComboBoxWithLabel("Operator", Operator_InputKey);
            OperatorPanel.IsVisible = false;

            EmptyDeltaPanel = CreateEmptyFill();
            EmptyBuySellPanel = CreateEmptyFill();
            EmptyBuySellPanel.IsVisible = false;

            grid.AddChild(VolumeViewPanel, 1, 4);
            grid.AddChild(ResultTypePanel, 2, 0);
            grid.AddChild(OnlyLarguestPanel, 2, 2, 2, 4);
            grid.AddChild(EmptyDeltaPanel, 2, 2, 2, 4);
            grid.AddChild(EmptyBuySellPanel, 2, 2);
            grid.AddChild(OperatorPanel, 2, 4);

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

            if (inputKey.Equals("ResultTypeKey")) {
                string[] enumNames = Enum.GetNames(typeof(ResultsType_Data));
                foreach (var item in enumNames) {
                    comboBox.AddItem(item);
                }
                comboBox.SelectedItem = FirstParams.ResultType.ToString();
            } else if (inputKey.Equals("ProfileOrDividedKey")) {
                string[] enumNames = Enum.GetNames(typeof(VolumeViewData));
                foreach (var item in enumNames) {
                    comboBox.AddItem(item);
                }
                comboBox.SelectedItem = FirstParams.VolView.ToString();
            } else {
                string[] enumNames = Enum.GetNames(typeof(OperatorBuySell_Data));
                foreach (var item in enumNames) {
                    comboBox.AddItem(item);
                }
                comboBox.SelectedItem = FirstParams.OperatorBuySell.ToString();
            }

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

        private static ControlBase CreateEmptyFill()
        {
            Border border = new()
            {
                Margin = "0 17 0 05",
                BorderThickness = "0 1 0 1",
                Style = Styles.CreateCommonBorderStyle()
            };

            StackPanel stackPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Margin = "0 1 0 1"
            };

            border.Child = stackPanel;
            return border;
        }

        private void RecalculateOutsideWithMsg() {
            string currentMode = ModeBtn.Text;
            ModeBtn.Text = $"{currentMode}\nCalculating...";
            Outside.BeginInvokeOnMainThread(() => {
                Outside.ClearAndRecalculate();
                ModeBtn.Text = currentMode;
            });
        }

        private void NextModeEvent(ButtonClickEventArgs obj)
        {
            if (Outside.VolumeModeInput == VolumeModeData.Normal)
            {
                Outside.VolumeModeInput = VolumeModeData.Buy_Sell;
                ModeBtn.Text = "Buy_Sell";

                ResultTypePanel.IsVisible = true;
                VolumeViewPanel.IsVisible = true;

                EmptyBuySellPanel.IsVisible = true;
                OperatorPanel.IsVisible = true;

                OnlyLarguestPanel.IsVisible = false;
                EmptyDeltaPanel.IsVisible = false;
            }
            else if (Outside.VolumeModeInput == VolumeModeData.Buy_Sell)
            {
                Outside.VolumeModeInput = VolumeModeData.Delta;
                ModeBtn.Text = "Delta";

                ResultTypePanel.IsVisible = true;
                VolumeViewPanel.IsVisible = true;

                OnlyLarguestPanel.IsVisible = Outside.VolumeViewInput == VolumeViewData.Divided;
                EmptyDeltaPanel.IsVisible = Outside.VolumeViewInput == VolumeViewData.Profile;

                EmptyBuySellPanel.IsVisible = false;
                OperatorPanel.IsVisible = false;
            }

            RecalculateOutsideWithMsg();
        }
        private void PrevModeEvent(ButtonClickEventArgs obj)
        {
            if (Outside.VolumeModeInput == VolumeModeData.Delta)
            {
                Outside.VolumeModeInput = VolumeModeData.Buy_Sell;
                ModeBtn.Text = "Buy_Sell";

                ResultTypePanel.IsVisible = true;
                VolumeViewPanel.IsVisible = true;

                EmptyBuySellPanel.IsVisible = true;
                OperatorPanel.IsVisible = true;

                OnlyLarguestPanel.IsVisible = false;
                EmptyDeltaPanel.IsVisible = false;
            }
            else if (Outside.VolumeModeInput == VolumeModeData.Buy_Sell)
            {
                Outside.VolumeModeInput = VolumeModeData.Normal;
                ModeBtn.Text = "Normal";
                ResultTypePanel.IsVisible = false;
                VolumeViewPanel.IsVisible = false;

                EmptyBuySellPanel.IsVisible = false;
                OperatorPanel.IsVisible = false;

                OnlyLarguestPanel.IsVisible = false;
                EmptyDeltaPanel.IsVisible = false;
            }

            RecalculateOutsideWithMsg();
        }
        private void ChangeParams(IndicatorParams indicatorParams)
        {
            foreach (var key in checkBoxMap.Keys)
            {
                switch (key)
                {
                    case "onlyLargestDividedKey": checkBoxMap[key].IsChecked = indicatorParams.OnlyLargestDivided; break;
                }
            }
            foreach (var key in textInputMap.Keys)
            {
                switch (key)
                {
                    case "BarsToShowKey": textInputMap[key].Text = indicatorParams.NBars.ToString(); break;
                    case "RowHeightKey": textInputMap[key].Text = indicatorParams.RowHeight.ToString("0.############################"); break;
                }
            }
            foreach (var key in comboBoxMap.Keys)
            {
                switch (key)
                {
                    case "ResultTypeKey": comboBoxMap[key].SelectedItem = indicatorParams.ResultType.ToString(); break;
                    case "ProfileOrDividedKey": comboBoxMap[key].SelectedItem = indicatorParams.VolView.ToString(); break;
                    case "OperatorKey": comboBoxMap[key].SelectedItem = indicatorParams.OperatorBuySell.ToString(); break;
                }
            }
        }
        private void ResetParamsEvent(ButtonClickEventArgs obj)
        {
            ChangeParams(FirstParams);
        }
        private void TextChangedEvent(TextChangedEventArgs obj)
        {
            int nBars = GetValueFromInput(BarsToShow_InputKey, -1);
            double rowHeight = GetDoubleFromInput(RowHeight_InputKey, -1);

            if (rowHeight != -1 && rowHeight > 0 && rowHeight != Outside.GetRowHeight()) {
                Outside.SetRowHeight(rowHeight);
                RecalculateOutsideWithMsg();
            }
            if ((nBars == -1 || nBars > 0) && nBars != Outside.GetLookback()) {
                Outside.SetLookback(nBars);
                RecalculateOutsideWithMsg();
            }
        }
        private void ComboBoxSelectedEvent(ComboBoxSelectedItemChangedEventArgs obj)
        {
            foreach (var key in comboBoxMap.Keys)
            {
                switch (key)
                {
                    case "ResultTypeKey": {
                        string selected = comboBoxMap[key].SelectedItem;
                        if (selected != Outside.ResultsTypeInput.ToString()) {
                            _ = Enum.TryParse(selected, out ResultsType_Data dynamicEnum);
                            Outside.ResultsTypeInput = dynamicEnum;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                    case "ProfileOrDividedKey": {
                        string selected = comboBoxMap[key].SelectedItem;
                        if (selected != Outside.VolumeViewInput.ToString()) {
                            _ = Enum.TryParse(selected, out VolumeViewData dynamicEnum);
                            Outside.VolumeViewInput = dynamicEnum;
                            RecalculateOutsideWithMsg();
                        }
                        if (selected == VolumeViewData.Divided.ToString()) {
                            if (Outside.VolumeModeInput == VolumeModeData.Delta) {
                                EmptyDeltaPanel.IsVisible = false;
                                OnlyLarguestPanel.IsVisible = true;
                            }
                            else {
                                EmptyBuySellPanel.IsVisible = true;
                                EmptyDeltaPanel.IsVisible = false;
                                OnlyLarguestPanel.IsVisible = false;
                            }
                        }
                        else {
                            EmptyDeltaPanel.IsVisible = true;
                            OnlyLarguestPanel.IsVisible = false;
                        }
                        break;
                    }
                    case "OperatorKey": {
                        string selected = comboBoxMap[key].SelectedItem;
                        if (selected != Outside.OperatorBuySellInput.ToString()) {
                            _ = Enum.TryParse(selected, out OperatorBuySell_Data dynamicEnum);
                            Outside.OperatorBuySellInput = dynamicEnum;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                }
            }
        }
        private void CheckBoxClickEvent(CheckBoxEventArgs obj)
        {
            bool value = GetValueFromCheckbox(onlyLargestDivided_InputKey, true);
            if (value != Outside.ColorOnlyLarguestInput) {
                Outside.ColorOnlyLarguestInput = value;
                RecalculateOutsideWithMsg();
            }
        }
        private int GetValueFromInput(string inputKey, int defaultValue)
        {
            return int.TryParse(textInputMap[inputKey].Text, out int value) ? value : defaultValue;
        }
        private double GetDoubleFromInput(string inputKey, int defaultValue)
        {
            return double.TryParse(textInputMap[inputKey].Text, out double value) ? value : defaultValue;
        }
        private bool GetValueFromCheckbox(string inputKey, bool defaultValue)
        {
            return checkBoxMap[inputKey].IsChecked ?? defaultValue;
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