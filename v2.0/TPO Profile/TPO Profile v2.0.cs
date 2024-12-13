/*
--------------------------------------------------------------------------------------------------------------------------------
                        TPO Profile v2.0
  It is VISUALLY BASED on the best TPO/Market Profile for MT4
(riv-ay-TPOChart.v102-06 and riv-ay-MarketProfileDWM.v131-2)

--Preset Settings:
Optimized for most assets (Currencies/Metals/Indices) focusing on Precision/Performance Balance,
and of course it can't cover everything, but you can Customize if you need to.

--TPO Divided into Colums
Just like in the books.

--Custom TPO Interval/rowHeight
Want more accuracy at the cost of more processing or just a custom TPO?
You can have both.

.NET 6.0+ is Required

What's new in v2.0?
-Added Params Panel for quickly switch between settings (TPO modes, row height, interval, etc) and most importantly, more user-friendly.
-Refactor to only use Colors API + it was a mess.
-Should work with Mac OS users.

AUTHOR: srlcarlg


== DON"T BE an ASSHOLE SELLING this FREE and OPEN-SOURCE indicator ==
----------------------------------------------------------------------------------------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using static cAlgo.TPOProfileV20;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class TPOProfileV20 : Indicator
    {
        public enum PanelAlignData
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
        [Parameter("Panel Position:", DefaultValue = PanelAlignData.Bottom_Right, Group = "==== TPO Profile v2.0 ====")]
        public PanelAlignData PanelAlignInput { get; set; }

        public enum StyleTypeData
        {
            Letters,
            Histogram,
        }
        [Parameter("[Aggr]Style:", DefaultValue = StyleTypeData.Histogram, Group = "==== TPO Profile v2.0 ====")]
        public StyleTypeData StyleTypeInput { get; set; }


        public enum ConfigRowData
        {
            Predefined,
            Custom,
        }
        [Parameter("Config:", DefaultValue = ConfigRowData.Predefined, Group = "==== Config ====")]
        public ConfigRowData ConfigRowInput { get; set; }

        [Parameter("Custom Interval:", DefaultValue = "Daily", Group = "==== Config ====")]
        public TimeFrame CustomInterval { get; set; }

        [Parameter("Custom Row Height:", DefaultValue = 1, MinValue = 0.2, Group = "==== Config ====")]
        public double CustomHeight { get; set; }


        public enum HistWidthData
        {
            _15,
            _30,
            _50,
            _70,
            _100
        }
        [Parameter("Histogram Width(%)", DefaultValue = HistWidthData._50, Group = "==== Histogram Settings ====")]
        public HistWidthData HistWidthInput { get; set; }

        [Parameter("Fill Histogram?", DefaultValue = true, Group = "==== Histogram Settings ====")]
        public bool FillHist { get; set; }


        [Parameter("Show OHLC Bar?", DefaultValue = true, Group = "==== Other settings ====")]
        public bool ShowOHLC { get; set; }

        [Parameter("Extended VAs?", DefaultValue = false, Group = "==== Other settings ====")]
        public bool ExtendVA { get; set; }

        [Parameter("[Aggr]Automatic FontSize?", DefaultValue = true , Group = "==== Other settings ====")]
        public bool AutoFontSize { get; set; }

        [Parameter("[Aggr]Fixed Font Size:", DefaultValue = 12, MinValue = 1, MaxValue = 80, Group = "==== Other settings ====")]
        public int FixedFontSize { get; set; }

        [Parameter("[Aggr]Spacing?:", DefaultValue = false, Group ="==== Other settings ====")]
        public bool SpacingBetween { get; set; }


        [Parameter("Histogram Color:", DefaultValue = "#6087CEEB", Group = "==== Colors Histogram ====")]
        public Color HistColor { get; set; }

        [Parameter("Histogram inside VA:", DefaultValue = "#7F00BFFF", Group = "==== Colors Histogram ====")]
        public Color HistColorVA  { get; set; }

        [Parameter("OHLC Bar Color:", DefaultValue = "Gray", Group = "==== Colors Histogram ====")]
        public Color ColorOHLC { get; set; }


        [Parameter("Color POC:", DefaultValue = "D0FFD700", Group = "==== Point of Control ====")]
        public Color ColorPOC { get; set; }

        [Parameter("LineStyle POC:", DefaultValue = LineStyle.Lines, Group = "==== Point of Control ====")]
        public LineStyle LineStylePOC { get; set; }

        [Parameter("Thickness POC:", DefaultValue = 2, MinValue = 1, MaxValue = 5, Group = "==== Point of Control ====")]
        public int ThicknessPOC { get; set; }


        [Parameter("Color VA:", DefaultValue = "#19F0F8FF",  Group = "==== Value Area ====")]
        public Color ColorVA { get; set; }

        [Parameter("Color VAH:", DefaultValue = "PowderBlue" , Group = "==== Value Area ====")]
        public Color ColorVAH { get; set; }

        [Parameter("Color VAL:", DefaultValue = "PowderBlue", Group = "==== Value Area ====")]
        public Color ColorVAL { get; set; }

        [Parameter("Opacity VA" , DefaultValue = 10, MinValue = 5, MaxValue = 100, Group = "==== Value Area ====")]
        public int OpacityVA { get; set; }

        [Parameter("LineStyle VA:", DefaultValue = LineStyle.LinesDots, Group = "==== Value Area ====")]
        public LineStyle LineStyleVA { get; set; }

        [Parameter("Thickness VA:", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "==== Value Area ====")]
        public int ThicknessVA { get; set; }


        [Parameter("Color Letters:", DefaultValue = "#8BE7E7E7" , Group = "==== Divided Mode ====")]
        public Color ColorLetters { get; set; }

        [Parameter("Close BarUP:", DefaultValue = "Green" , Group = "==== Divided Mode ====")]
        public Color ColorCandleUP { get; set; }

        [Parameter("Close BarDown:", DefaultValue = "Red", Group = "==== Divided Mode ====")]
        public Color ColorCandleDown { get; set; }


        [Parameter("Developed for cTrader/C#", DefaultValue = "by srlcarlg", Group = "==== Credits ====")]
        public string Credits { get; set; }
        [Parameter("Visually based in MT4", DefaultValue = "riv-ay-(TPOChart/MarketProfileDWM)", Group = "==== Credits ====")]
        public string Credits_2 { get; set; }

        private readonly VerticalAlignment V_Align = VerticalAlignment.Top;
        private readonly HorizontalAlignment H_Align = HorizontalAlignment.Center;
        private readonly string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        private List<double> Segments = new();
        private readonly IDictionary<double, string> TPOsRank = new Dictionary<double, string>();
        private readonly List<ChartText> TPOLines = new();
        private readonly List<ChartText> CandlesTPO = new();
        private readonly List<ChartTrendLine> POCsLines = new();
        private readonly List<ChartTrendLine> VALines = new();

        private readonly IDictionary<int, ChartRectangle> RectanglesToColor = new Dictionary<int, ChartRectangle>();
        private readonly List<ChartText> Fonts_TPOLines= new();

        private TimeFrame LookBack_TF;
        private Bars LookBack_Bars;

        private double HeightPips = 4;
        private double rowHeight = 0;
        private double drawHeight = 0;
        private double prevPrice;
        private double[] priceVA_LHP = {0, 0, 0};

        private bool Wrong = false;
        private bool isLive = false;
        private bool configHasChanged = false;

        private int cleanedIndex;
        private int updatedFontsize = 0;
        private int previousLetter_Index = 0;

        // Moved from cTrader Input to Params Panel
        public int Lookback { get; set; } = 5;
        public enum ModeTPOData
        {
            Aggregated,
            Divided,
            Both,
        }
        public ModeTPOData ModeTPOInput { get; set; } = ModeTPOData.Aggregated;
        public bool ShowVA { get; set; } = false;
        public bool KeepPOC { get; set; } = true;
        public bool ExtendPOC { get; set; } = false;

        // Params Panel
        private Border ParamBorder;
        public class IndicatorParams
        {
            public int LookBack { get; set; }
            public ModeTPOData ModeTPO { get; set; }
            public double RowHeight { get; set; }
            public TimeFrame Interval { get; set; }
            public bool KeepPOC { get; set; }
            public bool ExtendedPOC { get; set; }
            public bool ShowVA { get; set; }
        }

        private void AddHiddenButton(Panel panel, Color btnColor)
        {
            Button button = new()
            {
                Text = "TPO",
                Padding = 0,
                Height = 22,
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
            // ========== Predefined Config ==========
            if (ConfigRowInput == ConfigRowData.Predefined && (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Day3))
            {
                if (Chart.TimeFrame >= TimeFrame.Minute && Chart.TimeFrame <= TimeFrame.Minute4)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute)
                        LookBack_TF = TimeFrame.Hour;
                    else if (Chart.TimeFrame == TimeFrame.Minute2)
                        LookBack_TF = TimeFrame.Hour2;
                    else if (Chart.TimeFrame <= TimeFrame.Minute4)
                        LookBack_TF = TimeFrame.Hour3;

                    SetHeightPips(0.5, 8);
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute5 && Chart.TimeFrame <= TimeFrame.Minute10)
                {
                    if (Chart.TimeFrame == TimeFrame.Minute5)
                        LookBack_TF = TimeFrame.Hour4;
                    else if (Chart.TimeFrame == TimeFrame.Minute6)
                        LookBack_TF = TimeFrame.Hour6;
                    else if (Chart.TimeFrame <= TimeFrame.Minute8)
                        LookBack_TF = TimeFrame.Hour8;
                    else if (Chart.TimeFrame <= TimeFrame.Minute10)
                        LookBack_TF = TimeFrame.Hour12;

                    SetHeightPips(1, 25);
                }
                else if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour8)
                {
                    if (Chart.TimeFrame >= TimeFrame.Minute15 && Chart.TimeFrame <= TimeFrame.Hour) {
                        LookBack_TF = TimeFrame.Daily;
                        SetHeightPips(2, 50);
                    }
                    else if (Chart.TimeFrame <= TimeFrame.Hour8) {
                        LookBack_TF = TimeFrame.Weekly;
                        SetHeightPips(4, 140);
                    }
                }
                else if (Chart.TimeFrame >= TimeFrame.Hour12 && Chart.TimeFrame <= TimeFrame.Day3)
                {
                    LookBack_TF = TimeFrame.Monthly;
                    SetHeightPips(6, 220);
                }
            }
            else
            {
                if (ConfigRowInput == ConfigRowData.Predefined)
                {
                    string Msg = "'Predefined Config' is designed only for Standard Timeframe (Minutes, Hours, Days) \n Weekly and Monthly is not currently supported \n\n use 'Custom Config' to others Chart Timeframes (Renko/Range/Ticks).";
                    Chart.DrawStaticText("txt", $"{Msg}", V_Align, H_Align, Color.Orange);
                    Wrong = true;
                    return;
                }
                string[] timeBased = {"Minute", "Hour", "Daily", "Day", "Weekly", "Monthly"};
                if (!timeBased.Any(CustomInterval.Name.ToString().Contains))
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

                LookBack_TF = CustomInterval;
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

            LookBack_Bars = MarketData.GetBars(LookBack_TF);
            if (LookBack_Bars.ClosePrices.Count < Lookback)
            {
                while (LookBack_Bars.ClosePrices.Count < Lookback)
                {
                    int loadedCount = LookBack_Bars.LoadMoreHistory();
                    Print($"Loaded {loadedCount}, {LookBack_TF.ShortName} Bars, Current Bar Date: {LookBack_Bars.OpenTimes.Reverse().LastOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
            }

            // Ex: 4 pips to TPO calculation(rowHeight) = 2 pips between letters (drawHeight)
            rowHeight = Symbol.PipSize * HeightPips;
            drawHeight = Symbol.PipSize * (HeightPips/2);

            if (AutoFontSize && StyleTypeInput != StyleTypeData.Histogram)
                Chart.ZoomChanged += Chart_ZoomChanged;

            DrawOnScreen("Calculating...");
            Second_DrawOnScreen("Taking too long? You can: \n 1) Increase the rowHeight \n 2) Disable the Value Area (High Performance) \n");
            if (Application.UserTimeOffset.ToString() != "03:00:00")
                Third_DrawOnScreen("Set your UTC to UTC+3");

            // PARAMS PANEL
            VerticalAlignment vAlign = VerticalAlignment.Bottom;
            HorizontalAlignment hAlign = HorizontalAlignment.Right;

            if (PanelAlignInput == PanelAlignData.Bottom_Left)
                hAlign = HorizontalAlignment.Left;
            else if (PanelAlignInput == PanelAlignData.Top_Left)
                vAlign = VerticalAlignment.Top;
            else if (PanelAlignInput == PanelAlignData.Top_Right) {
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Right;
            } else if (PanelAlignInput == PanelAlignData.Center_Right) {
                vAlign = VerticalAlignment.Center;
                hAlign = HorizontalAlignment.Right;
            } else if (PanelAlignInput == PanelAlignData.Center_Left) {
                vAlign = VerticalAlignment.Center;
                hAlign = HorizontalAlignment.Left;
            } else if (PanelAlignInput == PanelAlignData.Top_Center) {
                vAlign = VerticalAlignment.Top;
                hAlign = HorizontalAlignment.Center;
            } else if (PanelAlignInput == PanelAlignData.Bottom_Center) {
                vAlign = VerticalAlignment.Bottom;
                hAlign = HorizontalAlignment.Center;
            }

            IndicatorParams DefaultParams = new()
            {
                LookBack = Lookback,
                ModeTPO = ModeTPOInput,
                RowHeight = rowHeight,
                Interval = LookBack_TF,
                KeepPOC = KeepPOC,
                ExtendedPOC = ExtendPOC,
                ShowVA = ShowVA
            };

            ParamsPanel ParamPanel = new(this, DefaultParams);
            Border borderParam = new()
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                Width = 225,
                Child = ParamPanel
            };
            Chart.AddControl(borderParam);
            ParamBorder = borderParam;

            var wrapPanel = new WrapPanel
            {
                VerticalAlignment = vAlign,
                HorizontalAlignment = hAlign,
            };
            AddHiddenButton(wrapPanel, Color.Gray);
            Chart.AddControl(wrapPanel);
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

            Bars TF_Bars = LookBack_Bars;
            // Get Index of TPO Interval to continue only in Lookback
            int iVerify = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            if (TF_Bars.ClosePrices.Count - iVerify > Lookback)
                return;

            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(TF_Bars.OpenTimes[TF_idx]);

            // ====== Extended POC/VA ========
            if (ExtendPOC && POCsLines.Count != 0 && POCsLines.FirstOrDefault().Y2 != priceVA_LHP[2])
            {
                for (int tl=0; tl < POCsLines.Count; tl++)
                {
                    POCsLines[tl].Time2 = Bars.OpenTimes[index];
                    string dynDate = LookBack_TF == TimeFrame.Daily ? POCsLines[tl].Time1.Date.AddDays(1).ToString().Replace("00:00:00", "") : POCsLines[tl].Time1.Date.ToString();
                    Chart.DrawText($"POC{POCsLines[tl].Time1}", $"{dynDate}", Bars.OpenTimes[index], POCsLines[tl].Y2+drawHeight, ColorPOC);
                }
            }

            if (ExtendVA && VALines.Count != 0 && VALines.FirstOrDefault().Time2 != Bars.OpenTimes[index])
            {
                for (int tl=0; tl < VALines.Count; tl++)
                    VALines[tl].Time2 = Bars.OpenTimes[index];
            }

            // === Clean Dicts/others ===
            if (index == indexStart && index != cleanedIndex || (index-1) == indexStart && (index-1) != cleanedIndex)
            {
                Segments.Clear();
                TPOsRank.Clear();
                TPOLines.Clear();
                CandlesTPO.Clear();
                RectanglesToColor.Clear();
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
                if (Bars.ClosePrices[index] >= (prevPrice+drawHeight) ||  Bars.ClosePrices[index] <= (prevPrice-drawHeight) || configHasChanged)
                {
                    for (int i=indexStart; i <= index; i++)
                    {
                        if (i == indexStart) {
                            TPOsRank.Clear();
                            previousLetter_Index = 0;
                        }

                        TPO(i, indexStart, true);
                    }
                    prevPrice = Bars.ClosePrices[index];
                    configHasChanged = false;
                }
            }
        }

        private void TPO(int index, int iStart, bool resizeHL)
        {
            // ======= Highest and Lowest =======
            Bars TF_Bars = LookBack_Bars;
            int TF_idx = TF_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

            double highest = TF_Bars.HighPrices[TF_idx], lowest = TF_Bars.LowPrices[TF_idx], open = TF_Bars.OpenPrices[TF_idx];

            // ======= Chart Segmentation =======
            List<double> currentSegments = new();
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
            Segments = currentSegments.OrderBy(x => x).ToList();

            // ======= CandlesTPO Columns + Letters =======
            if (previousLetter_Index >= Letters.Length)
                previousLetter_Index = 0;

            if (resizeHL)
            {
                string Letter = Letters[previousLetter_Index].ToString();
                CandleTPO($"{Letter}", index);
                previousLetter_Index += 1;
            }
            else
            {
                string Letter = Letters[previousLetter_Index].ToString();
                if (!IsLastBar)
                    previousLetter_Index = previousLetter_Index == 0 ? 1 : previousLetter_Index + 1;
                else
                    Letter = Letters[previousLetter_Index].ToString();

                if (!isLive)
                    CandleTPO($"{Letter}", index);
            }

            // ======= Drawing TPO =======
            if (Segments.Count == 0)
                return;

            for (int i = 0; i < Segments.Count; i++)
            {
                double priceKey = Segments[i];
                if (!TPOsRank.ContainsKey(priceKey))
                    continue;

                if (ModeTPOInput != ModeTPOData.Divided)
                {
                    if (StyleTypeInput != StyleTypeData.Histogram)
                    {
                        ChartText lineTPO = Chart.DrawText($"TPO{i}_{iStart}", TPOsRank[priceKey], iStart, Segments[i], HistColor);
                        if (!AutoFontSize)
                            lineTPO.FontSize = FixedFontSize;
                        else {
                            if (updatedFontsize != 0)
                                lineTPO.FontSize = updatedFontsize;
                        }

                        TPOLines.Add(lineTPO);
                        Fonts_TPOLines.Add(lineTPO);
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
                        //string largestTPO = TPOsRank.Values.OrderByDescending(x => x.Length).First(); in .NET Framework 4.x
                        string largestTPO = TPOsRank.Values.MaxBy(x => x.Length);

                        double lowerSegment = Segments[i]-rowHeight;
                        double upperSegment = Segments[i];

                        double maxLength = Bars[index].OpenTime.Subtract(Bars[iStart].OpenTime).TotalMilliseconds;
                        var selected = HistWidthInput;
                        double maxWidth = selected == HistWidthData._15 ? 1.25 : selected == HistWidthData._30 ? 1.50 : selected == HistWidthData._50 ? 2 : 4;
                        double proportion = TPOsRank[priceKey].Length * (maxLength-(maxLength/maxWidth));
                        if (selected == HistWidthData._100)
                            proportion = TPOsRank[priceKey].Length * maxLength;
                        double dynLength = proportion / largestTPO.Length;

                        ChartRectangle volHist;
                        volHist = Chart.DrawRectangle($"{iStart}_{i}_", Bars.OpenTimes[iStart], lowerSegment, Bars.OpenTimes[iStart].AddMilliseconds(dynLength), upperSegment, HistColor);

                        if (RectanglesToColor.ContainsKey(i))
                            RectanglesToColor[i] = volHist;
                        else
                            RectanglesToColor.Add(i, volHist);

                        if (FillHist)
                            volHist.IsFilled = true;
                    }
                }
                // ============= Coloring Letters + VAL / VAH / POC =============
                if (ShowVA)
                {
                    double[] VAL_VAH_POC = VA_Calculation();

                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[2]-rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[2]-rowHeight, ColorPOC);
                    ChartTrendLine vah;
                    if (StyleTypeInput == StyleTypeData.Histogram)
                        vah = Chart.DrawTrendLine($"VAH_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[1]+rowHeight, Bars.OpenTimes[index], VAL_VAH_POC[1]+rowHeight, ColorVAH);
                    else
                        vah = Chart.DrawTrendLine($"VAH_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[1]-Symbol.PipSize, Bars.OpenTimes[index], VAL_VAH_POC[1]-Symbol.PipSize, ColorVAH);

                    ChartTrendLine val = Chart.DrawTrendLine($"VAL_{iStart}", TF_Bars.OpenTimes[TF_idx], VAL_VAH_POC[0], Bars.OpenTimes[index], VAL_VAH_POC[0], ColorVAL);

                    double[] VAforColor = {VAL_VAH_POC[0], VAL_VAH_POC[1], VAL_VAH_POC[2]};
                    priceVA_LHP = VAforColor;

                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";
                    vah.LineStyle = LineStyleVA; vah.Thickness = ThicknessVA; vah.Comment = "VAH";
                    val.LineStyle = LineStyleVA; val.Thickness = ThicknessVA; val.Comment = "VAL";

                    // ==== POC Lines ====
                    if (POCsLines.Contains(poc))
                    {
                        for (int tl=0; tl < TPOLines.Count; tl++)
                        {
                            if (POCsLines[tl].Time1 == poc.Time1) {
                                POCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        POCsLines.Add(poc);

                    // ==== VAH / VAL Lines ====
                    if (VALines.Contains(vah) || VALines.Contains(val))
                    {
                        for (int tl=0; tl < VALines.Count; tl++)
                        {
                            if (VALines[tl].Comment == "VAH" && VALines[tl].Time1 == vah.Time1)
                                VALines[tl] = vah;
                            else if (VALines[tl].Comment == "VAL" && VALines[tl].Time1 == val.Time1)
                                VALines[tl] = val;
                        }
                    }
                    else
                    {
                        if (!VALines.Contains(vah))
                            VALines.Add(vah);
                        if (!VALines.Contains(val))
                            VALines.Add(val);
                    }

                    if (StyleTypeInput == StyleTypeData.Histogram)
                    {
                        // =========== Coloring Retangles ============
                        foreach (int key in RectanglesToColor.Keys)
                        {
                            if (RectanglesToColor[key].Y1 > priceVA_LHP[0] && RectanglesToColor[key].Y1 < priceVA_LHP[1])
                                RectanglesToColor[key].Color = HistColorVA;

                            if (RectanglesToColor[key].Y1 == priceVA_LHP[2]-rowHeight)
                                RectanglesToColor[key].Color = ColorPOC;

                            if (RectanglesToColor[key].Y1 == priceVA_LHP[1])
                                RectanglesToColor[key].Color = ColorVAH;
                            else if (RectanglesToColor[key].Y1 == priceVA_LHP[0])
                                RectanglesToColor[key].Color = ColorVAL;
                        }
                    }
                    else
                    {
                        // =========== Coloring Letters ============
                        for (int k=0; k < TPOLines.Count; k++)
                        {
                            if (TPOLines[k].Y > priceVA_LHP[0] && TPOLines[k].Y < priceVA_LHP[1]+drawHeight)
                                TPOLines[k].Color = HistColorVA;

                            if (TPOLines[k].Y == priceVA_LHP[2])
                                TPOLines[k].Color = ColorPOC;

                            if (TPOLines[k].Y == priceVA_LHP[1])
                                TPOLines[k].Color = ColorVAH;
                            else if (TPOLines[k].Y == priceVA_LHP[0]+rowHeight)
                                TPOLines[k].Color = ColorVAL;
                        }
                    }
                }
                else if (!ShowVA && KeepPOC)
                {
                    // string largestTPO = allTPOsRank.Values.OrderByDescending(x => x.Length).First(); in .NET Framework 4.x
                    string largestTPO = TPOsRank.Values.MaxBy(x => x.Length);
                    double priceLTPO = 0;
                    for (int k = 0; k < TPOsRank.Count; k++)
                    {
                        if (TPOsRank.ElementAt(k).Value == largestTPO) {
                            priceLTPO = TPOsRank.ElementAt(k).Key;
                            break;
                        }
                    }
                    ChartTrendLine poc = Chart.DrawTrendLine($"POC_{iStart}", TF_Bars.OpenTimes[TF_idx], priceLTPO-rowHeight, Bars.OpenTimes[index], priceLTPO-rowHeight, ColorPOC);
                    poc.LineStyle = LineStylePOC; poc.Thickness = ThicknessPOC; poc.Comment = "POC";

                    // ==== POC Lines ====
                    if (POCsLines.Contains(poc))
                    {
                        for (int tl=0; tl < TPOLines.Count; tl++)
                        {
                            if (POCsLines[tl].Time1 == poc.Time1) {
                                POCsLines[tl] = poc;
                                break;
                            }
                        }
                    }
                    else
                        POCsLines.Add(poc);

                    if (StyleTypeInput == StyleTypeData.Histogram)
                    {
                        // =========== Coloring Retangles ============
                        foreach (int key in RectanglesToColor.Keys)
                        {
                            if (RectanglesToColor[key].Y1 == priceLTPO-rowHeight)
                                RectanglesToColor[key].Color = ColorPOC;
                        }
                    }
                    else
                    {
                        // =========== Coloring Letters ============
                        for (int k=0; k < TPOLines.Count; k++)
                        {
                            if (TPOLines[k].Y == priceLTPO) {
                                TPOLines[k].Color = ColorPOC;
                                break;
                            }
                        }
                    }
                }
            }

            // ====== Rectangle VA ======
            if (ShowVA && priceVA_LHP[0] != 0)
            {
                ChartRectangle rectVA;
                if (StyleTypeInput == StyleTypeData.Histogram)
                    rectVA = Chart.DrawRectangle($"{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], priceVA_LHP[0], Bars.OpenTimes[index], priceVA_LHP[1]+rowHeight, ColorVA);
                else
                    rectVA = Chart.DrawRectangle($"{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], priceVA_LHP[0], Bars.OpenTimes[index], priceVA_LHP[1]-Symbol.PipSize, ColorVA);

                rectVA.IsFilled = true;
            }

            if (ModeTPOInput == ModeTPOData.Divided || ModeTPOInput == ModeTPOData.Both)
            {
                Color isBullish = Bars.ClosePrices[index] > Bars.OpenPrices[index] ? ColorCandleUP : ColorCandleDown;
                ChartText iconBarClose =  Chart.DrawText($"Close_{Bars.OpenTimes[index]}", "▶", Bars.OpenTimes[index], Bars.ClosePrices[index], isBullish);
                iconBarClose.VerticalAlignment = VerticalAlignment.Center;
                iconBarClose.HorizontalAlignment = HorizontalAlignment.Left;
                iconBarClose.FontSize = 8;
            }

            if (!ShowOHLC)
                return;
            if (ModeTPOInput == ModeTPOData.Divided)
                return;
            ChartText iconOpenSession =  Chart.DrawText($"Start{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], TF_Bars.OpenPrices[TF_idx], ColorOHLC);
            ChartText iconCloseSession =  Chart.DrawText($"End{TF_Bars.OpenTimes[TF_idx]}", "▂", TF_Bars.OpenTimes[TF_idx], Bars.ClosePrices[index], ColorOHLC);
            iconOpenSession.VerticalAlignment = VerticalAlignment.Center;
            iconOpenSession.HorizontalAlignment = HorizontalAlignment.Left;
            iconOpenSession.FontSize = 14;
            iconCloseSession.VerticalAlignment = VerticalAlignment.Center;
            iconCloseSession.HorizontalAlignment = HorizontalAlignment.Right;
            iconCloseSession.FontSize = 14;

            ChartTrendLine Session = Chart.DrawTrendLine($"Session{TF_Bars.OpenTimes[TF_idx]}", TF_Bars.OpenTimes[TF_idx], lowest, TF_Bars.OpenTimes[TF_idx], highest, ColorOHLC);
            Session.Thickness = 3;
        }


        // ====== Functions Area ======
        private void CandleTPO(string Letter, int index)
        {
            double High = Bars.HighPrices[index];
            double Low = Bars.LowPrices[index];

            int totalLetters = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                if (Segments[i] < High && Segments[i] > Low)
                    totalLetters += 1;
            }

            double prev_segment = High;
            for (int i = 0; i <= totalLetters; i++)
            {
                if (ModeTPOInput == ModeTPOData.Divided || ModeTPOInput == ModeTPOData.Both)
                {
                    ChartText dyntext = Chart.DrawText($"CandleTPO_{i}_{index}", Letter, Bars.OpenTimes[index], Draw_Yaxis_Rank(prev_segment, Letter), ColorLetters);
                    dyntext.VerticalAlignment = VerticalAlignment.Center;
                    dyntext.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else
                    Draw_Yaxis_Rank(prev_segment, Letter);

                prev_segment = Math.Abs(prev_segment - rowHeight);
            }
        }
        // ========= ========== ==========
        private double Draw_Yaxis_Rank(double BarSegment, string Letter)
        {
            double drawValue = 0.0;
            double prev_segmentValue = 0.0;
            string space = SpacingBetween ? " " : "";
            for (int i = 0; i < Segments.Count; i++)
            {
                if (prev_segmentValue != 0 && BarSegment >= prev_segmentValue && BarSegment <= Segments[i])
                {
                    drawValue = prev_segmentValue + drawHeight;
                    double priceKey = Segments[i];

                    if (TPOsRank.ContainsKey(priceKey))
                    {
                        if (TPOsRank[priceKey].LastOrDefault().ToString() != Letter)
                            TPOsRank[priceKey] += $"{space}{Letter}";
                    }
                    else
                        TPOsRank.Add(priceKey, Letter);

                    break;
                }
                prev_segmentValue = Segments[i];
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
                if (Fonts_TPOLines.LastOrDefault().FontSize != 25)
                    SetFontSize(20);
            }
            else if (Zoom <= 80 && Zoom > 40) {
                if (Fonts_TPOLines.LastOrDefault().FontSize != 18)
                    SetFontSize(18);
            }
            else if (Zoom <= 40 && Zoom > 20) {
                if (Fonts_TPOLines.LastOrDefault().FontSize != 13)
                    SetFontSize(10);
            }
            else if (Zoom <= 20 && Zoom > 15) {
                if (Fonts_TPOLines.LastOrDefault().FontSize != 11)
                    SetFontSize(8);
            }
            else if (Zoom == 10) {
                if (Fonts_TPOLines.LastOrDefault().FontSize != 8)
                    SetFontSize(8);
            }
            else if (Zoom == 5) {
                if (Fonts_TPOLines.LastOrDefault().FontSize != 6)
                    SetFontSize(6);
            }
            void SetFontSize(int fsize)
            {
                updatedFontsize = fsize;
                for (int k=0; k < Fonts_TPOLines.Count; k++)
                    Fonts_TPOLines[k].FontSize = fsize;
            }
        }
        // ========= ========== ==========
        private double[] VA_Calculation()
        {
        /*  https://onlinelibrary.wiley.com/doi/pdf/10.1002/9781118659724.app1
            https://www.mypivots.com/dictionary/definition/40/calculating-market-profile-value-area
            Visually based on riv_ay-TPOChart.v102-6 (MT4) and riv_ay-MarketProfileDWM.v131-2 (MT4) to see if it's right */

            string largestTPO = TPOsRank.Values.OrderByDescending(x => x.Length).First();
            // string largestTPO = allTPOsRank.Values.MaxBy(x => x.Length); in .NET Framework 6.0

            double totaltpo = 0;
            for (int i = 0; i < TPOsRank.Count; i++)
                totaltpo += TPOsRank.ElementAt(i).Value.Length;

            double _70percent = Math.Round((70 * totaltpo) / 100);
            int largestTPOLengh = largestTPO.Length;

            double priceLTPO = 0;
            for (int k = 0; k < TPOsRank.Count; k++)
            {
                if (TPOsRank.ElementAt(k).Value == largestTPO)
                {
                    priceLTPO = TPOsRank.ElementAt(k).Key;
                    break;
                }
            }
            double priceVAH = 0;
            double priceVAL = 0;

            double sumVA = largestTPOLengh;

            List<double> upKeys = new();
            List<double> downKeys = new();
            for (int i = 0; i < Segments.Count; i++)
            {
                double priceKey = Segments[i];

                if (TPOsRank.ContainsKey(priceKey))
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

            for (int i = 0; i < TPOsRank.Keys.Count; i++)
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
                        sumDown = TPOsRank[key].Length;
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
                        int upTPO = TPOsRank[key].Length;
                        int up2TPO = TPOsRank[prevUPkey].Length;

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
                        sumDown = TPOsRank[key].Length;
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
                        int downTPO = TPOsRank[key].Length;
                        int down2TPO = TPOsRank[prevDownkey].Length;

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
        public void ClearAndRecalculate()
        {
            Chart.RemoveAllObjects();

            int FirstIndex = Bars.OpenTimes.GetIndexByTime(LookBack_Bars.OpenTimes.FirstOrDefault());

            // Get Index of VOL Interval to continue only in Lookback
            int iVerify = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            while (LookBack_Bars.ClosePrices.Count - iVerify > Lookback) {
                FirstIndex++;
                iVerify = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            }

            int TF_idx = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[FirstIndex]);
            int indexStart = Bars.OpenTimes.GetIndexByTime(LookBack_Bars.OpenTimes[TF_idx]);

            // Historical data
            for (int index = indexStart; index < Bars.Count; index++)
            {
                TF_idx = LookBack_Bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
                indexStart = Bars.OpenTimes.GetIndexByTime(LookBack_Bars.OpenTimes[TF_idx]);

                if (index == indexStart && index != cleanedIndex || (index - 1) == indexStart && (index - 1) != cleanedIndex)
                {
                    Segments.Clear();
                    TPOsRank.Clear();
                    TPOLines.Clear();
                    CandlesTPO.Clear();
                    RectanglesToColor.Clear();
                    double[] VAforColor = {0, 0, 0};
                    priceVA_LHP = VAforColor;
                    previousLetter_Index = 0;
                    cleanedIndex = index == indexStart ? index : (index-1);
                }
                TPO(index, indexStart, true);
            }

            configHasChanged = true;
            if (ExtendPOC) {
                ExtendPOCNow();
            }
        }

        public void SetRowHeight(double number)
        {
            rowHeight = number;
            drawHeight = number/2;

        }
        public void SetLookback(int number)
        {
            Lookback = number;
        }
        public void SetInterval(TimeFrame newTF) {
            LookBack_TF = newTF;
            LookBack_Bars = MarketData.GetBars(LookBack_TF);
            if (LookBack_Bars.ClosePrices.Count < Lookback)
            {
                while (LookBack_Bars.ClosePrices.Count < Lookback)
                {
                    int loadedCount = LookBack_Bars.LoadMoreHistory();
                    Print($"Loaded {loadedCount}, {LookBack_TF.ShortName} LookBack Bars, Current Bar Date: {LookBack_Bars.OpenTimes.FirstOrDefault()}");
                    if (loadedCount == 0)
                        break;
                }
            }
        }
        public void ExtendPOCNow() {
            if (ExtendPOC && POCsLines.Count != 0)
            {
                for (int tl = 0; tl < POCsLines.Count; tl++)
                {
                    POCsLines[tl].Time2 = Bars.LastBar.OpenTime;
                    string dynDate = LookBack_TF == TimeFrame.Daily ? POCsLines[tl].Time1.Date.AddDays(1).ToString().Replace("00:00:00", "") : POCsLines[tl].Time1.Date.ToString();
                    Chart.DrawText($"POC{POCsLines[tl].Time1}", $"{dynDate}", Bars.LastBar.OpenTime, POCsLines[tl].Y2 + drawHeight, ColorPOC);
                }
            }
        }
        public int GetLookback()
        {
            return Lookback;
        }
        public double GetRowHeight()
        {
            return rowHeight;
        }
        public TimeFrame GetInterval() {
            return LookBack_TF;
        }
    }
    public class ParamsPanel : CustomControl
    {
        // Any
        private const string LookBack_InputKey = "LookBackKey";
        private const string RowHeight_InputKey = "RowHeightKey";
        private const string Interval_InputKey = "IntervalKey";
        private const string KeepPOC_InputKey = "KeepPOCKey";
        private const string ExtendedPOC_InputKey = "ExtendedPOCKey";
        private const string ShowVA_InputKey = "ShowVAKey";

        private readonly IDictionary<string, TextBox> textInputMap = new Dictionary<string, TextBox>();
        private readonly IDictionary<string, CheckBox> checkBoxMap = new Dictionary<string, CheckBox>();
        private readonly IDictionary<string, ComboBox> comboBoxMap = new Dictionary<string, ComboBox>();
        private readonly TPOProfileV20 Outside;

        private Button ModeBtn;
        private readonly Color BtnColor;
        private readonly IndicatorParams FirstParams;

        public ParamsPanel(TPOProfileV20 indicator, IndicatorParams defaultParams)
        {
            BtnColor = Color.FromHex("#7F808080");
            Outside = indicator;
            FirstParams = defaultParams;
            AddChild(CreateTradingPanel());
        }

        private ControlBase CreateTradingPanel()
        {
            StackPanel mainPanel = new();

            ControlBase header = CreateHeader();
            mainPanel.AddChild(header);

            StackPanel contentPanel = CreateContentPanel();
            mainPanel.AddChild(contentPanel);

            return mainPanel;
        }

        private static ControlBase CreateHeader()
        {
            Border headerBorder = new()
            {
                BorderThickness = "0 0 0 1",
                Style = Styles.CreateCommonBorderStyle(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 280
            };
            Grid grid = new(0, 0);

            TextBlock header = new()
            {
                Text = "TPO Profile",
                Margin = "10 7",
                Style = Styles.CreateHeaderStyle(),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            grid.AddChild(header, 0, 0);

            headerBorder.Child = grid;
            return headerBorder;
        }

        private StackPanel CreateContentPanel()
        {
            StackPanel contentPanel = new()
            {
                Margin = 10
            };
            Grid grid = new(3, 5);
            grid.Columns[1].SetWidthInPixels(5);
            grid.Columns[3].SetWidthInPixels(5);

            Button button_prev = CreatePassButton("<");
            grid.AddChild(button_prev, 0, 0);

            Button VolumeModeButton = CreateModeInfo_Button(FirstParams.ModeTPO.ToString());
            grid.AddChild(VolumeModeButton, 0, 1, 1, 3);

            Button button_next = CreatePassButton(">");
            grid.AddChild(button_next, 0, 4);

            var Lookback_Input = CreateInputWithLabel("Lookback", FirstParams.LookBack.ToString(), LookBack_InputKey);
            grid.AddChild(Lookback_Input, 1, 0);
            var RowHeightInput = CreateInputWithLabel("Row Height", FirstParams.RowHeight.ToString("0.############################"), RowHeight_InputKey);
            grid.AddChild(RowHeightInput, 1, 2);
            var IntervalInput = CreateComboBoxWithLabel("Interval", Interval_InputKey);
            grid.AddChild(IntervalInput, 1, 4);

            var POCCheckbox = CreateCheckboxWithLabel("POC", FirstParams.KeepPOC, KeepPOC_InputKey);
            grid.AddChild(POCCheckbox, 2, 0);
            var ExtendCheckbox = CreateCheckboxWithLabel("Extend", FirstParams.ExtendedPOC, ExtendedPOC_InputKey);
            grid.AddChild(ExtendCheckbox, 2, 2);
            var WithVACheckbox = CreateCheckboxWithLabel("VA", FirstParams.ShowVA, ShowVA_InputKey);
            grid.AddChild(WithVACheckbox, 2, 4);

            contentPanel.AddChild(grid);
            return contentPanel;
        }

        private Button CreatePassButton(string label)
        {
            Button button = new()
            {
                Text = label,
                Padding = 0,
                Width = 30,
                Height = 20,
                Margin = 0,
                BackgroundColor = BtnColor,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            if (label == ">")
            {
                button.Click += NextModeEvent;
            }
            else
            {
                button.Click += PrevModeEvent;
            }
            return button;
        }

        private Button CreateModeInfo_Button(string label)
        {
            Button button = new()
            {
                Text = label,
                Padding = 0,
                Width = 70,
                Height = 30,
                Margin = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            button.Click += ResetParamsEvent;
            ModeBtn = button;

            return button;
        }

        private Panel CreateInputWithLabel(string label, string defaultValue, string inputKey)
        {
            StackPanel stackPanel = new()
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0"
            };

            TextBlock textBlock = new()
            {
                Text = label,
                TextAlignment = TextAlignment.Center
            };

            TextBox input = new()
            {
                Margin = "0 5 0 0",
                Text = defaultValue,
                Style = Styles.CreateInputStyle(),
                TextAlignment = TextAlignment.Center
            };

            input.TextChanged += TextChangedEvent;
            textInputMap.Add(inputKey, input);

            stackPanel.AddChild(textBlock);
            stackPanel.AddChild(input);

            return stackPanel;
        }
        private Panel CreateComboBoxWithLabel(string label, string inputKey)
        {
            StackPanel stackPanel = new()
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0"
            };

            TextBlock textBlock = new()
            {
                Text = label,
                TextAlignment = TextAlignment.Center
            };

            ComboBox comboBox = new()
            {
                Margin = "0 5 0 0",
                Style = Styles.CreateInputStyle(),
            };

            string[] allTF = {
                // Candles
                "h1", "h2", "h3", "h4", "h6", "h8", "h12", "D1", "D2", "D3", "W1", "Month1",
            };
            for (int i = 0; i < allTF.Length; i++)
            {
                comboBox.AddItem(allTF[i]);
            }
            comboBox.SelectedItem = FirstParams.Interval.ShortName;
            comboBox.SelectedItemChanged += ComboBoxSelectedEvent;
            comboBoxMap.Add(inputKey, comboBox);

            stackPanel.AddChild(textBlock);
            stackPanel.AddChild(comboBox);

            return stackPanel;
        }

        private ControlBase CreateCheckboxWithLabel(string label, bool defaultValue, string inputKey)
        {
            Border checkBoxBorder = new()
            {
                Margin = "0 10 0 0",
                BorderThickness = "0 1 0 1",
                Style = Styles.CreateCommonBorderStyle()
            };

            StackPanel stackPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Margin = "0 10 0 10"
            };

            CheckBox input = new()
            {
                Margin = "0 0 5 0",
                IsChecked = defaultValue
            };

            TextBlock textBlock = new()
            {
                Text = label,
            };

            input.Click += CheckBoxClickEvent;
            checkBoxMap.Add(inputKey, input);

            stackPanel.AddChild(input);
            stackPanel.AddChild(textBlock);

            checkBoxBorder.Child = stackPanel;
            return checkBoxBorder;
        }

        private void RecalculateOutsideWithMsg() {
            string currentMode = ModeBtn.Text;
            ModeBtn.Text = $"{currentMode}\nCalculating...";
            Outside.BeginInvokeOnMainThread(() => {
                Outside.ClearAndRecalculate();
                ModeBtn.Text = currentMode;
            });
        }

        private void NextModeEvent(ButtonClickEventArgs obj)
        {
            if (Outside.ModeTPOInput == ModeTPOData.Aggregated)
            {
                Outside.ModeTPOInput = ModeTPOData.Both;
                ModeBtn.Text = "Both";
            }
            else if (Outside.ModeTPOInput == ModeTPOData.Both)
            {
                Outside.ModeTPOInput = ModeTPOData.Divided;
                ModeBtn.Text = "Divided";
            }

            RecalculateOutsideWithMsg();
        }
        private void PrevModeEvent(ButtonClickEventArgs obj)
        {
            if (Outside.ModeTPOInput == ModeTPOData.Divided)
            {
                Outside.ModeTPOInput = ModeTPOData.Both;
                ModeBtn.Text = "Both";
            }
            else if (Outside.ModeTPOInput == ModeTPOData.Both)
            {
                Outside.ModeTPOInput = ModeTPOData.Aggregated;
                ModeBtn.Text = "Aggregated";
            }

            RecalculateOutsideWithMsg();
        }
        private void ChangeParams(IndicatorParams indicatorParams)
        {
            foreach (var key in checkBoxMap.Keys)
            {
                switch (key)
                {
                    case "KeepPOCKey": checkBoxMap[key].IsChecked = indicatorParams.KeepPOC; break;
                    case "ExtendedPOCKey": checkBoxMap[key].IsChecked = indicatorParams.ExtendedPOC; break;
                    case "ShowVAKey": checkBoxMap[key].IsChecked = indicatorParams.ShowVA; break;
                }
            }
            foreach (var key in textInputMap.Keys)
            {
                switch (key)
                {
                    case "LookBackKey": textInputMap[key].Text = indicatorParams.LookBack.ToString(); break;
                    case "RowHeightKey": textInputMap[key].Text = indicatorParams.RowHeight.ToString("0.############################"); break;
                }
            }
            foreach (var key in comboBoxMap.Keys)
            {
                switch (key)
                {
                    case "IntervalKey": comboBoxMap[key].SelectedItem = indicatorParams.Interval.ToString(); break;
                }
            }
        }
        private void ResetParamsEvent(ButtonClickEventArgs obj)
        {
            ChangeParams(FirstParams);
        }
        private void TextChangedEvent(TextChangedEventArgs obj)
        {
            int lookBack = GetValueFromInput(LookBack_InputKey, -1);
            double rowHeight = GetDoubleFromInput(RowHeight_InputKey, -1);

            if (rowHeight != -1 && rowHeight > 0 && rowHeight != Outside.GetRowHeight()) {
                Outside.SetRowHeight(rowHeight);
                RecalculateOutsideWithMsg();
            }
            if ((lookBack == -1 || lookBack > 0) && lookBack != Outside.GetLookback()) {
                Outside.SetLookback(lookBack);
                RecalculateOutsideWithMsg();
            }
        }
        private void ComboBoxSelectedEvent(ComboBoxSelectedItemChangedEventArgs obj)
        {
            foreach (var key in comboBoxMap.Keys)
            {
                switch (key)
                {
                    case "IntervalKey": {
                        string selected = comboBoxMap[key].SelectedItem;
                        if (selected != Outside.GetInterval().ShortName) {
                            Outside.SetInterval(StringToTimeframe(selected));
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                }
            }
        }
        private void CheckBoxClickEvent(CheckBoxEventArgs obj)
        {
            foreach (var key in checkBoxMap.Keys)
            {
                switch (key)
                {
                    case "KeepPOCKey": {
                        bool selected = (bool)checkBoxMap[key].IsChecked;
                        if (selected != Outside.KeepPOC) {
                            Outside.KeepPOC = selected;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                    case "ExtendedPOCKey": {
                        bool selected = (bool)checkBoxMap[key].IsChecked;
                        if (selected != Outside.ExtendPOC) {
                            Outside.ExtendPOC = selected;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                    case "ShowVAKey": {
                        bool selected = (bool)checkBoxMap[key].IsChecked;
                        if (selected != Outside.ShowVA) {
                            Outside.ShowVA = selected;
                            RecalculateOutsideWithMsg();
                        }
                        break;
                    }
                }
            }
        }
        private int GetValueFromInput(string inputKey, int defaultValue)
        {
            return int.TryParse(textInputMap[inputKey].Text, out int value) ? value : defaultValue;
        }
        private double GetDoubleFromInput(string inputKey, int defaultValue)
        {
            return double.TryParse(textInputMap[inputKey].Text, out double value) ? value : defaultValue;
        }
        private static TimeFrame StringToTimeframe(string inputTF)
        {
            TimeFrame ifWrong = TimeFrame.Minute;
            switch (inputTF)
            {
                // Candles
                case "h1": return TimeFrame.Hour;
                case "h2": return TimeFrame.Hour2;
                case "h3": return TimeFrame.Hour3;
                case "h4": return TimeFrame.Hour4;
                case "h6": return TimeFrame.Hour6;
                case "h8": return TimeFrame.Hour8;
                case "h12": return TimeFrame.Hour12;
                case "D1": return TimeFrame.Daily;
                case "D2": return TimeFrame.Day2;
                case "D3": return TimeFrame.Day3;
                case "W1": return TimeFrame.Weekly;
                case "Month1": return TimeFrame.Monthly;
                default:
                    break;
            }
            return ifWrong;
        }
    }

    // ====================== THEME ============================
    public static class Styles
    {
        public static Style CreatePanelBackgroundStyle()
        {
            Style style = new();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#292929"), 0.85m), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.85m), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }
        public static Style CreateCommonBorderStyle()
        {
            Style style = new();
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.12m), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#000000"), 0.12m), ControlState.LightTheme);
            return style;
        }
        public static Style CreateHeaderStyle()
        {
            Style style = new();
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#FFFFFF", 0.70m), ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#000000", 0.65m), ControlState.LightTheme);
            return style;
        }
        public static Style CreateInputStyle()
        {
            Style style = new(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#1A1A1A"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#111111"), ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#E7EBED"), ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#D6DADC"), ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.CornerRadius, 3);
            return style;
        }
        private static Color GetColorWithOpacity(Color baseColor, decimal opacity)
        {
            int alpha = (int)Math.Round(byte.MaxValue * opacity, MidpointRounding.AwayFromZero);
            return Color.FromArgb(alpha, baseColor);
        }
    }
}
