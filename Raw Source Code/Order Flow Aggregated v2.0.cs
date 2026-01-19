/*
--------------------------------------------------------------------------------------------------------------------------------
                        Order Flow Agreggated v2.0
                                revision 2

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

What's new in rev. 2? (2026)
"Percentages Everywhere"

(ODF) Spike(ratio) / Bubbles(ratio):
  - [Fixed / Percentage or Percentile] type
  - Independent Ratios on Params-Panel
  - Move "[Debug] Show Strength Value?" standard input to Params-Panel
(ODF) Tick Spike:
  - [Delta, Delta_BuySell_Sum, Sum_Delta] sources alternatives
  - [L1Norm, SoftMax_Power] filters alternatives
    - These filters are not suitable for real-time notification (strength values are unstable until the bar is closed)
    - So, the spike notification will be processed after its strength confirmation (bar closed) in these filters
(ODF) Bubbles Chart:
  - "Change?" for any source.
  - [Delta_BuySell_Sum, Sum_Delta] sources alternatives
  - [SoftMax_Power, L2Norm, MinMax] filters alternatives

(VP) HVN + LVN:
  - Detection:
    - Smoothing => [Gaussian, Savitzky_Golay]
    - Nodes => [LocalMinMax, Topology, Percentile]
    - (Tip) Use "Percentile" for "Savitzky_Golay".
  - Levels(bands)
    - VA-like (set by percentage)
    - (Tip) Use 'LineStyles = [Solid, Lines, LinesDots]" if any stuttering/lagging occurs when scrolling at profiles on chart (Reduce GPU workload).
(VP-Fix) Concurrent Live VP always crashing.

(cTrader Inputs) Add "Panel Mode" input:
  - 'Volume_Profile' => Only related VP inputs will be show and used.
  - 'Order_Flow_Ticks' => Only related ODF inputs will be show and used.
  - 'Both' => Self-explanatory
  
  - Just use "Both
    - if "MiniVPs <= Daily" or "Main VP?" are used.
  - Or run 2 instances of ODF_AGG 
    - On the same chart with distinct PanelMode

(CODE) Improved Performance of:
  - (ODF) Tick Spike
  - (ODF) Bubbles Chart
  - (VP) Fixed Range
  - (VP) Main VP (uses "ODF + VP" input)
  - (BOTH) 'Results'
(CODE) Massive refactor/restructure of the entire code...(finally!)
  - It's still "all-in-one" .cs file, though.

(Off-topic) Python version finally shows its advantage! hehehe
(Off-topic) New features developed in C# version
    - "Change" for any delta-result => value from nº bars instead of previous bar.

===========================
'Sprint of ODF_Agg development'

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

        public enum PanelSwitch_Data
        {
            Volume_Profile,
            Order_Flow_Ticks,
            Both
        }
        [Parameter("Panel Mode:", DefaultValue = PanelSwitch_Data.Both, Group = "==== Order Flow Aggregated v2.0 ====")]
        public PanelSwitch_Data PanelSwitch_Input { get; set; }
        
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



        [Parameter("[ODF] Use Custom MAs?", DefaultValue = true, Group = "==== Specific Parameters ====")]
        public bool UseCustomMAs { get; set; }

        public enum UpdateVPStrategy_Data
        {
            Concurrent,
            SameThread_MayFreeze
        }
        [Parameter("[VP] Update Strategy", DefaultValue = UpdateVPStrategy_Data.Concurrent, Group = "==== Specific Parameters ====")]
        public UpdateVPStrategy_Data UpdateVPStrategy_Input { get; set; }

        [Parameter("[Renko] Show Wicks?", DefaultValue = true, Group = "==== Specific Parameters ====")]
        public bool ShowWicks { get; set; }


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


        [Parameter("[Levels][Spike] Show Touch Value?", DefaultValue = false, Group = "==== Debug(both) ====")]
        public bool SpikeLevels_ShowValue { get; set; }

        [Parameter("[Levels][Bubbles] Show Touch Value?", DefaultValue = false, Group = "==== Debug(both) ====")]
        public bool UltraBubbles_ShowValue { get; set; }


        [Parameter("Bubbles Chart Opacity(%):", DefaultValue = 40, MinValue = 1, MaxValue = 100, Group = "==== Spike HeatMap Coloring ====")]
        public int SpikeChart_Opacity { get; set; }

        [Parameter("Lowest Color:", DefaultValue = "Aqua", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeLowest_Color { get; set; }

        [Parameter("Low Color:", DefaultValue = "White", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeLow_Color { get; set; }

        [Parameter("Average Color:", DefaultValue = "#DAFFFF00", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeAverage_Color { get; set; }

        [Parameter("High Color:", DefaultValue = "#DAFFC000", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeHigh_Color { get; set; }

        [Parameter("Ultra Color:", DefaultValue = "#DAFF0000", Group = "==== Spike HeatMap Coloring ====")]
        public Color SpikeUltra_Color { get; set; }


        [Parameter("Opacity(%):", DefaultValue = 70, MinValue = 1, Step = 1, MaxValue = 100, Group = "==== Bubbles HeatMap Coloring ====")]
        public int BubblesOpacity { get; set; }

        [Parameter("Lowest Color:", DefaultValue = "Aqua", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapLowest_Color { get; set; }

        [Parameter("Low Color:", DefaultValue = "White", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapLow_Color { get; set; }

        [Parameter("Average Color:", DefaultValue = "Yellow", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapAverage_Color { get; set; }

        [Parameter("High Color:", DefaultValue = "Goldenrod", Group = "==== Bubbles HeatMap Coloring ====")]
        public Color HeatmapHigh_Color { get; set; }

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


        // ========= Moved from cTrader Input to Params Panel =========

        // ==== General ====
        public enum VolumeMode_Data
        {
            Normal,
            Buy_Sell,
            Delta,
        }
        public enum VolumeView_Data
        {
            Divided,
            Profile,
        }

        public class GeneralParams_Info {
            public int Lookback = 1;
            public VolumeMode_Data VolumeMode_Input = VolumeMode_Data.Delta;
            public VolumeView_Data VolumeView_Input = VolumeView_Data.Profile;

            // Coloring region - only for VolumeView_Data.Divided
            public bool ColoringOnlyLarguest = true;
        }
        public GeneralParams_Info GeneralParams = new();


        // ==== Volume Profile ====
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
        public class ProfileParams_Info {
            public bool EnableMainVP = false;

            // View
            public UpdateProfile_Data UpdateProfile_Input = UpdateProfile_Data.Through_2_Segments_Best;
            public bool FillHist_VP = false;
            public bool ShowHistoricalNumbers = false;
            public HistSide_Data HistogramSide_Input = HistSide_Data.Left;
            public HistWidth_Data HistogramWidth_Input = HistWidth_Data._70;

            // FWM Profiles
            public bool EnableFixedRange = false;
            public bool EnableWeeklyProfile = false;
            public bool EnableMonthlyProfile = false;

            // Intraday Profiles
            public bool ShowIntradayProfile = false;
            public bool ShowIntradayNumbers = false;
            public int OffsetBarsInput = 1;
            public TimeFrame OffsetTimeframeInput = TimeFrame.Hour;
            public bool FillIntradaySpace = false;

            // Mini VPs
            public bool EnableMiniProfiles = false;
            public TimeFrame MiniVPs_Timeframe = TimeFrame.Hour4;
            public bool ShowMiniResults = true;
        }
        public ProfileParams_Info ProfileParams = new();


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

            public bool EnableNodeDetection = false;

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

        // ==== Spike Filter ====
        public enum SpikeView_Data
        {
            Bubbles,
            Icon,
        }
        public enum SpikeSource_Data
        {
            Delta,
            Delta_BuySell_Sum,
            Sum_Delta,
        }
        public enum SpikeFilter_Data
        {
            MA,
            Standard_Deviation,
            L1Norm,
            SoftMax_Power
        }
        public enum NotificationType_Data
        {
            Popup,
            Sound,
            Both
        }
        public enum SpikeChartColoring_Data
        {
            Heatmap,
            Positive_Negative,
            PlusMinus_Highlight_Heatmap,
        }

        public class SpikeFilterParams_Info {
            public bool EnableSpikeFilter = true;
            public SpikeView_Data SpikeView_Input = SpikeView_Data.Icon;
            public ChartIconType IconView_Input = ChartIconType.Square;

            // Filter Settings
            public SpikeSource_Data SpikeSource_Input = SpikeSource_Data.Delta;
            public SpikeFilter_Data SpikeFilter_Input = SpikeFilter_Data.MA;

            public MovingAverageType MAtype = MovingAverageType.Simple;
            public int MAperiod = 20;

            // Notifications
            public bool EnableSpikeNotification = true;
            public NotificationType_Data NotificationType_Input = NotificationType_Data.Both;
            public SoundType Spike_SoundType = SoundType.Confirmation;

            // Chart
            public bool EnableSpikeChart = false;
            public SpikeChartColoring_Data SpikeChartColoring_Input = SpikeChartColoring_Data.Heatmap;
        }
        public SpikeFilterParams_Info SpikeFilterParams = new();


        // ==== Spike Levels ====
        public enum SpikeLevelsColoring_Data
        {
            Heatmap,
            Positive_Negative
        }
        public class SpikeLevelParams_Info {
            public bool ShowSpikeLevels = false;
            public bool ResetDaily = true;
            public int MaxCount = 2;

            public SpikeLevelsColoring_Data SpikeLevelsColoring_Input = SpikeLevelsColoring_Data.Positive_Negative;
        }
        public SpikeLevelParams_Info SpikeLevelParams = new();


        // ==== Spike Ratio ====
        public enum SpikeRatio_Data
        {
            Fixed,
            Percentage,
        }
        public class SpikeRatioParams_Info {
            public SpikeRatio_Data SpikeRatio_Input = SpikeRatio_Data.Percentage;
            public MovingAverageType MAtype_PctSpike = MovingAverageType.Simple;
            public int MAperiod_PctSpike = 20;
            public bool ShowStrengthValue = false;

            // Fixed Ratio
            public double Lowest_FixedValue = 0.5;
            public double Low_FixedValue = 1;
            public double Average_FixedValue = 1.5;
            public double High_FixedValue = 2;
            public double Ultra_FixedValue = 2.01;

            // Percentage Ratio
            public double Lowest_PctValue = 38.2;
            public double Low_PctValue = 61.8;
            public double Average_PctValue = 78.6;
            public double High_PctValue = 100;
            public double Ultra_PctValue = 101;
        }
        public SpikeRatioParams_Info SpikeRatioParams = new();


        // ==== Bubbles Chart ====
        public enum BubblesSource_Data
        {
            Delta,
            Delta_BuySell_Sum,
            Subtract_Delta,
            Sum_Delta,
        }
        public enum ChangeOperator_Data {
            Plus_KeepSign,
            Minus_KeepSign,
            Plus_Absolute,
            Minus_Absolue
        }
        public enum BubblesFilter_Data
        {
            MA,
            Standard_Deviation,
            Both,
            SoftMax_Power,
            L2Norm,
            MinMax,
        }
        public enum BubblesColoring_Data
        {
            Heatmap,
            Momentum,
        }
        public enum BubblesMomentum_Data
        {
            Fading,
            Positive_Negative,
        }

        public class BubblesChartParams_Info {
            public bool EnableBubblesChart = false;

            // Filter Settings
            public BubblesSource_Data BubblesSource_Input = BubblesSource_Data.Delta;
            
            public bool UseChangeSeries = false;
            public int changePeriod = 4;
            public ChangeOperator_Data ChangeOperator_Input = ChangeOperator_Data.Plus_KeepSign;

            public BubblesFilter_Data BubblesFilter_Input = BubblesFilter_Data.MA;
            public MovingAverageType MAtype = MovingAverageType.Exponential;
            public int MAperiod = 20;

            // View
            public double BubblesSizeMultiplier = 2;
            public BubblesColoring_Data BubblesColoring_Input = BubblesColoring_Data.Heatmap;
            public BubblesMomentum_Data BubblesMomentum_Input = BubblesMomentum_Data.Fading;
        }
        public BubblesChartParams_Info BubblesChartParams = new();



        // ==== Ultra Bubbles Levels ====
        public enum UltraBubbles_RectSizeData
        {
            High_Low,
            HighOrLow_Close,
            Bubble_Size,
        }
        public enum UltraBubblesBreak_Data
        {
            Close_Only,
            Close_plus_BarBody,
            OHLC_plus_BarBody,
        }
        public enum UltraBubblesColoring_Data
        {
            Bubble_Color,
            Positive_Negative
        }

        public class BubblesLevelParams_Info {
            public bool ShowUltraLevels = false;

            // Notification
            public bool EnableUltraNotification = true;
            public NotificationType_Data NotificationType_Input = NotificationType_Data.Both;
            public SoundType Ultra_SoundType = SoundType.PositiveNotification;

            // Levels settings
            public bool ResetDaily = true;
            public int MaxCount = 5;
            public UltraBubbles_RectSizeData UltraBubbles_RectSizeInput = UltraBubbles_RectSizeData.Bubble_Size;
            public UltraBubblesBreak_Data UltraBubblesBreak_Input = UltraBubblesBreak_Data.Close_Only;

            // View
            public UltraBubblesColoring_Data UltraBubblesColoring_Input = UltraBubblesColoring_Data.Positive_Negative;
        }
        public BubblesLevelParams_Info BubblesLevelParams = new();


        public enum BubblesRatio_Data
        {
            Fixed,
            Percentile,
        }
        public class BubblesRatioParams_Info {
            public BubblesRatio_Data BubblesRatio_Input = BubblesRatio_Data.Percentile;
            public int PctilePeriod = 20;
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
        }
        public BubblesRatioParams_Info BubblesRatioParams = new();


        // ==== Results ====
        public enum ResultsView_Data
        {
            Percentage,
            Value,
            Both
        }
        public enum OperatorBuySell_Data
        {
            Sum,
            Subtraction,
        }

        public class ResultParams_Info {
            public bool ShowResults = true;

            // Large Filter
            public bool EnableLargeFilter = true;
            public MovingAverageType MAtype = MovingAverageType.Exponential;
            public int MAperiod = 5;
            public double LargeRatio = 1.5;

            // Buy_Sell / Delta
            public ResultsView_Data ResultsView_Input = ResultsView_Data.Percentage;
            public bool ShowSideTotal = true;
            public OperatorBuySell_Data OperatorBuySell_Input = OperatorBuySell_Data.Subtraction;

            // Delta
            public bool ShowMinMaxDelta = false;
            public bool ShowOnlySubtDelta = true;
        }
        public ResultParams_Info ResultParams = new();


        // ==== Misc ====
        public enum SegmentsInterval_Data
        {
            Daily,
            Weekly,
            Monthly
        }
        public enum ODFInterval_Data
        {
            Daily,
            Weekly,
        }

        public class MiscParams_Info {
            public bool ShowHist = true;
            public bool FillHist = true;
            public bool ShowNumbers = true;
            public int DrawAtZoom_Value = 80;

            public SegmentsInterval_Data SegmentsInterval_Input = SegmentsInterval_Data.Weekly;
            public ODFInterval_Data ODFInterval_Input = ODFInterval_Data.Daily;

            public bool ShowBubbleValue = true;
        }
        public MiscParams_Info MiscParams = new();

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
        private readonly Dictionary<int, SegmentsExtremumInfo> segmentInfo = new();
        private readonly Dictionary<int, List<double>> segmentsDict = new();

        // Order Flow Ticks
        private List<double> Segments_Bar = new();
        private readonly Dictionary<double, int> VolumesRank = new();
        private readonly Dictionary<double, int> VolumesRank_Up = new();
        private readonly Dictionary<double, int> VolumesRank_Down = new();
        private readonly Dictionary<double, int> DeltaRank = new();
        private int[] MinMaxDelta = { 0, 0 };

        // Volume Profile Ticks
        private List<double> Segments_VP = new();
        private Dictionary<double, double> VP_VolumesRank = new();
        private Dictionary<double, double> VP_VolumesRank_Up = new();
        private Dictionary<double, double> VP_VolumesRank_Down = new();
        private Dictionary<double, double> VP_VolumesRank_Subt = new();
        private Dictionary<double, double> VP_DeltaRank = new();
        private double[] VP_MinMaxDelta = { 0, 0 };

        // Weekly, Monthly and Mini VPs
        public class VolumeRankType
        {
            public Dictionary<double, double> Normal = new();
            public Dictionary<double, double> Up = new();
            public Dictionary<double, double> Down = new();
            public Dictionary<double, double> Delta = new();
            public double[] MinMaxDelta = new double[2];

            public void ClearAllModes() {

                Dictionary<double, double>[] _all = new[] {
                    Normal, Up, Down, Delta,
                };

                foreach (var dict in _all)
                    dict.Clear();

                double[] resetDelta = {0, 0};
                MinMaxDelta = resetDelta;
            }
        }
        private readonly VolumeRankType MonthlyRank = new();
        private readonly VolumeRankType WeeklyRank = new();
        private readonly VolumeRankType MiniRank = new();
        private readonly Dictionary<string, VolumeRankType> FixedRank = new();

        // Fixed Range Profile
        public class RangeObjs_Info {
            public List<ChartRectangle> rectangles = new();
            public Dictionary<string, List<ChartText>> infoObjects = new();
            public Dictionary<string, Border> controlGrids = new();
        }
        private readonly RangeObjs_Info RangeObjs = new();

        // HVN + LVN => Performance
        public double[] nodesKernel = null;

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

        /*
          Its a annoying behavior that happens even in Candles Chart (Time-Based) on any symbol/broker.
          where it's jump/pass +1 index when .GetIndexByTime is used... the exactly behavior of Price-Based Charts
          Seems to happen only in Lower Timeframes (<=´Daily)
          So, to ensure that it works flawless, an additional verification is needed.
        */
        public class CleanedIndex {
            public int MainVP = 0;
            public int Mini = 0;
            public void ResetAll() {
                MainVP = 0;
                Mini = 0;
            }
        }
        private readonly CleanedIndex ClearIdx = new();

        // Concurrent Live VP Update
        private class LockObjs_Info {
            public readonly object Bar = new();
            public readonly object Tick = new();
            public readonly object MainVP = new();
            public readonly object WeeklyVP = new();
            public readonly object MonthlyVP = new();
            public readonly object MiniVP = new();
        }
        private readonly LockObjs_Info _Locks = new();

        private class TaskObjs_Info {
            public CancellationTokenSource cts;
            public Task MainVP;
            public Task WeeklyVP;
            public Task MonthlyVP;
            public Task MiniVP;
        }
        private readonly TaskObjs_Info _Tasks = new();

        private bool liveVP_RunWorker = false;

        public class LiveVPIndex {
            public int MainVP { get; set; }
            public int Mini { get; set; }
            public int Weekly { get; set; }
            public int Monthly { get; set; }
        }
        private readonly LiveVPIndex LiveVPIndexes = new();

        private DateTime[] BarTimes_Array = Array.Empty<DateTime>();
        private IEnumerable<Bar> TickBars_List;

        // High-Performance VP_Tick()
        private class PerfTickIndex {
            public int startIdx_MainVP = 0;
            public int startIdx_Mini = 0;
            public int startIdx_Weekly = 0;
            public int startIdx_Monthly = 0;

            public int lastIdx_MainVP = 0;
            public int lastIdx_Mini = 0;
            public int lastIdx_Weekly = 0;
            public int lastIdx_Monthly = 0;

            public int lastIdx_Bars = 0;
            public int lastIdx_Wicks = 0;

            public Dictionary<DateTime, int> IndexesByDate = new();

            public void ResetAll() {
                lastIdx_MainVP = 0;
                lastIdx_Mini = 0;
                lastIdx_Weekly = 0;
                lastIdx_Monthly = 0;
                lastIdx_Bars = 0;
                lastIdx_Wicks = 0;
            }
        }
        private readonly PerfTickIndex PerformanceTick = new();

        // Tick Volume
        public class TickObjs_Info {
            public DateTime firstTickTime;
            public DateTime fromDateTime;
            public ProgressBar syncProgressBar = null;
            public PopupNotification asyncPopup = null;
            public bool startAsyncLoading = false;
            public bool isLoadingComplete = false;
        }
        private readonly TickObjs_Info TickObjs = new();

        private Bars TicksOHLC;

        // Timer
        private class TimerHandler {
            public bool isAsyncLoading = false;
        }
        private readonly TimerHandler timerHandler = new();

        // Shared rowHeight
        private double heightPips = 4;
        public double heightATR = 4;
        private double rowHeight = 0;

        private double prevUpdatePrice;
        public bool isPriceBased_Chart = false;
        public bool isRenkoChart = false;

        // Some required utils
        public class BooleanUtils_Info {
            public bool segmentsConflict = false;
            public bool configHasChanged = false;
            public bool isPriceBased_NewBar = false;

            public bool isUpdateVP = false;
        }
        private readonly BooleanUtils_Info BooleanUtils = new();
        
        public class BooleanLocks_Info {
            // lock[...] mainly because of the Segments loop
            // Avoid Historical Data
            public bool spikeNotify = true;
            public bool ultraNotify = true;
            
            public bool ultraNotify_NewBar = false;
            public bool spikeNotify_NewBar = false;

            public bool lastIsUltra = false;
            public bool lastIsAvg = false;

            // Allow Historical Data
            // Although it needs to be redefined to false before each OrderFlow() call in Historical Data.
            public bool ultraLevels = false;
            public bool spikeLevels = false;

            public void SetAllToFalse() {
                spikeNotify = false;
                ultraNotify = false;
                
                ultraLevels = false;
                spikeLevels = false;
            }
            public void LevelsToFalse() {                
                ultraLevels = false;
                spikeLevels = false;
            }
            public void SetAllNewBar() {
                ultraNotify_NewBar = true;
                spikeNotify_NewBar = true;
            }
        }
        private readonly BooleanLocks_Info BooleanLocks = new();
        
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
        private readonly Dictionary<double, RectInfo> ultraRectangles = new ();
        private readonly Dictionary<string, RectInfo> spikeRectangles = new ();

        // Filters
        // DynamicSeries can be Normal, Buy_Sell or Delta Volume
        private IndicatorDataSeries Dynamic_Series, DeltaChange_Series, DeltaBuySell_Sum_Series,
                                    SubtractDelta_Series, SumDelta_Series,
                                    PercentageRatio_Series, PercentileRatio_Series;
        private MovingAverage MABubbles_Delta, MABubbles_DeltaChange, MABubbles_DeltaBuySell_Sum,
                              MABubbles_SubtractDelta, MABubbles_SumDelta,
                              MARatio_Percentage,
                              MASpike_Delta, MASpike_DeltaBuySell_Sum, MASpike_SumDelta,
                              MADynamic_LargeFilter, MASubtract_LargeFilter;
        private StandardDeviation StdDevBubbles_Delta, StdDevBubbles_DeltaChange, StdDevBubbles_DeltaBuySell_Sum,
                                  StdDevBubbles_SubtractDelta, StdDevBubbles_SumDelta,
                                  StdDevSpike_Delta, StdDevSpike_DeltaBuySell_Sum, StdDevSpike_SumDelta;

        // _Results => Raw Values
        private readonly Dictionary<int, int> Delta_Results = new();
        private readonly Dictionary<int, int> DeltaChange_Results = new();
        private readonly Dictionary<int, int> DeltaBuySell_Sum_Results = new();
        private readonly Dictionary<int, int> SubtractDelta_Results = new();
        private readonly Dictionary<int, int> SumDelta_Results = new();
        
        
        // Performance Drawing
        public class DrawInfo
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
        public enum DrawType
        {
            Text,
            Icon,
            Ellipse,
            Rectangle
        }

        public class PerfDrawingObjs_Info {
            /*
              Redraw should use another dict as value,
              to avoid creating previous Volume Modes objects
              or previous objects from Static Update.
              - intKey is the Bar index
              - stringKey is the DrawInfo.Id (object name)
              - DrawInfo is the current Bar object info.
            */
            public Dictionary<int, Dictionary<string, DrawInfo>> redrawInfos = new();
            /*
              For real-time market:
              - intKey is always [0]
              - stringKey is the DrawInfo.Id (object name)
              - DrawInfo is the current Bar object info.
            */
            public Dictionary<int, Dictionary<string, DrawInfo>> currentToRedraw = new();

            // It's fine to just keep the objects name as keys,
            // since hiddenInfos is populated/updated at each drawing.
            public Dictionary<string, ChartObject> hiddenInfos = new();
            /*
              For real-time market:
              - intKey is always [0]
              - stringKey is the DrawInfo.Id (object name)
              - DrawInfo is the current Bar object.
            */
            public Dictionary<int, Dictionary<string, ChartObject>> currentToHidden = new();
            public ChartStaticText staticText_DebugPerfDraw;

            public void ClearAll() {
                hiddenInfos.Clear();
                redrawInfos.Clear();
                currentToHidden.Clear();
                currentToRedraw.Clear();
            }
        }
        private readonly PerfDrawingObjs_Info PerfDrawingObjs = new();

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

        public class CustomMAObjs {
            public MAType_Data Large = MAType_Data.Exponential;
            public MAType_Data Bubbles = MAType_Data.Exponential;
            public MAType_Data Spike = MAType_Data.Simple;
            public MAType_Data SpikePctRatio = MAType_Data.Simple;
        }
        public CustomMAObjs CustomMAType = new();

        private readonly Dictionary<int, double> _dynamicBuffer = new();
        private readonly Dictionary<int, double> _maDynamic = new();

        private class DeltaBuffer {
            public Dictionary<int, double> Change = new();
            public Dictionary<int, double> BuySell_Sum = new();
            public Dictionary<int, double> Subtract = new();
            public Dictionary<int, double> Sum = new();
            public Dictionary<int, double> Spike_PctRatio = new();

            public Dictionary<int, double> MASubtract_Large = new();

            public Dictionary<int, double> MAChange_Bubbles = new();
            public Dictionary<int, double> MABuySellSum_Bubbles = new();
            public Dictionary<int, double> MASubtract_Bubbles = new();
            public Dictionary<int, double> MASum_Bubbles = new();

            public Dictionary<int, double> MABuySellSum_Spike = new();
            public Dictionary<int, double> MASum_Spike = new();

            public Dictionary<int, double> MASpike_PctRatio = new();

            public void ClearAll()
            {
                Dictionary<int, double>[] _all = new[] {
                    Change, BuySell_Sum, Subtract, Sum, Spike_PctRatio,
                    MASubtract_Large,
                    MAChange_Bubbles, MABuySellSum_Bubbles, MASubtract_Bubbles, MASum_Bubbles,
                    MABuySellSum_Spike, MASum_Spike, MASpike_PctRatio
                };

                foreach (var dict in _all)
                    dict.Clear();
            }
        }
        private readonly DeltaBuffer _deltaBuffer = new();

        private enum MASwitch {
            Large,
            Bubbles,
            Spike,
        }
        private enum DeltaSwitch {
            None,
            DeltaChange,
            DeltaBuySell_Sum,
            Subtract,
            Sum,
            Spike_PctRatio
        }

        // Params Panel
        private Border ParamBorder;

        public class IndicatorParams
        {
            public GeneralParams_Info GeneralParams { get; set; }
            public double RowHeightInPips { get; set; }
            public ProfileParams_Info ProfileParams { get; set; }
            public NodesParams_Info NodesParams { get; set; }

            public SpikeFilterParams_Info SpikeFilterParams { get; set; }
            public SpikeLevelParams_Info SpikeLevelParams { get; set; }
            public SpikeRatioParams_Info SpikeRatioParams { get; set; }

            public BubblesChartParams_Info BubblesChartParams { get; set; }
            public BubblesLevelParams_Info BubblesLevelParams { get; set; }
            public BubblesRatioParams_Info BubblesRatioParams { get; set; }

            public ResultParams_Info ResultParams { get; set; }
            public MiscParams_Info MiscParams { get; set; }
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
            Dynamic_Series = CreateDataSeries();
            DeltaChange_Series = CreateDataSeries();
            DeltaBuySell_Sum_Series = CreateDataSeries();
            SubtractDelta_Series = CreateDataSeries();
            SumDelta_Series = CreateDataSeries();
            PercentageRatio_Series = CreateDataSeries();
            PercentileRatio_Series = CreateDataSeries();

            if (!UseCustomMAs)
                CreateOrReset_cTraderIndicators();

            // First Ticks Data
            TicksOHLC = MarketData.GetBars(TimeFrame.Tick);

            // Load all at once, mostly due to:
            // Loading parameters that have it
            DailyBars = MarketData.GetBars(TimeFrame.Daily);
            WeeklyBars = MarketData.GetBars(TimeFrame.Weekly);
            MonthlyBars = MarketData.GetBars(TimeFrame.Monthly);
            MiniVPs_Bars = MarketData.GetBars(ProfileParams.MiniVPs_Timeframe);

            if (LoadTickStrategy_Input != LoadTickStrategy_Data.At_Startup_Sync)
            {
                if (LoadTickStrategy_Input == LoadTickStrategy_Data.On_ChartStart_Sync) {
                    StackPanel panel = new() {
                        Width = 200,
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    TickObjs.syncProgressBar = new ProgressBar { IsIndeterminate = true, Height = 12 };
                    panel.AddChild(TickObjs.syncProgressBar);
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
                Bars.BarOpened += (_) => BooleanUtils.isPriceBased_NewBar = true;
                // Even with any additional recalculation here,
                // when running on Backtest, any drawing that uses avoidStretching remains the same as in live market
                // works as expected in live market though                
            }
            isRenkoChart = currentTimeframe.Contains("Renko");

            // Spike Filter + Ultra Bubbles + Spike Levels
            Bars.BarOpened += (_) =>
            {
                BooleanLocks.SetAllToFalse();
                BooleanLocks.SetAllNewBar();
                BooleanUtils.isUpdateVP = true;
                if (ProfileParams.UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
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
                GeneralParams = GeneralParams,
                RowHeightInPips = heightPips,
                ProfileParams = ProfileParams,
                NodesParams = NodesParams,

                SpikeFilterParams = SpikeFilterParams,
                SpikeLevelParams = SpikeLevelParams,
                SpikeRatioParams = SpikeRatioParams,

                BubblesChartParams = BubblesChartParams,
                BubblesLevelParams = BubblesLevelParams,
                BubblesRatioParams = BubblesRatioParams,

                ResultParams = ResultParams,
                MiscParams = MiscParams,
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

        public override void Calculate(int index)
        {
            // Tick Data Collection on chart
            bool isOnChart = LoadTickStrategy_Input != LoadTickStrategy_Data.At_Startup_Sync;
            if (isOnChart && !TickObjs.isLoadingComplete)
                LoadMoreTicksOnChart();

            bool isOnChartAsync = LoadTickStrategy_Input == LoadTickStrategy_Data.On_ChartEnd_Async;
            if (isOnChartAsync && !TickObjs.isLoadingComplete)
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
            if (PanelSwitch_Input != PanelSwitch_Data.Order_Flow_Ticks && !IsLastBar) {
                CreateMonthlyVP(index);
                CreateWeeklyVP(index);
            }

            // LookBack
            Bars ODF_Bars = MiscParams.ODFInterval_Input == ODFInterval_Data.Daily ? DailyBars : WeeklyBars;

            // Get Index of ODF Interval to continue only in Lookback
            int iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (ODF_Bars.ClosePrices.Count - iVerify > GeneralParams.Lookback)
                return;

            int TF_idx = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes[TF_idx]);

            // ODF/VP => Reset filters and main VP
            if (index == indexStart ||
                (index - 1) == indexStart && isPriceBased_Chart ||
                (index - 1) == indexStart && (index - 1) != ClearIdx.MainVP
            )
                MassiveCleanUp(indexStart, index);

            // Historical data
            if (!IsLastBar) {
                // Required for [Ultra Bubbles, Spike] Levels in Historical Data
                BooleanLocks.LevelsToFalse();

                if (PanelSwitch_Input != PanelSwitch_Data.Volume_Profile) 
                {
                    CreateOrderFlow(index);
                    /*
                    if (!isPriceBased_Chart)
                        CreateOrderFlow(index);
                    else {
                        // PriceGap condition can't handle very strong gaps
                        try { CreateOrderFlow(index); } catch { };
                    }
                    */
                }

                if (PanelSwitch_Input != PanelSwitch_Data.Order_Flow_Ticks) {
                    if (ProfileParams.EnableMainVP)
                        VolumeProfile(indexStart, index);
                    
                    CreateMiniVPs(index);
                }

                BooleanUtils.isUpdateVP = true; // chart end
            }
            else
            {
                if (PanelSwitch_Input != PanelSwitch_Data.Volume_Profile) 
                {
                    // Required for Non-Time based charts (Renko, Range, Ticks)
                    if (BooleanUtils.isPriceBased_NewBar) {
                        lockNotifyInPriceBased(true);

                        CreateOrderFlow(index - 1);
                        BooleanUtils.isPriceBased_NewBar = false;

                        lockNotifyInPriceBased(false);
                        return;
                    }
                    CreateOrderFlow(index);
                }
                
                if (PanelSwitch_Input != PanelSwitch_Data.Order_Flow_Ticks) 
                {
                    // Live VP
                    if (UpdateVPStrategy_Input == UpdateVPStrategy_Data.SameThread_MayFreeze)
                    {
                        if (ProfileParams.EnableMainVP)
                            LiveVP_Update(indexStart, index);
                        else if (!ProfileParams.EnableMainVP && ProfileParams.EnableMiniProfiles)
                            LiveVP_Update(indexStart, index, true);
                    }
                    else
                        LiveVP_Concurrent(index, indexStart);
                }
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
                BooleanLocks.spikeNotify = value;
                BooleanLocks.ultraNotify = !value;
            }
        }

        private void MassiveCleanUp(int indexStart, int index) {
            // Reset VP
            // Segments are identified by TF_idx(start)
            // No need to clean up even if it's Daily Interval
            if (!IsLastBar)
                PerformanceTick.startIdx_MainVP = PerformanceTick.lastIdx_MainVP;
            VP_VolumesRank.Clear();
            VP_VolumesRank_Up.Clear();
            VP_VolumesRank_Down.Clear();
            VP_VolumesRank_Subt.Clear();
            VP_DeltaRank.Clear();
            ClearIdx.MainVP = index == indexStart ? index : (index - 1);

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
                // Any
                _dynamicBuffer.Clear();
                _maDynamic.Clear();
                // Delta(only)
                _deltaBuffer.ClearAll();
            }
            else {
                for (int i = 0; i < Bars.Count; i++)
                {
                    Dynamic_Series[i] = double.NaN;
                    DeltaChange_Series[i] = double.NaN;
                    DeltaBuySell_Sum_Series[i] = double.NaN;
                    SubtractDelta_Series[i] = double.NaN;
                    SumDelta_Series[i] = double.NaN;
                    PercentageRatio_Series[i] = double.NaN;
                    PercentileRatio_Series[i] = double.NaN;
                }
                CreateOrReset_cTraderIndicators();
            }
            

            // Reset Levels
            if (BubblesLevelParams.ResetDaily && BubblesChartParams.EnableBubblesChart)
            {
                foreach (var rect in ultraRectangles.Values) {
                    if (rect.isActive) {
                        rect.Rectangle.Time2 = Bars.OpenTimes[indexStart];
                    }
                }

                ultraRectangles.Clear();
            }
            if (SpikeLevelParams.ResetDaily && SpikeFilterParams.EnableSpikeFilter)
            {
                foreach (var rect in spikeRectangles.Values) {
                    if (rect.isActive) {
                        rect.Rectangle.Time2 = Bars.OpenTimes[indexStart];
                    }
                }

                spikeRectangles.Clear();
            }
        }
        private void CreateOrReset_cTraderIndicators() {
            // Large
            MADynamic_LargeFilter = Indicators.MovingAverage(Dynamic_Series, ResultParams.MAperiod, ResultParams.MAtype);
            MASubtract_LargeFilter = Indicators.MovingAverage(SubtractDelta_Series, ResultParams.MAperiod, ResultParams.MAtype);

            // Bubbles
            MABubbles_Delta = Indicators.MovingAverage(Dynamic_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            MABubbles_DeltaBuySell_Sum = Indicators.MovingAverage(DeltaBuySell_Sum_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            MABubbles_DeltaChange = Indicators.MovingAverage(DeltaChange_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            MABubbles_SubtractDelta = Indicators.MovingAverage(SubtractDelta_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            MABubbles_SumDelta = Indicators.MovingAverage(SumDelta_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);

            StdDevBubbles_Delta = Indicators.StandardDeviation(Dynamic_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            StdDevBubbles_DeltaChange = Indicators.StandardDeviation(DeltaChange_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            StdDevBubbles_DeltaBuySell_Sum = Indicators.StandardDeviation(DeltaBuySell_Sum_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            StdDevBubbles_SubtractDelta = Indicators.StandardDeviation(SubtractDelta_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);
            StdDevBubbles_SumDelta = Indicators.StandardDeviation(SumDelta_Series, BubblesChartParams.MAperiod, BubblesChartParams.MAtype);

            // Spike
            MASpike_Delta = Indicators.MovingAverage(Dynamic_Series, SpikeFilterParams.MAperiod, SpikeFilterParams.MAtype);
            MASpike_DeltaBuySell_Sum = Indicators.MovingAverage(DeltaBuySell_Sum_Series, SpikeFilterParams.MAperiod, SpikeFilterParams.MAtype);
            MASpike_SumDelta = Indicators.MovingAverage(SumDelta_Series, SpikeFilterParams.MAperiod, SpikeFilterParams.MAtype);

            StdDevSpike_Delta = Indicators.StandardDeviation(Dynamic_Series, SpikeFilterParams.MAperiod, SpikeFilterParams.MAtype);
            StdDevSpike_DeltaBuySell_Sum = Indicators.StandardDeviation(DeltaBuySell_Sum_Series, SpikeFilterParams.MAperiod, SpikeFilterParams.MAtype);
            StdDevSpike_SumDelta = Indicators.StandardDeviation(SumDelta_Series, SpikeFilterParams.MAperiod, SpikeFilterParams.MAtype);

            // Spike => Percentage Ratio
            MARatio_Percentage =  Indicators.MovingAverage(PercentageRatio_Series, SpikeRatioParams.MAperiod_PctSpike, MovingAverageType.Simple);
        }

        // *********** ORDER FLOW TICKS ***********
        private void LockODFTemplate() {
            // Lock Bubbles Chart template
            if (BubblesChartParams.EnableBubblesChart) {
                MiscParams.ShowHist = false;
                MiscParams.ShowNumbers = false;
                ProfileParams.EnableMainVP = false;
                ProfileParams.EnableMiniProfiles = false;
                SpikeFilterParams.EnableSpikeFilter = false;
                ResultParams.ShowResults = false;
                
            }
            // Lock Spike Chart template
            if (SpikeFilterParams.EnableSpikeChart) {
                SpikeFilterParams.EnableSpikeFilter = true;
                MiscParams.ShowHist = false;
                ResultParams.ShowResults = false;
            }
        }

        private void OrderFlow(int iStart)
        {
            // ==== Highest and Lowest ====
            double highest = Bars.HighPrices[iStart];
            double lowest = Bars.LowPrices[iStart];
            double open = Bars.OpenPrices[iStart];

            if (isRenkoChart && ShowWicks)
            {
                bool isUp = Bars.ClosePrices[iStart] > Bars.OpenPrices[iStart];
                DateTime currentOpenTime = Bars.OpenTimes[iStart];
                DateTime nextOpenTime = Bars.OpenTimes[iStart + 1];

                double[] wicks = GetWicks(currentOpenTime, nextOpenTime);

                if (IsLastBar && !BooleanUtils.isPriceBased_NewBar)
                {
                    lowest = wicks[0];
                    highest = wicks[1];
                    open = Bars.ClosePrices[iStart - 1];
                }
                else
                {
                    if (isUp)
                        lowest = wicks[0];
                    else
                        highest = wicks[1];
                }
            }

            // ==== Segments ====
            List<double> barSegments = new();

            lowest -= rowHeight;
            highest += rowHeight;

            for (int i = 0; i < Segments_VP.Count; i++)
            {
                double row = Segments_VP[i];
                if (lowest <= row)
                    barSegments.Add(row);
                if (highest < row)
                    break;
            }
            Segments_Bar = barSegments.OrderBy(x => x).ToList();

            // Lock features/design, if applicable.
            LockODFTemplate();
            
            // ==== Volume on Tick ====
            VP_Tick(iStart);

            // Do not populate series if the current bar is empty (like bars before TickObjs.firstTickTime)
            if (Segments_Bar.Count == 0 || !VolumesRank.Any())
                return;

            // Series for [Strength, Tick Spike, Bubbles Chart] filters
            PopulateSeries(iStart);
            
            // Tick Spike => strength of each row
            Dictionary<double, double> spikeProfile = GeneralParams.VolumeMode_Input == VolumeMode_Data.Delta ? CreateSpikeProfile(iStart) : new();

            // ==== Drawing ====
            DateTime xBar = Bars.OpenTimes[iStart];

            double maxLength_LeftSide = xBar.Subtract(Bars[iStart - 1].OpenTime).TotalMilliseconds;
            double proportion_LeftSide = maxLength_LeftSide / 3;

            double maxLength_RightSide = (!IsLastBar || BooleanUtils.isPriceBased_NewBar) ?
                                         Bars[iStart + 1].OpenTime.Subtract(xBar).TotalMilliseconds :
                                         maxLength_LeftSide;
            double proportion_RightSide = maxLength_RightSide / 3;

            bool gapWeekday = xBar.DayOfWeek == DayOfWeek.Sunday && Bars.OpenTimes[iStart - 1].DayOfWeek == DayOfWeek.Friday;
            bool priceGap = xBar == Bars[iStart - 1].OpenTime || Bars[iStart - 2].OpenTime == Bars[iStart - 1].OpenTime;
            bool isBullish = Bars.ClosePrices[iStart] > Bars.OpenPrices[iStart];
            bool avoidStretching = IsLastBar && !BooleanUtils.isPriceBased_NewBar; // For real-time => Avoid stretching the histograms away ad infinitum

            // (micro)Optimization for all modes
            int maxValue = GeneralParams.VolumeMode_Input switch {
                VolumeMode_Data.Normal => VolumesRank.Any() ? VolumesRank.Values.Max() : 0,
                VolumeMode_Data.Delta => DeltaRank.Any() ? DeltaRank.Values.Max() : 0,
                _ => 0
            };

            int buyMax = 0;
            int sellMax = 0;
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Buy_Sell) {
                buyMax = VolumesRank_Up.Any() ? VolumesRank_Up.Values.Max() : 0;
                sellMax = VolumesRank_Down.Any() ? VolumesRank_Down.Values.Max() : 0;
            }

            IEnumerable<int> negativeList = new List<int>();
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Delta)
                negativeList = DeltaRank.Values.Where(n => n < 0);

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

                    bool dividedCondition = GeneralParams.VolumeView_Input == VolumeView_Data.Profile && profileInMiddle; // Profile View - Half Proportion

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

                    bool dividedCondition = GeneralParams.VolumeView_Input == VolumeView_Data.Divided;
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
                    if (GeneralParams.ColoringOnlyLarguest)
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

                void DrawRectangle_Delta(int currentDelta, int positiveDeltaMax, IEnumerable<int> negativeDeltaList)
                {
                    int negativeDeltaMax = negativeDeltaList.Any() ? Math.Abs(negativeDeltaList.Min()) : 0;

                    // Divided View
                    double dynLengthDelta_Divided = 0;
                    if (currentDelta > 0)
                    {
                        double proportionDelta_Positive = currentDelta * proportion_RightSide;
                        dynLengthDelta_Divided = proportionDelta_Positive / positiveDeltaMax;
                    }
                    else
                    {
                        double proportionDelta_Negative = Math.Abs(currentDelta) * proportion_LeftSide;
                        dynLengthDelta_Divided = proportionDelta_Negative / negativeDeltaMax;
                        dynLengthDelta_Divided = -dynLengthDelta_Divided;
                    }

                    // Profile View - Complete Proportion
                    int deltaMax = positiveDeltaMax > Math.Abs(negativeDeltaMax) ? positiveDeltaMax : Math.Abs(negativeDeltaMax);

                    double proportion_ToMiddle = Math.Abs(currentDelta) * proportion_LeftSide;
                    double dynLength_ToMiddle = proportion_ToMiddle / deltaMax;

                    double proportion_ToRight = Math.Abs(currentDelta) * proportion_RightSide;
                    double dynLength_ToRight = proportion_ToRight / deltaMax;
                    // ========

                    bool dividedCondition = GeneralParams.VolumeView_Input == VolumeView_Data.Divided;
                    DateTime x1 = dividedCondition || gapWeekday ? xBar : xBar.AddMilliseconds(-proportion_LeftSide);

                    DateTime x2;
                    if (dividedCondition || gapWeekday)
                        x2 = x1.AddMilliseconds(dynLengthDelta_Divided);
                    else
                        x2 = x1.AddMilliseconds(dynLength_ToMiddle).AddMilliseconds(dynLength_ToRight);

                    if (isPriceBased_Chart && GeneralParams.VolumeView_Input == VolumeView_Data.Profile)
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
                    Color sellDividedColor = currentDelta != negativeDeltaMax ? SellColor : SellLargeColor;
                    if (GeneralParams.ColoringOnlyLarguest)
                    {
                        buyDividedColor = positiveDeltaMax > Math.Abs(negativeDeltaMax) && currentDelta == positiveDeltaMax ?
                            BuyLargeColor : BuyColor;
                        sellDividedColor = Math.Abs(negativeDeltaMax) > positiveDeltaMax && currentDelta == negativeDeltaMax ?
                            SellLargeColor : SellColor;
                    }

                    Color buyColorWithFilter = GeneralParams.VolumeView_Input == VolumeView_Data.Divided ? buyDividedColor : BuyColor;
                    Color sellColorWithFilter = GeneralParams.VolumeView_Input == VolumeView_Data.Divided ? sellDividedColor : SellColor;

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

                switch (GeneralParams.VolumeMode_Input)
                {
                    case VolumeMode_Data.Normal:
                    {
                        int normalValue = VolumesRank[priceKey];

                        if (MiscParams.ShowHist)
                            DrawRectangle_Normal(normalValue, maxValue);

                        if (MiscParams.ShowNumbers)
                        {
                            string valueFmtd = FormatNumbers ? FormatBigNumber(normalValue) : $"{normalValue}";

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
                        break;
                    }
                    case VolumeMode_Data.Buy_Sell:
                    {
                        int buyValue = VolumesRank_Up[priceKey];
                        int sellValue = VolumesRank_Down[priceKey];

                        if (MiscParams.ShowHist)
                            DrawRectangle_BuySell(buyValue, buyMax, sellValue, sellMax);

                        if (MiscParams.ShowNumbers)
                        {
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
                        break;
                    }
                    default:
                    {
                        int deltaValue = DeltaRank[priceKey];
                        if (MiscParams.ShowHist)
                            DrawRectangle_Delta(deltaValue, maxValue, negativeList);

                        if (MiscParams.ShowNumbers)
                        {
                            string deltaValueFmtd = deltaValue > 0 ? FormatBigNumber(deltaValue) : $"-{FormatBigNumber(Math.Abs(deltaValue))}";
                            string deltaFmtd = FormatNumbers ? deltaValueFmtd : $"{deltaValue}";

                            HorizontalAlignment horizontalAligh;
                            if (GeneralParams.VolumeView_Input == VolumeView_Data.Divided)
                                horizontalAligh = deltaValue > 0 ? HorizontalAlignment.Right : deltaValue < 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center;
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

                        // Tick Delta = Spike Filter
                        if (SpikeFilterParams.EnableSpikeFilter)
                        {
                            double rowStrength = spikeProfile[priceKey];

                            // Ratios
                            double lowestValue = SpikeRatioParams.Lowest_PctValue;
                            double lowValue = SpikeRatioParams.Low_PctValue;
                            double averageValue = SpikeRatioParams.Average_PctValue;
                            double highValue = SpikeRatioParams.High_PctValue;
                            double ultraValue = SpikeRatioParams.Ultra_PctValue;

                            if (SpikeRatioParams.SpikeRatio_Input == SpikeRatio_Data.Fixed) 
                            {
                                lowestValue = SpikeRatioParams.Lowest_FixedValue;
                                lowValue = SpikeRatioParams.Low_FixedValue;
                                averageValue = SpikeRatioParams.Average_FixedValue;
                                highValue = SpikeRatioParams.High_FixedValue;
                                ultraValue = SpikeRatioParams.Ultra_FixedValue;
                            }

                            Color spikeHeatColor = rowStrength < lowestValue ? SpikeLowest_Color :
                                                   rowStrength < lowValue ? SpikeLow_Color :
                                                   rowStrength < averageValue ? SpikeAverage_Color :
                                                   rowStrength < highValue ? SpikeHigh_Color :
                                                   rowStrength >= ultraValue ? SpikeUltra_Color : SpikeUltra_Color;

                            Color spikeBySideColor = deltaValue > 0 ? BuyColor : SellColor;
                            
                            // For real-time - "repaint/update" the spike price level.
                            if (IsLastBar) {
                                Chart.RemoveObject($"{iStart}_{i}_Spike");
                                if (DrawingStrategy_Input == DrawingStrategy_Data.Redraw_Fastest)
                                    PerfDrawingObjs.currentToRedraw.Clear();
                                else 
                                    PerfDrawingObjs.currentToHidden.Clear();
                            }

                            bool isSpikeAverage = rowStrength > lowValue;
                            if (isSpikeAverage || SpikeFilterParams.EnableSpikeChart)
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

                                if (SpikeFilterParams.SpikeView_Input == SpikeView_Data.Bubbles || SpikeFilterParams.EnableSpikeChart)
                                {
                                    Color spikeHeat_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeHeatColor.R, spikeHeatColor.G, spikeHeatColor.B);
                                    Color spikeBySide_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeBySideColor.R, spikeBySideColor.G, spikeBySideColor.B);
                                    Color spikeChartColor = SpikeFilterParams.SpikeChartColoring_Input == SpikeChartColoring_Data.Heatmap ?
                                                            spikeHeat_WithOpacity : spikeBySide_WithOpacity;


                                    if (SpikeFilterParams.SpikeChartColoring_Input == SpikeChartColoring_Data.PlusMinus_Highlight_Heatmap)
                                        spikeChartColor = isSpikeAverage ? spikeHeat_WithOpacity : spikeChartColor;

                                    Color bubbleColor = !SpikeFilterParams.EnableSpikeChart ? spikeHeatColor : spikeChartColor;
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
                                else
                                {
                                    DateTime positionX = GeneralParams.VolumeView_Input == VolumeView_Data.Divided ? xBar : X2;
                                    double positionY = (Y1 + Y2) / 2;
                                    ChartIcon icon = Chart.DrawIcon($"{iStart}_{i}_Spike", SpikeFilterParams.IconView_Input, positionX, positionY, spikeHeatColor);
                                    DrawOrCache(new DrawInfo
                                    {
                                        BarIndex = iStart,
                                        Type = DrawType.Icon,
                                        Id = $"{iStart}_{i}_Spike",
                                        IconType = SpikeFilterParams.IconView_Input,
                                        X1 = positionX,
                                        Y1 = positionY,
                                        Color = spikeHeatColor
                                    });
                                }

                                bool notifyLater = SpikeFilterParams.SpikeFilter_Input == SpikeFilter_Data.L1Norm ||
                                                   SpikeFilterParams.SpikeFilter_Input == SpikeFilter_Data.SoftMax_Power;

                                bool notifyBool = !notifyLater ? isSpikeAverage : (BooleanLocks.lastIsAvg && BooleanLocks.spikeNotify_NewBar);
                                
                                if (SpikeFilterParams.EnableSpikeNotification && IsLastBar && !BooleanLocks.spikeNotify && notifyBool)
                                {
                                    string symbolName = $"{Symbol.Name} ({Chart.TimeFrame.ShortName})";
                                    string popupText = $"{symbolName} => Tick Spike at {Server.Time}";

                                    switch (SpikeFilterParams.NotificationType_Input) {
                                        case NotificationType_Data.Sound:
                                            Notifications.PlaySound(SpikeFilterParams.Spike_SoundType);
                                            break;
                                        case NotificationType_Data.Popup:
                                            Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                            break;
                                        default:
                                            Notifications.PlaySound(SpikeFilterParams.Spike_SoundType);
                                            Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                            break;
                                    }
                                    BooleanLocks.spikeNotify = true;
                                    BooleanLocks.spikeNotify_NewBar = false;
                                }
                            }

                            // At the final loop when the bar is closed, if "isSpikeAverage", notify in the next bar.
                            // When Backtesting in Price-Based Charts, this condition doesn't seem to be triggered,
                            // Works fine in real-time market though.
                            if (isSpikeAverage) {
                                BooleanLocks.spikeNotify_NewBar = false;
                                BooleanLocks.lastIsAvg = true;
                            }
                            else
                                BooleanLocks.lastIsAvg = false;

                            if (SpikeRatioParams.ShowStrengthValue)
                            {
                                string suffix = SpikeRatioParams.SpikeRatio_Input == SpikeRatio_Data.Percentage ? "%" : "";
                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_{i}_TickStrengthValue",
                                    Text = $"   <= {rowStrength}{suffix}",
                                    X1 = xBar,
                                    Y1 = priceKey,
                                    horizontalAlignment = HorizontalAlignment.Right,
                                    verticalAlignment = VerticalAlignment.Bottom,
                                    FontSize = FontSizeNumbers,
                                    Color = RtnbFixedColor
                                });
                            }

                            // === Spike Levels ====
                            if (SpikeLevelParams.ShowSpikeLevels)
                            {
                                string spikeDictKey = $"{iStart}_{i}_SpikeLevel";

                                // For real-time - "repaint/update" the spike price level.
                                if (IsLastBar)
                                {
                                    try { Chart.RemoveObject($"{iStart}_{i}_SpikeLevelRectangle"); } catch { };
                                    if (SpikeLevels_ShowValue) {
                                        try { Chart.RemoveObject($"{iStart}_{i}_SpikeLevelText"); } catch { };
                                    }
                                    spikeRectangles.Remove(spikeDictKey);
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
                                if (!BooleanLocks.spikeLevels || IsLastBar)
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
                                        if (TouchesRect_Spike(open, high, low, close, top, bottom))
                                        {
                                            rect.Touches++;
                                            // Current forming bar already touched that rectangle.
                                            // So, lock it until a new LastBarIndex appear.
                                            rect.LastBarIndex = iStart;

                                            if (SpikeLevels_ShowValue)
                                                UpdateLabel_Spike(rect, Bars.OpenTimes[iStart]);

                                            if (rect.Touches >= SpikeLevelParams.MaxCount)
                                            {
                                                rect.isActive = false;

                                                // Stop extension → fix rectangle to current bar
                                                rect.Rectangle.Time2 = Bars.OpenTimes[iStart];
                                                rect.Rectangle.Color = Color.FromArgb(50, rect.Rectangle.Color);

                                                // Finalize label
                                                if (SpikeLevels_ShowValue)
                                                {
                                                    rect.Text.Text = $"{rect.Touches}";
                                                    rect.Text.Color = RtnbFixedColor;
                                                }
                                            }
                                        }
                                    }

                                    BooleanLocks.spikeLevels = true;
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
                                if (isSpikeAverage)
                                {
                                    Color spikeHeat_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeHeatColor.R, spikeHeatColor.G, spikeHeatColor.B);
                                    Color SpikeBySide_WithOpacity = Color.FromArgb((int)(2.55 * SpikeChart_Opacity), spikeBySideColor.R, spikeBySideColor.G, spikeBySideColor.B);
                                    Color spikeLevelColor = SpikeLevelParams.SpikeLevelsColoring_Input == SpikeLevelsColoring_Data.Heatmap ?
                                                            spikeHeat_WithOpacity : SpikeBySide_WithOpacity;

                                    double Y1 = priceKey;
                                    double Y2 = priceKey - rowHeight;
                                    CreateRect_Spike(Y1, Y2, iStart, i, spikeDictKey, spikeLevelColor);
                                }
                            }
                        }
                        break;
                    }
                }

                loopPrevSegment = Segments_Bar[i];
            }
            
            // Drawings that don't require each segment-price as y-axis
            // It can/should be outside SegmentsLoop for better performance.
            
            double rowHeightHalf = (rowHeight + rowHeight) / 2;
            double highestHalf = Bars.HighPrices[iStart] + rowHeightHalf;
            double lowestHalf = Bars.LowPrices[iStart] - rowHeightHalf;
            if (isRenkoChart && ShowWicks)
            {
                lowest += rowHeight;
                highest -= rowHeight;
                highestHalf = highest + rowHeightHalf;
                lowestHalf = lowest - rowHeightHalf;
            }

            // Results
            switch (GeneralParams.VolumeMode_Input)
            {
                case VolumeMode_Data.Normal:
                {
                    if (!ResultParams.ShowResults)
                        break;

                    double sumValue = Dynamic_Series[iStart];
                    string valueFmtd = FormatResults ? FormatBigNumber(sumValue) : $"{sumValue}";
                    Color resultColor = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : VolumeColor;

                    if (ResultParams.EnableLargeFilter)
                    {
                        // ====== Strength Filter ======
                        double filterValue = 0;
                        if (UseCustomMAs)
                            filterValue = CustomMAs(sumValue, iStart, ResultParams.MAperiod, CustomMAType.Large);
                        else
                            filterValue = MADynamic_LargeFilter.Result[iStart];

                        double volumeStrength = sumValue / filterValue;
                        Color filterColor = volumeStrength >= ResultParams.LargeRatio ? ColorLargeResult : resultColor;

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

                    break;
                }
                case VolumeMode_Data.Buy_Sell:
                {
                    if (!ResultParams.ShowResults)
                        break;
                        
                    int volBuy = VolumesRank_Up.Values.Sum();
                    int volSell = VolumesRank_Down.Values.Sum();

                    if (ResultParams.ShowSideTotal)
                    {
                        Color colorLeft = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : SellColor;
                        Color colorRight = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : BuyColor;

                        int percentBuy = (volBuy * 100) / (volBuy + volSell);
                        int percentSell = (volSell * 100) / (volBuy + volSell);

                        string volBuyFmtd = FormatResults ? FormatBigNumber(volBuy) : $"{volBuy}";
                        string volSellFmtd = FormatResults ? FormatBigNumber(volSell) : $"{volSell}";

                        string strBuy = ResultParams.ResultsView_Input switch {
                            ResultsView_Data.Percentage => $"\n{percentBuy}%",
                            ResultsView_Data.Value => $"\n{volBuyFmtd}",
                            _ => $"\n{percentBuy}%\n({volBuyFmtd})"
                        };
                        string strSell = ResultParams.ResultsView_Input switch {
                            ResultsView_Data.Percentage => $"\n{percentSell}%",
                            ResultsView_Data.Value => $"\n{volSellFmtd}",
                            _ => $"\n{percentSell}%\n({volSellFmtd})"
                        };

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

                    double sumValue = volBuy + volSell;
                    double subtValue = volBuy - volSell;

                    string sumFmtd = FormatResults ? FormatBigNumber(sumValue) : $"{sumValue}";

                    string subtValueFmtd = subtValue > 0 ? FormatBigNumber(subtValue) : $"-{FormatBigNumber(Math.Abs(subtValue))}";
                    string subtFmtd = FormatResults ? subtValueFmtd : $"{subtValue}";

                    string strFormated = ResultParams.OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumFmtd : subtFmtd;

                    Color compareColor = volBuy > volSell ? BuyColor : volBuy < volSell ? SellColor : RtnbFixedColor;
                    Color colorCenter = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareColor;

                    ResultsView_Data selectedView = ResultParams.ResultsView_Input;
                    bool showSide_notBoth = ResultParams.ShowSideTotal && (selectedView == ResultsView_Data.Percentage || selectedView == ResultsView_Data.Value);
                    bool showSide_Both = ResultParams.ShowSideTotal && selectedView == ResultsView_Data.Both;
                    string dynSpaceSum = showSide_notBoth ? $"\n\n\n" :
                                         showSide_Both ? $"\n\n\n\n" : "\n";

                    if (ResultParams.EnableLargeFilter)
                    {
                        double seriesValue = Dynamic_Series[iStart];
                        // ====== Strength Filter ======
                        double filterValue = 0;
                        if (UseCustomMAs)
                            filterValue = CustomMAs(seriesValue, iStart, ResultParams.MAperiod, CustomMAType.Large);
                        else
                            filterValue = MADynamic_LargeFilter.Result[iStart];

                        double bsStrength = seriesValue / filterValue;
                        Color filterColor = bsStrength >= ResultParams.LargeRatio ? ColorLargeResult : colorCenter;

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

                    break;
                }
                default:
                {
                    int deltaTotal = Delta_Results[iStart];
                    int prevDeltaTotal = Delta_Results[iStart - 1];

                    int deltaChange = DeltaChange_Results[iStart];
                    int prevDeltaChange = DeltaChange_Results[iStart - 1];

                    int deltaBuySell_Sum = DeltaBuySell_Sum_Results[iStart];
                    int prevDelta_BuySell_Sum = DeltaBuySell_Sum_Results[iStart - 1];

                    int minDelta = MinMaxDelta[0];
                    int maxDelta = MinMaxDelta[1];

                    int subtDelta = 0;
                    int prevSubtDelta = 0;
                    int sumDelta = 0;
                    int prevSumDelta = 0;
                    if (ResultParams.ShowMinMaxDelta)
                    {
                        subtDelta = SubtractDelta_Results[iStart];
                        prevSubtDelta = SubtractDelta_Results[iStart - 1];
                        sumDelta = SumDelta_Results[iStart];
                        prevSumDelta = SumDelta_Results[iStart - 1];
                    }

                    if (ResultParams.ShowResults)
                    {
                        if (ResultParams.ShowSideTotal)
                        {
                            int deltaBuy = DeltaRank.Values.Where(n => n > 0).Sum();
                            int deltaSell = DeltaRank.Values.Where(n => n < 0).Sum();

                            int percentBuy = 0;
                            int percentSell = 0;
                            try { percentBuy = (deltaBuy * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { }
                            try { percentSell = (deltaSell * 100) / (deltaBuy + Math.Abs(deltaSell)); } catch { }

                            string deltaBuyFmtd = FormatResults ? FormatBigNumber(deltaBuy) : $"{deltaBuy}";
                            string deltaSellFmtd = FormatResults ? FormatBigNumber(deltaSell) : $"{deltaSell}";
                            
                            string strBuy = ResultParams.ResultsView_Input switch {
                                ResultsView_Data.Percentage => $"\n{percentBuy}%",
                                ResultsView_Data.Value => $"\n{deltaBuyFmtd}",
                                _ => $"\n{percentBuy}%\n({deltaBuyFmtd})"
                            };
                            string strSell = ResultParams.ResultsView_Input switch {
                                ResultsView_Data.Percentage => $"\n{percentSell}%",
                                ResultsView_Data.Value => $"\n{deltaSellFmtd}",
                                _ => $"\n{percentSell}%\n({deltaSellFmtd})"
                            };

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

                        string deltaValueFmtd = deltaTotal > 0 ? FormatBigNumber(deltaTotal) : $"-{FormatBigNumber(Math.Abs(deltaTotal))}";
                        string deltaFmtd = FormatResults ? deltaValueFmtd : $"{deltaTotal}";

                        ResultsView_Data selectedView = ResultParams.ResultsView_Input;
                        bool showSide_notBoth = ResultParams.ShowSideTotal && (selectedView == ResultsView_Data.Percentage || selectedView == ResultsView_Data.Value);
                        bool showSide_Both = ResultParams.ShowSideTotal && selectedView == ResultsView_Data.Both;
                        string dynSpaceSum = showSide_notBoth ? $"\n\n\n" :
                                             showSide_Both ? $"\n\n\n\n" : "\n";

                        Color compareSum = deltaTotal > 0 ? BuyColor : deltaTotal < 0 ? SellColor : RtnbFixedColor;
                        Color colorCenter = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareSum;

                        if (ResultParams.ShowMinMaxDelta)
                        {
                            string buysellsumValueFmtd = FormatBigNumber(deltaBuySell_Sum);

                            string minValueFmtd = minDelta > 0 ? FormatBigNumber(minDelta) : $"-{FormatBigNumber(Math.Abs(minDelta))}";
                            string maxValueFmtd = maxDelta > 0 ? FormatBigNumber(maxDelta) : $"-{FormatBigNumber(Math.Abs(maxDelta))}";
                            string subtValueFmtd = subtDelta > 0 ? FormatBigNumber(subtDelta) : $"-{FormatBigNumber(Math.Abs(subtDelta))}";
                            string sumValueFmtd = FormatBigNumber(sumDelta);
                            
                            string buysellsumFmtd = FormatResults ? buysellsumValueFmtd : $"{deltaBuySell_Sum}";
                            
                            string minDeltaFmtd = FormatResults ? minValueFmtd : $"{minDelta}";
                            string maxDeltaFmtd = FormatResults ? maxValueFmtd : $"{maxDelta}";
                            string subtDeltaFmtd = FormatResults ? subtValueFmtd : $"{subtDelta}";
                            string sumDeltaFmtd = FormatResults ? sumValueFmtd : $"{sumDelta}";

                            Color subtractColor = colorCenter;
                            if (ResultParams.EnableLargeFilter)
                            {
                                double absSubtValue = SubtractDelta_Series[iStart];
                                // ====== Strength Filter ======
                                double filterValue = 0;
                                if (UseCustomMAs)
                                    filterValue = CustomMAs(
                                        absSubtValue,
                                        iStart, ResultParams.MAperiod,
                                        CustomMAType.Large, DeltaSwitch.Subtract
                                    );
                                else
                                    filterValue = MASubtract_LargeFilter.Result[iStart];

                                double subtractLargeStrength = absSubtValue / filterValue;
                                Color filterColor = subtractLargeStrength >= ResultParams.LargeRatio ? ColorLargeResult : colorCenter;
                                subtractColor = filterColor;
                            }

                            HorizontalAlignment hAligh = HorizontalAlignment.Center;
                            int fontSize = FontSizeResults - 1;
                            if (!ResultParams.ShowOnlySubtDelta)
                            {
                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_Delta_BuySellSum_Result",
                                    Text = $"\n\n{dynSpaceSum}buy_sell:{buysellsumFmtd}",
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
                                    Id = $"{iStart}_MinDeltaResult",
                                    Text = $"\n\n\n\n{dynSpaceSum}min:{minDeltaFmtd}",
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
                                    Text = $"\n\n\n\n\n\n{dynSpaceSum}max:{maxDeltaFmtd}",
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
                                    Text = $"\n\n\n\n\n\n\n\n{dynSpaceSum}subt:{subtDeltaFmtd}",
                                    X1 = xBar,
                                    Y1 = lowestHalf,
                                    horizontalAlignment = hAligh,
                                    FontSize = fontSize,
                                    Color = subtractColor
                                });

                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_SumDeltaResult",
                                    Text = $"\n\n\n\n\n\n\n\n\n\n{dynSpaceSum}sum:{sumDeltaFmtd}",
                                    X1 = xBar,
                                    Y1 = lowestHalf,
                                    horizontalAlignment = hAligh,
                                    FontSize = fontSize,
                                    Color = colorCenter
                                });
                            }
                            else
                            {
                                DrawOrCache(new DrawInfo
                                {
                                    BarIndex = iStart,
                                    Type = DrawType.Text,
                                    Id = $"{iStart}_SubtDeltaResult",
                                    Text = $"\n\n{dynSpaceSum}subt:{subtDeltaFmtd}",
                                    X1 = xBar,
                                    Y1 = lowestHalf,
                                    horizontalAlignment = hAligh,
                                    FontSize = fontSize,
                                    Color = subtractColor
                                });
                            }
                        }

                        string changeValueFmtd = deltaChange > 0 ? FormatBigNumber(deltaChange) : $"-{FormatBigNumber(Math.Abs(deltaChange))}";
                        string changeFmtd = FormatResults ? changeValueFmtd : $"{deltaChange}";

                        Color compareChange = deltaChange > prevDeltaChange ? BuyColor : deltaChange < prevDeltaChange ? SellColor : RtnbFixedColor;
                        Color colorChange = ResultsColoring_Input == ResultsColoring_Data.Fixed ? RtnbFixedColor : compareChange;

                        if (ResultParams.EnableLargeFilter)
                        {
                            double seriesValue = Dynamic_Series[iStart];
                            // ====== Strength Filter ======
                            double filterValue = 0;
                            if (UseCustomMAs)
                                filterValue = CustomMAs(seriesValue, iStart, ResultParams.MAperiod, CustomMAType.Large);
                            else
                                filterValue = MADynamic_LargeFilter.Result[iStart];

                            double deltaLargeStrength = seriesValue / filterValue;
                            Color filterColor = deltaLargeStrength >= ResultParams.LargeRatio ? ColorLargeResult : colorCenter;

                            colorCenter = filterColor;
                            if (LargeFilter_ColoringBars && filterColor == ColorLargeResult)
                                Chart.SetBarFillColor(iStart, ColorLargeResult);
                            else
                                Chart.SetBarFillColor(iStart, isBullish ? Chart.ColorSettings.BullFillColor : Chart.ColorSettings.BearFillColor);

                            if (LargeFilter_ColoringCD)
                                colorChange = filterColor == ColorLargeResult ? filterColor : colorChange;
                        }

                        DrawOrCache(new DrawInfo
                        {
                            BarIndex = iStart,
                            Type = DrawType.Text,
                            Id = $"{iStart}_DeltaTotal",
                            Text = $"{dynSpaceSum}{deltaFmtd}",
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
                            Id = $"{iStart}_DeltaChange",
                            Text = $"{changeFmtd}",
                            X1 = xBar,
                            Y1 = highestHalf,
                            horizontalAlignment = HorizontalAlignment.Center,
                            verticalAlignment = VerticalAlignment.Top,
                            FontSize = FontSizeResults,
                            Color = colorChange
                        });
                    }

                    // ====== Delta Bubbles Chart ======
                    if (BubblesChartParams.EnableBubblesChart)
                    {
                        IndicatorDataSeries sourceSeries = BubblesChartParams.UseChangeSeries ? DeltaChange_Series :
                        BubblesChartParams.BubblesSource_Input switch {
                            BubblesSource_Data.Delta_BuySell_Sum => DeltaBuySell_Sum_Series,
                            BubblesSource_Data.Subtract_Delta => SubtractDelta_Series,
                            BubblesSource_Data.Sum_Delta => SumDelta_Series,
                            _ => Dynamic_Series
                        };

                        double sourceValue = sourceSeries[iStart];

                        DeltaSwitch deltaSwitch = BubblesChartParams.UseChangeSeries ? DeltaSwitch.DeltaChange : 
                        BubblesChartParams.BubblesSource_Input switch {
                            BubblesSource_Data.Delta_BuySell_Sum => DeltaSwitch.DeltaBuySell_Sum,
                            BubblesSource_Data.Subtract_Delta => DeltaSwitch.Subtract,
                            BubblesSource_Data.Sum_Delta => DeltaSwitch.Sum,
                            _ => DeltaSwitch.None
                        };

                        double[] window = new double[BubblesChartParams.MAperiod];
                        if (BubblesChartParams.BubblesFilter_Input == BubblesFilter_Data.SoftMax_Power ||
                            BubblesChartParams.BubblesFilter_Input == BubblesFilter_Data.L2Norm ||
                            BubblesChartParams.BubblesFilter_Input == BubblesFilter_Data.MinMax)
                        {
                            for (int k = 0; k < BubblesChartParams.MAperiod; k++)
                                window[k] = sourceSeries[iStart - BubblesChartParams.MAperiod + 1 + k];
                        }

                        double deltaStrength = 0.0;
                        double filterValue = 1.0;
                        switch (BubblesChartParams.BubblesFilter_Input)
                        {
                            case BubblesFilter_Data.MA:
                                if (UseCustomMAs)
                                    filterValue = CustomMAs(sourceValue, iStart,
                                        BubblesChartParams.MAperiod, CustomMAType.Bubbles,
                                        deltaSwitch, MASwitch.Bubbles, false
                                    );
                                else
                                    filterValue = BubblesChartParams.UseChangeSeries ? MABubbles_DeltaChange.Result[iStart] :
                                    BubblesChartParams.BubblesSource_Input switch {
                                        BubblesSource_Data.Delta_BuySell_Sum => MABubbles_DeltaBuySell_Sum.Result[iStart],
                                        BubblesSource_Data.Subtract_Delta => MABubbles_SubtractDelta.Result[iStart],
                                        BubblesSource_Data.Sum_Delta => MABubbles_SumDelta.Result[iStart],
                                        _ => MABubbles_Delta.Result[iStart]
                                    };
                                deltaStrength = sourceValue / filterValue;
                                break;
                            case BubblesFilter_Data.Standard_Deviation:
                                if (UseCustomMAs)
                                    filterValue = CustomMAs(sourceValue, iStart,
                                        BubblesChartParams.MAperiod, CustomMAType.Bubbles,
                                        deltaSwitch, MASwitch.Bubbles, true
                                    );
                                else
                                    filterValue = BubblesChartParams.UseChangeSeries ? StdDevBubbles_DeltaChange.Result[iStart] :
                                    BubblesChartParams.BubblesSource_Input switch {
                                        BubblesSource_Data.Delta_BuySell_Sum => StdDevBubbles_DeltaBuySell_Sum.Result[iStart],
                                        BubblesSource_Data.Subtract_Delta => StdDevBubbles_SubtractDelta.Result[iStart],
                                        BubblesSource_Data.Sum_Delta => StdDevBubbles_SumDelta.Result[iStart],
                                        _ => StdDevBubbles_Delta.Result[iStart]
                                    };
                                deltaStrength = sourceValue / filterValue;
                                break;
                            case BubblesFilter_Data.Both:
                                double ma;
                                if (UseCustomMAs)
                                    ma = CustomMAs(sourceValue, iStart,
                                        BubblesChartParams.MAperiod, CustomMAType.Bubbles,
                                        deltaSwitch, MASwitch.Bubbles, false
                                    );
                                else
                                    ma = BubblesChartParams.UseChangeSeries ? MABubbles_DeltaChange.Result[iStart] :
                                    BubblesChartParams.BubblesSource_Input switch {
                                        BubblesSource_Data.Delta_BuySell_Sum => MABubbles_DeltaBuySell_Sum.Result[iStart],
                                        BubblesSource_Data.Subtract_Delta => MABubbles_SubtractDelta.Result[iStart],
                                        BubblesSource_Data.Sum_Delta => MABubbles_SumDelta.Result[iStart],
                                        _ => MABubbles_Delta.Result[iStart]
                                    };

                                double stddev;
                                if (UseCustomMAs)
                                    stddev = CustomMAs(sourceValue, iStart,
                                        BubblesChartParams.MAperiod, CustomMAType.Bubbles,
                                        deltaSwitch, MASwitch.Bubbles, true
                                    );
                                else
                                    stddev = BubblesChartParams.UseChangeSeries ? StdDevBubbles_DeltaChange.Result[iStart] :
                                    BubblesChartParams.BubblesSource_Input switch {
                                        BubblesSource_Data.Delta_BuySell_Sum => StdDevBubbles_DeltaBuySell_Sum.Result[iStart],
                                        BubblesSource_Data.Subtract_Delta => StdDevBubbles_SubtractDelta.Result[iStart],
                                        BubblesSource_Data.Sum_Delta => StdDevBubbles_SumDelta.Result[iStart],
                                        _ => StdDevBubbles_Delta.Result[iStart]
                                    };

                                deltaStrength = (sourceValue - ma) / stddev;
                                break;
                            case BubblesFilter_Data.SoftMax_Power:
                                deltaStrength = Filters.PowerSoftmax_Strength(window);
                                break;
                            case BubblesFilter_Data.L2Norm:
                                deltaStrength = Filters.L2Norm_Strength(window);
                                break;
                            case BubblesFilter_Data.MinMax:
                                deltaStrength = Filters.MinMax_Strength(window);
                                break;
                        }

                        deltaStrength = Math.Round(deltaStrength, 2);

                        if (BubblesRatioParams.BubblesRatio_Input == BubblesRatio_Data.Percentile)
                        {
                            PercentileRatio_Series[iStart] = deltaStrength;

                            double[] windowRatio = new double[BubblesRatioParams.PctilePeriod];
                            for (int i = 0; i < BubblesRatioParams.PctilePeriod; i++) {
                                windowRatio[i] = PercentileRatio_Series[iStart - BubblesRatioParams.PctilePeriod + 1 + i];
                            }

                            deltaStrength = Filters.RollingPercentile(windowRatio);
                            deltaStrength = Math.Round(deltaStrength, 1);
                        }

                        // Ratios
                        double lowestValue = BubblesRatioParams.Lowest_PctileValue;
                        double lowValue = BubblesRatioParams.Low_PctileValue;
                        double averageValue = BubblesRatioParams.Average_PctileValue;
                        double highValue = BubblesRatioParams.High_PctileValue;
                        double ultraValue = BubblesRatioParams.Ultra_PctileValue;

                        if (BubblesRatioParams.BubblesRatio_Input == BubblesRatio_Data.Fixed) 
                        {
                            lowestValue = BubblesRatioParams.Lowest_FixedValue;
                            lowValue = BubblesRatioParams.Low_FixedValue;
                            averageValue = BubblesRatioParams.Average_FixedValue;
                            highValue = BubblesRatioParams.High_FixedValue;
                            ultraValue = BubblesRatioParams.Ultra_FixedValue;
                        }

                        // Filter + Size for Bubbles
                        double filterSize = deltaStrength < lowestValue ? 2 :   // 1 = too small
                                            deltaStrength < lowValue ? 2.5 :
                                            deltaStrength < averageValue ? 3 :
                                            deltaStrength < highValue ? 4 :
                                            deltaStrength >= ultraValue ? 5 : 5;

                        // Coloring
                        Color heatColor = filterSize == 2 ? HeatmapLowest_Color :
                                          filterSize == 2.5 ? HeatmapLow_Color :
                                          filterSize == 3 ? HeatmapAverage_Color :
                                          filterSize == 4 ? HeatmapHigh_Color : HeatmapUltra_Color;

                        bool sourceFading = BubblesChartParams.UseChangeSeries ? deltaChange > prevDeltaChange :
                        BubblesChartParams.BubblesSource_Input switch {
                            BubblesSource_Data.Delta_BuySell_Sum => deltaBuySell_Sum > prevDelta_BuySell_Sum,
                            BubblesSource_Data.Subtract_Delta => subtDelta > prevSubtDelta,
                            BubblesSource_Data.Sum_Delta => sumDelta > prevSumDelta,
                            _ => deltaTotal > prevDeltaTotal
                        };
                        bool sourcePositiveNegative = BubblesChartParams.UseChangeSeries ? deltaChange > 0 :
                        BubblesChartParams.BubblesSource_Input switch {
                            BubblesSource_Data.Delta_BuySell_Sum => deltaBuySell_Sum > 0,
                            BubblesSource_Data.Subtract_Delta => subtDelta > 0,
                            BubblesSource_Data.Sum_Delta => sumDelta > 0,
                            _ => deltaTotal > 0
                        };

                        Color fadingColor = sourceFading ? BuyColor : SellColor;
                        Color positiveNegativeColor = sourcePositiveNegative ? BuyColor : SellColor;

                        Color momentumColor = BubblesChartParams.BubblesMomentum_Input == BubblesMomentum_Data.Fading ? fadingColor : positiveNegativeColor;
                        Color colorMode = BubblesChartParams.BubblesColoring_Input == BubblesColoring_Data.Heatmap ? heatColor : momentumColor;

                        // X-value
                        (double x1Position, double dynLength) CalculateX1X2(double maxLength)
                        {
                            double maxLengthUltra = maxLength * 1.4 * BubblesChartParams.BubblesSizeMultiplier; // Slightly bigger than Bar Body
                            double maxLengthBubble = maxLength * BubblesChartParams.BubblesSizeMultiplier;

                            double dynMaxProportion = filterSize == 5 ? maxLengthUltra : maxLengthBubble;
                            double proportion = filterSize * (dynMaxProportion / 3);

                            double dynMaxLength = filterSize == 5 ? 5 : 4;
                            double dynLength = proportion / dynMaxLength;

                            // X1 position from LeftSide
                            double x1Position = filterSize == 5 ? -(maxLengthUltra / 3) :
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
                        double maxHeightBubble = heightPips * BubblesChartParams.BubblesSizeMultiplier;
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

                        if (MiscParams.ShowBubbleValue)
                        {
                            string deltaFmtd = deltaTotal > 0 ? FormatBigNumber(deltaTotal) : $"-{FormatBigNumber(Math.Abs(deltaTotal))}";
                            string changeFmtd = deltaChange > 0 ? FormatBigNumber(deltaChange) : $"-{FormatBigNumber(Math.Abs(deltaChange))}";
                            string buysellsum_Fmtd = FormatBigNumber(deltaBuySell_Sum);
                            string subtFmtd = subtDelta > 0 ? FormatBigNumber(subtDelta) : $"-{FormatBigNumber(Math.Abs(subtDelta))}";
                            string sumFmtd = FormatBigNumber(sumDelta);

                            string dynBubbleValue =  BubblesChartParams.UseChangeSeries ? changeFmtd :
                            BubblesChartParams.BubblesSource_Input switch {
                                BubblesSource_Data.Delta_BuySell_Sum => buysellsum_Fmtd,
                                BubblesSource_Data.Subtract_Delta => subtFmtd,
                                BubblesSource_Data.Sum_Delta => sumFmtd,
                                _ => deltaFmtd
                            };

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
                        if (BubblesRatioParams.ShowStrengthValue)
                        {
                            DrawOrCache(new DrawInfo
                            {
                                BarIndex = iStart,
                                Type = DrawType.Text,
                                Id = $"{iStart}_BubbleStrengthValue",
                                Text = $"{deltaStrength}",
                                X1 = xBar,
                                Y1 = y2, // bottom of bubble
                                horizontalAlignment = isPriceToAvoid ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                                verticalAlignment = VerticalAlignment.Center,
                                FontSize = FontSizeNumbers,
                                Color = RtnbFixedColor
                            });
                        }

                        if (BubblesLevelParams.EnableUltraNotification && BooleanLocks.lastIsUltra && !BooleanLocks.ultraNotify && BooleanLocks.ultraNotify_NewBar)
                        {
                            string symbolName = $"{Symbol.Name} ({Chart.TimeFrame.ShortName})";
                            string sourceString = BubblesChartParams.BubblesSource_Input.ToString();
                            string popupText = $"{symbolName} => Ultra {sourceString} at {Server.Time}";
                            
                            switch (BubblesLevelParams.NotificationType_Input) {
                                case NotificationType_Data.Sound:
                                    Notifications.PlaySound(BubblesLevelParams.Ultra_SoundType);
                                    break;
                                case NotificationType_Data.Popup:
                                    Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                    break;
                                default:
                                    Notifications.PlaySound(BubblesLevelParams.Ultra_SoundType);
                                    Notifications.ShowPopup(NOTIFY_CAPTION, popupText, PopupNotificationState.Information);
                                    break;
                            }
                            
                            BooleanLocks.ultraNotify = true;
                            BooleanLocks.ultraNotify_NewBar = false;
                        }
                        // At the final loop when the bar is closed, if filterSize == 5, notify in the next bar.
                        // When Backtesting in Price-Based Charts, this condition doesn't seem to be triggered,
                        // Works fine in real-time market though.
                        if (filterSize == 5) {
                            BooleanLocks.ultraNotify_NewBar = false;
                            BooleanLocks.lastIsUltra = true;
                        }
                        else
                            BooleanLocks.lastIsUltra = false;

                        // === Ultra Bubbles Levels ====
                        if (BubblesLevelParams.ShowUltraLevels)
                        {
                            // Main logic by LLM
                            // Fixed and modified for the desired behavior
                            /*
                               The idea (count bars that pass or touch it to break it)
                               was made by human creativity => aka cheap copy of:
                               - Shved Supply and Demand indicator without (verified, untested, etc..) info.
                               Yes, I was a MT4 enjoyer.
                            */
                            // 'open' already declared.
                            double close = Bars.ClosePrices[iStart];
                            double high = Bars.HighPrices[iStart];
                            double low = Bars.LowPrices[iStart];

                            // Check touches for all active rectangles
                            if (!BooleanLocks.ultraLevels)
                            {
                                foreach (var rect in ultraRectangles.Values)
                                {
                                    if (!rect.isActive)
                                        continue;

                                    double top = Math.Max(rect.Y1, rect.Y2);
                                    double bottom = Math.Min(rect.Y1, rect.Y2);

                                    // Check OHLC one by one
                                    if (TouchesRect_Bubbles(open, high, low, close, top, bottom, BubblesLevelParams.UltraBubblesBreak_Input))
                                    {
                                        rect.Touches++;

                                        // Update label
                                        if (UltraBubbles_ShowValue)
                                            UpdateLabel_Bubbles(rect, top, Bars.OpenTimes[iStart]);

                                        if (rect.Touches >= BubblesLevelParams.MaxCount)
                                        {
                                            rect.isActive = false;

                                            // Stop extension → fix rectangle to current bar
                                            rect.Rectangle.Time2 = Bars.OpenTimes[iStart];
                                            rect.Rectangle.Color = Color.FromArgb(50, rect.Rectangle.Color);

                                            // Finalize label
                                            if (UltraBubbles_ShowValue)
                                            {
                                                rect.Text.Text = $"{rect.Touches}";
                                                rect.Text.Color = RtnbFixedColor;
                                            }
                                        }
                                    }
                                }

                                BooleanLocks.ultraLevels = true;
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
                            if (filterSize == 5)
                            {
                                bool isUltraColor = BubblesLevelParams.UltraBubblesColoring_Input == UltraBubblesColoring_Data.Bubble_Color;

                                if (BubblesLevelParams.UltraBubbles_RectSizeInput == UltraBubbles_RectSizeData.High_Low)
                                    CreateRect_Bubbles(high, low, iStart, isUltraColor ? HeatmapUltra_Color : positiveNegativeColor);
                                else if (BubblesLevelParams.UltraBubbles_RectSizeInput == UltraBubbles_RectSizeData.HighOrLow_Close)
                                    CreateRect_Bubbles(close > open ? high : low, close, iStart, isUltraColor ? HeatmapUltra_Color : positiveNegativeColor);
                                else
                                    CreateRect_Bubbles(y1, y2, iStart, isUltraColor ? HeatmapUltra_Color : positiveNegativeColor);
                            }
                        }
                    }

                    break;
                }
            }
        }

        private void PopulateSeries(int iStart)
        {
            switch (GeneralParams.VolumeMode_Input)
            {
                case VolumeMode_Data.Normal:
                    Dynamic_Series[iStart] = VolumesRank.Values.Sum();
                    break;
                case VolumeMode_Data.Buy_Sell:
                    double sumValue = VolumesRank_Up.Values.Sum() + VolumesRank_Down.Values.Sum();
                    double subtValue = VolumesRank_Up.Values.Sum() - VolumesRank_Down.Values.Sum();
                    Dynamic_Series[iStart] = ResultParams.OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumValue : Math.Abs(subtValue);
                    break;
                default:
                {
                        
                    int deltaTotal = DeltaRank.Values.Sum();

                    int deltaBuy = DeltaRank.Values.Where(n => n > 0).Sum();
                    int deltaSell = DeltaRank.Values.Where(n => n < 0).Sum();
                    int deltaBuySell_Sum = deltaBuy + Math.Abs(deltaSell);
                    deltaBuySell_Sum = Math.Max(1, deltaBuySell_Sum);

                    int minDelta = MinMaxDelta[0];
                    int maxDelta = MinMaxDelta[1];
                    int subtDelta = minDelta - maxDelta;
                    int sumDelta = Math.Abs(minDelta) + Math.Abs(maxDelta);

                    bool isNoDraw_MinMax = SpikeFilterParams.SpikeSource_Input == SpikeSource_Data.Sum_Delta || 
                        BubblesChartParams.BubblesSource_Input switch {
                            BubblesSource_Data.Subtract_Delta =>  true,
                            BubblesSource_Data.Sum_Delta => true,
                            _ => false
                        };

                    // _Results = > original values for plus/minus checker (later)
                    if (!Delta_Results.ContainsKey(iStart))
                        Delta_Results.Add(iStart, deltaTotal);
                    else
                        Delta_Results[iStart] = deltaTotal;
                    
                    // Delta Sum (BuySell)
                    if (!DeltaBuySell_Sum_Results.ContainsKey(iStart))
                        DeltaBuySell_Sum_Results.Add(iStart, deltaBuySell_Sum);
                    else
                        DeltaBuySell_Sum_Results[iStart] = deltaBuySell_Sum;

                    // [Subtract, Sum] Delta => MinMax
                    if (ResultParams.ShowMinMaxDelta || isNoDraw_MinMax)
                    {
                        if (!SubtractDelta_Results.ContainsKey(iStart))
                            SubtractDelta_Results.Add(iStart, subtDelta);
                        else
                            SubtractDelta_Results[iStart] = subtDelta;

                        if (!SumDelta_Results.ContainsKey(iStart))
                            SumDelta_Results.Add(iStart, sumDelta);
                        else
                            SumDelta_Results[iStart] = sumDelta;
                    }

                    // Any Delta => Change
                    // Keep previous "Change" implementation for Delta(only)
                    int deltaChange = Delta_Results.Keys.Count <= 1 ? Delta_Results[iStart] : (Delta_Results[iStart] - Delta_Results[iStart - 1]);
                    
                    if (BubblesChartParams.EnableBubblesChart && BubblesChartParams.UseChangeSeries) {
                        deltaChange = BubblesChartParams.BubblesSource_Input switch {
                            BubblesSource_Data.Delta_BuySell_Sum => WindowChange(DeltaBuySell_Sum_Results, iStart),
                            BubblesSource_Data.Subtract_Delta => WindowChange(SubtractDelta_Results, iStart),
                            BubblesSource_Data.Sum_Delta => WindowChange(SumDelta_Results, iStart),
                            _ => WindowChange(Delta_Results, iStart)
                        };
                    }

                    if (!DeltaChange_Results.ContainsKey(iStart))
                        DeltaChange_Results.Add(iStart, deltaChange);
                    else
                        DeltaChange_Results[iStart] = deltaChange;
                    
                    // _Series => always use absolute values (positive)
                    Dynamic_Series[iStart] = Math.Abs(deltaTotal);
                    DeltaChange_Series[iStart] = Math.Abs(deltaChange);
                    DeltaBuySell_Sum_Series[iStart] = Math.Abs(deltaBuySell_Sum);
                    if (ResultParams.ShowMinMaxDelta || isNoDraw_MinMax)
                    {
                        SubtractDelta_Series[iStart] = Math.Abs(subtDelta);
                        SumDelta_Series[iStart] = Math.Abs(sumDelta);
                    }
                    break;
                }
            }
            int WindowChange(Dictionary<int, int> source, int index)
            {
                int period = BubblesChartParams.changePeriod;
                int result = source[index];

                if (period <= 1 || index == 0)
                    return result;

                int available = Math.Min(period - 1, index);

                for (int i = 1; i <= available; i++)
                {
                    switch (BubblesChartParams.ChangeOperator_Input) {
                        case ChangeOperator_Data.Plus_KeepSign:
                            result += source[index - i]; break;
                        case ChangeOperator_Data.Minus_KeepSign:
                            result -= source[index - i]; break;
                        case ChangeOperator_Data.Plus_Absolute:
                            result += Math.Abs(source[index - i]); break;
                        case ChangeOperator_Data.Minus_Absolue:
                            result -= Math.Abs(source[index - i]); break;
                    }
                    
                }
                
                return result;
            }
        }

        private Dictionary<double, double> CreateSpikeProfile(int iStart)
        {
            if (!SpikeFilterParams.EnableSpikeFilter)
                return new();

            // Segments_Bar already sorted
            double[] validSegments = Segments_Bar.Where(key => VolumesRank.ContainsKey(key)).ToArray();

            double[] absProfile = validSegments.Select(key => (double)Math.Abs(DeltaRank[key])).ToArray();
            double[] normProfile = Array.Empty<double>();

            IndicatorDataSeries sourceSeries = SpikeFilterParams.SpikeSource_Input switch {
                SpikeSource_Data.Delta_BuySell_Sum => DeltaBuySell_Sum_Series,
                SpikeSource_Data.Sum_Delta => SumDelta_Series,
                _ => Dynamic_Series
            };
            double sourceValue = sourceSeries[iStart];

            DeltaSwitch deltaSwitch = SpikeFilterParams.SpikeSource_Input switch {
                SpikeSource_Data.Delta_BuySell_Sum => DeltaSwitch.DeltaBuySell_Sum,
                SpikeSource_Data.Sum_Delta => DeltaSwitch.Sum,
                _ => DeltaSwitch.None
            };

            double filterValue = 0;
            switch (SpikeFilterParams.SpikeFilter_Input)
            {
                case SpikeFilter_Data.MA:
                {
                    if (UseCustomMAs)
                        filterValue = CustomMAs(sourceValue, iStart, SpikeFilterParams.MAperiod, CustomMAType.Spike, deltaSwitch, MASwitch.Spike, false);
                    else
                        filterValue = SpikeFilterParams.SpikeSource_Input switch {
                            SpikeSource_Data.Delta_BuySell_Sum => MASpike_DeltaBuySell_Sum.Result[iStart],
                            SpikeSource_Data.Sum_Delta => MASpike_SumDelta.Result[iStart],
                            _ => MASpike_Delta.Result[iStart]
                        };
                    break;
                }
                case SpikeFilter_Data.Standard_Deviation:
                {
                    if (UseCustomMAs)
                        filterValue = CustomMAs(sourceValue, iStart, SpikeFilterParams.MAperiod, CustomMAType.Spike, deltaSwitch, MASwitch.Spike, true);
                    else
                        filterValue = SpikeFilterParams.SpikeSource_Input switch {
                            SpikeSource_Data.Delta_BuySell_Sum => StdDevSpike_DeltaBuySell_Sum.Result[iStart],
                            SpikeSource_Data.Sum_Delta => StdDevSpike_SumDelta.Result[iStart],
                            _ => StdDevSpike_Delta.Result[iStart]
                        };
                    break;
                }
                case SpikeFilter_Data.L1Norm:
                {
                    // Filter on Results
                    double[] window = new double[SpikeFilterParams.MAperiod];

                    for (int k = 0; k < SpikeFilterParams.MAperiod; k++)
                        window[k] = sourceSeries[iStart - SpikeFilterParams.MAperiod + 1 + k];

                    filterValue = Filters.L1Norm_Strength(window);
                    filterValue *= 100;

                    // Filter on Profile
                    normProfile = Filters.L1Norm_Profile(absProfile);
                    break;
                }
                case SpikeFilter_Data.SoftMax_Power:
                {
                    // Filter on Results
                    double[] window = new double[SpikeFilterParams.MAperiod];

                    for (int k = 0; k < SpikeFilterParams.MAperiod; k++)
                        window[k] = sourceSeries[iStart - SpikeFilterParams.MAperiod + 1 + k];

                    filterValue = Filters.PowerSoftmax_Strength(window);
                    filterValue *= 100;

                    // Filter on Profile
                    normProfile = Filters.PowerSoftmax_Profile(absProfile);
                    break;
                }
            }

            // Required for [L1Norm, SoftMax_Power]
            int normLength = normProfile.Length;
            if (normLength > 0)
            {
                double[] filterProfile = new double[normLength];
                for (int k = 0; k < normLength; k++)
                    filterProfile[k] = Math.Round(normProfile[k] * 100, 2);

                normProfile = filterProfile;
            }

            // Final step => rowStrength
            double[] whichProfile = normLength > 0 ? normProfile : absProfile;
            int length = whichProfile.Length;

            double[] strengthProfile = new double[length];
            for (int k = 0; k < length; k++)
            {
                double rowStrength = Math.Abs(whichProfile[k] / filterValue);
                strengthProfile[k] = Math.Round(rowStrength, 2);
            }

            if (SpikeRatioParams.SpikeRatio_Input == SpikeRatio_Data.Percentage)
            {
                // From srl-python-indicators/order_flow_ticks.py
                /*
                   simple math, normalize the values to 0~1, just:
                       - calculate the sum of all elements absolute value
                       - divide each element by the sum
                       - aka L1 normalization
                   added MA to get the values >= 100%, as well as, percentile-like behavior of bubbles chart.
                */
                double sumTotal = strengthProfile.Sum();
                PercentageRatio_Series[iStart] = sumTotal;

                double maTotal = UseCustomMAs ?
                                 CustomMAs(sumTotal, iStart, SpikeRatioParams.MAperiod_PctSpike, CustomMAType.SpikePctRatio, DeltaSwitch.Spike_PctRatio, MASwitch.Spike, false) :
                                 MARatio_Percentage.Result[iStart];

                for (int k = 0; k < length; k++)
                {
                    double rowStrength_Pct = strengthProfile[k] / maTotal;
                    strengthProfile[k] = Math.Round(rowStrength_Pct * 100, 1);
                }
            }
            
            Dictionary<double, double> dict = new();
            for (int i = 0; i < validSegments.Length; i++)
                dict[validSegments[i]] = strengthProfile[i];
                
            return dict;
        }

        // Spike Levels
        private void CreateRect_Spike(double p1, double p2, int index, int i, string spikeDictKey, Color color)
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
            rectangle.IsFilled = MiscParams.FillHist;

            ChartText label = null;
            if (SpikeLevels_ShowValue)
            {
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

            RectInfo rectangleInfo = new()
            {
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
        private static void UpdateLabel_Spike(RectInfo rect, DateTime time)
        {
            rect.Text.Text = $"{rect.Touches}";
            rect.Text.Time = time;
        }
        private static bool TouchesRect_Spike(double o, double h, double l, double c, double top, double bottom)
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

        // Ultra Bubbles Levels
        private void CreateRect_Bubbles(double p1, double p2, int index, Color color)
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
            rectangle.IsFilled = MiscParams.FillHist;

            ChartText label = null;
            if (UltraBubbles_ShowValue)
            {
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

            RectInfo rectangleInfo = new()
            {
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
        private static bool TouchesRect_Bubbles(double o, double h, double l, double c, double top, double bottom, UltraBubblesBreak_Data selectedBreak)
        {
            if (selectedBreak == UltraBubblesBreak_Data.Close_Only || selectedBreak == UltraBubblesBreak_Data.Close_plus_BarBody)
            {
                if (o >= bottom && o <= top)
                    return true;

                if (selectedBreak == UltraBubblesBreak_Data.Close_plus_BarBody)
                {
                    // If bar fully crosses rectangle (high above and low below)
                    if (h > top && l < bottom)
                        return true;
                }
            }
            else if (selectedBreak == UltraBubblesBreak_Data.OHLC_plus_BarBody)
            {
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
        private static void UpdateLabel_Bubbles(RectInfo rect, double top, DateTime time)
        {
            rect.Text.Text = $"{rect.Touches}";
            rect.Text.Time = time;
            rect.Text.Y = top;
        }

        private double CustomMAs(double seriesValue, int index, int maPeriod,
                                 MAType_Data maType, DeltaSwitch deltaSwitch = DeltaSwitch.None, MASwitch maSwitch = MASwitch.Large,
                                 bool isStdDev = false
                                )
        {
            Dictionary<int, double> buffer = deltaSwitch switch {
                DeltaSwitch.DeltaChange => _deltaBuffer.Change,
                DeltaSwitch.DeltaBuySell_Sum => _deltaBuffer.BuySell_Sum,
                DeltaSwitch.Subtract => _deltaBuffer.Subtract,
                DeltaSwitch.Sum => _deltaBuffer.Sum,
                DeltaSwitch.Spike_PctRatio => _deltaBuffer.Spike_PctRatio,
                _ => _dynamicBuffer
            };

            if (!buffer.ContainsKey(index))
                buffer.Add(index, seriesValue);
            else
                buffer[index] = seriesValue;

            Dictionary<int, double> prevMA_Dict = maSwitch switch
            {
                MASwitch.Bubbles => deltaSwitch switch {
                    DeltaSwitch.DeltaChange => _deltaBuffer.MAChange_Bubbles,
                    DeltaSwitch.DeltaBuySell_Sum => _deltaBuffer.MABuySellSum_Bubbles,
                    DeltaSwitch.Subtract => _deltaBuffer.MASubtract_Bubbles,
                    DeltaSwitch.Sum => _deltaBuffer.MASum_Bubbles,
                    _ => _maDynamic
                },
                MASwitch.Spike => deltaSwitch switch {
                    DeltaSwitch.DeltaBuySell_Sum => _deltaBuffer.MABuySellSum_Spike,
                    DeltaSwitch.Sum => _deltaBuffer.MASum_Spike,
                    DeltaSwitch.Spike_PctRatio => _deltaBuffer.MASpike_PctRatio,
                    _ => _maDynamic
                },
                // Large
                _ => deltaSwitch switch {
                    DeltaSwitch.Subtract => _deltaBuffer.MASubtract_Large,
                    _ => _maDynamic
                }
            };

            double maValue = maType switch
            {
                MAType_Data.Simple => CustomMA.SMA(index, maPeriod, buffer),
                MAType_Data.Exponential => CustomMA.EMA(index, maPeriod, buffer, prevMA_Dict),
                MAType_Data.Weighted => CustomMA.WMA(index, maPeriod, buffer),
                MAType_Data.Triangular => CustomMA.TMA(index, maPeriod, buffer),
                MAType_Data.Hull => CustomMA.Hull(index, maPeriod, buffer),
                MAType_Data.VIDYA => CustomMA.VIDYA(index, maPeriod, buffer, prevMA_Dict),
                MAType_Data.WilderSmoothing => CustomMA.Wilder(index, maPeriod, buffer, prevMA_Dict),
                MAType_Data.KaufmanAdaptive => CustomMA.KAMA(index, maPeriod, 2, 30, buffer, prevMA_Dict),
                _ => double.NaN
            };

            return isStdDev ? CustomMA.StdDev(index, maPeriod, maValue, buffer) : maValue;
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

            switch (MiscParams.SegmentsInterval_Input)
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
            return MiscParams.SegmentsInterval_Input switch
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
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Buy_Sell && (extraProfiles == ExtraProfiles.Weekly || extraProfiles == ExtraProfiles.Monthly))
               return;
               
            // ==== VP ====
            if (!drawOnly)
                VP_Tick(index, true, extraProfiles, fixedKey);
                
            // ==== Drawing ====
            if (Segments_VP.Count == 0 || isLoop)
                return;

            // Results or Fixed Range
            Bars TF_Bars = extraProfiles switch {
                ExtraProfiles.MiniVP => MiniVPs_Bars,
                ExtraProfiles.Weekly => WeeklyBars,
                ExtraProfiles.Monthly => MonthlyBars,
                _ => MiscParams.ODFInterval_Input == ODFInterval_Data.Daily ? DailyBars : WeeklyBars
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
            double maxHalfWidth = ProfileParams.HistogramWidth_Input switch {
                HistWidth_Data._15 => 1.12,
                HistWidth_Data._30 => 1.25,
                HistWidth_Data._50 => 1.40,
                _ => 1.75
            };

            double proportion_VP = maxLength - (maxLength / maxWidth);
            if (selectedWidth == HistWidth_Data._100)
                proportion_VP = maxLength;

            string prefix = extraProfiles == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";
            bool isRightSide = ProfileParams.HistogramSide_Input == HistSide_Data.Right;

            // Profile Selection
            IDictionary<double, double> vpNormal = new Dictionary<double, double>();
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Normal) {
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
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Buy_Sell) {
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
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Delta) {
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

            // (micro)Optimization for all modes
            double maxValue = GeneralParams.VolumeMode_Input switch {
                VolumeMode_Data.Normal => vpNormal.Any() ? vpNormal.Values.Max() : 0,
                VolumeMode_Data.Delta => vpDelta.Any() ? vpDelta.Values.Max() : 0,
                _ => 0
            };

            double buyMax = 0;
            double sellMax = 0;
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Buy_Sell) {
                buyMax = vpBuy.Any() ? vpBuy.Values.Max() : 0;
                sellMax = vpSell.Any() ? vpSell.Values.Max() : 0;
            }

            IEnumerable<double> negativeList = new List<double>();
            if (GeneralParams.VolumeMode_Input == VolumeMode_Data.Delta)
                negativeList = vpDelta.Values.Where(n => n < 0);

            // Segments selection
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

                    if (ProfileParams.FillHist_VP)
                        volHist.IsFilled = true;

                    if (isRightSide)
                    {
                        volHist.Time1 = xBar;
                        volHist.Time2 = xBar.AddMilliseconds(-dynLength);
                    }

                    if (ProfileParams.ShowHistoricalNumbers) {
                        double volumeNumber = currentVolume;
                        string volumeNumberFmtd = FormatNumbers ? FormatBigNumber(volumeNumber) : $"{volumeNumber}";

                        ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Normal", volumeNumberFmtd, isRightSide ? xBar : x1_Start, y1_text, RtnbFixedColor);
                        Center.HorizontalAlignment = isRightSide ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                        Center.FontSize = FontSizeNumbers;

                        if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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
                            }
                            else {
                                // Use Weekly position
                                volHist.Time1 = dateOffset_Duo;
                                volHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Intraday);
                                if (ProfileParams.FillIntradaySpace) {
                                    volHist.Time1 = dateOffset;
                                    volHist.Time2 = dateOffset.AddMilliseconds(dynLength_Intraday);
                                }
                            }
                        }

                        if (ProfileParams.ShowIntradayNumbers) {
                            double volumeNumber = currentVolume;
                            string volumeNumberFmtd = FormatNumbers ? FormatBigNumber(volumeNumber) : $"{volumeNumber}";

                            ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Normal",
                                volumeNumberFmtd, volHist.Time1, y1_text, RtnbFixedColor);
                            Center.FontSize = FontSizeResults;

                            Center.HorizontalAlignment = HorizontalAlignment.Left;
                            if (extraProfiles == ExtraProfiles.Weekly) {
                                if (!ProfileParams.EnableMonthlyProfile && ProfileParams.FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                            if (extraProfiles == ExtraProfiles.Monthly) {
                                if (ProfileParams.FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
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
                    if (ProfileParams.FillHist_VP)
                    {
                        buyHist.IsFilled = true;
                        sellHist.IsFilled = true;
                    }
                    if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
                    {
                        sellHist.Time1 = xBar;
                        sellHist.Time2 = xBar.AddMilliseconds(-dynLengthSell);
                        buyHist.Time1 = xBar;
                        buyHist.Time2 = xBar.AddMilliseconds(-dynLengthBuy);
                    }

                    if (ProfileParams.ShowHistoricalNumbers) {
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

                        if (ProfileParams.HistogramSide_Input == HistSide_Data.Right) {
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

                        if (ProfileParams.FillHist_VP)
                            subtHist.IsFilled = true;

                        if (ProfileParams.ShowIntradayNumbers) {
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

                        if (ProfileParams.ShowIntradayNumbers) {
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
                    double negativeDeltaMax = negativeDeltaList.Any() ? Math.Abs(negativeDeltaList.Min()) : 0;

                    double deltaMax = positiveDeltaMax > negativeDeltaMax ? positiveDeltaMax : negativeDeltaMax;

                    double proportion_Delta = Math.Abs(currentDelta) * proportion_VP;
                    double dynLength_Delta = proportion_Delta / deltaMax;

                    Color colorHist = currentDelta >= 0 ? BuyColor : SellColor;
                    DateTime x2 = x1_Start.AddMilliseconds(dynLength_Delta);

                    ChartRectangle deltaHist = Chart.DrawRectangle($"{prefix}_{i}_VP_{extraProfiles}_Delta", x1_Start, lowerSegmentY1, x2, upperSegmentY2, colorHist);

                    if (ProfileParams.FillHist_VP)
                        deltaHist.IsFilled = true;

                    if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
                    {
                        deltaHist.Time1 = xBar;
                        deltaHist.Time2 = deltaHist.Time2 != x1_Start ? xBar.AddMilliseconds(-dynLength_Delta) : x1_Start;
                    }

                    if (ProfileParams.ShowHistoricalNumbers) {
                        double deltaNumber = currentDelta;
                        string deltaNumberFmtd = deltaNumber > 0 ? FormatBigNumber(deltaNumber) : $"-{FormatBigNumber(Math.Abs(deltaNumber))}";
                        string deltaString = FormatNumbers ? deltaNumberFmtd : $"{deltaNumber}";

                        ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Delta", deltaString, x1_Start, y1_text, RtnbFixedColor);
                        Center.HorizontalAlignment = isRightSide ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                        Center.FontSize = FontSizeNumbers;

                        if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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
                            if (!ProfileParams.EnableMonthlyProfile && ProfileParams.FillIntradaySpace) {
                                deltaHist.Time1 = dateOffset;
                                deltaHist.Time2 = dateOffset.AddMilliseconds(dynLength_Delta);
                            }
                        }

                        if (extraProfiles == ExtraProfiles.Monthly) {
                            if (ProfileParams.EnableWeeklyProfile) {
                                // Show after
                                deltaHist.Time1 = dateOffset_Triple;
                                deltaHist.Time2 = dateOffset_Triple.AddMilliseconds(-dynLength_Delta);
                                // Show after together
                                if (ProfileParams.FillIntradaySpace) {
                                    deltaHist.Time1 = dateOffset_Duo;
                                    deltaHist.Time2 = dateOffset_Duo.AddMilliseconds(dynLength_Delta);
                                }
                            }
                            else {
                                // Use Weekly position
                                deltaHist.Time1 = dateOffset_Duo;
                                deltaHist.Time2 = dateOffset_Duo.AddMilliseconds(-dynLength_Delta);
                                if (ProfileParams.FillIntradaySpace) {
                                    deltaHist.Time1 = dateOffset;
                                    deltaHist.Time2 = dateOffset.AddMilliseconds(dynLength_Delta);
                                }
                            }
                        }

                        if (ProfileParams.ShowIntradayNumbers) {
                            double deltaNumber = currentDelta;
                            string deltaNumberFmtd = deltaNumber > 0 ? FormatBigNumber(deltaNumber) : $"-{FormatBigNumber(Math.Abs(deltaNumber))}";
                            string deltaString = FormatNumbers ? deltaNumberFmtd : $"{deltaNumber}";

                            ChartText Center = Chart.DrawText($"{prefix}_{i}_VP_{extraProfiles}_Number_Delta", deltaString, deltaHist.Time1, y1_text, RtnbFixedColor);
                            Center.FontSize = FontSizeResults;

                            Center.HorizontalAlignment = HorizontalAlignment.Left;
                            if (extraProfiles == ExtraProfiles.Weekly) {
                                if (!ProfileParams.EnableMonthlyProfile && ProfileParams.FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                            if (extraProfiles == ExtraProfiles.Monthly) {
                                if (ProfileParams.FillIntradaySpace)
                                    Center.HorizontalAlignment = HorizontalAlignment.Right;
                            }
                        }

                        intraDate = deltaHist.Time1;
                    }
                }

                switch (GeneralParams.VolumeMode_Input)
                {
                    case VolumeMode_Data.Normal:
                    {
                        double value = vpNormal[priceKey];
                        // Draw histograms and update 'intraDate', if applicable
                        DrawRectangle_Normal(value, maxValue, intraBool);
                        break;
                    }
                    case VolumeMode_Data.Buy_Sell:
                    {
                        if (vpBuy.ContainsKey(priceKey) && vpSell.ContainsKey(priceKey))
                            DrawRectangle_BuySell(vpBuy[priceKey], vpSell[priceKey], buyMax, sellMax, isIntraday);
                        break;
                    }
                    default:
                    {
                        double value = vpDelta[priceKey];
                        // Draw histograms and update 'intraDate', if applicable
                        DrawRectangle_Delta(value, maxValue, negativeList, intraBool);
                        break;
                    }
                }
            }

            // Drawings that don't require each segment-price as y-axis
            // It can/should be outside SegmentsLoop for better performance.

            double _lowest = TF_Bars.LowPrices[TF_idx];
            if (double.IsNaN(_lowest)) // Mini VPs avoid crash after recalculating
                _lowest = TF_Bars.LowPrices.LastValue;
            double y1_lowest = extraProfiles == ExtraProfiles.Fixed ? fixedLowest : _lowest;

            if (extraProfiles == ExtraProfiles.MiniVP && ProfileParams.ShowMiniResults ||
                extraProfiles != ExtraProfiles.MiniVP && ResultParams.ShowResults)
            {
                switch (GeneralParams.VolumeMode_Input)
                {
                    case VolumeMode_Data.Normal:
                    {
                        double sum = Math.Round(vpNormal.Values.Sum());
                        string strValue = FormatResults ? FormatBigNumber(sum) : $"{sum}";

                        ChartText Center = Chart.DrawText($"{prefix}_VP_{extraProfiles}_Normal_Result", $"\n{strValue}", x1_Start, y1_lowest, VolumeColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;

                        if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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

                        string strFormated = ResultParams.OperatorBuySell_Input == OperatorBuySell_Data.Sum ? sumFmtd :
                                             ResultParams.OperatorBuySell_Input == OperatorBuySell_Data.Subtraction ? subtractFmtd : $"{divide}";

                        Color centerColor = Math.Round(percentBuy) > Math.Round(percentSell) ? BuyColor : SellColor;

                        Center = Chart.DrawText($"{prefix}_VP_{extraProfiles}_BuySell_Result", $"\n{strFormated}", x1_Start, y1_lowest, centerColor);
                        Center.HorizontalAlignment = HorizontalAlignment.Center;
                        Center.FontSize = FontSizeResults - 1;

                        if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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

                        if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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

                        if (ResultParams.ShowMinMaxDelta)
                            Draw_MinMaxDelta(extraProfiles, fixedKey, y1_lowest, x1_Start, xBar, isIntraday, prefix);

                        break;
                    }
                }
            }

            // For [Normal, Delta] only
            IDictionary<double, double> vpDict = GeneralParams.VolumeMode_Input switch
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
                // HVN/LVN
                DrawVolumeNodes(vpDict, iStart, x1_Start, xBar, extraProfiles, isIntraday, intraDate, fixedKey);
            }

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

                if (!ResultParams.ShowOnlySubtDelta)
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

                    if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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

                    if (ProfileParams.HistogramSide_Input == HistSide_Data.Right)
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

        // *********** MWM PROFILES ***********
        private void CreateMiniVPs(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (ProfileParams.EnableMiniProfiles)
            {
                int miniIndex = MiniVPs_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int miniStart = Bars.OpenTimes.GetIndexByTime(MiniVPs_Bars.OpenTimes[miniIndex]);

                if (index == miniStart ||
                    (index - 1) == miniStart && isPriceBased_Chart ||
                    (index - 1) == miniStart && (index - 1) != ClearIdx.Mini || loopStart
                ) {
                    if (!IsLastBar)
                        PerformanceTick.startIdx_Mini = PerformanceTick.lastIdx_Mini;

                    MiniRank.ClearAllModes();
                    ClearIdx.Mini = index == miniStart ? index : (index - 1);
                }
                if (!isConcurrent)
                    VolumeProfile(miniStart, index, ExtraProfiles.MiniVP, isLoop);
                else
                {
                    _Tasks.MiniVP ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.MiniVP, _Tasks.cts.Token));

                    LiveVPIndexes.Mini = miniStart;

                    if (index != miniStart) {
                        lock (_Locks.MiniVP)
                        VolumeProfile(miniStart, index, ExtraProfiles.MiniVP, false, true);
                    }
                }
            }
        }
        private void CreateWeeklyVP(int index, bool loopStart = false, bool isLoop = false, bool isConcurrent = false) {
            if (ProfileParams.EnableWeeklyProfile)
            {
                // Avoid recalculating the same period.
                if (MiscParams.ODFInterval_Input == ODFInterval_Data.Weekly)
                    return;

                int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                if (index == weekStart ||
                    (index - 1) == weekStart && isPriceBased_Chart || loopStart
                ) {
                    if (!IsLastBar)
                        PerformanceTick.startIdx_Weekly = PerformanceTick.lastIdx_Weekly;
                    WeeklyRank.ClearAllModes();
                }

                if (!isConcurrent)
                    VolumeProfile(weekStart, index, ExtraProfiles.Weekly, isLoop);
                else
                {
                    _Tasks.WeeklyVP ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.Weekly, _Tasks.cts.Token));

                    LiveVPIndexes.Weekly = weekStart;

                    if (index != weekStart) {
                        lock (_Locks.WeeklyVP)
                            VolumeProfile(weekStart, index, ExtraProfiles.Weekly, false, true);
                    }

                    DateTime weekStartDate = WeeklyBars.OpenTimes[weekIndex];
                    TickObjs.firstTickTime = TickObjs.firstTickTime > weekStartDate ? TicksOHLC.OpenTimes.FirstOrDefault() : TickObjs.firstTickTime;
                    if (TickObjs.firstTickTime > weekStartDate)
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
            if (ProfileParams.EnableMonthlyProfile)
            {
                int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                if (index == monthStart ||
                    (index - 1) == monthStart && isPriceBased_Chart || loopStart
                ) {
                    if (!IsLastBar)
                        PerformanceTick.startIdx_Monthly = PerformanceTick.lastIdx_Monthly;
                    MonthlyRank.ClearAllModes();
                }
                if (!isConcurrent)
                    VolumeProfile(monthStart, index, ExtraProfiles.Monthly, isLoop);
                else
                {
                    _Tasks.MonthlyVP ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.Monthly, _Tasks.cts.Token));

                    LiveVPIndexes.Monthly = monthStart;

                    if (index != monthStart) {
                        lock (_Locks.MonthlyVP)
                            VolumeProfile(monthStart, index, ExtraProfiles.Monthly, false, true);
                    }

                    DateTime monthStartDate = MonthlyBars.OpenTimes[monthIndex];
                    TickObjs.firstTickTime = TickObjs.firstTickTime > monthStartDate ? TicksOHLC.OpenTimes.FirstOrDefault() : TickObjs.firstTickTime;
                    if (TickObjs.firstTickTime > monthStartDate)
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

            bool updateStrategy = ProfileParams.UpdateProfile_Input switch {
                UpdateProfile_Data.ThroughSegments_Balanced => Math.Abs(price - prevUpdatePrice) >= rowHeight,
                UpdateProfile_Data.Through_2_Segments_Best => Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight),
                _ => true
            };

            if (updateStrategy || BooleanUtils.isUpdateVP || BooleanUtils.configHasChanged)
            {
                if (!onlyMini)
                {
                    if (ProfileParams.EnableMonthlyProfile) {

                        int monthIndex = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        DateTime monthStartDate = MonthlyBars.OpenTimes[monthIndex];
                        int monthStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[monthIndex]);

                        if (TickObjs.firstTickTime > monthStartDate) {
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

                    if (ProfileParams.EnableWeeklyProfile && MiscParams.ODFInterval_Input != ODFInterval_Data.Weekly)
                    {
                        int weekIndex = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                        DateTime weekStartDate = WeeklyBars.OpenTimes[weekIndex];
                        int weekStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[weekIndex]);

                        TickObjs.firstTickTime = TickObjs.firstTickTime > weekStartDate ? TicksOHLC.OpenTimes.FirstOrDefault() : TickObjs.firstTickTime;
                        if (TickObjs.firstTickTime > weekStartDate) {
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

                    if (ProfileParams.EnableMiniProfiles) {
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

            BooleanUtils.isUpdateVP = false;
            BooleanUtils.configHasChanged = false;

            if (ProfileParams.UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                prevUpdatePrice = price;
        }

        private void LiveVP_Concurrent(int index, int indexStart)
        {
            if (!ProfileParams.EnableMainVP && !ProfileParams.EnableMiniProfiles)
                return;

            double price = Bars.ClosePrices[index];
            bool updateStrategy = ProfileParams.UpdateProfile_Input switch {
                UpdateProfile_Data.ThroughSegments_Balanced => Math.Abs(price - prevUpdatePrice) >= rowHeight,
                UpdateProfile_Data.Through_2_Segments_Best => Math.Abs(price - prevUpdatePrice) >= (rowHeight + rowHeight),
                _ => true
            };

            if (updateStrategy || BooleanUtils.isUpdateVP || BooleanUtils.configHasChanged)
            {
                if (Bars.Count > BarTimes_Array.Length)
                {
                    lock (_Locks.Bar)
                        BarTimes_Array = Bars.OpenTimes.ToArray();
                }
                lock (_Locks.Tick) {
                    int startFrom = ProfileParams.EnableMonthlyProfile ? 
                                    PerformanceTick.startIdx_Monthly :
                                    (ProfileParams.EnableWeeklyProfile && MiscParams.ODFInterval_Input != ODFInterval_Data.Weekly) ?
                                    PerformanceTick.startIdx_Weekly :
                                    ProfileParams.EnableMainVP ? 
                                    PerformanceTick.startIdx_MainVP :
                                    (ProfileParams.MiniVPs_Timeframe >= TimeFrame.Hour4 ? PerformanceTick.startIdx_Mini : PerformanceTick.startIdx_MainVP);

                    TickBars_List = new List<Bar>(TicksOHLC.Skip(startFrom - 1));
                }

                liveVP_RunWorker = true;
            }
            _Tasks.cts ??= new CancellationTokenSource();

            CreateMonthlyVP(index, isConcurrent: true);
            CreateWeeklyVP(index, isConcurrent: true);
            CreateMiniVPs(index, isConcurrent: true);

            if (ProfileParams.EnableMainVP)
            {
                _Tasks.MainVP ??= Task.Run(() => LiveVP_Worker(ExtraProfiles.No, _Tasks.cts.Token));
                LiveVPIndexes.MainVP = indexStart;
                if (index != indexStart) {
                    lock (_Locks.MainVP)
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

            Dictionary<double, double> Worker_VolumesRank = new();
            Dictionary<double, double> Worker_VolumesRank_Up = new();
            Dictionary<double, double> Worker_VolumesRank_Down = new();
            Dictionary<double, double> Worker_VolumesRank_Subt = new();
            Dictionary<double, double> Worker_DeltaRank = new();
            double[] Worker_MinMaxDelta = { 0, 0 };

            DateTime lastTime = new();
            IEnumerable<DateTime> TimesCopy = Array.Empty<DateTime>();
            IEnumerable<Bar> TicksCopy;

            while (!token.IsCancellationRequested)
            {
                if (!liveVP_RunWorker) {
                    // Stop itself
                    if (extraID == ExtraProfiles.No && !ProfileParams.EnableMainVP) {
                        _Tasks.MainVP = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.MiniVP && !ProfileParams.EnableMiniProfiles) {
                        _Tasks.MiniVP = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Weekly && !ProfileParams.EnableWeeklyProfile) {
                        _Tasks.WeeklyVP = null;
                        return;
                    }
                    if (extraID == ExtraProfiles.Monthly && !ProfileParams.EnableMonthlyProfile) {
                        _Tasks.MonthlyVP = null;
                        return;
                    }

                    Thread.Sleep(100);
                    continue;
                }

                try
                {
                    Worker_VolumesRank = new();
                    Worker_VolumesRank_Up = new();
                    Worker_VolumesRank_Down = new();
                    Worker_VolumesRank_Subt = new();
                    Worker_DeltaRank = new();
                    double[] resetDelta = {0, 0};
                    Worker_MinMaxDelta = resetDelta;

                    // Chart Bars
                    int startIndex = extraID switch {
                        ExtraProfiles.MiniVP => LiveVPIndexes.Mini,
                        ExtraProfiles.Weekly => LiveVPIndexes.Weekly,
                        ExtraProfiles.Monthly => LiveVPIndexes.Monthly,
                        _ => LiveVPIndexes.MainVP
                    };
                    DateTime lastBarTime = GetByInvoke(() => Bars.LastBar.OpenTime);

                    // Replace only when needed
                    if (lastTime != lastBarTime) {
                        lock (_Locks.Bar)
                            TimesCopy = BarTimes_Array.Skip(startIndex);
                        lastTime = lastBarTime;
                    }
                    int endIndex = TimesCopy.Count();

                    // 
                    // Tick => Always replace
                    // The ".Skip(startTickIndex)" is already done in LiveVP_Concurrent()
                    lock (_Locks.Tick)
                        TicksCopy = TickBars_List;
                    
                    for (int i = 0; i < endIndex; i++)
                    {
                        Worker_VP_Tick(i, extraID, i == (endIndex - 1));
                    }
                                     
                    object whichLock = extraID switch {
                        ExtraProfiles.MiniVP => _Locks.MiniVP,
                        ExtraProfiles.Weekly => _Locks.WeeklyVP,
                        ExtraProfiles.Monthly => _Locks.MonthlyVP,
                        _ => _Locks.MainVP
                    };
  
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

                        BooleanUtils.isUpdateVP = false;
                        BooleanUtils.configHasChanged = false;

                        if (ProfileParams.UpdateProfile_Input != UpdateProfile_Data.EveryTick_CPU_Workout)
                            prevUpdatePrice = TicksCopy.Last().Close;
                    }
                }
                catch (Exception e) { Print($"CRASH at LiveVP_Worker => {extraID}: {e}"); }

                liveVP_RunWorker = false;
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
                    bool modeIsBuySell = GeneralParams.VolumeMode_Input == VolumeMode_Data.Buy_Sell; 
                    bool modeIsDelta = GeneralParams.VolumeMode_Input == VolumeMode_Data.Delta;
                    
                    List<double> segmentsSource = Segments_VP;

                    double prevSegmentValue = 0.0;
                    for (int i = 0; i < segmentsSource.Count; i++)
                    {
                        if (prevSegmentValue != 0 && tickPrice >= prevSegmentValue && tickPrice <= segmentsSource[i])
                        {
                            double priceKey = segmentsSource[i];

                            double prevDelta = 0;
                            if (modeIsDelta && ResultParams.ShowMinMaxDelta)
                                prevDelta = Worker_DeltaRank.Values.Sum();

                            if (Worker_VolumesRank.ContainsKey(priceKey))
                            {
                                Worker_VolumesRank[priceKey] += 1;
                                
                                if (modeIsBuySell || modeIsDelta) 
                                {
                                    if (tickPrice > prevTick)
                                        Worker_VolumesRank_Up[priceKey] += 1;
                                    else if (tickPrice < prevTick)
                                        Worker_VolumesRank_Down[priceKey] += 1;
                                    else if (tickPrice == prevTick)
                                    {
                                        Worker_VolumesRank_Up[priceKey] += 1;
                                        Worker_VolumesRank_Down[priceKey] += 1;
                                    }
                                    
                                    Worker_VolumesRank_Subt[priceKey] = Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey];
                                }

                                if (modeIsDelta)
                                    Worker_DeltaRank[priceKey] += (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]);
                            }
                            else
                            {
                                Worker_VolumesRank.Add(priceKey, 1);
                                if (modeIsBuySell || modeIsDelta) 
                                {
                                    if (!Worker_VolumesRank_Up.ContainsKey(priceKey))
                                        Worker_VolumesRank_Up.Add(priceKey, 1);
                                    else
                                        Worker_VolumesRank_Up[priceKey] += 1;

                                    if (!Worker_VolumesRank_Down.ContainsKey(priceKey))
                                        Worker_VolumesRank_Down.Add(priceKey, 1);
                                    else
                                        Worker_VolumesRank_Down[priceKey] += 1;

                                    double value = Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey];
                                    if (!Worker_VolumesRank_Subt.ContainsKey(priceKey))
                                        Worker_VolumesRank_Subt.Add(priceKey, value);
                                    else
                                        Worker_VolumesRank_Subt[priceKey] = value;
                                }

                                if (modeIsDelta) 
                                {
                                    if (!Worker_DeltaRank.ContainsKey(priceKey))
                                        Worker_DeltaRank.Add(priceKey, (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]));
                                    else
                                        Worker_DeltaRank[priceKey] += (Worker_VolumesRank_Up[priceKey] - Worker_VolumesRank_Down[priceKey]);
                                }
                            }

                            if (modeIsDelta && ResultParams.ShowMinMaxDelta)
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
            RangeObjs.rectangles.Add(rect);

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

            ResetFixedRange(rect.Name, end);

            for (int i = startIdx; i <= endIdx; i++)
                VolumeProfile(startIdx, i, ExtraProfiles.Fixed, fixedKey: rect.Name, fixedLowest: bottomY);
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
            FixedRank[fixedKey].ClearAllModes();

            int endIdx = Bars.OpenTimes.GetIndexByTime(end);
            int TF_idx = GetSegmentIndex(endIdx);
            
            for (int i = 0; i < segmentsDict[TF_idx].Count; i++)
            {
                switch (GeneralParams.VolumeMode_Input) {
                    case VolumeMode_Data.Normal:    
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Normal");
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Normal");
                        break;
                    case VolumeMode_Data.Buy_Sell:
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Sell");
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Buy");
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Sell");
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Buy");
                        break;
                    default:
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Delta");
                        Chart.RemoveObject($"{fixedKey}_{i}_VP_Fixed_Number_Delta");
                        break;
                }
                // HVN + LVN
                if (NodesParams.EnableNodeDetection) {
                    Chart.RemoveObject($"{fixedKey}_LVN_Low_{i}_Fixed");
                    Chart.RemoveObject($"{fixedKey}_LVN_{i}_Fixed");
                    Chart.RemoveObject($"{fixedKey}_LVN_High_{i}_Fixed");
                    Chart.RemoveObject($"{fixedKey}_LVN_Band_{i}_Fixed");

                    Chart.RemoveObject($"{fixedKey}_HVN_Low_{i}_Fixed");
                    Chart.RemoveObject($"{fixedKey}_HVN_{i}_Fixed");
                    Chart.RemoveObject($"{fixedKey}_HVN_High_{i}_Fixed");
                    Chart.RemoveObject($"{fixedKey}_HVN_Band_{i}_Fixed");
                }
            }

            string[] objsNames = GeneralParams.VolumeMode_Input switch
            {
                VolumeMode_Data.Normal => new string[1] {
                    $"{fixedKey}_VP_Fixed_Normal_Result",
                },
                VolumeMode_Data.Buy_Sell => new string[3] {
                    $"{fixedKey}_VP_Fixed_Sell_Sum",
                    $"{fixedKey}_VP_Fixed_Buy_Sum",
                    $"{fixedKey}_VP_Fixed_BuySell_Result",
                },
                _ => new string[6] {
                    $"{fixedKey}_VP_Fixed_Delta_BuySum",
                    $"{fixedKey}_VP_Fixed_Delta_SellSum",
                    $"{fixedKey}_VP_Fixed_Delta_Result",

                    $"{fixedKey}_VP_Fixed_Delta_MinResult",
                    $"{fixedKey}_VP_Fixed_Delta_MaxResult",
                    $"{fixedKey}_VP_Fixed_Delta_SubResult",
                },
            };

            foreach (string name in objsNames)
                Chart.RemoveObject(name);
        }

        public void ResetFixedRange_Dicts() {
            RangeObjs.rectangles.Clear();
            RangeObjs.infoObjects.Clear();
            RangeObjs.controlGrids.Clear();
        }

        // *********** SOME SHARED FUCTIONS ***********
        private void VP_Tick(int index, bool isVP = false, ExtraProfiles extraVP = ExtraProfiles.No, string fixedKey = "")
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];

            // For real-time market - ODF
            if (IsLastBar && !isVP && !BooleanUtils.isPriceBased_NewBar)
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
                ExtraProfiles.Monthly => !IsLastBar ? PerformanceTick.lastIdx_Monthly : PerformanceTick.startIdx_Monthly,
                ExtraProfiles.Weekly => !IsLastBar ? PerformanceTick.lastIdx_Weekly : PerformanceTick.startIdx_Weekly,
                ExtraProfiles.MiniVP => !IsLastBar ? PerformanceTick.lastIdx_Mini : PerformanceTick.startIdx_Mini,
                _ => !isVP ? PerformanceTick.lastIdx_Bars : (!IsLastBar ? PerformanceTick.lastIdx_MainVP : PerformanceTick.startIdx_MainVP)
            };

            // For real-time market - ODF
            if (IsLastBar && !isVP) {
                while (TicksOHLC.OpenTimes[startIndex] < startTime)
                    startIndex++;

                PerformanceTick.lastIdx_Bars = startIndex;
            }
            
            if (extraVP == ExtraProfiles.Fixed) {
                ChartRectangle rect = RangeObjs.rectangles.Where(x => x.Name == fixedKey).FirstOrDefault();
                DateTime start = rect.Time1 < rect.Time2 ? rect.Time1 : rect.Time2;
                DateTime normalizedStart = start.Date;
                
                // We should normalize this for O(1) operations
                startIndex = PerformanceTick.IndexesByDate.Any() ? PerformanceTick.IndexesByDate[normalizedStart] : 0;
            }
            
            int TF_idx = extraVP == ExtraProfiles.Fixed ? GetSegmentIndex(index) : index;
            List<double> whichSegment_VP = extraVP == ExtraProfiles.Fixed ? segmentsDict[TF_idx] : Segments_VP;

            // =======================
            bool modeIsBuySell = GeneralParams.VolumeMode_Input == VolumeMode_Data.Buy_Sell; 
            bool modeIsDelta = GeneralParams.VolumeMode_Input == VolumeMode_Data.Delta;            
            bool isNoDraw_MinMax = SpikeFilterParams.SpikeSource_Input == SpikeSource_Data.Sum_Delta || 
                BubblesChartParams.BubblesSource_Input switch {
                    BubblesSource_Data.Subtract_Delta =>  true,
                    BubblesSource_Data.Sum_Delta => true,
                    _ => false
                }; 
            
            double prevLoopTick = 0;
            for (int tickIndex = startIndex; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar;
                tickBar = TicksOHLC[tickIndex];
                
                // Fixed Range => Performance
                if (extraVP == ExtraProfiles.Fixed && startIndex == 0) {
                    // Just add the first tickIndex of current date.
                    DateTime normalizedDate = tickBar.OpenTime.Date;
                    if (!PerformanceTick.IndexesByDate.ContainsKey(normalizedDate))
                        PerformanceTick.IndexesByDate.Add(normalizedDate, tickIndex);
                }
                
                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime)
                {
                    if (tickBar.OpenTime > endTime) {
                        // ODF
                        PerformanceTick.lastIdx_Bars = !isVP ? tickIndex : PerformanceTick.lastIdx_Bars;
                        // VP
                        if (isVP) {
                            _ = extraVP switch
                            {
                                ExtraProfiles.Monthly => PerformanceTick.lastIdx_Monthly = tickIndex,
                                ExtraProfiles.Weekly => PerformanceTick.lastIdx_Weekly = tickIndex,
                                ExtraProfiles.MiniVP => PerformanceTick.lastIdx_Mini = tickIndex,
                                ExtraProfiles.Fixed => 0,
                                _ => PerformanceTick.lastIdx_MainVP = tickIndex
                            };
                        }
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
                            if (modeIsDelta && ResultParams.ShowMinMaxDelta)
                                prevDelta = VP_DeltaRank.Values.Sum();

                            if (VP_VolumesRank.ContainsKey(priceKey))
                            {
                                VP_VolumesRank[priceKey] += 1;

                                if (modeIsBuySell || modeIsDelta) 
                                {
                                    if (tickPrice > prevTick)
                                        VP_VolumesRank_Up[priceKey] += 1;
                                    else if (tickPrice < prevTick)
                                        VP_VolumesRank_Down[priceKey] += 1;
                                    else if (tickPrice == prevTick)
                                    {
                                        VP_VolumesRank_Up[priceKey] += 1;
                                        VP_VolumesRank_Down[priceKey] += 1;
                                    }
                                    
                                    VP_VolumesRank_Subt[priceKey] = VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey];
                                }

                                if (modeIsDelta)
                                    VP_DeltaRank[priceKey] += (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]);
                            }
                            else
                            {
                                VP_VolumesRank.Add(priceKey, 1);

                                if (modeIsBuySell || modeIsDelta) 
                                {
                                    if (!VP_VolumesRank_Up.ContainsKey(priceKey))
                                        VP_VolumesRank_Up.Add(priceKey, 1);
                                    else
                                        VP_VolumesRank_Up[priceKey] += 1;

                                    if (!VP_VolumesRank_Down.ContainsKey(priceKey))
                                        VP_VolumesRank_Down.Add(priceKey, 1);
                                    else
                                        VP_VolumesRank_Down[priceKey] += 1;

                                    double value = VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey];
                                    if (!VP_VolumesRank_Subt.ContainsKey(priceKey))
                                        VP_VolumesRank_Subt.Add(priceKey, value);
                                    else
                                        VP_VolumesRank_Subt[priceKey] = value;
                                }

                                if (modeIsDelta) 
                                {
                                    if (!VP_DeltaRank.ContainsKey(priceKey))
                                        VP_DeltaRank.Add(priceKey, (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]));
                                    else
                                        VP_DeltaRank[priceKey] += (VP_VolumesRank_Up[priceKey] - VP_VolumesRank_Down[priceKey]);
                                }
                            }

                            if (modeIsDelta && ResultParams.ShowMinMaxDelta)
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
                            if (modeIsDelta && (ResultParams.ShowMinMaxDelta || isNoDraw_MinMax))
                                prevDelta = DeltaRank.Values.Sum();                                

                            if (VolumesRank.ContainsKey(priceKey))
                            {
                                VolumesRank[priceKey] += 1;

                                if (modeIsBuySell || modeIsDelta) 
                                {
                                    if (tickPrice > prevTick)
                                        VolumesRank_Up[priceKey] += 1;
                                    else if (tickPrice < prevTick)
                                        VolumesRank_Down[priceKey] += 1;
                                    else if (tickPrice == prevTick)
                                    {
                                        VolumesRank_Up[priceKey] += 1;
                                        VolumesRank_Down[priceKey] += 1;
                                    }
                                }

                                if (modeIsDelta)
                                    DeltaRank[priceKey] += (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]);
                            }
                            else
                            {
                                VolumesRank.Add(priceKey, 1);

                                if (modeIsBuySell || modeIsDelta) 
                                {
                                    if (!VolumesRank_Up.ContainsKey(priceKey))
                                        VolumesRank_Up.Add(priceKey, 1);
                                    else
                                        VolumesRank_Up[priceKey] += 1;

                                    if (!VolumesRank_Down.ContainsKey(priceKey))
                                        VolumesRank_Down.Add(priceKey, 1);
                                    else
                                        VolumesRank_Down[priceKey] += 1;
                                }

                                if (modeIsDelta) {
                                    if (!DeltaRank.ContainsKey(priceKey))
                                        DeltaRank.Add(priceKey, (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]));
                                    else
                                        DeltaRank[priceKey] += (VolumesRank_Up[priceKey] - VolumesRank_Down[priceKey]);
                                }
                            }

                            if (modeIsDelta && (ResultParams.ShowMinMaxDelta || isNoDraw_MinMax))
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
                if (modeIsDelta && ResultParams.ShowMinMaxDelta)
                    prevDelta = volRank.Delta.Values.Sum();

                if (volRank.Normal.ContainsKey(priceKey))
                {
                    volRank.Normal[priceKey] += 1;
                    if (modeIsBuySell || modeIsDelta) 
                    {
                        if (tickPrice > prevTick)
                            volRank.Up[priceKey] += 1;
                        else if (tickPrice < prevTick)
                            volRank.Down[priceKey] += 1;
                        else if (tickPrice == prevTick)
                        {
                            volRank.Up[priceKey] += 1;
                            volRank.Down[priceKey] += 1;
                        }
                    }

                    if (modeIsDelta)
                        volRank.Delta[priceKey] += (volRank.Up[priceKey] - volRank.Down[priceKey]);
                }
                else
                {
                    volRank.Normal.Add(priceKey, 1);

                    if (modeIsBuySell || modeIsDelta) 
                    {
                        if (!volRank.Up.ContainsKey(priceKey))
                            volRank.Up.Add(priceKey, 1);
                        else
                            volRank.Up[priceKey] += 1;

                        if (!volRank.Down.ContainsKey(priceKey))
                            volRank.Down.Add(priceKey, 1);
                        else
                            volRank.Down[priceKey] += 1;
                    }
                    
                    if (modeIsDelta) 
                    {
                        if (!volRank.Delta.ContainsKey(priceKey))
                            volRank.Delta.Add(priceKey, (volRank.Up[priceKey] - volRank.Down[priceKey]));
                        else
                            volRank.Delta[priceKey] += (volRank.Up[priceKey] - volRank.Down[priceKey]);
                    }
                }

                if (modeIsDelta && ResultParams.ShowMinMaxDelta)
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

            if (IsLastBar && !BooleanUtils.isPriceBased_NewBar)
                endTime = TicksOHLC.LastBar.OpenTime;

            for (int tickIndex = PerformanceTick.lastIdx_Wicks; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime) {
                    if (tickBar.OpenTime > endTime) {
                        PerformanceTick.lastIdx_Wicks = tickIndex;
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
            if (Zoom < MiscParams.DrawAtZoom_Value) {
                HiddenOrRemove(true);
                return;
            }

            void HiddenOrRemove(bool hiddenAll)
            {
                if (DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest && hiddenAll)
                {
                    foreach (var kvp in PerfDrawingObjs.hiddenInfos)
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
                    foreach (var kvp in PerfDrawingObjs.redrawInfos.Values)
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
                foreach (var kvp in PerfDrawingObjs.hiddenInfos)
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
                foreach (var kvp in PerfDrawingObjs.redrawInfos)
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
                    if (!PerfDrawingObjs.redrawInfos.ContainsKey(i))
                        continue;

                    var drawInfoList = PerfDrawingObjs.redrawInfos[i].Values;
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
                    PerfDrawingObjs.staticText_DebugPerfDraw ??= Chart.DrawStaticText("Debug_Perf_Draw", "", VerticalAlignment.Top, HorizontalAlignment.Left, Color.Lime);
                    bool IsHidden = DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest;
                    int cached = 0;
                    if (!IsHidden) {
                        foreach (var list in PerfDrawingObjs.redrawInfos.Values) {
                            cached += list.Count;
                        }
                    }
                    PerfDrawingObjs.staticText_DebugPerfDraw.Text = IsHidden ?
                        $"Hidden Mode\n Total Objects: {FormatBigNumber(PerfDrawingObjs.hiddenInfos.Values.Count)}\n Visible: {FormatBigNumber(visible)}" :
                        $"Redraw Mode\n Cached: {FormatBigNumber(PerfDrawingObjs.redrawInfos.Count)} bars\n Cached: {FormatBigNumber(cached)} objects\n Drawn: {FormatBigNumber(visible)}";
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
                    rectangle.IsFilled = MiscParams.FillHist;
                    return rectangle;

                default:
                    return null;
            }
        }
        private void DrawOrCache(DrawInfo info) {
            if (DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest)
            {
                if (!IsLastBar || BooleanUtils.isPriceBased_NewBar) {
                    ChartObject obj = CreateDraw(info);
                    obj.IsHidden = true;
                    PerfDrawingObjs.hiddenInfos[info.Id] = obj;
                } else {
                    ChartObject obj = CreateDraw(info);
                    // Replace current obj
                    if (!PerfDrawingObjs.currentToHidden.ContainsKey(0))
                        PerfDrawingObjs.currentToHidden[0] = new Dictionary<string, ChartObject>();
                    else
                        PerfDrawingObjs.currentToHidden[0][info.Id] = obj;
                }
            }
            else
            {
                // Add Keys if not present
                if (!PerfDrawingObjs.redrawInfos.ContainsKey(info.BarIndex)) {
                    PerfDrawingObjs.redrawInfos[info.BarIndex] = new Dictionary<string, DrawInfo> { { info.Id, info } };
                }
                else {
                    // Add/Replace drawing
                    if (!IsLastBar || BooleanUtils.isPriceBased_NewBar)
                        PerfDrawingObjs.redrawInfos[info.BarIndex][info.Id] = info;
                    else {
                        // Create drawing and replace current infos
                        CreateDraw(info);
                        if (!PerfDrawingObjs.currentToRedraw.ContainsKey(0))
                            PerfDrawingObjs.currentToRedraw[0] = new Dictionary<string, DrawInfo>();
                        else
                            PerfDrawingObjs.currentToRedraw[0][info.Id] = info;
                    }
                }
            }
        }
        private void LiveDrawing(BarOpenedEventArgs obj) {
            // Working with Lists in Calculate() is painful.

            if (DrawingStrategy_Input == DrawingStrategy_Data.Hidden_Slowest) {
                List<ChartObject> objList = PerfDrawingObjs.currentToHidden[0].Values.ToList();

                foreach (var drawObj in objList)
                    PerfDrawingObjs.hiddenInfos[drawObj.Name] = drawObj;

                PerfDrawingObjs.currentToHidden.Clear();
            }
            else {
                List<DrawInfo> drawList = PerfDrawingObjs.currentToRedraw[0].Values.ToList();
                foreach (DrawInfo info in drawList) {
                    PerfDrawingObjs.redrawInfos[drawList.FirstOrDefault().BarIndex][info.Id] = info;
                }

                PerfDrawingObjs.currentToRedraw.Clear();
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
                if (DateTime.TryParseExact(StringDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out TickObjs.fromDateTime)) {
                    if (TickObjs.fromDateTime > lastBarDate) {
                        TickObjs.fromDateTime = lastBarDate;
                        Notifications.ShowPopup(
                            NOTIFY_CAPTION,
                            $"Invalid DateTime '{StringDate}'. \nUsing '{TickObjs.fromDateTime.ToShortDateString()}",
                            PopupNotificationState.Error
                        );
                    }
                } else {
                    TickObjs.fromDateTime = lastBarDate;
                    Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"Invalid DateTime '{StringDate}'. \nUsing '{TickObjs.fromDateTime.ToShortDateString()}",
                        PopupNotificationState.Error
                    );
                }
            }
            else {
                TickObjs.fromDateTime = LoadTickFrom_Input switch {
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
            TickObjs.firstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
            if (TickObjs.firstTickTime >= TickObjs.fromDateTime) {

                PopupNotification progressPopup = null;
                bool notifyIsMinimal = LoadTickNotify_Input == LoadTickNotify_Data.Minimal;
                if (notifyIsMinimal)
                    progressPopup = Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Loading Tick Data Synchronously...",
                        PopupNotificationState.InProgress
                    );

                while (TicksOHLC.OpenTimes.FirstOrDefault() > TickObjs.fromDateTime)
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
                ChartVerticalLine lineInfo = Chart.DrawVerticalLine("FromDate", TickObjs.fromDateTime, Color.Yellow);
                lineInfo.LineStyle = LineStyle.Lines;
                ChartText textInfo = Chart.DrawText("FromDateText", "Target Tick Data", TickObjs.fromDateTime, Bars.HighPrices[Bars.OpenTimes.GetIndexByTime(TickObjs.fromDateTime)], Color.Yellow);
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

            TickObjs.firstTickTime = TicksOHLC.OpenTimes.FirstOrDefault();
            if (TickObjs.firstTickTime > TickObjs.fromDateTime)
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
                    while (TicksOHLC.OpenTimes.FirstOrDefault() > TickObjs.fromDateTime)
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
                    if (IsLastBar && !TickObjs.startAsyncLoading)
                        timerHandler.isAsyncLoading = true;
                }
            }
            else
                unlockChart();


            void unlockChart() {
                if (TickObjs.syncProgressBar != null) {
                    TickObjs.syncProgressBar.IsIndeterminate = false;
                    TickObjs.syncProgressBar.IsVisible = false;
                }
                TickObjs.syncProgressBar = null;
                TickObjs.isLoadingComplete = true;
                DrawStartVolumeLine();
            }
        }

        protected override void OnTimer()
        {
            if (timerHandler.isAsyncLoading)
            {
                if (!TickObjs.startAsyncLoading) {
                    string volumeLineInfo = "=> Zoom out and follow the Vertical Line";
                    TickObjs.asyncPopup = Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Loading Tick Data Asynchronously every 0.5 second...\n{volumeLineInfo}",
                        PopupNotificationState.InProgress
                    );
                    // Draw target date.
                    DrawFromDateLine();
                }

                if (!TickObjs.isLoadingComplete) {
                    TicksOHLC.LoadMoreHistoryAsync((_) => {
                        DateTime currentDate = _.Bars.FirstOrDefault().OpenTime;

                        DrawStartVolumeLine();

                        if (currentDate <= TickObjs.fromDateTime) {

                            if (TickObjs.asyncPopup.State != PopupNotificationState.Success)
                                TickObjs.asyncPopup.Complete(PopupNotificationState.Success);

                            if (LoadTickNotify_Input == LoadTickNotify_Data.Detailed) {
                                Notifications.ShowPopup(
                                    NOTIFY_CAPTION,
                                    $"[{Symbol.Name}] Asynchronous Tick Data Collection Finished.",
                                    PopupNotificationState.Success
                                );
                            }

                            TickObjs.isLoadingComplete = true;
                        }
                    });

                    TickObjs.startAsyncLoading = true;
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


        // *********** HVN + LVN ***********
        private void DrawVolumeNodes(IDictionary<double, double> profileDict, int iStart, DateTime x1_Start, DateTime xBar, ExtraProfiles extraVP = ExtraProfiles.No, bool isIntraday = false, DateTime intraX1 = default, string fixedKey = "")
        {
            if (!NodesParams.EnableNodeDetection)
                return;

            string prefix = extraVP == ExtraProfiles.Fixed ? fixedKey : $"{iStart}";
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

            // Let's draw if ProfileNode_Data.Percentile
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

                        ChartTrendLine low = Chart.DrawTrendLine($"{prefix}_{nodeName}_Low_{idxLow}_{extraVP}", x1_Start, lowPrice, xBar, lowPrice, ColorBand_Lower);
                        ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{nodeName}_{idxCenter}_{extraVP}", x1_Start, centerPrice, xBar, centerPrice, _nodeColor);
                        ChartTrendLine high = Chart.DrawTrendLine($"{prefix}_{nodeName}_High_{idxHigh}_{extraVP}", x1_Start, highPrice, xBar, highPrice, ColorBand_Upper);
                        ChartRectangle rectBand = Chart.DrawRectangle($"{prefix}_{nodeName}_Band_{idxCenter}_{extraVP}", x1_Start,  lowPrice, xBar, highPrice, ColorBand);

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

                ChartTrendLine low = Chart.DrawTrendLine($"{prefix}_{node}_Low_{idxLow}_{extraVP}", x1_Start, lvlLow, xBar, lvlLow, ColorBand_Lower);
                ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{node}_{idxCenter}_{extraVP}", x1_Start, lvlCenter, xBar, lvlCenter, nodeColor);
                ChartTrendLine high = Chart.DrawTrendLine($"{prefix}_{node}_High_{idxHigh}_{extraVP}", x1_Start, lvlHigh, xBar, lvlHigh, ColorBand_Upper);
                ChartRectangle rectBand = Chart.DrawRectangle($"{prefix}_{node}_Band_{idxCenter}_{extraVP}", x1_Start, lvlLow, xBar, lvlHigh, ColorBand);

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

                DateTime extDate = extraVP == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
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

                if (isIntraday && extraVP != ExtraProfiles.MiniVP) {
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
                    ChartTrendLine center = Chart.DrawTrendLine($"{prefix}_{nodeRaw}_{idx}_{extraVP}", x1_Start, nodePrice, xBar, nodePrice, nodeColor_Raw);
                    center.LineStyle = nodeStyle_Raw; center.Thickness = nodeThick_Raw;

                    DateTime extDate = extraVP == ExtraProfiles.Fixed ? Bars[Bars.OpenTimes.GetIndexByTime(Server.Time)].OpenTime : extendDate();
                    if (NodesParams.extendNodes) {
                        if (!NodesParams.extendNodes_FromStart)
                            center.Time1 = xBar;
                        center.Time2 = extDate;
                    }

                    if (isIntraday && extraVP != ExtraProfiles.MiniVP)
                        center.Time1 = intraX1;
                }
            }
            void ClearOldNodes() {
                // 1º remove old price levels
                // 2º allow static-update of Params-Panel
                for (int i = 0; i < profilePrices.Length; i++)
                {
                    Chart.RemoveObject($"{prefix}_LVN_Low_{i}_{extraVP}");
                    Chart.RemoveObject($"{prefix}_LVN_{i}_{extraVP}");
                    Chart.RemoveObject($"{prefix}_LVN_High_{i}_{extraVP}");
                    Chart.RemoveObject($"{prefix}_LVN_Band_{i}_{extraVP}");

                    Chart.RemoveObject($"{prefix}_HVN_Low_{i}_{extraVP}");
                    Chart.RemoveObject($"{prefix}_HVN_{i}_{extraVP}");
                    Chart.RemoveObject($"{prefix}_HVN_High_{i}_{extraVP}");
                    Chart.RemoveObject($"{prefix}_HVN_Band_{i}_{extraVP}");
                }
            }
            DateTime extendDate() {
                string tfName = extraVP == ExtraProfiles.No ?
                (MiscParams.ODFInterval_Input == ODFInterval_Data.Daily ? "D1" :
                MiscParams.ODFInterval_Input == ODFInterval_Data.Weekly ? "W1" : "Month1" ) :
                extraVP == ExtraProfiles.MiniVP ? ProfileParams.MiniVPs_Timeframe.ShortName.ToString() :
                extraVP == ExtraProfiles.Weekly ?  "W1" :  "Month1";

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

        // **********************
        // The chart should already be clear, with no objects and bar colors.
        // Unless it's a static update.
        public void ClearAndRecalculate()
        {
            // The plot (sometimes in some options, like Volume View) is too fast, slow down a bit.
            Thread.Sleep(300);

            // Avoid it
            VerifyConflict();
            if (BooleanUtils.segmentsConflict)
                return;

            // LookBack from VP
            Bars ODF_Bars = MiscParams.ODFInterval_Input == ODFInterval_Data.Daily ? DailyBars : WeeklyBars;
            int firstIndex = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes.FirstOrDefault());

            // Get Index of ODF Interval to continue only in Lookback
            int iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            while (ODF_Bars.ClosePrices.Count - iVerify > GeneralParams.Lookback) {
                firstIndex++;
                iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            }

            // Daily or Weekly ODF
            int TF_idx = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes[TF_idx]);

            // Weekly Profile but Daily ODF
            bool extraWeekly = ProfileParams.EnableWeeklyProfile && MiscParams.ODFInterval_Input == ODFInterval_Data.Daily;
            if (extraWeekly) {
                TF_idx = WeeklyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                indexStart = Bars.OpenTimes.GetIndexByTime(WeeklyBars.OpenTimes[TF_idx]);
            }

            // Monthly Profile
            bool extraMonthly = ProfileParams.EnableMonthlyProfile;
            if (extraMonthly) {
                TF_idx = MonthlyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[firstIndex]);
                indexStart = Bars.OpenTimes.GetIndexByTime(MonthlyBars.OpenTimes[TF_idx]);
            }

            // Reset Tick Index.
            PerformanceTick.ResetAll();
            // Reset Drawings
            PerfDrawingObjs.ClearAll();
            // Reset last update
            ClearIdx.ResetAll();
            // Reset Segments
            // It's needed since TF_idx(start) changes if SegmentsInterval_Input is switched on the panel
            Segments_VP.Clear();
            segmentInfo.Clear(); 
            // Reset Fixed Range
            foreach (ChartRectangle rect in RangeObjs.rectangles)
            {
                DateTime end = rect.Time1 < rect.Time2 ? rect.Time2 : rect.Time1;
                ResetFixedRange(rect.Name, end);
            }

            // Historical data
            for (int index = indexStart; index < Bars.Count; index++)
            {
                CreateSegments(index);

                if (PanelSwitch_Input != PanelSwitch_Data.Order_Flow_Ticks) {
                    CreateMonthlyVP(index);
                    CreateWeeklyVP(index);
                }
                // Calculate ODF only in lookback
                if (extraWeekly || extraMonthly) {
                    iVerify = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    if (ODF_Bars.ClosePrices.Count - iVerify > GeneralParams.Lookback)
                        continue;
                }

                TF_idx = ODF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                indexStart = Bars.OpenTimes.GetIndexByTime(ODF_Bars.OpenTimes[TF_idx]);

                if (index == indexStart ||
                   (index - 1) == indexStart && isPriceBased_Chart ||
                   (index - 1) == indexStart && (index - 1) != ClearIdx.MainVP)
                    MassiveCleanUp(indexStart, index);

                if (PanelSwitch_Input != PanelSwitch_Data.Order_Flow_Ticks) {
                    if (ProfileParams.EnableMainVP)
                        VolumeProfile(indexStart, index);
                    CreateMiniVPs(index);
                }
            
                if (PanelSwitch_Input != PanelSwitch_Data.Volume_Profile) {
                    try { CreateOrderflow(index); } catch { }
                }

            }

            BooleanUtils.configHasChanged = true;

            DrawStartVolumeLine();
            try { PerformanceDrawing(true); } catch { } // Draw without scroll or zoom

            void CreateOrderflow(int i) {
                // Required for Ultra Bubbles Levels in Historical Data
                BooleanLocks.LevelsToFalse();
                VolumesRank.Clear();
                VolumesRank_Up.Clear();
                VolumesRank_Down.Clear();
                DeltaRank.Clear();
                int[] resetDelta = {0, 0};
                MinMaxDelta = resetDelta;
                OrderFlow(i);
            }
        }
        private void VerifyConflict() {
            // Timeframes Conflict
            if (ProfileParams.EnableWeeklyProfile && MiscParams.SegmentsInterval_Input == SegmentsInterval_Data.Daily) {
                DrawOnScreen("Misc >> Segments should be set to 'Weekly' or 'Monthly' \n to calculate Weekly Profile");
                BooleanUtils.segmentsConflict = true;
                return;
            }
            if (ProfileParams.EnableMonthlyProfile && MiscParams.SegmentsInterval_Input != SegmentsInterval_Data.Monthly) {
                DrawOnScreen("Misc >> Segments should be set to 'Monthly' \n to calculate Monthly Profile");
                BooleanUtils.segmentsConflict = true;
                return;
            }
            if (MiscParams.ODFInterval_Input == ODFInterval_Data.Weekly && MiscParams.SegmentsInterval_Input == SegmentsInterval_Data.Daily) {
                DrawOnScreen("Misc >> Segments should be set to 'Weekly' or 'Monthly' \n to calculate Order Flow weekly");
                BooleanUtils.segmentsConflict = true;
                return;
            }
            BooleanUtils.segmentsConflict = false;
        }

        public void SetRowHeight(double number) {
            rowHeight = number;
        }
        public void SetLookback(int number) {
            GeneralParams.Lookback = number;
        }
        public void SetMiniVPsBars() {
            MiniVPs_Bars = MarketData.GetBars(ProfileParams.MiniVPs_Timeframe);
        }
        public double GetRowHeight() {
            return rowHeight;
        }
        public double GetLookback() {
            return GeneralParams.Lookback;
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
            bool isPanel_VP() => Outside.PanelSwitch_Input != PanelSwitch_Data.Order_Flow_Ticks;
            bool isPanelOnly_VP() => Outside.PanelSwitch_Input == PanelSwitch_Data.Volume_Profile;
            bool isPanel_ODF() => Outside.PanelSwitch_Input != PanelSwitch_Data.Volume_Profile;
            
            bool isIntraday_VP() => Outside.ProfileParams.ShowIntradayProfile;
            bool isEnable_AnyVP() => Outside.ProfileParams.EnableMainVP || Outside.ProfileParams.EnableMiniProfiles ||
                                     Outside.ProfileParams.EnableWeeklyProfile || Outside.ProfileParams.EnableMonthlyProfile ||
                                     Outside.ProfileParams.EnableFixedRange;
            bool isDeltaMode() => Outside.GeneralParams.VolumeMode_Input == VolumeMode_Data.Delta;

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
            
            bool isSpikeFilter() => Outside.SpikeFilterParams.EnableSpikeFilter;
            bool isSpikePercentage() => Outside.SpikeRatioParams.SpikeRatio_Input == SpikeRatio_Data.Percentage;
            bool isSpikeFixed() => Outside.SpikeRatioParams.SpikeRatio_Input == SpikeRatio_Data.Fixed;
            bool isSpike_NoMAType() => !(Outside.SpikeFilterParams.SpikeFilter_Input == SpikeFilter_Data.L1Norm || 
                                      Outside.SpikeFilterParams.SpikeFilter_Input == SpikeFilter_Data.SoftMax_Power);
            
            bool isBubblesChart() => Outside.BubblesChartParams.EnableBubblesChart;
            bool isBubblesPercentile() => Outside.BubblesRatioParams.BubblesRatio_Input == BubblesRatio_Data.Percentile;
            bool isBubblesFixed() => Outside.BubblesRatioParams.BubblesRatio_Input == BubblesRatio_Data.Fixed;
            bool isBubblesChange() => Outside.BubblesChartParams.UseChangeSeries;
            bool isBubbles_NoMAType() => !(Outside.BubblesChartParams.BubblesFilter_Input == BubblesFilter_Data.L2Norm || 
                                      Outside.BubblesChartParams.BubblesFilter_Input == BubblesFilter_Data.SoftMax_Power ||
                                      Outside.BubblesChartParams.BubblesFilter_Input == BubblesFilter_Data.MinMax);
            
            bool isNot_NormalMode() => Outside.GeneralParams.VolumeMode_Input != VolumeMode_Data.Normal;
            bool IsNot_BubblesChart() => !Outside.BubblesChartParams.EnableBubblesChart;
            bool IsNot_SpikeChart() =>  !Outside.SpikeFilterParams.EnableSpikeChart;
            
            return new List<ParamDefinition>
            {
                new()
                {
                    Region = "General",
                    RegionOrder = 1,
                    Key = "DaysToShowKey",
                    Label = "Nº Days",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.GeneralParams.Lookback,
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
                    GetDefault = p => p.GeneralParams.VolumeView_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(VolumeView_Data)),
                    OnChanged = _ => UpdateVolumeView(),
                    IsVisible = () => isNot_NormalMode() && IsNot_BubblesChart() && isPanel_ODF()
                },

                new()
                {
                    Region = "Coloring",
                    RegionOrder = 2,
                    Key = "LargestDividedKey",
                    Label = "Largest?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.GeneralParams.ColoringOnlyLarguest,
                    OnChanged = _ => UpdateCheckbox("LargestDividedKey", val => Outside.GeneralParams.ColoringOnlyLarguest = val),
                    IsVisible = () => isNot_NormalMode() && Outside.GeneralParams.VolumeView_Input == VolumeView_Data.Divided && IsNot_BubblesChart() && isPanel_ODF()
                },

                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "EnableVPKey",
                    Label = "Main VP?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableMainVP,
                    OnChanged = _ => UpdateCheckbox("EnableVPKey", val => Outside.ProfileParams.EnableMainVP = val),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "UpdateVPKey",
                    Label = "Update At",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.UpdateProfile_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UpdateProfile_Data)),
                    OnChanged = _ => UpdateVP(),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "FillVPKey",
                    Label = "Fill Histogram?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.FillHist_VP,
                    OnChanged = _ => UpdateCheckbox("FillVPKey", val => Outside.ProfileParams.FillHist_VP = val),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "SideVPKey",
                    Label = "Side",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.HistogramSide_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(HistSide_Data)),
                    OnChanged = _ => UpdateSideVP(),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "WidthVPKey",
                    Label = "Width",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.HistogramWidth_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(HistWidth_Data)),
                    OnChanged = _ => UpdateWidthVP(),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "NumbersVPKey",
                    Label = "Historical Nºs?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.ShowHistoricalNumbers,
                    OnChanged = _ => UpdateCheckbox("NumbersVPKey", val => Outside.ProfileParams.ShowHistoricalNumbers = val),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "IntradayVPKey",
                    Label = "Intraday?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.ShowIntradayProfile,
                    OnChanged = _ => UpdateCheckbox("IntradayVPKey", val => Outside.ProfileParams.ShowIntradayProfile = val),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 5,
                    Key = "IntraOffsetKey",
                    Label = "Offset(bars)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.ProfileParams.OffsetBarsInput.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateIntradayOffset(),
                    IsVisible = () => isIntraday_VP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "IntraTFKey",
                    Label = "Offset(time)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.OffsetTimeframeInput.ShortName,
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateIntradayTimeframe(),
                    IsVisible = () => isIntraday_VP() && Outside.isPriceBased_Chart && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MiniVPsKey",
                    Label = "Mini-VPs?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableMiniProfiles,
                    OnChanged = _ => UpdateCheckbox("MiniVPsKey", val => Outside.ProfileParams.EnableMiniProfiles = val),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MiniTFKey",
                    Label = "Mini-Interval",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ProfileParams.MiniVPs_Timeframe.ShortName.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(Supported_Timeframes)),
                    OnChanged = _ => UpdateMiniVPTimeframe(),
                    IsVisible = () => Outside.ProfileParams.EnableMiniProfiles && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MiniResultKey",
                    Label = "Mini-Result?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.ShowMiniResults,
                    OnChanged = _ => UpdateCheckbox("MiniResultKey", val => Outside.ProfileParams.ShowMiniResults = val),
                    IsVisible = () => Outside.ProfileParams.EnableMiniProfiles && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "FixedRangeKey",
                    Label = "Fixed Range?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableFixedRange,
                    OnChanged = _ => UpdateCheckbox("FixedRangeKey", val => Outside.ProfileParams.EnableFixedRange = val),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP()
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "WeeklyVPKey",
                    Label = "Weekly VP?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableWeeklyProfile,
                    OnChanged = _ => UpdateCheckbox("WeeklyVPKey", val => Outside.ProfileParams.EnableWeeklyProfile = val),
                    IsVisible = () => Outside.GeneralParams.VolumeMode_Input != VolumeMode_Data.Buy_Sell && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "MonthlyVPKey",
                    Label = "Monthly VP?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.EnableMonthlyProfile,
                    OnChanged = _ => UpdateCheckbox("MonthlyVPKey", val => Outside.ProfileParams.EnableMonthlyProfile = val),
                    IsVisible = () => Outside.GeneralParams.VolumeMode_Input != VolumeMode_Data.Buy_Sell && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "IntraNumbersKey",
                    Label = "Intra-Nºs?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.ShowIntradayNumbers,
                    OnChanged = _ => UpdateCheckbox("IntraNumbersKey", val => Outside.ProfileParams.ShowIntradayNumbers = val),
                    IsVisible = () => isIntraday_VP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "Volume Profile",
                    RegionOrder = 2,
                    Key = "FillIntraVPKey",
                    Label = "Intra-Space?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ProfileParams.FillIntradaySpace,
                    OnChanged = _ => UpdateCheckbox("FillIntraVPKey", val => Outside.ProfileParams.FillIntradaySpace = val),
                    IsVisible = () => isIntraday_VP() && (Outside.ProfileParams.EnableWeeklyProfile || Outside.ProfileParams.EnableMonthlyProfile) && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },


                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "EnableNodeKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.EnableNodeDetection,
                    OnChanged = _ => UpdateCheckbox("EnableNodeKey", val => Outside.NodesParams.EnableNodeDetection = val),
                    IsVisible = () => isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "NodeSmoothKey",
                    Label = "Smooth",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.NodesParams.ProfileSmooth_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ProfileSmooth_Data)),
                    OnChanged = _ => UpdateNodeSmooth(),
                    IsVisible = () => isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "NodeTypeKey",
                    Label = "Nodes",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.NodesParams.ProfileNode_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ProfileNode_Data)),
                    OnChanged = _ => UpdateNodeType(),
                    IsVisible = () => isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "ShowNodeKey",
                    Label = "Show",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.NodesParams.ShowNode_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ShowNode_Data)),
                    OnChanged = _ => UpdateShowNode(),
                    IsVisible = () => isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "HvnBandPctKey",
                    Label = "HVN Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.bandHVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Band(),
                    IsVisible = () => isNodeBand() && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "LvnBandPctKey",
                    Label = "LVN Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.bandLVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Band(),
                    IsVisible = () => isNodeBand() && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "NodeStrongKey",
                    Label = "Only Strong?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.onlyStrongNodes,
                    OnChanged = _ => UpdateCheckbox("NodeStrongKey", val => Outside.NodesParams.onlyStrongNodes = val),
                    IsVisible = () => isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                // 'Strong HVN' for HVN_Raw(only) on [LocalMinMax, Topology]
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "StrongHvnPctKey",
                    Label = "(%) >= POC",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.strongHVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Strong(),
                    IsVisible = () => Outside.NodesParams.onlyStrongNodes && isStrongHVN() && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                // 'Strong LVN' should be used by HVN_With_Bands, since the POCs are derived from LVN Split.
                // on [LocalMinMax, Topology]
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "StrongLvnPctKey",
                    Label = "(%) <= POC",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.strongLVN_Pct.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Strong(),
                    IsVisible = () => Outside.NodesParams.onlyStrongNodes && isStrongLVN() && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "ExtendNodeKey",
                    Label = "Extend?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.extendNodes,
                    OnChanged = _ => UpdateCheckbox("ExtendNodeKey", val => Outside.NodesParams.extendNodes = val),
                    IsVisible = () => isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "ExtNodesCountKey",
                    Label = "Extend(count)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.extendNodes_Count.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateExtendNodesCount(),
                    IsVisible = () => Outside.NodesParams.extendNodes && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "ExtBandsKey",
                    Label = "Ext.(bands)?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.extendNodes_WithBands,
                    OnChanged = _ => UpdateCheckbox("ExtBandsKey", val => Outside.NodesParams.extendNodes_WithBands = val),
                    IsVisible = () => Outside.NodesParams.extendNodes && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "HvnPctileKey",
                    Label = "HVN(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.pctileHVN_Value.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHVN_Pctile(),
                    IsVisible = () => Outside.NodesParams.ProfileNode_Input == ProfileNode_Data.Percentile && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "LvnPctileKey",
                    Label = "LVN(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.NodesParams.pctileLVN_Value.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLVN_Pctile(),
                    IsVisible = () => Outside.NodesParams.ProfileNode_Input == ProfileNode_Data.Percentile && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },
                new()
                {
                    Region = "HVN + LVN",
                    RegionOrder = 3,
                    Key = "ExtNodeStartKey",
                    Label = "From start?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.NodesParams.extendNodes_FromStart,
                    OnChanged = _ => UpdateCheckbox("ExtNodeStartKey", val => Outside.NodesParams.extendNodes_FromStart = val),
                    IsVisible = () => Outside.NodesParams.extendNodes && isEnable_AnyVP() && (IsNot_BubblesChart() && isPanel_VP() || isPanelOnly_VP())
                },


                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "EnableSpikeKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.SpikeFilterParams.EnableSpikeFilter,
                    OnChanged = _ => UpdateCheckbox("EnableSpikeKey", val => Outside.SpikeFilterParams.EnableSpikeFilter = val),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeSourceKey",
                    Label = "Source",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilterParams.SpikeSource_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeSource_Data)),
                    OnChanged = _ => UpdateSpikeSource(),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeViewKey",
                    Label = "View",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilterParams.SpikeView_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeView_Data)),
                    OnChanged = _ => UpdateSpikeView(),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeFilterKey",
                    Label = "Filter",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilterParams.SpikeFilter_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeFilter_Data)),
                    OnChanged = _ => UpdateSpikeFilter(),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikePeriodKey",
                    Label = "Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeFilterParams.MAperiod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSpikeMAPeriod(),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeMATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.CustomMAType.Spike.ToString() : p.SpikeFilterParams.MAtype.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateSpikeMAType(),
                    IsVisible = () => isDeltaMode() && isSpike_NoMAType() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "EnableNotifyKey",
                    Label = "Notify?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.SpikeFilterParams.EnableSpikeNotification,
                    OnChanged = _ => UpdateCheckbox("EnableNotifyKey", val => Outside.SpikeFilterParams.EnableSpikeNotification = val),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeTypeKey",
                    Label = "Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilterParams.NotificationType_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(NotificationType_Data)),
                    OnChanged = _ => UpdateSpikeNotifyType(),
                    IsVisible = () => isDeltaMode() && Outside.SpikeFilterParams.EnableSpikeNotification && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeSoundKey",
                    Label = "Sound",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilterParams.Spike_SoundType.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SoundType)),
                    OnChanged = _ => UpdateSpikeSound(),
                    IsVisible = () => isDeltaMode() && Outside.SpikeFilterParams.EnableSpikeNotification && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeLevelsKey",
                    Label = "Levels?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.SpikeLevelParams.ShowSpikeLevels,
                    OnChanged = _ => UpdateCheckbox("SpikeLevelsKey", val => Outside.SpikeLevelParams.ShowSpikeLevels = val),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeLvsTouchKey",
                    Label = "Max Touch",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeLevelParams.MaxCount.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSpikeLevels_MaxCount(),
                    IsVisible = () => isDeltaMode() && Outside.SpikeLevelParams.ShowSpikeLevels && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeLvsColorKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeLevelParams.SpikeLevelsColoring_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeLevelsColoring_Data)),
                    OnChanged = _ => UpdateSpikeLevels_Coloring(),
                    IsVisible = () => isDeltaMode() && Outside.SpikeLevelParams.ShowSpikeLevels && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeChartKey",
                    Label = "Chart?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.SpikeFilterParams.EnableSpikeChart,
                    OnChanged = _ => UpdateCheckbox("SpikeChartKey", val => Outside.SpikeFilterParams.EnableSpikeChart = val),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeColorKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilterParams.SpikeChartColoring_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeChartColoring_Data)),
                    OnChanged = _ => UpdateSpikeChart_Coloring(),
                    IsVisible = () => isDeltaMode() && Outside.SpikeFilterParams.EnableSpikeChart && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "SpikeLvsResetKey",
                    Label = "Reset Daily?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.SpikeLevelParams.ResetDaily,
                    OnChanged = _ => UpdateCheckbox("SpikeLvsResetKey", val => Outside.SpikeLevelParams.ResetDaily = val),
                    IsVisible = () => isDeltaMode() && Outside.SpikeLevelParams.ShowSpikeLevels && IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Tick Spike",
                    RegionOrder = 4,
                    Key = "IconViewKey",
                    Label = "Icon",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeFilterParams.IconView_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ChartIconType)),
                    OnChanged = _ => UpdateIconView(),
                    IsVisible = () => isDeltaMode() && Outside.SpikeFilterParams.SpikeView_Input == SpikeView_Data.Icon && IsNot_BubblesChart() && isPanel_ODF()
                },

                // Ratio
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "SpikeRatioKey",
                    Label = "Ratio",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.SpikeRatioParams.SpikeRatio_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SpikeRatio_Data)),
                    OnChanged = _ => UpdateSpikeRatio(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isPanel_ODF()
                },
                // Percentage => Period + MA type
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "PctPeriodKey",
                    Label = "Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.MAperiod_PctSpike.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdatePctPeriod_Spike(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikePercentage() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "PctMATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.CustomMAType.SpikePctRatio.ToString() : p.SpikeRatioParams.MAtype_PctSpike.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateMAType_PctSpike(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikePercentage() && isPanel_ODF()
                },
                // Percentage
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "LowestPctKey",
                    Label = "Lowest(<)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Lowest_PctValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowest_Pct(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikePercentage() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "LowPctKey",
                    Label = "Low",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Low_PctValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLow_Pct(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikePercentage() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "AveragePctKey",
                    Label = "Average",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Average_PctValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateAverage_Pct(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikePercentage() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "HighPctKey",
                    Label = "High",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.High_PctValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHigh_Pct(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikePercentage() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "UltraPctKey",
                    Label = "Ultra(>=)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Ultra_PctValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateUltra_Pct(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikePercentage() && isPanel_ODF()
                },
                // [Debug] Show Strength
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "DebugSpikeKey",
                    Label = "Debug?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.SpikeRatioParams.ShowStrengthValue,
                    OnChanged = _ => UpdateCheckbox("DebugSpikeKey", val => Outside.SpikeRatioParams.ShowStrengthValue = val),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isPanel_ODF()
                },
                // Fixed
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "LowestFixedSpikeKey",
                    Label = "Lowest(<)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Lowest_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowestFixed_Spike(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikeFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "LowFixedSpikeKey",
                    Label = "Low",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Low_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowFixed_Spike(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikeFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "AverageFixedSpikeKey",
                    Label = "Average",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Average_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateAverageFixed_Spike(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikeFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "HighFixedSpikeKey",
                    Label = "High",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.High_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHighFixed_Spike(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikeFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Spike(ratio)",
                    RegionOrder = 5,
                    Key = "UltraFixedSpikeKey",
                    Label = "Ultra(>=)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.SpikeRatioParams.Ultra_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateUltraFixed_Spike(),
                    IsVisible = () => isDeltaMode() && isSpikeFilter() && IsNot_BubblesChart() && isSpikeFixed() && isPanel_ODF()
                },


                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "EnableBubblesKey",
                    Label = "Enable?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.BubblesChartParams.EnableBubblesChart,
                    OnChanged = _ => UpdateCheckbox("EnableBubblesKey", val => Outside.BubblesChartParams.EnableBubblesChart = val),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubblesSizeKey",
                    Label = "Size Multiplier",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesChartParams.BubblesSizeMultiplier.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateBubblesSize(),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubblesSourceKey",
                    Label = "Source",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesChartParams.BubblesSource_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesSource_Data)),
                    OnChanged = _ => UpdateBubblesSource(),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubblesChangeKey",
                    Label = "Change?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.BubblesChartParams.UseChangeSeries,
                    OnChanged = _ => UpdateCheckbox("BubblesChangeKey", val => Outside.BubblesChartParams.UseChangeSeries = val),
                    IsVisible = () => isDeltaMode() &&IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "ChangePeriodKey",
                    Label = "Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesChartParams.changePeriod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateChangePeriod(),
                    IsVisible = () => isDeltaMode() && isBubblesChange() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "ChangeOperatorKey",
                    Label = "Operator",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesChartParams.ChangeOperator_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ChangeOperator_Data)),
                    OnChanged = _ => UpdateChangeOperator(),
                    IsVisible = () => isDeltaMode() && isBubblesChange() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubblesFilterKey",
                    Label = "Filter",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesChartParams.BubblesFilter_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesFilter_Data)),
                    OnChanged = _ => UpdateBubblesFilter(),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubbMAPeriodKey",
                    Label = "MA Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesChartParams.MAperiod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateBubblesMAPeriod(),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubbMATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.CustomMAType.Bubbles.ToString() : p.BubblesChartParams.MAtype.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateBubblesMAType(),
                    IsVisible = () => isDeltaMode() && isBubbles_NoMAType() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubblesColoringKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesChartParams.BubblesColoring_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesColoring_Data)),
                    OnChanged = _ => UpdateBubblesColoring(),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "BubblesMomentumKey",
                    Label = "Strategy",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesChartParams.BubblesMomentum_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesMomentum_Data)),
                    OnChanged = _ => UpdateBubblesMomentum(),
                    IsVisible = () => isDeltaMode() && Outside.BubblesChartParams.BubblesColoring_Input == BubblesColoring_Data.Momentum && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraNotifyKey",
                    Label = "Ultra Notify?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.BubblesLevelParams.EnableUltraNotification,
                    OnChanged = _ => UpdateCheckbox("UltraNotifyKey", val => Outside.BubblesLevelParams.EnableUltraNotification = val),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraTypeKey",
                    Label = "Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesLevelParams.NotificationType_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(NotificationType_Data)),
                    OnChanged = _ => UpdateUltraNotifyType(),
                    IsVisible = () => isDeltaMode() && Outside.BubblesLevelParams.EnableUltraNotification && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraSoundKey",
                    Label = "Sound",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesLevelParams.Ultra_SoundType.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SoundType)),
                    OnChanged = _ => UpdateUltraSound(),
                    IsVisible = () => isDeltaMode() && Outside.BubblesLevelParams.EnableUltraNotification && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraLevelskey",
                    Label = "Ultra Levels?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.BubblesLevelParams.ShowUltraLevels,
                    OnChanged = _ => UpdateCheckbox("UltraLevelskey", val => Outside.BubblesLevelParams.ShowUltraLevels = val),
                    IsVisible = () => isDeltaMode() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraCountKey",
                    Label = "Max Touch",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesLevelParams.MaxCount,
                    OnChanged = _ => UpdateUltraLevels_MaxCount(),
                    IsVisible = () => isDeltaMode() && Outside.BubblesLevelParams.ShowUltraLevels && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraBreakKey",
                    Label = "Touch from",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesLevelParams.UltraBubblesBreak_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UltraBubblesBreak_Data)),
                    OnChanged = _ => UpdateUltraBreakStrategy(),
                    IsVisible = () => isDeltaMode() && Outside.BubblesLevelParams.ShowUltraLevels && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraResetKey",
                    Label = "Reset Daily?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.BubblesLevelParams.ResetDaily,
                    OnChanged = _ => UpdateCheckbox("UltraResetKey", val => Outside.BubblesLevelParams.ResetDaily = val),
                    IsVisible = () => isDeltaMode() && Outside.BubblesLevelParams.ShowUltraLevels && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraRectSizeKey",
                    Label = "Level Size",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesLevelParams.UltraBubbles_RectSizeInput.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UltraBubbles_RectSizeData)),
                    OnChanged = _ => UpdateUltraRectangleSize(),
                    IsVisible = () => isDeltaMode() && Outside.BubblesLevelParams.ShowUltraLevels && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles Chart",
                    RegionOrder = 6,
                    Key = "UltraColoringKey",
                    Label = "Coloring",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesLevelParams.UltraBubblesColoring_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(UltraBubblesColoring_Data)),
                    OnChanged = _ => UpdateUltraColoring(),
                    IsVisible = () => isDeltaMode() && Outside.BubblesLevelParams.ShowUltraLevels && IsNot_SpikeChart() && isPanel_ODF()
                },


                // Ratio
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "BubblesRatioKey",
                    Label = "Ratio",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.BubblesRatioParams.BubblesRatio_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BubblesRatio_Data)),
                    OnChanged = _ => UpdateBubblesRatio(),
                    IsVisible = () => isBubblesChart() && isPanel_ODF()
                },
                // Percentile => Period
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "PctilePeriodKey",
                    Label = "Pctile Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.PctilePeriod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdatePctilePeriod_Bubbles(),
                    IsVisible = () => isBubblesChart() && isBubblesPercentile() && isPanel_ODF()
                },
                // [Debug] Show Strength
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "DebugBubblesKey",
                    Label = "Debug?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.BubblesRatioParams.ShowStrengthValue,
                    OnChanged = _ => UpdateCheckbox("DebugBubblesKey", val => Outside.BubblesRatioParams.ShowStrengthValue = val),
                    IsVisible = () => isBubblesChart() && isPanel_ODF()
                },
                // Percentile
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "LowestPctileKey",
                    Label = "Lowest(<)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Lowest_PctileValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowest_Pctile(),
                    IsVisible = () => isBubblesChart() && isBubblesPercentile() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "LowPctileKey",
                    Label = "Low",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Low_PctileValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLow_Pctile(),
                    IsVisible = () => isBubblesChart() && isBubblesPercentile() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "AveragePctileKey",
                    Label = "Average",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Average_PctileValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateAverage_Pctile(),
                    IsVisible = () => isBubblesChart() && isBubblesPercentile() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "HighPctileKey",
                    Label = "High",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.High_PctileValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHigh_Pctile(),
                    IsVisible = () => isBubblesChart() && isBubblesPercentile() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "UltraPctileKey",
                    Label = "Ultra(>=)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Ultra_PctileValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateUltra_Pctile(),
                    IsVisible = () => isBubblesChart() && isBubblesPercentile() && isPanel_ODF()
                },
                // Fixed
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "LowestFixedBubblesKey",
                    Label = "Lowest(<)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Lowest_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowestFixed_Bubbles(),
                    IsVisible = () => isBubblesChart() && isBubblesFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "LowFixedBubblesKey",
                    Label = "Low",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Low_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLowFixed_Bubbles(),
                    IsVisible = () => isBubblesChart() && isBubblesFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "AverageFixedBubblesKey",
                    Label = "Average",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Average_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateAverageFixed_Bubbles(),
                    IsVisible = () => isBubblesChart() && isBubblesFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "HighFixedBubblesKey",
                    Label = "High",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.High_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateHighFixed_Bubbles(),
                    IsVisible = () => isBubblesChart() && isBubblesFixed() && isPanel_ODF()
                },
                new()
                {
                    Region = "Bubbles(ratio)",
                    RegionOrder = 7,
                    Key = "UltraFixedBubblesKey",
                    Label = "Ultra(>=)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BubblesRatioParams.Ultra_FixedValue.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateUltraFixed_Bubbles(),
                    IsVisible = () => isBubblesChart() && isBubblesFixed() && isPanel_ODF()
                }, 


                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "ShowResultsKey",
                    Label = "Show?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ResultParams.ShowResults,
                    OnChanged = _ => UpdateCheckbox("ShowResultsKey", val => Outside.ResultParams.ShowResults = val),
                    IsVisible = () => IsNot_BubblesChart() && IsNot_SpikeChart()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "EnableLargeKey",
                    Label = "Enable Filter?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ResultParams.EnableLargeFilter,
                    OnChanged = _ => UpdateCheckbox("EnableLargeKey", val => Outside.ResultParams.EnableLargeFilter = val),
                    IsVisible = () => IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "ShowMinMaxKey",
                    Label = "Min/Max?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ResultParams.ShowMinMaxDelta,
                    OnChanged = _ => UpdateCheckbox("ShowMinMaxKey", val => Outside.ResultParams.ShowMinMaxDelta = val),
                    IsVisible = () => isDeltaMode() && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "LargeMATypeKey",
                    Label = "MA Type",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => Outside.UseCustomMAs ? Outside.CustomMAType.Large.ToString() : p.ResultParams.MAtype.ToString(),
                    EnumOptions = () => Outside.UseCustomMAs ? Enum.GetNames(typeof(MAType_Data)) : Enum.GetNames(typeof(MovingAverageType)),
                    OnChanged = _ => UpdateLargeMAType(),
                    IsVisible = () => Outside.ResultParams.EnableLargeFilter && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "LargePeriodKey",
                    Label = "MA Period",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.ResultParams.MAperiod.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLargeMAPeriod(),
                    IsVisible = () => Outside.ResultParams.EnableLargeFilter && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "LargeRatioKey",
                    Label = "Ratio",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.ResultParams.LargeRatio.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateLargeRatio(),
                    IsVisible = () => Outside.ResultParams.EnableLargeFilter && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "ShowSideTotalKey",
                    Label = "Side(total)?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ResultParams.ShowSideTotal,
                    OnChanged = _ => UpdateCheckbox("ShowSideTotalKey", val => Outside.ResultParams.ShowSideTotal = val),
                    IsVisible = () => isNot_NormalMode() && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "ResultViewKey",
                    Label = "Side(view)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ResultParams.ResultsView_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ResultsView_Data)),
                    OnChanged = _ => UpdateResultView(),
                    IsVisible = () => isNot_NormalMode() && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "OnlySubtKey",
                    Label = "Only Subtract?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.ResultParams.ShowOnlySubtDelta,
                    OnChanged = _ => UpdateCheckbox("OnlySubtKey", val => Outside.ResultParams.ShowOnlySubtDelta = val),
                    IsVisible = () => isDeltaMode() && Outside.ResultParams.ShowMinMaxDelta && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Results",
                    RegionOrder = 8,
                    Key = "OperatorKey",
                    Label = "Operator",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.ResultParams.OperatorBuySell_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(OperatorBuySell_Data)),
                    OnChanged = _ => UpdateOperator(),
                    IsVisible = () => Outside.GeneralParams.VolumeMode_Input == VolumeMode_Data.Buy_Sell && IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },

                new()
                {
                    Region = "Misc",
                    RegionOrder = 9,
                    Key = "ShowHistKey",
                    Label = "Histogram?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.MiscParams.ShowHist,
                    OnChanged = _ => UpdateCheckbox("ShowHistKey", val => Outside.MiscParams.ShowHist = val),
                    IsVisible = () => IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 9,
                    Key = "FillHistKey",
                    Label = "Fill Hist?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.MiscParams.FillHist,
                    OnChanged = _ => UpdateCheckbox("FillHistKey", val => Outside.MiscParams.FillHist = val),
                    IsVisible = () => IsNot_BubblesChart() && IsNot_SpikeChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 9,
                    Key = "ShowNumbersKey",
                    Label = "Numbers?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.MiscParams.ShowNumbers,
                    OnChanged = _ => UpdateCheckbox("ShowNumbersKey", val => Outside.MiscParams.ShowNumbers = val),
                    IsVisible = () => IsNot_BubblesChart() && isPanel_ODF()
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 9,
                    Key = "DrawAtKey",
                    Label = "Draw at Zoom",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.MiscParams.DrawAtZoom_Value.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateDrawAtZoom()
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 9,
                    Key = "SegmentsKey",
                    Label = "Segments",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.MiscParams.SegmentsInterval_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(SegmentsInterval_Data)),
                    OnChanged = _ => UpdateSegmentsInterval(),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 9,
                    Key = "ODFIntervalKey",
                    Label = "ODF + VP",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.MiscParams.ODFInterval_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(ODFInterval_Data)),
                    OnChanged = _ => UpdateODFInterval(),
                },
                new()
                {
                    Region = "Misc",
                    RegionOrder = 9,
                    Key = "BubbleValueKey",
                    Label = "Bubbles-V?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.MiscParams.ShowBubbleValue,
                    OnChanged = _ => UpdateCheckbox("BubbleValueKey", val => Outside.MiscParams.ShowBubbleValue = val),
                    IsVisible = () => isBubblesChart() && isPanel_ODF()
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
            grid.AddChild(CreateModeInfo_Button(FirstParams.GeneralParams.VolumeMode_Input.ToString()), 0, 1, 1, 3);
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
                var groupGrid = new Grid(9, 5);
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
                        Outside.MiscParams.ShowHist = (bool)_originalValues["ShowHistKey"];
                        Outside.MiscParams.ShowNumbers = (bool)_originalValues["ShowNumbersKey"];
                        Outside.ResultParams.ShowResults = (bool)_originalValues["ShowResultsKey"];
                        Outside.SpikeFilterParams.EnableSpikeFilter = (bool)_originalValues["EnableSpikeKey"];
                        Outside.ProfileParams.EnableMainVP = (bool)_originalValues["EnableVPKey"];
                        Outside.ProfileParams.EnableMiniProfiles = (bool)_originalValues["MiniVPsKey"];
                        Outside.Chart.ChartType = ChartType.Hlc;
                    }
                    break;
                case "SpikeChartKey":
                    if (value)
                        Outside.Chart.ChartType = ChartType.Hlc;
                    else if (!value && _originalValues.ContainsKey("ShowHistKey")) {
                        // ContainsKey avoids crash when loading
                        Outside.SpikeFilterParams.EnableSpikeFilter = (bool)_originalValues["EnableSpikeKey"];
                        Outside.MiscParams.ShowHist = (bool)_originalValues["ShowHistKey"];
                        Outside.ResultParams.ShowResults = (bool)_originalValues["ShowResultsKey"];
                        Outside.ResultParams.ShowMinMaxDelta = (bool)_originalValues["ShowMinMaxKey"];
                        Outside.Chart.ChartType = ChartType.Hlc;
                    }
                    break;
                case "IntradayVPKey":
                    RecalculateOutsideWithMsg(Outside.ProfileParams.ShowIntradayNumbers);
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
            if (Enum.TryParse(selected, out VolumeView_Data viewType) && viewType != Outside.GeneralParams.VolumeView_Input)
            {
                Outside.GeneralParams.VolumeView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Volume Profile ====
        private void UpdateVP()
        {
            var selected = comboBoxMap["UpdateVPKey"].SelectedItem;
            if (Enum.TryParse(selected, out UpdateProfile_Data updateType) && updateType != Outside.ProfileParams.UpdateProfile_Input)
            {
                Outside.ProfileParams.UpdateProfile_Input = updateType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSideVP()
        {
            var selected = comboBoxMap["SideVPKey"].SelectedItem;
            if (Enum.TryParse(selected, out HistSide_Data sideType) && sideType != Outside.ProfileParams.HistogramSide_Input)
            {
                Outside.ProfileParams.HistogramSide_Input = sideType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateWidthVP()
        {
            var selected = comboBoxMap["WidthVPKey"].SelectedItem;
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
        private void UpdateMiniVPTimeframe()
        {
            var selected = comboBoxMap["MiniTFKey"].SelectedItem;
            TimeFrame value = StringToTimeframe(selected);
            if (value != TimeFrame.Minute && value != Outside.ProfileParams.MiniVPs_Timeframe)
            {
                Outside.ProfileParams.MiniVPs_Timeframe = value;
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

        // ==== Spike Filter ====
        private void UpdateSpikeSource()
        {
            var selected = comboBoxMap["SpikeSourceKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeSource_Data sourceType) && sourceType != Outside.SpikeFilterParams.SpikeSource_Input)
            {
                Outside.SpikeFilterParams.SpikeSource_Input = sourceType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateSpikeView()
        {
            var selected = comboBoxMap["SpikeViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeView_Data viewType) && viewType != Outside.SpikeFilterParams.SpikeView_Input)
            {
                Outside.SpikeFilterParams.SpikeView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateIconView()
        {
            var selected = comboBoxMap["IconViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out ChartIconType viewType) && viewType != Outside.SpikeFilterParams.IconView_Input)
            {
                Outside.SpikeFilterParams.IconView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSpikeFilter()
        {
            var selected = comboBoxMap["SpikeFilterKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeFilter_Data filterType) && filterType != Outside.SpikeFilterParams.SpikeFilter_Input)
            {
                Outside.SpikeFilterParams.SpikeFilter_Input = filterType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateSpikeMAType()
        {
            var selected = comboBoxMap["SpikeMATypeKey"].SelectedItem;
            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.CustomMAType.Spike)
                {
                    Outside.CustomMAType.Spike = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.SpikeFilterParams.MAtype)
                {
                    Outside.SpikeFilterParams.MAtype = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }
        private void UpdateSpikeMAPeriod()
        {
            if (int.TryParse(textInputMap["SpikePeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.SpikeFilterParams.MAperiod)
                {
                    Outside.SpikeFilterParams.MAperiod = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateSpikeNotifyType()
        {
            var selected = comboBoxMap["SpikeTypeKey"].SelectedItem;
            if (Enum.TryParse(selected, out NotificationType_Data notifyType) && notifyType != Outside.SpikeFilterParams.NotificationType_Input)
            {
                Outside.SpikeFilterParams.NotificationType_Input = notifyType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSpikeSound()
        {
            var selected = comboBoxMap["SpikeSoundKey"].SelectedItem;
            if (Enum.TryParse(selected, out SoundType soundType) && soundType != Outside.SpikeFilterParams.Spike_SoundType)
            {
                Outside.SpikeFilterParams.Spike_SoundType = soundType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSpikeLevels_MaxCount()
        {
            if (int.TryParse(textInputMap["SpikeLvsTouchKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.SpikeLevelParams.MaxCount)
                {
                    Outside.SpikeLevelParams.MaxCount = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateSpikeLevels_Coloring()
        {
            var selected = comboBoxMap["SpikeLvsColorKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeLevelsColoring_Data coloringType) && coloringType != Outside.SpikeLevelParams.SpikeLevelsColoring_Input)
            {
                Outside.SpikeLevelParams.SpikeLevelsColoring_Input = coloringType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateSpikeChart_Coloring()
        {
            var selected = comboBoxMap["SpikeColorKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeChartColoring_Data coloringType) && coloringType != Outside.SpikeFilterParams.SpikeChartColoring_Input)
            {
                Outside.SpikeFilterParams.SpikeChartColoring_Input = coloringType;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Spike(ratio) ====

        private void UpdateSpikeRatio()
        {
            var selected = comboBoxMap["SpikeRatioKey"].SelectedItem;
            if (Enum.TryParse(selected, out SpikeRatio_Data ratioType) && ratioType != Outside.SpikeRatioParams.SpikeRatio_Input)
            {
                Outside.SpikeRatioParams.SpikeRatio_Input = ratioType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdatePctPeriod_Spike() {
            if (int.TryParse(textInputMap["PctPeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.SpikeRatioParams.MAperiod_PctSpike)
                {
                    Outside.SpikeRatioParams.MAperiod_PctSpike = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateMAType_PctSpike()
        {
            var selected = comboBoxMap["PctMATypeKey"].SelectedItem;
            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.CustomMAType.SpikePctRatio)
                {
                    Outside.CustomMAType.SpikePctRatio = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.SpikeRatioParams.MAtype_PctSpike)
                {
                    Outside.SpikeRatioParams.MAtype_PctSpike = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }
        // Percentage
        private void UpdateLowest_Pct()
        {
            if (double.TryParse(textInputMap["LowestPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Lowest_PctValue)
                {
                    Outside.SpikeRatioParams.Lowest_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateLow_Pct()
        {
            if (double.TryParse(textInputMap["LowPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Low_PctValue)
                {
                    Outside.SpikeRatioParams.Low_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateAverage_Pct()
        {
            if (double.TryParse(textInputMap["AveragePctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Average_PctValue)
                {
                    Outside.SpikeRatioParams.Average_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateHigh_Pct()
        {
            if (double.TryParse(textInputMap["HighPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.High_PctValue)
                {
                    Outside.SpikeRatioParams.High_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateUltra_Pct()
        {
            if (double.TryParse(textInputMap["UltraPctKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Ultra_PctValue)
                {
                    Outside.SpikeRatioParams.Ultra_PctValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        // Fixed
        private void UpdateLowestFixed_Spike()
        {
            if (double.TryParse(textInputMap["LowestFixedSpikeKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Lowest_FixedValue)
                {
                    Outside.SpikeRatioParams.Lowest_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateLowFixed_Spike()
        {
            if (double.TryParse(textInputMap["LowFixedSpikeKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Low_FixedValue)
                {
                    Outside.SpikeRatioParams.Low_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateAverageFixed_Spike()
        {
            if (double.TryParse(textInputMap["AverageFixedSpikeKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Average_FixedValue)
                {
                    Outside.SpikeRatioParams.Average_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateHighFixed_Spike()
        {
            if (double.TryParse(textInputMap["HighFixedSpikeKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.High_FixedValue)
                {
                    Outside.SpikeRatioParams.High_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateUltraFixed_Spike()
        {
            if (double.TryParse(textInputMap["UltraFixedSpikeKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.SpikeRatioParams.Ultra_FixedValue)
                {
                    Outside.SpikeRatioParams.Ultra_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }


        // ==== Bubbles Chart ====
        private void UpdateBubblesSize()
        {
            int value = int.TryParse(textInputMap["BubblesSizeKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.BubblesChartParams.BubblesSizeMultiplier)
            {
                Outside.BubblesChartParams.BubblesSizeMultiplier = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBubblesSource()
        {
            var selected = comboBoxMap["BubblesSourceKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesSource_Data sourceType) && sourceType != Outside.BubblesChartParams.BubblesSource_Input)
            {
                Outside.BubblesChartParams.BubblesSource_Input = sourceType;
                RecalculateOutsideWithMsg(Outside.BubblesLevelParams.ShowUltraLevels);
            }
        }
        private void UpdateChangePeriod()
        {
            int value = int.TryParse(textInputMap["ChangePeriodKey"].Text, out var n) ? n : -1;
            if (value > 0 && value != Outside.BubblesChartParams.changePeriod)
            {
                Outside.BubblesChartParams.changePeriod = value;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateChangeOperator()
        {
            var selected = comboBoxMap["ChangeOperatorKey"].SelectedItem;
            if (Enum.TryParse(selected, out ChangeOperator_Data operatorType) && operatorType != Outside.BubblesChartParams.ChangeOperator_Input)
            {
                Outside.BubblesChartParams.ChangeOperator_Input = operatorType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBubblesFilter()
        {
            var selected = comboBoxMap["BubblesFilterKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesFilter_Data filterType) && filterType != Outside.BubblesChartParams.BubblesFilter_Input)
            {
                Outside.BubblesChartParams.BubblesFilter_Input = filterType;
                RecalculateOutsideWithMsg(Outside.BubblesLevelParams.ShowUltraLevels);
            }
        }
        private void UpdateBubblesMAType() {
            var selected = comboBoxMap["BubbMATypeKey"].SelectedItem;

            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.CustomMAType.Bubbles)
                {
                    Outside.CustomMAType.Bubbles = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.BubblesChartParams.MAtype)
                {
                    Outside.BubblesChartParams.MAtype = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }

        private void UpdateBubblesMAPeriod() {
            if (int.TryParse(textInputMap["BubbMAPeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.BubblesChartParams.MAperiod)
                {
                    Outside.BubblesChartParams.MAperiod = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateBubblesColoring()
        {
            var selected = comboBoxMap["BubblesColoringKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesColoring_Data coloringType) && coloringType != Outside.BubblesChartParams.BubblesColoring_Input)
            {
                Outside.BubblesChartParams.BubblesColoring_Input = coloringType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateBubblesMomentum()
        {
            var selected = comboBoxMap["BubblesMomentumKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesMomentum_Data strategyType) && strategyType != Outside.BubblesChartParams.BubblesMomentum_Input)
            {
                Outside.BubblesChartParams.BubblesMomentum_Input = strategyType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraNotifyType()
        {
            var selected = comboBoxMap["UltraTypeKey"].SelectedItem;
            if (Enum.TryParse(selected, out NotificationType_Data notifyType) && notifyType != Outside.BubblesLevelParams.NotificationType_Input)
            {
                Outside.BubblesLevelParams.NotificationType_Input = notifyType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraSound()
        {
            var selected = comboBoxMap["UltraSoundKey"].SelectedItem;
            if (Enum.TryParse(selected, out SoundType soundType) && soundType != Outside.BubblesLevelParams.Ultra_SoundType)
            {
                Outside.SpikeFilterParams.Spike_SoundType = soundType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraLevels_MaxCount()
        {
            if (int.TryParse(textInputMap["UltraCountKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.BubblesLevelParams.MaxCount)
                {
                    Outside.BubblesLevelParams.MaxCount = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateUltraBreakStrategy()
        {
            var selected = comboBoxMap["UltraBreakKey"].SelectedItem;
            if (Enum.TryParse(selected, out UltraBubblesBreak_Data breakType) && breakType != Outside.BubblesLevelParams.UltraBubblesBreak_Input)
            {
                Outside.BubblesLevelParams.UltraBubblesBreak_Input = breakType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraRectangleSize()
        {
            var selected = comboBoxMap["UltraRectSizeKey"].SelectedItem;
            if (Enum.TryParse(selected, out UltraBubbles_RectSizeData rectSizeType) && rectSizeType != Outside.BubblesLevelParams.UltraBubbles_RectSizeInput)
            {
                Outside.BubblesLevelParams.UltraBubbles_RectSizeInput = rectSizeType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateUltraColoring()
        {
            var selected = comboBoxMap["UltraColoringKey"].SelectedItem;
            if (Enum.TryParse(selected, out UltraBubblesColoring_Data coloringType) && coloringType != Outside.BubblesLevelParams.UltraBubblesColoring_Input)
            {
                Outside.BubblesLevelParams.UltraBubblesColoring_Input = coloringType;
                RecalculateOutsideWithMsg(false);
            }
        }

        // ==== Bubbles(ratio)

        private void UpdateBubblesRatio()
        {
            var selected = comboBoxMap["BubblesRatioKey"].SelectedItem;
            if (Enum.TryParse(selected, out BubblesRatio_Data ratioType) && ratioType != Outside.BubblesRatioParams.BubblesRatio_Input)
            {
                Outside.BubblesRatioParams.BubblesRatio_Input = ratioType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdatePctilePeriod_Bubbles() {
            if (int.TryParse(textInputMap["PctilePeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.BubblesRatioParams.PctilePeriod)
                {
                    Outside.BubblesRatioParams.PctilePeriod = value;
                    SetApplyVisibility();
                }
            }
        }
        // Percentile
        private void UpdateLowest_Pctile()
        {
            if (int.TryParse(textInputMap["LowestPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Lowest_PctileValue)
                {
                    Outside.BubblesRatioParams.Lowest_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateLow_Pctile()
        {
            if (int.TryParse(textInputMap["LowPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Low_PctileValue)
                {
                    Outside.BubblesRatioParams.Low_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateAverage_Pctile()
        {
            if (int.TryParse(textInputMap["AveragePctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Average_PctileValue)
                {
                    Outside.BubblesRatioParams.Average_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateHigh_Pctile()
        {
            if (int.TryParse(textInputMap["HighPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.High_PctileValue)
                {
                    Outside.BubblesRatioParams.High_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateUltra_Pctile()
        {
            if (int.TryParse(textInputMap["UltraPctileKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Ultra_PctileValue)
                {
                    Outside.BubblesRatioParams.Ultra_PctileValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        // Fixed
        private void UpdateLowestFixed_Bubbles()
        {
            if (double.TryParse(textInputMap["LowestFixedBubblesKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Lowest_FixedValue)
                {
                    Outside.BubblesRatioParams.Lowest_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateLowFixed_Bubbles()
        {
            if (double.TryParse(textInputMap["LowFixedBubblesKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Low_FixedValue)
                {
                    Outside.BubblesRatioParams.Low_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateAverageFixed_Bubbles()
        {
            if (double.TryParse(textInputMap["AverageFixedBubblesKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Average_FixedValue)
                {
                    Outside.BubblesRatioParams.Average_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateHighFixed_Bubbles()
        {
            if (double.TryParse(textInputMap["HighFixedBubblesKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.High_FixedValue)
                {
                    Outside.BubblesRatioParams.High_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateUltraFixed_Bubbles()
        {
            if (double.TryParse(textInputMap["UltraFixedBubblesKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BubblesRatioParams.Ultra_FixedValue)
                {
                    Outside.BubblesRatioParams.Ultra_FixedValue = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }

        // ==== Results ====
        private void UpdateResultView()
        {
            var selected = comboBoxMap["ResultViewKey"].SelectedItem;
            if (Enum.TryParse(selected, out ResultsView_Data viewType) && viewType != Outside.ResultParams.ResultsView_Input)
            {
                Outside.ResultParams.ResultsView_Input = viewType;
                RecalculateOutsideWithMsg(false);
            }
        }
        private void UpdateLargeMAType()
        {
            var selected = comboBoxMap["LargeMATypeKey"].SelectedItem;

            if (Outside.UseCustomMAs) {
                if (Enum.TryParse(selected, out MAType_Data MAType) && MAType != Outside.CustomMAType.Large)
                {
                    Outside.CustomMAType.Large = MAType;
                    RecalculateOutsideWithMsg();
                }
            } else {
                if (Enum.TryParse(selected, out MovingAverageType MAType) && MAType != Outside.ResultParams.MAtype)
                {
                    Outside.ResultParams.MAtype = MAType;
                    RecalculateOutsideWithMsg();
                }
            }
        }
        private void UpdateLargeMAPeriod()
        {
            if (int.TryParse(textInputMap["LargePeriodKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.ResultParams.MAperiod)
                {
                    Outside.ResultParams.MAperiod = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateLargeRatio()
        {
            if (double.TryParse(textInputMap["LargeRatioKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.ResultParams.LargeRatio)
                {
                    Outside.ResultParams.LargeRatio = value;
                    SetApplyVisibility();
                }
            }
        }
        private void UpdateOperator()
        {
            var selected = comboBoxMap["OperatorKey"].SelectedItem;
            if (Enum.TryParse(selected, out OperatorBuySell_Data op) && op != Outside.ResultParams.OperatorBuySell_Input)
            {
                Outside.ResultParams.OperatorBuySell_Input = op;
                RecalculateOutsideWithMsg();
            }
        }

        // ==== Misc ====
        private void UpdateDrawAtZoom()
        {
            if (int.TryParse(textInputMap["DrawAtKey"].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                if (value != Outside.MiscParams.DrawAtZoom_Value)
                {
                    Outside.MiscParams.DrawAtZoom_Value = value;
                }
            }
        }
        private void UpdateSegmentsInterval()
        {
            var selected = comboBoxMap["SegmentsKey"].SelectedItem;
            if (Enum.TryParse(selected, out SegmentsInterval_Data segmentsType) && segmentsType != Outside.MiscParams.SegmentsInterval_Input)
            {
                Outside.MiscParams.SegmentsInterval_Input = segmentsType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateODFInterval()
        {
            var selected = comboBoxMap["ODFIntervalKey"].SelectedItem;
            if (Enum.TryParse(selected, out ODFInterval_Data intervalType) && intervalType != Outside.MiscParams.ODFInterval_Input)
            {
                Outside.MiscParams.ODFInterval_Input = intervalType;
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

            Outside.GeneralParams.VolumeMode_Input = Outside.GeneralParams.VolumeMode_Input switch
            {
                VolumeMode_Data.Normal => VolumeMode_Data.Buy_Sell,
                VolumeMode_Data.Buy_Sell => VolumeMode_Data.Delta,
                _ => VolumeMode_Data.Normal
            };
            ModeBtn.Text = Outside.GeneralParams.VolumeMode_Input.ToString();
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

            Outside.GeneralParams.VolumeMode_Input = Outside.GeneralParams.VolumeMode_Input switch
            {
                VolumeMode_Data.Delta => VolumeMode_Data.Buy_Sell,
                VolumeMode_Data.Buy_Sell => VolumeMode_Data.Normal,
                _ => VolumeMode_Data.Delta
            };
            ModeBtn.Text = Outside.GeneralParams.VolumeMode_Input.ToString();
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
            storageModel.Params["PanelMode"] = Outside.GeneralParams.VolumeMode_Input;

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
            Outside.GeneralParams.VolumeMode_Input = volMode;
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


    public static class CustomMA
    {
        //  ===== CUSTOM MAS ====
        // MAs logic generated by LLM
        // Modified to handle multiples sources
        // as well as specific OrderFlow() needs.
        public static double StdDev(int index, int Period, double maValue, Dictionary<int, double> buffer)
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

        public static double SMA(int index, int period, Dictionary<int, double> buffer)
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
        public static double EMA(int index, int period, Dictionary<int, double> buffer, Dictionary<int, double> emaDict)
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
        public static double WMA(int index, int period, Dictionary<int, double> buffer, double? overrideLast = null)
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
        public static double TMA(int index, int period, Dictionary<int, double> buffer)
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
        public static double Hull(int index, int period, Dictionary<int, double> buffer)
        {
            if (period < 2) return buffer[index];

            int half = Math.Max(1, period / 2);
            int sqrt = Math.Max(1, (int)Math.Round(Math.Sqrt(period)));

            double wmaHalf = WMA(index, half, buffer);
            double wmaFull = WMA(index, period, buffer);

            double raw = 2 * wmaHalf - wmaFull;
            return WMA(index, sqrt, buffer, raw);
        }
        public static double Wilder(int index, int period, Dictionary<int, double> buffer, Dictionary<int, double> wilderDict)
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
        public static double KAMA(int index, int period, int fast, int slow, Dictionary<int, double> buffer, Dictionary<int, double> kamaDict)
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
        public static double VIDYA(int index, int period, Dictionary<int, double> buffer, Dictionary<int, double> vidyaDict)
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
        public static double CMO(int index, int length, Dictionary<int, double> buffer)
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
    }

    public static class Filters
    {
        // logic generated/converted by LLM
        public static double RollingPercentile(double[] window)
        {
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
        public static double PowerSoftmax_Strength(double[] window, double alpha = 1.0)
        {
            if (window == null || window.Length == 0)
                return 0.0;

            double sum = 0.0;
            double lastP = 0.0;

            for (int i = 0; i < window.Length; i++)
            {
                double w = Math.Max(window[i], 1e-12);
                double p = Math.Pow(w, alpha);

                sum += p;

                if (i == window.Length - 1)
                    lastP = p;
            }

            return sum != 0.0 ? lastP / sum : 0.0;
        }

        public static double[] PowerSoftmax_Profile(double[] window, double alpha = 1.0)
        {
            int n = window.Length;
            double[] result = new double[n];

            if (n == 0)
                return result;

            // First pass: compute powered values
            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double w = Math.Max(window[i], 1e-12);
                double p = Math.Pow(w, alpha);

                result[i] = p;
                sum += p;
            }

            // Second pass: normalize
            if (sum != 0.0)
            {
                for (int i = 0; i < n; i++)
                    result[i] /= sum;
            }

            return result;
        }


        public static double L1Norm_Strength(double[] window)
        {
            if (window == null || window.Length == 0)
                return 0.0;

            double denom = 0.0;
            for (int i = 0; i < window.Length; i++)
                denom += Math.Abs(window[i]);

            return denom != 0.0
                ? window[window.Length - 1] / denom
                : 1.0;
        }
        public static double[] L1Norm_Profile(double[] window)
        {
            int n = window.Length;
            double[] result = new double[n];

            if (n == 0)
                return result;

            double denom = 0.0;
            for (int i = 0; i < n; i++)
                denom += Math.Abs(window[i]);

            for (int i = 0; i < n; i++) {
                if (denom != 0.0)
                    result[i] = window[i] / denom;
                else
                    result[i] = 1.0;
            }

            return result;
        }

        public static double L2Norm_Strength(double[] window)
        {
            if (window == null || window.Length == 0)
                return 0.0;

            double sumSq = 0.0;

            for (int i = 0; i < window.Length; i++)
                sumSq += window[i] * window[i];

            double denom = Math.Sqrt(sumSq);

            return denom != 0.0
                ? window[window.Length - 1] / denom
                : 0.0;
        }

        public static double MinMax_Strength(double[] window)
        {
            if (window == null || window.Length == 0)
                return 0.0;

            double min = double.MaxValue;
            double max = double.MinValue;

            for (int i = 0; i < window.Length; i++)
            {
                double v = window[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            double range = max - min;
            if (range <= 0.0)
                return 0.0;

            return (window[window.Length - 1] - min) / range;
        }

    }

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