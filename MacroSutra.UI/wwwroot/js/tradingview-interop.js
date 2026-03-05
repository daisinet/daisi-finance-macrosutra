// TradingView Lightweight Charts JS interop
window.TradingViewInterop = {
    charts: {},

    createChart: function (elementId, options) {
        const container = document.getElementById(elementId);
        if (!container || !window.LightweightCharts) return;
        const chart = LightweightCharts.createChart(container, {
            width: container.clientWidth,
            height: options.height || 400,
            layout: { background: { type: 'solid', color: options.darkMode ? '#1e1e1e' : '#ffffff' }, textColor: options.darkMode ? '#d1d4dc' : '#191919' },
            grid: { vertLines: { color: options.darkMode ? '#2B2B43' : '#e1ecf2' }, horzLines: { color: options.darkMode ? '#2B2B43' : '#e1ecf2' } },
            timeScale: { timeVisible: true, secondsVisible: false }
        });
        this.charts[elementId] = chart;
        return true;
    },

    _saveViewState: function (chart) {
        try {
            return {
                logicalRange: chart.timeScale().getVisibleLogicalRange()
            };
        } catch { return null; }
    },

    _restoreViewState: function (chart, state) {
        if (!state || !state.logicalRange) {
            chart.timeScale().fitContent();
            return;
        }
        try {
            chart.timeScale().setVisibleLogicalRange(state.logicalRange);
        } catch { chart.timeScale().fitContent(); }
    },

    _clearSeries: function (chart) {
        if (chart._candleSeries) { chart.removeSeries(chart._candleSeries); chart._candleSeries = null; }
        if (chart._lineSeries) { chart.removeSeries(chart._lineSeries); chart._lineSeries = null; }
    },

    setCandlestickData: function (elementId, data, preserveView) {
        const chart = this.charts[elementId];
        if (!chart) return;
        const state = preserveView ? this._saveViewState(chart) : null;
        this._clearSeries(chart);
        const series = chart.addSeries(LightweightCharts.CandlestickSeries, {
            upColor: '#26a69a', downColor: '#ef5350', borderVisible: false,
            wickUpColor: '#26a69a', wickDownColor: '#ef5350'
        });
        series.setData(data.map(d => ({ time: d.time, open: d.open, high: d.high, low: d.low, close: d.close })));
        chart._candleSeries = series;
        this._restoreViewState(chart, state);
    },

    setLineData: function (elementId, data, preserveView) {
        const chart = this.charts[elementId];
        if (!chart) return;
        const state = preserveView ? this._saveViewState(chart) : null;
        this._clearSeries(chart);
        const series = chart.addSeries(LightweightCharts.LineSeries, { color: '#2962FF', lineWidth: 2 });
        series.setData(data.map(d => ({ time: d.time, value: d.close })));
        chart._lineSeries = series;
        this._restoreViewState(chart, state);
    },

    resizeChart: function (elementId, width, height) {
        const chart = this.charts[elementId];
        if (chart) chart.resize(width, height);
    },

    destroyChart: function (elementId) {
        const chart = this.charts[elementId];
        if (chart) { chart.remove(); delete this.charts[elementId]; }
    }
};

// Recent symbols localStorage helper
window.RecentSymbols = {
    key: 'macrosutra_recent_symbols',
    get: function () {
        try { return JSON.parse(localStorage.getItem(this.key)) || []; }
        catch { return []; }
    },
    add: function (symbol) {
        var list = this.get().filter(s => s !== symbol);
        list.unshift(symbol);
        if (list.length > 10) list = list.slice(0, 10);
        localStorage.setItem(this.key, JSON.stringify(list));
        return list;
    }
};

// File download helper
window.downloadFile = function (filename, contentType, base64) {
    const link = document.createElement('a');
    link.href = 'data:' + contentType + ';base64,' + base64;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
