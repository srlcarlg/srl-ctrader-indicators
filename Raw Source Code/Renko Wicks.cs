/*
--------------------------------------------------------------------------------------------------------------------------------
                      Renko Wicks
                      revision 1

Renko Wicks applies tick volume logic on Price-Based charts.

Uses Tick Data to make the calculation of wicks
UpWick = Lowest price existing during the formation of a bar (between of OpenTime and CloseTime)
DownWick = Highest price existing during the formation of a bar (between OpenTime and CloseTime)

What's new in rev.1?
-Includes all "Order Flow Aggregated" related improvements
    - High-performance GetWicks()
    - Asynchronous Tick Data Collection

AUTHOR: srlcarlg
----------------------------------------------------------------------------------------------------------------------------
*/
using cAlgo.API;
using cAlgo.API.Internals;
using System;
using System.Linq;
using System.Globalization;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class RenkoWicks : Indicator
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


        [Parameter("Wicks Thickness:", DefaultValue = 1, MaxValue = 5, Group = "==== Renko Wicks ====")]
        public int Thickness { get; set; }

        // Tick Volume
        public readonly string NOTIFY_CAPTION = "Renko Wicks";
        private DateTime firstTickTime;
        private DateTime fromDateTime;
        private Bars TicksOHLC;
        private int lastTick_Wicks = 0;
        private ProgressBar syncTickProgressBar = null;
        PopupNotification asyncTickPopup = null;
        private bool loadingAsyncTicks = false;
        private bool loadingTicksComplete = false;

        // Timer
        private class TimerHandler {
            public bool isAsyncLoading = false;
        }
        private readonly TimerHandler timerHandler = new();

        private bool WrongTF = false;
        private Color UpColor;
        private Color DownColor;

        protected override void Initialize()
        {
            // ===== Verify Timeframe =====
            string currentTimeframe = Chart.TimeFrame.ToString();
            if (!currentTimeframe.Contains("Renko"))
            {
                DrawOnScreen($"Renko Wicks \n WORKS ONLY IN RENKO CHART!");
                WrongTF = true;
                return;
            }

            // First Ticks Data
            TicksOHLC = MarketData.GetBars(TimeFrame.Tick);

            UpColor = Chart.ColorSettings.BullOutlineColor;
            DownColor = Chart.ColorSettings.BearOutlineColor;

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
            if (WrongTF)
                return;

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

            double highest = Bars.HighPrices[index];
            double lowest = Bars.LowPrices[index];
            double open = Bars.OpenPrices[index];

            bool isBullish = Bars.ClosePrices[index] > Bars.OpenPrices[index];
            bool prevIsBullish = Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1];
            bool priceGap = Bars.OpenTimes[index] == Bars[index - 1].OpenTime || Bars[index - 2].OpenTime == Bars[index - 1].OpenTime;
            DateTime currentOpenTime = Bars.OpenTimes[index];
            DateTime nextOpenTime = Bars.OpenTimes[index + 1];

            double[] wicks = GetWicks(currentOpenTime, nextOpenTime);
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
                    ChartTrendLine trendlineUp = Chart.DrawTrendLine($"UpWick_{index}", currentOpenTime, open, currentOpenTime, lowest, UpColor);
                    trendlineUp.Thickness = Thickness;
                    Chart.RemoveObject($"DownWick_{index}");
                }
            }
            else
            {
                if (highest > open && !priceGap) {
                    if (IsLastBar && prevIsBullish && Bars.ClosePrices[index] < open)
                        open = Bars.OpenPrices[index];
                    ChartTrendLine trendlineDown = Chart.DrawTrendLine($"DownWick_{index}", currentOpenTime, open, currentOpenTime, highest, DownColor);
                    trendlineDown.Thickness = Thickness;
                    Chart.RemoveObject($"UpWick_{index}");
                }
            }
        }

        // ========= Functions Area ==========
        private double[] GetWicks(DateTime startTime, DateTime endTime)
        {
            double min = Int32.MaxValue;
            double max = 0;

            if (IsLastBar)
                endTime = TicksOHLC.LastBar.OpenTime;

            for (int tickIndex = lastTick_Wicks; tickIndex < TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime) {
                    if (tickBar.OpenTime > endTime) { lastTick_Wicks = tickIndex; break; }
                    else continue;
                }

                if (tickBar.Close < min)
                    min = tickBar.Close;
                else if (tickBar.Close > max)
                    max = tickBar.Close;
            }

            double[] toReturn = { min, max };
            return toReturn;
        }
        private void DrawOnScreen(string msg)
        {
            Chart.DrawStaticText("txt", $"{msg}", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
        }

        private void Recalculate() {
            int startIndex = Bars.OpenTimes.GetIndexByTime(TicksOHLC.OpenTimes.FirstOrDefault());
            for (int index = startIndex; index < Bars.Count; index++)
            {
                double highest = Bars.HighPrices[index];
                double lowest = Bars.LowPrices[index];
                double open = Bars.OpenPrices[index];

                bool isBullish = Bars.ClosePrices[index] > Bars.OpenPrices[index];
                bool priceGap = Bars.OpenTimes[index] == Bars[index - 1].OpenTime || Bars[index - 2].OpenTime == Bars[index - 1].OpenTime;
                DateTime currentOpenTime = Bars.OpenTimes[index];
                DateTime nextOpenTime = Bars.OpenTimes[index + 1];

                double[] wicks = GetWicks(currentOpenTime, nextOpenTime);
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
                        ChartTrendLine trendlineUp = Chart.DrawTrendLine($"UpWick_{index}", currentOpenTime, open, currentOpenTime, lowest, UpColor);
                        trendlineUp.Thickness = Thickness;
                        Chart.RemoveObject($"DownWick_{index}");
                    }
                }
                else
                {
                    if (highest > open && !priceGap) {
                        ChartTrendLine trendlineDown = Chart.DrawTrendLine($"DownWick_{index}", currentOpenTime, open, currentOpenTime, highest, DownColor);
                        trendlineDown.Thickness = Thickness;
                        Chart.RemoveObject($"UpWick_{index}");
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