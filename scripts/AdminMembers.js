var AdminMembersJs = (function () {

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
        $ApiRequest('AdminMembers/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('am-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('am-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('am-open');
        }, 3200);
    }

    function openNav() {
        el('am-nav').classList.add('am-open');
        el('am-scrim').classList.add('am-open');
    }

    function closeNav() {
        el('am-nav').classList.remove('am-open');
        el('am-scrim').classList.remove('am-open');
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
        el('am-modal').classList.add('am-open');
        times(el('am-mbody'));
        var f = el('am-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('am-modal');
        if (m) {
            m.classList.remove('am-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('am-per')) {
            el('am-per').value = k;
        }
        if (k !== 'range') {
            if (el('am-from')) {
                el('am-from').value = '';
            }
            if (el('am-to')) {
                el('am-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('am-per') }, { key: 'pfrom', vlu: val('am-from') }, { key: 'pto', vlu: val('am-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('am-main'));
        if (window.MutationObserver && el('am-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('am-main'));
                }, 30);
            }).observe(el('am-main'), { childList: true, subtree: true });
        }
        if (window.AdminMembersJsloaded) {
            window.AdminMembersJsloaded();
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
        if (!askBox || !askBox.classList.contains('am-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('am-open');
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
        askBox.className = 'am-ask';
        askBox.innerHTML = '<div class="am-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="am-err" role="alert"></em><div class="am-askacts"><button type="button" class="am-btn am-btn-ghost am-btn-sm"></button><button type="button" class="am-btn am-btn-sm"></button></div></div>';
        (el('am-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('am-open')) {
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
        x.b[1].className = 'am-btn am-btn-sm' + (o.danger ? ' am-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('am-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('am-open');
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

    window.AdminMembersJsloaded = function () {
        if (qs('type') && el('am-mtype')) {
            el('am-mtype').value = qs('type');
        }
        list(1);
        desk();
        if (qs('id')) {
            open(qs('id'));
        }
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', [{ key: 'term', vlu: val('am-q') }, { key: 'mtype', vlu: val('am-mtype') }, { key: 'mtier', vlu: val('am-mtier') }, { key: 'mstat', vlu: val('am-mstat') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function desk() {
        api('Desk', []);
    }

    function open(id) {
        api('Open', [{ key: 'mid', vlu: id }]);
    }

    function checkIn() {
        api('CheckIn', [{ key: 'ccode', vlu: val('am-ccode') }]);
        return false;
    }

    function checkOut(id) {
        api('CheckOut', [{ key: 'vid', vlu: id }]);
    }

    function sell() {
        api('Sell', [{ key: 'wfirst', vlu: val('am-wfirst') }, { key: 'wlast', vlu: val('am-wlast') }, { key: 'wmail', vlu: val('am-wmail') }, { key: 'wphone', vlu: val('am-wphone') }]);
        return false;
    }

    function sellForm() {
        el('am-sellf').hidden = !el('am-sellf').hidden;
    }

    function save(id) {
        api('Save', [{ key: 'mid', vlu: id }, { key: 'mfirst', vlu: val('am-mfirst') }, { key: 'mlast', vlu: val('am-mlast') }, { key: 'mphone', vlu: val('am-mphone') }, { key: 'mborn', vlu: val('am-mborn') }, { key: 'mtype2', vlu: val('am-mtype2') }, { key: 'mnotes', vlu: val('am-mnotes') }]);
    }

    function suspend(id, on) {
        if (!on) {
            api('Suspend', [{ key: 'mid', vlu: id }, { key: 'son', vlu: '0' }, { key: 'swhy', vlu: '' }]);
            return;
        }
        ask({ title: 'Suspend this account?', text: 'They can no longer sign in or register. Existing registrations stay. Why?', value: '', ok: 'Suspend', danger: true, check: function (v) {
            return v.length < 3 ? 'Write a short reason.' : '';
        } }, function (v) {
            api('Suspend', [{ key: 'mid', vlu: id }, { key: 'son', vlu: '1' }, { key: 'swhy', vlu: v }]);
        });
    }

    function adjust(id) {
        api('Adjust', [{ key: 'mid', vlu: id }, { key: 'apts', vlu: val('am-apts') }, { key: 'awhy', vlu: val('am-awhy') }]);
    }

    function sellTo(id) {
        api('Sell', [{ key: 'wmid', vlu: id }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('am-', 'am-e-') : '';
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
        list: list,
        desk: desk,
        open: open,
        checkIn: checkIn,
        checkOut: checkOut,
        sell: sell,
        sellForm: sellForm,
        save: save,
        suspend: suspend,
        adjust: adjust,
        sellTo: sellTo
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminMembersJs.reveal);
} else {
    AdminMembersJs.reveal();
}
