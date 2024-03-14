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

        public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }
        [Parameter("[Buy&Sell] Operator", DefaultValue = OperatorBuySell_Data.Sum, Group = "==== Results/Numbers ====")]
        public OperatorBuySell_Data OperatorBuySellInput { get; set; }

        public enum ResultsType_Data
        {
            Percentage,
            Value,
            Both
        }
        [Parameter("Results Type:", DefaultValue = ResultsType_Data.Percentage, Group = "==== Results/Numbers ====")]
        public ResultsType_Data ResultsTypeInput { get; set; }

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
        public Colors RawColorLarguest { get; set; }

        [Parameter("[Delta] Only Larguest?", DefaultValue = true, Group = "==== Volume ====")]
        public bool ColorOnlyLarguestInput { get; set; }


        [Parameter("Color Buy:", DefaultValue = Colors.DeepSkyBlue, Group = "==== Buy ====")]
        public Colors RawColorBuy { get; set; }

        [Parameter("Color Largest Buy:", DefaultValue = Colors.Gold, Group = "==== Buy ====")]
        public Colors RawColorBuyLarguest { get; set; }


        [Parameter("Color Sell:", DefaultValue = Colors.Crimson, Group = "==== Sell ====")]
        public Colors RawColorSell { get; set; }

        [Parameter("Color Largest Sell:", DefaultValue = Colors.Goldenrod, Group = "==== Sell ====")]
        public Colors RawColorSellLarguest { get; set; }


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


        private VerticalAlignment verticalAlign = VerticalAlignment.Top;
        private HorizontalAlignment horizontalAlign = HorizontalAlignment.Center;

        private List<double> Segments = new List<double>();
        private IDictionary<double, int> VolumesRank = new Dictionary<double, int>();
        private IDictionary<double, int> VolumesRank_Up = new Dictionary<double, int>();
        private IDictionary<double, int> VolumesRank_Down = new Dictionary<double, int>();
        private IDictionary<double, int> DeltaRank = new Dictionary<double, int>();
        private IDictionary<double, int> CumulDeltaRank = new Dictionary<double, int>();

        private double heightPips = 4;
        private double rowHeight = 0;

        private Color volumeColor;
        private Color buyColor;
        private Color sellColor;

        private Color volumeLargeColor;
        private Color buyLargeColor;
        private Color sellLargeColor;
        private Color rtnbFixedColor;

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
            else {
                if (ConfigRowInput == ConfigRowData.Predefined) {
                    string msg = "'Predefined Config' is designed only for Standard Timeframe (Minutes, Hours, Days, Weekly, Monthly)\n\n use 'Custom Config' to others Chart Timeframes (Renko/Range/Ticks).";
                    Chart.DrawStaticText("txt", $"{msg}", verticalAlign, horizontalAlign, Color.Orange);
                    isWrong = true;
                    return;
                }
                heightPips = CustomHeightInput;
            }

            void SetHeightPips(double digits5, double digits2) {
                if (Symbol.Digits == 5)
                    heightPips = digits5;
                else if (Symbol.Digits == 2)
                {
                    heightPips = digits2;
                    if (Symbol.PipSize == 0.1)
                        heightPips /= 2;
                }
            }

            if (EnableFilter) {
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
            rowHeight = (Symbol.PipSize) * heightPips;

            // ===== Colors with Opacity =====
            int opacity = (int)(2.55 * OpacityHist);
            Color rawHist = Color.FromName(RawColorHist.ToString());
            volumeColor = Color.FromArgb(opacity, rawHist.R, rawHist.G, rawHist.B);

            Color rawBuy = Color.FromName(RawColorBuy.ToString());
            buyColor = Color.FromArgb(opacity, rawBuy.R, rawBuy.G, rawBuy.B);

            Color rawSell = Color.FromName(RawColorSell.ToString());
            sellColor = Color.FromArgb(opacity, rawSell.R, rawSell.G, rawSell.B);

            // Largest Volume
            Color rawLarguest = Color.FromName(RawColorLarguest.ToString());
            volumeLargeColor = Color.FromArgb(opacity, rawLarguest.R, rawLarguest.G, rawLarguest.B);

            Color rawBuyLarguest = Color.FromName(RawColorBuyLarguest.ToString());
            buyLargeColor = Color.FromArgb(opacity, rawBuyLarguest.R, rawBuyLarguest.G, rawBuyLarguest.B);

            Color rawSellLarguest = Color.FromName(RawColorSellLarguest.ToString());
            sellLargeColor = Color.FromArgb(opacity, rawSellLarguest.R, rawSellLarguest.G, rawSellLarguest.B);

            // Fixed Result/Numbers Color
            int rtnbOpacity = (int)(2.55 * OpacityNumbers);
            Color rawFixed = Color.FromName(RawColorRtNb.ToString());
            rtnbFixedColor = Color.FromArgb(rtnbOpacity, rawFixed.R, rawFixed.G, rawFixed.B);

            // === Info Corner ===
            Color rawColor = Color.FromName(RawColorInfoC.ToString());
            Color infoColor = Color.FromArgb((int)(2.55 * 70), rawColor.R, rawColor.G, rawColor.B);
            string strMode = ConfigRowInput == ConfigRowData.Predefined ? "Predefined" : "Custom";
            string strVisual = (ModeVOLInput == ModeVOLData.Buy_Sell || ModeVOLInput == ModeVOLData.Delta) ? $"{DeltaVisualInput}" : "";
            string InfoText = $"{strVisual} \n" +
                             $"VOL {ModeVOLInput} \n" +
                             $"{strMode} Row \n" +
                             $"Row Height: {heightPips} pip(s) \n";

            VerticalAlignment vAlign = VerticalAlignment.Bottom;
            HorizontalAlignment hAlign = HorizontalAlignment.Left;

            if (ConfigInfoC_Input == ConfigInfoC.Bottom_Right)
                hAlign = HorizontalAlignment.Right;
            else if (ConfigInfoC_Input == ConfigInfoC.Top_Left)
                vAlign = VerticalAlignment.Top;
            else if (ConfigInfoC_Input == ConfigInfoC.Top_Right){
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Right;
            }

            Chart.DrawStaticText("Vol Info", InfoText, vAlign, hAlign, infoColor);

            DrawOnScreen("Calculating...");
            Second_DrawOnScreen("Taking too long? \nSet Nº Bars to Show");
        }

        public override void Calculate(int index)
        {
            if (isWrong)
                return;

            // Removing Messages
            if (!IsLastBar) {
                DrawOnScreen("");
                Second_DrawOnScreen("");
            }
            // LookBack
            if (index < (Bars.OpenTimes.GetIndexByTime(Server.Time)-Lookback) && (Lookback != -1 && Lookback > 0))
                return;

            // Historical data
            if (!IsLastBar)
            {
                if (!isLive)
                    CreateOrderFlow(index);
                else
                    isNewBar=true;
            }
            else
            {
               // Required for Non-Time based charts (Renko, Range, Ticks)
               isLive = true;
               if (isNewBar) {
                    string currentTimeframe = Chart.TimeFrame.ToString();
                    if (ModeVOLInput == ModeVOLData.Normal && !isCalcFinished &&
                    (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range")))
                    {
                        isCalcFinished=true;
                        CreateOrderFlow(index-1);
                        isCalcLocked = true;
                        return;
                    }
                    CreateOrderFlow(index-1);
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

            List<double> currentSegments = new List<double>();
            double prevSegment = open;
            while (prevSegment >= (lowest-rowHeight))
            {
                currentSegments.Add(prevSegment);
                prevSegment = Math.Abs(prevSegment - rowHeight);
            }
            prevSegment = open;
            while (prevSegment <= (highest+rowHeight))
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
                    maxLength = Bars[iStart].OpenTime.Subtract(Bars[iStart-1].OpenTime).TotalMilliseconds;
                    if ((currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                    && ModeVOLInput == ModeVOLData.Normal && isCalcFinished && !isCalcLocked)
                       maxLength = Bars[iStart + 1].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                }

                double proportion = VolumesRank[priceKey] * (maxLength - (maxLength/1.50));
                double dynLength = proportion / largestVOL;

                // Bull/Up/Buy
                double proportion_Up = VolumesRank_Up[priceKey] * (maxLength - (maxLength/1.50));
                double dynLength_Up = proportion_Up / VolumesRank_Up.Values.Max();
                // Bear/Down/Sell
                double maxLength_Left = Bars[iStart].OpenTime.Subtract(Bars[iStart-1].OpenTime).TotalMilliseconds;
                double proportion_Down = VolumesRank_Down[priceKey] * (maxLength_Left - (maxLength_Left/1.50));
                double dynLength_Down = proportion_Down / VolumesRank_Down.Values.Max();
                // Delta
                double proportion_Delta = DeltaRank[priceKey] * (maxLength - (maxLength/1.50));
                double dynLength_Delta = proportion_Delta / DeltaRank.Values.Max();

                if (DeltaRank[priceKey] < 0 && DeltaVisualInput == DeltaVisualData.Divided && ModeVOLInput == ModeVOLData.Delta) {
                    // Negative Delta
                    proportion_Delta = DeltaRank[priceKey] * (maxLength_Left - (maxLength_Left/1.50));
                    dynLength_Delta = proportion_Delta / DeltaRank.Values.Where(n => n < 0).Min();
                }

                if (DeltaVisualInput == DeltaVisualData.Profile && ModeVOLInput == ModeVOLData.Buy_Sell) {
                    // Buy vs Sell = Pseudo Delta
                    int buyVolume = VolumesRank_Up.Values.Max();
                    int sellVolume = VolumesRank_Down.Values.Max();
                    int sideVolMax = buyVolume > sellVolume ? buyVolume : sellVolume;

                    proportion_Up = VolumesRank_Up[priceKey] * (maxLength - (maxLength/1.20));
                    dynLength_Up = proportion_Up / sideVolMax;
                    proportion_Down = VolumesRank_Down[priceKey] * (maxLength - (maxLength/1.50));
                    dynLength_Down = proportion_Down / sideVolMax;
                }
                else if (DeltaVisualInput == DeltaVisualData.Profile && ModeVOLInput == ModeVOLData.Delta) {
                    int Positive_Delta = DeltaRank.Values.Max();
                    IEnumerable<int> allNegative = DeltaRank.Values.Where(n => n < 0);
                    int Negative_Delta = 0;
                    try {Negative_Delta = Math.Abs(allNegative.Min());} catch {}

                    int deltaMax = Positive_Delta > Negative_Delta ? Positive_Delta : Negative_Delta;

                    dynLength_Delta = proportion_Delta / deltaMax;
                }


                if (ModeVOLInput == ModeVOLData.Normal)
                {
                    Color dynColor = VolumesRank[priceKey] != largestVOL ? volumeColor : volumeLargeColor;
                    ChartRectangle volHist;
                    if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                        volHist = Chart.DrawRectangle($"{iStart}_{i}", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, dynColor);
                    else
                        volHist = Chart.DrawRectangle($"{iStart}_{i}", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength*2), upperSegment, dynColor);

                    if (FillHist)
                        volHist.IsFilled = true;

                    if (ShowNumbers)
                    {
                        ChartText C = Chart.DrawText($"{iStart}_{i}Center", $"{VolumesRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, rtnbFixedColor);
                        C.HorizontalAlignment = HorizontalAlignment.Center;
                        C.FontSize = FontSizeNumbers;
                    }
                    if (ShowResults)
                    {
                        ChartText Center;
                        Color dynResColor = ResultsColoringInput == ResultsColoringData.Fixed ? rtnbFixedColor : volumeColor;
                        Center = Chart.DrawText($"{iStart}SumCenter", $"\n{VolumesRank.Values.Sum()}", Bars.OpenTimes[iStart], lowest, dynResColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;

                        if (EnableFilter)
                        {
                            DynamicSeries[iStart] = VolumesRank.Values.Sum();

                            // ====== Dynamic Series Filter ======
                            double DynamicFilter = DynamicSeries[iStart] / MADynamic.Result[iStart];
                            double DynamicLarge = DynamicFilter >= Filter_Ratio ? DynamicSeries[iStart] : 0;

                            Color dynBarColor = DynamicLarge >= 2 ? Color.FromName(RawColorLargeR.ToString()) : dynResColor;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == Color.FromName(RawColorLargeR.ToString()))
                                Chart.SetBarFillColor(iStart, Color.FromName(RawColorLargeR.ToString()));
                        }
                    }

                }
                else if (ModeVOLInput == ModeVOLData.Buy_Sell)
                {
                    Color dynColorBuy = VolumesRank_Up[priceKey] != VolumesRank_Up.Values.Max() ? buyColor : buyLargeColor;
                    Color dynColorSell = VolumesRank_Down[priceKey] != VolumesRank_Down.Values.Max() ? sellColor : sellLargeColor;

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
                            sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Down), upperSegment, sellColor);
                            buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Up), upperSegment, buyColor);
                        }
                        else {
                            sellHist = Chart.DrawRectangle($"{iStart}_{i}Sell", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength_Down*2), upperSegment, sellColor);
                            buyHist = Chart.DrawRectangle($"{iStart}_{i}Buy", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength_Up*2), upperSegment, buyColor);
                        }
                    }

                    if (FillHist)
                    {
                        buyHist.IsFilled = true;
                        sellHist.IsFilled = true;
                    }

                    if (ShowNumbers)
                    {
                        ChartText L = Chart.DrawText($"{iStart}_{i}SellNumber", $"{VolumesRank_Down[priceKey]}", Bars.OpenTimes[iStart], priceKey, rtnbFixedColor);
                        ChartText R = Chart.DrawText($"{iStart}_{i}BuyNumber", $"{VolumesRank_Up[priceKey]}", Bars.OpenTimes[iStart], priceKey, rtnbFixedColor);

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
                        var selected = ResultsTypeInput;

                        int volBuy = VolumesRank_Up.Values.Sum();
                        int volSell = VolumesRank_Down.Values.Sum();

                        Color compare = volBuy > volSell ? buyColor : volBuy < volSell ? sellColor : rtnbFixedColor;
                        Color dynColorCenter = ResultsColoringInput == ResultsColoringData.Fixed ? rtnbFixedColor : compare;

                        if (ShowSideTotalInput) {
                            Color dynColorLeft = ResultsColoringInput == ResultsColoringData.Fixed ? rtnbFixedColor : sellColor;
                            Color dynColorRight = ResultsColoringInput == ResultsColoringData.Fixed ? rtnbFixedColor : buyColor;

                            int percentBuy = (volBuy * 100) / (volBuy + volSell);
                            int percentSell = (volSell * 100) / (volBuy + volSell);

                            string dynStrBuy = selected == ResultsType_Data.Percentage ? $"\n{percentBuy}%" : selected == ResultsType_Data.Value ? $"\n{volBuy}" : $"\n{percentBuy}%\n({volBuy})";
                            string dynStrSell = selected == ResultsType_Data.Percentage ? $"\n{percentSell}%" : selected == ResultsType_Data.Value ? $"\n{volSell}" : $"\n{percentSell}%\n({volSell})";

                            ChartText Left, Right;
                            Left = Chart.DrawText($"{iStart}SellSum", $"{dynStrSell}", Bars.OpenTimes[iStart], lowest, dynColorLeft);
                            Right = Chart.DrawText($"{iStart}BuySum", $"{dynStrBuy}", Bars.OpenTimes[iStart], lowest, dynColorRight);

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

                            Color dynBarColor = DynamicLarge >= 2 ? Color.FromName(RawColorLargeR.ToString()) : dynColorCenter;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == Color.FromName(RawColorLargeR.ToString()))
                                Chart.SetBarFillColor(iStart, Color.FromName(RawColorLargeR.ToString()));
                        }
                    }

                }
                else
                {
                    IEnumerable<int> allNegative = DeltaRank.Values.Where(n => n < 0);
                    int Negative_Delta = 0;
                    try {Negative_Delta = allNegative.Min();} catch {}

                    Color dynColorBuy = DeltaRank[priceKey] != DeltaRank.Values.Max() ? buyColor : buyLargeColor;
                    Color dynColorSell = DeltaRank[priceKey] != Negative_Delta ? sellColor : sellLargeColor;

                    if (ColorOnlyLarguestInput) {
                        if (DeltaRank[priceKey] == DeltaRank.Values.Max())
                            dynColorBuy = DeltaRank.Values.Max() > Math.Abs(Negative_Delta) ? volumeLargeColor : buyColor;
                        if (DeltaRank[priceKey] == Negative_Delta)
                            dynColorSell = DeltaRank.Values.Max() < Math.Abs(Negative_Delta) ? volumeLargeColor : sellColor;
                    }

                    ChartRectangle deltaHist;
                    if (DeltaVisualInput == DeltaVisualData.Divided)
                    {
                        try {
                            if (DeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, dynColorBuy);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Delta), upperSegment, dynColorSell);
                        } catch {
                           if (DeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, dynColorBuy);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}DynDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime, upperSegment, dynColorSell);
                        }
                    }
                    else
                    {
                        try {
                        if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range"))
                        {
                            if (DeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength_Delta), upperSegment, buyColor);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-dynLength_Delta), upperSegment, sellColor);
                        }
                        else
                        {
                            if (DeltaRank[priceKey] >= 0)
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(dynLength_Delta*2), upperSegment, buyColor);
                            else
                                deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)).AddMilliseconds(-dynLength_Delta*2), upperSegment, sellColor);
                        }
                        } catch {
                            deltaHist = Chart.DrawRectangle($"{iStart}_{i}ProfileDelta", Bars.OpenTimes[iStart].AddMilliseconds(-(maxLength/3)), lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(-(maxLength/3)), upperSegment, rtnbFixedColor);
                        }

                    }

                    if (FillHist)
                        deltaHist.IsFilled = true;

                    if (ShowNumbers)
                    {
                        ChartText nbText;
                        if (DeltaRank[priceKey] > 0)
                        {
                            nbText = Chart.DrawText($"{iStart}_{i}DynNumberDelta", $"{DeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, rtnbFixedColor);
                            if (DeltaVisualInput == DeltaVisualData.Divided)
                                nbText.HorizontalAlignment = HorizontalAlignment.Right;
                            else
                                nbText.HorizontalAlignment = HorizontalAlignment.Center;

                            nbText.FontSize = FontSizeNumbers;
                        }
                        else if (DeltaRank[priceKey] < 0)
                        {
                            nbText = Chart.DrawText($"{iStart}_{i}DynNumberDelta", $"{DeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, rtnbFixedColor);
                            if (DeltaVisualInput == DeltaVisualData.Divided)
                                nbText.HorizontalAlignment = HorizontalAlignment.Left;
                            else
                                nbText.HorizontalAlignment = HorizontalAlignment.Center;
                            nbText.FontSize = FontSizeNumbers;
                        }
                        else
                        {
                            nbText = Chart.DrawText($"{iStart}_{i}DynNumberDelta", $"{DeltaRank[priceKey]}", Bars.OpenTimes[iStart], priceKey, rtnbFixedColor);
                            nbText.HorizontalAlignment = HorizontalAlignment.Center;
                            nbText.FontSize = FontSizeNumbers;
                        }

                    }

                    // =======  Results  =======
                    if (ShowResults)
                    {
                        var selected = ResultsTypeInput;

                        Color dynColorLeft = ResultsColoringInput == ResultsColoringData.Fixed ? rtnbFixedColor : sellColor;
                        Color dynColorRight = ResultsColoringInput == ResultsColoringData.Fixed ? rtnbFixedColor : buyColor;

                        Color compareSumD = DeltaRank.Values.Sum() > 0 ? buyColor : DeltaRank.Values.Sum() < 0 ? sellColor : sellLargeColor;
                        Color dynColorCenter = ResultsColoringInput == ResultsColoringData.Fixed ? sellLargeColor : compareSumD;

                        if (ShowSideTotalInput) {
                            int deltaBuy = DeltaRank.Values.Where(n => n > 0).Sum();
                            int deltaSell = DeltaRank.Values.Where(n => n < 0).Sum();

                            int percentBuy = 0;
                            int percentSell = 0;
                            try {percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell));} catch {};
                            try {percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell));} catch {}

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

                        int CumulDelta = CumulDeltaRank.Keys.Count <= 1 ? CumulDeltaRank[iStart] : (CumulDeltaRank[iStart] + CumulDeltaRank[iStart-1]);
                        int prevCumulDelta = CumulDeltaRank.Keys.Count <= 2 ? CumulDeltaRank[iStart] : (CumulDeltaRank[iStart-1] + CumulDeltaRank[iStart-2]);

                        Color compareCD = CumulDelta > prevCumulDelta ? buyColor : CumulDelta < prevCumulDelta ? sellColor : sellLargeColor;
                        Color dynColorCD = ResultsColoringInput == ResultsColoringData.Fixed ? sellLargeColor : compareCD;

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

                            Color dynBarColor = DynamicLarge >= 2 ? Color.FromName(RawColorLargeR.ToString()) : dynColorCenter;
                            Center.Color = dynBarColor;
                            if (ColoringBars && dynBarColor == Color.FromName(RawColorLargeR.ToString()))
                                Chart.SetBarFillColor(iStart, Color.FromName(RawColorLargeR.ToString()));

                            if (ColoringCD) {
                                // ====== Cumul Delta Filter ======
                                double CumulDeltaFilter = CumulDeltaSeries[iStart] / MACumulDelta.Result[iStart];
                                double CumulDeltaLarge = CumulDeltaFilter > Filter_Ratio ? CumulDeltaSeries[iStart] : 0;
                                Color dynCDColor = CumulDeltaLarge > 2 ? Color.FromName(RawColorLargeR.ToString()) : dynColorCD;
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
            DateTime endTime = Bars.OpenTimes[index+1];

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
            Chart.DrawStaticText("txt", $"{msg}", verticalAlign, horizontalAlign, Color.LightBlue);
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
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out fromDateTime)) {
                    if (fromDateTime > Server.Time.Date) {
                        // for Log
                        fromDateTime = Server.Time.Date;
                        Print($"Invalid DateTime '{StringDate}'. Using '{fromDateTime}'");
                    }
                }
                else {
                    // for Log
                    fromDateTime = Server.Time.Date;
                    Print($"Invalid DateTime '{StringDate}'. Using '{fromDateTime}'");
                }
            }
            else {
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
            DateTime FirstTickTime = TicksOHLC.OpenTimes.Reverse().LastOrDefault();
            if (FirstTickTime >= fromDateTime){
                LoadMoreTicks(fromDateTime);
                DrawOnScreen("Data Collection Finished \n Calculating...");
            }
            else {
                Print($"Using existing tick data from '{FirstTickTime}'");
                DrawOnScreen($"Using existing tick data from '{FirstTickTime}' \n Calculating...");
            }
        }
        private void LoadMoreTicks(DateTime fromDateTime)
        {
            bool msg = false;

            while (TicksOHLC.OpenTimes.Reverse().LastOrDefault() > fromDateTime)
            {
                if (!msg) {
                    Print($"Loading from '{TicksOHLC.OpenTimes.Reverse().Last()}' to '{fromDateTime}'...");
                    msg = true;
                }

                int loadedCount = TicksOHLC.LoadMoreHistory();
                Print("Loaded {0} Ticks, Current Tick Date: {1}", loadedCount, TicksOHLC.OpenTimes.Reverse().LastOrDefault());
                if (loadedCount == 0)
                    break;
            }
            Print("Data Collection Finished, First Tick from: {0}", TicksOHLC.OpenTimes.Reverse().LastOrDefault());
        }
    }
}
