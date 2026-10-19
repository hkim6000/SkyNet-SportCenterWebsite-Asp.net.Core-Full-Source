var InstructorsJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Instructors/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('it-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('it-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('it-open');
        }, 3200);
    }

    function openNav() {
        el('it-nav').classList.add('it-open');
        el('it-scrim').classList.add('it-open');
    }

    function closeNav() {
        el('it-nav').classList.remove('it-open');
        el('it-scrim').classList.remove('it-open');
    }

    function ago(d) {
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) {
            return 'Just now';
        }
        if (s < 3600) {
            var m = Math.floor(s / 60);
            return m + (m === 1 ? ' min ago' : ' mins ago');
        }
        if (s < 86400) {
            var h = Math.floor(s / 3600);
            return h + (h === 1 ? ' hour ago' : ' hours ago');
        }
        return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]:not([data-x])');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            t.setAttribute('data-x', '1');
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'hm') {
                t.textContent = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    var pendingReady = true;

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function sbx() {
        return getCookie('sp_sbx');
    }

    function mem() {
        return getCookie('sp_mem');
    }

    function send(method, list) {
        list = list || [];
        list.push({ key: 'sbx', vlu: sbx() });
        list.push({ key: 'mtok', vlu: mem() });
        call(method, list);
    }

    function applyTheme(theme) {
        el('it-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('it-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('sp-theme', next);
        } catch (e) {
        }
    }

    function demoBar(mins) {
        var d = el('it-demo');
        if (!d) {
            return;
        }
        d.innerHTML = 'Your demo copy' + (mins ? ' &middot; ' + mins + ' min left' : '') + ' &middot; <a href="AdminLogin">Open the admin</a>';
        d.hidden = false;
    }

    function hydrated(ok, accent, theme, mins) {
        document.documentElement.classList.remove('it-hydrating');
        if (!ok) {
            setCookie('sp_sbx', '', 0);
            runReady();
            return;
        }
        if (accent) {
            el('it-site').style.setProperty('--it-accent', accent);
        }
        var stored = '';
        try {
            stored = localStorage.getItem('sp-theme') || '';
        } catch (e) {
        }
        applyTheme(stored || theme);
        demoBar(mins);
        times();
        runReady();
    }

    function sandboxed(id) {
        setCookie('sp_sbx', id, 3600);
        demoBar(60);
    }

    function signedIn(tok, secs, go) {
        setCookie('sp_mem', tok, secs);
        if (go) {
            location.href = go;
        }
    }

    function signedOut() {
        setCookie('sp_mem', '', 0);
    }

    function who(name, tier, tierName) {
        var m = el('it-mem');
        if (!m) {
            return;
        }
        if (!name) {
            signedOut();
            return;
        }
        if (el('it-site')) {
            el('it-site').classList.add('it-in');
        }
        m.href = 'Member';
        m.innerHTML = '<span>' + esc(name) + '</span>' + (tier ? '<span class="it-tier it-t-' + esc(tier) + '">' + esc(tierName) + '</span>' : '');
        m.title = 'Member area';
        var c = el('it-cta');
        if (c) {
            c.textContent = 'My classes';
            c.href = 'Member';
        }
    }

    function fieldErr(id, msg) {
        var e = el('it-e-' + id);
        if (e) {
            e.textContent = msg;
        }
        var f = el('it-' + id);
        if (f && f.focus) {
            f.focus();
        }
    }

    function fmtCard(i) {
        var v = i.value.replace(/\D/g, '').slice(0, 19);
        i.value = v.replace(/(.{4})/g, '$1 ').trim();
    }

    function fmtExp(i) {
        var v = i.value.replace(/\D/g, '').slice(0, 4);
        i.value = v.length > 2 ? v.slice(0, 2) + '/' + v.slice(2) : v;
    }

    function printBox(id) {
        var src = el(id);
        if (!src) {
            return;
        }
        var f = document.createElement('iframe');
        f.setAttribute('aria-hidden', 'true');
        f.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
        document.body.appendChild(f);
        var css = [].map.call(document.querySelectorAll('link[rel="stylesheet"]'), function (l) {
            return '<link rel="stylesheet" href="' + l.href + '">';
        }).join('');
        var d = f.contentDocument;
        d.open();
        d.write('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Receipt</title>' + css + '</head><body><div class="it-site it-printing" data-theme="light">' + src.outerHTML + '</div></body></html>');
        d.close();
        setTimeout(function () {
            f.contentWindow.focus();
            f.contentWindow.print();
            setTimeout(function () {
                f.parentNode.removeChild(f);
            }, 1500);
        }, 450);
    }

    function runReady() {
        if (typeof ready === 'function') {
            ready();
        }
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('it-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('it-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'it-ask';
        askBox.innerHTML = '<div class="it-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="it-err" role="alert"></em><div class="it-askacts"><button type="button" class="it-btn it-btn-ghost it-btn-sm"></button><button type="button" class="it-btn it-btn-sm"></button></div></div>';
        (el('it-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('it-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'it-btn it-btn-sm' + (o.danger ? ' it-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('it-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('it-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function leave(go) {
        ask({ title: 'Leave without saving?', text: 'Your changes since the last save will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, go);
    }

    function start() {
    }

    function ready() {
    }

    function reveal() {
        var stored = '';
        try {
            stored = localStorage.getItem('sp-theme') || '';
        } catch (e) {
        }
        if (stored) {
            applyTheme(stored);
        }
        times();
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('it-', 'it-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
        start();
        if (mem()) {
            send('Who', []);
        }
        if (sbx()) {
            call('Hydrate', [{ key: 'sbx', vlu: sbx() }, { key: 'qstr', vlu: location.search }]);
        } else {
            runReady();
        }
    }

    if (document.cookie.indexOf('sp_sbx=') >= 0) {
        document.documentElement.classList.add('it-hydrating');
        setTimeout(function () {
            document.documentElement.classList.remove('it-hydrating');
        }, 2500);
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        hydrated: hydrated,
        sandboxed: sandboxed,
        signedIn: signedIn,
        signedOut: signedOut,
        who: who,
        fieldErr: fieldErr,
        fmtCard: fmtCard,
        fmtExp: fmtExp,
        printBox: printBox
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', InstructorsJs.reveal);
} else {
    InstructorsJs.reveal();
}
