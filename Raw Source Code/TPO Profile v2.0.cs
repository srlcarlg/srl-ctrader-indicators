/*
--------------------------------------------------------------------------------------------------------------------------------
                        TPO Profile v2.0
                           revision 2
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

Last update => 19/01/2026
===========================

- What's new in rev. 2? (2026)

HVN + LVN:
  - Detection:
    - Smoothing => [Gaussian, Savitzky_Golay]
    - Nodes => [LocalMinMax, Topology, Percentile]
  - Levels(bands)
    - VA-like, set by percentage.
    - (Important!) The "mini-pocs" shown in 'HVN_With_Bands' are derived from LVN splits!
        - Decrease the "(%) <= POC" input of "Only Strong?" when filtering the LVNs or HVN_With_Bands.
        - This 'rule' apply only to [LocalMinMax, Topology].
    - (Tip) Use 'LineStyles = [Solid, Lines, LinesDots]' if any stuttering/lagging occurs when scrolling at profiles on chart (Reduce GPU workload).
      
Improved Performance of:
  - 'VA + POC'
  - 'Results'

Add "Segments" to "TPO Profile" => "Fixed Range?" (params-panel):
  - Monthly_Aligned (limited to the current Month)
  - From_Profile (available to any period without the 'bug' between months)
    
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

        [Parameter("LineStyle VA:", DefaultValue = LineStyle.LinesDots, Group = "==== Value Area ====")]
        public LineStyle LineStyleVA { get; set; }

        [Parameter("Thickness VA:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Value Area ====")]
        public int ThicknessVA { get; set; }


        [Parameter("Color HVN:", DefaultValue = "#DFFFD700" , Group = "==== HVN/LVN ====")]
        public Color ColorHVN { get; set; }
        
        [Parameter("LineStyle HVN:", DefaultValue = LineStyle.LinesDots, Group = "==== HVN/LVN ====")]
        public LineStyle LineStyleHVN { get; set; }

        [Parameter("Thickness HVN:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== HVN/LVN ====")]
        public int ThicknessHVN { get; set; }

        [Parameter("Color LVN:", DefaultValue = "#DFDC143C", Group = "==== HVN/LVN ====")]
        public Color ColorLVN { get; set; }

        [Parameter("LineStyle LVN:", DefaultValue = LineStyle.LinesDots, Group = "==== HVN/LVN ====")]
        public LineStyle LineStyleLVN { get; set; }

        [Parameter("Thickness LVN:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== HVN/LVN ====")]
        public int ThicknessLVN { get; set; }


        [Parameter("Color Band:", DefaultValue = "#19F0F8FF",  Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public Color ColorBand { get; set; }
        
        [Parameter("Color Lower:", DefaultValue = "#6CB0E0E6",  Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public Color ColorBand_Lower { get; set; }

        [Parameter("Color Upper:", DefaultValue = "#6CB0E0E6",  Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public Color ColorBand_Upper { get; set; }

        [Parameter("LineStyle Bands:", DefaultValue = LineStyle.DotsVeryRare, Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public LineStyle LineStyleBands { get; set; }

        [Parameter("Thickness Bands:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Symmetric Bands (HVN/LVN) ====")]
        public int ThicknessBands { get; set; }


        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }
        [Parameter("Visually based in MT4", DefaultValue = "riv-ay-(TPOChart/MarketProfileDWM)", Group = "==== Credits ====")]
        public string Credits_2 { get; set; }

        // Moved from cTrader Input to Params Panel

        // ==== General ====
        public enum TPOMode_Data {
            Aggregated
        }
        public enum TPOInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }

        public class GeneralParams_Info {
            public int Lookback = 1;
            public TPOMode_Data TPOMode_Input = TPOMode_Data.Aggregated;
            public TPOInterval_Data TPOInterval_Input = TPOInterval_Data.Daily;
        }
        public GeneralParams_Info GeneralParams = new();


        // ==== TPO Profile ====
        public enum UpdateProfile_Data
        {
            EveryTick_CPU_Workout,
            ThroughSegments_Balanced,
            Through_2_Segments_Best,
        }
        public enum HistSide_Data
        {
            Left,
            Right,
        }
        public enum HistWidth_Data
        {
            _15,
            _30,
            _50,
            _70,
            _100
        }
        // Allow "old" segmentation "From_Profile", 
        // so the "Fixed Range" doesn't "bug" => remains on chart between months (end/start of each month) / limited to the current month.
        public enum SegmentsFixedRange_Data
        {
            Monthly_Aligned,
            From_Profile
        }       
        
        public class ProfileParams_Info {
            public bool EnableMainTPO = false;
            public bool EnableWeeklyProfile = false;
            public bool EnableMonthlyProfile = false;
            public UpdateProfile_Data UpdateProfile_Input = UpdateProfile_Data.Through_2_Segments_Best;
            public bool FillHist_TPO = true;

            public HistSide_Data HistogramSide_Input = HistSide_Data.Left;
            public HistWidth_Data HistogramWidth_Input = HistWidth_Data._70;

            public bool EnableFixedRange = false;
            public SegmentsFixedRange_Data SegmentsFixedRange_Input = SegmentsFixedRange_Data.From_Profile;
                    
            public bool ShowOHLC = false;
            public bool ShowResults = true;

            // ==== Mini TPOs ====
            public bool EnableMiniProfiles = true;
            public TimeFrame MiniTPOs_Timeframe = TimeFrame.Hour4;
            public bool ShowMiniResults = true;

            // ==== Intraday View ====
            public bool ShowIntradayProfile = false;
            public int OffsetBarsInput = 2;
            public TimeFrame OffsetTimeframeInput = TimeFrame.Hour;
            public bool FillIntradaySpace { get; set; }

        }
        public ProfileParams_Info ProfileParams = new();
        

        // ==== VA + POC ====
        public class VAParams_Info {
            public bool ShowVA = false;
            public int PercentVA = 65;
            public bool KeepPOC = false;
            public bool ExtendPOC = false;
            public bool ExtendVA = false;
            public int ExtendCount = 1;
        }
        public VAParams_Info VAParams = new();

        
        // ==== HVN + LVN ====
        public enum ProfileSmooth_Data
        {
            Gaussian,
            Savitzky_Golay
        }
        public enum ProfileNode_Data
        {
            LocalMinMax,
            Topology,
            Percentile
        }
        public enum ShowNode_Data
        {
            HVN_With_Bands,
            HVN_Raw,
            LVN_With_Bands,
            LVN_Raw
        }
        public class NodesParams_Info {

            public bool EnableNodeDetection = true;

            public ProfileSmooth_Data ProfileSmooth_Input = ProfileSmooth_Data.Gaussian;
            public ProfileNode_Data ProfileNode_Input = ProfileNode_Data.LocalMinMax;

            public ShowNode_Data ShowNode_Input = ShowNode_Data.HVN_With_Bands;
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
        }
        public NodesParams_Info NodesParams = new();

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
        private readonly Dictionary<int, SegmentsExtremumInfo> segmentInfo = new();
        private readonly Dictionary<int, List<double>> segmentsDict = new();
        private readonly Dictionary<string, List<double>> segmentsFromProfile = new();
        private List<double> Segments = new();

        private Dictionary<double, double> TPO_Rank_Histogram = new();

        // Weekly, Monthly and Mini TPOs
        public class TPORankType
        {
            public Dictionary<double, double> TPO_Histogram = new();

            public void ClearAll() {
                TPO_Histogram.Clear();
            }
        }
        private readonly TPORankType MonthlyRank = new();
        private readonly TPORankType WeeklyRank = new();
        private readonly TPORankType MiniRank = new();
        private readonly Dictionary<string, TPORankType> FixedRank = new();

        // Fixed Range Profile
        public class RangeObjs_Info {
            public List<ChartRectangle> rectangles = new();
            public Dictionary<string, List<ChartText>> infoObjects = new();
            public Dictionary<string, Border> controlGrids = new();
        }
        private readonly RangeObjs_Info RangeObjs = new();

        // HVN + LVN => Performance
        public double[] nodesKernel = null;

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

        /*
          Its a annoying behavior that happens even in Candles Chart (Time-Based) on any symbol/broker.
          where it's jump/pass +1 index when .GetIndexByTime is used... the exactly behavior of Price-Based Charts
          Seems to happen only in Lower Timeframes (<=´Daily)
          So, to ensure that it works flawless, an additional verification is needed.
        */
        public class CleanedIndex {
            public int MainTPO = 0;
            public int Mini = 0;
            public void ResetAll() {
                MainTPO = 0;
                Mini = 0;
            }
        }
        private readonly CleanedIndex ClearIdx = new();

        // Concurrent Live TPO Update
        private class LockObjs_Info {
            public readonly object Bar = new();
            public readonly object MainTPO = new();
            public readonly object WeeklyTPO = new();
            public readonly object MonthlyTPO = new();
            public readonly object MiniTPO = new();
        }
        private readonly LockObjs_Info _Locks = new();

        private class TaskObjs_Info {
            public CancellationTokenSource cts;
            public Task MainTPO;
            public Task WeeklyTPO;
            public Task MonthlyTPO;
            public Task MiniTPO;
        }
        private readonly TaskObjs_Info _Tasks = new();

        private bool liveTPO_RunWorker = false;

        public class LiveTPOIndex {
            public int MainTPO { get; set; }
            public int Mini { get; set; }
            public int Weekly { get; set; }
            public int Monthly { get; set; }
        }
        private readonly LiveTPOIndex LiveTPOIndexes = new();
        private List<Bar> Bars_List = new();

        // Shared rowHeight
        private double rowHeight = 0;
        private double heightPips = 4;
        public double heightATR = 4;

        // Some required utils
        private double prevUpdatePrice;
        private bool configHasChanged = false;
        private bool isUpdateTPO = false;
        public bool isPriceBased_Chart = false;
        public bool isRenkoChart = false;

        // Params Panel
        private Border ParamBorder;
        public class IndicatorParams
        {
            public GeneralParams_Info GeneralParams { get; set; }
            public double RowHeightInPips { get; set; }
            public ProfileParams_Info ProfileParams { get; set; }
            public VAParams_Info VAParams { get; set; }
            public NodesParams_Info NodesParams { get; set; }
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
            if (RowConfig_Input == RowConfig_Data.ATR && Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Day3)
            {
                if (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Minute4)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Hour;
                    else if (Chart.TimeFrame == TimeFrame.Minute2)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Hour2;
                    else if (Chart.TimeFrame <= TimeFrame.Minute4)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Hour3;
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute5)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Hour4;
                    else if (Chart.TimeFrame == TimeFrame.Minute6)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Hour6;
                    else if (Chart.TimeFrame <= TimeFrame.Minute8)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Hour8;
                    else if (Chart.TimeFrame <= TimeFrame.Minute10)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Hour12;
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour)
                        ProfileParams.MiniTPOs_Timeframe = TimeFrame.Daily;

                    else if (Chart.TimeFrame <= TimeFrame.Hour8) {
                        ProfileParams.EnableMainTPO = true;
                        ProfileParams.EnableMiniProfiles = false;
                        GeneralParams.TPOInterval_Input = TPOInterval_Data.Weekly;
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Weekly) {
                    ProfileParams.EnableMainTPO = true;
                    ProfileParams.EnableMiniProfiles = false;
                    GeneralParams.TPOInterval_Input = TPOInterval_Data.Monthly;
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
            MiniTPOs_Bars = MarketData.GetBars(ProfileParams.MiniTPOs_Timeframe);

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
                GeneralParams = GeneralParams,
                RowHeightInPips = heightPips,
                ProfileParams = ProfileParams,
                VAParams = VAParams,
                NodesParams = NodesParams,
            };

            ParamsPanel ParamPanel = new(this, DefaultParams);

            ParamBorder = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                // ParamsPanel - Lock Width
                Width = 290,
                Child = ParamPanel
            };
            Chart.AddControl(ParamBorder);

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
            if (!IsLastBar) {
                CreateMonthlyTPO(index);
                CreateWeeklyTPO(index);
            }

            // LookBack
            Bars tpoBars = GeneralParams.TPOInterval_Input == TPOInterval_Data.Daily ? DailyBars :
                           GeneralParams.TPOInterval_Input == TPOInterval_Data.Weekly ? WeeklyBars : MonthlyBars;

            // Get Index of TPO Interval to continue only in Lookback
            int iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (tpoBars.ClosePrices.Count - iVerify > GeneralParams.Lookback)
                return;

            int TF_idx = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int startIndex = Bars.OpenTimes.GetIndexByTime(tpoBars .OpenTimes[TF_idx]);

            // Clean Dicts
            if (index == startIndex ||
                (index - 1) == startIndex && isPriceBased_Chart ||
                (index - 1) == startIndex && (index - 1) != ClearIdx.MainTPO
            )
                CleanUp_MainTPO(index, startIndex);

            // Historical data
            if (!IsLastBar)
            {
                // Allows MiniTPOs if (!EnableTPO)
                CreateMiniTPOs(index);

                if (ProfileParams.EnableMainTPO)
                    TPO_Profile(startIndex, index);

                isUpdateTPO = true; // chart end
            }
            else
            {
                if (UpdateStrategy_Input == UpdateStrategy_Data.SameThread_MayFreeze)
                {
                    if (ProfileParams.EnableMainTPO)
                        LiveTPO_Update(startIndex, index);
                    else if (!ProfileParams.EnableMainTPO && ProfileParams.EnableMiniProfiles)
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
            ClearIdx.MainTPO = index == startIndex ? index : (index - 1);
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
            if (ProfileParams.SegmentsFixedRange_Input == SegmentsFixedRange_Data.From_Profile)
                return segmentsFromProfile[fixedKey];
            else
                return segmentsDict[TF_idx];
        }

        // *********** MWM PROFILES ***********
        private void CreateMiniTPOs(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (ProfileParams.EnableMiniProfiles)
            {
                int miniIndex = MiniTPOs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int miniStart = Bars.OpenTimes.GetIndexByTime(MiniTPOs_Bars.OpenTimes[miniIndex]);

                if (index == miniStart ||
                    (index - 1) == miniStart && isPriceBased_Chart ||
                    (index - 1) == miniStart && (index - 1) != ClearIdx.Mini || loopStart
                ) {
                    MiniRank.ClearAll();
                    ClearIdx.Mini = index == miniStart ? index : (index - 1);
                }
                if (!isConcurrent)
                    TPO_Profile(miniStart, index, ExtraProfiles.MiniTPO, isLoop);
                else
                {
                    _Tasks.MiniTPO ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.MiniTPO, _Tasks.cts.Token));

                    LiveTPOIndexes.Mini = miniStart;

                    if (index != miniStart) {
                        lock (_Locks.MiniTPO)
                            TPO_Profile(miniStart, index, ExtraProfiles.MiniTPO, false, true);
                    }
                }
            }
        }
        private void CreateWeeklyTPO(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (ProfileParams.EnableWeeklyProfile)
            {
                // Avoid recalculating the same period.
                if (GeneralParams.TPOInterval_Input == TPOInterval_Data.Weekly)
                    return;

                int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                if (index == weekStart || (index - 1) == weekStart && isPriceBased_Chart || loopStart)
                    WeeklyRank.ClearAll();

                if (!isConcurrent)
                    TPO_Profile(weekStart, index, ExtraProfiles.Weekly, isLoop);
                else
                {
                    _Tasks.WeeklyTPO ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.Weekly, _Tasks.cts.Token));

                    LiveTPOIndexes.Weekly = weekStart;

                    if (index != weekStart) {
                        lock (_Locks.WeeklyTPO)
                            TPO_Profile(weekStart, index, ExtraProfiles.Weekly, false, true);
                    }
                }
            }
        }
        private void CreateMonthlyTPO(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            // Avoid recalculating the same period.
            if (GeneralParams.TPOInterval_Input == TPOInterval_Data.Monthly)
                return;

            if (ProfileParams.EnableMonthlyProfile)
            {
                int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                if (index == monthStart || (index - 1) == monthStart && isPriceBased_Chart || loopStart)
                    MonthlyRank.ClearAll();

                if (!isConcurrent)
                    TPO_Profile(monthStart, index, ExtraProfiles.Monthly, isLoop);
                else
                {
                    _Tasks.MonthlyTPO ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.Monthly, _Tasks.cts.Token));

                    LiveTPOIndexes.Monthly = monthStart;

                    if (index != monthStart) {
                        lock (_Locks.MonthlyTPO)
                            TPO_Profile(monthStart, index, ExtraProfiles.Monthly, false, true);
                    }
                }
            }
        }

        // *********** TPO PROFILE ***********
        private void TPO_Profile(int iStart, int index,  ExtraProfiles extraProfiles = ExtraProfiles.No, bool isLoop = false, bool drawOnly = false, string fixedKey = "", double fixedLowest = 0, double fixedHighest = 0)
        {
            if (extraProfiles == ExtraProfiles.Fixed && ProfileParams.SegmentsFixedRange_Input == SegmentsFixedRange_Data.From_Profile)
                CreateSegments_FromFixedRange(Bars.OpenPrices[iStart], fixedLowest, fixedHighest, fixedKey);
                
            // ==== TPO Column ====
            if (!drawOnly)
                TPO_Bars(index, extraProfiles, fixedKey);

            // ==== Drawing ====
            if (Segments.Count == 0 || isLoop)
                return;

            // For Results
            Bars mainTF = GeneralParams.TPOInterval_Input switch {
                TPOInterval_Data.Weekly => WeeklyBars,
                TPOInterval_Data.Monthly => MonthlyBars,
                _ => DailyBars
            };                           
            Bars TF_Bars = extraProfiles switch {
                ExtraProfiles.MiniTPO => MiniTPOs_Bars,
                ExtraProfiles.Weekly => WeeklyBars,
                ExtraProfiles.Monthly => MonthlyBars,
                // Fixed should use Monthly Bars, so TF_idx can be used by "whichSegment" variable
                ExtraProfiles.Fixed => MonthlyBars,
                _ => mainTF
            };
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

            bool gapWeekend = Bars.OpenTimes[iStart].DayOfWeek == DayOfWeek.Friday && Bars.OpenTimes[iStart].Hour < 2;
            DateTime x1_Start = Bars.OpenTimes[iStart + (gapWeekend ? 1 : 0)];
            DateTime xBar = Bars.OpenTimes[index];

            bool isIntraday = ProfileParams.ShowIntradayProfile && index == Chart.LastVisibleBarIndex && !isLoop;
            DateTime intraDate = xBar;

            // Any Volume Mode
            double maxLength = xBar.Subtract(x1_Start).TotalMilliseconds;

            HistWidth_Data selectedWidth = ProfileParams.HistogramWidth_Input;
            double maxWidth = ProfileParams.HistogramWidth_Input switch {
                HistWidth_Data._15 => 1.25,
                HistWidth_Data._30 => 1.50,
                HistWidth_Data._50 => 2,
                _ => 3
            };
            double proportion_TPO = maxLength - (maxLength / maxWidth);
            if (selectedWidth == HistWidth_Data._100)
                proportion_TPO = maxLength;
            
            // Profile Selection
            Dictionary<double, double> tpoDict = extraProfiles switch
            {
                ExtraProfiles.Monthly => MonthlyRank.TPO_Histogram,
                ExtraProfiles.Weekly => WeeklyRank.TPO_Histogram,
                ExtraProfiles.MiniTPO => MiniRank.TPO_Histogram,
                ExtraProfiles.Fixed => FixedRank[fixedKey].TPO_Histogram,
                _ => TPO_Rank_Histogram
            };

            // Same for all
            bool intraBool = extraProfiles switch
            {
                ExtraProfiles.Monthly => isIntraday,
                ExtraProfiles.Weekly => isIntraday,
                ExtraProfiles.MiniTPO => false,
                ExtraProfiles.Fixed => false,
                _ => isIntraday
            };

            // (micro)Optimization
            double maxValue = tpoDict.Any() ? tpoDict.Values.Max() : 0;

            // Segments selection
            string prefix = extraProfiles == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";
            List<double> whichSegment = extraProfiles == ExtraProfiles.Fixed ? GetRangeSegments(TF_idx, fixedKey) : Segments;
                        
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

                    if (ProfileParams.FillHist_TPO)
                        volHist.IsFilled = true;

                    if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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
                            if (!ProfileParams.EnableMonthlyProfile && ProfileParams.FillIntradaySpace)
                            {
                                volHist.Time1 = dateOffset;
                                volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                            }
                        }
                        if (extraProfiles == ExtraProfiles.Monthly)
                        {
                            if (ProfileParams.EnableWeeklyProfile) {
                                // Show after
                                volHist.Time1 = dateOffset_Triple;
                                volHist.Time2 = dateOffset_Triple.AddMilliseconds(-dynLength_Intraday);
                                // Show after together
                                if (ProfileParams.FillIntradaySpace) {
                                    volHist.Time1 = dateOffset_Duo;
                                    volHist.Time2 = dateOffset_Duo.AddMilliseconds(dynLength_Intraday);
                                }
                            } else {
                                // Use Weekly position
                                volHist.Time1 = dateOffset_Duo;
                                volHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Intraday);
                                if (ProfileParams.FillIntradaySpace) {
                                    volHist.Time1 = dateOffset;
                                    volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                                }
                            }
                        }

                        intraDate = volHist.Time1;
                    }
                }

                // Draw histograms and update 'intraDate' for VA/POC, if applicable
                DrawRectangle_Normal(tpoDict[priceKey], maxValue, intraBool);
            }
            
            // Drawings that don't require each segment-price as y-axis
            // It can/should be outside SegmentsLoop for better performance.
            
            double lowest = TF_Bars.LowPrices[TF_idx];
            double highest = TF_Bars.HighPrices[TF_idx];
            // Mini TPOs avoid crash after recalculating
            if (double.IsNaN(lowest)) {
                lowest = TF_Bars.LowPrices.LastValue;
                highest = TF_Bars.HighPrices.LastValue;
            }
            double y1 = extraProfiles == ExtraProfiles.Fixed ? fixedLowest : lowest;
            
            // Results
            if (extraProfiles == ExtraProfiles.MiniTPO && ProfileParams.ShowMiniResults || 
                extraProfiles != ExtraProfiles.MiniTPO && ProfileParams.ShowResults)
            {
                double sum = Math.Round(tpoDict.Values.Sum());
                string strValue = FormatResults ? FormatBigNumber(sum) : $"{sum}";

                ChartText Center = Chart.DrawText($"{prefix}_TPO_{extraProfiles}_Result", $"\n{strValue}", x1_Start, y1, HistColor);
                Center.HorizontalAlignment = HorizontalAlignment.Center;
                Center.FontSize = FontSizeResults - 1;

                if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
                    Center.Time = xBar;

                // Intraday Right Profile
                if (isIntraday && extraProfiles == ExtraProfiles.No) {
                    DateTime dateOffset = TimeBasedOffset(xBar);
                    Center.Time = dateOffset;
                }
            }
            
            // VA + POC
            Draw_VA_POC(tpoDict, iStart, x1_Start, xBar, extraProfiles, isIntraday, intraDate, fixedKey);
            
            // HVN/LVN
            DrawVolumeNodes(tpoDict, iStart, x1_Start, xBar, extraProfiles, isIntraday, intraDate, fixedKey);
                
            if (!ProfileParams.ShowOHLC || extraProfiles == ExtraProfiles.Fixed)
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
            List<double> whichSegment = extraTPO == ExtraProfiles.Fixed ? GetRangeSegments(TF_idx, fixedKey) : Segments;

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

            bool updateStrategy = ProfileParams.UpdateProfile_Input switch {
                UpdateProfile_Data.ThroughSegments_Balanced => Math.Abs(price - prevUpdatePrice) >= rowHeight,
                UpdateProfile_Data.Through_2_Segments_Best => Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight),
                _ => true
            };

            if (updateStrategy || isUpdateTPO || configHasChanged)
            {
                if (!onlyMini)
                {
                    if (ProfileParams.EnableMonthlyProfile && GeneralParams.TPOInterval_Input != TPOInterval_Data.Monthly)
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

                    if (ProfileParams.EnableWeeklyProfile && GeneralParams.TPOInterval_Input != TPOInterval_Data.Weekly)
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

                    if (ProfileParams.EnableMiniProfiles)
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
            if (ProfileParams.UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                prevUpdatePrice = price;
        }

        private void LiveTPO_Concurrent(int index, int startIndex)
        {
            if (!ProfileParams.EnableMainTPO && !ProfileParams.EnableMiniProfiles)
                return;

            double price = Bars.ClosePrices[index];
            bool updateStrategy = ProfileParams.UpdateProfile_Input switch {
                UpdateProfile_Data.ThroughSegments_Balanced => Math.Abs(price - prevUpdatePrice) >= rowHeight,
                UpdateProfile_Data.Through_2_Segments_Best => Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight),
                _ => true
            };

            if (updateStrategy || isUpdateTPO || configHasChanged)
            {
                lock (_Locks.Bar)
                    Bars_List = Bars.ToList();

                liveTPO_RunWorker = true;
            }
            _Tasks.cts ??= new CancellationTokenSource();

            CreateMonthlyTPO(index, isConcurrent: true);
            CreateWeeklyTPO(index, isConcurrent: true);
            CreateMiniTPOs(index, isConcurrent: true);

            if (ProfileParams.EnableMainTPO)
            {
                _Tasks.MainTPO ??= Task.Run(() => LiveTPO_Worker(ExtraProfiles.No, _Tasks.cts.Token));
                LiveTPOIndexes.MainTPO = startIndex;
                if (index != startIndex) {
                    lock (_Locks.MainTPO)
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
            Dictionary<double, double> Worker_TPO_Histogram = new();
            IEnumerable<Bar> BarsCopy = new List<Bar>();

            while (!token.IsCancellationRequested)
            {
                if (!liveTPO_RunWorker) {
                    // Stop itself
                    if (extraID == ExtraProfiles.No && !ProfileParams.EnableMainTPO) {
                        _Tasks.MainTPO = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.MiniTPO && !ProfileParams.EnableMiniProfiles) {
                        _Tasks.MiniTPO = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Weekly && !ProfileParams.EnableWeeklyProfile) {
                        _Tasks.WeeklyTPO = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Monthly && !ProfileParams.EnableMonthlyProfile) {
                        _Tasks.MonthlyTPO = null;
                        return;
                    }

                    Thread.Sleep(100);
                    continue;
                }

                try
                {
                    Worker_TPO_Histogram = new Dictionary<double, double>();

                    // Chart Bars
                    int startIndex = extraID switch {
                        ExtraProfiles.MiniTPO => LiveTPOIndexes.Mini,
                        ExtraProfiles.Weekly => LiveTPOIndexes.Weekly,
                        ExtraProfiles.Monthly => LiveTPOIndexes.Monthly,
                        _ => LiveTPOIndexes.MainTPO
                    };
                    DateTime lastBarTime = GetByInvoke(() => Bars.LastBar.OpenTime);

                    // Always replace
                    lock (_Locks.Bar)
                        BarsCopy = Bars_List.Skip(startIndex);

                    int endIndex = BarsCopy.Count();
                    for (int i = 0; i < endIndex; i++)
                    {
                        Worker_TPO_Bars(i, extraID, i == (endIndex - 1));
                    }

                    object whichLock = extraID switch {
                        ExtraProfiles.MiniTPO => _Locks.MiniTPO,
                        ExtraProfiles.Weekly => _Locks.WeeklyTPO,
                        ExtraProfiles.Monthly => _Locks.MonthlyTPO,
                        _ => _Locks.MainTPO
                    };
  
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

                        if (ProfileParams.UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                            prevUpdatePrice = BarsCopy.Last().Close;
                    }
                }
                catch (Exception e) { Print($"CRASH at LiveTPO_Worker => {extraID}: {e}"); }

                liveTPO_RunWorker = false;
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
            _Tasks.cts.Cancel();
            if (ProfileParams.EnableFixedRange) {
                foreach (ChartRectangle item in RangeObjs.rectangles)
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
            if (!ProfileParams.EnableFixedRange)
                return;

            foreach (var rect in RangeObjs.rectangles.ToArray())
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
            foreach (var control in RangeObjs.controlGrids.Values)
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
            RangeObjs.rectangles.Add(rect);

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

            RangeObjs.infoObjects[rect.Name] = list;
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
            RangeObjs.controlGrids[rect.Name] = border;
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
                TPO_Profile(startIdx, i, ExtraProfiles.Fixed, fixedKey: rect.Name, fixedLowest: bottomY, fixedHighest: topY);
        }

        private void UpdateInfoBox(ChartRectangle rect)
        {
            if (!RangeObjs.infoObjects.TryGetValue(rect.Name, out var objs)) return;
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
            if (!RangeObjs.controlGrids.TryGetValue(rect.Name, out var grid)) return;
            double topY = Math.Max(rect.Y1, rect.Y2);
            DateTime rightTime = rect.Time1 > rect.Time2 ? rect.Time1 : rect.Time2;
            Chart.MoveControl(grid, rightTime, topY);
        }

        public void DeleteRectangle(ChartRectangle rect)
        {
            if (rect == null) return;
            Chart.RemoveObject(rect.Name);
            RangeObjs.rectangles.Remove(rect);

            // remove info objects
            if (RangeObjs.infoObjects.TryGetValue(rect.Name, out var objs))
            {
                foreach (var o in objs)
                    Chart.RemoveObject(o.Name);
                RangeObjs.infoObjects.Remove(rect.Name);
            }

            // remove control grid
            if (RangeObjs.controlGrids.TryGetValue(rect.Name, out var grid))
            {
                Chart.RemoveControl(grid);
                RangeObjs.controlGrids.Remove(rect.Name);
            }

            // remove histograms/lines drawings
            DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
            ResetFixedRange(rect.Name, end);
        }

        private void ResetFixedRange(string fixedKey, DateTime end)
        {
            FixedRank[fixedKey].TPO_Histogram.Clear();
            
            List<double> whichSegment;
            if (ProfileParams.SegmentsFixedRange_Input == SegmentsFixedRange_Data.Monthly_Aligned) {
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
                Chart.RemoveObject($"{fixedKey}_{i}_TPO_Fixed");
                
                Chart.RemoveObject($"{fixedKey}_LVN_Low_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_LVN_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_LVN_High_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_LVN_Band_{i}_Fixed");

                Chart.RemoveObject($"{fixedKey}_HVN_Low_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_HVN_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_HVN_High_{i}_Fixed");
                Chart.RemoveObject($"{fixedKey}_HVN_Band_{i}_Fixed");
            }

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
            RangeObjs.rectangles.Clear();
            RangeObjs.infoObjects.Clear();
            RangeObjs.controlGrids.Clear();
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
                tfName = ProfileParams.OffsetTimeframeInput.ShortName.ToString();

            // Get the time-based interval value
            string tfString = string.Join("", tfName.Where(char.IsDigit));
            int tfValue = int.TryParse(tfString, out int value) ? value : 1;

            DateTime dateToReturn = dateBar;
            int offsetCondiditon = !isSubt ? (ProfileParams.OffsetBarsInput + 1) : Math.Max(2, ProfileParams.OffsetBarsInput - 1);
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
                    string suffix = i switch {
                        4 => "ms",
                        3 => "s",
                        2 => "m",
                        1 => "h",
                        _ => "d"
                    };
                    timelapse_Value = suffix switch {
                        "ms" => ts.TotalMilliseconds,
                        "s" => ts.TotalSeconds,
                        "m" => ts.TotalMinutes,
                        "h" => ts.TotalHours,
                        _ => ts.TotalDays
                    };
                    timelapse_Suffix = suffix;
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
        private void Draw_VA_POC(Dictionary<double, double> tpoDict, int iStart, DateTime x1_Start, DateTime xBar, ExtraProfiles extraTPO = ExtraProfiles.No, bool isIntraday = false, DateTime intraX1 = default, string fixedKey = "")
        {
            string prefix = extraTPO == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";

            if (VAParams.ShowVA) {
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
                if (VAParams.ExtendVA) {
                    vah.Time2 = extDate;
                    val.Time2 = extDate;
                    rectVA.Time2 = extDate;
                }
                if (VAParams.ExtendPOC)
                    poc.Time2 = extDate;

                if (isIntraday && extraTPO != ExtraProfiles.MiniTPO) {
                    poc.Time1 = intraX1;
                    vah.Time1 = intraX1;
                    val.Time1 = intraX1;
                    rectVA.Time1 = intraX1;
                }
            }
            else if (!VAParams.ShowVA && VAParams.KeepPOC)
            {
                double largestVOL = Math.Abs(tpoDict.Values.Max());

                double priceLVOL = 0;
                foreach (var kv in tpoDict)
                {
                    if (Math.Abs(kv.Value) == largestVOL) { priceLVOL = kv.Key; break; }
                }
                ChartTrendLine poc = Chart.DrawTrendLine($"{prefix}_POC_{extraTPO}", x1_Start, priceLVOL - rowHeight, xBar, priceLVOL - rowHeight, ColorPOC);
                poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";

                if (VAParams.ExtendPOC)
                    poc.Time2 = extraTPO == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();

                if (isIntraday && extraTPO != ExtraProfiles.MiniTPO)
                    poc.Time1 = intraX1;
            }

            DateTime extendDate() {
                string tfName = extraTPO == ExtraProfiles.No ?
                (GeneralParams.TPOInterval_Input == TPOInterval_Data.Daily ? "D1" :
                    GeneralParams.TPOInterval_Input == TPOInterval_Data.Weekly ? "W1" : "Month1" ) :
                extraTPO == ExtraProfiles.MiniTPO ? ProfileParams.MiniTPOs_Timeframe.ShortName.ToString() :
                extraTPO == ExtraProfiles.Weekly ?  "W1" :  "Month1";

                // Get the time-based interval value
                string tfString = string.Join("", tfName.Where(char.IsDigit));
                int tfValue = int.TryParse(tfString, out int value) ? value : 1;

                DateTime dateToReturn = xBar;
                if (tfName.Contains('m'))
                    dateToReturn = xBar.AddMinutes(tfValue * VAParams.ExtendCount);
                else if (tfName.Contains('h'))
                    dateToReturn = xBar.AddHours(tfValue * VAParams.ExtendCount);
                else if (tfName.Contains('D'))
                    dateToReturn = xBar.AddDays(tfValue * VAParams.ExtendCount);
                else if (tfName.Contains('W'))
                    dateToReturn = xBar.AddDays(7 * VAParams.ExtendCount);
                else if (tfName.Contains("Month1"))
                    dateToReturn = xBar.AddMonths(tfValue * VAParams.ExtendCount);

                return dateToReturn;
            }
        }

        private double[] VA_Calculation(Dictionary<double, double> tpoDict)
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
            double _70percent = Math.Round((VAParams.PercentVA * totalvol) / 100);

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

        private double[] VA_Calculation_Letter(Dictionary<double, string> tpoDict)
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
            double _70percent = Math.Round((VAParams.PercentVA * totalvol) / 100);

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

        
        // *********** HVN + LVN ***********
        private void DrawVolumeNodes(Dictionary<double, double> profileDict, int iStart, DateTime x1_Start, DateTime xBar, ExtraProfiles extraTPO = ExtraProfiles.No, bool isIntraday = false, DateTime intraX1 = default, string fixedKey = "") 
        { 
            if (!NodesParams.EnableNodeDetection)
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
            // nodesKernel should be null (params-panel)
            nodesKernel ??= NodesParams.ProfileSmooth_Input == ProfileSmooth_Data.Gaussian ?
                            NodesAnalizer.FixedKernel() :
                            NodesAnalizer.FixedCoefficients();
            
            // Smooth values
            double[] profileSmoothed = NodesParams.ProfileSmooth_Input == ProfileSmooth_Data.Gaussian ?
                                       NodesAnalizer.GaussianSmooth(profileValues, nodesKernel) :
                                       NodesAnalizer.SavitzkyGolay(profileValues, nodesKernel);

            // Get indexes of LVNs/HVNs
            var (hvnsRaw, lvnsRaw) = NodesParams.ProfileNode_Input switch {
                ProfileNode_Data.LocalMinMax => NodesAnalizer.FindLocalMinMax(profileSmoothed),
                ProfileNode_Data.Topology => NodesAnalizer.ProfileTopology(profileSmoothed),
                _ => NodesAnalizer.PercentileNodes(profileSmoothed, NodesParams.pctileHVN_Value, NodesParams.pctileLVN_Value)
            };
            
            // Filter it
            if (NodesParams.onlyStrongNodes)
                (hvnsRaw, lvnsRaw) = NodesAnalizer.GetStrongNodes(profileSmoothed, hvnsRaw, lvnsRaw, NodesParams.strongHVN_Pct, NodesParams.strongLVN_Pct);

            bool isRaw = NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw || NodesParams.ShowNode_Input == ShowNode_Data.LVN_Raw;
            bool isBands = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands || NodesParams.ShowNode_Input == ShowNode_Data.LVN_With_Bands;
                        
            if (NodesParams.ProfileNode_Input == ProfileNode_Data.Percentile) 
            {
                ClearOldNodes();                                               
                
                if (isBands)
                {
                    Color _nodeColor = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? ColorHVN : ColorLVN;

                    var hvnsGroups = NodesAnalizer.GroupConsecutiveIndexes(hvnsRaw);
                    var lvnsGroups = NodesAnalizer.GroupConsecutiveIndexes(lvnsRaw);
                    List<List<int>> nodeGroups = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? hvnsGroups : lvnsGroups;
                    
                    string nodeName = NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw ? "HVN" : "LVN";   
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
                   
            // Get Bands
            var (hvnLevels, hvnIndexes, lvnLevels, lvnIndexes) = NodesAnalizer.
            GetBandsTuples(profileSmoothed, profilePrices, lvnsRaw, NodesParams.bandHVN_Pct, NodesParams.bandLVN_Pct);

            // Let's draw
            ClearOldNodes();

            string node = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? "HVN" : "LVN";
            Color nodeColor = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? ColorHVN : ColorLVN;
            
            var nodeLvls = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? hvnLevels : lvnLevels;
            var nodeIdxes = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? hvnIndexes : lvnIndexes;
            
            for (int i = 0; i < nodeLvls.Count; i++)
            {
                var (lvlLow, lvlCenter, lvlHigh) = nodeLvls[i];
                var (idxLow, idxCenter, idxHigh) = nodeIdxes[i];

                ChartTrendLine low = Chart.DrawTrendLine($"{prefix}_{node}_Low_{idxLow}_{extraTPO}", x1_Start, lvlLow, xBar, lvlLow, ColorBand_Lower);
                ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{node}_{idxCenter}_{extraTPO}", x1_Start, lvlCenter, xBar, lvlCenter, nodeColor);
                ChartTrendLine high = Chart.DrawTrendLine($"{prefix}_{node}_High_{idxHigh}_{extraTPO}", x1_Start, lvlHigh, xBar, lvlHigh, ColorBand_Upper);
                ChartRectangle rectBand = Chart.DrawRectangle($"{prefix}_{node}_Band_{idxCenter}_{extraTPO}", x1_Start, lvlLow, xBar, lvlHigh, ColorBand);

                FinalizeBands(low, center, high, rectBand);
            }
            
            // Local
            void FinalizeBands(ChartTrendLine low, ChartTrendLine center, ChartTrendLine high, ChartRectangle rectBand) 
            {
                LineStyle nodeStyle = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? LineStyleHVN : LineStyleLVN;
                int  nodeThick = NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ? ThicknessHVN : ThicknessLVN;
            
                rectBand.IsFilled = true; 
                
                low.LineStyle = LineStyleBands; high.Thickness = ThicknessBands;
                center.LineStyle = nodeStyle; center.Thickness = nodeThick;
                high.LineStyle = LineStyleBands; high.Thickness = ThicknessBands;

                DateTime extDate = extraTPO == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
                if (NodesParams.extendNodes) 
                {
                    if (!NodesParams.extendNodes_FromStart) {
                        low.Time1 = xBar;
                        center.Time1 = xBar;
                        high.Time1 = xBar;
                        rectBand.Time1 = xBar;
                    }
                    
                    center.Time2 = extDate;
                    if (NodesParams.extendNodes_WithBands) {
                        low.Time2 = extDate;
                        high.Time2 = extDate;
                        rectBand.Time2 = extDate;
                    }
                }
                
                if (isIntraday && extraTPO != ExtraProfiles.MiniTPO) {
                    low.Time1 = intraX1;
                    center.Time1 = intraX1;
                    high.Time1 = intraX1;
                    rectBand.Time1 = intraX1;
                }
            }
            void DrawRawNodes() 
            {
                string nodeRaw = NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw ? "HVN" : "LVN";
                List<int> nodeIndexes = NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw ? hvnsRaw : lvnsRaw;
                
                LineStyle nodeStyle_Raw = NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw ? LineStyleHVN : LineStyleLVN;
                int  nodeThick_Raw = NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw ? ThicknessHVN : ThicknessLVN;
                Color nodeColor_Raw = NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw ? ColorHVN : ColorLVN;

                foreach (int idx in nodeIndexes) 
                {
                    double nodePrice = profilePrices[idx];
                    ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{nodeRaw}_{idx}_{extraTPO}", x1_Start, nodePrice, xBar, nodePrice, nodeColor_Raw);
                    center.LineStyle = nodeStyle_Raw; center.Thickness = nodeThick_Raw;
                                        
                    DateTime extDate = extraTPO == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
                    if (NodesParams.extendNodes) {
                        if (!NodesParams.extendNodes_FromStart)
                            center.Time1 = xBar;
                        center.Time2 = extDate;
                    }
                    
                    if (isIntraday && extraTPO != ExtraProfiles.MiniTPO)
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
                (GeneralParams.TPOInterval_Input == TPOInterval_Data.Daily ? "D1" :
                    GeneralParams.TPOInterval_Input == TPOInterval_Data.Weekly ? "W1" : "Month1" ) :
                extraTPO == ExtraProfiles.MiniTPO ? ProfileParams.MiniTPOs_Timeframe.ShortName.ToString() :
                extraTPO == ExtraProfiles.Weekly ?  "W1" :  "Month1";

                // Get the time-based interval value
                string tfString = string.Join("", tfName.Where(char.IsDigit));
                int tfValue = int.TryParse(tfString, out int value) ? value : 1;

                DateTime dateToReturn = xBar;
                if (tfName.Contains('m'))
                    dateToReturn = xBar.AddMinutes(tfValue * NodesParams.extendNodes_Count);
                else if (tfName.Contains('h'))
                    dateToReturn = xBar.AddHours(tfValue * NodesParams.extendNodes_Count);
                else if (tfName.Contains('D'))
                    dateToReturn = xBar.AddDays(tfValue * NodesParams.extendNodes_Count);
                else if (tfName.Contains('W'))
                    dateToReturn = xBar.AddDays(7 * NodesParams.extendNodes_Count);
                else if (tfName.Contains("Month1"))
                    dateToReturn = xBar.AddMonths(tfValue * NodesParams.extendNodes_Count);

                return dateToReturn;
            }            
        }

        // ========= ========== ==========

        public void ClearAndRecalculate()
        {
            Thread.Sleep(300);

            // LookBack from TPO
            Bars tpoBars = GeneralParams.TPOInterval_Input == TPOInterval_Data.Daily ? DailyBars :
                           GeneralParams.TPOInterval_Input == TPOInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            int firstIndex = Bars.OpenTimes.GetIndexByTime(tpoBars.OpenTimes.FirstOrDefault());

            // Get index of TPO Interval to continue only in Lookback
            int iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            while (tpoBars.ClosePrices.Count - iVerify > GeneralParams.Lookback) {
                firstIndex++;
                iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            }

            // Daily or Weekly TPO
            int TF_idx = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            int startIndex = Bars.OpenTimes.GetIndexByTime(tpoBars.OpenTimes[TF_idx]);

            // Weekly Profile but Daily TPO
            bool extraWeekly = ProfileParams.EnableWeeklyProfile && GeneralParams.TPOInterval_Input == TPOInterval_Data.Daily;
            if (extraWeekly) {
                TF_idx = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                startIndex = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[TF_idx]);
            }

            // Monthly Profile
            bool extraMonthly = ProfileParams.EnableMonthlyProfile;
            if (extraMonthly) {
                TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                startIndex = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[TF_idx]);
            }

            // Reset last update
            ClearIdx.ResetAll();
            // Reset Segments
            Segments.Clear();
            segmentInfo.Clear();
            // Reset Fixed Range
            foreach (ChartRectangle rect in RangeObjs.rectangles)
            {
                DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
                ResetFixedRange(rect.Name, end);
            }

            // Reset Fixed Range
            foreach (ChartRectangle rect in RangeObjs.rectangles)
            {
                DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
                ResetFixedRange(rect.Name, end);
            }

            // Historical data
            for (int index = startIndex; index < Bars.Count; index++)
            {
                CreateSegments(index);

                CreateMonthlyTPO(index);
                CreateWeeklyTPO(index);

                // Calculate TPO only in lookback
                if (extraWeekly || extraMonthly) {
                    iVerify = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    if (tpoBars.ClosePrices.Count - iVerify > GeneralParams.Lookback)
                        continue;
                }

                TF_idx = tpoBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                startIndex = Bars.OpenTimes.GetIndexByTime(tpoBars.OpenTimes[TF_idx]);

                if (index == startIndex ||
                   (index - 1) == startIndex && isPriceBased_Chart ||
                   (index - 1) == startIndex && (index - 1) != ClearIdx.MainTPO)
                    CleanUp_MainTPO(startIndex, index);

                if (ProfileParams.EnableMainTPO) 
                    TPO_Profile(startIndex, index);
                
                CreateMiniTPOs(index);
            }

            configHasChanged = true;
        }

        public void SetRowHeight(double number)
        {
            rowHeight = number;
        }
        public void SetLookback(int number)
        {
            GeneralParams.Lookback = number;
        }
        public int GetLookback()
        {
            return GeneralParams.Lookback;
        }
        public double GetRowHeight()
        {
            return rowHeight;
        }

        public void SetMiniTPOsBars() {
            MiniTPOs_Bars = MarketData.GetBars(ProfileParams.MiniTPOs_Timeframe);
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
            bool isNodeBand() => (
                Outside.NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands ||
                Outside.NodesParams.ShowNode_Input == ShowNode_Data.LVN_With_Bands
            ) && Outside.NodesParams.ProfileNode_Input != ProfileNode_Data.Percentile;
            bool isStrongHVN() => (
                Outside.NodesParams.ShowNode_Input == ShowNode_Data.HVN_Raw ||
                Outside.NodesParams.ProfileNode_Input == ProfileNode_Data.Percentile && Outside.NodesParams.ShowNode_Input == ShowNode_Data.HVN_With_Bands
            );
            bool isStrongLVN() => (
                Outside.NodesParams.ShowNode_Input != ShowNode_Data.HVN_Raw && Outside.NodesParams.ProfileNode_Input != ProfileNode_Data.Percentile ||
                Outside.NodesParams.ProfileNode_Input == ProfileNode_Data.Percentile &&
                (Outside.NodesParams.ShowNode_Input == ShowNode_Data.LVN_With_Bands || Outside.NodesParams.ShowNode_Input == ShowNode_Data.LVN_Raw)
            );

            return new List<ParamDefinition>
            {
                new()
                {
                    Region = "General",
                    RegionOrder = 1,
                    Key = "LookbackKey",
                    Label = "Lookback",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.GeneralParams.Lookback,
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
                    GetDefault = p => p.GeneralParams.TPOInterval_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(TPOInterval_Data)),
                    OnChanged = _ => UpdateTPOInterval(),
                },

                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "EnableTPOKey",
                    Label = "Main Profile?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableMainTPO,
                    OnChanged = _ => UpdateCheckbox("EnableTPOKey", val => Outside.ProfileParams.EnableMainTPO = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "WeeklyTPOKey",
                    Label = "Weekly?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableWeeklyProfile,
                    OnChanged = _ => UpdateCheckbox("WeeklyTPOKey", val => Outside.ProfileParams.EnableWeeklyProfile = val),
                    IsVisible = () => Outside.GeneralParams.TPOInterval_Input != TPOInterval_Data.Weekly
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "MonthlyTPOKey",
                    Label = "Monthly?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableMonthlyProfile,
                    OnChanged = _ => UpdateCheckbox("MonthlyTPOKey", val => Outside.ProfileParams.EnableMonthlyProfile = val),
                    IsVisible = () => Outside.GeneralParams.TPOInterval_Input != TPOInterval_Data.Monthly
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "FillTPOKey",
                    Label = "Fill Histogram?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.FillHist_TPO,
                    OnChanged = _ => UpdateCheckbox("FillTPOKey", val => Outside.ProfileParams.FillHist_TPO = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "SideTPOKey",
                    Label = "Side",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.HistogramSide_Input.ToString(),
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
                    GetDefault = p => p.ProfileParams.HistogramWidth_Input.ToString(),
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
                    GetDefault = p => p.ProfileParams.ShowIntradayProfile,
                    OnChanged = _ => UpdateCheckbox("IntradayTPOKey", val => Outside.ProfileParams.ShowIntradayProfile = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "IntraOffsetKey",
                    Label = "Offset(bars)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.ProfileParams.OffsetBarsInput.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateIntradayOffset(),
                    IsVisible = () => Outside.ProfileParams.ShowIntradayProfile
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "IntraTFKey",
                    Label = "Offset(time)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.OffsetTimeframeInput.ShortName,
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateIntradayTimeframe(),
                    IsVisible = () => Outside.ProfileParams.ShowIntradayProfile && Outside.isPriceBased_Chart
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "FixedRangeKey",
                    Label = "Fixed Range?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableFixedRange,
                    OnChanged = _ => UpdateCheckbox("FixedRangeKey", val => Outside.ProfileParams.EnableFixedRange = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "FixedSegmentsKey",
                    Label = "Segments",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.SegmentsFixedRange_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SegmentsFixedRange_Data)),
                    OnChanged = _ => UpdateRangeSegments(),
                    IsVisible = () => Outside.ProfileParams.EnableFixedRange
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "ShowOHLCKey",
                    Label = "OHLC Body?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.ShowOHLC,
                    OnChanged = _ => UpdateCheckbox("ShowOHLCKey", val => Outside.ProfileParams.ShowOHLC = val),
                },
                new()
                {
                    Region = "TPO Profile",
                    RegionOrder = 2,
                    Key = "FillIntraTPOKey",
                    Label = "Intra-Space?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.FillIntradaySpace,
                    OnChanged = _ => UpdateCheckbox("FillIntraTPOKey", val => Outside.ProfileParams.FillIntradaySpace = val),
                    IsVisible = () => Outside.ProfileParams.ShowIntradayProfile && (Outside.ProfileParams.EnableWeeklyProfile || Outside.ProfileParams.EnableMonthlyProfile)
                },

                new()
                {
                    Region = "Mini TPOs",
                    RegionOrder = 3,
                    Key = "MiniTPOsKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableMiniProfiles,
                    OnChanged = _ => UpdateCheckbox("MiniTPOsKey", val => Outside.ProfileParams.EnableMiniProfiles = val)
                },
                new()
                {
                    Region = "Mini TPOs",
                    RegionOrder = 3,
                    Key = "MiniTFKey",
                    Label = "Mini-Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.MiniTPOs_Timeframe.ShortName.ToString(),
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
                    GetDefault = p => p.ProfileParams.ShowMiniResults,
                    OnChanged = _ => UpdateCheckbox("MiniResultKey", val => Outside.ProfileParams.ShowMiniResults = val)
                },

                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "EnableVAKey",
                    Label = "Enable VA?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.VAParams.ShowVA,
                    OnChanged = _ => UpdateCheckbox("EnableVAKey", val => Outside.VAParams.ShowVA = val)
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "VAValueKey",
                    Label = "VA(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.VAParams.PercentVA.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdatePercentVA(),
                    IsVisible = () => Outside.VAParams.ShowVA
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "OnlyPOCKey",
                    Label = "Only POC?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.VAParams.KeepPOC,
                    OnChanged = _ => UpdateCheckbox("OnlyPOCKey", val => Outside.VAParams.KeepPOC = val)
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "ExtendVAKey",
                    Label = "Extend VA?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.VAParams.ExtendVA,
                    OnChanged = _ => UpdateCheckbox("ExtendVAKey", val => Outside.VAParams.ExtendVA = val)
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "ExtendCountKey",
                    Label = "Extend(count)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.VAParams.ExtendCount.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateExtendCount(),
                    IsVisible = () => Outside.VAParams.ExtendVA || Outside.VAParams.ExtendPOC
                },
                new()
                {
                    Region = "VA + POC",
                    RegionOrder = 3,
                    Key = "ExtendPOCKey",
                    Label = "Extend POC?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.VAParams.ExtendPOC,
                    OnChanged = _ => UpdateCheckbox("ExtendPOCKey", val => Outside.VAParams.ExtendPOC = val)
                },

                
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "EnableNodeKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.EnableNodeDetection,
                    OnChanged = _ => UpdateCheckbox("EnableNodeKey", val => Outside.NodesParams.EnableNodeDetection = val)
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "NodeSmoothKey",
                    Label = "Smooth",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.NodesParams.ProfileSmooth_Input.ToString(),
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
                    GetDefault = p => p.NodesParams.ProfileNode_Input.ToString(),
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
                    GetDefault = p => p.NodesParams.ShowNode_Input.ToString(),
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
                    GetDefault = p => p.NodesParams.bandHVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Band(),
                    IsVisible = () => isNodeBand()
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "LvnBandPctKey",
                    Label = "LVN Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.bandLVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Band(),
                    IsVisible = () => isNodeBand()
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "NodeStrongKey",
                    Label = "Only Strong?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.onlyStrongNodes, 
                    OnChanged = _ => UpdateCheckbox("NodeStrongKey", val => Outside.NodesParams.onlyStrongNodes = val)
                },
                // 'Strong HVN' for HVN_Raw(only) on [LocalMinMax, Topology]
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "StrongHvnPctKey",
                    Label = "(%) >= POC",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.strongHVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Strong(),
                    IsVisible = () => Outside.NodesParams.onlyStrongNodes && isStrongHVN()
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
                    GetDefault = p => p.NodesParams.strongLVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Strong(),
                    IsVisible = () => Outside.NodesParams.onlyStrongNodes && isStrongLVN()
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtendNodeKey",
                    Label = "Extend?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.extendNodes,
                    OnChanged = _ => UpdateCheckbox("ExtendNodeKey", val => Outside.NodesParams.extendNodes = val)
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtNodesCountKey",
                    Label = "Extend(count)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.extendNodes_Count.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateExtendNodesCount(),
                    IsVisible = () => Outside.NodesParams.extendNodes
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtBandsKey",
                    Label = "Ext.(bands)?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.extendNodes_WithBands,
                    OnChanged = _ => UpdateCheckbox("ExtBandsKey", val => Outside.NodesParams.extendNodes_WithBands = val),
                    IsVisible = () => Outside.NodesParams.extendNodes
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "HvnPctileKey",
                    Label = "HVN(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.pctileHVN_Value.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Pctile(),
                    IsVisible = () => Outside.NodesParams.ProfileNode_Input == ProfileNode_Data.Percentile
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "LvnPctileKey",
                    Label = "LVN(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.pctileLVN_Value.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Pctile(),
                    IsVisible = () => Outside.NodesParams.ProfileNode_Input == ProfileNode_Data.Percentile
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 4,
                    Key = "ExtNodeStartKey",
                    Label = "From start?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.extendNodes_FromStart,
                    OnChanged = _ => UpdateCheckbox("ExtNodeStartKey", val => Outside.NodesParams.extendNodes_FromStart = val),
                    IsVisible = () => Outside.NodesParams.extendNodes
                },

                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "UpdateTPOKey",
                    Label = "Update At",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.UpdateProfile_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UpdateProfile_Data)),
                    OnChanged = _ => UpdateTPO(),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 5,
                    Key = "ShowResultsKey",
                    Label = "Results?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.ShowResults,
                    OnChanged = _ => UpdateCheckbox("ShowResultsKey", val => Outside.ProfileParams.ShowResults = val),
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
                Width = 250, // ParamsPanel Width
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
                Width = 250, // ParamsPanel Width
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
            grid.AddChild(CreateModeInfo_Button(FirstParams.GeneralParams.TPOMode_Input.ToString()), 0, 1, 1, 3);
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
                    RefreshVisibility();
                    return;
                case "NodeStrongKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "ExtendNodeKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "ExtBandsKey":
                    RecalculateOutsideWithMsg(false);
                    return;
                case "ExtNodeStartKey":
                    RecalculateOutsideWithMsg(false);
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
            if (Enum.TryParse(selected, out TPOInterval_Data intervalType) && intervalType != Outside.GeneralParams.TPOInterval_Input)
            {
                Outside.GeneralParams.TPOInterval_Input = intervalType;
                RecalculateOutsideWithMsg();
            }
        }

        // ==== TPO Profile ====
        private void UpdateSideTPO()
        {
            var selected = comboBoxMap["SideTPOKey"].SelectedItem;
            if (Enum.TryParse(selected, out HistSide_Data sideType) && sideType != Outside.ProfileParams.HistogramSide_Input)
            {
                Outside.ProfileParams.HistogramSide_Input = sideType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateWidthTPO()
        {
            var selected = comboBoxMap["WidthTPOKey"].SelectedItem;
            if (Enum.TryParse(selected, out HistWidth_Data widthType) && widthType != Outside.ProfileParams.HistogramWidth_Input)
            {
                Outside.ProfileParams.HistogramWidth_Input = widthType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateIntradayOffset()
        {
            int value = int.TryParse(textInputMap["IntraOffsetKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.ProfileParams.OffsetBarsInput)
            {
                Outside.ProfileParams.OffsetBarsInput = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateIntradayTimeframe()
        {
            var selected = comboBoxMap["IntraTFKey"].SelectedItem;
            TimeFrame value = StringToTimeframe(selected);
            if (value != TimeFrame.Minute && value != Outside.ProfileParams.OffsetTimeframeInput)
            {
                Outside.ProfileParams.OffsetTimeframeInput = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateRangeSegments() {
            var selected = comboBoxMap["FixedSegmentsKey"].SelectedItem;
            if (Enum.TryParse(selected, out SegmentsFixedRange_Data segmentsType) && segmentsType != Outside.ProfileParams.SegmentsFixedRange_Input)
            {
                Outside.ProfileParams.SegmentsFixedRange_Input = segmentsType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateMiniTPOTimeframe()
        {
            var selected = comboBoxMap["MiniTFKey"].SelectedItem;
            TimeFrame value = StringToTimeframe(selected);
            if (value != TimeFrame.Minute && value != Outside.ProfileParams.MiniTPOs_Timeframe)
            {
                Outside.ProfileParams.MiniTPOs_Timeframe = value;
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
            if (value > 0 && value <= 100 && value != Outside.VAParams.PercentVA)
            {
                Outside.VAParams.PercentVA = value;
                SetApplyVisibility();
            }
        }
        private void UpdateExtendCount()
        {
            int value = int.TryParse(textInputMap["ExtendCountKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.VAParams.ExtendCount)
            {
                Outside.VAParams.ExtendCount = value;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== HVN + LVN ====
        private void UpdateNodeSmooth()
        {
            var selected = comboBoxMap["NodeSmoothKey"].SelectedItem;
            if (Enum.TryParse(selected, out ProfileSmooth_Data smoothType) && smoothType != Outside.NodesParams.ProfileSmooth_Input)
            {
                Outside.NodesParams.ProfileSmooth_Input = smoothType;
                Outside.nodesKernel = null;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateNodeType()
        {
            var selected = comboBoxMap["NodeTypeKey"].SelectedItem;
            if (Enum.TryParse(selected, out ProfileNode_Data nodeType) && nodeType != Outside.NodesParams.ProfileNode_Input)
            {
                Outside.NodesParams.ProfileNode_Input = nodeType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateShowNode()
        {
            var selected = comboBoxMap["ShowNodeKey"].SelectedItem;
            if (Enum.TryParse(selected, out ShowNode_Data showNodeType) && showNodeType != Outside.NodesParams.ShowNode_Input)
            {
                Outside.NodesParams.ShowNode_Input = showNodeType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateHVN_Band()
        {
            if (double.TryParse(textInputMap["HvnBandPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.NodesParams.bandHVN_Pct)
                {
                    Outside.NodesParams.bandHVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateLVN_Band()
        {
            if (double.TryParse(textInputMap["LvnBandPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.NodesParams.bandLVN_Pct)
                {
                    Outside.NodesParams.bandLVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateHVN_Strong()
        {
            if (double.TryParse(textInputMap["StrongHvnPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.NodesParams.strongHVN_Pct)
                {
                    Outside.NodesParams.strongHVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateLVN_Strong()
        {
            if (double.TryParse(textInputMap["StrongLvnPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0.9)
            {
                if (value != Outside.NodesParams.strongLVN_Pct)
                {
                    Outside.NodesParams.strongLVN_Pct = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateExtendNodesCount() 
        {
            int value = int.TryParse(textInputMap["ExtNodesCountKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.NodesParams.extendNodes_Count)
            {
                Outside.NodesParams.extendNodes_Count = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateHVN_Pctile()
        {
            int value = int.TryParse(textInputMap["HvnPctileKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.NodesParams.pctileHVN_Value)
            {
                Outside.NodesParams.pctileHVN_Value = value;
                SetApplyVisibility();
            }
        }
        private void UpdateLVN_Pctile()
        {
            int value = int.TryParse(textInputMap["LvnPctileKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.NodesParams.pctileLVN_Value)
            {
                Outside.NodesParams.pctileLVN_Value = value;
                SetApplyVisibility();
            }
        }


        // ==== Misc ====
        private void UpdateTPO()
        {
            var selected = comboBoxMap["UpdateTPOKey"].SelectedItem;
            if (Enum.TryParse(selected, out UpdateProfile_Data updateType) && updateType != Outside.ProfileParams.UpdateProfile_Input)
            {
                Outside.ProfileParams.UpdateProfile_Input = updateType;
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

            Outside.GeneralParams.TPOMode_Input = Outside.GeneralParams.TPOMode_Input switch
            {
                TPOMode_Data.Aggregated => TPOMode_Data.Aggregated,
                _ => TPOMode_Data.Aggregated
            };
            ModeBtn.Text = Outside.GeneralParams.TPOMode_Input.ToString();
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

            Outside.GeneralParams.TPOMode_Input = Outside.GeneralParams.TPOMode_Input switch
            {
                TPOMode_Data.Aggregated => TPOMode_Data.Aggregated,
                _ => TPOMode_Data.Aggregated
            };
            ModeBtn.Text = Outside.GeneralParams.TPOMode_Input.ToString();
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
            RangeBtn.IsVisible = Outside.ProfileParams.EnableFixedRange;
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
            storageModel.Params["PanelMode"] = Outside.GeneralParams.TPOMode_Input;

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
            Outside.GeneralParams.TPOMode_Input = tpoMode;
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
                    Width = 250, // ParamsPanel Width
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

        // === can/should be static ===
        public static (List<int> strongHvnIdxs, List<int> stronglvnIdxs) GetStrongNodes(double[] profileSmoothed, List<int> hvnsRaw, List<int> lvnsRaw, double hvnPct, double lvnPct) {
            double globalPoc = profileSmoothed.Max();

            double decimalHvnPct = Math.Round(hvnPct / 100.0, 3);
            double decimalLvnPct = Math.Round(lvnPct / 100.0, 3);

            var strongHvns = new List<int>();
            var strongLvns = new List<int>();

            foreach (int idx in hvnsRaw)
            {
                if (profileSmoothed[idx] >= decimalHvnPct * globalPoc)
                    strongHvns.Add(idx);
            }

            foreach (int idx in lvnsRaw)
            {
                if (profileSmoothed[idx] <= decimalLvnPct * globalPoc)
                    strongLvns.Add(idx);
            }

            return (strongHvns, strongLvns);
        }

        public static List<(int Start, int End, int Poc)> GetBells(double[] profileSmoothed, List<int> lvnsRaw)
        {
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

            return bells;
        }

        public static (List<(double Low, double Center, double High)> hvnLvls, List<(int Low, int Center, int High)> hvnIdxs,
                       List<(double Low, double Center, double High)> lvnLvls, List<(int Low, int Center, int High)> lvnIdxs)
                       GetBandsTuples(double[] profileSmoothed, double[] profilePrices, List<int> lvnsRaw, double hvnPct, double lvnPct)
        {
            // Extract mini-bells
            var bells = GetBells(profileSmoothed, lvnsRaw);

            // Extract HVN/LVN/POC + Levels
            // [(low, center, high), ...]
            var hvnLevels = new List<(double Low, double Center, double High)>();
            var hvnIndexes = new List<(int Low, int Center, int High)>();

            var lvnLevels = new List<(double Low, double Center, double High)>();
            var lvnIndexes = new List<(int Low, int Center, int High)>();

            double hvnBandPct = Math.Round(hvnPct / 100.0, 3);
            double lvnBandPct = Math.Round(lvnPct / 100.0, 3);

            foreach (var (startIdx, endIdx, pocIdx) in bells)
            {
                // HVNs/POCs + levels
                var (hvnLow, hvnHigh) = HVN_SymmetricVA(startIdx, endIdx, pocIdx, hvnBandPct);

                hvnLevels.Add( (profilePrices[hvnLow], profilePrices[pocIdx], profilePrices[hvnHigh]) );
                hvnIndexes.Add( (hvnLow, pocIdx, hvnHigh) );

                // LVNs + Levels
                var (lvnLow, lvnHigh) = LVN_SymmetricBand( startIdx, endIdx, lvnBandPct);

                lvnIndexes.Add( (lvnLow, startIdx, lvnHigh) );
                lvnLevels.Add( (profilePrices[lvnLow], profilePrices[startIdx], profilePrices[lvnHigh]) );
            }

            return (hvnLevels, hvnIndexes, lvnLevels, lvnIndexes);
        }
    }
}
