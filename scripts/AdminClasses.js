var AdminClassesJs = (function () {

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
        $ApiRequest('AdminClasses/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ak-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ak-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ak-open');
        }, 3200);
    }

    function openNav() {
        el('ak-nav').classList.add('ak-open');
        el('ak-scrim').classList.add('ak-open');
    }

    function closeNav() {
        el('ak-nav').classList.remove('ak-open');
        el('ak-scrim').classList.remove('ak-open');
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
        el('ak-modal').classList.add('ak-open');
        times(el('ak-mbody'));
        var f = el('ak-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('ak-modal');
        if (m) {
            m.classList.remove('ak-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ak-per')) {
            el('ak-per').value = k;
        }
        if (k !== 'range') {
            if (el('ak-from')) {
                el('ak-from').value = '';
            }
            if (el('ak-to')) {
                el('ak-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ak-per') }, { key: 'pfrom', vlu: val('ak-from') }, { key: 'pto', vlu: val('ak-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ak-main'));
        if (window.MutationObserver && el('ak-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('ak-main'));
                }, 30);
            }).observe(el('ak-main'), { childList: true, subtree: true });
        }
        if (window.AdminClassesJsloaded) {
            window.AdminClassesJsloaded();
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
        if (!askBox || !askBox.classList.contains('ak-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ak-open');
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
        askBox.className = 'ak-ask';
        askBox.innerHTML = '<div class="ak-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ak-err" role="alert"></em><div class="ak-askacts"><button type="button" class="ak-btn ak-btn-ghost ak-btn-sm"></button><button type="button" class="ak-btn ak-btn-sm"></button></div></div>';
        (el('ak-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ak-open')) {
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
        x.b[1].className = 'ak-btn ak-btn-sm' + (o.danger ? ' ak-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ak-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ak-open');
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

    var pk = { kind: '', allow: '', multi: false, tag: '', done: null, page: 1, chosen: [] };
    var pkTimer = null;

    function pkBuild() {
        if (el('ak-pk')) {
            return;
        }
        var d = document.createElement('div');
        d.className = 'ak-pk';
        d.id = 'ak-pk';
        d.setAttribute('role', 'dialog');
        d.setAttribute('aria-modal', 'true');
        d.innerHTML = '<div class="ak-pkbox"><header><h2 id="ak-pkt">Choose from Media</h2><button type="button" class="ak-ibtn" aria-label="Close" onclick="AdminClassesJs.pkClose()">&times;</button></header><div class="ak-pkbar"><input type="search" id="ak-pkq" placeholder="Search by name or tag" maxlength="60" oninput="AdminClassesJs.pkSearch()"><select id="ak-pkkind" aria-label="Type" onchange="AdminClassesJs.pkList(1)"><option value="">Photos and videos</option><option value="photo">Photos</option><option value="video">Videos</option><option value="doc">Documents</option></select><select id="ak-pktag" aria-label="Tag" onchange="AdminClassesJs.pkList(1)"><option value="">Any tag</option></select><label class="ak-btn ak-btn-sm ak-btn-ghost ak-pkup" id="ak-pkupl">Upload a photo<input type="file" id="ak-pkfile" accept="image/jpeg,image/png,image/webp" onchange="AdminClassesJs.pkUpload(this)"></label></div><em class="ak-err ak-pkerr" id="ak-pkerr" role="alert"></em><div class="ak-pkgrid" id="ak-pkgrid"></div><div class="ak-pkmore" id="ak-pkmore"></div><footer><span id="ak-pkn"></span><button type="button" class="ak-btn ak-btn-ghost" onclick="AdminClassesJs.pkClose()">Cancel</button><button type="button" class="ak-btn ak-btn-accent" id="ak-pkuse" onclick="AdminClassesJs.pkUse()">Add</button></footer></div>';
        (el('ak-site') || document.body).appendChild(d);
        d.addEventListener('mousedown', function (e) {
            if (e.target === d) {
                pkClose();
            }
        });
        d.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                e.stopPropagation();
                pkClose();
            }
        });
    }

    function pickMedia(opts) {
        pkBuild();
        pk.allow = opts.allow || '';
        pk.multi = !!opts.multi;
        pk.tag = opts.tag || '';
        pk.done = opts.done;
        pk.chosen = [];
        el('ak-pkt').textContent = opts.title || 'Choose from Media';
        el('ak-pkq').value = '';
        el('ak-pkerr').textContent = '';
        var k = el('ak-pkkind');
        k.value = pk.allow === 'photo' || pk.allow === 'video' || pk.allow === 'doc' ? pk.allow : '';
        k.disabled = !!pk.allow;
        el('ak-pkupl').hidden = pk.allow === 'video' || pk.allow === 'doc';
        el('ak-pkuse').hidden = !pk.multi;
        pkCount();
        el('ak-pk').classList.add('ak-open');
        pkList(1);
        setTimeout(function () {
            el('ak-pkq').focus();
        }, 30);
    }

    function pkList(pg) {
        pk.page = pg || 1;
        api('MediaPick', [{ key: 'pkind', vlu: val('ak-pkkind') }, { key: 'pallow', vlu: pk.allow }, { key: 'pterm', vlu: val('ak-pkq') }, { key: 'ptag', vlu: val('ak-pktag') }, { key: 'pctx', vlu: pk.tag }, { key: 'ppg', vlu: String(pk.page) }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            pkList(1);
        }, 300);
    }

    function pkShown(html, more, tags, append) {
        var g = el('ak-pkgrid');
        if (append) {
            g.insertAdjacentHTML('beforeend', html);
        } else {
            g.innerHTML = html;
            g.scrollTop = 0;
        }
        el('ak-pkmore').innerHTML = more ? '<button type="button" class="ak-btn ak-btn-sm ak-btn-ghost" onclick="AdminClassesJs.pkList(' + (pk.page + 1) + ')">Show more</button>' : '';
        if (tags !== null) {
            var t = el('ak-pktag');
            var cur = t.value;
            t.innerHTML = '<option value="">Any tag</option>' + tags.map(function (x) {
                return '<option value="' + esc(x) + '">' + esc(x) + '</option>';
            }).join('');
            t.value = cur;
        }
        var b = g.querySelectorAll('.ak-pki');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('ak-on', pk.chosen.some(function (c) {
                return c.src === b[i].getAttribute('data-src');
            }));
        }
    }

    function pkItem(b) {
        return { src: b.getAttribute('data-src'), thumb: b.getAttribute('data-thumb'), kind: b.getAttribute('data-kind'), title: b.getAttribute('data-title') };
    }

    function pkToggle(b) {
        var it = pkItem(b);
        if (!pk.multi) {
            var f = pk.done;
            pkClose();
            if (f) {
                f([it]);
            }
            return;
        }
        var at = -1;
        pk.chosen.forEach(function (c, i) {
            if (c.src === it.src) {
                at = i;
            }
        });
        if (at >= 0) {
            pk.chosen.splice(at, 1);
        } else {
            pk.chosen.push(it);
        }
        b.classList.toggle('ak-on', at < 0);
        pkCount();
    }

    function pkCount() {
        var n = pk.chosen.length;
        el('ak-pkn').textContent = pk.multi ? (n ? n + ' selected' : 'Tap to select one or more') : 'Tap one to use it';
        el('ak-pkuse').disabled = n === 0;
        el('ak-pkuse').textContent = n > 1 ? 'Add ' + n : 'Add';
    }

    function pkUse() {
        var f = pk.done;
        var list = pk.chosen.slice();
        pkClose();
        if (f && list.length) {
            f(list);
        }
    }

    function pkClose() {
        var d = el('ak-pk');
        if (d) {
            d.classList.remove('ak-open');
        }
        pk.done = null;
    }

    function pkShrink(img, max, q) {
        var r = Math.min(1, max / Math.max(img.naturalWidth, img.naturalHeight));
        var c = document.createElement('canvas');
        c.width = Math.round(img.naturalWidth * r);
        c.height = Math.round(img.naturalHeight * r);
        c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
        var d = c.toDataURL('image/webp', q);
        return d.indexOf('data:image/webp') === 0 ? d : c.toDataURL('image/jpeg', q);
    }

    function pkUpload(input) {
        var f = input.files && input.files[0];
        el('ak-pkerr').textContent = '';
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type) || f.size > 30 * 1024 * 1024) {
            el('ak-pkerr').textContent = 'Choose a JPG, PNG or WebP photo under 30 MB.';
            input.value = '';
            return;
        }
        var url = URL.createObjectURL(f);
        var img = new Image();
        img.onload = function () {
            URL.revokeObjectURL(url);
            var full = pkShrink(img, 1600, 0.86);
            var thumb = pkShrink(img, 640, 0.8);
            el('ak-pkupl').classList.add('ak-busy');
            api('MediaUp', [{ key: 'full', vlu: full }, { key: 'thumb', vlu: thumb }, { key: 'pname', vlu: f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').substring(0, 80) }, { key: 'pctx', vlu: pk.tag }, { key: 'pw', vlu: String(img.naturalWidth) }, { key: 'ph', vlu: String(img.naturalHeight) }]);
        };
        img.onerror = function () {
            URL.revokeObjectURL(url);
            el('ak-pkerr').textContent = 'That image could not be read.';
        };
        img.src = url;
        input.value = '';
    }

    function pkUploaded(ok, msg) {
        el('ak-pkupl').classList.remove('ak-busy');
        if (!ok) {
            el('ak-pkerr').textContent = msg;
            return;
        }
        el('ak-pkq').value = '';
        el('ak-pkkind').value = pk.allow === 'photo' ? 'photo' : '';
        el('ak-pktag').value = '';
        pkList(1);
        toast('Photo added to Media.');
    }

    function picPick(id, allow) {
        pickMedia({ allow: allow || 'photo', tag: 'classes', title: 'Choose a photo', done: function (list) {
            var it = list[0];
            el(id).value = it.src;
            el(id + '-b').innerHTML = '<img src="' + esc(it.thumb) + '" alt="">';
        } });
    }

    function picClear(id) {
        el(id).value = '';
        el(id + '-b').innerHTML = '<span>Choose a photo</span>';
    }

    window.AdminClassesJsloaded = function () {
        if (qs('sport') && el('ak-ksport')) {
            el('ak-ksport').value = qs('sport');
        }
        list(1);
        if (qs('id')) {
            edit(qs('id') === 'new' ? '' : qs('id'));
        }
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', [{ key: 'term', vlu: val('ak-q') }, { key: 'fsport', vlu: val('ak-ksport') }, { key: 'fstate', vlu: val('ak-kstate') }, { key: 'fcoach', vlu: val('ak-kcoach') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function edit(id) {
        api('Form', [{ key: 'kid', vlu: id || '' }]);
    }

    function days() {
        return [].map.call(document.querySelectorAll('#ak-kdays input:checked'), function (x) {
            return x.value;
        }).join(',');
    }

    function preview() {
        api('Preview', [{ key: 'kfirst', vlu: val('ak-kfirst') }, { key: 'kdays', vlu: days() }, { key: 'kstart', vlu: val('ak-kstart') }, { key: 'kcount', vlu: val('ak-kcount') }, { key: 'kmin', vlu: val('ak-kmin') }]);
    }

    function save(id) {
        var keys = ['ktitle', 'ksp', 'klevel', 'kamin', 'kamax', 'kcoach2', 'kplace', 'kstart', 'kmin', 'kfirst', 'kcount', 'kprice', 'kcap', 'kopens', 'kimg', 'ktext', 'kbring'];
        var list2 = keys.map(function (k) {
            return { key: k, vlu: k === 'ktext' ? el('ak-' + k).value : val('ak-' + k) };
        });
        list2.push({ key: 'kid', vlu: id || '' });
        list2.push({ key: 'kdays', vlu: days() });
        list2.push({ key: 'kpub', vlu: el('ak-kpub').checked ? '1' : '0' });
        api('Save', list2);
    }

    function cancelClass(id, title, n) {
        ask({ title: 'Cancel ' + title + '?', text: n + ' people will be refunded in full and taken off the class. Tell them why:', value: '', place: 'Reason shown to members', ok: 'Cancel class and refund', no: 'Keep the class', danger: true, check: function (v) {
            return v.length < 5 ? 'Write a short reason.' : '';
        } }, function (v) {
            api('CancelClass', [{ key: 'kid', vlu: id }, { key: 'kwhy', vlu: v }]);
        });
    }

    function drop(id, title) {
        ask({ title: 'Delete ' + title + '?', text: 'Only classes nobody ever registered for can be deleted.', ok: 'Delete', danger: true }, function () {
            api('Drop', [{ key: 'kid', vlu: id }]);
        });
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
            var id = e.target && e.target.id ? e.target.id.replace('ak-', 'ak-e-') : '';
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
        edit: edit,
        preview: preview,
        save: save,
        cancelClass: cancelClass,
        drop: drop,
        picPick: picPick,
        picClear: picClear,
        pickMedia: pickMedia,
        pkList: pkList,
        pkSearch: pkSearch,
        pkShown: pkShown,
        pkToggle: pkToggle,
        pkUse: pkUse,
        pkClose: pkClose,
        pkUpload: pkUpload,
        pkUploaded: pkUploaded
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminClassesJs.reveal);
} else {
    AdminClassesJs.reveal();
}
