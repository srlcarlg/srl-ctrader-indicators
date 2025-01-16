/*
--------------------------------------------------------------------------------------------------------------------------------
                      VWAP Midas Buttons
Just a VWAP Midas with 5 Buttons in just 1 indicator

Usage:
Create VWAP: Click on the button and select the bar for the VWAP
Remove VWAP: Click the button again when it is activated.

VWAP will be updated with each new bar

AUTHOR: srlcarlg
----------------------------------------------------------------------------------------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class VWAPMidasButtons : Indicator
    {
        public enum ConfigButtonsData
        {
            Top_Right,
            Top_Left,
            Bottom_Right,
            Bottom_Left,
        }

        [Parameter("Buttons Color:", DefaultValue = "98ADD8E6", Group = "==== VWAP Midas Buttons ====")]
        public Color BtnColor { get; set; }

        [Parameter("Buttons Position:", DefaultValue = ConfigButtonsData.Top_Right, Group = "==== VWAP Midas Buttons ====")]
        public ConfigButtonsData ConfigButtonsInput { get; set; }

        [Parameter("Buttons Orientation:", DefaultValue = Orientation.Horizontal, Group = "==== VWAP Midas Buttons ====")]
        public Orientation BtnOrientation { get; set; }

        [Output("Top VWAP", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP { get; set; }
        [Output("Top VWAP 2", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_2 { get; set; }
        [Output("Top VWAP 3", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_3 { get; set; }
        [Output("Top VWAP 4", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_4 { get; set; }
        [Output("Top VWAP 5", LineColor = "DeepSkyBlue", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries TopVWAP_5 { get; set; }

        [Output("Middle VWAP", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP { get; set; }
        [Output("Middle VWAP 2", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_2 { get; set; }
        [Output("Middle VWAP 3", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_3 { get; set; }
        [Output("Middle VWAP 4", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_4 { get; set; }
        [Output("Middle VWAP 5", LineColor = "LightYellow", LineStyle = LineStyle.Lines, PlotType = PlotType.Line)]
        public IndicatorDataSeries MiddleVWAP_5 { get; set; }

        [Output("Bottom VWAP", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP { get; set; }
        [Output("Bottom VWAP 2", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_2 { get; set; }
        [Output("Bottom VWAP 3", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_3 { get; set; }
        [Output("Bottom VWAP 4", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_4 { get; set; }
        [Output("Bottom VWAP 5", LineColor = "Orange", LineStyle = LineStyle.Solid, PlotType = PlotType.Line)]
        public IndicatorDataSeries BottomVWAP_5 { get; set; }

        private readonly int[] btnIndexes = { 0, 0, 0, 0, 0 };
        private bool mouseIsActive;
        private bool btnIsActive;
        private Button currentBtn;
        private readonly IDictionary<int, Button> ButtonsDict = new Dictionary<int, Button>();
        private ChartVerticalLine verticalLine;

        protected override void Initialize()
        {
            VerticalAlignment v_align = VerticalAlignment.Bottom;
            HorizontalAlignment h_align = HorizontalAlignment.Left;
            if (ConfigButtonsInput == ConfigButtonsData.Bottom_Right)
                h_align = HorizontalAlignment.Right;
            else if (ConfigButtonsInput == ConfigButtonsData.Top_Left)
                v_align = VerticalAlignment.Top;
            else if (ConfigButtonsInput == ConfigButtonsData.Top_Right)
            {
                v_align = VerticalAlignment.Top;
                h_align = HorizontalAlignment.Right;
            }

            var wrapPanel = new WrapPanel
            {
                HorizontalAlignment = h_align,
                VerticalAlignment = v_align,
                Orientation = BtnOrientation,
            };

            for (int i = 1; i < 6; i++)
                AddButton(wrapPanel, BtnColor, i);

            Chart.AddControl(wrapPanel);

            Chart.MouseMove += DrawVerticalLine;
            Chart.MouseDown += AddVWAP;

            Bars.BarOpened += UpdateVWAP;
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
            ButtonsDict.Add(btnIndex, button);
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
            Chart.DrawStaticText("txt", "Select a bar for VWAP.", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Orange);
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
            if (mouseIsActive == false)
                return;

            mouseIsActive = false;

            int btnIndex = 0;
            for (int i = 1; i < 6; i++)
            {
                if (ButtonsDict[i] == currentBtn)
                {
                    ButtonsDict[i].Text = $"{i}";
                    ButtonsDict[i].IsEnabled = true;
                    btnIndex = i;
                    break;
                }
            }

            double sumHigh = 0.0;
            double sumHL2 = 0.0;
            double sumLow = 0.0;
            double sumVol = 0.0;
            for (int j = Bars.OpenTimes.GetIndexByTime(verticalLine.Time); j < Chart.BarsTotal; j++)
            {
                sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                sumVol += Bars.TickVolumes[j];

                switch (btnIndex)
                {
                    case 1:
                        TopVWAP[j] = sumHigh / sumVol;
                        MiddleVWAP[j] = sumHL2 / sumVol;
                        BottomVWAP[j] = sumLow / sumVol;
                        break;
                    case 2:
                        TopVWAP_2[j] = sumHigh / sumVol;
                        MiddleVWAP_2[j] = sumHL2 / sumVol;
                        BottomVWAP_2[j] = sumLow / sumVol;
                        break;
                    case 3:
                        TopVWAP_3[j] = sumHigh / sumVol;
                        MiddleVWAP_3[j] = sumHL2 / sumVol;
                        BottomVWAP_3[j] = sumLow / sumVol;
                        break;
                    case 4:
                        TopVWAP_4[j] = sumHigh / sumVol;
                        MiddleVWAP_4[j] = sumHL2 / sumVol;
                        BottomVWAP_4[j] = sumLow / sumVol;
                        break;
                    case 5:
                        TopVWAP_5[j] = sumHigh / sumVol;
                        MiddleVWAP_5[j] = sumHL2 / sumVol;
                        BottomVWAP_5[j] = sumLow / sumVol;
                        break;
                }
            }

            for (int i = 1; i < 6; i++)
            {
                if (i == 1 && btnIndexes[0] == 0 && ButtonsDict[i].Text != "")
                    btnIndexes[0] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);
                if (i == 2 && btnIndexes[1] == 0 && ButtonsDict[i].Text != "")
                    btnIndexes[1] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);
                if (i == 3 && btnIndexes[2] == 0 && ButtonsDict[i].Text != "")
                    btnIndexes[2] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);
                if (i == 4 && btnIndexes[3] == 0 && ButtonsDict[i].Text != "")
                    btnIndexes[3] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);
                if (i == 5 && btnIndexes[4] == 0 && ButtonsDict[i].Text != "")
                    btnIndexes[4] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);
            }

            if (verticalLine != null)
            {
                Chart.DrawStaticText("txt", "", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Orange);
                Chart.RemoveObject("VerticalLine");
                Chart.RemoveObject("txt");
                verticalLine = null;
                btnIsActive = false;
            }

        }

        private void UpdateVWAP(BarOpenedEventArgs obj)
        {
            int[] btnActives = { 0, 0, 0, 0, 0 };
            for (int i = 1; i < 6; i++)
            {
                if (i == 1 && ButtonsDict[i].Text != "")
                    btnActives[0] = 1;
                if (i == 2 && ButtonsDict[i].Text != "")
                    btnActives[1] = 1;
                if (i == 3 && ButtonsDict[i].Text != "")
                    btnActives[2] = 1;
                if (i == 4 && ButtonsDict[i].Text != "")
                    btnActives[3] = 1;
                if (i == 5 && ButtonsDict[i].Text != "")
                    btnActives[4] = 1;
            }
            if (btnActives[0] == 1)
            {
                double sumHigh = 0.0;
                double sumHL2 = 0.0;
                double sumLow = 0.0;
                double sumVol = 0.0;
                for (int j = btnIndexes[0]; j < Chart.BarsTotal; j++)
                {
                    sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                    sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                    sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                    sumVol += Bars.TickVolumes[j];

                    TopVWAP[j] = sumHigh / sumVol;
                    MiddleVWAP[j] = sumHL2 / sumVol;
                    BottomVWAP[j] = sumLow / sumVol;
                }
            }
            if (btnActives[1] == 1)
            {
                double sumHigh = 0.0;
                double sumHL2 = 0.0;
                double sumLow = 0.0;
                double sumVol = 0.0;
                for (int j = btnIndexes[1]; j < Chart.BarsTotal; j++)
                {
                    sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                    sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                    sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                    sumVol += Bars.TickVolumes[j];

                    TopVWAP_2[j] = sumHigh / sumVol;
                    MiddleVWAP_2[j] = sumHL2 / sumVol;
                    BottomVWAP_2[j] = sumLow / sumVol;
                }
            }
            if (btnActives[2] == 1)
            {
                double sumHigh = 0.0;
                double sumHL2 = 0.0;
                double sumLow = 0.0;
                double sumVol = 0.0;
                for (int j = btnIndexes[2]; j < Chart.BarsTotal; j++)
                {
                    sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                    sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                    sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                    sumVol += Bars.TickVolumes[j];

                    TopVWAP_3[j] = sumHigh / sumVol;
                    MiddleVWAP_3[j] = sumHL2 / sumVol;
                    BottomVWAP_3[j] = sumLow / sumVol;
                }
            }
            if (btnActives[3] == 1)
            {
                double sumHigh = 0.0;
                double sumHL2 = 0.0;
                double sumLow = 0.0;
                double sumVol = 0.0;
                for (int j = btnIndexes[3]; j < Chart.BarsTotal; j++)
                {
                    sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                    sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                    sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                    sumVol += Bars.TickVolumes[j];

                    TopVWAP_4[j] = sumHigh / sumVol;
                    MiddleVWAP_4[j] = sumHL2 / sumVol;
                    BottomVWAP_4[j] = sumLow / sumVol;
                }
            }
            if (btnActives[4] == 1)
            {
                double sumHigh = 0.0;
                double sumHL2 = 0.0;
                double sumLow = 0.0;
                double sumVol = 0.0;
                for (int j = btnIndexes[4]; j < Chart.BarsTotal; j++)
                {
                    sumHigh += Bars.HighPrices[j] * Bars.TickVolumes[j];
                    sumHL2 += Bars.MedianPrices[j] * Bars.TickVolumes[j];
                    sumLow += Bars.LowPrices[j] * Bars.TickVolumes[j];
                    sumVol += Bars.TickVolumes[j];

                    TopVWAP_5[j] = sumHigh / sumVol;
                    MiddleVWAP_5[j] = sumHL2 / sumVol;
                    BottomVWAP_5[j] = sumLow / sumVol;
                }
            }
        }

        private void ClearVWAP(int btnIndex)
        {
            for (int i = 0; i < Chart.BarsTotal; i++)
            {
                switch (btnIndex)
                {
                    case 1:
                        btnIndexes[0] = 0;
                        TopVWAP[i] = double.NaN;
                        MiddleVWAP[i] = double.NaN;
                        BottomVWAP[i] = double.NaN;
                        break;
                    case 2:
                        btnIndexes[1] = 0;
                        TopVWAP_2[i] = double.NaN;
                        MiddleVWAP_2[i] = double.NaN;
                        BottomVWAP_2[i] = double.NaN;
                        break;
                    case 3:
                        btnIndexes[2] = 0;
                        TopVWAP_3[i] = double.NaN;
                        MiddleVWAP_3[i] = double.NaN;
                        BottomVWAP_3[i] = double.NaN;
                        break;
                    case 4:
                        btnIndexes[3] = 0;
                        TopVWAP_4[i] = double.NaN;
                        MiddleVWAP_4[i] = double.NaN;
                        BottomVWAP_4[i] = double.NaN;
                        break;
                    case 5:
                        btnIndexes[4] = 0;
                        TopVWAP_5[i] = double.NaN;
                        MiddleVWAP_5[i] = double.NaN;
                        BottomVWAP_5[i] = double.NaN;
                        break;
                }
            }
        }
        public override void Calculate(int index)
        {
            // Really?
        }
    }
}
