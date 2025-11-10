/*
--------------------------------------------------------------------------------------------------------------------------------
                        Order Flow Agreggated v2.0

From srl-python-indicators/notebooks/REAME.MD:
    Actually, it's a conjunction of Volume Profile (Ticks) + Order Flow Ticks indicators.
    - Volume Profile (intervals/values) for Aggregate Order Flow data.
    - Volume Profile (segmentation) to calculate the Order Flow of each bar.

    This 'combination' gives the quality that others footprint/order-flow software have:
    - Aligned Rows for all bars on the chart, or - in our case, at the given interval.
    - Possibility to create a truly (Volume, Delta) Bubbles chart.

    It's means that Order Flow Ticks is wrong / no longer useful? Absolutely not! Think about it:

    - With ODF_Ticks you get -> exactly <- what happened inside a bar, it's like looking at:
        a microstructure (ticks) through a microscope (bar segments) using optical zoom (bar).

    - With ODF_Aggregated you get a -> structured view <- of what happened inside the bars, it's like looking at:
        a microstructure (ticks) through a filter lens (VP segments) of a microscope (VP values) using digital zoom (VP interval).

    In other words:
    - Order Flow Ticks - raw detail.
    - Order Flow Aggregated - compressed detail.

===========================

Days since ODF_Ticks rev.1.5 => 18 Days

Days of fine-tuning as final step - 6 days
    - Price Based Charts (better support)
    - Concurrent Volume Profile (performance)
    - Custom MAs (performance)

New ODF_Ticks (not ODF_AGG exclusive) features after ODF_Ticks rev.1.5 (27/08/2025)
    - Perfomance Drawing - No more "Nº Bars to Show"!
    - High-performance VP_Tick()
    - Asynchronous Tick Data Collection
    - Bubbles Chart - Ultra Bubbles Levels
    - Tick Spike Filter - Spikes Levels
    - Subtract Delta
        - Large Filter
        - As source to Bubbles Chart
    - Custom MAs for performance.
    - Another OrderFlow() Loop refactor!

New "Free Volume Profile v2.0" features after rev.1.2 (12/08/2025), but developed only in ODF_Agg (xx/09/2025).
    - Concurrent Live VP Update
    - Daily/Monthly/Weekly Shared Segments
    - Mini-VPs that uses the current shared Segments.
    - Show Any or All (Mini-VPs/Daily/Weekly/Monthly) Profiles at once!

Fix => Custom MAs:
- Always coloring yellow bars
- EMA, KAMA, Wilder, VIDYA using wrongly previous values
- Replace "MA Period" checker => from index-based to avaiable values count.
Fix => Concurrent Live VP:
- Refactor duplicated code

===========================

Final revision (2025)

- (VP) Fixed Range Profiles
- (VP) Code optimization/readability, mostly switch expressions
- Fix: Params Panel on MacOs
    - Supposedly cut short/half the size (Can't reproduce it through VM)
    - WrapPanel isn't fully supported (The button is hidden)
    - MissingMethodException on cAlgo.API.Panel.get_Children() (...)
        - At ToggleExpandCollapse event.

- Tested on MacOS (12 Monterey / 13 Ventura) without 3D accelerated graphics

==========================

Why 'v2.0' suffix?
- Coming from 'v2.0 revision 1.x' indicators.
- It has Params Panel (main reason).

AUTHOR: srlcarlg

==========================

== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using static cAlgo.OrderFlowTicksV20;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace cAlgo
{
    // Keep the ODF_Ticks class name so that both versions can be interchangeable.
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
            At_Startup_Sync,
            On_ChartStart_Sync,
            On_ChartEnd_Async
        }
        [Parameter("Load Type:", DefaultValue = LoadTickStrategy_Data.On_ChartEnd_Async, Group = "==== Tick Volume Settings ====")]
        public LoadTickStrategy_Data LoadTickStrategy_Input { get; set; }

        [Parameter("Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== Tick Volume Settings ====")]
        public string StringDate { get; set; }

        public enum LoadTickNotify_Data
        {
            Minimal,
            Detailed,
        }
        [Parameter("Notifications Type:", DefaultValue = LoadTickNotify_Data.Minimal, Group = "==== Tick Volume Settings ====")]
        public LoadTickNotify_Data LoadTickNotify_Input { get; set; }
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
        [Parameter("Panel Position:", DefaultValue = PanelAlign_Data.Bottom_Left, Group = "==== Order Flow Aggregated v2.0 ====")]
        public PanelAlign_Data PanelAlign_Input { get; set; }

        public enum StorageKeyConfig_Data
        {
            Symbol_Timeframe,
            Broker_Symbol_Timeframe
        }
        [Parameter("Storage By:", DefaultValue = StorageKeyConfig_Data.Broker_Symbol_Timeframe, Group = "==== Order Flow Aggregated v2.0 ====")]
        public StorageKeyConfig_Data StorageKeyConfig_Input { get; set; }

        public enum RowConfig_Data
        {
            ATR,
            Custom,
        }
        [Parameter("Row Config:", DefaultValue = RowConfig_Data.ATR, Group = "==== Order Flow Aggregated v2.0 ====")]
        public RowConfig_Data RowConfig_Input { get; set; }

        [Parameter("Custom Row(pips):", DefaultValue = 0.2, MinValue = 0.2, Group = "==== Order Flow Aggregated v2.0 ====")]
        public double CustomHeightInPips { get; set; }


        [Parameter("ATR Period:", DefaultValue = 5, MinValue = 1, Group = "==== ATR Row Config ====")]
        public int ATRPeriod { get; set; }

        [Parameter("Row Detail(%):", DefaultValue = 70, MinValue = 20, MaxValue = 100, Group = "==== ATR Row Config ====")]
        public int RowDetailATR { get; set; }

        [Parameter("Replace Loaded Row?", DefaultValue = false, Group = "==== ATR Row Config ====")]
        public bool ReplaceByATR { get; set; }


        public enum DrawingStrategy_Data
        {
            Hidden_Slowest,
            Redraw_Fastest
        }
        [Parameter("Drawing Strategy", DefaultValue = DrawingStrategy_Data.Redraw_Fastest, Group = "==== Performance Drawing ====")]
        public DrawingStrategy_Data DrawingStrategy_Input { get; set; }

        [Parameter("[Debug] Show Count?:", DefaultValue = false , Group = "==== Performance Drawing ====")]
        public bool ShowDrawingInfo { get; set; }


        [Parameter("[Renko] Show Wicks?", DefaultValue = true, Group = "==== Specific Parameters ====")]
        public bool ShowWicks { get; set; }

        public enum UpdateVPStrategy_Data
        {
            Concurrent,
            SameThread_MayFreeze
        }
        [Parameter("[VP] Update Strategy", DefaultValue = UpdateVPStrategy_Data.Concurrent, Group = "==== Specific Parameters ====")]
        public UpdateVPStrategy_Data UpdateVPStrategy_Input { get; set; }

        [Parameter("[ODF] Use Custom MAs?", DefaultValue = true, Group = "==== Specific Parameters ====")]
        public bool UseCustomMAs { get; set; }


        [Parameter("Show Controls at Zoom(%):", DefaultValue = 10, Group = "==== Fixed Range ====")]
        public int FixedHiddenZoom { get; set; }

        [Parameter("Show Info?", DefaultValue = true, Group = "==== Fixed Range ====")]
        public bool ShowFixedInfo { get; set; }

        [Parameter("Rectangle Color:", DefaultValue = "#6087CEEB", Group = "==== Fixed Range ====")]
        public Color FixedColor { get; set; }


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


        [Parameter("Large R. Color", DefaultValue = "Gold", Group = "==== Large Result Filter ====")]
        public Color ColorLargeResult { get; set; }

        [Parameter("Coloring Bar?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool LargeFilter_ColoringBars { get; set; }

        [Parameter("[Delta] Coloring Cumulative?", DefaultValue = true, Group = "==== Large Result Filter ====")]
        public bool LargeFilter_ColoringCD { get; set; }


        [Parameter("[Debug] Show Strength Value?", DefaultValue = false, Group = "==== Tick Spike Filter ====")]
        public bool ShowTickStrengthValue { get; set; }

        [Parameter("[Levels] Show Touch Value?", DefaultValue = false, Group = "==== Tick Spike Filter ====")]
        public bool SpikeLevels_ShowValue { get; set; }


        [Parameter("Bubbles Chart Opacity(%):", DefaultValue = 40, MinValue = 1, MaxValue = 100, Group = "==== Spike HeatMap Coloring ====")]
        public int SpikeChart_Opacity { get; set; }

        [Parameter("Lowest < Max Threshold:", DefaultValue = 0.5, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeLowest_Value { get; set; }
        [Parameter("Lowest Color:", DefaultValue = "Aqua", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeLowest_Color { get; set; }

        [Parameter("Low:", DefaultValue = 1.2, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeLow_Value { get; set; }
        [Parameter("Low Color:", DefaultValue = "White", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeLow_Color { get; set; }

        [Parameter("Average < Max Threshold:", DefaultValue = 2.5, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeAverage_Value { get; set; }
        [Parameter("Average Color:", DefaultValue = "#DAFFFF00", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeAverage_Color { get; set; }

        [Parameter("High:", DefaultValue = 3.5, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeHigh_Value { get; set; }
        [Parameter("High Color:", DefaultValue = "#DAFFC000", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeHigh_Color { get; set; }

        [Parameter("Ultra >= Max Threshold:", DefaultValue = 3.51, MinValue = 0.01, Step = 0.01, Group = "==== Spike HeatMap Coloring ====")]
        public double SpikeUltra_Value { get; set; }
        [Parameter("Ultra Color:", DefaultValue = "#DAFF0000", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeUltra_Color { get; set; }


        [Parameter("[Debug] Show Strength Value?", DefaultValue = false, Group = "==== Bubbles Chart ====")]
        public bool ShowStrengthValue { get; set; }

        [Parameter("[Levels] Show Touch Value?", DefaultValue = false, Group = "==== Bubbles Chart ====")]
        public bool UltraBubbles_ShowValue { get; set; }


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
        [Parameter("High Color:", DefaultValue = "Goldenrod", Group = "==== Bubbles HeatMap Coloring ====")]
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


        [Parameter("Color Weekly:", DefaultValue = "#B2FFD700", Group = "==== WM Profiles ====")]
        public Color WeeklyColor { get; set; }

        [Parameter("Color Monthly:", DefaultValue = "#920071C1", Group = "==== WM Profiles ====")]
        public Color MonthlyColor { get; set; }


        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }

        // ========= Moved from cTrader Input to Params Panel =========
        public int Lookback = 1;


        // ==== General ====
        public enum VolumeMode_Data
        {
            Normal,
            Buy_Sell,
            Delta,
        }
        public VolumeMode_Data VolumeMode_Input = VolumeMode_Data.Delta;

        public enum VolumeView_Data
        {
            Divided,
            Profile,
        }
        public VolumeView_Data VolumeView_Input = VolumeView_Data.Profile;

        public bool ColoringOnlyLarguest = true;


        // ==== Volume Profile ====
        public bool EnableVP = false;
        public enum UpdateProfile_Data
        {
            EveryTick_CPU_Workout,
            ThroughSegments_Balanced,
            Through_2_Segments_Best,
        }
        public UpdateProfile_Data UpdateProfile_Input = UpdateProfile_Data.Through_2_Segments_Best;
        public bool FillHist_VP = false;
        public bool ShowHistoricalNumbers_VP = false;

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

        public bool EnableFixedRange = false;
        public bool EnableWeeklyProfile = false;
        public bool EnableMonthlyProfile = false;


        // ==== Intraday Profiles ====
        public bool ShowIntradayProfile = false;
        public bool ShowIntradayNumbers = false;
        public int OffsetBarsInput = 1;
        public TimeFrame OffsetTimeframeInput = TimeFrame.Hour;
        public bool FillIntradaySpace { get; set; }


        // ==== Mini VPs ====
        public bool EnableMiniProfiles = false;
        public TimeFrame MiniVPs_Timeframe = TimeFrame.Hour4;
        public bool ShowMiniResults = true;


        // ==== Results ====
        public bool ShowResults = true;
        public bool EnableLargeFilter = true;
        public MovingAverageType MAtype_Large = MovingAverageType.Exponential;
        public int MAperiod_Large = 5;
        public double LargeFilter_Ratio = 1.5;

        public enum ResultsView_Data
        {
            Percentage,
            Value,
            Both
        }
        public ResultsView_Data ResultsView_Input = ResultsView_Data.Percentage;

        public bool ShowSideTotal = true;

        public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }
        public OperatorBuySell_Data OperatorBuySell_Input = OperatorBuySell_Data.Subtraction;

        public bool ShowMinMaxDelta = false;
        public bool ShowOnlySubtDelta = true;


        // ==== Spike Filter ====
        public bool EnableSpikeFilter = true;
        public bool EnableSpikeNotification = true;
        public bool EnableSpikeChart = false;

        public enum SpikeView_Data
        {
            Bubbles,
            Icon,
        }
        public SpikeView_Data SpikeView_Input = SpikeView_Data.Icon;

        public ChartIconType IconView_Input = ChartIconType.Square;

        public enum NotificationType_Data
        {
            Popup,
            Sound,
            Both
        }
        public NotificationType_Data Spike_NotificationType_Input = NotificationType_Data.Both;

        public SoundType Spike_SoundType = SoundType.Confirmation;

        public enum SpikeChartColoring_Data
        {
            Heatmap,
            Positive_Negative,
            PlusMinus_Highlight_Heatmap,
        }
        public SpikeChartColoring_Data SpikeChartColoring_Input = SpikeChartColoring_Data.Heatmap;

        public enum SpikeFilter_Data
        {
            MA,
            Standard_Deviation,
        }
        public SpikeFilter_Data SpikeFilter_Input = SpikeFilter_Data.MA;

        public MovingAverageType MAtype_Spike = MovingAverageType.Simple;

        public int MAperiod_Spike = 20;


        // ==== Spike Levels ====
        public bool ShowSpikeLevels = false;
        public bool SpikeLevels_ResetDaily = true;
        public int SpikeLevels_MaxCount = 2;

        public enum SpikeLevelsColoring_Data
        {
            Heatmap,
            Positive_Negative
        }
        public SpikeLevelsColoring_Data SpikeLevelsColoring_Input = SpikeLevelsColoring_Data.Positive_Negative;


        // ==== Bubbles Chart ====
        public bool EnableBubblesChart = false;

        public double BubblesSizeMultiplier = 2;

        public enum BubblesSource_Data
        {
            Delta,
            Subtract_Delta,
            Cumulative_Delta_Change,
        }
        public BubblesSource_Data BubblesSource_Input = BubblesSource_Data.Delta;

        public enum BubblesFilter_Data
        {
            MA,
            Standard_Deviation,
            Both
        }
        public BubblesFilter_Data BubblesFilter_Input = BubblesFilter_Data.MA;
        public enum BubblesColoring_Data
        {
            Heatmap,
            Momentum,
        }
        public BubblesColoring_Data BubblesColoring_Input = BubblesColoring_Data.Heatmap;

        public enum BubblesMomentumStrategy_Data
        {
            Fading,
            Positive_Negative,
        }
        public BubblesMomentumStrategy_Data BubblesMomentumStrategy_Input = BubblesMomentumStrategy_Data.Fading;

        public MovingAverageType MAtype_Bubbles = MovingAverageType.Exponential;

        public int MAperiod_Bubbles = 20;


        // ==== Ultra Bubbles Levels ====
        public bool ShowUltraBubblesLevels = false;
        public bool EnableUltraBubblesNotification = true;

        public NotificationType_Data UltraBubbles_NotificationType_Input = NotificationType_Data.Both;
        public SoundType UltraBubbles_SoundType = SoundType.PositiveNotification;

        public bool UltraBubbles_ResetDaily = true;
        public int UltraBubbles_MaxCount = 5;

        public enum UltraBubbles_RectSizeData
        {
            High_Low,
            HighOrLow_Close,
            Bubble_Size,
        }
        public UltraBubbles_RectSizeData UltraBubbles_RectSizeInput = UltraBubbles_RectSizeData.Bubble_Size;

        public enum UltraBubblesBreak_Data
        {
            Close_Only,
            Close_plus_BarBody,
            OHLC_plus_BarBody,
        }
        public UltraBubblesBreak_Data UltraBubblesBreak_Input = UltraBubblesBreak_Data.Close_Only;

        public enum UltraBubblesColoring_Data
        {
            Bubble_Color,
            Positive_Negative
        }
        public UltraBubblesColoring_Data UltraBubblesColoring_Input = UltraBubblesColoring_Data.Positive_Negative;


        // ==== Misc ====
        public bool ShowHist = true;
        public bool FillHist = true;
        public bool ShowNumbers = true;
        public int DrawAtZoom_Value = 80;
        public enum SegmentsInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }
        public SegmentsInterval_Data SegmentsInterval_Input = SegmentsInterval_Data.Weekly;
        public enum ODFInterval_Data
        {
            Daily,
            Weekly,
        }
        public ODFInterval_Data ODFInterval_Input = ODFInterval_Data.Daily;
        public bool ShowBubbleValue = true;

        // ======================================================

        public readonly string NOTIFY_CAPTION = "Order Flow Ticks \n    Aɢɢʀᴇɢᴀᴛᴇᴅ";

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

        // Order Flow Ticks
        private List<double> Segments_Bar = new();
        private readonly IDictionary<double, int> VolumesRank = new Dictionary<double, int>();
        private readonly IDictionary<double, int> VolumesRank_Up = new Dictionary<double, int>();
        private readonly IDictionary<double, int> VolumesRank_Down = new Dictionary<double, int>();
        private readonly IDictionary<double, int> DeltaRank = new Dictionary<double, int>();
        private readonly IDictionary<double, int> TotalDeltaRank = new Dictionary<double, int>();
        private readonly IDictionary<double, int> SubtractDeltaRank = new Dictionary<double, int>();
        private int[] MinMaxDelta = { 0, 0 };

        // Volume Profile Ticks
        private List<double> Segments_VP = new();
        private IDictionary<double, double> VP_VolumesRank = new Dictionary<double, double>();
        private IDictionary<double, double> VP_VolumesRank_Up = new Dictionary<double, double>();
        private IDictionary<double, double> VP_VolumesRank_Down = new Dictionary<double, double>();
        private IDictionary<double, double> VP_VolumesRank_Subt = new Dictionary<double, double>();
        private IDictionary<double, double> VP_DeltaRank = new Dictionary<double, double>();
        private double[] VP_MinMaxDelta = { 0, 0 };

        // Weekly, Monthly and Mini VPs
        public class VolumeRankType
        {
            public IDictionary<double, double> Normal { get; set; } = new Dictionary<double, double>();
            public IDictionary<double, double> Up { get; set; } = new Dictionary<double, double>();
            public IDictionary<double, double> Down { get; set; } = new Dictionary<double, double>();
            public IDictionary<double, double> Delta { get; set;  } = new Dictionary<double, double>();
            public double[] MinMaxDelta { get; set; } = new double[2];
        }
        private readonly VolumeRankType MonthlyRank = new();
        private readonly VolumeRankType WeeklyRank = new();
        private readonly VolumeRankType MiniRank = new();
        private readonly IDictionary<string, VolumeRankType> FixedRank = new Dictionary<string, VolumeRankType>();

        private Bars MiniVPs_Bars;
        private Bars DailyBars;
        private Bars WeeklyBars;
        private Bars MonthlyBars;

        public enum ExtraProfiles {
            No,
            MiniVP,
            Weekly,
            Monthly,
            Fixed
        }

        // Its a annoying behavior that happens even in Candles Chart (Time-Based) on any symbol/broker.
        // where it's jump/pass +1 index when .GetIndexByTime is used... the exactly behavior of Price-Based Charts
        // Seems to happen only in Lower Timeframes (<=´Daily)
        // So, to ensure that it works flawless, an additional verification is needed.
        public class CleanedIndex {
            public int _ODF_Interval = 0;
            public int _Mini = 0;
        }
        private readonly CleanedIndex lastCleaned = new();

        // Concurrent Live VP Update
        private readonly object _lockBars = new();
        private readonly object _lockTick = new();
        private readonly object _lock = new();
        private readonly object _weeklyLock = new();
        private readonly object _monthlyLock = new();
        private readonly object _miniLock = new();

        private CancellationTokenSource cts;
        private Task liveVP_Task;
        private Task weeklyVP_Task;
        private Task monthlyVP_Task;
        private Task miniVP_Task;
        private bool liveVP_UpdateIt = false;

        public class LiveVPIndex {
            public int ODF { get; set; }
            public int Mini { get; set; }
            public int Weekly { get; set; }
            public int Monthly { get; set; }
        }
        private readonly LiveVPIndex liveVP_StartIndexes = new();

        private DateTime[] BarTimes_Array = Array.Empty<DateTime>();
        private IEnumerable<Bar> TickBars_List;

        // High-Performance VP_Tick()
        public class LastTickIndex {
            public int _Mini = 0;
            public int _MiniStart = 0;
            public int _Weekly = 0;
            public int _WeeklyStart = 0;
            public int _Monthly = 0;
            public int _MonthlyStart = 0;
        }
        private readonly LastTickIndex lastTick_ExtraVPs = new();

        private int lastTick_Bars = 0;
        private int lastTick_VPStart = 0;
        private int lastTick_VP = 0;
        private int lastTick_Wicks = 0;

        // Fixed Range Profile
        private readonly List<ChartRectangle> _rectangles = new();
        private readonly Dictionary<string, List<ChartText>> _infoObjects = new();
        private readonly Dictionary<string, Border> _controlGrids = new();

        // Shared rowHeight
        private double heightPips = 4;
        private double rowHeight = 0;
        public double heightATR = 4;

        // Tick Volume
        private DateTime firstTickTime;
        private DateTime fromDateTime;
        private Bars TicksOHLC;
        private ProgressBar syncTickProgressBar = null;
        PopupNotification asyncTickPopup = null;
        private bool loadingAsyncTicks = false;
        private bool loadingTicksComplete = false;

        // Some required utils
        private bool segmentsConflict = false;
        private bool configHasChanged = false;
        private bool isUpdateVP = false;
        public bool isPriceBased_Chart = false;
        private bool isPriceBased_NewBar = false;
        public bool isRenkoChart = false;
        private double prevUpdatePrice;

        // lock[...] mainly because of the Segments loop
        // Avoid Historical Data
        private bool lockTickNotify = true;
        private bool lockUltraNotify = true;
        private bool ultraNotify_NewBar = false;
        private bool lastIsUltra = false;
        // Allow Historical Data
        // Although it needs to be redefined to false before each OrderFlow() call in Historical Data.
        private bool lockUltraLevels = false;
        private bool lockSpikeLevels = false;

        // For [Ultra Bubbles, Spike] Levels
        private class RectInfo
        {
            public int LastBarIndex;
            public bool isActive;
            public ChartRectangle Rectangle;
            public ChartText Text;
            public int Touches;
            public double Y1;
            public double Y2;
        }
        private readonly IDictionary<double, RectInfo> ultraRectangles = new Dictionary<double, RectInfo>();
        private readonly IDictionary<string, RectInfo> spikeRectangles = new Dictionary<string, RectInfo>();

        // Filters
        /*
        CumulDeltaSeries is Cumulative Delta Change,
        - IT'S NOT CVD (Cumulative Volume Delta)
        DynamicSeries can be Normal, Buy_Sell or Delta Volume
        */
        private IndicatorDataSeries DynamicSeries, CumulDeltaSeries, SubtractDeltaSeries;
        private MovingAverage MABubbles, MABubbles_CumulDelta, MABubbles_SubtractDelta, MASpikeFilter, MADynamic_LargeFilter, MASubtract_LargeFilter;
        private StandardDeviation StdDevBubbles, StdDevBubbles_CumulDelta, StdDevBubbles_SubtractDelta, StdDevSpikeFilter;

        // Performance Drawing
        private class DrawInfo
        {
            public int BarIndex;
            public DrawType Type;
            public string Id;
            public DateTime X1;
            public double Y1;
            public DateTime X2;
            public double Y2;
            public string Text;
            public HorizontalAlignment horizontalAlignment;
            public VerticalAlignment verticalAlignment;
            public int FontSize;
            public ChartIconType IconType;
            public Color Color;
        }
        private enum DrawType
        {
            Text,
            Icon,
            Ellipse,
            Rectangle
        }

        /*
        Redraw should use another dict as value,
        to avoid creating previous Volume Modes objects
        or previous objects from Static Update.
        - intKey is the Bar index
        - stringKey is the DrawInfo.Id (object name)
        - DrawInfo is the current Bar object info.
        */
        private readonly Dictionary<int, IDictionary<string, DrawInfo>> redrawInfos = new();
        /*
         For real-time market:
        - intKey is always [0]
        - stringKey is the DrawInfo.Id (object name)
        - DrawInfo is the current Bar object info.
        */
        private readonly Dictionary<int, IDictionary<string, DrawInfo>> currentToRedraw = new();

        // It's fine to just keep the objects name as keys,
        // since hiddenInfos is populated/updated at each drawing.
        private readonly IDictionary<string, ChartObject> hiddenInfos = new Dictionary<string, ChartObject>();
        /*
        For real-time market:
        - intKey is always [0]
        - stringKey is the DrawInfo.Id (object name)
        - DrawInfo is the current Bar object.
        */
        private readonly Dictionary<int, IDictionary<string, ChartObject>> currentToHidden = new();
        private ChartStaticText _StaticText_DebugPerfDraw;

        // Timer
        private class TimerHandler {
            public bool isAsyncLoading = false;
        }
        private readonly TimerHandler timerHandler = new();

        // Custom MAs
        public enum MAType_Data
        {
            Simple,
            Exponential,
            Weighted,
            Triangular,
            Hull,
            VIDYA,
            WilderSmoothing,
            KaufmanAdaptive,
        }
        public MAType_Data customMAtype_Large = MAType_Data.Exponential;
        public MAType_Data customMAtype_Bubbles = MAType_Data.Exponential;
        public MAType_Data customMAtype_Spike = MAType_Data.Simple;

        private readonly Dictionary<int, double> _dynamicBuffer = new();
        private readonly Dictionary<int, double> _maDynamic = new();

        private enum DeltaSwitch {
            None,
            Subtract,
            CumulDelta,
            Spike
        }
        private class DeltaBuffer {
            public Dictionary<int, double> Subtract = new();
            public Dictionary<int, double> CumulDelta = new();

            public Dictionary<int, double> MASubtract_Large = new();
            public Dictionary<int, double> MASubtract_Bubbles = new();
            public Dictionary<int, double> MACumulDelta_Bubbles = new();
            public Dictionary<int, double> MASpike = new();
        }
        private readonly DeltaBuffer _deltaBuffer = new();
        private enum MASwitch {
            Large,
            Bubbles,
            Spike
        }

        // Params Panel
        private Border ParamBorder;

        public class IndicatorParams
        {
            // General
            public double N_Days { get; set; }
            public double RowHeightInPips { get; set; }
            public VolumeMode_Data VolMode { get; set; }
            public VolumeView_Data VolView { get; set; }
            public bool OnlyLargestDivided { get; set; }

            // Volume Profile
            public bool EnableVP { get; set; }
            public UpdateProfile_Data UpdateProfileStrategy { get; set; }
            public bool FillHist_VP { get; set; }
            public bool ShowHistoricalNumbers_VP { get; set; }
            public HistSide_Data HistogramSide { get; set; }
            public HistWidth_Data HistogramWidth { get; set; }
            // Intraday Profiles
            public bool ShowIntradayProfile { get; set; }
            public bool ShowIntradayNumbers { get; set; }
            public bool FillIntradaySpace { get; set; }
            public int OffsetBarsIntraday { get; set; }
            public TimeFrame OffsetTimeframeIntraday { get; set; }
            // Fixed/Weekly/Monthly
            public bool FixedRange { get; set; }
            public bool EnableWeeklyProfile { get; set; }
            public bool EnableMonthlyProfile { get; set; }
            // Mini VPs
            public bool EnableMiniProfiles { get; set; }
            public TimeFrame MiniVPsTimeframe { get; set; }
            public bool ShowMiniResults { get; set; }

            // Results
            public bool ShowResults { get; set; }
            // Results - Buy_Sell / Delta
            public bool ShowSideTotal { get; set; }
            public ResultsView_Data ResultView { get; set; }
            public OperatorBuySell_Data OperatorBuySell { get; set; }
            // Results - Delta
            public bool ShowMinMax { get; set; }
            public bool ShowOnlySubtDelta { get; set; }
            // Result - Large Filter
            public bool EnableLargeFilter { get; set; }
            public MovingAverageType MAtype_Large { get; set; }
            public int MAperiod_Large { get; set; }
            public double LargeFilter_Ratio { get; set; }

            // Spike Filter
            public bool EnableSpike { get; set; }
            // Spike Filter - Filter Settings
            public SpikeFilter_Data SpikeFilter { get; set; }
            public MovingAverageType MAtype_Spike { get; set; }
            public int MAperiod_Spike { get; set; } = 20;
            public bool EnableSpikeNotify { get; set; }
            // Spike Filter - Notifications
            public NotificationType_Data Spike_NotificationType { get; set; }
            public SoundType Spike_SoundType { get; set; }
            // Spike Filter - View
            public SpikeView_Data SpikeView { get; set; }
            public ChartIconType IconView { get; set; }
            // Spike Filter - Bubbles Chart
            public bool EnableSpikeChart { get; set; }
            public SpikeChartColoring_Data SpikeChartColoring { get; set; }
            // Spike Filter - Levels
            public bool ShowSpikeLevels { get; set; }
            public int SpikeLevels_MaxCount { get; set; }
            public bool SpikeLevels_ResetDaily { get; set; }
            public SpikeLevelsColoring_Data SpikeLevelsColoring { get; set; }

            // Bubbles Chart
            public bool EnableBubbles { get; set; }
            public double BubblesSize { get; set; }
            public BubblesSource_Data BubblesSource { get; set; }
            public BubblesFilter_Data BubblesFilter { get; set; }
            public MovingAverageType MAtype_Bubbles { get; set; }
            public int MAperiod_Bubbles { get; set; }
            public BubblesColoring_Data BubblesColoring { get; set; }
            public BubblesMomentumStrategy_Data BubblesMomentumStrategy { get; set; }
            // Bubbles Chart - Ultra Notifications
            public bool EnableUltraBubblesNotifiy { get; set; }
            public NotificationType_Data UltraBubbles_NotifyType { get; set; }
            public SoundType UltraBubbles_SoundType { get; set; }

            // Bubbles Chart - Ultra Levels
            public bool ShowUltraBubblesLevels { get; set; }
            public int UltraBubbles_MaxCount { get; set; }
            public bool UltraBubbles_ResetDaily { get; set; }
            public UltraBubbles_RectSizeData UltraBubbles_RectSize{ get; set; }
            public UltraBubblesBreak_Data UltraBubblesBreak { get; set; }
            public UltraBubblesColoring_Data UltraBubblesColoring { get; set; }

            // Misc
            public bool ShowHist { get; set; }
            public bool FillHist { get; set; }
            public bool ShowNumbers { get; set; }
            public int DrawAtZoom_Value { get; set; }
            public SegmentsInterval_Data SegmentsInterval { get; set; }
            public ODFInterval_Data VPInterval { get; set; }
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
                double atrInTick = atr.Result.LastValue / Symbol.TickSize;
                double priceInTick = Bars.LastBar.Close / Symbol.TickSize;

                // Original => (smaATRInTick * targetRows) / smaPriceInTick;
                // However, Initialize() already has a lot of heavy things to start (Tick / Filters / Panel),
                // Plus, the current approach is good enough and gives slightly/better higher numbers.
                double K_Factor = (atrInTick * RowDetailATR) / priceInTick;
                double rowSizeInTick = (atrInTick * atrInTick) / (K_Factor * priceInTick);

                // Original => Math.Max(1, Math.Round(rowSizeInTick, 2)) * (Symbol.TickSize / Symbol.PipSize)
                // Should 'never' go bellow 0.2 pips.
                double rowSizePips = Math.Max(0.2, Math.Round(rowSizeInTick, 2));
                heightPips = rowSizePips;
                heightATR = rowSizePips;
            }

            // Define rowHeight by Pips
            rowHeight = Symbol.PipSize * heightPips;

            // Filters
            DynamicSeries = CreateDataSeries();
            SubtractDeltaSeries = CreateDataSeries();
            CumulDeltaSeries = CreateDataSeries();

            if (!UseCustomMAs) {
                MADynamic_LargeFilter = Indicators.MovingAverage(DynamicSeries, MAperiod_Large, MAtype_Large);
                MASubtract_LargeFilter = Indicators.MovingAverage(SubtractDeltaSeries, MAperiod_Large, MAtype_Large);

                MABubbles_SubtractDelta = Indicators.MovingAverage(SubtractDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);
                StdDevBubbles_SubtractDelta = Indicators.StandardDeviation(SubtractDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);

                MABubbles_CumulDelta = Indicators.MovingAverage(CumulDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);
                StdDevBubbles_CumulDelta = Indicators.StandardDeviation(CumulDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);

                MABubbles = Indicators.MovingAverage(DynamicSeries, MAperiod_Bubbles, MAtype_Bubbles);
                StdDevBubbles = Indicators.StandardDeviation(DynamicSeries, MAperiod_Bubbles, MAtype_Bubbles);

                MASpikeFilter = Indicators.MovingAverage(DynamicSeries, MAperiod_Spike, MAtype_Spike);
                StdDevSpikeFilter = Indicators.StandardDeviation(DynamicSeries, MAperiod_Spike, MAtype_Spike);
            }
            // First Ticks Data
            TicksOHLC = MarketData.GetBars(TimeFrame.Tick);

            // Load all at once, mostly due to:
            // Loading parameters that have it
            DailyBars = MarketData.GetBars(TimeFrame.Daily);
            WeeklyBars = MarketData.GetBars(TimeFrame.Weekly);
            MonthlyBars = MarketData.GetBars(TimeFrame.Monthly);
            MiniVPs_Bars = MarketData.GetBars(MiniVPs_Timeframe);

            if (LoadTickStrategy_Input != LoadTickStrategy_Data.At_Startup_Sync)
            {
                if (LoadTickStrategy_Input == LoadTickStrategy_Data.On_ChartStart_Sync) {
                    StackPanel panel = new() {
                        Width = 200,
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    syncTickProgressBar = new ProgressBar { IsIndeterminate = true, Height = 12 };
                    panel.AddChild(syncTickProgressBar);
                    Chart.AddControl(panel);
                } else
                    Timer.Start(TimeSpan.FromSeconds(0.5));

                VolumeInitialize(true);
            }
            else
                VolumeInitialize();

            DrawOnScreen("Loading Ticks Data... \n or \n Calculating...");

            string[] timesBased = { "Minute", "Hour", "Daily", "Day" };
            string ticksInfo = $"Keep in mind: \n 1) Tick data are stored in RAM \n 2) 'Lower Timeframe' with 'Small Row Size' \n   - Takes longer to calculate/draw the entirely data";
            Second_DrawOnScreen($"Taking too long? You can: \n 1) Increase the Row Size \n 2) Disable Volume Profile (High Performance) \n\n {ticksInfo}");

            // Design
            Chart.ChartType = ChartType.Hlc;

            // Performance Drawing
            Chart.ZoomChanged += PerformanceDrawing;
            Chart.ScrollChanged += PerformanceDrawing;
            Bars.BarOpened += LiveDrawing;

            // Required to recalculate the histograms in Live Market
            string currentTimeframe = Chart.TimeFrame.ToString();
            isPriceBased_Chart = currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick");
            if (isPriceBased_Chart) {
                Bars.BarOpened += (_) =>
                {
                    isPriceBased_NewBar = true;
                    // Even with any additional recalculation here,
                    // when running on Backtest, any drawing that uses avoidStretching remains the same as in live market
                    // works as expected in live market though
                };
            }
            isRenkoChart = currentTimeframe.Contains("Renko");

            // Spike Filter + Ultra Bubbles + Spike Levels
            Bars.BarOpened += (_) =>
            {
                lockTickNotify = false;
                lockUltraNotify = false;
                ultraNotify_NewBar = true;
                lockUltraLevels = false;
                lockSpikeLevels = false;
                isUpdateVP = true;
                if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                    prevUpdatePrice = _.Bars.LastBar.Close;
                try { PerformanceDrawing(true); } catch { } // Draw without scroll or zoom
            };

            // Fixed Range Profiles
            RangeInitialize();

            // Params Panel
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

            IndicatorParams DefaultParams = new() {
                // General
                N_Days = Lookback,
                RowHeightInPips = heightPips,
                VolMode = VolumeMode_Input,
                VolView = VolumeView_Input,
                OnlyLargestDivided = ColoringOnlyLarguest,

                // Volume Profile
                EnableVP = EnableVP,
                UpdateProfileStrategy = UpdateProfile_Input,
                FillHist_VP = FillHist_VP,
                HistogramSide = HistogramSide_Input,
                HistogramWidth = HistogramWidth_Input,
                ShowHistoricalNumbers_VP = ShowHistoricalNumbers_VP,

                // Intraday Profiles
                ShowIntradayProfile = ShowIntradayProfile,
                OffsetBarsIntraday = OffsetBarsInput,
                OffsetTimeframeIntraday = OffsetTimeframeInput,
                FillIntradaySpace = FillIntradaySpace,
                // Fixed/Weekly/Monthly
                FixedRange = EnableFixedRange,
                EnableWeeklyProfile = EnableWeeklyProfile,
                EnableMonthlyProfile = EnableMonthlyProfile,
                // Mini VPs
                EnableMiniProfiles = EnableMiniProfiles,
                MiniVPsTimeframe = MiniVPs_Timeframe,
                ShowMiniResults = ShowMiniResults,

                // Results
                ShowResults = ShowResults,
                // Results - Buy_Sell / Delta
                ShowSideTotal = ShowSideTotal,
                ResultView = ResultsView_Input,
                OperatorBuySell = OperatorBuySell_Input,
                // Results - Delta
                ShowMinMax = ShowMinMaxDelta,
                ShowOnlySubtDelta = ShowOnlySubtDelta,
                // Results - Large Filter
                EnableLargeFilter = EnableLargeFilter,
                MAtype_Large = MAtype_Large,
                MAperiod_Large = MAperiod_Large,
                LargeFilter_Ratio = LargeFilter_Ratio,

                // Spike Filter
                EnableSpike = EnableSpikeFilter,
                SpikeView = SpikeView_Input,
                IconView = IconView_Input,
                // Spike Filter - Filter Settings
                SpikeFilter = SpikeFilter_Input,
                MAtype_Spike = MAtype_Spike,
                MAperiod_Spike = MAperiod_Spike,
                // Spike Filter - Notifications
                EnableSpikeNotify = EnableSpikeNotification,
                Spike_NotificationType = Spike_NotificationType_Input,
                Spike_SoundType = Spike_SoundType,
                // Spike Filter - Bubbles Chart
                EnableSpikeChart = EnableSpikeChart,
                SpikeChartColoring = SpikeChartColoring_Input,
                // Spike Filter - Levels
                ShowSpikeLevels = ShowSpikeLevels,
                SpikeLevels_MaxCount = SpikeLevels_MaxCount,
                SpikeLevels_ResetDaily = SpikeLevels_ResetDaily,
                SpikeLevelsColoring = SpikeLevelsColoring_Input,

                // Bubbles Chart
                EnableBubbles = EnableBubblesChart,
                BubblesSize = BubblesSizeMultiplier,
                BubblesSource = BubblesSource_Input,
                BubblesFilter = BubblesFilter_Input,
                MAtype_Bubbles = MAtype_Bubbles,
                MAperiod_Bubbles = MAperiod_Bubbles,
                BubblesColoring = BubblesColoring_Input,
                BubblesMomentumStrategy = BubblesMomentumStrategy_Input,
                // Bubbles Chart - Ultra Notifications
                EnableUltraBubblesNotifiy = EnableUltraBubblesNotification,
                UltraBubbles_NotifyType = UltraBubbles_NotificationType_Input,
                UltraBubbles_SoundType = UltraBubbles_SoundType,
                // Bubbles Chart - Ultra Levels
                ShowUltraBubblesLevels = ShowUltraBubblesLevels,
                UltraBubbles_ResetDaily = UltraBubbles_ResetDaily,
                UltraBubbles_MaxCount = UltraBubbles_MaxCount,
                UltraBubblesBreak = UltraBubblesBreak_Input,
                UltraBubbles_RectSize = UltraBubbles_RectSizeInput,
                UltraBubblesColoring = UltraBubblesColoring_Input,

                // Misc
                ShowHist = ShowHist,
                FillHist = FillHist,
                ShowNumbers = ShowNumbers,
                DrawAtZoom_Value = DrawAtZoom_Value,
                SegmentsInterval = SegmentsInterval_Input,
                VPInterval = ODFInterval_Input,
                ShowBubbleValue = ShowBubbleValue
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

        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "ODFT",
                Padding = 0,
                Height = 22,
                Width = 40, // Fix MacOS => stretching button when StackPanel is used.
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

        private void VerifyConflict() {
            // Timeframes Conflict
            if (EnableVP && EnableWeeklyProfile && SegmentsInterval_Input == SegmentsInterval_Data.Daily) {
                DrawOnScreen("Misc >> Segments should be set to 'Weekly' or 'Monthly' \n to calculate Weekly Profile");
                segmentsConflict = true;
                return;
            }
            if (EnableVP && EnableMonthlyProfile && SegmentsInterval_Input != SegmentsInterval_Data.Monthly) {
                DrawOnScreen("Misc >> Segments should be set to 'Monthly' \n to calculate Monthly Profile");
                segmentsConflict = true;
                return;
            }
            if (ODFInterval_Input == ODFInterval_Data.Weekly && SegmentsInterval_Input == SegmentsInterval_Data.Daily) {
                DrawOnScreen("Misc >> Segments should be set to 'Weekly' or 'Monthly' \n to calculate Order Flow weekly");
                segmentsConflict = true;
                return;
            }
            segmentsConflict = false;
        }
        public override void Calculate(int index)
        {
            // Tick Data Collection on chart
            bool isOnChart = LoadTickStrategy_Input != LoadTickStrategy_Data.At_Startup_Sync;
            if (isOnChart && !loadingTicksComplete)
                LoadMoreTicksOnChart();

            bool isOnChartAsync = LoadTickStrategy_Input == LoadTickStrategy_Data.On_ChartEnd_Async;
            if (isOnChartAsync && !loadingTicksComplete)
                return;

            // Removing Messages
            if (!IsLastBar) {
                DrawOnScreen("");
                Second_DrawOnScreen("");
            }

            // For some reason, the OrderFlow() call doesn't seem to be enough.
            LockODFTemplate();

            // ==== Chart Segmentation ====
            CreateSegments(index);

            /*
            After Initialize() or Indicator's restart, when loading settings that have Week/Month Profiles
            Calculate() will draw any period that Tick Data is available
            instead of drawing at current lookback DATE as ClearAndRecalculate() loop.
            It's expected but not desired behavior.
            */
            if (EnableVP && !IsLastBar){
                CreateMonthlyVP(index);
                CreateWeeklyVP(index);
            }

            // LookBack
            Bars ODF_Bars = ODFInterval_Input == ODFInterval_Data.Daily ? DailyBars : WeeklyBars;

            // Get Index of ODF Interval to continue only in Lookback
            int iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (ODF_Bars.ClosePrices.Count - iVerify > Lookback)
                return;

            int TF_idx = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes[TF_idx]);

            // ODF/VP => Reset filters and main VP
            if (index == indexStart ||
                (index - 1) == indexStart && isPriceBased_Chart ||
                (index - 1) == indexStart && (index - 1) != lastCleaned._ODF_Interval
            )
                MassiveCleanUp(indexStart, index);

            // Historical data
            if (!IsLastBar) {
                // Required for [Ultra Bubbles, Spike] Levels in Historical Data
                lockUltraLevels = false;
                lockSpikeLevels = false;

                if (!isPriceBased_Chart)
                    CreateOrderFlow(index);
                else {
                    // PriceGap condition can't handle very strong gaps
                    try { CreateOrderFlow(index); } catch { };
                }

                // Allows MiniVPs if (!EnableVP)
                CreateMiniVPs(index);

                if (EnableVP)
                    VolumeProfile(indexStart, index);

                isUpdateVP = true; // chart end
            }
            else
            {
                // Required for Non-Time based charts (Renko, Range, Ticks)
                if (isPriceBased_NewBar) {
                    lockNotifyInPriceBased(true);

                    CreateOrderFlow(index - 1);
                    isPriceBased_NewBar = false;

                    lockNotifyInPriceBased(false);
                    return;
                }
                CreateOrderFlow(index);

                // Live VP
                if (UpdateVPStrategy_Input == UpdateVPStrategy_Data.SameThread_MayFreeze)
                {
                    if (EnableVP)
                        LiveVP_Update(indexStart, index);
                    else if (!EnableVP && EnableMiniProfiles)
                        LiveVP_Update(indexStart, index, true);
                }
                else
                    LiveVP_Concurrent(index, indexStart);
            }

            void CreateOrderFlow(int idx)
            {
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                int[] resetDelta = {0, 0};
                MinMaxDelta = resetDelta;
                OrderFlow(idx);
            }
            void lockNotifyInPriceBased(bool value) {
                lockTickNotify = value;
                lockUltraNotify = !value;
            }
        }

        private void MassiveCleanUp(int indexStart, int index) {
            // Reset VP
            // Segments are identified by TF_idx(start)
            // No need to clean up even if it's Daily Interval
            if (!IsLastBar)
                lastTick_VPStart = lastTick_VP;
            VP_VolumesRank.Clear();
            VP_VolumesRank_Up.Clear();
            VP_VolumesRank_Down.Clear();
            VP_VolumesRank_Subt.Clear();
            VP_DeltaRank.Clear();
            lastCleaned._ODF_Interval = index == indexStart ? index : (index - 1);

            // Reset Filters
            /*
            A high memory usage at the first indicator's startup or after being compiled was located here,
            where I just repeat the "// Filters" Initialize() code.
            - CreateDataSeries() first call outside Initialize() leads to massive RAM usage, even if it's suppose to be empty,
              then the GC does it job.
            - Subsequents calls doesn't affect anything, just increase 5-7mb of ram incrementally.

            Also, I did a simple test where just one rectangle with the same ID(name), is created/update every Calculate() call:
                Chart.DrawRectangle("eat-ram-now", Bars.OpenTimes[index - 1], Bars.OpenPrices[index - 1], Bars.OpenTimes[index], Bars.ClosePrices[index - 1], Color.Red);
            After the first tick update, the memory usage grows up quickly (Max 3gb) then (maybe GC does it) return to normality.

            - This happens ONLY ONCE, at the first custom indicator's / cTrader startup
            - Since everything works as expected afterwards, it's OK... I suppose.

            - In this test, no "Algo" instances or multiples charts were open, just:
                - Open Task Manager
                - Open cTrader > Trade > 1 EURUSD chart > add "eat-ram-now" test indicator > wait the first tick
                - Watch the cTrader memory usage rise and fall quickly.

            // ==================================

            Now about the Filters:

            At lower timeframes like 1m (high bars count) WITH ANY FILTER ACTIVATED:
            - AsyncTickLoading or Calculate() first call, everything is drawn at the speed of light...

            However, when ClearAndRecalculate() is called from ParamsPanel:
            - Any(even one) .Result[index] of filters(MAs, StdDev) in Loop Segments of OrderFlow(),
            causes a SEVERE SLOWDOWN in Loop performance, no matter if the recalculations are made insider a Timer.

            So, in order to reach the same performance as Calculate() with the Filters activated:
             - Custom MAs implementation is required.
            */
            if (UseCustomMAs) {
                _dynamicBuffer.Clear();
                _deltaBuffer.CumulDelta.Clear();
                _deltaBuffer.Subtract.Clear();
                // MAs that uses previous values should be cleaned
                _maDynamic.Clear();
                _deltaBuffer.MACumulDelta_Bubbles.Clear();
                _deltaBuffer.MASubtract_Bubbles.Clear();
                _deltaBuffer.MASubtract_Large.Clear();
                _deltaBuffer.MASpike.Clear();
            }
            else
            {
                for (int i = 0; i < Bars.Count; i++)
                {
                    DynamicSeries[i] = double.NaN;
                    SubtractDeltaSeries[i] = double.NaN;
                    CumulDeltaSeries[i] = double.NaN;
                }

                MADynamic_LargeFilter = Indicators.MovingAverage(DynamicSeries, MAperiod_Large, MAtype_Large);
                MASubtract_LargeFilter = Indicators.MovingAverage(SubtractDeltaSeries, MAperiod_Large, MAtype_Large);

                MABubbles_SubtractDelta = Indicators.MovingAverage(SubtractDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);
                StdDevBubbles_SubtractDelta = Indicators.StandardDeviation(SubtractDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);

                MABubbles_CumulDelta = Indicators.MovingAverage(CumulDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);
                StdDevBubbles_CumulDelta = Indicators.StandardDeviation(CumulDeltaSeries, MAperiod_Bubbles, MAtype_Bubbles);

                MABubbles = Indicators.MovingAverage(DynamicSeries, MAperiod_Bubbles, MAtype_Bubbles);
                StdDevBubbles = Indicators.StandardDeviation(DynamicSeries, MAperiod_Bubbles, MAtype_Bubbles);

                MASpikeFilter = Indicators.MovingAverage(DynamicSeries, MAperiod_Spike, MAtype_Spike);
                StdDevSpikeFilter = Indicators.StandardDeviation(DynamicSeries, MAperiod_Spike, MAtype_Spike);
            }

            // Reset Levels
            if (UltraBubbles_ResetDaily && EnableBubblesChart)
            {
                foreach (var rect in ultraRectangles.Values) {
                    if (rect.isActive) {
                        rect.Rectangle.Time2 = Bars.OpenTimes[indexStart];
                    }
                }

                ultraRectangles.Clear();
            }
            if (SpikeLevels_ResetDaily && EnableSpikeFilter)
            {
                foreach (var rect in spikeRectangles.Values) {
                    if (rect.isActive) {
                        rect.Rectangle.Time2 = Bars.OpenTimes[indexStart];
                    }
                }

                spikeRectangles.Clear();
            }
        }

        // *********** ORDER FLOW TICKS ***********
        private void LockODFTemplate() {
            // Lock Bubbles Chart template
            if (EnableBubblesChart) {
                ShowHist = false;
                ShowNumbers = false;
                ShowResults = false;
                EnableSpikeFilter = false;
                EnableVP = false;
                EnableMiniProfiles = false;
            }
            // Lock Spike Columns Chart template
            if (EnableSpikeChart) {
                EnableSpikeFilter = true;
                ShowHist = false;
                ShowResults = false;
                ShowMinMaxDelta = false;
            }
        }
        private void OrderFlow(int iStart, bool isLookback = false)
        {
            // ==== Highest and Lowest ====
            double highest = Bars.HighPrices[iStart];
            double lowest = Bars.LowPrices[iStart];
            double open = Bars.OpenPrices[iStart];

            if (isRenkoChart && ShowWicks) {
                bool isUp = Bars.ClosePrices[iStart] > Bars.OpenPrices[iStart];
                DateTime currentOpenTime = Bars.OpenTimes[iStart ];
                DateTime nextOpenTime = Bars.OpenTimes[iStart + 1];

                double[] wicks = GetWicks(currentOpenTime, nextOpenTime);

                if (IsLastBar && !isPriceBased_NewBar) {
                    lowest = wicks[0];
                    highest = wicks[1];
                    open = Bars.ClosePrices[iStart - 1];
                } else {
                    if (isUp)
                        lowest = wicks[0];
                    else
                        highest = wicks[1];
                }
            }
            // ==== Segments ====
            // Start - Modified Logic
            List<double> barsSegments = new();

            lowest -= rowHeight;
            highest += rowHeight;

            for (int i = 0; i < Segments_VP.Count; i++)
            {
                double row = Segments_VP[i];
                if (lowest <= row)
                    barsSegments.Add(row);
                if (highest < row)
                    break;
            }
            Segments_Bar = barsSegments.OrderBy(x => x).ToList();
            // End - Modified Logic

            // ==== Volume on Tick ====
            VP_Tick(iStart);

            // ==== Drawing ====
            if (Segments_Bar.Count == 0 || isLookback)
                return;

            LockODFTemplate();

            // For results
            double rowHeightHalf = (rowHeight + rowHeight) / 2;
            double highestHalf = Bars.HighPrices[iStart] + rowHeightHalf;
            double lowestHalf = Bars.LowPrices[iStart] - rowHeightHalf;
            if (isRenkoChart && ShowWicks) {
                lowest += rowHeight;
                highest -= rowHeight;
                highestHalf = highest + rowHeightHalf;
                lowestHalf = lowest - rowHeightHalf;
            }

            DateTime xBar = Bars.OpenTimes[iStart];

            // Any Volume Mode
            double maxLength_LeftSide = xBar.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
            double proportion_LeftSide = maxLength_LeftSide / 3;

            double maxLength_RightSide;
            if (!IsLastBar || isPriceBased_NewBar)
                maxLength_RightSide = Bars[iStart + 1].OpenTime.Subtract(xBar).TotalMilliseconds;
            else
                maxLength_RightSide = maxLength_LeftSide;
            double proportion_RightSide = maxLength_RightSide / 3;

            bool gapWeekday = xBar.DayOfWeek == DayOfWeek.Sunday && Bars.OpenTimes[iStart - 1].DayOfWeek == DayOfWeek.Friday;
            bool priceGap = xBar == Bars[iStart - 1].OpenTime || Bars[iStart - 2].OpenTime == Bars[iStart - 1].OpenTime;
            bool isBullish = Bars.ClosePrices[iStart] > Bars.OpenPrices[iStart];
            // For real-time => Avoid stretching the histograms away ad infinitum
            bool avoidStretching = IsLastBar && !isPriceBased_NewBar;

            // Manual Refactoring.
            // LLM allucinates.
            // ODF_Agg => Refactored again
            double loopPrevSegment = 0;
            for (int i = 0; i < Segments_Bar.Count; i++)
            {
                if (loopPrevSegment == 0)
                    loopPrevSegment = Segments_Bar[i];

                double priceKey = Segments_Bar[i];
                if (!VolumesRank.ContainsKey(priceKey))
                    continue;

                // ====  HISTOGRAMs + Texts  ====
                /*
                    Indeed, the value of X-Axis is simply a rule of three,
                    where the maximum value of the respective side (Volume/Buy/Sell) will be the maxLength (in Milliseconds),
                    from there the math adjusts the histograms.
                        MaxValue    maxLength(ms)
                        x             ?(ms)
                    The values 1.50 and 3 are the manually set values like the size of the Bar body in any timeframe
                    (Candle, Ticks, Renko, Range)

                    NEW IN ODF_AGG => To avoid unnecessary workarounds for others timeframes/charts,
                    as well as improve readability, instead of just one maxLength,
                    that works great in Candles Charts(timebased) only, now we have:
                    - maxLengthLeft / maxLengthRight
                    - Like in Divided View, for Profile view we should add one more step:
                        - Calculate the Middle-to-Left & Middle-to-Right
                        - Set Left side as starting point (this)
                        - Add Left-to-Middle proportion
                        - Add Middle-to-Right proportion to current (BarLeft + Left = Middle) total milliseconds.
                */

                double lowerSegmentY1 = loopPrevSegment;
                double upperSegmentY2 = Segments_Bar[i];

                void DrawRectangle_Normal(int currentVolume, int maxVolume, bool profileInMiddle = false)
                {
                    double proportion_ToMiddle = currentVolume * proportion_LeftSide;
                    double dynLength_ToMiddle = proportion_ToMiddle / maxVolume;

                    double proportion_ToRight = currentVolume * proportion_RightSide;
                    double dynLength_ToRight = proportion_ToRight / maxVolume;

                    bool dividedCondition = VolumeView_Input == VolumeView_Data.Profile && profileInMiddle; // Profile View - Half Proportion

                    DateTime x1 = dividedCondition ? xBar : xBar.AddMilliseconds(-proportion_LeftSide);
                    DateTime x2;
                    if (dividedCondition)
                        x2 = x1.AddMilliseconds(dynLength_ToRight);
                    else
                        x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(dynLength_ToRight);

                    if (isPriceBased_Chart)
                    {
                        if (avoidStretching)
                            x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(0);

                        if (priceGap)
                        {
                            x1 = xBar;
                            x2 = x1.AddMilliseconds(dynLength_ToRight);
                        }
                    }

                    Color colorHist = currentVolume != maxVolume ? VolumeColor : VolumeLargeColor;

                    DrawOrCache(new DrawInfo
                    {
                        BarIndex = iStart,
                        Type = DrawType.Rectangle,
                        Id = $"{iStart}_{i}_Normal",
                        X1 = x1,
                        Y1 = lowerSegmentY1,
                        X2 = x2,
                        Y2 = upperSegmentY2,
                        Color = colorHist
                    });
                }

                void DrawRectangle_BuySell(
                    int currentBuy, int maxBuy,
                    int currentSell, int maxSell)
                {

                    // Right Side - Divided View
                    double proportionBuy_RightSide = currentBuy * proportion_RightSide;
                    double dynLengthBuy_RightSide = proportionBuy_RightSide / maxBuy;

                    // Left Side - Divided View
                    double proportionSell_LeftSide = currentSell * proportion_LeftSide;
                    double dynLengthSell_LeftSide = proportionSell_LeftSide / maxSell;

                    // Profile View - Complete Proportion
                    int profileMaxVolume = maxBuy > maxSell ? maxBuy : maxSell;

                    double proportionBuy_ToMiddle = currentBuy * proportion_LeftSide;
                    double dynLengthBuy_ToMiddle = proportionBuy_ToMiddle / profileMaxVolume;

                    double proportionSell_ToMiddle = currentSell * proportion_LeftSide;
                    double dynLengthSell_ToMiddle = proportionSell_ToMiddle / profileMaxVolume;

                    double proportionSell_RightSide = currentSell * proportion_RightSide;
                    double dynLengthSell_RightSide = proportionSell_RightSide / profileMaxVolume;
                    // ========

                    bool dividedCondition = VolumeView_Input == VolumeView_Data.Divided;
                    DateTime x1 = dividedCondition || gapWeekday ? xBar : xBar.AddMilliseconds(-proportion_LeftSide);

                    DateTime x2_Buy = x1.AddMilliseconds(dividedCondition ? dynLengthBuy_RightSide : dynLengthBuy_ToMiddle);
                    DateTime x2_Sell;
                    if (dividedCondition || gapWeekday)
                        x2_Sell = x1.AddMilliseconds(-dynLengthSell_LeftSide);
                    else
                        x2_Sell = x1.AddMilliseconds(dynLengthSell_ToMiddle).AddMilliseconds(dynLengthSell_RightSide);

                    if (isPriceBased_Chart)
                    {
                        if (avoidStretching)
                        {
                            dynLengthSell_RightSide = 0;
                            dynLengthBuy_ToMiddle /= 2;

                            x2_Buy = x1.AddMilliseconds(dynLengthBuy_ToMiddle);
                            x2_Sell = x1.AddMilliseconds(dynLengthSell_ToMiddle).AddMilliseconds(dynLengthSell_RightSide);
                        }

                        if (priceGap)
                        {
                            proportionBuy_ToMiddle = currentBuy * (proportion_RightSide / 2);
                            dynLengthBuy_ToMiddle = proportionBuy_ToMiddle / profileMaxVolume;

                            proportionSell_RightSide = currentSell * proportion_RightSide;
                            dynLengthSell_RightSide = proportionSell_RightSide / profileMaxVolume;

                            x2_Buy = x1.AddMilliseconds(dynLengthBuy_ToMiddle);
                            x2_Sell = x1.AddMilliseconds(dynLengthSell_RightSide);
                        }
                    }

                    Color buyDividedColor = currentBuy != maxBuy ? BuyColor : BuyLargeColor;
                    Color sellDividedColor = currentSell != maxSell ? SellColor : SellLargeColor;
                    if (ColoringOnlyLarguest)
                    {
                        buyDividedColor = maxBuy > maxSell && currentBuy == maxBuy ?
                            BuyLargeColor : BuyColor;
                        sellDividedColor = maxSell > maxBuy && currentSell == maxSell ?
                            SellLargeColor : SellColor;
                    }

                    Color buyColor = dividedCondition ? buyDividedColor : BuyColor;
                    Color sellColor = dividedCondition ? sellDividedColor : SellColor;

                    // Sell histogram first, Buy histogram to override it.
                    DrawOrCache(new DrawInfo
                    {
                        BarIndex = iStart,
                        Type = DrawType.Rectangle,
                        Id = $"{iStart}_{i}_Sell",
                        X1 = x1,
                        Y1 = lowerSegmentY1,
                        X2 = x2_Sell,
                        Y2 = upperSegmentY2,
                        Color = sellColor
                    });

                    DrawOrCache(new DrawInfo
                    {
                        BarIndex = iStart,
                        Type = DrawType.Rectangle,
                        Id = $"{iStart}_{i}_Buy",
                        X1 = x1,
                        Y1 = lowerSegmentY1,
                        X2 = x2_Buy,
                        Y2 = upperSegmentY2,
                        Color = buyColor
                    });
                }

                void DrawRectangle_Delta(int currentDelta)
                {
                    int positiveDeltaMax = DeltaRank.Values.Max();
                    int negativaDeltaMax = 0;
                    try { negativaDeltaMax = DeltaRank.Values.Where(n => n < 0).Min(); } catch { }

                    // Divided View
                    double dynLengthDelta_Divided = 0;
                    if (currentDelta > 0)
                    {
                        double proportionDelta_Positive = currentDelta * proportion_RightSide;
                        dynLengthDelta_Divided = proportionDelta_Positive / positiveDeltaMax;
                    }
                    else
                    {
                        double proportionDelta_Negative = currentDelta * proportion_LeftSide;
                        dynLengthDelta_Divided = proportionDelta_Negative / negativaDeltaMax;
                        dynLengthDelta_Divided = -dynLengthDelta_Divided;
                    }

                    // Profile View - Complete Proportion
                    int deltaMax = positiveDeltaMax > Math.Abs(negativaDeltaMax) ? positiveDeltaMax : Math.Abs(negativaDeltaMax);

                    double proportion_ToMiddle = Math.Abs(currentDelta) * proportion_LeftSide;
                    double dynLength_ToMiddle = proportion_ToMiddle / deltaMax;

                    double proportion_ToRight = Math.Abs(currentDelta) * proportion_RightSide;
                    double dynLength_ToRight = proportion_ToRight / deltaMax;
                    // ========

                    bool dividedCondition = VolumeView_Input == VolumeView_Data.Divided;
                    DateTime x1 = dividedCondition || gapWeekday ? xBar : xBar.AddMilliseconds(-proportion_LeftSide);

                    DateTime x2;
                    if (dividedCondition || gapWeekday)
                        x2 = x1.AddMilliseconds(dynLengthDelta_Divided);
                    else
                        x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(dynLength_ToRight);

                    if (isPriceBased_Chart && VolumeView_Input == VolumeView_Data.Profile)
                    {
                        if (avoidStretching)
                            x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(0);

                        if (priceGap)
                        {
                            x1 = xBar;
                            x2 = x1.AddMilliseconds(dynLengthDelta_Divided);
                        }
                    }

                    Color buyDividedColor = currentDelta != positiveDeltaMax ? BuyColor : BuyLargeColor;
                    Color sellDividedColor = currentDelta != negativaDeltaMax ? SellColor : SellLargeColor;
                    if (ColoringOnlyLarguest)
                    {
                        buyDividedColor = positiveDeltaMax > Math.Abs(negativaDeltaMax) && currentDelta == positiveDeltaMax ?
                            BuyLargeColor : BuyColor;
                        sellDividedColor = Math.Abs(negativaDeltaMax) > positiveDeltaMax && currentDelta == negativaDeltaMax ?
                            SellLargeColor : SellColor;
                    }

                    Color buyColorWithFilter = VolumeView_Input == VolumeView_Data.Divided ? buyDividedColor : BuyColor;
                    Color sellColorWithFilter = VolumeView_Input == VolumeView_Data.Divided ? sellDividedColor : SellColor;

                    Color colorHist = currentDelta > 0 ? buyColorWithFilter : sellColorWithFilter;

                    DrawOrCache(new DrawInfo
                    {
                        BarIndex = iStart,
                        Type = DrawType.Rectangle,
                        Id = $"{iStart}_{i}_Delta",
                        X1 = x1,
                        Y1 = lowerSegmentY1,
                        X2 = x2,
                        Y2 = upperSegmentY2,
                        Color = colorHist
                    });
                }

                if (VolumeMode_Input == VolumeMode_Data.Normal)
                {
                    if (ShowHist)
                        DrawRectangle_Normal(VolumesRank[priceKey], VolumesRank.Values.Max());

                    if (ShowNumbers)
                    {
                        double value = VolumesRank[priceKey];
                        string valueFmtd = FormatNumbers ? FormatBigNumber(value) : $"{value}";

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_{i}_NormalNumber",
                            Text = valueFmtd,
                            X1 = Bars.OpenTimes[iStart],
                            Y1 = priceKey,
                            horizontalAlignment = HorizontalAlignment.Center,
                            verticalAlignment = VerticalAlignment.Bottom,
                            FontSize = FontSizeNumbers,
                            Color = RtnbFixedColor
                        });
                    }

                    double sumValue = VolumesRank.Values.Sum();
                    DynamicSeries[iStart] = sumValue;

                    if (ShowResults)
                    {
                        string valueFmtd = FormatResults ? FormatBigNumber(sumValue) : $"{sumValue}";
                        Color resultColor = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : VolumeColor;

                        if (EnableLargeFilter)
                        {
                            // ====== Strength Filter ======
                            double filterValue = 0;
                            if (UseCustomMAs)
                                filterValue = CustomMAs(DynamicSeries[iStart], iStart, MAperiod_Large, customMAtype_Large);
                            else
                                filterValue = MADynamic_LargeFilter.Result[iStart];

                            double volumeStrength = DynamicSeries[iStart] / filterValue;
                            Color filterColor = volumeStrength >= LargeFilter_Ratio ? ColorLargeResult : resultColor;

                            resultColor = filterColor;
                            if (LargeFilter_ColoringBars && filterColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                            else
                                Chart.SetBarFillColor(iStart, isBullish ? Chart.ColorSettings.BullFillColor : Chart.ColorSettings.BearFillColor);
                        }

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_NormalSum",
                            Text = $"\n{valueFmtd}",
                            X1 = xBar,
                            Y1 = lowestHalf,
                            horizontalAlignment = HorizontalAlignment.Center,
                            FontSize = FontSizeResults,
                            Color = resultColor
                        });
                    }
                }
                else if (VolumeMode_Input == VolumeMode_Data.Buy_Sell)
                {
                    if (ShowHist)
                    {
                        DrawRectangle_BuySell(
                            VolumesRank_Up[priceKey], VolumesRank_Up.Values.Max(),
                            VolumesRank_Down[priceKey], VolumesRank_Down.Values.Max()
                        );
                    }

                    if (ShowNumbers)
                    {
                        double buyValue = VolumesRank_Up[priceKey];
                        double sellValue = VolumesRank_Down[priceKey];
                        string buyValueFmt = FormatNumbers ? FormatBigNumber(buyValue) : $"{buyValue}";
                        string sellValueFmt = FormatNumbers ? FormatBigNumber(sellValue) : $"{sellValue}";

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_{i}_BuyNumber",
                            Text = buyValueFmt,
                            X1 = xBar,
                            Y1 = priceKey,
                            horizontalAlignment = HorizontalAlignment.Right,
                            verticalAlignment = VerticalAlignment.Bottom,
                            FontSize = FontSizeNumbers,
                            Color = RtnbFixedColor
                        });

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_{i}_SellNumber",
                            Text = sellValueFmt,
                            X1 = xBar,
                            Y1 = priceKey,
                            horizontalAlignment = HorizontalAlignment.Left,
                            verticalAlignment = VerticalAlignment.Bottom,
                            FontSize = FontSizeNumbers,
                            Color = RtnbFixedColor
                        });
                    }

                    double sumValue = VolumesRank_Up.Values.Sum() + VolumesRank_Down.Values.Sum();
                    double subtValue = VolumesRank_Up.Values.Sum() - VolumesRank_Down.Values.Sum();

                    DynamicSeries[iStart] = OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumValue : Math.Abs(subtValue);

                    if (ShowResults)
                    {
                        var selected = ResultsView_Input;

                        int volBuy = VolumesRank_Up.Values.Sum();
                        int volSell = VolumesRank_Down.Values.Sum();

                        if (ShowSideTotal)
                        {
                            Color colorLeft = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : SellColor;
                            Color colorRight = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : BuyColor;

                            int percentBuy = (volBuy * 100) / (volBuy + volSell);
                            int percentSell = (volSell * 100) / (volBuy + volSell);

                            string volBuyFmtd = FormatResults ? FormatBigNumber(volBuy) : $"{volBuy}";
                            string volSellFmtd = FormatResults ? FormatBigNumber(volSell) : $"{volSell}";

                            string strBuy = selected == ResultsView_Data.Percentage ? $"\n{percentBuy}%" : selected == ResultsView_Data.Value ? $"\n{volBuyFmtd}" : $"\n{percentBuy}%\n({volBuyFmtd})";
                            string strSell = selected == ResultsView_Data.Percentage ? $"\n{percentSell}%" : selected == ResultsView_Data.Value ? $"\n{volSellFmtd}" : $"\n{percentSell}%\n({volSellFmtd})";

                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_SellSideSum",
                                Text = strSell,
                                X1 = xBar,
                                Y1 = lowestHalf,
                                horizontalAlignment = HorizontalAlignment.Left,
                                FontSize = FontSizeResults,
                                Color = colorLeft
                            });

                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_BuySideSum",
                                Text = strBuy,
                                X1 = xBar,
                                Y1 = lowestHalf,
                                horizontalAlignment = HorizontalAlignment.Right,
                                FontSize = FontSizeResults,
                                Color = colorRight
                            });
                        }

                        string sumFmtd = FormatResults ? FormatBigNumber(sumValue) : $"{sumValue}";

                        string subtValueFmtd = subtValue > 0 ? FormatBigNumber(subtValue) : $"-{FormatBigNumber(Math.Abs(subtValue))}";
                        string subtFmtd = FormatResults ? subtValueFmtd : $"{subtValue}";

                        string strFormated = OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumFmtd : subtFmtd;

                        Color compareColor = volBuy > volSell ? BuyColor : volBuy < volSell ? SellColor : RtnbFixedColor;
                        Color colorCenter = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareColor;

                        ResultsView_Data selectedView = ResultsView_Input;
                        bool showSide_notBoth = ShowSideTotal && (selectedView == ResultsView_Data.Percentage || selectedView == ResultsView_Data.Value);
                        bool showSide_Both = ShowSideTotal && selectedView == ResultsView_Data.Both;
                        string dynSpaceSum = showSide_notBoth ? $"\n\n\n" :
                                              showSide_Both ? $"\n\n\n\n" :
                                              "\n";

                        if (EnableLargeFilter)
                        {
                            // ====== Strength Filter ======
                            double filterValue = 0;
                            if (UseCustomMAs)
                                filterValue = CustomMAs(DynamicSeries[iStart], iStart, MAperiod_Large, customMAtype_Large);
                            else
                                filterValue = MADynamic_LargeFilter.Result[iStart];

                            double bsStrength = DynamicSeries[iStart] / filterValue;
                            Color filterColor = bsStrength >= LargeFilter_Ratio ? ColorLargeResult : colorCenter;

                            colorCenter = filterColor;
                            if (LargeFilter_ColoringBars && filterColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                            else
                                Chart.SetBarFillColor(iStart, isBullish ? Chart.ColorSettings.BullFillColor : Chart.ColorSettings.BearFillColor);
                        }

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_BSResultOperator",
                            Text = $"{dynSpaceSum}{strFormated}",
                            X1 = xBar,
                            Y1 = lowestHalf,
                            horizontalAlignment = HorizontalAlignment.Center,
                            FontSize = FontSizeResults,
                            Color = colorCenter
                        });
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

                        HorizontalAlignment horizontalAligh;
                        if (VolumeView_Input == VolumeView_Data.Divided)
                            horizontalAligh = deltaValue > 0 ? HorizontalAlignment.Right : deltaValue > 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                        else
                            horizontalAligh = HorizontalAlignment.Center;

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_{i}_DeltaNumber",
                            Text = deltaFmtd,
                            X1 = xBar,
                            Y1 = priceKey,
                            horizontalAlignment = horizontalAligh,
                            verticalAlignment = VerticalAlignment.Bottom,
                            FontSize = FontSizeNumbers,
                            Color = RtnbFixedColor
                        });
                    }

                    int totalDelta = DeltaRank.Values.Sum();

                    if (!TotalDeltaRank.ContainsKey(iStart))
                        TotalDeltaRank.Add(iStart, totalDelta);
                    else
                        TotalDeltaRank[iStart] = totalDelta;

                    int cumulDelta = TotalDeltaRank.Keys.Count <= 1 ? TotalDeltaRank[iStart] : (TotalDeltaRank[iStart] + TotalDeltaRank[iStart - 1]);
                    int prevCumulDelta = TotalDeltaRank.Keys.Count <= 2 ? TotalDeltaRank[iStart] : (TotalDeltaRank[iStart - 1] + TotalDeltaRank[iStart - 2]);

                    CumulDeltaSeries[iStart] = Math.Abs(cumulDelta);
                    DynamicSeries[iStart] = Math.Abs(totalDelta);

                    int minDelta = MinMaxDelta[0];
                    int maxDelta = MinMaxDelta[1];
                    int subDelta = minDelta - maxDelta;
                    int prevSubDelta = 0;
                    if (ShowMinMaxDelta || BubblesSource_Input == BubblesSource_Data.Subtract_Delta)
                    {
                        if (!SubtractDeltaRank.ContainsKey(iStart))
                            SubtractDeltaRank.Add(iStart, subDelta);
                        else
                            SubtractDeltaRank[iStart] = subDelta;

                        SubtractDeltaSeries[iStart] = Math.Abs(subDelta);
                        prevSubDelta = SubtractDeltaRank.Keys.Count <= 2 ? TotalDeltaRank[iStart] : SubtractDeltaRank[iStart - 1];
                    }

                    if (ShowResults)
                    {
                        ResultsView_Data selectedView = ResultsView_Input;

                        if (ShowSideTotal)
                        {
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

                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_DeltaSellSideSum",
                                Text = strSell,
                                X1 = xBar,
                                Y1 = lowestHalf,
                                horizontalAlignment = HorizontalAlignment.Left,
                                FontSize = FontSizeResults,
                                Color = colorLeft
                            });

                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_DeltaBuySideSum",
                                Text = strBuy,
                                X1 = xBar,
                                Y1 = lowestHalf,
                                horizontalAlignment = HorizontalAlignment.Right,
                                FontSize = FontSizeResults,
                                Color = colorRight
                            });
                        }

                        string totalDeltaValueFmtd = totalDelta > 0 ? FormatBigNumber(totalDelta) : $"-{FormatBigNumber(Math.Abs(totalDelta))}";
                        string totalDeltaFmtd = FormatResults ? totalDeltaValueFmtd : $"{totalDelta}";

                        bool showSide_notBoth = ShowSideTotal && (selectedView == ResultsView_Data.Percentage || selectedView == ResultsView_Data.Value);
                        bool showSide_Both = ShowSideTotal && selectedView == ResultsView_Data.Both;
                        string dynSpaceSum = showSide_notBoth ? $"\n\n\n" :
                                              showSide_Both ? $"\n\n\n\n" :
                                              "\n";

                        Color compareSum = DeltaRank.Values.Sum() > 0 ? BuyColor : DeltaRank.Values.Sum() < 0 ? SellColor : RtnbFixedColor;
                        Color colorCenter = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareSum;

                        if (ShowMinMaxDelta)
                        {
                            string minDeltaValueFmtd = minDelta > 0 ? FormatBigNumber(minDelta) : $"-{FormatBigNumber(Math.Abs(minDelta))}";
                            string maxDeltaValueFmtd = maxDelta > 0 ? FormatBigNumber(maxDelta) : $"-{FormatBigNumber(Math.Abs(maxDelta))}";
                            string subDeltaValueFmtd = subDelta > 0 ? FormatBigNumber(subDelta) : $"-{FormatBigNumber(Math.Abs(subDelta))}";
                            string minDeltaFmtd = FormatResults ? minDeltaValueFmtd : $"{minDelta}";
                            string maxDeltaFmtd = FormatResults ? maxDeltaValueFmtd : $"{maxDelta}";
                            string subDeltaFmtd = FormatResults ? subDeltaValueFmtd : $"{subDelta}";

                            Color subtractColor = colorCenter;
                            if (EnableLargeFilter)
                            {
                                // ====== Strength Filter ======
                                double filterValue = 0;
                                if (UseCustomMAs)
                                    filterValue = CustomMAs(
                                        SubtractDeltaSeries[iStart],
                                        iStart, MAperiod_Large,
                                        customMAtype_Large, DeltaSwitch.Subtract
                                    );
                                else
                                    filterValue = MASubtract_LargeFilter.Result[iStart];

                                double subtractLargeStrength = SubtractDeltaSeries[iStart] / filterValue;
                                Color filterColor = subtractLargeStrength >= LargeFilter_Ratio ? ColorLargeResult : colorCenter;
                                subtractColor = filterColor;
                            }

                            HorizontalAlignment hAligh = HorizontalAlignment.Center;
                            int fontSize = FontSizeResults - 1;
                            if (!ShowOnlySubtDelta)
                            {
                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_MinDeltaResult",
                                    Text = $"\n\n{dynSpaceSum}min:{minDeltaFmtd}",
                                    X1 = xBar,
                                    Y1 = lowestHalf,
                                    horizontalAlignment = hAligh,
                                    FontSize = fontSize,
                                    Color = colorCenter
                                });

                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_MaxDeltaResult",
                                    Text = $"\n\n\n\n{dynSpaceSum}max:{maxDeltaFmtd}",
                                    X1 = xBar,
                                    Y1 = lowestHalf,
                                    horizontalAlignment = hAligh,
                                    FontSize = fontSize,
                                    Color = colorCenter
                                });

                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_SubtDeltaResult",
                                    Text = $"\n\n\n\n\n\n{dynSpaceSum}subt:{subDeltaFmtd}",
                                    X1 = xBar,
                                    Y1 = lowestHalf,
                                    horizontalAlignment = hAligh,
                                    FontSize = fontSize,
                                    Color = subtractColor
                                });
                            }
                            else
                            {
                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_SubtDeltaResult",
                                    Text = $"\n\n{dynSpaceSum}subt:{subDeltaFmtd}",
                                    X1 = xBar,
                                    Y1 = lowestHalf,
                                    horizontalAlignment = hAligh,
                                    FontSize = fontSize,
                                    Color = subtractColor
                                });
                            }
                        }

                        string cumulDeltaValueFmtd = cumulDelta > 0 ? FormatBigNumber(cumulDelta) : $"-{FormatBigNumber(Math.Abs(cumulDelta))}";
                        string cumulDeltaFmtd = FormatResults ? cumulDeltaValueFmtd : $"{cumulDelta}";

                        Color compareCD = cumulDelta > prevCumulDelta ? BuyColor : cumulDelta < prevCumulDelta ? SellColor : RtnbFixedColor;
                        Color colorCD = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareCD;

                        if (EnableLargeFilter)
                        {
                            // ====== Strength Filter ======
                            double filterValue = 0;
                            if (UseCustomMAs)
                                filterValue = CustomMAs(DynamicSeries[iStart], iStart, MAperiod_Large, customMAtype_Large);
                            else
                                filterValue = MADynamic_LargeFilter.Result[iStart];

                            double deltaLargeStrength = DynamicSeries[iStart] / filterValue;
                            Color filterColor = deltaLargeStrength >= LargeFilter_Ratio ? ColorLargeResult : colorCenter;

                            colorCenter = filterColor;
                            if (LargeFilter_ColoringBars && filterColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                            else
                                Chart.SetBarFillColor(iStart, isBullish ? Chart.ColorSettings.BullFillColor : Chart.ColorSettings.BearFillColor);

                            if (LargeFilter_ColoringCD)
                                colorCD = filterColor == ColorLargeResult ? filterColor : colorCD;

                        }

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_DeltaSum",
                            Text = $"{dynSpaceSum}{totalDeltaFmtd}",
                            X1 = xBar,
                            Y1 = lowestHalf,
                            horizontalAlignment = HorizontalAlignment.Center,
                            FontSize = FontSizeResults,
                            Color = colorCenter
                        });

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_CumulDeltaChange",
                            Text = $"{cumulDeltaFmtd}",
                            X1 = xBar,
                            Y1 = highestHalf,
                            horizontalAlignment = HorizontalAlignment.Center,
                            verticalAlignment = VerticalAlignment.Top,
                            FontSize = FontSizeResults,
                            Color = colorCD
                        });

                    }

                    // ====== Delta Bubbles Chart ======
                    if (EnableBubblesChart) {

                        double deltaValue = totalDelta;
                        double prevDeltaValue = TotalDeltaRank[iStart - 1];

                        double cumulDeltaValue = cumulDelta;
                        double prevCumulDeltaValue = prevCumulDelta;

                        double subtractDeltaValue = subDelta;
                        double prevSubtractDeltaValue = prevSubDelta;

                        bool sourceIsDelta = BubblesSource_Input == BubblesSource_Data.Delta;
                        bool sourceIsCumul = BubblesSource_Input == BubblesSource_Data.Cumulative_Delta_Change;

                        double currentSeriesValue = sourceIsDelta ? DynamicSeries[iStart] :
                                                    sourceIsCumul ? CumulDeltaSeries[iStart] :
                                                    SubtractDeltaSeries[iStart];

                        double currenFilterValue = 1;
                        if (UseCustomMAs) {
                            if (BubblesFilter_Input != BubblesFilter_Data.Both) {
                                DeltaSwitch deltaSwitch = sourceIsDelta ? DeltaSwitch.None :
                                                          sourceIsCumul ? DeltaSwitch.CumulDelta :
                                                          DeltaSwitch.Subtract;
                                bool isStdDev = BubblesFilter_Input == BubblesFilter_Data.Standard_Deviation;
                                currenFilterValue = CustomMAs(currentSeriesValue, iStart,
                                    MAperiod_Bubbles, customMAtype_Bubbles,
                                    deltaSwitch, isStdDev, MASwitch.Bubbles
                                );
                            }
                        }
                        else {
                            currenFilterValue = sourceIsDelta ? MABubbles.Result[iStart] :
                                                sourceIsCumul ? MABubbles_CumulDelta.Result[iStart] :
                                                MABubbles_SubtractDelta.Result[iStart];

                            if (BubblesFilter_Input == BubblesFilter_Data.Standard_Deviation)
                                currenFilterValue = sourceIsDelta ? StdDevBubbles.Result[iStart] :
                                                    sourceIsCumul ? StdDevBubbles_CumulDelta.Result[iStart] :
                                                    StdDevBubbles_SubtractDelta.Result[iStart];
                        }

                        double deltaStrength = currentSeriesValue / currenFilterValue;
                        if (BubblesFilter_Input == BubblesFilter_Data.Both)
                        {
                            if (UseCustomMAs) {
                                DeltaSwitch deltaSwitch = sourceIsDelta ? DeltaSwitch.None :
                                                          sourceIsCumul ? DeltaSwitch.CumulDelta :
                                                          DeltaSwitch.Subtract;
                                double ma = CustomMAs(currentSeriesValue, iStart, MAperiod_Bubbles, customMAtype_Bubbles, deltaSwitch, false, MASwitch.Bubbles);
                                double stddev = CustomMAs(currentSeriesValue, iStart, MAperiod_Bubbles, customMAtype_Bubbles, deltaSwitch, true, MASwitch.Bubbles);

                                deltaStrength = (currentSeriesValue - ma) / stddev;
                            }
                            else {
                                if (sourceIsDelta)
                                    deltaStrength = (currentSeriesValue - MABubbles.Result[iStart]) / StdDevBubbles.Result[iStart];
                                else if (sourceIsCumul)
                                    deltaStrength = (currentSeriesValue - MABubbles_CumulDelta.Result[iStart]) / StdDevBubbles_CumulDelta.Result[iStart];
                                else
                                    deltaStrength = (currentSeriesValue - MABubbles_SubtractDelta.Result[iStart]) / StdDevBubbles_SubtractDelta.Result[iStart];
                            }
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

                        bool sourceFading = sourceIsDelta ? (deltaValue > prevDeltaValue) :
                                            sourceIsCumul ? (cumulDeltaValue > prevCumulDeltaValue) :
                                            (subtractDeltaValue > prevSubtractDeltaValue);
                        bool sourcePositiveNegative = sourceIsDelta ? (deltaValue > 0) :
                                                      sourceIsCumul ? (cumulDeltaValue > 0) :
                                                      (subtractDeltaValue > 0);

                        Color fadingColor = sourceFading ? BuyColor : SellColor;
                        Color positiveNegativeColor = sourcePositiveNegative ? BuyColor : SellColor;

                        Color momentumColor = BubblesMomentumStrategy_Input == BubblesMomentumStrategy_Data.Fading ? fadingColor : positiveNegativeColor;
                        Color colorMode = BubblesColoring_Input == BubblesColoring_Data.Heatmap ? heatColor : momentumColor;

                        // X-value
                        (double x1Position, double dynLength) CalculateX1X2(double maxLength)
                        {
                            double maxLengthMaxBubble = maxLength * 1.4 * BubblesSizeMultiplier; // Slightly bigger than Bar Body
                            double maxLengthBubble = maxLength * BubblesSizeMultiplier;

                            double dynMaxProportion = filterSize == 5 ? maxLengthMaxBubble : maxLengthBubble;
                            double proportion = filterSize * (dynMaxProportion / 3);

                            double dynMaxLength = filterSize == 5 ? 5 : 4;
                            double dynLength = proportion / dynMaxLength;

                            // X1 position from LeftSide
                            double x1Position = filterSize == 5 ? -(maxLengthMaxBubble / 3) :
                                                filterSize == 4 ? -(maxLengthBubble / 3) :
                                                filterSize == 3 ? -(maxLengthBubble / 4) :
                                                filterSize == 2.5 ? -(maxLengthBubble / 5) :
                                                                    -(maxLengthBubble / 6);

                            return (x1Position, dynLength);
                        }
                        // X1 to Left / x2 to Middle
                        var (x1Position, dynLength_ToMiddle) = CalculateX1X2(maxLength_LeftSide);

                        // x2 from Middle to Right
                        var (_, dynLength_ToRight) = CalculateX1X2(maxLength_RightSide);

                        bool isPriceToAvoid = isPriceBased_Chart && avoidStretching;

                        DateTime x1 = xBar.AddMilliseconds(x1Position);
                        DateTime x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(isPriceToAvoid || gapWeekday ? 0 : dynLength_ToRight);

                        // Y-Value
                        double maxHeightBubble = heightPips * BubblesSizeMultiplier;
                        double proportionHeight = filterSize * maxHeightBubble;
                        double dynHeight = proportionHeight / 5;

                        double y1 = Bars.ClosePrices[iStart] + (Symbol.PipSize * dynHeight);
                        double y2 = Bars.ClosePrices[iStart] - (Symbol.PipSize * dynHeight);

                        // Draw
                        Color colorModeWithAlpha = Color.FromArgb((int)(2.55 * BubblesOpacity), colorMode.R, colorMode.G, colorMode.B);
                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Ellipse,
                            Id = $"{iStart}_Bubble",
                            X1 = x1,
                            Y1 = y1,
                            X2 = x2,
                            Y2 = y2,
                            Color = colorModeWithAlpha
                        });

                        if (ShowBubbleValue) {
                            string sumValueFmtd = deltaValue > 0 ? FormatBigNumber(deltaValue) : $"-{FormatBigNumber(Math.Abs(deltaValue))}";
                            string cumulDeltaFmtd = cumulDeltaValue > 0 ? FormatBigNumber(cumulDeltaValue) : $"-{FormatBigNumber(Math.Abs(cumulDeltaValue))}";
                            string subtValueFmtd = subtractDeltaValue > 0 ? FormatBigNumber(subtractDeltaValue) : $"-{FormatBigNumber(Math.Abs(subtractDeltaValue))}";

                            string dynBubbleValue = sourceIsDelta ? sumValueFmtd :
                                                    sourceIsCumul ? cumulDeltaFmtd :
                                                    subtValueFmtd;

                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_BubbleValue",
                                Text = dynBubbleValue,
                                X1 = xBar,
                                Y1 = Bars[iStart].Close,
                                horizontalAlignment = isPriceToAvoid ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                                verticalAlignment = VerticalAlignment.Center,
                                FontSize = FontSizeResults,
                                Color = RtnbFixedColor
                            });
                        }
                        if (ShowStrengthValue) {
                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_BubbleStrengthValue",
                                Text = $"{deltaStrength} \n {filterSize} ",
                                X1 = xBar,
                                Y1 = y2, // bottom of bubble
                                horizontalAlignment = isPriceToAvoid ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                                verticalAlignment = VerticalAlignment.Center,
                                FontSize = FontSizeNumbers,
                                Color = RtnbFixedColor
                            });
                        }

                        if (EnableUltraBubblesNotification && lastIsUltra && !lockUltraNotify && ultraNotify_NewBar) {
                            string symbolName = $"{Symbol.Name} ({Chart.TimeFrame.ShortName})";
                            string sourceString = sourceIsDelta ? "Delta" : sourceIsCumul ? "Cumulative Change Delta" : "Subtract Delta";
                            string popupText = $"{symbolName} => Ultra {sourceString} at {Server.Time}";
                            if (UltraBubbles_NotificationType_Input == NotificationType_Data.Sound) {
                                Notifications.PlaySound(UltraBubbles_SoundType);
                                lockUltraNotify = true;
                                ultraNotify_NewBar = false;
                            } else if (UltraBubbles_NotificationType_Input == NotificationType_Data.Popup) {
                                Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                lockUltraNotify = true;
                                ultraNotify_NewBar = false;
                            } else {
                                Notifications.PlaySound(UltraBubbles_SoundType);
                                Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                lockUltraNotify = true;
                                ultraNotify_NewBar = false;
                            }
                        }
                        // At the final loop when the bar is closed, if filterSize == 5, notify in the next bar.
                        // When Backtesting in Price-Based Charts, this condition doesn't seem to be triggered,
                        // Works fine in real-time market though.
                        if (filterSize == 5) {
                            lastIsUltra = true;
                            ultraNotify_NewBar = false;
                        }
                        else
                            lastIsUltra = false;

                        // === Ultra Bubbles Levels ====
                        if (ShowUltraBubblesLevels)
                        {
                            // Main logic By LLM
                            // Fixed and modified for the desired behavior
                            /*
                                The idea (count bars that pass or touch it to break it)
                                was made by human creativity => aka cheap copy of:
                                - Shved Supply and Demand indicator without (verified, untested, etc..) info.
                                Yes, I was a MT4 enjoyer.
                            */
                            bool TouchesRect(double o, double h, double l, double c, double top, double bottom)
                            {
                                UltraBubblesBreak_Data selectedBreak = UltraBubblesBreak_Input;
                                if (selectedBreak == UltraBubblesBreak_Data.Close_Only || selectedBreak == UltraBubblesBreak_Data.Close_plus_BarBody) {
                                    if (o >= bottom && o <= top)
                                        return true;

                                    if (selectedBreak == UltraBubblesBreak_Data.Close_plus_BarBody) {
                                        // If bar fully crosses rectangle (high above and low below)
                                        if (h > top && l < bottom)
                                            return true;
                                    }
                                }
                                else if (UltraBubblesBreak_Input == UltraBubblesBreak_Data.OHLC_plus_BarBody) {
                                    // If any OHLC inside rectangle
                                    if ((o >= bottom && o <= top) ||
                                        (h >= bottom && h <= top) ||
                                        (l >= bottom && l <= top) ||
                                        (c >= bottom && c <= top))
                                        return true;

                                    // If bar fully crosses rectangle (high above and low below)
                                    if (h > top && l < bottom)
                                        return true;
                                }

                                return false;
                            }

                            void CreateRect(double p1, double p2, int index, Color color)
                            {
                                ChartRectangle rectangle = Chart.DrawRectangle(
                                    $"{index}_UltraBubbleRectangle",
                                    Bars.OpenTimes[index],
                                    p1,
                                    Bars.OpenTimes[index + 1],
                                    p2,
                                    Color.FromArgb(80, color),
                                    1,
                                    LineStyle.Solid
                                );
                                rectangle.IsFilled = FillHist;

                                ChartText label = null;
                                if (UltraBubbles_ShowValue) {
                                    label = Chart.DrawText(
                                        $"{index}_UltraBubbleText",
                                        "0",
                                        Bars.OpenTimes[index],
                                        p2,
                                        Color.Yellow
                                    );
                                    label.HorizontalAlignment = HorizontalAlignment.Left;
                                    label.FontSize = FontSizeResults;
                                }

                                RectInfo rectangleInfo = new () {
                                    Rectangle = rectangle,
                                    Text = label,
                                    Touches = 0,
                                    Y1 = p1,
                                    Y2 = p2,
                                    isActive = true
                                };

                                if (ultraRectangles.ContainsKey(index))
                                    ultraRectangles[index] = rectangleInfo;
                                else
                                    ultraRectangles.Add(index, rectangleInfo);

                            }

                            void UpdateLabel(RectInfo rect, int index, double top)
                            {
                                rect.Text.Text = $"{rect.Touches}";
                                rect.Text.Time = Bars.OpenTimes[index];
                                rect.Text.Y = top;
                            }

                            // 'open' already declared.
                            double close = Bars.ClosePrices[iStart];
                            double high = Bars.HighPrices[iStart];
                            double low = Bars.LowPrices[iStart];

                            // Check touches for all active rectangles
                            if (!lockUltraLevels)
                            {
                                foreach (var rect in ultraRectangles.Values)
                                {
                                    if (!rect.isActive)
                                        continue;

                                    double top = Math.Max(rect.Y1, rect.Y2);
                                    double bottom = Math.Min(rect.Y1, rect.Y2);

                                    // Check OHLC one by one
                                    if (TouchesRect(open, high, low, close, top, bottom))
                                    {
                                        rect.Touches++;

                                        // Update label
                                        if (UltraBubbles_ShowValue)
                                            UpdateLabel(rect, iStart, top);

                                        if (rect.Touches >= UltraBubbles_MaxCount)
                                        {
                                            rect.isActive = false;

                                            // Stop extension → fix rectangle to current bar
                                            rect.Rectangle.Time2 = Bars.OpenTimes[iStart];
                                            rect.Rectangle.Color = Color.FromArgb(50, rect.Rectangle.Color);

                                            // Finalize label
                                            if (UltraBubbles_ShowValue) {
                                                rect.Text.Text = $"{rect.Touches}";
                                                rect.Text.Color = RtnbFixedColor;
                                            }
                                        }
                                    }
                                }

                                lockUltraLevels = true;
                            }

                            // Stretch
                            foreach (var rect in ultraRectangles.Values)
                            {
                                if (!rect.isActive)
                                    continue;
                                // Historical not desactivated yet;
                                if (UltraBubbles_ShowValue)
                                    rect.Text.Time = Bars.LastBar.OpenTime;

                                if (rect.Rectangle.Time2 == Bars.LastBar.OpenTime)
                                    continue;
                                rect.Rectangle.Time2 = Bars.LastBar.OpenTime;
                            }

                            // Create new rectangle for each Ultra Bubble
                            if (filterSize == 5) {
                                bool isUltraColor = UltraBubblesColoring_Input == UltraBubblesColoring_Data.Bubble_Color;

                                if (UltraBubbles_RectSizeInput == UltraBubbles_RectSizeData.High_Low)
                                    CreateRect(high, low, iStart, isUltraColor ? HeatmapUltra_Color : positiveNegativeColor);
                                else if (UltraBubbles_RectSizeInput == UltraBubbles_RectSizeData.HighOrLow_Close)
                                    CreateRect(close > open ? high : low, close, iStart, isUltraColor ? HeatmapUltra_Color : positiveNegativeColor);
                                else
                                    CreateRect(y1, y2, iStart, isUltraColor ? HeatmapUltra_Color : positiveNegativeColor);
                            }
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

                        double spikeFilterValue = 1;
                        if (UseCustomMAs) {
                            bool isStdDev = SpikeFilter_Input == SpikeFilter_Data.Standard_Deviation;
                            spikeFilterValue = CustomMAs(DynamicSeries[iStart], iStart, MAperiod_Spike, customMAtype_Spike, DeltaSwitch.Spike, isStdDev, MASwitch.Spike);
                        }
                        else {
                            spikeFilterValue = SpikeFilter_Input == SpikeFilter_Data.MA ?
                                MASpikeFilter.Result[iStart] : StdDevSpikeFilter.Result[iStart];
                        }

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
                        Color spikeHeatColor = rowStrength < SpikeLowest_Value ? SpikeLowest_Color :
                                            rowStrength < SpikeLow_Value ? SpikeLow_Color :
                                            rowStrength < SpikeAverage_Value ? SpikeAverage_Color :
                                            rowStrength < SpikeHigh_Value ? SpikeHigh_Color :
                                            rowStrength >= SpikeUltra_Value ? SpikeUltra_Color : SpikeUltra_Color;

                        Color spikeBySideColor = rowValue > 0 ? BuyColor : SellColor;

                        if (rowStrength > SpikeLow_Value || EnableSpikeChart)
                        {
                            double proportion_ToMiddle = 1 * proportion_LeftSide;
                            double dynLength_ToMiddle = proportion_ToMiddle / 1;

                            double proportion_ToRight = 1 * proportion_RightSide;
                            double dynLength_ToRight = proportion_ToRight / 1;

                            DateTime X1 = xBar.AddMilliseconds(-proportion_LeftSide);
                            DateTime X2 = X1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(
                                (avoidStretching && isPriceBased_Chart || gapWeekday) ? 0 : dynLength_ToRight
                            );

                            double Y1 = priceKey;
                            double Y2 = priceKey - rowHeight;

                            // For real-time - "repaint/update" the spike price level.
                            if (IsLastBar)
                                Chart.RemoveObject($"{iStart}_{i}_Spike");

                            if (SpikeView_Input == SpikeView_Data.Bubbles || EnableSpikeChart) {
                                Color spikeHeat_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeHeatColor.R, spikeHeatColor.G, spikeHeatColor.B);
                                Color SpikeBySide_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeBySideColor.R, spikeBySideColor.G, spikeBySideColor.B);
                                Color spikeChartColor = SpikeChartColoring_Input == SpikeChartColoring_Data.Heatmap ?
                                                        spikeHeat_WithOpacity : SpikeBySide_WithOpacity;


                                if (SpikeChartColoring_Input == SpikeChartColoring_Data.PlusMinus_Highlight_Heatmap)
                                    spikeChartColor = rowStrength > SpikeLow_Value ? spikeHeat_WithOpacity : spikeChartColor;

                                Color bubbleColor = !EnableSpikeChart ? spikeHeatColor : spikeChartColor;
                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Ellipse,
                                    Id = $"{iStart}_{i}_Spike",
                                    X1 = X1,
                                    Y1 = Y1,
                                    X2 = X2,
                                    Y2 = Y2,
                                    Color = bubbleColor
                                });
                            }
                            else {
                                DateTime positionX = VolumeView_Input == VolumeView_Data.Divided ? xBar : X2;
                                double positionY = (Y1 + Y2) / 2;
                                ChartIcon icon = Chart.DrawIcon($"{iStart}_{i}_Spike", IconView_Input, positionX, positionY, spikeHeatColor);
                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Icon,
                                    Id = $"{iStart}_{i}_Spike",
                                    IconType = IconView_Input,
                                    X1 = positionX,
                                    Y1 = positionY,
                                    Color = spikeHeatColor
                                });
                            }
                            if (EnableSpikeNotification && IsLastBar && !lockTickNotify && rowStrength > SpikeLow_Value) {
                                string symbolName = $"{Symbol.Name} ({Chart.TimeFrame.ShortName})";
                                string popupText = $"{symbolName} => Tick Spike at {Server.Time}";
                                if (Spike_NotificationType_Input == NotificationType_Data.Sound) {
                                    Notifications.PlaySound(Spike_SoundType);
                                    lockTickNotify = true;
                                } else if (Spike_NotificationType_Input == NotificationType_Data.Popup) {
                                    Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                    lockTickNotify = true;
                                } else {
                                    Notifications.PlaySound(Spike_SoundType);
                                    Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                    lockTickNotify = true;
                                }
                            }
                        }

                        if (ShowTickStrengthValue)
                        {
                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_{i}_TickStrengthValue",
                                Text = $"   <= {rowStrength}",
                                X1 = xBar,
                                Y1 = priceKey,
                                horizontalAlignment = HorizontalAlignment.Right,
                                verticalAlignment = VerticalAlignment.Bottom,
                                FontSize = FontSizeNumbers,
                                Color = RtnbFixedColor
                            });
                        }

                        // === Spike Levels ====
                        if (ShowSpikeLevels)
                        {
                            string spikeDictKey = $"{iStart}_{i}_SpikeLevel";

                            // For real-time - "repaint/update" the spike price level.
                            if (IsLastBar) {
                                try { Chart.RemoveObject($"{iStart}_{i}_SpikeLevelRectangle"); } catch { };
                                if (SpikeLevels_ShowValue) {
                                    try { Chart.RemoveObject($"{iStart}_{i}_SpikeLevelText"); } catch { };
                                }
                                spikeRectangles.Remove(spikeDictKey);
                            }

                            bool TouchesRect(double o, double h, double l, double c, double top, double bottom)
                            {
                                // If any OHLC inside rectangle
                                if ((o >= bottom && o <= top) ||
                                    (h >= bottom && h <= top) ||
                                    (l >= bottom && l <= top) ||
                                    (c >= bottom && c <= top))
                                    return true;

                                // If bar fully crosses rectangle (high above and low below)
                                if (h >= top && l <= bottom)
                                    return true;

                                return false;
                            }

                            void CreateRect(double p1, double p2, int index, Color color)
                            {
                                ChartRectangle rectangle = Chart.DrawRectangle(
                                    $"{index}_{i}_SpikeLevelRectangle",
                                    Bars.OpenTimes[index],
                                    p1,
                                    Bars.OpenTimes[index + 1],
                                    p2,
                                    Color.FromArgb(80, color),
                                    1,
                                    LineStyle.Solid
                                );
                                rectangle.IsFilled = FillHist;

                                ChartText label = null;
                                if (SpikeLevels_ShowValue) {
                                    label = Chart.DrawText(
                                        $"{index}_{i}_SpikeLevelText",
                                        "0",
                                        Bars.OpenTimes[index],
                                        (p1 + p2) / 2,
                                        Color.Yellow
                                    );
                                    label.HorizontalAlignment = HorizontalAlignment.Left;
                                    label.VerticalAlignment = VerticalAlignment.Center;
                                    label.FontSize = FontSizeResults;
                                }

                                RectInfo rectangleInfo = new () {
                                    Rectangle = rectangle,
                                    Text = label,
                                    Touches = 0,
                                    Y1 = p1,
                                    Y2 = p2,
                                    isActive = true,
                                    // Real-time Market
                                    // The current bar Spike Rectangle should not be used.
                                    LastBarIndex = !IsLastBar ? -1 : index,
                                };

                                if (spikeRectangles.ContainsKey(spikeDictKey))
                                    spikeRectangles[spikeDictKey] = rectangleInfo;
                                else
                                    spikeRectangles.Add(spikeDictKey, rectangleInfo);
                            }

                            void UpdateLabel(RectInfo rect, int index)
                            {
                                rect.Text.Text = $"{rect.Touches}";
                                rect.Text.Time = Bars.OpenTimes[index];
                            }

                            // 'open' already declared.
                            double close = Bars.ClosePrices[iStart];
                            double high = (isRenkoChart && ShowWicks) ?
                                            highest : Bars.HighPrices[iStart];
                            double low = (isRenkoChart && ShowWicks) ?
                                            lowest : Bars.LowPrices[iStart];

                            // Check touches for all active rectangles
                            // Historical Data || Real-time Market
                            // This one gave more headache than Ultra Bubbles
                            if (!lockSpikeLevels || IsLastBar)
                            {
                                foreach (var rect in spikeRectangles.Values)
                                {
                                    if (!rect.isActive)
                                        continue;

                                    // Avoid "touch counting" on the current bar rectangles
                                    if (rect.LastBarIndex == iStart && IsLastBar)
                                        continue;

                                    double top = Math.Max(rect.Y1, rect.Y2);
                                    double bottom = Math.Min(rect.Y1, rect.Y2);

                                    // Check OHLC one by one
                                    if (TouchesRect(open, high, low, close, top, bottom))
                                    {
                                        rect.Touches++;
                                        // Current forming bar already touched that rectangle.
                                        // So, lock it until a new LastBarIndex appear.
                                        rect.LastBarIndex = iStart;

                                        if (SpikeLevels_ShowValue)
                                            UpdateLabel(rect, iStart);

                                        if (rect.Touches >= SpikeLevels_MaxCount)
                                        {
                                            rect.isActive = false;

                                            // Stop extension → fix rectangle to current bar
                                            rect.Rectangle.Time2 = Bars.OpenTimes[iStart];
                                            rect.Rectangle.Color = Color.FromArgb(50, rect.Rectangle.Color);

                                            // Finalize label
                                            if (SpikeLevels_ShowValue) {
                                                rect.Text.Text = $"{rect.Touches}";
                                                rect.Text.Color = RtnbFixedColor;
                                            }
                                        }
                                    }
                                }

                                lockSpikeLevels = true;
                            }

                            // Stretch
                            foreach (var rect in spikeRectangles.Values)
                            {
                                if (!rect.isActive)
                                    continue;
                                // Historical not desactivated yet;
                                if (SpikeLevels_ShowValue)
                                    rect.Text.Time = Bars.LastBar.OpenTime;

                                if (rect.Rectangle.Time2 == Bars.LastBar.OpenTime)
                                    continue;
                                rect.Rectangle.Time2 = Bars.LastBar.OpenTime;
                            }

                            // Create new rectangle for each Tick Spike
                            if (rowStrength > SpikeLow_Value) {
                                Color spikeHeat_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeHeatColor.R, spikeHeatColor.G, spikeHeatColor.B);
                                Color SpikeBySide_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeBySideColor.R, spikeBySideColor.G, spikeBySideColor.B);
                                Color spikeLevelColor = SpikeLevelsColoring_Input == SpikeLevelsColoring_Data.Heatmap ?
                                                        spikeHeat_WithOpacity : SpikeBySide_WithOpacity;

                                double Y1 = priceKey;
                                double Y2 = priceKey - rowHeight;
                                CreateRect(Y1, Y2, iStart, spikeLevelColor);
                            }
                        }
                    }
                }

                loopPrevSegment = Segments_Bar[i];
            }
        }

        private double CustomMAs(double seriesValue, int index, int maPeriod,
                                 MAType_Data maType, DeltaSwitch deltaSwitch = DeltaSwitch.None,
                                 bool isStdDev = false, MASwitch maSwitch = MASwitch.Large
                                ) {
            switch (deltaSwitch)
            {
                case DeltaSwitch.Subtract:
                    if (!_deltaBuffer.Subtract.ContainsKey(index))
                        _deltaBuffer.Subtract.Add(index, seriesValue);
                    else
                        _deltaBuffer.Subtract[index] = seriesValue;
                    break;
                case DeltaSwitch.CumulDelta:
                    if (!_deltaBuffer.CumulDelta.ContainsKey(index))
                        _deltaBuffer.CumulDelta.Add(index, seriesValue);
                    else
                        _deltaBuffer.CumulDelta[index] = seriesValue;
                    break;
                default:
                    if (!_dynamicBuffer.ContainsKey(index))
                        _dynamicBuffer.Add(index, seriesValue);
                    else
                        _dynamicBuffer[index] = seriesValue;
                    break;
            }
            Dictionary<int, double> buffer = deltaSwitch == DeltaSwitch.None ? _dynamicBuffer :
                deltaSwitch == DeltaSwitch.Subtract ? _deltaBuffer.Subtract :
                deltaSwitch == DeltaSwitch.CumulDelta ? _deltaBuffer.CumulDelta : _dynamicBuffer;

            Dictionary<int, double> prevMA_Dict = _maDynamic;
            switch (maSwitch) {
                case MASwitch.Bubbles:
                    if (deltaSwitch == DeltaSwitch.Subtract)
                        prevMA_Dict = _deltaBuffer.MASubtract_Bubbles;
                    else if (deltaSwitch == DeltaSwitch.CumulDelta)
                        prevMA_Dict = _deltaBuffer.MACumulDelta_Bubbles;
                    break;
                case MASwitch.Spike:
                    prevMA_Dict = _deltaBuffer.MASpike;
                    break;
                default:
                    if (deltaSwitch == DeltaSwitch.Subtract)
                        prevMA_Dict = _deltaBuffer.MASubtract_Large;
                    break;
            }

            double maValue = maType switch
            {
                MAType_Data.Simple => SMA(index, maPeriod, buffer),
                MAType_Data.Exponential => EMA(index, maPeriod, buffer, prevMA_Dict),
                MAType_Data.Weighted => WMA(index, maPeriod, buffer),
                MAType_Data.Triangular => TMA(index, maPeriod, buffer),
                MAType_Data.Hull => Hull(index, maPeriod, buffer),
                MAType_Data.VIDYA => VIDYA(index, maPeriod, buffer, prevMA_Dict),
                MAType_Data.WilderSmoothing => Wilder(index, maPeriod, buffer, prevMA_Dict),
                MAType_Data.KaufmanAdaptive => KAMA(index, maPeriod, 2, 30, buffer, prevMA_Dict),
                _ => double.NaN
            };

            return isStdDev ? StdDev(index, maPeriod, maValue, buffer) : maValue;
        }
        //  ===== CUSTOM MAS ====
        // MAs logic generated by LLM
        // Modified to handle multiples sources
        // as well as specific OrderFlow() needs.
        private static double StdDev(int index, int Period, double maValue, Dictionary<int, double> buffer)
        {
            double mean = maValue;
            double sumSq = 0.0;
            for (int i = index - Period + 1; i <= index; i++)
            {
                try {
                    double diff = buffer[i] - mean;
                    sumSq += diff * diff;
                } catch {}
            }
            // Sample => (Period - 1) / Population => Period
            return (Period > 1) ? Math.Sqrt(sumSq / (Period - 1)) : 0.0;
        }

        private static double SMA(int index, int period, Dictionary<int, double> buffer)
        {
            if (buffer.Count < period)
                return double.NaN;

            double sum = 0;
            for (int i = index; i > index - period; i--) {
                // The index may jump on Sunday Bars
                try { sum += buffer[i]; } catch { }
            }
            return sum / period;
        }
        private static double EMA(int index, int period, Dictionary<int, double> buffer, Dictionary<int, double> emaDict)
        {
            if (emaDict.Count == 0) {
                emaDict[0] = buffer[index];
                emaDict[1] = buffer[index];
                emaDict[index] = buffer[index];
                return buffer[index];
            }
            double k = 2.0 / (period + 1);
            double value = buffer[index] * k + emaDict[0] * (1 - k);

            if (index != emaDict.Keys.LastOrDefault()) {
                // Always 3
                double prev = emaDict[1];
                emaDict.Clear();
                emaDict[0] = prev;
                emaDict[1] = value;
                emaDict[index] = value; // just to be identified
            } else {
                emaDict[1] = value;
                emaDict[index] = value;
            }
            return value;
        }
        private static double WMA(int index, int period, Dictionary<int, double> buffer, double? overrideLast = null)
        {
            if (buffer.Count < period)
            {
                // not enough values -> average available
                /*
                double sumA = 0;
                for (int i = 0; i <= index; i++) {
                    try { sumA += buffer[i]; } catch { }
                }
                return sumA / available;
                */
                return double.NaN;
            }

            double numerator = 0;
            double denominator = 0;
            int w = 1;
            int start = index - period + 1;
            for (int i = start; i <= index; i++, w++)
            {
                double v = 0;
                try { v = (i == index && overrideLast.HasValue) ? overrideLast.Value : buffer[i]; } catch { }
                numerator += v * w;
                denominator += w;
            }
            return numerator / denominator;
        }
        private static double TMA(int index, int period, Dictionary<int, double> buffer)
        {
            if (period <= 1)
                return buffer[index];

            // need at least 2*period - 1 samples to compute full triangular, otherwise fallback
            if (buffer.Count < 2 * period - 2)
                return double.NaN; // return SMA(index, period, buffer);

            double sumSma = 0.0;
            for (int k = index - period + 1; k <= index; k++)
            {
                double smaK = SMA(k, period, buffer);
                sumSma += smaK;
            }
            return sumSma / period;
        }
        private static double Hull(int index, int period, Dictionary<int, double> buffer)
        {
            if (period < 2) return buffer[index];

            int half = Math.Max(1, period / 2);
            int sqrt = Math.Max(1, (int)Math.Round(Math.Sqrt(period)));

            double wmaHalf = WMA(index, half, buffer);
            double wmaFull = WMA(index, period, buffer);

            double raw = 2 * wmaHalf - wmaFull;
            return WMA(index, sqrt, buffer, raw);
        }
        private static double Wilder(int index, int period, Dictionary<int, double> buffer, Dictionary<int, double> wilderDict)
        {
            if (wilderDict.Count == 0) {
                wilderDict[0] = buffer[index];
                wilderDict[1] = buffer[index];
                wilderDict[index] = buffer[index];
                return buffer[index];
            }
            double value =  (wilderDict[0] * (period - 1) + buffer[index]) / period;

            if (index != wilderDict.Keys.LastOrDefault()) {
                // Always 3
                double prev = wilderDict[1];
                wilderDict.Clear();
                wilderDict[0] = prev;
                wilderDict[1] = value;
                wilderDict[index] = value; // just to be identified
            } else {
                wilderDict[1] = value;
                wilderDict[index] = value;
            }
            return value;
        }
        private static double KAMA(int index, int period, int fast, int slow, Dictionary<int, double> buffer, Dictionary<int, double> kamaDict)
        {
            if (kamaDict.Count == 0) {
                kamaDict[0] = buffer[index];
                kamaDict[1] = buffer[index];
                kamaDict[index] = buffer[index];
                return buffer[index];
            }
            if (buffer.Count < period) return SMA(index, period, buffer);

            double change;
            try { change = Math.Abs(buffer[index] - buffer[index - period]); }
            catch {
                int idxValue = index - period;
                for (int i = idxValue; i < index; i++) {
                    idxValue = i;
                    if (buffer.ContainsKey(i)) break;
                }
                change = Math.Abs(buffer[index] - buffer[idxValue]);
            }

            double volatility = 0.0;
            for (int i = index - period + 1; i <= index; i++) {
                try { volatility += Math.Abs(buffer[i] - buffer[i - 1]); } catch { }
            }

            double er = volatility == 0 ? 0 : change / volatility;
            double fastSC = 2.0 / (fast + 1);
            double slowSC = 2.0 / (slow + 1);
            double sc = Math.Pow(er * (fastSC - slowSC) + slowSC, 2);

            double value = kamaDict[0] + sc * (buffer[index] - kamaDict[0]);

            if (index != kamaDict.Keys.LastOrDefault()) {
                // Always 3
                double prev = kamaDict[1];
                kamaDict.Clear();
                kamaDict[0] = prev;
                kamaDict[1] = value;
                kamaDict[index] = value; // just to be identified
            } else {
                kamaDict[1] = value;
                kamaDict[index] = value;
            }
            return value;
        }
        private static double VIDYA(int index, int period, Dictionary<int, double> buffer, Dictionary<int, double> vidyaDict)
        {
            if (vidyaDict.Count == 0) {
                vidyaDict[0] = buffer[index];
                vidyaDict[1] = buffer[index];
                vidyaDict[index] = buffer[index];
                return buffer[index];
            }

            double cmo = CMO(index, period, buffer);
            // scale factor, tuneable; using 0.2 base as example
            // cTrader uses 0.65 as default
            double alphaBase = 0.65;
            double k = alphaBase * Math.Abs(cmo / 100.0);
            double value = k * buffer[index] + (1 - k) * vidyaDict[0];

            if (index != vidyaDict.Keys.LastOrDefault()) {
                // Always 3
                double prev = vidyaDict[1];
                vidyaDict.Clear();
                vidyaDict[0] = prev;
                vidyaDict[1] = value;
                vidyaDict[index] = value; // just to be identified
            } else {
                vidyaDict[1] = value;
                vidyaDict[index] = value;
            }
            return value;
        }
        private static double CMO(int index, int length, Dictionary<int, double> buffer)
        {
            if (index < 1 || length < 1) return 0.0;
            int start = Math.Max(1, index - length + 1);
            double up = 0, down = 0;
            for (int i = start; i <= index; i++)
            {
                try {
                    double diff = buffer[i] - buffer[i - 1];
                    if (diff > 0) up += diff;
                    else down += -diff;
                } catch { }
            }
            double denom = up + down;
            return denom == 0 ? 0.0 : 100.0 * (up - down) / denom;
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
                    segmentsDict.Add(startKey, Segments_VP);
                else
                    segmentsDict[startKey] = Segments_VP;
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

                Segments_VP = currentSegments.OrderBy(x => x).ToList();
            }
        }
        private int GetSegmentIndex(int index) {
            return SegmentsInterval_Input switch
            {
                SegmentsInterval_Data.Monthly => MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]),
                SegmentsInterval_Data.Weekly => WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]),
                _ => DailyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index])
            };
        }

        // *********** VOLUME PROFILE TICKS ***********
        private void VolumeProfile(int iStart, int index, ExtraProfiles extraProfiles = ExtraProfiles.No, bool isLoop = false, bool drawOnly = false, string fixedKey = "", double fixedLowest = 0)
        {
            // Weekly/Monthly on Buy_Sell is a waste of time
            if (VolumeMode_Input == VolumeMode_Data.Buy_Sell && (extraProfiles == ExtraProfiles.Weekly || extraProfiles == ExtraProfiles.Monthly))
               return;

            // ==== VP ====
            if (!drawOnly)
                VP_Tick(index, true, extraProfiles, fixedKey);

            // ==== Drawing ====
            if (Segments_VP.Count == 0 || isLoop)
                return;

            // For Results
            Bars mainTF = ODFInterval_Input == ODFInterval_Data.Daily ? DailyBars :
                           ODFInterval_Input == ODFInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            Bars TF_Bars = extraProfiles == ExtraProfiles.No ? mainTF:
                           extraProfiles == ExtraProfiles.MiniVP ? MiniVPs_Bars :
                           extraProfiles == ExtraProfiles.Weekly ? WeeklyBars : MonthlyBars;

            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            double lowest = TF_Bars.LowPrices[TF_idx];

            // Mini VPs avoid crash after recalculating
            if (double.IsNaN(lowest))
                lowest = TF_Bars.LowPrices.LastValue;


            bool gapWeekend = Bars.OpenTimes[iStart].DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart].Hour < 2;
            DateTime x1_Start = Bars.OpenTimes[iStart + (gapWeekend ? 1 : 0)];
            DateTime xBar = Bars.OpenTimes[index];

            bool isIntraday = ShowIntradayProfile && index == Chart.LastVisibleBarIndex && !isLoop;
            bool histRightSide = HistogramSide_Input == HistSide_Data.Right;

            // Any Volume Mode
            double maxLength = xBar.Subtract(x1_Start).TotalMilliseconds;

            HistWidth_Data selectedWidth = HistogramWidth_Input;
            double maxWidth = selectedWidth == HistWidth_Data._15 ? 1.25 :
                              selectedWidth == HistWidth_Data._30 ? 1.50 :
                              selectedWidth == HistWidth_Data._50 ? 2 : 3;
            double maxHalfWidth = selectedWidth == HistWidth_Data._15 ? 1.12 :
                                  selectedWidth == HistWidth_Data._30 ? 1.25 :
                                  selectedWidth == HistWidth_Data._50 ? 1.40 : 1.75;

            double proportion_VP = maxLength - (maxLength / maxWidth);
            if (selectedWidth == HistWidth_Data._100)
                proportion_VP = maxLength;

            string prefix = extraProfiles == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";
            double y1_lowest = extraProfiles == ExtraProfiles.Fixed ? fixedLowest : lowest;

            int segmentIdx = extraProfiles == ExtraProfiles.Fixed ? GetSegmentIndex(index) : index;
            List<double> whichSegment = extraProfiles == ExtraProfiles.Fixed ? segmentsDict[segmentIdx] : Segments_VP;

            // Manual Refactoring.
            // LLM allucinates.
            for (int i = 0; i < whichSegment.Count; i++)
            {
                double priceKey = whichSegment[i];

                bool skip = extraProfiles switch
                {
                    ExtraProfiles.Monthly => !MonthlyRank.Normal.ContainsKey(priceKey),
                    ExtraProfiles.Weekly => !WeeklyRank.Normal.ContainsKey(priceKey),
                    ExtraProfiles.MiniVP => !MiniRank.Normal.ContainsKey(priceKey),
                    ExtraProfiles.Fixed => !FixedRank[fixedKey].Normal.ContainsKey(priceKey),
                    _ => !VP_VolumesRank.ContainsKey(priceKey),
                };
                if (skip)
                    continue;

                /*
                Indeed, the value of X-Axis is simply a rule of three,
                where the maximum value will be the maxLength (in Milliseconds),
                from there the math adjusts the histograms.
                    MaxValue    maxLength(ms)
                       x             ?(ms)
                The values 1.25 and 4 are the manually set values
                ===================
                NEW IN ODF_AGG => To avoid histograms unexpected behavior that occurs in historical data
                - on Price-Based Charts (sometimes in candles too) where interval goes through weekend
                  We'll skip 1 bar (friday) since Bar Index as X-axis didn't resolve the problem.
                */

                double lowerSegmentY1 = whichSegment[i] - rowHeight;
                double upperSegmentY2 = whichSegment[i];

                double y1_text = priceKey;

                void DrawRectangle_Normal(double currentVolume, double maxVolume, bool intradayProfile = false)
                {
                    double proportion = currentVolume * proportion_VP;
                    double dynLength = proportion / maxVolume;

                    DateTime x2 = x1_Start.AddMilliseconds(dynLength);

                    Color histogramColor = extraProfiles switch
                    {
                        ExtraProfiles.Monthly => MonthlyColor,
                        ExtraProfiles.Weekly => WeeklyColor,
                        _ => VolumeColor,
                    };

                    ChartRectangle volHist = Chart.DrawRectangle($"{prefix}_{i}_VP_{extraProfiles}_Normal", x1_Start, lowerSegmentY1, x2, upperSegmentY2, histogramColor);

                    if (FillHist_VP)
                        volHist.IsFilled = true;

                    if (histRightSide)
                    {
                        volHist.Time1 = xBar;
                        volHist.Time2 = xBar.AddMilliseconds(-dynLength);
                    }

                    if (ShowHistoricalNumbers_VP) {
                        double volumeNumber = currentVolume;
                        string volumeNumberFmtd = FormatNumbers ? FormatBigNumber(volumeNumber) : $"{volumeNumber}";

                        ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Normal", volumeNumberFmtd, histRightSide ? xBar : x1_Start, y1_text, RtnbFixedColor);
                        Center.HorizontalAlignment = histRightSide ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                        Center.FontSize = FontSizeNumbers;

                        if (HistogramSide_Input == HistSide_Data.Right)
                            Center.Time = xBar;
                    }

                    if (intradayProfile && extraProfiles != ExtraProfiles.MiniVP)
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
                            }
                            else {
                                // Use Weekly position
                                volHist.Time1 = dateOffset_Duo;
                                volHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Intraday);
                                if (FillIntradaySpace) {
                                    volHist.Time1 = dateOffset;
                                    volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                                }
                            }
                        }

                        if (ShowIntradayNumbers) {
                            double volumeNumber = currentVolume;
                            string volumeNumberFmtd = FormatNumbers ? FormatBigNumber(volumeNumber) : $"{volumeNumber}";

                            ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Normal",
                                volumeNumberFmtd, volHist.Time1, y1_text, RtnbFixedColor);
                            Center.FontSize = FontSizeResults;

                            Center.HorizontalAlignment = HorizontalAlignment.Left;
                            if (extraProfiles == ExtraProfiles.Weekly) {
                                if (!EnableMonthlyProfile && FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                            if (extraProfiles == ExtraProfiles.Monthly) {
                                if (FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                        }
                    }
                }

                void DrawRectangle_BuySell(
                    double currentBuy, double currentSell,
                    double buyMax, double sellMax,
                    bool intradayProfile = false)
                {
                    // Buy vs Sell - already
                    double maxBuyVolume = buyMax;
                    double maxSellVolume = sellMax;

                    double maxSideVolume = maxBuyVolume > maxSellVolume ? maxBuyVolume : maxSellVolume;

                    double proportionBuy = 0;
                    try { proportionBuy = currentBuy * (maxLength - (maxLength / maxHalfWidth)); } catch { };
                    if (selectedWidth == HistWidth_Data._100)
                        try { proportionBuy = currentBuy * (maxLength - (maxLength / 3)); } catch { };

                    double dynLengthBuy = proportionBuy / maxSideVolume; ;

                    double proportionSell = 0;
                    try { proportionSell = currentSell * proportion_VP; } catch { };
                    double dynLengthSell = proportionSell / maxSideVolume;

                    DateTime x2_Sell = x1_Start.AddMilliseconds(dynLengthSell);
                    DateTime x2_Buy = x1_Start.AddMilliseconds(dynLengthBuy);

                    ChartRectangle buyHist, sellHist;
                    sellHist = Chart.DrawRectangle($"{prefix}_{i}_VP_{extraProfiles}_Sell", x1_Start, lowerSegmentY1, x2_Sell, upperSegmentY2, SellColor);
                    buyHist = Chart.DrawRectangle($"{prefix}_{i}_VP_{extraProfiles}_Buy", x1_Start, lowerSegmentY1, x2_Buy, upperSegmentY2, BuyColor);
                    if (FillHist_VP)
                    {
                        buyHist.IsFilled = true;
                        sellHist.IsFilled = true;
                    }
                    if (HistogramSide_Input == HistSide_Data.Right)
                    {
                        sellHist.Time1 = xBar;
                        sellHist.Time2 = xBar.AddMilliseconds(-dynLengthSell);
                        buyHist.Time1 = xBar;
                        buyHist.Time2 = xBar.AddMilliseconds(-dynLengthBuy);
                    }

                    if (ShowHistoricalNumbers_VP) {
                        double buyNumber = currentBuy;
                        string buyNumberFmtd = FormatNumbers ? FormatBigNumber(buyNumber) : $"{buyNumber}";
                        double sellNumber = currentSell;
                        string sellNumberFmtd = FormatNumbers ? FormatBigNumber(sellNumber) : $"{sellNumber}";

                        ChartText Left, Right;
                        Left = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Sell", sellNumberFmtd, x1_Start, y1_text, RtnbFixedColor);
                        Right = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Buy", buyNumberFmtd, x1_Start, y1_text, RtnbFixedColor);

                        Left.HorizontalAlignment = HorizontalAlignment.Left;
                        Right.HorizontalAlignment = HorizontalAlignment.Right;

                        Left.FontSize = FontSizeNumbers;
                        Right.FontSize = FontSizeNumbers;

                        if (HistogramSide_Input == HistSide_Data.Right) {
                            Left.Time = xBar;
                            Right.Time = xBar;
                        }
                    }

                    // Intraday Right Profile
                    if (intradayProfile && extraProfiles != ExtraProfiles.MiniVP)
                    {
                        // ==== Subtract Profile / Plain Delta - Profile View ====
                        // Recalculate histograms 'X' position
                        DateTime dateOffset_Subt = TimeBasedOffset(xBar);

                        double maxPositive = VP_VolumesRank_Subt.Values.Max();
                        IEnumerable<double> negativeVolumeList = VP_VolumesRank_Subt.Values.Where(n => n < 0);
                        double maxNegative = 0;
                        try { maxNegative = Math.Abs(negativeVolumeList.Min()); } catch { }

                        double subtMax = maxPositive > maxNegative ? maxPositive : maxNegative;

                        double maxLength_Intraday = dateOffset_Subt.Subtract(xBar).TotalMilliseconds;
                        double proportion_Intraday = VP_VolumesRank_Subt[priceKey] * (maxLength_Intraday - (maxLength_Intraday / maxWidth));
                        double dynLength = proportion_Intraday / subtMax;

                        // Set 'X'
                        DateTime x1 = dateOffset_Subt;
                        DateTime x2 = x1.AddMilliseconds(dynLength);

                        Color colorHist = dynLength > 0 ? BuyColor : SellColor;
                        ChartRectangle subtHist = Chart.DrawRectangle($"{iStart}_{i}_VP_Subt", x1, lowerSegmentY1, x2, upperSegmentY2, colorHist);

                        dynLength = -Math.Abs(dynLength);
                        subtHist.Time1 = dateOffset_Subt;
                        subtHist.Time2 = subtHist.Time2 != dateOffset_Subt ? dateOffset_Subt.AddMilliseconds(dynLength) : dateOffset_Subt;

                        if (FillHist_VP)
                            subtHist.IsFilled = true;

                        if (ShowIntradayNumbers) {
                            double volumeNumber = VP_VolumesRank_Subt[priceKey];
                            string volumeNumberFmtd = volumeNumber > 0 ? FormatBigNumber(volumeNumber) : $"-{FormatBigNumber(Math.Abs(volumeNumber))}";

                            ChartText Center;
                            Center = Chart.DrawText($"{iStart}_{i}_VP_Number_Subt", volumeNumberFmtd, x1, y1_text, RtnbFixedColor);
                            Center.HorizontalAlignment = HorizontalAlignment.Left;
                            Center.FontSize = FontSizeResults;
                        }

                        // ==== Buy_Sell - Divided View - Half Width ====
                        // Recalculate histograms 'X' position
                        DateTime dateOffset = TimeBasedOffset(dateOffset_Subt, true);
                        maxLength_Intraday = dateOffset.Subtract(dateOffset_Subt).TotalMilliseconds;

                        // Replaced maxHalfWidth to maxWidth since it's Divided View
                        proportionBuy = 0;
                        try { proportionBuy = currentBuy * (maxLength_Intraday - (maxLength_Intraday / maxHalfWidth)); } catch { };
                        if (selectedWidth == HistWidth_Data._100)
                            try { proportionBuy = currentBuy * maxLength_Intraday; } catch { };

                        dynLengthBuy = proportionBuy / maxBuyVolume; ;

                        proportionSell = 0;
                        try { proportionSell = currentSell * (maxLength_Intraday - (maxLength_Intraday / maxHalfWidth)); } catch { };
                        if (selectedWidth == HistWidth_Data._100)
                            try { proportionSell = currentSell * maxLength_Intraday; } catch { };

                        dynLengthSell = proportionSell / maxSellVolume;

                        // Set 'X'
                        sellHist.Time1 = dateOffset;
                        sellHist.Time2 = dateOffset.AddMilliseconds(-dynLengthSell);
                        buyHist.Time1 = dateOffset;
                        buyHist.Time2 = dateOffset.AddMilliseconds(dynLengthBuy);

                        if (ShowIntradayNumbers) {
                            double buyNumber = currentBuy;
                            string buyNumberFmtd = FormatNumbers ? FormatBigNumber(buyNumber) : $"{buyNumber}";
                            double sellNumber = currentSell;
                            string sellNumberFmtd = FormatNumbers ? FormatBigNumber(sellNumber) : $"{sellNumber}";

                            ChartText Left, Right;
                            Left = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Sell", sellNumberFmtd, dateOffset, y1_text, RtnbFixedColor);
                            Right = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Buy", buyNumberFmtd, dateOffset, y1_text, RtnbFixedColor);

                            Left.HorizontalAlignment = HorizontalAlignment.Left;
                            Right.HorizontalAlignment = HorizontalAlignment.Right;

                            Left.FontSize = FontSizeResults;
                            Right.FontSize = FontSizeResults;
                        }
                    }
                }

                void DrawRectangle_Delta(double currentDelta, double positiveDeltaMax, IEnumerable<double> negativeDeltaList, bool intradayProfile = false)
                {
                    double negativeDeltaMax = 0;
                    try { negativeDeltaMax = Math.Abs(negativeDeltaList.Min()); } catch { }

                    double deltaMax = positiveDeltaMax > negativeDeltaMax ? positiveDeltaMax : negativeDeltaMax;

                    double proportion_Delta = Math.Abs(currentDelta) * proportion_VP;
                    double dynLength_Delta = proportion_Delta / deltaMax;

                    Color colorHist = currentDelta >= 0 ? BuyColor : SellColor;
                    DateTime x2 = x1_Start.AddMilliseconds(dynLength_Delta);

                    ChartRectangle deltaHist = Chart.DrawRectangle($"{prefix}_{i}_VP_{extraProfiles}_Delta", x1_Start, lowerSegmentY1, x2, upperSegmentY2, colorHist);

                    if (FillHist_VP)
                        deltaHist.IsFilled = true;

                    if (HistogramSide_Input == HistSide_Data.Right)
                    {
                        deltaHist.Time1 = xBar;
                        deltaHist.Time2 = deltaHist.Time2 != x1_Start ? xBar.AddMilliseconds(-dynLength_Delta) : x1_Start;
                    }

                    if (ShowHistoricalNumbers_VP) {
                        double deltaNumber = currentDelta;
                        string deltaNumberFmtd = deltaNumber > 0 ? FormatBigNumber(deltaNumber) : $"-{FormatBigNumber(Math.Abs(deltaNumber))}";
                        string deltaString = FormatNumbers ? deltaNumberFmtd : $"{deltaNumber}";

                        ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Delta", deltaString, x1_Start, y1_text, RtnbFixedColor);
                        Center.HorizontalAlignment = histRightSide ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                        Center.FontSize = FontSizeNumbers;

                        if (HistogramSide_Input == HistSide_Data.Right)
                            Center.Time = xBar;
                    }

                    // Intraday Right Profile
                    if (intradayProfile && extraProfiles != ExtraProfiles.MiniVP)
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
                        proportion_Delta = currentDelta * (maxLength_Intraday - (maxLength_Intraday / maxWidth));
                        if (selectedWidth == HistWidth_Data._100)
                            proportion_Delta = currentDelta * maxLength_Intraday;
                        dynLength_Delta = proportion_Delta / deltaMax;

                        colorHist = dynLength_Delta > 0 ? BuyColor : SellColor;
                        dynLength_Delta = Math.Abs(dynLength_Delta); // Profile view only

                        // Set 'X'
                        deltaHist.Time1 = dateOffset;
                        deltaHist.Time2 = deltaHist.Time2 != dateOffset ? dateOffset.AddMilliseconds(-dynLength_Delta) : dateOffset;
                        deltaHist.Color = colorHist;

                        if (extraProfiles == ExtraProfiles.Weekly) {
                            deltaHist.Time1 = dateOffset_Duo;
                            deltaHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Delta);
                            if (!EnableMonthlyProfile && FillIntradaySpace) {
                                deltaHist.Time1 = dateOffset;
                                deltaHist.Time2 = dateOffset.AddMilliseconds(dynLength_Delta);
                            }
                        }

                        if (extraProfiles == ExtraProfiles.Monthly) {
                            if (EnableWeeklyProfile) {
                                // Show after
                                deltaHist.Time1 = dateOffset_Triple;
                                deltaHist.Time2 = dateOffset_Triple.AddMilliseconds(-dynLength_Delta);
                                // Show after together
                                if (FillIntradaySpace) {
                                    deltaHist.Time1 = dateOffset_Duo;
                                    deltaHist.Time2 = dateOffset_Duo.AddMilliseconds(dynLength_Delta);
                                }
                            }
                            else {
                                // Use Weekly position
                                deltaHist.Time1 = dateOffset_Duo;
                                deltaHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Delta);
                                if (FillIntradaySpace) {
                                    deltaHist.Time1 = dateOffset;
                                    deltaHist.Time2 = dateOffset.AddMilliseconds(dynLength_Delta);
                                }
                            }
                        }

                        if (ShowIntradayNumbers) {
                            double deltaNumber = currentDelta;
                            string deltaNumberFmtd = deltaNumber > 0 ? FormatBigNumber(deltaNumber) : $"-{FormatBigNumber(Math.Abs(deltaNumber))}";
                            string deltaString = FormatNumbers ? deltaNumberFmtd : $"{deltaNumber}";

                            ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Delta", deltaString, deltaHist.Time1, y1_text, RtnbFixedColor);
                            Center.FontSize = FontSizeResults;

                            Center.HorizontalAlignment = HorizontalAlignment.Left;
                            if (extraProfiles == ExtraProfiles.Weekly) {
                                if (!EnableMonthlyProfile && FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                            if (extraProfiles == ExtraProfiles.Monthly) {
                                if (FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                        }
                    }
                }

                if (VolumeMode_Input == VolumeMode_Data.Normal)
                {
                    IDictionary<double, double> vpNormal = extraProfiles switch
                    {
                        ExtraProfiles.Monthly => MonthlyRank.Normal,
                        ExtraProfiles.Weekly => WeeklyRank.Normal,
                        ExtraProfiles.MiniVP => MiniRank.Normal,
                        ExtraProfiles.Fixed => FixedRank[fixedKey].Normal,
                        _ => VP_VolumesRank
                    };

                    bool intraBool = extraProfiles switch
                    {
                        ExtraProfiles.Monthly => isIntraday,
                        ExtraProfiles.Weekly => isIntraday,
                        ExtraProfiles.MiniVP => false,
                        ExtraProfiles.Fixed => false,
                        _ => isIntraday
                    };

                    double value = vpNormal[priceKey];
                    double maxValue = vpNormal.Values.Max();

                    DrawRectangle_Normal(value, maxValue, intraBool);

                    if (ShowResults || ShowMiniResults)
                    {
                        if (extraProfiles == ExtraProfiles.MiniVP && !ShowMiniResults)
                            continue;
                        if (extraProfiles != ExtraProfiles.MiniVP && !ShowResults && extraProfiles != ExtraProfiles.Fixed)
                            continue;

                        double sum = Math.Round(vpNormal.Values.Sum());
                        string strValue = FormatResults ? FormatBigNumber(sum) : $"{sum}";

                        ChartText Center = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Normal_Result", $"\n{strValue}", x1_Start, y1_lowest, VolumeColor);
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
                else if (VolumeMode_Input == VolumeMode_Data.Buy_Sell)
                {
                    IDictionary<double, double> vpBuy = extraProfiles switch
                    {
                        ExtraProfiles.MiniVP => MiniRank.Up,
                        ExtraProfiles.Fixed => FixedRank[fixedKey].Up,
                        _ => VP_VolumesRank_Up
                    };
                    IDictionary<double, double> vpSell = extraProfiles switch
                    {
                        ExtraProfiles.MiniVP => MiniRank.Down,
                        ExtraProfiles.Fixed => FixedRank[fixedKey].Down,
                        _ => VP_VolumesRank_Down
                    };

                    double buyMax = 0;
                    try { buyMax = vpBuy.Values.Max(); } catch { }
                    double sellMax = 0;
                    try { sellMax = vpSell.Values.Max(); } catch { }

                    if (vpBuy.ContainsKey(priceKey) && vpSell.ContainsKey(priceKey))
                        DrawRectangle_BuySell(vpBuy[priceKey], vpSell[priceKey], buyMax, sellMax, isIntraday);

                    if (ShowResults || ShowMiniResults)
                    {
                        if (extraProfiles == ExtraProfiles.MiniVP && !ShowMiniResults)
                            continue;
                        if (extraProfiles != ExtraProfiles.MiniVP && !ShowResults && extraProfiles != ExtraProfiles.Fixed)
                            continue;

                        double volBuy = vpBuy.Values.Sum();
                        double volSell = vpSell.Values.Sum();

                        double percentBuy = (volBuy * 100) / (volBuy + volSell);
                        double percentSell = (volSell * 100) / (volBuy + volSell);
                        percentBuy = Math.Round(percentBuy);
                        percentSell = Math.Round(percentSell);

                        ChartText Left, Right;
                        Left = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Sell_Sum", $"{percentSell}%", x1_Start, y1_lowest, SellColor);
                        Right = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Buy_Sum", $"{percentBuy}%", x1_Start, y1_lowest, BuyColor);
                        Left.HorizontalAlignment = HorizontalAlignment.Left;
                        Right.HorizontalAlignment = HorizontalAlignment.Right;
                        Left.FontSize = FontSizeResults;
                        Right.FontSize = FontSizeResults;

                        ChartText Center;
                        double sum = Math.Round(volBuy + volSell);
                        double subtract = Math.Round(volBuy - volSell);
                        double divide = 0;
                        if (volBuy != 0 && volSell != 0)
                            divide = Math.Round(volBuy / volSell, 3);

                        string sumFmtd = FormatResults ? FormatBigNumber(sum) : $"{sum}";
                        string subtractValueFmtd = subtract > 0 ? FormatBigNumber(subtract) : $"-{FormatBigNumber(Math.Abs(subtract))}";
                        string subtractFmtd = FormatResults ? subtractValueFmtd : $"{subtract}";

                        string strFormated = OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumFmtd :
                                             OperatorBuySell_Input == OperatorBuySell_Data.Subtraction ? subtractFmtd : $"{divide}";

                        Color centerColor = Math.Round(percentBuy) > Math.Round(percentSell) ? BuyColor : SellColor;

                        Center = Chart.DrawText($"{prefix}_VP_{extraProfiles}_BuySell_Result", $"\n{strFormated}", x1_Start, y1_lowest, centerColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;

                        if (HistogramSide_Input == HistSide_Data.Right)
                        {
                            Right.Time = xBar;
                            Left.Time = xBar;
                            Center.Time = xBar;
                        }

                        // Intraday Right Profile
                        if (isIntraday) {
                            DateTime dateOffset = TimeBasedOffset(xBar);
                            Right.Time = dateOffset;
                            Left.Time = dateOffset;
                            Center.Time = dateOffset;
                        }
                    }
                }
                else
                {
                    IDictionary<double, double> vpDelta = extraProfiles switch
                    {
                        ExtraProfiles.Monthly => MonthlyRank.Delta,
                        ExtraProfiles.Weekly => WeeklyRank.Delta,
                        ExtraProfiles.MiniVP => MiniRank.Delta,
                        ExtraProfiles.Fixed => FixedRank[fixedKey].Delta,
                        _ => VP_DeltaRank
                    };

                    bool intraBool = extraProfiles switch
                    {
                        ExtraProfiles.Monthly => isIntraday,
                        ExtraProfiles.Weekly => isIntraday,
                        ExtraProfiles.MiniVP => false,
                        ExtraProfiles.Fixed => false,
                        _ => isIntraday
                    };

                    double value = vpDelta[priceKey];
                    double maxValue = vpDelta.Values.Max();
                    IEnumerable<double> negativeList = vpDelta.Values.Where(n => n < 0);

                    DrawRectangle_Delta(value, maxValue, negativeList, intraBool);

                    if (ShowResults || ShowMiniResults)
                    {
                        if (extraProfiles == ExtraProfiles.MiniVP && !ShowMiniResults)
                            continue;
                        if (extraProfiles != ExtraProfiles.MiniVP && !ShowResults && extraProfiles != ExtraProfiles.Fixed)
                            continue;

                        double deltaBuy = vpDelta.Values.Where(n => n > 0).Sum();
                        double deltaSell = vpDelta.Values.Where(n => n < 0).Sum();
                        double totalDelta = vpDelta.Values.Sum();

                        double percentBuy = 0;
                        double percentSell = 0;
                        try { percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { };
                        try { percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { }
                        percentBuy = Math.Round(percentBuy);
                        percentSell = Math.Round(percentSell);

                        ChartText Left, Right;
                        Right = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_BuySum", $"{percentBuy}%", x1_Start, y1_lowest, BuyColor);
                        Left = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_SellSum", $"{percentSell}%", x1_Start, y1_lowest, SellColor);
                        Left.HorizontalAlignment = HorizontalAlignment.Left;
                        Right.HorizontalAlignment = HorizontalAlignment.Right;
                        Left.FontSize = FontSizeResults;
                        Right.FontSize = FontSizeResults;

                        ChartText Center;
                        string totalDeltaFmtd = totalDelta > 0 ? FormatBigNumber(totalDelta) : $"-{FormatBigNumber(Math.Abs(totalDelta))}";
                        string totalDeltaString = FormatResults ? totalDeltaFmtd : $"{totalDelta}";

                        Color centerColor = totalDelta > 0 ? BuyColor : SellColor;
                        Center = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_Result", $"\n{totalDeltaString}", x1_Start, y1_lowest, centerColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;

                        if (HistogramSide_Input == HistSide_Data.Right)
                        {
                            Right.Time = xBar;
                            Left.Time = xBar;
                            Center.Time = xBar;
                        }

                        // Intraday Right Profile
                        if (isIntraday && extraProfiles == ExtraProfiles.No) {
                            DateTime dateOffset = TimeBasedOffset(xBar);
                            Right.Time = dateOffset;
                            Left.Time = dateOffset;
                            Center.Time = dateOffset;
                        }

                        if (ShowMinMaxDelta)
                        {
                            ChartText MinText, MaxText, SubText;

                            double[] vpMinMax = extraProfiles switch
                            {
                                ExtraProfiles.Monthly => MonthlyRank.MinMaxDelta,
                                ExtraProfiles.Weekly => WeeklyRank.MinMaxDelta,
                                ExtraProfiles.MiniVP => MiniRank.MinMaxDelta,
                                ExtraProfiles.Fixed => FixedRank[fixedKey].MinMaxDelta,
                                _ => VP_MinMaxDelta
                            };

                            double minDelta = Math.Round(vpMinMax[0]);
                            double maxDelta = Math.Round(vpMinMax[1]);
                            double subDelta = Math.Round(minDelta - maxDelta);

                            string minDeltaFmtd = minDelta > 0 ? FormatBigNumber(minDelta) : $"-{FormatBigNumber(Math.Abs(minDelta))}";
                            string maxDeltaFmtd = maxDelta > 0 ? FormatBigNumber(maxDelta) : $"-{FormatBigNumber(Math.Abs(maxDelta))}";
                            string subDeltaFmtd = subDelta > 0 ? FormatBigNumber(subDelta) : $"-{FormatBigNumber(Math.Abs(subDelta))}";

                            string minDeltaString = FormatResults ? minDeltaFmtd : $"{minDelta}";
                            string maxDeltaString = FormatResults ? maxDeltaFmtd : $"{maxDelta}";
                            string subDeltaString = FormatResults ? subDeltaFmtd : $"{subDelta}";

                            Color subColor = subDelta > 0 ? BuyColor : SellColor;

                            if (!ShowOnlySubtDelta)
                            {
                                MinText = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_MinResult", $"\n\nMin: {minDeltaString}", x1_Start, lowest, SellColor);
                                MaxText = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_MaxResult", $"\n\n\nMax: {maxDeltaString}", x1_Start, lowest, BuyColor);
                                SubText = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_SubResult", $"\n\n\n\nSub: {subDeltaString}", x1_Start, lowest, subColor);
                                MinText.HorizontalAlignment = HorizontalAlignment.Center;
                                MaxText.HorizontalAlignment = HorizontalAlignment.Center;
                                SubText.HorizontalAlignment = HorizontalAlignment.Center;
                                MinText.FontSize = FontSizeResults - 1;
                                MaxText.FontSize = FontSizeResults - 1;
                                SubText.FontSize = FontSizeResults - 1;

                                if (HistogramSide_Input == HistSide_Data.Right)
                                {
                                    MinText.Time = xBar;
                                    MaxText.Time = xBar;
                                    SubText.Time = xBar;
                                }

                                // Intraday Right Profile
                                if (isIntraday && extraProfiles == ExtraProfiles.No) {
                                    DateTime dateOffset = TimeBasedOffset(xBar);
                                    MinText.Time = dateOffset;
                                    MaxText.Time = dateOffset;
                                    SubText.Time = dateOffset;
                                }
                            }
                            else {
                                SubText = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_SubResult", $"\n\nSub: {subDeltaString}", x1_Start, lowest, subColor);
                                SubText.HorizontalAlignment = HorizontalAlignment.Center;
                                SubText.FontSize = FontSizeResults - 1;

                                if (HistogramSide_Input == HistSide_Data.Right)
                                    SubText.Time = xBar;
                                // Intraday Right Profile
                                if (isIntraday && extraProfiles == ExtraProfiles.No) {
                                    DateTime dateOffset = TimeBasedOffset(xBar);
                                    SubText.Time = dateOffset;
                                }
                            }
                        }
                    }
                }
            }
        }

        // *********** MWM PROFILES ***********
        private void CreateMiniVPs(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (EnableMiniProfiles)
            {
                int miniIndex = MiniVPs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int miniStart = Bars.OpenTimes.GetIndexByTime(MiniVPs_Bars.OpenTimes[miniIndex]);

                if (index == miniStart ||
                    (index - 1) == miniStart && isPriceBased_Chart ||
                    (index - 1) == miniStart && (index - 1) != lastCleaned._Mini || loopStart
                ) {
                    if (!IsLastBar)
                        lastTick_ExtraVPs._MiniStart = lastTick_ExtraVPs._Mini;
                    MiniRank.Normal.Clear();
                    MiniRank.Up.Clear();
                    MiniRank.Down.Clear();
                    MiniRank.Delta.Clear();
                    double[] resetDelta = {0, 0};
                    MiniRank.MinMaxDelta = resetDelta;
                    lastCleaned._Mini = index == miniStart ? index : (index - 1);
                }
                if (!isConcurrent)
                    VolumeProfile(miniStart, index, ExtraProfiles.MiniVP, isLoop);
                else
                {
                    miniVP_Task ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.MiniVP, cts.Token));

                    liveVP_StartIndexes.Mini = miniStart;

                    if (index != miniStart) {
                        lock (_miniLock)
                        VolumeProfile(miniStart, index, ExtraProfiles.MiniVP, false, true);
                    }
                }
            }
        }
        private void CreateWeeklyVP(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (EnableVP && EnableWeeklyProfile)
            {
                // Avoid recalculating the same period.
                if (ODFInterval_Input == ODFInterval_Data.Weekly)
                    return;

                int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                if (index == weekStart ||
                    (index - 1) == weekStart && isPriceBased_Chart || loopStart
                ) {
                    if (!IsLastBar)
                        lastTick_ExtraVPs._WeeklyStart = lastTick_ExtraVPs._Weekly;
                    WeeklyRank.Normal.Clear();
                    WeeklyRank.Up.Clear();
                    WeeklyRank.Down.Clear();
                    WeeklyRank.Delta.Clear();
                    double[] resetDelta = {0, 0};
                    WeeklyRank.MinMaxDelta = resetDelta;
                }

                if (!isConcurrent)
                    VolumeProfile(weekStart, index, ExtraProfiles.Weekly, isLoop);
                else
                {
                    weeklyVP_Task ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.Weekly, cts.Token));

                    liveVP_StartIndexes.Weekly = weekStart;

                    if (index != weekStart) {
                        lock (_weeklyLock)
                            VolumeProfile(weekStart, index, ExtraProfiles.Weekly, false, true);
                    }

                    DateTime weekStartDate = WeeklyBars.OpenTimes[weekIndex];
                    firstTickTime = firstTickTime > weekStartDate ? TicksOHLC.OpenTimes.FirstOrDefault() : firstTickTime;
                    if (firstTickTime > weekStartDate)
                    {
                        DrawOnScreen("Not enough Tick data to calculate Weekly Profile \n Zoom out to see the vertical Aqua line");
                        Chart.DrawVerticalLine("WeekStart", weekStartDate, Color.Aqua);
                        ChartText text = Chart.DrawText("WeekStartText", "Target Weekly Tick Data", weekStartDate,
                                    WeeklyBars.HighPrices[weekIndex], Color.Aqua);
                        text.HorizontalAlignment = HorizontalAlignment.Right;
                        text.VerticalAlignment = VerticalAlignment.Top;
                        text.FontSize = 8;
                    }
                }
            }
        }
        private void CreateMonthlyVP(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (EnableVP && EnableMonthlyProfile)
            {
                int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                if (index == monthStart ||
                    (index - 1) == monthStart && isPriceBased_Chart || loopStart
                ) {
                    if (!IsLastBar)
                        lastTick_ExtraVPs._MonthlyStart = lastTick_ExtraVPs._Monthly;
                    MonthlyRank.Normal.Clear();
                    MonthlyRank.Up.Clear();
                    MonthlyRank.Down.Clear();
                    MonthlyRank.Delta.Clear();
                    double[] resetDelta = {0, 0};
                    MonthlyRank.MinMaxDelta = resetDelta;
                }
                if (!isConcurrent)
                    VolumeProfile(monthStart, index, ExtraProfiles.Monthly, isLoop);
                else
                {
                    monthlyVP_Task ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.Monthly, cts.Token));

                    liveVP_StartIndexes.Monthly = monthStart;

                    if (index != monthStart) {
                        lock (_monthlyLock)
                            VolumeProfile(monthStart, index, ExtraProfiles.Monthly, false, true);
                    }

                    DateTime monthStartDate = MonthlyBars.OpenTimes[monthIndex];
                    firstTickTime = firstTickTime > monthStartDate ? TicksOHLC.OpenTimes.FirstOrDefault() : firstTickTime;
                    if (firstTickTime > monthStartDate)
                    {
                        Second_DrawOnScreen("Not enough Tick data to calculate Monthly Profile \n- Zoom out to see the vertical Aqua line");
                        Chart.DrawVerticalLine("MonthStart", monthStartDate, Color.Aqua);
                        ChartText text = Chart.DrawText("MonthStartText", "Target Monthly Tick Data", monthStartDate,
                                    MonthlyBars.HighPrices[monthIndex], Color.Aqua);
                        text.HorizontalAlignment = HorizontalAlignment.Right;
                        text.VerticalAlignment = VerticalAlignment.Top;
                        text.FontSize = 8;
                    }
                }
            }
        }

        // *********** LIVE PROFILE UPDATE ***********
        private void LiveVP_Update(int indexStart, int index, bool onlyMini = false) {
            double price = Bars.ClosePrices[index];

            bool updateStrategy = UpdateProfile_Input == UpdateProfile_Data.ThroughSegments_Balanced ?
                                Math.Abs(price - prevUpdatePrice) >= rowHeight :
                                UpdateProfile_Input != UpdateProfile_Data.Through_2_Segments_Best ||
                                Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight);

            if (updateStrategy || isUpdateVP || configHasChanged)
            {
                if (!onlyMini)
                {
                    if (EnableMonthlyProfile) {

                        int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        DateTime monthStartDate = MonthlyBars.OpenTimes[monthIndex];
                        int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                        if (firstTickTime > monthStartDate) {
                            Second_DrawOnScreen("Not enough Tick data to calculate Monthly Profile \n- Zoom out to see the vertical Aqua line");
                            Chart.DrawVerticalLine("MonthStart", monthStartDate, Color.Aqua);
                            ChartText text =Chart.DrawText("MonthStartText", "Target Monthly Tick Data", monthStartDate,
                                           MonthlyBars.HighPrices[monthIndex], Color.Aqua);
                            text.HorizontalAlignment = HorizontalAlignment.Right;
                            text.VerticalAlignment = VerticalAlignment.Top;
                            text.FontSize = 8;
                        }

                        if (index != monthStart)
                        {
                            bool loopStart = true;
                            for (int i = monthStart; i <= index; i++) {
                                if (i < index)
                                    CreateMonthlyVP(i, loopStart, true); // Update only
                                else
                                    CreateMonthlyVP(i, loopStart, false); // Update and Draw
                                loopStart = false;
                            }

                        }
                    }

                    if (EnableWeeklyProfile && ODFInterval_Input != ODFInterval_Data.Weekly)
                    {
                        int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        DateTime weekStartDate = WeeklyBars.OpenTimes[weekIndex];
                        int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                        firstTickTime = firstTickTime > weekStartDate ? TicksOHLC.OpenTimes.FirstOrDefault() : firstTickTime;
                        if (firstTickTime > weekStartDate) {
                            DrawOnScreen("Not enough Tick data to calculate Weekly Profile \n Zoom out to see the vertical Aqua line");
                            Chart.DrawVerticalLine("WeekStart", weekStartDate, Color.Aqua);
                            ChartText text = Chart.DrawText("WeekStartText", "Target Weekly Tick Data", weekStartDate,
                                           WeeklyBars.HighPrices[weekIndex], Color.Aqua);
                            text.HorizontalAlignment = HorizontalAlignment.Right;
                            text.VerticalAlignment = VerticalAlignment.Top;
                            text.FontSize = 8;
                        }

                        if (index != weekStart)
                        {
                            bool loopStart = true;
                            for (int i = weekStart; i <= index; i++) {
                                if (i < index)
                                    CreateWeeklyVP(i, loopStart, true); // Update only
                                else
                                    CreateWeeklyVP(i, loopStart, false); // Update and Draw
                                loopStart = false;
                            }
                        }
                    }

                    if (EnableMiniProfiles) {
                        int miniIndex = MiniVPs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        int miniStart = Bars.OpenTimes.GetIndexByTime(MiniVPs_Bars.OpenTimes[miniIndex]);

                        if (index != miniStart)
                        {
                            bool loopStart = true;
                            for (int i = miniStart; i <= index; i++)
                            {
                                if (i < index)
                                    CreateMiniVPs(i, loopStart, true); // Update only
                                else
                                    CreateMiniVPs(i, loopStart, false); // Update and Draw
                                loopStart = false;
                            }
                        }
                    }

                    if (index != indexStart)
                    {
                        for (int i = indexStart; i <= index; i++)
                        {
                            if (i == indexStart) {
                                VP_VolumesRank.Clear();
                                VP_VolumesRank_Up.Clear();
                                VP_VolumesRank_Down.Clear();
                                VP_VolumesRank_Subt.Clear();
                                VP_DeltaRank.Clear();
                            }
                            if (i < index)
                                VolumeProfile(indexStart, i, ExtraProfiles.No, true); // Update only
                            else
                                VolumeProfile(indexStart, i, ExtraProfiles.No, false); // Update and Draw
                        }
                    }
                }
                else
                {
                    int miniIndex = MiniVPs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    int miniStart = Bars.OpenTimes.GetIndexByTime(MiniVPs_Bars.OpenTimes[miniIndex]);

                    if (index != miniStart)
                    {
                        bool loopStart = true;
                        for (int i = miniStart; i <= index; i++)
                        {
                            if (i < index)
                                CreateMiniVPs(i, loopStart, true); // Update only
                            else
                                CreateMiniVPs(i, loopStart, false); // Update and Draw
                            loopStart = false;
                        }
                    }
                }
            }

            isUpdateVP = false;
            configHasChanged = false;

            if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                prevUpdatePrice = price;
        }

        private void LiveVP_Concurrent(int index, int indexStart)
        {
            if (!EnableVP && !EnableMiniProfiles)
                return;

            double price = Bars.ClosePrices[index];
            bool updateStrategy = UpdateProfile_Input == UpdateProfile_Data.ThroughSegments_Balanced ?
                                Math.Abs(price - prevUpdatePrice) >= rowHeight :
                                UpdateProfile_Input != UpdateProfile_Data.Through_2_Segments_Best ||
                                Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight);

            if (updateStrategy || isUpdateVP || configHasChanged)
            {
                if (Bars.Count > BarTimes_Array.Length)
                {
                    lock (_lockBars)
                        BarTimes_Array = Bars.OpenTimes.ToArray();
                }
                lock (_lockTick) {
                    int startFrom = EnableVP && EnableMonthlyProfile ? lastTick_ExtraVPs._MonthlyStart :
                                    EnableVP && EnableWeeklyProfile &&
                                    ODFInterval_Input != ODFInterval_Data.Weekly ? lastTick_ExtraVPs._WeeklyStart :
                                    EnableVP ? lastTick_VPStart :
                                    (MiniVPs_Timeframe >= TimeFrame.Hour4 ? lastTick_ExtraVPs._MiniStart : lastTick_VPStart);

                    TickBars_List = new List<Bar>(TicksOHLC.Skip(startFrom - 1));
                }

                liveVP_UpdateIt = true;
            }
            cts ??= new CancellationTokenSource();

            CreateMonthlyVP(index, isConcurrent: true);
            CreateWeeklyVP(index, isConcurrent: true);
            CreateMiniVPs(index, isConcurrent: true);

            if (EnableVP)
            {
                liveVP_Task ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.No, cts.Token));
                liveVP_StartIndexes.ODF = indexStart;
                if (index != indexStart) {
                    lock (_lock)
                        VolumeProfile(indexStart, index, ExtraProfiles.No, false, true);
                }
            }
        }

        private void LiveVP_Worker(ExtraProfiles extraID, CancellationToken token)
        {
            /*
            It's quite simple, but gave headaches mostly due to GetByInvoke() unexpected behavior and debugging it.
             - GetByInvoke() will slowdown loops due to accumulative Bars[index] => "0.xx ms" operations
            The major reason why Copy of Time/Bars are used.

            I've tried:
                TimesCopy = GetByInvoke(() => Bars.OpenTimes.Skip(startIndex))
                TicksCopy = GetByInvoke(() => TicksOHLC.Skip(startTickIndex))
            With or without ToArray or ToList; leads to RAM spíkes at startup.
            */

            IDictionary<double, double> Worker_VolumesRank = new Dictionary<double, double>();
            IDictionary<double, double> Worker_VolumesRank_Up = new Dictionary<double, double>();
            IDictionary<double, double> Worker_VolumesRank_Down = new Dictionary<double, double>();
            IDictionary<double, double> Worker_VolumesRank_Subt = new Dictionary<double, double>();
            IDictionary<double, double> Worker_DeltaRank = new Dictionary<double, double>();
            double[] Worker_MinMaxDelta = { 0, 0 };

            DateTime lastTime = new();
            IEnumerable<DateTime> TimesCopy = Array.Empty<DateTime>();
            IEnumerable<Bar> TicksCopy;

            while (!token.IsCancellationRequested)
            {
                if (!liveVP_UpdateIt) {
                    // Stop itself
                    if (extraID == ExtraProfiles.No && !EnableVP) {
                        liveVP_Task = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.MiniVP && !EnableMiniProfiles) {
                        miniVP_Task = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Weekly && !EnableVP) {
                        weeklyVP_Task = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Monthly && !EnableVP) {
                        monthlyVP_Task = null;
                        return;
                    }

                    Thread.Sleep(100);
                    continue;
                }

                try
                {
                    Worker_VolumesRank = new Dictionary<double, double>();
                    Worker_VolumesRank_Up = new Dictionary<double, double>();
                    Worker_VolumesRank_Down = new Dictionary<double, double>();
                    Worker_VolumesRank_Subt = new Dictionary<double, double>();
                    Worker_DeltaRank = new Dictionary<double, double>();
                    double[] resetDelta = {0, 0};
                    Worker_MinMaxDelta = resetDelta;

                    // Chart Bars
                    int startIndex = extraID == ExtraProfiles.No ? liveVP_StartIndexes.ODF :
                                     extraID == ExtraProfiles.MiniVP ? liveVP_StartIndexes.Mini :
                                     extraID == ExtraProfiles.Weekly ? liveVP_StartIndexes.Weekly : liveVP_StartIndexes.Monthly;
                    DateTime lastBarTime = GetByInvoke(() => Bars.LastBar.OpenTime);

                    // Replace only when needed
                    if (lastTime != lastBarTime) {
                        lock (_lockBars)
                            TimesCopy = BarTimes_Array.Skip(startIndex);
                        lastTime = lastBarTime;
                    }
                    int endIndex = TimesCopy.Count();

                    // Tick
                    int startTickIndex = extraID == ExtraProfiles.No ? lastTick_VPStart :
                                         extraID == ExtraProfiles.MiniVP ? lastTick_ExtraVPs._MiniStart :
                                         extraID == ExtraProfiles.Weekly ? lastTick_ExtraVPs._WeeklyStart : lastTick_ExtraVPs._MonthlyStart;

                    // Always replace
                    lock (_lockTick)
                        TicksCopy = TickBars_List.Skip(startIndex);

                    for (int i = 0; i < endIndex; i++)
                    {
                        Worker_VP_Tick(i, extraID, i == (endIndex - 1));
                    }

                    object whichLock = extraID == ExtraProfiles.No ? _lock :
                                       extraID == ExtraProfiles.MiniVP ? _miniLock :
                                       extraID == ExtraProfiles.Weekly ? _weeklyLock : _monthlyLock;
                    lock (whichLock) {
                        switch (extraID)
                        {
                            case ExtraProfiles.MiniVP:
                                MiniRank.Normal = Worker_VolumesRank;
                                MiniRank.Up = Worker_VolumesRank_Up;
                                MiniRank.Down = Worker_VolumesRank_Down;
                                MiniRank.Delta = Worker_DeltaRank;
                                MiniRank.MinMaxDelta = Worker_MinMaxDelta;
                                break;
                            case ExtraProfiles.Weekly:
                                WeeklyRank.Normal = Worker_VolumesRank;
                                WeeklyRank.Up = Worker_VolumesRank_Up;
                                WeeklyRank.Down = Worker_VolumesRank_Down;
                                WeeklyRank.Delta = Worker_DeltaRank;
                                WeeklyRank.MinMaxDelta = Worker_MinMaxDelta;
                                break;
                            case ExtraProfiles.Monthly:
                                MonthlyRank.Normal = Worker_VolumesRank;
                                MonthlyRank.Up = Worker_VolumesRank_Up;
                                MonthlyRank.Down = Worker_VolumesRank_Down;
                                MonthlyRank.Delta = Worker_DeltaRank;
                                MonthlyRank.MinMaxDelta = Worker_MinMaxDelta;
                                break;
                            default:
                                VP_VolumesRank = Worker_VolumesRank;
                                VP_VolumesRank_Up = Worker_VolumesRank_Up;
                                VP_VolumesRank_Down = Worker_VolumesRank_Down;
                                VP_VolumesRank_Subt = Worker_VolumesRank_Subt;
                                VP_DeltaRank = Worker_DeltaRank;
                                VP_MinMaxDelta = Worker_MinMaxDelta;
                                break;
                        }

                        isUpdateVP = false;
                        configHasChanged = false;

                        if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                            prevUpdatePrice = TicksCopy.Last().Close;
                    }
                }
                catch (Exception e) { Print($"CRASH at LiveVP_Worker => {extraID}: {e}"); }

                liveVP_UpdateIt = false;
            }

            void Worker_VP_Tick(int index, ExtraProfiles extraVP = ExtraProfiles.No, bool isLastBarLoop = false)
            {
                DateTime startTime = TimesCopy.ElementAt(index);
                DateTime endTime = !isLastBarLoop ? TimesCopy.ElementAt(index + 1) : TicksCopy.Last().OpenTime;

                double prevLoopTick = 0;
                for (int tickIndex = 0; tickIndex < TicksCopy.Count(); tickIndex++)
                {
                    Bar tickBar = TicksCopy.ElementAt(tickIndex);

                    if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime)
                    {
                        if (tickBar.OpenTime > endTime)
                            break;
                        else
                            continue;
                    }

                    if (prevLoopTick != 0)
                        RankVolume(tickBar.Close, prevLoopTick);

                    prevLoopTick = tickBar.Close;
                }

                // =======================
                void RankVolume(double tickPrice, double prevTick)
                {
                    var segmentsSource = Segments_VP;

                    double prevSegmentValue = 0.0;
                    for (int i = 0; i < segmentsSource.Count; i++)
                    {
                        if (prevSegmentValue != 0 && tickPrice >= prevSegmentValue && tickPrice <= segmentsSource[i])
                        {
                            double priceKey = segmentsSource[i];

                            double prevDelta = 0;
                            if (ShowMinMaxDelta)
                                prevDelta = Worker_DeltaRank.Values.Sum();

                            if (Worker_VolumesRank.ContainsKey(priceKey))
                            {
                                Worker_VolumesRank[priceKey] += 1;

                                if (tickPrice > prevTick)
                                    Worker_VolumesRank_Up[priceKey] += 1;
                                else if (tickPrice < prevTick)
                                    Worker_VolumesRank_Down[priceKey] += 1;
                                else if (tickPrice == prevTick)
                                {
                                    Worker_VolumesRank_Up[priceKey] += 1;
                                    Worker_VolumesRank_Down[priceKey] += 1;
                                }

                                Worker_DeltaRank[priceKey] += (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]);

                                Worker_VolumesRank_Subt[priceKey] = Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey];
                            }
                            else
                            {
                                Worker_VolumesRank.Add(priceKey, 1);

                                if (!Worker_VolumesRank_Up.ContainsKey(priceKey))
                                    Worker_VolumesRank_Up.Add(priceKey, 1);
                                else
                                    Worker_VolumesRank_Up[priceKey] += 1;

                                if (!Worker_VolumesRank_Down.ContainsKey(priceKey))
                                    Worker_VolumesRank_Down.Add(priceKey, 1);
                                else
                                    Worker_VolumesRank_Down[priceKey] += 1;

                                if (!Worker_DeltaRank.ContainsKey(priceKey))
                                    Worker_DeltaRank.Add(priceKey, (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]));
                                else
                                    Worker_DeltaRank[priceKey] += (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]);

                                double value = Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey];
                                if (!Worker_VolumesRank_Subt.ContainsKey(priceKey))
                                    Worker_VolumesRank_Subt.Add(priceKey, value);
                                else
                                    Worker_VolumesRank_Subt[priceKey] = value;
                            }

                            if (ShowMinMaxDelta)
                            {
                                double currentDelta = Worker_DeltaRank.Values.Sum();
                                if (prevDelta > currentDelta)
                                    Worker_MinMaxDelta[0] = prevDelta; // Min
                                if (prevDelta < currentDelta)
                                    Worker_MinMaxDelta[1] = prevDelta; // Max before final delta
                            }

                            break;
                        }
                        prevSegmentValue = segmentsSource[i];
                    }
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
            int miniIndex = MiniVPs_Bars.OpenTimes.GetIndexByTime(lastBarDate);
            int miniStart = Bars.OpenTimes.GetIndexByTime(MiniVPs_Bars.OpenTimes[miniIndex]);

            string nameKey = $"FixedRange_{DateTime.UtcNow.Ticks}";
            ChartRectangle rect = Chart.DrawRectangle(
                nameKey,
                Bars.OpenTimes[miniStart],
                MiniVPs_Bars.LowPrices[miniIndex],
                lastBarDate,
                MiniVPs_Bars.HighPrices[miniIndex],
                FixedColor,
                2,
                LineStyle.Lines
            );

            rect.IsInteractive = true;
            _rectangles.Add(rect);

            FixedRank.Add(nameKey, new VolumeRankType());

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
                VolumeProfile(startIdx, i, ExtraProfiles.Fixed, fixedKey: rect.Name, fixedLowest: bottomY);
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
            FixedRank[fixedKey].Normal.Clear();
            FixedRank[fixedKey].Up.Clear();
            FixedRank[fixedKey].Down.Clear();
            FixedRank[fixedKey].Delta.Clear();
            FixedRank[fixedKey].MinMaxDelta = new double[2];

            int endIdx = Bars.OpenTimes.GetIndexByTime(end);
            int TF_idx = GetSegmentIndex(endIdx);

            for (int i = 0; i < segmentsDict[TF_idx].Count; i++)
            {
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Normal");
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Normal");

                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Sell");
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Buy");
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Sell");
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Buy");

                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Delta");
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Delta");
            }

            string[] objsNames = new string[10] {
                $"{fixedKey}_VP_Fixed_Normal_Result",

                $"{fixedKey}_VP_Fixed_Sell_Sum",
                $"{fixedKey}_VP_Fixed_Buy_Sum",
                $"{fixedKey}_VP_Fixed_BuySell_Result",

                $"{fixedKey}_VP_Fixed_Delta_BuySum",
                $"{fixedKey}_VP_Fixed_Delta_SellSum",
                $"{fixedKey}_VP_Fixed_Delta_Result",

                $"{fixedKey}_VP_Fixed_Delta_MinResult",
                $"{fixedKey}_VP_Fixed_Delta_MaxResult",
                $"{fixedKey}_VP_Fixed_Delta_SubResult",
            };

            foreach (string name in objsNames)
                Chart.RemoveObject(name);
        }

        public void ResetFixedRange_Dicts() {
            _rectangles.Clear();
            _infoObjects.Clear();
            _controlGrids.Clear();
        }

        // *********** SOME SHARED FUCTIONS ***********
        private void VP_Tick(int index, bool isVP = false, ExtraProfiles extraVP = ExtraProfiles.No, string fixedKey = "")
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];

            // For real-time market - ODF
            if (IsLastBar && !isVP && !isPriceBased_NewBar)
                endTime = TicksOHLC.LastBar.OpenTime;

            // For real-time market - VP
            // Run conditional only in the last bar of repaint loop
            if (IsLastBar && isVP && Bars.OpenTimes[index] == Bars.LastBar.OpenTime)
                endTime = TicksOHLC.LastBar.OpenTime;

            /*
                TicksOHLC.OpenTimes => .GetIndexByExactTime() and .GetIndexByTime() returns -1 for historical data
                So, the VP/Wicks loop can't be optimized like the ODF_Ticks/Agg' or VP' Python version.

                NEW IN ODF_AGG => Trying to get only 3 days of Order Flow was painfully/extremely slow...
                Just doing a simple thing, which is keeping the last used tickIndex for both VP/ODF.
                Performs the calculations/drawings at the speed of light, even for 1 month of ticks!
            */
            int startIndex = extraVP switch
            {
                ExtraProfiles.Monthly => !IsLastBar ? lastTick_ExtraVPs._Monthly : lastTick_ExtraVPs._MonthlyStart,
                ExtraProfiles.Weekly => !IsLastBar ? lastTick_ExtraVPs._Weekly : lastTick_ExtraVPs._WeeklyStart,
                ExtraProfiles.MiniVP => !IsLastBar ? lastTick_ExtraVPs._Mini : lastTick_ExtraVPs._MiniStart,
                _ => isVP ? lastTick_VPStart : lastTick_Bars
            };
            if (extraVP == ExtraProfiles.Fixed) {
                ChartRectangle rect = _rectangles.Where(x => x.Name == fixedKey).FirstOrDefault();
                DateTime start = rect.Time1 < rect.Time2 ? rect.Time1 : rect.Time2;
                startIndex = Bars.OpenTimes.GetIndexByTime(start);
            }

            // For real-time market - ODF
            if (IsLastBar && !isVP) {
                while (TicksOHLC.OpenTimes[startIndex] < startTime)
                    startIndex++;

                lastTick_Bars = startIndex;
            }

            int TF_idx = extraVP == ExtraProfiles.Fixed ? GetSegmentIndex(index) : index;
            List<double> whichSegment_VP = extraVP == ExtraProfiles.Fixed ? segmentsDict[TF_idx] : Segments_VP;

            // =======================
            double prevLoopTick = 0;
            for (int tickIndex = startIndex; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar;
                tickBar = TicksOHLC[tickIndex];
                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime)
                {
                    if (tickBar.OpenTime > endTime) {
                        // ODF
                        lastTick_Bars = !isVP ? tickIndex : lastTick_Bars;
                        // VP
                        _ = extraVP switch
                        {
                            ExtraProfiles.Monthly => lastTick_ExtraVPs._Monthly = tickIndex,
                            ExtraProfiles.Weekly => lastTick_ExtraVPs._Weekly = tickIndex,
                            ExtraProfiles.MiniVP => lastTick_ExtraVPs._Mini = tickIndex,
                            _ => isVP ? tickIndex : lastTick_VP
                        };
                        break;
                    } else
                        continue;
                }

                if (prevLoopTick != 0)
                    RankVolume(tickBar.Close, prevLoopTick);

                prevLoopTick = tickBar.Close;
            }

            // =======================
            void RankVolume(double tickPrice, double prevTick)
            {
                List<double> segmentsSource = isVP ? whichSegment_VP : Segments_Bar;

                double prevSegmentValue = 0.0;
                for (int i = 0; i < segmentsSource.Count; i++)
                {
                    if (prevSegmentValue != 0 && tickPrice >= prevSegmentValue && tickPrice <= segmentsSource[i])
                    {
                        double priceKey = segmentsSource[i];

                        if (isVP)
                        {
                            if (extraVP != ExtraProfiles.No)
                            {
                                VolumeRankType extraRank = extraVP switch
                                {
                                    ExtraProfiles.Monthly => MonthlyRank,
                                    ExtraProfiles.Weekly => WeeklyRank,
                                    ExtraProfiles.Fixed => FixedRank[fixedKey],
                                    _ => MiniRank
                                };
                                UpdateExtraProfiles(extraRank, priceKey, tickPrice, prevTick);
                                return;
                            }

                            double prevDelta = 0;
                            if (ShowMinMaxDelta)
                                prevDelta = VP_DeltaRank.Values.Sum();

                            if (VP_VolumesRank.ContainsKey(priceKey))
                            {
                                VP_VolumesRank[priceKey] += 1;

                                if (tickPrice > prevTick)
                                    VP_VolumesRank_Up[priceKey] += 1;
                                else if (tickPrice < prevTick)
                                    VP_VolumesRank_Down[priceKey] += 1;
                                else if (tickPrice == prevTick)
                                {
                                    VP_VolumesRank_Up[priceKey] += 1;
                                    VP_VolumesRank_Down[priceKey] += 1;
                                }

                                VP_DeltaRank[priceKey] += (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]);

                                VP_VolumesRank_Subt[priceKey] = VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey];
                            }
                            else
                            {
                                VP_VolumesRank.Add(priceKey, 1);

                                if (!VP_VolumesRank_Up.ContainsKey(priceKey))
                                    VP_VolumesRank_Up.Add(priceKey, 1);
                                else
                                    VP_VolumesRank_Up[priceKey] += 1;

                                if (!VP_VolumesRank_Down.ContainsKey(priceKey))
                                    VP_VolumesRank_Down.Add(priceKey, 1);
                                else
                                    VP_VolumesRank_Down[priceKey] += 1;

                                if (!VP_DeltaRank.ContainsKey(priceKey))
                                    VP_DeltaRank.Add(priceKey, (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]));
                                else
                                    VP_DeltaRank[priceKey] += (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]);

                                double value = VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey];
                                if (!VP_VolumesRank_Subt.ContainsKey(priceKey))
                                    VP_VolumesRank_Subt.Add(priceKey, value);
                                else
                                    VP_VolumesRank_Subt[priceKey] = value;
                            }

                            if (ShowMinMaxDelta)
                            {
                                double currentDelta = VP_DeltaRank.Values.Sum();
                                if (prevDelta > currentDelta)
                                    VP_MinMaxDelta[0] = prevDelta; // Min
                                if (prevDelta < currentDelta)
                                    VP_MinMaxDelta[1] = prevDelta; // Max before final delta
                            }
                        }
                        else
                        {
                            int prevDelta = 0;
                            if (ShowMinMaxDelta || BubblesSource_Input == BubblesSource_Data.Subtract_Delta)
                                prevDelta = DeltaRank.Values.Sum();

                            if (VolumesRank.ContainsKey(priceKey))
                            {
                                VolumesRank[priceKey] += 1;

                                if (tickPrice > prevTick)
                                    VolumesRank_Up[priceKey] += 1;
                                else if (tickPrice < prevTick)
                                    VolumesRank_Down[priceKey] += 1;
                                else if (tickPrice == prevTick)
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

                            if (ShowMinMaxDelta || BubblesSource_Input == BubblesSource_Data.Subtract_Delta)
                            {
                                int currentDelta = DeltaRank.Values.Sum();
                                if (prevDelta > currentDelta)
                                    MinMaxDelta[0] = prevDelta; // Min
                                if (prevDelta < currentDelta)
                                    MinMaxDelta[1] = prevDelta; // Max before final delta
                            }
                        }

                        break;
                    }
                    prevSegmentValue = segmentsSource[i];
                }
            }

            void UpdateExtraProfiles(VolumeRankType volRank, double priceKey, double tickPrice, double prevTick) {
                double prevDelta = 0;
                if (ShowMinMaxDelta)
                    prevDelta = volRank.Delta.Values.Sum();

                if (volRank.Normal.ContainsKey(priceKey))
                {
                    volRank.Normal[priceKey] += 1;

                    if (tickPrice > prevTick)
                        volRank.Up[priceKey] += 1;
                    else if (tickPrice < prevTick)
                        volRank.Down[priceKey] += 1;
                    else if (tickPrice == prevTick)
                    {
                        volRank.Up[priceKey] += 1;
                        volRank.Down[priceKey] += 1;
                    }

                    volRank.Delta[priceKey] += (volRank.Up[priceKey] - volRank.Down[priceKey]);
                }
                else
                {
                    volRank.Normal.Add(priceKey, 1);

                    if (!volRank.Up.ContainsKey(priceKey))
                        volRank.Up.Add(priceKey, 1);
                    else
                        volRank.Up[priceKey] += 1;

                    if (!volRank.Down.ContainsKey(priceKey))
                        volRank.Down.Add(priceKey, 1);
                    else
                        volRank.Down[priceKey] += 1;

                    if (!volRank.Delta.ContainsKey(priceKey))
                        volRank.Delta.Add(priceKey, (volRank.Up[priceKey] - volRank.Down[priceKey]));
                    else
                        volRank.Delta[priceKey] += (volRank.Up[priceKey] - volRank.Down[priceKey]);
                }

                if (ShowMinMaxDelta)
                {
                    double currentDelta = volRank.Delta.Values.Sum();
                    if (prevDelta > currentDelta)
                        volRank.MinMaxDelta[0] = prevDelta; // Min
                    if (prevDelta < currentDelta)
                        volRank.MinMaxDelta[1] = prevDelta; // Max before final delta
                }
            }
        }

        private double[] GetWicks(DateTime startTime, DateTime endTime)
        {
            double min = Int32.MaxValue;
            double max = 0;

            if (IsLastBar && !isPriceBased_NewBar)
                endTime = TicksOHLC.LastBar.OpenTime;

            for (int tickIndex = lastTick_Wicks; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime) {
                    if (tickBar.OpenTime > endTime) {
                        lastTick_Wicks = tickIndex;
                        break;
                    }
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

        private void DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
        }

        private void Second_DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt2", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }


        // *********** PERFORMANCE DRAWING ***********
        /*
            An simple idea that came up during the development of ODF_AGG.
            LLM code generating was used to quickly test the idea concepts.

            - Re-draw => Objects are deleted and recreated each time,
                - Fastest approach
                - Removes only objects outside the visible chart range
                - when cleaning up the chart with Chart.RemoveAllObjects()
                    it takes only 1/0.5 seconds.

            - Hidden => Objects are never deleted, just .IsHidden = True.
                - Slowest approach
                - IsHidden = false, only in visibles objects.
                - when cleaning up the chart with Chart.RemoveAllObjects()
                    it lags/freezes the chart/panel UI,
                    the waiting time scales with the drawings count.
                - Lags at scrolling at MASSIVE hidden drawings count.
        */
        private void PerformanceDrawing(object obj)
        {
            int first = Chart.FirstVisibleBarIndex;
            int last = Chart.LastVisibleBarIndex;
            int visible = 0;

            // ==== Drawing at Zoom ====
            int Zoom = Chart.ZoomLevel;
            // Keep rectangles from Filters or VPs
            if (Zoom < DrawAtZoom_Value) {
                HiddenOrRemove(true);
                return;
            }

            void HiddenOrRemove(bool hiddenAll)
            {
                if (DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest && hiddenAll)
                {
                    foreach (var kvp in hiddenInfos)
                    {
                        string drawName = kvp.Key;
                        ChartObject drawObj = kvp.Value;

                        // Extract index from name
                        string[] parts = drawName.Split('_');
                        if (parts.Length < 2) continue;
                        if (!int.TryParse(parts.FirstOrDefault(), out _)) continue;

                        drawObj.IsHidden = hiddenAll;
                    }
                }
                else if (DrawingStrategy_Input == DrawingStrategy_Data.Redraw_Fastest && hiddenAll) {
                    // Remove everything
                    foreach (var kvp in redrawInfos.Values)
                    {
                        var drawInfoList = kvp.Values;
                        foreach (DrawInfo drawInfo in drawInfoList)
                            Chart.RemoveObject(drawInfo.Id);
                    }
                }

                DebugPerfDraw();
            }

            // ==== Drawing at scroll ====
            if (DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest) {
                // Display the hidden ones
                foreach (var kvp in hiddenInfos)
                {
                    string drawName = kvp.Key;
                    ChartObject drawObj = kvp.Value;

                    // Extract index from name
                    string[] parts = drawName.Split('_');
                    if (parts.Length < 2) continue;
                    if (!int.TryParse(parts.FirstOrDefault(), out int idx)) continue;

                    bool isVis = idx >= first && idx <= last;
                    drawObj.IsHidden = !isVis;

                    if (ShowDrawingInfo) {
                        if (isVis) visible++;
                    }
                }
            }
            else {
                // Clean up
                foreach (var kvp in redrawInfos)
                {
                    var drawInfoList = kvp.Value.Values;
                    foreach (DrawInfo drawInfo in drawInfoList)
                    {
                        // The actual lazy cleanup.
                        if (kvp.Key < first || kvp.Key > last)
                            Chart.RemoveObject(drawInfo.Id);
                    }
                }

                // Draw visible
                for (int i = first; i <= last; i++)
                {
                    if (!redrawInfos.ContainsKey(i))
                        continue;

                    var drawInfoList = redrawInfos[i].Values;
                    foreach (DrawInfo info in drawInfoList)
                    {
                        CreateDraw(info);
                        if (ShowDrawingInfo)
                            visible++;
                    }
                }
            }

            DebugPerfDraw();

            void DebugPerfDraw() {
                if (ShowDrawingInfo) {
                    _StaticText_DebugPerfDraw ??= Chart.DrawStaticText("Debug_Perf_Draw", "", VerticalAlignment.Top, HorizontalAlignment.Left, Color.Lime);
                    bool IsHidden = DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest;
                    int cached = 0;
                    if (!IsHidden) {
                        foreach (var list in redrawInfos.Values) {
                            cached += list.Count;
                        }
                    }
                    _StaticText_DebugPerfDraw.Text = IsHidden ?
                        $"Hidden Mode\n Total Objects: {FormatBigNumber(hiddenInfos.Values.Count)}\n Visible: {FormatBigNumber(visible)}" :
                        $"Redraw Mode\n Cached: {FormatBigNumber(redrawInfos.Count)} bars\n Cached: {FormatBigNumber(cached)} objects\n Drawn: {FormatBigNumber(visible)}";
                }
            }
        }
        private ChartObject CreateDraw(DrawInfo info)
        {
            switch (info.Type)
            {
                case DrawType.Text:
                    ChartText text = Chart.DrawText(info.Id, info.Text, info.X1, info.Y1, info.Color);
                    text.HorizontalAlignment = info.horizontalAlignment;
                    text.VerticalAlignment = info.verticalAlignment;
                    text.FontSize = info.FontSize;
                    return text;
                case DrawType.Icon:
                    return Chart.DrawIcon(info.Id, info.IconType, info.X1, info.Y1, info.Color);

                case DrawType.Ellipse:
                    ChartEllipse ellipse = Chart.DrawEllipse(info.Id, info.X1, info.Y1, info.X2, info.Y2, info.Color);
                    ellipse.IsFilled = true;
                    return ellipse;

                case DrawType.Rectangle:
                    ChartRectangle rectangle = Chart.DrawRectangle(info.Id, info.X1, info.Y1, info.X2, info.Y2, info.Color);
                    rectangle.IsFilled = FillHist;
                    return rectangle;

                default:
                    return null;
            }
        }
        private void DrawOrCache(DrawInfo info) {
            if (DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest)
            {
                if (!IsLastBar || isPriceBased_NewBar) {
                    ChartObject obj = CreateDraw(info);
                    obj.IsHidden = true;
                    hiddenInfos[info.Id] = obj;
                } else {
                    ChartObject obj = CreateDraw(info);
                    // Replace current obj
                    if (!currentToHidden.ContainsKey(0))
                        currentToHidden[0] = new Dictionary<string, ChartObject>();
                    else
                        currentToHidden[0][info.Id] = obj;
                }
            }
            else
            {
                // Add Keys if not present
                if (!redrawInfos.ContainsKey(info.BarIndex)) {
                    redrawInfos[info.BarIndex] = new Dictionary<string, DrawInfo> { { info.Id, info } };
                }
                else {
                    // Add/Replace drawing
                    if (!IsLastBar || isPriceBased_NewBar)
                        redrawInfos[info.BarIndex][info.Id] = info;
                    else {
                        // Create drawing and replace current infos
                        CreateDraw(info);
                        if (!currentToRedraw.ContainsKey(0))
                            currentToRedraw[0] = new Dictionary<string, DrawInfo>();
                        else
                            currentToRedraw[0][info.Id] = info;
                    }
                }
            }
        }
        private void LiveDrawing(BarOpenedEventArgs obj) {
            // Working with Lists in Calculate() is painful.

            if (DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest) {
                List<ChartObject> objList = currentToHidden[0].Values.ToList();

                foreach (var drawObj in objList)
                    hiddenInfos[drawObj.Name] = drawObj;

                currentToHidden.Clear();
            }
            else {
                List<DrawInfo> drawList = currentToRedraw[0].Values.ToList();
                foreach (DrawInfo info in drawList) {
                    redrawInfos[drawList.FirstOrDefault().BarIndex][info.Id] = info;
                }

                currentToRedraw.Clear();
            }
        }

        // *********** VOLUME RENKO/RANGE ***********
        /*
            Original source code by srlcarlg (me) (https://ctrader.com/algos/indicators/show/3045)
            Uses Ticks Data to make the calculation of volume, just like Candles.

            Refactored in Order Flow Ticks v2.0 revision 1.5
            Improved in Order Flow Aggregated v2.0
        */
        private void VolumeInitialize(bool onlyDate = false)
        {
            DateTime lastBarDate = Bars.LastBar.OpenTime.Date;

            if (LoadTickFrom_Input == LoadTickFrom_Data.Custom) {
                // ==== Get datetime to load from: dd/mm/yyyy ====
                if (DateTime.TryParseExact(StringDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDateTime)) {
                    if (fromDateTime > lastBarDate) {
                        fromDateTime = lastBarDate;
                        Notifications.ShowPopup(
                            NOTIFY_CAPTION,
                            $"Invalid DateTime '{StringDate}'. \nUsing '{fromDateTime.ToShortDateString()}",
                            PopupNotificationState.Error
                        );
                    }
                } else {
                    fromDateTime = lastBarDate;
                    Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"Invalid DateTime '{StringDate}'. \nUsing '{fromDateTime.ToShortDateString()}",
                        PopupNotificationState.Error
                    );
                }
            }
            else {
                fromDateTime = LoadTickFrom_Input switch {
                    LoadTickFrom_Data.Yesterday => MarketData.GetBars(TimeFrame.Daily).LastBar.OpenTime.Date,
                    LoadTickFrom_Data.Before_Yesterday => MarketData.GetBars(TimeFrame.Daily).Last(1).OpenTime.Date,
                    LoadTickFrom_Data.One_Week => MarketData.GetBars(TimeFrame.Weekly).LastBar.OpenTime.Date,
                    LoadTickFrom_Data.Two_Week => MarketData.GetBars(TimeFrame.Weekly).Last(1).OpenTime.Date,
                    LoadTickFrom_Data.Monthly => MarketData.GetBars(TimeFrame.Monthly).LastBar.OpenTime.Date,
                    _ => lastBarDate,
                };
            }

            if (onlyDate) {
                DrawStartVolumeLine();
                return;
            }

            // ==== Check if existing ticks data on the chart really needs more data ====
            firstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
            if (firstTickTime >= fromDateTime) {

                PopupNotification progressPopup = null;
                bool notifyIsMinimal = LoadTickNotify_Input == LoadTickNotify_Data.Minimal;
                if (notifyIsMinimal)
                    progressPopup = Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Loading Tick Data Synchronously...",
                        PopupNotificationState.InProgress
                    );

                while (TicksOHLC.OpenTimes.FirstOrDefault() > fromDateTime)
                {
                    int loadedCount = TicksOHLC.LoadMoreHistory();
                    if (LoadTickNotify_Input == LoadTickNotify_Data.Detailed) {
                        Notifications.ShowPopup(
                            NOTIFY_CAPTION,
                            $"[{Symbol.Name}] Loaded {loadedCount} Ticks. \nCurrent Tick Date: {TicksOHLC.OpenTimes.FirstOrDefault()}",
                            PopupNotificationState.Partial
                        );
                    }
                    if (loadedCount == 0)
                        break;
                }

                if (notifyIsMinimal)
                    progressPopup.Complete(PopupNotificationState.Success);
                else {
                    Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Synchronous Tick Data Collection Finished.",
                        PopupNotificationState.Success
                    );
                }
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
        private void DrawFromDateLine() {
            try {
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("FromDate", fromDateTime, Color.Yellow);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText("FromDateText", "Target Tick Data", fromDateTime, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(fromDateTime)], Color.Yellow);
                textInfo.HorizontalAlignment = HorizontalAlignment.Left;
                textInfo.VerticalAlignment = VerticalAlignment.Center;
                textInfo.FontSize = 8;
            } catch { };
        }

        private void LoadMoreTicksOnChart()
        {
            /*
                At the moment, LoadMoreHistoryAsync() doesn't work
                while Calculate() is invoked for historical data (!IsLastBar)
                and loading at each price update (IsLastBar) isn't wanted.
                - Plus, LoadMoreHistory() performance seems better.

                NEW IN ODF_AGG => "Seems better"... famous last words.
                    - Asynchronous Tick Data loading has been added.
            */

            firstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
            if (firstTickTime > fromDateTime)
            {
                bool notifyIsMinimal = LoadTickNotify_Input == LoadTickNotify_Data.Minimal;
                PopupNotification progressPopup = null;

                if (LoadTickStrategy_Input == LoadTickStrategy_Data.On_ChartStart_Sync) {

                    if (notifyIsMinimal)
                        progressPopup = Notifications.ShowPopup(
                            NOTIFY_CAPTION,
                            $"[{Symbol.Name}] Loading Tick Data Synchronously...",
                            PopupNotificationState.InProgress
                        );

                    // "Freeze" the Chart at the beginning of Calculate()
                    while (TicksOHLC.OpenTimes.FirstOrDefault() > fromDateTime)
                    {
                        int loadedCount = TicksOHLC.LoadMoreHistory();
                        if (LoadTickNotify_Input == LoadTickNotify_Data.Detailed) {
                            Notifications.ShowPopup(
                                NOTIFY_CAPTION,
                                $"[{Symbol.Name}] Loaded {loadedCount} Ticks. \nCurrent Tick Date: {TicksOHLC.OpenTimes.FirstOrDefault()}",
                                PopupNotificationState.Partial
                            );
                        }
                        if (loadedCount == 0)
                            break;
                    }

                    if (notifyIsMinimal)
                        progressPopup.Complete(PopupNotificationState.Success);
                    else
                    {
                        Notifications.ShowPopup(
                            NOTIFY_CAPTION,
                            $"[{Symbol.Name}] Synchronous Tick Data Collection Finished.",
                            PopupNotificationState.Success
                        );
                    }
                    unlockChart();
                }
                else {
                    if (IsLastBar && !loadingAsyncTicks)
                        timerHandler.isAsyncLoading = true;
                }
            }
            else
                unlockChart();


            void unlockChart() {
                if (syncTickProgressBar != null) {
                    syncTickProgressBar.IsIndeterminate = false;
                    syncTickProgressBar.IsVisible = false;
                }
                syncTickProgressBar = null;
                loadingTicksComplete = true;
                DrawStartVolumeLine();
            }
        }

        protected override void OnTimer()
        {
            if (timerHandler.isAsyncLoading)
            {
                if (!loadingAsyncTicks) {
                    string volumeLineInfo = "=> Zoom out and follow the Vertical Line";
                    asyncTickPopup = Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Loading Tick Data Asynchronously every 0.5 second...\n{volumeLineInfo}",
                        PopupNotificationState.InProgress
                    );
                    // Draw target date.
                    DrawFromDateLine();
                }

                if (!loadingTicksComplete) {
                    TicksOHLC.LoadMoreHistoryAsync((_) => {
                        DateTime currentDate = _.Bars.FirstOrDefault().OpenTime;

                        DrawStartVolumeLine();

                        if (currentDate <= fromDateTime) {

                            if (asyncTickPopup.State != PopupNotificationState.Success)
                                asyncTickPopup.Complete(PopupNotificationState.Success);

                            if (LoadTickNotify_Input == LoadTickNotify_Data.Detailed) {
                                Notifications.ShowPopup(
                                    NOTIFY_CAPTION,
                                    $"[{Symbol.Name}] Asynchronous Tick Data Collection Finished.",
                                    PopupNotificationState.Success
                                );
                            }

                            loadingTicksComplete = true;
                        }
                    });

                    loadingAsyncTicks = true;
                }
                else {
                    DrawOnScreen("");
                    Second_DrawOnScreen("");
                    timerHandler.isAsyncLoading = false;
                    ClearAndRecalculate();
                    Timer.Stop();
                }
            }
        }

        // The chart should already be clear, with no objects and bar colors.
        // Unless it's a static update.
        public void ClearAndRecalculate()
        {
            // The plot (sometimes in some options, like Volume View) is too fast, slow down a bit.
            Thread.Sleep(300);

            // Avoid it
            VerifyConflict();
            if (segmentsConflict)
                return;

            // LookBack from VP
            Bars ODF_Bars = ODFInterval_Input == ODFInterval_Data.Daily ? DailyBars : WeeklyBars;
            int firstIndex = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes.FirstOrDefault());

            // Get Index of ODF Interval to continue only in Lookback
            int iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            while (ODF_Bars.ClosePrices.Count - iVerify > Lookback) {
                firstIndex++;
                iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            }

            // Daily or Weekly ODF
            int TF_idx = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes[TF_idx]);

            // Weekly Profile but Daily ODF
            bool extraWeekly = EnableVP && EnableWeeklyProfile && ODFInterval_Input == ODFInterval_Data.Daily;
            if (extraWeekly) {
                TF_idx = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                indexStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[TF_idx]);
            }

            // Monthly Profile
            bool extraMonthly = EnableVP && EnableMonthlyProfile;
            if (extraMonthly) {
                TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                indexStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[TF_idx]);
            }

            // Reset Tick Index.
            lastTick_VP = 0;
            lastTick_Bars = 0;
            lastTick_Wicks = 0;
            lastTick_ExtraVPs._Mini = 0;
            lastTick_ExtraVPs._Weekly = 0;
            lastTick_ExtraVPs._Monthly = 0;

            // Reset Drawings
            redrawInfos.Clear();
            hiddenInfos.Clear();
            currentToHidden.Clear();
            currentToRedraw.Clear();

            // Reset Segments
            // It's needed since TF_idx(start) change if SegmentsInterval_Input is switched on the panel
            Segments_VP.Clear();
            segmentInfo.Clear();

            // Reset last update
            lastCleaned._ODF_Interval = 0;
            lastCleaned._Mini = 0;

            // Reset Fixed Range
            foreach (ChartRectangle rect in _rectangles)
            {
                DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
                ResetFixedRange(rect.Name, end);
            }

            // Historical data
            for (int index = indexStart; index < Bars.Count; index++)
            {
                CreateSegments(index);

                if (EnableVP) {
                    CreateMonthlyVP(index);
                    CreateWeeklyVP(index);
                }
                // Calculate ODF only in lookback
                if (extraWeekly || extraMonthly) {
                    iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    if (ODF_Bars.ClosePrices.Count - iVerify > Lookback)
                        continue;
                }

                TF_idx = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                indexStart = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes[TF_idx]);

                if (index == indexStart ||
                   (index - 1) == indexStart && isPriceBased_Chart ||
                   (index - 1) == indexStart && (index - 1) != lastCleaned._ODF_Interval)
                    MassiveCleanUp(indexStart, index);

                CreateMiniVPs(index);

                try { if (EnableVP) VolumeProfile(indexStart, index); } catch { }

                try { CreateOrderflow(index); } catch { }

            }

            configHasChanged = true;

            DrawStartVolumeLine();
            try { PerformanceDrawing(true); } catch { } // Draw without scroll or zoom

            void CreateOrderflow(int i) {
                // Required for Ultra Bubbles Levels in Historical Data
                lockUltraLevels = false;
                lockSpikeLevels = false;
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                int[] resetDelta = {0, 0};
                MinMaxDelta = resetDelta;
                OrderFlow(i);
            }
        }

        public void SetRowHeight(double number) {
            rowHeight = number;
        }
        public void SetLookback(int number) {
            Lookback = number;
        }
        public void SetMiniVPsBars() {
            MiniVPs_Bars = MarketData.GetBars(MiniVPs_Timeframe);
        }
        public double GetRowHeight() {
            return rowHeight;
        }
        public double GetLookback() {
            return Lookback;
        }


    }

    // ================ PARAMS PANEL ================
    /*
    What I've done since bringing it from ODF Ticks, by order:
        - Add remaining Result parameters (Large Filter)
        - Chang some hard-coded Widths "// ParamsPanel"
        - Add remaining and new "Tick Spike" parameters (Spike Chart/Levels)
        - Add textInputLabelMap
        - Add remaining and new "Bubbles Chart" parameters (Ultra Bubbles Levels/Notify)
        - Increase grid rows from 4 to 6 => CreateContentPanel
            - Reorganize Tick Spike / Bubbles Chart Parametrs
        - Add Volume Profile parameters
        - Add Segments/ODF intervals parameters
            - Fix RegionOrder between Results <=> Misc
        - Add Crimson foreground-color for parameters that will eat up RAM (RefreshHighlighting())
            - Or are better to leave at default, unless Higher Timeframes(Bars >= h2) are used.
        - Add ReplaceByATR (override loaded row config by ATR in LoadParams())
        - Add IsVisible conditions to specific parameters
        - Add OnChanged to all new parameters added so far.
            - Revision of Static Update for new parameters
        - Change prefix for LocalStorage to ODFT-AGG (GetStorageKey())
        - Add Outside.UseCustomMAs condition to every "MA Type" inputs (3)
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
    public enum Supported_Timeframes {
        m5, m10, m15, m30, m45, h1, h2, h3, h4, h6, h8, h12, D1, D2, D3
    }

    public class ParamsPanel : CustomControl
    {
        private readonly OrderFlowTicksV20 Outside;
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
                    Key = "DaysToShowKey",
                    Label = "Nº Days",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.N_Days,
                    OnChanged = _ => UpdateDaysToShow()
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
                    RegionOrder = 2,
                    Key = "LargestDividedKey",
                    Label = "Largest?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.OnlyLargestDivided,
                    OnChanged = _ => UpdateCheckbox("LargestDividedKey", val => Outside.ColoringOnlyLarguest = val),
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Normal && Outside.VolumeView_Input == VolumeView_Data.Divided && !Outside.EnableBubblesChart
                },

                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "EnableVPKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableVP,
                    OnChanged = _ => UpdateCheckbox("EnableVPKey", val => Outside.EnableVP = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "UpdateVPKey",
                    Label = "Update At",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UpdateProfileStrategy.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UpdateProfile_Data)),
                    OnChanged = _ => UpdateVP(),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "FillVPKey",
                    Label = "Fill Histogram?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillHist_VP,
                    OnChanged = _ => UpdateCheckbox("FillVPKey", val => Outside.FillHist_VP = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "SideVPKey",
                    Label = "Side",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.HistogramSide.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(HistSide_Data)),
                    OnChanged = _ => UpdateSideVP(),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "WidthVPKey",
                    Label = "Width",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.HistogramWidth.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(HistWidth_Data)),
                    OnChanged = _ => UpdateWidthVP(),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "NumbersVPKey",
                    Label = "Historical Nºs?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowHistoricalNumbers_VP,
                    OnChanged = _ => UpdateCheckbox("NumbersVPKey", val => Outside.ShowHistoricalNumbers_VP = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "IntradayVPKey",
                    Label = "Intraday?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowIntradayProfile,
                    OnChanged = _ => UpdateCheckbox("IntradayVPKey", val => Outside.ShowIntradayProfile = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },

                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 5,
                    Key = "IntraOffsetKey",
                    Label = "Offset(bars)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.OffsetBarsIntraday.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateIntradayOffset(),
                    IsVisible = () => Outside.ShowIntradayProfile && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "IntraTFKey",
                    Label = "Offset(time)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.OffsetTimeframeIntraday.ShortName,
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateIntradayTimeframe(),
                    IsVisible = () => Outside.ShowIntradayProfile && Outside.isPriceBased_Chart && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MiniVPsKey",
                    Label = "Mini-VPs?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableMiniProfiles,
                    OnChanged = _ => UpdateCheckbox("MiniVPsKey", val => Outside.EnableMiniProfiles = val),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MiniTFKey",
                    Label = "Mini-Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.MiniVPsTimeframe.ShortName.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateMiniVPTimeframe(),
                    IsVisible = () => Outside.EnableMiniProfiles && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MiniResultKey",
                    Label = "Mini-Result?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowMiniResults,
                    OnChanged = _ => UpdateCheckbox("MiniResultKey", val => Outside.ShowMiniResults = val),
                    IsVisible = () => Outside.EnableMiniProfiles && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "FixedRangeKey",
                    Label = "Fixed Range?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FixedRange,
                    OnChanged = _ => UpdateCheckbox("FixedRangeKey", val => Outside.EnableFixedRange = val),
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "WeeklyVPKey",
                    Label = "Weekly VP?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableWeeklyProfile,
                    OnChanged = _ => UpdateCheckbox("WeeklyVPKey", val => Outside.EnableWeeklyProfile = val),
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Buy_Sell && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MonthlyVPKey",
                    Label = "Monthly VP?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableMonthlyProfile,
                    OnChanged = _ => UpdateCheckbox("MonthlyVPKey", val => Outside.EnableMonthlyProfile = val),
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Buy_Sell && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "IntraNumbersKey",
                    Label = "Intra-Nºs?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowIntradayNumbers,
                    OnChanged = _ => UpdateCheckbox("IntraNumbersKey", val => Outside.ShowIntradayNumbers = val),
                    IsVisible = () => Outside.ShowIntradayProfile && !Outside.EnableBubblesChart
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
                    Key = "SpikeMATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.customMAtype_Spike.ToString() : p.MAtype_Spike.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateSpikeMAType(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikePeriodKey",
                    Label = "MA Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.MAperiod_Spike.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSpikeMAPeriod(),
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
                    RegionOrder = 3,
                    Key = "SpikeTypeKey",
                    Label = "Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.Spike_NotificationType.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(NotificationType_Data)),
                    OnChanged = _ => UpdateSpikeNotifyType(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta &&
                                        Outside.EnableSpikeNotification && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeSoundKey",
                    Label = "Sound",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.Spike_SoundType.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SoundType)),
                    OnChanged = _ => UpdateSpikeSound(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta &&
                                        Outside.EnableSpikeNotification && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeLevelsKey",
                    Label = "Levels?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowSpikeLevels,
                    OnChanged = _ => UpdateCheckbox("SpikeLevelsKey", val => Outside.ShowSpikeLevels = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeLvsTouchKey",
                    Label = "Max Touch",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeLevels_MaxCount.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSpikeLevels_MaxCount(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowSpikeLevels && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeLvsColorKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeLevelsColoring.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeLevelsColoring_Data)),
                    OnChanged = _ => UpdateSpikeLevels_Coloring(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowSpikeLevels && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeChartKey",
                    Label = "Chart?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableSpikeChart,
                    OnChanged = _ => UpdateCheckbox("SpikeChartKey", val => Outside.EnableSpikeChart = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeColorKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeChartColoring.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeChartColoring_Data)),
                    OnChanged = _ => UpdateSpikeChart_Coloring(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta &&
                                        Outside.EnableSpikeChart && !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 3,
                    Key = "SpikeLvsResetKey",
                    Label = "Reset Daily?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.SpikeLevels_ResetDaily,
                    OnChanged = _ => UpdateCheckbox("SpikeLvsResetKey", val => Outside.SpikeLevels_ResetDaily = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowSpikeLevels && !Outside.EnableBubblesChart
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
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubblesSizeKey",
                    Label = "Size Multiplier",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesSize.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateBubblesSize(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
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
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
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
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubbMATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.customMAtype_Bubbles.ToString() : p.MAtype_Bubbles.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateBubblesMAType(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "BubbMAPeriodKey",
                    Label = "MA Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.MAperiod_Bubbles.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateBubblesMAPeriod(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
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
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
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
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.BubblesColoring_Input == BubblesColoring_Data.Momentum && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraNotifyKey",
                    Label = "Ultra Notify?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableUltraBubblesNotifiy,
                    OnChanged = _ => UpdateCheckbox("UltraNotifyKey", val => Outside.EnableUltraBubblesNotification = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraTypeKey",
                    Label = "Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UltraBubbles_NotifyType.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(NotificationType_Data)),
                    OnChanged = _ => UpdateUltraNotifyType(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.EnableUltraBubblesNotification && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraSoundKey",
                    Label = "Sound",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UltraBubbles_SoundType.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SoundType)),
                    OnChanged = _ => UpdateUltraSound(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.EnableUltraBubblesNotification && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraLevelskey",
                    Label = "Ultra Levels?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowUltraBubblesLevels,
                    OnChanged = _ => UpdateCheckbox("UltraLevelskey", val => Outside.ShowUltraBubblesLevels = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraCountKey",
                    Label = "Max Touch",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.UltraBubbles_MaxCount,
                    OnChanged = _ => UpdateUltraLevels_MaxCount(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowUltraBubblesLevels && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraBreakKey",
                    Label = "Touch from",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UltraBubblesBreak.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UltraBubblesBreak_Data)),
                    OnChanged = _ => UpdateUltraBreakStrategy(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowUltraBubblesLevels && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraResetKey",
                    Label = "Reset Daily?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.UltraBubbles_ResetDaily,
                    OnChanged = _ => UpdateCheckbox("UltraResetKey", val => Outside.UltraBubbles_ResetDaily = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowUltraBubblesLevels && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraRectSizeKey",
                    Label = "Level Size",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UltraBubbles_RectSize.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UltraBubbles_RectSizeData)),
                    OnChanged = _ => UpdateUltraRectangleSize(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowUltraBubblesLevels && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 4,
                    Key = "UltraColoringKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UltraBubblesColoring.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UltraBubblesColoring_Data)),
                    OnChanged = _ => UpdateUltraColoring(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowUltraBubblesLevels && !Outside.EnableSpikeChart
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
                    IsVisible = () => !Outside.EnableBubblesChart && !Outside.EnableSpikeChart
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
                    IsVisible = () => !Outside.EnableBubblesChart && !Outside.EnableSpikeChart
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
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "LargeMATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.customMAtype_Large.ToString() : p.MAtype_Large.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateLargeMAType(),
                    IsVisible = () => Outside.EnableLargeFilter && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "LargePeriodKey",
                    Label = "MA Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.MAperiod_Large.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLargeMAPeriod(),
                    IsVisible = () => Outside.EnableLargeFilter && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "LargeRatioKey",
                    Label = "Ratio",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.LargeFilter_Ratio.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLargeRatio(),
                    IsVisible = () => Outside.EnableLargeFilter && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
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
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Normal && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
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
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Normal && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 5,
                    Key = "OnlySubtKey",
                    Label = "Only Subtract?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowOnlySubtDelta,
                    OnChanged = _ => UpdateCheckbox("OnlySubtKey", val => Outside.ShowOnlySubtDelta = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowMinMaxDelta && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
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
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Buy_Sell && (!Outside.EnableBubblesChart && !Outside.EnableSpikeChart)
                },

                new()
                {
                    Region = "Misc",
                    RegionOrder = 6,
                    Key = "ShowHistKey",
                    Label = "Histogram?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowHist,
                    OnChanged = _ => UpdateCheckbox("ShowHistKey", val => Outside.ShowHist = val),
                    IsVisible = () => !Outside.EnableBubblesChart && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 6,
                    Key = "FillHistKey",
                    Label = "Fill Hist?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillHist,
                    OnChanged = _ => UpdateCheckbox("FillHistKey", val => Outside.FillHist = val),
                    IsVisible = () => !Outside.EnableBubblesChart && !Outside.EnableSpikeChart
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 6,
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
                    RegionOrder = 6,
                    Key = "DrawAtKey",
                    Label = "Draw at Zoom",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.DrawAtZoom_Value.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateDrawAtZoom()
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 6,
                    Key = "SegmentsKey",
                    Label = "Segments",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SegmentsInterval.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SegmentsInterval_Data)),
                    OnChanged = _ => UpdateSegmentsInterval(),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 6,
                    Key = "ODFIntervalKey",
                    Label = "ODF + VP",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.VPInterval.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ODFInterval_Data)),
                    OnChanged = _ => UpdateODFInterval(),
                    IsVisible = () => !Outside.EnableBubblesChart
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 6,
                    Key = "BubbleValueKey",
                    Label = "Bubbles-V?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowBubbleValue,
                    OnChanged = _ => UpdateCheckbox("BubbleValueKey", val => Outside.ShowBubbleValue = val),
                    IsVisible = () => Outside.EnableBubblesChart
                },

                new()
                {
                    Region = "Misc",
                    RegionOrder = 6,
                    Key = "FillIntraVPKey",
                    Label = "Intra-Space?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillIntradaySpace,
                    OnChanged = _ => UpdateCheckbox("FillIntraVPKey", val => Outside.FillIntradaySpace = val),
                    IsVisible = () => Outside.ShowIntradayProfile && (Outside.EnableWeeklyProfile || Outside.EnableMonthlyProfile) && !Outside.EnableBubblesChart
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
                Text = "Order Flow Ticks\nAggregated",
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
                case "EnableBubblesKey":
                    if (value)
                        Outside.Chart.ChartType = ChartType.Line;
                    else if (!value && _originalValues.ContainsKey("ShowHistKey")) {
                        // ContainsKey avoids crash when loading
                        Outside.ShowHist = (bool)_originalValues["ShowHistKey"];
                        Outside.ShowNumbers = (bool)_originalValues["ShowNumbersKey"];
                        Outside.ShowResults = (bool)_originalValues["ShowResultsKey"];
                        Outside.EnableSpikeFilter = (bool)_originalValues["EnableSpikeKey"];
                        Outside.EnableVP = (bool)_originalValues["EnableVPKey"];
                        Outside.EnableMiniProfiles = (bool)_originalValues["MiniVPsKey"];
                        Outside.Chart.ChartType = ChartType.Hlc;
                    }
                    break;
                case "SpikeChartKey":
                    if (value)
                        Outside.Chart.ChartType = ChartType.Hlc;
                    else if (!value && _originalValues.ContainsKey("ShowHistKey")) {
                        // ContainsKey avoids crash when loading
                        Outside.EnableSpikeFilter = (bool)_originalValues["EnableSpikeKey"];
                        Outside.ShowHist = (bool)_originalValues["ShowHistKey"];
                        Outside.ShowResults = (bool)_originalValues["ShowResultsKey"];
                        Outside.ShowMinMaxDelta = (bool)_originalValues["ShowMinMaxKey"];
                        Outside.Chart.ChartType = ChartType.Hlc;
                    }
                    break;
                case "IntradayVPKey":
                    RecalculateOutsideWithMsg(Outside.ShowIntradayNumbers);
                    return;
                case "FillIntraVPKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "EnableNotifyKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "SpikeLvsResetKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "UltraNotifyKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "UltraResetKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "FillHistKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "FixedRangeKey":
                    RangeBtn.IsVisible = value;
                    return;
            }

            RecalculateOutsideWithMsg();
        }

        // ==== General ====
        private void UpdateDaysToShow()
        {
            int value = int.TryParse(textInputMap["DaysToShowKey"].Text, out var n) ? n : -2;
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
        private void UpdateVolumeView()
        {
            var selected = comboBoxMap["VolumeViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out VolumeView_Data viewType) && viewType != Outside.VolumeView_Input)
            {
                Outside.VolumeView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Volume Profile ====
        private void UpdateVP()
        {
            var selected = comboBoxMap["UpdateVPKey"].SelectedItem;
            if (Enum.TryParse(selected, out UpdateProfile_Data updateType) && updateType != Outside.UpdateProfile_Input)
            {
                Outside.UpdateProfile_Input = updateType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSideVP()
        {
            var selected = comboBoxMap["SideVPKey"].SelectedItem;
            if (Enum.TryParse(selected, out HistSide_Data sideType) && sideType != Outside.HistogramSide_Input)
            {
                Outside.HistogramSide_Input = sideType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateWidthVP()
        {
            var selected = comboBoxMap["WidthVPKey"].SelectedItem;
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
        private void UpdateMiniVPTimeframe()
        {
            var selected = comboBoxMap["MiniTFKey"].SelectedItem;
            TimeFrame value = StringToTimeframe(selected);
            if (value != TimeFrame.Minute && value != Outside.MiniVPs_Timeframe)
            {
                Outside.MiniVPs_Timeframe = value;
                Outside.SetMiniVPsBars();
                RecalculateOutsideWithMsg();
            }
        }
        private static TimeFrame StringToTimeframe(string inputTF)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                // Candles
                case "m5": return TimeFrame.Minute5;
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

        // ==== Spike Filter ====
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
        private void UpdateSpikeFilter()
        {
            var selected = comboBoxMap["SpikeFilterKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeFilter_Data filterType) && filterType != Outside.SpikeFilter_Input)
            {
                Outside.SpikeFilter_Input = filterType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateSpikeMAType()
        {
            var selected = comboBoxMap["SpikeMATypeKey"].SelectedItem;
            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.customMAtype_Spike)
                {
                    Outside.customMAtype_Spike = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.MAtype_Spike)
                {
                    Outside.MAtype_Spike = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }
        private void UpdateSpikeMAPeriod()
        {
            if (int.TryParse(textInputMap["SpikePeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.MAperiod_Spike)
                {
                    Outside.MAperiod_Spike = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateSpikeNotifyType()
        {
            var selected = comboBoxMap["SpikeTypeKey"].SelectedItem;
            if (Enum.TryParse(selected, out NotificationType_Data notifyType) && notifyType != Outside.Spike_NotificationType_Input)
            {
                Outside.Spike_NotificationType_Input = notifyType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSpikeSound()
        {
            var selected = comboBoxMap["SpikeSoundKey"].SelectedItem;
            if (Enum.TryParse(selected, out SoundType soundType) && soundType != Outside.Spike_SoundType)
            {
                Outside.Spike_SoundType = soundType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSpikeLevels_MaxCount()
        {
            if (int.TryParse(textInputMap["SpikeLvsTouchKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.SpikeLevels_MaxCount)
                {
                    Outside.SpikeLevels_MaxCount = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateSpikeLevels_Coloring()
        {
            var selected = comboBoxMap["SpikeLvsColorKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeLevelsColoring_Data coloringType) && coloringType != Outside.SpikeLevelsColoring_Input)
            {
                Outside.SpikeLevelsColoring_Input = coloringType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSpikeChart_Coloring()
        {
            var selected = comboBoxMap["SpikeColorKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeChartColoring_Data coloringType) && coloringType != Outside.SpikeChartColoring_Input)
            {
                Outside.SpikeChartColoring_Input = coloringType;
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
                RecalculateOutsideWithMsg(Outside.ShowUltraBubblesLevels);
            }
        }
        private void UpdateBubblesFilter()
        {
            var selected = comboBoxMap["BubblesFilterKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesFilter_Data filterType) && filterType != Outside.BubblesFilter_Input)
            {
                Outside.BubblesFilter_Input = filterType;
                RecalculateOutsideWithMsg(Outside.ShowUltraBubblesLevels);
            }
        }
        private void UpdateBubblesMAType() {
            var selected = comboBoxMap["BubbMATypeKey"].SelectedItem;

            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.customMAtype_Bubbles)
                {
                    Outside.customMAtype_Bubbles = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.MAtype_Bubbles)
                {
                    Outside.MAtype_Bubbles = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }

        private void UpdateBubblesMAPeriod() {
            if (int.TryParse(textInputMap["BubbMAPeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.MAperiod_Bubbles)
                {
                    Outside.MAperiod_Bubbles = value;
                    SetApplyVisibility();
                }
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
        private void UpdateUltraNotifyType()
        {
            var selected = comboBoxMap["UltraTypeKey"].SelectedItem;
            if (Enum.TryParse(selected, out NotificationType_Data notifyType) && notifyType != Outside.UltraBubbles_NotificationType_Input)
            {
                Outside.UltraBubbles_NotificationType_Input = notifyType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraSound()
        {
            var selected = comboBoxMap["UltraSoundKey"].SelectedItem;
            if (Enum.TryParse(selected, out SoundType soundType) && soundType != Outside.UltraBubbles_SoundType)
            {
                Outside.Spike_SoundType = soundType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraLevels_MaxCount()
        {
            if (int.TryParse(textInputMap["UltraCountKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.UltraBubbles_MaxCount)
                {
                    Outside.UltraBubbles_MaxCount = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateUltraBreakStrategy()
        {
            var selected = comboBoxMap["UltraBreakKey"].SelectedItem;
            if (Enum.TryParse(selected, out UltraBubblesBreak_Data breakType) && breakType != Outside.UltraBubblesBreak_Input)
            {
                Outside.UltraBubblesBreak_Input = breakType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraRectangleSize()
        {
            var selected = comboBoxMap["UltraRectSizeKey"].SelectedItem;
            if (Enum.TryParse(selected, out UltraBubbles_RectSizeData rectSizeType) && rectSizeType != Outside.UltraBubbles_RectSizeInput)
            {
                Outside.UltraBubbles_RectSizeInput = rectSizeType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraColoring()
        {
            var selected = comboBoxMap["UltraColoringKey"].SelectedItem;
            if (Enum.TryParse(selected, out UltraBubblesColoring_Data coloringType) && coloringType != Outside.UltraBubblesColoring_Input)
            {
                Outside.UltraBubblesColoring_Input = coloringType;
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
        private void UpdateLargeMAType()
        {
            var selected = comboBoxMap["LargeMATypeKey"].SelectedItem;

            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.customMAtype_Large)
                {
                    Outside.customMAtype_Large = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.MAtype_Large)
                {
                    Outside.MAtype_Large = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }
        private void UpdateLargeMAPeriod()
        {
            if (int.TryParse(textInputMap["LargePeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.MAperiod_Large)
                {
                    Outside.MAperiod_Large = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateLargeRatio()
        {
            if (double.TryParse(textInputMap["LargeRatioKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.LargeFilter_Ratio)
                {
                    Outside.LargeFilter_Ratio = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateOperator()
        {
            var selected = comboBoxMap["OperatorKey"].SelectedItem;
            if (Enum.TryParse(selected, out OperatorBuySell_Data op) && op != Outside.OperatorBuySell_Input)
            {
                Outside.OperatorBuySell_Input = op;
                RecalculateOutsideWithMsg();
            }
        }

        // ==== Misc ====
        private void UpdateDrawAtZoom()
        {
            if (int.TryParse(textInputMap["DrawAtKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.DrawAtZoom_Value)
                {
                    Outside.DrawAtZoom_Value = value;
                }
            }
        }
        private void UpdateSegmentsInterval()
        {
            var selected = comboBoxMap["SegmentsKey"].SelectedItem;
            if (Enum.TryParse(selected, out SegmentsInterval_Data segmentsType) && segmentsType != Outside.SegmentsInterval_Input)
            {
                Outside.SegmentsInterval_Input = segmentsType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateODFInterval()
        {
            var selected = comboBoxMap["ODFIntervalKey"].SelectedItem;
            if (Enum.TryParse(selected, out ODFInterval_Data intervalType) && intervalType != Outside.ODFInterval_Input)
            {
                Outside.ODFInterval_Input = intervalType;
                RecalculateOutsideWithMsg();
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
                    Outside.Chart.ResetBarColors();
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

            Outside.VolumeMode_Input = Outside.VolumeMode_Input switch
            {
                VolumeMode_Data.Normal => VolumeMode_Data.Buy_Sell,
                VolumeMode_Data.Buy_Sell => VolumeMode_Data.Delta,
                _ => VolumeMode_Data.Normal
            };
            ModeBtn.Text = Outside.VolumeMode_Input.ToString();
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

            Outside.VolumeMode_Input = Outside.VolumeMode_Input switch
            {
                VolumeMode_Data.Delta => VolumeMode_Data.Buy_Sell,
                VolumeMode_Data.Buy_Sell => VolumeMode_Data.Normal,
                _ => VolumeMode_Data.Delta
            };
            ModeBtn.Text = Outside.VolumeMode_Input.ToString();
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
                        if (param.Key == "WeeklyVPKey")
                            checkBoxTextMap[param.Key].ForegroundColor = Color.Crimson;
                        if (param.Key == "MonthlyVPKey")
                            checkBoxTextMap[param.Key].ForegroundColor = Color.Crimson;
                        checkBoxTextMap[param.Key].FontStyle = fontStyle;
                        break;
                    case ParamInputType.ComboBox:
                        if (param.Key == "SegmentsKey")
                            comboBoxTextMap[param.Key].ForegroundColor = Color.Crimson;
                        if (param.Key == "ODFIntervalKey")
                            comboBoxTextMap[param.Key].ForegroundColor = Color.Crimson;
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
                ? $"ODFT-AGG {BrokerPrefix} {SymbolPrefix} {TimeframePrefix}"
                : $"ODFT-AGG {SymbolPrefix} {TimeframePrefix}";
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