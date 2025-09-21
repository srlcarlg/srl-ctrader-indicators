/*
--------------------------------------------------------------------------------------------------------------------------------
                      Volume for Renko/Range
                           revision 1
VolumeRenkoRange applies tick volume logic on Price-Based charts.

Uses Tick Data to make the calculation of volume, just like Candles.
It's possible because we have the [Open, Close]Time of the Bar, so:
Volume logic = Number of price updates (ticks) that come during the formation of a bar (between of OpenTime and CloseTime).

What's new in rev.1?
-Includes all "Order Flow Aggregated" related improvements
    - High-performance VolumeTick()
    - Asynchronous Tick Data Collection
    - Heatmap/Fading Coloring

AUTHOR: srlcarlg
----------------------------------------------------------------------------------------------------------------------------
*/
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System;
using System.Linq;
using System.Globalization;

namespace cAlgo
{
    [Indicator(AutoRescale = true, ScalePrecision = 0, AccessRights = AccessRights.None)]
    public class VolumeRenkoRange : Indicator
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

        public enum VolumeColoring_Data
        {
            None,
            Up_Down,
            Heatmap,
            Fading,
        }
        [Parameter("Coloring:", DefaultValue = VolumeColoring_Data.Heatmap, Group = "==== Volume Filter ====")]
        public VolumeColoring_Data VolumeColoring_Input { get; set; }
        public enum VolumeFilter_Data
        {
            MA,
            Standard_Deviation,
            Both
        }
        [Parameter("Filter Heatmap:", DefaultValue = VolumeFilter_Data.MA, Group = "==== Volume Filter ====")]
        public VolumeFilter_Data VolumeFilter_Input { get; set; }

        [Parameter("MA Type:", DefaultValue = MovingAverageType.Triangular, Group = "==== Volume Filter ====")]
        public MovingAverageType MAtype { get; set; }

        [Parameter("MA Period:", DefaultValue = 20, MinValue = 1, Group = "==== Volume Filter ====")]
        public int MAperiod { get; set; }

        [Parameter("Coloring Bars?:", DefaultValue = true, Group = "==== Volume Filter ====")]
        public bool ColoringBars { get; set; }


        [Parameter("[Debug] Show Strength Value?:", DefaultValue = false, Group = "==== HeatMap Coloring ====")]
        public bool ShowStrengthValue { get; set; }
        [Parameter("Lowest < Max Threshold:", DefaultValue = 0.5, MinValue = 0.01, Step = 0.01, Group = "==== HeatMap Coloring ====")]
        public double HeatmapLowest_Value { get; set; }
        [Parameter("Lowest Color[Bar]:", DefaultValue = "Aqua", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapLowest_Color { get; set; }

        [Parameter("Low:", DefaultValue = 1.2, MinValue = 0.01, Step = 0.01, Group = "==== HeatMap Coloring ====")]
        public double HeatmapLow_Value { get; set; }
        [Parameter("Low Color[Bar]:", DefaultValue = "White", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapLow_Color { get; set; }

        [Parameter("Average:", DefaultValue = 2.5, MinValue = 0.01, Step = 0.01, Group = "==== HeatMap Coloring ====")]
        public double HeatmapAverage_Value { get; set; }
        [Parameter("Average Color[Bar]:", DefaultValue = "Yellow", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapAverage_Color { get; set; }

        [Parameter("High:", DefaultValue = 3.5, MinValue = 0.01, Step = 0.01, Group = "==== HeatMap Coloring ====")]
        public double HeatmapHigh_Value { get; set; }
        [Parameter("High Color[Bar]:", DefaultValue = "DarkOrange", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapHigh_Color { get; set; }

        [Parameter("Ultra >= Max Threshold:", DefaultValue = 3.51, MinValue = 0.01, Step = 0.01, Group = "==== HeatMap Coloring ====")]
        public double HeatmapUltra_Value { get; set; }
        [Parameter("Ultra Color[Bar]:", DefaultValue = "Red", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapUltra_Color { get; set; }


        [Parameter("Fading Up[Bar]:", DefaultValue = "#B200BFFF", Group = "==== Fading Coloring ====")]
        public Color FadingUp_Color { get; set; }
        [Parameter("Fading Down[Bar]:", DefaultValue = "#B2DC143C", Group = "==== Fading Coloring ====")]
        public Color FadingDown_Color { get; set; }


        [Output("VolumeRR", LineColor = "LightBlue", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries VolumeSeries { get; set; }
        [Output("VolumeRR_MA", LineColor = "Orange")]
        public IndicatorDataSeries MovingAverageLine { get; set; }
        [Output("VolumeRR_StdDev", LineColor = "Blue")]
        public IndicatorDataSeries StdDevLine { get; set; }

        [Output("VolumeRR_Up", LineColor = "Green", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries UpVolume { get; set; }
        [Output("VolumeRR_Down", LineColor = "Red", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries DownVolume { get; set; }

        [Output("VolumeRR_Ultra", LineColor = "Red", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries HeatmapUltraVolume { get; set; }
        [Output("VolumeRR_High", LineColor = "DarkOrange", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries HeatmapHighVolume { get; set; }
        [Output("VolumeRR_Average", LineColor = "Yellow", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries HeatmapAverageVolume { get; set; }
        [Output("VolumeRR_Low", LineColor = "White", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries HeatmapLowVolume { get; set; }
        [Output("VolumeRR_Lowest", LineColor = "Aqua", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries HeatmapLowestVolume { get; set; }

        [Output("VolumeRR_FadingUp", LineColor = "#B200BFFF", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries FadingUpVolume { get; set; }
        [Output("VolumeRR_FadingDown", LineColor = "#B2DC143C", PlotType = PlotType.Histogram, Thickness = 4)]
        public IndicatorDataSeries FadingDownVolume { get; set; }

        // Volume RR
        private MovingAverage maVol;
        private StandardDeviation stdDev;

        private enum HeatmapSwitch {
            Lowest,
            Low,
            Average,
            High,
            Ultra
        }
        // Tick Volume
        public readonly string NOTIFY_CAPTION = "Volume Renko/Range";
        private DateTime firstTickTime;
        private DateTime fromDateTime;
        private Bars TicksOHLC;
        private int lastTick_Bars = 0;
        private ProgressBar syncTickProgressBar = null;
        PopupNotification asyncTickPopup = null;
        private bool loadingAsyncTicks = false;
        private bool loadingTicksComplete = false;

        // Timer
        private class TimerHandler {
            public bool isAsyncLoading = false;
        }
        private readonly TimerHandler timerHandler = new();

        protected override void Initialize()
        {
            // First Ticks Data
            TicksOHLC = MarketData.GetBars(TimeFrame.Tick);

            maVol = Indicators.MovingAverage(VolumeSeries, MAperiod, MAtype);
            stdDev = Indicators.StandardDeviation(VolumeSeries, MAperiod, MAtype);

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
                }
                VolumeInitialize(true);
            }
            else
                VolumeInitialize();

            Timer.Start(TimeSpan.FromSeconds(0.5));

            DrawOnScreen("Loading Ticks Data... \n or \n Calculating...");
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
            if (!IsLastBar)
                DrawOnScreen("");

            VolumeSeries[index] = VolumeTick(index);
            MovingAverageLine[index] = maVol.Result[index];
            StdDevLine[index] = stdDev.Result[index];

            bool isUp = Bars.ClosePrices[index] > Bars.OpenPrices[index];
            double volume = VolumeSeries[index];
            if (VolumeColoring_Input == VolumeColoring_Data.Up_Down) {
                if (isUp) {
                    UpVolume[index] = volume;
                    DownVolume[index] = double.NaN;
                }
                else {
                    DownVolume[index] = volume;
                    UpVolume[index] = double.NaN;
                }
            }
            if (VolumeColoring_Input == VolumeColoring_Data.Heatmap) {
                double filterValue = VolumeFilter_Input == VolumeFilter_Data.MA ?
                                     maVol.Result[index] : stdDev.Result[index];
                double volumeStrength = volume / filterValue;

                if (VolumeFilter_Input == VolumeFilter_Data.Both)
                    volumeStrength = (volume - maVol.Result[index]) / stdDev.Result[index];

                volumeStrength = Math.Round(Math.Abs(volumeStrength), 2);
                if (ShowStrengthValue) {
                    double y1 = isUp ? Bars[index].High : Bars[index].Low;
                    ChartText text = Chart.DrawText($"strength_{index}", $"{volumeStrength}", Bars[index].OpenTime, y1, HeatmapLow_Color);
                    text.HorizontalAlignment = HorizontalAlignment.Center;
                    text.VerticalAlignment = isUp ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                }

                _ = volumeStrength < HeatmapLowest_Value ? heatmapSeries(HeatmapSwitch.Lowest) :
                        volumeStrength < HeatmapLow_Value ? heatmapSeries(HeatmapSwitch.Low) :
                        volumeStrength < HeatmapAverage_Value ? heatmapSeries(HeatmapSwitch.Average) :
                        volumeStrength < HeatmapHigh_Value ? heatmapSeries(HeatmapSwitch.High) :
                        volumeStrength >= HeatmapUltra_Value ? heatmapSeries(HeatmapSwitch.Ultra) : heatmapSeries(HeatmapSwitch.Ultra);

                if (ColoringBars) {
                    _ = volumeStrength < HeatmapLowest_Value ? heatmapColor(HeatmapLowest_Color) :
                         volumeStrength < HeatmapLow_Value ? heatmapColor(HeatmapLow_Color) :
                         volumeStrength < HeatmapAverage_Value ? heatmapColor(HeatmapAverage_Color) :
                         volumeStrength < HeatmapHigh_Value ? heatmapColor(HeatmapHigh_Color) :
                         volumeStrength >= HeatmapUltra_Value ? heatmapColor(HeatmapUltra_Color) : heatmapColor(HeatmapUltra_Color);
                }
                bool heatmapSeries(HeatmapSwitch heatSwitch) {
                    HeatmapLowestVolume[index] = heatSwitch == HeatmapSwitch.Lowest ? volume : double.NaN;
                    HeatmapLowVolume[index] = heatSwitch == HeatmapSwitch.Low ? volume : double.NaN;
                    HeatmapAverageVolume[index] = heatSwitch == HeatmapSwitch.Average ? volume : double.NaN;
                    HeatmapHighVolume[index] = heatSwitch == HeatmapSwitch.High ? volume : double.NaN;
                    HeatmapUltraVolume[index] = heatSwitch == HeatmapSwitch.Ultra ? volume : double.NaN;
                    return true;
                }
                bool heatmapColor(Color color) {
                    Chart.SetBarColor(index, color);
                    return true;
                }

            }
            if (VolumeColoring_Input == VolumeColoring_Data.Fading) {
                if (volume > VolumeSeries[index - 1]) {
                    FadingUpVolume[index] = volume;
                    FadingDownVolume[index] = double.NaN;
                    Chart.SetBarColor(index, FadingUp_Color);
                }
                if (volume < VolumeSeries[index - 1]) {
                    FadingDownVolume[index] = volume;
                    FadingUpVolume[index] = double.NaN;
                    Chart.SetBarColor(index, FadingDown_Color);
                }
                if (volume == VolumeSeries[index - 1]) {
                    FadingDownVolume[index] = double.NaN;
                    FadingUpVolume[index] = double.NaN;
                    Chart.SetBarColor(index, HeatmapLow_Color);
                }
            }
        }

        // ========= Functions Area ==========
        private int VolumeTick(int index)
        {
            DateTime startTime = Bars.OpenTimes[index];
            DateTime endTime = Bars.OpenTimes[index + 1];

            // For real-time market
            if (IsLastBar)
                endTime = TicksOHLC.LastBar.OpenTime;

            int startIndex = lastTick_Bars;

            if (IsLastBar) {
                while (TicksOHLC.OpenTimes[startIndex] < startTime)
                    startIndex++;

                lastTick_Bars = startIndex;
            }
            int volume = 0;

            for (int tickIndex = startIndex; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime) {
                    if (tickBar.OpenTime > endTime) { lastTick_Bars = tickIndex; break; }
                    else continue;
                }

                volume += 1;
            }

            return volume;
        }
        private void DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
        }

        private void Recalculate() {
            int startIndex = Bars.OpenTimes.GetIndexByTime(TicksOHLC.OpenTimes.FirstOrDefault());
            for (int index = startIndex; index < Bars.Count; index++)
            {
                VolumeSeries[index] = VolumeTick(index);
                MovingAverageLine[index] = maVol.Result[index];
                StdDevLine[index] = stdDev.Result[index];

                bool isUp = Bars.ClosePrices[index] > Bars.OpenPrices[index];
                double volume = VolumeSeries[index];
                if (VolumeColoring_Input == VolumeColoring_Data.Up_Down) {
                    if (isUp) {
                        UpVolume[index] = volume;
                        DownVolume[index] = double.NaN;
                    }
                    else {
                        DownVolume[index] = volume;
                        UpVolume[index] = double.NaN;
                    }
                }
                if (VolumeColoring_Input == VolumeColoring_Data.Heatmap) {
                    double filterValue = VolumeFilter_Input == VolumeFilter_Data.MA ?
                                         maVol.Result[index] : stdDev.Result[index];
                    double volumeStrength = volume / filterValue;

                    if (VolumeFilter_Input == VolumeFilter_Data.Both)
                        volumeStrength = (volume - maVol.Result[index]) / stdDev.Result[index];

                    volumeStrength = Math.Round(Math.Abs(volumeStrength), 2);
                    if (ShowStrengthValue) {
                        double y1 = isUp ? Bars[index].High : Bars[index].Low;
                        ChartText text = Chart.DrawText($"strength_{index}", $"{volumeStrength}", Bars[index].OpenTime, y1, HeatmapLow_Color);
                        text.HorizontalAlignment = HorizontalAlignment.Center;
                        text.VerticalAlignment = isUp ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                    }

                    _ = volumeStrength < HeatmapLowest_Value ? heatmapSeries(HeatmapSwitch.Lowest) :
                            volumeStrength < HeatmapLow_Value ? heatmapSeries(HeatmapSwitch.Low) :
                            volumeStrength < HeatmapAverage_Value ? heatmapSeries(HeatmapSwitch.Average) :
                            volumeStrength < HeatmapHigh_Value ? heatmapSeries(HeatmapSwitch.High) :
                            volumeStrength >= HeatmapUltra_Value ? heatmapSeries(HeatmapSwitch.Ultra) : heatmapSeries(HeatmapSwitch.Ultra);

                    if (ColoringBars) {
                        _ = volumeStrength < HeatmapLowest_Value ? heatmapColor(HeatmapLowest_Color) :
                             volumeStrength < HeatmapLow_Value ? heatmapColor(HeatmapLow_Color) :
                             volumeStrength < HeatmapAverage_Value ? heatmapColor(HeatmapAverage_Color) :
                             volumeStrength < HeatmapHigh_Value ? heatmapColor(HeatmapHigh_Color) :
                             volumeStrength >= HeatmapUltra_Value ? heatmapColor(HeatmapUltra_Color) : heatmapColor(HeatmapUltra_Color);
                    }
                    bool heatmapSeries(HeatmapSwitch heatSwitch) {
                        HeatmapLowestVolume[index] = heatSwitch == HeatmapSwitch.Lowest ? volume : double.NaN;
                        HeatmapLowVolume[index] = heatSwitch == HeatmapSwitch.Low ? volume : double.NaN;
                        HeatmapAverageVolume[index] = heatSwitch == HeatmapSwitch.Average ? volume : double.NaN;
                        HeatmapHighVolume[index] = heatSwitch == HeatmapSwitch.High ? volume : double.NaN;
                        HeatmapUltraVolume[index] = heatSwitch == HeatmapSwitch.Ultra ? volume : double.NaN;
                        return true;
                    }
                    bool heatmapColor(Color color) {
                        Chart.SetBarColor(index, color);
                        return true;
                    }

                }
                if (VolumeColoring_Input == VolumeColoring_Data.Fading) {
                    if (volume > VolumeSeries[index - 1]) {
                        FadingUpVolume[index] = volume;
                        FadingDownVolume[index] = double.NaN;
                        Chart.SetBarColor(index, FadingUp_Color);
                    }
                    if (volume < VolumeSeries[index - 1]) {
                        FadingDownVolume[index] = volume;
                        FadingUpVolume[index] = double.NaN;
                        Chart.SetBarColor(index, FadingDown_Color);
                    }
                    if (volume == VolumeSeries[index - 1]) {
                        FadingDownVolume[index] = double.NaN;
                        FadingUpVolume[index] = double.NaN;
                        Chart.SetBarColor(index, HeatmapLow_Color);
                    }
                }
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
                if (!loadingAsyncTicks)
                {
                    string volumeLineInfo = "=> Zoom out and follow the Vertical Line";
                    asyncTickPopup = Notifications.ShowPopup(
                        NOTIFY_CAPTION,
                        $"[{Symbol.Name}] Loading Tick Data Asynchronously every 0.5 second...\n{volumeLineInfo}",
                        PopupNotificationState.InProgress
                    );
                    // Draw target date.
                    DrawFromDateLine();
                }

                if (!loadingTicksComplete)
                {
                    TicksOHLC.LoadMoreHistoryAsync((_) =>
                    {
                        DateTime currentDate = _.Bars.FirstOrDefault().OpenTime;

                        DrawStartVolumeLine();

                        if (currentDate <= fromDateTime)
                        {

                            if (asyncTickPopup.State != PopupNotificationState.Success)
                                asyncTickPopup.Complete(PopupNotificationState.Success);

                            if (LoadTickNotify_Input == LoadTickNotify_Data.Detailed)
                            {
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
                else
                {
                    Timer.Stop();
                    DrawOnScreen("");
                    Recalculate();
                    timerHandler.isAsyncLoading = false;
                }
            }
        }

    }
}