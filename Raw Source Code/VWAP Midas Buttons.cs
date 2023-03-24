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
        
        [Parameter("Buttons Color:", DefaultValue = Colors.LightBlue, Group = "==== VWAP Midas Buttons ====")]
        public Colors RawBtnColor { get; set; }
        
        [Parameter("Buttons Opacity:" , DefaultValue = 50, MinValue = 5, MaxValue = 100, Group = "==== VWAP Midas Buttons ====")]
        public int BtnOpacity { get; set; }
        
        [Parameter("Buttons Position:", DefaultValue = ConfigButtonsData.Bottom_Left, Group = "==== VWAP Midas Buttons ====")]
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
        
        private int[] btnIndexes = {0,0,0,0,0};
        private bool mouseActive;
        private bool btnActive;
        private Button currentBtn;
        private ChartVerticalLine verticalLine;
        private IDictionary<int, Button> allButtons = new Dictionary<int, Button>();
        
        protected override void Initialize()
        {
            var btnOpacity = (int)(2.55 * BtnOpacity);
            Color rawColor = Color.FromName(RawBtnColor.ToString());
            var btnColor = Color.FromArgb(btnOpacity, rawColor.R, rawColor.G, rawColor.B);
            
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
            
            for (int i=1; i < 6; i++)
                AddButton(wrapPanel, btnColor, i);
                
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
            Button button = new Button 
            {
                Text = "",
                Padding = 0,
                Width = 22,
                Height = 22,
                Margin = 2,
                BackgroundColor = btnColor
            };

            button.Click += Button_Click;
            panel.AddChild(button);
            allButtons.Add(btnIndex, button);
        }
        private void Button_Click(ButtonClickEventArgs obj)
        {
            if (obj.Button.Text != "")
            {
                ClearVWAP(Convert.ToInt32(obj.Button.Text));
                obj.Button.Text = "";
                return;
            }
            btnActive=true; 
            currentBtn= obj.Button;
            obj.Button.IsEnabled = false;
            Chart.DrawStaticText("txt", "Select a bar for VWAP.", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Orange);
        }
        public void DrawVerticalLine(ChartMouseEventArgs obj)
        {
            if (btnActive)
            {
                mouseActive = true;

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
            if (mouseActive == false)
                return;
                
            mouseActive = false;
            
            int btnIndex = 0;
            for (int i=1; i < 6; i++)
            {
                if (allButtons[i] == currentBtn)
                {
                    allButtons[i].Text = $"{i}";
                    allButtons[i].IsEnabled = true;
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
                
                if (btnIndex == 1)
                {
                    TopVWAP[j] = sumHigh / sumVol;
                    MiddleVWAP[j] = sumHL2 / sumVol;
                    BottomVWAP[j] = sumLow / sumVol;
                }
                else if (btnIndex == 2)
                {
                    TopVWAP_2[j] = sumHigh / sumVol;
                    MiddleVWAP_2[j] = sumHL2 / sumVol;
                    BottomVWAP_2[j] = sumLow / sumVol;
                }
                else if (btnIndex == 3)
                {
                    TopVWAP_3[j] = sumHigh / sumVol;
                    MiddleVWAP_3[j] = sumHL2 / sumVol;
                    BottomVWAP_3[j] = sumLow / sumVol;
                }
                else if (btnIndex == 4)
                {
                    TopVWAP_4[j] = sumHigh / sumVol;
                    MiddleVWAP_4[j] = sumHL2 / sumVol;
                    BottomVWAP_4[j] = sumLow / sumVol;
                }
                else if (btnIndex == 5)
                {
                    TopVWAP_5[j] = sumHigh / sumVol;
                    MiddleVWAP_5[j] = sumHL2 / sumVol;
                    BottomVWAP_5[j] = sumLow / sumVol;
                }
            }
            
            for (int i=1; i < 6; i++)
            {
                if (i == 1 && btnIndexes[0] == 0 && allButtons[i].Text != "")
                    btnIndexes[0] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);       
                if (i == 2 && btnIndexes[1] == 0 && allButtons[i].Text != "")
                    btnIndexes[1] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);  
                if (i == 3 && btnIndexes[2] == 0 && allButtons[i].Text != "")
                    btnIndexes[2] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);  
                if (i == 4 && btnIndexes[3] == 0 && allButtons[i].Text != "")
                    btnIndexes[3] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);  
                if (i == 5 && btnIndexes[4] == 0 && allButtons[i].Text != "")
                    btnIndexes[4] = Bars.OpenTimes.GetIndexByTime(verticalLine.Time);  
            }
            
            if (verticalLine != null)
            {
                Chart.DrawStaticText("txt", "", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Orange);
                Chart.RemoveObject("VerticalLine");
                Chart.RemoveObject("txt");
                verticalLine = null;
                btnActive = false;
            }

        }
        
        private void UpdateVWAP(BarOpenedEventArgs obj)
        {
            int[] btnActives = {0,0,0,0,0};
            for (int i=1; i < 6; i++)
            {
                if (i == 1 && allButtons[i].Text != "")
                    btnActives[0] = 1;       
                if (i == 2 && allButtons[i].Text != "")
                    btnActives[1] = 1;  
                if (i == 3 && allButtons[i].Text != "")
                    btnActives[2] = 1;  
                if (i == 4 && allButtons[i].Text != "")
                    btnActives[3] = 1;  
                if (i == 5 && allButtons[i].Text != "")
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
                if (btnIndex == 1)
                {
                    btnIndexes[0] = 0;
                    TopVWAP[i] = double.NaN;
                    MiddleVWAP[i] = double.NaN;
                    BottomVWAP[i] = double.NaN;
                }
                else if (btnIndex == 2)
                {
                    btnIndexes[1] = 0;
                    TopVWAP_2[i] = double.NaN;
                    MiddleVWAP_2[i] = double.NaN;
                    BottomVWAP_2[i] = double.NaN;
                }
                else if (btnIndex == 3)
                {
                    btnIndexes[2] = 0;
                    TopVWAP_3[i] = double.NaN;
                    MiddleVWAP_3[i] = double.NaN;
                    BottomVWAP_3[i] = double.NaN;
                }
                else if (btnIndex == 4)
                {
                    btnIndexes[3] = 0;
                    TopVWAP_4[i] = double.NaN;
                    MiddleVWAP_4[i] = double.NaN;
                    BottomVWAP_4[i] = double.NaN;
                }
                else if (btnIndex == 5)
                {
                    btnIndexes[4] = 0;
                    TopVWAP_5[i] = double.NaN;
                    MiddleVWAP_5[i] = double.NaN;
                    BottomVWAP_5[i] = double.NaN;
                }
            }
        }
        public override void Calculate(int index)
        {
            // Really?
        }
    }
}