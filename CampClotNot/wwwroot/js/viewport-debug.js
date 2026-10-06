// Viewport diagnostics for the iOS home-screen app (#326). Triple-tap the mobile header to
// toggle a readout of every height iOS reports, so layout fixes can be based on real numbers
// rather than guesses. Remembered per device; nothing is sent anywhere.
(function () {
    const KEY = 'ccnVpDebug';
    let panel = null, probes = null, timer = null;

    function on() { try { return localStorage.getItem(KEY) === '1'; } catch { return false; } }
    function set(v) { try { v ? localStorage.setItem(KEY, '1') : localStorage.removeItem(KEY); } catch { } }

    // Elements whose rendered height tells us what each CSS unit/env() resolves to.
    function makeProbes() {
        const box = document.createElement('div');
        box.style.cssText = 'position:fixed;left:-9999px;top:0;visibility:hidden;pointer-events:none';
        const defs = {
            '100vh': 'height:100vh', '100svh': 'height:100svh', '100lvh': 'height:100lvh', '100dvh': 'height:100dvh',
            'fill-avail': 'height:-webkit-fill-available', 'inset0': 'position:fixed;top:0;bottom:0',
            'safeTop': 'height:env(safe-area-inset-top)', 'safeBottom': 'height:env(safe-area-inset-bottom)'
        };
        const out = {};
        for (const [k, css] of Object.entries(defs)) {
            const d = document.createElement('div');
            d.style.cssText = 'width:1px;' + css;
            box.appendChild(d); out[k] = d;
        }
        document.body.appendChild(box);
        return out;
    }

    function rect(sel) {
        const e = [...document.querySelectorAll(sel)].find(x => getComputedStyle(x).display !== 'none');
        if (!e) return '–';
        const r = e.getBoundingClientRect();
        return `${Math.round(r.top)}→${Math.round(r.bottom)} (h${Math.round(r.height)})`;
    }

    function render() {
        if (!panel) return;
        const h = k => Math.round(probes[k].getBoundingClientRect().height);
        const vv = window.visualViewport;
        const rows = [
            ['path', location.pathname],
            ['standalone', String(matchMedia('(display-mode: standalone)').matches || navigator.standalone === true)],
            ['screen', `${screen.width}×${screen.height}`],
            ['innerH', innerHeight], ['clientH', document.documentElement.clientHeight],
            ['visualVP', vv ? `${Math.round(vv.height)} @${Math.round(vv.offsetTop)} ×${vv.scale}` : '–'],
            ['100vh/svh/lvh/dvh', `${h('100vh')}/${h('100svh')}/${h('100lvh')}/${h('100dvh')}`],
            ['fill-avail / inset0', `${h('fill-avail')} / ${h('inset0')}`],
            ['safe top/bottom', `${h('safeTop')} / ${h('safeBottom')}`],
            ['scrollY', Math.round(scrollY)],
            ['.app-frame', rect('.app-frame')], ['header', rect('.nav-mobile-header')],
            ['.app-main', rect('.app-main')], ['bottom bar', rect('.nav-bottom-bar')],
            ['ua', navigator.userAgent.replace(/^Mozilla\/5\.0 /, '').slice(0, 90)]
        ];
        panel.innerHTML = rows.map(([k, v]) => `<div><b>${k}</b> ${v}</div>`).join('') +
            '<div style="opacity:.6;margin-top:4px">triple-tap header to hide</div>';
    }

    function show() {
        if (panel) return;
        probes = probes || makeProbes();
        panel = document.createElement('div');
        panel.style.cssText = 'position:fixed;left:8px;right:8px;top:45%;z-index:2147483647;background:rgba(0,0,0,.85);' +
            'color:#0f0;font:11px/1.35 ui-monospace,Menlo,monospace;padding:8px 10px;border-radius:8px;pointer-events:none;' +
            'word-break:break-all';
        document.body.appendChild(panel);
        render();
        timer = setInterval(render, 500);
    }

    function hide() {
        clearInterval(timer); timer = null;
        panel?.remove(); panel = null;
    }

    // Triple-tap (3 taps within ~700ms) on the mobile header toggles it. The push banner covers
    // the header when it's showing, so its text counts too (not its buttons).
    let taps = [];
    document.addEventListener('click', e => {
        const t = e.target;
        if (!t.closest || t.closest('button') || !t.closest('.nav-mobile-header, .ccn-push-banner')) return;
        const now = Date.now();
        taps = taps.filter(t => now - t < 700); taps.push(now);
        if (taps.length < 3) return;
        taps = [];
        const next = !on(); set(next); next ? show() : hide();
    }, true);

    const start = () => { if (on()) show(); };
    document.readyState === 'loading' ? document.addEventListener('DOMContentLoaded', start) : start();
})();
