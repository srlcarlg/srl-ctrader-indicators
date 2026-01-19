/*
--------------------------------------------------------------------------------------------------------------------------------
                      Anchored VWAP Buttons
                              v2.0

Just a Anchored VWAP with 5 Buttons in just 1 indicator

Usage:
Create VWAP: Click on the button and select the bar for the VWAP
Remove VWAP: Click the button again when it is activated.

VWAP will be updated with each new bar

Last update => 19/01/2026
===========================

What's new in version 2.0?
- "Lite-version" of ParamsPanel
- "[Panel] Load Previous Anchors?" standard input.

===========================
AUTHOR: srlcarlg
----------------------------------------------------------------------------------------------------------------------------
*/

using cAlgo.API;
using cAlgo.API.Internals;
using static cAlgo.AnchoredVWAPButtonsV20;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.Json.Nodes;

namespace cAlgo
{
    [Cloud("Upper Band 1", "Upper Band 3")]
    [Cloud("Lower Band 1", "Lower Band 3")]

    [Indicator(IsOverlay = true, AutoRescale = false, AccessRights = AccessRights.None)]
    public class AnchoredVWAPButtonsV20 : Indicator
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
        [Parameter("Buttons Color:", DefaultValue = "98ADD8E6", Group = "==== Anchored VWAP Buttons v2.0 ====")]
        public Color BtnColor { get; set; }
        [Parameter("Buttons Position:", DefaultValue = ConfigButtons_Data.Top_Right, Group = "==== Anchored VWAP Buttons v2.0 ====")]
        public ConfigButtons_Data ConfigButtons_Input { get; set; }
        [Parameter("Buttons Orientation:", DefaultValue = Orientation.Horizontal, Group = "==== Anchored VWAP Buttons v2.0 ====")]
        public Orientation BtnOrientation { get; set; }

        public enum StorageKeyConfig_Data
        {
            Symbol,
            Symbol_Timeframe,
            Broker_Symbol,
            Broker_Symbol_Timeframe,
        }
        [Parameter("Storage By:", DefaultValue = StorageKeyConfig_Data.Broker_Symbol, Group = "==== Anchored VWAP Buttons v2.0 ====")]
        public StorageKeyConfig_Data StorageKeyConfig_Input { get; set; }
        
        

        [Parameter("[Panel] Load Previous Anchors?:", DefaultValue = true, Group = "==== Specific Settings ====")]
        public bool LoadPreviousAnchors { get; set; }
        [Parameter("[DWM] Remove Interval Line? (first bar)", DefaultValue = true, Group = "==== Specific Settings ====")]
        public bool RemoveIntervalLine { get; set; }


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

        // Moved from cTrader Input to Params Panel
        public enum BandsType_Data
        {
            Std_Dev,
            Percentile,
            Percentile_Asymmetric 
        }
        public enum BandsSource_Data
        {
            Anchored,
            Daily,
            Weekly,
            Monthly
        }
        public class GeneralParams_Info {
            public bool ShowDaily = false;
            public bool ShowWeekly = false;
            public bool ShowMonthly = false;
            
            public BandsType_Data BandsType_Input = BandsType_Data.Percentile_Asymmetric;
            public BandsSource_Data BandsSource_Input = BandsSource_Data.Anchored;
            public bool IsVolumeBands = true;
        }
        public readonly GeneralParams_Info GeneralParams = new();

        public class BandsRatioParams_Info {
            public double StdDev1_Input = 0.236;
            public double StdDev2_Input = 1.382;
            public double StdDev3_Input = 2.618;

            
            public double Pctile1_Input = 35;
            public double Pctile2_Input = 85;
            public double Pctile3_Input = 100;
            
            public double Pctile1_Up_Input = 50;
            public double Pctile2_Up_Input = 80;
            public double Pctile3_Up_Input = 100;

            public double Pctile1_Down_Input = 45;
            public double Pctile2_Down_Input = 80;
            public double Pctile3_Down_Input = 100;
        }
        public readonly BandsRatioParams_Info BandsRatioParams = new();


        private bool mouseIsActive;
        private bool btnIsActive;
        private Button currentBtn;
        private ChartVerticalLine verticalLine;
        private readonly int[] buttonsStartIndexes = { 0, 0, 0, 0, 0 };
        private readonly IDictionary<int, Button> buttonsDict = new Dictionary<int, Button>();

        public string[] buttonsStartDates = { "", "", "", "", "" };

        // DWM VWAPs
        public IndicatorDataSeries CumulPriceVol_D;
        public IndicatorDataSeries CumulPriceVol_W;
        public IndicatorDataSeries CumulPriceVol_M;

        public IndicatorDataSeries CumulVol_D;
        public IndicatorDataSeries CumulVol_W;
        public IndicatorDataSeries CumulVol_M;

        private Bars Daily_Bars = null;
        private Bars Weekly_Bars = null;
        private Bars Monthly_Bars = null;

        // Params Panel (mini)
        private Border ParamBorder;
        public class IndicatorParams
        {
            public GeneralParams_Info GeneralParams { get; set; }
            public BandsRatioParams_Info BandsRatioParams { get; set; }
        }

        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "⚙️",
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

            // ===============
            IndicatorParams DefaultParams = new() {
                GeneralParams = GeneralParams,
                BandsRatioParams = BandsRatioParams,
            };

            ParamsPanel ParamPanel = new(this, DefaultParams);

            ParamBorder = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                // ParamsPanel - Lock Width
                Width = 262,
                Child = ParamPanel
            };
            Chart.AddControl(ParamBorder);
            // ===============

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
            AddHiddenButton(stackPanel, BtnColor);

            Chart.AddControl(stackPanel);
            Chart.MouseMove += DrawVerticalLine;
            Chart.MouseDown += AddVWAP;
            Bars.BarOpened += UpdateVWAP;

            CumulPriceVol_D = CreateDataSeries();
            CumulVol_D = CreateDataSeries();

            CumulPriceVol_W = CreateDataSeries();
            CumulVol_W = CreateDataSeries();
            
            CumulPriceVol_M = CreateDataSeries();
            CumulVol_M = CreateDataSeries();

            /*
                No comments needed, everything is self explanatory
            */

            if (LoadPreviousAnchors) 
            {
                for (int btnIndex = 0; btnIndex < 5; btnIndex++)
                {
                    if (buttonsStartDates[btnIndex] == "")
                        continue;

                    buttonsDict[btnIndex].Text = $"{btnIndex}";
                    buttonsDict[btnIndex].IsEnabled = true;
                    
                    int startIndex = Bars.OpenTimes.GetIndexByTime(DateTime.Parse(buttonsStartDates[btnIndex]));
                    buttonsStartIndexes[btnIndex] = startIndex;

                    CreateAnchoredVWWAP(btnIndex, startIndex);
                }
            }
        }

        private void CreateAnchoredVWWAP(int btnIndex, int startIndex)
        {
            double sumHigh = 0.0;
            double sumHL2 = 0.0;
            double sumLow = 0.0;
            double sumVol = 0.0;

            for (int j = startIndex; j < Bars.Count; j++)
            {
                sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                sumVol += Bars.TickVolumes[j];

                void CreateVWAP(IndicatorDataSeries TopVWAP_Series, IndicatorDataSeries MiddleVWAP_Series, IndicatorDataSeries BottomVWAP_Series)
                {
                    TopVWAP_Series[j] = sumHigh / sumVol;
                    MiddleVWAP_Series[j] = sumHL2 / sumVol;
                    BottomVWAP_Series[j] = sumLow / sumVol;
                    // Only ONE Anchored StdDev VWAP
                    if (GeneralParams.BandsSource_Input == BandsSource_Data.Anchored && btnIndex == 0)
                        _ = GeneralParams.BandsType_Input switch
                        {
                            BandsType_Data.Percentile => QuantileBands(startIndex, j, MiddleVWAP_Series),
                            BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(startIndex, j, MiddleVWAP_Series),
                            _ => StdDevBands(startIndex, j, MiddleVWAP_Series),
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
                ClearVWAP(int.Parse(obj.Button.Text));
                obj.Button.Text = "";
                return;
            }
            btnIsActive = true;
            currentBtn = obj.Button;
            obj.Button.IsEnabled = false;
            Chart.DrawStaticText("txt", "Select a bar for VWAP.", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
            if (GeneralParams.BandsSource_Input == BandsSource_Data.Anchored)
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

            int startIndex = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);
            CreateAnchoredVWWAP(btnIndex, startIndex);

            if (buttonsStartIndexes[btnIndex] == 0) {
                buttonsStartIndexes[btnIndex] = startIndex;
                buttonsStartDates[btnIndex] = Bars.OpenTimes[startIndex].ToString();
            }

            if (verticalLine != null)
            {
                Chart.DrawStaticText("txt", "", VerticalAlignment.Top, HorizontalAlignment.Center, Color.LightBlue);
                Chart.RemoveObject("VerticalLine");
                Chart.RemoveObject("txt");
                if (GeneralParams.BandsSource_Input == BandsSource_Data.Anchored)
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
                for (int j = buttonsStartIndexes[btnIndex]; j < Bars.Count; j++)
                {
                    sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                    sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                    sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                    sumVol += Bars.TickVolumes[j];

                    TopVWAP_Series[j] = sumHigh / sumVol;
                    MiddleVWAP_Series[j] = sumHL2 / sumVol;
                    BottomVWAP_Series[j] = sumLow / sumVol;

                    // Only ONE Anchored StdDev VWAP
                    if (GeneralParams.BandsSource_Input == BandsSource_Data.Anchored && btnIndex == 0)
                        _ = GeneralParams.BandsType_Input switch
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
                buttonsStartDates[btnIndex] = "";

                TopVWAP_Series[i] = double.NaN;
                MiddleVWAP_Series[i] = double.NaN;
                BottomVWAP_Series[i] = double.NaN;

                // Only ONE Anchored StdDev VWAP
                if (GeneralParams.BandsSource_Input == BandsSource_Data.Anchored && btnIndex == 0)
                {
                    Top_1[i] = double.NaN;
                    Top_2[i] = double.NaN;
                    Top_3[i] = double.NaN;

                    Bottom_1[i] = double.NaN;
                    Bottom_2[i] = double.NaN;
                    Bottom_3[i] = double.NaN;
                }
            }

            for (int i = buttonsStartIndexes[btnIndex]; i < Bars.Count; i++)
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
            if (GeneralParams.ShowDaily && Daily_Bars == null)
                Daily_Bars ??= MarketData.GetBars(TimeFrame.Daily);
            if (GeneralParams.ShowWeekly && Weekly_Bars == null)
                Weekly_Bars ??= MarketData.GetBars(TimeFrame.Weekly);
            if (GeneralParams.ShowMonthly && Monthly_Bars == null)
                Monthly_Bars ??= MarketData.GetBars(TimeFrame.Monthly);

            if (GeneralParams.ShowDaily) {
                Bars TF_Bars = Daily_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_D, CumulVol_D, DailyVWAP);

                if (GeneralParams.BandsSource_Input == BandsSource_Data.Daily)
                    _ = GeneralParams.BandsType_Input switch
                    {
                        BandsType_Data.Percentile => QuantileBands(indexStart, index, DailyVWAP),
                        BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, DailyVWAP),
                        _ => StdDevBands(indexStart, index, DailyVWAP),
                    };                    
            }

            if (GeneralParams.ShowWeekly) {
                Bars TF_Bars = Weekly_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_W, CumulVol_W, WeeklyVWAP);

                if (GeneralParams.BandsSource_Input == BandsSource_Data.Weekly)
                    _ = GeneralParams.BandsType_Input switch
                    {
                        BandsType_Data.Percentile => QuantileBands(indexStart, index, WeeklyVWAP),
                        BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, WeeklyVWAP),
                        _ => StdDevBands(indexStart, index, WeeklyVWAP),
                    };                    
            }

            if (GeneralParams.ShowMonthly) {
                Bars TF_Bars = Monthly_Bars;
                int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                DWM_VWAP(index, indexStart, CumulPriceVol_M, CumulVol_M, MonthlyVWAP);

                if (GeneralParams.BandsSource_Input == BandsSource_Data.Monthly)
                    _ = GeneralParams.BandsType_Input switch
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

                double extra = GeneralParams.IsVolumeBands ? vol : 1.0;
                squaredErrors += diff * diff * extra;
                
                periodSum += 1;
                volumeSum += vol;
            }

            if (periodSum == 0)
                return false;

            // Sample => (Period - 1) / Population => Period 
            double cumulValue = GeneralParams.IsVolumeBands ? volumeSum : (periodSum - 1);
            double variance = squaredErrors / cumulValue;
            double stdDev = Math.Sqrt(variance);

            Top_1[index] = seriesVWAP[index] + stdDev * BandsRatioParams.StdDev1_Input;
            Top_2[index] = seriesVWAP[index] + stdDev * BandsRatioParams.StdDev2_Input;
            Top_3[index] = seriesVWAP[index] + stdDev * BandsRatioParams.StdDev3_Input;

            Bottom_1[index] = seriesVWAP[index] - stdDev * BandsRatioParams.StdDev1_Input;
            Bottom_2[index] = seriesVWAP[index] - stdDev * BandsRatioParams.StdDev2_Input;
            Bottom_3[index] = seriesVWAP[index] - stdDev * BandsRatioParams.StdDev3_Input;

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

            double pct1 = GetPercentageDecimal(BandsRatioParams.Pctile1_Input);
            double pct2 = GetPercentageDecimal(BandsRatioParams.Pctile2_Input);
            double pct3 = GetPercentageDecimal(BandsRatioParams.Pctile3_Input);

            double q1 = GeneralParams.IsVolumeBands ? WeightedQuantile(distances, volumes, pct1) : Quantile(distances, pct1);
            double q2 = GeneralParams.IsVolumeBands ? WeightedQuantile(distances, volumes, pct2) : Quantile(distances, pct2);
            double q3 = GeneralParams.IsVolumeBands ? WeightedQuantile(distances, volumes, pct3) : Quantile(distances, pct3);

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
                double pct1 = GetPercentageDecimal(BandsRatioParams.Pctile1_Up_Input);
                double pct2 = GetPercentageDecimal(BandsRatioParams.Pctile2_Up_Input);
                double pct3 = GetPercentageDecimal(BandsRatioParams.Pctile3_Up_Input);

                double q1 = GeneralParams.IsVolumeBands ? WeightedQuantile(posDistances, posVolumes, pct1) : Quantile(posDistances, pct1);
                double q2 = GeneralParams.IsVolumeBands ? WeightedQuantile(posDistances, posVolumes, pct2) : Quantile(posDistances, pct2);
                double q3 = GeneralParams.IsVolumeBands ? WeightedQuantile(posDistances, posVolumes, pct3) : Quantile(posDistances, pct3);

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
                double pct1 = GetPercentageDecimal(BandsRatioParams.Pctile1_Down_Input);
                double pct2 = GetPercentageDecimal(BandsRatioParams.Pctile2_Down_Input);
                double pct3 = GetPercentageDecimal(BandsRatioParams.Pctile3_Down_Input);

                double q1 = GeneralParams.IsVolumeBands ? WeightedQuantile(negDistances, negVolumes, pct1) : Quantile(negDistances, pct1);
                double q2 = GeneralParams.IsVolumeBands ? WeightedQuantile(negDistances, negVolumes, pct2) : Quantile(negDistances, pct2);
                double q3 = GeneralParams.IsVolumeBands ? WeightedQuantile(negDistances, negVolumes, pct3) : Quantile(negDistances, pct3);

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

        public void ClearAndRecalculate() {
            if (GeneralParams.ShowDaily && Daily_Bars == null)
                Daily_Bars ??= MarketData.GetBars(TimeFrame.Daily);
            if (GeneralParams.ShowWeekly && Weekly_Bars == null)
                Weekly_Bars ??= MarketData.GetBars(TimeFrame.Weekly);
            if (GeneralParams.ShowMonthly && Monthly_Bars == null)
                Monthly_Bars ??= MarketData.GetBars(TimeFrame.Monthly);

            if (!GeneralParams.ShowDaily && !double.IsNaN(DailyVWAP.LastValue))
                resetVWAP(DailyVWAP);
            if (!GeneralParams.ShowWeekly && !double.IsNaN(WeeklyVWAP.LastValue))
                resetVWAP(WeeklyVWAP);
            if (!GeneralParams.ShowMonthly && !double.IsNaN(MonthlyVWAP.LastValue))
                resetVWAP(MonthlyVWAP);
            
            // Just cover all possibilities
            if (!double.IsNaN(Top_1.LastValue))
                resetBands();

            // Anchored
            UpdateVWAP(null);
            
            // DWM
            for (int index = 0; index < Bars.Count; index++)
            {    
                if (GeneralParams.ShowDaily) {
                    Bars TF_Bars = Daily_Bars;
                    int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                    DWM_VWAP(index, indexStart, CumulPriceVol_D, CumulVol_D, DailyVWAP);

                    if (GeneralParams.BandsSource_Input == BandsSource_Data.Daily)
                        _ = GeneralParams.BandsType_Input switch 
                        {
                            BandsType_Data.Percentile => QuantileBands(indexStart, index, DailyVWAP),
                            BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, DailyVWAP),
                            _ => StdDevBands(indexStart, index, DailyVWAP),
                        };                    
                }
                
                if (GeneralParams.ShowWeekly) {
                    Bars TF_Bars = Weekly_Bars;
                    int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                    DWM_VWAP(index, indexStart, CumulPriceVol_W, CumulVol_W, WeeklyVWAP);

                    if (GeneralParams.BandsSource_Input == BandsSource_Data.Weekly)
                        _ = GeneralParams.BandsType_Input switch
                        {
                            BandsType_Data.Percentile => QuantileBands(indexStart, index, WeeklyVWAP),
                            BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, WeeklyVWAP),
                            _ => StdDevBands(indexStart, index, WeeklyVWAP),
                        };                    
                }

                if (GeneralParams.ShowMonthly) {
                    Bars TF_Bars = Monthly_Bars;
                    int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                    int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

                    DWM_VWAP(index, indexStart, CumulPriceVol_M, CumulVol_M, MonthlyVWAP);

                    if (GeneralParams.BandsSource_Input == BandsSource_Data.Monthly)
                    _ = GeneralParams.BandsType_Input switch
                    {
                        BandsType_Data.Percentile => QuantileBands(indexStart, index, MonthlyVWAP),
                        BandsType_Data.Percentile_Asymmetric => QuantileAsymmetricBands(indexStart, index, MonthlyVWAP),
                        _ => StdDevBands(indexStart, index, MonthlyVWAP),
                    };                    
                }
            }
            void resetVWAP(IndicatorDataSeries _Series)
            {
                for (int i = 0; i < Bars.Count; i++)
                    _Series[i] = double.NaN;
            }
            void resetBands()
            {
                for (int i = 0; i < Bars.Count; i++) {
                    Top_1[i] = double.NaN;
                    Top_2[i] = double.NaN;
                    Top_3[i] = double.NaN;

                    Bottom_1[i] = double.NaN;
                    Bottom_2[i] = double.NaN;
                    Bottom_3[i] = double.NaN;
                }
            }
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

    public class ParamsPanel : CustomControl
    {
        private readonly AnchoredVWAPButtonsV20 Outside;
        private readonly IndicatorParams FirstParams;
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

        public ParamsPanel(AnchoredVWAPButtonsV20 indicator, IndicatorParams defaultParams)
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
            bool isStdDev() => Outside.GeneralParams.BandsType_Input == BandsType_Data.Std_Dev;
            bool isPctile() => Outside.GeneralParams.BandsType_Input == BandsType_Data.Percentile;
            bool isAsym() => Outside.GeneralParams.BandsType_Input == BandsType_Data.Percentile_Asymmetric;
            return new List<ParamDefinition>
            {
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "EnableDailyKey",
                    Label = "Daily?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.GeneralParams.ShowDaily,
                    OnChanged = _ => UpdateCheckbox("EnableDailyKey", val => Outside.GeneralParams.ShowDaily = val),
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "EnableWeeklyKey",
                    Label = "Weekly?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.GeneralParams.ShowWeekly,
                    OnChanged = _ => UpdateCheckbox("EnableWeeklyKey", val => Outside.GeneralParams.ShowWeekly = val),
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "EnableMonthlyKey",
                    Label = "Monthly?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.GeneralParams.ShowMonthly,
                    OnChanged = _ => UpdateCheckbox("EnableMonthlyKey", val => Outside.GeneralParams.ShowMonthly = val),
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "BandsSourceKey",
                    Label = "Bands(source)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.GeneralParams.BandsSource_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BandsSource_Data)),
                    OnChanged = _ => UpdateBandsSource(),
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "BandsTypeKey",
                    Label = "Bands(type)",
                    InputType = ParamInputType.ComboBox,
                    GetDefault = p => p.GeneralParams.BandsType_Input.ToString(),
                    EnumOptions = () => Enum.GetNames(typeof(BandsType_Data)),
                    OnChanged = _ => UpdateBandsType(),
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "BandsVolumeKey",
                    Label = "Bands(volume)?",
                    InputType = ParamInputType.Checkbox,
                    GetDefault = p => p.GeneralParams.IsVolumeBands,
                    OnChanged = _ => UpdateCheckbox("BandsVolumeKey", val => Outside.GeneralParams.IsVolumeBands = val),
                },

                
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "StdDevFirstKey",
                    Label = "1º Band(*)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.StdDev1_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateFirst_StdDev(),
                    IsVisible = () => isStdDev()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "StdDevSecondKey",
                    Label = "2º Band(*)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.StdDev2_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSecond_StdDev(),
                    IsVisible = () => isStdDev()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "StdDevThirdKey",
                    Label = "3º Band(*)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.StdDev3_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateThird_StdDev(),
                    IsVisible = () => isStdDev()
                },
                
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "PctileFirstKey",
                    Label = "1º Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile1_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateFirst_Pctile(),
                    IsVisible = () => isPctile()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "PctileSecondKey",
                    Label = "2º Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile2_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSecond_Pctile(),
                    IsVisible = () => isPctile()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "PctileThirdKey",
                    Label = "3º Band(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile3_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateThird_Pctile(),
                    IsVisible = () => isPctile()
                },

                
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "AsymUpper1Key",
                    Label = "1º Upper(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile1_Up_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateFirstUpper_Asym(),
                    IsVisible = () => isAsym()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "AsymUpper2Key",
                    Label = "2º Upper(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile2_Up_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSecondUpper_Asym(),
                    IsVisible = () => isAsym()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "AsymUpper3Key",
                    Label = "3º Upper(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile3_Up_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateThirdUpper_Asym(),
                    IsVisible = () => isAsym()
                },

                
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "AsymBottom1Key",
                    Label = "1º Bottom(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile1_Down_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateFirstBottom_Asym(),
                    IsVisible = () => isAsym()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "AsymBottom2Key",
                    Label = "2º Bottom(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile2_Down_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateSecondBottom_Asym(),
                    IsVisible = () => isAsym()
                },
                new()
                {
                    Region = "General",
                    RegionOrder = 0,
                    Key = "AsymBottom3Key",
                    Label = "3º Bottom(%)",
                    InputType = ParamInputType.Text,
                    GetDefault = p => p.BandsRatioParams.Pctile3_Down_Input.ToString("0.############################", CultureInfo.InvariantCulture),
                    OnChanged = _ => UpdateThirdBottom_Asym(),
                    IsVisible = () => isAsym()
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
                Text = "Anchored VWAP Buttons",
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
            var grid = new Grid(1, 5);
            grid.Columns[1].SetWidthInPixels(5);
            grid.Columns[3].SetWidthInPixels(5);

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

        // ==== General ====
        private void UpdateBandsSource()
        {
            var selected = comboBoxMap["BandsSourceKey"].SelectedItem;
            if (Enum.TryParse(selected, out BandsSource_Data sourceType) && sourceType != Outside.GeneralParams.BandsSource_Input)
            {
                Outside.GeneralParams.BandsSource_Input = sourceType;
                RecalculateOutsideWithMsg();
            }
        }
        private void UpdateBandsType()
        {
            var selected = comboBoxMap["BandsTypeKey"].SelectedItem;
            if (Enum.TryParse(selected, out BandsType_Data numbersType) && numbersType != Outside.GeneralParams.BandsType_Input)
            {
                Outside.GeneralParams.BandsType_Input = numbersType;
                RecalculateOutsideWithMsg();
            }
        }

        private void UpdateFirst_StdDev()
        {
            if (double.TryParse(textInputMap["StdDevFirstKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.StdDev1_Input)
                {
                    Outside.BandsRatioParams.StdDev1_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateSecond_StdDev()
        {
            if (double.TryParse(textInputMap["StdDevSecondKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.StdDev2_Input)
                {
                    Outside.BandsRatioParams.StdDev2_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateThird_StdDev()
        {
            if (double.TryParse(textInputMap["StdDevThirdKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.StdDev3_Input)
                {
                    Outside.BandsRatioParams.StdDev3_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }


        private void UpdateFirst_Pctile()
        {
            if (double.TryParse(textInputMap["PctileFirstKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile1_Input)
                {
                    Outside.BandsRatioParams.Pctile1_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateSecond_Pctile()
        {
            if (double.TryParse(textInputMap["PctileSecondKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile2_Input)
                {
                    Outside.BandsRatioParams.Pctile2_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateThird_Pctile()
        {
            if (double.TryParse(textInputMap["PctileThirdKey"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile3_Input)
                {
                    Outside.BandsRatioParams.Pctile3_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }

        
        private void UpdateFirstUpper_Asym()
        {
            if (double.TryParse(textInputMap["AsymUpper1Key"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile1_Up_Input)
                {
                    Outside.BandsRatioParams.Pctile1_Up_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateSecondUpper_Asym()
        {
            if (double.TryParse(textInputMap["AsymUpper2Key"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile2_Up_Input)
                {
                    Outside.BandsRatioParams.Pctile2_Up_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateThirdUpper_Asym()
        {
            if (double.TryParse(textInputMap["AsymUpper3Key"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile3_Up_Input)
                {
                    Outside.BandsRatioParams.Pctile3_Up_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }

        private void UpdateFirstBottom_Asym()
        {
            if (double.TryParse(textInputMap["AsymBottom1Key"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile1_Down_Input)
                {
                    Outside.BandsRatioParams.Pctile1_Down_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateSecondBottom_Asym()
        {
            if (double.TryParse(textInputMap["AsymBottom2Key"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile2_Down_Input)
                {
                    Outside.BandsRatioParams.Pctile2_Down_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void UpdateThirdBottom_Asym()
        {
            if (double.TryParse(textInputMap["AsymBottom3Key"].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                if (value != Outside.BandsRatioParams.Pctile3_Down_Input)
                {
                    Outside.BandsRatioParams.Pctile3_Up_Input = value;
                    ApplyBtn.IsVisible = true;
                }
            }
        }
        private void RecalculateOutsideWithMsg(bool reset = true)
        {
            // Avoid multiples calls when loading parameters from LocalStorage
            if (isLoadingParams)
                return;

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
                    ParamInputType.Text => textInputMap[param.Key].Text,
                    ParamInputType.Checkbox => checkBoxMap[param.Key].IsChecked ?? false,
                    ParamInputType.ComboBox => comboBoxMap[param.Key].SelectedItem,
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

            string dynamicKey = Outside.StorageKeyConfig_Input switch {
                StorageKeyConfig_Data.Symbol => $"AnchVWAP {SymbolPrefix}",
                StorageKeyConfig_Data.Symbol_Timeframe => $"AnchVWAP {SymbolPrefix} {TimeframePrefix}",
                StorageKeyConfig_Data.Broker_Symbol => $"AnchVWAP {BrokerPrefix} {SymbolPrefix}",
                _ => $"AnchVWAP {BrokerPrefix} {SymbolPrefix} {TimeframePrefix}",
            };
            return dynamicKey;
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
            storageModel.Params["PrevAnchors"] = Outside.buttonsStartDates;
            
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
            if (Outside.LoadPreviousAnchors) {
                string volModeText = storageModel.Params["PrevAnchors"].ToString(); // if 'var' is used => 'Newtonsoft.Json.Linq.JArray'
                JsonNode jsonObject = JsonNode.Parse(volModeText);;
                
                string[] prevDates = { (string)jsonObject[0], (string)jsonObject[1], (string)jsonObject[2], (string)jsonObject[3], (string)jsonObject[4] };
                Outside.buttonsStartDates = prevDates;
            }

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
