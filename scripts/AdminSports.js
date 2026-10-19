var AdminSportsJs = (function () {

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
        $ApiRequest('AdminSports/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('as-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('as-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('as-open');
        }, 3200);
    }

    function openNav() {
        el('as-nav').classList.add('as-open');
        el('as-scrim').classList.add('as-open');
    }

    function closeNav() {
        el('as-nav').classList.remove('as-open');
        el('as-scrim').classList.remove('as-open');
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
        el('as-modal').classList.add('as-open');
        times(el('as-mbody'));
        var f = el('as-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('as-modal');
        if (m) {
            m.classList.remove('as-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('as-per')) {
            el('as-per').value = k;
        }
        if (k !== 'range') {
            if (el('as-from')) {
                el('as-from').value = '';
            }
            if (el('as-to')) {
                el('as-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('as-per') }, { key: 'pfrom', vlu: val('as-from') }, { key: 'pto', vlu: val('as-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('as-main'));
        if (window.MutationObserver && el('as-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('as-main'));
                }, 30);
            }).observe(el('as-main'), { childList: true, subtree: true });
        }
        if (window.AdminSportsJsloaded) {
            window.AdminSportsJsloaded();
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
        if (!askBox || !askBox.classList.contains('as-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('as-open');
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
        askBox.className = 'as-ask';
        askBox.innerHTML = '<div class="as-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="as-err" role="alert"></em><div class="as-askacts"><button type="button" class="as-btn as-btn-ghost as-btn-sm"></button><button type="button" class="as-btn as-btn-sm"></button></div></div>';
        (el('as-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('as-open')) {
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
        x.b[1].className = 'as-btn as-btn-sm' + (o.danger ? ' as-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('as-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('as-open');
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
        if (el('as-pk')) {
            return;
        }
        var d = document.createElement('div');
        d.className = 'as-pk';
        d.id = 'as-pk';
        d.setAttribute('role', 'dialog');
        d.setAttribute('aria-modal', 'true');
        d.innerHTML = '<div class="as-pkbox"><header><h2 id="as-pkt">Choose from Media</h2><button type="button" class="as-ibtn" aria-label="Close" onclick="AdminSportsJs.pkClose()">&times;</button></header><div class="as-pkbar"><input type="search" id="as-pkq" placeholder="Search by name or tag" maxlength="60" oninput="AdminSportsJs.pkSearch()"><select id="as-pkkind" aria-label="Type" onchange="AdminSportsJs.pkList(1)"><option value="">Photos and videos</option><option value="photo">Photos</option><option value="video">Videos</option><option value="doc">Documents</option></select><select id="as-pktag" aria-label="Tag" onchange="AdminSportsJs.pkList(1)"><option value="">Any tag</option></select><label class="as-btn as-btn-sm as-btn-ghost as-pkup" id="as-pkupl">Upload a photo<input type="file" id="as-pkfile" accept="image/jpeg,image/png,image/webp" onchange="AdminSportsJs.pkUpload(this)"></label></div><em class="as-err as-pkerr" id="as-pkerr" role="alert"></em><div class="as-pkgrid" id="as-pkgrid"></div><div class="as-pkmore" id="as-pkmore"></div><footer><span id="as-pkn"></span><button type="button" class="as-btn as-btn-ghost" onclick="AdminSportsJs.pkClose()">Cancel</button><button type="button" class="as-btn as-btn-accent" id="as-pkuse" onclick="AdminSportsJs.pkUse()">Add</button></footer></div>';
        (el('as-site') || document.body).appendChild(d);
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
        el('as-pkt').textContent = opts.title || 'Choose from Media';
        el('as-pkq').value = '';
        el('as-pkerr').textContent = '';
        var k = el('as-pkkind');
        k.value = pk.allow === 'photo' || pk.allow === 'video' || pk.allow === 'doc' ? pk.allow : '';
        k.disabled = !!pk.allow;
        el('as-pkupl').hidden = pk.allow === 'video' || pk.allow === 'doc';
        el('as-pkuse').hidden = !pk.multi;
        pkCount();
        el('as-pk').classList.add('as-open');
        pkList(1);
        setTimeout(function () {
            el('as-pkq').focus();
        }, 30);
    }

    function pkList(pg) {
        pk.page = pg || 1;
        api('MediaPick', [{ key: 'pkind', vlu: val('as-pkkind') }, { key: 'pallow', vlu: pk.allow }, { key: 'pterm', vlu: val('as-pkq') }, { key: 'ptag', vlu: val('as-pktag') }, { key: 'pctx', vlu: pk.tag }, { key: 'ppg', vlu: String(pk.page) }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            pkList(1);
        }, 300);
    }

    function pkShown(html, more, tags, append) {
        var g = el('as-pkgrid');
        if (append) {
            g.insertAdjacentHTML('beforeend', html);
        } else {
            g.innerHTML = html;
            g.scrollTop = 0;
        }
        el('as-pkmore').innerHTML = more ? '<button type="button" class="as-btn as-btn-sm as-btn-ghost" onclick="AdminSportsJs.pkList(' + (pk.page + 1) + ')">Show more</button>' : '';
        if (tags !== null) {
            var t = el('as-pktag');
            var cur = t.value;
            t.innerHTML = '<option value="">Any tag</option>' + tags.map(function (x) {
                return '<option value="' + esc(x) + '">' + esc(x) + '</option>';
            }).join('');
            t.value = cur;
        }
        var b = g.querySelectorAll('.as-pki');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('as-on', pk.chosen.some(function (c) {
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
        b.classList.toggle('as-on', at < 0);
        pkCount();
    }

    function pkCount() {
        var n = pk.chosen.length;
        el('as-pkn').textContent = pk.multi ? (n ? n + ' selected' : 'Tap to select one or more') : 'Tap one to use it';
        el('as-pkuse').disabled = n === 0;
        el('as-pkuse').textContent = n > 1 ? 'Add ' + n : 'Add';
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
        var d = el('as-pk');
        if (d) {
            d.classList.remove('as-open');
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
        el('as-pkerr').textContent = '';
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type) || f.size > 30 * 1024 * 1024) {
            el('as-pkerr').textContent = 'Choose a JPG, PNG or WebP photo under 30 MB.';
            input.value = '';
            return;
        }
        var url = URL.createObjectURL(f);
        var img = new Image();
        img.onload = function () {
            URL.revokeObjectURL(url);
            var full = pkShrink(img, 1600, 0.86);
            var thumb = pkShrink(img, 640, 0.8);
            el('as-pkupl').classList.add('as-busy');
            api('MediaUp', [{ key: 'full', vlu: full }, { key: 'thumb', vlu: thumb }, { key: 'pname', vlu: f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').substring(0, 80) }, { key: 'pctx', vlu: pk.tag }, { key: 'pw', vlu: String(img.naturalWidth) }, { key: 'ph', vlu: String(img.naturalHeight) }]);
        };
        img.onerror = function () {
            URL.revokeObjectURL(url);
            el('as-pkerr').textContent = 'That image could not be read.';
        };
        img.src = url;
        input.value = '';
    }

    function pkUploaded(ok, msg) {
        el('as-pkupl').classList.remove('as-busy');
        if (!ok) {
            el('as-pkerr').textContent = msg;
            return;
        }
        el('as-pkq').value = '';
        el('as-pkkind').value = pk.allow === 'photo' ? 'photo' : '';
        el('as-pktag').value = '';
        pkList(1);
        toast('Photo added to Media.');
    }

    function picPick(id, allow) {
        pickMedia({ allow: allow || 'photo', tag: 'sports', title: 'Choose a photo', done: function (list) {
            var it = list[0];
            el(id).value = it.src;
            el(id + '-b').innerHTML = '<img src="' + esc(it.thumb) + '" alt="">';
        } });
    }

    function picClear(id) {
        el(id).value = '';
        el(id + '-b').innerHTML = '<span>Choose a photo</span>';
    }

    window.AdminSportsJsloaded = function () {
        list(1);
        if (qs('id')) {
            edit(qs('id') === 'new' ? '' : qs('id'));
        }
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', []);
    }

    function edit(id) {
        api('Form', [{ key: 'sid', vlu: id || '' }]);
    }

    function save(id) {
        api('Save', [{ key: 'sid', vlu: id || '' }, { key: 'skey', vlu: val('as-skey') }, { key: 'sname', vlu: val('as-sname') }, { key: 'sblurb', vlu: val('as-sblurb') }, { key: 'stext', vlu: el('as-stext').value },
            { key: 'sicon', vlu: val('as-sicon') }, { key: 'splace', vlu: val('as-splace') }, { key: 'simg', vlu: val('as-simg') }, { key: 'simg2', vlu: val('as-simg2') }, { key: 'sorder', vlu: val('as-sorder') },
            { key: 'spub', vlu: el('as-spub').checked ? '1' : '0' }]);
    }

    function drop(id, name) {
        ask({ title: 'Delete ' + name + '?', text: 'It disappears from the website. Sports with classes cannot be deleted; unpublish them instead.', ok: 'Delete', danger: true }, function () {
            api('Drop', [{ key: 'sid', vlu: id }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('as-', 'as-e-') : '';
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
        save: save,
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
    document.addEventListener('DOMContentLoaded', AdminSportsJs.reveal);
} else {
    AdminSportsJs.reveal();
}
