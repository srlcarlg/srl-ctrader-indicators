/*
--------------------------------------------------------------------------------------------------------------------------------
                      Anchored VWAP Buttons
                          revision 1

Just a Anchored VWAP with 5 Buttons in just 1 indicator

What's new in rev.1?
-Added Daily, Weekly, Monthly VWAPs
-Added StdDev to any VWAP (one StdDev per instance)
-Great refactor

Usage:
Create VWAP: Click on the button and select the bar for the VWAP
Remove VWAP: Click the button again when it is activated.

VWAP will be updated with each new bar

Last update => 10/11/2025
===========================

Final revision (2025)

- Fix: UI Panel on MacOs
    - WrapPanel isn't fully supported (The buttons are hidden)
    
- Tested on MacOS (12 Monterey / 13 Ventura) without 3D accelerated graphics

===========================
AUTHOR: srlcarlg
----------------------------------------------------------------------------------------------------------------------------
*/

using cAlgo.API;
using cAlgo.API.Internals;
using System;
using System.Collections.Generic;

namespace cAlgo
{
    [Cloud("Top VWAP", "Middle VWAP")]
    [Cloud("Bottom VWAP", "Middle VWAP")]

    [Cloud("Upper Deviation 1", "Upper Deviation 3")]
    [Cloud("Lower Deviation 1", "Lower Deviation 3")]

    [Indicator(IsOverlay = true, AutoRescale = false, AccessRights = AccessRights.None)]
    public class AnchoredVWAPButtons : Indicator
    {
        public enum ConfigButtons_Data
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
        [Parameter("Buttons Color:", DefaultValue = "98ADD8E6", Group = "==== Anchored VWAP Buttons ====")]
        public Color BtnColor { get; set; }
        [Parameter("Buttons Position:", DefaultValue = ConfigButtons_Data.Top_Right, Group = "==== Anchored VWAP Buttons ====")]
        public ConfigButtons_Data ConfigButtons_Input { get; set; }
        [Parameter("Buttons Orientation:", DefaultValue = Orientation.Horizontal, Group = "==== Anchored VWAP Buttons ====")]
        public Orientation BtnOrientation { get; set; }

        [Parameter("Show Daily", DefaultValue = false, Group = "==== DWM VWAPs ====")]
        public bool ShowDaily { get; set; }
        [Parameter("Show Weekly", DefaultValue = false, Group = "==== DWM VWAPs ====")]
        public bool ShowWeekly { get; set; }
        [Parameter("Show Monthly", DefaultValue = false, Group = "==== DWM VWAPs ====")]
        public bool ShowMonthly { get; set; }
        [Parameter("Remove Interval Line (first bar)", DefaultValue = true, Group = "==== DWM VWAPs ====")]
        public bool RemoveIntervalLine { get; set; }

        public enum StdDevSource_Data
        {
            Anchored,
            Daily,
            Weekly,
            Monthly
        }
        [Parameter("StdDev Source", DefaultValue = StdDevSource_Data.Anchored, Group = "==== StdDev VWAP ====")]
        public StdDevSource_Data StdDevSource_Input { get; set; }
        [Parameter("First Multiplier", DefaultValue = 1.0, Group = "==== StdDev VWAP ====")]
        public double StdDev1_Input { get; set; }
        [Parameter("Second Multiplier", DefaultValue = 2.0, Group = "==== StdDev VWAP ====")]
        public double StdDev2_Input { get; set; }
        [Parameter("Third Multiplier", DefaultValue = 3.0, Group = "==== StdDev VWAP ====")]
        public double StdDev3_Input { get; set; }


        [Output("Daily VWAP", LineColor = "FFFFFF33", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries DailyVWAP { get; set; }
        [Output("Weekly VWAP", LineColor = "FF02AFF1", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries WeeklyVWAP { get; set; }
        [Output("Monthly VWAP", LineColor = "FF00BF00", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries MonthlyVWAP { get; set; }

        [Output("Upper Deviation 1", LineColor = "7400BFFF", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries TopStdDev_1 { get; set; }
        [Output("Upper Deviation 2", LineColor = "7400BFFF", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries TopStdDev_2 { get; set; }
        [Output("Upper Deviation 3", LineColor = "7400BFFF", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries TopStdDev_3 { get; set; }

        [Output("Lower Deviation 1", LineColor = "74DC143C", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries BottomStdDev_1 { get; set; }
        [Output("Lower Deviation 2", LineColor = "74DC143C", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries BottomStdDev_2 { get; set; }
        [Output("Lower Deviation 3", LineColor = "74DC143C", Thickness = 1, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries BottomStdDev_3 { get; set; }


        [Output("Top VWAP", LineColor = "A000BFFF", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP { get; set; }
        [Output("Top VWAP 2", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_1 { get; set; }
        [Output("Top VWAP 3", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_2 { get; set; }
        [Output("Top VWAP 4", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_3 { get; set; }
        [Output("Top VWAP 5", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_4 { get; set; }

        [Output("Middle VWAP", LineColor = "A0FFFFE0", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP { get; set; }
        [Output("Middle VWAP 2", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_1 { get; set; }
        [Output("Middle VWAP 3", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_2 { get; set; }
        [Output("Middle VWAP 4", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_3 { get; set; }
        [Output("Middle VWAP 5", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_4 { get; set; }

        [Output("Bottom VWAP", LineColor = "A0FFA500", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP { get; set; }
        [Output("Bottom VWAP 2", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_1 { get; set; }
        [Output("Bottom VWAP 3", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_2 { get; set; }
        [Output("Bottom VWAP 4", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_3 { get; set; }
        [Output("Bottom VWAP 5", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_4 { get; set; }


        private bool mouseIsActive;
        private bool btnIsActive;
        private Button currentBtn;
        private ChartVerticalLine verticalLine;
        private readonly int[] buttonsStartIndexes = { 0, 0, 0, 0, 0 };
        private readonly IDictionary<int, Button> buttonsDict = new Dictionary<int, Button>();

        // DWM VWAPs
        public IndicatorDataSeries CumulPriceVol_D;
        public IndicatorDataSeries CumulPriceVol_W;
        public IndicatorDataSeries CumulPriceVol_M;

        public IndicatorDataSeries CumulVol_D;
        public IndicatorDataSeries CumulVol_W;
        public IndicatorDataSeries CumulVol_M;

        private Bars Daily_Bars;
        private Bars Weekly_Bars;
        private Bars Monthly_Bars;

        protected override void Initialize()
        {
            VerticalAlignment vAlign = VerticalAlignment.Bottom;
            HorizontalAlignment hAlign = HorizontalAlignment.Right;

            switch (ConfigButtons_Input)
            {
                case ConfigButtons_Data.Bottom_Left:
                    hAlign = HorizontalAlignment.Left;
                    break;
                case ConfigButtons_Data.Top_Left:
                    vAlign = VerticalAlignment.Top;
                    hAlign = HorizontalAlignment.Left;
                    break;
                case ConfigButtons_Data.Top_Right:
                    vAlign = VerticalAlignment.Top;
                    hAlign = HorizontalAlignment.Right;
                    break;
                case ConfigButtons_Data.Center_Right:
                    vAlign = VerticalAlignment.Center;
                    hAlign = HorizontalAlignment.Right;
                    break;
                case ConfigButtons_Data.Center_Left:
                    vAlign = VerticalAlignment.Center;
                    hAlign = HorizontalAlignment.Left;
                    break;
                case ConfigButtons_Data.Top_Center:
                    vAlign = VerticalAlignment.Top;
                    hAlign = HorizontalAlignment.Center;
                    break;
                case ConfigButtons_Data.Bottom_Center:
                    vAlign = VerticalAlignment.Bottom;
                    hAlign = HorizontalAlignment.Center;
                    break;
            }

            StackPanel stackPanel = new()
            {
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Orientation = BtnOrientation,
            };

            // For Loop slowdown indicator startup
            AddButton(stackPanel, BtnColor, 0);
            AddButton(stackPanel, BtnColor, 1);
            AddButton(stackPanel, BtnColor, 2);
            AddButton(stackPanel, BtnColor, 3);
            AddButton(stackPanel, BtnColor, 4);

            Chart.AddControl(stackPanel);
            Chart.MouseMove += DrawVerticalLine;
            Chart.MouseDown += AddVWAP;
            Bars.BarOpened += UpdateVWAP;

            if (ShowDaily) {
                Daily_Bars = MarketData.GetBars(TimeFrame.Daily);
                CumulPriceVol_D = CreateDataSeries();
                CumulVol_D = CreateDataSeries();
            }
            if (ShowWeekly) {
                Weekly_Bars = MarketData.GetBars(TimeFrame.Weekly);
                CumulPriceVol_W = CreateDataSeries();
                CumulVol_W = CreateDataSeries();
            }
            if (ShowMonthly) {
                Monthly_Bars = MarketData.GetBars(TimeFrame.Monthly);
                CumulPriceVol_M = CreateDataSeries();
                CumulVol_M = CreateDataSeries();
            }
            /*
                No comments needed, everything is self explanatory
            */
        }
        private void AddButton(Panel panel, Color btnColor, int btnIndex)
        {
            Button button = new()
            {
                Text = "",
                Padding = 0,
                Width = 22,
                Height = 22,
                Margin = 2,
                BackgroundColor = btnColor
            };

            button.Click += ButtonClick;
            panel.AddChild(button);
            buttonsDict.Add(btnIndex, button);
        }
        private void ButtonClick(ButtonClickEventArgs obj)
        {
            if (obj.Button.Text != "")
            {
                ClearVWAP(Convert.ToInt32(obj.Button.Text));
                obj.Button.Text = "";
                return;
            }
            btnIsActive = true;
            currentBtn = obj.Button;
            obj.Button.IsEnabled = false;
            Chart.DrawStaticText("txt", "Select a bar for VWAP.", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
            if (StdDevSource_Input == StdDevSource_Data.Anchored)
                Chart.DrawStaticText("txt1", "\nOnly the first button[0] will have the Standard Deviation.", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
        }
        public void DrawVerticalLine(ChartMouseEventArgs obj)
        {
            if (btnIsActive)
            {
                mouseIsActive = true;

                if (verticalLine == null)
                    verticalLine = Chart.DrawVerticalLine("VerticalLine", obj.TimeValue, Chart.ColorSettings.ForegroundColor);
                else
                    verticalLine.Time = obj.TimeValue;

                verticalLine.IsInteractive = true;
                verticalLine.IsLocked = true;
            }
        }

        public void AddVWAP(ChartMouseEventArgs obj)
        {
            if (!mouseIsActive)
                return;

            mouseIsActive = false;

            int btnIndex = 0;
            for (int i = 0; i < 5; i++)
            {
                if (buttonsDict[i] == currentBtn)
                {
                    buttonsDict[i].Text = $"{i}";
                    buttonsDict[i].IsEnabled = true;
                    btnIndex = i;
                    break;
                }
            }

            double sumHigh = 0.0;
            double sumHL2 = 0.0;
            double sumLow = 0.0;
            double sumVol = 0.0;
            int indexStart = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);
            for (int j = indexStart; j < Chart.BarsTotal; j++)
            {
                sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                sumVol += Bars.TickVolumes[j];

                void CreateVWAP(IndicatorDataSeries TopVWAP_Series, IndicatorDataSeries MiddleVWAP_Series, IndicatorDataSeries BottomVWAP_Series) {
                    TopVWAP_Series[j] = sumHigh / sumVol;
                    MiddleVWAP_Series[j] = sumHL2 / sumVol;
                    BottomVWAP_Series[j] = sumLow / sumVol;
                    // Only ONE Anchored StdDev VWAP
                    if (StdDevSource_Input == StdDevSource_Data.Anchored && btnIndex == 0)
                        StdDev_VWAP(indexStart, j, MiddleVWAP_Series);
                }

                switch (btnIndex)
                {
                    case 0: CreateVWAP(TopVWAP, MiddleVWAP, BottomVWAP); break;
                    case 1: CreateVWAP(TopVWAP_1, MiddleVWAP_1, BottomVWAP_1); break;
                    case 2: CreateVWAP(TopVWAP_2, MiddleVWAP_2, BottomVWAP_2); break;
                    case 3: CreateVWAP(TopVWAP_3, MiddleVWAP_3, BottomVWAP_3); break;
                    case 4: CreateVWAP(TopVWAP_4, MiddleVWAP_4, BottomVWAP_4); break;
                }
            }

            if (buttonsStartIndexes[btnIndex] == 0)
                buttonsStartIndexes[btnIndex] = indexStart;

            if (verticalLine != null)
            {
                Chart.DrawStaticText("txt", "", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
                Chart.RemoveObject("VerticalLine");
                Chart.RemoveObject("txt");
                if (StdDevSource_Input == StdDevSource_Data.Anchored)
                    Chart.RemoveObject("txt1");
                verticalLine = null;
                btnIsActive = false;
            }

        }

        private void UpdateVWAP(BarOpenedEventArgs obj)
        {
            bool[] btnActives = { false, false, false, false, false };
            for (int i = 0; i < 5; i++)
            {
                if (buttonsStartIndexes[i] != 0)
                    btnActives[i] = true;
            }

            void UpdtVWAP(int btnIndex, IndicatorDataSeries TopVWAP_Series, IndicatorDataSeries MiddleVWAP_Series, IndicatorDataSeries BottomVWAP_Series)
            {
                double sumHigh = 0.0;
                double sumHL2 = 0.0;
                double sumLow = 0.0;
                double sumVol = 0.0;
                for (int j = buttonsStartIndexes[btnIndex]; j < Chart.BarsTotal; j++)
                {
                    sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                    sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                    sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                    sumVol += Bars.TickVolumes[j];

                    TopVWAP_Series[j] = sumHigh / sumVol;
                    MiddleVWAP_Series[j] = sumHL2 / sumVol;
                    BottomVWAP_Series[j] = sumLow / sumVol;

                    // Only ONE Anchored StdDev VWAP
                    if (StdDevSource_Input == StdDevSource_Data.Anchored && btnIndex == 0)
                        StdDev_VWAP(buttonsStartIndexes[btnIndex], j, MiddleVWAP_Series);
                }
            }

            if (btnActives[0])
                UpdtVWAP(0, TopVWAP, MiddleVWAP, BottomVWAP);
            if (btnActives[1])
                UpdtVWAP(1, TopVWAP_1, MiddleVWAP_1, BottomVWAP_1);
            if (btnActives[2])
                UpdtVWAP(2, TopVWAP_2, MiddleVWAP_2, BottomVWAP_2);
            if (btnActives[3])
                UpdtVWAP(3, TopVWAP_3, MiddleVWAP_3, BottomVWAP_3);
            if (btnActives[4])
                UpdtVWAP(4, TopVWAP_4, MiddleVWAP_4, BottomVWAP_4);
        }

        private void ClearVWAP(int btnIndex)
        {
            void resetVWAP(int i, IndicatorDataSeries TopVWAP_Series, IndicatorDataSeries MiddleVWAP_Series, IndicatorDataSeries BottomVWAP_Series)
            {
                buttonsStartIndexes[btnIndex] = 0;
                TopVWAP_Series[i] = double.NaN;
                MiddleVWAP_Series[i] = double.NaN;
                BottomVWAP_Series[i] = double.NaN;

                // Only ONE Anchored StdDev VWAP
                if (StdDevSource_Input == StdDevSource_Data.Anchored && btnIndex == 0)
                {
                    TopStdDev_1[i] = double.NaN;
                    TopStdDev_2[i] = double.NaN;
                    TopStdDev_3[i] = double.NaN;

                    BottomStdDev_1[i] = double.NaN;
                    BottomStdDev_2[i] = double.NaN;
                    BottomStdDev_3[i] = double.NaN;
                }
            }

            for (int i = buttonsStartIndexes[btnIndex]; i < Chart.BarsTotal; i++)
            {
                switch (btnIndex)
                {
                    case 0: resetVWAP(i, TopVWAP, MiddleVWAP, BottomVWAP); break;
                    case 1: resetVWAP(i, TopVWAP_1, MiddleVWAP_1, BottomVWAP_1); break;
                    case 2: resetVWAP(i, TopVWAP_2, MiddleVWAP_2, BottomVWAP_2); break;
                    case 3: resetVWAP(i, TopVWAP_3, MiddleVWAP_3, BottomVWAP_3); break;
                    case 4: resetVWAP(i, TopVWAP_4, MiddleVWAP_4, BottomVWAP_4); break;
                }
            }
        }
        public override void Calculate(int index)
        {
            if (ShowDaily) {
                Bars TF_Bars = Daily_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_D, CumulVol_D, DailyVWAP);

                if (StdDevSource_Input == StdDevSource_Data.Daily)
                    StdDev_VWAP(indexStart, index, DailyVWAP);
            }

            if (ShowWeekly) {
                Bars TF_Bars = Weekly_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_W, CumulVol_W, WeeklyVWAP);

                if (StdDevSource_Input == StdDevSource_Data.Weekly)
                    StdDev_VWAP(indexStart, index, WeeklyVWAP);
            }

            if (ShowMonthly) {
                Bars TF_Bars = Monthly_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_M, CumulVol_M, MonthlyVWAP);

                if (StdDevSource_Input == StdDevSource_Data.Monthly)
                    StdDev_VWAP(indexStart, index, MonthlyVWAP);
            }
        }
        private void DWM_VWAP(int index, int indexStart, IndicatorDataSeries CumulPriceVol_Series, IndicatorDataSeries CumulVol_Series, IndicatorDataSeries VWAP_Series) {
                CumulPriceVol_Series[index] = (Bars.MedianPrices[index] * Bars.TickVolumes[index]) + CumulPriceVol_Series[index - 1];
                CumulVol_Series[index] = Bars.TickVolumes[index] + CumulVol_Series[index - 1];
                VWAP_Series[index] = CumulPriceVol_Series[index] / CumulVol_Series[index];

                if (index == indexStart)
                {
                    CumulPriceVol_Series[index] = Bars.MedianPrices[index] * Bars.TickVolumes[index];
                    CumulVol_Series[index] = Bars.TickVolumes[index];
                    VWAP_Series[index] = CumulPriceVol_Series[index] / CumulVol_Series[index];
                    if (RemoveIntervalLine)
                        VWAP_Series[index] = double.NaN;
                }
        }
        private void StdDev_VWAP(int indexStart, int index, IndicatorDataSeries series) {
            double squaredErrors = 0;
            int count = 0;
            for (int i = indexStart; i <= index; i++)
            {
                double diff = Bars.MedianPrices[i] - series[index];
                squaredErrors += diff * diff;
                count++;
            }

            if (count == 0)
                return;

            // Sample => (Period - 1) / Population => Period
            double variance = squaredErrors / (count - 1);
            double stdDev = Math.Sqrt(variance);

            TopStdDev_1[index] = stdDev * StdDev1_Input + series[index];
            TopStdDev_2[index] = stdDev * StdDev2_Input + series[index];
            TopStdDev_3[index] = stdDev * StdDev3_Input + series[index];

            BottomStdDev_1[index] = series[index] - stdDev * StdDev1_Input;
            BottomStdDev_2[index] = series[index] - stdDev * StdDev2_Input;
            BottomStdDev_3[index] = series[index] - stdDev * StdDev3_Input;
        }
    }
}
