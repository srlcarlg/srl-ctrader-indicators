/*
--------------------------------------------------------------------------------------------------------------------------------
                      Anchored VWAP Buttons
                          revision 2

Just a Anchored VWAP with 5 Buttons in just 1 indicator

Usage:
Create VWAP: Click on the button and select the bar for the VWAP
Remove VWAP: Click the button again when it is activated.

VWAP will be updated with each new bar

Last update => 02/01/2026
===========================

What's new in rev.2?

- StdDev => Volume weighted bands
- Percentile Bands:
  - Symmetric/Asymmetric bands.
  - Volume weighted (bands) for both.

  
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
using System.Linq;
using System.Collections.Generic;

namespace cAlgo
{
    [Cloud("Upper Band 1", "Upper Band 3")]
    [Cloud("Lower Band 1", "Lower Band 3")]

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

        [Parameter("Show Daily?", DefaultValue = false, Group = "==== DWM VWAPs ====")]
        public bool ShowDaily { get; set; }
        [Parameter("Show Weekly?", DefaultValue = false, Group = "==== DWM VWAPs ====")]
        public bool ShowWeekly { get; set; }
        [Parameter("Show Monthly?", DefaultValue = false, Group = "==== DWM VWAPs ====")]
        public bool ShowMonthly { get; set; }
        [Parameter("Remove Interval Line? (first bar)", DefaultValue = true, Group = "==== DWM VWAPs ====")]
        public bool RemoveIntervalLine { get; set; }


        public enum BandsType_Data
        {
            Std_Dev,
            Percentile,
            Percentile_Asymmetric 
        }
        [Parameter("Type:", DefaultValue = BandsType_Data.Std_Dev, Group = "==== Bands VWAP ====")]
        public BandsType_Data BandsType_Input { get; set; }

        public enum BandsSource_Data
        {
            Anchored,
            Daily,
            Weekly,
            Monthly
        }
        [Parameter("Source:", DefaultValue = BandsSource_Data.Anchored, Group = "==== Bands VWAP ====")]
        public BandsSource_Data BandsSource_Input { get; set; }

        [Parameter("Volume Weighted Bands?", DefaultValue = true, Group = "==== Bands VWAP ====")]
        public bool IsVolumeBands { get; set; }


        [Parameter("First Multiplier", DefaultValue = 0.236, Group = "==== StdDev Bands ====")]
        public double StdDev1_Input { get; set; }
        [Parameter("Second Multiplier", DefaultValue = 1.382, Group = "==== StdDev Bands ====")]
        public double StdDev2_Input { get; set; }
        [Parameter("Third Multiplier", DefaultValue = 2.618, Group = "==== StdDev Bands ====")]
        public double StdDev3_Input { get; set; }
        

        [Parameter("First(%)", DefaultValue = 35, Group = "==== Percentile Bands ====")]
        public double Pctile1_Input { get; set; }
        [Parameter("Second(%)", DefaultValue = 85, Group = "==== Percentile Bands ====")]
        public double Pctile2_Input { get; set; }
        [Parameter("Third(%)", DefaultValue = 100, Group = "==== Percentile Bands ====")]
        public double Pctile3_Input { get; set; }

        
        [Parameter("First(%) Up", DefaultValue = 50, Group = "==== Pctile-Asymmetric Bands ====")]
        public double Pctile1_Up_Input { get; set; }
        [Parameter("Second(%) Up", DefaultValue = 80, Group = "==== Pctile-Asymmetric Bands ====")]
        public double Pctile2_Up_Input { get; set; }
        [Parameter("Third(%) UP", DefaultValue = 100, Group = "==== Pctile-Asymmetric Bands ====")]
        public double Pctile3_Up_Input { get; set; }

        [Parameter("First(%) Down", DefaultValue = 45, Group = "==== Pctile-Asymmetric Bands ====")]
        public double Pctile1_Down_Input { get; set; }
        [Parameter("Second(%) Down", DefaultValue = 80, Group = "==== Pctile-Asymmetric Bands ====")]
        public double Pctile2_Down_Input { get; set; }
        [Parameter("Third(%) Down", DefaultValue = 100, Group = "==== Pctile-Asymmetric Bands ====")]
        public double Pctile3_Down_Input { get; set; }


        [Output("Daily VWAP", LineColor = "FFFFFF33", Thickness = 1, LineStyle = LineStyle.Solid, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries DailyVWAP { get; set; }
        [Output("Weekly VWAP", LineColor = "FF02AFF1", Thickness = 1, LineStyle = LineStyle.Solid, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries WeeklyVWAP { get; set; }
        [Output("Monthly VWAP", LineColor = "FF00BF00", Thickness = 1, LineStyle = LineStyle.Solid, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries MonthlyVWAP { get; set; }

        [Output("Upper Band 1", LineColor = "FF68D0F7", Thickness = 1, LineStyle = LineStyle.DotsRare, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries Top_1 { get; set; }
        [Output("Upper Band 2", LineColor = "FF68D0F7", Thickness = 1, LineStyle = LineStyle.DotsRare, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries Top_2 { get; set; }
        [Output("Upper Band 3", LineColor = "8168D0F7", Thickness = 1, LineStyle = LineStyle.DotsRare, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries Top_3 { get; set; }

        [Output("Lower Band 1", LineColor = "81FED966", Thickness = 1, LineStyle = LineStyle.DotsRare, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries Bottom_1 { get; set; }
        [Output("Lower Band 2", LineColor = "FFFED966", Thickness = 1, LineStyle = LineStyle.DotsRare, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries Bottom_2 { get; set; }
        [Output("Lower Band 3", LineColor = "FFFED966", Thickness = 1, LineStyle = LineStyle.DotsRare, PlotType = PlotType.DiscontinuousLine)]
        public IndicatorDataSeries Bottom_3 { get; set; }


        [Output("Top VWAP", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP { get; set; }
        [Output("Top VWAP 2", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_1 { get; set; }
        [Output("Top VWAP 3", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_2 { get; set; }
        [Output("Top VWAP 4", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_3 { get; set; }
        [Output("Top VWAP 5", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_4 { get; set; }

        [Output("Middle VWAP", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP { get; set; }
        [Output("Middle VWAP 2", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_1 { get; set; }
        [Output("Middle VWAP 3", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_2 { get; set; }
        [Output("Middle VWAP 4", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_3 { get; set; }
        [Output("Middle VWAP 5", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_4 { get; set; }

        [Output("Bottom VWAP", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
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
            if (BandsSource_Input == BandsSource_Data.Anchored)
                Chart.DrawStaticText("txt1", "\nOnly the first button[0] will have the VWAP Bands.", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
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
                    if (BandsSource_Input == BandsSource_Data.Anchored && btnIndex == 0)
                        _ = BandsType_Input switch
                        {
                            BandsType_Data.Percentile => QuantileBands(indexStart, j, MiddleVWAP_Series),
                            BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, j, MiddleVWAP_Series),
                            _ => StdDevBands(indexStart, j, MiddleVWAP_Series),
                        };                    
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
                if (BandsSource_Input == BandsSource_Data.Anchored)
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
                    if (BandsSource_Input == BandsSource_Data.Anchored && btnIndex == 0)
                        _ = BandsType_Input switch
                        {
                            BandsType_Data.Percentile => QuantileBands(buttonsStartIndexes[btnIndex], j, MiddleVWAP_Series),
                            BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(buttonsStartIndexes[btnIndex], j, MiddleVWAP_Series),
                            _ => StdDevBands(buttonsStartIndexes[btnIndex], j, MiddleVWAP_Series),
                        };                    
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
                if (BandsSource_Input == BandsSource_Data.Anchored && btnIndex == 0)
                {
                    Top_1[i] = double.NaN;
                    Top_2[i] = double.NaN;
                    Top_3[i] = double.NaN;

                    Bottom_1[i] = double.NaN;
                    Bottom_2[i] = double.NaN;
                    Bottom_3[i] = double.NaN;
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

                if (BandsSource_Input == BandsSource_Data.Daily)
                    _ = BandsType_Input switch
                    {
                        BandsType_Data.Percentile => QuantileBands(indexStart, index, DailyVWAP),
                        BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, DailyVWAP),
                        _ => StdDevBands(indexStart, index, DailyVWAP),
                    };                    
            }

            if (ShowWeekly) {
                Bars TF_Bars = Weekly_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_W, CumulVol_W, WeeklyVWAP);

                if (BandsSource_Input == BandsSource_Data.Weekly)
                    _ = BandsType_Input switch
                    {
                        BandsType_Data.Percentile => QuantileBands(indexStart, index, WeeklyVWAP),
                        BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, WeeklyVWAP),
                        _ => StdDevBands(indexStart, index, WeeklyVWAP),
                    };                    
            }

            if (ShowMonthly) {
                Bars TF_Bars = Monthly_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_M, CumulVol_M, MonthlyVWAP);

                if (BandsSource_Input == BandsSource_Data.Monthly)
                    _ = BandsType_Input switch
                    {
                        BandsType_Data.Percentile => QuantileBands(indexStart, index, MonthlyVWAP),
                        BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, MonthlyVWAP),
                        _ => StdDevBands(indexStart, index, MonthlyVWAP),
                    };                    
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
        
        // StdDev Bands + Volume Weighted Bands
        private bool StdDevBands(int indexStart, int index, IndicatorDataSeries seriesVWAP) 
        {
            double squaredErrors = 0;
            double volumeSum = 0.0;
            double periodSum = 0;

            for (int i = indexStart; i <= index; i++)
            {
                double diff = Bars.MedianPrices[i] - seriesVWAP[index];
                double vol = Bars.TickVolumes[i];

                double extra = IsVolumeBands ? vol : 1.0;
                squaredErrors += diff * diff * extra;
                
                periodSum += 1;
                volumeSum += vol;
            }

            if (periodSum == 0)
                return false;

            // Sample => (Period - 1) / Population => Period 
            double cumulValue = IsVolumeBands ? volumeSum : (periodSum - 1);
            double variance = squaredErrors / cumulValue;
            double stdDev = Math.Sqrt(variance);

            Top_1[index] = seriesVWAP[index] + stdDev * StdDev1_Input;
            Top_2[index] = seriesVWAP[index] + stdDev * StdDev2_Input;
            Top_3[index] = seriesVWAP[index] + stdDev * StdDev3_Input;

            Bottom_1[index] = seriesVWAP[index] - stdDev * StdDev1_Input;
            Bottom_2[index] = seriesVWAP[index] - stdDev * StdDev2_Input;
            Bottom_3[index] = seriesVWAP[index] - stdDev * StdDev3_Input;

            return true;
        }

        // Quantile Bands + Volume Weighted Bands
        private bool QuantileBands(int indexStart, int index, IndicatorDataSeries seriesVWAP)
        {
            var distances = new List<double>();
            var volumes   = new List<double>();

            for (int i = indexStart; i <= index; i++)
            {
                double dist = Math.Abs(Bars.MedianPrices[i] - seriesVWAP[index]);
                double vol  = Bars.TickVolumes[i];
                distances.Add(dist);
                volumes.Add(vol);
            }

            if (distances.Count == 0)
                return false;

            double pct1 = GetPercentageDecimal(Pctile1_Input);
            double pct2 = GetPercentageDecimal(Pctile2_Input);
            double pct3 = GetPercentageDecimal(Pctile3_Input);

            double q1 = IsVolumeBands ? WeightedQuantile(distances, volumes, pct1) : Quantile(distances, pct1);
            double q2 = IsVolumeBands ? WeightedQuantile(distances, volumes, pct2) : Quantile(distances, pct2);
            double q3 = IsVolumeBands ? WeightedQuantile(distances, volumes, pct3) : Quantile(distances, pct3);

            Top_1[index] = seriesVWAP[index] + q1;
            Top_2[index] = seriesVWAP[index] + q2;
            Top_3[index] = seriesVWAP[index] + q3;

            Bottom_1[index] = seriesVWAP[index] - q1;
            Bottom_2[index] = seriesVWAP[index] - q2;
            Bottom_3[index] = seriesVWAP[index] - q3;

            return true;
        }
        
        
        // Quantile Asymmetric Bands + Volume Weighted Bands
        private bool QuantileAsymmetricBands(int indexStart, int index, IndicatorDataSeries seriesVWAP)
        {
            var posDistances = new List<double>();
            var posVolumes   = new List<double>();

            var negDistances = new List<double>();
            var negVolumes   = new List<double>();

            for (int i = indexStart; i <= index; i++)
            {
                double diff = Bars.MedianPrices[i] - seriesVWAP[index];
                double vol  = Bars.TickVolumes[i];

                if (diff > 0)
                {
                    posDistances.Add(diff);
                    posVolumes.Add(vol);
                }
                else if (diff < 0)
                {
                    negDistances.Add(-diff); // flip to positive
                    negVolumes.Add(vol);
                }
            }

            // ----- Upper side -----
            if (posDistances.Count > 0)
            {                
                double pct1 = GetPercentageDecimal(Pctile1_Up_Input);
                double pct2 = GetPercentageDecimal(Pctile2_Up_Input);
                double pct3 = GetPercentageDecimal(Pctile3_Up_Input);

                double q1 = IsVolumeBands ? WeightedQuantile(posDistances, posVolumes, pct1) : Quantile(posDistances, pct1);
                double q2 = IsVolumeBands ? WeightedQuantile(posDistances, posVolumes, pct2) : Quantile(posDistances, pct2);
                double q3 = IsVolumeBands ? WeightedQuantile(posDistances, posVolumes, pct3) : Quantile(posDistances, pct3);

                Top_1[index] = seriesVWAP[index] + q1;
                Top_2[index] = seriesVWAP[index] + q2;
                Top_3[index] = seriesVWAP[index] + q3;
            }
            else
            {
                Top_1[index] = seriesVWAP[index];
                Top_2[index] = seriesVWAP[index];
                Top_3[index] = seriesVWAP[index];
            }

            // ----- Lower side -----
            if (negDistances.Count > 0)
            {
                double pct1 = GetPercentageDecimal(Pctile1_Down_Input);
                double pct2 = GetPercentageDecimal(Pctile2_Down_Input);
                double pct3 = GetPercentageDecimal(Pctile3_Down_Input);

                double q1 = IsVolumeBands ? WeightedQuantile(negDistances, negVolumes, pct1) : Quantile(negDistances, pct1);
                double q2 = IsVolumeBands ? WeightedQuantile(negDistances, negVolumes, pct2) : Quantile(negDistances, pct2);
                double q3 = IsVolumeBands ? WeightedQuantile(negDistances, negVolumes, pct3) : Quantile(negDistances, pct3);

                Bottom_1[index] = seriesVWAP[index] - q1;
                Bottom_2[index] = seriesVWAP[index] - q2;
                Bottom_3[index] = seriesVWAP[index] - q3;
            }
            else
            {
                Bottom_1[index] = seriesVWAP[index];
                Bottom_2[index] = seriesVWAP[index];
                Bottom_3[index] = seriesVWAP[index];
            }
            
            return true;
        }

        private static double GetPercentageDecimal(double value) {
            return Math.Round(value / 100, 3);
        }

        private static double Quantile(List<double> data, double q)
        {
            // generated/converted by LLM
            var sorted = data.OrderBy(x => x).ToList();

            double pos = (sorted.Count - 1) * q;
            int idx = (int)pos;
            double frac = pos - idx;

            if (idx + 1 < sorted.Count)
                return sorted[idx] + frac * (sorted[idx + 1] - sorted[idx]);

            return sorted[idx];
        }
        private static double WeightedQuantile(List<double> values, List<double> weights, double q)
        {
            // generated/converted by LLM
            var pairs = values
                .Select((v, i) => new { Value = v, Weight = weights[i] })
                .OrderBy(p => p.Value)
                .ToList();

            double totalWeight = pairs.Sum(p => p.Weight);
            double cumulative = 0.0;
            
            foreach (var p in pairs)
            {
                cumulative += p.Weight;
                if (cumulative / totalWeight >= q)
                    return p.Value;
            }
            
            /*
            // 1-to-1 with Python version
            // same result as above
            double cutoff = q * pairs.Sum(p => p.Weight);
            double cumWeight = 0.0;

            foreach (var p in pairs)
            {
                cumWeight += p.Weight;
                if (cumWeight >= cutoff)
                    return p.Value;
            }
            */

            return pairs.Last().Value;
        }
    }
}
