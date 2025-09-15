Every aspect of **Order Flow Aggregated** was carefully designed with performance in mind, so it can run even in lower-end machines.

Still, here some **"Tips and Tricks"** for better usage:
- **Tick data** are stored in RAM, even though cTrader already have good memory management:
    - Load just what's really needed for live trading.
- **Params Panel** already shows which parameters are supported by each mode.
- **Segments Interval** may change the filters analysis, use **Daily** interval for intraday momentum.
- At Higher Timeframes (Price-Based Charts included)
    - Set **ODF + VP** to **Weekly**
    - **Nº days / Reset Daily?** parameters is now Week/Weekly.
    - All filters will be reset every Week instead of each Day.
- **"Lower Timeframe"** with **"Small Row Size"**:
    - Custom MAs are made to speed up the recalculate when switching settings at Params Panel.
    - If you want to stick with cTrader Moving Averages, set **[ODF] Use Custom MAs?** to **False/No**
        - Have patience if any Filter [Results / Spike / Bubbles] is activated.
- If **High-Performance** is needed:
    - Disable Volume Profile and/or use Mini-VPs instead. (default)
        - Use "Concurrent" on "[VP] Update Strategy" at "Specific Settings", there's a RAM usage cost, though. (default)
    - Use "Redraw_Fastest" on "Performance Drawing" settings (default)
    - Use CustomMAs (default)
    - Any filled and/or Text drawings are quite heavy when there are many of them.
- When backtesting, some drawings on Price-Based Charts are broken, but works fine in real-time.    
- Saved Settings are stored at "Documents/cAlgo/LocalStorage/ODFT-AGG ..."

**New analysis features of Order Flow Ticks (Aggregated)**:
- **Tick Spike Filter**
    - Moving Average or Standard Deviation
    - as Icon or Bubbles
    - Spike Levels
    - Spike Chart (Bubbles):
        - Heatmap Coloring
        - Positive/Negative
- **Bubbles Chart**
    - Moving Averages, Standard Deviation, or both.
    - [Volume Delta / Cumulative Delta Change / Subtract Delta] as source.
    - Ultra Bubbles Levels
    - Heatmap Coloring
    - Momentum Coloring
        - Fading:
        - Positive/Negative
- **Volume Profile**
    - Mini-VPs - From 5 minutes to 1 day.
    - Daily, Weekly, Monthly Profiles

Here some improvements made since ODF-Ticks revision 1.3:
- **Order Flow Ticks(agg):**
    - Row Height by ATR
    - Perfomance Drawing - No more "Nº Bars to Show"!
    - High-performance Tick Volume calculation - Handles even 1 month of ticks with ease!
    - Asynchronous Tick Data Collection
    - Use of CTrader's 5.x specific features:
        - Built-in Songs/Pop-ups => [Tick Spike, Ultra Bubbles] Alert.
        - Local storage => Save Params.
        - ProgressBar => Calculating...
    - Better Params UI.
    - Better Support for [Ticks / Renko / Range] Charts.
    - Static Update of Drawings, where possible.
- **Free Volume Profile:**
    - Concurrent Live VP Update
    - Daily/Monthly/Weekly Shared Segments
    - Mini-VPs that uses the current shared Segments.
    - Show Any or All (Mini-VPs/Daily/Weekly/Monthly) Profiles at once!
