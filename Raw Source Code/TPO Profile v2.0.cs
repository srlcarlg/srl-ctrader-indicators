/*
--------------------------------------------------------------------------------------------------------------------------------
                        TPO Profile v2.0
                           revision 1
  It is VISUALLY BASED on the best TPO/Market Profile for MT4
(riv-ay-TPOChart.v102-06 and riv-ay-MarketProfileDWM.v131-2)

Preset Settings:
    Optimized for most assets (Currencies/Metals/Indices) focusing on Precision/Performance Balance,
    and of course it can't cover everything, but you can Customize if you need to.
TPO Divided into Colums
    Just like in the books.
Custom TPO Interval/rowHeight
    More accuracy at the cost of CPU load!

What's new in rev. 1? (after ODF_AGG)
- Rewritten using related improvements of ODF_AGG/Volume Profile.
- Concurrent Live TPO Update
- Show Any or All (Mini-VPs/Daily/Weekly/Monthly) Profiles at once!
- Fixed Range Profiles

Last update => 10/11/2025
===========================

Final revision (2025)

- Fix: Params Panel on MacOs
    - Supposedly cut short/half the size (Can't reproduce it through VM)
    - WrapPanel isn't fully supported (The button is hidden)
    - MissingMethodException on cAlgo.API.Panel.get_Children() (...)
        - At ToggleExpandCollapse event.

- Tested on MacOS (12 Monterey / 13 Ventura) without 3D accelerated graphics

===========================

AUTHOR: srlcarlg

== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using static cAlgo.TPOProfileV20;
using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class TPOProfileV20 : Indicator
    {
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
        [Parameter("Panel Position:", DefaultValue = PanelAlign_Data.Bottom_Left, Group = "==== TPO Profile v2.0 ====")]
        public PanelAlign_Data PanelAlign_Input { get; set; }

        public enum StorageKeyConfig_Data
        {
            Symbol_Timeframe,
            Broker_Symbol_Timeframe
        }
        [Parameter("Storage By:", DefaultValue = StorageKeyConfig_Data.Broker_Symbol_Timeframe, Group = "==== TPO Profile v2.0 ====")]
        public StorageKeyConfig_Data StorageKeyConfig_Input { get; set; }

        public enum RowConfig_Data
        {
            ATR,
            Custom,
        }
        [Parameter("Row Config:", DefaultValue = RowConfig_Data.ATR, Group = "==== TPO Profile v2.0 ====")]
        public RowConfig_Data RowConfig_Input { get; set; }

        [Parameter("Custom Row(pips):", DefaultValue = 0.2, MinValue = 0.2, Group = "==== TPO Profile v2.0 ====")]
        public double CustomHeightInPips { get; set; }


        [Parameter("ATR Period:", DefaultValue = 5, MinValue = 1, Group = "==== ATR Row Config ====")]
        public int ATRPeriod { get; set; }

        [Parameter("Row Detail(%):", DefaultValue = 70, MinValue = 1, MaxValue = 100, Group = "==== ATR Row Config ====")]
        public int RowDetailATR { get; set; }

        [Parameter("Replace Loaded Row?", DefaultValue = false, Group = "==== ATR Row Config ====")]
        public bool ReplaceByATR { get; set; }


        [Parameter("Show Controls at Zoom(%):", DefaultValue = 10, Group = "==== Fixed Range ====")]
        public int FixedHiddenZoom { get; set; }

        [Parameter("Show Info?", DefaultValue = true, Group = "==== Fixed Range ====")]
        public bool ShowFixedInfo { get; set; }

        [Parameter("Rectangle Color:", DefaultValue = "#6087CEEB", Group = "==== Fixed Range ====")]
        public Color FixedColor { get; set; }


        public enum UpdateStrategy_Data
        {
            Concurrent,
            SameThread_MayFreeze
        }
        [Parameter("[TPO] Update Strategy:", DefaultValue = UpdateStrategy_Data.Concurrent, Group = "==== Specific Parameters ====")]
        public UpdateStrategy_Data UpdateStrategy_Input { get; set; }


        [Parameter("Font Size Results:", DefaultValue = 10, MinValue = 1, MaxValue = 80, Group = "==== Results ====")]
        public int FontSizeResults { get; set; }

        [Parameter("Format Results?", DefaultValue = true, Group = "==== Results ====")]
        public bool FormatResults { get; set; }

        public enum FormatMaxDigits_Data
        {
            Zero,
            One,
            Two,
        }
        [Parameter("Format Max Digits:", DefaultValue = FormatMaxDigits_Data.One, Group = "==== Results ====")]
        public FormatMaxDigits_Data FormatMaxDigits_Input { get; set; }


        [Parameter("Histogram Color:", DefaultValue = "#6087CEEB", Group = "==== Colors ====")]
        public Color HistColor { get; set; }

        [Parameter("Weekly Color:", DefaultValue = "#B2FFD700", Group = "==== Colors ====")]
        public Color WeeklyColor { get; set; }

        [Parameter("Monthly Color:", DefaultValue = "#920071C1", Group = "==== Colors ====")]
        public Color MonthlyColor { get; set; }

        [Parameter("OHLC Bar Color:", DefaultValue = "Gray", Group = "==== Colors ====")]
        public Color ColorOHLC { get; set; }


        [Parameter("Color POC:", DefaultValue = "D0FFD700", Group = "==== Point of Control ====")]
        public Color ColorPOC { get; set; }

        [Parameter("LineStyle POC:", DefaultValue = LineStyle.Lines, Group = "==== Point of Control ====")]
        public LineStyle LineStylePOC { get; set; }

        [Parameter("Thickness POC:", DefaultValue = 2, MinValue = 1, MaxValue = 5, Group = "==== Point of Control ====")]
        public int ThicknessPOC { get; set; }


        [Parameter("Color VA:", DefaultValue = "#19F0F8FF",  Group = "==== Value Area ====")]
        public Color ColorVA { get; set; }

        [Parameter("Color VAH:", DefaultValue = "PowderBlue" , Group = "==== Value Area ====")]
        public Color ColorVAH { get; set; }

        [Parameter("Color VAL:", DefaultValue = "PowderBlue", Group = "==== Value Area ====")]
        public Color ColorVAL { get; set; }

        [Parameter("Opacity VA" , DefaultValue = 10, MinValue = 5, MaxValue = 100, Group = "==== Value Area ====")]
        public int OpacityVA { get; set; }

        [Parameter("LineStyle VA:", DefaultValue = LineStyle.LinesDots, Group = "==== Value Area ====")]
        public LineStyle LineStyleVA { get; set; }

        [Parameter("Thickness VA:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Value Area ====")]
        public int ThicknessVA { get; set; }


        [Parameter("Color Letters:", DefaultValue = "#8BE7E7E7" , Group = "==== Divided Mode ====")]
        public Color ColorLetters { get; set; }

        [Parameter("Close BarUP:", DefaultValue = "Green" , Group = "==== Divided Mode ====")]
        public Color ColorCandleUP { get; set; }

        [Parameter("Close BarDown:", DefaultValue = "Red", Group = "==== Divided Mode ====")]
        public Color ColorCandleDown { get; set; }


        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }
        [Parameter("Visually based in MT4", DefaultValue = "riv-ay-(TPOChart/MarketProfileDWM)", Group = "==== Credits ====")]
        public string Credits_2 { get; set; }

        // Moved from cTrader Input to Params Panel

        // ==== General ====
        public int Lookback = 1;
        public enum TPOMode_Data {
            Aggregated
        }
        public TPOMode_Data TPOMode_Input = TPOMode_Data.Aggregated;

        public enum TPOInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }
        public TPOInterval_Data TPOInterval_Input = TPOInterval_Data.Daily;


        // ==== TPO Profile ====
        public bool EnableTPO = false;

        public enum UpdateProfile_Data
        {
            EveryTick_CPU_Workout,
            ThroughSegments_Balanced,
            Through_2_Segments_Best,
        }
        public UpdateProfile_Data UpdateProfile_Input = UpdateProfile_Data.Through_2_Segments_Best;
        public bool FillHist_TPO = true;

        public enum HistSide_Data
        {
            Left,
            Right,
        }
        public HistSide_Data HistogramSide_Input = HistSide_Data.Left;

        public enum HistWidth_Data
        {
            _15,
            _30,
            _50,
            _70,
            _100
        }
        public HistWidth_Data HistogramWidth_Input = HistWidth_Data._70;

        public bool EnableWeeklyProfile = false;
        public bool EnableMonthlyProfile = false;

        public bool ShowOHLC = false;
        public bool EnableFixedRange = false;


        // ==== Intraday Profiles ====
        public bool ShowIntradayProfile = false;
        public int OffsetBarsInput = 2;
        public TimeFrame OffsetTimeframeInput = TimeFrame.Hour;
        public bool FillIntradaySpace { get; set; }


        // ==== Mini TPOs ====
        public bool EnableMiniProfiles = true;
        public TimeFrame MiniTPOs_Timeframe = TimeFrame.Hour4;
        public bool ShowMiniResults = true;


        // ==== VA + POC ====
        public bool ShowVA = false;
        public int PercentVA = 65;
        public bool KeepPOC = true;
        public bool ExtendPOC = false;
        public bool ExtendVA = false;
        public int ExtendCount = 1;

        // ==== Results ====
        public bool ShowResults = true;

        // Always Monthly
        public enum SegmentsInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }
        public SegmentsInterval_Data SegmentsInterval_Input = SegmentsInterval_Data.Monthly;

        // ======================================================

        public readonly string NOTIFY_CAPTION = "TPO Profile \n    v2.0";

        private readonly VerticalAlignment V_Align = VerticalAlignment.Top;
        private readonly HorizontalAlignment H_Align = HorizontalAlignment.Center;

        // Segments
        private class SegmentsExtremumInfo
        {
            public double LastHighest;
            public double LastLowest;
        }
        // intKey is the intervalIndex
        // value is the last updated Highest/Lowest
        private readonly IDictionary<int, SegmentsExtremumInfo> segmentInfo = new Dictionary<int, SegmentsExtremumInfo>();
        private readonly IDictionary<int, List<double>> segmentsDict = new Dictionary<int, List<double>>();
        private List<double> Segments = new();

        private IDictionary<double, double> TPO_Rank_Histogram = new Dictionary<double, double>();

        // Weekly, Monthly and Mini TPOs
        public class TPORankType
        {
            public IDictionary<double, double> TPO_Histogram { get; set; } = new Dictionary<double, double>();
        }
        private readonly TPORankType MonthlyRank = new();
        private readonly TPORankType WeeklyRank = new();
        private readonly TPORankType MiniRank = new();
        private readonly IDictionary<string, TPORankType> FixedRank = new Dictionary<string, TPORankType>();

        private Bars MiniTPOs_Bars;
        private Bars DailyBars;
        private Bars WeeklyBars;
        private Bars MonthlyBars;

        public enum ExtraProfiles {
            No,
            MiniTPO,
            Weekly,
            Monthly,
            Fixed,
        }

        // Its a annoying behavior that happens even in Candles Chart (Time-Based) on any symbol/broker.
        // where it's jump/pass +1 index when .GetIndexByTime is used... the exactly behavior of Price-Based Charts
        // Seems to happen only in Lower Timeframes (<=´Daily)
        // So, to ensure that it works flawless, an additional verification is needed.
        public class CleanedIndex {
            public int _TPO_Interval = 0;
            public int _Mini = 0;
        }
        private readonly CleanedIndex lastCleaned = new();

        // Concurrent Live TPO Update
        private readonly object _lockSource = new();
        private readonly object _lock = new();
        private readonly object _weeklyLock = new();
        private readonly object _monthlyLock = new();
        private readonly object _miniLock = new();

        private CancellationTokenSource cts;
        private Task liveTPO_Task;
        private Task weeklyTPO_Task;
        private Task monthlyTPO_Task;
        private Task miniTPO_Task;
        private bool liveTPO_UpdateIt = false;

        public class LiveTPOIndex {
            public int TPO_Interval { get; set; }
            public int Mini { get; set; }
            public int Weekly { get; set; }
            public int Monthly { get; set; }
        }
        private readonly LiveTPOIndex liveTPO_StartIndexes = new();
        private List<Bar> Bars_List = new();

        // Fixed Range Profile
        private readonly List<ChartRectangle> _rectangles = new();
        private readonly Dictionary<string, List<ChartText>> _infoObjects = new();
        private readonly Dictionary<string, Border> _controlGrids = new();

        // Shared rowHeight
        private double heightPips = 4;
        public double heightATR = 4;
        private double rowHeight = 0;

        // Some required utils
        private bool configHasChanged = false;
        public bool isPriceBased_Chart = false;
        public bool isRenkoChart = false;
        private bool isUpdateTPO = false;
        private double prevUpdatePrice;

        // Params Panel
        private Border ParamBorder;
        public class IndicatorParams
        {
            // ==== General ====
            public int LookBack { get; set; }
            public TPOMode_Data ModeTPO { get; set; }
            public double RowHeightInPips { get; set; }
            public TPOInterval_Data TPOInterval { get; set; }

            // ==== TPO Profile ====
            public bool EnableTPO { get; set; }
            public bool EnableWeeklyProfile { get; set; }
            public bool EnableMonthlyProfile { get; set; }

            // View
            public bool FillHist_TPO { get; set; }
            public HistSide_Data HistogramSide { get; set; }
            public HistWidth_Data HistogramWidth { get; set; }
            public bool Gradient { get; set; }
            public bool OHLC { get; set; }
            public bool FixedRange { get; set; }

            // Intraday View
            public bool ShowIntradayProfile { get; set; }
            public bool FillIntradaySpace { get; set; }
            public int OffsetBarsIntraday { get; set; }
            public TimeFrame OffsetTimeframeIntraday { get; set; }

            // ==== Mini TPOs ====
            public bool EnableMiniProfiles { get; set; }
            public TimeFrame MiniTPOsTimeframe { get; set; }
            public bool ShowMiniResults { get; set; }

            // ==== VA + POC ====
            public bool ShowVA { get; set; }
            public int PercentVA { get; set; }
            public bool KeepPOC { get; set; }
            public bool ExtendPOC { get; set; }
            public bool ExtendVA { get; set; }
            public int ExtendCount { get; set; }

            // ==== Misc ====
            public UpdateProfile_Data UpdateProfileStrategy { get; set; }
            public bool ShowResults { get; set; }
        }

        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "TPO",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = 0,
                Height = 22,
                Width = 35, // Fix MacOS => stretching button when StackPanel is used.
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
            if (RowConfig_Input == RowConfig_Data.ATR && (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Day3))
            {
                if (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Minute4)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute)
                        MiniTPOs_Timeframe = TimeFrame.Hour;
                    else if (Chart.TimeFrame == TimeFrame.Minute2)
                        MiniTPOs_Timeframe = TimeFrame.Hour2;
                    else if (Chart.TimeFrame <= TimeFrame.Minute4)
                        MiniTPOs_Timeframe = TimeFrame.Hour3;
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute5)
                        MiniTPOs_Timeframe = TimeFrame.Hour4;
                    else if (Chart.TimeFrame == TimeFrame.Minute6)
                        MiniTPOs_Timeframe = TimeFrame.Hour6;
                    else if (Chart.TimeFrame <= TimeFrame.Minute8)
                        MiniTPOs_Timeframe = TimeFrame.Hour8;
                    else if (Chart.TimeFrame <= TimeFrame.Minute10)
                        MiniTPOs_Timeframe = TimeFrame.Hour12;
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour)
                        MiniTPOs_Timeframe = TimeFrame.Daily;

                    else if (Chart.TimeFrame <= TimeFrame.Hour8) {
                        EnableTPO = true;
                        EnableMiniProfiles = false;
                        TPOInterval_Input = TPOInterval_Data.Weekly;
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Weekly) {
                    EnableTPO = true;
                    EnableMiniProfiles = false;
                    TPOInterval_Input = TPOInterval_Data.Monthly;
                }
            }

            if (RowConfig_Input == RowConfig_Data.Custom)
                heightPips = CustomHeightInPips;
            else {
                // Math Formulas by LLM
                // Manual coding with adaptations for cTrader Algo API.
                // The idea is => Set the rowHeight for any symbol with [1, 2, 5] digits with fewer hard-coded values.
                AverageTrueRange atr = Indicators.AverageTrueRange(ATRPeriod, MovingAverageType.Exponential);
                double atrInTick = atr.Result.LastValue / Symbol.TickSize;
                double priceInTick = Bars.LastBar.Close / Symbol.TickSize;

                // Original => (smaATRInTick * targetRows) / smaPriceInTick;
                // However, Initialize() already has a lot of heavy things to start (Tick / Filters / Panel),
                // Plus, the current approach is good enough and gives slightly/better higher numbers.
                double K_Factor = (atrInTick * RowDetailATR) / priceInTick;
                double rowSizeInTick = (atrInTick * atrInTick) / (K_Factor * priceInTick);

                // Original => Math.Max(1, Math.Round(rowSizeInTick, 2)) * (Symbol.TickSize / Symbol.PipSize)
                // Should 'never' go bellow 0.3 pips.
                double rowSizePips = Math.Max(0.3, Math.Round(rowSizeInTick, 2));
                heightPips = rowSizePips;
                heightATR = rowSizePips;
            }

            // Define rowHeight by Pips
            rowHeight = Symbol.PipSize * heightPips;

            // Load all at once, mostly due to:
            // Loading parameters that have it
            DailyBars = MarketData.GetBars(TimeFrame.Daily);
            WeeklyBars = MarketData.GetBars(TimeFrame.Weekly);
            MonthlyBars = MarketData.GetBars(TimeFrame.Monthly);
            MiniTPOs_Bars = MarketData.GetBars(MiniTPOs_Timeframe);

            // Chart
            string currentTimeframe = Chart.TimeFrame.ToString();
            isPriceBased_Chart = currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick");
            isRenkoChart = Chart.TimeFrame.ToString().Contains("Renko");

            DrawOnScreen("Calculating...");
            Second_DrawOnScreen($"Taking too long? You can: \n 1) Increase the rowHeight \n 2) Disable the Value Area (High Performance)");

            // Fixed Range Profiles
            RangeInitialize();

            // PARAMS PANEL
            VerticalAlignment vAlign = VerticalAlignment.Bottom;
            HorizontalAlignment hAlign = HorizontalAlignment.Right;

            switch (PanelAlign_Input)
            {
                case PanelAlign_Data.Bottom_Left:
                    hAlign = HorizontalAlignment.Left;
                    break;
                case PanelAlign_Data.Top_Left:
                    vAlign = VerticalAlignment.Top;
                    hAlign = HorizontalAlignment.Left;
                    break;
                case PanelAlign_Data.Top_Right:
                    vAlign = VerticalAlignment.Top;
                    hAlign = HorizontalAlignment.Right;
                    break;
                case PanelAlign_Data.Center_Right:
                    vAlign = VerticalAlignment.Center;
                    hAlign = HorizontalAlignment.Right;
                    break;
                case PanelAlign_Data.Center_Left:
                    vAlign = VerticalAlignment.Center;
                    hAlign = HorizontalAlignment.Left;
                    break;
                case PanelAlign_Data.Top_Center:
                    vAlign = VerticalAlignment.Top;
                    hAlign = HorizontalAlignment.Center;
                    break;
                case PanelAlign_Data.Bottom_Center:
                    vAlign = VerticalAlignment.Bottom;
                    hAlign = HorizontalAlignment.Center;
                    break;
            }

            IndicatorParams DefaultParams = new()
            {
                // General
                LookBack = Lookback,
                ModeTPO = TPOMode_Input,
                RowHeightInPips = heightPips,
                TPOInterval = TPOInterval_Input,

                // TPO Profile
                EnableTPO = EnableTPO,
                EnableWeeklyProfile = EnableWeeklyProfile,
                EnableMonthlyProfile = EnableMonthlyProfile,

                // View
                FillHist_TPO = FillHist_TPO,
                HistogramSide = HistogramSide_Input,
                HistogramWidth = HistogramWidth_Input,
                OHLC = ShowOHLC,
                FixedRange = EnableFixedRange,

                // Intraday View
                ShowIntradayProfile = ShowIntradayProfile,
                OffsetBarsIntraday = OffsetBarsInput,
                OffsetTimeframeIntraday = OffsetTimeframeInput,
                FillIntradaySpace = FillIntradaySpace,

                // Mini TPOs
                EnableMiniProfiles = EnableMiniProfiles,
                MiniTPOsTimeframe = MiniTPOs_Timeframe,
                ShowMiniResults = ShowMiniResults,

                // VA + POC
                ShowVA = ShowVA,
                PercentVA = PercentVA,
                KeepPOC = KeepPOC,
                ExtendPOC = ExtendPOC,
                ExtendVA = ExtendVA,
                ExtendCount = ExtendCount,

                // Misc
                UpdateProfileStrategy = UpdateProfile_Input,
                ShowResults = ShowResults,
            };

            ParamsPanel ParamPanel = new(this, DefaultParams);

            Border borderParam = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                // ParamsPanel - Lock Width
                Width = 262,
                Child = ParamPanel
            };
            Chart.AddControl(borderParam);
            ParamBorder = borderParam;

            StackPanel stackPanel = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
            };
            AddHiddenButton(stackPanel, Color.FromHex("#7F808080"));
            Chart.AddControl(stackPanel);
        }

        public override void Calculate(int index)
        {
            // Removing Messages
            if (!IsLastBar) {
                DrawOnScreen("");
                Second_DrawOnScreen("");
            }

            // Chart Segmentation
            CreateSegments(index);

            // WM
            if (EnableTPO && !IsLastBar) {
                CreateMonthlyTPO(index);
                CreateWeeklyTPO(index);
            }

            // LookBack
            Bars tpoBars = TPOInterval_Input == TPOInterval_Data.Daily ? DailyBars :
                           TPOInterval_Input == TPOInterval_Data.Weekly ? WeeklyBars : MonthlyBars;

            // Get Index of TPO Interval to continue only in Lookback
            int iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (tpoBars.ClosePrices.Count - iVerify > Lookback)
                return;

            int TF_idx = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int startIndex = Bars.OpenTimes.GetIndexByTime(tpoBars .OpenTimes[TF_idx]);

            // Clean Dicts
            if (index == startIndex ||
                (index - 1) == startIndex && isPriceBased_Chart ||
                (index - 1) == startIndex && (index - 1) != lastCleaned._TPO_Interval
            )
                CleanUp_MainTPO(index, startIndex);

            // Historical data
            if (!IsLastBar)
            {
                // Allows MiniTPOs if (!EnableTPO)
                CreateMiniTPOs(index);

                if (EnableTPO)
                    TPO_Profile(startIndex, index);

                isUpdateTPO = true; // chart end
            }
            else
            {
                if (UpdateStrategy_Input == UpdateStrategy_Data.SameThread_MayFreeze)
                {
                    if (EnableTPO)
                        LiveTPO_Update(startIndex, index);
                    else if (!EnableTPO && EnableMiniProfiles)
                        LiveTPO_Update(startIndex, index, true);
                }
                else
                    LiveTPO_Concurrent(index, startIndex);
            }
        }

        private void CleanUp_MainTPO(int index, int startIndex)
        {
            // Reset TPO
            // Segments are identified by TF_idx(start)
            // No need to clean up even if it's Daily Interval
            TPO_Rank_Histogram.Clear();
            lastCleaned._TPO_Interval = index == startIndex ? index : (index - 1);
        }

        // *********** INTERVAL SEGMENTS ***********
        /*
            In order to optimize Volume Profile and reduce CPU worload
            as well as create the possiblity to:
                - See Weekly and/or Monthly "Intraday" Profile
                - use Aligned Segments at Higher Timeframes (D1 to D3)
            Segments will be calculated outside VolumeProfile()
            and updated at new High/Low of its interval [D1, W1, M1]
        */
        private void CreateSegments(int index) {

            // ==== Highest and Lowest ====
            int TF_idx;
            double open, highest, lowest;

            switch (SegmentsInterval_Input)
            {
                case SegmentsInterval_Data.Weekly:
                    TF_idx = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

                    highest = WeeklyBars.HighPrices[TF_idx];
                    lowest = WeeklyBars.LowPrices[TF_idx];
                    open = WeeklyBars.OpenPrices[TF_idx];
                    break;
                case SegmentsInterval_Data.Monthly:
                    TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

                    highest = MonthlyBars.HighPrices[TF_idx];
                    lowest = MonthlyBars.LowPrices[TF_idx];
                    open = MonthlyBars.OpenPrices[TF_idx];
                    break;
                default:
                    TF_idx = DailyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

                    highest = DailyBars.HighPrices[TF_idx];
                    lowest = DailyBars.LowPrices[TF_idx];
                    open = DailyBars.OpenPrices[TF_idx];
                    break;
            }

            // Add indexKey if not present
            int startKey = TF_idx;
            if (!segmentInfo.ContainsKey(startKey)) {
                segmentInfo.Add(startKey, new SegmentsExtremumInfo {
                    LastHighest = highest,
                    LastLowest = lowest
                });
                updateSegments();
            }
            else {
                // Update the entirely Segments
                // when a new High/Low is made.
                if (segmentInfo[startKey].LastHighest != highest) {
                    updateSegments();
                    segmentInfo[startKey].LastHighest = highest;
                }

                if (segmentInfo[startKey].LastLowest != lowest) {
                    updateSegments();
                    segmentInfo[startKey].LastLowest = lowest;
                }

                if (!segmentsDict.ContainsKey(startKey))
                    segmentsDict.Add(startKey, Segments);
                else
                    segmentsDict[startKey] = Segments;
            }

            void updateSegments() {
                List<double> currentSegments = new();

                // ==== Chart Segmentation ====
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
            }
        }

        // *********** MWM PROFILES ***********
        private void CreateMiniTPOs(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (EnableMiniProfiles)
            {
                int miniIndex = MiniTPOs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int miniStart = Bars.OpenTimes.GetIndexByTime(MiniTPOs_Bars.OpenTimes[miniIndex]);

                if (index == miniStart ||
                    (index - 1) == miniStart && isPriceBased_Chart ||
                    (index - 1) == miniStart && (index - 1) != lastCleaned._Mini || loopStart
                ) {
                    MiniRank.TPO_Histogram.Clear();
                    lastCleaned._Mini = index == miniStart ? index : (index - 1);
                }
                if (!isConcurrent)
                    TPO_Profile(miniStart, index, ExtraProfiles.MiniTPO, isLoop);
                else
                {
                    miniTPO_Task ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.MiniTPO, cts.Token));

                    liveTPO_StartIndexes.Mini = miniStart;

                    if (index != miniStart) {
                        lock (_miniLock)
                            TPO_Profile(miniStart, index, ExtraProfiles.MiniTPO, false, true);
                    }
                }
            }
        }
        private void CreateWeeklyTPO(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (EnableTPO && EnableWeeklyProfile)
            {
                // Avoid recalculating the same period.
                if (TPOInterval_Input == TPOInterval_Data.Weekly)
                    return;

                int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                if (index == weekStart || (index - 1) == weekStart && isPriceBased_Chart || loopStart)
                    WeeklyRank.TPO_Histogram.Clear();

                if (!isConcurrent)
                    TPO_Profile(weekStart, index, ExtraProfiles.Weekly, isLoop);
                else
                {
                    weeklyTPO_Task ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.Weekly, cts.Token));

                    liveTPO_StartIndexes.Weekly = weekStart;

                    if (index != weekStart) {
                        lock (_weeklyLock)
                            TPO_Profile(weekStart, index, ExtraProfiles.Weekly, false, true);
                    }
                }
            }
        }
        private void CreateMonthlyTPO(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            // Avoid recalculating the same period.
            if (TPOInterval_Input == TPOInterval_Data.Monthly)
                return;

            if (EnableTPO && EnableMonthlyProfile)
            {
                int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                if (index == monthStart || (index - 1) == monthStart && isPriceBased_Chart || loopStart)
                    MonthlyRank.TPO_Histogram.Clear();

                if (!isConcurrent)
                    TPO_Profile(monthStart, index, ExtraProfiles.Monthly, isLoop);
                else
                {
                    monthlyTPO_Task ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.Monthly, cts.Token));

                    liveTPO_StartIndexes.Monthly = monthStart;

                    if (index != monthStart) {
                        lock (_monthlyLock)
                            TPO_Profile(monthStart, index, ExtraProfiles.Monthly, false, true);
                    }
                }
            }
        }

        // *********** TPO PROFILE ***********
        private void TPO_Profile(int iStart, int index,  ExtraProfiles extraProfiles = ExtraProfiles.No, bool isLoop = false, bool drawOnly = false, string fixedKey = "", double fixedLowest = 0)
        {
            // ==== TPO Column ====
            if (!drawOnly)
                TPO_Bars(index, extraProfiles, fixedKey);

            // ==== Drawing ====
            if (Segments.Count == 0 || isLoop)
                return;

            // For Results
            Bars mainTF = TPOInterval_Input == TPOInterval_Data.Daily ? DailyBars :
                           TPOInterval_Input == TPOInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            Bars TF_Bars = extraProfiles == ExtraProfiles.No ? mainTF:
                           extraProfiles == ExtraProfiles.MiniTPO ? MiniTPOs_Bars :
                           extraProfiles == ExtraProfiles.Weekly ? WeeklyBars : MonthlyBars; // Fixed should use Monthly Bars, so TF_idx can be used by "whichSegment" variable

            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            double lowest = TF_Bars.LowPrices[TF_idx];
            double highest = TF_Bars.HighPrices[TF_idx];

            // Mini TPOs avoid crash after recalculating
            if (double.IsNaN(lowest)) {
                lowest = TF_Bars.LowPrices.LastValue;
                highest = TF_Bars.HighPrices.LastValue;
            }

            bool gapWeekend = Bars.OpenTimes[iStart].DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart].Hour < 2;
            DateTime x1_Start = Bars.OpenTimes[iStart + (gapWeekend ? 1 : 0)];
            DateTime xBar = Bars.OpenTimes[index];

            bool isIntraday = ShowIntradayProfile && index == Chart.LastVisibleBarIndex && !isLoop;

            // Any Volume Mode
            double maxLength = xBar.Subtract(x1_Start).TotalMilliseconds;

            HistWidth_Data selectedWidth = HistogramWidth_Input;
            double maxWidth = selectedWidth == HistWidth_Data._15 ? 1.25 :
                              selectedWidth == HistWidth_Data._30 ? 1.50 :
                              selectedWidth == HistWidth_Data._50 ? 2 : 3;

            double proportion_TPO = maxLength - (maxLength / maxWidth);
            if (selectedWidth == HistWidth_Data._100)
                proportion_TPO = maxLength;

            string prefix = extraProfiles == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";
            double y1 = extraProfiles == ExtraProfiles.Fixed ? fixedLowest : lowest;

            List<double> whichSegment = extraProfiles == ExtraProfiles.Fixed ? segmentsDict[TF_idx] : Segments;

            for (int i = 0; i < whichSegment.Count; i++)
            {
                double priceKey = whichSegment[i];

                bool skip = extraProfiles switch
                {
                    ExtraProfiles.Monthly => !MonthlyRank.TPO_Histogram.ContainsKey(priceKey),
                    ExtraProfiles.Weekly => !WeeklyRank.TPO_Histogram.ContainsKey(priceKey),
                    ExtraProfiles.MiniTPO => !MiniRank.TPO_Histogram.ContainsKey(priceKey),
                    ExtraProfiles.Fixed => !FixedRank[fixedKey].TPO_Histogram.ContainsKey(priceKey),
                    _ => !TPO_Rank_Histogram.ContainsKey(priceKey),
                };
                if (skip)
                    continue;

                double lowerSegmentY1 = whichSegment[i] - rowHeight;
                double upperSegmentY2 = whichSegment[i];

                void DrawRectangle_Normal(double currentVolume, double maxVolume, bool intradayProfile = false)
                {
                    double proportion = currentVolume * proportion_TPO;
                    double dynLength = proportion / maxVolume;

                    DateTime x2 = x1_Start.AddMilliseconds(dynLength);

                    Color histogramColor = extraProfiles switch
                    {
                        ExtraProfiles.Monthly => MonthlyColor,
                        ExtraProfiles.Weekly => WeeklyColor,
                        _ => HistColor,
                    };

                    ChartRectangle volHist = Chart.DrawRectangle($"{prefix}_{i}_TPO_{extraProfiles}", x1_Start, lowerSegmentY1, x2, upperSegmentY2, histogramColor);

                    if (FillHist_TPO)
                        volHist.IsFilled = true;

                    if (HistogramSide_Input == HistSide_Data.Right)
                    {
                        volHist.Time1 = xBar;
                        volHist.Time2 = xBar.AddMilliseconds(-dynLength);
                    }

                    if (intradayProfile && extraProfiles != ExtraProfiles.MiniTPO)
                    {
                        DateTime dateOffset = TimeBasedOffset(xBar);
                        DateTime dateOffset_Duo = TimeBasedOffset(dateOffset, true);
                        DateTime dateOffset_Triple = TimeBasedOffset(dateOffset_Duo, true);

                        double maxLength_Intraday = dateOffset.Subtract(xBar).TotalMilliseconds;

                        if (extraProfiles == ExtraProfiles.Weekly)
                            maxLength_Intraday = dateOffset_Duo.Subtract(dateOffset).TotalMilliseconds;
                        if (extraProfiles == ExtraProfiles.Monthly)
                            maxLength_Intraday = dateOffset_Triple.Subtract(dateOffset_Duo).TotalMilliseconds;

                        // Recalculate histograms 'X' position
                        double proportion_Intraday = currentVolume * (maxLength_Intraday - (maxLength_Intraday / maxWidth));
                        if (selectedWidth == HistWidth_Data._100)
                            proportion_Intraday = currentVolume * maxLength_Intraday;

                        double dynLength_Intraday = proportion_Intraday / maxVolume;

                        // Set 'X'
                        volHist.Time1 = dateOffset;
                        volHist.Time2 = dateOffset.AddMilliseconds(-dynLength_Intraday);

                        if (extraProfiles == ExtraProfiles.Weekly)
                        {
                            volHist.Time1 = dateOffset_Duo;
                            volHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Intraday);
                            if (!EnableMonthlyProfile && FillIntradaySpace)
                            {
                                volHist.Time1 = dateOffset;
                                volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                            }
                        }
                        if (extraProfiles == ExtraProfiles.Monthly)
                        {
                            if (EnableWeeklyProfile) {
                                // Show after
                                volHist.Time1 = dateOffset_Triple;
                                volHist.Time2 = dateOffset_Triple.AddMilliseconds(-dynLength_Intraday);
                                // Show after together
                                if (FillIntradaySpace) {
                                    volHist.Time1 = dateOffset_Duo;
                                    volHist.Time2 = dateOffset_Duo.AddMilliseconds(dynLength_Intraday);
                                }
                            } else {
                                // Use Weekly position
                                volHist.Time1 = dateOffset_Duo;
                                volHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Intraday);
                                if (FillIntradaySpace) {
                                    volHist.Time1 = dateOffset;
                                    volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                                }
                            }
                        }

                        CalculateVA(true, volHist.Time1);
                    }
                }

                IDictionary<double, double> tpoDict = extraProfiles switch
                {
                    ExtraProfiles.Monthly => MonthlyRank.TPO_Histogram,
                    ExtraProfiles.Weekly => WeeklyRank.TPO_Histogram,
                    ExtraProfiles.MiniTPO => MiniRank.TPO_Histogram,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].TPO_Histogram,
                    _ => TPO_Rank_Histogram
                };

                bool intraBool = extraProfiles switch
                {
                    ExtraProfiles.Monthly => isIntraday,
                    ExtraProfiles.Weekly => isIntraday,
                    ExtraProfiles.MiniTPO => false,
                    ExtraProfiles.Fixed => false,
                    _ => isIntraday
                };

                double value = tpoDict[priceKey];
                double maxValue = tpoDict.Values.Max();

                // Draw VA/POC
                CalculateVA();

                // Draw histograms and update intraday VA/POC, if applicable
                DrawRectangle_Normal(value, maxValue, intraBool);

                if (ShowResults || ShowMiniResults)
                {
                    if (extraProfiles == ExtraProfiles.MiniTPO && !ShowMiniResults)
                        continue;
                    if (extraProfiles != ExtraProfiles.MiniTPO && !ShowResults)
                        continue;

                    double sum = Math.Round(tpoDict.Values.Sum());
                    string strValue = FormatResults ? FormatBigNumber(sum) : $"{sum}";

                    ChartText Center = Chart.DrawText($"{prefix}_TPO_{extraProfiles}_Result", $"\n{strValue}", x1_Start, y1, HistColor);
                    Center.HorizontalAlignment = HorizontalAlignment.Center;
                    Center.FontSize = FontSizeResults - 1;

                    if (HistogramSide_Input == HistSide_Data.Right)
                        Center.Time = xBar;

                    // Intraday Right Profile
                    if (isIntraday && extraProfiles == ExtraProfiles.No) {
                        DateTime dateOffset = TimeBasedOffset(xBar);
                        Center.Time = dateOffset;
                    }
                }
            }
            void CalculateVA(bool isIntraday = false, DateTime intraX1 = default) {
                IDictionary<double, double> TPOdict = extraProfiles switch
                {
                    ExtraProfiles.Monthly => MonthlyRank.TPO_Histogram,
                    ExtraProfiles.Weekly => WeeklyRank.TPO_Histogram,
                    ExtraProfiles.MiniTPO => MiniRank.TPO_Histogram,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].TPO_Histogram,
                    _ => TPO_Rank_Histogram
                };
                Draw_VA_POC(TPOdict, iStart, x1_Start, xBar, extraProfiles, isIntraday, intraX1, fixedKey);
            }

            if (!ShowOHLC || extraProfiles == ExtraProfiles.Fixed)
                return;

            DateTime OHLC_Date = TF_Bars.OpenTimes[TF_idx];

            ChartText iconOpenSession =  Chart.DrawText($"{OHLC_Date}_OHLC_Start", "▂", OHLC_Date, TF_Bars.OpenPrices[TF_idx], ColorOHLC);
            iconOpenSession.VerticalAlignment = VerticalAlignment.Center;
            iconOpenSession.HorizontalAlignment = HorizontalAlignment.Left;
            iconOpenSession.FontSize = 14;

            ChartText iconCloseSession =  Chart.DrawText($"{OHLC_Date}_OHLC_End", "▂", OHLC_Date, TF_Bars.ClosePrices[TF_idx], ColorOHLC);
            iconCloseSession.VerticalAlignment = VerticalAlignment.Center;
            iconCloseSession.HorizontalAlignment = HorizontalAlignment.Right;
            iconCloseSession.FontSize = 14;

            ChartTrendLine Session = Chart.DrawTrendLine($"{OHLC_Date}_OHLC_Body", OHLC_Date, lowest, OHLC_Date, highest, ColorOHLC);
            Session.Thickness = 3;
        }

        private void TPO_Bars(int index, ExtraProfiles extraTPO, string fixedKey)
        {
            double high = Bars.HighPrices[index];
            double low = Bars.LowPrices[index];

            int TF_idx = extraTPO == ExtraProfiles.Fixed ? MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]) : index;
            List<double> whichSegment = extraTPO == ExtraProfiles.Fixed ? segmentsDict[TF_idx] : Segments;

            int totalLetters = 0;
            for (int i = 0; i < whichSegment.Count; i++)
            {
                if (whichSegment[i] < high && whichSegment[i] > low)
                    totalLetters += 1;
            }

            double prev_segment = high;
            for (int i_count = 0; i_count <= totalLetters; i_count++)
            {
                Y_axis_Rank(prev_segment, extraTPO);
                prev_segment = Math.Abs(prev_segment - rowHeight);
            }

            void Y_axis_Rank(double barSegment, ExtraProfiles extraProfile)
            {
                double loop_segment = 0.0;
                for (int i = 0; i < whichSegment.Count; i++)
                {
                    if (loop_segment != 0 && barSegment >= loop_segment && barSegment <= whichSegment[i])
                    {
                        double priceKey = whichSegment[i];

                        if (extraProfile != ExtraProfiles.No)
                        {
                            TPORankType extraRank = extraProfile switch
                            {
                                ExtraProfiles.Monthly => MonthlyRank,
                                ExtraProfiles.Weekly => WeeklyRank,
                                ExtraProfiles.Fixed => FixedRank[fixedKey],
                                _ => MiniRank
                            };
                            UpdateExtraProfiles(extraRank, priceKey);
                            break;
                        }

                        if (TPO_Rank_Histogram.ContainsKey(priceKey))
                            TPO_Rank_Histogram[priceKey] += 1;
                        else
                            TPO_Rank_Histogram.Add(priceKey, 1);

                        break;
                    }
                    loop_segment = whichSegment[i];
                }
            }

            void UpdateExtraProfiles(TPORankType tpoRank, double priceKey) {
                if (tpoRank.TPO_Histogram.ContainsKey(priceKey))
                    tpoRank.TPO_Histogram[priceKey] += 1;
                else
                    tpoRank.TPO_Histogram.Add(priceKey, 1);
            }
        }

        // *********** LIVE PROFILE UPDATE ***********
        private void LiveTPO_Update(int startIndex, int index, bool onlyMini = false) {
            double price = Bars.ClosePrices[index];

            bool updateStrategy = UpdateProfile_Input == UpdateProfile_Data.ThroughSegments_Balanced ?
                                Math.Abs(price - prevUpdatePrice) >= rowHeight :
                                UpdateProfile_Input != UpdateProfile_Data.Through_2_Segments_Best ||
                                Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight);

            if (updateStrategy || isUpdateTPO || configHasChanged)
            {
                if (!onlyMini)
                {
                    if (EnableMonthlyProfile && TPOInterval_Input != TPOInterval_Data.Monthly)
                    {
                        int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                        if (index != monthStart)
                        {
                            bool loopStart = true;
                            for (int i = monthStart; i <= index; i++) {
                                if (i < index)
                                    CreateMonthlyTPO(i, loopStart, true); // Update only
                                else
                                    CreateMonthlyTPO(i, loopStart, false); // Update and Draw
                                loopStart = false;
                            }
                        }
                    }

                    if (EnableWeeklyProfile && TPOInterval_Input != TPOInterval_Data.Weekly)
                    {
                        int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                        if (index != weekStart)
                        {
                            bool loopStart = true;
                            for (int i = weekStart; i <= index; i++) {
                                if (i < index)
                                    CreateWeeklyTPO(i, loopStart, true); // Update only
                                else
                                    CreateWeeklyTPO(i, loopStart, false); // Update and Draw
                                loopStart = false;
                            }
                        }
                    }

                    if (EnableMiniProfiles)
                    {
                        int miniIndex = MiniTPOs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        int miniStart = Bars.OpenTimes.GetIndexByTime(MiniTPOs_Bars.OpenTimes[miniIndex]);

                        if (index != miniStart)
                        {
                            bool loopStart = true;
                            for (int i = miniStart; i <= index; i++)
                            {
                                if (i < index)
                                    CreateMiniTPOs(i, loopStart, true); // Update only
                                else
                                    CreateMiniTPOs(i, loopStart, false); // Update and Draw
                                loopStart = false;
                            }
                        }
                    }

                    if (index != startIndex)
                    {
                        for (int i = startIndex; i <= index; i++)
                        {
                            if (i == startIndex)
                                TPO_Rank_Histogram.Clear();

                            if (i < index)
                                TPO_Profile(startIndex, i, ExtraProfiles.No, true); // Update only
                            else
                                TPO_Profile(startIndex, i, ExtraProfiles.No, false); // Update and Draw
                        }
                    }
                }
                else
                {
                    int miniIndex = MiniTPOs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    int miniStart = Bars.OpenTimes.GetIndexByTime(MiniTPOs_Bars.OpenTimes[miniIndex]);

                    if (index != miniStart)
                    {
                        bool loopStart = true;
                        for (int i = miniStart; i <= index; i++)
                        {
                            if (i < index)
                                CreateMiniTPOs(i, loopStart, true); // Update only
                            else
                                CreateMiniTPOs(i, loopStart, false); // Update and Draw
                            loopStart = false;
                        }
                    }
                }
            }

            configHasChanged = false;
            isUpdateTPO = false;
            if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                prevUpdatePrice = price;
        }

        private void LiveTPO_Concurrent(int index, int startIndex)
        {
            if (!EnableTPO && !EnableMiniProfiles)
                return;

            double price = Bars.ClosePrices[index];
            bool updateStrategy = UpdateProfile_Input == UpdateProfile_Data.ThroughSegments_Balanced ?
                                Math.Abs(price - prevUpdatePrice) >= rowHeight :
                                UpdateProfile_Input != UpdateProfile_Data.Through_2_Segments_Best ||
                                Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight);
            if (updateStrategy || isUpdateTPO || configHasChanged)
            {
                lock (_lockSource)
                    Bars_List = Bars.ToList();

                liveTPO_UpdateIt = true;
            }
            cts ??= new CancellationTokenSource();

            CreateMonthlyTPO(index, isConcurrent: true);
            CreateWeeklyTPO(index, isConcurrent: true);
            CreateMiniTPOs(index, isConcurrent: true);

            if (EnableTPO)
            {
                liveTPO_Task ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.No, cts.Token));
                liveTPO_StartIndexes.TPO_Interval = startIndex;
                if (index != startIndex) {
                    lock (_lock)
                        TPO_Profile(startIndex, index, ExtraProfiles.No, false, true);
                }
            }
        }

        private void LiveTPO_Worker(ExtraProfiles extraID, CancellationToken token)
        {
            /*
            It's quite simple, but gave headaches mostly due to GetByInvoke() unexpected behavior and debugging it.
             - GetByInvoke() will slowdown loops due to accumulative Bars[index] => "0.xx ms" operations
            The major reason why Copy of Time/Bars are used.
            */
            IDictionary<double, double> Worker_TPO_Histogram = new Dictionary<double, double>();
            IEnumerable<Bar> BarsCopy = new List<Bar>();

            while (!token.IsCancellationRequested)
            {
                if (!liveTPO_UpdateIt) {
                    // Stop itself
                    if (extraID == ExtraProfiles.No && !EnableTPO) {
                        liveTPO_Task = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.MiniTPO && !EnableMiniProfiles) {
                        miniTPO_Task = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Weekly && !EnableTPO) {
                        weeklyTPO_Task = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Monthly && !EnableTPO) {
                        monthlyTPO_Task = null;
                        return;
                    }

                    Thread.Sleep(100);
                    continue;
                }

                try
                {
                    Worker_TPO_Histogram = new Dictionary<double, double>();

                    // Chart Bars
                    int startIndex = extraID == ExtraProfiles.No ? liveTPO_StartIndexes.TPO_Interval :
                                     extraID == ExtraProfiles.MiniTPO ? liveTPO_StartIndexes.Mini :
                                     extraID == ExtraProfiles.Weekly ? liveTPO_StartIndexes.Weekly : liveTPO_StartIndexes.Monthly;
                    DateTime lastBarTime = GetByInvoke(() => Bars.LastBar.OpenTime);

                    // Always replace
                    lock (_lockSource)
                        BarsCopy = Bars_List.Skip(startIndex);

                    int endIndex = BarsCopy.Count();

                    for (int i = 0; i < endIndex; i++)
                    {
                        Worker_TPO_Bars(i, extraID, i == (endIndex - 1));
                    }

                    object whichLock = extraID == ExtraProfiles.No ? _lock :
                                       extraID == ExtraProfiles.MiniTPO ? _miniLock :
                                       extraID == ExtraProfiles.Weekly ? _weeklyLock : _monthlyLock;
                    lock (whichLock) {
                        switch (extraID)
                        {
                            case ExtraProfiles.MiniTPO:
                                MiniRank.TPO_Histogram = Worker_TPO_Histogram; break;
                            case ExtraProfiles.Weekly:
                                WeeklyRank.TPO_Histogram = Worker_TPO_Histogram; break;
                            case ExtraProfiles.Monthly:
                                MonthlyRank.TPO_Histogram = Worker_TPO_Histogram; break;
                            default:
                                TPO_Rank_Histogram = Worker_TPO_Histogram; break;
                        }

                        configHasChanged = false;
                        isUpdateTPO = false;

                        if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                            prevUpdatePrice = BarsCopy.Last().Close;
                    }
                }
                catch (Exception e) { Print($"CRASH at LiveTPO_Worker => {extraID}: {e}"); }

                liveTPO_UpdateIt = false;
            }

            void Worker_TPO_Bars(int index, ExtraProfiles extraTPO = ExtraProfiles.No, bool isLastBarLoop = false)
            {
                double high = BarsCopy.ElementAt(index).High;
                double low = BarsCopy.ElementAt(index).Low;

                int totalLetters = 0;
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (Segments[i] < high && Segments[i] > low)
                        totalLetters += 1;
                }

                double prev_segment = high;
                for (int i_count = 0; i_count <= totalLetters; i_count++)
                {
                    Worker_Y_axis_Rank(prev_segment);
                    prev_segment = Math.Abs(prev_segment - rowHeight);
                }
            }

            void Worker_Y_axis_Rank(double barSegment)
            {
                double loop_segment = 0.0;
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (loop_segment != 0 && barSegment >= loop_segment && barSegment <= Segments[i])
                    {
                        double priceKey = Segments[i];

                        if (Worker_TPO_Histogram.ContainsKey(priceKey))
                            Worker_TPO_Histogram[priceKey] += 1;
                        else
                            Worker_TPO_Histogram.Add(priceKey, 1);

                        break;
                    }
                    loop_segment = Segments[i];
                }
            }
        }

        protected override void OnDestroy()
        {
            cts.Cancel();
            if (EnableFixedRange) {
                foreach (ChartRectangle item in _rectangles)
                    Chart.RemoveObject(item.Name);
            }
        }

        // Code generated by LLM.
        /*
            From my attempts, it should never be declared/invoked in the main thread,
                - ManualResetEventSlim(false) locks the indicator's Initialize, no matter the field or location it's on.

            The idea is "Get any cTrader's object by running BeginInvokeOnMainThread on it"
            The downside is calling it at every cTrader related objects (obviously) (Bars, Chart, etc..)

            A small price to pay to avoid freezes and lags.
        */
        public T GetByInvoke<T>(Func<T> func, string label = null)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            T result = default;
            var done = new ManualResetEventSlim(false);

            Stopwatch sw = null;
            if (!string.IsNullOrEmpty(label))
                sw = Stopwatch.StartNew();

            BeginInvokeOnMainThread(() =>
            {
                try {
                    result = func();
                }
                finally {
                    if (!string.IsNullOrEmpty(label)) {
                        sw.Stop();
                        Print($"[GetByInvoke] {label} took {sw.Elapsed.TotalMilliseconds:F2} ms");
                    }
                    done.Set();
                }
            });

            done.Wait(); // wait for main thread to finish
            return result;
        }


        // *********** FIXED RANGE PROFILE ***********
        // LLM code generating was used to quickly get the Drawings (Rectangles/Texts/ControlGrid) logic.

        void RangeInitialize()
        {
            Chart.ObjectsUpdated += OnObjectsUpdated;
            Chart.ZoomChanged += HiddenRangeControls;
        }

        private void OnObjectsUpdated(ChartObjectsEventArgs args)
        {
            if (!EnableFixedRange)
                return;

            foreach (var rect in _rectangles.ToArray())
            {
                if (rect == null) continue;

                if (rect.IsInteractive)
                    UpdateRectangle(rect);

                if (ShowFixedInfo)
                    UpdateInfoBox(rect);

                UpdateControlGrid(rect);
            }
        }
        private void HiddenRangeControls(ChartZoomEventArgs args)
        {
            foreach (var control in _controlGrids.Values)
                control.IsVisible = args.Chart.ZoomLevel >= FixedHiddenZoom;
        }

        public void CreateNewRange()
        {
            // Use Mini Interval as first X/Y axis
            DateTime lastBarDate = Bars.LastBar.OpenTime;
            int miniIndex = MiniTPOs_Bars.OpenTimes.GetIndexByTime(lastBarDate);
            int miniStart = Bars.OpenTimes.GetIndexByTime(MiniTPOs_Bars.OpenTimes[miniIndex]);

            string nameKey = $"FixedRange_{DateTime.UtcNow.Ticks}";
            ChartRectangle rect = Chart.DrawRectangle(
                nameKey,
                Bars.OpenTimes[miniStart],
                MiniTPOs_Bars.LowPrices[miniIndex],
                lastBarDate,
                MiniTPOs_Bars.HighPrices[miniIndex],
                FixedColor,
                2,
                LineStyle.Lines
            );

            rect.IsInteractive = true;
            _rectangles.Add(rect);

            FixedRank.Add(nameKey, new TPORankType());

            if (ShowFixedInfo)
                CreateInfoBox(rect);

            CreateControlGrid(rect);
        }

        private void CreateInfoBox(ChartRectangle rect)
        {
            string prefixName = $"{rect.Name}_InfoBox";

            List<ChartText> list = new();
            ChartText fromTxt = Chart.DrawText(prefixName + "_From", "", rect.Time1, rect.Y1, FixedColor);
            ChartText toTxt = Chart.DrawText(prefixName + "_To", "", rect.Time1, rect.Y1, FixedColor);
            ChartText spanTxt = Chart.DrawText(prefixName + "_Span", "", rect.Time1, rect.Y1, FixedColor);

            foreach (ChartText t in new[] { fromTxt, toTxt, spanTxt }) {
                t.FontSize = 11;
                t.VerticalAlignment = VerticalAlignment.Bottom;
                list.Add(t);
            }

            _infoObjects[rect.Name] = list;
            UpdateInfoBox(rect);
        }

        private void CreateControlGrid(ChartRectangle rect)
        {
            Grid grid = new(2, 1)
            {
                Style = Styles.CreateButtonStyle(),
                Margin = 0,
                Height = 75,
                Width = 25,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            CheckBox fixCheck = new()
            {
                IsChecked = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            fixCheck.Click += (s) =>
            {
                bool isFixed = (bool)fixCheck.IsChecked;
                rect.IsInteractive = !isFixed;
                rect.LineStyle = isFixed ? LineStyle.Solid : LineStyle.Lines;
            };

            Button delBtn = new()
            {
                Text = "🗑️",
                Width = 20,
                Height = 20,
                FontSize = 11,
                Padding = 0,
                Margin = "0 0 0 0",
                BackgroundColor = Color.Crimson,
                ForegroundColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            delBtn.Click += (_) => DeleteRectangle(rect);

            grid.AddChild(fixCheck, 0, 0);
            grid.AddChild(delBtn, 1, 0);

            Border border = new()
            {
                Child = grid
            };

            Chart.AddControl(border, rect.Time2, rect.Y2);
            _controlGrids[rect.Name] = border;
        }

        private void UpdateRectangle(ChartRectangle rect)
        {
            DateTime start = rect.Time1 < rect.Time2 ? rect.Time1 : rect.Time2;
            DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;

            int startIdx = Bars.OpenTimes.GetIndexByTime(start);
            int endIdx = Bars.OpenTimes.GetIndexByTime(end);
            if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx) return;

            double high = double.MinValue;
            double low = double.MaxValue;
            for (int i = startIdx; i <= endIdx; i++)
            {
                if (Bars.HighPrices[i] > high) high = Bars.HighPrices[i];
                if (Bars.LowPrices[i] < low) low = Bars.LowPrices[i];
            }

            rect.Y1 = high;
            rect.Y2 = low;
            rect.Time1 = Bars.OpenTimes[startIdx];
            rect.Time2 = Bars.OpenTimes[endIdx];

            // Update/Draw
            double bottomY = Math.Min(rect.Y1, rect.Y2);

            ResetFixedRange(rect.Name, end);

            for (int i = startIdx; i <= endIdx; i++)
                TPO_Profile(startIdx, i, ExtraProfiles.Fixed, fixedKey: rect.Name, fixedLowest: bottomY);
        }

        private void UpdateInfoBox(ChartRectangle rect)
        {
            if (!_infoObjects.TryGetValue(rect.Name, out var objs)) return;
            if (objs.Count < 3) return;

            ChartText fromTxt = objs[0];
            ChartText toTxt = objs[1];
            ChartText spanTxt = objs[2];

            DateTime start = rect.Time1 < rect.Time2 ? rect.Time1 : rect.Time2;
            DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
            TimeSpan interval = end.Subtract(start);
            double interval_ms = interval.TotalMilliseconds;

            // Dynamic TimeLapse Format
            string[] interval_timelapse = GetTimeLapse(interval_ms);
            string timelapse_Fmtd = interval_timelapse[0] + interval_timelapse[1];

            int startIdx = Bars.OpenTimes.GetIndexByTime(start);
            int endIdx = Bars.OpenTimes.GetIndexByTime(end);
            if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx) return;

            fromTxt.Text = $"{start:MM/dd HH:mm}";
            toTxt.Text = $"{end:MM/dd HH:mm}";
            spanTxt.Text = timelapse_Fmtd;

            double maxLength = end.Subtract(start).TotalMilliseconds;
            DateTime midTime = start.AddMilliseconds(maxLength / 2);
            double textY = Math.Max(rect.Y1, rect.Y2);

            fromTxt.Time = rect.Time1;
            fromTxt.Y = textY;

            spanTxt.Time = midTime;
            spanTxt.Y = textY;
            spanTxt.HorizontalAlignment = HorizontalAlignment.Center;

            toTxt.Time = rect.Time2;
            toTxt.Y = textY;
            toTxt.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private void UpdateControlGrid(ChartRectangle rect)
        {
            if (!_controlGrids.TryGetValue(rect.Name, out var grid)) return;
            double topY = Math.Max(rect.Y1, rect.Y2);
            DateTime rightTime = rect.Time1 > rect.Time2 ? rect.Time1 : rect.Time2;
            Chart.MoveControl(grid, rightTime, topY);
        }

        public void DeleteRectangle(ChartRectangle rect)
        {
            if (rect == null) return;
            Chart.RemoveObject(rect.Name);
            _rectangles.Remove(rect);

            // remove info objects
            if (_infoObjects.TryGetValue(rect.Name, out var objs))
            {
                foreach (var o in objs)
                    Chart.RemoveObject(o.Name);
                _infoObjects.Remove(rect.Name);
            }

            // remove control grid
            if (_controlGrids.TryGetValue(rect.Name, out var grid))
            {
                Chart.RemoveControl(grid);
                _controlGrids.Remove(rect.Name);
            }

            // remove histograms/lines drawings
            DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
            ResetFixedRange(rect.Name, end);
        }

        private void ResetFixedRange(string fixedKey, DateTime end)
        {
            FixedRank[fixedKey].TPO_Histogram.Clear();

            int endIdx = Bars.OpenTimes.GetIndexByTime(end);
            int TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[endIdx]); //Segments are always monthly

            for (int i = 0; i < segmentsDict[TF_idx].Count; i++)
                Chart.RemoveObject($"{fixedKey}_{i}_TPO_Fixed");

            string[] objsNames = new string[5] {
                $"{fixedKey}_TPO_Fixed_Result",

                $"{fixedKey}_POC_Fixed",
                $"{fixedKey}_VAH_Fixed",
                $"{fixedKey}_VAL_Fixed",
                $"{fixedKey}_RectVA_Fixed",
            };

            foreach (string name in objsNames)
                Chart.RemoveObject(name);
        }

        public void ResetFixedRange_Dicts() {
            _rectangles.Clear();
            _infoObjects.Clear();
            _controlGrids.Clear();
        }

        // ====== Functions Area ======
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

        private DateTime TimeBasedOffset(DateTime dateBar, bool isSubt = false) {
            // Offset by timebased timeframe (15m bar * nº bars of 15m)
            string[] timesBased = { "Minute", "Hour", "Daily", "Day", "Weekly", "Monthly" };
            string currentTimeframe = Chart.TimeFrame.ToString();

            // Required for Price-Based Charts for manual offset
            string tfName;
            if (timesBased.Any(currentTimeframe.Contains))
                tfName = Chart.TimeFrame.ShortName.ToString();
            else
                tfName = OffsetTimeframeInput.ShortName.ToString();

            // Get the time-based interval value
            string tfString = string.Join("", tfName.Where(char.IsDigit));
            int tfValue = int.TryParse(tfString, out int value) ? value : 1;

            DateTime dateToReturn = dateBar;
            int offsetCondiditon = !isSubt ? (OffsetBarsInput + 1) : Math.Max(2, OffsetBarsInput - 1);
            if (tfName.Contains('m'))
                dateToReturn = dateBar.AddMinutes(tfValue * offsetCondiditon);
            else if (tfName.Contains('h'))
                dateToReturn = dateBar.AddHours(tfValue * offsetCondiditon);
            else if (tfName.Contains('D'))
                dateToReturn = dateBar.AddDays(tfValue * offsetCondiditon);
            else if (tfName.Contains('W'))
                dateToReturn = dateBar.AddDays(7 * offsetCondiditon);
            else if (tfName.Contains("Month1"))
                dateToReturn = dateBar.AddMonths(tfValue * offsetCondiditon);

            return dateToReturn;
        }

        private static string[] GetTimeLapse(double interval_ms)
        {
            // Dynamic TimeLapse Format
            // from Weis & Wykoff System
            TimeSpan ts = TimeSpan.FromMilliseconds(interval_ms);

            string timelapse_Suffix = "";
            double timelapse_Value = 0;

            double[] dividedTimestamp = { ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds };
            for (int i = 0; i < dividedTimestamp.Length; i++)
            {
                if (dividedTimestamp[i] != 0)
                {
                    string suffix = i == 4 ? "ms" : i == 3 ? "s" : i == 2 ? "m" : i == 1 ? "h" : "d";

                    if (suffix == "ms")
                    {
                        timelapse_Value = ts.TotalMilliseconds;
                        timelapse_Suffix = suffix;
                    }
                    else if (suffix == "s")
                    {
                        timelapse_Value = ts.TotalSeconds;
                        timelapse_Suffix = suffix;
                    }
                    else if (suffix == "m")
                    {
                        timelapse_Value = ts.TotalMinutes;
                        timelapse_Suffix = suffix;
                    }
                    else if (suffix == "h")
                    {
                        timelapse_Value = ts.TotalHours;
                        timelapse_Suffix = suffix;
                    }
                    else if (suffix == "d")
                    {
                        timelapse_Value = ts.TotalDays;
                        timelapse_Suffix = suffix;
                    }
                    break;
                }
            }
            string[] interval_timelapse = { $"{Math.Round(timelapse_Value, 1)}", timelapse_Suffix };
            return interval_timelapse;
        }

        private void DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.LightBlue);
        }

        private void Second_DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt2", $"{Msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }

        // *********** VA + POC ***********
        private void Draw_VA_POC(IDictionary<double, double> tpoDict, int iStart, DateTime x1_Start, DateTime xBar, ExtraProfiles extraTPO = ExtraProfiles.No, bool isIntraday = false, DateTime intraX1 = default, string fixedKey = "")
        {
            string prefix = extraTPO == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";

            if (ShowVA) {
                double[] VAL_VAH_POC = VA_Calculation(tpoDict);

                if (!VAL_VAH_POC.Any())
                    return;

                ChartTrendLine poc = Chart.DrawTrendLine($"{prefix}_POC_{extraTPO}", x1_Start, VAL_VAH_POC[2] - rowHeight, xBar, VAL_VAH_POC[2] - rowHeight, ColorPOC);
                ChartTrendLine vah = Chart.DrawTrendLine($"{prefix}_VAH_{extraTPO}", x1_Start, VAL_VAH_POC[1] + rowHeight, xBar, VAL_VAH_POC[1] + rowHeight, ColorVAH);
                ChartTrendLine val = Chart.DrawTrendLine($"{prefix}_VAL_{extraTPO}", x1_Start, VAL_VAH_POC[0], xBar, VAL_VAH_POC[0], ColorVAL);

                poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                vah.LineStyle = LineStyleVA; vah.Thickness = ThicknessVA; vah.Comment = "VAH";
                val.LineStyle = LineStyleVA; val.Thickness = ThicknessVA; val.Comment = "VAL";

                ChartRectangle rectVA;
                rectVA = Chart.DrawRectangle($"{prefix}_RectVA_{extraTPO}", x1_Start, VAL_VAH_POC[0], xBar, VAL_VAH_POC[1] + rowHeight, ColorVA);
                rectVA.IsFilled = true;

                DateTime extDate = extraTPO == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
                if (ExtendVA) {
                    vah.Time2 = extDate;
                    val.Time2 = extDate;
                    rectVA.Time2 = extDate;
                }
                if (ExtendPOC)
                    poc.Time2 = extDate;

                if (isIntraday && extraTPO != ExtraProfiles.MiniTPO) {
                    poc.Time1 = intraX1;
                    vah.Time1 = intraX1;
                    val.Time1 = intraX1;
                    rectVA.Time1 = intraX1;
                }
            }
            else if (!ShowVA && KeepPOC)
            {
                double largestVOL = Math.Abs(tpoDict.Values.Max());

                double priceLVOL = 0;
                foreach (var kv in tpoDict)
                {
                    if (Math.Abs(kv.Value) == largestVOL) { priceLVOL = kv.Key; break; }
                }
                ChartTrendLine poc = Chart.DrawTrendLine($"{prefix}_POC_{extraTPO}", x1_Start, priceLVOL - rowHeight, xBar, priceLVOL - rowHeight, ColorPOC);
                poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";

                if (ExtendPOC)
                    poc.Time2 = extraTPO == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();

                if (isIntraday && extraTPO != ExtraProfiles.MiniTPO)
                    poc.Time1 = intraX1;
            }

            DateTime extendDate() {
                string tfName = extraTPO == ExtraProfiles.No ?
                (TPOInterval_Input == TPOInterval_Data.Daily ? "D1" :
                    TPOInterval_Input == TPOInterval_Data.Weekly ? "W1" : "Month1" ) :
                extraTPO == ExtraProfiles.MiniTPO ? MiniTPOs_Timeframe.ShortName.ToString() :
                extraTPO == ExtraProfiles.Weekly ?  "W1" :  "Month1";

                // Get the time-based interval value
                string tfString = string.Join("", tfName.Where(char.IsDigit));
                int tfValue = int.TryParse(tfString, out int value) ? value : 1;

                DateTime dateToReturn = xBar;
                if (tfName.Contains('m'))
                    dateToReturn = xBar.AddMinutes(tfValue * ExtendCount);
                else if (tfName.Contains('h'))
                    dateToReturn = xBar.AddHours(tfValue * ExtendCount);
                else if (tfName.Contains('D'))
                    dateToReturn = xBar.AddDays(tfValue * ExtendCount);
                else if (tfName.Contains('W'))
                    dateToReturn = xBar.AddDays(7 * ExtendCount);
                else if (tfName.Contains("Month1"))
                    dateToReturn = xBar.AddMonths(tfValue * ExtendCount);

                return dateToReturn;
            }
        }

        private double[] VA_Calculation(IDictionary<double, double> tpoDict)
        {
            /*
                https://onlinelibrary.wiley.com/doi/pdf/10.1002/9781118659724.app1
                https://www.mypivots.com/dictionary/definition/40/calculating-market-profile-value-area
                Visually based on riv_ay-TPOChart.v102-6 (MT4) and riv_ay-MarketProfileDWM.v131-2 (MT4) to see if it's right
            */

            if (tpoDict.Values.Count < 4)
                return Array.Empty<double>();

            double largestVOL = Math.Abs(tpoDict.Values.Max());
            double totalvol = Math.Abs(tpoDict.Values.Sum());
            double _70percent = Math.Round((PercentVA * totalvol) / 100);

            double priceLVOL = 0;
            foreach (var kv in tpoDict)
            {
                if (Math.Abs(kv.Value) == largestVOL) { priceLVOL = kv.Key; break; }
            }
            double priceVAH = 0;
            double priceVAL = 0;

            double sumVA = largestVOL;

            List<double> upKeys = new();
            List<double> downKeys = new();
            for (int i = 0; i < Segments.Count; i++)
            {
                double priceKey = Segments[i];

                if (tpoDict.ContainsKey(priceKey))
                {
                    if (priceKey < priceLVOL)
                        downKeys.Add(priceKey);
                    else if (priceKey > priceLVOL)
                        upKeys.Add(priceKey);
                }
            }

            double[] withoutVA = { priceLVOL - (rowHeight * 2), priceLVOL + (rowHeight / 2), priceLVOL };
            if (!upKeys.Any() || !downKeys.Any())
                return withoutVA;

            upKeys.Sort();
            if (upKeys.Count > 2)
                upKeys.Remove(upKeys.LastOrDefault());
            downKeys.Sort();
            downKeys.Reverse();

            double[] prev2UP = { 0, 0 };
            double[] prev2Down = { 0, 0 };

            bool lockAbove = false;
            double[] aboveKV = { 0, 0 };

            bool lockBelow = false;
            double[] belowKV = { 0, 0 };

            for (int i = 0; i < tpoDict.Keys.Count; i++)
            {
                if (sumVA >= _70percent)
                    break;

                double sumUp = 0;
                double sumDown = 0;

                // ==== Above of POC ====
                double prevUPkey = upKeys.First();
                double keyUP = 0;
                foreach (double key in upKeys)
                {
                    if (upKeys.Count == 1 || prev2UP[0] != 0 && prev2UP[1] != 0 && key == upKeys.Last())
                    {
                        sumDown = Math.Abs(tpoDict[key]);
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
                        double upVOL = Math.Abs(tpoDict[key]);
                        double up2VOL = Math.Abs(tpoDict[prevUPkey]);

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

                // ==== Below of POC ====
                double prevDownkey = downKeys.First();
                double keyDw = 0;
                foreach (double key in downKeys)
                {
                    if (downKeys.Count == 1 || prev2Down[0] != 0 && prev2Down[1] != 0 && key == downKeys.Last())
                    {
                        sumDown = Math.Abs(tpoDict[key]);
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
                        double downVOL = Math.Abs(tpoDict[key]);
                        double down2VOL = Math.Abs(tpoDict[prevDownkey]);

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

                // ==== VA rating ====
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

        private double[] VA_Calculation_Letter(IDictionary<double, string> tpoDict)
        {
            /*
                https://onlinelibrary.wiley.com/doi/pdf/10.1002/9781118659724.app1
                https://www.mypivots.com/dictionary/definition/40/calculating-market-profile-value-area
                Visually based on riv_ay-TPOChart.v102-6 (MT4) and riv_ay-MarketProfileDWM.v131-2 (MT4) to see if it's right
            */

            if (tpoDict.Values.Count < 4)
                return Array.Empty<double>();

            double largestVOL = Math.Abs(tpoDict.Values.MaxBy(x => x.Length).Length);
            double totalvol = Math.Abs(tpoDict.Values.Sum(x => x.Length));
            double _70percent = Math.Round((PercentVA * totalvol) / 100);

            double priceLVOL = 0;
            foreach (var kv in tpoDict)
            {
                if (Math.Abs(kv.Value.Length) == largestVOL) { priceLVOL = kv.Key; break; }
            }
            double priceVAH = 0;
            double priceVAL = 0;

            double sumVA = largestVOL;

            List<double> upKeys = new();
            List<double> downKeys = new();
            for (int i = 0; i < Segments.Count; i++)
            {
                double priceKey = Segments[i];

                if (tpoDict.ContainsKey(priceKey))
                {
                    if (priceKey < priceLVOL)
                        downKeys.Add(priceKey);
                    else if (priceKey > priceLVOL)
                        upKeys.Add(priceKey);
                }
            }

            double[] withoutVA = { priceLVOL - (rowHeight * 2), priceLVOL + (rowHeight / 2), priceLVOL };
            if (!upKeys.Any() || !downKeys.Any())
                return withoutVA;

            upKeys.Sort();
            if (upKeys.Count > 2)
                upKeys.Remove(upKeys.LastOrDefault());
            downKeys.Sort();
            downKeys.Reverse();

            double[] prev2UP = { 0, 0 };
            double[] prev2Down = { 0, 0 };

            bool lockAbove = false;
            double[] aboveKV = { 0, 0 };

            bool lockBelow = false;
            double[] belowKV = { 0, 0 };

            for (int i = 0; i < tpoDict.Keys.Count; i++)
            {
                if (sumVA >= _70percent)
                    break;

                double sumUp = 0;
                double sumDown = 0;

                // ==== Above of POC ====
                double prevUPkey = upKeys.First();
                double keyUP = 0;
                foreach (double key in upKeys)
                {
                    if (upKeys.Count == 1 || prev2UP[0] != 0 && prev2UP[1] != 0 && key == upKeys.Last())
                    {
                        sumDown = Math.Abs(tpoDict[key].Length);
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
                        double upVOL = Math.Abs(tpoDict[key].Length);
                        double up2VOL = Math.Abs(tpoDict[prevUPkey].Length);

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

                // ==== Below of POC ====
                double prevDownkey = downKeys.First();
                double keyDw = 0;
                foreach (double key in downKeys)
                {
                    if (downKeys.Count == 1 || prev2Down[0] != 0 && prev2Down[1] != 0 && key == downKeys.Last())
                    {
                        sumDown = Math.Abs(tpoDict[key].Length);
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
                        double downVOL = Math.Abs(tpoDict[key].Length);
                        double down2VOL = Math.Abs(tpoDict[prevDownkey].Length);

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

                // ==== VA rating ====
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

        // ========= ========== ==========

        public void ClearAndRecalculate()
        {
            Thread.Sleep(300);

            // LookBack from TPO
            Bars tpoBars = TPOInterval_Input == TPOInterval_Data.Daily ? DailyBars :
                           TPOInterval_Input == TPOInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            int firstIndex = Bars.OpenTimes.GetIndexByTime(tpoBars.OpenTimes.FirstOrDefault());

            // Get index of TPO Interval to continue only in Lookback
            int iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            while (tpoBars.ClosePrices.Count - iVerify > Lookback) {
                firstIndex++;
                iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            }

            // Daily or Weekly TPO
            int TF_idx = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            int startIndex = Bars.OpenTimes.GetIndexByTime(tpoBars.OpenTimes[TF_idx]);

            // Weekly Profile but Daily TPO
            bool extraWeekly = EnableTPO && EnableWeeklyProfile && TPOInterval_Input == TPOInterval_Data.Daily;
            if (extraWeekly) {
                TF_idx = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                startIndex = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[TF_idx]);
            }

            // Monthly Profile
            bool extraMonthly = EnableTPO && EnableMonthlyProfile;
            if (extraMonthly) {
                TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                startIndex = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[TF_idx]);
            }

            // Reset Segments
            Segments.Clear();
            segmentInfo.Clear();

            // Reset Fixed Range
            foreach (ChartRectangle rect in _rectangles)
            {
                DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
                ResetFixedRange(rect.Name, end);
            }

            // Historical data
            for (int index = startIndex; index < Bars.Count; index++)
            {
                CreateSegments(index);

                if (EnableTPO) {
                    CreateMonthlyTPO(index);
                    CreateWeeklyTPO(index);
                }

                // Calculate TPO only in lookback
                if (extraWeekly || extraMonthly) {
                    iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    if (tpoBars.ClosePrices.Count - iVerify > Lookback)
                        continue;
                }

                TF_idx = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                startIndex = Bars.OpenTimes.GetIndexByTime(tpoBars.OpenTimes[TF_idx]);

                if (index == startIndex ||
                   (index - 1) == startIndex && isPriceBased_Chart ||
                   (index - 1) == startIndex && (index - 1) != lastCleaned._TPO_Interval)
                    CleanUp_MainTPO(startIndex, index);

                CreateMiniTPOs(index);

                if (EnableTPO) TPO_Profile(startIndex, index);
            }

            configHasChanged = true;
        }

        public void SetRowHeight(double number)
        {
            rowHeight = number;
        }
        public void SetLookback(int number)
        {
            Lookback = number;
        }
        public int GetLookback()
        {
            return Lookback;
        }
        public double GetRowHeight()
        {
            return rowHeight;
        }

        public void SetMiniTPOsBars() {
            MiniTPOs_Bars = MarketData.GetBars(MiniTPOs_Timeframe);
        }
    }

    // ================ PARAMS PANEL ================

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
    public enum Supported_Timeframes {
        m5, m10, m15, m30, m45, h1, h2, h3, h4, h6, h8, h12, D1, D2, D3
    }
    public enum Supported_Sources {
        m1, m2, m3, m4, m5, m6, m7, m8, m9, m10, m15, m30, m45, h1, h2, h3, h4, h6, h8, h12, D1, D2, D3
    }

    public class ParamsPanel : CustomControl
    {
        private readonly TPOProfileV20 Outside;
        private readonly IndicatorParams FirstParams;
        private Button ModeBtn;
        private Button SaveBtn;
        private Button ApplyBtn;
        private Button RangeBtn;
        private ProgressBar _progressBar;
        private bool isLoadingParams;

        private readonly Dictionary<string, TextBox> textInputMap = new();
        private readonly Dictionary<string, TextBlock> textInputLabelMap = new();

        private readonly Dictionary<string, TextBlock> checkBoxTextMap = new();
        private readonly Dictionary<string, CheckBox> checkBoxMap = new();

        private readonly Dictionary<string, ComboBox> comboBoxMap = new();
        private readonly Dictionary<string, TextBlock> comboBoxTextMap = new();

        private readonly List<ParamDefinition> _paramDefinitions;
        private readonly Dictionary<string, RegionSection> _regionSections = new();
        private readonly Dictionary<string, object> _originalValues = new();
        private ColorTheme ApplicationTheme => Outside.Application.ColorTheme;

        public ParamsPanel(TPOProfileV20 indicator, IndicatorParams defaultParams)
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
                    Key = "LookbackKey",
                    Label = "Lookback",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.LookBack,
                    OnChanged = _ => UpdateLookback()
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
                    Key = "TPOIntervalKey",
                    Label = "Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.TPOInterval.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(TPOInterval_Data)),
                    OnChanged = _ => UpdateTPOInterval(),
                },

                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "EnableTPOKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableTPO,
                    OnChanged = _ => UpdateCheckbox("EnableTPOKey", val => Outside.EnableTPO = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "WeeklyTPOKey",
                    Label = "Weekly?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableWeeklyProfile,
                    OnChanged = _ => UpdateCheckbox("WeeklyTPOKey", val => Outside.EnableWeeklyProfile = val),
                    IsVisible = () => Outside.TPOInterval_Input != TPOInterval_Data.Weekly
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "MonthlyTPOKey",
                    Label = "Monthly?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableMonthlyProfile,
                    OnChanged = _ => UpdateCheckbox("MonthlyTPOKey", val => Outside.EnableMonthlyProfile = val),
                    IsVisible = () => Outside.TPOInterval_Input != TPOInterval_Data.Monthly
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "FillTPOKey",
                    Label = "Fill Histogram?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillHist_TPO,
                    OnChanged = _ => UpdateCheckbox("FillTPOKey", val => Outside.FillHist_TPO = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "SideTPOKey",
                    Label = "Side",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.HistogramSide.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(HistSide_Data)),
                    OnChanged = _ => UpdateSideTPO(),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "WidthTPOKey",
                    Label = "Width",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.HistogramWidth.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(HistWidth_Data)),
                    OnChanged = _ => UpdateWidthTPO(),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "IntradayTPOKey",
                    Label = "Intraday?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowIntradayProfile,
                    OnChanged = _ => UpdateCheckbox("IntradayTPOKey", val => Outside.ShowIntradayProfile = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "IntraOffsetKey",
                    Label = "Offset(bars)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.OffsetBarsIntraday.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateIntradayOffset(),
                    IsVisible = () => Outside.ShowIntradayProfile
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "IntraTFKey",
                    Label = "Offset(time)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.OffsetTimeframeIntraday.ShortName,
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateIntradayTimeframe(),
                    IsVisible = () => Outside.ShowIntradayProfile && Outside.isPriceBased_Chart
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "FixedRangeKey",
                    Label = "Fixed Range?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FixedRange,
                    OnChanged = _ => UpdateCheckbox("FixedRangeKey", val => Outside.EnableFixedRange = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "ShowOHLCKey",
                    Label = "OHLC Body?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.OHLC,
                    OnChanged = _ => UpdateCheckbox("ShowOHLCKey", val => Outside.ShowOHLC = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "FillIntraTPOKey",
                    Label = "Intra-Space?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillIntradaySpace,
                    OnChanged = _ => UpdateCheckbox("FillIntraTPOKey", val => Outside.FillIntradaySpace = val),
                    IsVisible = () => Outside.ShowIntradayProfile && (Outside.EnableWeeklyProfile || Outside.EnableMonthlyProfile)
                },

                new()
                {
                    Region = "Mini TPOs",
                    RegionOrder = 3,
                    Key = "MiniTPOsKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableMiniProfiles,
                    OnChanged = _ => UpdateCheckbox("MiniTPOsKey", val => Outside.EnableMiniProfiles = val)
                },
                new()
                {
                    Region = "Mini TPOs",
                    RegionOrder = 3,
                    Key = "MiniTFKey",
                    Label = "Mini-Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.MiniTPOsTimeframe.ShortName.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateMiniTPOTimeframe()
                },
                new()
                {
                    Region = "Mini TPOs",
                    RegionOrder = 3,
                    Key = "MiniResultKey",
                    Label = "Mini-Result?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowMiniResults,
                    OnChanged = _ => UpdateCheckbox("MiniResultKey", val => Outside.ShowMiniResults = val)
                },

                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "EnableVAKey",
                    Label = "Enable VA?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowVA,
                    OnChanged = _ => UpdateCheckbox("EnableVAKey", val => Outside.ShowVA = val)
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "VAValueKey",
                    Label = "VA(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.PercentVA.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdatePercentVA(),
                    IsVisible = () => Outside.ShowVA
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "OnlyPOCKey",
                    Label = "Only POC?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.KeepPOC,
                    OnChanged = _ => UpdateCheckbox("OnlyPOCKey", val => Outside.KeepPOC = val)
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "ExtendVAKey",
                    Label = "Extend VA?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ExtendVA,
                    OnChanged = _ => UpdateCheckbox("ExtendVAKey", val => Outside.ExtendVA = val)
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "ExtendCountKey",
                    Label = "Extend(count))",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.ExtendCount.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateExtendCount(),
                    IsVisible = () => Outside.ExtendVA || Outside.ExtendPOC
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "ExtendPOCKey",
                    Label = "Extend POC?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ExtendPOC,
                    OnChanged = _ => UpdateCheckbox("ExtendPOCKey", val => Outside.ExtendPOC = val)
                },

                new()
                {
                    Region = "Misc",
                    RegionOrder = 4,
                    Key = "UpdateTPOKey",
                    Label = "Update At",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UpdateProfileStrategy.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UpdateProfile_Data)),
                    OnChanged = _ => UpdateTPO(),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 4,
                    Key = "ShowResultsKey",
                    Label = "Results?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowResults,
                    OnChanged = _ => UpdateCheckbox("ShowResultsKey", val => Outside.ShowResults = val),
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
                Text = "TPO Profile",
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
                Width = 230, // ParamsPanel Width
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

            // Fix MacOS => small size button (save)
            footerGrid.Rows[0].SetHeightInPixels(35);

            var saveButton = CreateSaveButton();
            footerGrid.AddChild(saveButton, 0, 2);

            _progressBar = new ProgressBar {
                Height = 12,
                Margin = "0 2 0 0"
            };
            footerGrid.AddChild(_progressBar, 0, 0);

            footerGrid.AddChild(CreateApplyButton_TextInput(), 1, 0, 1, 3);
            footerGrid.AddChild(CreateFixedRangeButton(), 1, 0, 1, 3);

            return footerGrid;
        }

        private ScrollViewer CreateContentPanel()
        {
            var contentPanel = new StackPanel
            {
                Margin = 10,
                // Fix MacOS => large string increase column and hidden others
                Width = 230, // ParamsPanel Width
                // Fix MacOS(maybe) => panel is cut short/half the size
                VerticalAlignment = VerticalAlignment.Top,
            };

            // --- Mode controls at the top ---
            var grid = new Grid(2, 5);
            grid.Columns[1].SetWidthInPixels(5);
            grid.Columns[3].SetWidthInPixels(5);

            // Fix MacOS => small size button (modeinfo)
            grid.Rows[0].SetHeightInPixels(45);

            grid.AddChild(CreatePassButton("<"), 0, 0);
            grid.AddChild(CreateModeInfo_Button(FirstParams.ModeTPO.ToString()), 0, 1, 1, 3);
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
                var groupGrid = new Grid(6, 5);
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
        private void SetApplyVisibility() {
            ApplyBtn.IsVisible = true;
            RangeBtn.IsVisible = false;
        }
        private Button CreateFixedRangeButton()
        {
            Button button = new() {
                Text = "➕ Range",
                Padding = 0,
                Width = 50,
                Height = 20,
                Margin = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            button.Click += (_) => Outside.CreateNewRange();
            RangeBtn = button;
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

            var text = new TextBlock { Text = label, TextAlignment = TextAlignment.Center };
            textInputLabelMap[key] = text;

            stack.AddChild(text);
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
            switch (key) {
                case "IntradayTPOKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "FillIntraTPOKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "ExtendVAKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "ExtendPOCKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "FixedRangeKey":
                    RangeBtn.IsVisible = value;
                    return;
            }

            RecalculateOutsideWithMsg();
        }

        // ==== General ====
        private void UpdateLookback()
        {
            int value = int.TryParse(textInputMap["LookbackKey"].Text, out var n) ? n : -2;
            if (value >= -1 && value != Outside.GetLookback())
            {
                Outside.SetLookback(value);
                SetApplyVisibility();
            }
        }
        private void UpdateRowHeight()
        {
            if (double.TryParse(textInputMap["RowHeightKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.1)
            {
                double height = Outside.Symbol.PipSize * value;
                if (height != Outside.GetRowHeight())
                {
                    Outside.SetRowHeight(height);
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateTPOInterval()
        {
            var selected = comboBoxMap["TPOIntervalKey"].SelectedItem;
            if (Enum.TryParse(selected, out TPOInterval_Data intervalType) && intervalType != Outside.TPOInterval_Input)
            {
                Outside.TPOInterval_Input = intervalType;
                RecalculateOutsideWithMsg();
            }
        }

        // ==== TPO Profile ====
        private void UpdateSideTPO()
        {
            var selected = comboBoxMap["SideTPOKey"].SelectedItem;
            if (Enum.TryParse(selected, out HistSide_Data sideType) && sideType != Outside.HistogramSide_Input)
            {
                Outside.HistogramSide_Input = sideType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateWidthTPO()
        {
            var selected = comboBoxMap["WidthTPOKey"].SelectedItem;
            if (Enum.TryParse(selected, out HistWidth_Data widthType) && widthType != Outside.HistogramWidth_Input)
            {
                Outside.HistogramWidth_Input = widthType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateIntradayOffset()
        {
            int value = int.TryParse(textInputMap["IntraOffsetKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.OffsetBarsInput)
            {
                Outside.OffsetBarsInput = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateIntradayTimeframe()
        {
            var selected = comboBoxMap["IntraTFKey"].SelectedItem;
            TimeFrame value = StringToTimeframe(selected);
            if (value != TimeFrame.Minute && value != Outside.OffsetTimeframeInput)
            {
                Outside.OffsetTimeframeInput = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateMiniTPOTimeframe()
        {
            var selected = comboBoxMap["MiniTFKey"].SelectedItem;
            TimeFrame value = StringToTimeframe(selected);
            if (value != TimeFrame.Minute && value != Outside.MiniTPOs_Timeframe)
            {
                Outside.MiniTPOs_Timeframe = value;
                Outside.SetMiniTPOsBars();
                RecalculateOutsideWithMsg();
            }
        }
        private static TimeFrame StringToTimeframe(string inputTF)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                // Candles
                case "m1": return TimeFrame.Minute;
                case "m2": return TimeFrame.Minute2;
                case "m3": return TimeFrame.Minute3;
                case "m4": return TimeFrame.Minute4;
                case "m5": return TimeFrame.Minute5;
                case "m6": return TimeFrame.Minute6;
                case "m7": return TimeFrame.Minute7;
                case "m8": return TimeFrame.Minute8;
                case "m9": return TimeFrame.Minute9;
                case "m10": return TimeFrame.Minute10;
                case "m15": return TimeFrame.Minute15;
                case "m30": return TimeFrame.Minute30;
                case "m45": return TimeFrame.Minute45;
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

        // ==== POC + VA ====
        private void UpdatePercentVA()
        {
            int value = int.TryParse(textInputMap["VAValueKey"].Text, out var n) ? n : -1;
            if (value > 0 && value <= 100 && value != Outside.PercentVA)
            {
                Outside.PercentVA = value;
                SetApplyVisibility();
            }
        }
        private void UpdateExtendCount()
        {
            int value = int.TryParse(textInputMap["ExtendCountKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.ExtendCount)
            {
                Outside.ExtendCount = value;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Misc ====
        private void UpdateTPO()
        {
            var selected = comboBoxMap["UpdateTPOKey"].SelectedItem;
            if (Enum.TryParse(selected, out UpdateProfile_Data updateType) && updateType != Outside.UpdateProfile_Input)
            {
                Outside.UpdateProfile_Input = updateType;
                RecalculateOutsideWithMsg(false);
            }
        }

        private void RecalculateOutsideWithMsg(bool reset = true)
        {
            // Avoid multiples calls when loading parameters from LocalStorage
            if (isLoadingParams)
                return;

            string current = ModeBtn.Text;
            ModeBtn.Text = $"{current}\nCalculating...";
            Outside.BeginInvokeOnMainThread(() => {
                try { _progressBar.IsIndeterminate = true; } catch { }
            });

            if (reset) {
                Outside.BeginInvokeOnMainThread(() =>
                {
                    Outside.Chart.RemoveAllObjects();
                    Outside.ResetFixedRange_Dicts();
                });
            }

            Outside.BeginInvokeOnMainThread(() =>
            {
                Outside.ClearAndRecalculate();
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
            PopupNotification  cleaningProgress = Outside.Notifications.ShowPopup(
                Outside.NOTIFY_CAPTION,
                "Cleaning up the chart...",
                PopupNotificationState.InProgress
            );

            Outside.TPOMode_Input = Outside.TPOMode_Input switch
            {
                TPOMode_Data.Aggregated => TPOMode_Data.Aggregated,
                _ => TPOMode_Data.Aggregated
            };
            ModeBtn.Text = Outside.TPOMode_Input.ToString();
            RefreshVisibility();
            RecalculateOutsideWithMsg();

            cleaningProgress.Complete(PopupNotificationState.Success);
        }

        private void PrevModeEvent(ButtonClickEventArgs e)
        {
            PopupNotification  cleaningProgress = Outside.Notifications.ShowPopup(
                Outside.NOTIFY_CAPTION,
                "Cleaning up the chart...",
                PopupNotificationState.InProgress
            );

            Outside.TPOMode_Input = Outside.TPOMode_Input switch
            {
                TPOMode_Data.Aggregated => TPOMode_Data.Aggregated,
                _ => TPOMode_Data.Aggregated
            };
            ModeBtn.Text = Outside.TPOMode_Input.ToString();
            RefreshVisibility();
            RecalculateOutsideWithMsg();

            cleaningProgress.Complete(PopupNotificationState.Success);
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
                        textInputLabelMap[param.Key].IsVisible = isVisible;
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
                        ParamInputType.Text => textInputMap[p.Key].IsVisible || textInputLabelMap[p.Key].IsVisible,
                        ParamInputType.ComboBox => comboBoxMap[p.Key].IsVisible || comboBoxTextMap[p.Key].IsVisible,
                        ParamInputType.Checkbox => checkBoxMap[p.Key].IsVisible || checkBoxTextMap[p.Key].IsVisible,
                        _ => false
                    };
                });

                section.SetVisible(anyVisible);
            }

            // Manually hidden Apply Button
            ApplyBtn.IsVisible = false;
            RangeBtn.IsVisible = Outside.EnableFixedRange;
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
                ? $"TPO {BrokerPrefix} {SymbolPrefix} {TimeframePrefix}"
                : $"TPO {SymbolPrefix} {TimeframePrefix}";
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
            storageModel.Params["PanelMode"] = Outside.TPOMode_Input;

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
                        if (param.Key == "RowHeightKey") {
                            if (Outside.ReplaceByATR && Outside.RowConfig_Input == RowConfig_Data.ATR) {
                                textInputMap[param.Key].Text = Outside.heightATR.ToString();
                            }
                        }
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
            string tpoModeText = storageModel.Params["PanelMode"].ToString();
            _ = Enum.TryParse(tpoModeText, out TPOMode_Data tpoMode);
            Outside.TPOMode_Input = tpoMode;
            ModeBtn.Text = tpoModeText;

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

            // Fix MacOS => MissingMethodException <cAlgo.API.Panel.get_Children()>
            private readonly List<ControlBase> _panelChildren = new();

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
                    // Width = 200,
                    Width = 230, // ParamsPanel Width
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

                foreach (var child in _panelChildren)
                    child.IsVisible = _isExpanded;
            }

            public void AddParamControl(ControlBase control)
            {
                control.IsVisible = _isExpanded;
                Container.AddChild(control);
                _panelChildren.Add(control);
            }

            public void SetVisible(bool visible)
            {
                Container.IsVisible = visible;
            }
        }
    }
    // ========= THEME =========
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
