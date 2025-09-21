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

Last update => 21/09/2025

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


        public enum UpdateVPStrategy_Data
        {
            Concurrent,
            SameThread_MayFreeze
        }
        [Parameter("[VP] Update Strategy", DefaultValue = UpdateVPStrategy_Data.Concurrent, Group = "==== Specific Parameters ====")]
        public UpdateVPStrategy_Data UpdateVPStrategy_Input { get; set; }

        public enum LoadBarsStrategy_Data
        {
            Sync,
            Async
        }
        [Parameter("[Source] Load Type:", DefaultValue = LoadBarsStrategy_Data.Async, Group = "==== Specific Parameters ====")]
        public LoadBarsStrategy_Data LoadBarsStrategy_Input { get; set; }

        [Parameter("[Gradient] Opacity:", DefaultValue = 60, MinValue = 5, MaxValue = 100, Group = "==== Specific Parameters ====")]
        public int OpacityHistInput { get; set; }


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


        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }

        // Moved from cTrader Input to Params Panel
        public int Lookback { get; set; } = 2;
        public enum VolumeMode_Data
        {
            Normal,
            Buy_Sell,
            Delta,
            Normal_Delta
        }
        public VolumeMode_Data VolumeMode_Input { get; set; } = VolumeMode_Data.Normal;

        public enum VPInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }
        public VPInterval_Data VPInterval_Input { get; set; } = VPInterval_Data.Daily;

        // ==== Volume Profile ====
        public bool EnableVP { get; set; } = false;

        public enum UpdateProfile_Data
        {
            EveryTick_CPU_Workout,
            ThroughSegments_Balanced,
            Through_2_Segments_Best,
        }
        public UpdateProfile_Data UpdateProfile_Input { get; set; } = UpdateProfile_Data.Through_2_Segments_Best;
        public bool FillHist_VP { get; set; } = true;

        public enum HistSide_Data
        {
            Left,
            Right,
        }
        public HistSide_Data HistogramSide_Input { get; set; } = HistSide_Data.Left;

        public enum HistWidth_Data
        {
            _15,
            _30,
            _50,
            _70,
            _100
        }
        public HistWidth_Data HistogramWidth_Input { get; set; } = HistWidth_Data._70;

        public bool EnableWeeklyProfile { get; set; } = false;
        public bool EnableMonthlyProfile { get; set; } = false;
        public bool EnableGradient { get; set; } = true;

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
        public Distribution_Data Distribution_Input { get; set; } = Distribution_Data.OHLC_No_Avg;
        public bool ShowOHLC { get; set; } = false;

        // ==== Intraday Profiles ====
        public bool ShowIntradayProfile { get; set; } = false;
        public int OffsetBarsInput { get; set; } = 2;
        public TimeFrame OffsetTimeframeInput { get; set; } = TimeFrame.Hour;
        public bool FillIntradaySpace { get; set; }


        // ==== Mini VPs ====
        public bool EnableMiniProfiles { get; set; } = true;
        public TimeFrame MiniVPs_Timeframe { get; set; } = TimeFrame.Hour4;
        public bool ShowMiniResults { get; set; } = true;


        // ==== VA + POC ====
        public bool ShowVA { get; set; } = false;
        public int PercentVA { get; set; } = 65;
        public bool KeepPOC { get; set; } = true;
        public bool ExtendPOC { get; set; } = false;
        public bool ExtendVA { get; set; } = false;
        public int ExtendCount { get; set; } = 1;

        // ==== Results ====
        public bool ShowResults { get; set; } = true;

        public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }
        public OperatorBuySell_Data OperatorBuySell_Input { get; set; } = OperatorBuySell_Data.Subtraction;

        public bool ShowMinMaxDelta { get; set; } = false;
        public bool ShowOnlySubtDelta { get; set; } = true;

        public TimeFrame VOL_Timeframe { get; set; } = TimeFrame.Minute;
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
        private readonly VolumeRankType miniVPsRank = new();

        private Bars MiniVPs_Bars;
        private Bars DailyBars;
        private Bars WeeklyBars;
        private Bars MonthlyBars;

        public enum ExtraProfiles {
            No,
            MiniVP,
            Weekly,
            Monthly,
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
        private List<Bar> Bars_List = new();
        private DateTime[] BarTimes_Array = Array.Empty<DateTime>();

        // High-Performance VP_Bars()
        public class LastBarIndex {
            public int _Mini = 0;
            public int _MiniStart = 0;
            public int _Weekly = 0;
            public int _WeeklyStart = 0;
            public int _Monthly = 0;
            public int _MonthlyStart = 0;
        }
        private readonly LastBarIndex lastBar_ExtraVPs = new();

        private int lastBar_VP = 0;
        private int lastBar_VPStart = 0;
        private Bars VOL_Bars;

        // Shared rowHeight
        private double heightPips = 4;
        private double rowHeight = 0;
        public double heightATR = 4;

        // Some required utils
        private bool configHasChanged = false;
        private bool isUpdateVP = false;
        private bool isEndChart = false;
        private double prevUpdatePrice;
        public bool isPriceBased_Chart = false;
        public bool isRenkoChart = false;

        // Timer
        private class TimerHandler {
            public bool isAsyncLoading = false;
        }
        private readonly TimerHandler timerHandler = new();

        PopupNotification asyncBarsPopup = null;
        private bool loadingAsyncBars = false;
        private bool loadingBarsComplete = false;

        // Params Panel
        private Border ParamBorder;
        public class IndicatorParams
        {
            public int LookBack { get; set; }
            public VolumeMode_Data VolMode { get; set; }
            public double RowHeightInPips { get; set; }
            public VPInterval_Data VPInterval { get; set; }

            // Volume Profile
            public bool EnableVP { get; set; }
            public bool EnableWeeklyProfile { get; set; }
            public bool EnableMonthlyProfile { get; set; }
            // View
            public bool FillHist_VP { get; set; }
            public HistSide_Data HistogramSide { get; set; }
            public HistWidth_Data HistogramWidth { get; set; }
            public bool Gradient { get; set; }
            public bool OHLC { get; set; }
            // Intraday View
            public bool ShowIntradayProfile { get; set; }
            public bool FillIntradaySpace { get; set; }
            public int OffsetBarsIntraday { get; set; }
            public TimeFrame OffsetTimeframeIntraday { get; set; }
            // Mini VPs
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

            // Results
            public bool ShowResults { get; set; }
            // Results - Buy_Sell / Delta
            public bool ShowSideTotal { get; set; }
            public OperatorBuySell_Data OperatorBuySell { get; set; }
            // Results - Delta
            public bool ShowMinMax { get; set; }
            public bool ShowOnlySubtDelta { get; set; }

            // Misc
            public UpdateProfile_Data UpdateProfileStrategy { get; set; }
            public TimeFrame Source { get; set; }
            public Distribution_Data Distribution { get; set; }
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

            Bars.BarOpened += (_) => {
                isUpdateVP = true;
                if (UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                    prevUpdatePrice = _.Bars.LastBar.Close;
            };

            string currentTimeframe = Chart.TimeFrame.ToString();
            isPriceBased_Chart = currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick");
            isRenkoChart = Chart.TimeFrame.ToString().Contains("Renko");

            DrawStartVolumeLine();

            DrawOnScreen("Calculating...");
            Second_DrawOnScreen($"Taking too long? You can: \n 1) Increase the rowHeight \n 2) Disable the Value Area (High Performance)");

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

                // Results
                ShowResults = ShowResults,
                OperatorBuySell = OperatorBuySell_Input,
                ShowMinMax = ShowMinMaxDelta,
                ShowOnlySubtDelta = ShowOnlySubtDelta,

                UpdateProfileStrategy = UpdateProfile_Input,
                Source = VOL_Timeframe,
                Distribution = Distribution_Input
            };

            ParamsPanel ParamPanel = new(this, DefaultParams);

            Border borderParam = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                // ParamsPanel - Lock Width
                Width = 255,
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

        public override void Calculate(int index)
        {
            // ==== Removing Messages ====
            if (!IsLastBar) {
                DrawOnScreen(""); Second_DrawOnScreen("");
            }

            // ==== Chart Segmentation ====
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
            int indexStart = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes[TF_idx]);

            // === Clean Dicts/others ===
            if (index == indexStart ||
                (index - 1) == indexStart && isPriceBased_Chart ||
                (index - 1) == indexStart && (index - 1) != lastCleaned._VP_Interval
            )
                CleanUp_MainVP(index, indexStart);

            // Historical data
            if (!IsLastBar)
            {
                // Allows MiniVPs if (!EnableVP)
                CreateMiniVPs(index);

                if (EnableVP)
                    VolumeProfile(indexStart, index);

                isUpdateVP = true; // chart end
            }
            else
            {
                // Live VP
                /*
                Known Issue:
                */
                if (UpdateVPStrategy_Input == UpdateVPStrategy_Data.SameThread_MayFreeze)
                {
                    if (EnableVP)
                        LiveVP_Update(indexStart, index);
                    else if (!EnableVP && EnableMiniProfiles)
                        LiveVP_Update(indexStart, index, true);
                }
                else
                    LiveVP_Concurrent(index, indexStart);

                if (!isEndChart) {
                    LoadMoreHistory_IfNeeded();
                    isEndChart = true;
                }
            }
        }

        private void CleanUp_MainVP(int index, int indexStart)
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

            lastCleaned._VP_Interval = index == indexStart ? index : (index - 1);
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

        // *********** VOLUME PROFILE BARS ***********
        private void VolumeProfile(int iStart, int index, ExtraProfiles extraProfiles = ExtraProfiles.No, bool isLoop = false, bool drawOnly = false)
        {
            // Weekly/Monthly on Buy_Sell is a waste of time
            if (VolumeMode_Input == VolumeMode_Data.Buy_Sell && (extraProfiles == ExtraProfiles.Weekly || extraProfiles == ExtraProfiles.Monthly))
               return;

            // ==== VP ====
            if (!drawOnly)
                VP_Bars(index, extraProfiles);

            // ==== Drawing ====
            if (Segments_VP.Count == 0 || isLoop)
                return;

            // For Results
            Bars mainTF = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                           VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            Bars TF_Bars = extraProfiles == ExtraProfiles.No ? mainTF:
                           extraProfiles == ExtraProfiles.MiniVP ? MiniVPs_Bars :
                           extraProfiles == ExtraProfiles.Weekly ? WeeklyBars : MonthlyBars;
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            double lowest = TF_Bars.LowPrices[TF_idx];
            double highest = TF_Bars.HighPrices[TF_idx];
            if (double.IsNaN(lowest)) {
                lowest = TF_Bars.LowPrices.LastValue;
                highest = TF_Bars.HighPrices.LastValue;
            } // Mini VPs avoid crash after recalculating

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

            // Manual Refactoring.
            // LLM allucinates.
            for (int i = 0; i < Segments_VP.Count; i++)
            {
                double priceKey = Segments_VP[i];

                if (extraProfiles == ExtraProfiles.Monthly)
                {
                    if (!MonthlyRank.Normal.ContainsKey(priceKey))
                        continue;
                }
                else if (extraProfiles == ExtraProfiles.Weekly)
                {
                    if (!WeeklyRank.Normal.ContainsKey(priceKey))
                        continue;
                }
                else if (extraProfiles == ExtraProfiles.MiniVP)
                {
                    if (!miniVPsRank.Normal.ContainsKey(priceKey))
                        continue;
                }
                else if (extraProfiles == ExtraProfiles.No)
                {
                    if (!VP_VolumesRank.ContainsKey(priceKey))
                        continue;
                }

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

                double lowerSegmentY1 = Segments_VP[i] - rowHeight;
                double upperSegmentY2 = Segments_VP[i];

                double y1_text = priceKey;

                void DrawRectangle_Normal(double currentVolume, double maxVolume, bool intradayProfile = false)
                {
                    double proportion = currentVolume * proportion_VP;
                    double dynLength = proportion / maxVolume;

                    DateTime x2 = x1_Start.AddMilliseconds(dynLength);

                    string extraName = extraProfiles == ExtraProfiles.No ? "" :
                                       extraProfiles == ExtraProfiles.MiniVP ? "Mini" :
                                       extraProfiles == ExtraProfiles.Weekly ? "Weekly" : "Monthly";
                    Color histogramColor = extraProfiles == ExtraProfiles.No ? HistColor :
                                       extraProfiles == ExtraProfiles.MiniVP ? HistColor :
                                       extraProfiles == ExtraProfiles.Weekly ? WeeklyColor : MonthlyColor;
                    if (EnableGradient)
                    {
                        Color minColor = extraProfiles == ExtraProfiles.No ? ColorGrandient_Min :
                                          extraProfiles == ExtraProfiles.MiniVP ? ColorGrandient_Min :
                                          extraProfiles == ExtraProfiles.Weekly ? WeeklyGrandient_Min : MonthlyGrandient_Min;
                        Color maxColor = extraProfiles == ExtraProfiles.No ? ColorGrandient_Max :
                                          extraProfiles == ExtraProfiles.MiniVP ? ColorGrandient_Max :
                                          extraProfiles == ExtraProfiles.Weekly ? WeeklyGrandient_Max : MonthlyGrandient_Max;

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

                    ChartRectangle volHist = Chart.DrawRectangle($"{iStart}_{i}_VP{extraName}_Normal", x1_Start, lowerSegmentY1, x2, upperSegmentY2, histogramColor);

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

                        if (extraName == "Weekly")
                            maxLength_Intraday = dateOffset_Duo.Subtract(dateOffset).TotalMilliseconds;

                        if (extraName == "Monthly")
                            maxLength_Intraday = dateOffset_Triple.Subtract(dateOffset_Duo).TotalMilliseconds;

                        // Recalculate histograms 'X' position
                        double proportion_Intraday = currentVolume * (maxLength_Intraday - (maxLength_Intraday / maxWidth));
                        if (selectedWidth == HistWidth_Data._100)
                            proportion_Intraday = currentVolume * maxLength_Intraday;

                        double dynLength_Intraday = proportion_Intraday / maxVolume;

                        // Set 'X'
                        volHist.Time1 = dateOffset;
                        volHist.Time2 = dateOffset.AddMilliseconds(-dynLength_Intraday);

                        if (extraName == "Weekly")
                        {
                            volHist.Time1 = dateOffset_Duo;
                            volHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Intraday);
                            if (!EnableMonthlyProfile && FillIntradaySpace)
                            {
                                volHist.Time1 = dateOffset;
                                volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                            }
                        }
                        if (extraName == "Monthly")
                        {
                            if (EnableWeeklyProfile)
                            {
                                // Show after
                                volHist.Time1 = dateOffset_Triple;
                                volHist.Time2 = dateOffset_Triple.AddMilliseconds(-dynLength_Intraday);
                                // Show after together
                                if (FillIntradaySpace)
                                {
                                    volHist.Time1 = dateOffset_Duo;
                                    volHist.Time2 = dateOffset_Duo.AddMilliseconds(dynLength_Intraday);
                                }
                            }
                            else
                            {
                                // Use Weekly position
                                volHist.Time1 = dateOffset_Duo;
                                volHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Intraday);
                                if (FillIntradaySpace)
                                {
                                    volHist.Time1 = dateOffset;
                                    volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                                }
                            }
                        }

                        IDictionary<double, double> VPdict = extraProfiles == ExtraProfiles.No ? VP_VolumesRank :
                                                            extraProfiles == ExtraProfiles.MiniVP ? miniVPsRank.Normal :
                                                            extraProfiles == ExtraProfiles.Weekly ? WeeklyRank.Normal : MonthlyRank.Normal;
                        Draw_VA_POC(VPdict, iStart, x1_Start, xBar, extraProfiles, true, volHist.Time1);
                    }
                }

                void DrawRectangle_BuySell(
                    double currentBuy, double currentSell,
                    double buyMax, double sellMax,
                    bool intradayProfile = false)
                {
                    string extraName = extraProfiles == ExtraProfiles.No ? "" :
                                       extraProfiles == ExtraProfiles.MiniVP ? "Mini" :
                                       extraProfiles == ExtraProfiles.Weekly ? "Weekly" : "Monthly";

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
                    sellHist = Chart.DrawRectangle($"{iStart}_{i}_VP{extraName}Sell", x1_Start, lowerSegmentY1, x2_Sell, upperSegmentY2, SellColor);
                    buyHist = Chart.DrawRectangle($"{iStart}_{i}_VP{extraName}Buy", x1_Start, lowerSegmentY1, x2_Buy, upperSegmentY2, BuyColor);
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
                        ChartRectangle subtHist = Chart.DrawRectangle($"{iStart}_{i}_VPSubt", x1, lowerSegmentY1, x2, upperSegmentY2, colorHist);

                        dynLength = -Math.Abs(dynLength);
                        subtHist.Time1 = dateOffset_Subt;
                        subtHist.Time2 = subtHist.Time2 != dateOffset_Subt ? dateOffset_Subt.AddMilliseconds(dynLength) : dateOffset_Subt;

                        if (FillHist_VP)
                            subtHist.IsFilled = true;

                        Draw_VA_POC(VP_VolumesRank_Subt, iStart, x1_Start, xBar, extraProfiles, true, subtHist.Time1);
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

                    string extraName = extraProfiles == ExtraProfiles.No ? "" :
                                       extraProfiles == ExtraProfiles.MiniVP ? "Mini" :
                                       extraProfiles == ExtraProfiles.Weekly ? "Weekly" : "Monthly";

                    ChartRectangle deltaHist = Chart.DrawRectangle($"{iStart}_{i}_VP{extraName}_Delta", x1_Start, lowerSegmentY1, x2, upperSegmentY2, colorHist);

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

                        if (extraName == "Weekly")
                            maxLength_Intraday = dateOffset_Duo.Subtract(dateOffset).TotalMilliseconds;

                        if (extraName == "Monthly")
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

                        if (extraName == "Weekly") {
                            deltaHist.Time1 = dateOffset_Duo;
                            deltaHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Delta);
                            if (!EnableMonthlyProfile && FillIntradaySpace) {
                                deltaHist.Time1 = dateOffset;
                                deltaHist.Time2 = dateOffset.AddMilliseconds(dynLength_Delta);
                            }
                        }
                        if (extraName == "Monthly") {
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
                        IDictionary<double, double> VPdict = extraProfiles == ExtraProfiles.No ? VP_DeltaRank :
                                                            extraProfiles == ExtraProfiles.MiniVP ? miniVPsRank.Delta :
                                                            extraProfiles == ExtraProfiles.Weekly ? WeeklyRank.Delta : MonthlyRank.Delta;
                        Draw_VA_POC(VPdict, iStart, x1_Start, xBar, extraProfiles, true, deltaHist.Time1);
                    }
                }

                if (VolumeMode_Input == VolumeMode_Data.Normal)
                {
                    IDictionary<double, double> VPdict = extraProfiles == ExtraProfiles.No ? VP_VolumesRank:
                                                         extraProfiles == ExtraProfiles.MiniVP ? miniVPsRank.Normal:
                                                         extraProfiles == ExtraProfiles.Weekly ? WeeklyRank.Normal : MonthlyRank.Normal;
                    Draw_VA_POC(VPdict, iStart, x1_Start, xBar, extraProfiles);

                    if (extraProfiles == ExtraProfiles.No)
                        DrawRectangle_Normal(VP_VolumesRank[priceKey], VP_VolumesRank.Values.Max(), isIntraday);

                    if (extraProfiles == ExtraProfiles.MiniVP)
                        DrawRectangle_Normal(miniVPsRank.Normal[priceKey], miniVPsRank.Normal.Values.Max(), false);

                    if (extraProfiles == ExtraProfiles.Weekly)
                        DrawRectangle_Normal(WeeklyRank.Normal[priceKey], WeeklyRank.Normal.Values.Max(), isIntraday);

                    if (extraProfiles == ExtraProfiles.Monthly)
                        DrawRectangle_Normal(MonthlyRank.Normal[priceKey], MonthlyRank.Normal.Values.Max(), isIntraday);

                    if (ShowResults || ShowMiniResults)
                    {
                        if (extraProfiles == ExtraProfiles.MiniVP && !ShowMiniResults)
                            continue;
                        if (extraProfiles != ExtraProfiles.MiniVP && !ShowResults)
                            continue;

                        double sum = 0;
                        if (extraProfiles == ExtraProfiles.No)
                            sum = Math.Round(VP_VolumesRank.Values.Sum());
                        if (extraProfiles == ExtraProfiles.MiniVP)
                            sum = Math.Round(miniVPsRank.Normal.Values.Sum());
                        if (extraProfiles == ExtraProfiles.Weekly)
                            sum = Math.Round(WeeklyRank.Normal.Values.Sum());
                        if (extraProfiles == ExtraProfiles.Monthly)
                            sum = Math.Round(MonthlyRank.Normal.Values.Sum());

                        string strValue = FormatResults ? FormatBigNumber(sum) : $"{sum}";

                        ChartText Center = Chart.DrawText($"{iStart}_VPNormal{extraProfiles}Result", $"\n{strValue}", x1_Start, lowest, EnableGradient ? ColorGrandient_Min : HistColor);
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
                    if (extraProfiles == ExtraProfiles.No) {
                        double buyMax = 0;
                        try { buyMax = VP_VolumesRank_Up.Values.Max(); } catch { }
                        double sellMax = 0;
                        try { sellMax = VP_VolumesRank_Down.Values.Max(); } catch { }
                        if (VP_VolumesRank_Up.ContainsKey(priceKey) && VP_VolumesRank_Down.ContainsKey(priceKey))
                            DrawRectangle_BuySell(VP_VolumesRank_Up[priceKey], VP_VolumesRank_Down[priceKey],
                                                  buyMax, sellMax, isIntraday);
                    }
                    if (extraProfiles == ExtraProfiles.MiniVP) {
                        double buyMax = 0;
                        try { buyMax = miniVPsRank.Up.Values.Max(); } catch { }
                        double sellMax = 0;
                        try { sellMax = miniVPsRank.Down.Values.Max(); } catch { }
                        if (miniVPsRank.Up.ContainsKey(priceKey) && miniVPsRank.Down.ContainsKey(priceKey))
                            DrawRectangle_BuySell(miniVPsRank.Up[priceKey], miniVPsRank.Down[priceKey],
                                                  buyMax, sellMax, false);
                    }

                    if (ShowResults || ShowMiniResults)
                    {
                        if (extraProfiles == ExtraProfiles.MiniVP && !ShowMiniResults)
                            continue;

                        double volBuy = VP_VolumesRank_Up.Values.Sum();
                        double volSell = VP_VolumesRank_Down.Values.Sum();
                        double percentBuy = (volBuy * 100) / (volBuy + volSell);
                        double percentSell = (volSell * 100) / (volBuy + volSell);
                        percentBuy = Math.Round(percentBuy);
                        percentSell = Math.Round(percentSell);

                        string extraName = extraProfiles == ExtraProfiles.No ? "" :
                                           extraProfiles == ExtraProfiles.MiniVP ? "Mini" :
                                           extraProfiles == ExtraProfiles.Weekly ? "Weekly" : "Monthly";
                        ChartText Left, Right;
                        Left = Chart.DrawText($"{iStart}_VPSell{extraName}Sum", $"{percentSell}%", x1_Start, lowest, SellColor);
                        Right = Chart.DrawText($"{iStart}_VPBuy{extraName}Sum", $"{percentBuy}%", x1_Start, lowest, BuyColor);
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

                        Center = Chart.DrawText($"{iStart}_VPBuySell{extraName}Result", $"\n{strFormated}", x1_Start, lowest, centerColor);
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

                    IDictionary<double, double> VPdict = extraProfiles == ExtraProfiles.No ? VP_DeltaRank :
                                                        extraProfiles == ExtraProfiles.MiniVP ? miniVPsRank.Delta :
                                                        extraProfiles == ExtraProfiles.Weekly ? WeeklyRank.Delta : MonthlyRank.Delta;
                    Draw_VA_POC(VPdict, iStart, x1_Start, xBar, extraProfiles);

                    if (extraProfiles == ExtraProfiles.No) {
                        DrawRectangle_Delta(VP_DeltaRank[priceKey],
                                            VP_DeltaRank.Values.Max(),
                                            VP_DeltaRank.Values.Where(n => n < 0), isIntraday);
                    }
                    if (extraProfiles == ExtraProfiles.MiniVP)
                        DrawRectangle_Delta(miniVPsRank.Delta[priceKey],
                                            miniVPsRank.Delta.Values.Max(),
                                            miniVPsRank.Delta.Values.Where(n => n < 0), false);

                    if (extraProfiles == ExtraProfiles.Weekly)
                        DrawRectangle_Delta(WeeklyRank.Delta[priceKey],
                                            WeeklyRank.Delta.Values.Max(),
                                            WeeklyRank.Delta.Values.Where(n => n < 0), isIntraday);

                    if (extraProfiles == ExtraProfiles.Monthly)
                        DrawRectangle_Delta(MonthlyRank.Delta[priceKey],
                                            MonthlyRank.Delta.Values.Max(),
                                            MonthlyRank.Delta.Values.Where(n => n < 0), isIntraday);

                    if (ShowResults || ShowMiniResults)
                    {
                        if (extraProfiles == ExtraProfiles.MiniVP && !ShowMiniResults)
                            continue;

                        string extraName = extraProfiles == ExtraProfiles.No ? "" :
                                           extraProfiles == ExtraProfiles.MiniVP ? "Mini" :
                                           extraProfiles == ExtraProfiles.Weekly ? "Weekly" : "Monthly";
                        double deltaBuy = 0;
                        double deltaSell = 0;
                        double totalDelta = 0;

                        if (extraProfiles == ExtraProfiles.No) {
                            deltaBuy = VP_DeltaRank.Values.Where(n => n > 0).Sum();
                            deltaSell = VP_DeltaRank.Values.Where(n => n < 0).Sum();
                            totalDelta = Math.Round(VP_DeltaRank.Values.Sum());
                        }
                        if (extraProfiles == ExtraProfiles.MiniVP) {
                            deltaBuy = miniVPsRank.Delta.Values.Where(n => n > 0).Sum();
                            deltaSell = miniVPsRank.Delta.Values.Where(n => n < 0).Sum();
                            totalDelta = Math.Round(miniVPsRank.Delta.Values.Sum());
                        }
                        if (extraProfiles == ExtraProfiles.Weekly) {
                            deltaBuy = WeeklyRank.Delta.Values.Where(n => n > 0).Sum();
                            deltaSell = WeeklyRank.Delta.Values.Where(n => n < 0).Sum();
                            totalDelta = Math.Round(WeeklyRank.Delta.Values.Sum());
                        }
                        if (extraProfiles == ExtraProfiles.Monthly) {
                            deltaBuy = MonthlyRank.Delta.Values.Where(n => n > 0).Sum();
                            deltaSell = MonthlyRank.Delta.Values.Where(n => n < 0).Sum();
                            totalDelta = Math.Round(MonthlyRank.Delta.Values.Sum());
                        }

                        double percentBuy = 0;
                        double percentSell = 0;
                        try { percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { };
                        try { percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { }
                        percentBuy = Math.Round(percentBuy);
                        percentSell = Math.Round(percentSell);

                        ChartText Left, Right;
                        Right = Chart.DrawText($"{iStart}_VP{extraName}DeltaBuySum", $"{percentBuy}%", x1_Start, lowest, BuyColor);
                        Left = Chart.DrawText($"{iStart}_VP{extraName}DeltaSellSum", $"{percentSell}%", x1_Start, lowest, SellColor);
                        Left.HorizontalAlignment = HorizontalAlignment.Left;
                        Right.HorizontalAlignment = HorizontalAlignment.Right;
                        Left.FontSize = FontSizeResults;
                        Right.FontSize = FontSizeResults;

                        ChartText Center;
                        string totalDeltaFmtd = totalDelta > 0 ? FormatBigNumber(totalDelta) : $"-{FormatBigNumber(Math.Abs(totalDelta))}";
                        string totalDeltaString = FormatResults ? totalDeltaFmtd : $"{totalDelta}";

                        Color centerColor = totalDelta > 0 ? BuyColor : SellColor;
                        Center = Chart.DrawText($"{iStart}_VP{extraName}DeltaResult", $"\n{totalDeltaString}", x1_Start, lowest, centerColor);
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

                            double minDelta = 0;
                            double maxDelta = 0;
                            double subDelta = 0;
                            if (extraProfiles == ExtraProfiles.No) {
                                minDelta = Math.Round(VP_MinMaxDelta[0]);
                                maxDelta = Math.Round(VP_MinMaxDelta[1]);
                                subDelta = Math.Round(minDelta - maxDelta);
                            }
                            if (extraProfiles == ExtraProfiles.MiniVP) {
                                minDelta = Math.Round(miniVPsRank.MinMaxDelta[0]);
                                maxDelta = Math.Round(miniVPsRank.MinMaxDelta[1]);
                                subDelta = Math.Round(minDelta - maxDelta);
                            }
                            if (extraProfiles == ExtraProfiles.Weekly) {
                                minDelta = Math.Round(WeeklyRank.MinMaxDelta[0]);
                                maxDelta = Math.Round(WeeklyRank.MinMaxDelta[1]);
                                subDelta = Math.Round(minDelta - maxDelta);
                            }
                            if (extraProfiles == ExtraProfiles.Monthly) {
                                minDelta = Math.Round(MonthlyRank.MinMaxDelta[0]);
                                maxDelta = Math.Round(MonthlyRank.MinMaxDelta[1]);
                                subDelta = Math.Round(minDelta - maxDelta);
                            }

                            string minDeltaFmtd = minDelta > 0 ? FormatBigNumber(minDelta) : $"-{FormatBigNumber(Math.Abs(minDelta))}";
                            string maxDeltaFmtd = maxDelta > 0 ? FormatBigNumber(maxDelta) : $"-{FormatBigNumber(Math.Abs(maxDelta))}";
                            string subDeltaFmtd = subDelta > 0 ? FormatBigNumber(subDelta) : $"-{FormatBigNumber(Math.Abs(subDelta))}";

                            string minDeltaString = FormatResults ? minDeltaFmtd : $"{minDelta}";
                            string maxDeltaString = FormatResults ? maxDeltaFmtd : $"{maxDelta}";
                            string subDeltaString = FormatResults ? subDeltaFmtd : $"{subDelta}";

                            Color subColor = subDelta > 0 ? BuyColor : SellColor;

                            if (!ShowOnlySubtDelta)
                            {
                                MinText = Chart.DrawText($"{iStart}_VP{extraName}DeltaMinResult", $"\n\nMin: {minDeltaString}", x1_Start, lowest, SellColor);
                                MaxText = Chart.DrawText($"{iStart}_VP{extraName}DeltaMaxResult", $"\n\n\nMax: {maxDeltaString}", x1_Start, lowest, BuyColor);
                                SubText = Chart.DrawText($"{iStart}_VP{extraName}DeltaSubResult", $"\n\n\n\nSub: {subDeltaString}", x1_Start, lowest, subColor);
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
                                SubText = Chart.DrawText($"{iStart}_VP{extraName}DeltaSubResult", $"\n\nSub: {subDeltaString}", x1_Start, lowest, subColor);
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

            if (!ShowOHLC)
                return;
            ChartText iconOpenSession = Chart.DrawText($"{iStart}_OHLC_Open{extraProfiles}", "▂", histRightSide ? xBar : x1_Start , TF_Bars.OpenPrices[TF_idx], ColorOHLC);
            ChartText iconCloseSession = Chart.DrawText($"{iStart}_OHLC_Close{extraProfiles}", "▂", histRightSide ? xBar : x1_Start, Bars.ClosePrices[index], ColorOHLC);
            iconOpenSession.VerticalAlignment = VerticalAlignment.Center;
            iconOpenSession.HorizontalAlignment = HorizontalAlignment.Left;
            iconOpenSession.FontSize = 14;
            iconCloseSession.VerticalAlignment = VerticalAlignment.Center;
            iconCloseSession.HorizontalAlignment = HorizontalAlignment.Right;
            iconCloseSession.FontSize = 14;

            ChartTrendLine Session = Chart.DrawTrendLine($"{iStart}_OHLC_Body{extraProfiles}", histRightSide ? xBar : x1_Start, lowest, histRightSide ? xBar : x1_Start, highest, ColorOHLC);
            Session.Thickness = 3;
        }

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

            configHasChanged = false;
            isUpdateVP = false;
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

                lock (_lockSource)
                    Bars_List = new List<Bar>(VOL_Bars);

                liveVP_UpdateIt = true;
            }
            cts ??= new CancellationTokenSource();

            CreateMonthlyVP(index, isConcurrent: true);
            CreateWeeklyVP(index, isConcurrent: true);
            CreateMiniVPs(index, isConcurrent: true);

            if (EnableVP)
            {
                liveVP_Task ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.No, cts.Token));
                liveVP_StartIndexes.VP = indexStart;
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
                            TimesCopy = BarTimes_Array.Skip(startIndex);
                        lastTime = lastBarTime;
                    }
                    int endIndex = TimesCopy.Count();

                    // Source Bars
                    int startSourceIndex = extraID == ExtraProfiles.No ? lastBar_VPStart:
                                     extraID == ExtraProfiles.MiniVP ? lastBar_ExtraVPs._MiniStart :
                                     extraID == ExtraProfiles.Weekly ? lastBar_ExtraVPs._WeeklyStart : lastBar_ExtraVPs._MonthlyStart;

                    lock (_lockSource)
                        BarsCopy = Bars_List.Skip(startSourceIndex);

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
                                miniVPsRank.Normal = Worker_VolumesRank;
                                miniVPsRank.Up = Worker_VolumesRank_Up;
                                miniVPsRank.Down = Worker_VolumesRank_Down;
                                miniVPsRank.Delta = Worker_DeltaRank;
                                miniVPsRank.MinMaxDelta = Worker_MinMaxDelta;
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
                        double final = hl2SQRT / hl2SQRT;

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
                        lastBar_ExtraVPs._MiniStart = lastBar_ExtraVPs._Mini;
                    miniVPsRank.Normal.Clear();
                    miniVPsRank.Up.Clear();
                    miniVPsRank.Down.Clear();
                    miniVPsRank.Delta.Clear();
                    double[] resetDelta = {0, 0};
                    miniVPsRank.MinMaxDelta = resetDelta;
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

                if (index == weekStart ||
                    (index - 1) == weekStart && isPriceBased_Chart || loopStart
                ) {
                    if (!IsLastBar)
                        lastBar_ExtraVPs._WeeklyStart = lastBar_ExtraVPs._Weekly;
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

                if (index == monthStart ||
                    (index - 1) == monthStart && isPriceBased_Chart || loopStart
                ) {
                    if (!IsLastBar)
                        lastBar_ExtraVPs._MonthlyStart = lastBar_ExtraVPs._Monthly;
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

        // ====== Functions Area ======
        private void VP_Bars(int index, ExtraProfiles extraVP = ExtraProfiles.No)
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];

            // For real-time market - VP
            // Run conditional only in the last bar of repaint loop
            if (IsLastBar && Bars.OpenTimes[index] == Bars.LastBar.OpenTime)
                endTime = VOL_Bars.LastBar.OpenTime;

            int startIndex = lastBar_VPStart;

            if (extraVP == ExtraProfiles.Monthly) {
                startIndex = lastBar_ExtraVPs._Monthly;
                if (IsLastBar)
                    startIndex = lastBar_ExtraVPs._MonthlyStart;
            }
            if (extraVP == ExtraProfiles.Weekly) {
                startIndex = lastBar_ExtraVPs._Weekly;
                if (IsLastBar)
                    startIndex = lastBar_ExtraVPs._WeeklyStart;
            }
            if (extraVP == ExtraProfiles.MiniVP) {
                startIndex = lastBar_ExtraVPs._Mini;
                if (IsLastBar)
                    startIndex = lastBar_ExtraVPs._MiniStart;
            }

            // Keep shared VOL_Bars since 1min bars
            // are quite cheap in terms of RAM, even for 1 year.
            for (int k = startIndex; k < VOL_Bars.Count; ++k)
            {
                Bar volBar;
                volBar = VOL_Bars[k];

                if (volBar.OpenTime < startTime || volBar.OpenTime > endTime)
                {
                    if (volBar.OpenTime > endTime) {
                        if (extraVP == ExtraProfiles.No)
                            lastBar_VP = k;
                        if (extraVP == ExtraProfiles.Monthly)
                            lastBar_ExtraVPs._Monthly = k;
                        if (extraVP == ExtraProfiles.Weekly)
                            lastBar_ExtraVPs._Weekly = k;
                        if (extraVP == ExtraProfiles.MiniVP)
                            lastBar_ExtraVPs._Mini = k;

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
                    double final = hl2SQRT / hl2SQRT;

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
                if (EnableMonthlyProfile && extraVP == ExtraProfiles.Monthly)
                    UpdateExtraProfiles(MonthlyRank, priceKey, vol, isBullish);

                if (EnableWeeklyProfile && extraVP == ExtraProfiles.Weekly)
                    UpdateExtraProfiles(WeeklyRank, priceKey, vol, isBullish);

                if (EnableMiniProfiles && extraVP == ExtraProfiles.MiniVP)
                    UpdateExtraProfiles(miniVPsRank, priceKey, vol, isBullish);

                if (extraVP != ExtraProfiles.No)
                    return;

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

        private void DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.LightBlue);
        }
        private void Second_DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt2", $"{Msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }
        private void Draw_VA_POC(IDictionary<double, double> vpDict, int iStart, DateTime x1_Start, DateTime xBar, ExtraProfiles extraProfiles = ExtraProfiles.No, bool isIntraday = false, DateTime intraX1 = default)
        {
            if (ShowVA) {
                double[] VAL_VAH_POC = VA_Calculation(vpDict);

                if (!VAL_VAH_POC.Any())
                    return;

                ChartTrendLine poc = Chart.DrawTrendLine($"{iStart}_POC{extraProfiles}", x1_Start, VAL_VAH_POC[2] - rowHeight, xBar, VAL_VAH_POC[2] - rowHeight, ColorPOC);
                ChartTrendLine vah = Chart.DrawTrendLine($"{iStart}_VAH{extraProfiles}", x1_Start, VAL_VAH_POC[1] + rowHeight, xBar, VAL_VAH_POC[1] + rowHeight, ColorVAH);
                ChartTrendLine val = Chart.DrawTrendLine($"{iStart}_VAL{extraProfiles}", x1_Start, VAL_VAH_POC[0], xBar, VAL_VAH_POC[0], ColorVAL);

                poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                vah.LineStyle = LineStyleVA; vah.Thickness = ThicknessVA; vah.Comment = "VAH";
                val.LineStyle = LineStyleVA; val.Thickness = ThicknessVA; val.Comment = "VAL";

                ChartRectangle rectVA;
                rectVA = Chart.DrawRectangle($"{iStart}_RectVA{extraProfiles}", x1_Start, VAL_VAH_POC[0], xBar, VAL_VAH_POC[1] + rowHeight, VAColor);
                rectVA.IsFilled = true;

                if (ExtendVA) {
                    vah.Time2 = extendDate();
                    val.Time2 = extendDate();
                    rectVA.Time2 = extendDate();
                }
                if (ExtendPOC)
                    poc.Time2 = extendDate();

                if (isIntraday && extraProfiles != ExtraProfiles.MiniVP) {
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
                ChartTrendLine poc = Chart.DrawTrendLine($"{iStart}_POC{extraProfiles}", x1_Start, priceLVOL - rowHeight, xBar, priceLVOL - rowHeight, ColorPOC);
                poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";

                if (ExtendPOC)
                    poc.Time2 = extendDate();

                if (isIntraday && extraProfiles != ExtraProfiles.MiniVP)
                    poc.Time1 = intraX1;
            }

            DateTime extendDate() {
                string tfName = extraProfiles == ExtraProfiles.No ?
                (VPInterval_Input == VPInterval_Data.Daily ? "D1" :
                    VPInterval_Input == VPInterval_Data.Weekly ? "W1" : "Month1" ) :
                extraProfiles == ExtraProfiles.MiniVP ? MiniVPs_Timeframe.ShortName.ToString() :
                extraProfiles == ExtraProfiles.Weekly ?  "W1" :  "Month1";

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
        // ========= ========== ==========
        /*
            TODO: VA + POC for historical MiniVPs, Daily Weekly mothnly
            TODO, VA + POC intraday if possible
            TODO extended POCS/VAS by nº day or nº Mini VP timeframe
        */
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

        public void ClearAndRecalculate()
        {
            Thread.Sleep(300);
            LoadMoreHistory_IfNeeded();

            // LookBack from VP
            Bars vpBars = VPInterval_Input == VPInterval_Data.Daily ? DailyBars :
                           VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars : MonthlyBars;
            int FirstIndex = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes.FirstOrDefault());
            // Get Index of ODF Interval to continue only in Lookback
            int iVerify = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            while (vpBars.ClosePrices.Count - iVerify > Lookback) {
                FirstIndex++;
                iVerify = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            }

            // Daily or Weekly VP
            int TF_idx = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes[TF_idx]);

            // Weekly Profile but Daily VP
            bool extraWeekly = EnableVP && EnableWeeklyProfile && VPInterval_Input == VPInterval_Data.Daily;
            if (extraWeekly) {
                TF_idx = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
                indexStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[TF_idx]);
            }
            // Monthly Profile
            bool extraMonthly = EnableVP && EnableMonthlyProfile;
            if (extraMonthly) {
                TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
                indexStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[TF_idx]);
            }

            // Reset Tick Index.
            lastBar_VP = 0;
            lastBar_ExtraVPs._Mini = 0;
            lastBar_ExtraVPs._Weekly = 0;
            lastBar_ExtraVPs._Monthly = 0;

            // Reset Segments
            // It's needed since TF_idx(start) change if SegmentsInterval_Input is switched on the panel
            Segments_VP.Clear();
            segmentInfo.Clear();

            // Reset last update
            lastCleaned._VP_Interval = 0;

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
                    iVerify = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    if (vpBars.ClosePrices.Count - iVerify > Lookback)
                        continue;
                }

                TF_idx = vpBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                indexStart = Bars.OpenTimes.GetIndexByTime(vpBars.OpenTimes[TF_idx]);

                if (index == indexStart ||
                   (index - 1) == indexStart && isPriceBased_Chart ||
                   (index - 1) == indexStart && (index - 1) != lastCleaned._VP_Interval)
                    CleanUp_MainVP(indexStart, index);

                CreateMiniVPs(index);

                try { if (EnableVP) VolumeProfile(indexStart, index); } catch { }
            }

            configHasChanged = true;
            DrawStartVolumeLine();
        }

        public void DrawStartVolumeLine() {
            try
            {
                DateTime FirstVolDate = VOL_Bars.OpenTimes.FirstOrDefault();
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("VolumeStart", FirstVolDate, Color.Red);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText($"VolumeStartText", $"{VOL_Timeframe.ShortName} Volume Data \n ends here", FirstVolDate, VOL_Bars.HighPrices.FirstOrDefault(), Color.Red);
                textInfo.FontSize = 8;
            }
            catch { };
            try
            {
                Bar FirstIntervalBar = VPInterval_Input == VPInterval_Data.Daily ? DailyBars.FirstOrDefault() :
                                            VPInterval_Input == VPInterval_Data.Weekly ? WeeklyBars.FirstOrDefault() : MonthlyBars.FirstOrDefault();
                DateTime FirstIntervalDate = FirstIntervalBar.OpenTime;
                double FirstIntervalPrice = FirstIntervalBar.High;

                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("LookbackStart", FirstIntervalDate, Color.Gray);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText($"LookbackStartText", $"{VPInterval_Input} Interval Data \n ends here", FirstIntervalDate, FirstIntervalPrice, Color.Gray);
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

            if (LoadBarsStrategy_Input == LoadBarsStrategy_Data.Async) {
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

            if (EnableMiniProfiles && MiniVPs_Bars.OpenTimes.FirstOrDefault() > lookbackDate) {
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
                if (!loadingAsyncBars) {
                    string volumeLineInfo = "=> Zoom out and follow the Vertical Line";
                    asyncBarsPopup = Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Loading Async {VOL_Timeframe.ShortName} Bars \n{volumeLineInfo}",
                        PopupNotificationState.InProgress
                    );
                }

                if (!loadingBarsComplete) {
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
                    DateTime sourceDate = EnableWeeklyProfile && !EnableMonthlyProfile ? WeeklyBars.LastBar.OpenTime.Date :
                                          EnableMonthlyProfile ? MonthlyBars.LastBar.OpenTime.Date :
                                          lookbackDate;

                    // Draw target date.
                    DrawTargetDateLine();

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
                    Region = "Misc",
                    RegionOrder = 4,
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
                    RegionOrder = 4,
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
                    RegionOrder = 4,
                    Key = "DistributionKey",
                    Label = "Distribution",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.Distribution.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Distribution_Data)),
                    OnChanged = _ => UpdateDistribuition(),
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
                new()
                {
                    Region = "Misc",
                    RegionOrder = 4,
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
                    RegionOrder = 4,
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
                    RegionOrder = 4,
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
            var grid = new Grid(6, 5);
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
            CheckboxHandler(key);
        }
        private void CheckboxHandler(string key)
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
                ApplyBtn.IsVisible = true;
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
                    ApplyBtn.IsVisible = true;
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
                ApplyBtn.IsVisible = true;
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
        private void UpdateDistribuition()
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
                    Outside.Chart.ResetBarColors();
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
