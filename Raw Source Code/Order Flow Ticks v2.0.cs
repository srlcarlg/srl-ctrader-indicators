/*
--------------------------------------------------------------------------------------------------------------------------------
                        Order Flow Ticks v2.0
                             revision 1
Order Flow Ticks brings the main concepts of Order Flow (aka Footprint) for cTrader.
Using ideas from my previous creations (Volume for Renko/Range, TPO Profile) made this possible.

Also works on [Ticks / Renko / Range] Charts

Comparing with Footprint, we have the features:
    - Normal Mode => Volume Profile of Bar
    - Buy vs Sell => Bid/Ask Footprint
    - Delta => Delta Footprint

Strength Filters, which are used to re-create:
    - Delta => Bubbles Charts
    - Delta => Tick Spike Filter

All parameters are self-explanatory.

What's new in rev. 1?
- (rev 1.5) "Final" version => A major refactoring done before adding it to Order Flow Aggregated
    - Improved Perfomance.
    - Row Height by ATR
    - Better Support for [Ticks / Renko / Range] Charts.
    - Better Params UI
    - Design(HLC/Line charts) Templates, where possible.
    - Static Update of Drawings, where possible.
    - Use of CTrader's 5.x specific features:
        - Built-in Songs/Pop-ups => Tick Spike Alert.
        - Local storage => Save Params.
        - ProgressBar => Calculating...
    - Futures Updates?
        - No, at least for a while... like a year.

-(rev 1.4-pass) -----------
-(rev 1.3-pass) Delta only:
    - Bubbles Chart
    - Tick Spike Filter
-(rev 1.2) Row Height in Pips!
-(rev 1.1) Custom Format (k, M) for Big Numbers (n >= 1000)!
-(rev 1) Fix => Indicator's objects being randomly removed right after switching Panel settings.
-(rev 1) Fix => Histogram Stretching in Profile View (all Modes) on weekend candles.
-(rev 1) Delta => Min, Max, Subtract (min - max).

What's new in v2.0?
-Added Params Panel for quickly switch between settings (volume modes, row height, etc) and most importantly, more user-friendly.
-Refactor to only use Colors API.
-Should work with Mac OS users.

Last update => 27/08/2025

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
using System.Threading;
using System.Threading.Tasks;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class OrderFlowTicksV20 : Indicator
    {
        public enum LoadTickFrom_Data
        {
            Today,
            Yesterday,
            Before_Yesterday,
            One_Week,
            Two_Week,
            Monthly,
            Custom
        }
        [Parameter("Load From:", DefaultValue = LoadTickFrom_Data.Today, Group = "==== Tick Volume Settings ====")]
        public LoadTickFrom_Data LoadTickFrom_Input { get; set; }

        public enum LoadTickStrategy_Data
        {
            At_Startup,
            On_Chart,
        }
        [Parameter("Load Type:", DefaultValue = LoadTickStrategy_Data.On_Chart, Group = "==== Tick Volume Settings ====")]
        public LoadTickStrategy_Data LoadTickStrategy_Input { get; set; }

        [Parameter("Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== Tick Volume Settings ====")]
        public string StringDate { get; set; }

        public enum PanelAlign_Data
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
        [Parameter("Panel Position:", DefaultValue = PanelAlign_Data.Bottom_Right, Group = "==== Order Flow Ticks v2.0 ====")]
        public PanelAlign_Data PanelAlign_Input { get; set; }

        public enum StorageKeyConfig_Data
        {
            Symbol_Timeframe,
            Broker_Symbol_Timeframe
        }
        [Parameter("Storage By:", DefaultValue = StorageKeyConfig_Data.Broker_Symbol_Timeframe, Group = "==== Order Flow Ticks v2.0 ====")]
        public StorageKeyConfig_Data StorageKeyConfig_Input { get; set; }

        public enum RowConfig_Data
        {
            ATR,
            Custom,
        }
        [Parameter("Row Config:", DefaultValue = RowConfig_Data.ATR, Group = "==== Order Flow Ticks v2.0 ====")]
        public RowConfig_Data RowConfig_Input { get; set; }

        [Parameter("Custom Row(pips):", DefaultValue = 0.2, MinValue = 0.2, Group = "==== Order Flow Ticks v2.0 ====")]
        public double CustomHeightInPips { get; set; }


        [Parameter("ATR Period:", DefaultValue = 5, MinValue = 1, Group = "==== ATR Row Config ====")]
        public int ATRPeriod { get; set; }

        [Parameter("Row Detail(%):", DefaultValue = 70, MinValue = 20, MaxValue = 100, Group = "==== ATR Row Config ====")]
        public int RowDetailATR { get; set; }


        [Parameter("[Renko] Show Wicks?", DefaultValue = true, Group = "==== Renko Chart ====")]
        public bool ShowWicks { get; set; }


        public enum FormatMaxDigits_Data
        {
            Zero,
            One,
            Two,
        }
        [Parameter("Format Max Digits:", DefaultValue = FormatMaxDigits_Data.One, Group = "==== Big Numbers ====")]
        public FormatMaxDigits_Data FormatMaxDigits_Input { get; set; }

        [Parameter("Format Numbers?", DefaultValue = true, Group = "==== Big Numbers ====")]
        public bool FormatNumbers { get; set; }

        [Parameter("Format Results?", DefaultValue = true, Group = "==== Big Numbers ====")]
        public bool FormatResults { get; set; }


        [Parameter("Font Size Numbers:", DefaultValue = 8, MinValue = 1, MaxValue = 80, Group = "==== Font Size ====")]
        public int FontSizeNumbers { get; set; }

        [Parameter("Font Size Results:", DefaultValue = 10, MinValue = 1, MaxValue = 80, Group = "==== Font Size ====")]
        public int FontSizeResults { get; set; }


        public enum ResultsColoring_Data
        {
            bySide,
            Fixed,
        }
        [Parameter("Results Coloring:", DefaultValue = ResultsColoring_Data.bySide, Group = "==== Results/Numbers ====")]
        public ResultsColoring_Data ResultsColoring_Input { get; set; }

        [Parameter("Fixed Color RT/NB:", DefaultValue = "#CCFFFFFF", Group = "==== Results/Numbers ====")]
        public Color RtnbFixedColor { get; set; }


        [Parameter("MA Filter Type:", DefaultValue = MovingAverageType.Exponential, Group = "==== Large Result Filter ====")]
        public MovingAverageType MAtype_Large { get; set; }

        [Parameter("MA Filter Period:", DefaultValue = 5, MinValue = 1, Group = "==== Large Result Filter ====")]
        public int MAperiod_Large { get; set; }

        [Parameter("Large R. Ratio", DefaultValue = 1.5, MinValue = 1, MaxValue = 2, Group = "==== Large Result Filter ====")]
        public double LargeFilterRatio { get; set; }

        [Parameter("Large R. Color", DefaultValue = "Gold", Group = "==== Large Result Filter ====")]
        public Color ColorLargeResult { get; set; }

        [Parameter("Coloring Bar?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool LargeFilter_ColoringBars { get; set; }

        [Parameter("[Delta] Coloring Cumulative?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool LargeFilter_ColoringCD { get; set; }


        [Parameter("MA Type:", DefaultValue = MovingAverageType.Simple, Group = "==== Tick Spike Filter ====")]
        public MovingAverageType MAtype_Spike { get; set; }

        [Parameter("MA Period:", DefaultValue = 20, MinValue = 1, Group = "==== Tick Spike Filter ====")]
        public int MAperiod_Spike { get; set; }

        [Parameter("[Debug] Show Strength Value?", DefaultValue = false, Group = "==== Tick Spike Filter ====")]
        public bool ShowTickStrengthValue { get; set; }


        public enum NotificationType_Data
        {
            Popup,
            Sound,
            Both
        }
        [Parameter("Notification Type?", DefaultValue = NotificationType_Data.Both, Group = "==== Spike Notification ====")]
        public NotificationType_Data NotificationType_Input { get; set; }

        [Parameter("Sound Type:", DefaultValue = SoundType.Confirmation, Group = "==== Spike Notification ====")]
        public SoundType SoundType_Spike { get; set; }


        [Parameter("Minimum Threshold:", DefaultValue = 1.2, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeMinimum_Value { get; set; }

        [Parameter("Average < Max Threshold:", DefaultValue = 2.5, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeAverage_Value { get; set; }
        [Parameter("Average Color:", DefaultValue = "#DAFFFF00", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeAverage_Color { get; set; }

        [Parameter("High:", DefaultValue = 3.5, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeHigh_Value { get; set; }
        [Parameter("High Color:", DefaultValue = "#DAFFD700", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeHigh_Color { get; set; }

        [Parameter("Ultra >= Max Threshold:", DefaultValue = 3.51, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeUltra_Value { get; set; }
        [Parameter("Ultra Color:", DefaultValue = "#DAFF0000", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeUltra_Color { get; set; }


        [Parameter("MA Type:", DefaultValue = MovingAverageType.Exponential, Group = "==== Bubbles Chart ====")]
        public MovingAverageType MAtype_Bubbles { get; set; }

        [Parameter("MA Period:", DefaultValue = 20, MinValue = 1, Group = "==== Bubbles Chart ====")]
        public int MAperiod_Bubbles { get; set; }

        [Parameter("[Debug] Show Strength Value?", DefaultValue = false, Group = "==== Bubbles Chart ====")]
        public bool ShowStrengthValue { get; set; }


        [Parameter("Opacity(%):", DefaultValue = 70, MinValue = 1, Step = 1, MaxValue = 100, Group = "==== Bubbles HeatMap Coloring ====")]
        public int BubblesOpacity { get; set; }

        [Parameter("Lowest < Max Threshold:", DefaultValue = 0.3, MinValue = 0.01, Step = 0.01, Group = "==== Bubbles HeatMap Coloring ====")]
        public double HeatmapLowest_Value { get; set; }
        [Parameter("Lowest Color:", DefaultValue = "Aqua", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapLowest_Color { get; set; }

        [Parameter("Low:", DefaultValue = 0.7, MinValue = 0.01, Step = 0.01, Group = "==== Bubbles HeatMap Coloring ====")]
        public double HeatmapLow_Value { get; set; }
        [Parameter("Low Color:", DefaultValue = "White", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapLow_Color { get; set; }

        [Parameter("Average:", DefaultValue = 1.2, MinValue = 0.01, Step = 0.01, Group = "==== Bubbles HeatMap Coloring ====")]
        public double HeatmapAverage_Value { get; set; }
        [Parameter("Average Color:", DefaultValue = "Yellow", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapAverage_Color { get; set; }

        [Parameter("High:", DefaultValue = 2, MinValue = 0.01, Step = 0.01, Group = "==== Bubbles HeatMap Coloring ====")]
        public double HeatmapHigh_Value { get; set; }
        [Parameter("High Color:", DefaultValue = "Gold", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapHigh_Color { get; set; }

        [Parameter("Ultra >= Max Threshold:", DefaultValue = 2.01, MinValue = 0.01, Step = 0.01, Group = "==== Bubbles HeatMap Coloring ====")]
        public double HeatmapUltra_Value { get; set; }
        [Parameter("Ultra Color:", DefaultValue = "Red", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapUltra_Color { get; set; }


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
        private readonly IDictionary<double, int> TotalDeltaRank = new Dictionary<double, int>();
        private double[] MinMaxDelta = { 0, 0 };

        private double heightPips = 4;
        private double rowHeight = 0;
        private int totalBarsFromStartup = 0;

        private DateTime fromDateTime;
        private Bars TicksOHLC;
        private ProgressBar ticksProgressBar = null;
        private bool ticksLoadingComplete = false;

        private bool isBarClosed = false;
        private bool lockTickNotify = true;

        // Filters
        // CumulDeltaSeries is Cumulative Delta Change,
        //   - IT'S NOT CVD (Cumulative Delta Volume)
        // DynamicSeries can be Normal, Buy_Sell or Delta Volume
        private IndicatorDataSeries CumulDeltaSeries, DynamicSeries;
        private MovingAverage MABubblesCumulDelta, MADynamicLargeFilter, MABubbles, MASpikeFilter;
        private StandardDeviation StdDevBubblesCumulDelta, StdDevBubbles, StdDevSpikeFilter;

        // ==== Moved from cTrader Input to Params Panel ====
        public int Lookback = 50;

        // ==== General ====
        public enum VolumeMode_Data
        {
            Normal,
            Buy_Sell,
            Delta,
        }
        public VolumeMode_Data VolumeMode_Input { get; set; } = VolumeMode_Data.Delta;

        public enum VolumeView_Data
        {
            Divided,
            Profile,
        }
        public VolumeView_Data VolumeView_Input { get; set; } = VolumeView_Data.Profile;


        public bool ColoringOnlyLarguest { get; set; } = true;


        // ==== Results ====
        public bool ShowResults { get; set; } = true;

        public bool EnableLargeFilter { get; set; } = true;

        public enum ResultsView_Data
        {
            Percentage,
            Value,
            Both
        }
        public ResultsView_Data ResultsView_Input { get; set; } = ResultsView_Data.Percentage;

        public bool ShowSideTotal { get; set; } = true;

        public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }
        public OperatorBuySell_Data OperatorBuySell_Input { get; set; } = OperatorBuySell_Data.Subtraction;

        public bool ShowMinMaxDelta { get; set; } = false;


        // ==== Spike Filter ====
        public bool EnableSpikeFilter { get; set; } = true;
        public bool EnableSpikeNotification { get; set; } = true;

        public enum SpikeView_Data
        {
            Bubbles,
            Icon,
        }
        public SpikeView_Data SpikeView_Input { get; set; } = SpikeView_Data.Icon;

        public ChartIconType IconView_Input { get; set; } = ChartIconType.Square;

        public enum SpikeFilter_Data
        {
            MA,
            Standard_Deviation,
        }
        public SpikeFilter_Data SpikeFilter_Input { get; set; } = SpikeFilter_Data.MA;


        // ==== Bubbles Chart ====
        public bool EnableBubblesChart { get; set; } = false;

        public double BubblesSizeMultiplier { get; set; } = 2;

        public enum BubblesSource_Data
        {
            Delta,
            Cumulative_Delta_Change,
        }
        public BubblesSource_Data BubblesSource_Input { get; set; } = BubblesSource_Data.Delta;

        public enum BubblesFilter_Data
        {
            MA,
            Standard_Deviation,
            Both
        }
        public BubblesFilter_Data BubblesFilter_Input { get; set; } = BubblesFilter_Data.MA;
        public enum BubblesColoring_Data
        {
            Heatmap,
            Momentum,
        }
        public BubblesColoring_Data BubblesColoring_Input { get; set; } = BubblesColoring_Data.Heatmap;

        public enum BubblesMomentumStrategy_Data
        {
            Fading,
            Positive_Negative,
        }
        public BubblesMomentumStrategy_Data BubblesMomentumStrategy_Input { get; set; } = BubblesMomentumStrategy_Data.Fading;

        // ==== Misc ====

        public bool ShowHist { get; set; } = true;
        public bool FillHist { get; set; } = true;
        public bool ShowNumbers { get; set; } = true;
        public bool ShowBubbleValue { get; set; } = true;

        // Params Panel
        private Border ParamBorder;

        public class IndicatorParams
        {
            // General
            public double N_Bars { get; set; }
            public double RowHeightInPips { get; set; }
            public VolumeMode_Data VolMode { get; set; }
            public VolumeView_Data VolView { get; set; }
            public bool OnlyLargestDivided { get; set; }

            // Results
            public bool ShowResults { get; set; }
            public bool EnableLargeFilter { get; set; }
            public bool ShowSideTotal { get; set; }
            public ResultsView_Data ResultView { get; set; }
            public OperatorBuySell_Data OperatorBuySell { get; set; }
            public bool ShowMinMax { get; set; }

            // Spike Filter
            public bool EnableSpike { get; set; }
            public bool EnableSpikeNotify { get; set; }
            public SpikeFilter_Data SpikeFilter { get; set; }
            public SpikeView_Data SpikeView { get; set; }
            public ChartIconType IconView { get; set; }

            // Bubbles Chart
            public bool EnableBubbles { get; set; }
            public double BubblesSize { get; set; }
            public BubblesSource_Data BubblesSource { get; set; }
            public BubblesFilter_Data BubblesFilter { get; set; }
            public BubblesColoring_Data BubblesColoring { get; set; }
            public BubblesMomentumStrategy_Data BubblesMomentumStrategy { get; set; }

            // Misc
            public bool ShowHist { get; set; }
            public bool FillHist { get; set; }
            public bool ShowNumbers { get; set; }
            public bool ShowBubbleValue { get; set; }
        }

        protected override void Initialize()
        {
            if (RowConfig_Input == RowConfig_Data.Custom)
                heightPips = CustomHeightInPips;
            else {
                // Math Formulas by LLM
                // Manual coding with adaptations for cTrader Algo API.
                // The idea is => Set the rowHeight for any symbol with [1, 2, 5] digits with fewer hard-coded values.
                AverageTrueRange atr = Indicators.AverageTrueRange(ATRPeriod, MovingAverageType.Exponential);
                double atrInTick = atr.Result.Last() / Symbol.TickSize;
                double priceInTick = Bars.LastBar.Close / Symbol.TickSize;

                // Original => (smaATRInTick * targetRows) / smaPriceInTick;
                // However, Initialize() already has a lot of heavy things to start (Tick / Filters / Panel),
                // Plus, the current approach is good enough and gives slightly/better higher numbers.
                double K_Factor = (atrInTick * RowDetailATR) / priceInTick;
                double rowSizeInTick = (atrInTick * atrInTick) / (K_Factor * priceInTick);

                // Original => Math.Max(1, Math.Round(rowSizeInTick, 2)) * (Symbol.TickSize / Symbol.PipSize)
                // Should 'never' go bellow 0.4 pips.
                double rowSizePips = Math.Max(0.4, Math.Round(rowSizeInTick, 2));
                heightPips = rowSizePips;
            }

            // Define rowHeight by Pips
            rowHeight = Symbol.PipSize * heightPips;

            // Filters
            DynamicSeries = CreateDataSeries();
            MADynamicLargeFilter = Indicators.MovingAverage(DynamicSeries, MAperiod_Large, MAtype_Large);

            CumulDeltaSeries = CreateDataSeries();
            MABubblesCumulDelta = Indicators.MovingAverage(CumulDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);
            StdDevBubblesCumulDelta = Indicators.StandardDeviation(CumulDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);

            MABubbles = Indicators.MovingAverage(DynamicSeries, MAperiod_Bubbles, MAtype_Bubbles);
            StdDevBubbles = Indicators.StandardDeviation(DynamicSeries, MAperiod_Bubbles, MAtype_Bubbles);

            MASpikeFilter = Indicators.MovingAverage(DynamicSeries, MAperiod_Spike, MAtype_Spike);
            StdDevSpikeFilter = Indicators.StandardDeviation(DynamicSeries, MAperiod_Spike, MAtype_Spike);

            // First Ticks Data
            TicksOHLC = MarketData.GetBars(TimeFrame.Tick);

            if (LoadTickStrategy_Input == LoadTickStrategy_Data.On_Chart)
            {
                StackPanel panel = new() {
                    Width = 200,
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ticksProgressBar = new ProgressBar { IsIndeterminate = true, Height = 12 };
                panel.AddChild(ticksProgressBar);
                Chart.AddControl(panel);
                VolumeInitialize(true);
            }
            else
                VolumeInitialize();

            DrawOnScreen("Loading Ticks Data... \n Calculating...");

            string[] timesBased = { "Minute", "Hour", "Daily", "Day" };
            string ticksInfo = $"Keep in mind: \n 1) Tick data are stored in RAM \n 2) 'Lower Timeframe' and 'Large Tick Data' with 'Nº Bars == -1' \n   - Takes longer to calculate/draw the entirely data";
            Second_DrawOnScreen($"Taking too long? You can: \n 1) Set Nº Bars \n 2) Increase the rowHeight \n\n {ticksInfo}");

            // Design
            Chart.ChartType = ChartType.Hlc;

            // Required to recalculate the histograms in Live Market
            string currentTimeframe = Chart.TimeFrame.ToString();
            if (currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick"))
                Bars.BarOpened += (_) => isBarClosed = true;

            // Spike Filter
            Bars.BarOpened += (_) => lockTickNotify = false;

            // Required to avoid the first call of Calculate() doesn't draw the entirely Tick data
            totalBarsFromStartup = Bars.Count;

            // Params Panel
            VerticalAlignment vAlign = VerticalAlignment.Bottom;
            HorizontalAlignment hAlign = HorizontalAlignment.Right;

            if (PanelAlign_Input == PanelAlign_Data.Bottom_Left)
                hAlign = HorizontalAlignment.Left;
            else if (PanelAlign_Input == PanelAlign_Data.Top_Left){
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Left;
            }
            else if (PanelAlign_Input == PanelAlign_Data.Top_Right) {
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Right;
            } else if (PanelAlign_Input == PanelAlign_Data.Center_Right) {
                vAlign = VerticalAlignment.Center;
                hAlign = HorizontalAlignment.Right;
            } else if (PanelAlign_Input == PanelAlign_Data.Center_Left) {
                vAlign = VerticalAlignment.Center;
                hAlign = HorizontalAlignment.Left;
            } else if (PanelAlign_Input == PanelAlign_Data.Top_Center) {
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Center;
            } else if (PanelAlign_Input == PanelAlign_Data.Bottom_Center) {
                vAlign = VerticalAlignment.Bottom;
                hAlign = HorizontalAlignment.Center;
            }

            IndicatorParams DefaultParams = new() {
                N_Bars = Lookback,
                RowHeightInPips = heightPips,
                VolMode = VolumeMode_Input,
                VolView = VolumeView_Input,

                OnlyLargestDivided = ColoringOnlyLarguest,

                ShowResults = ShowResults,
                EnableLargeFilter = EnableLargeFilter,
                ShowSideTotal = ShowSideTotal,
                ResultView = ResultsView_Input,
                OperatorBuySell = OperatorBuySell_Input,
                ShowMinMax = ShowMinMaxDelta,

                EnableSpike = EnableSpikeFilter,
                EnableSpikeNotify = EnableSpikeNotification,
                SpikeFilter = SpikeFilter_Input,
                SpikeView = SpikeView_Input,
                IconView = IconView_Input,

                EnableBubbles = EnableBubblesChart,
                BubblesSize = BubblesSizeMultiplier,
                BubblesSource = BubblesSource_Input,
                BubblesFilter = BubblesFilter_Input,
                BubblesColoring = BubblesColoring_Input,
                BubblesMomentumStrategy = BubblesMomentumStrategy_Input,

                ShowHist = ShowHist,
                FillHist = FillHist,
                ShowNumbers = ShowNumbers,
                ShowBubbleValue = ShowBubbleValue
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

            WrapPanel wrapPanel = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
            };
            AddHiddenButton(wrapPanel, Color.FromHex("#7F808080"));
            Chart.AddControl(wrapPanel);
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

        public override void Calculate(int index)
        {
            if (ticksProgressBar != null && !ticksLoadingComplete && LoadTickStrategy_Input == LoadTickStrategy_Data.On_Chart)
                LoadMoreTicksOnChart();

            // Removing Messages
            if (!IsLastBar) {
                DrawOnScreen("");
                Second_DrawOnScreen("");
            }

            // LookBack should only be applied to drawings.
            // Use the entirely ticks data, mainly for Filters.
            if (index < (totalBarsFromStartup - Lookback) && Lookback != -1)
            {
                CreateOrderFlow(index, true);

                if (VolumeMode_Input == VolumeMode_Data.Normal)
                    DynamicSeries[index] = VolumesRank.Values.Sum();
                else if (VolumeMode_Input == VolumeMode_Data.Buy_Sell) {
                    double sumValue = VolumesRank_Up.Values.Sum() + VolumesRank_Down.Values.Sum();
                    double subtValue = VolumesRank_Up.Values.Sum() - VolumesRank_Down.Values.Sum();

                    DynamicSeries[index] = OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumValue : subtValue;
                } else {
                    if (!TotalDeltaRank.ContainsKey(index))
                        TotalDeltaRank.Add(index, DeltaRank.Values.Sum());
                    else
                        TotalDeltaRank[index] = DeltaRank.Values.Sum();

                    int cumulDeltaChange = TotalDeltaRank.Keys.Count <= 1 ? TotalDeltaRank[index] : (TotalDeltaRank[index] + TotalDeltaRank[index - 1]);
                    double totalDelta = DeltaRank.Values.Sum();

                    CumulDeltaSeries[index] = Math.Abs(cumulDeltaChange);
                    DynamicSeries[index] = Math.Abs(totalDelta);
                }
                return;
            }

            // Required for Non-Time based charts (Renko, Range, Ticks)
            if (isBarClosed) {
                CreateOrderFlow(index - 1);
                isBarClosed = false;
            }

            CreateOrderFlow(index);

            void CreateOrderFlow(int idx, bool isLookback = false)
            {
                Segments.Clear();
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                double[] resetDelta = {0, 0};
                MinMaxDelta = resetDelta;
                OrderFlow(idx, isLookback);
            }
        }

        private void OrderFlow(int iStart, bool isLookback = false)
        {
            // ==== Highest and Lowest ====
            double highest = Bars.HighPrices[iStart];
            double lowest = Bars.LowPrices[iStart];
            double open = Bars.OpenPrices[iStart];

            if (Chart.TimeFrame.ToString().Contains("Renko") && ShowWicks) {
                var isBullish = Bars.ClosePrices[iStart] > Bars.OpenPrices[iStart];
                var currentOpenTime = Bars.OpenTimes[iStart ];
                var nextOpenTime = Bars.OpenTimes[iStart + 1];

                double[] wicks = GetWicks(currentOpenTime, nextOpenTime);

                if (IsLastBar && !isBarClosed) {
                    lowest = wicks[0];
                    highest = wicks[1];
                    open = Bars.ClosePrices[iStart - 1];
                } else {
                    if (isBullish)
                        lowest = wicks[0];
                    else
                        highest = wicks[1];
                }
            }

            // ==== Bar Segments ====
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

            // ==== Volume on Tick ====
            VP_Tick(iStart);

            // ==== Drawing ====
            if (Segments.Count == 0 || isLookback)
                return;

            // Lock Bubbles Chart template
            if (EnableBubblesChart) {
                ShowHist = false;
                ShowNumbers = false;
                ShowResults = false;
                EnableSpikeFilter = false;
            }

            // Manual Refactoring
            // LLM allucinates
            double loopPrevSegment = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                if (loopPrevSegment == 0)
                    loopPrevSegment = Segments[i];

                double priceKey = Segments[i];
                if (!VolumesRank.ContainsKey(priceKey))
                    continue;

                // ====  HISTOGRAMs + Texts  ====
                /*
                    Indeed, the value of X-Axis is simply a rule of three,
                    where the maximum value of the respective side (One/Buy/Sell) will be the maxLength (in Milliseconds),
                    from there the math adjusts the histograms.

                        MaxValue    maxLength(ms)
                        x             ?(ms)

                    The values 1.50 and 3 are the manually set values like the size of the Bar body in any timeframe (Candle, Ticks, Renko, Range)
                */
                string currentTimeframe = Chart.TimeFrame.ToString();
                bool isPriceBasedChart = currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick");
                // For real-time => Avoid stretching the histograms away ad infinitum
                bool avoidStretching = IsLastBar && !isBarClosed;

                // Any Volume Mode
                double maxLength = 0;
                if (!IsLastBar || isBarClosed)
                    maxLength = Bars[iStart + 1].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                else
                    maxLength = Bars[iStart].OpenTime.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;

                bool gapWeekday = Bars[iStart].OpenTime.DayOfWeek == DayOfWeek.Sunday && Bars.OpenTimes[iStart - 1].DayOfWeek == DayOfWeek.Friday;

                double lowerSegmentY1 = loopPrevSegment;
                double upperSegmentY2 = Segments[i];

                void DrawRectangle_Normal(int currentVolume, int maxVolume, bool profileInMiddle = false)
                {
                    // Same as (maxLength - (maxLength / 1.50))
                    double candlesProportion = maxLength / 3;
                    double proportion = currentVolume * candlesProportion;
                    double dynLength = proportion / maxVolume;

                    bool dividedCondition = VolumeView_Input == VolumeView_Data.Divided ||
                        (VolumeView_Input == VolumeView_Data.Profile && profileInMiddle);

                    DateTime xBar = Bars.OpenTimes[iStart];
                    DateTime x1 = dividedCondition ? xBar : xBar.AddMilliseconds(-candlesProportion);
                    DateTime x2 = x1.AddMilliseconds(dividedCondition ? dynLength : dynLength * 2);

                    if (isPriceBasedChart || gapWeekday)
                    {
                        // Profile View - Complete Proportion
                        double maxLength_LeftSide = xBar.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                        double leftSideProportion = maxLength_LeftSide / 3;

                        double proportion_ToMiddle = currentVolume * leftSideProportion;
                        double dynLength_ToMiddle = proportion_ToMiddle / maxVolume;

                        if (avoidStretching)
                            dynLength = 0;

                        x1 = xBar.AddMilliseconds(-leftSideProportion);
                        x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(dynLength);

                        // In cases of Gap or
                        // Profile View - Half Proportion
                        if (xBar == Bars[iStart - 1].OpenTime || Bars[iStart - 2].OpenTime == Bars[iStart - 1].OpenTime || profileInMiddle) {
                            x1 = xBar;
                            x2 = x1.AddMilliseconds(dynLength);
                        }
                    }

                    bool gapWeekend = xBar.DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart + 1].DayOfWeek == DayOfWeek.Sunday;
                    if (gapWeekend) {
                        x1 = xBar;
                        x2 = x1.AddMilliseconds(dynLength);
                    }

                    Color colorHist = currentVolume != maxVolume ? VolumeColor : VolumeLargeColor;
                    ChartRectangle histogram = Chart.DrawRectangle($"{iStart}_{i}_Normal", x1, lowerSegmentY1, x2, upperSegmentY2, colorHist);

                    histogram.IsFilled = FillHist;
                }

                void DrawRectangle_BuySell(
                    int currentBuy, int maxBuy,
                    int currentSell, int maxSell)
                {
                    // Same as (maxLength - (maxLength_LeftSide / 1.50))
                    double candlesProportion = maxLength / 3;

                    // Right Side - Divided View
                    double proportionBuy_RightSide = currentBuy * candlesProportion;
                    double dynLengthBuy_RightSide = proportionBuy_RightSide / maxBuy;

                    // Left Side - Divided View
                    double maxLength_LeftSide = Bars[iStart].OpenTime.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                    double leftSideProportion = maxLength_LeftSide / 3;

                    double proportionSell_LeftSide = currentSell * leftSideProportion;
                    double dynLengthSell_LeftSide = proportionSell_LeftSide / maxSell;

                    // Profile View
                    int profileMaxVolume = maxBuy > maxSell ? maxBuy : maxSell;

                    double proportionBuy = currentBuy * candlesProportion;
                    double dynLengthBuy = proportionBuy / profileMaxVolume;

                    double proportionSell = currentSell * candlesProportion;
                    double dynLengthSell = proportionSell / profileMaxVolume;
                    // ==============

                    bool dividedCondition = VolumeView_Input == VolumeView_Data.Divided;

                    DateTime xBar = Bars.OpenTimes[iStart];
                    DateTime x1_Buy = dividedCondition ? xBar : xBar.AddMilliseconds(-candlesProportion);
                    DateTime x2_Buy = x1_Buy.AddMilliseconds(dividedCondition ? dynLengthBuy_RightSide : dynLengthBuy);

                    DateTime x1_Sell = dividedCondition ? xBar : xBar.AddMilliseconds(-candlesProportion);
                    DateTime x2_Sell = x1_Sell.AddMilliseconds(dividedCondition ? -dynLengthSell_LeftSide : dynLengthSell * 2);

                    if (isPriceBasedChart && VolumeView_Input == VolumeView_Data.Profile || gapWeekday)
                    {
                        // Profile View - Complete Proportion
                        double candlesProportion_Left = maxLength_LeftSide / 3;
                        double proportionBuy_ToMiddle = currentBuy * candlesProportion_Left;
                        double dynLengthBuy_ToMiddle = proportionBuy_ToMiddle / profileMaxVolume;

                        double proportionSell_ToMiddle = currentSell * candlesProportion_Left;
                        double dynLengthSell_ToMiddle = proportionSell_ToMiddle / profileMaxVolume;

                        double proportionSell_RightSide = currentSell * candlesProportion;
                        double dynLengthSell_RightSide = proportionSell_RightSide / profileMaxVolume;

                        x1_Buy = xBar.AddMilliseconds(-leftSideProportion);
                        x1_Sell = xBar.AddMilliseconds(-leftSideProportion);

                        if (avoidStretching) {
                            dynLengthSell_RightSide = 0;
                            dynLengthBuy_ToMiddle /= 2;
                        }

                        x2_Buy = x1_Buy.AddMilliseconds(dynLengthBuy_ToMiddle);
                        x2_Sell = x1_Sell.AddMilliseconds(dynLengthSell_ToMiddle).AddMilliseconds(dynLengthSell_RightSide);

                        // In cases of Gap or
                        // Profile View - Half Proportion
                        if (xBar == Bars[iStart - 1].OpenTime || Bars[iStart - 2].OpenTime == Bars[iStart - 1].OpenTime) {
                            x1_Buy = xBar;
                            x1_Sell = xBar;

                            proportionBuy = currentBuy * (candlesProportion / 2);
                            dynLengthBuy = proportionBuy / profileMaxVolume;
                            proportionSell = currentSell * candlesProportion;
                            dynLengthSell = proportionSell / profileMaxVolume;

                            x2_Buy = x1_Buy.AddMilliseconds(dynLengthBuy);
                            x2_Sell = x1_Sell.AddMilliseconds(dynLengthSell);
                        }
                    }

                    bool gapWeekend = xBar.DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart + 1].DayOfWeek == DayOfWeek.Sunday;
                    if (gapWeekend) {
                        x1_Buy = xBar;
                        x1_Sell = xBar;

                        x2_Buy = x1_Buy.AddMilliseconds(dynLengthBuy);
                        x2_Sell = x1_Sell.AddMilliseconds(dynLengthSell);
                    }

                    ChartRectangle buyHist, sellHist;
                    Color buyDividedColor = currentBuy != maxBuy ? BuyColor : BuyLargeColor;
                    Color sellDividedColor = currentSell != maxSell ? SellColor : SellLargeColor;
                    if (ColoringOnlyLarguest) {
                        buyDividedColor = maxBuy > maxSell && currentBuy == maxBuy ?
                            BuyLargeColor : BuyColor;
                        sellDividedColor = maxSell > maxBuy && currentSell == maxSell ?
                            SellLargeColor : SellColor;
                    }

                    Color buyColor = VolumeView_Input == VolumeView_Data.Divided ? buyDividedColor : BuyColor;
                    Color sellColor = VolumeView_Input == VolumeView_Data.Divided ? sellDividedColor : SellColor;

                    // Sell histogram first, Buy histogram to override it.
                    sellHist = Chart.DrawRectangle($"{iStart}_{i}_Sell", x1_Sell, lowerSegmentY1, x2_Sell, upperSegmentY2, sellColor);
                    buyHist = Chart.DrawRectangle($"{iStart}_{i}_Buy", x1_Buy, lowerSegmentY1, x2_Buy, upperSegmentY2, buyColor);

                    buyHist.IsFilled = FillHist;
                    sellHist.IsFilled = FillHist;
                }

                void DrawRectangle_Delta(int currentDelta)
                {
                    // Same as (maxLength - (maxLength / 1.50))
                    double candlesProportion = maxLength / 3;

                    double proportionDelta = currentDelta * candlesProportion;
                    int positiveDeltaMax = DeltaRank.Values.Max();
                    IEnumerable<int> negativeRowDeltaList = DeltaRank.Values.Where(n => n < 0);

                    // Divided View
                    double dynLengthDelta_Divided = 0;
                    if (currentDelta > 0)
                        dynLengthDelta_Divided = proportionDelta / positiveDeltaMax;
                    else {
                        int negativaDeltaMax = 0;
                        try { negativaDeltaMax = negativeRowDeltaList.Min(); } catch { }

                        double maxLength_LeftSide = Bars[iStart].OpenTime.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                        double leftSideProportion = maxLength_LeftSide / 3;
                        double proportionDelta_Negative = currentDelta * leftSideProportion;

                        dynLengthDelta_Divided = proportionDelta_Negative / negativaDeltaMax;
                    }

                    // Profile View
                    int absoluteNegativeDeltaMax = 0;
                    try { absoluteNegativeDeltaMax = Math.Abs(negativeRowDeltaList.Min()); } catch { }
                    int deltaMax = positiveDeltaMax > absoluteNegativeDeltaMax ? positiveDeltaMax : absoluteNegativeDeltaMax;

                    double dynLengthDelta_Profile = Math.Abs(proportionDelta) / deltaMax;
                    // ==============

                    bool dividedCondition = VolumeView_Input == VolumeView_Data.Divided;
                    DateTime xBar = Bars.OpenTimes[iStart];
                    DateTime x1 = dividedCondition ? xBar : xBar.AddMilliseconds(-candlesProportion);

                    double lengthWithOperator = currentDelta >= 0 ? dynLengthDelta_Divided : -dynLengthDelta_Divided;
                    DateTime x2 = x1.AddMilliseconds(dividedCondition ? lengthWithOperator : dynLengthDelta_Profile * 2);

                    if (isPriceBasedChart && VolumeView_Input == VolumeView_Data.Profile || gapWeekday)
                    {
                        // Profile View - Complete Proportion
                        double maxLength_LeftSide = xBar.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                        double leftSideProportion = maxLength_LeftSide / 3;

                        double proportion_ToMiddle = Math.Abs(currentDelta) * leftSideProportion;
                        double dynLength_ToMiddle = proportion_ToMiddle / deltaMax;

                        if (avoidStretching)
                            dynLengthDelta_Profile = 0;

                        x1 = xBar.AddMilliseconds(-leftSideProportion);
                        x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(dynLengthDelta_Profile);

                        // In cases of Gap or
                        // Profile View - Half Proportion
                        if (xBar == Bars[iStart - 1].OpenTime || Bars[iStart - 2].OpenTime == Bars[iStart - 1].OpenTime) {
                            x1 = xBar;
                            x2 = x1.AddMilliseconds(dynLengthDelta_Divided);
                        }
                    }

                    bool gapWeekend = xBar.DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart + 1].DayOfWeek == DayOfWeek.Sunday;
                    if (gapWeekend) {
                        x1 = xBar;
                        x2 = x1.AddMilliseconds(dynLengthDelta_Divided);
                    }

                    Color buyDividedColor = currentDelta != positiveDeltaMax ? BuyColor : BuyLargeColor;
                    Color sellDividedColor = currentDelta != -absoluteNegativeDeltaMax ? SellColor : SellLargeColor;
                    if (ColoringOnlyLarguest) {
                        buyDividedColor = positiveDeltaMax > absoluteNegativeDeltaMax && currentDelta == positiveDeltaMax ?
                            BuyLargeColor : BuyColor;
                        sellDividedColor = absoluteNegativeDeltaMax > positiveDeltaMax && currentDelta == -absoluteNegativeDeltaMax ?
                            SellLargeColor : SellColor;
                    }

                    Color buyColorWithFilter = VolumeView_Input == VolumeView_Data.Divided ? buyDividedColor : BuyColor;
                    Color sellColorWithFilter = VolumeView_Input == VolumeView_Data.Divided ? sellDividedColor : SellColor;

                    Color colorHist = currentDelta > 0 ? buyColorWithFilter : sellColorWithFilter;
                    ChartRectangle histogram = Chart.DrawRectangle($"{iStart}_{i}_Delta", x1, lowerSegmentY1, x2, upperSegmentY2, colorHist);

                    histogram.IsFilled = FillHist;
                }

                if (VolumeMode_Input == VolumeMode_Data.Normal)
                {
                    if (ShowHist)
                        DrawRectangle_Normal(VolumesRank[priceKey], VolumesRank.Values.Max());

                    if (ShowNumbers)
                    {
                        double value = VolumesRank[priceKey];
                        string valueFmtd = FormatNumbers ? FormatBigNumber(value) : $"{value}";
                        ChartText C = Chart.DrawText($"{iStart}_{i}_Center", valueFmtd, Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                        C.HorizontalAlignment = HorizontalAlignment.Center;
                        C.FontSize = FontSizeNumbers;
                    }

                    double sumValue = VolumesRank.Values.Sum();
                    DynamicSeries[iStart] = sumValue;

                    if (ShowResults)
                    {
                        string valueFmtd = FormatResults ? FormatBigNumber(sumValue) : $"{sumValue}";
                        Color resultColor = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : VolumeColor;
                        ChartText Center = Chart.DrawText($"{iStart}_SumCenter", $"\n{valueFmtd}", Bars.OpenTimes[iStart], lowest, resultColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;

                        if (EnableLargeFilter) {
                            // ====== Strength Filter ======
                            double volumeStrength  = DynamicSeries[iStart] / MADynamicLargeFilter.Result[iStart];
                            Color barColor = volumeStrength >= LargeFilterRatio ? ColorLargeResult : resultColor;

                            Center.Color = barColor;
                            if (LargeFilter_ColoringBars && barColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                        }
                    }
                }
                else if (VolumeMode_Input == VolumeMode_Data.Buy_Sell)
                {
                    if (ShowHist) {
                        DrawRectangle_BuySell(
                            VolumesRank_Up[priceKey], VolumesRank_Up.Values.Max(),
                            VolumesRank_Down[priceKey], VolumesRank_Down.Values.Max()
                        );
                    }

                    if (ShowNumbers) {
                        double buyValue = VolumesRank_Up[priceKey];
                        double sellValue = VolumesRank_Down[priceKey];
                        string buyValueFmt = FormatNumbers ? FormatBigNumber(buyValue) : $"{buyValue}";
                        string sellValueFmt = FormatNumbers ? FormatBigNumber(sellValue) : $"{sellValue}";

                        ChartText R = Chart.DrawText($"{iStart}_{i}_BuyNumber", buyValueFmt, Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                        ChartText L = Chart.DrawText($"{iStart}_{i}_SellNumber", sellValueFmt, Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);

                        L.HorizontalAlignment = HorizontalAlignment.Left;
                        R.HorizontalAlignment = HorizontalAlignment.Right;

                        L.FontSize = FontSizeNumbers;
                        R.FontSize = FontSizeNumbers;
                    }

                    double sumValue = VolumesRank_Up.Values.Sum() + VolumesRank_Down.Values.Sum();
                    double subtValue = VolumesRank_Up.Values.Sum() - VolumesRank_Down.Values.Sum();

                    DynamicSeries[iStart] = OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumValue : subtValue;

                    if (ShowResults) {
                        var selected = ResultsView_Input;

                        int volBuy = VolumesRank_Up.Values.Sum();
                        int volSell = VolumesRank_Down.Values.Sum();

                        if (ShowSideTotal) {
                            Color colorLeft = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : SellColor;
                            Color colorRight = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : BuyColor;

                            int percentBuy = (volBuy * 100) / (volBuy + volSell);
                            int percentSell = (volSell * 100) / (volBuy + volSell);

                            string volBuyFmtd = FormatResults ? FormatBigNumber(volBuy) : $"{volBuy}";
                            string volSellFmtd = FormatResults ? FormatBigNumber(volSell) : $"{volSell}";

                            string strBuy = selected == ResultsView_Data.Percentage ? $"\n{percentBuy}%" : selected == ResultsView_Data.Value ? $"\n{volBuyFmtd}" : $"\n{percentBuy}%\n({volBuyFmtd})";
                            string strSell = selected == ResultsView_Data.Percentage ? $"\n{percentSell}%" : selected == ResultsView_Data.Value ? $"\n{volSellFmtd}" : $"\n{percentSell}%\n({volSellFmtd})";

                            ChartText Left, Right;
                            Left = Chart.DrawText($"{iStart}SellSum", strSell, Bars.OpenTimes[iStart], lowest, colorLeft);
                            Right = Chart.DrawText($"{iStart}BuySum", strBuy, Bars.OpenTimes[iStart], lowest, colorRight);

                            Left.HorizontalAlignment = HorizontalAlignment.Left;
                            Right.HorizontalAlignment = HorizontalAlignment.Right;

                            Left.FontSize = FontSizeResults;
                            Right.FontSize = FontSizeResults;
                        }

                        string sumFmtd = FormatResults ? FormatBigNumber(sumValue) : $"{sumValue}";

                        string subtValueFmtd = subtValue > 0 ? FormatBigNumber(subtValue) : $"-{FormatBigNumber(Math.Abs(subtValue))}";
                        string subtFmtd = FormatResults ? subtValueFmtd : $"{subtValue}";

                        string strFormated = OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumFmtd : subtFmtd;

                        Color compareColor = volBuy > volSell ? BuyColor : volBuy < volSell ? SellColor : RtnbFixedColor;
                        Color colorCenter = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareColor;

                        string dynSpaceSum = (selected == ResultsView_Data.Percentage || selected == ResultsView_Data.Value) ? $"\n\n" : $"\n\n\n";
                        ChartText Center = Chart.DrawText($"{iStart}SumCenter", $"{dynSpaceSum}{strFormated}", Bars.OpenTimes[iStart], lowest, colorCenter);

                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults;

                        if (EnableLargeFilter)
                        {
                            // ====== Strength Filter ======
                            double bsStrength = DynamicSeries[iStart] / MADynamicLargeFilter.Result[iStart];
                            Color barColor = bsStrength >= LargeFilterRatio ? ColorLargeResult : colorCenter;

                            Center.Color = barColor;
                            if (LargeFilter_ColoringBars && barColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                        }
                    }

                }
                else
                {
                    if (ShowHist)
                        DrawRectangle_Delta(DeltaRank[priceKey]);

                    if (ShowNumbers)
                    {
                        double deltaValue = DeltaRank[priceKey];
                        string deltaValueFmtd = deltaValue > 0 ? FormatBigNumber(deltaValue) : $"-{FormatBigNumber(Math.Abs(deltaValue))}";
                        string deltaFmtd = FormatNumbers ? deltaValueFmtd : $"{deltaValue}";

                        ChartText nbText = Chart.DrawText($"{iStart}_{i}_DeltaNumber", deltaFmtd, Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);

                        if (VolumeView_Input == VolumeView_Data.Divided)
                            nbText.HorizontalAlignment = deltaValue > 0 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                        else
                            nbText.HorizontalAlignment = HorizontalAlignment.Center;

                        if (deltaValue == 0)
                            nbText.HorizontalAlignment = HorizontalAlignment.Center;

                        nbText.FontSize = FontSizeNumbers;
                    }

                    if (!TotalDeltaRank.ContainsKey(iStart))
                        TotalDeltaRank.Add(iStart, DeltaRank.Values.Sum());
                    else
                        TotalDeltaRank[iStart] = DeltaRank.Values.Sum();

                    int cumulDelta = TotalDeltaRank.Keys.Count <= 1 ? TotalDeltaRank[iStart] : (TotalDeltaRank[iStart] + TotalDeltaRank[iStart - 1]);
                    int prevCumulDelta = TotalDeltaRank.Keys.Count <= 2 ? TotalDeltaRank[iStart] : (TotalDeltaRank[iStart - 1] + TotalDeltaRank[iStart - 2]);

                    double totalDelta = DeltaRank.Values.Sum();

                    CumulDeltaSeries[iStart] = Math.Abs(cumulDelta);
                    DynamicSeries[iStart] = Math.Abs(totalDelta);

                    if (ShowResults) {
                        ResultsView_Data selectedView = ResultsView_Input;

                        if (ShowSideTotal) {
                            int deltaBuy = DeltaRank.Values.Where(n => n > 0).Sum();
                            int deltaSell = DeltaRank.Values.Where(n => n < 0).Sum();

                            int percentBuy = 0;
                            int percentSell = 0;
                            try { percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { };
                            try { percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { }

                            string deltaBuyFmtd = FormatResults ? FormatBigNumber(deltaBuy) : $"{deltaBuy}";
                            string deltaSellFmtd = FormatResults ? FormatBigNumber(deltaSell) : $"{deltaSell}";

                            string strBuy = selectedView == ResultsView_Data.Percentage ? $"\n{percentBuy}%" : selectedView == ResultsView_Data.Value ? $"\n{deltaBuyFmtd}" : $"\n{percentBuy}%\n({deltaBuyFmtd})";
                            string strSell = selectedView == ResultsView_Data.Percentage ? $"\n{percentSell}%" : selectedView == ResultsView_Data.Value ? $"\n{deltaSellFmtd}" : $"\n{percentSell}%\n({deltaSellFmtd})";

                            Color colorLeft = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : SellColor;
                            Color colorRight = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : BuyColor;

                            ChartText Left, Right;
                            Left = Chart.DrawText($"{iStart}SumDeltaSell", strSell, Bars.OpenTimes[iStart], lowest, colorLeft);
                            Right = Chart.DrawText($"{iStart}SumDeltaBuy", strBuy, Bars.OpenTimes[iStart], lowest, colorRight);

                            Left.HorizontalAlignment = HorizontalAlignment.Left;
                            Left.FontSize = FontSizeResults;
                            Right.HorizontalAlignment = HorizontalAlignment.Right;
                            Right.FontSize = FontSizeResults;
                        }

                        string totalDeltaValueFmtd = totalDelta > 0 ? FormatBigNumber(totalDelta) : $"-{FormatBigNumber(Math.Abs(totalDelta))}";
                        string totalDeltaFmtd = FormatResults ? totalDeltaValueFmtd : $"{totalDelta}";
                        string dynSpaceSum = (selectedView == ResultsView_Data.Percentage || selectedView == ResultsView_Data.Value) ? $"\n\n" : $"\n\n\n";

                        Color compareSum = DeltaRank.Values.Sum() > 0 ? BuyColor : DeltaRank.Values.Sum() < 0 ? SellColor : RtnbFixedColor;
                        Color colorCenter = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareSum;

                        ChartText Center = Chart.DrawText($"{iStart}SumDeltaCenter", $"{dynSpaceSum}{totalDeltaFmtd}", Bars.OpenTimes[iStart], lowest, colorCenter);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults;

                        if (ShowMinMaxDelta) {
                            ChartText MinText, MaxText, SubText;
                            double minDelta = Math.Round(MinMaxDelta[0]);
                            double maxDelta = Math.Round(MinMaxDelta[1]);
                            double subDelta = Math.Round(minDelta - maxDelta);

                            string minDeltaValueFmtd = minDelta > 0 ? FormatBigNumber(minDelta) : $"-{FormatBigNumber(Math.Abs(minDelta))}";
                            string maxDeltaValueFmtd = maxDelta > 0 ? FormatBigNumber(maxDelta) : $"-{FormatBigNumber(Math.Abs(maxDelta))}";
                            string subDeltaValueFmtd = subDelta > 0 ? FormatBigNumber(subDelta) : $"-{FormatBigNumber(Math.Abs(subDelta))}";
                            string minDeltaFmtd = FormatResults ? minDeltaValueFmtd : $"{minDelta}";
                            string maxDeltaFmtd = FormatResults ? maxDeltaValueFmtd : $"{maxDelta}";
                            string subDeltaFmtd = FormatResults ? subDeltaValueFmtd : $"{subDelta}";

                            MinText = Chart.DrawText($"{iStart}MinDeltaCenter", $"\n{dynSpaceSum}min:{minDeltaFmtd}", Bars.OpenTimes[iStart], lowest, colorCenter);
                            MaxText = Chart.DrawText($"{iStart}MaxDeltaCenter", $"\n\n{dynSpaceSum}max:{maxDeltaFmtd}", Bars.OpenTimes[iStart], lowest, colorCenter);
                            SubText = Chart.DrawText($"{iStart}SubDeltaCenter", $"\n\n\n{dynSpaceSum}sub:{subDeltaFmtd}", Bars.OpenTimes[iStart], lowest, colorCenter);

                            MinText.HorizontalAlignment = HorizontalAlignment.Center;
                            MaxText.HorizontalAlignment = HorizontalAlignment.Center;
                            SubText.HorizontalAlignment = HorizontalAlignment.Center;
                            MinText.FontSize = FontSizeResults - 1;
                            MaxText.FontSize = FontSizeResults - 1;
                            SubText.FontSize = FontSizeResults - 1;
                        }

                        string cumulDeltaValueFmtd = cumulDelta > 0 ? FormatBigNumber(cumulDelta) : $"-{FormatBigNumber(Math.Abs(cumulDelta))}";
                        string cumulDeltaFmtd = FormatResults ? cumulDeltaValueFmtd : $"{cumulDelta}";

                        Color compareCD = cumulDelta > prevCumulDelta ? BuyColor : cumulDelta < prevCumulDelta ? SellColor : RtnbFixedColor;
                        Color colorCD = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareCD;

                        ChartText CD = Chart.DrawText($"{iStart}CD", $"\n{cumulDeltaFmtd}\n", Bars.OpenTimes[iStart], highest, colorCD);
                        CD.HorizontalAlignment = HorizontalAlignment.Center;
                        CD.VerticalAlignment = VerticalAlignment.Top;
                        CD.FontSize = FontSizeResults;

                        if (EnableLargeFilter) {
                            // ====== Strength Filter ======
                            double deltaLargeStrength = DynamicSeries[iStart] / MADynamicLargeFilter.Result[iStart];
                            Color barColor = deltaLargeStrength >= LargeFilterRatio ? ColorLargeResult : colorCenter;

                            Center.Color = barColor;
                            if (LargeFilter_ColoringBars && barColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);

                            if (LargeFilter_ColoringCD)
                                CD.Color = barColor == ColorLargeResult ? barColor : colorCD;
                        }

                    }

                    // ====== Delta Bubbles Chart ======
                    if (EnableBubblesChart) {
                        double deltaValue = totalDelta;
                        double cumulDeltaValue = cumulDelta;
                        double prevCumulDeltaValue = prevCumulDelta;
                        double prevDeltaValue = TotalDeltaRank[iStart - 1];

                        bool sourceIsDelta = BubblesSource_Input == BubblesSource_Data.Delta;

                        double currentSeriesValue = sourceIsDelta ? DynamicSeries[iStart] : CumulDeltaSeries[iStart];
                        double currenFilterValue = sourceIsDelta ? MABubbles.Result[iStart] : MABubblesCumulDelta.Result[iStart];
                        if (BubblesFilter_Input == BubblesFilter_Data.Standard_Deviation)
                            currenFilterValue = sourceIsDelta ? StdDevBubbles.Result[iStart] : StdDevBubblesCumulDelta.Result[iStart];

                        double deltaStrength = currentSeriesValue / currenFilterValue;

                        if (BubblesFilter_Input == BubblesFilter_Data.Both)
                        {
                            if (sourceIsDelta)
                                deltaStrength = (currentSeriesValue - MABubbles.Result[iStart]) / StdDevBubbles.Result[iStart];
                            else
                                deltaStrength = (currentSeriesValue - MABubblesCumulDelta.Result[iStart]) / StdDevBubblesCumulDelta.Result[iStart];
                        }

                        deltaStrength = Math.Round(Math.Abs(deltaStrength), 2);

                        // Filter + Size for Bubbles
                        double filterSize = deltaStrength < HeatmapLowest_Value ? 2 :   // 1 = too small
                                            deltaStrength < HeatmapLow_Value ? 2.5 :
                                            deltaStrength < HeatmapAverage_Value ? 3 :
                                            deltaStrength < HeatmapHigh_Value ? 4 :
                                            deltaStrength >= HeatmapUltra_Value ? 5 : 5;

                        // Coloring
                        Color heatColor = filterSize == 2 ? HeatmapLowest_Color :
                                        filterSize == 2.5 ? HeatmapLow_Color :
                                        filterSize == 3 ? HeatmapAverage_Color :
                                        filterSize == 4 ? HeatmapHigh_Color : HeatmapUltra_Color;

                        bool sourceFading = sourceIsDelta ? (deltaValue > prevDeltaValue) : (cumulDeltaValue > prevCumulDeltaValue);
                        bool sourcePositiveNegative = sourceIsDelta ? (deltaValue > 0) : (cumulDeltaValue > 0);
                        Color fadingColor = sourceFading ? BuyColor : SellColor;
                        Color positiveNegativeColor = sourcePositiveNegative ? BuyColor : SellColor;

                        Color momentumColor = BubblesMomentumStrategy_Input == BubblesMomentumStrategy_Data.Fading ? fadingColor : positiveNegativeColor;
                        Color colorMode = BubblesColoring_Input == BubblesColoring_Data.Heatmap ? heatColor : momentumColor;

                        // X-value
                        double maxLengthMaxBubble = maxLength * 1.4 * BubblesSizeMultiplier; // 1.4 => Slightly bigger than the Bar Body
                        double maxLengthBubble = maxLength * BubblesSizeMultiplier;

                        double dynMaxProportion = filterSize == 5 ? maxLengthMaxBubble : maxLengthBubble;
                        double proportion = filterSize * (dynMaxProportion / 3); // Same as => filterSize * (dynMaxProportion - (dynMaxProportion / 1.50)

                        double dynMaxLength = filterSize == 5 ? 5 : 4;
                        double dynLength = proportion / dynMaxLength;

                        double x1Position = filterSize == 5 ? -(maxLengthMaxBubble / 3) :
                                        filterSize == 4 ? -(maxLengthBubble / 3) :
                                        filterSize == 3 ? -(maxLengthBubble / 4) :
                                        filterSize == 2.5 ? -(maxLengthBubble / 5) : -(maxLengthBubble / 6);

                        DateTime xBar = Bars.OpenTimes[iStart];
                        DateTime x1 = xBar.AddMilliseconds(x1Position);
                        DateTime x2 = x1.AddMilliseconds(dynLength * 2);

                        if (isPriceBasedChart || gapWeekday)
                        {
                            double maxLength_LeftSide = xBar.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                            double leftSideProportion = maxLength_LeftSide / 3;

                            // Repeat code of X-Value with maxLength_LeftSide
                            maxLengthMaxBubble = maxLength_LeftSide * 1.4 * BubblesSizeMultiplier;
                            maxLengthBubble = maxLength_LeftSide * BubblesSizeMultiplier;

                            dynMaxProportion = filterSize == 5 ? maxLengthMaxBubble : maxLengthBubble;
                            proportion = filterSize * (dynMaxProportion / 3);

                            dynMaxLength = filterSize == 5 ? 5 : 4;
                            double dynLength_ToMiddle = proportion / dynMaxLength;

                            x1Position = filterSize == 5 ? -(maxLengthMaxBubble / 3) :
                                            filterSize == 4 ? -(maxLengthBubble / 3) :
                                            filterSize == 3 ? -(maxLengthBubble / 4) :
                                            filterSize == 2.5 ? -(maxLengthBubble / 5) : -(maxLengthBubble / 6);
                            // =================

                            if (avoidStretching)
                                dynLength = 0;

                            x1 = xBar.AddMilliseconds(x1Position);
                            x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(dynLength);
                        }

                        // Y-Value
                        double maxHeightBubble = heightPips * BubblesSizeMultiplier;
                        proportion = filterSize * maxHeightBubble;
                        double dynHeight = proportion / 5;

                        double y1 = Bars.ClosePrices[iStart] + (Symbol.PipSize * dynHeight);
                        double y2 = Bars.ClosePrices[iStart] - (Symbol.PipSize * dynHeight);

                        bool gapWeekend = xBar.DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart + 1].DayOfWeek == DayOfWeek.Sunday;
                        if (gapWeekend) {
                            x1 = xBar;
                            x2 = x1.AddMilliseconds(dynLength);
                        }

                        // Draw
                        Color colorModeWithAlpha = Color.FromArgb((int)(2.55 * BubblesOpacity), colorMode.R, colorMode.G, colorMode.B);
                        ChartEllipse ellipse = Chart.DrawEllipse($"{iStart}_Bubble", x1, y1, x2, y2, colorModeWithAlpha);
                        ellipse.IsFilled = true;

                        if (ShowBubbleValue) {
                            string cumulDeltaFmtd = cumulDeltaValue > 0 ? FormatBigNumber(cumulDeltaValue) : $"-{FormatBigNumber(Math.Abs(cumulDeltaValue))}";
                            string sumValueFmtd = deltaValue > 0 ? FormatBigNumber(deltaValue) : $"-{FormatBigNumber(Math.Abs(deltaValue))}";

                            string dynBubbleValue = sourceIsDelta ? sumValueFmtd : cumulDeltaFmtd;

                            double deltaLargeStrength = DynamicSeries[iStart] / MADynamicLargeFilter.Result[iStart];

                            ChartText Center = Chart.DrawText($"{iStart}_BubbleValue", dynBubbleValue, Bars.OpenTimes[iStart], Bars[iStart].Close, deltaLargeStrength >= LargeFilterRatio ? ColorLargeResult : RtnbFixedColor);
                            Center.HorizontalAlignment = HorizontalAlignment.Center;
                            if (isPriceBasedChart && avoidStretching)
                                Center.HorizontalAlignment = HorizontalAlignment.Left;
                            Center.VerticalAlignment = VerticalAlignment.Center;
                            Center.FontSize = FontSizeResults;
                        }
                        if (ShowStrengthValue) {
                            ChartText Center = Chart.DrawText($"{iStart}_StrengthValue", $"{deltaStrength} \n {filterSize} ", Bars.OpenTimes[iStart], y2, RtnbFixedColor);
                            Center.HorizontalAlignment = HorizontalAlignment.Center;
                            if (isPriceBasedChart && avoidStretching)
                                Center.HorizontalAlignment = HorizontalAlignment.Left;
                            Center.VerticalAlignment = VerticalAlignment.Center;
                            Center.FontSize = FontSizeNumbers;
                        }
                    }

                    // Tick Delta = Spike Filter
                    if (EnableSpikeFilter) {
                        /*
                        - StdDev:
                            - It's like doing [ (total bar delta - ema(bar delta) ) / StdDev ] equation
                                where ( total bar delta - ema(bar delta) ) result is the delta row value.

                            - At lower timeframes, acts like a Heatmap. (More Spikes)
                            - At Higher timeframes, acts like "Less but powerful" Spikes Levels
                        - MA:
                            - With the current threshold (for StdDev), the Bubbles Bars are lesser colorful.
                                but painted Spikes Levels are quite meaningful.

                            - At Lower timeframes, acts like "Moderate but powerful" Spikes Levels
                            - At Higher timeframes, acts like a Heatmap. (More Spikes)

                        - MAs that generate Spike Noise until there are enough Period bars:
                            - EMA, WilderSmoothing, Double/Triple EMA, KaufmanAdaptive
                        */
                        double rowValue = DeltaRank[priceKey];

                        double spikeFilterValue = SpikeFilter_Input == SpikeFilter_Data.MA ? MASpikeFilter.Result[iStart] : StdDevSpikeFilter.Result[iStart];

                        double rowStrength = rowValue / spikeFilterValue;
                        rowStrength = Math.Round(Math.Abs(rowStrength), 2);

                        // Bubbles Columns Charts for ODF_Aggregated
                        // Looks better only with aligned rows
                        /*
                        Color spikeHeatColor = rowStrength <= 0.5 ? Color.Aqua :
                                           rowStrength < 1.2 ? Color.White :
                                           rowStrength < 2.5  ? Color.Yellow :
                                           rowStrength < 3.5 ? Color.Gold : Color.Red;
                        */
                        Color spikeHeatColor = rowStrength < SpikeMinimum_Value ? Color.Transparent :
                                            rowStrength < SpikeAverage_Value ? SpikeAverage_Color :
                                            rowStrength < SpikeHigh_Value ? SpikeHigh_Color :
                                            rowStrength >= SpikeUltra_Value ? SpikeUltra_Color : SpikeUltra_Color;


                        if (rowStrength > SpikeMinimum_Value)
                        {
                            double proportion = 1 * (maxLength / 3);
                            double dynLength = proportion / 1;

                            double Y1 = priceKey;
                            double Y2 = priceKey - rowHeight;

                            DateTime xBar = Bars.OpenTimes[iStart];
                            DateTime X1 = xBar.AddMilliseconds(-(maxLength / 3));
                            DateTime X2 = X1.AddMilliseconds(dynLength * 2);

                            if (isPriceBasedChart || gapWeekday)
                            {
                                double maxLength_LeftSide = xBar.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
                                double leftSideProportion = maxLength_LeftSide / 3;

                                proportion = 1 * (maxLength_LeftSide / 3);
                                double dynLength_ToMiddle = proportion / 1;

                                // For real-time => Avoid stretching the bubbles away ad infinitum
                                if (avoidStretching)
                                    dynLength = 0;

                                X1 = xBar.AddMilliseconds(-(maxLength_LeftSide / 3));
                                X2 = X1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(dynLength);
                            }

                            // For real-time - "repaint/update" the spike price level.
                            if (IsLastBar)
                                try { Chart.RemoveObject($"{iStart}_{i}_Spike"); } catch { };

                            if (SpikeView_Input == SpikeView_Data.Bubbles) {
                                bool gapWeekend = xBar.DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart + 1].DayOfWeek == DayOfWeek.Sunday;
                                if (gapWeekend) {
                                    X1 = xBar;
                                    X2 = X1.AddMilliseconds(dynLength);
                                }

                                ChartEllipse ellipseTick = Chart.DrawEllipse($"{iStart}_{i}_Spike", X1, Y1, X2, Y2, spikeHeatColor);
                                ellipseTick.IsFilled = true;
                            }
                            else {
                                DateTime positionX = VolumeView_Input == VolumeView_Data.Divided ? xBar : X2;
                                double positionY = (Y1 + Y2) / 2;
                                ChartIcon icon = Chart.DrawIcon($"{iStart}_{i}_Spike", IconView_Input, positionX, positionY, spikeHeatColor);
                            }
                            if (EnableSpikeNotification && IsLastBar && !lockTickNotify) {
                                string symbolName = Symbol.Name;
                                string caption = "Order Flow Ticks";
                                string popupText = $"{symbolName} => Tick Spike at {Server.Time}";
                                if (NotificationType_Input == NotificationType_Data.Sound) {
                                    Notifications.PlaySound(SoundType_Spike);
                                    lockTickNotify = true;
                                } else if (NotificationType_Input == NotificationType_Data.Popup) {
                                    Notifications.ShowPopup(caption, popupText, PopupNotificationState.Information);
                                    lockTickNotify = true;
                                } else {
                                    Notifications.PlaySound(SoundType_Spike);
                                    Notifications.ShowPopup(caption, popupText, PopupNotificationState.Information);
                                    lockTickNotify = true;
                                }
                            }
                        }

                        if (ShowTickStrengthValue)
                        {
                            ChartText Center = Chart.DrawText($"{iStart}_{i}_TickStrengthValue", $"   <= {rowStrength}", Bars.OpenTimes[iStart], priceKey, RtnbFixedColor);
                            Center.HorizontalAlignment = HorizontalAlignment.Right;
                            Center.FontSize = FontSizeNumbers;
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

            if (IsLastBar && !isBarClosed)
                endTime = TicksOHLC.Last().OpenTime;

            // TicksOHLC.OpenTimes => .GetIndexByExactTime() and GetIndexByTime() returns -1 for historical data
            // So, the VP/Wicks loop can't be optimized like the ODF_Ticks' Python version.
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
            // ====== ======= =======
            void RankVolume(double tickPrice)
            {
                double prevSegmentValue = 0.0;
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (prevSegmentValue != 0 && tickPrice >= prevSegmentValue && tickPrice <= Segments[i])
                    {
                        double priceKey = Segments[i];
                        double prevDelta = 0;
                        if (ShowMinMaxDelta)
                            prevDelta = DeltaRank.Values.Sum();

                        if (VolumesRank.ContainsKey(priceKey))
                        {
                            VolumesRank[priceKey] += 1;

                            if (tickPrice > prevTick && prevTick != 0)
                                VolumesRank_Up[priceKey] += 1;
                            else if (tickPrice < prevTick && prevTick != 0)
                                VolumesRank_Down[priceKey] += 1;
                            else if (tickPrice == prevTick && prevTick != 0) {
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

                        if (ShowMinMaxDelta)
                        {
                            double currentDelta = DeltaRank.Values.Sum();
                            if (prevDelta > currentDelta)
                                MinMaxDelta[0] = prevDelta; // Min
                            if (prevDelta < currentDelta)
                                MinMaxDelta[1] = prevDelta; // Max before final delta
                        }
                        break;
                    }
                    prevSegmentValue = Segments[i];
                }
            }
        }

        private double[] GetWicks(DateTime startTime, DateTime endTime)
        {
            double min = Int32.MaxValue;
            double max = 0;

            if (IsLastBar && !isBarClosed)
                endTime = TicksOHLC.Last().OpenTime;

            for (int tickIndex = 0; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime) {
                    if (tickBar.OpenTime > endTime)
                        break;
                    else
                        continue;
                }

                if (tickBar.Close < min)
                    min = tickBar.Close;
                else if (tickBar.Close > max)
                    max = tickBar.Close;
            }

            double[] toReturn = { min, max };
            return toReturn;
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

        private void DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
        }
        private void Second_DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt2", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }

        // ************************** VOLUME RENKO/RANGE **************************
        /*
            Original source code by srlcarlg (me) (https://ctrader.com/algos/indicators/show/3045)
            Uses Ticks Data to make the calculation of volume, just like Candles.

            Refactored in Order Flow Ticks v2.0 revision 1.5
        */
        private void VolumeInitialize(bool onlyDate = false)
        {
            DateTime lastBarDate = Bars.LastBar.OpenTime.Date;

            if (LoadTickFrom_Input == LoadTickFrom_Data.Custom) {
                // ==== Get datetime to load from: dd/mm/yyyy ====
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out fromDateTime)) {
                    if (fromDateTime > lastBarDate) {
                        fromDateTime = lastBarDate;
                        Print($"Invalid DateTime '{StringDate}'. Using '{fromDateTime}'");
                    }
                } else {
                    fromDateTime = lastBarDate;
                    Print($"Invalid DateTime '{StringDate}'. Using '{fromDateTime}'");
                }
            }
            else {
                fromDateTime = LoadTickFrom_Input switch {
                    LoadTickFrom_Data.Yesterday => MarketData.GetBars(TimeFrame.Daily).Last().OpenTime.Date,
                    LoadTickFrom_Data.Before_Yesterday => MarketData.GetBars(TimeFrame.Daily).Last(1).OpenTime.Date,
                    LoadTickFrom_Data.One_Week => MarketData.GetBars(TimeFrame.Weekly).Last().OpenTime.Date,
                    LoadTickFrom_Data.Two_Week => MarketData.GetBars(TimeFrame.Weekly).Last(1).OpenTime.Date,
                    LoadTickFrom_Data.Monthly => MarketData.GetBars(TimeFrame.Monthly).Last().OpenTime.Date,
                    _ => lastBarDate,
                };
            }

            if (onlyDate) {
                DrawStartVolumeLine();
                return;
            }

            // ==== Check if existing ticks data on the chart really needs more data ====
            DateTime FirstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
            if (FirstTickTime >= fromDateTime) {
                while (TicksOHLC.OpenTimes.FirstOrDefault() > fromDateTime) {
                    int loadedCount = TicksOHLC.LoadMoreHistory();
                    // Print($"Initialize() => Loaded {loadedCount} Ticks, Current Tick Date: {TicksOHLC.OpenTimes.FirstOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
                // Print($"Initialize() => Data Collection Finished, First Tick from: {TicksOHLC.OpenTimes.FirstOrDefault()}");
            }

            DrawStartVolumeLine();
        }

        private void DrawStartVolumeLine() {
            try {
                DateTime firstTickDate = TicksOHLC.OpenTimes.FirstOrDefault();
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeStart", firstTickDate, Color.Red);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText("VolumeStartText", "Tick Volume Data \n ends here", firstTickDate, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(firstTickDate)], Color.Red);
                textInfo.HorizontalAlignment = HorizontalAlignment.Right;
                textInfo.VerticalAlignment = VerticalAlignment.Top;
                textInfo.FontSize = 8;
            } catch { };
        }
        private void LoadMoreTicksOnChart()
        {
            /*
                At the moment, LoadMoreHistoryAsync() doesn't work
                while Calculate() is invoked for historical data (!IsLastBar)
                and loading at each price update (IsLastBar) isn't wanted.

                Plus, LoadMoreHistory() performance seems better.
            */
            DateTime firstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();

            if (firstTickTime > fromDateTime)
            {
                // "Freeze" the Chart at the beginning of Calculate()
                while (TicksOHLC.OpenTimes.FirstOrDefault() > fromDateTime)
                {
                    int loadedCount = TicksOHLC.LoadMoreHistory();
                    // Print($"Calculate() => Loaded {loadedCount} Ticks, Current Tick Date: {TicksOHLC.OpenTimes.FirstOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
            }

            unlockChart();

            void unlockChart() {
                ticksProgressBar.IsIndeterminate = false;
                ticksProgressBar.IsVisible = false;
                ticksProgressBar = null;
                ticksLoadingComplete = true;
                // Print($"Calculate() => Data Collection Finished, First Tick from: {TicksOHLC.OpenTimes.FirstOrDefault()}");
                DrawStartVolumeLine();
            }
        }


        public void ClearAndRecalculate(bool isStatic = false)
        {
            // The chart should already be clear
            // No objects and bar colors
            // Unless it's a static update.

            int startIndex;
            if (Lookback != -1 && Lookback > 0)
                startIndex = isStatic ? Bars.Count - Lookback : Lookback;
            else
                startIndex = Bars.OpenTimes.GetIndexByTime(TicksOHLC.OpenTimes.FirstOrDefault());

            // The plot (sometimes in some options, like Volume View) is too fast, slow down a bit.
            Thread.Sleep(200);

            // Historical data
            for (int index = startIndex; index < Bars.Count; index++)
            {
                // Ensures to update DynamicSeries values with the current rowHeight.
                if (index < (Bars.Count - Lookback) && Lookback != -1 && !isStatic)
                {
                    CreateOrderflow(index, true);

                    if (VolumeMode_Input == VolumeMode_Data.Normal)
                        DynamicSeries[index] = VolumesRank.Values.Sum();
                    else if (VolumeMode_Input == VolumeMode_Data.Buy_Sell) {
                        double sumValue = VolumesRank_Up.Values.Sum() + VolumesRank_Down.Values.Sum();
                        double subtValue = VolumesRank_Up.Values.Sum() - VolumesRank_Down.Values.Sum();

                        DynamicSeries[index] = OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumValue : subtValue;
                    } else {
                        if (!TotalDeltaRank.ContainsKey(index))
                            TotalDeltaRank.Add(index, DeltaRank.Values.Sum());
                        else
                            TotalDeltaRank[index] = DeltaRank.Values.Sum();

                        int cumulDeltaChange = TotalDeltaRank.Keys.Count <= 1 ? TotalDeltaRank[index] : (TotalDeltaRank[index] + TotalDeltaRank[index - 1]);
                        double totalDelta = DeltaRank.Values.Sum();

                        CumulDeltaSeries[index] = Math.Abs(cumulDeltaChange);
                        DynamicSeries[index] = Math.Abs(totalDelta);
                    }

                    continue;
                }
                // Then draw only in the lookback
                try { CreateOrderflow(index); } catch { }
            }

            DrawStartVolumeLine();

            void CreateOrderflow(int i, bool isLookBack = false) {
                Segments.Clear();
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                double[] resetDelta = {0, 0};
                MinMaxDelta = resetDelta;
                OrderFlow(i, isLookBack);
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
    // ====================== PARAMS PANEL ============================
    /*
    Primarily refactored by LLM
    What I've done since the first refactoring, by order:
        - Fixed errors from its refactoring (obviously)
        - Added ComboBoxTextMap
        - (LLM) Region (UI)
        - (LLM) Expand/Collapse Regions (UI)
        - Modified CreateCheckboxWithLabel to display labels above checkbox. (checkBoxTextMap)
        - (LLM) Save/Load Params (functionality)
        - Fixed wrongly Save/Load implementation
        - (LLM) RefreshHighlighting (functionality)
        - Added RegionOrder
        - Added New Inputs (Spike Filter, Bubbles Chart)
        - (LLM) ScrollViewer to CreateContentPanel
        - (LLM) CreateTransparentStyle to ScrollViewer
        - (LLM) CreateFooter
        - (LLM) Replace StackPanel to Grid
        - Added New Inputs (Misc, 2+ Result options)
        - Added Key Strategy with StorageKeyConfig
        - Added ProgressBar
        - (LLM after some prompt pain) Refactor of Save/Load methods => One file(One Key) containing all parameters.
        - Added 'isLoading' (RecalculateOutsideWithMsg was being called at each parameter on LoadParams())
        - Implement onChange methods of remaining settings
        - Added Bubbles Chart lock template + CheckboxHandler
        - Added 'PanelMode' parameter to Save/Load methods
        - (LLM) Refactor Styles class
        - Added CreateButtonStyle
        - Refactor RefreshHighlighting => Fix colors + Specific highlighting by paramType
        - Fine-Tuning of UI Colors on Dark/Light Theme
        - Create ApplyBtn for BarsToShowKey/RowHeightKey (only)
        - Added EnableNotifyKey for Tick Spike
    */
    public enum ParamInputType { Text, Checkbox, ComboBox }

    public class ParamDefinition
    {
        public string Region { get; init; }
        public int RegionOrder { get; init; }
        public string Key { get; init; }
        public string Label { get; init; }
        public ParamInputType InputType { get; init; }
        public Func<IndicatorParams, object> GetDefault { get; init; }
        public Action<string> OnChanged { get; init; }
        public Func<IEnumerable<string>> EnumOptions { get; init; } = null;
        public Func<bool> IsVisible { get; set; } = () => true;
    }

    public class ParamsPanel : CustomControl
    {
        private readonly OrderFlowTicksV20 Outside;
        private readonly IndicatorParams FirstParams;
        private Button ModeBtn;
        private Button SaveBtn;
        private Button ApplyBtn;
        private ProgressBar _progressBar;
        private bool isLoadingParams;

        private readonly Dictionary<string, TextBox> textInputMap = new();

        private readonly Dictionary<string, TextBlock> checkBoxTextMap = new();
        private readonly Dictionary<string, CheckBox> checkBoxMap = new();

        private readonly Dictionary<string, ComboBox> comboBoxMap = new();
        private readonly Dictionary<string, TextBlock> comboBoxTextMap = new();

        private readonly List<ParamDefinition> _paramDefinitions;
        private readonly Dictionary<string, RegionSection> _regionSections = new();
        private readonly Dictionary<string, object> _originalValues = new();
        private ColorTheme ApplicationTheme => Outside.Application.ColorTheme;

        public ParamsPanel(OrderFlowTicksV20 indicator, IndicatorParams defaultParams)
        {
            Outside = indicator;
            FirstParams = defaultParams;
            _paramDefinitions = DefineParams();

            AddChild(CreateTradingPanel());

            LoadParams(); // If not present, use defaults params.
            RefreshVisibility(); // Refresh UI with the current values.
        }

        private List<ParamDefinition> DefineParams()
        {
            return new List<ParamDefinition>
            {
                new()
                {
                    Region = "General",
                    RegionOrder = 1,
                    Key = "BarsToShowKey",
                    Label = "Nº Bars",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.N_Bars,
                    OnChanged = _ => UpdateBarsToShow()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 1,
                    Key = "RowHeightKey",
                    Label = "Row(pips)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.RowHeightInPips.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateRowHeight()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 1,
                    Key = "VolumeViewKey",
                    Label = "Volume View",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.VolView.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(VolumeView_Data)),
                    OnChanged = _ => UpdateVolumeView(),
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Normal && !Outside.EnableBubblesChart
                },

                new()
                {
                    Region = "Coloring",
                    Key = "LargestDividedKey",
                    RegionOrder = 2,
                    Label = "Largest?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.OnlyLargestDivided,
                    OnChanged = _ => UpdateCheckbox("LargestDividedKey", val => Outside.ColoringOnlyLarguest = val),
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Normal && Outside.VolumeView_Input == VolumeView_Data.Divided && !Outside.EnableBubblesChart
                },

                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "ShowResultsKey",
                    Label = "Show?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowResults,
                    OnChanged = _ => UpdateCheckbox("ShowResultsKey", val => Outside.ShowResults = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "EnableLargeKey",
                    Label = "Enable Filter?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableLargeFilter,
                    OnChanged = _ => UpdateCheckbox("EnableLargeKey", val => Outside.EnableLargeFilter = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "ShowSideTotalKey",
                    Label = "Side(total)?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowSideTotal,
                    OnChanged = _ => UpdateCheckbox("ShowSideTotalKey", val => Outside.ShowSideTotal = val),
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Normal && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "ResultViewKey",
                    Label = "Side(view)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ResultView.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ResultsView_Data)),
                    OnChanged = _ => UpdateResultView(),
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Normal && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "ShowMinMaxKey",
                    Label = "Min/Max?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowMinMax,
                    OnChanged = _ => UpdateCheckbox("ShowMinMaxKey", val => Outside.ShowMinMaxDelta = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.VolumeView_Input == VolumeView_Data.Profile && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "OperatorKey",
                    Label = "Operator",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.OperatorBuySell.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(OperatorBuySell_Data)),
                    OnChanged = _ => UpdateOperator(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Buy_Sell && !Outside.EnableBubblesChart
                },

                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "EnableSpikeKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableSpike,
                    OnChanged = _ => UpdateCheckbox("EnableSpikeKey", val => Outside.EnableSpikeFilter = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeFilterKey",
                    Label = "Filter",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilter.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeFilter_Data)),
                    OnChanged = _ => UpdateSpikeFilter(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "EnableNotifyKey",
                    Label = "Notify?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableSpikeNotify,
                    OnChanged = _ => UpdateCheckbox("EnableNotifyKey", val => Outside.EnableSpikeNotification = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    Key = "SpikeViewKey",
                    Label = "View",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeView.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeView_Data)),
                    OnChanged = _ => UpdateSpikeView(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "IconViewKey",
                    Label = "Icon",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.IconView.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ChartIconType)),
                    OnChanged = _ => UpdateIconView(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.SpikeView_Input == SpikeView_Data.Icon && !Outside.EnableBubblesChart
                },

                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "EnableBubblesKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableBubbles,
                    OnChanged = _ => UpdateCheckbox("EnableBubblesKey", val => Outside.EnableBubblesChart = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubblesSizeKey",
                    Label = "Size Multiplier",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesSize,
                    OnChanged = _ => UpdateBubblesSize(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubblesSourceKey",
                    Label = "Source",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesSource.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesSource_Data)),
                    OnChanged = _ => UpdateBubblesSource(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubblesFilterKey",
                    Label = "Filter",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesFilter.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesFilter_Data)),
                    OnChanged = _ => UpdateBubblesFilter(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubblesColoringKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesColoring.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesColoring_Data)),
                    OnChanged = _ => UpdateBubblesColoring(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubblesMomentumKey",
                    Label = "Strategy",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesMomentumStrategy.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesMomentumStrategy_Data)),
                    OnChanged = _ => UpdateBubblesMomentum(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.BubblesColoring_Input == BubblesColoring_Data.Momentum
                },


                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "ShowHistKey",
                    Label = "Histogram?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowHist,
                    OnChanged = _ => UpdateCheckbox("ShowHistKey", val => Outside.ShowHist = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "FillHistKey",
                    Label = "Fill Hist?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillHist,
                    OnChanged = _ => UpdateCheckbox("FillHistKey", val => Outside.FillHist = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "ShowNumbersKey",
                    Label = "Numbers?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowNumbers,
                    OnChanged = _ => UpdateCheckbox("ShowNumbersKey", val => Outside.ShowNumbers = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "BubbleValueKey",
                    Label = "Bubbles-V?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowBubbleValue,
                    OnChanged = _ => UpdateCheckbox("BubbleValueKey", val => Outside.ShowBubbleValue = val),
                    IsVisible = () => Outside.EnableBubblesChart
                },
            };
        }

        private ControlBase CreateTradingPanel()
        {
            // Replace StackPanel to Grid
            // So the Footer stays pinned at the bottom, always visible.
            Grid mainPanel = new(3, 1);

            mainPanel.Rows[0].SetHeightToAuto();
            mainPanel.AddChild(CreateHeader(), 0, 0);

            mainPanel.Rows[1].SetHeightInStars(1); // Takes remaining space
            mainPanel.AddChild(CreateContentPanel(), 1, 0);

            mainPanel.Rows[2].SetHeightToAuto();
            mainPanel.AddChild(CreateFooter(), 2, 0);

            return mainPanel;
        }

        private static ControlBase CreateHeader()
        {
            var grid = new Grid(0, 0);
            grid.AddChild(new TextBlock
            {
                Text = "Order Flow Ticks",
                Margin = "10 7",
                Style = Styles.CreateHeaderStyle(),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center

            });
            var border = new Border
            {
                BorderThickness = "0 0 0 1",
                Style = Styles.CreateCommonBorderStyle(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 280,
                Child = grid
            };
            return border;
        }

        private ControlBase CreateFooter()
        {
            var footerGrid = new Grid(2, 3)
            {
                Margin = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            footerGrid.Columns[0].SetWidthInStars(1);
            footerGrid.Columns[1].SetWidthInPixels(8);
            footerGrid.Columns[2].SetWidthToAuto();

            var saveButton = CreateSaveButton();
            footerGrid.AddChild(saveButton, 0, 2);

            _progressBar = new ProgressBar {
                Height = 12,
                Margin = "0 2 0 0"
            };
            footerGrid.AddChild(_progressBar, 0, 0);

            footerGrid.AddChild(CreateApplyButton_TextInput(), 1, 0, 1, 3);

            return footerGrid;
        }

        private Control CreateContentPanel()
        {
            var contentPanel = new StackPanel { Margin = 10 };

            // --- Mode controls at the top ---
            var grid = new Grid(4, 5);
            grid.Columns[1].SetWidthInPixels(5);
            grid.Columns[3].SetWidthInPixels(5);

            grid.AddChild(CreatePassButton("<"), 0, 0);
            grid.AddChild(CreateModeInfo_Button(FirstParams.VolMode.ToString()), 0, 1, 1, 3);
            grid.AddChild(CreatePassButton(">"), 0, 4);

            contentPanel.AddChild(grid);

            // --- Create region sections ---
            var groups = _paramDefinitions
                .GroupBy(p => p.Region)
                .OrderBy(g => g.FirstOrDefault().RegionOrder);
            // With g.FirstOrDefault().Key => Worked as expected until 2x "Enable[...]Key" appear

            foreach (var group in groups)
            {
                var section = new RegionSection(group.Key, group);
                _regionSections[group.Key] = section;

                // param grid inside section
                var groupGrid = new Grid(4, 5);
                groupGrid.Columns[1].SetWidthInPixels(5);
                groupGrid.Columns[3].SetWidthInPixels(5);

                int row = 0, col = 0;
                foreach (var param in group)
                {
                    var control = CreateParamControl(param);
                    groupGrid.AddChild(control, row, col);
                    col += 2;
                    if (col > 4) { row++; col = 0; }
                }

                section.AddParamControl(groupGrid);
                contentPanel.AddChild(section.Container);
            }

            ScrollViewer scroll = new() {
                Content = contentPanel,
                Style = Styles.CreateScrollViewerTransparentStyle(),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = Outside.Chart.Height - 100
            };

            Outside.Chart.SizeChanged += (_) => {
                scroll.MaxHeight = Outside.Chart.Height - 100;
            };

            return scroll;
        }

        private ControlBase CreateParamControl(ParamDefinition param)
        {
            return param.InputType switch
            {
                ParamInputType.Text => CreateInputWithLabel(param.Label, param.GetDefault(FirstParams).ToString(), param.Key, param.OnChanged),
                ParamInputType.Checkbox => CreateCheckboxWithLabel(param.Label, (bool)param.GetDefault(FirstParams), param.Key, param.OnChanged),
                ParamInputType.ComboBox => CreateComboBoxWithLabel(param.Label, param.Key, (string)param.GetDefault(FirstParams), param.EnumOptions(), param.OnChanged),
                _ => throw new NotSupportedException()
            };
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
                BackgroundColor = Color.FromHex("#7F808080"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            button.Click += label == ">" ? NextModeEvent : PrevModeEvent;
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
                Style = Styles.CreateButtonStyle(),
                HorizontalAlignment = HorizontalAlignment.Center

            };
            button.Click += _ => ResetParamsEvent();
            ModeBtn = button;
            return button;
        }

        private Button CreateSaveButton()
        {
            Button button = new()
            {
                Text = "💾 Save",
                Margin = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            button.Click += (_) => SaveParams();
            SaveBtn = button;
            return button;
        }
        private Button CreateApplyButton_TextInput()
        {
            Button button = new() {
                Text = "Apply ✓",
                Padding = 0,
                Width = 50,
                Height = 20,
                Margin = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            button.Click += (_) => RecalculateOutsideWithMsg();
            ApplyBtn = button;
            return button;
        }

        private Panel CreateInputWithLabel(string label, string defaultValue, string key, Action<string> onChanged)
        {
            var input = new TextBox
            {
                Text = defaultValue,
                Style = Styles.CreateInputStyle(),
                TextAlignment = TextAlignment.Center,
                Margin = "0 5 0 0"
            };
            input.TextChanged += _ => onChanged?.Invoke(key);
            textInputMap[key] = input;

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0",
            };

            stack.AddChild(new TextBlock { Text = label, TextAlignment = TextAlignment.Center });
            stack.AddChild(input);
            return stack;
        }

        private Panel CreateComboBoxWithLabel(string label, string key, string selected, IEnumerable<string> options, Action<string> onChanged)
        {
            var combo = new ComboBox
            {
                Style = Styles.CreateInputStyle(),
                Margin = "0 5 0 0",

            };
            foreach (var option in options)
                combo.AddItem(option);
            combo.SelectedItem = selected;
            combo.SelectedItemChanged += _ => onChanged?.Invoke(key);
            comboBoxMap[key] = combo;

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0",
            };

            var text = new TextBlock { Text = label, TextAlignment = TextAlignment.Center };
            comboBoxTextMap[key] = text;

            stack.AddChild(text);
            stack.AddChild(combo);

            return stack;
        }

        private ControlBase CreateCheckboxWithLabel(string label, bool defaultValue, string key, Action<string> onChanged)
        {
            var checkbox = new CheckBox {
                Margin = "0 0 5 0",
                IsChecked = defaultValue,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            checkbox.Click += _ => onChanged?.Invoke(key);
            checkBoxMap[key] = checkbox;

            var text = new TextBlock { Text = label, TextAlignment = TextAlignment.Center };
            checkBoxTextMap[key] = text;

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 10",
            };

            stack.AddChild(text);
            stack.AddChild(checkbox);

            return stack;
        }

        private void ResetParamsEvent() => ChangeParams(FirstParams);

        private void ChangeParams(IndicatorParams p)
        {
            foreach (var param in _paramDefinitions)
            {
                switch (param.InputType)
                {
                    case ParamInputType.Text:
                        textInputMap[param.Key].Text = param.GetDefault(p).ToString();
                        break;
                    case ParamInputType.Checkbox:
                        checkBoxMap[param.Key].IsChecked = (bool)param.GetDefault(p);
                        break;
                    case ParamInputType.ComboBox:
                        comboBoxMap[param.Key].SelectedItem = param.GetDefault(p).ToString();
                        break;
                }
            }
        }

        private void UpdateCheckbox(string key, Action<bool> applyAction)
        {
            bool value = checkBoxMap[key].IsChecked ?? false;
            applyAction(value);
            CheckboxHandler(key, value);
        }
        private void CheckboxHandler(string key, bool value)
        {
            if (key == "EnableBubblesKey") {
                if (value)
                    Outside.Chart.ChartType = ChartType.Line;
                else if (!value && _originalValues.ContainsKey("ShowHistKey")) {
                    // ContainsKey avoids crash when loading
                    Outside.ShowHist = (bool)_originalValues["ShowHistKey"];
                    Outside.ShowNumbers = (bool)_originalValues["ShowNumbersKey"];
                    Outside.ShowResults = (bool)_originalValues["ShowResultsKey"];
                    Outside.EnableSpikeFilter = (bool)_originalValues["EnableSpikeKey"];
                    Outside.Chart.ChartType = ChartType.Hlc;
                }
            } else if (key == "FillHistKey") {
                RecalculateOutsideWithMsg(false);
                return;
            } else if (key == "EnableNotifyKey") {
                RecalculateOutsideWithMsg(false);
                return;
            }

            RecalculateOutsideWithMsg();
        }

        // ==== General ====
        private void UpdateBarsToShow()
        {
            int value = int.TryParse(textInputMap["BarsToShowKey"].Text, out var n) ? n : -2;
            if (value >= -1 && value != Outside.GetLookback())
            {
                Outside.SetLookback(value);
                ApplyBtn.IsVisible = true;
            }
        }
        private void UpdateRowHeight()
        {
            if (double.TryParse(textInputMap["RowHeightKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                double height = Outside.Symbol.PipSize * value;
                if (height != Outside.GetRowHeight())
                {
                    Outside.SetRowHeight(height);
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateVolumeView()
        {
            var selected = comboBoxMap["VolumeViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out VolumeView_Data viewType) && viewType != Outside.VolumeView_Input)
            {
                Outside.VolumeView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Results ====
        private void UpdateResultView()
        {
            var selected = comboBoxMap["ResultViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out ResultsView_Data viewType) && viewType != Outside.ResultsView_Input)
            {
                Outside.ResultsView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateOperator()
        {
            var selected = comboBoxMap["OperatorKey"].SelectedItem;
            if (Enum.TryParse(selected, out OperatorBuySell_Data op) && op != Outside.OperatorBuySell_Input)
            {
                Outside.OperatorBuySell_Input = op;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Spike Filter ====
        private void UpdateSpikeFilter()
        {
            var selected = comboBoxMap["SpikeFilterKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeFilter_Data filterType) && filterType != Outside.SpikeFilter_Input)
            {
                Outside.SpikeFilter_Input = filterType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateSpikeView()
        {
            var selected = comboBoxMap["SpikeViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeView_Data viewType) && viewType != Outside.SpikeView_Input)
            {
                Outside.SpikeView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateIconView()
        {
            var selected = comboBoxMap["IconViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out ChartIconType viewType) && viewType != Outside.IconView_Input)
            {
                Outside.IconView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Bubbles Chart ====
        private void UpdateBubblesSize()
        {
            int value = int.TryParse(textInputMap["BubblesSizeKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.BubblesSizeMultiplier)
            {
                Outside.BubblesSizeMultiplier = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBubblesSource()
        {
            var selected = comboBoxMap["BubblesSourceKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesSource_Data sourceType) && sourceType != Outside.BubblesSource_Input)
            {
                Outside.BubblesSource_Input = sourceType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBubblesFilter()
        {
            var selected = comboBoxMap["BubblesFilterKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesFilter_Data filterType) && filterType != Outside.BubblesFilter_Input)
            {
                Outside.BubblesFilter_Input = filterType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBubblesColoring()
        {
            var selected = comboBoxMap["BubblesColoringKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesColoring_Data coloringType) && coloringType != Outside.BubblesColoring_Input)
            {
                Outside.BubblesColoring_Input = coloringType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBubblesMomentum()
        {
            var selected = comboBoxMap["BubblesMomentumKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesMomentumStrategy_Data strategyType) && strategyType != Outside.BubblesMomentumStrategy_Input)
            {
                Outside.BubblesMomentumStrategy_Input = strategyType;
                RecalculateOutsideWithMsg(false);
            }
        }

        private void RecalculateOutsideWithMsg(bool reset = true)
        {
            // Avoid multiples call when loading parameters from LocalStorage
            if (isLoadingParams)
                return;

            string current = ModeBtn.Text;
            ModeBtn.Text = $"{current}\nCalculating...";
            Outside.BeginInvokeOnMainThread(() => {
                try { _progressBar.IsIndeterminate = true; } catch { }
            });

            if (reset) {
                Outside.Chart.RemoveAllObjects();
                Outside.Chart.ResetBarColors();
            }

            Outside.BeginInvokeOnMainThread(() =>
            {
                Outside.ClearAndRecalculate(!reset);
                ModeBtn.Text = current;
            });

            // Slow down a bit, avoid crash.
            Thread.Sleep(200);

            Outside.BeginInvokeOnMainThread(() => {
                try { _progressBar.IsIndeterminate = false; } catch { }
            });

            // Update UI every OnChange()
            RefreshVisibility();
            // Highlight any modified/unsaved parameter
            // The reset of _originalValues only happens in Load/Save methods
            RefreshHighlighting();
        }

        private void NextModeEvent(ButtonClickEventArgs e)
        {
            Outside.VolumeMode_Input = Outside.VolumeMode_Input switch
            {
                VolumeMode_Data.Normal => VolumeMode_Data.Buy_Sell,
                VolumeMode_Data.Buy_Sell => VolumeMode_Data.Delta,
                _ => VolumeMode_Data.Normal
            };
            ModeBtn.Text = Outside.VolumeMode_Input.ToString();
            RefreshVisibility();
            RecalculateOutsideWithMsg();
        }

        private void PrevModeEvent(ButtonClickEventArgs e)
        {
            Outside.VolumeMode_Input = Outside.VolumeMode_Input switch
            {
                VolumeMode_Data.Delta => VolumeMode_Data.Buy_Sell,
                VolumeMode_Data.Buy_Sell => VolumeMode_Data.Normal,
                _ => VolumeMode_Data.Delta
            };
            ModeBtn.Text = Outside.VolumeMode_Input.ToString();
            RefreshVisibility();
            RecalculateOutsideWithMsg();
        }
        private void RefreshVisibility()
        {
            foreach (var param in _paramDefinitions)
            {
                bool isVisible = param.IsVisible();
                switch (param.InputType)
                {
                    case ParamInputType.Text:
                        textInputMap[param.Key].IsVisible = isVisible;
                        break;
                    case ParamInputType.ComboBox:
                        comboBoxMap[param.Key].IsVisible = isVisible;
                        comboBoxTextMap[param.Key].IsVisible = isVisible;
                        break;
                    case ParamInputType.Checkbox:
                        checkBoxMap[param.Key].IsVisible = isVisible;
                        checkBoxTextMap[param.Key].IsVisible = isVisible;
                        break;
                }
            }

            // Hide regions if all params are invisible
            foreach (var section in _regionSections.Values)
            {
                bool anyVisible = section.Params.Any(p =>
                {
                    return p.InputType switch
                    {
                        ParamInputType.Text => textInputMap[p.Key].IsVisible,
                        ParamInputType.ComboBox => comboBoxMap[p.Key].IsVisible || comboBoxTextMap[p.Key].IsVisible,
                        ParamInputType.Checkbox => checkBoxMap[p.Key].IsVisible || checkBoxTextMap[p.Key].IsVisible,
                        _ => false
                    };
                });

                section.SetVisible(anyVisible);
            }

            // Manually hidden Apply Button
            ApplyBtn.IsVisible = false;
        }

        private void RefreshHighlighting()
        {
            bool anyChange = false;
            foreach (var param in _paramDefinitions)
            {
                object currentValue = param.InputType switch
                {
                    ParamInputType.Text => (object)textInputMap[param.Key].Text,
                    ParamInputType.Checkbox => (object)(checkBoxMap[param.Key].IsChecked ?? false),
                    ParamInputType.ComboBox => (object)comboBoxMap[param.Key].SelectedItem,
                    _ => null
                };

                // Save original value if not already saved
                if (!_originalValues.ContainsKey(param.Key))
                    _originalValues[param.Key] = currentValue;

                bool isChanged = !Equals(currentValue, _originalValues[param.Key]);
                if (!anyChange && isChanged)
                    anyChange = isChanged;

                Color darkColorButton = Styles.ColorDarkTheme_PanelBorder;
                Color darkColor = Styles.ColorDarkTheme_Input;
                Color darkHover = Styles.ColorDarkTheme_ButtonHover;

                Color whiteColor = Styles.ColorLightTheme_Input;
                Color whiteHover = Styles.ColorLightTheme_InputHover;

                Color backgroundThemeColor = ApplicationTheme == ColorTheme.Dark ? darkColor : whiteColor;
                Color highlightThemeColor = ApplicationTheme == ColorTheme.Dark ? darkHover : whiteHover;

                SaveBtn.BackgroundColor = anyChange ? Color.FromHex("#D4D6262A") : (backgroundThemeColor == darkColor ? darkColorButton : whiteColor);
                FontStyle fontStyle = isChanged ? FontStyle.Oblique : FontStyle.Normal;

                switch (param.InputType)
                {
                    case ParamInputType.Text:
                        textInputMap[param.Key].BackgroundColor = isChanged ? highlightThemeColor : backgroundThemeColor;
                        break;
                    case ParamInputType.Checkbox:
                        checkBoxTextMap[param.Key].FontStyle = fontStyle;
                        break;
                    case ParamInputType.ComboBox:
                        comboBoxTextMap[param.Key].FontStyle = fontStyle;
                        comboBoxMap[param.Key].FontStyle = fontStyle;
                        break;
                }
            }
        }

        public class ParamStorage
        {
            public Dictionary<string, object> Values { get; set; } = new();
        }


        private async void AnimateProgressBar()
        {
            for (int i = 0; i <= 150; i += 25)
            {
                Outside.BeginInvokeOnMainThread(() => _progressBar.Value = i);
                await Task.Delay(100);
            }

            await Task.Delay(700);

            Outside.BeginInvokeOnMainThread(() => _progressBar.Value = 0);
        }

        private string GetStorageKey()
        {
            string SymbolPrefix = Outside.SymbolName;
            string BrokerPrefix = Outside.Account.BrokerName;
            string TimeframePrefix = Outside.TimeFrame.ShortName;

            BrokerPrefix = BrokerPrefix.ToLowerInvariant();
            SymbolPrefix = SymbolPrefix.ToUpperInvariant();

            bool selectbyBroker = Outside.StorageKeyConfig_Input == StorageKeyConfig_Data.Broker_Symbol_Timeframe;
            return selectbyBroker
                ? $"ODFT {BrokerPrefix} {SymbolPrefix} {TimeframePrefix}"
                : $"ODFT {SymbolPrefix} {TimeframePrefix}";
        }

        private class ParamStorageModel
        {
            public Dictionary<string, object> Params { get; set; } = new();
        }

        private void SaveParams()
        {
            var storageModel = new ParamStorageModel();

            foreach (var param in _paramDefinitions)
            {
                object value = param.InputType switch
                {
                    ParamInputType.Text => textInputMap[param.Key].Text,
                    ParamInputType.Checkbox => checkBoxMap[param.Key].IsChecked ?? false,
                    ParamInputType.ComboBox => comboBoxMap[param.Key].SelectedItem,
                    _ => null
                };

                if (value != null)
                    storageModel.Params[param.Key] = value;

                // Reset highlighting tracking
                _originalValues[param.Key] = value;
            }

            // Save current volume mode to start from there later.
            storageModel.Params["PanelMode"] = Outside.VolumeMode_Input;

            Outside.LocalStorage.SetObject(GetStorageKey(), storageModel, LocalStorageScope.Device);
            Outside.LocalStorage.Flush(LocalStorageScope.Device);

            // Use loaded params as _originalValues
            RefreshHighlighting();
            // Some fancy fake progress
            AnimateProgressBar();
        }

        private void LoadParams()
        {
            isLoadingParams = true;

            Outside.LocalStorage.Reload(LocalStorageScope.Device);
            var storageModel = Outside.LocalStorage.GetObject<ParamStorageModel>(GetStorageKey(), LocalStorageScope.Device);

            if (storageModel == null) {
                // Add keys and use default parameters as _originalValues;
                RefreshHighlighting();
                isLoadingParams = false;
                return;
            }

            foreach (var param in _paramDefinitions)
            {
                if (!storageModel.Params.TryGetValue(param.Key, out var storedValue))
                    continue;

                switch (param.InputType)
                {
                    case ParamInputType.Text:
                        textInputMap[param.Key].Text = storedValue.ToString();
                        param.OnChanged?.Invoke(param.Key);
                        break;
                    case ParamInputType.Checkbox:
                        if (storedValue is bool b)
                            checkBoxMap[param.Key].IsChecked = b;
                        param.OnChanged?.Invoke(param.Key);
                        break;
                    case ParamInputType.ComboBox:
                        if (comboBoxMap.ContainsKey(param.Key))
                            comboBoxMap[param.Key].SelectedItem = storedValue.ToString();
                        param.OnChanged?.Invoke(param.Key);
                        break;
                }

                // Reset highlighting tracking
                _originalValues[param.Key] = storedValue;
            }

            // Load the previously saved volume mode.
            string volModeText = storageModel.Params["PanelMode"].ToString();
            _ = Enum.TryParse(volModeText, out VolumeMode_Data volMode);
            Outside.VolumeMode_Input = volMode;
            ModeBtn.Text = volModeText;

            // Use loaded params as _originalValues
            RefreshHighlighting();

            isLoadingParams = false;
        }

        public class RegionSection
        {
            public string Name { get; }
            public StackPanel Container { get; }
            public ControlBase Header { get; }
            public List<ParamDefinition> Params { get; }

            private bool _isExpanded = false;

            public RegionSection(string name, IEnumerable<ParamDefinition> parameters)
            {
                Name = name;
                Params = parameters.ToList();

                Container = new StackPanel { Margin = "0 0 0 10" };

                // Only expand General region by default
                _isExpanded = name == "General";

                Header = CreateToggleHeader(name);
                Container.AddChild(Header);
            }

            private ControlBase CreateToggleHeader(string text)
            {
                var btn = new Button
                {
                    Text = (_isExpanded ? "▼ " : "► ") + text, // ▼ expanded / ► collapsed
                    Padding = 0,
                    Width = 200,
                    Height = 25,
                    Margin = "0 10 0 0",
                    Style = Styles.CreateButtonStyle(),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                btn.Click += _ => ToggleExpandCollapse(btn);
                return btn;
            }

            private void ToggleExpandCollapse(Button btn)
            {
                _isExpanded = !_isExpanded;
                btn.Text = (_isExpanded ? "▼ " : "► ") + Name;

                foreach (var child in Container.Children.Skip(1)) // skip header
                    child.IsVisible = _isExpanded;
            }

            public void AddParamControl(ControlBase control)
            {
                control.IsVisible = _isExpanded;
                Container.AddChild(control);
            }

            public void SetVisible(bool visible)
            {
                Container.IsVisible = visible;
            }
        }


        /*                              Old SaveParams, creates one file for every parameter
        private void SaveParams()
        {
            bool selectbyBroker = Outside.StorageKeyConfig_Input == StorageKeyConfig_Data.Broker_Symbol_Timeframe;
            string keyPrefix = selectbyBroker ? $"{BrokerPrefix} {SymbolPrefix} {TimeframePrefix}" : $"{SymbolPrefix} {TimeframePrefix}";

            foreach (var param in _paramDefinitions)
            {
                string key = $"{keyPrefix} param {param.Key}";

                object value = param.InputType switch
                {
                    ParamInputType.Text => textInputMap[param.Key].Text,
                    ParamInputType.Checkbox => checkBoxMap[param.Key].IsChecked ?? false,
                    ParamInputType.ComboBox => comboBoxMap[param.Key].SelectedItem,
                    _ => null
                };

                if (value != null)
                    Outside.LocalStorage.SetString(key, value.ToString(), LocalStorageScope.Device);

                // Reset original value tracking to current after save
                _originalValues[param.Key] = value;
            }

            Outside.LocalStorage.Flush(LocalStorageScope.Device);

            // Refresh highlighting to remove all highlights
            RefreshHighlighting();

            AnimateProgressBar();
        }

        private void LoadParams()
        {
            Outside.LocalStorage.Reload(LocalStorageScope.Device);

            bool selectbyBroker = Outside.StorageKeyConfig_Input == StorageKeyConfig_Data.Broker_Symbol_Timeframe;
            string keyPrefix = selectbyBroker ? $"{BrokerPrefix.ToLower()} {SymbolPrefix.ToLower()} {TimeframePrefix.ToLower()}" : $"{SymbolPrefix} {TimeframePrefix}";

            foreach (var param in _paramDefinitions)
            {
                string key = $"{keyPrefix} param {param.Key}";
                if (string.IsNullOrEmpty(Outside.LocalStorage.GetString(key)))
                    continue;

                string stored = Outside.LocalStorage.GetString(key, LocalStorageScope.Device);

                switch (param.InputType)
                {
                    case ParamInputType.Text:
                        textInputMap[param.Key].Text = stored;
                        param.OnChanged?.Invoke(param.Key);
                        break;
                    case ParamInputType.Checkbox:
                        if (bool.TryParse(stored, out var b))
                            checkBoxMap[param.Key].IsChecked = b;
                        param.OnChanged?.Invoke(param.Key);
                        break;
                    case ParamInputType.ComboBox:
                        if (comboBoxMap.ContainsKey(param.Key))
                            comboBoxMap[param.Key].SelectedItem = stored;
                        param.OnChanged?.Invoke(param.Key);
                        break;
                }
            }

            RefreshHighlighting();
        }
        */
    }

    // ============ THEME ============
    public static class Styles
    {
        public static readonly Color ColorDarkTheme_Panel = GetColorWithOpacity(Color.FromHex("#292929"), 0.85);
        public static readonly Color ColorLightTheme_Panel = GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.85);

        public static readonly Color ColorDarkTheme_PanelBorder = Color.FromHex("#3C3C3C");
        public static readonly Color ColorLightTheme_PanelBorder = Color.FromHex("#C3C3C3");

        public static readonly Color ColorDarkTheme_CommonBorder = GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.12);
        public static readonly Color ColorLightTheme_CommonBorder = GetColorWithOpacity(Color.FromHex("#000000"), 0.12);

        public static readonly Color ColorDarkTheme_Header = GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.70);
        public static readonly Color ColorLightTheme_Header = GetColorWithOpacity(Color.FromHex("#000000"), 0.65);

        public static readonly Color ColorDarkTheme_Input = Color.FromHex("#1A1A1A");
        public static readonly Color ColorDarkTheme_InputHover = Color.FromHex("#111111");
        public static readonly Color ColorLightTheme_Input = Color.FromHex("#E7EBED");
        public static readonly Color ColorLightTheme_InputHover = Color.FromHex("#D6DADC");

        public static readonly Color ColorDarkTheme_ButtonHover = Color.FromHex("#444444");

        public static Style CreatePanelBackgroundStyle()
        {
            Style style = new();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, ColorDarkTheme_Panel, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, ColorLightTheme_Panel, ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, ColorDarkTheme_PanelBorder, ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, ColorLightTheme_PanelBorder, ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }
        public static Style CreateButtonStyle()
        {
            Style style = new(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.CornerRadius, 3);

            style.Set(ControlProperty.BackgroundColor, ColorDarkTheme_PanelBorder, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, ColorDarkTheme_ButtonHover, ControlState.DarkTheme | ControlState.Hover);

            style.Set(ControlProperty.BackgroundColor, ColorLightTheme_Input, ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, ColorLightTheme_InputHover, ControlState.LightTheme | ControlState.Hover);

            style.Set(ControlProperty.BorderColor, ColorDarkTheme_PanelBorder, ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, ColorLightTheme_PanelBorder, ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }
        public static Style CreateCommonBorderStyle()
        {
            Style style = new();
            style.Set(ControlProperty.BorderColor, ColorDarkTheme_CommonBorder, ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, ColorLightTheme_CommonBorder, ControlState.LightTheme);
            return style;
        }
        public static Style CreateHeaderStyle()
        {
            Style style = new();
            style.Set(ControlProperty.ForegroundColor, ColorDarkTheme_Header, ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, ColorLightTheme_Header, ControlState.LightTheme);
            return style;
        }
        public static Style CreateInputStyle()
        {
            Style style = new(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, ColorDarkTheme_Input, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, ColorDarkTheme_InputHover, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, ColorLightTheme_Input, ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, ColorLightTheme_InputHover, ControlState.LightTheme | ControlState.Hover);
            return style;
        }
        public static Style CreateComboBoxStyle()
        {
            Style style = new(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, ColorDarkTheme_Input, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, ColorDarkTheme_InputHover, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, ColorLightTheme_Input, ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, ColorLightTheme_InputHover, ControlState.LightTheme | ControlState.Hover);
            return style;
        }
        public static Style CreateScrollViewerTransparentStyle()
        {
            var style = new Style();

            style.Set(ControlProperty.BackgroundColor, Color.Transparent, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.Transparent, ControlState.LightTheme);

            style.Set(ControlProperty.BorderColor, Color.Transparent, ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.Transparent, ControlState.LightTheme);

            style.Set(ControlProperty.BorderThickness, new Thickness(0));
            style.Set(ControlProperty.CornerRadius, 0);
            style.Set(ControlProperty.Padding, new Thickness(0));
            style.Set(ControlProperty.Margin, new Thickness(0));

            return style;
        }
        private static Color GetColorWithOpacity(Color baseColor, double opacity)
        {
            if (opacity < 0.0 || opacity > 1.0)
                throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0.0 and 1.0");

            byte alpha = (byte)Math.Round(255 * opacity, MidpointRounding.AwayFromZero);
            return Color.FromArgb(alpha, baseColor);
        }
    }

}