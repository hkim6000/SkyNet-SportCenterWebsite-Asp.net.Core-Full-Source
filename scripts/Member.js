var MemberJs = (function () {

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
        $ApiRequest('Member/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('mb-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('mb-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('mb-open');
        }, 3200);
    }

    function openNav() {
        el('mb-nav').classList.add('mb-open');
        el('mb-scrim').classList.add('mb-open');
    }

    function closeNav() {
        el('mb-nav').classList.remove('mb-open');
        el('mb-scrim').classList.remove('mb-open');
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
        el('mb-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('mb-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('sp-theme', next);
        } catch (e) {
        }
    }

    function demoBar(mins) {
        var d = el('mb-demo');
        if (!d) {
            return;
        }
        d.innerHTML = 'Your demo copy' + (mins ? ' &middot; ' + mins + ' min left' : '') + ' &middot; <a href="AdminLogin">Open the admin</a>';
        d.hidden = false;
    }

    function hydrated(ok, accent, theme, mins) {
        document.documentElement.classList.remove('mb-hydrating');
        if (!ok) {
            setCookie('sp_sbx', '', 0);
            runReady();
            return;
        }
        if (accent) {
            el('mb-site').style.setProperty('--mb-accent', accent);
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
        var m = el('mb-mem');
        if (!m) {
            return;
        }
        if (!name) {
            signedOut();
            return;
        }
        if (el('mb-site')) {
            el('mb-site').classList.add('mb-in');
        }
        m.href = 'Member';
        m.innerHTML = '<span>' + esc(name) + '</span>' + (tier ? '<span class="mb-tier mb-t-' + esc(tier) + '">' + esc(tierName) + '</span>' : '');
        m.title = 'Member area';
        var c = el('mb-cta');
        if (c) {
            c.textContent = 'My classes';
            c.href = 'Member';
        }
    }

    function fieldErr(id, msg) {
        var e = el('mb-e-' + id);
        if (e) {
            e.textContent = msg;
        }
        var f = el('mb-' + id);
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
        d.write('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Receipt</title>' + css + '</head><body><div class="mb-site mb-printing" data-theme="light">' + src.outerHTML + '</div></body></html>');
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
        if (!askBox || !askBox.classList.contains('mb-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('mb-open');
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
        askBox.className = 'mb-ask';
        askBox.innerHTML = '<div class="mb-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="mb-err" role="alert"></em><div class="mb-askacts"><button type="button" class="mb-btn mb-btn-ghost mb-btn-sm"></button><button type="button" class="mb-btn mb-btn-sm"></button></div></div>';
        (el('mb-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('mb-open')) {
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
        x.b[1].className = 'mb-btn mb-btn-sm' + (o.danger ? ' mb-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('mb-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('mb-open');
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

    function cardVals() {
        return [{ key: 'cname', vlu: val('mb-cname') }, { key: 'cnum', vlu: val('mb-cnum') }, { key: 'cexp', vlu: val('mb-cexp') }, { key: 'ccvc', vlu: val('mb-ccvc') }];
    }

    var cur = 'overview';
    var mback = null;

    function start() {
        var t = qs('tab');
        if (t) {
            cur = t;
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeModal();
            }
        });
    }

    function ready() {
        if (!mem()) {
            needSignIn();
            return;
        }
        load();
    }

    function load() {
        send('Load', [{ key: 'mtab', vlu: cur }, { key: 'mwel', vlu: qs('welcome') }]);
    }

    function reload() {
        load();
    }

    function needSignIn() {
        signedOut();
        location.replace('SignIn?next=' + encodeURIComponent('Member' + (cur !== 'overview' ? '?tab=' + cur : '')));
    }

    function tab(t) {
        cur = t;
        [].forEach.call(document.querySelectorAll('.mb-mtab'), function (b) {
            b.classList.toggle('mb-act', b.getAttribute('data-tab') === t);
        });
        try {
            history.replaceState(null, '', 'Member' + (t !== 'overview' ? '?tab=' + t : ''));
        } catch (e) {
        }
        load();
    }

    function loaded(t) {
        times(el('mb-mbody'));
        var a = document.querySelector('.mb-mtab.mb-act');
        if (a && a.scrollIntoView && window.innerWidth < 640) {
            a.scrollIntoView({ inline: 'center', block: 'nearest' });
        }
    }

    function openModal() {
        mback = document.activeElement;
        el('mb-modal').hidden = false;
        document.body.style.overflow = 'hidden';
        var f = el('mb-mpanel').querySelector('input:not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        if (el('mb-modal').hidden) {
            return;
        }
        el('mb-modal').hidden = true;
        document.body.style.overflow = '';
        if (mback && mback.focus && document.body.contains(mback)) {
            mback.focus();
        }
    }

    function receipt(id) {
        send('Receipt', [{ key: 'rid', vlu: id }]);
    }

    function passReceipt(id) {
        send('PassReceipt', [{ key: 'pid', vlu: id }]);
    }

    function payForm(id) {
        send('PayForm', [{ key: 'rid', vlu: id }]);
    }

    function payQuote(id) {
        send('PayQuote', [{ key: 'rid', vlu: id }, { key: 'pts', vlu: val('mb-ppts') || '0' }]);
    }

    function pay(id) {
        el('mb-paygo').disabled = true;
        el('mb-e-pay').textContent = '';
        send('Pay', [{ key: 'rid', vlu: id }, { key: 'pts', vlu: val('mb-ppts') || '0' }, { key: 'bstreet', vlu: val('mb-bstreet') }, { key: 'bcity', vlu: val('mb-bcity') }, { key: 'bregion', vlu: val('mb-bregion') }, { key: 'bzip', vlu: val('mb-bzip') }].concat(cardVals()));
        return false;
    }

    function paid(ok) {
        if (ok) {
            closeModal();
            reload();
            return;
        }
        if (el('mb-paygo')) {
            el('mb-paygo').disabled = false;
        }
    }

    function cancelReg(id, label, text) {
        ask({ title: label + '?', text: text, ok: label, no: 'Keep it', danger: true }, function () {
            send('CancelReg', [{ key: 'rid', vlu: id }]);
        });
    }

    function askForm(id, text) {
        el('mb-mpanel').innerHTML = '<h2>Ask for a refund</h2><p class="mb-hint"></p><form class="mb-form" onsubmit="return MemberJs.askSend(\'' + esc(id) + '\')" novalidate><label class="mb-field"><span>What happened?</span><textarea id="mb-areason" rows="4" maxlength="600"></textarea></label><em class="mb-err" id="mb-e-areason" role="alert"></em><div class="mb-mfoot"><button type="button" class="mb-btn mb-btn-ghost" onclick="MemberJs.closeModal()">Cancel</button><button type="submit" class="mb-btn mb-btn-accent">Send request</button></div></form>';
        el('mb-mpanel').querySelector('p').textContent = text;
        openModal();
    }

    function askSend(id) {
        send('AskRefund', [{ key: 'rid', vlu: id }, { key: 'areason', vlu: val('mb-areason') }]);
        return false;
    }

    function famForm(id) {
        send('FamForm', [{ key: 'fid', vlu: id }]);
    }

    function famSave(id) {
        send('FamSave', [{ key: 'fid', vlu: id }, { key: 'ffirst', vlu: val('mb-ffirst') }, { key: 'flast', vlu: val('mb-flast') }, { key: 'fborn', vlu: val('mb-fborn') }, { key: 'frel', vlu: val('mb-frel') }]);
        return false;
    }

    function famDrop(id) {
        ask({ title: 'Remove this family member?', text: 'Their past classes stay on your receipts.', ok: 'Remove', danger: true }, function () {
            send('FamDrop', [{ key: 'fid', vlu: id }]);
        });
    }

    function saveProfile() {
        send('SaveProfile', ['pfirst', 'plast', 'pphone', 'pborn', 'pstreet', 'pcity', 'pregion', 'pzip'].map(function (k) {
            return { key: k, vlu: val('mb-' + k) };
        }));
        return false;
    }

    function savePass() {
        send('SavePass', [{ key: 'pcur', vlu: el('mb-pcur').value }, { key: 'pnew', vlu: el('mb-pnew').value }]);
        return false;
    }

    function signOutMember() {
        send('SignOutMember', []);
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
            var id = e.target && e.target.id ? e.target.id.replace('mb-', 'mb-e-') : '';
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
        document.documentElement.classList.add('mb-hydrating');
        setTimeout(function () {
            document.documentElement.classList.remove('mb-hydrating');
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
        printBox: printBox,
        tab: tab,
        loaded: loaded,
        reload: reload,
        needSignIn: needSignIn,
        openModal: openModal,
        closeModal: closeModal,
        receipt: receipt,
        passReceipt: passReceipt,
        payForm: payForm,
        payQuote: payQuote,
        pay: pay,
        paid: paid,
        cancelReg: cancelReg,
        askForm: askForm,
        askSend: askSend,
        famForm: famForm,
        famSave: famSave,
        famDrop: famDrop,
        saveProfile: saveProfile,
        savePass: savePass,
        signOutMember: signOutMember
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', MemberJs.reveal);
} else {
    MemberJs.reveal();
}
