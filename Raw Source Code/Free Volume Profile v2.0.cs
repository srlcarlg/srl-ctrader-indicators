/*
--------------------------------------------------------------------------------------------------------------------------------
                        Volume Profile v2.0
                            revision 2

All core features of TPO Profile but in VOLUME
It also has the features of Order Flow Ticks

=== Volume Modes ===
*Normal/Gradient Mode = Volume Profile with Fixed/Gradient Color
*Buy vs Sell Mode = The name explains itself
*Delta Mode = Volume Delta Profile

The Volume Calculation(in Bars Volume Source)
is exported, with adaptations, from the BEST VP I have see/used for MT4/MT5,
of Russian FXcoder's https://gitlab.com/fxcoder-mql/vp (VP 10.1), author of the famous (Volume Profile + Range v6.0)
a BIG THANKS to HIM!

All parameters are self-explanatory.

.NET 6.0+ is Required

What's new in rev. 1? (after ODF_AGG)
- Rewritten using related improvements of ODF_AGG/Volume Profile.
- High-Performance VP_Bars
- Concurrent Live VP Update
- Show Any or All (Mini-VPs/Daily/Weekly/Monthly) Profiles at once!

Last update => 09/01/2026
===========================

What's new in rev. 2? (2026)
- HVN + LVN:
    - Detection:
      - Smoothing => [Gaussian, Savitzky_Golay]
      - Nodes => [LocalMinMax, Topology, Percentile]
    - Levels(bands)
      - VA-like (set by percentage)
      - (Tip) Use 'LineStyles = Solid" if any stuttering/lagging occurs when scrolling at profiles on chart (Reduce GPU workload). 
      
- Improved Performance of (all modes):
    - 'VA + POC'
    - 'Results'
    - 'Misc' => 'Distribution' (all options with less O(1) operations)
    
- Add "Segments" to "Volume Profile" => "Fixed Range?" (params-panel):
    - Monthly_Aligned (limited to the current Month)
    - From_Profile (available to any period without the 'bug' between months)
    
    
Final revision (2025)
- Fixed Range Profiles
- Code optimization/readability, mostly switch expressions
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
using static cAlgo.FreeVolumeProfileV20;
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
    public class FreeVolumeProfileV20 : Indicator
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
        [Parameter("Panel Position:", DefaultValue = PanelAlign_Data.Bottom_Left, Group = "==== Volume Profile v2.0 ====")]
        public PanelAlign_Data PanelAlign_Input { get; set; }

        public enum StorageKeyConfig_Data
        {
            Symbol_Timeframe,
            Broker_Symbol_Timeframe
        }
        [Parameter("Storage By:", DefaultValue = StorageKeyConfig_Data.Broker_Symbol_Timeframe, Group = "==== Volume Profile v2.0 ====")]
        public StorageKeyConfig_Data StorageKeyConfig_Input { get; set; }

        public enum RowConfig_Data
        {
            ATR,
            Custom,
        }
        [Parameter("Row Config:", DefaultValue = RowConfig_Data.ATR, Group = "==== Volume Profile v2.0 ====")]
        public RowConfig_Data RowConfig_Input { get; set; }

        [Parameter("Custom Row(pips):", DefaultValue = 0.2, MinValue = 0.2, Group = "==== Volume Profile v2.0 ====")]
        public double CustomHeightInPips { get; set; }


        [Parameter("ATR Period:", DefaultValue = 5, MinValue = 1, Group = "==== ATR Row Config ====")]
        public int ATRPeriod { get; set; }

        [Parameter("Row Detail(%):", DefaultValue = 60, MinValue = 20, MaxValue = 100, Group = "==== ATR Row Config ====")]
        public int RowDetailATR { get; set; }

        [Parameter("Replace Loaded Row?", DefaultValue = false, Group = "==== ATR Row Config ====")]
        public bool ReplaceByATR { get; set; }


        public enum UpdateStrategy_Data
        {
            Concurrent,
            SameThread_MayFreeze
        }
        [Parameter("[VP] Update Strategy", DefaultValue = UpdateStrategy_Data.Concurrent, Group = "==== Specific Parameters ====")]
        public UpdateStrategy_Data UpdateStrategy_Input { get; set; }

        public enum LoadBarsStrategy_Data
        {
            Sync,
            Async
        }
        [Parameter("[Source] Load Type:", DefaultValue = LoadBarsStrategy_Data.Async, Group = "==== Specific Parameters ====")]
        public LoadBarsStrategy_Data LoadBarsStrategy_Input { get; set; }

        [Parameter("[Gradient] Opacity:", DefaultValue = 60, MinValue = 5, MaxValue = 100, Group = "==== Specific Parameters ====")]
        public int OpacityHistInput { get; set; }


        [Parameter("Show Controls at Zoom(%):", DefaultValue = 10, Group = "==== Fixed Range ====")]
        public int FixedHiddenZoom { get; set; }

        [Parameter("Show Info?", DefaultValue = true, Group = "==== Fixed Range ====")]
        public bool ShowFixedInfo { get; set; }

        [Parameter("Rectangle Color:", DefaultValue = "#6087CEEB", Group = "==== Fixed Range ====")]
        public Color FixedColor { get; set; }


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


        [Parameter("Normal Color", DefaultValue = "#B287CEEB", Group = "==== Colors Histogram ====")]
        public Color HistColor  { get; set; }

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


        [Parameter("Weekly Color:", DefaultValue = "#B2FFD700", Group = "==== WM Profiles ====")]
        public Color WeeklyColor { get; set; }

        [Parameter("Monthly Color:", DefaultValue = "#920071C1", Group = "==== WM Profiles ====")]
        public Color MonthlyColor { get; set; }

        [Parameter("Weekly Gradient Min. Vol:", DefaultValue = "#FFFF9900", Group = "==== WM Profiles ====")]
        public Color WeeklyGrandient_Min { get; set; }

        [Parameter("Weekly Color Max. Vol:", DefaultValue = "#FFE42226", Group = "==== WM Profiles ====")]
        public Color WeeklyGrandient_Max { get; set; }

        [Parameter("Monthly Gradient Min. Vol:", DefaultValue = "#FF090979", Group = "==== WM Profiles ====")]
        public Color MonthlyGrandient_Min { get; set; }

        [Parameter("Monthly Color Max. Vol:", DefaultValue = "#FF33C1F3", Group = "==== WM Profiles ====")]
        public Color MonthlyGrandient_Max { get; set; }


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

        
        [Parameter("Color HVN:", DefaultValue = "Gold" , Group = "==== HVN/LVN ====")]
        public Color ColorHVN { get; set; }
        
        [Parameter("LineStyle HVN:", DefaultValue = LineStyle.LinesDots, Group = "==== HVN/LVN ====")]
        public LineStyle LineStyleHVN { get; set; }

        [Parameter("Thickness HVN:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== HVN/LVN ====")]
        public int ThicknessHVN { get; set; }

        [Parameter("Color LVN:", DefaultValue = "Crimson", Group = "==== HVN/LVN ====")]
        public Color ColorLVN { get; set; }

        [Parameter("LineStyle LVN:", DefaultValue = LineStyle.LinesDots, Group = "==== HVN/LVN ====")]
        public LineStyle LineStyleLVN { get; set; }

        [Parameter("Thickness LVN:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== HVN/LVN ====")]
        public int ThicknessLVN { get; set; }


        [Parameter("Color Band:", DefaultValue = "#19F0F8FF",  Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public Color ColorBand { get; set; }
        
        [Parameter("Color Lower:", DefaultValue = "PowderBlue",  Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public Color ColorBand_Lower { get; set; }

        [Parameter("Color Upper:", DefaultValue = "PowderBlue",  Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public Color ColorBand_Upper { get; set; }

        [Parameter("LineStyle Bands:", DefaultValue = LineStyle.Dots, Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public LineStyle LineStyleBands { get; set; }

        [Parameter("Thickness Bands:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public int ThicknessBands { get; set; }
        

        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }

        // Moved from cTrader Input to Params Panel

        // ==== General ====
        public int Lookback = 1;
        public enum VolumeMode_Data
        {
            Normal,
            Buy_Sell,
            Delta,
        }
        public VolumeMode_Data VolumeMode_Input = VolumeMode_Data.Normal;

        public enum VPInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }
        public VPInterval_Data VPInterval_Input = VPInterval_Data.Daily;


        // ==== Volume Profile ====
        public bool EnableVP = false;

        public enum UpdateProfile_Data
        {
            EveryTick_CPU_Workout,
            ThroughSegments_Balanced,
            Through_2_Segments_Best,
        }
        public UpdateProfile_Data UpdateProfile_Input = UpdateProfile_Data.Through_2_Segments_Best;
        public bool FillHist_VP = true;

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
        public bool EnableGradient = true;

        public enum Distribution_Data
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
        public Distribution_Data Distribution_Input = Distribution_Data.OHLC;
        public bool ShowOHLC = false;
        public bool EnableFixedRange = false;


        // ==== Intraday Profiles ====
        public bool ShowIntradayProfile = false;
        public int OffsetBarsInput = 2;
        public TimeFrame OffsetTimeframeInput = TimeFrame.Hour;
        public bool FillIntradaySpace { get; set; }


        // ==== Mini VPs ====
        public bool EnableMiniProfiles = true;
        public TimeFrame MiniVPs_Timeframe = TimeFrame.Hour4;
        public bool ShowMiniResults = true;


        // ==== VA + POC ====
        public bool ShowVA = false;
        public int PercentVA = 65;
        public bool KeepPOC = true;
        public bool ExtendPOC = false;
        public bool ExtendVA = false;
        public int ExtendCount = 1;

        
        // ==== HVN + LVN ====        
        public bool EnableNodeDetection = false;

        public enum ProfileSmooth_Data
        {
            Gaussian,
            Savitzky_Golay
        }
        public ProfileSmooth_Data ProfileSmooth_Input = ProfileSmooth_Data.Gaussian;

        public enum ProfileNode_Data
        {
            LocalMinMax,
            Topology,
            Percentile
        }
        public ProfileNode_Data ProfileNode_Input = ProfileNode_Data.LocalMinMax;

        public int pctileHVN_Value = 90;
        public int pctileLVN_Value = 25;

        public bool onlyStrongNodes = false;
        public double strongHVN_Pct = 23.6;
        public double strongLVN_Pct = 55.3;

        public double bandHVN_Pct = 61.8;
        public double bandLVN_Pct = 23.6;

        public bool extendNodes = false;
        public int extendNodes_Count = 1;
        public bool extendNodes_WithBands = false;
        public bool extendNodes_FromStart = true;


        public enum ShowNode_Data
        {
            HVN_With_Bands,
            HVN_Raw,
            LVN_With_Bands,
            LVN_Raw
        }
        public ShowNode_Data ShowNode_Input = ShowNode_Data.HVN_With_Bands;


        // ==== Results ====
        public bool ShowResults = true;

        public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }
        public OperatorBuySell_Data OperatorBuySell_Input = OperatorBuySell_Data.Subtraction;

        public bool ShowMinMaxDelta = false;
        public bool ShowOnlySubtDelta = true;

        public TimeFrame VOL_Timeframe = TimeFrame.Minute;

        // Allow "old" segmentation "From_Profile", 
        // so the "Fixed Range" doesn't "bug" => remains on chart between months (end/start of each month)
        public enum SegmentsFixedRange_Data
        {
            Monthly_Aligned,
            From_Profile
        }       
        public SegmentsFixedRange_Data SegmentsFixedRange_Input = SegmentsFixedRange_Data.From_Profile;
        
        // Always Monthly
        public enum SegmentsInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }
        public SegmentsInterval_Data SegmentsInterval_Input = SegmentsInterval_Data.Monthly;

        // ======================================================

        public readonly string NOTIFY_CAPTION = "Free Volume Profile \n    v2.0";

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
        private readonly IDictionary<string, List<double>> segmentsFromProfile = new Dictionary<string, List<double>>();
        private List<double> Segments_VP = new();

        // Volume Profile Bars
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
            public int _VP_Interval = 0;
            public int _Mini = 0;
        }
        private readonly CleanedIndex lastCleaned = new();

        // Concurrent Live VP Update
        private readonly object _lockSource = new();
        private readonly object _lockBars = new();
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
            public int VP { get; set; }
            public int Mini { get; set; }
            public int Weekly { get; set; }
            public int Monthly { get; set; }
        }
        private readonly LiveVPIndex liveVP_StartIndexes = new();
        private List<Bar> Bars_SourceList = new();
        private DateTime[] Bars_ChartArray = Array.Empty<DateTime>();

        // High-Performance VP_Bars()
        public class LastBarIndex {
            public int _Mini = 0;
            public int _MiniStart = 0;
            public int _Weekly = 0;
            public int _WeeklyStart = 0;
            public int _Monthly = 0;
            public int _MonthlyStart = 0;
        }
        private readonly LastBarIndex lastBar_ExtraProfiles = new();
        private int lastBar_VP = 0;
        private int lastBar_VPStart = 0;
        private Bars VOL_Bars;

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
        private bool isUpdateVP = false;
        private bool isEndChart = false;
        public bool isPriceBased_Chart = false;
        public bool isRenkoChart = false;
        private double prevUpdatePrice;

        // Timer
        private class TimerHandler {
            public bool isAsyncLoading = false;
        }
        private readonly TimerHandler timerHandler = new();

        PopupNotification asyncBarsPopup = null;
        private bool loadingAsyncBars = false;
        private bool loadingBarsComplete = false;

        // HVN + LVN => Performance
        private double[] nodesKernel = null;
        
        // Params Panel
        private Border ParamBorder;
        public class IndicatorParams
        {
            // ==== General ====
            public int LookBack { get; set; }
            public VolumeMode_Data VolMode { get; set; }
            public double RowHeightInPips { get; set; }
            public VPInterval_Data VPInterval { get; set; }

            // ==== Volume Profile ====
            public bool EnableVP { get; set; }
            public bool EnableWeeklyProfile { get; set; }
            public bool EnableMonthlyProfile { get; set; }

            // View
            public bool FillHist_VP { get; set; }
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

            // ==== Mini VPs ====
            public bool EnableMiniProfiles { get; set; }
            public TimeFrame MiniVPsTimeframe { get; set; }
            public bool ShowMiniResults { get; set; }

            // ==== VA + POC ====
            public bool ShowVA { get; set; }
            public int PercentVA { get; set; }
            public bool KeepPOC { get; set; }
            public bool ExtendPOC { get; set; }
            public bool ExtendVA { get; set; }
            public int ExtendCount { get; set; }
            
            
            // ==== HVN + LVN ====        
            public bool EnableNodes { get; set; }

            public ProfileSmooth_Data ProfileSmooth { get; set; }

            public ProfileNode_Data ProfileNode { get; set; }

            public ShowNode_Data ShowNode { get; set; }
            public double BandHVN { get; set; }
            public double BandLVN { get; set; }

            public bool OnlyStrong { get; set; }
            public double StrongHVN { get; set; }
            public double StrongLVN { get; set; }
            
            public int PctileHVN { get; set; }
            public int PctileLVN { get; set; }

            public bool ExtendNodes { get; set; }
            public int ExtendNodes_Count { get; set; }
            public bool ExtendNodes_WithBands { get; set; }
            public bool ExtendNodes_FromStart { get; set; }


            // ==== Results ====
            public bool ShowResults { get; set; }
            // Results - Buy_Sell / Delta
            public bool ShowSideTotal { get; set; }
            public OperatorBuySell_Data OperatorBuySell { get; set; }
            // Results - Delta
            public bool ShowMinMax { get; set; }
            public bool ShowOnlySubtDelta { get; set; }

            // ==== Misc ====
            public UpdateProfile_Data UpdateProfileStrategy { get; set; }
            public TimeFrame Source { get; set; }
            public Distribution_Data Distribution { get; set; }
            public SegmentsFixedRange_Data SegmentsFixedRange { get; set; }
        }

        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "VP",
                Padding = 0,
                Height = 22,
                Width = 30, // Fix MacOS => stretching button when StackPanel is used.
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
                        MiniVPs_Timeframe = TimeFrame.Hour;
                    else if (Chart.TimeFrame == TimeFrame.Minute2)
                        MiniVPs_Timeframe = TimeFrame.Hour2;
                    else if (Chart.TimeFrame <= TimeFrame.Minute4)
                        MiniVPs_Timeframe = TimeFrame.Hour3;
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute5)
                        MiniVPs_Timeframe = TimeFrame.Hour4;
                    else if (Chart.TimeFrame == TimeFrame.Minute6)
                        MiniVPs_Timeframe = TimeFrame.Hour6;
                    else if (Chart.TimeFrame <= TimeFrame.Minute8)
                        MiniVPs_Timeframe = TimeFrame.Hour8;
                    else if (Chart.TimeFrame <= TimeFrame.Minute10)
                        MiniVPs_Timeframe = TimeFrame.Hour12;
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour)
                        MiniVPs_Timeframe = TimeFrame.Daily;

                    else if (Chart.TimeFrame <= TimeFrame.Hour8) {
                        EnableVP = true;
                        EnableMiniProfiles = false;
                        VPInterval_Input = VPInterval_Data.Weekly;
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Weekly) {
                    EnableVP = true;
                    EnableMiniProfiles = false;
                    VPInterval_Input = VPInterval_Data.Monthly;
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
            MiniVPs_Bars = MarketData.GetBars(MiniVPs_Timeframe);
            VOL_Bars = MarketData.GetBars(VOL_Timeframe);

            // Concurrent Live VP
            Bars.BarOpened += (_) => {
                isUpdateVP = true;
                if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                    prevUpdatePrice = _.Bars.LastBar.Close;
            };

            // Chart
            string currentTimeframe = Chart.TimeFrame.ToString();
            isPriceBased_Chart = currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick");
            isRenkoChart = Chart.TimeFrame.ToString().Contains("Renko");

            DrawStartVolumeLine();

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
                VolMode = VolumeMode_Input,
                RowHeightInPips = heightPips,
                VPInterval = VPInterval_Input,

                // Volume Profile
                EnableVP = EnableVP,
                EnableWeeklyProfile = EnableWeeklyProfile,
                EnableMonthlyProfile = EnableMonthlyProfile,

                // View
                FillHist_VP = FillHist_VP,
                HistogramSide = HistogramSide_Input,
                HistogramWidth = HistogramWidth_Input,
                Gradient = EnableGradient,
                OHLC = ShowOHLC,
                FixedRange = EnableFixedRange,

                // Intraday View
                ShowIntradayProfile = ShowIntradayProfile,
                OffsetBarsIntraday = OffsetBarsInput,
                OffsetTimeframeIntraday = OffsetTimeframeInput,
                FillIntradaySpace = FillIntradaySpace,

                // Mini VPs
                EnableMiniProfiles = EnableMiniProfiles,
                MiniVPsTimeframe = MiniVPs_Timeframe,
                ShowMiniResults = ShowMiniResults,

                // VA + POC
                ShowVA = ShowVA,
                PercentVA = PercentVA,
                KeepPOC = KeepPOC,
                ExtendPOC = ExtendPOC,
                ExtendVA = ExtendVA,
                ExtendCount = ExtendCount,

                // HVN + LVN
                EnableNodes = EnableNodeDetection,
                ProfileSmooth = ProfileSmooth_Input,
                ProfileNode = ProfileNode_Input,
                
                ShowNode = ShowNode_Input,
                BandHVN = bandHVN_Pct,
                BandLVN = bandLVN_Pct,

                OnlyStrong = onlyStrongNodes,
                StrongHVN = strongHVN_Pct,
                StrongLVN = strongLVN_Pct,

                PctileHVN = pctileHVN_Value,
                PctileLVN = pctileLVN_Value,
                
                ExtendNodes = extendNodes,
                ExtendNodes_Count = extendNodes_Count,
                ExtendNodes_WithBands = extendNodes_WithBands,
                ExtendNodes_FromStart = extendNodes_FromStart,

                // Results
                ShowResults = ShowResults,
                OperatorBuySell = OperatorBuySell_Input,
                ShowMinMax = ShowMinMaxDelta,
                ShowOnlySubtDelta = ShowOnlySubtDelta,

                // Misc
                UpdateProfileStrategy = UpdateProfile_Input,
                Source = VOL_Timeframe,
                Distribution = Distribution_Input,
                SegmentsFixedRange = SegmentsFixedRange_Input
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

            StackPanel stackPanel = new() {
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
            if (EnableVP && !IsLastBar){
                CreateMonthlyVP(index);
                CreateWeeklyVP(index);
            }

            // LookBack
            Bars vpBars = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                           VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;

            // Get Index of VP Interval to continue only in Lookback
            int iVerify = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (vpBars.ClosePrices.Count - iVerify > Lookback)
                return;

            int TF_idx = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int startIndex = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes[TF_idx]);

            // === Clean Dicts/others ===
            if (index == startIndex ||
                (index - 1) == startIndex && isPriceBased_Chart ||
                (index - 1) == startIndex && (index - 1) != lastCleaned._VP_Interval
            )
                CleanUp_MainVP(index, startIndex);

            // Historical data
            if (!IsLastBar)
            {
                // Allows MiniVPs if (!EnableVP)
                CreateMiniVPs(index);

                if (EnableVP)
                    VolumeProfile(startIndex, index);

                isUpdateVP = true; // chart end
            }
            else
            {
                if (UpdateStrategy_Input == UpdateStrategy_Data.SameThread_MayFreeze)
                {
                    if (EnableVP)
                        LiveVP_Update(startIndex, index);
                    else if (!EnableVP && EnableMiniProfiles)
                        LiveVP_Update(startIndex, index, true);
                }
                else
                    LiveVP_Concurrent(index, startIndex);

                if (!isEndChart) {
                    LoadMoreHistory_IfNeeded();
                    isEndChart = true;
                }
            }
        }

        private void CleanUp_MainVP(int index, int startIndex)
        {
            // Reset VP
            // Segments are identified by TF_idx(start)
            // No need to clean up even if it's Daily Interval
            if (!IsLastBar)
                lastBar_VPStart = lastBar_VP;
            VP_VolumesRank.Clear();
            VP_VolumesRank_Up.Clear();
            VP_VolumesRank_Down.Clear();
            VP_VolumesRank_Subt.Clear();
            VP_DeltaRank.Clear();

            double[] resetDelta = { 0, 0 };
            VP_MinMaxDelta = resetDelta;

            lastCleaned._VP_Interval = index == startIndex ? index : (index - 1);
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
        private void CreateSegments_FromFixedRange(double open, double lowest, double highest, string fixedKey) {
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

            currentSegments = currentSegments.OrderBy(x => x).ToList();
        
            if (!segmentsFromProfile.ContainsKey(fixedKey))
                segmentsFromProfile.Add(fixedKey, currentSegments);
            else
                segmentsFromProfile[fixedKey] = currentSegments;
        }
        private List<double> GetRangeSegments(int TF_idx, string fixedKey) 
        {
            if (SegmentsFixedRange_Input == SegmentsFixedRange_Data.From_Profile)
                return segmentsFromProfile[fixedKey];
            else
                return segmentsDict[TF_idx];
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
                        lastBar_ExtraProfiles._MiniStart = lastBar_ExtraProfiles._Mini;
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
                if (VPInterval_Input == VPInterval_Data.Weekly)
                    return;

                int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                if (index == weekStart || (index - 1) == weekStart && isPriceBased_Chart || loopStart)
                {
                    if (!IsLastBar)
                        lastBar_ExtraProfiles._WeeklyStart = lastBar_ExtraProfiles._Weekly;
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
                }
            }
        }
        private void CreateMonthlyVP(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            // Avoid recalculating the same period.
            if (VPInterval_Input == VPInterval_Data.Monthly)
                return;

            if (EnableVP && EnableMonthlyProfile)
            {
                int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                if (index == monthStart || (index - 1) == monthStart && isPriceBased_Chart || loopStart) {
                    if (!IsLastBar)
                        lastBar_ExtraProfiles._MonthlyStart = lastBar_ExtraProfiles._Monthly;
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
                }
            }
        }

        // *********** VOLUME PROFILE BARS ***********
        private void VolumeProfile(int iStart, int index, ExtraProfiles extraProfiles = ExtraProfiles.No, bool isLoop = false, bool drawOnly = false, string fixedKey = "", double fixedLowest = 0, double fixedHighest = 0)
        {
            // Weekly/Monthly on Buy_Sell is a waste of time
            if (VolumeMode_Input == VolumeMode_Data.Buy_Sell && (extraProfiles == ExtraProfiles.Weekly || extraProfiles == ExtraProfiles.Monthly))
               return;
               
            if (extraProfiles == ExtraProfiles.Fixed && SegmentsFixedRange_Input == SegmentsFixedRange_Data.From_Profile)
                CreateSegments_FromFixedRange(Bars.OpenPrices[iStart], fixedLowest, fixedHighest, fixedKey);
                
            // ==== VP ====
            if (!drawOnly)
                VP_Bars(index, extraProfiles, fixedKey);

            // ==== Drawing ====
            if (Segments_VP.Count == 0 || isLoop)
                return;

            // For Results
            Bars mainTF = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                           VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            Bars TF_Bars = extraProfiles == ExtraProfiles.No ? mainTF:
                           extraProfiles == ExtraProfiles.MiniVP ? MiniVPs_Bars :
                           extraProfiles == ExtraProfiles.Weekly ? WeeklyBars : MonthlyBars; // Fixed should use Monthly Bars, so TF_idx can be used by "whichSegment" variable

            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            double lowest = TF_Bars.LowPrices[TF_idx];
            double highest = TF_Bars.HighPrices[TF_idx];

            // Mini VPs avoid crash after recalculating
            if (double.IsNaN(lowest)) {
                lowest = TF_Bars.LowPrices.LastValue;
                highest = TF_Bars.HighPrices.LastValue;
            }

            bool gapWeekend = Bars.OpenTimes[iStart].DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart].Hour < 2;
            DateTime x1_Start = Bars.OpenTimes[iStart + (gapWeekend ? 1 : 0)];
            DateTime xBar = Bars.OpenTimes[index];

            bool isIntraday = ShowIntradayProfile && index == Chart.LastVisibleBarIndex && !isLoop;
            DateTime intraDate = xBar;
            
            // Any Volume Mode
            double maxLength = xBar.Subtract(x1_Start).TotalMilliseconds;
            bool histRightSide = HistogramSide_Input == HistSide_Data.Right;

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

            // Profile Selection
            IDictionary<double, double> vpNormal = new Dictionary<double, double>();
            if (VolumeMode_Input == VolumeMode_Data.Normal) {
                vpNormal = extraProfiles switch
                {
                    ExtraProfiles.Monthly => MonthlyRank.Normal,
                    ExtraProfiles.Weekly => WeeklyRank.Normal,
                    ExtraProfiles.MiniVP => MiniRank.Normal,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].Normal,
                    _ => VP_VolumesRank
                };
            }

            IDictionary<double, double> vpBuy = new Dictionary<double, double>();
            IDictionary<double, double> vpSell = new Dictionary<double, double>();
            if (VolumeMode_Input == VolumeMode_Data.Buy_Sell) {
                vpBuy = extraProfiles switch
                {
                    ExtraProfiles.MiniVP => MiniRank.Up,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].Up,
                    _ => VP_VolumesRank_Up
                };
                vpSell = extraProfiles switch
                {
                    ExtraProfiles.MiniVP => MiniRank.Down,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].Down,
                    _ => VP_VolumesRank_Down
                };
            }
            
            IDictionary<double, double> vpDelta = new Dictionary<double, double>();
            if (VolumeMode_Input == VolumeMode_Data.Delta) {
                vpDelta = extraProfiles switch
                {
                    ExtraProfiles.Monthly => MonthlyRank.Delta,
                    ExtraProfiles.Weekly => WeeklyRank.Delta,
                    ExtraProfiles.MiniVP => MiniRank.Delta,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].Delta,
                    _ => VP_DeltaRank
                };
            }
            
            // Same for all
            bool intraBool = extraProfiles switch
            {
                ExtraProfiles.Monthly => isIntraday,
                ExtraProfiles.Weekly => isIntraday,
                ExtraProfiles.MiniVP => false,
                ExtraProfiles.Fixed => false,
                _ => isIntraday
            };

            List<double> whichSegment = extraProfiles == ExtraProfiles.Fixed ? GetRangeSegments(TF_idx, fixedKey) : Segments_VP;
            
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
                
                void DrawRectangle_Normal(double currentVolume, double maxVolume, bool intradayProfile = false)
                {
                    double proportion = currentVolume * proportion_VP;
                    double dynLength = proportion / maxVolume;

                    DateTime x2 = x1_Start.AddMilliseconds(dynLength);

                    Color histogramColor = extraProfiles switch
                    {
                        ExtraProfiles.Monthly => MonthlyColor,
                        ExtraProfiles.Weekly => WeeklyColor,
                        _ => HistColor,
                    };

                    if (EnableGradient)
                    {
                        Color minColor = extraProfiles switch
                        {
                            ExtraProfiles.Monthly => MonthlyGrandient_Min,
                            ExtraProfiles.Weekly => WeeklyGrandient_Min,
                            _ => ColorGrandient_Min,
                        };

                        Color maxColor = extraProfiles switch
                        {
                            ExtraProfiles.Monthly => MonthlyGrandient_Max,
                            ExtraProfiles.Weekly => WeeklyGrandient_Max,
                            _ => ColorGrandient_Max,
                        };

                        double Intensity = (currentVolume * 100 / maxVolume) / 100;
                        double stepR = (maxColor.R - minColor.R) * Intensity;
                        double stepG = (maxColor.G - minColor.G) * Intensity;
                        double stepB = (maxColor.B - minColor.B) * Intensity;

                        int A = (int)(2.55 * OpacityHistInput);
                        int R = (int)Math.Round(minColor.R + stepR);
                        int G = (int)Math.Round(minColor.G + stepG);
                        int B = (int)Math.Round(minColor.B + stepB);

                        Color dynColor = Color.FromArgb(A, R, G, B);

                        histogramColor = dynColor;
                    }

                    ChartRectangle volHist = Chart.DrawRectangle($"{prefix}_{i}_VP_{extraProfiles}_Normal", x1_Start, lowerSegmentY1, x2, upperSegmentY2, histogramColor);

                    if (FillHist_VP)
                        volHist.IsFilled = true;

                    if (histRightSide)
                    {
                        volHist.Time1 = xBar;
                        volHist.Time2 = xBar.AddMilliseconds(-dynLength);
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

                        intraDate = volHist.Time1;
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

                        intraDate = subtHist.Time1;

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

                        intraDate = deltaHist.Time1;
                    }
                }
                
                switch (VolumeMode_Input) 
                {
                    case VolumeMode_Data.Normal:
                    {
                        double value = vpNormal[priceKey];
                        double maxValue = vpNormal.Values.Max();

                        // Draw histograms and update 'intraDate' for VA/POC, if applicable
                        DrawRectangle_Normal(value, maxValue, intraBool);
                        break;
                    }
                    case VolumeMode_Data.Buy_Sell:             
                    {
                        double buyMax = 0;
                        try { buyMax = vpBuy.Values.Max(); } catch { }
                        double sellMax = 0;
                        try { sellMax = vpSell.Values.Max(); } catch { }

                        if (vpBuy.ContainsKey(priceKey) && vpSell.ContainsKey(priceKey))
                            DrawRectangle_BuySell(vpBuy[priceKey], vpSell[priceKey], buyMax, sellMax, isIntraday);
                        break;
                    }
                    default:
                    {
                        double value = vpDelta[priceKey];
                        double maxValue = vpDelta.Values.Max();
                        IEnumerable<double> negativeList = vpDelta.Values.Where(n => n < 0);

                        // Draw histograms and update 'intraDate' for VA/POC, if applicable
                        DrawRectangle_Delta(value, maxValue, negativeList, intraBool);
                        break;
                    }   
                }
            }

            // Drawings that don't require each segment-price as y-axis
            // It can/should be outside SegmentsLoop for better performance.
            
            // Results
            if (extraProfiles == ExtraProfiles.MiniVP && ShowMiniResults || 
                extraProfiles != ExtraProfiles.MiniVP && ShowResults)
            {
                switch (VolumeMode_Input) 
                {
                    case VolumeMode_Data.Normal:
                    {
                        double sum = Math.Round(vpNormal.Values.Sum());
                        string strValue = FormatResults ? FormatBigNumber(sum) : $"{sum}";

                        ChartText Center = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Normal_Result", $"\n{strValue}", x1_Start, y1_lowest, EnableGradient ? ColorGrandient_Min : HistColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;

                        if (HistogramSide_Input == HistSide_Data.Right)
                            Center.Time = xBar;

                        // Intraday Right Profile
                        if (isIntraday && extraProfiles == ExtraProfiles.No) {
                            DateTime dateOffset = TimeBasedOffset(xBar);
                            Center.Time = dateOffset;
                        }     
                        break;
                    }
                    case VolumeMode_Data.Buy_Sell:
                    {
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
                        break;
                    }
                    default: {
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
                        Left.HorizontalAlignment = HorizontalAlignment.Left; Left.FontSize = FontSizeResults;
                        Right.HorizontalAlignment = HorizontalAlignment.Right; Right.FontSize = FontSizeResults;
                        
                        ChartText Center;
                        string totalDeltaFmtd = totalDelta > 0 ? FormatBigNumber(totalDelta) : $"-{FormatBigNumber(Math.Abs(totalDelta))}";
                        string totalDeltaString = FormatResults ? totalDeltaFmtd : $"{totalDelta}";

                        Color centerColor = totalDelta > 0 ? BuyColor : SellColor;
                        Center = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_Result", $"\n{totalDeltaString}", x1_Start, y1_lowest, centerColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center; Center.FontSize = FontSizeResults - 1;

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
                            Draw_MinMaxDelta(extraProfiles, fixedKey, lowest, x1_Start, xBar, isIntraday, prefix);
                        
                        break;
                    }
                }
            }
            
            // For [Normal, Delta] only
            IDictionary<double, double> vpDict = VolumeMode_Input switch
            {
                VolumeMode_Data.Normal => extraProfiles switch
                {
                    ExtraProfiles.Monthly => MonthlyRank.Normal,
                    ExtraProfiles.Weekly => WeeklyRank.Normal,
                    ExtraProfiles.MiniVP => MiniRank.Normal,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].Normal,
                    _ => VP_VolumesRank
                },
                VolumeMode_Data.Delta => extraProfiles switch
                {
                    ExtraProfiles.Monthly => MonthlyRank.Delta,
                    ExtraProfiles.Weekly => WeeklyRank.Delta,
                    ExtraProfiles.MiniVP => MiniRank.Delta,
                    ExtraProfiles.Fixed => FixedRank[fixedKey].Delta,
                    _ => VP_DeltaRank
                },
                _ => new Dictionary<double, double>(),
            };
            
            if (vpDict.Count > 0) {
                // VA + POC
                Draw_VA_POC(vpDict, iStart, x1_Start, xBar, extraProfiles, isIntraday, intraDate, fixedKey);

                // HVN/LVN
                DrawVolumeNodes(vpDict, iStart, x1_Start, xBar, extraProfiles, isIntraday, intraDate, fixedKey);   
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

            void Draw_MinMaxDelta(ExtraProfiles extraProfiles, string fixedKey, double lowest, DateTime x1_Start, DateTime xBar, bool isIntraday, string prefix)
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
                    if (isIntraday && extraProfiles == ExtraProfiles.No)
                    {
                        DateTime dateOffset = TimeBasedOffset(xBar);
                        MinText.Time = dateOffset;
                        MaxText.Time = dateOffset;
                        SubText.Time = dateOffset;
                    }
                }
                else
                {
                    SubText = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Delta_SubResult", $"\n\nSub: {subDeltaString}", x1_Start, lowest, subColor);
                    SubText.HorizontalAlignment = HorizontalAlignment.Center;
                    SubText.FontSize = FontSizeResults - 1;

                    if (HistogramSide_Input == HistSide_Data.Right)
                        SubText.Time = xBar;
                    // Intraday Right Profile
                    if (isIntraday && extraProfiles == ExtraProfiles.No)
                    {
                        DateTime dateOffset = TimeBasedOffset(xBar);
                        SubText.Time = dateOffset;
                    }
                }
            }
        }

        private void VP_Bars(int index, ExtraProfiles extraVP = ExtraProfiles.No, string fixedKey = "")
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];

            // For real-time market - VP
            // Run conditional only in the last bar of repaint loop
            if (IsLastBar && Bars.OpenTimes[index] == Bars.LastBar.OpenTime)
                endTime = VOL_Bars.LastBar.OpenTime;

            int startIndex = extraVP switch
            {
                ExtraProfiles.Monthly => !IsLastBar ? lastBar_ExtraProfiles._Monthly : lastBar_ExtraProfiles._MonthlyStart,
                ExtraProfiles.Weekly => !IsLastBar ? lastBar_ExtraProfiles._Weekly : lastBar_ExtraProfiles._WeeklyStart,
                ExtraProfiles.MiniVP => !IsLastBar ? lastBar_ExtraProfiles._Mini : lastBar_ExtraProfiles._MiniStart,
                _ => lastBar_VPStart
            };
            if (extraVP == ExtraProfiles.Fixed) {
                ChartRectangle rect = _rectangles.Where(x => x.Name == fixedKey).FirstOrDefault();
                DateTime start = rect.Time1 < rect.Time2 ? rect.Time1 : rect.Time2;
                startIndex = Bars.OpenTimes.GetIndexByTime(start);
            }

            int TF_idx = extraVP == ExtraProfiles.Fixed ? MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]) : index;
            List<double> whichSegment = extraVP == ExtraProfiles.Fixed ? GetRangeSegments(TF_idx, fixedKey) : Segments_VP;

            // Keep shared VOL_Bars since 1min bars
            // are quite cheap in terms of RAM, even for 1 year.
            for (int k = startIndex; k < VOL_Bars.Count; ++k)
            {
                Bar volBar;
                volBar = VOL_Bars[k];

                if (volBar.OpenTime < startTime || volBar.OpenTime > endTime)
                {
                    if (volBar.OpenTime > endTime) {
                        _ = extraVP switch
                        {
                            ExtraProfiles.Monthly => lastBar_ExtraProfiles._Monthly = k,
                            ExtraProfiles.Weekly => lastBar_ExtraProfiles._Weekly = k,
                            ExtraProfiles.MiniVP => lastBar_ExtraProfiles._Mini = k,
                            _ => lastBar_VP = k
                        };
                        break;
                    }
                    else
                        continue;
                }

                /*
                The Volume Calculation(in Bars Volume Source) is exported, with adaptations, from the BEST VP I have see/used for MT4/MT5,
                    of Russian FXcoder's https://gitlab.com/fxcoder-mql/vp (VP 10.1), author of the famous (Volume Profile + Range v6.0)

                I tried to reproduce as close as possible from the original,
                I would say it was very good approximation in most core options, except the:
                    - "Triangular", witch I had to interpret it my way, and it turned out different, of course.
                    - "Parabolic", but the result turned out good
                */

                bool isBullish = volBar.Close >= volBar.Open;
                if (Distribution_Input == Distribution_Data.OHLC || Distribution_Input == Distribution_Data.OHLC_No_Avg)
                {
                    bool isAvg = Distribution_Input == Distribution_Data.OHLC;
                    // ========= Tick Simulation =========
                    // Bull/Buy/Up bar
                    if (volBar.Close >= volBar.Open)
                    {
                        // Average Tick Volume
                        double avgVol = isAvg ?
                        volBar.TickVolume / (volBar.Open + volBar.High + volBar.Low + volBar.Close / 4) :
                        volBar.TickVolume;

                        for (int i = 0; i < whichSegment.Count; i++)
                        {
                            double priceKey = whichSegment[i];
                            double currentSegment = priceKey;
                            if (currentSegment <= volBar.Open && currentSegment >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (currentSegment <= volBar.High && currentSegment >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (currentSegment <= volBar.High && currentSegment >= volBar.Close)
                                AddVolume(priceKey, avgVol, isBullish);
                        }
                    }
                    // Bear/Sell/Down bar
                    else
                    {
                        // Average Tick Volume
                        double avgVol = isAvg ? volBar.TickVolume / (volBar.Open + volBar.High + volBar.Low + volBar.Close / 4) : volBar.TickVolume;
                        for (int i = 0; i < whichSegment.Count; i++)
                        {
                            double priceKey = whichSegment[i];
                            double currentSegment = priceKey;
                            if (currentSegment >= volBar.Open && currentSegment <= volBar.High)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (currentSegment <= volBar.High && currentSegment >= volBar.Low)
                                AddVolume(priceKey, avgVol, isBullish);
                            if (currentSegment >= volBar.Low && currentSegment <= volBar.Close)
                                AddVolume(priceKey, avgVol, isBullish);
                        }
                    }
                }
                else if (Distribution_Input == Distribution_Data.High || Distribution_Input == Distribution_Data.Low || Distribution_Input == Distribution_Data.Close)
                {
                    var selected = Distribution_Input;
                    if (selected == Distribution_Data.High)
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < whichSegment.Count; i++)
                        {
                            double currentSegment = whichSegment[i];
                            if (currentSegment >= volBar.High && prevSegment <= volBar.High)
                                AddVolume(currentSegment, volBar.TickVolume, isBullish);
                            prevSegment = whichSegment[i];
                        }
                    }
                    else if (selected == Distribution_Data.Low)
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < whichSegment.Count; i++)
                        {
                            double currentSegment = whichSegment[i];
                            if (currentSegment >= volBar.Low && prevSegment <= volBar.Low)
                                AddVolume(currentSegment, volBar.TickVolume, isBullish);
                            prevSegment = whichSegment[i];
                        }
                    }
                    else
                    {
                        double prevSegment = 0;
                        for (int i = 0; i < whichSegment.Count; i++)
                        {
                            double currentSegment = whichSegment[i];
                            if (currentSegment >= volBar.Close && prevSegment <= volBar.Close)
                                AddVolume(currentSegment, volBar.TickVolume, isBullish);
                            prevSegment = whichSegment[i];
                        }
                    }
                }
                else if (Distribution_Input == Distribution_Data.Uniform_Distribution)
                {
                    double HL = Math.Abs(volBar.High - volBar.Low);
                    double uniVol = volBar.TickVolume / HL;
                    for (int i = 0; i < whichSegment.Count; i++)
                    {
                        double currentSegment = whichSegment[i];
                        if (currentSegment >= volBar.Low && currentSegment <= volBar.High)
                            AddVolume(currentSegment, uniVol, isBullish);
                    }
                }
                else if (Distribution_Input == Distribution_Data.Uniform_Presence)
                {
                    double uniP_Vol = 1;
                    for (int i = 0; i < whichSegment.Count; i++)
                    {
                        double currentSegment = whichSegment[i];
                        if (currentSegment >= volBar.Low && currentSegment <= volBar.High)
                            AddVolume(currentSegment, uniP_Vol, isBullish);
                    }
                }
                else if (Distribution_Input == Distribution_Data.Parabolic_Distribution)
                {
                    double HL2 = Math.Abs(volBar.High - volBar.Low) / 2;
                    double hl2SQRT = Math.Sqrt(HL2);
                    double final = hl2SQRT / HL2;

                    double parabolicVol = volBar.TickVolume / final;

                    for (int i = 0; i < whichSegment.Count; i++)
                    {
                        double currentSegment = whichSegment[i];
                        if (currentSegment >= volBar.Low && currentSegment <= volBar.High)
                            AddVolume(currentSegment, parabolicVol, isBullish);
                    }
                }
                else if (Distribution_Input == Distribution_Data.Triangular_Distribution)
                {
                    double HL = Math.Abs(volBar.High - volBar.Low);
                    double HL2 = HL / 2;
                    double HL_minus = HL - HL2;
                    // =====================================
                    double oneStep = HL2 * HL_minus / 2;
                    double secondStep = HL_minus * HL / 2;
                    double final = oneStep + secondStep;

                    double triangularVol = volBar.TickVolume / final;

                    for (int i = 0; i < whichSegment.Count; i++)
                    {
                        double currentSegment = whichSegment[i];
                        if (currentSegment >= volBar.Low && currentSegment <= volBar.High)
                            AddVolume(currentSegment, triangularVol, isBullish);
                    }
                }
            }

            void AddVolume(double priceKey, double vol, bool isBullish)
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
                    UpdateExtraProfiles(extraRank, priceKey, vol, isBullish);
                    return;
                }

                if (!VP_VolumesRank.ContainsKey(priceKey))
                    VP_VolumesRank.Add(priceKey, vol);
                else
                    VP_VolumesRank[priceKey] += vol;

                bool condition = VolumeMode_Input != VolumeMode_Data.Normal;
                if (condition)
                    Add_BuySell(priceKey, vol, isBullish);
            }
            void Add_BuySell(double priceKey, double vol, bool isBullish)
            {
                if (isBullish)
                {
                    if (!VP_VolumesRank_Up.ContainsKey(priceKey))
                        VP_VolumesRank_Up.Add(priceKey, vol);
                    else
                        VP_VolumesRank_Up[priceKey] += vol;
                }
                else
                {
                    if (!VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_VolumesRank_Down.Add(priceKey, vol);
                    else
                        VP_VolumesRank_Down[priceKey] += vol;
                }

                double prevDelta = VP_DeltaRank.Values.Sum();

                if (!VP_DeltaRank.ContainsKey(priceKey))
                {
                    if (VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_DeltaRank.Add(priceKey, (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]));
                    else if (VP_VolumesRank_Up.ContainsKey(priceKey) && !VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_DeltaRank.Add(priceKey, (VP_VolumesRank_Up[priceKey]));
                    else if (!VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_DeltaRank.Add(priceKey, (-VP_VolumesRank_Down[priceKey]));
                    else
                        VP_DeltaRank.Add(priceKey, 0);
                }
                else
                {
                    if (VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_DeltaRank[priceKey] += (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]);
                    else if (VP_VolumesRank_Up.ContainsKey(priceKey) && !VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_DeltaRank[priceKey] += (VP_VolumesRank_Up[priceKey]);
                    else if (!VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_DeltaRank[priceKey] += (-VP_VolumesRank_Down[priceKey]);

                }

                double currentDelta = VP_DeltaRank.Values.Sum();
                if (prevDelta > currentDelta)
                    VP_MinMaxDelta[0] = prevDelta; // Min
                if (prevDelta < currentDelta)
                    VP_MinMaxDelta[1] = prevDelta; // Max before final delta

                if (VolumeMode_Input != VolumeMode_Data.Buy_Sell)
                    return;

                // Subtract Profile - Plain Delta
                if (!VP_VolumesRank_Subt.ContainsKey(priceKey))
                {
                    if (VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_VolumesRank_Subt.Add(priceKey, (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]));
                    else if (VP_VolumesRank_Up.ContainsKey(priceKey) && !VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_VolumesRank_Subt.Add(priceKey, (VP_VolumesRank_Up[priceKey]));
                    else if (!VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_VolumesRank_Subt.Add(priceKey, (-VP_VolumesRank_Down[priceKey]));
                    else
                        VP_VolumesRank_Subt.Add(priceKey, 0);
                }
                else
                {
                    if (VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_VolumesRank_Subt[priceKey] = (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]);
                    else if (VP_VolumesRank_Up.ContainsKey(priceKey) && !VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_VolumesRank_Subt[priceKey] = (VP_VolumesRank_Up[priceKey]);
                    else if (!VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                        VP_VolumesRank_Subt[priceKey] = (-VP_VolumesRank_Down[priceKey]);
                }
            }

            void UpdateExtraProfiles(VolumeRankType volRank, double priceKey, double vol, bool isBullish) {
                if (!volRank.Normal.ContainsKey(priceKey))
                    volRank.Normal.Add(priceKey, vol);
                else
                    volRank.Normal[priceKey] += vol;

                bool condition = VolumeMode_Input != VolumeMode_Data.Normal;
                if (condition)
                    Add_BuySell_Extra(volRank, priceKey, vol, isBullish);
            }

            void Add_BuySell_Extra(VolumeRankType volRank, double priceKey, double vol, bool isBullish)
            {
                if (isBullish)
                {
                    if (!volRank.Up.ContainsKey(priceKey))
                        volRank.Up.Add(priceKey, vol);
                    else
                        volRank.Up[priceKey] += vol;
                }
                else
                {
                    if (!volRank.Down.ContainsKey(priceKey))
                        volRank.Down.Add(priceKey, vol);
                    else
                        volRank.Down[priceKey] += vol;
                }

                double prevDelta = volRank.Delta.Values.Sum();

                if (!volRank.Delta.ContainsKey(priceKey))
                {
                    if (volRank.Up.ContainsKey(priceKey) && volRank.Down.ContainsKey(priceKey))
                        volRank.Delta.Add(priceKey, (volRank.Up[priceKey] - volRank.Down[priceKey]));
                    else if (volRank.Up.ContainsKey(priceKey) && !volRank.Down.ContainsKey(priceKey))
                        volRank.Delta.Add(priceKey, (volRank.Up[priceKey]));
                    else if (!volRank.Up.ContainsKey(priceKey) && volRank.Down.ContainsKey(priceKey))
                        volRank.Delta.Add(priceKey, (-volRank.Down[priceKey]));
                    else
                        volRank.Delta.Add(priceKey, 0);
                }
                else
                {
                    if (volRank.Up.ContainsKey(priceKey) && volRank.Down.ContainsKey(priceKey))
                        volRank.Delta[priceKey] += (volRank.Up[priceKey] - volRank.Down[priceKey]);
                    else if (volRank.Up.ContainsKey(priceKey) && !volRank.Down.ContainsKey(priceKey))
                        volRank.Delta[priceKey] += (volRank.Up[priceKey]);
                    else if (!volRank.Up.ContainsKey(priceKey) && volRank.Down.ContainsKey(priceKey))
                        volRank.Delta[priceKey] += (-volRank.Down[priceKey]);

                }

                double currentDelta = volRank.Delta.Values.Sum();
                if (prevDelta > currentDelta)
                    volRank.MinMaxDelta[0] = prevDelta; // Min
                if (prevDelta < currentDelta)
                    volRank.MinMaxDelta[1] = prevDelta; // Max before final delta
            }
        }

        // *********** LIVE PROFILE UPDATE ***********
        private void LiveVP_Update(int startIndex, int index, bool onlyMini = false) {
            double price = Bars.ClosePrices[index];

            bool updateStrategy = UpdateProfile_Input == UpdateProfile_Data.ThroughSegments_Balanced ?
                                Math.Abs(price - prevUpdatePrice) >= rowHeight :
                                UpdateProfile_Input != UpdateProfile_Data.Through_2_Segments_Best ||
                                Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight);

            if (updateStrategy || isUpdateVP || configHasChanged)
            {
                if (!onlyMini)
                {
                    if (EnableMonthlyProfile && VPInterval_Input != VPInterval_Data.Monthly)
                    {
                        int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

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

                    if (EnableWeeklyProfile && VPInterval_Input != VPInterval_Data.Weekly)
                    {
                        int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

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

                    if (EnableMiniProfiles)
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

                    if (index != startIndex)
                    {
                        for (int i = startIndex; i <= index; i++)
                        {
                            if (i == startIndex) {
                                VP_VolumesRank.Clear();
                                VP_VolumesRank_Up.Clear();
                                VP_VolumesRank_Down.Clear();
                                VP_VolumesRank_Subt.Clear();
                                VP_DeltaRank.Clear();
                            }
                            if (i < index)
                                VolumeProfile(startIndex, i, ExtraProfiles.No, true); // Update only
                            else
                                VolumeProfile(startIndex, i, ExtraProfiles.No, false); // Update and Draw
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

            configHasChanged = false;
            isUpdateVP = false;
            if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                prevUpdatePrice = price;
        }

        private void LiveVP_Concurrent(int index, int startIndex)
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
                if (Bars.Count > Bars_ChartArray.Length)
                {
                    lock (_lockBars)
                        Bars_ChartArray = Bars.OpenTimes.ToArray();
                }

                lock (_lockSource)
                    Bars_SourceList = new List<Bar>(VOL_Bars);

                liveVP_UpdateIt = true;
            }
            cts ??= new CancellationTokenSource();

            CreateMonthlyVP(index, isConcurrent: true);
            CreateWeeklyVP(index, isConcurrent: true);
            CreateMiniVPs(index, isConcurrent: true);

            if (EnableVP)
            {
                liveVP_Task ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.No, cts.Token));
                liveVP_StartIndexes.VP = startIndex;
                if (index != startIndex) {
                    lock (_lock)
                        VolumeProfile(startIndex, index, ExtraProfiles.No, false, true);
                }
            }
        }

        private void LiveVP_Worker(ExtraProfiles extraID, CancellationToken token)
        {
            /*
            It's quite simple, but gave headaches mostly due to GetByInvoke() unexpected behavior and debugging it.
             - GetByInvoke() will slowdown loops due to accumulative Bars[index] => "0.xx ms" operations
            The major reason why Copy of Time/Bars are used.
            */

            IDictionary<double, double> Worker_VolumesRank = new Dictionary<double, double>();
            IDictionary<double, double> Worker_VolumesRank_Up = new Dictionary<double, double>();
            IDictionary<double, double> Worker_VolumesRank_Down = new Dictionary<double, double>();
            IDictionary<double, double> Worker_VolumesRank_Subt = new Dictionary<double, double>();
            IDictionary<double, double> Worker_DeltaRank = new Dictionary<double, double>();
            double[] Worker_MinMaxDelta = { 0, 0 };

            DateTime lastTime = new();
            IEnumerable<DateTime> TimesCopy = Array.Empty<DateTime>();
            IEnumerable<Bar> BarsCopy = new List<Bar>();

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
                    int startIndex = extraID == ExtraProfiles.No ? liveVP_StartIndexes.VP :
                                     extraID == ExtraProfiles.MiniVP ? liveVP_StartIndexes.Mini :
                                     extraID == ExtraProfiles.Weekly ? liveVP_StartIndexes.Weekly : liveVP_StartIndexes.Monthly;
                    DateTime lastBarTime = GetByInvoke(() => Bars.LastBar.OpenTime);

                    // Replace only when needed
                    if (lastTime != lastBarTime) {
                        lock (_lockBars)
                            TimesCopy = Bars_ChartArray.Skip(startIndex);
                        lastTime = lastBarTime;
                    }
                    int endIndex = TimesCopy.Count();

                    // Source Bars
                    int startSourceIndex = extraID == ExtraProfiles.No ? lastBar_VPStart:
                                     extraID == ExtraProfiles.MiniVP ? lastBar_ExtraProfiles._MiniStart :
                                     extraID == ExtraProfiles.Weekly ? lastBar_ExtraProfiles._WeeklyStart : lastBar_ExtraProfiles._MonthlyStart;

                    lock (_lockSource)
                        BarsCopy = Bars_SourceList.Skip(startSourceIndex);

                    for (int i = 0; i < endIndex; i++)
                    {
                        Worker_VP_Bars(i, extraID, i == (endIndex - 1));
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

                        configHasChanged = false;
                        isUpdateVP = false;

                        if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                            prevUpdatePrice = BarsCopy.Last().Close;
                    }
                }
                catch (Exception e) { Print($"CRASH at LiveVP_Worker => {extraID}: {e}"); }

                liveVP_UpdateIt = false;
            }

            void Worker_VP_Bars(int index, ExtraProfiles extraVP = ExtraProfiles.No, bool isLastBarLoop = false)
            {
                DateTime startTime = TimesCopy.ElementAt(index);
                DateTime endTime = !isLastBarLoop ? TimesCopy.ElementAt(index + 1) : BarsCopy.Last().OpenTime;

                for (int k = 0; k < BarsCopy.Count(); ++k)
                {
                    Bar volBar = BarsCopy.ElementAt(k);

                    if (volBar.OpenTime < startTime || volBar.OpenTime > endTime)
                    {
                        if (volBar.OpenTime > endTime)
                            break;
                        else
                            continue;
                    }

                    /*
                    The Volume Calculation(in Bars Volume Source) is exported, with adaptations, from the BEST VP I have see/used for MT4/MT5,
                        of Russian FXcoder's https://gitlab.com/fxcoder-mql/vp (VP 10.1), author of the famous (Volume Profile + Range v6.0)

                    I tried to reproduce as close as possible from the original,
                    I would say it was very good approximation in most core options, except the:
                        - "Triangular", witch I had to interpret it my way, and it turned out different, of course.
                        - "Parabolic", but the result turned out good
                    */

                    bool isBullish = volBar.Close >= volBar.Open;
                    if (Distribution_Input == Distribution_Data.OHLC || Distribution_Input == Distribution_Data.OHLC_No_Avg)
                    {
                        bool isAvg = Distribution_Input == Distribution_Data.OHLC;
                        // ========= Tick Simulation ================
                        // Bull/Buy/Up bar
                        if (volBar.Close >= volBar.Open)
                        {
                            // Average Tick Volume
                            double avgVol = isAvg ?
                            volBar.TickVolume / (volBar.Open + volBar.High + volBar.Low + volBar.Close / 4) :
                            volBar.TickVolume;

                            for (int i = 0; i < Segments_VP.Count; i++)
                            {
                                double priceKey = Segments_VP[i];
                                if (Segments_VP[i] <= volBar.Open && Segments_VP[i] >= volBar.Low)
                                    AddVolume(priceKey, avgVol, isBullish);
                                if (Segments_VP[i] <= volBar.High && Segments_VP[i] >= volBar.Low)
                                    AddVolume(priceKey, avgVol, isBullish);
                                if (Segments_VP[i] <= volBar.High && Segments_VP[i] >= volBar.Close)
                                    AddVolume(priceKey, avgVol, isBullish);
                            }
                        }
                        // Bear/Sell/Down bar
                        else
                        {
                            // Average Tick Volume
                            double avgVol = isAvg ? volBar.TickVolume / (volBar.Open + volBar.High + volBar.Low + volBar.Close / 4) : volBar.TickVolume;
                            for (int i = 0; i < Segments_VP.Count; i++)
                            {
                                double priceKey = Segments_VP[i];
                                if (Segments_VP[i] >= volBar.Open && Segments_VP[i] <= volBar.High)
                                    AddVolume(priceKey, avgVol, isBullish);
                                if (Segments_VP[i] <= volBar.High && Segments_VP[i] >= volBar.Low)
                                    AddVolume(priceKey, avgVol, isBullish);
                                if (Segments_VP[i] >= volBar.Low && Segments_VP[i] <= volBar.Close)
                                    AddVolume(priceKey, avgVol, isBullish);
                            }
                        }
                    }
                    else if (Distribution_Input == Distribution_Data.High || Distribution_Input == Distribution_Data.Low || Distribution_Input == Distribution_Data.Close)
                    {
                        var selected = Distribution_Input;
                        if (selected == Distribution_Data.High)
                        {
                            double prevSegment = 0;
                            for (int i = 0; i < Segments_VP.Count; i++)
                            {
                                if (Segments_VP[i] >= volBar.High && prevSegment <= volBar.High)
                                    AddVolume(Segments_VP[i], volBar.TickVolume, isBullish);
                                prevSegment = Segments_VP[i];
                            }
                        }
                        else if (selected == Distribution_Data.Low)
                        {
                            double prevSegment = 0;
                            for (int i = 0; i < Segments_VP.Count; i++)
                            {
                                if (Segments_VP[i] >= volBar.Low && prevSegment <= volBar.Low)
                                    AddVolume(Segments_VP[i], volBar.TickVolume, isBullish);
                                prevSegment = Segments_VP[i];
                            }
                        }
                        else
                        {
                            double prevSegment = 0;
                            for (int i = 0; i < Segments_VP.Count; i++)
                            {
                                if (Segments_VP[i] >= volBar.Close && prevSegment <= volBar.Close)
                                    AddVolume(Segments_VP[i], volBar.TickVolume, isBullish);
                                prevSegment = Segments_VP[i];
                            }
                        }
                    }
                    else if (Distribution_Input == Distribution_Data.Uniform_Distribution)
                    {
                        double HL = Math.Abs(volBar.High - volBar.Low);
                        double uniVol = volBar.TickVolume / HL;
                        for (int i = 0; i < Segments_VP.Count; i++)
                        {
                            if (Segments_VP[i] >= volBar.Low && Segments_VP[i] <= volBar.High)
                                AddVolume(Segments_VP[i], uniVol, isBullish);
                        }
                    }
                    else if (Distribution_Input == Distribution_Data.Uniform_Presence)
                    {
                        double uniP_Vol = 1;
                        for (int i = 0; i < Segments_VP.Count; i++)
                        {
                            if (Segments_VP[i] >= volBar.Low && Segments_VP[i] <= volBar.High)
                                AddVolume(Segments_VP[i], uniP_Vol, isBullish);
                        }
                    }
                    else if (Distribution_Input == Distribution_Data.Parabolic_Distribution)
                    {
                        double HL2 = Math.Abs(volBar.High - volBar.Low) / 2;
                        double hl2SQRT = Math.Sqrt(HL2);
                        double final = hl2SQRT / HL2;

                        double parabolicVol = volBar.TickVolume / final;

                        for (int i = 0; i < Segments_VP.Count; i++)
                        {
                            if (Segments_VP[i] >= volBar.Low && Segments_VP[i] <= volBar.High)
                                AddVolume(Segments_VP[i], parabolicVol, isBullish);
                        }
                    }
                    else if (Distribution_Input == Distribution_Data.Triangular_Distribution)
                    {
                        double HL = Math.Abs(volBar.High - volBar.Low);
                        double HL2 = HL / 2;
                        double HL_minus = HL - HL2;
                        // =====================================
                        double oneStep = HL2 * HL_minus / 2;
                        double secondStep = HL_minus * HL / 2;
                        double final = oneStep + secondStep;

                        double triangularVol = volBar.TickVolume / final;

                        for (int i = 0; i < Segments_VP.Count; i++)
                        {
                            if (Segments_VP[i] >= volBar.Low && Segments_VP[i] <= volBar.High)
                                AddVolume(Segments_VP[i], triangularVol, isBullish);
                        }
                    }
                }

                void AddVolume(double priceKey, double vol, bool isBullish)
                {
                    if (!Worker_VolumesRank.ContainsKey(priceKey))
                        Worker_VolumesRank.Add(priceKey, vol);
                    else
                        Worker_VolumesRank[priceKey] += vol;

                    bool condition = VolumeMode_Input != VolumeMode_Data.Normal;
                    if (condition)
                        Add_BuySell(priceKey, vol, isBullish);
                }
                void Add_BuySell(double priceKey, double vol, bool isBullish)
                {
                    if (isBullish)
                    {
                        if (!Worker_VolumesRank_Up.ContainsKey(priceKey))
                            Worker_VolumesRank_Up.Add(priceKey, vol);
                        else
                            Worker_VolumesRank_Up[priceKey] += vol;
                    }
                    else
                    {
                        if (!Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_VolumesRank_Down.Add(priceKey, vol);
                        else
                            Worker_VolumesRank_Down[priceKey] += vol;
                    }

                    double prevDelta = Worker_DeltaRank.Values.Sum();

                    if (!Worker_DeltaRank.ContainsKey(priceKey))
                    {
                        if (Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_DeltaRank.Add(priceKey, (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]));
                        else if (Worker_VolumesRank_Up.ContainsKey(priceKey) && !Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_DeltaRank.Add(priceKey, (Worker_VolumesRank_Up[priceKey]));
                        else if (!Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_DeltaRank.Add(priceKey, (-Worker_VolumesRank_Down[priceKey]));
                        else
                            Worker_DeltaRank.Add(priceKey, 0);
                    }
                    else
                    {
                        if (Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_DeltaRank[priceKey] += (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]);
                        else if (Worker_VolumesRank_Up.ContainsKey(priceKey) && !Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_DeltaRank[priceKey] += (Worker_VolumesRank_Up[priceKey]);
                        else if (!Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_DeltaRank[priceKey] += (-Worker_VolumesRank_Down[priceKey]);

                    }

                    double currentDelta = Worker_DeltaRank.Values.Sum();
                    if (prevDelta > currentDelta)
                        Worker_MinMaxDelta[0] = prevDelta; // Min
                    if (prevDelta < currentDelta)
                        Worker_MinMaxDelta[1] = prevDelta; // Max before final delta

                    if (extraVP != ExtraProfiles.No && VolumeMode_Input != VolumeMode_Data.Buy_Sell)
                        return;
                    // Subtract Profile - Plain Delta
                    if (!Worker_VolumesRank_Subt.ContainsKey(priceKey))
                    {
                        if (Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_VolumesRank_Subt.Add(priceKey, (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]));
                        else if (Worker_VolumesRank_Up.ContainsKey(priceKey) && !Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_VolumesRank_Subt.Add(priceKey, (Worker_VolumesRank_Up[priceKey]));
                        else if (!Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_VolumesRank_Subt.Add(priceKey, (-Worker_VolumesRank_Down[priceKey]));
                        else
                            Worker_VolumesRank_Subt.Add(priceKey, 0);
                    }
                    else
                    {
                        if (Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_VolumesRank_Subt[priceKey] = (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]);
                        else if (Worker_VolumesRank_Up.ContainsKey(priceKey) && !Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_VolumesRank_Subt[priceKey] = (Worker_VolumesRank_Up[priceKey]);
                        else if (!Worker_VolumesRank_Up.ContainsKey(priceKey) && Worker_VolumesRank_Down.ContainsKey(priceKey))
                            Worker_VolumesRank_Subt[priceKey] = (-Worker_VolumesRank_Down[priceKey]);
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
            double topY = Math.Max(rect.Y1, rect.Y2);

            ResetFixedRange(rect.Name, end);

            for (int i = startIdx; i <= endIdx; i++)
                VolumeProfile(startIdx, i, ExtraProfiles.Fixed, fixedKey: rect.Name, fixedLowest: bottomY, fixedHighest: topY);
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

            List<double> whichSegment;
            if (SegmentsFixedRange_Input == SegmentsFixedRange_Data.Monthly_Aligned) {
                int endIdx = Bars.OpenTimes.GetIndexByTime(end);
                int TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[endIdx]); //Segments are always monthly
                whichSegment = segmentsDict[TF_idx];
            }
            else {
                if (!segmentsFromProfile.ContainsKey(fixedKey))
                    segmentsFromProfile.Add(fixedKey, new List<double>());
                whichSegment = segmentsFromProfile[fixedKey];   
            }
            
            for (int i = 0; i < whichSegment.Count; i++)
            {
                // Histograms
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Normal");

                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Sell");
                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Buy");

                Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Delta");

                // HVN + LVN
                Chart.RemoveObject($"{fixedKey}_LVN_Low_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_LVN_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_LVN_High_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_LVN_Band_{i}_Fixed");

                Chart.RemoveObject($"{fixedKey}_HVN_Low_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_HVN_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_HVN_High_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_HVN_Band_{i}_Fixed");
            }

            string[] objsNames = new string[14] {
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
        private void Draw_VA_POC(IDictionary<double, double> vpDict, int iStart, DateTime x1_Start, DateTime xBar, ExtraProfiles extraVP = ExtraProfiles.No, bool isIntraday = false, DateTime intraX1 = default, string fixedKey = "")
        {
            string prefix = extraVP == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";

            if (ShowVA) {
                double[] VAL_VAH_POC = VA_Calculation(vpDict);

                if (!VAL_VAH_POC.Any())
                    return;

                ChartTrendLine poc = Chart.DrawTrendLine($"{prefix}_POC_{extraVP}", x1_Start, VAL_VAH_POC[2] - rowHeight, xBar, VAL_VAH_POC[2] - rowHeight, ColorPOC);
                ChartTrendLine vah = Chart.DrawTrendLine($"{prefix}_VAH_{extraVP}", x1_Start, VAL_VAH_POC[1] + rowHeight, xBar, VAL_VAH_POC[1] + rowHeight, ColorVAH);
                ChartTrendLine val = Chart.DrawTrendLine($"{prefix}_VAL_{extraVP}", x1_Start, VAL_VAH_POC[0], xBar, VAL_VAH_POC[0], ColorVAL);

                poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                vah.LineStyle = LineStyleVA; vah.Thickness = ThicknessVA; vah.Comment = "VAH";
                val.LineStyle = LineStyleVA; val.Thickness = ThicknessVA; val.Comment = "VAL";

                ChartRectangle rectVA;
                rectVA = Chart.DrawRectangle($"{prefix}_RectVA_{extraVP}", x1_Start, VAL_VAH_POC[0], xBar, VAL_VAH_POC[1] + rowHeight, VAColor);
                rectVA.IsFilled = true;

                DateTime extDate = extraVP == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
                if (ExtendVA) {
                    vah.Time2 = extDate;
                    val.Time2 = extDate;
                    rectVA.Time2 = extDate;
                }
                if (ExtendPOC)
                    poc.Time2 = extDate;

                if (isIntraday && extraVP != ExtraProfiles.MiniVP) {
                    poc.Time1 = intraX1;
                    vah.Time1 = intraX1;
                    val.Time1 = intraX1;
                    rectVA.Time1 = intraX1;
                }
            }
            else if (!ShowVA && KeepPOC)
            {
                double positiveMax = Math.Abs(vpDict.Values.Max());
                double negativeMax = 0;
                try { negativeMax = Math.Abs(vpDict.Values.Where(n => n < 0).Min()); } catch { }

                double largestVOL = positiveMax > negativeMax ? positiveMax : negativeMax;

                double priceLVOL = 0;
                foreach (var kv in vpDict)
                {
                    if (Math.Abs(kv.Value) == largestVOL) { priceLVOL = kv.Key; break; }
                }
                ChartTrendLine poc = Chart.DrawTrendLine($"{prefix}_POC_{extraVP}", x1_Start, priceLVOL - rowHeight, xBar, priceLVOL - rowHeight, ColorPOC);
                poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";

                if (ExtendPOC)
                    poc.Time2 = extraVP == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();

                if (isIntraday && extraVP != ExtraProfiles.MiniVP)
                    poc.Time1 = intraX1;
            }

            DateTime extendDate() {
                string tfName = extraVP == ExtraProfiles.No ?
                (VPInterval_Input == VPInterval_Data.Daily ? "D1" :
                    VPInterval_Input == VPInterval_Data.Weekly ? "W1" : "Month1" ) :
                extraVP == ExtraProfiles.MiniVP ? MiniVPs_Timeframe.ShortName.ToString() :
                extraVP == ExtraProfiles.Weekly ?  "W1" :  "Month1";

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

        private double[] VA_Calculation(IDictionary<double, double> vpDict)
        {
            /*  https://onlinelibrary.wiley.com/doi/pdf/10.1002/9781118659724.app1
                https://www.mypivots.com/dictionary/definition/40/calculating-market-profile-value-area
                Same of TPO Profile(https://ctrader.com/algos/indicators/show/3074)  */

            if (vpDict.Values.Count < 4)
                return Array.Empty<double>();

            double positiveMax = Math.Abs(vpDict.Values.Max());
            double negativeMax = 0;
            try { negativeMax = Math.Abs(vpDict.Values.Where(n => n < 0).Min()); } catch { }

            double largestVOL = positiveMax > negativeMax ? positiveMax : negativeMax;

            double totalvol = Math.Abs(vpDict.Values.Sum());
            double _70percent = Math.Round((PercentVA * totalvol) / 100);

            double priceLVOL = 0;
            foreach (var kv in vpDict)
            {
                if (Math.Abs(kv.Value) == largestVOL) { priceLVOL = kv.Key; break; }
            }
            double priceVAH = 0;
            double priceVAL = 0;

            double sumVA = largestVOL;

            List<double> upKeys = new();
            List<double> downKeys = new();
            for (int i = 0; i < Segments_VP.Count; i++)
            {
                double priceKey = Segments_VP[i];

                if (vpDict.ContainsKey(priceKey))
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

            for (int i = 0; i < vpDict.Keys.Count; i++)
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
                        sumDown = Math.Abs(vpDict[key]);
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
                        double upVOL = Math.Abs(vpDict[key]);
                        double up2VOL = Math.Abs(vpDict[prevUPkey]);

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
                        sumDown = Math.Abs(vpDict[key]);
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
                        double downVOL = Math.Abs(vpDict[key]);
                        double down2VOL = Math.Abs(vpDict[prevDownkey]);

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

        
        // *********** HVN + LVN ***********
        private void DrawVolumeNodes(IDictionary<double, double> profileDict, int iStart, DateTime x1_Start, DateTime xBar, ExtraProfiles extraTPO = ExtraProfiles.No, bool isIntraday = false, DateTime intraX1 = default, string fixedKey = "") 
        { 
            if (!EnableNodeDetection)
                return;
                
            string prefix = extraTPO == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";
            /*
                Alternatives for ordering:
                - "SortedDictionary<>()" 
                    - for [TPO_Rank_Histogram, TPORankType.TPO_Histogram] dicts
                - tpoDict.OrderBy(x => x.key).ToDictionary(kv => kv.Key, kv => kv.Value);
                    - Then .ToArray()
                - https://dotnettips.wordpress.com/2018/01/30/performance-sorteddictionary-vs-dictionary/
            */
            
            // This approach seems more efficient.
            double[] profilePrices = profileDict.Keys.ToArray();
            Array.Sort(profilePrices);
            double[] profileValues = profilePrices.Select(key => profileDict[key]).ToArray();
            /*
            // Alternative, no LINQ
            double[] profileValues = new double[profilePrices.Length];
            for (int i = 0; i < profilePrices.Length; i++)
                profileValues[i] = tpoDict[profilePrices[i]];
            */
            
            // Calculate Kernels/Coefficientes only once.
            nodesKernel ??= ProfileSmooth_Input == ProfileSmooth_Data.Gaussian ?
                            NodesAnalizer.FixedKernel() : NodesAnalizer.FixedCoefficients();
            
            // Smooth values
            double[] profileSmoothed = ProfileSmooth_Input == ProfileSmooth_Data.Gaussian ?
                                       NodesAnalizer.GaussianSmooth(profileValues, nodesKernel) : NodesAnalizer.SavitzkyGolay(profileValues, nodesKernel);
            
            // Get indexes of LVNs/HVNs
            var (hvnsRaw, lvnsRaw) = ProfileNode_Input switch {
                ProfileNode_Data.LocalMinMax => NodesAnalizer.FindLocalMinMax(profileSmoothed),
                ProfileNode_Data.Topology => NodesAnalizer.ProfileTopology(profileSmoothed),
                _ => NodesAnalizer.PercentileNodes(profileSmoothed, pctileHVN_Value, pctileLVN_Value)
            };
            
            // Filter it
            if (onlyStrongNodes)
            {
                double globalPoc = profileSmoothed.Max();

                double hvnPct = Math.Round(strongHVN_Pct / 100.0, 3);
                double lvnPct = Math.Round(strongLVN_Pct / 100.0, 3);

                var strongHvns = new List<int>();
                var strongLvns = new List<int>();

                foreach (int idx in hvnsRaw)
                {
                    if (profileSmoothed[idx] >= hvnPct * globalPoc)
                        strongHvns.Add(idx);
                }

                foreach (int idx in lvnsRaw)
                {
                    if (profileSmoothed[idx] <= lvnPct * globalPoc)
                        strongLvns.Add(idx);
                }

                hvnsRaw = strongHvns;
                lvnsRaw = strongLvns;
            }
                
            bool isRaw = ShowNode_Input == ShowNode_Data.HVN_Raw || ShowNode_Input == ShowNode_Data.LVN_Raw;
            bool isBands = ShowNode_Input == ShowNode_Data.HVN_With_Bands || ShowNode_Input == ShowNode_Data.LVN_With_Bands;
            
            if (ProfileNode_Input == ProfileNode_Data.Percentile) 
            {
                ClearOldNodes();                                               
                
                if (isBands)
                {
                    Color _nodeColor = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? ColorHVN : ColorLVN;

                    var hvnsGroups = NodesAnalizer.GroupConsecutiveIndexes(hvnsRaw);
                    var lvnsGroups = NodesAnalizer.GroupConsecutiveIndexes(lvnsRaw);
                    List<List<int>> nodeGroups = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? hvnsGroups : lvnsGroups;
                    
                    string nodeName = ShowNode_Input == ShowNode_Data.HVN_Raw ? "HVN" : "LVN";   
                    foreach (var group in nodeGroups) 
                    {
                        int idxLow = group[0];
                        int idxCenter = group[group.Count / 2];
                        int idxHigh = group[group.Count - 1];
                        
                        double lowPrice = profilePrices[idxLow];
                        double centerPrice = profilePrices[idxCenter];
                        double highPrice = profilePrices[idxHigh];
                        
                        ChartTrendLine low = Chart.DrawTrendLine($"{prefix}_{nodeName}_Low_{idxLow}_{extraTPO}", x1_Start, lowPrice, xBar, lowPrice, ColorBand_Lower);
                        ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{nodeName}_{idxCenter}_{extraTPO}", x1_Start, centerPrice, xBar, centerPrice, _nodeColor);
                        ChartTrendLine high = Chart.DrawTrendLine($"{prefix}_{nodeName}_High_{idxHigh}_{extraTPO}", x1_Start, highPrice, xBar, highPrice, ColorBand_Upper);   
                        ChartRectangle rectBand = Chart.DrawRectangle($"{prefix}_{nodeName}_Band_{idxCenter}_{extraTPO}", x1_Start,  lowPrice, xBar, highPrice, ColorBand);
                        
                        FinalizeBands(low, center, high, rectBand);
                    }
                } 
                else 
                    DrawRawNodes();
                
                return;
            }

            // Draw raw-nodes, if applicable
            if (isRaw)  {
                ClearOldNodes();
                DrawRawNodes();
                return;
            }
                        
            // Split profile by LVNs
            var areasBetween = new List<(int Start, int End)>();
            int start = 0;
            foreach (int lvn in lvnsRaw)
            {
                areasBetween.Add((start, lvn));
                start = lvn;
            }
            areasBetween.Add((start, profileSmoothed.Length - 1));

            // Extract mini-bells
            var bells = new List<(int Start, int End, int Poc)>();
            foreach (var (Start, End) in areasBetween)
            {
                int startIndex = Start;
                int endIndex = End;

                if (endIndex <= startIndex)
                    continue;

                int pocIdx = startIndex;
                double maxVol = profileSmoothed[startIndex];

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    if (profileSmoothed[i] > maxVol)
                    {
                        maxVol = profileSmoothed[i];
                        pocIdx = i;
                    }
                }

                bells.Add((startIndex, endIndex, pocIdx));
            }
            
            // Extract HVN/LVN/POC + Levels
            // [(low, center, high), ...]
            var hvnLevels = new List<(double Low, double Center, double High)>();
            var hvnIndexes = new List<(int Low, int Center, int High)>();

            var lvnLevels = new List<(double Low, double Center, double High)>();
            var lvnIndexes = new List<(int Low, int Center, int High)>();

            double hvnBandPct = Math.Round(bandHVN_Pct / 100.0, 3);
            double lvnBandPct = Math.Round(bandLVN_Pct / 100.0, 3);

            foreach (var (startIdx, endIdx, pocIdx) in bells)
            {
                // HVNs/POCs + levels
                var (hvnLow, hvnHigh) = NodesAnalizer.HVN_SymmetricVA(startIdx, endIdx, pocIdx, hvnBandPct);

                hvnLevels.Add( (profilePrices[hvnLow], profilePrices[pocIdx], profilePrices[hvnHigh]) );
                hvnIndexes.Add( (hvnLow, pocIdx, hvnHigh) );

                // LVNs + Levels
                var (lvnLow, lvnHigh) = NodesAnalizer.LVN_SymmetricBand( startIdx, endIdx, lvnBandPct);

                lvnIndexes.Add( (lvnLow, startIdx, lvnHigh) );
                lvnLevels.Add( (profilePrices[lvnLow], profilePrices[startIdx], profilePrices[lvnHigh]) );
            }
            
            // Let's draw
            ClearOldNodes();

            string node = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? "HVN" : "LVN";
            Color nodeColor = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? ColorHVN : ColorLVN;
            
            var nodeLvls = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? hvnLevels : lvnLevels;
            var nodeIdxes = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? hvnIndexes : lvnIndexes;
            
            for (int i = 0; i < nodeLvls.Count; i++)
            {
                var level = nodeLvls[i];
                var index = nodeIdxes[i];
                
                ChartTrendLine low = Chart.DrawTrendLine($"{prefix}_{node}_Low_{index.Low}_{extraTPO}", x1_Start, level.Low, xBar, level.Low, ColorBand_Lower);   
                ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{node}_{index.Center}_{extraTPO}", x1_Start, level.Center, xBar, level.Center, nodeColor);   
                ChartTrendLine high = Chart.DrawTrendLine($"{prefix}_{node}_High_{index.High}_{extraTPO}", x1_Start, level.High, xBar, level.High, ColorBand_Upper);   
                ChartRectangle rectBand = Chart.DrawRectangle($"{prefix}_{node}_Band_{index.Center}_{extraTPO}", x1_Start, level.Low, xBar, level.High, ColorBand);
                
                FinalizeBands(low, center, high, rectBand);
            }
            
            // Local
            void FinalizeBands(ChartTrendLine low, ChartTrendLine center, ChartTrendLine high, ChartRectangle rectBand) 
            {
                LineStyle nodeStyle = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? LineStyleHVN : LineStyleLVN;
                int  nodeThick = ShowNode_Input == ShowNode_Data.HVN_With_Bands ? ThicknessHVN : ThicknessLVN;
            
                rectBand.IsFilled = true; 
                
                low.LineStyle = LineStyleBands; high.Thickness = ThicknessBands;
                center.LineStyle = nodeStyle; center.Thickness = nodeThick;
                high.LineStyle = LineStyleBands; high.Thickness = ThicknessBands;

                DateTime extDate = extraTPO == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
                if (extendNodes) 
                {
                    if (!extendNodes_FromStart) {
                        low.Time1 = xBar;
                        center.Time1 = xBar;
                        high.Time1 = xBar;
                        rectBand.Time1 = xBar;
                    }
                    
                    center.Time2 = extDate;
                    if (extendNodes_WithBands) {
                        low.Time2 = extDate;
                        high.Time2 = extDate;
                        rectBand.Time2 = extDate;
                    }
                }
                
                if (isIntraday && extraTPO != ExtraProfiles.MiniVP) {
                    low.Time1 = intraX1;
                    center.Time1 = intraX1;
                    high.Time1 = intraX1;
                    rectBand.Time1 = intraX1;
                }
            }
            void DrawRawNodes() 
            {
                string nodeRaw = ShowNode_Input == ShowNode_Data.HVN_Raw ? "HVN" : "LVN";
                List<int> nodeIndexes = ShowNode_Input == ShowNode_Data.HVN_Raw ? hvnsRaw : lvnsRaw;
                
                LineStyle nodeStyle_Raw = ShowNode_Input == ShowNode_Data.HVN_Raw ? LineStyleHVN : LineStyleLVN;
                int  nodeThick_Raw = ShowNode_Input == ShowNode_Data.HVN_Raw ? ThicknessHVN : ThicknessLVN;
                Color nodeColor_Raw = ShowNode_Input == ShowNode_Data.HVN_Raw ? ColorHVN : ColorLVN;

                foreach (int idx in nodeIndexes) 
                {
                    double nodePrice = profilePrices[idx];
                    ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{nodeRaw}_{idx}_{extraTPO}", x1_Start, nodePrice, xBar, nodePrice, nodeColor_Raw);
                    center.LineStyle = nodeStyle_Raw; center.Thickness = nodeThick_Raw;
                                        
                    DateTime extDate = extraTPO == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
                    if (extendNodes) {
                        if (!extendNodes_FromStart)
                            center.Time1 = xBar;
                        center.Time2 = extDate;
                    }
                    
                    if (isIntraday && extraTPO != ExtraProfiles.MiniVP)
                        center.Time1 = intraX1;
                }
            }
            void ClearOldNodes() {
                // 1º remove old price levels
                // 2º allow static-update of Params-Panel
                for (int i = 0; i < profilePrices.Length; i++)
                {
                    Chart.RemoveObject($"{prefix}_LVN_Low_{i}_{extraTPO}");
                    Chart.RemoveObject($"{prefix}_LVN_{i}_{extraTPO}");
                    Chart.RemoveObject($"{prefix}_LVN_High_{i}_{extraTPO}");
                    Chart.RemoveObject($"{prefix}_LVN_Band_{i}_{extraTPO}");

                    Chart.RemoveObject($"{prefix}_HVN_Low_{i}_{extraTPO}");
                    Chart.RemoveObject($"{prefix}_HVN_{i}_{extraTPO}");
                    Chart.RemoveObject($"{prefix}_HVN_High_{i}_{extraTPO}");
                    Chart.RemoveObject($"{prefix}_HVN_Band_{i}_{extraTPO}");
                }
            }
            DateTime extendDate() {
                string tfName = extraTPO == ExtraProfiles.No ?
                (VPInterval_Input == VPInterval_Data.Daily ? "D1" :
                    VPInterval_Input == VPInterval_Data.Weekly ? "W1" : "Month1" ) :
                extraTPO == ExtraProfiles.MiniVP ? MiniVPs_Timeframe.ShortName.ToString() :
                extraTPO == ExtraProfiles.Weekly ?  "W1" :  "Month1";

                // Get the time-based interval value
                string tfString = string.Join("", tfName.Where(char.IsDigit));
                int tfValue = int.TryParse(tfString, out int value) ? value : 1;

                DateTime dateToReturn = xBar;
                if (tfName.Contains('m'))
                    dateToReturn = xBar.AddMinutes(tfValue * extendNodes_Count);
                else if (tfName.Contains('h'))
                    dateToReturn = xBar.AddHours(tfValue * extendNodes_Count);
                else if (tfName.Contains('D'))
                    dateToReturn = xBar.AddDays(tfValue * extendNodes_Count);
                else if (tfName.Contains('W'))
                    dateToReturn = xBar.AddDays(7 * extendNodes_Count);
                else if (tfName.Contains("Month1"))
                    dateToReturn = xBar.AddMonths(tfValue * extendNodes_Count);

                return dateToReturn;
            }            
        }

        // ========= ========== ==========

        public void ClearAndRecalculate()
        {
            Thread.Sleep(300);
            LoadMoreHistory_IfNeeded();

            // LookBack from VP
            Bars vpBars = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                           VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            int firstIndex = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes.FirstOrDefault());

            // Get index of VP Interval to continue only in Lookback
            int iVerify = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            while (vpBars.ClosePrices.Count - iVerify > Lookback) {
                firstIndex++;
                iVerify = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            }

            // Daily or Weekly VP
            int TF_idx = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            int startIndex = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes[TF_idx]);

            // Weekly Profile but Daily VP
            bool extraWeekly = EnableVP && EnableWeeklyProfile && VPInterval_Input == VPInterval_Data.Daily;
            if (extraWeekly) {
                TF_idx = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                startIndex = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[TF_idx]);
            }

            // Monthly Profile
            bool extraMonthly = EnableVP && EnableMonthlyProfile;
            if (extraMonthly) {
                TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                startIndex = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[TF_idx]);
            }

            // Reset VOL_Bars/Source Index.
            lastBar_VP = 0;
            lastBar_ExtraProfiles._Mini = 0;
            lastBar_ExtraProfiles._Weekly = 0;
            lastBar_ExtraProfiles._Monthly = 0;

            // Reset Segments
            Segments_VP.Clear();
            segmentInfo.Clear();

            // Reset last update
            lastCleaned._VP_Interval = 0;
            lastCleaned._Mini = 0;

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

                if (EnableVP) {
                    CreateMonthlyVP(index);
                    CreateWeeklyVP(index);
                }

                // Calculate VP only in lookback
                if (extraWeekly || extraMonthly) {
                    iVerify = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    if (vpBars.ClosePrices.Count - iVerify > Lookback)
                        continue;
                }

                TF_idx = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                startIndex = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes[TF_idx]);

                if (index == startIndex ||
                   (index - 1) == startIndex && isPriceBased_Chart ||
                   (index - 1) == startIndex && (index - 1) != lastCleaned._VP_Interval)
                    CleanUp_MainVP(startIndex, index);

                CreateMiniVPs(index);

                try { if (EnableVP) VolumeProfile(startIndex, index); } catch { }
            }

            configHasChanged = true;
            DrawStartVolumeLine();
        }

        public void DrawStartVolumeLine() {
            try {
                DateTime firstVolDate = VOL_Bars.OpenTimes.FirstOrDefault();
                double firstVolPrice = VOL_Bars.HighPrices.FirstOrDefault();
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("Volume_Start", firstVolDate, Color.Red);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText($"Volume_Start_Text", $"{VOL_Timeframe.ShortName} Volume Data \n ends here", firstVolDate, firstVolPrice, Color.Red);
                textInfo.FontSize = 8;
            }
            catch { };

            try {
                Bar firstInterval_Bar = VPInterval_Input == VPInterval_Data.Daily ? DailyBars.FirstOrDefault() :
                                       VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars.FirstOrDefault() : MonthlyBars.FirstOrDefault();
                DateTime firstInterval_Date = firstInterval_Bar.OpenTime;
                double firstInterval_Price = firstInterval_Bar.High;

                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("Lookback_Start", firstInterval_Date, Color.Gray);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText($"Lookback_Start_Text", $"{VPInterval_Input} Interval Data \n ends here", firstInterval_Date, firstInterval_Price, Color.Gray);
                textInfo.FontSize = 8;
            }
            catch { };
        }
        public void DrawTargetDateLine() {
            try
            {
                Bars VPInterval_Bars = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                                       VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
                DateTime TargetVolDate = VPInterval_Bars.OpenTimes[VPInterval_Bars.ClosePrices.Count - Lookback];
                TargetVolDate = EnableWeeklyProfile && !EnableMonthlyProfile ? WeeklyBars.LastBar.OpenTime.Date :
                                EnableMonthlyProfile ? MonthlyBars.LastBar.OpenTime.Date :
                                TargetVolDate;
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeTarget", TargetVolDate, Color.Yellow);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText($"VolumeTargetText", $"Target Volume Data", TargetVolDate, VOL_Bars.HighPrices.FirstOrDefault(), Color.Red);
                textInfo.FontSize = 8;
            }
            catch { }
        }

        public void LoadMoreHistory_IfNeeded() {
            Bars VPInterval_Bars = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                                   VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;

            DateTime sourceDate = EnableWeeklyProfile && !EnableMonthlyProfile ? WeeklyBars.LastBar.OpenTime.Date :
                                  EnableMonthlyProfile ? MonthlyBars.LastBar.OpenTime.Date :
                                  VPInterval_Bars.OpenTimes[VPInterval_Bars.ClosePrices.Count - Lookback];

            if (LoadBarsStrategy_Input == LoadBarsStrategy_Data.Async)
            {
                if (VPInterval_Bars.ClosePrices.Count < Lookback || VOL_Bars.OpenTimes.FirstOrDefault() > sourceDate) {
                    loadingAsyncBars = false;
                    loadingBarsComplete = false;
                    timerHandler.isAsyncLoading = true;
                    Timer.Start(TimeSpan.FromSeconds(0.5));
                }
                return;
            }

            // Lookback
            if (VPInterval_Bars.ClosePrices.Count < Lookback)
            {
                PopupNotification notifyProgress = Notifications.ShowPopup(NOTIFY_CAPTION, $"Loading Sync => {VPInterval_Bars} Lookback Bars", PopupNotificationState.InProgress);

                while (VPInterval_Bars.ClosePrices.Count < Lookback)
                {
                    int loadedCount = VPInterval_Bars.LoadMoreHistory();
                    if (loadedCount == 0)
                        break;
                }

                notifyProgress.Complete(PopupNotificationState.Success);
            }

            DateTime lookbackDate = VPInterval_Bars.OpenTimes[VPInterval_Bars.ClosePrices.Count - Lookback];

            sourceDate = EnableWeeklyProfile && !EnableMonthlyProfile ? WeeklyBars.LastBar.OpenTime.Date :
                         EnableMonthlyProfile ? MonthlyBars.LastBar.OpenTime.Date :
                         VPInterval_Bars.OpenTimes[VPInterval_Bars.ClosePrices.Count - Lookback];

            if (EnableMiniProfiles && MiniVPs_Bars.OpenTimes.FirstOrDefault() > lookbackDate)
            {
                PopupNotification notifyProgress = Notifications.ShowPopup(NOTIFY_CAPTION, $"Loading Sync => {MiniVPs_Timeframe} Lookback Bars", PopupNotificationState.InProgress);

                while (MiniVPs_Bars.OpenTimes.FirstOrDefault() > lookbackDate)
                {
                    int loadedCount = MiniVPs_Bars.LoadMoreHistory();
                    if (loadedCount == 0)
                        break;
                }

                notifyProgress.Complete(PopupNotificationState.Success);
            }

            // Source
            if (VOL_Bars.OpenTimes.FirstOrDefault() > sourceDate)
            {
                PopupNotification notifyProgress_Two = Notifications.ShowPopup(NOTIFY_CAPTION, $"Loading Sync => {VOL_Timeframe.ShortName} Bars", PopupNotificationState.InProgress);

                while (VOL_Bars.OpenTimes.FirstOrDefault() > sourceDate)
                {
                    int loadedCount = VOL_Bars.LoadMoreHistory();
                    if (loadedCount == 0)
                        break;
                }

                notifyProgress_Two.Complete(PopupNotificationState.Success);
            }
        }

        protected override void OnTimer()
        {
            if (timerHandler.isAsyncLoading)
            {
                if (!loadingAsyncBars)
                {
                    string volumeLineInfo = "=> Zoom out and follow the Vertical Line";
                    asyncBarsPopup = Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Loading Async {VOL_Timeframe.ShortName} Bars \n{volumeLineInfo}",
                        PopupNotificationState.InProgress
                    );
                }

                if (!loadingBarsComplete)
                {
                    Bars VPInterval_Bars = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                                           VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
                    if (VPInterval_Bars.ClosePrices.Count < Lookback)
                    {
                        while (VPInterval_Bars.ClosePrices.Count < Lookback)
                        {
                            int loadedCount = VPInterval_Bars.LoadMoreHistory();
                            if (loadedCount == 0)
                                break;
                        }
                    }

                    DateTime lookbackDate = VPInterval_Bars.OpenTimes[VPInterval_Bars.ClosePrices.Count - Lookback];
                    if (EnableMiniProfiles && MiniVPs_Bars.OpenTimes.FirstOrDefault() > lookbackDate) {
                        while (MiniVPs_Bars.OpenTimes.FirstOrDefault() > lookbackDate)
                        {
                            int loadedCount = MiniVPs_Bars.LoadMoreHistory();
                            if (loadedCount == 0)
                                break;
                        }
                    }

                    DrawTargetDateLine();

                    DateTime sourceDate = EnableWeeklyProfile && !EnableMonthlyProfile ? WeeklyBars.LastBar.OpenTime.Date :
                                          EnableMonthlyProfile ? MonthlyBars.LastBar.OpenTime.Date :
                                          lookbackDate;

                    VOL_Bars.LoadMoreHistoryAsync((_) => {
                        DateTime currentDate = _.Bars.FirstOrDefault().OpenTime;

                        DrawStartVolumeLine();

                        if (currentDate != default && currentDate < sourceDate) {
                            if (asyncBarsPopup.State != PopupNotificationState.Success)
                                asyncBarsPopup.Complete(PopupNotificationState.Success);

                            loadingBarsComplete = true;
                        }
                    });

                    loadingAsyncBars = true;
                }
                else {
                    ClearAndRecalculate();
                    timerHandler.isAsyncLoading = false;
                    Timer.Stop();
                }
            }

        }

        public int GetLookback() {
            return Lookback;
        }
        public double GetRowHeight() {
            return rowHeight;
        }

        public void SetRowHeight(double number) {
            rowHeight = number;
        }
        public void SetLookback(int number) {
            Lookback = number;
            LoadMoreHistory_IfNeeded();
        }
        public void SetMiniVPsBars() {
            MiniVPs_Bars = MarketData.GetBars(MiniVPs_Timeframe);
        }
        public void SetVPBars() {
            VOL_Bars = MarketData.GetBars(VOL_Timeframe);
            LoadMoreHistory_IfNeeded();
        }

    }

    // ================ PARAMS PANEL ================
    /*
    What I've done since bringing it from ODF Aggregated, by order:
        Remove all unrelated Volume Profile inputs
        Add remaining VP settings
        Reogarnize inputs
        Add "VA + POC"
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
    public enum Supported_Sources {
        m1, m2, m3, m4, m5, m6, m7, m8, m9, m10, m15, m30, m45, h1, h2, h3, h4, h6, h8, h12, D1, D2, D3
    }

    public class ParamsPanel : CustomControl
    {
        private readonly FreeVolumeProfileV20 Outside;
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

        public ParamsPanel(FreeVolumeProfileV20 indicator, IndicatorParams defaultParams)
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
                    Key = "VPIntervalKey",
                    Label = "Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.VPInterval.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(VPInterval_Data)),
                    OnChanged = _ => UpdateVPInterval(),
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
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Buy_Sell && Outside.VPInterval_Input != VPInterval_Data.Weekly
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
                    IsVisible = () => Outside.VolumeMode_Input != VolumeMode_Data.Buy_Sell && Outside.VPInterval_Input != VPInterval_Data.Monthly
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
                },
                new()
                {
                    Region = "Volume Profile",
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
                    Region = "Volume Profile",
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
                    Key = "FixedSegmentsKey",
                    Label = "Segments",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SegmentsFixedRange.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SegmentsFixedRange_Data)),
                    OnChanged = _ => UpdateRangeSegments(),
                    IsVisible = () => Outside.EnableFixedRange
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "ShowOHLCKey",
                    Label = "OHLC Body?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.OHLC,
                    OnChanged = _ => UpdateCheckbox("ShowOHLCKey", val => Outside.ShowOHLC = val),
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "GradientKey",
                    Label = "Gradient?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.Gradient,
                    OnChanged = _ => UpdateCheckbox("GradientKey", val => Outside.EnableGradient = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Normal
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "FillIntraVPKey",
                    Label = "Intra-Space?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillIntradaySpace,
                    OnChanged = _ => UpdateCheckbox("FillIntraVPKey", val => Outside.FillIntradaySpace = val),
                    IsVisible = () => Outside.ShowIntradayProfile && (Outside.EnableWeeklyProfile || Outside.EnableMonthlyProfile)
                },

                new()
                {
                    Region = "Mini VPs",
                    RegionOrder = 3,
                    Key = "MiniVPsKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableMiniProfiles,
                    OnChanged = _ => UpdateCheckbox("MiniVPsKey", val => Outside.EnableMiniProfiles = val)
                },
                new()
                {
                    Region = "Mini VPs",
                    RegionOrder = 3,
                    Key = "MiniTFKey",
                    Label = "Mini-Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.MiniVPsTimeframe.ShortName.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateMiniVPTimeframe()
                },
                new()
                {
                    Region = "Mini VPs",
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
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "EnableNodeKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableNodes,
                    OnChanged = _ => UpdateCheckbox("EnableNodeKey", val => Outside.EnableNodeDetection = val)
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "NodeSmoothKey",
                    Label = "Smooth",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileSmooth.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ProfileSmooth_Data)),
                    OnChanged = _ => UpdateNodeSmooth()
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "NodeTypeKey",
                    Label = "Nodes",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileNode.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ProfileNode_Data)),
                    OnChanged = _ => UpdateNodeType()
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ShowNodeKey",
                    Label = "Show",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ShowNode.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ShowNode_Data)),
                    OnChanged = _ => UpdateShowNode(),
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "HvnBandPctKey",
                    Label = "HVN Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandHVN.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Band(),
                    IsVisible = () => (Outside.ShowNode_Input == ShowNode_Data.HVN_With_Bands || Outside.ShowNode_Input == ShowNode_Data.LVN_With_Bands) &&
                                       Outside.ProfileNode_Input != ProfileNode_Data.Percentile
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "LvnBandPctKey",
                    Label = "LVN Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandLVN.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Band(),
                    IsVisible = () => (Outside.ShowNode_Input == ShowNode_Data.HVN_With_Bands || Outside.ShowNode_Input == ShowNode_Data.LVN_With_Bands) &&
                                       Outside.ProfileNode_Input != ProfileNode_Data.Percentile
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "NodeStrongKey",
                    Label = "Only Strong?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.OnlyStrong, 
                    OnChanged = _ => UpdateCheckbox("NodeStrongKey", val => Outside.onlyStrongNodes = val)
                },
                // 'Strong HVN' for HVN_Raw(only) on [LocalMinMax, Topology]
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "StrongHvnPctKey",
                    Label = "(%) >= POC",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.StrongHVN.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Strong(),
                    IsVisible = () => Outside.onlyStrongNodes && (Outside.ShowNode_Input == ShowNode_Data.HVN_Raw ||
                                      Outside.ProfileNode_Input == ProfileNode_Data.Percentile && Outside.ShowNode_Input == ShowNode_Data.HVN_With_Bands)
                },
                // 'Strong LVN' should be used by HVN_With_Bands, since the POCs are derived from LVN Split.
                // on [LocalMinMax, Topology] 
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "StrongLvnPctKey",
                    Label = "(%) <= POC",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.StrongLVN.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Strong(),
                    IsVisible = () => Outside.onlyStrongNodes &&
                            (Outside.ShowNode_Input != ShowNode_Data.HVN_Raw && Outside.ProfileNode_Input != ProfileNode_Data.Percentile ||
                            Outside.ProfileNode_Input == ProfileNode_Data.Percentile && 
                            (Outside.ShowNode_Input == ShowNode_Data.LVN_With_Bands || Outside.ShowNode_Input == ShowNode_Data.LVN_Raw))
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtendNodeKey",
                    Label = "Extend?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ExtendNodes,
                    OnChanged = _ => UpdateCheckbox("ExtendNodeKey", val => Outside.extendNodes = val)
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtNodesCountKey",
                    Label = "Extend(count)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.ExtendNodes_Count.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateExtendNodesCount(),
                    IsVisible = () => Outside.extendNodes
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtBandsKey",
                    Label = "Ext.(bands)?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ExtendNodes_WithBands,
                    OnChanged = _ => UpdateCheckbox("ExtBandsKey", val => Outside.extendNodes_WithBands = val),
                    IsVisible = () => Outside.extendNodes
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "HvnPctileKey",
                    Label = "HVN(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.PctileHVN.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Pctile(),
                    IsVisible = () => Outside.ProfileNode_Input == ProfileNode_Data.Percentile
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "LvnPctileKey",
                    Label = "LVN(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.PctileLVN.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Pctile(),
                    IsVisible = () => Outside.ProfileNode_Input == ProfileNode_Data.Percentile
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtNodeStartKey",
                    Label = "From start?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ExtendNodes_FromStart,
                    OnChanged = _ => UpdateCheckbox("ExtNodeStartKey", val => Outside.extendNodes_FromStart = val),
                    IsVisible = () => Outside.extendNodes
                },

                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "UpdateVPKey",
                    Label = "Update At",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.UpdateProfileStrategy.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UpdateProfile_Data)),
                    OnChanged = _ => UpdateVP(),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "SourceVPKey",
                    Label = "Source",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => "m1",
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Sources)),
                    OnChanged = _ => UpdateSourceVP(),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "DistributionKey",
                    Label = "Distribution",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.Distribution.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Distribution_Data)),
                    OnChanged = _ => UpdateDistribution(),
                },

                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "ShowResultsKey",
                    Label = "Results?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowResults,
                    OnChanged = _ => UpdateCheckbox("ShowResultsKey", val => Outside.ShowResults = val),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "ShowMinMaxKey",
                    Label = "Min/Max?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowMinMax,
                    OnChanged = _ => UpdateCheckbox("ShowMinMaxKey", val => Outside.ShowMinMaxDelta = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowResults
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "OnlySubtKey",
                    Label = "Only Subtract?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowOnlySubtDelta,
                    OnChanged = _ => UpdateCheckbox("OnlySubtKey", val => Outside.ShowOnlySubtDelta = val),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Delta && Outside.ShowMinMaxDelta && Outside.ShowResults
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "OperatorKey",
                    Label = "Operator",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.OperatorBuySell.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(OperatorBuySell_Data)),
                    OnChanged = _ => UpdateOperator(),
                    IsVisible = () => Outside.VolumeMode_Input == VolumeMode_Data.Buy_Sell && Outside.ShowResults
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
                Text = "Free Volume Profile",
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
                case "IntradayVPKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "FillIntraVPKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "GradientKey":
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
                    RefreshVisibility();
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
        private void UpdateVPInterval()
        {
            var selected = comboBoxMap["VPIntervalKey"].SelectedItem;
            if (Enum.TryParse(selected, out VPInterval_Data intervalType) && intervalType != Outside.VPInterval_Input)
            {
                Outside.VPInterval_Input = intervalType;
                Outside.LoadMoreHistory_IfNeeded();
                RecalculateOutsideWithMsg();
            }
        }

        // ==== Volume Profile ====
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
        private void UpdateRangeSegments() {
            var selected = comboBoxMap["FixedSegmentsKey"].SelectedItem;
            if (Enum.TryParse(selected, out SegmentsFixedRange_Data segmentsType) && segmentsType != Outside.SegmentsFixedRange_Input)
            {
                Outside.SegmentsFixedRange_Input = segmentsType;
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

        // ==== Results ====
        private void UpdateOperator()
        {
            var selected = comboBoxMap["OperatorKey"].SelectedItem;
            if (Enum.TryParse(selected, out OperatorBuySell_Data op) && op != Outside.OperatorBuySell_Input)
            {
                Outside.OperatorBuySell_Input = op;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== HVN + LVN ====
        private void UpdateNodeSmooth()
        {
            var selected = comboBoxMap["NodeSmoothKey"].SelectedItem;
            if (Enum.TryParse(selected, out ProfileSmooth_Data smoothType) && smoothType != Outside.ProfileSmooth_Input)
            {
                Outside.ProfileSmooth_Input = smoothType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateNodeType()
        {
            var selected = comboBoxMap["NodeTypeKey"].SelectedItem;
            if (Enum.TryParse(selected, out ProfileNode_Data nodeType) && nodeType != Outside.ProfileNode_Input)
            {
                Outside.ProfileNode_Input = nodeType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateShowNode()
        {
            var selected = comboBoxMap["ShowNodeKey"].SelectedItem;
            if (Enum.TryParse(selected, out ShowNode_Data showNodeType) && showNodeType != Outside.ShowNode_Input)
            {
                Outside.ShowNode_Input = showNodeType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateHVN_Band()
        {
            if (double.TryParse(textInputMap["HvnBandPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.bandHVN_Pct)
                {
                    Outside.bandHVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateLVN_Band()
        {
            if (double.TryParse(textInputMap["LvnBandPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.bandLVN_Pct)
                {
                    Outside.bandLVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateHVN_Strong()
        {
            if (double.TryParse(textInputMap["StrongHvnPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.strongHVN_Pct)
                {
                    Outside.strongHVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateLVN_Strong()
        {
            if (double.TryParse(textInputMap["StrongLvnPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.strongLVN_Pct)
                {
                    Outside.strongLVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateExtendNodesCount() 
        {
            int value = int.TryParse(textInputMap["ExtNodesCountKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.extendNodes_Count)
            {
                Outside.extendNodes_Count = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateHVN_Pctile()
        {
            int value = int.TryParse(textInputMap["HvnPctileKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.pctileHVN_Value)
            {
                Outside.pctileHVN_Value = value;
                SetApplyVisibility();
            }
        }
        private void UpdateLVN_Pctile()
        {
            int value = int.TryParse(textInputMap["LvnPctileKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.pctileLVN_Value)
            {
                Outside.pctileLVN_Value = value;
                SetApplyVisibility();
            }
        }

        // ==== Misc ====
        private void UpdateVP()
        {
            var selected = comboBoxMap["UpdateVPKey"].SelectedItem;
            if (Enum.TryParse(selected, out UpdateProfile_Data updateType) && updateType != Outside.UpdateProfile_Input)
            {
                Outside.UpdateProfile_Input = updateType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSourceVP()
        {
            var selected = comboBoxMap["SourceVPKey"].SelectedItem;
            TimeFrame value = StringToTimeframe(selected);
            if (value != Outside.VOL_Timeframe)
            {
                Outside.VOL_Timeframe = value;
                Outside.SetVPBars();
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateDistribution()
        {
            var selected = comboBoxMap["DistributionKey"].SelectedItem;
            if (Enum.TryParse(selected, out Distribution_Data distributionType) && distributionType != Outside.Distribution_Input)
            {
                Outside.Distribution_Input = distributionType;
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
                ? $"VP {BrokerPrefix} {SymbolPrefix} {TimeframePrefix}"
                : $"VP {SymbolPrefix} {TimeframePrefix}";
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


    // ================ HVN + LVN ================
    public static class NodesAnalizer {
        
        public static double[] FixedKernel(double sigma = 2.0) {
            int radius = (int)(3 * sigma);
            int size = radius * 2 + 1;

            double[] kernel = new double[size];
            
            double sigma2 = sigma * sigma;
            double twoSigma2 = 2.0 * sigma2;
            double invSigma2 = 1.0 / twoSigma2;

            double sum = 0.0;
            for (int i = -radius; i <= radius; i++)
            {
                double v = Math.Exp(-(i * i) * invSigma2);
                kernel[i + radius] = v;
                sum += v;
            }

            // Normalize
            double invSum = 1.0 / sum;
            for (int i = 0; i < size; i++)
                kernel[i] *= invSum;

            return kernel;
        }

        public static double[] FixedCoefficients(int windowSize = 9) {
            if (windowSize % 2 == 0)
                throw new ArgumentException("windowSize must be odd");
            
            int polyOrder = 3;
            if (polyOrder >= windowSize)
                throw new ArgumentException("polyOrder must be < windowSize");

            int half = windowSize / 2;
            int size = windowSize;
            int cols = polyOrder + 1;

            // --- Design matrix A ---
            double[,] A = new double[size, cols];
            double power = 1.0;
            for (int i = -half; i <= half; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    A[i + half, j] = power;
                    power *= i;
                }
            }

            // --- Pseudoinverse (AᵀA)⁻¹Aᵀ ---
            double[,] AT = Transpose(A);
            double[,] ATA = Multiply(AT, A);
            double[,] ATAInv = Invert(ATA);
            double[,] pinv = Multiply(ATAInv, AT);

            // First row = smoothing coefficients
            double[] coeffs = new double[size];
            for (int i = 0; i < size; i++)
                coeffs[i] = pinv[0, i];

            return coeffs;
        }
        
        // === Smoothing ==
        // logic generated/converted by LLM
        // Added fixed kernel/coefficients
        public static double[] GaussianSmooth(double[] arr, double[] fixedKernel = null, double sigma = 2.0)
        {
            int radius = (int)(3 * sigma);

            fixedKernel ??= Array.Empty<double>();
            
            double[] kernel;
            if (fixedKernel.Length == 0)
            {
                int size = radius * 2 + 1;
                kernel = new double[size];

                // Build kernel
                double sum = 0.0;
                for (int i = -radius; i <= radius; i++)
                {
                    double value = Math.Exp(-(i * i) / (2.0 * sigma * sigma));
                    kernel[i + radius] = value;
                    sum += value;
                }

                // Normalize kernel
                for (int i = 0; i < size; i++)
                    kernel[i] /= sum;
            }
            else
                kernel = fixedKernel;

            int n = arr.Length;
            double[] result = new double[n];

            // Convolution (mode="same")
            for (int i = 0; i < n; i++)
            {
                double acc = 0.0;

                for (int k = -radius; k <= radius; k++)
                {
                    int idx = i + k;
                    if (idx >= 0 && idx < n)
                        acc += arr[idx] * kernel[k + radius];
                }

                result[i] = acc;
            }

            return result;
        }

        public static double[] SavitzkyGolay(double[] y, double [] fixedCoeff = null, int windowSize = 9)
        {
            if (windowSize % 2 == 0)
                throw new ArgumentException("windowSize must be odd");
            
            int polyOrder = 3;
            if (polyOrder >= windowSize)
                throw new ArgumentException("polyOrder must be < windowSize");

            fixedCoeff ??= Array.Empty<double>();
            
            double[] coeffs;
            if (fixedCoeff.Length == 0)
                coeffs = FixedCoefficients(windowSize);
            else
                coeffs = fixedCoeff;
                
            int half = windowSize / 2;
            int size = windowSize;
            
            // --- Pad signal (edge mode) ---
            int n = y.Length;
            double[] padded = new double[n + 2 * half];

            for (int i = 0; i < half; i++)
                padded[i] = y[0];

            for (int i = 0; i < n; i++)
                padded[i + half] = y[i];
            
            for (int i = 0; i < half; i++)
                padded[n + half + i] = y[n - 1];

            // --- Convolution (valid) ---
            double[] result = new double[n];

            for (int i = 0; i < n; i++)
            {
                double acc = 0.0;
                for (int j = 0; j < size; j++)
                    acc += padded[i + j] * coeffs[size - 1 - j];

                result[i] = acc;
            }

            return result;
        }
        private static double[,] Transpose(double[,] m)
        {
            int r = m.GetLength(0);
            int c = m.GetLength(1);
            double[,] t = new double[c, r];

            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    t[j, i] = m[i, j];

            return t;
        }
        private static double[,] Multiply(double[,] a, double[,] b)
        {
            int r = a.GetLength(0);
            int c = b.GetLength(1);
            int n = a.GetLength(1);

            double[,] m = new double[r, c];

            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    for (int k = 0; k < n; k++)
                        m[i, j] += a[i, k] * b[k, j];

            return m;
        }
        private static double[,] Invert(double[,] m)
        {
            int n = m.GetLength(0);
            double[,] a = new double[n, n * 2];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    a[i, j] = m[i, j];
                    a[i, j + n] = (i == j) ? 1.0 : 0.0;
                }

            for (int i = 0; i < n; i++)
            {
                double diag = a[i, i];
                for (int j = 0; j < n * 2; j++)
                    a[i, j] /= diag;

                for (int k = 0; k < n; k++)
                {
                    if (k == i) continue;
                    double factor = a[k, i];
                    for (int j = 0; j < n * 2; j++)
                        a[k, j] -= factor * a[i, j];
                }
            }

            double[,] inv = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    inv[i, j] = a[i, j + n];

            return inv;
        }

        // === Volume Node => Detection
        public static (List<int> maximum, List<int> minimum) FindLocalMinMax(double[] arr)
        {
            List<int> minimum = new();
            List<int> maximum = new();

            int n = arr.Length;
            if (n < 3)
                return (maximum, minimum);

            for (int i = 1; i < n - 1; i++)
            {
                if (arr[i] < arr[i - 1] && arr[i] < arr[i + 1])
                    minimum.Add(i);

                if (arr[i] > arr[i - 1] && arr[i] > arr[i + 1])
                    maximum.Add(i);
            }
            
            return (maximum, minimum);
        }
        public static (List<int> peaks, List<int> valleys) ProfileTopology(double[] profile)
        {
            int n = profile.Length;

            List<int> peaks = new();
            List<int> valleys = new();

            if (n < 3)
                return (peaks, valleys);

            // --- First derivative ---
            double[] d1 = new double[n];
            for (int i = 1; i < n - 1; i++)
                d1[i] = (profile[i + 1] - profile[i - 1]) * 0.5;

            d1[0] = profile[1] - profile[0];
            d1[n - 1] = profile[n - 1] - profile[n - 2];

            // --- Second derivative ---
            double[] d2 = new double[n];
            for (int i = 1; i < n - 1; i++)
                d2[i] = (d1[i + 1] - d1[i - 1]) * 0.5;

            // --- Peak & Valley detection ---
            for (int i = 1; i < n - 1; i++)
            {
                double s1 = Math.Sign(d1[i - 1]);
                double s2 = Math.Sign(d1[i]);

                // Peak (HVN / POC)
                if (s1 > 0 && s2 < 0 && d2[i] < 0)
                    peaks.Add(i);

                // Valley (LVN)
                if (s1 < 0 && s2 > 0 && d2[i] > 0)
                    valleys.Add(i);
            }
            
            return (peaks, valleys);
        }
        public static (List<int> hvnIdx, List<int> lvnIdx) PercentileNodes(double[] profile, int hvnPct, int lvnPct)
        {
            List<int> hvnIdx = new();
            List<int> lvnIdx = new();

            if (profile.Length == 0)
                return (hvnIdx, lvnIdx);

            double hvnThreshold = Percentile(profile, hvnPct);
            double lvnThreshold = Percentile(profile, lvnPct);

            for (int i = 0; i < profile.Length; i++)
            {
                if (profile[i] >= hvnThreshold)
                    hvnIdx.Add(i);

                if (profile[i] <= lvnThreshold)
                    lvnIdx.Add(i);
            }
            
            return (hvnIdx, lvnIdx);
        }

        private static double Percentile(double[] data, double percentile)
        {
            if (data.Length == 0)
                return 0.0;

            double[] copy = (double[])data.Clone();
            Array.Sort(copy);

            double pos = (percentile / 100.0) * (copy.Length - 1);
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);

            if (lo == hi)
                return copy[lo];

            double frac = pos - lo;
            return copy[lo] * (1.0 - frac) + copy[hi] * frac;
        }
        
        // === Volume Node => Levels
        public static (int Low, int High) HVN_SymmetricVA(int startIdx, int endIdx, int pocIdx, double vaPct = 0.70)
        {
            int width = endIdx - startIdx;
            int half = (int)(width * vaPct / 2.0);

            int low = Math.Max(startIdx, pocIdx - half);
            int high = Math.Min(endIdx, pocIdx + half);

            return (low, high);
        }
        public static (int Low, int High) LVN_SymmetricBand(int lvn, int nextLvn, double bandPct = 0.25)
        {
            int width = nextLvn - lvn;
            int radius = (int)(width * bandPct / 2.0);

            int low = Math.Max(0, lvn - radius);
            int high = Math.Min(nextLvn, lvn + radius);

            return (low, high);
        }
        public static List<List<int>> GroupConsecutiveIndexes(IList<int> indices)
        {
            var groups = new List<List<int>>();

            if (indices == null || indices.Count == 0)
                return groups;

            var current = new List<int> { indices[0] };
            groups.Add(current);

            for (int i = 1; i < indices.Count; i++)
            {
                if (indices[i] == indices[i - 1] + 1) 
                    current.Add(indices[i]);
                else {
                    current = new List<int> { indices[i] };
                    groups.Add(current);
                }
            }

            return groups;
        }
    }
}
