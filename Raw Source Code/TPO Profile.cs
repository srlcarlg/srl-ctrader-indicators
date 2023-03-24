/*
--------------------------------------------------------------------------------------------------------------------------------
                        TPO Profile
  It is VISUALLY BASED on the best TPO/Market Profile for MT4 
(riv-ay-TPOChart.v102-06 and riv-ay-MarketProfileDWM.v131-2)

  It's probably the second Free/Open-Source Market Profile you'll find for cTrader
without having to Pay/Trial or allow full access without knowing what it's doing...

  The first is Market Profile of EarnForex(https://www.earnforex.com/metatrader-indicators/MarketProfile/) for cTrader,
which my notebook doesn't run very well,
so I made this TPO Profile, which runs like a charm :)

  This has almost all the functionality of the riv-ay-TPOChart.v102-06 it was based on,
plus a few things I added, like:

--Preset Settings:
Optimized for most assets (Currencies/Metals/Indices) focusing on Precision/Performance Balance,
and of course it can't cover everything, but you can Customize if you need to.

--TPO Divided into Colums 
Just like in the books.

--Custom TPO Interval/rowHeight
Want more accuracy at the cost of more processing or just a custom TPO?
You can have both.


For Better Performance, Recompile it on cTrader with .NET 6.0 instead .NET 4.x.


AUTHOR: srlcarlg 
 
 
== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
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
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class TPOProfile : Indicator
    {
        [Parameter("Lookback:", DefaultValue = 5, MinValue = 1, MaxValue = 100, Group = "==== TPO Profile ====")]
        public int Lookback { get; set; }
        
        public enum ModeTPOData
        {
            Aggregated,
            Divided,
            Both,
        }

        [Parameter("Mode:", DefaultValue = ModeTPOData.Aggregated, Group = "==== TPO Profile ====")]
        public ModeTPOData ModeTPOInput { get; set; }
        
        public enum StyleTypeData
        {
            Letters,
            Squares,
            Pseudo_Histogram,
            Real_Histogram,
        }

        [Parameter("Style (onlyAgg):", DefaultValue = StyleTypeData.Real_Histogram, Group = "==== TPO Profile ====")]
        public StyleTypeData StyleTypeInput { get; set; }
                
        public enum ConfigTPOData
        {
            Predefined,
            Custom,
        }
        [Parameter("Config:", DefaultValue = ConfigTPOData.Predefined, Group = "==== TPO Profile ====")]
        public ConfigTPOData ConfigTPOInput { get; set; }
        
        
        [Parameter("Custom Interval:", DefaultValue = "Daily", Group = "==== Custom TPO Config ====")]
        public TimeFrame CustomInterval { get; set; }
        
        [Parameter("Custom Row Height:", DefaultValue = 1, MinValue = 0.2, Group = "==== Custom TPO Config ====")]
        public double CustomHeight { get; set; }
        
        [Parameter("Show L/S/H?", DefaultValue = true, Group = "==== Letters / Squares / (Pseudo Histogram | Real Histogram) ====")]
        public bool ShowLSH { get; set; }
        
        [Parameter("Color L/S/H:", DefaultValue = Colors.SkyBlue, Group = "==== Letters / Squares / (Pseudo Histogram | Real Histogram) ====")]
        public Colors RawColorLSH { get; set; }
        
        [Parameter("L/S/H inside VA:", DefaultValue = Colors.DeepSkyBlue, Group = "==== Letters / Squares / (Pseudo Histogram | Real Histogram) ====")]
        public Colors RawColorLSH_VA { get; set; }
        
        [Parameter("Opacity:", DefaultValue = 30, MinValue = 5, MaxValue = 100, Group = "==== Letters / Squares / (Pseudo Histogram | Real Histogram) ====")]
        public int OpacityLSH { get; set; }
        
        [Parameter("Opacity inside VA:", DefaultValue = 50, MinValue = 5, MaxValue = 100, Group = "==== Letters / Squares / (Pseudo Histogram | Real Histogram) ====")]
        public int OpacityLSH_VA { get; set; }
        
        public enum HistWidthData
        {
            _15,
            _30,
            _50,
            _70,
            _100
        }
        [Parameter("Histogram Width(%)", DefaultValue = HistWidthData._50, Group = "==== Real Histogram settings ====")]
        public HistWidthData HistWidthInput { get; set; }
        
        [Parameter("Fill Histogram?", DefaultValue = true, Group = "==== Real Histogram settings ====")]
        public bool FillHist { get; set; }
        
        [Parameter("Color by Session?:", DefaultValue = false, Group = "==== Real Histogram settings ====")]
        public bool BySession { get; set; }
        
        [Parameter("End 1 / Start 2:", DefaultValue = 6, Step = 0.1, MinValue = 0, MaxValue = 24, Group = "==== Session config ====")]
        public double Session_One { get; set; }
        
        [Parameter("End 2 / Start 3:", DefaultValue = 12, Step = 0.1, MinValue = 0, MaxValue = 24, Group = "==== Session config ====")]
        public double Session_Two { get; set; }
        
        [Parameter("Color Session 1:", DefaultValue = Colors.Green, Group = "==== Session config ====")]
        public Colors RawColorSS1 { get; set; }
        
        [Parameter("Color Session 2:", DefaultValue = Colors.Red, Group = "==== Session config ====")]
        public Colors RawColorSS2 { get; set; }
        
        [Parameter("Color Session 3:", DefaultValue = Colors.Blue, Group = "==== Session config ====")]
        public Colors RawColorSS3 { get; set; }
        
        
        [Parameter("Show OHLC Bar?", DefaultValue = true, Group = "==== Other settings ====")]
        public bool ShowOHLC { get; set; }
        
        [Parameter("Show Value Area?", DefaultValue = true, Group = "==== Other settings ====")]
        public bool ShowVA { get; set; }
        
        [Parameter("Show POC Migration?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ShowMigration { get; set; }
        
        [Parameter("Keep POC? (no VA)", DefaultValue = true, Group = "==== Other settings ====")]
        public bool KeepPOC { get; set; }
                
        [Parameter("Extended POCs?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ExtendPOC { get; set; }
           
        [Parameter("Extended VAs?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ExtendVA { get; set; }
        
        
        public enum ConfigInfoC
        {
            Top_Right,
            Top_Left,
            Bottom_Right,
            Bottom_Left,
        }
        
        [Parameter("OHLC Bar Color:", DefaultValue = Colors.Gray, Group = "==== Others ====")]
        public Colors RawColorOHLC { get; set; }
        
        [Parameter("Info Corner Position:", DefaultValue = ConfigInfoC.Bottom_Left, Group = "==== Others ====")]
        public ConfigInfoC ConfigInfoC_Input { get; set; }
        
        [Parameter("Info Corner Color:", DefaultValue = Colors.Snow, Group = "==== Others ====")]
        public Colors RawColorInfoC { get; set; }
        
        
        [Parameter("Color POC:", DefaultValue = Colors.Gold, Group = "==== Point of Control ====")]
        public Colors RawColorPOC { get; set; }
        
        [Parameter("LineStyle POC:", DefaultValue = LineStyle.Solid, Group = "==== Point of Control ====")]
        public LineStyle LineStylePOC { get; set; }
        
        [Parameter("Thickness POC:", DefaultValue = 2, MinValue = 1, MaxValue = 5, Group = "==== Point of Control ====")]
        public int ThicknessPOC { get; set; }
                
                
        [Parameter("Color VA:", DefaultValue = Colors.AliceBlue,  Group = "==== Value Area ====")]
        public Colors RawColorVA { get; set; }
        
        [Parameter("Opacity VA" , DefaultValue = 10, MinValue = 5, MaxValue = 100, Group = "==== Value Area ====")]
        public int OpacityVA { get; set; }
                
        [Parameter("LineStyle VA:", DefaultValue = LineStyle.Solid, Group = "==== Value Area ====")]
        public LineStyle LineStyleVA { get; set; }
               
        [Parameter("Thickness VA:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Value Area ====")]
        public int ThicknessVA { get; set; }
        
        [Parameter("Color VAH:", DefaultValue = Colors.PowderBlue , Group = "==== Value Area ====")]
        public Colors RawColorVAH { get; set; }
        
        [Parameter("Color VAL:", DefaultValue = Colors.PowderBlue, Group = "==== Value Area ====")]
        public Colors RawColorVAL { get; set; }
        
        
        [Parameter("Automatic FontSize?", DefaultValue = true , Group = "==== Aggregated Mode ====")]
        public bool AutoFontSize { get; set; } 
        
        [Parameter("Fixed Font Size:", DefaultValue = 12, MinValue = 1, MaxValue = 80, Group = "==== Aggregated Mode ====")]
        public int FixedFontSize { get; set; } 
        
        [Parameter("Spacing?:", DefaultValue = false, Group ="==== Aggregated Mode ====")]
        public bool SpacingBetween { get; set; }

                
        [Parameter("Color Letters:", DefaultValue = Colors.White , Group = "==== Divided Mode ====")]
        public Colors RawColor_Letters { get; set; } 
        
        [Parameter("Opacity:", DefaultValue = 100, MinValue = 5, MaxValue = 100, Group = "==== Divided Mode ====")]
        public int OpacityLetters { get; set; }
        
        [Parameter("Close BarUP:", DefaultValue = Colors.Blue , Group = "==== Divided Mode ====")]
        public Colors RawColor_CandleUP { get; set; }
        
        [Parameter("Close BarDown:", DefaultValue = Colors.Blue, Group = "==== Divided Mode ====")]
        public Colors RawColor_CandleDown { get; set; }
        
        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }
        [Parameter("Visually based in MT4", DefaultValue = "riv-ay-(TPOChart/MarketProfileDWM)", Group = "==== Credits ====")]
        public string Credits_2 { get; set; }
        
        [Output("POC Migration Line", LineStyle=LineStyle.LinesDots, LineColor = "PaleGoldenrod", Thickness = 1)]
        public IndicatorDataSeries POCMigration { get; set; }
                
        private VerticalAlignment V_Align = VerticalAlignment.Top;
        private HorizontalAlignment H_Align = HorizontalAlignment.Center;
        private bool Wrong = false;
        
        private string allLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        
        private List<double> allSegmentsPrices = new List<double>();        
        private IDictionary<double, string> allTPOsRank = new Dictionary<double, string>();
        private List<ChartText> allTPOLines= new List<ChartText>();     
        private List<ChartText> allCandlesTPO= new List<ChartText>();  
        
        private List<ChartText> Fonts_allTPOLines= new List<ChartText>();
        private List<ChartTrendLine> allPOCsLines = new List<ChartTrendLine>();   
        private List<ChartTrendLine> allVALines = new List<ChartTrendLine>(); 
        
        private IDictionary<int, ChartRectangle> allRectangles = new Dictionary<int, ChartRectangle>();
        private IDictionary<int, ChartRectangle> Session1Rectangles = new Dictionary<int, ChartRectangle>();
        private IDictionary<int, ChartRectangle> Session2Rectangles = new Dictionary<int, ChartRectangle>();
        
        private double HeightPips = 4;
        private TimeFrame TPO_TF;
        private Bars dynTF;
        
        private double rowHeight = 0;
        private double drawHeight = 0;
        
        private int previousLetter_Index = 0;
        private double[] priceVA_LHP = {0, 0, 0};

        private bool isLive = false;
        
        private Color LSHColor;
        private Color LSHColorVA;
        private int LSH_VA_Opacity;
        
        private Color VAColor;
        private Color LettersColor;  
        private int Letters_Opacity;
        
        private int cleanedIndex;
        private int updatedFontsize = 0;
        
        private double prevPrice;
        
        protected override void Initialize()
        {
            // ========== Predefined Config ==========
            if (ConfigTPOInput == ConfigTPOData.Predefined && (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Day3))
            {
                if (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Minute4)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute) {
                        TPO_TF = TimeFrame.Hour; 
                        SetHeight();
                    }
                    else if (Chart.TimeFrame == TimeFrame.Minute2) {
                        TPO_TF = TimeFrame.Hour2;
                        SetHeight();
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Minute4) {
                        TPO_TF = TimeFrame.Hour3;
                        SetHeight();
                    }
                    void SetHeight() {
                        SetHeightPips(1, 15);
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute5) {
                        TPO_TF = TimeFrame.Hour4;
                        SetHeight();
                    }
                    else if (Chart.TimeFrame == TimeFrame.Minute6) {
                        TPO_TF = TimeFrame.Hour6;
                        SetHeight();
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Minute8) {
                        TPO_TF = TimeFrame.Hour8;
                        SetHeight();
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Minute10) {
                        TPO_TF = TimeFrame.Hour12;
                        SetHeight();
                    }
                    void SetHeight() {
                        SetHeightPips(2, 40);
                    }
                    
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour) {
                        TPO_TF = TimeFrame.Daily;
                        SetHeightPips(4, 80);
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Hour8) {
                        TPO_TF = TimeFrame.Weekly;
                        SetHeightPips(6, 180);
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Day3) {
                    TPO_TF = TimeFrame.Monthly;
                    SetHeightPips(8, 280);
                }
            }
            else
            {   
                if (ConfigTPOInput == ConfigTPOData.Predefined)
                {
                    string Msg = "'Predefined Config' is designed only for Standard Timeframe (Minutes, Hours, Days) \n Weekly and Monthly is not currently supported \n\n use 'Custom Config' to others Chart Timeframes (Renko/Range/Ticks).";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                string[] timeBased = {"Minute", "Hour", "Daily", "Day", "Weekly", "Monthly"};
                if (!(timeBased.Any(CustomInterval.Name.ToString().Contains)))
                {
                    string Msg = $"'TPO Interval' is designed ONLY for TIME \n (Minutes, Hours, Days, Monthly)";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                if (CustomInterval == Chart.TimeFrame || CustomInterval < Chart.TimeFrame)
                {
                    string comp = CustomInterval == Chart.TimeFrame ? "==" : "<";
                    string Msg = $"TPO Interval ({CustomInterval.ShortName}) {comp} Chart Timeframe ({Chart.TimeFrame.ShortName})\nWhy?";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                if (CustomInterval < TimeFrame.Minute15)
                {
                    string Msg = "The minimum 'Custom Interval' is 15 minutes";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                
                TPO_TF = CustomInterval;
                HeightPips = CustomHeight;
            }
            
            void SetHeightPips(double digits5, double digits2)
            {
                if (Symbol.Digits == 5)
                    HeightPips = digits5;
                else if (Symbol.Digits == 2)
                {
                    HeightPips = digits2;
                    if (Symbol.PipSize == 0.1)
                        HeightPips /= 2;
                }
            }
            // ============================================================================
            dynTF = MarketData.GetBars(TPO_TF);
            if (dynTF.ClosePrices.Count < Lookback)
            {
                while (dynTF.ClosePrices.Count < Lookback)
                {
                    int loadedCount = dynTF.LoadMoreHistory();
                    Print($"Loaded {loadedCount}, {TPO_TF.ShortName} Bars, Current Bar Date: {dynTF.OpenTimes.Reverse().LastOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
            }
            
            // Ex: 4 pips to TPO calculation(rowHeight) = 2 pips between letters (drawHeight)
            rowHeight = (Symbol.PipSize) * HeightPips;
            drawHeight = (Symbol.PipSize) * (HeightPips/2);
            
            // ===== Colors with Opacity =====
            int LSH_Opacity = (int)(2.55 * OpacityLSH);
            Color rawLSH = Color.FromName(RawColorLSH.ToString());
            LSHColor = Color.FromArgb(LSH_Opacity, rawLSH.R, rawLSH.G, rawLSH.B);
            
            LSH_VA_Opacity = (int)(2.55 * OpacityLSH_VA);
            Color rawLSH_VA = Color.FromName(RawColorLSH_VA.ToString());
            LSHColorVA = Color.FromArgb(LSH_VA_Opacity, rawLSH_VA.R, rawLSH_VA.G, rawLSH_VA.B);
            
            int VA_Opacity = (int)(2.55 * OpacityVA);
            Color rawVA = Color.FromName(RawColorVA.ToString());
            VAColor = Color.FromArgb(VA_Opacity, rawVA.R, rawVA.G, rawVA.B);
            
            Letters_Opacity = (int)(2.55 * OpacityLetters);
            Color rawLetters = Color.FromName(RawColor_Letters.ToString());
            LettersColor = Color.FromArgb(Letters_Opacity, rawLetters.R, rawLetters.G, rawLetters.B);
            
            // === Info Corner ===
            Color rawColor = Color.FromName(RawColorInfoC.ToString());
            Color InfoColor = Color.FromArgb((int)(2.55 * 50), rawColor.R, rawColor.G, rawColor.B);
            string strMode = ConfigTPOInput == ConfigTPOData.Predefined ? "Predefined" : "Custom";
            string tpoInfo = $"{strMode} \n" +
                             $"TPO Data: {Chart.TimeFrame.ShortName} \n" + 
                             $"TPO Interval: {TPO_TF.ShortName} \n" + 
                             $"Row Height: {HeightPips} pip(s) \n";
            
            VerticalAlignment v_align = VerticalAlignment.Bottom;
            HorizontalAlignment h_align = HorizontalAlignment.Left;
            if (ConfigInfoC_Input == ConfigInfoC.Bottom_Right)
                h_align = HorizontalAlignment.Right;
            else if (ConfigInfoC_Input == ConfigInfoC.Top_Left)
                v_align = VerticalAlignment.Top;
            else if (ConfigInfoC_Input == ConfigInfoC.Top_Right)
            {
                v_align = VerticalAlignment.Top;
                h_align = HorizontalAlignment.Right;
            }
            Chart.DrawStaticText("TPO Info", tpoInfo, v_align, h_align, InfoColor);
            
            if (AutoFontSize && StyleTypeInput != StyleTypeData.Real_Histogram)
                Chart.ZoomChanged += Chart_ZoomChanged;

            DrawOnScreen("Calculating...");
            Second_DrawOnScreen("Taking too long? You can: \n 1) Increase the rowHeight \n 2) Disable the Value Area (High Performance) \n");
            if (Application.UserTimeOffset.ToString() != "03:00:00")
                Third_DrawOnScreen("Set your UTC to UTC+3");

        }

        public override void Calculate(int index)
        {
            if (Wrong)
                return;
            
            // ==== Removing Messages ====
            if (!IsLastBar)
            {
                DrawOnScreen("");
                Second_DrawOnScreen("");
                Third_DrawOnScreen("");
            }
                
            Bars TF_Bars = dynTF;
            // Get Index of TPO Interval to continue only in Lookback
            int iVerify = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (TF_Bars.ClosePrices.Count - iVerify > Lookback)
                return;
            
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);
            
            // ====== Extended POC/VA ========
            if (ExtendPOC && allPOCsLines.Count != 0 && IsLastBar && allPOCsLines.LastOrDefault().Y2 != priceVA_LHP[2])
            {
                for (int tl=0; tl < allPOCsLines.Count; tl++)
                {
                    allPOCsLines[tl].Time2 = Bars.OpenTimes[index];
                    string dynDate = TPO_TF == TimeFrame.Daily ? allPOCsLines[tl].Time1.Date.AddDays(1).ToString().Replace("00:00:00", "") : allPOCsLines[tl].Time1.Date.ToString();
                    Chart.DrawText($"POC{allPOCsLines[tl].Time1}", $"{dynDate}", Bars.OpenTimes[index], allPOCsLines[tl].Y2+drawHeight, Color.FromName(RawColorPOC.ToString()));
                }
            }
            
            if (ExtendVA && allVALines.Count != 0 && IsLastBar && allVALines.LastOrDefault().Time2 != Bars.OpenTimes[index])
            {
                for (int tl=0; tl < allVALines.Count; tl++)
                    allVALines[tl].Time2 = Bars.OpenTimes[index];
            }
            
            // === Clean Dicts/others ===
            if (index == indexStart && index != cleanedIndex || (index-1) == indexStart && (index-1) != cleanedIndex) 
            {
                allSegmentsPrices.Clear();
                allTPOsRank.Clear();
                allTPOLines.Clear();
                allCandlesTPO.Clear();
                allRectangles.Clear();
                Session1Rectangles.Clear();
                Session2Rectangles.Clear();
                double[] VAforColor = {0, 0, 0};
                priceVA_LHP = VAforColor;
                previousLetter_Index = 0;
                cleanedIndex = index == indexStart ? index : (index-1);
            }
            
            // Historical data 
            if (!IsLastBar)
                TPO(index, indexStart, false); 
            else
            {
                isLive = true;
                // "Repaint" if the price moves half of rowHeight
                if (Bars.ClosePrices[index] >= (prevPrice+drawHeight) ||  Bars.ClosePrices[index] <= (prevPrice-drawHeight))
                {                        
                    for (int i=indexStart; i <= index; i++)
                    {
                        if (i == indexStart) {
                            allTPOsRank.Clear();
                            previousLetter_Index = 0;
                            if (BySession)
                                Repaint_Sessions();
                        }
                            
                        TPO(i, indexStart, true); 
                    } 
                    prevPrice = Bars.ClosePrices[index];
                }
            }
            
            void Repaint_Sessions()
           {
               foreach (int key in Session1Rectangles.Keys) 
                {
                    try {
                    Chart.RemoveObject(Session1Rectangles[key].Name);
                    } catch {}
                }
                foreach (int key in Session2Rectangles.Keys) 
                {
                    try {
                    Chart.RemoveObject(Session2Rectangles[key].Name);
                    } catch {};
                }    
                Session1Rectangles.Clear(); Session2Rectangles.Clear();
            }


        }
        
        private void TPO(int index, int iStart, bool resizeHL)
        {
            // ======= Highest and Lowest =======
            Bars TF_Bars = dynTF;
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            
            double highest = TF_Bars.HighPrices[TF_idx], lowest = TF_Bars.LowPrices[TF_idx], open = TF_Bars.OpenPrices[TF_idx];
          
            // ======= Chart Segmentation =======
            List<double> currentSegments = new List<double>();        
            double prev_segment = open;    
            while (prev_segment >= (lowest-rowHeight))
            {
                currentSegments.Add(prev_segment);
                prev_segment = Math.Abs(prev_segment - rowHeight);
            }
            prev_segment = open;    
            while (prev_segment <= (highest+rowHeight))
            {
                currentSegments.Add(prev_segment);
                prev_segment = Math.Abs(prev_segment + rowHeight);
            }
            allSegmentsPrices = currentSegments.OrderBy(x => x).ToList();
            
            // ======= CandlesTPO Columns + Letters =======
            if (previousLetter_Index >= allLetters.Length)
                previousLetter_Index = 0;
            
            if (resizeHL)
            {
                string Letter = allLetters[previousLetter_Index].ToString();
                CandleTPO($"{Letter}", index); 
                previousLetter_Index += 1;
            }
            else
            {
                string Letter = allLetters[previousLetter_Index].ToString();
                if (!IsLastBar)
                    previousLetter_Index = previousLetter_Index == 0 ? 1 : previousLetter_Index + 1;
                else
                    Letter = allLetters[previousLetter_Index].ToString();
                
                if (!isLive)
                    CandleTPO($"{Letter}", index);  
            }

            // ======= Drawing TPO =======
            if (allSegmentsPrices.Count == 0)
                return;
            
            for (int i = 0; i < allSegmentsPrices.Count; i++)
            {
                double priceKey = allSegmentsPrices[i];
                if (!allTPOsRank.ContainsKey(priceKey))
                    continue;
                    
                if (ModeTPOInput != ModeTPOData.Divided && ShowLSH)
                {      
                    if (StyleTypeInput != StyleTypeData.Real_Histogram)
                    {
                        string dynStr = StyleTypeInput == StyleTypeData.Letters ? allTPOsRank[priceKey] : Squares_PseHistogram(allTPOsRank[priceKey].Length);
                        ChartText lineTPO = Chart.DrawText($"TPO{i}_{iStart}", dynStr, iStart, allSegmentsPrices[i], LSHColor);
                        if (!AutoFontSize)
                            lineTPO.FontSize = FixedFontSize;
                        else {
                            if (updatedFontsize != 0)
                                lineTPO.FontSize = updatedFontsize;
                        }
                        
                        allTPOLines.Add(lineTPO);
                        Fonts_allTPOLines.Add(lineTPO);
                    }
                    else
                    {
                        /*
                        Indeed, the value of X-Axis is simply a rule of three, 
                        where the maximum value will be the maxLength (in Milliseconds),
                        from there the math adjusts the histograms.
                            
                            MaxValue    maxLength(ms)
                               x             ?(ms)
                        
                        The values 1.25 and 4 are the manually set values
                        */
                        string largestTPO = allTPOsRank.Values.OrderByDescending(x => x.Length).First();
                        // string largestTPO = allTPOsRank.Values.MaxBy(x => x.Length); in .NET Framework 6.0
                        
                        double lowerSegment = allSegmentsPrices[i]-rowHeight;
                        double upperSegment = allSegmentsPrices[i];
                        
                        double maxLength = Bars[index].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                        var selected = HistWidthInput;
                        double maxWidth = selected == HistWidthData._15 ? 1.25 : selected == HistWidthData._30 ? 1.50 : selected == HistWidthData._50 ? 2 : 4;
                        double proportion = allTPOsRank[priceKey].Length * (maxLength-(maxLength/maxWidth));
                        if (selected == HistWidthData._100)
                            proportion = allTPOsRank[priceKey].Length * maxLength;
                        double dynLength = proportion / largestTPO.Length;
                        
                        ChartRectangle volHist;
                        if (!BySession)
                        {
                            volHist = Chart.DrawRectangle($"{iStart}_{i}_", Bars.OpenTimes[iStart], lowerSegment, Bars.OpenTimes[iStart].AddMilliseconds(dynLength), upperSegment, LSHColor);
                            
                            if (allRectangles.ContainsKey(i))
                                allRectangles[i] = volHist;
                            else
                                allRectangles.Add(i, volHist);
                        }
                        else 
                        {
                            volHist = Chart.DrawRectangle($"{iStart}_{i}_", Bars.OpenTimes[iStart], lowerSegment, Bars.OpenTimes[iStart], upperSegment, Color.Transparent);
                            bool endSSOne = Bars.OpenTimes[index] <= Bars.OpenTimes[index].Date.AddHours(Session_One);                        
                            bool endSsTwo = Bars.OpenTimes[index] <= Bars.OpenTimes[index].Date.AddHours(Session_Two);
                            
                            bool startSsGMT = Bars.OpenTimes[index] >= Bars.OpenTimes[index].Date.AddHours(21);
                            
                            double High = Bars.HighPrices[index];
                            double Low = Bars.LowPrices[index];
                            bool havePrice = (allSegmentsPrices[i] < High && allSegmentsPrices[i] > Low);
                            
                            Color rawSS1 = Color.FromName(RawColorSS1.ToString());
                            Color opacifiedSS1 = Color.FromArgb(OpacityLSH, rawSS1.R, rawSS1.G, rawSS1.B);
                            
                            Color rawSS2 = Color.FromName(RawColorSS2.ToString());
                            Color opacifiedSS2 = Color.FromArgb(OpacityLSH, rawSS2.R, rawSS2.G, rawSS2.B);
                            
                            Color rawSS3 = Color.FromName(RawColorSS3.ToString());
                            Color opacifiedSS3 = Color.FromArgb(OpacityLSH, rawSS3.R, rawSS3.G, rawSS3.B);
                            
                            if (endSSOne && havePrice || (!endSSOne && !endSsTwo && startSsGMT && havePrice)) 
                            {                                      
                                volHist = Chart.DrawRectangle($"{iStart}_{i}_First", Bars.OpenTimes[iStart], lowerSegment, Bars.OpenTimes[iStart].AddMilliseconds(dynLength), upperSegment, opacifiedSS1);
                                // =================
                                if (Session1Rectangles.ContainsKey(i))
                                    Session1Rectangles[i] = volHist;
                                else
                                    Session1Rectangles.Add(i, volHist);
                            }
                            else if (!endSSOne && endSsTwo && havePrice) 
                            {
                                if (Session1Rectangles.ContainsKey(i))
                                    volHist = Chart.DrawRectangle($"{iStart}_{i}_Second", Session1Rectangles[i].Time2, lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, opacifiedSS2);
                                else
                                    volHist = Chart.DrawRectangle($"{iStart}_{i}_Second", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, opacifiedSS2);
                                // =================
                                if (Session2Rectangles.ContainsKey(i))
                                    Session2Rectangles[i] = volHist;
                                else
                                    Session2Rectangles.Add(i, volHist);
                            }
                            else if (!endSsTwo && havePrice) 
                            {
                                if (Session2Rectangles.ContainsKey(i))
                                    volHist = Chart.DrawRectangle($"{iStart}_{i}_Three", Session2Rectangles[i].Time2, lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, opacifiedSS3);
                                else
                                {
                                    if (!Session1Rectangles.ContainsKey(i))
                                        volHist = Chart.DrawRectangle($"{iStart}_{i}_Three", Bars.OpenTimes[iStart], lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, opacifiedSS3);
                                    else
                                        volHist = Chart.DrawRectangle($"{iStart}_{i}_Three", Session1Rectangles[i].Time2, lowerSegment, Bars[iStart].OpenTime.AddMilliseconds(dynLength), upperSegment, opacifiedSS3);
                                }
                            }
                        }
                        if (FillHist)
                            volHist.IsFilled = true;
                    }
                }
                // ==============================================================================================================================================
                // ============= Coloring Letters + VAL / VAH / POC =============
                if (ShowVA)
                {
                    double[] VAL_VAH_POC = VA_Calculation();
                    
                    if (ShowMigration)
                        POCMigration[index] = VAL_VAH_POC[2]-rowHeight;
                    
                    // ==========================
                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[2]-rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[2]-rowHeight, Color.FromName(RawColorPOC.ToString()));
                    ChartTrendLine vah;
                    if (StyleTypeInput == StyleTypeData.Real_Histogram)
                        vah = Chart.DrawTrendLine($"VAH_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[1]+rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[1]+rowHeight, Color.FromName(RawColorVAH.ToString()));
                    else
                        vah = Chart.DrawTrendLine($"VAH_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[1]-Symbol.PipSize, Bars.OpenTimes[index], VAL_VAH_POC[1]-Symbol.PipSize, Color.FromName(RawColorVAH.ToString()));
                    
                    ChartTrendLine val = Chart.DrawTrendLine($"VAL_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[0], Bars.OpenTimes[index], VAL_VAH_POC[0], Color.FromName(RawColorVAL.ToString()));
                    
                    double[] VAforColor = {VAL_VAH_POC[0], VAL_VAH_POC[1], VAL_VAH_POC[2]};
                    priceVA_LHP = VAforColor;
                    
                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                    vah.LineStyle = LineStyleVA; vah.Thickness = ThicknessVA; vah.Comment = "VAH";
                    val.LineStyle = LineStyleVA; val.Thickness = ThicknessVA; val.Comment = "VAL"; 
                    
                    // ==== POC Lines ====
                    if (allPOCsLines.Contains(poc))
                    {
                        for (int tl=0; tl < allTPOLines.Count; tl++)
                        {
                            if (allPOCsLines[tl].Time1 == poc.Time1) {
                                allPOCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        allPOCsLines.Add(poc);
                        
                    // ==== VAH / VAL Lines ====
                    if (allVALines.Contains(vah) || allVALines.Contains(val))
                    {
                        for (int tl=0; tl < allVALines.Count; tl++)
                        {
                            if (allVALines[tl].Comment == "VAH" && allVALines[tl].Time1 == vah.Time1)
                                allVALines[tl] = vah;
                            else if (allVALines[tl].Comment == "VAL" && allVALines[tl].Time1 == val.Time1)
                                allVALines[tl] = val;
                        }
                    }
                    else
                    {
                        if (!allVALines.Contains(vah))
                            allVALines.Add(vah);
                        if (!allVALines.Contains(val))
                            allVALines.Add(val);
                    }   
                                                                
                    Color rawPOC = Color.FromName(RawColorPOC.ToString());
                    Color opacifiedPOC = Color.FromArgb(LSH_VA_Opacity, rawPOC.R, rawPOC.G, rawPOC.B);
                    
                    Color rawVAH = Color.FromName(RawColorVAH.ToString());
                    Color opacifiedVAH = Color.FromArgb(LSH_VA_Opacity, rawVAH.R, rawVAH.G, rawVAH.B);
                    
                    Color rawVAL = Color.FromName(RawColorVAL.ToString());
                    Color opacifiedVAL = Color.FromArgb(LSH_VA_Opacity, rawVAL.R, rawVAL.G, rawVAL.B);
                    if (StyleTypeInput == StyleTypeData.Real_Histogram)
                    {
                        // =========== Coloring Retangles ============
                        foreach (int key in allRectangles.Keys)
                        {                            
                            if (allRectangles[key].Y1 > priceVA_LHP[0] && allRectangles[key].Y1 < priceVA_LHP[1])
                                allRectangles[key].Color = LSHColorVA;
                            
                            if (allRectangles[key].Y1 == priceVA_LHP[2]-rowHeight)
                                allRectangles[key].Color = opacifiedPOC;
                            
                            if (allRectangles[key].Y1 == priceVA_LHP[1])
                                allRectangles[key].Color = opacifiedVAH;
                            else if (allRectangles[key].Y1 == priceVA_LHP[0])
                                allRectangles[key].Color = opacifiedVAL;
                        }
                    }
                    else 
                    {
                        // =========== Coloring Letters ============
                        for (int k=0; k < allTPOLines.Count; k++)
                        {
                            if (allTPOLines[k].Y > priceVA_LHP[0] && allTPOLines[k].Y < priceVA_LHP[1]+drawHeight)
                                allTPOLines[k].Color = LSHColorVA;
            
                            if (allTPOLines[k].Y == priceVA_LHP[2])
                                allTPOLines[k].Color = opacifiedPOC;
                                
                            if (allTPOLines[k].Y == priceVA_LHP[1])
                                allTPOLines[k].Color = opacifiedVAH;
                            else if (allTPOLines[k].Y == priceVA_LHP[0]+rowHeight)
                                allTPOLines[k].Color = opacifiedVAL;
                        }
                    }
                }
                else if (!ShowVA && KeepPOC)
                {
                    string largestTPO = allTPOsRank.Values.OrderByDescending(x => x.Length).First();
                    // string largestTPO = allTPOsRank.Values.MaxBy(x => x.Length); in .NET Framework 6.0
                    double priceLTPO = 0;
                    for (int k = 0; k < allTPOsRank.Count; k++)
                    {
                        if (allTPOsRank.ElementAt(k).Value == largestTPO) {
                            priceLTPO = allTPOsRank.ElementAt(k).Key;
                            break;
                        }
                    }
                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], priceLTPO-rowHeight, Bars.OpenTimes[index], priceLTPO-rowHeight, Color.FromName(RawColorPOC.ToString()));
                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                    
                    if (ShowMigration)
                        POCMigration[index] = priceLTPO-rowHeight;
    
                    // ==== POC Lines ====
                    if (allPOCsLines.Contains(poc))
                    {
                        for (int tl=0; tl < allTPOLines.Count; tl++)
                        {
                            if (allPOCsLines[tl].Time1 == poc.Time1) {
                                allPOCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        allPOCsLines.Add(poc);
                    
    
                    Color rawPOC = Color.FromName(RawColorPOC.ToString());
                    Color opacifiedPOC = Color.FromArgb(LSH_VA_Opacity, rawPOC.R, rawPOC.G, rawPOC.B);
                    if (StyleTypeInput == StyleTypeData.Real_Histogram)
                    {
                        // =========== Coloring Retangles ============
                        foreach (int key in allRectangles.Keys)
                        { 
                            if (allRectangles[key].Y1 == priceLTPO-rowHeight)
                                allRectangles[key].Color = opacifiedPOC;
                        }
                    }
                    else
                    {
                        // =========== Coloring Letters ============
                        for (int k=0; k < allTPOLines.Count; k++)
                        {
                            if (allTPOLines[k].Y == priceLTPO) {   
                                allTPOLines[k].Color = opacifiedPOC;
                                break;
                            }
                        }
                    }
                }
            }
            string Squares_PseHistogram(int tpoLength)
            {
                StringBuilder builder = new StringBuilder();
                for (int i=0; i < tpoLength; i++)
                {
                    if (StyleTypeInput == StyleTypeData.Squares)
                        builder.Append("⬛");
                    else
                        builder.Append("▆");
                }
                return builder.ToString();
            }
           
            // ====== Rectangle VA ======
            if (ShowVA && priceVA_LHP[0] != 0) 
            {
                ChartRectangle rectVA;
                if (StyleTypeInput == StyleTypeData.Real_Histogram)
                    rectVA = Chart.DrawRectangle($"{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], priceVA_LHP[0], Bars.OpenTimes[index], priceVA_LHP[1]+rowHeight, VAColor);
                else
                    rectVA = Chart.DrawRectangle($"{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], priceVA_LHP[0], Bars.OpenTimes[index], priceVA_LHP[1]-Symbol.PipSize, VAColor);
                
                rectVA.IsFilled = true;
            }
            
            if (ModeTPOInput == ModeTPOData.Divided || ModeTPOInput == ModeTPOData.Both)
            {
                Color rawUP = Color.FromName(RawColor_CandleUP.ToString());
                Color opacifiedUP = Color.FromArgb(Letters_Opacity, rawUP.R, rawUP.G, rawUP.B);
                Color rawDown = Color.FromName(RawColor_CandleDown.ToString());
                Color opacifiedDW = Color.FromArgb(Letters_Opacity, rawDown.R, rawDown.G, rawDown.B);
                
                Color isBullish = Bars.ClosePrices[index] > Bars.OpenPrices[index] ? opacifiedUP : opacifiedDW;
                ChartText iconBarClose =  Chart.DrawText($"Close_{Bars.OpenTimes[index]}", "▶", Bars.OpenTimes[index], Bars.ClosePrices[index], isBullish);
                iconBarClose.VerticalAlignment = VerticalAlignment.Center;
                iconBarClose.HorizontalAlignment = HorizontalAlignment.Left;
                iconBarClose.FontSize = 8;   
            }

            if (!ShowOHLC)
                return;
            ChartText iconOpenSession =  Chart.DrawText($"Start{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], TF_Bars.OpenPrices[TF_idx], Color.FromName(RawColorOHLC.ToString()));
            ChartText iconCloseSession =  Chart.DrawText($"End{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], Bars.ClosePrices[index], Color.FromName(RawColorOHLC.ToString()));
            iconOpenSession.VerticalAlignment = VerticalAlignment.Center;
            iconOpenSession.HorizontalAlignment = HorizontalAlignment.Left;
            iconOpenSession.FontSize = 14;
            iconCloseSession.VerticalAlignment = VerticalAlignment.Center;
            iconCloseSession.HorizontalAlignment = HorizontalAlignment.Right;
            iconCloseSession.FontSize = 14;
            
            ChartTrendLine Session = Chart.DrawTrendLine($"Session{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], lowest, TF_Bars.OpenTimes[TF_idx], highest, Color.FromName(RawColorOHLC.ToString()));
            Session.Thickness = 3;
            
        }
        
        
        // ====== Functions Area ======       
        private void CandleTPO(string Letter, int index)
        {
            double High = Bars.HighPrices[index];
            double Low = Bars.LowPrices[index];

            int totalLetters = 0;
            for (int i = 0; i < allSegmentsPrices.Count; i++)
            {
                if (allSegmentsPrices[i] < High && allSegmentsPrices[i] > Low)
                    totalLetters += 1;
            }

            double prev_segment = High;
            for (int i = 0; i <= totalLetters; i++)
            {
                if (ModeTPOInput == ModeTPOData.Divided || ModeTPOInput == ModeTPOData.Both)
                {
                    ChartText dyntext = Chart.DrawText($"CandleTPO_{i}_{index}", Letter, Bars.OpenTimes[index], Draw_yRank(prev_segment, Letter), LettersColor);
                    dyntext.VerticalAlignment = VerticalAlignment.Center;
                    dyntext.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else
                    Draw_yRank(prev_segment, Letter);
                    
                prev_segment = Math.Abs(prev_segment - rowHeight);
            }
        }
        // ========= ========== ==========
        private double Draw_yRank(double BarSegment, string Letter)
        {
            double drawValue = 0.0;
            double prev_segmentValue = 0.0;
            string space = SpacingBetween ? " " : "";
            for (int i = 0; i < allSegmentsPrices.Count; i++)
            {
                if (prev_segmentValue != 0 && BarSegment >= prev_segmentValue && BarSegment <= allSegmentsPrices[i])
                {
                    drawValue = prev_segmentValue + drawHeight;
                    double priceKey = allSegmentsPrices[i];

                    if (allTPOsRank.ContainsKey(priceKey))
                    {
                        if (allTPOsRank[priceKey].LastOrDefault().ToString() != Letter)
                            allTPOsRank[priceKey] += $"{space}{Letter}";
                    }
                    else
                        allTPOsRank.Add(priceKey, Letter);

                    break;  
                }
                prev_segmentValue = allSegmentsPrices[i];
            }
            
            return drawValue;
        }
        private void DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.LightBlue);
        }
        private void Second_DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt2", $"{Msg}", VerticalAlignment.Top, HorizontalAlignment.Left, Color.LightBlue);
        }
        private void Third_DrawOnScreen(string Msg)
        {
            Chart.DrawStaticText("txt3", $"{Msg}", VerticalAlignment.Top, HorizontalAlignment.Right, Color.Yellow);
        }
        private void Chart_ZoomChanged(ChartZoomEventArgs obj)
        {
            int Zoom = obj.Chart.ZoomLevel;
            if (Zoom <= 500 && Zoom > 80){
                if (Fonts_allTPOLines.LastOrDefault().FontSize != 25)
                    SetFontSize(20);
            }
            else if (Zoom <= 80 && Zoom > 40) {
                if (Fonts_allTPOLines.LastOrDefault().FontSize != 18)
                    SetFontSize(18);
            }
            else if (Zoom <= 40 && Zoom > 20) {
                if (Fonts_allTPOLines.LastOrDefault().FontSize != 13)
                    SetFontSize(10);
            }
            else if (Zoom <= 20 && Zoom > 15) {
                if (Fonts_allTPOLines.LastOrDefault().FontSize != 11)
                    SetFontSize(8);
            }
            else if (Zoom == 10) {
                if (Fonts_allTPOLines.LastOrDefault().FontSize != 8)
                    SetFontSize(8);
            }
            else if (Zoom == 5) {
                if (Fonts_allTPOLines.LastOrDefault().FontSize != 6)
                    SetFontSize(6);
            }
            void SetFontSize(int fsize)
            {
                updatedFontsize = fsize;
                for (int k=0; k < Fonts_allTPOLines.Count; k++)
                    Fonts_allTPOLines[k].FontSize = fsize;
            }
        }
        // ========= ========== ==========
        private double[] VA_Calculation()
        {
        /*  https://onlinelibrary.wiley.com/doi/pdf/10.1002/9781118659724.app1
            https://www.mypivots.com/dictionary/definition/40/calculating-market-profile-value-area 
            Visually based on riv_ay-TPOChart.v102-6 (MT4) and riv_ay-MarketProfileDWM.v131-2 (MT4) to see if it's right */

            string largestTPO = allTPOsRank.Values.OrderByDescending(x => x.Length).First();
            // string largestTPO = allTPOsRank.Values.MaxBy(x => x.Length); in .NET Framework 6.0
            
            double totaltpo = 0;
            for (int i = 0; i < allTPOsRank.Count; i++)
                totaltpo += allTPOsRank.ElementAt(i).Value.Length;
                
            double _70percent = Math.Round((70 * totaltpo) / 100);
            int largestTPOLengh = largestTPO.Length;
            
            double priceLTPO = 0;
            for (int k = 0; k < allTPOsRank.Count; k++)
            {
                if (allTPOsRank.ElementAt(k).Value == largestTPO)
                {
                    priceLTPO = allTPOsRank.ElementAt(k).Key;
                    break;
                }
            }
            double priceVAH = 0;
            double priceVAL = 0;
            
            double sumVA = largestTPOLengh;
                        
            List<double> upKeys = new List<double>();
            List<double> downKeys = new List<double>();
            for (int i = 0; i < allSegmentsPrices.Count; i++)
            {
                double priceKey = allSegmentsPrices[i];
                
                if (allTPOsRank.ContainsKey(priceKey))
                {
                    if (priceKey < priceLTPO)
                        downKeys.Add(priceKey);
                    else if (priceKey > priceLTPO)
                        upKeys.Add(priceKey);
                }
            }
            upKeys.Sort();
            downKeys.Sort();
            downKeys.Reverse();
            
            double[] withoutVA = {priceLTPO-(rowHeight*2), priceLTPO+drawHeight, priceLTPO};
            if (upKeys.Count == 0 || downKeys.Count == 0)
                return withoutVA;

            double[] prev2UP = {0, 0};
            double[] prev2Down = {0, 0};
            
            bool lockAbove = false;
            double[] aboveKV = {0, 0};
            
            bool lockBelow = false;
            double[] belowKV = {0, 0};

            for (int i = 0; i < allTPOsRank.Keys.Count; i++)
            {            
                if (sumVA >= _70percent)
                    break;

                double sumUp = 0;
                double sumDown = 0;
                
                // ========= Above of POC =========
                double prevUPkey = upKeys.First();
                double keyUP = 0;
                foreach (double key in upKeys)
                {
                    if (upKeys.Count == 1 || prev2UP[0] != 0 && prev2UP[1] != 0 && key == upKeys.Last())
                    {
                        sumDown = allTPOsRank[key].Length;
                        keyUP = key;
                        break;
                    }
                    if (lockAbove)
                    {
                        keyUP = aboveKV[0];
                        sumUp = aboveKV[1];
                        break;
                    }
                    if (prev2UP[0] == 0 && prev2UP[1] == 0 && key != prevUPkey 
                    || prev2UP[0] != 0 && prev2UP[1] != 0 && prevUPkey > aboveKV[0] && key > aboveKV[0])
                    {
                        int upTPO = allTPOsRank[key].Length;
                        int up2TPO = allTPOsRank[prevUPkey].Length;
                        
                        keyUP = key;
                        
                        double[] _2up = {prevUPkey, keyUP};
                        prev2UP = _2up;
                        
                        double[] _above = {keyUP, upTPO + up2TPO};
                        aboveKV = _above;
                        
                        sumUp = upTPO + up2TPO;
                        break;
                    }
                    prevUPkey = key;
                }

                // ========= Below of POC =========
                double prevDownkey = downKeys.First();
                double keyDw = 0;
                foreach (double key in downKeys)
                {
                    if (downKeys.Count == 1 || prev2Down[0] != 0 && prev2Down[1] != 0 && key == downKeys.Last())
                    {
                        sumDown = allTPOsRank[key].Length;
                        keyDw = key;
                        break;
                    }
                    if (lockBelow)
                    {
                        keyDw = belowKV[0];
                        sumDown = belowKV[1];
                        break;
                    }
                    if (prev2Down[0] == 0 && prev2Down[1] == 0 && key != prevDownkey 
                    || prev2Down[0] != 0 && prev2Down[1] != 0 && prevDownkey < aboveKV[0] && key < belowKV[0])
                    {
                        int downTPO = allTPOsRank[key].Length;
                        int down2TPO = allTPOsRank[prevDownkey].Length;
                        
                        keyDw = key;
                        
                        double[] _2down = {prevDownkey, keyDw};
                        prev2Down = _2down;
                        
                        double[] _below = {keyDw, downTPO + down2TPO};
                        belowKV = _below;
                        
                        sumDown = downTPO + down2TPO;
                        break;
                    }
                    prevDownkey = key;
                }

                // ========= VA rating ========= 
                if (sumUp > sumDown)
                {
                    sumVA += sumUp;
                    priceVAH = keyUP;
                    priceVAL = keyDw;
                    
                    lockBelow = true;
                    lockAbove = false;
                }
                else if (sumDown > sumUp)
                {                    
                    sumVA += sumDown;
                    priceVAH = keyUP;
                    priceVAL = keyDw;
                    
                    lockBelow = false;
                    lockAbove = true;
                }
                else if (sumUp == sumDown)
                {
                    double[] _2up = {prevUPkey, keyUP};
                    prev2UP = _2up;
                    double[] _2down = {prevDownkey, keyDw};
                    prev2Down = _2down;
                    
                    sumVA += (sumUp + sumDown);
                    priceVAH = keyUP;
                    priceVAL = keyDw;
                    
                    lockBelow = false;
                    lockAbove = false;
                }
            }
            
            double[] VA = {priceVAL, priceVAH, priceLTPO}; 

            return VA;
        }
    }
}