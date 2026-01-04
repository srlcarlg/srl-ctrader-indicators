/*
--------------------------------------------------------------------------------------------------------------------------------
                    [Renko] Weis & Wyckoff System v2.0
                                revision 1
showcases the concepts of David H. Weis and Richard Wyckoff on Renko Chart

It's just a way of visualizing the Waves and Volume numerically, it's not an original idea.
You can find this way of visualization first at
(www.youtube.com/watch?v=uzISUr1itWg, most recent www.vimeo.com/394541866)

This uses the code concepts of (Numbers-Renko 数字練行足 https://www.tradingview.com/script/9BKOIhdl-Numbers-Renko/ in PineScript),
=> Cheers to the akutsusho!

I IMPROVED IT and BROUGHT IT to cTrader/C#.
I added many other features based on the original design and my personal taste, like:

(Make your favorite design template yourself): 14 design parameters with a total of 32 sub-options
(Non-Repaint and Repaint Weis Waves Option): You can choose whether to see the Current Trend Wave value.
(Dynamic TimeLapse): Time Waves showed the difference in milliseconds, seconds, minutes, hours, days!
And many others...

What's new in rev. 1? (after ODF_AGG)
- Support to [Candles, Heikin-Ash, Tick, Range] Charts
- Improved ZigZag => MTF support + [ATR, Percentage, Pips, NoLag_HighLow] Modes
- Includes all "Order Flow Aggregated" related improvements
    - Custom MAs
    - Performance Drawing
    - Strength Filters (MA/StdDev/Both)
    - High-performance VP_Tick()
    - High-performance GetWicks()
    - Asynchronous Tick Data Collection

Last update => 04/01/2026
===========================

What's new in rev.2 (2026)?

- New features for 'Wyckoff Bars' => 'Coloring':
    - "L1Norm" to Strength Filter
    - "Percentile" Ratio
    - Independent Ratios on Params-Panel
        - for [Fixed, Percentile] types
        - and [Normalized_Emphasized] filter
- Move '[Wyckoff] Show Strength?' debug parameter to Params-Panel
- Fix (params-panel) => Normalized_Emphasized parameters (Period, Multiplier) doesn't set new values.
- Fix (custom-mas) => StdDev was using the MA values instead of respective MA-Source values


Final revision (2025)

- Fix: Params Panel on MacOs
    - Supposedly cut short/half the size (Can't reproduce it through VM)
    - WrapPanel isn't fully supported (The button is hidden)
    - MissingMethodException on cAlgo.API.Panel.get_Children() (...)
        - At ToggleExpandCollapse event.
- Tested on MacOS (12 Monterey / 13 Ventura) without 3D accelerated graphics

========================================================================

              Transcribed & Improved for cTrader/C#
                          by srlcarlg

        Original Code Concepts in TradingView/Pinescript
                          by akutsusho

=========================================================================

== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using static cAlgo.WeisWyckoffSystemV20;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AutoRescale = true, AccessRights = AccessRights.None)]
    public class WeisWyckoffSystemV20 : Indicator
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
        [Parameter("Panel Position:", DefaultValue = PanelAlign_Data.Bottom_Right, Group = "==== Weis & Wyckoff System v2.0 ====")]
        public PanelAlign_Data PanelAlign_Input { get; set; }


        [Parameter("[Candles] Use 'Tick Volume' from bars?", DefaultValue = true, Group = "==== Specific Parameters ====")]
        public bool UseTimeBasedVolume { get; set; }

        [Parameter("[Wyckoff] Use Custom MAs?", DefaultValue = true, Group = "==== Specific Parameters ====")]
        public bool UseCustomMAs { get; set; }

        [Parameter("[Renko] Show Wicks?", DefaultValue = false, Group = "==== Specific Parameters ====")]
        public bool ShowWicks { get; set; }

        [Parameter("[Renko] Wicks Thickness:", DefaultValue = 1, MaxValue = 5, Group = "==== Specific Parameters ====")]
        public int RenkoThickness { get; set; }

        [Parameter("[ZZ] ATR Multiplier", DefaultValue = 2, MinValue = 0, MaxValue = 10, Group = "==== Specific Parameters ====")]
        public double ATR_Multiplier { get; set; }

        [Parameter("[ZZ] ATR Period", DefaultValue = 10, MinValue = 1, Group = "==== Specific Parameters ====")]
        public int ATR_Period { get; set; }

        public enum StorageKeyConfig_Data
        {
            Symbol_Timeframe,
            Broker_Symbol_Timeframe
        }
        [Parameter("Storage By:", DefaultValue = StorageKeyConfig_Data.Broker_Symbol_Timeframe, Group = "==== Weis & Wyckoff System v2.0 ====")]
        public StorageKeyConfig_Data StorageKeyConfig_Input { get; set; }

        [Parameter("Draw At Zoom(%)", DefaultValue = 40, Group = "==== Performance Drawing ====")]
        public int DrawAtZoom_Value { get; set; }
        public enum DrawingStrategy_Data
        {
            Hidden_Slowest,
            Redraw_Fastest
        }
        [Parameter("Drawing Strategy", DefaultValue = DrawingStrategy_Data.Redraw_Fastest, Group = "==== Performance Drawing ====")]
        public DrawingStrategy_Data DrawingStrategy_Input { get; set; }

        [Parameter("[Debug] Show Count?:", DefaultValue = false , Group = "==== Performance Drawing ====")]
        public bool ShowDrawingInfo { get; set; }


        [Parameter("Format Numbers?", DefaultValue = true, Group = "==== Numbers ====")]
        public bool FormatNumbers { get; set; }

        public enum FormatMaxDigits_Data
        {
            Zero,
            One,
            Two,
        }
        [Parameter("Format Max Digits:", DefaultValue = FormatMaxDigits_Data.One, Group = "==== Numbers ====")]
        public FormatMaxDigits_Data FormatMaxDigits_Input { get; set; }

        [Parameter("Font Size [Bars]:", DefaultValue = 11, MinValue = 1, MaxValue = 80, Group = "==== Numbers ====")]
        public int FontSizeNumbers { get; set; }

        [Parameter("Font Size [Waves]:", DefaultValue = 12, MinValue = 1, MaxValue = 80, Group = "==== Numbers ====")]
        public int FontSizeWaves { get; set; }

        [Parameter("Custom Color:", DefaultValue = "White", Group = "==== Numbers ====")]
        public Color CustomNumbersColor { get; set; }


        [Parameter("Show TrendLines?", DefaultValue = true, Group = "==== Trend Lines Settings ====")]
        public bool ShowTrendLines { get; set; }

        [Parameter("Up/Down Coloring?", DefaultValue = false, Group = "==== Trend Lines Settings ====")]
        public bool ColorfulTrendLines { get; set; }

        [Parameter("Large Wave Coloring?", DefaultValue = true, Group = "==== Trend Lines Settings ====")]
        public bool ShowYellowTrendLines { get; set; }

        [Parameter("Thickness", DefaultValue = 3, MinValue = 1, Group = "==== Trend Lines Settings ====")]
        public int TrendThickness { get; set; }

        [Parameter("NoTrend Line Color", DefaultValue = "SteelBlue", Group = "==== Trend Lines Settings ====")]
        public Color NoTrendColor { get; set; }

        [Parameter("UpTrend Line Color", DefaultValue = "Green", Group = "==== Trend Lines Settings ====")]
        public Color UpLineColor { get; set; }

        [Parameter("DownTrend Line Color", DefaultValue = "Red", Group = "==== Trend Lines Settings ====")]
        public Color DownLineColor { get; set; }


        [Parameter("[ZigZag] Show Turning Point Bar?", DefaultValue = false, Group = "==== Debug ====")]
        public bool ShowTurningPoint { get; set; }
        [Parameter("[ZigZag] Invert Turning Color?", DefaultValue = true, Group = "==== Debug ====")]
        public bool InvertTurningColor { get; set; }
        [Parameter("[Weis] Show Wave Ratio?", DefaultValue = false, Group = "==== Debug ====")]
        public bool ShowRatioValue { get; set; }


        [Parameter("Opacity(%) [Nº Inside]:", DefaultValue = 70, MinValue = 1, MaxValue = 100, Group = "==== HeatMap Coloring ====")]
        public int HeatmapBars_Opacity { get; set; }

        [Parameter("Lowest Color:", DefaultValue = "#FF737373", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapLowest_Color { get; set; }

        [Parameter("Low Color:", DefaultValue = "#8F9092", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapLow_Color { get; set; }

        [Parameter("Average Color:", DefaultValue = "#D9D9D9", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapAverage_Color { get; set; }

        [Parameter("High Color [Up]:", DefaultValue = "#A1F6A1", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapHighUp_Color { get; set; }
        [Parameter("High Color [Down]:", DefaultValue = "#FA6681", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapHighDown_Color { get; set; }

        [Parameter("Ultra Color[Up]:", DefaultValue = "#1D8934", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapUltraUp_Color { get; set; }
        [Parameter("Ultra Color[Down]:", DefaultValue = "#E00106", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapUltraDown_Color { get; set; }


        [Parameter("Up Wave Color", DefaultValue = "SeaGreen", Group = "==== Waves Color ====")]
        public Color UpWaveColor { get; set; }

        [Parameter("Down Wave Color", DefaultValue = "OrangeRed", Group = "==== Waves Color ====")]
        public Color DownWaveColor { get; set; }

        [Parameter("Large Wave Color", DefaultValue = "Yellow", Group = "==== Waves Color ====")]
        public Color LargeWaveColor { get; set; }


        [Parameter("Transcribed & Improved", DefaultValue = "for cTrader/C# by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }
        [Parameter("Original Code Concepts", DefaultValue = "in TDV/Pinescript by akutsusho", Group = "==== Credits ====")]
        public string Credits_2 { get; set; }


        // Moved from cTrader Input to Params Panel
        public enum Template_Data
        {
            Insider,
            Volume,
            Time,
            BigBrain,
            Custom
        }
        public Template_Data Template_Input = Template_Data.Insider;

        // Wyckoff Bars
        public bool EnableWyckoff = true;
        public enum Numbers_Data
        {
            Both,
            Volume,
            Time,
            None
        }
        public Numbers_Data Numbers_Input = Numbers_Data.Both;

        public enum NumbersPosition_Data
        {
            Inside,
            Outside,
        }
        public NumbersPosition_Data NumbersPosition_Input = NumbersPosition_Data.Inside;

        public enum NumbersBothPosition_Data
        {
            Default,
            Invert,
        }
        public NumbersBothPosition_Data NumbersBothPosition_Input = NumbersBothPosition_Data.Default;

        public enum NumbersColor_Data
        {
            Volume,
            Time,
            CustomColor
        }
        public NumbersColor_Data NumbersColor_Input = NumbersColor_Data.Volume;

        public enum BarsColor_Data
        {
            Volume,
            Time,
        }
        public BarsColor_Data BarsColor_Input = BarsColor_Data.Volume;

        public bool FillBars = true;
        public bool KeepOutline = false;
        public bool ShowOnlyLargeNumbers = false;

        public enum StrengthFilter_Data
        {
            MA,
            Standard_Deviation,
            Both,
            Normalized_Emphasized,
            L1Norm
        }
        public StrengthFilter_Data StrengthFilter_Input = StrengthFilter_Data.MA;
        
        public enum StrengthRatio_Data
        {
            Fixed,
            Percentile,
        }        
        public StrengthRatio_Data StrengthRatio_Input = StrengthRatio_Data.Percentile;
        public MovingAverageType MAtype = MovingAverageType.Exponential;
        public int MAperiod = 5;
        public int Pctile_Period = 20;
        public int NormalizePeriod = 5;
        public int NormalizeMultiplier = 10;
        public bool ShowStrengthValue = false;

        // Fixed Ratio
        public double Lowest_FixedValue = 0.5;
        public double Low_FixedValue = 1;
        public double Average_FixedValue = 1.5;
        public double High_FixedValue = 2;
        public double Ultra_FixedValue = 2.01;

        // Percentile Ratio
        public int Lowest_PctileValue = 40;
        public int Low_PctileValue = 70;
        public int Average_PctileValue = 90;
        public int High_PctileValue = 97;
        public int Ultra_PctileValue = 99;

        // Normalized_Emphasized ratio
        public double Lowest_PctValue = 23.6;
        public double Low_PctValue = 38.2;
        public double Average_PctValue = 61.8;
        public double High_PctValue = 100;
        public double Ultra_PctValue = 101;


        // Weis Waves
        public bool ShowCurrentWave = true;

        public enum ShowWaves_Data
        {
            No,
            Both,
            Volume,
            EffortvsResult
        }
        public ShowWaves_Data ShowWaves_Input = ShowWaves_Data.EffortvsResult;

        public enum ShowOtherWaves_Data
        {
            No,
            Both,
            Price,
            Time
        }
        public ShowOtherWaves_Data ShowOtherWaves_Input = ShowOtherWaves_Data.Both;

        public enum ShowMarks_Data
        {
            No,
            Both,
            Left,
            Right
        }
        public ShowMarks_Data ShowMarks_Input = ShowMarks_Data.No;

        public double EvsR_Ratio = 1.5;
        public double WW_Ratio = 1.7;

        // ZigZag
        public enum WavesMode_Data
        {
            Reversal,
            ZigZag,
        }
        public WavesMode_Data WavesMode_Input = WavesMode_Data.ZigZag;

        public enum YellowZigZag_Data
        {
            UsePrev_SameWave,
            UsePrev_InvertWave,
            UseCurrent
        }
        public YellowZigZag_Data YellowZigZag_Input = YellowZigZag_Data.UseCurrent;
        public bool YellowRenko_IgnoreRanging = false;

        public enum ZigZagMode_Data
        {
            ATR,
            Percentage,
            Pips,
            NoLag_HighLow
        }
        public ZigZagMode_Data ZigZagMode_Input = ZigZagMode_Data.NoLag_HighLow;

        public double PercentageZZ = 0.01;
        public double PipsZZ = 0.1;

        public enum Priority_Data {
            None,
            Auto,
            Skip
        }
        public Priority_Data Priority_Input = Priority_Data.None;

        public enum ZigZagSource_Data {
            Current,
            MultiTF
        }
        public ZigZagSource_Data ZigZagSource_Input = ZigZagSource_Data.Current;
        public TimeFrame MTFSource_TimeFrame = TimeFrame.Minute30;
        public MTF_Sources MTFSource_Panel = MTF_Sources.Standard;


        // ==== Weis Wave & Wyckoff System ====
        public readonly string NOTIFY_CAPTION = "Weis & Wyckoff System";
        private IndicatorDataSeries TimeSeries;
        private IndicatorDataSeries StrengthSeries_Vol;
        private IndicatorDataSeries StrengthSeries_Time;
        private MovingAverage MATime, MAVol;
        private StandardDeviation stdDev_Time, stdDev_Vol;

        double[] prevWave_Up = { 0, 0 };
        double[] prevWave_Down = { 0, 0 };
        // Volume/Cumulative Renko = EvsR
        double[] prevWaves_EvsR = { 0, 0, 0, 0 };
        // onlyVolume = Large WW
        double[] prevWaves_Volume = { 0, 0, 0, 0 };

        public bool isRenkoChart = false;
        public bool isTickChart = false;
        public bool isPriceBased_Chart = false;
        public bool isPriceBased_NewBar = false;
        private bool isLargeWave_EvsR = false;
        private bool lockMTFNotify = false;
        private ChartTrendLine PrevWave_TrendLine;

        // Zig Zag
        private enum Direction
        {
            UP,
            DOWN
        }
        private Direction direction = Direction.DOWN;
        private double extremumPrice = 0.0;
        private int extremumIndex = 0;
        private int trendStartIndex = 0;
        private Bars _m1Bars;
        private Bars MTFSource_Bars;
        private AverageTrueRange _ATR;

        [Output("ZigZag", LineColor = "DeepSkyBlue", Thickness = 2, PlotType = PlotType.Line)]
        public IndicatorDataSeries ZigZagBuffer { get; set; }

        // ==== VolumeRenkoRange (Tick Volume) ====
        private IndicatorDataSeries VolumeSeries;
        private DateTime firstTickTime;
        private DateTime fromDateTime;
        private Bars TicksOHLC;
        private ProgressBar syncTickProgressBar = null;
        PopupNotification asyncTickPopup = null;
        private bool loadingAsyncTicks = false;
        private bool loadingTicksComplete = false;

        // ==== Renko Wicks ====
        private Color UpWickColor;
        private Color DownWickColor;

        // High-Performance Tick
        private int lastTick_Wicks = 0;
        private int lastTick_Bars = 0;

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
        public MAType_Data customMAtype = MAType_Data.Triangular;

        private readonly Dictionary<int, double> _dynamicBuffer = new();
        private readonly Dictionary<int, double> _maDynamic = new();

        // Performance Drawing
        // Disable X2, Y2 and IconType "never assigned" warning
        #pragma warning disable CS0649
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

        private bool lockDrawRemove = false;
        // Params Panel
        private Border ParamBorder;

        public class IndicatorParams
        {
            public Template_Data Template { get; set; }

            // Wyckoff Bars
            public bool EnableWyckoff { get; set; }
            public Numbers_Data ShowNumbers { get; set; }
            public NumbersPosition_Data NumbersPosition { get; set; }
            public NumbersBothPosition_Data NumbersBothPosition { get; set; }
            public NumbersColor_Data NumbersColor { get; set; }
            public BarsColor_Data BarsColor { get; set; }
            public bool ShowOnlyLargeNumbers { get; set; }
            public bool FillBars { get; set; }
            public bool KeepOutline { get; set; }

            public StrengthFilter_Data StrengthFilter { get; set; }
            public MovingAverageType MAtype { get; set; }
            public int MAperiod { get; set; }
            public int NormalizePeriod { get; set; }
            public int NormalizeMultiplier { get; set; }

            public int PctilePeriod { get; set; }
            public StrengthRatio_Data StrengthRatio { get; set; }
            public bool ShowStrength { get; set; }
            
            public double Lowest_Pctile { get; set; }
            public double Low_Pctile { get; set; }
            public double Average_Pctile { get; set; }
            public double High_Pctile { get; set; }
            public double Ultra_Pctile { get; set; }

            
            public double Lowest_Pct { get; set; }
            public double Low_Pct { get; set; }
            public double Average_Pct { get; set; }
            public double High_Pct { get; set; }
            public double Ultra_Pct { get; set; }

            
            public double Lowest_Fixed { get; set; }
            public double Low_Fixed { get; set; }
            public double Average_Fixed { get; set; }
            public double High_Fixed { get; set; }
            public double Ultra_Fixed { get; set; }

            // Weis Waves
            public bool ShowCurrentWave { get; set; }
            public ShowWaves_Data ShowWaves { get; set; }
            public ShowOtherWaves_Data ShowOtherWaves { get; set; }
            public ShowMarks_Data ShowMarks { get; set; }
            public WavesMode_Data WavesMode { get; set; }

            public double EvsR_Ratio { get; set; }
            public double WW_Ratio { get; set; }

            public YellowZigZag_Data YellowZigZag { get; set; }
            public bool YellowRenko_IgnoreRanging { get; set; }

            // ZigZag
            public ZigZagMode_Data ZigZagMode { get; set; }
            public double PercentageZZ { get; set; }
            public double PipsZZ { get; set; }
            public Priority_Data Priority { get; set; }
            public ZigZagSource_Data ZigZagSource { get; set; }
            public TimeFrame MTFSource_TimeFrame { get; set; }

            public MTF_Sources MTFSource_Panel  { get; set; }
        }
        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "WWS",
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

        protected override void Initialize()
        {
            string currentTimeframe = Chart.TimeFrame.ToString();
            isRenkoChart = currentTimeframe.Contains("Renko");
            isTickChart = currentTimeframe.Contains("Tick");
            isPriceBased_Chart = currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick");
            if (isPriceBased_Chart) {
                Bars.BarOpened += (_) => {
                    isPriceBased_NewBar = true;
                    lockDrawRemove = false;
                };
            }
            // Performance Drawing
            Chart.ZoomChanged += PerformanceDrawing;
            Chart.ScrollChanged += PerformanceDrawing;
            Bars.BarOpened += LiveDrawing;

            // Predefined Config
            Design_Templates();
            SpecificChart_Templates();
            DrawingConflict();

            // VolumeRenkoRange / Renko Wicks
            TicksOHLC = MarketData.GetBars(TimeFrame.Tick);
            VolumeSeries = CreateDataSeries();

            if (!UseTimeBasedVolume && !isPriceBased_Chart || isPriceBased_Chart) {
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
            }

            // Renko Wicks
            UpWickColor = Chart.ColorSettings.BullOutlineColor;
            DownWickColor = Chart.ColorSettings.BearOutlineColor;

            // WyckoffAnalysis()
            TimeSeries = CreateDataSeries();
            StrengthSeries_Vol = CreateDataSeries();
            StrengthSeries_Time = CreateDataSeries();

            if (!UseCustomMAs) {
                MATime = Indicators.MovingAverage(TimeSeries, MAperiod, MAtype);
                MAVol = Indicators.MovingAverage(VolumeSeries, MAperiod, MAtype);
                stdDev_Vol = Indicators.StandardDeviation(VolumeSeries, MAperiod, MAtype);
                stdDev_Time = Indicators.StandardDeviation(TimeSeries, MAperiod, MAtype);
            }

            // WeisWaveAnalysis()
            _ATR = Indicators.AverageTrueRange(ATR_Period, MovingAverageType.Weighted);
            _m1Bars = MarketData.GetBars(TimeFrame.Minute);
            MTFSource_Bars = MarketData.GetBars(MTFSource_TimeFrame);

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
                Template = Template_Input,

                // Wyckoff Bars
                EnableWyckoff = EnableWyckoff,
                ShowNumbers = Numbers_Input,
                NumbersPosition = NumbersPosition_Input,
                NumbersBothPosition = NumbersBothPosition_Input,
                NumbersColor = NumbersColor_Input,
                BarsColor = BarsColor_Input,
                FillBars = FillBars,
                KeepOutline = KeepOutline,
                ShowOnlyLargeNumbers = ShowOnlyLargeNumbers,

                // Coloring
                StrengthFilter = StrengthFilter_Input,
                MAtype = MAtype,
                MAperiod = MAperiod,
                NormalizePeriod = NormalizePeriod,
                NormalizeMultiplier = NormalizeMultiplier,
                PctilePeriod = Pctile_Period,
                StrengthRatio = StrengthRatio_Input,
                ShowStrength = ShowStrengthValue,
                
                Lowest_Pctile = Lowest_PctileValue,
                Low_Pctile = Low_PctileValue,
                Average_Pctile = Average_PctileValue,
                High_Pctile = High_PctileValue,
                Ultra_Pctile = Ultra_PctileValue,
                
                Lowest_Pct = Lowest_PctValue,
                Low_Pct = Low_PctValue,
                Average_Pct = Average_PctValue,
                High_Pct = High_PctValue,
                Ultra_Pct = Ultra_PctValue,

                Lowest_Fixed = Lowest_FixedValue,
                Low_Fixed = Low_FixedValue,
                Average_Fixed = Average_FixedValue,
                High_Fixed = High_FixedValue,
                Ultra_Fixed = Ultra_FixedValue,
                
                // Weis Waves
                ShowCurrentWave = ShowCurrentWave,
                ShowWaves = ShowWaves_Input,
                ShowOtherWaves = ShowOtherWaves_Input,
                ShowMarks = ShowMarks_Input,
                WavesMode = WavesMode_Input,
                EvsR_Ratio = EvsR_Ratio,
                WW_Ratio = WW_Ratio,
                YellowZigZag = YellowZigZag_Input,
                YellowRenko_IgnoreRanging = YellowRenko_IgnoreRanging,

                // ZigZag
                ZigZagMode = ZigZagMode_Input,
                PercentageZZ = PercentageZZ,
                PipsZZ = PipsZZ,
                Priority = Priority_Input,
                ZigZagSource = ZigZagSource_Input,

                MTFSource_TimeFrame = MTFSource_TimeFrame,
                MTFSource_Panel = MTFSource_Panel
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

            var stackPanel = new StackPanel
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
            };
            AddHiddenButton(stackPanel, Color.FromHex("#7F808080"));
            Chart.AddControl(stackPanel);
        }

        public override void Calculate(int index)
        {
            if (!UseTimeBasedVolume && !isPriceBased_Chart || isPriceBased_Chart) {
                // Tick Data Collection on chart
                bool isOnChart = LoadTickStrategy_Input != LoadTickStrategy_Data.At_Startup_Sync;
                if (isOnChart && !loadingTicksComplete)
                    LoadMoreTicksOnChart();

                bool isOnChartAsync = LoadTickStrategy_Input == LoadTickStrategy_Data.On_ChartEnd_Async;
                if (isOnChartAsync && !loadingTicksComplete)
                    return;

                if (index < Bars.OpenTimes.GetIndexByTime(TicksOHLC.OpenTimes.FirstOrDefault())) {
                    Chart.SetBarColor(index, HeatmapLowest_Color);
                    return;
                }
            }

            // Removing Messages
            if (!IsLastBar)
                DrawOnScreen("");

            // VolumeRR
            if (UseTimeBasedVolume && !isPriceBased_Chart)
                VolumeSeries[index] = Bars.TickVolumes[index];
            else
                VolumeSeries[index] = Get_Volume_or_Wicks(index, true)[2];

            // ==== Wyckoff ====
            if (EnableWyckoff)
                WyckoffAnalysis(index);

            // ==== Weis Wave ====
            try { WeisWaveAnalysis(index); } catch {
                if (ZigZagSource_Input == ZigZagSource_Data.MultiTF && !lockMTFNotify) {
                    Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"ERROR => ZigZag MTF(source): \nCannot use {MTFSource_TimeFrame.ShortName} interval for {Chart.TimeFrame.ShortName} chart \nThe interval is probably too short?",
                        PopupNotificationState.Error
                    );
                    lockMTFNotify = true;
                }
            }

            // ==== Renko Wicks ====
            if (ShowWicks && isRenkoChart)
                RenkoWicks(index);

            isPriceBased_NewBar = false;
        }

        private void Design_Templates() {
            switch (Template_Input)
            {
                case Template_Data.Insider:
                    Numbers_Input = Numbers_Data.Both;
                    NumbersColor_Input = NumbersColor_Data.Volume;
                    NumbersPosition_Input = NumbersPosition_Data.Inside;
                    BarsColor_Input = BarsColor_Data.Volume;

                    ShowWaves_Input = ShowWaves_Data.EffortvsResult;
                    ShowOtherWaves_Input = ShowOtherWaves_Data.Both;
                    ShowMarks_Input = ShowMarks_Data.No;

                    EnableWyckoff = true;
                    ShowCurrentWave = true;
                    FillBars = true;
                    KeepOutline = false;
                    Chart.ChartType = ChartType.Candlesticks;
                    break;
                case Template_Data.Volume:
                    Numbers_Input = Numbers_Data.Volume;
                    NumbersColor_Input = NumbersColor_Data.Volume;
                    NumbersPosition_Input = NumbersPosition_Data.Inside;
                    BarsColor_Input = BarsColor_Data.Volume;

                    ShowWaves_Input = ShowWaves_Data.Volume;
                    ShowOtherWaves_Input = ShowOtherWaves_Data.Price;
                    ShowMarks_Input = ShowMarks_Data.No;

                    EnableWyckoff = true;
                    ShowCurrentWave = true;
                    FillBars = true;
                    KeepOutline = false;
                    Chart.ChartType = ChartType.Candlesticks;
                    break;
                case Template_Data.Time:
                    Numbers_Input = Numbers_Data.Time;
                    NumbersColor_Input = NumbersColor_Data.Time;
                    NumbersPosition_Input = NumbersPosition_Data.Inside;
                    BarsColor_Input = BarsColor_Data.Time;

                    ShowWaves_Input = ShowWaves_Data.EffortvsResult;
                    ShowOtherWaves_Input = ShowOtherWaves_Data.Time;
                    ShowMarks_Input = ShowMarks_Data.No;

                    EnableWyckoff = true;
                    ShowCurrentWave = true;
                    FillBars = true;
                    KeepOutline = false;
                    Chart.ChartType = ChartType.Candlesticks;
                    break;
                case Template_Data.BigBrain:
                    Numbers_Input = Numbers_Data.Both;
                    NumbersColor_Input = NumbersColor_Data.Volume;
                    NumbersPosition_Input = NumbersPosition_Data.Inside;
                    BarsColor_Input = BarsColor_Data.Time;

                    ShowWaves_Input = ShowWaves_Data.Both;
                    ShowOtherWaves_Input = ShowOtherWaves_Data.Both;
                    ShowMarks_Input = ShowMarks_Data.Both;

                    EnableWyckoff = true;
                    ShowCurrentWave = true;
                    FillBars = true;
                    KeepOutline = false;
                    Chart.ChartType = ChartType.Hlc;
                    break;
                default: break;
            }
        }
        private void SpecificChart_Templates(bool isInit = true) {
            if (Template_Input == Template_Data.Custom)
                return;
            // Tick / Time-Based Chart (Standard Candles/Heikin-Ash)
            if (isTickChart || !isPriceBased_Chart) {
                if (isTickChart) {
                    if (isInit) Numbers_Input = Numbers_Data.Time;
                    MTFSource_TimeFrame = TimeFrame.Tick100;
                    MTFSource_Panel = MTF_Sources.Tick;
                } else {
                    if (isInit) Numbers_Input = Numbers_Data.Volume;
                    MTFSource_TimeFrame = TimeFrame.Minute30;
                    MTFSource_Panel = MTF_Sources.Standard;
                }
            }
            // Range
            if (isPriceBased_Chart && !isRenkoChart && !isTickChart) {
                if (isInit) Numbers_Input = Numbers_Data.Volume;
                StrengthFilter_Input = StrengthFilter_Data.MA;
                MAperiod = 20;
                MAtype = MovingAverageType.Triangular;
                Lowest_FixedValue = 0.5;
                Low_FixedValue = 1.2;
                Average_FixedValue = 2.5;
                High_FixedValue = 3.5;
                Ultra_FixedValue = 3.51;

                MTFSource_TimeFrame = TimeFrame.Range10;
                MTFSource_Panel = MTF_Sources.Range;
            }
            if (isRenkoChart) {
                MTFSource_TimeFrame = TimeFrame.Renko5;
                MTFSource_Panel = MTF_Sources.Renko;
            }
        }
        private void DrawingConflict() {
            if (NumbersPosition_Input == NumbersPosition_Data.Outside)
            {
                /* It's a combination that won't be used,
                   and btw drawing this combination is a little tiring,
                   I left it out. */
                if (Numbers_Input == Numbers_Data.Both && (ShowWaves_Input == ShowWaves_Data.Both || ShowWaves_Input == ShowWaves_Data.Volume || ShowWaves_Input == ShowWaves_Data.EffortvsResult))
                {
                    Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        "WAVES POSITIONS are not optimized for BOTH/OUTSIDE NUMBERS, setting to VOLUME/OUTSIDE NUMBERS instead",
                        PopupNotificationState.Error
                    );
                    Numbers_Input = Numbers_Data.Volume;
                }
                else if (Numbers_Input == Numbers_Data.Both && (ShowOtherWaves_Input == ShowOtherWaves_Data.Both || ShowOtherWaves_Input == ShowOtherWaves_Data.Price || ShowOtherWaves_Input == ShowOtherWaves_Data.Time))
                {
                    Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        "WAVES POSITIONS are not optimized for BOTH/OUTSIDE NUMBERS, setting to VOLUME/OUTSIDE NUMBERS instead",
                        PopupNotificationState.Error
                    );
                    Numbers_Input = Numbers_Data.Volume;
                }
            }
        }
        private void WyckoffAnalysis(int rawindex)
        {
            int index = rawindex;
            if (index < 2)
                return;

            // ==== Time Filter ====
            DateTime openTime = Bars.OpenTimes[index];
            DateTime closeTime = Bars.OpenTimes[index + 1];
            if (IsLastBar)
                closeTime = UseTimeBasedVolume && !isPriceBased_Chart ? Server.Time : TicksOHLC.OpenTimes.LastValue;
            TimeSpan interval = closeTime.Subtract(openTime);
            double interval_ms = interval.TotalMilliseconds;

            // Dynamic TimeLapse Format
            string[] interval_timelapse = GetTimeLapse(interval_ms);
            double timelapse_Value = Convert.ToDouble(interval_timelapse[0]);
            string timelapse_Suffix = interval_timelapse[1];

            TimeSeries[index] = timelapse_Value;
            
            // ==== Strength Filter ====
            double volume = VolumeSeries[index];
            double time = TimeSeries[index];
            double volumeStrength = 0;
            double timeStrength = 0;
            switch (StrengthFilter_Input) 
            {
                case StrengthFilter_Data.MA: {
                    double maValue = UseCustomMAs ? CustomMAs(volume, index, MAperiod, customMAtype) : MAVol.Result[index];
                    volumeStrength = volume / maValue;
                    // ========
                    maValue = UseCustomMAs ? CustomMAs(time, index, MAperiod, customMAtype) : MATime.Result[index];
                    timeStrength = time / maValue;
                    break;
                }
                case StrengthFilter_Data.Standard_Deviation: {
                    double  stddevValue = UseCustomMAs ? CustomMAs(volume, index, MAperiod, customMAtype, true, VolumeSeries) : stdDev_Vol.Result[index];
                    volumeStrength = volume / stddevValue; 
                    // ========
                    stddevValue = UseCustomMAs ? CustomMAs(time, index, MAperiod, customMAtype, true, TimeSeries) : stdDev_Time.Result[index];
                    timeStrength = time / stddevValue;
                    break;
                }
                case StrengthFilter_Data.Both: {
                    double maValue = UseCustomMAs ? CustomMAs(volume, index, MAperiod, customMAtype) : MAVol.Result[index];
                    double stddevValue = UseCustomMAs ? CustomMAs(volume, index, MAperiod, customMAtype, true, VolumeSeries) : stdDev_Vol.Result[index];
                    volumeStrength = (volume - maValue) / stddevValue; 
                    // ========
                    maValue = UseCustomMAs ? CustomMAs(time, index, MAperiod, customMAtype) : MATime.Result[index];
                    stddevValue = UseCustomMAs ? CustomMAs(time, index, MAperiod, customMAtype, true, TimeSeries) : stdDev_Time.Result[index];
                    timeStrength = (time - maValue) / stddevValue; 
                    break;
                }
                case StrengthFilter_Data.Normalized_Emphasized:
                    double Normalization(bool isTime = false) {
                        /*
                        ==== References for Normalized_Emphasized ====
                        (Normalized Volume Oscillator 2008/2014) (https://www.mql5.com/en/code/8208)
                        // (The key idea for normalized volume by average volume period)
                        (Volumes Emphasized.mq4) (???)
                        // (improvement of above indicator)

                        It seems to be... the most suitable filter approach for Time-Based Charts, without Candle Spread Analysis.
                        Since CFD's Volume can be very flat at higher Tick activity,
                        - the slightest value change will be highlighted... as in ODF_Ticks/AGG.
                        */
                        if (index < NormalizePeriod)
                            return 0;

                        double avg = 0;
                        for (int j = index; j > index - NormalizePeriod; j--) {
                            if (isTime)
                                avg += TimeSeries[j];
                            else
                                avg += VolumeSeries[j];
                        }

                        avg /= NormalizePeriod;

                        double normalizedValue = isTime ? (time / avg) : (volume / avg);
                        double normalizedPercentage = (normalizedValue * 100) - 100;
                        normalizedPercentage *= NormalizeMultiplier; // I've added this to get "less but meaningful" coloring

                        return normalizedPercentage;
                    }
                    volumeStrength = Normalization();
                    timeStrength = Normalization(true);
                    break;
                case StrengthFilter_Data.L1Norm:
                    double[] window = new double[MAperiod];

                    for (int i = 0; i < MAperiod; i++)
                        window[i] = VolumeSeries[index - MAperiod + 1 + i];

                    volumeStrength = L1NormStrength(window);
                    timeStrength = L1NormStrength(window);
                    break;
            }
            
            // Keep negative values of Normalized_Emphasized
            if (StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized) {
                volumeStrength = Math.Abs(volumeStrength);
                timeStrength = Math.Abs(timeStrength);
            }
            
            volumeStrength = Math.Round(volumeStrength, 2);
            timeStrength = Math.Round(timeStrength, 2);

            if (StrengthRatio_Input == StrengthRatio_Data.Percentile && StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized) 
            {        
                StrengthSeries_Vol[index] = volumeStrength;
                StrengthSeries_Time[index] = timeStrength;
                
                double[] windowVol = new double[Pctile_Period];
                double[] windowTime = new double[Pctile_Period];
                
                for (int i = 0; i < Pctile_Period; i++) {
                    windowVol[i] = StrengthSeries_Vol[index - Pctile_Period + 1 + i];
                    windowTime[i] = StrengthSeries_Time[index - Pctile_Period + 1 + i];
                }

                volumeStrength = RollingPercentile(windowVol);
                volumeStrength = Math.Round(volumeStrength, 1);
                // ========
                timeStrength = RollingPercentile(windowTime);
                timeStrength = Math.Round(timeStrength, 1);
            }

            // ==== Drawing ====
            // Y-Axis
            bool isBullish = Bars.ClosePrices[index] > Bars.OpenPrices[index];
            double y_Close = Bars.ClosePrices[index];
            double y_Open = Bars.OpenPrices[index];

            // Coloring
            double colorTypeNumbers = NumbersColor_Input == NumbersColor_Data.Time ? timeStrength : volumeStrength;
            double colorTypeBars = BarsColor_Input== BarsColor_Data.Time ? timeStrength : volumeStrength;
            bool isNumbersOutside = NumbersPosition_Input == NumbersPosition_Data.Outside || Numbers_Input == Numbers_Data.None;

            int alpha = (int)(2.55 * HeatmapBars_Opacity);
            Color lowestColor = isNumbersOutside ? HeatmapLowest_Color : Color.FromArgb(alpha, HeatmapLowest_Color);
            Color lowColor = isNumbersOutside ? HeatmapLow_Color : Color.FromArgb(alpha, HeatmapLow_Color);
            Color averageColor = isNumbersOutside ? HeatmapAverage_Color : Color.FromArgb(alpha, HeatmapAverage_Color);

            Color highColorUp = isNumbersOutside ? HeatmapHighUp_Color : Color.FromArgb(alpha, HeatmapHighUp_Color);
            Color highColorDown = isNumbersOutside ? HeatmapHighDown_Color : Color.FromArgb(alpha, HeatmapHighDown_Color);
            Color highColor = isBullish ? highColorUp : highColorDown;

            Color ultraColorUp = isNumbersOutside ? HeatmapUltraUp_Color : Color.FromArgb(alpha, HeatmapUltraUp_Color);
            Color ultraColorDown = isNumbersOutside ? HeatmapUltraDown_Color : Color.FromArgb(alpha, HeatmapUltraDown_Color);
            Color ultraColor = isBullish ? ultraColorUp : ultraColorDown;

            if (StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized) {
                // if negative, just to be sure.
                colorTypeBars = colorTypeBars < 0 ? 0 : colorTypeBars;
                colorTypeNumbers = colorTypeNumbers < 0 ? 0 : colorTypeNumbers;
            }

            // Ratio
            bool isFixed = StrengthRatio_Input == StrengthRatio_Data.Fixed;

            double lowest = isFixed ? Lowest_FixedValue : Lowest_PctileValue;
            double low = isFixed ? Low_FixedValue : Low_PctileValue;
            double average = isFixed ? Average_FixedValue : Average_PctileValue;
            double high = isFixed ? High_FixedValue : High_PctileValue;
            double ultra = isFixed ? Ultra_FixedValue : Ultra_PctileValue;

            if (StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized) {
                lowest = Lowest_PctValue;
                low = Low_PctValue;
                average = Average_PctValue;
                high = High_PctValue;
                ultra = Ultra_PctValue;
            }

            Color barColor = colorTypeBars < lowest ? lowestColor :
                             colorTypeBars < low ? lowColor :
                             colorTypeBars < average ? averageColor :
                             colorTypeBars < high ? highColor :
                             colorTypeBars >= ultra ? ultraColor : lowestColor;

            highColor = isBullish ? HeatmapHighUp_Color : HeatmapHighDown_Color;
            ultraColor = isBullish ? HeatmapUltraUp_Color : HeatmapUltraDown_Color;
            Color numberColor = colorTypeNumbers < lowest ? HeatmapLowest_Color :
                                colorTypeNumbers < low ? HeatmapLow_Color :
                                colorTypeNumbers < average ? HeatmapAverage_Color :
                                colorTypeNumbers < high ? highColor :
                                colorTypeNumbers >= ultra ? ultraColor : HeatmapLowest_Color;

            // Numbers
            timelapse_Value = Math.Round(timelapse_Value);
            string onlyTime = ShowOnlyLargeNumbers ?
                              (timeStrength > low ? timelapse_Value + timelapse_Suffix : "") :
                              timelapse_Value + timelapse_Suffix;

            string onlyVol = ShowOnlyLargeNumbers ?
                             (volumeStrength > low ? FormatBigNumber(volume) : "") :
                             FormatBigNumber(volume);

            string bothVolTime = NumbersBothPosition_Input == NumbersBothPosition_Data.Default ? $"{onlyTime}\n{onlyVol}" : $"{onlyVol}\n{onlyTime}";

            string numbersFmtd = Numbers_Input == Numbers_Data.Time ? onlyTime :
                                 Numbers_Input == Numbers_Data.Volume ? onlyVol :
                                 Numbers_Input == Numbers_Data.Both ? bothVolTime : "";

            if (numbersFmtd != "")
            {
                double y1 = isBullish ? y_Close : y_Open;
                VerticalAlignment v_align = VerticalAlignment.Bottom;
                HorizontalAlignment h_align;

                if (Chart.ChartType != ChartType.Bars && Chart.ChartType != ChartType.Hlc)
                {
                    if (NumbersPosition_Input == NumbersPosition_Data.Outside && isBullish)
                        v_align = VerticalAlignment.Top;

                    if (!isBullish) {
                        v_align = VerticalAlignment.Bottom;
                        if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                            y1 = y_Close;
                    }

                    h_align = HorizontalAlignment.Center;
                }
                else {
                    h_align = HorizontalAlignment.Stretch;
                    if (!isBullish) {
                        v_align = VerticalAlignment.Top;
                        y1 = y_Close;
                    }
                }

                DrawOrCache(new DrawInfo
                {
                    BarIndex = index,
                    Type = DrawType.Text,
                    Id = $"{index}_wyckoff",
                    Text = numbersFmtd,
                    X1 = Bars[index].OpenTime,
                    Y1 = y1,
                    horizontalAlignment = h_align,
                    verticalAlignment = v_align,
                    FontSize = FontSizeNumbers,
                    Color = NumbersColor_Input == NumbersColor_Data.CustomColor ? CustomNumbersColor : numberColor
                });
            }

            // Fill + Outline Settings
            if (!FillBars && !KeepOutline) {
                Chart.SetBarFillColor(index, Color.Transparent);
                Chart.SetBarOutlineColor(index, barColor);
                if (isBullish) UpWickColor = barColor;
                else DownWickColor = barColor;
            }
            else if (FillBars && KeepOutline) {
                Chart.SetBarFillColor(index, barColor);
                if (isBullish) UpWickColor = Chart.ColorSettings.BullOutlineColor;
                else DownWickColor = Chart.ColorSettings.BullOutlineColor;
            }
            else if (!FillBars && KeepOutline) {
                Chart.SetBarFillColor(index, Color.Transparent);
                if (isBullish) UpWickColor = Chart.ColorSettings.BullOutlineColor;
                else DownWickColor = Chart.ColorSettings.BullOutlineColor;
            }
            else {
                Chart.SetBarColor(index, barColor);
                if (isBullish) UpWickColor = barColor;
                else DownWickColor = barColor;
            }

            if (ShowStrengthValue) {
                if (Numbers_Input == Numbers_Data.Volume || Numbers_Input == Numbers_Data.Both) {
                    DrawOrCache(new DrawInfo
                    {
                        BarIndex = index,
                        Type = DrawType.Text,
                        Id = $"{index}_strengthVol",
                        Text = $"{volumeStrength}v",
                        X1 = Bars[index].OpenTime,
                        Y1 = isBullish ? Bars[index].High : Bars[index].Low,
                        horizontalAlignment = HorizontalAlignment.Center,
                        verticalAlignment = isBullish ? VerticalAlignment.Top : VerticalAlignment.Bottom,
                        FontSize = FontSizeNumbers,
                        Color = CustomNumbersColor
                    });
                }
                if (Numbers_Input == Numbers_Data.Time || Numbers_Input == Numbers_Data.Both) {
                    DrawOrCache(new DrawInfo
                    {
                        BarIndex = index,
                        Type = DrawType.Text,
                        Id = $"{index}_strengthTime",
                        Text = $"{timeStrength}ts",
                        X1 = Bars[index].OpenTime,
                        Y1 = isBullish ? Bars[index].Low : Bars[index].High,
                        horizontalAlignment = HorizontalAlignment.Center,
                        verticalAlignment = isBullish ? VerticalAlignment.Bottom : VerticalAlignment.Top,
                        FontSize = FontSizeNumbers,
                        Color = CustomNumbersColor
                    });
                }
                
                DrawOnScreen("v => volume \n ts => time");
            }

        }

        private static double RollingPercentile(double[] window)
        {
            // generated/converted by LLM
            if (window == null || window.Length == 0)
                return 0.0;

            double last = window[window.Length - 1];
            int count = 0;

            for (int i = 0; i < window.Length; i++)
            {
                if (window[i] <= last)
                    count++;
            }

            return 100.0 * count / window.Length;
        }
        
        private static double L1NormStrength(double[] window)
        {
            // generated/converted by LLM
            if (window == null || window.Length == 0)
                return 0.0;

            double denom = 0.0;

            for (int i = 0; i < window.Length; i++)
                denom += Math.Abs(window[i]);

            return denom != 0.0
                ? window[window.Length - 1] / denom
                : 1.0;
        }

        private double CustomMAs(double seriesValue, int index,
                                 int maPeriod, MAType_Data maType, bool isStdDev = false,
                                 IndicatorDataSeries stddev_buffer = null) {
            if (!_dynamicBuffer.ContainsKey(index))
                _dynamicBuffer.Add(index, seriesValue);
            else
                _dynamicBuffer[index] = seriesValue;

            Dictionary<int, double> buffer = _dynamicBuffer;
            Dictionary<int, double> prevMA_Dict = _maDynamic;

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

            return isStdDev ? StdDev(index, maPeriod, maValue, stddev_buffer) : maValue;
        }
        //  ===== CUSTOM MAS ====
        // MAs logic generated by LLM
        // Modified to handle multiples sources
        // as well as specific OrderFlow() needs.
        private static double StdDev(int index, int Period, double maValue, IndicatorDataSeries buffer)
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
                        if (redrawInfos.ContainsKey(info.BarIndex) && !lockDrawRemove) {
                            redrawInfos[info.BarIndex].Remove($"{trendStartIndex}_WavesMisc");
                            redrawInfos[info.BarIndex].Remove($"{trendStartIndex}_WavesVolume");
                            redrawInfos[info.BarIndex].Remove($"{trendStartIndex}_WavesEvsR");
                            lockDrawRemove = true;
                        }
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

        private string FormatBigNumber(double num)
        {
            if (double.IsNaN(num) || num.ToString().Length == 1)
                return num.ToString();

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
                    timerHandler.isAsyncLoading = false;
                    ClearAndRecalculate();
                    Timer.Stop();
                }
            }
        }

        private double[] Get_Volume_or_Wicks(int index, bool isVolume)
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];
            // For real-time market
            if (IsLastBar)
                endTime = TicksOHLC.LastBar.OpenTime;

            int volume = 0;
            double min = Int32.MaxValue;
            double max = 0;

            int startIndex = isVolume ? lastTick_Bars : lastTick_Wicks;
            if (IsLastBar) {
                while (TicksOHLC.OpenTimes[startIndex] < startTime)
                    startIndex++;
                if (isVolume)
                    lastTick_Bars = startIndex;
                else
                    lastTick_Wicks = startIndex;
            }

            for (int tickIndex = startIndex; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime) {
                    if (tickBar.OpenTime > endTime) {
                        lastTick_Bars = isVolume ? tickIndex : lastTick_Bars;
                        lastTick_Wicks = !isVolume ? tickIndex : lastTick_Wicks;
                        break;
                    }
                    else
                        continue;
                }
                if (isVolume)
                    volume += 1;
                else {
                    if (tickBar.Close < min)
                        min = tickBar.Close;
                    else if (tickBar.Close > max)
                        max = tickBar.Close;
                }
            }

            double[] toReturn = { min, max, volume };
            return toReturn;
        }

        // *********** RENKO WICKS ***********
        /*
            Original source code by srlcarlg (me) (https://ctrader.com/algos/indicators/show/3046)
            Improved after Order Flow Aggregated v2.0
        */
        private void RenkoWicks(int index)
        {
            double highest = Bars.HighPrices[index];
            double lowest = Bars.LowPrices[index];
            double open = Bars.OpenPrices[index];

            bool isBullish = Bars.ClosePrices[index] > Bars.OpenPrices[index];
            bool prevIsBullish = Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1];
            bool priceGap = Bars.OpenTimes[index] == Bars[index - 1].OpenTime || Bars[index - 2].OpenTime == Bars[index - 1].OpenTime;
            DateTime currentOpenTime = Bars.OpenTimes[index];

            double[] wicks = Get_Volume_or_Wicks(index, false);
            if (IsLastBar) {
                lowest = wicks[0];
                highest = wicks[1];
                open = Bars.ClosePrices[index - 1];
            } else {
                if (isBullish)
                    lowest = wicks[0];
                else
                    highest = wicks[1];
            }

            if (isBullish)
            {
                if (lowest < open && !priceGap) {
                    if (IsLastBar && !prevIsBullish && Bars.ClosePrices[index] > open)
                        open = Bars.OpenPrices[index];
                    ChartTrendLine trendlineUp = Chart.DrawTrendLine($"UpWick_{index}", currentOpenTime, open, currentOpenTime, lowest, UpWickColor);
                    trendlineUp.Thickness = RenkoThickness;
                    Chart.RemoveObject($"DownWick_{index}");
                }
            }
            else
            {
                if (highest > open && !priceGap) {
                    if (IsLastBar && prevIsBullish && Bars.ClosePrices[index] < open)
                        open = Bars.OpenPrices[index];
                    ChartTrendLine trendlineDown = Chart.DrawTrendLine($"DownWick_{index}", currentOpenTime, open, currentOpenTime, highest, DownWickColor);
                    trendlineDown.Thickness = RenkoThickness;
                    Chart.RemoveObject($"UpWick_{index}");
                }
            }
        }

        private void DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
        }

        // ************************ WEIS WAVE SYSTEM **************************
        /*
                                   Improved Weis Waves
                                           by
                                        srlcarlg

                          ====== References for Studies ======
        (Numbers-Renko 数字練行足) by akutsusho (https://www.tradingview.com/script/9BKOIhdl-Numbers-Renko) (Code concepts in PineScript)
        (ZigZag) by mike.ourednik (https://ctrader.com/algos/indicators/show/1419) (decreased a lot of code, base for any ZigZag)
        (Swing Gann) by TradeExperto (https://ctrader.com/algos/indicators/show/2521) (helped to make the structure of waves calculation)

        =========================================

        NEW IN Revision 1 (after ODF_AGG):
        - Instead of using the ZigZag, the DirectionChanged() method was doing the heavy job...
            - In order to use WWSystem on [Ticks, Range and time-based charts], the proper use of zigzag is needed.
        - Add [ATR, Pips] to Standard ZigZag.
        - Add simple Multi-Timeframe Price lookup.

                        ==== References for NoLag-HighLow ZigZag ===
        (Absolute ZigZag - 2024/2025) (https://tradingview.com/script/lRY74dha-Absolute-ZigZag-Lib/)
        // (The key idea for high/low bars analysis)
        (Professional ZigZag - 2011/2016) https://www.mql5.com/en/code/263
        // (The idea of High/Low order formation by looking at lower timeframes, seems to be the first one)

        I needed to simplify the High/Low Bars analysis because I wanted to keep the current ZigZag structure,
        which is quite optimized and easy to understand.
        Compared to "Absolute ZigZag" logic, I did:
            - Remove [High or Low] Priority, keep the Auto (lower timeframe order formation) for Time-Based charts only.
            - Add [Skip or None] Priority for "bars that have both a higher high and a higher low"
        */

        private void WeisWaveAnalysis(int rawIndex)
        {
            int index = rawIndex - 1;

            if (index < 2)
                return;

            if (WavesMode_Input == WavesMode_Data.Reversal && isRenkoChart) {
                if (IsLastBar) // IsLastBar=false at each new BarOpened
                    return;
                bool isUp = Bars.ClosePrices[index] > Bars.OpenPrices[index];

                if (ShowCurrentWave)
                    CalculateWaves(isUp ? Direction.UP : Direction.DOWN, trendStartIndex, index, false);

                if (ShowTrendLines) {
                    ChartTrendLine trendLine = Chart.DrawTrendLine($"TrendLine_{trendStartIndex}",
                                   trendStartIndex, Bars.OpenPrices[trendStartIndex],
                                   index, Bars.OpenPrices[index], isUp ? UpLineColor : DownLineColor);
                    trendLine.Thickness = TrendThickness;
                }

                if (!Reversal_DirectionChanged(index))
                    return;

                CalculateWaves(isUp ? Direction.UP : Direction.DOWN, trendStartIndex, index, true);

                if (ShowTrendLines) {
                    ChartTrendLine trendLine = Chart.DrawTrendLine($"TrendLine_NO{index}",
                                               index, Bars.OpenPrices[index],
                                               index + 1, Bars.OpenPrices[index], NoTrendColor);
                    trendLine.Thickness = TrendThickness;
                }

                trendStartIndex = index + 1;
            }
            else
                ZigZag(index);
        }
        private bool Reversal_DirectionChanged(int index)
        {
            bool isUp = Bars.ClosePrices[index] > Bars.OpenPrices[index];

            bool prevIsUp = Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1];
            bool nextIsUp = Bars.ClosePrices[index + 1] > Bars.OpenPrices[index + 1];
            bool prevIsDown = Bars.ClosePrices[index - 1] < Bars.OpenPrices[index - 1];
            bool nextIsDown = Bars.ClosePrices[index + 1] < Bars.OpenPrices[index + 1];

            return prevIsUp && isUp && nextIsDown || prevIsDown && isUp && nextIsDown ||
                   prevIsDown && !isUp && nextIsUp || prevIsUp && !isUp && nextIsUp;
        }

        private bool ZigZag_DirectionChanged(int index, double low, double high, double prevLow, double prevHigh)
        {
            switch (ZigZagMode_Input)
            {
                case ZigZagMode_Data.Percentage:
                    if (direction == Direction.DOWN)
                        return high >= extremumPrice * (1.0 + PercentageZZ * 0.01);
                    else
                        return low <= extremumPrice * (1.0 - PercentageZZ * 0.01);
                case ZigZagMode_Data.NoLag_HighLow:
                    bool bothIsPivot = high > prevHigh && low < prevLow;
                    bool highIsPivot = high > prevHigh && low >= prevLow;
                    bool lowIsPivot = low < prevLow && high <= prevHigh;
                    if (bothIsPivot)
                        return false;
                    return direction == Direction.UP ? lowIsPivot : highIsPivot;
                default:
                    bool isATR = ZigZagMode_Input == ZigZagMode_Data.ATR;
                    double value = isATR ? (_ATR.Result[index] * ATR_Multiplier) : (PipsZZ * Symbol.PipSize);
                    if (direction == Direction.DOWN)
                        return Math.Abs(extremumPrice - high) >= value;
                    else
                        return Math.Abs(low - extremumPrice) >= value;
            }
        }
        private void ZigZag(int index) {
            double prevHigh = Bars.HighPrices[index - 1];
            double prevLow = Bars.LowPrices[index - 1];
            double high = Bars.HighPrices[index];
            double low = Bars.LowPrices[index];
            if (ZigZagSource_Input == ZigZagSource_Data.MultiTF) {
                DateTime prevBarDate = Bars.OpenTimes[index - 1];
                DateTime barDate = Bars.OpenTimes[index];

                int TF_PrevIdx = MTFSource_Bars.OpenTimes.GetIndexByTime(prevBarDate);
                int TF_idx = MTFSource_Bars.OpenTimes.GetIndexByTime(barDate);

                prevHigh = MTFSource_Bars.HighPrices[TF_PrevIdx];
                prevLow = MTFSource_Bars.LowPrices[TF_PrevIdx];
                high = MTFSource_Bars.HighPrices[TF_idx];
                low = MTFSource_Bars.LowPrices[TF_idx];
            }

            if (extremumPrice == 0) {
                extremumPrice = high;
                extremumIndex = index;
            }

            if (ZigZagMode_Input == ZigZagMode_Data.NoLag_HighLow && Priority_Input != Priority_Data.None && !isPriceBased_Chart) {
                if (NoLag_BothIsPivot(index, low, high, prevLow, prevHigh) || Priority_Input == Priority_Data.Skip)
                    return;
            }
            bool directionChanged = ZigZag_DirectionChanged(index, low, high, prevLow, prevHigh);
            if (direction == Direction.DOWN)
            {
                if (low <= extremumPrice)
                    MoveExtremum(index, low);
                else if (directionChanged) {
                    SetExtremum(index, high, false);
                    direction = Direction.UP;
                }
            }
            else
            {
                if (high >= extremumPrice)
                    MoveExtremum(index, high);
                else if (directionChanged) {
                    SetExtremum(index, low, false);
                    direction = Direction.DOWN;
                }
            }
        }
        private void MoveExtremum(int index, double price)
        {
            if (!ShowTrendLines)
                ZigZagBuffer[extremumIndex] = double.NaN;
            SetExtremum(index, price, true);
        }
        private void SetExtremum(int index, double price, bool isMove)
        {
            if (!isMove) {
                // End of direction
                CalculateWaves(direction, trendStartIndex, extremumIndex, true);
                trendStartIndex = extremumIndex + 1;

                DateTime extremeDate = Bars[extremumIndex].OpenTime;
                double extremePrice = direction == Direction.UP ? Bars[extremumIndex].High : Bars[extremumIndex].Low;
                if (ZigZagSource_Input == ZigZagSource_Data.MultiTF) {
                    int TF_idx = MTFSource_Bars.OpenTimes.GetIndexByTime(extremeDate);
                    extremePrice = direction == Direction.UP ? MTFSource_Bars[TF_idx].High : MTFSource_Bars[TF_idx].Low;
                }
                if (ShowTurningPoint) {
                    Color turningColor = InvertTurningColor ?
                                        (direction == Direction.UP ? DownLineColor : UpLineColor) :
                                        (direction == Direction.UP ? UpLineColor : DownLineColor);

                    Chart.DrawTrendLine($"{extremumIndex}_horizontal",
                                        extremeDate,
                                        extremePrice,
                                        Bars[index].OpenTime,
                                        extremePrice, turningColor);
                    Chart.DrawTrendLine($"{extremumIndex}_vertical",
                                        Bars[index].OpenTime,
                                        extremePrice,
                                        Bars[index].OpenTime,
                                        direction == Direction.UP ? Bars[index].High : Bars[index].Low, turningColor);
                }

                if (ShowTrendLines) {
                    PrevWave_TrendLine.LineStyle = LineStyle.Solid;
                    if (isLargeWave_EvsR && ShowYellowTrendLines)
                        PrevWave_TrendLine.Color = LargeWaveColor;

                    Color lineColor = ColorfulTrendLines ?
                                      (direction == Direction.UP ? DownLineColor : UpLineColor) :
                                      NoTrendColor;
                    double trendEndPrice = direction == Direction.UP ? Bars[index].Low : Bars[index].High;
                    PrevWave_TrendLine = Chart.DrawTrendLine($"TrendLine_{trendStartIndex}",
                                                            extremeDate,
                                                            extremePrice,
                                                            Bars[index].OpenTime,
                                                            trendEndPrice, lineColor);
                    PrevWave_TrendLine.Thickness = TrendThickness;
                }
            }
            else if (isMove && ShowCurrentWave)
                CalculateWaves(direction, trendStartIndex, extremumIndex, false);

            if (ZigZagSource_Input == ZigZagSource_Data.MultiTF && isMove) {
                // Workaround to remove the behavior of shift(nº) when moving the extremum at custom timeframe price source
                double extremePrice = direction == Direction.UP ? Bars[extremumIndex].High : Bars[extremumIndex].Low;
                double currentPrice = direction == Direction.UP ? Bars[index].High : Bars[index].Low;
                bool condition = direction == Direction.UP ? currentPrice <= extremePrice : currentPrice >= extremePrice;
                extremumIndex = condition ? extremumIndex : index;
            }
            else
                extremumIndex = index;

            extremumPrice = price;

            if (!ShowTrendLines)
                ZigZagBuffer[extremumIndex] = extremumPrice;

            if (isMove)
                MovingTrendLine(Bars[extremumIndex].OpenTime, price);
        }

        private void MovingTrendLine(DateTime endDate, double endPrice)
        {
            if (ShowTrendLines)
            {
                int startIndex = trendStartIndex - 1;
                // Yeah... index jumps are quite annoying to debug.
                try { _ = Bars[startIndex].OpenTime; } catch { startIndex = trendStartIndex; }

                DateTime startDate = Bars[startIndex].OpenTime;
                double startPrice = direction == Direction.UP ? Bars[startIndex].Low : Bars[startIndex].High;

                Color lineColor = ColorfulTrendLines ? (direction == Direction.UP ? DownLineColor : UpLineColor) : NoTrendColor;
                PrevWave_TrendLine = Chart.DrawTrendLine($"TrendLine_{trendStartIndex}",
                                     startDate,
                                     startPrice,
                                     endDate,
                                     endPrice, lineColor);
                PrevWave_TrendLine.Thickness = TrendThickness;
                PrevWave_TrendLine.LineStyle = LineStyle.Dots;
            }
        }
        private bool NoLag_BothIsPivot(int  index, double low, double high, double prevLow, double prevHigh) {
            bool bothIsPivot = high > prevHigh && low < prevLow;
            if (!bothIsPivot || Priority_Input != Priority_Data.Auto)
                return false;

            bool HighIsFirst = AutoPriority(index, prevLow, prevHigh, low, high);
            if (HighIsFirst) {
                // Chart.DrawText($"{index}_First", "First(High)", Bars[index].OpenTime, high, Color.White);
                // Chart.DrawText($"{index}_Last", "Last(Low)", Bars[index].OpenTime, low, Color.White);
                // Chart.DrawText($"{index}_DIRECTION", direction.ToString(), Bars[index].OpenTime, Bars.OpenPrices[index], Color.White);
                if (direction == Direction.UP)
                {
                    if (high > extremumPrice && !ShowTrendLines)
                        ZigZagBuffer[extremumIndex] = high;

                    SetExtremum(index, low, true);
                    direction = Direction.DOWN;
                }
            }
            else {
                // Chart.DrawText($"{index}_First", "First(Low)", Bars[index].OpenTime, low, Color.White);
                // Chart.DrawText($"{index}_Last", "Last(High)", Bars[index].OpenTime, high, Color.White);
                // Chart.DrawText($"{index}_DIRECTION", direction.ToString(), Bars[index].OpenTime, Bars.OpenPrices[index], Color.White);
                if (direction == Direction.DOWN)
                {
                    if (low < extremumPrice && !ShowTrendLines)
                        ZigZagBuffer[index] = low;

                    SetExtremum(index, high, true);
                    direction = Direction.UP;
                }
            }

            return true;
        }

        private bool AutoPriority(int index, double prevLow, double prevHigh, double low, double high)
        {
            DateTime barStart = Bars.OpenTimes[index];
            DateTime barEnd = Bars.OpenTimes[index + 1];
            if (ZigZagSource_Input == ZigZagSource_Data.MultiTF) {
                int TF_idxStart = MTFSource_Bars.OpenTimes.GetIndexByTime(barStart);
                int TF_idxEnd = MTFSource_Bars.OpenTimes.GetIndexByTime(barEnd);

                barStart = MTFSource_Bars.OpenTimes[TF_idxStart];
                barEnd = MTFSource_Bars.OpenTimes[TF_idxEnd];
            }
            if (IsLastBar)
                barEnd = _m1Bars.LastBar.OpenTime;

            bool firstIsHigh = false;
            bool atLeastOne = false;

            int startM1 = _m1Bars.OpenTimes.GetIndexByTime(barStart);
            for (int i = startM1; i < _m1Bars.OpenTimes.Count; i++)
            {
                if (_m1Bars.OpenTimes[i] > barEnd)
                    break;

                if (_m1Bars.HighPrices[i] > prevHigh) {
                    firstIsHigh = true;
                    atLeastOne = true;
                    break;
                }
                if (_m1Bars.LowPrices[i] < prevLow) {
                    firstIsHigh = true;
                    atLeastOne = true;
                    break;
                }
            }

            if (!atLeastOne) {
                double subtHigh = Math.Abs(high - prevHigh);
                double subtLow = Math.Abs(prevLow - low);
                return subtHigh >= subtLow;
            }

            return firstIsHigh;
        }

        private double GetY1_Waves(int extremeIndex) {
            if (WavesMode_Input == WavesMode_Data.Reversal && isRenkoChart)
                return Bars.ClosePrices[extremeIndex];

            DateTime extremeDate = Bars[extremeIndex].OpenTime;
            double extremePrice = direction == Direction.UP ? Bars[extremeIndex].High : Bars[extremeIndex].Low;
            if (ZigZagSource_Input == ZigZagSource_Data.MultiTF) {
                int TF_idx = MTFSource_Bars.OpenTimes.GetIndexByTime(extremeDate);
                extremePrice = direction == Direction.UP ? MTFSource_Bars[TF_idx].High : MTFSource_Bars[TF_idx].Low;
            }
            return extremePrice;
        }

        private void CalculateWaves(Direction direction, int firstCandleIdx, int lastCandleIdx, bool directionChanged = false)
        {
            double cumulVolume()
            {
                double volume = 0.0;
                for (int i = firstCandleIdx; i <= lastCandleIdx; i++)
                    volume += VolumeSeries[i];

                return volume;
            }
            double cumulRenko()
            {
                double renkoCount = 0;
                for (int i = firstCandleIdx; i <= lastCandleIdx; i++)
                    renkoCount += 1;

                return renkoCount;
            }
            double cumulativePrice(bool isUp)
            {
                double price;
                if (isUp)
                    price = Bars.HighPrices[lastCandleIdx] - Bars.LowPrices[firstCandleIdx];
                else
                    price = Bars.HighPrices[firstCandleIdx] - Bars.LowPrices[lastCandleIdx];

                if (ZigZagSource_Input == ZigZagSource_Data.MultiTF && WavesMode_Input == WavesMode_Data.ZigZag) {
                    int TF_idxLast = MTFSource_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[lastCandleIdx]);
                    int TF_idxFirst = MTFSource_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstCandleIdx]);
                    if (isUp)
                        price = MTFSource_Bars.HighPrices[TF_idxLast] - MTFSource_Bars.LowPrices[TF_idxFirst];
                    else
                        price = MTFSource_Bars.HighPrices[TF_idxFirst] - MTFSource_Bars.LowPrices[TF_idxLast];
                }
                price /= Symbol.PipSize;

                return Math.Round(price, 2);
            }
            double cumulativeTime()
            {
                DateTime openTime = Bars.OpenTimes[firstCandleIdx];
                DateTime closeTime = Bars.OpenTimes[lastCandleIdx + 1];
                TimeSpan interval = closeTime.Subtract(openTime);
                double interval_ms = interval.TotalMilliseconds;
                return interval_ms;
            }
            bool directionIsUp = direction == Direction.UP;
            if (ShowWaves_Input == ShowWaves_Data.No)
            {
                // Other Waves
                if (!ShowCurrentWave && directionChanged || ShowCurrentWave)
                    OthersWaves(directionIsUp);
                return;
            }

            double cumlVolume = cumulVolume();
            double cumlRenkoOrPrice = cumulRenko();
            if (!isRenkoChart)
                cumlRenkoOrPrice = cumulativePrice(directionIsUp);
            double cumlVolPrice = Math.Round(cumlVolume / cumlRenkoOrPrice, 1);

            // Standard Waves
            if (!ShowCurrentWave && directionChanged || ShowCurrentWave) {
                EvsR_Analysis(cumlVolPrice, directionChanged, directionIsUp);
                WW_Analysis(cumlVolume, directionChanged, directionIsUp);
            }
            // Other Waves
            if (!ShowCurrentWave && directionChanged || ShowCurrentWave)
                OthersWaves(directionIsUp);

            // Prev Waves Analysis
            if (directionIsUp) {
                bool prevIsDown = Bars.ClosePrices[lastCandleIdx - 1] < Bars.OpenPrices[lastCandleIdx - 1];
                bool nextIsDown = Bars.ClosePrices[lastCandleIdx + 1] < Bars.OpenPrices[lastCandleIdx + 1];
                // Set Previous Bullish Wave Accumulated
                SetPrevWaves(cumlVolume, cumlVolPrice, prevIsDown, nextIsDown, true, directionChanged);
            } else {
                bool prevIsUp = Bars.ClosePrices[lastCandleIdx - 1] > Bars.OpenPrices[lastCandleIdx - 1];
                bool nextIsUp = Bars.ClosePrices[lastCandleIdx + 1] > Bars.OpenPrices[lastCandleIdx + 1];
                // Set Previous Downish Wave Accumulated
                SetPrevWaves(cumlVolume, cumlVolPrice, prevIsUp, nextIsUp, false, directionChanged);
            }

            void OthersWaves(bool isUp)
            {
                if (ShowOtherWaves_Input == ShowOtherWaves_Data.No)
                    return;

                double cumulPrice = cumulativePrice(isUp);
                string cumulPriceFmtd = cumulPrice > 1000 ? FormatBigNumber(cumulPrice) : cumulPrice.ToString();
                double cumlTime = cumulativeTime();

                if (cumlTime == 0 || double.IsNaN(cumlTime))
                    return;

                string[] interval_timelapse = GetTimeLapse(cumlTime);

                ShowWaves_Data selectedWave = ShowWaves_Input;
                double timelapse_Value = Convert.ToDouble(interval_timelapse[0]);
                string timelapseString = Math.Round(timelapse_Value) + interval_timelapse[1];

                string waveInfo;
                if (isUp)
                {
                    if (ShowOtherWaves_Input == ShowOtherWaves_Data.Both)
                    {
                        if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"{timelapseString} ⎪ {cumulPriceFmtd}p\n\n" : selectedWave == ShowWaves_Data.Both ? $"{timelapseString} ⎪ {cumulPriceFmtd}p\n\n\n\n" : $"{timelapseString} ⎪ {cumulPriceFmtd}p\n\n\n";
                        else
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"{timelapseString} ⎪ {cumulPriceFmtd}p" : selectedWave == ShowWaves_Data.Both ? $"{timelapseString} ⎪ {cumulPriceFmtd}p\n\n\n" : $"{timelapseString} ⎪ {cumulPriceFmtd}p\n\n";
                    }
                    else {
                        string sourceWave = ShowOtherWaves_Input == ShowOtherWaves_Data.Price ? cumulPriceFmtd : timelapseString;
                        string suffixWave = ShowOtherWaves_Input == ShowOtherWaves_Data.Price ? "p" : "";

                        if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"{sourceWave}{suffixWave}\n\n" : selectedWave == ShowWaves_Data.Both ? $"{sourceWave}{suffixWave}\n\n\n\n" : $"{sourceWave}{suffixWave}\n\n\n";
                        else
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"{sourceWave}{suffixWave}" : selectedWave == ShowWaves_Data.Both ? $"{sourceWave}{suffixWave}\n\n\n" : $"{sourceWave}{suffixWave}\n\n";
                    }
                }
                else
                {
                    if (ShowOtherWaves_Input == ShowOtherWaves_Data.Both)
                    {
                        if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"\n{timelapseString} ⎪ {cumulPriceFmtd}p" : selectedWave == ShowWaves_Data.Both ? $"\n\n\n{timelapseString} ⎪ {cumulPriceFmtd}p" : $"\n\n{timelapseString} ⎪ {cumulPriceFmtd}p";
                        else
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"{timelapseString} ⎪ {cumulPriceFmtd}p" : selectedWave == ShowWaves_Data.Both ? $"\n\n{timelapseString} ⎪ {cumulPriceFmtd}p" : $"\n{timelapseString} ⎪ {cumulPriceFmtd}p";
                    }
                    else {
                        string sourceWave = ShowOtherWaves_Input == ShowOtherWaves_Data.Price ? cumulPriceFmtd : timelapseString;
                        string suffixWave = ShowOtherWaves_Input == ShowOtherWaves_Data.Price ? "p" : "";

                        if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"\n{sourceWave}{suffixWave}" : selectedWave == ShowWaves_Data.Both ? $"\n\n\n{sourceWave}{suffixWave}" : $"\n\n{sourceWave}{suffixWave}";
                        else
                            waveInfo = selectedWave == ShowWaves_Data.No ? $"{sourceWave}{suffixWave}" : selectedWave == ShowWaves_Data.Both ? $"\n\n{sourceWave}{suffixWave}" : $"\n{sourceWave}{suffixWave}";
                    }
                }

                double y1 = GetY1_Waves(lastCandleIdx);
                DrawOrCache(new DrawInfo
                {
                    BarIndex = lastCandleIdx,
                    Type = DrawType.Text,
                    Id = $"{firstCandleIdx}_WavesMisc",
                    Text = waveInfo,
                    X1 = Bars.OpenTimes[lastCandleIdx],
                    Y1 = y1,
                    horizontalAlignment = HorizontalAlignment.Center,
                    verticalAlignment = isUp ? VerticalAlignment.Top : VerticalAlignment.Bottom,
                    FontSize = FontSizeWaves,
                    Color = isUp ? UpWaveColor : DownWaveColor
                });
            }

            void WW_Analysis(double cumlVolume, bool endWave, bool isUp)
            {
                if (ShowWaves_Input == ShowWaves_Data.No || ShowWaves_Input == ShowWaves_Data.EffortvsResult)
                    return;
                string leftMark = "";
                string rightMark = "";
                string waveInfo;

                if (isUp)
                {
                    if (ShowMarks_Input == ShowMarks_Data.Left)
                        leftMark = cumlVolume > prevWave_Up[0] ? "⮝" : "⮟";
                    else if (ShowMarks_Input == ShowMarks_Data.Right)
                        rightMark = cumlVolume > prevWave_Down[0] ? "🡩" : "🡫";
                    else if (ShowMarks_Input == ShowMarks_Data.Both)
                    {
                        leftMark = cumlVolume > prevWave_Up[0] ? "⮝" : "⮟";
                        rightMark = cumlVolume > prevWave_Down[0] ? "" : leftMark == "⮟" ? "" : "🡫";
                    }

                    string defaultStr = $"({leftMark}{FormatBigNumber(cumlVolume)}{rightMark})";
                    waveInfo = (ShowWaves_Input == ShowWaves_Data.Volume || ShowWaves_Input == ShowWaves_Data.Both) ? defaultStr : "";

                    if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                        waveInfo = (ShowWaves_Input == ShowWaves_Data.Volume || ShowWaves_Input == ShowWaves_Data.Both) ? $"{defaultStr}\n\n" : "";
                }
                else
                {
                    if (ShowMarks_Input == ShowMarks_Data.Left)
                        leftMark = cumlVolume > prevWave_Down[0] ? "⮟" : "⮝";
                    else if (ShowMarks_Input == ShowMarks_Data.Right)
                        rightMark = cumlVolume > prevWave_Up[0] ? "🡫" : "🡩";
                    else if (ShowMarks_Input == ShowMarks_Data.Both)
                    {
                        leftMark = cumlVolume > prevWave_Down[0] ? "⮟" : "⮝";
                        rightMark = cumlVolume > prevWave_Up[0] ? "" : leftMark == "⮝" ? "" : "🡩";
                    }

                    string defaultStr = $"({leftMark}{FormatBigNumber(cumlVolume)}{rightMark})";
                    waveInfo = (ShowWaves_Input == ShowWaves_Data.Volume || ShowWaves_Input == ShowWaves_Data.Both) ? defaultStr : "";

                    if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                        waveInfo = (ShowWaves_Input == ShowWaves_Data.Volume || ShowWaves_Input == ShowWaves_Data.Both) ? $"\n{defaultStr}" : "";
                }

                double y1 = GetY1_Waves(lastCandleIdx);
                bool largeVol = endWave && Volume_Large();
                Color waveColor = largeVol ? LargeWaveColor : (isUp ? UpWaveColor : DownWaveColor);

                if (ShowRatioValue) {
                    double ratio = (cumlVolume + prevWaves_Volume[0] + prevWaves_Volume[1] + prevWaves_Volume[2] + prevWaves_Volume[3]) / 5 * WW_Ratio;
                    ratio = Math.Round(ratio, 2);
                    waveInfo = $"{waveInfo} > {ratio}? {cumlVolume > ratio} ";
                }

                DrawOrCache(new DrawInfo
                {
                    BarIndex = lastCandleIdx,
                    Type = DrawType.Text,
                    Id = $"{firstCandleIdx}_WavesVolume",
                    Text = waveInfo,
                    X1 = Bars.OpenTimes[lastCandleIdx],
                    Y1 = y1,
                    horizontalAlignment = HorizontalAlignment.Center,
                    verticalAlignment = isUp ? VerticalAlignment.Top : VerticalAlignment.Bottom,
                    FontSize = FontSizeWaves,
                    Color = waveColor
                });

                bool Volume_Large()
                {
                    bool haveZero = false;
                    foreach (double value in prevWaves_Volume)
                    {
                        if (value == 0) {
                            haveZero = true;
                            break;
                        }
                    }
                    if (haveZero)
                        return false;

                    return (cumlVolume + prevWaves_Volume[0] + prevWaves_Volume[1] + prevWaves_Volume[2] + prevWaves_Volume[3]) / 5 * WW_Ratio < cumlVolume;
                }
            }

            void EvsR_Analysis(double cumlVolPrice, bool endWave, bool isUp)
            {
                if (ShowWaves_Input == ShowWaves_Data.No || ShowWaves_Input == ShowWaves_Data.Volume)
                    return;

                string leftMark = "";
                string rightMark = "";
                string waveInfo;

                if (isUp)
                {
                    if (ShowMarks_Input == ShowMarks_Data.Left)
                        leftMark = cumlVolPrice > prevWave_Up[1] ? "⮝" : "⮟";
                    else if (ShowMarks_Input == ShowMarks_Data.Right)
                        rightMark = cumlVolPrice > prevWave_Down[1] ? "🡩" : "🡫";
                    else if (ShowMarks_Input == ShowMarks_Data.Both)
                    {
                        leftMark = cumlVolPrice > prevWave_Up[1] ? "⮝" : "⮟";
                        rightMark = cumlVolPrice > prevWave_Down[1] ? "" : leftMark == "⮟" ? "" : "🡫";
                    }

                    string defaultStr = $"[{leftMark}{FormatBigNumber(cumlVolPrice)}{rightMark}]";

                    waveInfo = ShowWaves_Input == ShowWaves_Data.EffortvsResult ? defaultStr : ShowWaves_Input == ShowWaves_Data.Both ? $"{defaultStr}\n\n" : "";
                    if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                        waveInfo = ShowWaves_Input == ShowWaves_Data.EffortvsResult ? $"{defaultStr}\n\n" : ShowWaves_Input == ShowWaves_Data.Both ? $"{defaultStr}\n\n\n" : "";
                }
                else
                {
                    if (ShowMarks_Input == ShowMarks_Data.Left)
                        leftMark = cumlVolPrice > prevWave_Down[1] ? "⮟" : "⮝";
                    else if (ShowMarks_Input == ShowMarks_Data.Right)
                        rightMark = cumlVolPrice > prevWave_Up[1] ? "🡫" : "🡩";
                    else if (ShowMarks_Input == ShowMarks_Data.Both)
                    {
                        leftMark = cumlVolPrice > prevWave_Down[1] ? "⮟" : "⮝";
                        rightMark = cumlVolPrice > prevWave_Up[1] ? "" : leftMark == "⮝" ? "" : "🡩";
                    }

                    string defaultStr = $"[{leftMark}{FormatBigNumber(cumlVolPrice)}{rightMark}]";

                    waveInfo = ShowWaves_Input == ShowWaves_Data.EffortvsResult ? defaultStr : ShowWaves_Input == ShowWaves_Data.Both ? $"\n{defaultStr}" : "";
                    if (NumbersPosition_Input == NumbersPosition_Data.Outside)
                        waveInfo = ShowWaves_Input == ShowWaves_Data.EffortvsResult ? $"\n{defaultStr}" : ShowWaves_Input == ShowWaves_Data.Both ? $"\n\n{defaultStr}" : "";
                }

                double y1 = GetY1_Waves(lastCandleIdx);
                bool largeEffort = endWave && EvsR_Large();
                Color waveColor = largeEffort ? LargeWaveColor : (isUp ? UpWaveColor : DownWaveColor);

                if (ShowRatioValue) {
                    double ratio = (cumlVolPrice + prevWaves_EvsR[0] + prevWaves_EvsR[1] + prevWaves_EvsR[2] + prevWaves_EvsR[3]) / 5 * EvsR_Ratio;
                    ratio = Math.Round(ratio, 2);
                    waveInfo = $"{waveInfo} > {ratio}? {cumlVolPrice > ratio}";
                }

                DrawOrCache(new DrawInfo
                {
                    BarIndex = lastCandleIdx,
                    Type = DrawType.Text,
                    Id = $"{firstCandleIdx}_WavesEvsR",
                    Text = waveInfo,
                    X1 = Bars.OpenTimes[lastCandleIdx],
                    Y1 = y1,
                    horizontalAlignment = HorizontalAlignment.Center,
                    verticalAlignment = isUp ? VerticalAlignment.Top : VerticalAlignment.Bottom,
                    FontSize = FontSizeWaves,
                    Color = waveColor
                });

                isLargeWave_EvsR = false;
                if (!largeEffort)
                    return;
                isLargeWave_EvsR = true;

                if (!FillBars && !KeepOutline) {
                    Chart.SetBarFillColor(lastCandleIdx, Color.Transparent);
                    Chart.SetBarOutlineColor(lastCandleIdx, LargeWaveColor);
                }
                else if (FillBars && KeepOutline)
                    Chart.SetBarFillColor(lastCandleIdx, LargeWaveColor);
                else if (!FillBars && KeepOutline)
                    Chart.SetBarFillColor(lastCandleIdx, Color.Transparent);
                else if (FillBars && !KeepOutline)
                    Chart.SetBarColor(lastCandleIdx, LargeWaveColor);

                // Large EvsR [Yellow]
                bool EvsR_Large()
                {
                    bool haveZero = false;
                    foreach (double value in prevWaves_EvsR)
                    {
                        if (value == 0) {
                            haveZero = true;
                            break;
                        }
                    }
                    if (haveZero)
                        return false;

                    return (cumlVolPrice + prevWaves_EvsR[0] + prevWaves_EvsR[1] + prevWaves_EvsR[2] + prevWaves_EvsR[3]) / 5 * EvsR_Ratio < cumlVolPrice;
                }
            }
        }

        private void SetPrevWaves(double cumlVolume, double cumlVolPrice, bool prevIs_UpDown, bool nextIs_UpDown, bool isUp, bool directionChanged)
        {
            // Exclude the most old wave, keep the 3 others and add current Wave value for most recent Wave
            /*
                The previous "wrongly" implementation turns out to be a good filter,
                with the correct implementation of 5 waves, it gives too many yellow bars.
                Since it's useful, keep it.
            */
            double[] cumul = { cumlVolume, cumlVolPrice };

            if (WavesMode_Input == WavesMode_Data.ZigZag) {
                if (!directionChanged) return;
                setTrend();
                return;
            }

            bool conditionRanging = prevIs_UpDown && directionChanged && nextIs_UpDown;
            bool conditionTrend = !prevIs_UpDown && directionChanged && nextIs_UpDown;

            if (isUp) {
                // (prevIsDown && DirectionChanged && nextIsDown);
                if (conditionRanging)
                    setRanging();
                // (!prevIsDown && DirectionChanged && nextIsDown);
                else if (conditionTrend)
                    setTrend();
            } else {
                // (prevIsUp && DirectionChanged && nextIsUp)
                if (conditionRanging)
                    setRanging();
                // (!prevIsUp && DirectionChanged && nextIsUp);
                else if (conditionTrend)
                    setTrend();
            }

            // Ranging or 1 renko trend pullback
            void setRanging() {
                // Volume Wave Analysis
                double[] newWave_Vol = { prevWaves_Volume[1], prevWaves_Volume[2], prevWaves_Volume[3], cumlVolume };
                prevWaves_Volume = newWave_Vol;

                // Effort vs Result Analysis
                double[] newWave_EvsR = { prevWaves_EvsR[1], prevWaves_EvsR[2], prevWaves_EvsR[3], cumlVolPrice };
                prevWaves_EvsR = newWave_EvsR;

                if (!YellowRenko_IgnoreRanging) {
                    if (isUp) prevWave_Up = cumul;
                    else prevWave_Down = cumul;
                }
            }
            void setTrend() {
                if (isUp) {
                    // Volume Wave Analysis
                    double volumeValue = YellowZigZag_Input == YellowZigZag_Data.UseCurrent ? cumlVolume :
                                         YellowZigZag_Input == YellowZigZag_Data.UsePrev_SameWave ? prevWave_Down[0] : prevWave_Up[0];
                    double[] newWave_Vol = { prevWaves_Volume[1], prevWaves_Volume[2], prevWaves_Volume[3], volumeValue };
                    prevWaves_Volume = newWave_Vol;

                    // Effort vs Result Analysis
                    double evsrValue = YellowZigZag_Input == YellowZigZag_Data.UseCurrent ? cumlVolPrice :
                                       YellowZigZag_Input == YellowZigZag_Data.UsePrev_SameWave ? prevWave_Up[1] : prevWave_Down[1];
                    double[] newWave_EvsR = { prevWaves_EvsR[1], prevWaves_EvsR[2], prevWaves_EvsR[3], evsrValue };
                    prevWaves_EvsR = newWave_EvsR;

                    // Prev Wave
                    prevWave_Up = cumul;
                }
                else {
                    // Volume Wave Analysis
                    double volumeValue = YellowZigZag_Input == YellowZigZag_Data.UseCurrent ? cumlVolume :
                                         YellowZigZag_Input == YellowZigZag_Data.UsePrev_SameWave ? prevWave_Down[0] : prevWave_Up[0];
                    double[] newWave_Vol = { prevWaves_Volume[1], prevWaves_Volume[2], prevWaves_Volume[3], volumeValue };
                    prevWaves_Volume = newWave_Vol;

                    // Effort vs Result Analysis
                    double evsrValue = YellowZigZag_Input == YellowZigZag_Data.UseCurrent ? cumlVolPrice :
                                       YellowZigZag_Input == YellowZigZag_Data.UsePrev_SameWave ? prevWave_Down[1] : prevWave_Up[1];
                    double[] newWave_EvsR = { prevWaves_EvsR[1], prevWaves_EvsR[2], prevWaves_EvsR[3], evsrValue };
                    prevWaves_EvsR = newWave_EvsR;

                    // Prev Wave
                    prevWave_Down = cumul;
                }
            }
        }

        private static string[] GetTimeLapse(double interval_ms)
        {
            // Dynamic TimeLapse Format
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
            string[] interval_timelapse = { $"{timelapse_Value}", timelapse_Suffix };
            return interval_timelapse;
        }

        public void ClearAndRecalculate()
        {
            Thread.Sleep(300);

            Design_Templates();
            SpecificChart_Templates(false);
            DrawingConflict();

            if (!ShowTrendLines) {
                for (int i = 0; i < Bars.Count; i++)
                {
                    if (!double.IsNaN(ZigZagBuffer[i]))
                        ZigZagBuffer[i] = double.NaN;
                }
            }
            // Reset Zigzag.
            extremumPrice = 0;
            lockMTFNotify = false;

            // Reset Tick Index.
            lastTick_Bars = 0;
            lastTick_Wicks = 0;

            // Reset Drawings
            redrawInfos.Clear();
            hiddenInfos.Clear();
            currentToHidden.Clear();
            currentToRedraw.Clear();

            int firstLoadedTick = Bars.OpenTimes.GetIndexByTime(TicksOHLC.OpenTimes.FirstOrDefault());
            int startIndex = UseTimeBasedVolume && !isPriceBased_Chart ? 0 : firstLoadedTick;
            int endIndex = Bars.Count;
            for (int index = startIndex; index < endIndex; index++)
            {
                if (!UseTimeBasedVolume && !isPriceBased_Chart || isPriceBased_Chart) {
                    if (index < firstLoadedTick) {
                        Chart.SetBarColor(index, HeatmapLowest_Color);
                        continue;
                    }
                }

                if (UseTimeBasedVolume && !isPriceBased_Chart)
                    VolumeSeries[index] = Bars.TickVolumes[index];
                else
                    VolumeSeries[index] = Get_Volume_or_Wicks(index, true)[2];

                if (EnableWyckoff)
                    WyckoffAnalysis(index);

                // Catch MTF ZigZag < Current timeframe (ArgumentOutOfRangeException, index)
                try { WeisWaveAnalysis(index); } catch {
                    if (ZigZagSource_Input == ZigZagSource_Data.MultiTF && !lockMTFNotify) {
                        Notifications.ShowPopup(
                            NOTIFY_CAPTION,
                            $"ERROR => ZigZag MTF(source): \nCannot use {MTFSource_TimeFrame.ShortName} interval for {Chart.TimeFrame.ShortName} chart \nThe interval is probably too short?",
                            PopupNotificationState.Error
                        );
                        lockMTFNotify = true;
                    }
                }

                if (ShowWicks && isRenkoChart)
                    RenkoWicks(index);
            }


            if (!UseTimeBasedVolume && !isPriceBased_Chart || isPriceBased_Chart)
                DrawStartVolumeLine();
            try { PerformanceDrawing(true); } catch { } // Draw without scroll or zoom
        }

        public void SetMTFSource_TimeFrame(TimeFrame timeFrame) {
            MTFSource_TimeFrame = timeFrame;
            MTFSource_Bars = MarketData.GetBars(timeFrame);
        }

    }

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
    public enum MTF_Sources {
        Standard, Tick, Renko, Range, Heikin_Ash
    }
    public enum Standard_Sources {
        m1, m2, m3, m4, m5, m6, m7, m8, m9, m10,
        m15, m30, m45, h1, h2, h3, h4, h6, h8, h12,
        D1, D2, D3, W1, Month1
    }
    public enum Tick_Sources {
        t1, t2, t3, t4, t5, t6, t7, t8, t9, t10,
        t15, t20, t25, t30, t40, t50, t60, t80, t90, t100,
        t150, t200, t250, t300, t500, t750, t1000
    }
    public enum Renko_Sources {
        Re1, Re2, Re3, Re4, Re5, Re6, Re7, Re8, Re9, Re10,
        Re15, Re20, Re25, Re30, Re35, Re40, Re45, Re50,
        Re100, Re150, Re200, Re300, Re500, Re800, Re1000, Re2000
    }
    public enum Range_Sources {
        Ra1, Ra2, Ra3, Ra4, Ra5, Ra8, Ra10,
        Ra20, Ra30, Ra50, Ra80,
        Ra100, Ra150, Ra200, Ra300, Ra500, Ra800,
        Ra1000, Ra2000, Ra5000, Ra7500, Ra10000
    }
    public class ParamsPanel : CustomControl
    {
        private readonly WeisWyckoffSystemV20 Outside;
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

        public ParamsPanel(WeisWyckoffSystemV20 indicator, IndicatorParams defaultParams)
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
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "EnableWyckoffKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.EnableWyckoff,
                    OnChanged = _ => UpdateCheckbox("EnableWyckoffKey", val => Outside.EnableWyckoff = val),
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "ShowNumbersKey",
                    Label = "Numbers",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ShowNumbers.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Numbers_Data)),
                    OnChanged = _ => UpdateNumbers(),
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "NumbersPositionKey",
                    Label = "Position",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.NumbersPosition.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(NumbersPosition_Data)),
                    OnChanged = _ => UpdateNumbersPosition(),
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "BothPositionKey",
                    Label = "Position[Both]",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.NumbersBothPosition.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(NumbersBothPosition_Data)),
                    OnChanged = _ => UpdateNumbersBothPosition(),
                    IsVisible = () => Outside.Numbers_Input == Numbers_Data.Both
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "NumbersColorKey",
                    Label = "Coloring[nº]",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.NumbersColor.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(NumbersColor_Data)),
                    OnChanged = _ => UpdateNumbersColoring(),
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "BarsColorKey",
                    Label = "Coloring[bars]",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BarsColor.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BarsColor_Data)),
                    OnChanged = _ => UpdateBarsColoring(),
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "FillBarsKey",
                    Label = "Fill?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.FillBars,
                    OnChanged = _ => UpdateCheckbox("FillBarsKey", val => Outside.FillBars = val),
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "OutlineKey",
                    Label = "Outline?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.KeepOutline,
                    OnChanged = _ => UpdateCheckbox("OutlineKey", val => Outside.KeepOutline = val),
                },
                new()
                {
                    Region = "Wyckoff Bars",
                    RegionOrder = 0,
                    Key = "NumbersLargeKey",
                    Label = "Only Avg[nº]?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowOnlyLargeNumbers,
                    OnChanged = _ => UpdateCheckbox("NumbersLargeKey", val => Outside.ShowOnlyLargeNumbers = val),
                },

                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "StrengthFilterKey",
                    Label = "Filter",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.StrengthFilter.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(StrengthFilter_Data)),
                    OnChanged = _ => UpdateStrengthFilter(),
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "MATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.customMAtype.ToString() : p.MAtype.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateMAType(),
                    IsVisible = () => Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized && Outside.StrengthFilter_Input != StrengthFilter_Data.L1Norm
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "MAPeriodKey",
                    Label = "Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.MAperiod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateMAPeriod(),
                    IsVisible = () => Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "NmlzPeriodKey",
                    Label = "Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NormalizePeriod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateNormalizePeriod(),
                    IsVisible = () => Outside.StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "NmlzMultipKey",
                    Label = "Multiplier",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NormalizeMultiplier.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateNormalizeMultiplier(),
                    IsVisible = () => Outside.StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized
                },
                
                // Ratio
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "StrengthRatioKey",
                    Label = "Ratio",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.StrengthRatio.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(StrengthRatio_Data)),
                    OnChanged = _ => UpdateStrengthRatio(),
                    IsVisible = () => Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                

                // Percentile 
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "LowestPctileKey",
                    Label = "Lowest(<)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Lowest_Pctile.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowest_Pctile(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Percentile && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "LowPctileKey",
                    Label = "Low",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Low_Pctile.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLow_Pctile(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Percentile && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "AveragePctileKey",
                    Label = "Average",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Average_Pctile.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateAverage_Pctile(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Percentile && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "HighPctileKey",
                    Label = "High",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.High_Pctile.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHigh_Pctile(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Percentile && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "UltraPctileKey",
                    Label = "Ultra(>=)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Ultra_Pctile.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateUltra_Pctile(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Percentile && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                
                // Percentile => Period
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "PctilePeriodKey",
                    Label = "Pctile Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.PctilePeriod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdatePercentilePeriod(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Percentile && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                
                
                // Percentage => Normalized_Emphasized 
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "LowestPctKey",
                    Label = "Lowest(<)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Lowest_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowest_Pct(),
                    IsVisible = () => Outside.StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "LowPctKey",
                    Label = "Low",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Low_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLow_Pct(),
                    IsVisible = () => Outside.StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "AveragePctKey",
                    Label = "Average",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Average_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateAverage_Pct(),
                    IsVisible = () => Outside.StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "HighPctKey",
                    Label = "High",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.High_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHigh_Pct(),
                    IsVisible = () => Outside.StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "UltraPctKey",
                    Label = "Ultra(>=)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Ultra_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateUltra_Pct(),
                    IsVisible = () => Outside.StrengthFilter_Input == StrengthFilter_Data.Normalized_Emphasized
                },

                // [Debug] Show Strength
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "DebugStrengthKey",
                    Label = "Debug?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowStrength,
                    OnChanged = _ => UpdateCheckbox("DebugStrengthKey", val => Outside.ShowStrengthValue = val),
                },

                // Fixed
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "LowestFixedKey",
                    Label = "Lowest(<)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Lowest_Fixed.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowest_Fixed(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Fixed && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "LowFixedKey",
                    Label = "Low",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Low_Fixed.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLow_Fixed(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Fixed && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "AverageFixedKey",
                    Label = "Average",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Average_Fixed.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateAverage_Fixed(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Fixed && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "HighFixedKey",
                    Label = "High",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.High_Fixed.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHigh_Fixed(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Fixed && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },
                new()
                {
                    Region = "Coloring",
                    RegionOrder = 1,
                    Key = "UltraFixedKey",
                    Label = "Ultra(>=)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.Ultra_Fixed.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateUltra_Fixed(),
                    IsVisible = () => Outside.StrengthRatio_Input == StrengthRatio_Data.Fixed && Outside.StrengthFilter_Input != StrengthFilter_Data.Normalized_Emphasized
                },


                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "CurrentWaveKey",
                    Label = "Current?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ShowCurrentWave,
                    OnChanged = _ => UpdateCheckbox("CurrentWaveKey", val => Outside.ShowCurrentWave = val),
                },
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "ShowWavesKey",
                    Label = "Waves",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ShowWaves.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ShowWaves_Data)),
                    OnChanged = _ => UpdateWaves(),
                },
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "OtherWavesKey",
                    Label = "Waves(misc)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ShowOtherWaves.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ShowOtherWaves_Data)),
                    OnChanged = _ => UpdateOtherWaves(),
                },
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "MarksKey",
                    Label = "Marks",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ShowMarks.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ShowMarks_Data)),
                    OnChanged = _ => UpdateMarks(),
                },
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "RatioVolumeKey",
                    Label = "Ratio(volume)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.WW_Ratio.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateVolumeRatio()
                },
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "RatioEvsRKey",
                    Label = "Ratio(EvsR)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.EvsR_Ratio.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateEvsRRatio()
                },
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "WavesModeKey",
                    Label = "Waves(mode)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.WavesMode.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(WavesMode_Data)),
                    OnChanged = _ => UpdateWavesMode(),
                    IsVisible = () => Outside.isRenkoChart
                },
                //
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "YellowZZKey",
                    Label = "Last(ratio)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.YellowZigZag.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(YellowZigZag_Data)),
                    OnChanged = _ => UpdateYellowZZ(),
                },
                new()
                {
                    Region = "Weis Waves",
                    RegionOrder = 2,
                    Key = "YellowRenkoKey",
                    Label = "Ranging?(ratio)",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.YellowRenko_IgnoreRanging,
                    OnChanged = _ => UpdateCheckbox("YellowRenkoKey", val => Outside.YellowRenko_IgnoreRanging = val),
                    IsVisible = () => Outside.WavesMode_Input == WavesMode_Data.Reversal
                },

                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "ZigZagModeKey",
                    Label = "Mode",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ZigZagMode.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ZigZagMode_Data)),
                    OnChanged = _ => UpdateZigZagMode()
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "PercentageZZKey",
                    Label = "Value(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.PercentageZZ.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdatePercentage(),
                    IsVisible = () => Outside.ZigZagMode_Input == ZigZagMode_Data.Percentage
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "PipsZZKey",
                    Label = "Value(pips)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.PipsZZ.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdatePips(),
                    IsVisible = () => Outside.ZigZagMode_Input == ZigZagMode_Data.Pips
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "PriorityZZKey",
                    Label = "Priority(HH/HL)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.Priority.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Priority_Data)),
                    OnChanged = _ => UpdatePriority(),
                    IsVisible = () => Outside.ZigZagMode_Input == ZigZagMode_Data.NoLag_HighLow
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "ZZSourceKey",
                    Label = "Source(TF)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ZigZagSource.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ZigZagSource_Data)),
                    OnChanged = _ => UpdateZigZagSource(),
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "MTFSourceKey",
                    Label = "MTF(source)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.MTFSource_Panel.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(MTF_Sources)),
                    OnChanged = _ => UpdateMTFSource(),
                    IsVisible = () => Outside.ZigZagSource_Input == ZigZagSource_Data.MultiTF
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "MTFCandlesKey",
                    Label = "Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Standard_Sources.m30.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Standard_Sources)),
                    OnChanged = _ => UpdateCandles(),
                    IsVisible = () => Outside.ZigZagSource_Input == ZigZagSource_Data.MultiTF && (Outside.MTFSource_Panel == MTF_Sources.Standard || Outside.MTFSource_Panel == MTF_Sources.Heikin_Ash)
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "MTFRenkoKey",
                    Label = "Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Renko_Sources.Re1.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Renko_Sources)),
                    OnChanged = _ => UpdateRenko(),
                    IsVisible = () => Outside.ZigZagSource_Input == ZigZagSource_Data.MultiTF && Outside.MTFSource_Panel == MTF_Sources.Renko
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "MTFRangeKey",
                    Label = "Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Range_Sources.Ra1.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Range_Sources)),
                    OnChanged = _ => UpdateRange(),
                    IsVisible = () => Outside.ZigZagSource_Input == ZigZagSource_Data.MultiTF && Outside.MTFSource_Panel == MTF_Sources.Range
                },
                new()
                {
                    Region = "ZigZag",
                    RegionOrder = 3,
                    Key = "MTFTicksKey",
                    Label = "Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Tick_Sources.t100.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Tick_Sources)),
                    OnChanged = _ => UpdateTick(),
                    IsVisible = () => Outside.ZigZagSource_Input == ZigZagSource_Data.MultiTF && Outside.MTFSource_Panel == MTF_Sources.Tick
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
                Text = "Weis & Wyckoff System",
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
            grid.AddChild(CreateModeInfo_Button(FirstParams.Template.ToString()), 0, 1, 1, 3);
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
                var groupGrid = new Grid(9, 5); // Increase total rows for independent ratio: from 6 => 9
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
                default:
                    break;
            }

            RecalculateOutsideWithMsg();
        }

        // ==== Wyckoff Bars ====
        private void UpdateNumbers()
        {
            var selected = comboBoxMap["ShowNumbersKey"].SelectedItem;
            if (Enum.TryParse(selected, out Numbers_Data numbersType) && numbersType != Outside.Numbers_Input)
            {
                Outside.Numbers_Input = numbersType;
                RecalculateOutsideWithMsg(numbersType == Numbers_Data.None);
            }
        }
        private void UpdateNumbersPosition()
        {
            var selected = comboBoxMap["NumbersPositionKey"].SelectedItem;
            if (Enum.TryParse(selected, out NumbersPosition_Data positionType) && positionType != Outside.NumbersPosition_Input)
            {
                Outside.NumbersPosition_Input = positionType;
                RecalculateOutsideWithMsg(false);
            }
        }

        private void UpdateNumbersBothPosition()
        {
            var selected = comboBoxMap["BothPositionKey"].SelectedItem;
            if (Enum.TryParse(selected, out NumbersBothPosition_Data positionType) && positionType != Outside.NumbersBothPosition_Input)
            {
                Outside.NumbersBothPosition_Input = positionType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateNumbersColoring()
        {
            var selected = comboBoxMap["NumbersColorKey"].SelectedItem;
            if (Enum.TryParse(selected, out NumbersColor_Data colorType) && colorType != Outside.NumbersColor_Input)
            {
                Outside.NumbersColor_Input = colorType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBarsColoring()
        {
            var selected = comboBoxMap["BarsColorKey"].SelectedItem;
            if (Enum.TryParse(selected, out BarsColor_Data colorType) && colorType != Outside.BarsColor_Input)
            {
                Outside.BarsColor_Input = colorType;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Coloring ====
        private void UpdateStrengthFilter()
        {
            var selected = comboBoxMap["StrengthFilterKey"].SelectedItem;
            if (Enum.TryParse(selected, out StrengthFilter_Data filterType) && filterType != Outside.StrengthFilter_Input)
            {
                Outside.StrengthFilter_Input = filterType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateMAType()
        {
            var selected = comboBoxMap["MATypeKey"].SelectedItem;
            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.customMAtype)
                {
                    Outside.customMAtype = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.MAtype)
                {
                    Outside.MAtype = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }
        private void UpdateMAPeriod()
        {
            if (int.TryParse(textInputMap["MAPeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.MAperiod)
                {
                    Outside.MAperiod = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateNormalizePeriod()
        {
            if (int.TryParse(textInputMap["NmlzPeriodKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.NormalizePeriod)
                {
                    Outside.NormalizePeriod = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateNormalizeMultiplier()
        {
            if (int.TryParse(textInputMap["NmlzMultipKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.NormalizeMultiplier)
                {
                    Outside.NormalizeMultiplier = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }

        private void UpdatePercentilePeriod()
        {
            if (int.TryParse(textInputMap["PctilePeriodKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Pctile_Period)
                {
                    Outside.Pctile_Period = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateStrengthRatio()
        {
            var selected = comboBoxMap["StrengthRatioKey"].SelectedItem;
            if (Enum.TryParse(selected, out StrengthRatio_Data ratioType) && ratioType != Outside.StrengthRatio_Input)
            {
                Outside.StrengthRatio_Input = ratioType;
                RecalculateOutsideWithMsg(false);
            }
        }


        // Percentile
        private void UpdateLowest_Pctile()
        {
            if (int.TryParse(textInputMap["LowestPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Lowest_PctileValue)
                {
                    Outside.Lowest_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateLow_Pctile()
        {
            if (int.TryParse(textInputMap["LowPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Low_PctileValue)
                {
                    Outside.Low_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateAverage_Pctile()
        {
            if (int.TryParse(textInputMap["AveragePctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Average_PctileValue)
                {
                    Outside.Average_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateHigh_Pctile()
        {
            if (int.TryParse(textInputMap["HighPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.High_PctileValue)
                {
                    Outside.High_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateUltra_Pctile()
        {
            if (int.TryParse(textInputMap["UltraPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Ultra_PctileValue)
                {
                    Outside.Ultra_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }

        // Percentage => Normalized_Emphasized
        private void UpdateLowest_Pct()
        {
            if (double.TryParse(textInputMap["LowestPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Lowest_PctValue)
                {
                    Outside.Lowest_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateLow_Pct()
        {
            if (double.TryParse(textInputMap["LowPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Low_PctValue)
                {
                    Outside.Low_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateAverage_Pct()
        {
            if (double.TryParse(textInputMap["AveragePctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Average_PctValue)
                {
                    Outside.Average_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateHigh_Pct()
        {
            if (double.TryParse(textInputMap["HighPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.High_PctValue)
                {
                    Outside.High_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateUltra_Pct()
        {
            if (double.TryParse(textInputMap["UltraPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Ultra_PctValue)
                {
                    Outside.Ultra_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        
        // Fixed
        private void UpdateLowest_Fixed()
        {
            if (double.TryParse(textInputMap["LowestFixedKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Lowest_FixedValue)
                {
                    Outside.Lowest_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateLow_Fixed()
        {
            if (double.TryParse(textInputMap["LowFixedKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Low_FixedValue)
                {
                    Outside.Low_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateAverage_Fixed()
        {
            if (double.TryParse(textInputMap["AverageFixedKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Average_FixedValue)
                {
                    Outside.Average_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateHigh_Fixed()
        {
            if (double.TryParse(textInputMap["HighFixedKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.High_FixedValue)
                {
                    Outside.High_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateUltra_Fixed()
        {
            if (double.TryParse(textInputMap["UltraFixedKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.Ultra_FixedValue)
                {
                    Outside.Ultra_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }


        // ==== Weis Waves ====
        private void UpdateWaves()
        {
            var selected = comboBoxMap["ShowWavesKey"].SelectedItem;
            if (Enum.TryParse(selected, out ShowWaves_Data wavesType) && wavesType != Outside.ShowWaves_Input)
            {
                Outside.ShowWaves_Input = wavesType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateOtherWaves()
        {
            var selected = comboBoxMap["OtherWavesKey"].SelectedItem;
            if (Enum.TryParse(selected, out ShowOtherWaves_Data wavesType) && wavesType != Outside.ShowOtherWaves_Input)
            {
                Outside.ShowOtherWaves_Input = wavesType;
                RecalculateOutsideWithMsg(wavesType == ShowOtherWaves_Data.No);
            }
        }
        private void UpdateMarks()
        {
            var selected = comboBoxMap["MarksKey"].SelectedItem;
            if (Enum.TryParse(selected, out ShowMarks_Data markType) && markType != Outside.ShowMarks_Input)
            {
                Outside.ShowMarks_Input = markType;
                RecalculateOutsideWithMsg(markType == ShowMarks_Data.No);
            }
        }
        private void UpdateVolumeRatio()
        {
            if (double.TryParse(textInputMap["RatioVolumeKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.WW_Ratio)
                {
                    Outside.WW_Ratio = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateEvsRRatio()
        {
            if (double.TryParse(textInputMap["RatioEvsRKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.EvsR_Ratio)
                {
                    Outside.EvsR_Ratio = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateWavesMode()
        {
            var selected = comboBoxMap["WavesModeKey"].SelectedItem;
            if (Enum.TryParse(selected, out WavesMode_Data wavesType) && wavesType != Outside.WavesMode_Input)
            {
                Outside.WavesMode_Input = wavesType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateYellowZZ()
        {
            var selected = comboBoxMap["YellowZZKey"].SelectedItem;
            if (Enum.TryParse(selected, out YellowZigZag_Data yellowType) && yellowType != Outside.YellowZigZag_Input)
            {
                Outside.YellowZigZag_Input = yellowType;
                RecalculateOutsideWithMsg();
            }
        }

        // ==== ZigZag ====
        private void UpdateZigZagMode()
        {
            var selected = comboBoxMap["ZigZagModeKey"].SelectedItem;
            if (Enum.TryParse(selected, out ZigZagMode_Data zigzagType) && zigzagType != Outside.ZigZagMode_Input)
            {
                Outside.ZigZagMode_Input = zigzagType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdatePercentage()
        {
            if (double.TryParse(textInputMap["PercentageZZKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.PercentageZZ)
                {
                    Outside.PercentageZZ = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdatePips()
        {
            if (double.TryParse(textInputMap["PipsZZKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.PipsZZ)
                {
                    Outside.PipsZZ = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateZigZagSource()
        {
            var selected = comboBoxMap["ZZSourceKey"].SelectedItem;
            if (Enum.TryParse(selected, out ZigZagSource_Data sourceType) && sourceType != Outside.ZigZagSource_Input)
            {
                Outside.ZigZagSource_Input = sourceType;
                if (sourceType == ZigZagSource_Data.MultiTF) {
                    UpdateMTFSource();
                    return;
                }
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateMTFSource()
        {
            var selected = comboBoxMap["MTFSourceKey"].SelectedItem;
            if (Enum.TryParse(selected, out MTF_Sources sourceType))
            {
                Outside.MTFSource_Panel = sourceType;
                switch (sourceType)
                {
                    case MTF_Sources.Tick:
                        UpdateTick(); return;
                    case MTF_Sources.Renko:
                        UpdateRenko(); return;
                    case MTF_Sources.Range:
                        UpdateRange(); return;
                    default:
                        UpdateCandles(); return;
                }
            }
        }
        private void UpdateCandles() {
            var selected = comboBoxMap["MTFCandlesKey"].SelectedItem;

            TimeFrame value = StringToTimeframe(selected);
            if (Outside.MTFSource_Panel == MTF_Sources.Heikin_Ash)
                value = StringToTimeframe(selected, true);

            UpdateMTFInterval(value);
        }
        private void UpdateRenko() {
            var selected = comboBoxMap["MTFRenkoKey"].SelectedItem;
            TimeFrame value = StringToRenko(selected);
            UpdateMTFInterval(value);
        }
        private void UpdateRange() {
            var selected = comboBoxMap["MTFRangeKey"].SelectedItem;
            TimeFrame value = StringToRange(selected);
            UpdateMTFInterval(value);
        }
        private void UpdateTick() {
            var selected = comboBoxMap["MTFTicksKey"].SelectedItem;
            TimeFrame value = StringToTick(selected);
            UpdateMTFInterval(value);
        }
        private void UpdateMTFInterval(TimeFrame tf)
        {
            string[] timesBased = { "Minute", "Hour", "Daily", "Day", "Weekly", "Monthly" };
            string tfName = tf.ToString();
            bool isSelected = Outside.MTFSource_Panel == MTF_Sources.Tick && tfName.Contains("Tick") ||
                              Outside.MTFSource_Panel == MTF_Sources.Range && tfName.Contains("Range") ||
                              Outside.MTFSource_Panel == MTF_Sources.Renko && tfName.Contains("Renko") ||
                              Outside.MTFSource_Panel == MTF_Sources.Heikin_Ash && tfName.Contains("Heikin") ||
                              Outside.MTFSource_Panel == MTF_Sources.Standard && timesBased.Any(tfName.Contains);
            if (isSelected) {
                Outside.SetMTFSource_TimeFrame(tf);
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdatePriority()
        {
            var selected = comboBoxMap["PriorityZZKey"].SelectedItem;
            if (Enum.TryParse(selected, out Priority_Data priorityType) && priorityType != Outside.Priority_Input)
            {
                Outside.Priority_Input = priorityType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private static TimeFrame StringToTimeframe(string inputTF, bool isHeikin = false)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                case "m1": return !isHeikin ? TimeFrame.Minute : TimeFrame.HeikinMinute;
                case "m2": return !isHeikin ? TimeFrame.Minute2 : TimeFrame.HeikinMinute2;
                case "m3": return !isHeikin ? TimeFrame.Minute3 : TimeFrame.HeikinMinute3;
                case "m4": return !isHeikin ? TimeFrame.Minute4 : TimeFrame.HeikinMinute4;
                case "m5": return !isHeikin ? TimeFrame.Minute5 : TimeFrame.HeikinMinute5;
                case "m6": return !isHeikin ? TimeFrame.Minute6 : TimeFrame.HeikinMinute6;
                case "m7": return !isHeikin ? TimeFrame.Minute7 : TimeFrame.HeikinMinute7;
                case "m8": return !isHeikin ? TimeFrame.Minute8 : TimeFrame.HeikinMinute8;
                case "m9": return !isHeikin ? TimeFrame.Minute9 : TimeFrame.HeikinMinute9;
                case "m10": return !isHeikin ? TimeFrame.Minute10 : TimeFrame.HeikinMinute10;
                case "m15": return !isHeikin ? TimeFrame.Minute15 : TimeFrame.HeikinMinute15;
                case "m30": return !isHeikin ? TimeFrame.Minute30 : TimeFrame.HeikinMinute30;
                case "m45": return !isHeikin ? TimeFrame.Minute45 : TimeFrame.HeikinMinute45;
                case "h1": return !isHeikin ? TimeFrame.Hour : TimeFrame.HeikinHour;
                case "h2": return !isHeikin ? TimeFrame.Hour2 : TimeFrame.HeikinHour2;
                case "h3": return !isHeikin ? TimeFrame.Hour3 : TimeFrame.HeikinHour3;
                case "h4": return !isHeikin ? TimeFrame.Hour4 : TimeFrame.HeikinHour4;
                case "h6": return !isHeikin ? TimeFrame.Hour6 : TimeFrame.HeikinHour6;
                case "h8": return !isHeikin ? TimeFrame.Hour8 : TimeFrame.HeikinHour8;
                case "h12": return !isHeikin ? TimeFrame.Hour12 : TimeFrame.HeikinHour12;
                case "D1": return !isHeikin ? TimeFrame.Daily : TimeFrame.HeikinDaily;
                case "D2": return !isHeikin ? TimeFrame.Day2 : TimeFrame.HeikinDay2;
                case "D3": return !isHeikin ? TimeFrame.Day3 : TimeFrame.HeikinDay3;
                case "W1": return !isHeikin ? TimeFrame.Weekly : TimeFrame.HeikinWeekly;
                case "Month1": return !isHeikin ? TimeFrame.Monthly : TimeFrame.HeikinMonthly;
                default:
                    break;
            }
            return ifWrong;
        }

        private static TimeFrame StringToRenko(string inputTF)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                case "Re1": return TimeFrame.Renko1;
                case "Re2": return TimeFrame.Renko2;
                case "Re3": return TimeFrame.Renko3;
                case "Re4": return TimeFrame.Renko4;
                case "Re5": return TimeFrame.Renko5;
                case "Re6": return TimeFrame.Renko6;
                case "Re7": return TimeFrame.Renko7;
                case "Re8": return TimeFrame.Renko8;
                case "Re9": return TimeFrame.Renko9;
                case "Re10": return TimeFrame.Renko10;
                case "Re15": return TimeFrame.Renko15;
                case "Re20": return TimeFrame.Renko20;
                case "Re25": return TimeFrame.Renko25;
                case "Re30": return TimeFrame.Renko30;
                case "Re35": return TimeFrame.Renko35;
                case "Re40": return TimeFrame.Renko40;
                case "Re45": return TimeFrame.Renko45;
                case "Re50": return TimeFrame.Renko50;
                case "Re100": return TimeFrame.Renko100;
                case "Re150": return TimeFrame.Renko150;
                case "Re200": return TimeFrame.Renko200;
                case "Re300": return TimeFrame.Renko300;
                case "Re500": return TimeFrame.Renko500;
                case "Re800": return TimeFrame.Renko800;
                case "Re1000": return TimeFrame.Renko1000;
                case "Re2000": return TimeFrame.Renko2000;
                default:
                    break;
            }
            return ifWrong;
        }
        private static TimeFrame StringToRange(string inputTF)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                case "Ra1": return TimeFrame.Range1;
                case "Ra2": return TimeFrame.Range2;
                case "Ra3": return TimeFrame.Range3;
                case "Ra4": return TimeFrame.Range4;
                case "Ra5": return TimeFrame.Range5;
                case "Ra8": return TimeFrame.Range8;
                case "Ra10": return TimeFrame.Range10;
                case "Ra20": return TimeFrame.Range20;
                case "Ra30": return TimeFrame.Range30;
                case "Ra50": return TimeFrame.Range50;
                case "Ra80": return TimeFrame.Range80;
                case "Ra100": return TimeFrame.Range100;
                case "Ra150": return TimeFrame.Range150;
                case "Ra200": return TimeFrame.Range200;
                case "Ra300": return TimeFrame.Range300;
                case "Ra500": return TimeFrame.Range500;
                case "Ra800": return TimeFrame.Range800;
                case "Ra1000": return TimeFrame.Range1000;
                case "Ra2000": return TimeFrame.Range2000;
                case "Ra5000": return TimeFrame.Range5000;
                case "Ra7500": return TimeFrame.Range7500;
                case "Ra10000": return TimeFrame.Range10000;
                default:
                    break;
            }
            return ifWrong;
        }
        private static TimeFrame StringToTick(string inputTF)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                case "t1": return TimeFrame.Tick;
                case "t2": return TimeFrame.Tick2;
                case "t3": return TimeFrame.Tick3;
                case "t4": return TimeFrame.Tick4;
                case "t5": return TimeFrame.Tick5;
                case "t6": return TimeFrame.Tick6;
                case "t7": return TimeFrame.Tick7;
                case "t8": return TimeFrame.Tick8;
                case "t9": return TimeFrame.Tick9;
                case "t10": return TimeFrame.Tick10;
                case "t15": return TimeFrame.Tick15;
                case "t20": return TimeFrame.Tick20;
                case "t25": return TimeFrame.Tick25;
                case "t30": return TimeFrame.Tick30;
                case "t40": return TimeFrame.Tick40;
                case "t50": return TimeFrame.Tick50;
                case "t60": return TimeFrame.Tick60;
                case "t80": return TimeFrame.Tick80;
                case "t90": return TimeFrame.Tick90;
                case "t100": return TimeFrame.Tick100;
                case "t150": return TimeFrame.Tick150;
                case "t200": return TimeFrame.Tick200;
                case "t300": return TimeFrame.Tick300;
                case "t500": return TimeFrame.Tick500;
                case "t750": return TimeFrame.Tick750;
                case "t1000": return TimeFrame.Tick1000;
                default:
                    break;
            }
            return ifWrong;
        }

        private void RecalculateOutsideWithMsg(bool reset = true, bool isTemplate = false)
        {
            // Avoid multiples calls when loading parameters from LocalStorage
            if (isLoadingParams)
                return;

            string current = isTemplate ? ModeBtn.Text : "Custom";
            if (!isTemplate)
                Outside.Template_Input = Template_Data.Custom;

            ModeBtn.Text = $"{current}\nCalculating...";
            Outside.BeginInvokeOnMainThread(() => {
                try { _progressBar.IsIndeterminate = true; } catch { }
            });

            if (reset) {
                Outside.BeginInvokeOnMainThread(() => {
                    Outside.Chart.RemoveAllObjects();
                    Outside.Chart.ResetBarColors();
                });
            }

            Outside.BeginInvokeOnMainThread(() => {
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

            Outside.Template_Input = Outside.Template_Input switch
            {
                Template_Data.Insider => Template_Data.Volume,
                Template_Data.Volume => Template_Data.Time,
                Template_Data.Time => Template_Data.BigBrain,
                Template_Data.BigBrain => Template_Data.Insider,
                _ => Template_Data.Insider
            };
            ModeBtn.Text = Outside.Template_Input.ToString();

            ChangePanelParams_DesignTemplates();
            ChangePanelParams_SpecificChartTemplates();

            RefreshVisibility();
            RecalculateOutsideWithMsg(true, true);

            cleaningProgress.Complete(PopupNotificationState.Success);
        }
        private void PrevModeEvent(ButtonClickEventArgs e)
        {
            PopupNotification  cleaningProgress = Outside.Notifications.ShowPopup(
                Outside.NOTIFY_CAPTION,
                "Cleaning up the chart...",
                PopupNotificationState.InProgress
            );

            Outside.Template_Input = Outside.Template_Input switch
            {
                Template_Data.BigBrain => Template_Data.Time,
                Template_Data.Time => Template_Data.Volume,
                Template_Data.Volume => Template_Data.Insider,
                Template_Data.Insider => Template_Data.BigBrain,
                _ => Template_Data.Insider
            };
            ModeBtn.Text = Outside.Template_Input.ToString();

            ChangePanelParams_DesignTemplates();
            ChangePanelParams_SpecificChartTemplates();

            RefreshVisibility();
            RecalculateOutsideWithMsg(true, true);

            cleaningProgress.Complete(PopupNotificationState.Success);
        }

        private void ChangePanelParams_DesignTemplates() {
            switch (Outside.Template_Input)
            {
                case Template_Data.Insider:
                    comboBoxMap["ShowNumbersKey"].SelectedItem = $"{Numbers_Data.Both}";
                    comboBoxMap["NumbersPositionKey"].SelectedItem = $"{NumbersPosition_Data.Inside}";
                    comboBoxMap["NumbersColorKey"].SelectedItem = $"{NumbersColor_Data.Volume}";
                    comboBoxMap["BarsColorKey"].SelectedItem = $"{BarsColor_Data.Volume}";

                    comboBoxMap["ShowWavesKey"].SelectedItem = $"{ShowWaves_Data.EffortvsResult}";
                    comboBoxMap["OtherWavesKey"].SelectedItem = $"{ShowOtherWaves_Data.Both}";
                    comboBoxMap["MarksKey"].SelectedItem = $"{ShowMarks_Data.No}";

                    checkBoxMap["EnableWyckoffKey"].IsChecked = true;
                    Outside.EnableWyckoff = true;
                    checkBoxMap["CurrentWaveKey"].IsChecked = true;
                    Outside.ShowCurrentWave = true;
                    checkBoxMap["FillBarsKey"].IsChecked = true;
                    Outside.FillBars = true;
                    checkBoxMap["OutlineKey"].IsChecked = false;
                    Outside.KeepOutline = false;

                    Outside.Chart.ChartType = ChartType.Candlesticks;
                    break;
                case Template_Data.Volume:
                    comboBoxMap["ShowNumbersKey"].SelectedItem = $"{Numbers_Data.Volume}";
                    comboBoxMap["NumbersPositionKey"].SelectedItem = $"{NumbersPosition_Data.Inside}";
                    comboBoxMap["NumbersColorKey"].SelectedItem = $"{NumbersColor_Data.Volume}";
                    comboBoxMap["BarsColorKey"].SelectedItem = $"{BarsColor_Data.Volume}";

                    comboBoxMap["ShowWavesKey"].SelectedItem = $"{ShowWaves_Data.Volume}";
                    comboBoxMap["OtherWavesKey"].SelectedItem = $"{ShowOtherWaves_Data.Price}";
                    comboBoxMap["MarksKey"].SelectedItem = $"{ShowMarks_Data.No}";

                    checkBoxMap["EnableWyckoffKey"].IsChecked = true;
                    checkBoxMap["CurrentWaveKey"].IsChecked = true;
                    checkBoxMap["FillBarsKey"].IsChecked = true;
                    checkBoxMap["OutlineKey"].IsChecked = false;
                    break;
                case Template_Data.Time:
                    comboBoxMap["ShowNumbersKey"].SelectedItem = $"{Numbers_Data.Time}";
                    comboBoxMap["NumbersPositionKey"].SelectedItem = $"{NumbersPosition_Data.Inside}";
                    comboBoxMap["NumbersColorKey"].SelectedItem = $"{NumbersColor_Data.Time}";
                    comboBoxMap["BarsColorKey"].SelectedItem = $"{BarsColor_Data.Time}";

                    comboBoxMap["ShowWavesKey"].SelectedItem = $"{ShowWaves_Data.EffortvsResult}";
                    comboBoxMap["OtherWavesKey"].SelectedItem = $"{ShowOtherWaves_Data.Time}";
                    comboBoxMap["MarksKey"].SelectedItem = $"{ShowMarks_Data.No}";

                    checkBoxMap["EnableWyckoffKey"].IsChecked = true;
                    checkBoxMap["CurrentWaveKey"].IsChecked = true;
                    checkBoxMap["FillBarsKey"].IsChecked = true;
                    checkBoxMap["OutlineKey"].IsChecked = false;
                    break;
                case Template_Data.BigBrain:
                    comboBoxMap["ShowNumbersKey"].SelectedItem = $"{Numbers_Data.Both}";
                    comboBoxMap["NumbersPositionKey"].SelectedItem = $"{NumbersPosition_Data.Inside}";

                    // causes a 2x UI update, no idea why.
                    // comboBoxMap["NumbersColorKey"].SelectedItem = $"{NumbersColor_Data.Time}";
                    // comboBoxMap["BarsColorKey"].SelectedItem = $"{BarsColor_Data.Volume}";

                    comboBoxMap["ShowWavesKey"].SelectedItem = $"{ShowWaves_Data.Both}";
                    comboBoxMap["OtherWavesKey"].SelectedItem = $"{ShowOtherWaves_Data.Both}";
                    comboBoxMap["MarksKey"].SelectedItem = $"{ShowMarks_Data.Both}";

                    checkBoxMap["EnableWyckoffKey"].IsChecked = true;
                    Outside.EnableWyckoff = true;
                    checkBoxMap["CurrentWaveKey"].IsChecked = true;
                    Outside.ShowCurrentWave = true;
                    checkBoxMap["FillBarsKey"].IsChecked = true;
                    Outside.FillBars = true;
                    checkBoxMap["OutlineKey"].IsChecked = false;
                    Outside.KeepOutline = false;
                    break;
                default: break;
            }
        }
        private void ChangePanelParams_SpecificChartTemplates() {
            if (Outside.Template_Input == Template_Data.Custom)
                return;
            // Tick / Time-Based Chart (Standard Candles/Heikin-Ash)
            if (Outside.isTickChart || !Outside.isPriceBased_Chart) {
                if (Outside.isTickChart) {
                    comboBoxMap["StrengthFilterKey"].SelectedItem = $"{StrengthFilter_Data.Both}";

                    textInputMap["MAPeriodKey"].Text = "20";
                    comboBoxMap["MATypeKey"].SelectedItem = $"{MovingAverageType.Triangular}";

                    textInputMap["LowestFixedKey"].Text = "0.5";
                    textInputMap["LowFixedKey"].Text = "1.2";
                    textInputMap["AverageFixedKey"].Text = "2.5";
                    textInputMap["HighFixedKey"].Text = "3.5";
                    textInputMap["UltraFixedKey"].Text = "3.51";
                }
            }
            // Range
            if (Outside.isPriceBased_Chart && !Outside.isRenkoChart && !Outside.isTickChart) {
                comboBoxMap["ShowNumbersKey"].SelectedItem = $"{Numbers_Data.Volume}";
                comboBoxMap["StrengthFilterKey"].SelectedItem = $"{StrengthFilter_Data.MA}";

                textInputMap["MAPeriodKey"].Text = "20";
                comboBoxMap["MATypeKey"].SelectedItem = $"{MovingAverageType.Triangular}";

                textInputMap["LowestFixedKey"].Text = "0.5";
                textInputMap["LowFixedKey"].Text = "1.2";
                textInputMap["AverageFixedKey"].Text = "2.5";
                textInputMap["HighFixedKey"].Text = "3.5";
                textInputMap["UltraFixedKey"].Text = "3.51";
            }
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
                ? $"WWS {BrokerPrefix} {SymbolPrefix} {TimeframePrefix}"
                : $"WWS {SymbolPrefix} {TimeframePrefix}";
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
            storageModel.Params["PanelMode"] = Outside.Template_Input;

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
            string templateText = storageModel.Params["PanelMode"].ToString();
            _ = Enum.TryParse(templateText, out Template_Data templateMode);
            Outside.Template_Input = templateMode;
            ModeBtn.Text = templateText;

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
