/*
--------------------------------------------------------------------------------------------------------------------------------
                      Volume for Renko/Range
                           revision 2
VolumeRenkoRange applies tick volume logic on Price-Based charts.

Uses Tick Data to make the calculation of volume, just like Candles.
It's possible because we have the [Open, Close]Time of the Bar, so:
Volume logic = Number of price updates (ticks) that come during the formation of a bar (between of OpenTime and CloseTime).

What's new in rev.2?
- Features from "Weis & Wyckoff System" (wyckoff bars):
  - [Normalized_Emphasized, L1Norm] filters
  - "Percentile" Ratio
  - "Use 'Tick Volume' from bars?" parameter/feature

Last update => 03/01/2026

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

        [Parameter("[Candles] Use 'Tick Volume' from bars?", DefaultValue = true, Group = "==== Volume Filter ====")]
        public bool UseTimeBasedVolume { get; set; }

        public enum VolumeFilter_Data
        {
            MA,
            Standard_Deviation,
            Both,
            Normalized_Emphasized,
            L1Norm
        }
        [Parameter("Filter:", DefaultValue = VolumeFilter_Data.MA, Group = "==== Heatmap Filter ====")]
        public VolumeFilter_Data VolumeFilter_Input { get; set; }

        [Parameter("[MA, L1] Period:", DefaultValue = 20, MinValue = 1, Group = "==== Heatmap Filter ====")]
        public int MAperiod { get; set; }

        [Parameter("MA Type:", DefaultValue = MovingAverageType.Triangular, Group = "==== Heatmap Filter ====")]
        public MovingAverageType MAtype { get; set; }
        
        [Parameter("Normalized Period:", DefaultValue = 5, MinValue = 1, Group = "==== Heatmap Filter ====")]
        public int NormalizePeriod { get; set; }

        [Parameter("Normalized Multiplier:", DefaultValue = 10, MinValue = 1, Group = "==== Heatmap Filter ====")]
        public int NormalizeMultiplier { get; set; }


        public enum VolumeRatio_Data
        {
            Fixed,
            Percentile,
        }
        [Parameter("Type:", DefaultValue = VolumeRatio_Data.Percentile, Group = "==== Ratio ====")]
        public VolumeRatio_Data VolumeRatio_Input { get; set; }

        [Parameter("[Percentile] Period:", DefaultValue = 20, MinValue = 1, Group = "==== Ratio ====")]
        public int Pctile_Period { get; set; }
        

        [Parameter("Coloring Bars?:", DefaultValue = true, Group = "==== HeatMap Coloring ====")]
        public bool ColoringBars { get; set; }
        
        [Parameter("[Debug] Show Strength Value?:", DefaultValue = false, Group = "==== HeatMap Coloring ====")]
        public bool ShowStrengthValue { get; set; }

        [Parameter("Lowest Color[Bar]:", DefaultValue = "Aqua", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapLowest_Color { get; set; }

        [Parameter("Low Color[Bar]:", DefaultValue = "White", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapLow_Color { get; set; }

        [Parameter("Average Color[Bar]:", DefaultValue = "Yellow", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapAverage_Color { get; set; }


        [Parameter("High Color[Bar]:", DefaultValue = "DarkOrange", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapHigh_Color { get; set; }

        [Parameter("Ultra Color[Bar]:", DefaultValue = "Red", Group = "==== HeatMap Coloring ====")]
        public Color HeatmapUltra_Color { get; set; }


        [Parameter("Fading Up[Bar]:", DefaultValue = "#B200BFFF", Group = "==== Fading Coloring ====")]
        public Color FadingUp_Color { get; set; }
        [Parameter("Fading Down[Bar]:", DefaultValue = "#B2DC143C", Group = "==== Fading Coloring ====")]
        public Color FadingDown_Color { get; set; }


        [Parameter("Lowest < Max Threshold:", DefaultValue = 0.5, MinValue = 0.1, Step = 0.01, Group = "==== Fixed Ratio ====")]
        public double Lowest_FixedValue { get; set; }

        [Parameter("Low:", DefaultValue = 1.2, MinValue = 0.1, Step = 0.01, Group = "==== Fixed Ratio ====")]
        public double Low_FixedValue { get; set; }

        [Parameter("Average:", DefaultValue = 2.5, MinValue = 0.1, Step = 0.01, Group = "==== Fixed Ratio ====")]
        public double Average_FixedValue { get; set; }

        [Parameter("High:", DefaultValue = 3.5, MinValue = 0.1, Step = 0.01, Group = "==== Fixed Ratio ====")]
        public double High_FixedValue { get; set; }

        [Parameter("Ultra >= Max Threshold:", DefaultValue = 3.51, MinValue = 0.1, Step = 0.01, Group = "==== Fixed Ratio ====")]
        public double Ultra_FixedValue { get; set; }

        
        [Parameter("Lowest < Max Threshold:", DefaultValue = 40, MinValue = 1, Group = "==== Percentile Ratio ====")]
        public int Lowest_PctileValue { get; set; }

        [Parameter("Low:", DefaultValue = 70, MinValue = 1, Group = "==== Percentile Ratio ====")]
        public int Low_PctileValue { get; set; }

        [Parameter("Average:", DefaultValue = 90, MinValue = 1, Group = "==== Percentile Ratio ====")]
        public int Average_PctileValue { get; set; }

        [Parameter("High:", DefaultValue = 97, MinValue = 1, Group = "==== Percentile Ratio ====")]
        public int High_PctileValue { get; set; }

        [Parameter("Ultra >= Max Threshold:", DefaultValue = 99, MinValue = 1, Group = "==== Percentile Ratio ====")]
        public int Ultra_PctileValue { get; set; }

        
        [Parameter("Lowest < Max Threshold:", DefaultValue = 23.6, MinValue = 1, Step = 0.1, Group = "==== Emphasized Ratio(%) ====")]
        public double Lowest_PctValue { get; set; }

        [Parameter("Low:", DefaultValue = 38.2, MinValue = 1, Step = 0.1, Group = "==== Emphasized Ratio(%) ====")]
        public double Low_PctValue { get; set; }

        [Parameter("Average:", DefaultValue = 61.8, MinValue = 1, Step = 0.1, Group = "==== Emphasized Ratio(%) ====")]
        public double Average_PctValue { get; set; }

        [Parameter("High:", DefaultValue = 100, MinValue = 1, Step = 0.1, Group = "==== Emphasized Ratio(%) ====")]
        public double High_PctValue { get; set; }

        [Parameter("Ultra >= Max Threshold:", DefaultValue = 101, MinValue = 1, Step = 0.1, Group = "==== Emphasized Ratio(%) ====")]
        public double Ultra_PctValue { get; set; }


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
        private IndicatorDataSeries StrengthSeries;

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
        private bool isPriceBased_Chart = false;

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
            StrengthSeries = CreateDataSeries();

            string currentTimeframe = Chart.TimeFrame.ToString();
            isPriceBased_Chart = currentTimeframe.Contains("Renko") || currentTimeframe.Contains("Range") || currentTimeframe.Contains("Tick");
            
            if (!UseTimeBasedVolume && !isPriceBased_Chart || isPriceBased_Chart) 
            { 
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
            }
            Timer.Start(TimeSpan.FromSeconds(0.5));
            
            DrawOnScreen("Loading Ticks Data... \n or \n Calculating...");
        }

        public override void Calculate(int index)
        {
            if (!UseTimeBasedVolume && !isPriceBased_Chart || isPriceBased_Chart)
            {
                // Tick Data Collection on chart
                bool isOnChart = LoadTickStrategy_Input != LoadTickStrategy_Data.At_Startup_Sync;
                if (isOnChart && !loadingTicksComplete)
                    LoadMoreTicksOnChart();

                bool isOnChartAsync = LoadTickStrategy_Input == LoadTickStrategy_Data.On_ChartEnd_Async;
                if (isOnChartAsync && !loadingTicksComplete)
                    return;
            }
            // Removing Messages
            if (!IsLastBar)
                DrawOnScreen("");

            VolumeSeries[index] = UseTimeBasedVolume && !isPriceBased_Chart ? Bars.TickVolumes[index] : VolumeTick(index);
            MovingAverageLine[index] = maVol.Result[index];
            StdDevLine[index] = stdDev.Result[index];

            bool isUp = Bars.ClosePrices[index] > Bars.OpenPrices[index];
            double volume = VolumeSeries[index];
            Filters(index, isUp, volume);
        }

        private void Filters(int index, bool isUp, double volume)
        {
            switch (VolumeColoring_Input)
            {
                case VolumeColoring_Data.Up_Down:
                    if (isUp)
                    {
                        UpVolume[index] = volume;
                        DownVolume[index] = double.NaN;
                    }
                    else
                    {
                        DownVolume[index] = volume;
                        UpVolume[index] = double.NaN;
                    }
                    break;
                case VolumeColoring_Data.Heatmap:
                    double strengthVolume = 0;

                    switch (VolumeFilter_Input)
                    {
                        case VolumeFilter_Data.MA:
                            strengthVolume = volume / maVol.Result[index]; break;
                        case VolumeFilter_Data.Standard_Deviation:
                            strengthVolume = volume / stdDev.Result[index]; break;
                        case VolumeFilter_Data.Both:
                            strengthVolume = (volume - maVol.Result[index]) / stdDev.Result[index]; break;
                        case VolumeFilter_Data.Normalized_Emphasized:
                            double Normalization()
                            {
                                if (index < NormalizePeriod)
                                    return 0;

                                double avg = 0;
                                for (int j = index; j > index - NormalizePeriod; j--)
                                    avg += VolumeSeries[j];

                                avg /= NormalizePeriod;

                                double normalizedValue = VolumeSeries[index] / avg;
                                double normalizedPercentage = (normalizedValue * 100) - 100;
                                normalizedPercentage *= NormalizeMultiplier;

                                return normalizedPercentage;
                            }
                            strengthVolume = Normalization();
                            break;
                        case VolumeFilter_Data.L1Norm:
                            double[] window = new double[MAperiod];

                            for (int i = 0; i < MAperiod; i++)
                                window[i] = VolumeSeries[index - MAperiod + 1 + i];

                            strengthVolume = L1NormStrength(window);
                            break;
                    }

                    // Keep negative values of Normalized_Emphasized
                    if (VolumeFilter_Input != VolumeFilter_Data.Normalized_Emphasized)
                        strengthVolume = Math.Abs(strengthVolume);

                    strengthVolume = Math.Round(strengthVolume, 2);

                    if (VolumeRatio_Input == VolumeRatio_Data.Percentile && VolumeFilter_Input != VolumeFilter_Data.Normalized_Emphasized)
                    {
                        StrengthSeries[index] = strengthVolume;

                        double[] window = new double[Pctile_Period];

                        for (int i = 0; i < Pctile_Period; i++)
                            window[i] = StrengthSeries[index - Pctile_Period + 1 + i];

                        strengthVolume = RollingPercentile(window);
                        strengthVolume = Math.Round(strengthVolume, 1);
                    }

                    if (ShowStrengthValue)
                    {
                        double y1 = isUp ? Bars[index].High : Bars[index].Low;
                        ChartText text = Chart.DrawText($"strength_{index}", $"{strengthVolume}", Bars[index].OpenTime, y1, HeatmapLow_Color);
                        text.HorizontalAlignment = HorizontalAlignment.Center;
                        text.VerticalAlignment = isUp ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                    }

                    bool isFixed = VolumeRatio_Input == VolumeRatio_Data.Fixed;

                    double lowest = isFixed ? Lowest_FixedValue : Lowest_PctileValue;
                    double low = isFixed ? Low_FixedValue : Low_PctileValue;
                    double average = isFixed ? Average_FixedValue : Average_PctileValue;
                    double high = isFixed ? High_FixedValue : High_PctileValue;
                    double ultra = isFixed ? Ultra_FixedValue : Ultra_PctileValue;

                    if (VolumeFilter_Input == VolumeFilter_Data.Normalized_Emphasized)
                    {
                        lowest = Lowest_PctValue;
                        low = Low_PctValue;
                        average = Average_PctValue;
                        high = High_PctValue;
                        ultra = Ultra_PctValue;
                    }

                    _ = strengthVolume < lowest ? heatmapSeries(HeatmapSwitch.Lowest) :
                        strengthVolume < low ? heatmapSeries(HeatmapSwitch.Low) :
                        strengthVolume < average ? heatmapSeries(HeatmapSwitch.Average) :
                        strengthVolume < high ? heatmapSeries(HeatmapSwitch.High) :
                        strengthVolume >= Ultra_FixedValue ? heatmapSeries(HeatmapSwitch.Ultra) : heatmapSeries(HeatmapSwitch.Lowest);

                    if (ColoringBars)
                    {
                        _ = strengthVolume < lowest ? heatmapColor(HeatmapLowest_Color) :
                            strengthVolume < low ? heatmapColor(HeatmapLow_Color) :
                            strengthVolume < average ? heatmapColor(HeatmapAverage_Color) :
                            strengthVolume < high ? heatmapColor(HeatmapHigh_Color) :
                            strengthVolume >= ultra ? heatmapColor(HeatmapUltra_Color) : heatmapColor(HeatmapLowest_Color);
                    }

                    bool heatmapSeries(HeatmapSwitch heatSwitch)
                    {
                        HeatmapLowestVolume[index] = heatSwitch == HeatmapSwitch.Lowest ? volume : double.NaN;
                        HeatmapLowVolume[index] = heatSwitch == HeatmapSwitch.Low ? volume : double.NaN;
                        HeatmapAverageVolume[index] = heatSwitch == HeatmapSwitch.Average ? volume : double.NaN;
                        HeatmapHighVolume[index] = heatSwitch == HeatmapSwitch.High ? volume : double.NaN;
                        HeatmapUltraVolume[index] = heatSwitch == HeatmapSwitch.Ultra ? volume : double.NaN;
                        return true;
                    }
                    bool heatmapColor(Color color)
                    {
                        Chart.SetBarColor(index, color);
                        return true;
                    }
                    break;
                case VolumeColoring_Data.Fading:
                    if (volume > VolumeSeries[index - 1])
                    {
                        FadingUpVolume[index] = volume;
                        FadingDownVolume[index] = double.NaN;
                        Chart.SetBarColor(index, FadingUp_Color);
                    }
                    if (volume < VolumeSeries[index - 1])
                    {
                        FadingDownVolume[index] = volume;
                        FadingUpVolume[index] = double.NaN;
                        Chart.SetBarColor(index, FadingDown_Color);
                    }
                    if (volume == VolumeSeries[index - 1])
                    {
                        FadingDownVolume[index] = double.NaN;
                        FadingUpVolume[index] = double.NaN;
                        Chart.SetBarColor(index, HeatmapLow_Color);
                    }
                    break;
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

        private void Recalculate() {
            int startIndex = Bars.OpenTimes.GetIndexByTime(TicksOHLC.OpenTimes.FirstOrDefault());
            for (int index = startIndex; index < Bars.Count; index++)
            {
                VolumeSeries[index] = UseTimeBasedVolume && !isPriceBased_Chart ? Bars.TickVolumes[index] : VolumeTick(index);
                MovingAverageLine[index] = maVol.Result[index];
                StdDevLine[index] = stdDev.Result[index];

                bool isUp = Bars.ClosePrices[index] > Bars.OpenPrices[index];
                double volume = VolumeSeries[index];
                
                Filters(index, isUp, volume);
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