/*
--------------------------------------------------------------------------------------------------------------------------------
                    [Renko] Weis & Wyckoff System 
showcases the concepts of David H. Weis and Richard Wyckoff on Renko Chart

It's just a way of visualizing the Waves and Volume numerically, it's not an original idea.
You can find this way of visualization first at (www.youtube.com/watch?v=uzISUr1itWg, most recent www.vimeo.com/394541866)

This uses the code concepts of (Numbers-Renko 数字練行足 https://www.tradingview.com/script/9BKOIhdl-Numbers-Renko/ in PineScript),
Cheers to the akutsusho!.
I IMPROVED IT and BROUGHT IT to cTrader/C#.

I added many other features based on the original design and my personal taste, like:

(Make your favorite design template yourself): 14 design parameters with a total of 32 sub-options
(Non-Repaint and Repaint Weis Waves Option): You can choose whether to see the Current Trend Wave value.
(Dynamic TimeLapse): Time Waves showed the difference in milliseconds, seconds, minutes, hours, days!
And Many Others...

For Better Performance, Recompile it on cTrader with .NET 6.0 instead .NET 4.x.

=========================================================================

                "Talk is cheap. Show me the code." 
                         Linus Torvalds
                         
=========================================================================

              Transcribed & Improved for cTrader/C# 
                          by srlcarlg

        Original Code Concepts in TradingView/Pinescript 
                          by akutsusho
                          
=========================================================================

== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AutoRescale = true, AccessRights = AccessRights.None)]
    public class RenkoWeisWyckoffSystem : Indicator
    {
        public enum LoadFromData
        {
            Today,
            Yesterday,
            One_Week,
            Two_Week,
            Monthly,
            Custom
        }

        [Parameter("Load From:", DefaultValue = LoadFromData.Today, Group = "==== [Renko] Weis & Wyckoff System ====")]
        public LoadFromData LoadFromInput { get; set; }

        [Parameter("Custom (dd/mm/yyyy):", DefaultValue = "00/00/0000", Group = "==== [Renko] Weis & Wyckoff System ====")]
        public string StringDate { get; set; }
        
        [Parameter("Nº Bars to Show:", DefaultValue = -1, MinValue = -1, Group = "==== [Renko] Weis & Wyckoff System ====")]
        public int Lookback { get; set; }

        [Parameter("Show Wicks?", DefaultValue = false, Group = "==== [Renko] Weis & Wyckoff System ====")]
        public bool ShowWicks { get; set; }

        [Parameter("Wicks Thickness:", DefaultValue = 1, MaxValue = 5, Group = "==== [Renko] Weis & Wyckoff System ====")]
        public int Thickness { get; set; }

        public enum NumbersBarData
        {
            Both,
            Volume,
            Time
        }
        public enum NumbersBothPositionData
        {
            Default,
            Invert,
        }
        public enum NumbersBarPositionData
        {
            Inside,
            Outside,
        }
        public enum NumbersColorData
        {
            Volume,
            Time,
            CustomColor
        }
        public enum BarsColorData
        {
            Volume,
            Time,
        }
        public enum DigitsToView
        {
            All,
            _4_Digits,
            _3_Digits,
        }

        [Parameter("Show Numbers:", DefaultValue = NumbersBarData.Volume, Group = "==== Numerical Renko Bars ====")]
        public NumbersBarData ShowNumbersInput { get; set; }

        [Parameter("Numbers Color:", DefaultValue = NumbersColorData.Volume, Group = "==== Numerical Renko Bars ====")]
        public NumbersColorData NumbersColorInput { get; set; }
        
        [Parameter("CustomColor:", DefaultValue = Colors.White, Group = "Numerical Renko Bars")]
        public Colors CustomNumbersColor { get; set; }

        [Parameter("Numbers Both Sequence:", DefaultValue = NumbersBothPositionData.Default, Group = "==== Numerical Renko Bars ====")]
        public NumbersBothPositionData NumbersBothPositionInput { get; set; }

        [Parameter("Numbers Bar Position:", DefaultValue = NumbersBarPositionData.Inside, Group = "==== Numerical Renko Bars ====")]
        public NumbersBarPositionData NumbersPositionInput { get; set; }

        [Parameter("Show Only Large Numbers:", DefaultValue = false, Group = "==== Numerical Renko Bars ====")]
        public bool ShowOnlyLargeBool { get; set; }

        [Parameter("Volume Digits View:", DefaultValue = DigitsToView.All, Group = "==== Numerical Renko Bars ====")]
        public DigitsToView DigitsToViewInput { get; set; }


        [Parameter("Renko Bars Color:", DefaultValue = BarsColorData.Volume, Group = "==== Renko Bars ====")]
        public BarsColorData BarsColorInput { get; set; }

        [Parameter("Fill Renko Bars?", DefaultValue = true, Group = "==== Renko Bars ====")]
        public bool BarsFillBool { get; set; }

        [Parameter("Keep Bull/Bear Outline?", DefaultValue = false, Group = "==== Renko Bars ====")]
        public bool BarsOutlineBool { get; set; }


        public enum ShowWavesData
        {
            No,
            Both,
            Volume,
            EffortvsResult
        }
        public enum ShowMarksData
        {
            No,
            Both,
            Left,
            Right
        }
        public enum ShowOtherWaves_Data
        {
            No,
            Both,
            Price,
            Time
        }
        public enum RepaintData
        {
            No,
            itsRepaint,
        }

        [Parameter("Show Waves", DefaultValue = ShowWavesData.Both, Group = "==== Waves Information ====")]
        public ShowWavesData ShowWavesInput { get; set; }

        [Parameter("Show Other Waves", DefaultValue = ShowOtherWaves_Data.No, Group = "==== Waves Information ====")]
        public ShowOtherWaves_Data ShowOtherWaves_Input { get; set; }

        [Parameter("Show Comparison Marks", DefaultValue = ShowMarksData.Both, Group = "==== Waves Information ====")]
        public ShowMarksData ShowMarksInput { get; set; }
        
        [Parameter("Show Current Wave", DefaultValue = RepaintData.No, Group = "==== Waves Information ====")]
        public RepaintData RepaintInput { get; set; }

        [Parameter("Bull Wave Color", DefaultValue = Colors.SeaGreen, Group = "==== Waves Information ====")]
        public Colors strBullWaveColor { get; set; }

        [Parameter("Bear Wave Color", DefaultValue = Colors.OrangeRed, Group = "==== Waves Information ====")]
        public Colors strBearWaveColor { get; set; }


        [Parameter("Effort vs Result Ratio", DefaultValue = 1.5, MinValue = 0, Group = "==== Waves Ratio ====")]
        public double EvsR_Ratio { get; set; }

        [Parameter("Large Weis Waves Ratio", DefaultValue = 1.5, MinValue = 0, Group = "==== Waves Ratio ====")]
        public double WW_Ratio { get; set; }

        [Parameter("Large WW/EvsR Color", DefaultValue = Colors.Yellow, Group = "==== Waves Ratio ====")]
        public Colors strLargeColor { get; set; }


        [Parameter("MA Filter Type:", DefaultValue = MovingAverageType.Exponential, Group = "==== Moving Average for Numbers/Bars Colors ====")]
        public MovingAverageType MAtype { get; set; }

        [Parameter("MA Filter Period:", DefaultValue = 5, MinValue = 1, Group = "==== Moving Average for Numbers/Bars Colors ====")]
        public int MAperiod { get; set; }
        
        
        [Parameter("Show TrendLines?", DefaultValue = false, Group = "==== Trend Lines Settings ====")]
        public bool ShowTrendLines { get; set; }
        
        [Parameter("NoTrend Line Color", DefaultValue = Colors.SteelBlue, Group = "==== Trend Lines Settings ====")]
        public Colors NoTrendColor { get; set; }
        
        [Parameter("BullTrend Line Color", DefaultValue = Colors.Green, Group = "==== Trend Lines Settings ====")]
        public Colors BullLineColor { get; set; }
        
        [Parameter("BearTrend Line Color", DefaultValue = Colors.Red, Group = "==== Trend Lines Settings ====")]
        public Colors BearLineColor { get; set; }
        
        [Parameter("Transcribed & Improved", DefaultValue = "for cTrader/C# by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }
        [Parameter("Original Code Concepts", DefaultValue = "in TDV/Pinescript by akutsusho", Group = "==== Credits ====")]
        public string Credits_2 { get; set; }
        
        /* 
            Using TrendLines instead of Output.Line because
            I want to enable/disable lines WITHOUT having to click 3 buttons or set 3 different colors to Transparent
            
            [after some time...]
            It is possible to disable the lines with one parameter in the Output.Line Method, 
            I still keep the trendlines because "who wants to know the numerical information of a line"? hahaha
        */


        // ======= Weis Wave & Wyckoff System =======
        string[] Bars_Colors = { "", "", "", "", "", "", "" };
        string[] Numbers_Colors = { "", "", "", "", "", "", "" };

        private IndicatorDataSeries renkoAllTimes;
        private MovingAverage MATime, MAVol;

        private double prevWaveVol_Bull;
        private double prevVP_Bull;

        private double prevWaveVol_Bear;
        private double prevVP_Bear;

        double[] prevCumul_Bull = { 0, 0 };
        double[] prevCumul_Bear = { 0, 0 };

        double[] prevWaves_VP = { 0, 0, 0, 0 };
        double[] prevWaves_Vol = { 0, 0, 0, 0 };

        private bool WrongTF = false;
        private bool FinishedBool = false;
        private int PipsMutliplier = 1;

        private List<ChartText> TextsNumbersBar = new List<ChartText>();
        private ChartType currentChartType;
        
        // --- Zig Zag ---
        enum Direction
        {
            up,
            down
        }
        private Direction direction = Direction.down;
        private double extremumPrice = 0.0;
        private int extremumIndex = 0;
        
        private int trendStartIndex = 0;
        private double trendStartPrice = 0;    
        private IndicatorDataSeries TrendBuffer;

        // ======= Volume Renko&Range =======
        private DateTime FromDateTime;
        private IndicatorDataSeries VolumeRR;
        private Bars _TicksOHLC;
        private int CurrentVol = 0;
        
        // ======= Renko Wicks =======       
        private IndicatorDataSeries AllWicks;
        private Color BullWickColor;
        private Color BearWickColor;
        private List<double> currentPriceWicks = new List<double>();
        private List<ChartTrendLine> TrendLinesWicks = new List<ChartTrendLine>();

        private bool TextsRemoved = false;
        private VerticalAlignment V_Align = VerticalAlignment.Top;
        private HorizontalAlignment H_Align = HorizontalAlignment.Center;
        
        protected override void Initialize()
        {
            // ===== Verify Timeframe =====
            string currentTimeframe = Chart.TimeFrame.ToString();
            if (!currentTimeframe.Contains("Renko"))
            {
                DrawOnScreen($"Weis&Wyckoff System \n WORKS ONLY IN RENKO CHART!");
                WrongTF = true;
                return;
            }

            // ===== Settings Bars/Numbers Colors =====
            if (NumbersPositionInput == NumbersBarPositionData.Outside)
            {
                // Colors                 
                string[] B_Colors = { "EE3E3E40", "EE8F9092", "DDFFFFFF", "EEA1F6A1", "EE1D8934", "EEFA6681", "EEE00106" };
                Bars_Colors = B_Colors;

                string[] N_Colors = { "EE3E3E40", "EE8F9092", "DDFFFFFF", "EEA1F6A1", "EE1D8934", "EEFA6681", "EEE00106" };
                Numbers_Colors = N_Colors;
            }
            else if (NumbersPositionInput == NumbersBarPositionData.Inside)
            {
                // Colors                 
                string[] B_Colors = { "843E3E40", "658F9092", "65FFFFFF", "65A1F6A1", "651D8934", "65FA6681", "65E00106" };
                Bars_Colors = B_Colors;

                string[] N_Colors = { "FF3E3E40", "FF8F9092", "FFFFFFFF", "FFA1F6A1", "FF1D8934", "FFFA6681", "FFE00106" };
                Numbers_Colors = N_Colors;
            }
            
            if (NumbersPositionInput == NumbersBarPositionData.Outside)
            {
                /* As it's a combination that won't be used much, 
                   and btw drawing this combination is a little tiring, 
                   I left it out. */
                if (ShowNumbersInput == NumbersBarData.Both && (ShowWavesInput == ShowWavesData.Both || ShowWavesInput == ShowWavesData.Volume || ShowWavesInput == ShowWavesData.EffortvsResult))
                {
                    Print("W.WAVES POSITIONS in OUTSIDE NUMBERS OPTION is not optimized for BOTH NUMBERS OPTION, setting BOTH to VOLUME instead");
                    ShowNumbersInput = NumbersBarData.Volume;
                }
                else if (ShowNumbersInput == NumbersBarData.Both && (ShowOtherWaves_Input == ShowOtherWaves_Data.Both || ShowOtherWaves_Input == ShowOtherWaves_Data.Price || ShowOtherWaves_Input == ShowOtherWaves_Data.Time))
                {
                    Print("W.Waves POSITIONS in OUTSIDE NUMBERS OPTION is not optimized for BOTH NUMBERS OPTION, setting BOTH to VOLUME instead");
                    ShowNumbersInput = NumbersBarData.Volume;
                }
            }
            
            // ===== Volume RR Inicialization =====
            _TicksOHLC = MarketData.GetBars(TimeFrame.Tick);
            Bars.BarOpened += ResetCurrentVol;
            Chart.ObjectsRemoved += SetTextsRemoved;
            VolumeRR = CreateDataSeries();
            VolumeInitialize();

            // ===== Renko Wicks Inicialization =====
            AllWicks = CreateDataSeries();
            Bars.BarOpened += ResetCurrentWick;
            Chart.ObjectsRemoved += SetTextsRemoved;
            Chart.ColorsChanged += SetTrendLinesColor;

            // ===== Coloring Volume&Time / Numeric Renko Inicialization / Wyckoff Part =====
            renkoAllTimes = CreateDataSeries();

            MATime = Indicators.MovingAverage(renkoAllTimes, MAperiod, MAtype);
            MAVol = Indicators.MovingAverage(VolumeRR, MAperiod, MAtype);

            if (Symbol.Digits == 2 && Symbol.PipSize == 0.1)
                PipsMutliplier = 10;

            Chart.ChartTypeChanged += SetNumbersPositionEvent;
            currentChartType = Chart.ChartType;
            
            TrendBuffer = CreateDataSeries();
            
        }

        public override void Calculate(int index)
        {
            if (WrongTF)
                return;

            // ==== Removing Messages + Others ====
            if (IsLastBar)
            {    
                CurrentVol += 1;
                if (ShowWicks)
                    currentPriceWicks.Add(Bars.ClosePrices[index]);
            }
            else
                DrawOnScreen("");
                
            if (index < (Bars.OpenTimes.GetIndexByTime(Server.Time)-Lookback) && (Lookback != -1 && Lookback > 0)) 
            {
                Chart.SetBarOutlineColor(index, Bars_Colors[1]); 
                Chart.SetBarFillColor(index, Bars_Colors[1]); 
                return;
            }
            // ==== Volume RR ====
            DateTime CurrentTimeBar = Bars.OpenTimes[index];
            DateTime PreviousTimeBar = Bars.OpenTimes[index - 1];

            VolumeRR[index - 1] = Get_Volume_or_Wicks(PreviousTimeBar, CurrentTimeBar, true);
            VolumeRR[index] = CurrentVol;

            // ==== Coloring Renko Bars ====
            Standard_Colors(index);

            // ==== Weis Wave ====
            WeisWaveSystem(index);
            
            // ==== Renko Wicks ====
            if (ShowWicks)
                RenkoWicks(index);
            
            // Live Volume 
            ChartText dyntext = Chart.DrawText($"liveVol", $"\n{CurrentVol}", index, Bars.ClosePrices[index], Color.White);
            dyntext.HorizontalAlignment = HorizontalAlignment.Center;
            // Live Time 
            DateTime c_prevTime = Bars.OpenTimes[index];
            DateTime c_currentTime2 = Server.Time;
            TimeSpan c_interval = c_currentTime2.Subtract(c_prevTime);
            double interval_ms = c_interval.TotalMilliseconds;
            string[] interval_tlapse = DynTimeLapse(interval_ms);
            double dynInterval = Convert.ToDouble(interval_tlapse[0]);
            string actualTimeLapse = interval_tlapse[1];
            
            ChartText dyntext_t = Chart.DrawText($"liveTimer", $"{Math.Round(dynInterval) + actualTimeLapse}", index, Bars.ClosePrices[index], Color.White);
            dyntext_t.HorizontalAlignment = HorizontalAlignment.Center;
            
        }
        
        private void Standard_Colors(int index)
        {
            // =========== Timer ===========
            // Previous Interval
            DateTime prevTime = Bars.OpenTimes[index - 2];
            DateTime currentTime = Bars.OpenTimes[index - 1];
            TimeSpan interval = currentTime.Subtract(prevTime);
            double interval_ms = interval.TotalMilliseconds;

            // Dynamic TimeLapse Format
            string[] interval_tlapse = DynTimeLapse(interval_ms);
            double dynInterval = Convert.ToDouble(interval_tlapse[0]);
            string actualTimeLapse = interval_tlapse[1];
            // -----------------

            renkoAllTimes[index - 1] = dynInterval;

            // Current Interval
            DateTime c_prevTime = Bars.OpenTimes[index - 1];
            DateTime c_currentTime2 = Bars.OpenTimes[index];
            TimeSpan c_interval = c_currentTime2.Subtract(c_prevTime);
            double c_interval_ms = c_interval.TotalMilliseconds;
            // -----------------
            
            renkoAllTimes[index] = c_interval_ms;

            // =========== Time Filter ===========
            double rawTimeFilter = dynInterval / MATime.Result[index - 1];

            string strTimeFilter = Filtered_TwoDecimais();
            string Filtered_TwoDecimais()
            {
                string[] rawStr = rawTimeFilter.ToString().Split(',');
                if (rawStr.Length == 1)
                    return rawStr[0];
                string rawDecimais = rawStr[1];
                string twoDecimais = rawDecimais.Substring(0, 2);
                return $"{rawStr[0]},{twoDecimais}";
            }

            double timeFilter = Math.Round(Convert.ToDouble(strTimeFilter), 1);
            double timeLarge = timeFilter > 1 ? Math.Round(dynInterval) : 0;

            // =========== Volume Filter ===========
            double volumeFilter = VolumeRR[index - 1] / MAVol.Result[index - 1];
            double volumeLarge = volumeFilter > 1 ? VolumeRR[index - 1] : 0;

            //  ========== Drawing ==========
            // --- Color type ---
            double colorTypeNumbers = NumbersColorInput == NumbersColorData.Time ? timeFilter : volumeFilter;
            double colorTypeBars = BarsColorInput == BarsColorData.Time ? timeFilter : volumeFilter;
            // --- Y-Axis ---
            bool isBullish = (Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1]);
            double y_bull = Bars.ClosePrices[index - 1];
            double y_bear = Bars.OpenPrices[index - 1];
            // --- Shows Dynamic Time/Volume ---
            string strTimeLarge = timeLarge != 0 ? timeLarge.ToString() + actualTimeLapse : "";
            string strVolLarge = volumeLarge != 0 ? volumeLarge.ToString() : "";

            string onlyTime = ShowOnlyLargeBool ? $"{strTimeLarge}" : $"{Math.Round(dynInterval) + actualTimeLapse}";
            string onlyVol = ShowOnlyLargeBool ? $"{strVolLarge}" : $"{SetDigits(VolumeRR[index - 1])}";
            
            string dynLargePosition = "";
            string VolTime = "";
            if (NumbersBothPositionInput == NumbersBothPositionData.Default) 
            {
                dynLargePosition = strTimeLarge == "" ? $"{strVolLarge}" : strVolLarge == "" ? $"{strTimeLarge}" : $"{Math.Round(dynInterval) + actualTimeLapse}\n{SetDigits(VolumeRR[index - 1])}";
                VolTime = ShowOnlyLargeBool ? dynLargePosition : $"{Math.Round(dynInterval) + actualTimeLapse}\n{SetDigits(VolumeRR[index - 1])}";
            }
            else
            {
                dynLargePosition = strTimeLarge == "" ? $"{strVolLarge}" : strVolLarge == "" ? $"{strTimeLarge}" : $"{SetDigits(VolumeRR[index - 1])}\n{Math.Round(dynInterval) + actualTimeLapse}";
                VolTime = ShowOnlyLargeBool ? dynLargePosition : $"{SetDigits(VolumeRR[index - 1])}\n{Math.Round(dynInterval) + actualTimeLapse}";
            }
            // --- Shows Time/Volume/Both ---
            var selectedNumbers = ShowNumbersInput;
            string dynString = selectedNumbers == NumbersBarData.Time ? onlyTime : selectedNumbers == NumbersBarData.Volume ? onlyVol : VolTime;
            
            // === Bull ===
            if (isBullish)
            {
                if (y_bull.ToString() == "NaN")
                    return;
                    
                // Number/Bar Color
                Color dynNumberColor = colorTypeNumbers > 2 ? Numbers_Colors[4] : colorTypeNumbers > 1.5 ? Numbers_Colors[3] : colorTypeNumbers > 1 ? Numbers_Colors[2] : colorTypeNumbers > 0.5 ? Numbers_Colors[1] : Numbers_Colors[0];
                Color dynBarColor = colorTypeBars > 2 ? Bars_Colors[4] : colorTypeBars > 1.5 ? Bars_Colors[3] : colorTypeBars > 1 ? Bars_Colors[2] : colorTypeBars > 0.5 ? Bars_Colors[1] : Bars_Colors[0];
                
                if (VolumeRR[index-1] != 0)
                {
                    ChartText dyntext = Chart.DrawText($"VolTimeBull_{index - 1}", dynString, index - 1, y_bull, dynNumberColor);
                    
                    // Positions Settings + Others
                    if (Chart.ChartType != ChartType.Bars)
                    {
                        if (NumbersPositionInput == NumbersBarPositionData.Outside)
                            dyntext.VerticalAlignment = VerticalAlignment.Top;
                        dyntext.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                    else
                        dyntext.HorizontalAlignment = HorizontalAlignment.Stretch;
                        
                    dyntext.Comment = "Bull";
                    if (NumbersColorInput == NumbersColorData.CustomColor)
                        dyntext.Color = Color.FromName(CustomNumbersColor.ToString());
                    TextsNumbersBar.Add(dyntext);
                }
                // Fill + Outline Settings
                if (!BarsFillBool && !BarsOutlineBool)
                {
                    Chart.SetBarFillColor(index - 1, Color.Transparent);
                    Chart.SetBarOutlineColor(index - 1, dynBarColor);

                    BullWickColor = dynBarColor;
                }
                else if (BarsFillBool && BarsOutlineBool)
                {
                    Chart.SetBarFillColor(index - 1, dynBarColor);
                    BullWickColor = Chart.ColorSettings.BullOutlineColor;
                }
                else if (!BarsFillBool && BarsOutlineBool)
                {
                    Chart.SetBarFillColor(index - 1, Color.Transparent);
                    BullWickColor = Chart.ColorSettings.BullOutlineColor;
                }
                else if (BarsFillBool && !BarsOutlineBool)
                {
                    Chart.SetBarColor(index - 1, dynBarColor);
                    BullWickColor = dynBarColor;
                }
            }
            // === Bear ===
            else
            {
                if (y_bear.ToString() == "NaN")
                    return;
                // Number/Bar Color
                Color dynNumberColor = colorTypeNumbers > 2 ? Numbers_Colors[6] : colorTypeNumbers > 1.5 ? Numbers_Colors[5] : colorTypeNumbers > 1 ? Numbers_Colors[2] : colorTypeNumbers > 0.5 ? Numbers_Colors[1] : Numbers_Colors[0];
                Color dynBarColor = colorTypeBars > 2 ? Bars_Colors[6] : colorTypeBars > 1.5 ? Bars_Colors[5] : colorTypeBars > 1 ? Bars_Colors[2] : colorTypeBars > 0.5 ? Bars_Colors[1] : Bars_Colors[0];
                
                if (VolumeRR[index-1] != 0)
                {
                    ChartText dyntext = Chart.DrawText($"VolTimeBull_{index - 1}", dynString, index - 1, y_bear, dynNumberColor);
                    
                    // Positions Settings + Others
                    if (Chart.ChartType != ChartType.Bars)
                    {
                        if (NumbersPositionInput == NumbersBarPositionData.Outside)
                            dyntext.Y = y_bull;
                        dyntext.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                    else
                        dyntext.HorizontalAlignment = HorizontalAlignment.Stretch;
                        
                    dyntext.Comment = "Bear";
                        
                    if (NumbersColorInput == NumbersColorData.CustomColor)
                        dyntext.Color = Color.FromName(CustomNumbersColor.ToString());
                    
                    TextsNumbersBar.Add(dyntext);
                }
                
                // Fill + Outline Settings
                if (!BarsFillBool && !BarsOutlineBool)
                {
                    Chart.SetBarFillColor(index - 1, Color.Transparent);
                    Chart.SetBarOutlineColor(index - 1, dynBarColor);

                    BearWickColor = dynBarColor;
                }
                else if (BarsFillBool && BarsOutlineBool)
                {
                    Chart.SetBarFillColor(index - 1, dynBarColor);
                    BearWickColor = Chart.ColorSettings.BearOutlineColor;
                }
                else if (!BarsFillBool && BarsOutlineBool)
                {
                    Chart.SetBarFillColor(index - 1, Color.Transparent);
                    BearWickColor = Chart.ColorSettings.BearOutlineColor;
                }
                else if (BarsFillBool && !BarsOutlineBool)
                {
                    Chart.SetBarColor(index - 1, dynBarColor);
                    BearWickColor = dynBarColor;
                }
            }
        }
        // ========= ========== ==========
        private void SetNumbersPositionEvent(ChartTypeEventArgs obj)
        {
            currentChartType = obj.Chart.ChartType;
            ChartText last = TextsNumbersBar.LastOrDefault();
            bool alreadySet_Bars = last.HorizontalAlignment == HorizontalAlignment.Stretch;
            bool alreadySet_Candlesticks = last.HorizontalAlignment == HorizontalAlignment.Center;
            if (Chart.ChartType == ChartType.Bars && !alreadySet_Bars)
            {
                for (int index = 0; index < TextsNumbersBar.Count; index++)
                {
                    if (TextsNumbersBar[index].Comment == "Bull")
                        TextsNumbersBar[index].HorizontalAlignment = HorizontalAlignment.Stretch;
                    else
                        TextsNumbersBar[index].HorizontalAlignment = HorizontalAlignment.Stretch;
                }
            }
            else if (obj.Chart.ChartType == ChartType.Candlesticks && !alreadySet_Candlesticks)
            {
                for (int index = 0; index < TextsNumbersBar.Count; index++)
                {
                    if (TextsNumbersBar[index].Comment == "Bull")
                        TextsNumbersBar[index].HorizontalAlignment = HorizontalAlignment.Center;
                    else
                        TextsNumbersBar[index].HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
        }
        // ========= ========== ==========
        private double SetDigits(double Number, bool haveComma=false)
        {
            if (DigitsToViewInput == DigitsToView.All || Number == 0 || Number.ToString() == "NaN" || Number.ToString().Length == 1)
                return Number;
                
            if (!haveComma)
            {
                string strNumber = Number.ToString();
                int dynDigits = DigitsToViewInput == DigitsToView._4_Digits ? 4 : 3;

                string newValue = strNumber;
                
                if (dynDigits == 4)
                {
                    for (int i = 0; i < newValue.Length; i++)
                    {
                        if (strNumber.Length == dynDigits+1)
                        {
                            if (strNumber.Length == 2)
                                break;
                            newValue = strNumber.Remove(strNumber.Length - 1);
                            break;
                        }
                        else if (newValue.Length > dynDigits)
                            newValue = newValue.Remove(newValue.Length - 1);
                        else
                            break;
                    }
               }
               else
               {
                    for (int i = 0; i < newValue.Length; i++)
                    {
                        if (strNumber.Length == 3)
                        {
                            newValue = strNumber.Remove(strNumber.Length - 1);
                            break;
                        }
                        else if (strNumber.Length == dynDigits+1)
                        {
                            if (strNumber.Length == 2)
                                break;
                            newValue = strNumber.Remove(strNumber.Length - 1);
                            break;
                        }
                        else if (newValue.Length > dynDigits)
                            newValue = newValue.Remove(newValue.Length - 1);
                        else
                            break;
                    }
               }
                return Convert.ToDouble(newValue);
            }
            else
            {
                string strNumber = Number.ToString().Replace(",", "");
                int dynDigits = DigitsToViewInput == DigitsToView._4_Digits ? 4 : 5;
                
                string newValue = strNumber;
                for (int i = 0; i < newValue.Length; i++)
                {
                    if (strNumber.Length == dynDigits+1)
                    {
                        if (strNumber.Length <= 2)
                            break;
                        newValue = strNumber.Remove(strNumber.Length - 1);
                        break;
                    }
                    if (newValue.Length > dynDigits+1)
                        newValue = newValue.Remove(newValue.Length - 1);
                    else
                        break;
                }
                
                char last = newValue.Last();
                string removedLast = newValue.Remove(newValue.Length - 1);
                string newStr = $"{removedLast}{last}";
                
                return Convert.ToDouble(newStr);
            }
        }
        
        // ************************** VOLUME RENKO/RANGE **************************
        /*
            Original source code by srlcarlg (me) (https://ctrader.com/algos/indicators/show/3045)
        */
        private void VolumeInitialize()
        {
            if (LoadFromInput == LoadFromData.Custom)
            {
                // ==== Get datetime to load from: dd/mm/yyyy ====
                if (DateTime.TryParseExact(StringDate, "dd/mm/yyyy", new CultureInfo("en-US"), DateTimeStyles.None, out FromDateTime))
                {
                    if (FromDateTime > Server.Time.Date) {
                        // for Log
                        FromDateTime = Server.Time.Date;
                        Print($"Invalid DateTime '{StringDate}'. Using '{FromDateTime}'");
                    }
                }
                else {
                    // for Log
                    FromDateTime = Server.Time.Date;
                    Print($"Invalid DateTime '{StringDate}'. Using '{FromDateTime}'");
                }
            }
            else
            {
                DateTime LastBarTime = Bars.LastBar.OpenTime.Date;
                if (LoadFromInput == LoadFromData.Today)
                    FromDateTime = LastBarTime.Date;
                else if (LoadFromInput == LoadFromData.Yesterday)
                    FromDateTime = LastBarTime.AddDays(-1);
                else if (LoadFromInput == LoadFromData.One_Week)
                    FromDateTime = LastBarTime.AddDays(-5);
                else if (LoadFromInput == LoadFromData.Two_Week)
                    FromDateTime = LastBarTime.AddDays(-10);
                else if (LoadFromInput == LoadFromData.Monthly)
                    FromDateTime = LastBarTime.AddMonths(-1);
            }

            // ==== Check if existing ticks data on the chart really needs more data ====
            DateTime FirstTickTime = _TicksOHLC.OpenTimes.Reverse().LastOrDefault();
            if (FirstTickTime >= FromDateTime) {
                LoadMoreTicks(FromDateTime);
                DrawOnScreen("Data Collection Finished \n Calculating...");
            }
            else {
                Print($"Using existing tick data from '{FirstTickTime}'");
                DrawOnScreen($"Using existing tick data from '{FirstTickTime}' \n Calculating...");
            }
        }
        // ========= ========== ==========
        private double Get_Volume_or_Wicks(DateTime startTime, DateTime endTime, bool isVolume, bool isBullish = false)
        {
            int volume = 0;

            double min = Int32.MaxValue;
            double max = 0;

            for (int tickIndex = 0; tickIndex < _TicksOHLC.Count; tickIndex++)
            {
                Bar tickBar = _TicksOHLC[tickIndex];

                if (tickBar.OpenTime < startTime || tickBar.OpenTime > endTime)
                {
                    if (tickBar.OpenTime > endTime)
                        break;
                    else
                        continue;
                }

                volume += 1;

                if (isBullish && !isVolume && tickBar.Close < min)
                    min = tickBar.Close;       
                else if (!isBullish &&  !isVolume && tickBar.Close > max)
                    max = tickBar.Close;
            }
            
            if (isVolume)
                return volume;
            else
                return isBullish ? min : max;
        }
        // ========= ========== ==========
        private void LoadMoreTicks(DateTime FromDateTime)
        {
            bool msg = false;

            while (_TicksOHLC.OpenTimes.Reverse().LastOrDefault() > FromDateTime)
            {
                if (!msg) {
                    Print($"Loading from '{_TicksOHLC.OpenTimes.Reverse().Last()}' to '{FromDateTime}'...");
                    msg = true;
                }

                int loadedCount = _TicksOHLC.LoadMoreHistory();
                Print("Loaded {0} Ticks, Current Tick Date: {1}", loadedCount, _TicksOHLC.OpenTimes.Reverse().LastOrDefault());
                if (loadedCount == 0)
                    break;
            }
            Print("Data Collection Finished, First Tick from: {0}", _TicksOHLC.OpenTimes.Reverse().LastOrDefault());
        }
        // ========= ========== ==========
        private void ResetCurrentVol(BarOpenedEventArgs obj)
        {
            CurrentVol = 0;
        }
        // ========= ========== ==========
        private void SetTextsRemoved(ChartObjectsRemovedEventArgs obj)
        {
            TextsRemoved = true;
        }

        // ************************** RENKO WICKS **************************
        /*
            Original source code by srlcarlg (me) (https://ctrader.com/algos/indicators/show/3046)
        */
        private void RenkoWicks(int index)
        {
            DateTime CurrentTimeBar = Bars.OpenTimes[index];
            DateTime PreviousTimeBar = Bars.OpenTimes[index - 1];
            double PrevOpen = Bars.OpenPrices[index - 1];

            bool isBullish = (Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1]);
            bool currentIsBullish = (Bars.ClosePrices[index] > Bars.OpenPrices[index]);
            bool Gap = Bars.OpenTimes[index - 1] == Bars.OpenTimes[index - 2];
            // ==============

            AllWicks[index - 1] = Get_Volume_or_Wicks(PreviousTimeBar, CurrentTimeBar, false, isBullish);

            // ==== HISTORICAL BULL WICK ====           
            if (isBullish)
            {
                if (AllWicks[index - 1] < PrevOpen && !Gap)
                {
                    ChartTrendLine trendBull = Chart.DrawTrendLine("BullWick_" + (index - 1), PreviousTimeBar, AllWicks[index - 1], PreviousTimeBar, Bars.OpenPrices[index - 1], BullWickColor);
                    trendBull.Thickness = Thickness;
                    trendBull.Comment = "BullWick";
                    TrendLinesWicks.Add(trendBull);
                }
            }
            // ==== HISTORICAL BEAR WICK ====
            else
            {
                if (AllWicks[index - 1] > PrevOpen && !Gap)
                {
                    ChartTrendLine trendBear = Chart.DrawTrendLine("BearWick_" + (index - 1), PreviousTimeBar, AllWicks[index - 1], PreviousTimeBar, Bars.OpenPrices[index - 1], BearWickColor);
                    trendBear.Thickness = Thickness;
                    trendBear.Comment = "BearWick";
                    TrendLinesWicks.Add(trendBear);
                }
            }

            // ==== CURRENT BULL WICK ====
            if (currentIsBullish)
            {
                if (currentPriceWicks.Count == 0)
                    return;

                AllWicks[index] = currentPriceWicks.Min();
                ChartTrendLine currentTrendBull = Chart.DrawTrendLine("currentPriceLines", CurrentTimeBar, AllWicks[index], CurrentTimeBar, currentPriceWicks.Max(), BullWickColor);
                currentTrendBull.Thickness = Thickness;
            }
            // ==== CURRENT BEAR WICK ====
            else
            {
                if (currentPriceWicks.Count == 0)
                    return;

                AllWicks[index] = currentPriceWicks.Max();
                ChartTrendLine currentTrendBear = Chart.DrawTrendLine("currentPriceLines", CurrentTimeBar, AllWicks[index], CurrentTimeBar, currentPriceWicks.Min(), BearWickColor);
                currentTrendBear.Thickness = Thickness;
            }
        }
        // ========= Functions Area ==========
        private void ResetCurrentWick(BarOpenedEventArgs obj)
        {
            currentPriceWicks.Clear();
            Chart.RemoveObject("currentPriceLines");
        }
        // ========= ========== ==========
        private void DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
        }
        // ========= ========== ==========
        private void SetTrendLinesColor(ChartColorEventArgs obj)
        {
            if (obj.Chart.ColorSettings.BullOutlineColor != BullWickColor)
            {
                for (int wickIndex = 0; wickIndex < TrendLinesWicks.Count; wickIndex++)
                {
                    if (TrendLinesWicks[wickIndex].Comment == "BullWick")
                        TrendLinesWicks[wickIndex].Color = obj.Chart.ColorSettings.BullOutlineColor;
                }
                BullWickColor = obj.Chart.ColorSettings.BullOutlineColor;
            }

            if (obj.Chart.ColorSettings.BearOutlineColor != BearWickColor)
            {
                for (int wickIndex = 0; wickIndex < TrendLinesWicks.Count; wickIndex++)
                {
                    if (TrendLinesWicks[wickIndex].Comment == "BearWick")
                        TrendLinesWicks[wickIndex].Color = obj.Chart.ColorSettings.BearOutlineColor;
                }
                BearWickColor = obj.Chart.ColorSettings.BearOutlineColor;
            }
        }

        // ************************ WEIS WAVE SYSTEM **************************
        /* 
                                   Improved Weis Waves
                                           by 
                                        srlcarlg
                                        
                          ====== References for Studies ======
        (Numbers-Renko 数字練行足) by akutsusho (https://www.tradingview.com/script/9BKOIhdl-Numbers-Renko) (Code concepts in PineScript)
        (Swing Gann) by TradeExperto (https://ctrader.com/algos/indicators/show/2521) (helped a lot in the structure of the calculation of waves)
        (ZigZag) by mike.ourednik (https://ctrader.com/algos/indicators/show/1419) (also, decreased a lot of code)
        
        */
        private void WeisWaveSystem(int rawIndex)
        {
            int index = rawIndex;
            if (RepaintInput == RepaintData.No)
                index = rawIndex - 2;
            else
                index = rawIndex - 1;
            
            if (index < 2)
                return;
            
            double low = Bars.LowPrices[index];
            double high = Bars.HighPrices[index];
            
            if (extremumPrice == 0.0)
                extremumPrice = high;
  
            if (direction == Direction.down)
            {
                if (low <= extremumPrice)
                    moveExtremum(index, low);
                else if (high >= extremumPrice * (1.0 + 0.01 * 0.01))
                {
                    setExtremum(index, high);
                    direction = Direction.up;
                }
            }
            else
            {
                if (high >= extremumPrice)
                    moveExtremum(index, high);
                else if (low <= extremumPrice * (1.0 - 0.01 * 0.01))
                {
                    setExtremum(index, low);
                    direction = Direction.down;
                }
            }
                        
            // ===== Local Functions Area =====
            void moveExtremum(int indexBar, double price)
            {
                // Need 
                TrendBuffer[extremumIndex+1] = Bars.OpenPrices[index];
                
                // === Calculate Wave = In Trend ===
                bool dynCurrentUpDw = Bars.ClosePrices[index] > Bars.OpenPrices[index];
                if (dynCurrentUpDw)
                {
                    CalculateWaves(1, trendStartIndex, index, ShowWavesInput, trendStartIndex, false);

                    if (ShowTrendLines)
                        Chart.DrawTrendLine("TrendLine" + trendStartIndex, trendStartIndex, Bars.OpenPrices[trendStartIndex], index, Bars.OpenPrices[index], Color.Green);
                    /*
                        In Output.Line Method, just do: 
                        BullLine[index] = TrendBuffer[extremumIndex+1];
                        BearLine[index] = double.NaN;
                    */
                }
                else
                {
                    CalculateWaves(3, trendStartIndex, index, ShowWavesInput, trendStartIndex, false);

                    if (ShowTrendLines)
                        Chart.DrawTrendLine("TrendLine" + trendStartIndex, trendStartIndex, Bars.OpenPrices[trendStartIndex], index, Bars.OpenPrices[index], Color.Red);
                    /*
                        In Output.Line Method, just do: 
                        BearLine[index] = TrendBuffer[extremumIndex+1];
                        BullLine[index] = double.NaN;
                    */
                }
                
                setExtremum(index, price);
            }
            // ========= ========== ==========
            void setExtremum(int indexBar, double price)
            {
                // I had to change the parameter index to indexBar because cTrader on .NET 4.x is slow and boring :)
                // cTrader on .NET 6.0, parameter named in index works normally 
                
                extremumIndex = index;
                extremumPrice = price;
                TrendBuffer[extremumIndex+1] = extremumPrice;
                
                if (DirectionChanged(index))
                {                    
                    // Current index is the end of trend
                    // === Calculate Wave = Final Trend ===
                    bool dynCurrentUpDw = Bars.ClosePrices[index] > Bars.OpenPrices[index];
                    bool prevIsUp = Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1];
                    bool nextIsUp = Bars.ClosePrices[index + 1] > Bars.OpenPrices[index + 1];
                    bool prevIsDown = Bars.ClosePrices[index - 1] < Bars.OpenPrices[index - 1];
                    bool nextIsDown = Bars.ClosePrices[index + 1] < Bars.OpenPrices[index + 1];
                    

                    if (dynCurrentUpDw)
                    {
                        CalculateWaves(1, trendStartIndex, index, ShowWavesInput, trendStartIndex, true);
                        
                        if (ShowTrendLines)
                        {
                            Chart.DrawTrendLine("TrendLine" + trendStartIndex, trendStartIndex, Bars.OpenPrices[trendStartIndex], index, Bars.OpenPrices[index], Color.FromName(BullLineColor.ToString()));
                            Chart.DrawTrendLine("TrendLine" + extremumIndex, index + 1, Bars.OpenPrices[index+1], index, Bars.OpenPrices[index], Color.FromName(NoTrendColor.ToString()));
                            Chart.DrawTrendLine("TrendLine" + extremumIndex, index + 1, Bars.OpenPrices[index+1], index, Bars.OpenPrices[index+1], Color.FromName(NoTrendColor.ToString()));
                        }
                        /*
                            In Output.Line Method, just do: 
                            BullLine[index] = TrendBuffer[extremumIndex+1];
                            BearLine[index] = double.NaN;
                        */
                    }
                    else
                    {
                        int dynEndIndex = (prevIsDown && dynCurrentUpDw && nextIsDown) || (prevIsUp && !dynCurrentUpDw && nextIsUp) ? trendStartIndex : index;
                        CalculateWaves(3, trendStartIndex, index, ShowWavesInput, trendStartIndex, true);
                        
                        if (ShowTrendLines)
                        {
                            Chart.DrawTrendLine("TrendLine" + trendStartIndex, trendStartIndex, Bars.OpenPrices[trendStartIndex], index, Bars.OpenPrices[index], Color.FromName(BearLineColor.ToString()));
                            Chart.DrawTrendLine("TrendLine" + extremumIndex, index + 1, Bars.OpenPrices[index+1], dynEndIndex, Bars.OpenPrices[index], Color.FromName(NoTrendColor.ToString()));
                            Chart.DrawTrendLine("TrendLine" + extremumIndex, index + 1, Bars.OpenPrices[index+1], index, Bars.OpenPrices[index+1], Color.FromName(NoTrendColor.ToString()));
                        }
                        /*
                            In Output.Line Method, just do: 
                            BearLine[index] = TrendBuffer[extremumIndex+1];
                            BullLine[index] = double.NaN;
                        */
                    }

                    trendStartIndex = index + 1;
                    trendStartPrice = Bars.OpenPrices[index + 1];
                    TrendBuffer[extremumIndex+1] = Bars.OpenPrices[index + 1];
                }
            }
            // ========= ========== ==========
            bool DirectionChanged(int indexbar)
            {               
            
                // Dynamic Current Bar
                bool dynCurrentUpDw = Bars.ClosePrices[index] > Bars.OpenPrices[index];
                
                // I didn't put a dynamic bool because it can confuse
                bool prevIsUp = Bars.ClosePrices[index - 1] > Bars.OpenPrices[index - 1];
                bool nextIsUp = Bars.ClosePrices[index + 1] > Bars.OpenPrices[index + 1];
                bool prevIsDown = Bars.ClosePrices[index - 1] < Bars.OpenPrices[index - 1];
                bool nextIsDown = Bars.ClosePrices[index + 1] < Bars.OpenPrices[index + 1];
                                
                bool dynDirectionChanged =  prevIsUp && dynCurrentUpDw && nextIsDown  || prevIsDown && dynCurrentUpDw && nextIsDown 
                                        || prevIsDown &&  !dynCurrentUpDw && nextIsUp || prevIsUp && !dynCurrentUpDw && nextIsUp;
                
                return dynDirectionChanged;
            }
        }
        
        /* 
        The comments from here will be shortened, 
        because there is a lot of REPETITION of STRUCTURE, 
        so when you understand a part, all are understood 
        */
        private void CalculateWaves(int direction, int firstCandle, int lastCandle, ShowWavesData WaveOption, int objectIndex, bool DirectionChanged = false)
        {
            // direction == 1 is Bull, direction == 3 is Bear
            if (VolumeRR[lastCandle] == 0)
                return;
            if (WaveOption == ShowWavesData.No)
            {
                bool prevIsDown = Bars.ClosePrices[lastCandle - 1] < Bars.OpenPrices[lastCandle - 1];
                bool nextIsDown = Bars.ClosePrices[lastCandle + 1] < Bars.OpenPrices[lastCandle + 1];  
                bool prevIsUp = Bars.ClosePrices[lastCandle - 1] > Bars.OpenPrices[lastCandle - 1];
                bool nextIsUp = Bars.ClosePrices[lastCandle + 1] > Bars.OpenPrices[lastCandle + 1];
                
                if (RepaintInput == RepaintData.No)
                {
                    // Making sure to draw only at the end of waves
                    bool dynEndWave = (!prevIsDown && !DirectionChanged && nextIsDown || prevIsDown && DirectionChanged && nextIsDown)
                                      || (!prevIsUp && !DirectionChanged && nextIsUp || prevIsUp && DirectionChanged && nextIsUp);
                    if (dynEndWave)
                        OthersWaves();
                }
                else
                    OthersWaves();
                    
                return;
            }
            else
            {
                if (direction == 1)
                {
                    double cumlVolume = cumulVolume();

                    if (cumlVolume == 0)
                        return;

                    double cumlRenko = cumulRenko();
                    double cumlVolPrice = Math.Round(cumlVolume / cumlRenko, 1);

                    bool prevIsDown = Bars.ClosePrices[lastCandle - 1] < Bars.OpenPrices[lastCandle - 1];
                    bool nextIsDown = Bars.ClosePrices[lastCandle + 1] < Bars.OpenPrices[lastCandle + 1];
                    
                    bool endWave = (!prevIsDown && !DirectionChanged && nextIsDown || prevIsDown && DirectionChanged && nextIsDown);
                    if (RepaintInput == RepaintData.No)
                    {
                        // Making sure to draw only at the end of waves
                        if (endWave)
                        {
                            EvsR_Analysis(cumlVolPrice, endWave, true);
                            WW_Analysis(cumlVolume, true);
                        }
                    }
                    else
                    {
                        EvsR_Analysis(cumlVolPrice, endWave, true);
                        WW_Analysis(cumlVolume, true);
                    }
                    
                    // --- Set Previous Bullish Wave Accumulated ---
                    SetPrevWaves(cumlVolume, cumlVolPrice, prevIsDown, nextIsDown, true);
                    
                    // Other Waves
                    if (RepaintInput == RepaintData.No)
                    {
                        // Making sure to draw only at the end of waves
                        if (endWave)
                            OthersWaves();
                    }
                    else
                        OthersWaves();

                }
                else if (direction == 3)
                {
                    double cumlVolume = cumulVolume();

                    if (cumlVolume == 0)
                        return;

                    double cumlRenko = cumulRenko();
                    double cumlVolPrice = Math.Round(cumlVolume / cumlRenko, 1);

                    bool prevIsUp = Bars.ClosePrices[lastCandle - 1] > Bars.OpenPrices[lastCandle - 1];
                    bool nextIsUp = Bars.ClosePrices[lastCandle + 1] > Bars.OpenPrices[lastCandle + 1];
                    
                    bool endWave = (!prevIsUp && !DirectionChanged && nextIsUp || prevIsUp && DirectionChanged && nextIsUp);
                    if (RepaintInput == RepaintData.No)
                    {
                        // Making sure to draw only at the end of waves
                        if (endWave)
                        {
                            EvsR_Analysis(cumlVolPrice, endWave, false);
                            WW_Analysis(cumlVolume, false);
                        }
                    }
                    else
                    {
                        EvsR_Analysis(cumlVolPrice, endWave, false);
                        WW_Analysis(cumlVolume, false);
                    }

                    // --- Set Previous Bearish Wave Accumulated ---
                    SetPrevWaves(cumlVolume, cumlVolPrice, prevIsUp, nextIsUp, false);
                    
                    // Others Waves
                    if (RepaintInput == RepaintData.No)
                    {
                        // Making sure to draw only at the end of waves
                        if (endWave)
                            OthersWaves();
                    }
                    else
                        OthersWaves();

                }
            }
            // ========= ========== ==========
            void OthersWaves()
            {
                if (ShowOtherWaves_Input == ShowOtherWaves_Data.No)
                    return;
                else if (ShowOtherWaves_Input == ShowOtherWaves_Data.Both)
                {
                    if (direction == 1)
                    {
                        double Volume = cumulVolume();
                        if (Volume == 0)
                            return;

                        double dynPrice = cumulPrice(true);
                        double cumlTime = cumulTime();

                        if (cumlTime == 0 || cumlTime.ToString() == "NaN")
                            return;

                        string[] interval_tlapse = DynTimeLapse(cumlTime);

                        PriceTimeWave(dynPrice, interval_tlapse, true);
                    }
                    else if (direction == 3)
                    {
                        double Volume = cumulVolume();
                        if (Volume == 0)
                            return;

                        double dynPrice = cumulPrice(false);
                        double cumlTime = cumulTime();

                        if (cumlTime == 0 || cumlTime.ToString() == "NaN")
                            return;

                        string[] interval_tlapse = DynTimeLapse(cumlTime);

                        PriceTimeWave(dynPrice, interval_tlapse, false);
                    }
                    void PriceTimeWave(double dynPrice, string[] interval_tlapse, bool isBull)
                    {
                        if (isBull)
                        {
                            var selectedWave = ShowWavesInput;
                            string defaultStr = $"{Convert.ToDouble(interval_tlapse[0])}{interval_tlapse[1]}";
                            
                            string dynStr = "";
                            if (NumbersPositionInput == NumbersBarPositionData.Outside)
                                dynStr = selectedWave == ShowWavesData.No ? $"{defaultStr} ⎪ {dynPrice}p\n\n" : selectedWave == ShowWavesData.Both ? $"{defaultStr} ⎪ {dynPrice}p\n\n\n\n" : $"{defaultStr} ⎪ {dynPrice}p\n\n\n";
                            else
                                dynStr = selectedWave == ShowWavesData.No ? $"{defaultStr} ⎪ {dynPrice}p" : selectedWave == ShowWavesData.Both ? $"{defaultStr} ⎪ {dynPrice}p\n\n\n" : $"{defaultStr} ⎪ {dynPrice}p\n\n";

                            ChartText dynText = Chart.DrawText($"PriceWave_{objectIndex}", dynStr, Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], Color.FromName(strBullWaveColor.ToString()));
                            dynText.VerticalAlignment = VerticalAlignment.Top;
                            dynText.HorizontalAlignment = HorizontalAlignment.Center;
                        }
                        else
                        {
                            var selectedWave = ShowWavesInput;
                            string defaultStr = $"{Convert.ToDouble(interval_tlapse[0])}{interval_tlapse[1]}";
                            
                            string dynStr = "";
                            if (NumbersPositionInput == NumbersBarPositionData.Outside)
                                dynStr = selectedWave == ShowWavesData.No ? $"\n{defaultStr} ⎪ {dynPrice}p" : selectedWave == ShowWavesData.Both ? $"\n\n\n{defaultStr} ⎪ {dynPrice}p" : $"\n\n{defaultStr} ⎪ {dynPrice}p";
                            else
                                dynStr = selectedWave == ShowWavesData.No ? $"{defaultStr} ⎪ {dynPrice}p" : selectedWave == ShowWavesData.Both ? $"\n\n{defaultStr} ⎪ {dynPrice}p" : $"\n{defaultStr} ⎪ {dynPrice}p";
                            

                            ChartText dynText = Chart.DrawText($"PriceWave_{objectIndex}", dynStr, Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], Color.FromName(strBearWaveColor.ToString()));
                            dynText.HorizontalAlignment = HorizontalAlignment.Center;
                        }

                    }
                }
                else if (ShowOtherWaves_Input == ShowOtherWaves_Data.Price)
                {
                    if (direction == 1)
                    {
                        double Volume = cumulVolume();
                        if (Volume == 0)
                            return;

                        double dynPrice = cumulPrice(true);
                        PriceWave(dynPrice, true);
                    }
                    else if (direction == 3)
                    {
                        double Volume = cumulVolume();
                        if (Volume == 0)
                            return;

                        double dynPrice = cumulPrice(false);
                        PriceWave(dynPrice, false);
                    }
                    void PriceWave(double dynPrice, bool isBull)
                    {
                        if (isBull)
                        {
                            var selectedWave = ShowWavesInput;
                            
                            string dynStr = "";
                            if (NumbersPositionInput == NumbersBarPositionData.Outside)
                                dynStr = selectedWave == ShowWavesData.No ? $"{dynPrice}p\n\n" : selectedWave == ShowWavesData.Both ? $"{dynPrice}p\n\n\n\n" : $"{dynPrice}p\n\n\n";
                            else
                                dynStr = selectedWave == ShowWavesData.No ? $"{dynPrice}p" : selectedWave == ShowWavesData.Both ? $"{dynPrice}p\n\n\n" : $"{dynPrice}p\n\n";

                            ChartText dynText = Chart.DrawText($"PriceWave_{objectIndex}", dynStr, Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], Color.FromName(strBullWaveColor.ToString()));
                            dynText.VerticalAlignment = VerticalAlignment.Top;
                            dynText.HorizontalAlignment = HorizontalAlignment.Center;
                        }
                        else
                        {
                            var selectedWave = ShowWavesInput;
                            
                            string dynStr = "";
                            if (NumbersPositionInput == NumbersBarPositionData.Outside)
                                dynStr = selectedWave == ShowWavesData.No ? $"\n{dynPrice}p" : selectedWave == ShowWavesData.Both ? $"\n\n\n{dynPrice}p" : $"\n\n{dynPrice}p";
                            else
                                dynStr = selectedWave == ShowWavesData.No ? $"{dynPrice}p" : selectedWave == ShowWavesData.Both ? $"\n\n{dynPrice}p" : $"\n{dynPrice}p";
                            
                            ChartText dynText = Chart.DrawText($"PriceWave_{objectIndex}", dynStr, Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], Color.FromName(strBearWaveColor.ToString()));
                            dynText.HorizontalAlignment = HorizontalAlignment.Center;
                        }

                    }
                }
                else if (ShowOtherWaves_Input == ShowOtherWaves_Data.Time)
                {
                    if (direction == 1)
                    {
                        double Volume = cumulVolume();
                        if (Volume == 0)
                            return;

                        double cumlTime = cumulTime();

                        if (cumlTime == 0 || cumlTime.ToString() == "NaN")
                            return;

                        string[] interval_tlapse = DynTimeLapse(cumlTime);
                        TimeWave(interval_tlapse, true);
                    }
                    else if (direction == 3)
                    {
                        double Volume = cumulVolume();
                        if (Volume == 0)
                            return;

                        double cumlTime = cumulTime();

                        if (cumlTime == 0 || cumlTime.ToString() == "NaN")
                            return;

                        string[] interval_tlapse = DynTimeLapse(cumlTime);
                        TimeWave(interval_tlapse, false);
                    }
                    void TimeWave(string[] interval_tlapse, bool isBull)
                    {
                        if (isBull)
                        {
                            var selectedWave = ShowWavesInput;
                            string defaultStr = $"{Convert.ToDouble(interval_tlapse[0])}{interval_tlapse[1]}";
                            
                            string dynStr = "";
                            if (NumbersPositionInput == NumbersBarPositionData.Outside)
                                dynStr = selectedWave == ShowWavesData.No ? $"{defaultStr}\n\n" : selectedWave == ShowWavesData.Both ? $"{defaultStr}\n\n\n\n" : $"{defaultStr}\n\n\n";
                            else
                                dynStr = selectedWave == ShowWavesData.No ? $"{defaultStr}" : selectedWave == ShowWavesData.Both ? $"{defaultStr}\n\n\n" : $"{defaultStr}\n\n";

                            ChartText dynText = Chart.DrawText($"TimeWave_{objectIndex}", $"{dynStr}", Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], Color.FromName(strBullWaveColor.ToString()));
                            dynText.VerticalAlignment = VerticalAlignment.Top;
                            dynText.HorizontalAlignment = HorizontalAlignment.Center;
                        }
                        else
                        {
                            var selectedWave = ShowWavesInput;
                            string defaultStr = $"{Convert.ToDouble(interval_tlapse[0])}{interval_tlapse[1]}";
                            
                            string dynStr = "";
                            if (NumbersPositionInput == NumbersBarPositionData.Outside)
                                dynStr = selectedWave == ShowWavesData.No ? $"\n{defaultStr}" : selectedWave == ShowWavesData.Both ? $"\n\n\n{defaultStr}" : $"\n\n{defaultStr}";
                            else
                                dynStr = selectedWave == ShowWavesData.No ? $"{defaultStr}" : selectedWave == ShowWavesData.Both ? $"\n\n{defaultStr}" : $"\n{defaultStr}";

                            ChartText dynText = Chart.DrawText($"TimeWave_{objectIndex}", dynStr, Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], Color.FromName(strBearWaveColor.ToString()));
                            dynText.HorizontalAlignment = HorizontalAlignment.Center;
                        }

                    }
                }
            }
            // ========= Local Functions Area ==========
            void WW_Analysis(double cumlVolume, bool isBull)
            {
                //  --- Weis Wave Volume and Large_WW ---
                if (isBull)
                {
                    // Comparison Marks
                    string leftMark = "";
                    string rightMark = "";
                    if (ShowMarksInput == ShowMarksData.Left)
                        leftMark = cumlVolume > prevWaveVol_Bull ? "⮝" : "⮟";
                    else if (ShowMarksInput == ShowMarksData.Right)
                        rightMark = cumlVolume > prevWaveVol_Bull ? cumlVolume > prevWaveVol_Bear ? "" : "🡫" : cumlVolume > prevWaveVol_Bear ? "🡩" : "";
                    else if (ShowMarksInput == ShowMarksData.Both)
                    {
                        leftMark = cumlVolume > prevWaveVol_Bull ? "⮝" : "⮟";
                        rightMark = cumlVolume > prevWaveVol_Bull ? cumlVolume > prevWaveVol_Bear ? "" : "🡫" : cumlVolume > prevWaveVol_Bear ? "🡩" : "";
                    }

                    Color dynColor = (cumlVolume + prevWaves_Vol[0] + prevWaves_Vol[1] + prevWaves_Vol[2] + prevWaves_Vol[3]) / 5 * WW_Ratio < cumlVolume ? Color.FromName(strLargeColor.ToString()) : Color.FromName(strBullWaveColor.ToString());

                    string defaultStr = $"({leftMark}{SetDigits(cumlVolume)}{rightMark})";
                    var selectedWave = ShowWavesInput;
                    
                    string dynStr = "";
                    if (NumbersPositionInput == NumbersBarPositionData.Outside)
                        dynStr = selectedWave == ShowWavesData.Volume ? $"{defaultStr}\n\n" : selectedWave == ShowWavesData.Both ? $"{defaultStr}\n\n" : "";
                    else
                        dynStr = selectedWave == ShowWavesData.Volume ? $"{defaultStr}" : selectedWave == ShowWavesData.Both ? $"{defaultStr}" : "";

                    if (dynStr == "")
                        return;

                    ChartText dynText = Chart.DrawText("WWV" + objectIndex, $"{dynStr}", Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], dynColor);
                    dynText.VerticalAlignment = VerticalAlignment.Top;
                    dynText.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else
                {
                    // Comparison Marks
                    string leftMark = "";
                    string rightMark = "";

                    if (ShowMarksInput == ShowMarksData.Left)
                        leftMark = cumlVolume > prevWaveVol_Bear ? "⮟" : "⮝";
                    else if (ShowMarksInput == ShowMarksData.Right)
                        rightMark = cumlVolume > prevWaveVol_Bear ? cumlVolume > prevWaveVol_Bull ? "" : "🡩" : cumlVolume > prevWaveVol_Bull ? "🡫" : "";
                    else if (ShowMarksInput == ShowMarksData.Both)
                    {
                        leftMark = cumlVolume > prevWaveVol_Bear ? "⮟" : "⮝";
                        rightMark = cumlVolume > prevWaveVol_Bear ? cumlVolume > prevWaveVol_Bull ? "" : "🡩" : cumlVolume > prevWaveVol_Bull ? "🡫" : "";
                    }
                    
                    Color dynColor = (cumlVolume + prevWaves_Vol[0] + prevWaves_Vol[1] + prevWaves_Vol[2] + prevWaves_Vol[3]) / 5 * WW_Ratio < cumlVolume ? Color.FromName(strLargeColor.ToString()) : Color.FromName(strBearWaveColor.ToString());

                    string defaultStr = $"({leftMark}{SetDigits(cumlVolume)}{rightMark})";
                    var selectedWave = ShowWavesInput;
                    
                    string dynStr = "";
                    if (NumbersPositionInput == NumbersBarPositionData.Outside)
                        dynStr = selectedWave == ShowWavesData.Volume ? $"\n{defaultStr}" : selectedWave == ShowWavesData.Both ? $"\n{defaultStr}" : "";
                    else
                        dynStr = selectedWave == ShowWavesData.Volume ? $"{defaultStr}" : selectedWave == ShowWavesData.Both ? $"{defaultStr}" : "";

                    if (dynStr == "")
                        return;

                    ChartText dynText = Chart.DrawText("WWV" + objectIndex, $"{dynStr}", Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], dynColor);
                    dynText.HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
            // ========= ========== ==========
            void EvsR_Analysis(double cumlVolPrice, bool endWave, bool isBull)
            {
                // --- Effort vs Result ---
                if (isBull)
                {
                    // Comparison Marks
                    string leftMark = "";
                    string rightMark = "";                       
                    if (ShowMarksInput == ShowMarksData.Left)
                        leftMark = cumlVolPrice > prevVP_Bull ? "⮝" : "⮟";
                    else if (ShowMarksInput == ShowMarksData.Right)
                        rightMark = cumlVolPrice > prevVP_Bull ? cumlVolPrice > prevVP_Bear ? "" : "🡫" : cumlVolPrice > prevVP_Bear ? "🡩" : "";
                    else if (ShowMarksInput == ShowMarksData.Both)
                    {
                        leftMark = cumlVolPrice > prevVP_Bull ? "⮝" : "⮟";
                        rightMark = cumlVolPrice > prevVP_Bull ? cumlVolPrice > prevVP_Bear ? "" : "🡫" : cumlVolPrice > prevVP_Bear ? "🡩" : "";
                    }

                    // Large EvsR Color
                    bool colorEvsR = endWave ? EvsR_Large() : false;
                    Color dynColor = colorEvsR ? Color.FromName(strLargeColor.ToString()) : Color.FromName(strBullWaveColor.ToString());

                    string defaultStr = $"[{leftMark}{SetDigits(cumlVolPrice, true)}{rightMark}]";
                    var selectedWave = ShowWavesInput;
                    
                    string dynStr = "";
                    if (NumbersPositionInput == NumbersBarPositionData.Outside)
                        dynStr = selectedWave == ShowWavesData.EffortvsResult ? $"{defaultStr}\n\n" : selectedWave == ShowWavesData.Both ? $"{defaultStr}\n\n\n" : "";
                    else
                        dynStr = selectedWave == ShowWavesData.EffortvsResult ? $"{defaultStr}" : selectedWave == ShowWavesData.Both ? $"{defaultStr}\n\n" : "";

                    if (dynStr == "")
                        return;

                    ChartText dynText = Chart.DrawText("EvsR" + objectIndex, $"{dynStr}", Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], dynColor);
                    dynText.VerticalAlignment = VerticalAlignment.Top;
                    dynText.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else
                {
                    // Comparison Marks
                    string leftMark = "";
                    string rightMark = "";
                    if (ShowMarksInput == ShowMarksData.Left)
                        leftMark = cumlVolPrice > prevVP_Bear ? "⮟" : "⮝";
                    else if (ShowMarksInput == ShowMarksData.Right)
                        rightMark = cumlVolPrice > prevVP_Bear ? cumlVolPrice > prevVP_Bull ? "" : "🡩" : cumlVolPrice > prevVP_Bull ? "🡫" : "";
                    else if (ShowMarksInput == ShowMarksData.Both)
                    {
                        leftMark = cumlVolPrice > prevVP_Bear ? "⮟" : "⮝";
                        rightMark = cumlVolPrice > prevVP_Bear ? cumlVolPrice > prevVP_Bull ? "" : "🡩" : cumlVolPrice > prevVP_Bull ? "🡫" : "";
                    }

                    // Large EvsR Color
                    bool colorEvsR = endWave ? EvsR_Large() : false;
                    Color dynColor = colorEvsR ? Color.FromName(strLargeColor.ToString()) : Color.FromName(strBearWaveColor.ToString());

                    string defaultStr = $"[{leftMark}{SetDigits(cumlVolPrice, true)}{rightMark}]";
                    var selectedWave = ShowWavesInput;
                    
                    string dynStr = "";
                    if (NumbersPositionInput == NumbersBarPositionData.Outside)
                        dynStr = selectedWave == ShowWavesData.EffortvsResult ? $"\n{defaultStr}" : selectedWave == ShowWavesData.Both ? $"\n\n{defaultStr}" : "";
                    else
                        dynStr = selectedWave == ShowWavesData.EffortvsResult ? $"{defaultStr}" : selectedWave == ShowWavesData.Both ? $"\n{defaultStr}" : "";

                    if (dynStr == "")
                        return;

                    ChartText dynText = Chart.DrawText("EvsR" + objectIndex, $"{dynStr}", Bars.OpenTimes[lastCandle], Bars.ClosePrices[lastCandle], dynColor);
                    dynText.HorizontalAlignment = HorizontalAlignment.Center;
                }

                // --- Large EvsR [Yellow] ---
                bool EvsR_Large()
                {
                    bool haveZero = false;
                    foreach (var value in prevWaves_VP)
                    {
                        if (value == 0)
                        {
                            haveZero = true;
                            break;
                        }
                    }

                    if (!haveZero)
                    {
                        string haveSquare = (cumlVolPrice + prevWaves_VP[0] + prevWaves_VP[1] + prevWaves_VP[2] + prevWaves_VP[3]) / 5 * EvsR_Ratio < cumlVolPrice ? "DRAW IT" : "";
                        if (haveSquare == "DRAW IT")
                        {
                            string Filtered_Color()
                            {
                                string rawStr = Color.FromName(strLargeColor.ToString()).ToHexString();
                                string newRawStr = rawStr.Remove(0, 3);
                                string newValue = $"#C2{newRawStr}";
                                return newValue;
                            }
                            Color dynTColor = Filtered_Color();
                            if (isBull)
                            {
                                if (!BarsFillBool && !BarsOutlineBool)
                                {
                                    Chart.SetBarFillColor(lastCandle, Color.Transparent);
                                    Chart.SetBarOutlineColor(lastCandle, dynTColor);

                                    BullWickColor = dynTColor;
                                }
                                else if (BarsFillBool && BarsOutlineBool)
                                {
                                    Chart.SetBarFillColor(lastCandle, dynTColor);
                                    BullWickColor = Chart.ColorSettings.BullOutlineColor;
                                }
                                else if (!BarsFillBool && BarsOutlineBool)
                                {
                                    Chart.SetBarFillColor(lastCandle, Color.Transparent);
                                    BullWickColor = Chart.ColorSettings.BullOutlineColor;
                                }
                                else if (BarsFillBool && !BarsOutlineBool)
                                {
                                    Chart.SetBarColor(lastCandle, dynTColor);
                                    BullWickColor = dynTColor;
                                }
                                return true;
                            }
                            else
                            {
                                if (!BarsFillBool && !BarsOutlineBool)
                                {
                                    Chart.SetBarFillColor(lastCandle, Color.Transparent);
                                    Chart.SetBarOutlineColor(lastCandle, dynTColor);

                                    BearWickColor = dynTColor;
                                }
                                else if (BarsFillBool && BarsOutlineBool)
                                {
                                    Chart.SetBarFillColor(lastCandle, dynTColor);
                                    BearWickColor = Chart.ColorSettings.BearOutlineColor;
                                }
                                else if (!BarsFillBool && BarsOutlineBool)
                                {
                                    Chart.SetBarFillColor(lastCandle, Color.Transparent);
                                    BearWickColor = Chart.ColorSettings.BearOutlineColor;
                                }
                                else if (BarsFillBool && !BarsOutlineBool)
                                {
                                    Chart.SetBarColor(lastCandle, dynTColor);
                                    BearWickColor = dynTColor;
                                }
                                return true;
                            }
                        }
                        else
                            return false;
                    }
                    else
                        return false;
                }
            }
            // ========= ========== ==========
            void SetPrevWaves(double cumlVolume, double cumlVolPrice, bool prevIsBull_Bear, bool nextIsBull_Bear, bool isBull)
            {
                if (isBull)
                {
                    // --- Set Previous Bullish Wave Accumulated ---
                    // prev or next is Bear
                    // (!prevIsDown && !DirectionChanged && nextIsDown || prevIsDown && DirectionChanged && nextIsDown);
                    if (!prevIsBull_Bear && !DirectionChanged && nextIsBull_Bear || prevIsBull_Bear && DirectionChanged && nextIsBull_Bear)
                    {
                        prevWaveVol_Bull = cumlVolume;
                        prevVP_Bull = cumlVolPrice;
                        /* Exclude the most old wave, keep the 3 others and add current Wave value for most recent Wave
                           is for Effort vs Result Analysis | Large WW Analysis */
                        // Volume/cRenko = EvsR
                        double[] newPrevWavesVP = { prevWaves_VP[1], prevWaves_VP[2], prevWaves_VP[3], cumlVolPrice };
                        prevWaves_VP = newPrevWavesVP;
                        // onlyVolume = Large WW
                        double[] newPrevWavesVol = { prevWaves_Vol[1], prevWaves_Vol[2], prevWaves_Vol[3], cumlVolume };
                        prevWaves_Vol = newPrevWavesVol;
                    }
                    // (!prevIsDown && DirectionChanged && nextIsDown);
                    else if (!prevIsBull_Bear && DirectionChanged && nextIsBull_Bear)
                    {
                        prevWaveVol_Bull = prevCumul_Bull[0];
                        prevVP_Bull = prevCumul_Bull[1];

                        /* Exclude the most old wave, keep the 3 others and add current Wave value for most recent Wave
                           is for Effort vs Result Analysis | Large WW Analysis */
                        // Volume/cRenko = EvsR
                        double[] newPrevWavesVP = { prevWaves_VP[1], prevWaves_VP[2], prevWaves_VP[3], prevVP_Bull };
                        prevWaves_VP = newPrevWavesVP;
                        // onlyVolume = Large WW
                        double[] newPrevWavesVol = { prevWaves_Vol[1], prevWaves_Vol[2], prevWaves_Vol[3], prevWaveVol_Bull };
                        prevWaves_Vol = newPrevWavesVol;
                    }

                    // Prev Wave = For Left/Right Mark
                    double[] prevCumul = { cumlVolume, cumlVolPrice };
                    prevCumul_Bull = prevCumul;
                }
                else
                {
                    // --- Set Previous Bearish Wave Accumulated ---
                    // prev or next is Bull
                    // (!prevIsUp && !DirectionChanged && nextIsUp || prevIsUp && DirectionChanged && nextIsUp)
                    if (!prevIsBull_Bear && !DirectionChanged && nextIsBull_Bear || prevIsBull_Bear && DirectionChanged && nextIsBull_Bear)
                    {
                        prevWaveVol_Bear = cumlVolume;
                        prevVP_Bear = cumlVolPrice;
                        /* Exclude the most old wave, keep the 3 others and add current Wave value for most recent Wave
                           is for Effort vs Result Analysis | Large WW Analysis */
                        // Volume/cRenko = EvsR
                        double[] newPrevWavesVP = { prevWaves_VP[1], prevWaves_VP[2], prevWaves_VP[3], cumlVolPrice };
                        prevWaves_VP = newPrevWavesVP;
                        // onlyVolume = Large WW
                        double[] newPrevWavesVol = { prevWaves_Vol[1], prevWaves_Vol[2], prevWaves_Vol[3], cumlVolume };
                        prevWaves_Vol = newPrevWavesVol;
                    }
                    // (!prevIsUp && DirectionChanged && nextIsUp);
                    else if (!prevIsBull_Bear && DirectionChanged && nextIsBull_Bear)
                    {
                        prevWaveVol_Bear = prevCumul_Bear[0];
                        prevVP_Bear = prevCumul_Bear[1];
                        /* Exclude the most old wave, keep the 3 others and add current Wave value for most recent Wave
                           is for Effort vs Result Analysis | Large WW Analysis */
                        // Volume/cRenko = EvsR
                        double[] newPrevWavesVP = { prevWaves_VP[1], prevWaves_VP[2], prevWaves_VP[3], prevVP_Bear };
                        prevWaves_VP = newPrevWavesVP;
                        // onlyVolume = Large WW
                        double[] newPrevWavesVol = { prevWaves_Vol[1], prevWaves_Vol[2], prevWaves_Vol[3], prevWaveVol_Bear };
                        prevWaves_Vol = newPrevWavesVol;
                    }

                    // Prev Wave = For Left/Right Mark
                    double[] prevCumul = { cumlVolume, cumlVolPrice };
                    prevCumul_Bear = prevCumul;
                }
            }
            // ========= ========== ==========
            double cumulVolume()
            {
                double volume = 0.0;
                for (int i = firstCandle; i <= lastCandle; i++)
                    volume += VolumeRR[i];

                return volume;
            }
            // ========= ========== ==========
            double cumulRenko()
            {
                double renkoCount = 0;
                for (int i = firstCandle; i <= lastCandle; i++)
                    renkoCount += 1;

                return renkoCount;
            }
            // ========= ========== ==========
            double cumulPrice(bool isBull)
            {
                double price = 0;
                if (isBull)
                    price = Bars.ClosePrices[lastCandle] - Bars.OpenPrices[firstCandle];
                else
                    price = Bars.OpenPrices[firstCandle] - Bars.ClosePrices[lastCandle];
                price = Math.Round(price, Symbol.Digits);
                price = (price / Symbol.PipSize) / PipsMutliplier;

                return Math.Round(price, 2);
            }
            // ========= ========== ==========
            double cumulTime()
            {
                DateTime prevTime = Bars.OpenTimes[firstCandle - 1];
                DateTime currentTime = Bars.OpenTimes[lastCandle];
                TimeSpan interval = currentTime.Subtract(prevTime);
                double interval_ms = interval.TotalMilliseconds;

                return interval_ms;
            }
        }
        // ========= ========== ==========
        string[] DynTimeLapse(double interval_ms)
        {
            // Dynamic TimeLapse Format
            TimeSpan ts = TimeSpan.FromMilliseconds(interval_ms);

            string actualTimeLapse = "";
            double dynInterval = 0;

            double[] dividedTs = { ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds };
            for (int i = 0; i < dividedTs.Length; i++)
            {
                if (dividedTs[i] != 0)
                {
                    string dynEq = i == 4 ? "ms" : i == 3 ? "s" : i == 2 ? "m" : i == 1 ? "h" : "d";

                    if (dynEq == "ms")
                    {
                        dynInterval = ts.TotalMilliseconds;
                        actualTimeLapse = dynEq;
                    }
                    else if (dynEq == "s")
                    {
                        dynInterval = ts.TotalSeconds;
                        actualTimeLapse = dynEq;
                    }
                    else if (dynEq == "m")
                    {
                        dynInterval = ts.TotalMinutes;
                        actualTimeLapse = dynEq;
                    }
                    else if (dynEq == "h")
                    {
                        dynInterval = ts.TotalHours;
                        actualTimeLapse = dynEq;
                    }
                    else if (dynEq == "d")
                    {
                        dynInterval = ts.TotalDays;
                        actualTimeLapse = dynEq;
                    }
                    break;
                }
            }
            string[] interval_tlapse = { $"{Math.Round(dynInterval)}", actualTimeLapse };
            return interval_tlapse;
        }
    }
}