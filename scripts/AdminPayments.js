var AdminPaymentsJs = (function () {

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
        $ApiRequest('AdminPayments/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ap-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ap-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ap-open');
        }, 3200);
    }

    function openNav() {
        el('ap-nav').classList.add('ap-open');
        el('ap-scrim').classList.add('ap-open');
    }

    function closeNav() {
        el('ap-nav').classList.remove('ap-open');
        el('ap-scrim').classList.remove('ap-open');
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

    var stimer = null;
    var otimer = null;

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('sp_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('sp_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        setCookie('sp_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('ap-modal').classList.add('ap-open');
        times(el('ap-mbody'));
        var f = el('ap-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('ap-modal');
        if (m) {
            m.classList.remove('ap-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ap-per')) {
            el('ap-per').value = k;
        }
        if (k !== 'range') {
            if (el('ap-from')) {
                el('ap-from').value = '';
            }
            if (el('ap-to')) {
                el('ap-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ap-per') }, { key: 'pfrom', vlu: val('ap-from') }, { key: 'pto', vlu: val('ap-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ap-main'));
        if (window.MutationObserver && el('ap-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('ap-main'));
                }, 30);
            }).observe(el('ap-main'), { childList: true, subtree: true });
        }
        if (window.AdminPaymentsJsloaded) {
            window.AdminPaymentsJsloaded();
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
        if (!askBox || !askBox.classList.contains('ap-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ap-open');
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
        askBox.className = 'ap-ask';
        askBox.innerHTML = '<div class="ap-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ap-err" role="alert"></em><div class="ap-askacts"><button type="button" class="ap-btn ap-btn-ghost ap-btn-sm"></button><button type="button" class="ap-btn ap-btn-sm"></button></div></div>';
        (el('ap-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ap-open')) {
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
        x.b[1].className = 'ap-btn ap-btn-sm' + (o.danger ? ' ap-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ap-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ap-open');
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

    function printBox(id) {
        var src = el(id);
        if (!src) {
            return;
        }
        var f = document.createElement('iframe');
        f.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
        document.body.appendChild(f);
        var css = [].map.call(document.querySelectorAll('link[rel="stylesheet"]'), function (l) {
            return '<link rel="stylesheet" href="' + l.href + '">';
        }).join('');
        var d = f.contentDocument;
        d.open();
        d.write('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Receipt</title>' + css + '</head><body style="background:#fff"><div class="ap-site" data-theme="light" style="padding:20px;min-height:0;background:#fff">' + src.outerHTML + '</div></body></html>');
        d.close();
        setTimeout(function () {
            f.contentWindow.focus();
            f.contentWindow.print();
            setTimeout(function () {
                f.parentNode.removeChild(f);
            }, 1500);
        }, 450);
    }

    var tab = 'pay';

    window.AdminPaymentsJsloaded = function () {
        tab = qs('tab') === 'asks' ? 'asks' : 'pay';
        show(tab);
        if (qs('id')) {
            pay(qs('id'));
        }
    };

    function loadArgs() {
        return [];
    }

    function show(t) {
        tab = t;
        [].forEach.call(document.querySelectorAll('.ap-tab'), function (b) {
            b.classList.toggle('ap-act', b.getAttribute('data-t') === t);
        });
        el('ap-paybox').hidden = t !== 'pay';
        el('ap-askbox').hidden = t !== 'asks';
        try {
            history.replaceState(null, '', 'AdminPayments' + (t === 'asks' ? '?tab=asks' : ''));
        } catch (e) {
        }
        list(1);
    }

    function list(pg) {
        if (tab === 'asks') {
            api('Asks', [{ key: 'astat', vlu: val('ap-astat') }]);
            return;
        }
        api('List', [{ key: 'term', vlu: val('ap-q') }, { key: 'pkind', vlu: val('ap-pkind') }, { key: 'pmeth', vlu: val('ap-pmeth') }, { key: 'pg', vlu: String(pg || 1) }].concat(period()));
    }

    function pay(id) {
        api('Pay', [{ key: 'pid', vlu: id }]);
    }

    function askOpen(rid) {
        api('AskOpen', [{ key: 'rid', vlu: rid }]);
    }

    function decide(rid, ok) {
        api('Decide', [{ key: 'rid', vlu: rid }, { key: 'dok', vlu: ok ? '1' : '0' }, { key: 'damt', vlu: val('ap-damt') }, { key: 'dnote', vlu: val('ap-dnote') }, { key: 'dkeep', vlu: el('ap-dkeep') && el('ap-dkeep').checked ? '1' : '0' }]);
    }

    function refund(pid) {
        api('Refund', [{ key: 'pid', vlu: pid }, { key: 'famt', vlu: val('ap-famt') }, { key: 'fwhy', vlu: val('ap-fwhy') }]);
    }

    function csv() {
        api('Csv', [{ key: 'term', vlu: val('ap-q') }, { key: 'pkind', vlu: val('ap-pkind') }, { key: 'pmeth', vlu: val('ap-pmeth') }].concat(period()));
    }

    function download(name, b64) {
        var bin = atob(b64);
        var bytes = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) {
            bytes[i] = bin.charCodeAt(i);
        }
        var url = URL.createObjectURL(new Blob([bytes], { type: 'text/csv;charset=utf-8' }));
        var a = document.createElement('a');
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        setTimeout(function () {
            URL.revokeObjectURL(url);
            a.parentNode.removeChild(a);
        }, 500);
    }

    function reveal() {
        if (!getCookie('sp_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', loadArgs());
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('ap-', 'ap-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        per: per,
        search: search,
        loaded: loaded,
        show: show,
        list: list,
        pay: pay,
        askOpen: askOpen,
        decide: decide,
        refund: refund,
        csv: csv,
        download: download,
        printBox: printBox
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminPaymentsJs.reveal);
} else {
    AdminPaymentsJs.reveal();
}
