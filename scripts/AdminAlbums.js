var AdminAlbumsJs = (function () {

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
        $ApiRequest('AdminAlbums/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('aa-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('aa-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('aa-open');
        }, 3200);
    }

    function openNav() {
        el('aa-nav').classList.add('aa-open');
        el('aa-scrim').classList.add('aa-open');
    }

    function closeNav() {
        el('aa-nav').classList.remove('aa-open');
        el('aa-scrim').classList.remove('aa-open');
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
        el('aa-modal').classList.add('aa-open');
        times(el('aa-mbody'));
        var f = el('aa-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('aa-modal');
        if (m) {
            m.classList.remove('aa-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('aa-per')) {
            el('aa-per').value = k;
        }
        if (k !== 'range') {
            if (el('aa-from')) {
                el('aa-from').value = '';
            }
            if (el('aa-to')) {
                el('aa-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('aa-per') }, { key: 'pfrom', vlu: val('aa-from') }, { key: 'pto', vlu: val('aa-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('aa-main'));
        if (window.MutationObserver && el('aa-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('aa-main'));
                }, 30);
            }).observe(el('aa-main'), { childList: true, subtree: true });
        }
        if (window.AdminAlbumsJsloaded) {
            window.AdminAlbumsJsloaded();
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
        if (!askBox || !askBox.classList.contains('aa-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('aa-open');
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
        askBox.className = 'aa-ask';
        askBox.innerHTML = '<div class="aa-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="aa-err" role="alert"></em><div class="aa-askacts"><button type="button" class="aa-btn aa-btn-ghost aa-btn-sm"></button><button type="button" class="aa-btn aa-btn-sm"></button></div></div>';
        (el('aa-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('aa-open')) {
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
        x.b[1].className = 'aa-btn aa-btn-sm' + (o.danger ? ' aa-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('aa-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('aa-open');
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
        if (el('aa-pk')) {
            return;
        }
        var d = document.createElement('div');
        d.className = 'aa-pk';
        d.id = 'aa-pk';
        d.setAttribute('role', 'dialog');
        d.setAttribute('aria-modal', 'true');
        d.innerHTML = '<div class="aa-pkbox"><header><h2 id="aa-pkt">Choose from Media</h2><button type="button" class="aa-ibtn" aria-label="Close" onclick="AdminAlbumsJs.pkClose()">&times;</button></header><div class="aa-pkbar"><input type="search" id="aa-pkq" placeholder="Search by name or tag" maxlength="60" oninput="AdminAlbumsJs.pkSearch()"><select id="aa-pkkind" aria-label="Type" onchange="AdminAlbumsJs.pkList(1)"><option value="">Photos and videos</option><option value="photo">Photos</option><option value="video">Videos</option><option value="doc">Documents</option></select><select id="aa-pktag" aria-label="Tag" onchange="AdminAlbumsJs.pkList(1)"><option value="">Any tag</option></select><label class="aa-btn aa-btn-sm aa-btn-ghost aa-pkup" id="aa-pkupl">Upload a photo<input type="file" id="aa-pkfile" accept="image/jpeg,image/png,image/webp" onchange="AdminAlbumsJs.pkUpload(this)"></label></div><em class="aa-err aa-pkerr" id="aa-pkerr" role="alert"></em><div class="aa-pkgrid" id="aa-pkgrid"></div><div class="aa-pkmore" id="aa-pkmore"></div><footer><span id="aa-pkn"></span><button type="button" class="aa-btn aa-btn-ghost" onclick="AdminAlbumsJs.pkClose()">Cancel</button><button type="button" class="aa-btn aa-btn-accent" id="aa-pkuse" onclick="AdminAlbumsJs.pkUse()">Add</button></footer></div>';
        (el('aa-site') || document.body).appendChild(d);
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
        el('aa-pkt').textContent = opts.title || 'Choose from Media';
        el('aa-pkq').value = '';
        el('aa-pkerr').textContent = '';
        var k = el('aa-pkkind');
        k.value = pk.allow === 'photo' || pk.allow === 'video' || pk.allow === 'doc' ? pk.allow : '';
        k.disabled = !!pk.allow;
        el('aa-pkupl').hidden = pk.allow === 'video' || pk.allow === 'doc';
        el('aa-pkuse').hidden = !pk.multi;
        pkCount();
        el('aa-pk').classList.add('aa-open');
        pkList(1);
        setTimeout(function () {
            el('aa-pkq').focus();
        }, 30);
    }

    function pkList(pg) {
        pk.page = pg || 1;
        api('MediaPick', [{ key: 'pkind', vlu: val('aa-pkkind') }, { key: 'pallow', vlu: pk.allow }, { key: 'pterm', vlu: val('aa-pkq') }, { key: 'ptag', vlu: val('aa-pktag') }, { key: 'pctx', vlu: pk.tag }, { key: 'ppg', vlu: String(pk.page) }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            pkList(1);
        }, 300);
    }

    function pkShown(html, more, tags, append) {
        var g = el('aa-pkgrid');
        if (append) {
            g.insertAdjacentHTML('beforeend', html);
        } else {
            g.innerHTML = html;
            g.scrollTop = 0;
        }
        el('aa-pkmore').innerHTML = more ? '<button type="button" class="aa-btn aa-btn-sm aa-btn-ghost" onclick="AdminAlbumsJs.pkList(' + (pk.page + 1) + ')">Show more</button>' : '';
        if (tags !== null) {
            var t = el('aa-pktag');
            var cur = t.value;
            t.innerHTML = '<option value="">Any tag</option>' + tags.map(function (x) {
                return '<option value="' + esc(x) + '">' + esc(x) + '</option>';
            }).join('');
            t.value = cur;
        }
        var b = g.querySelectorAll('.aa-pki');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('aa-on', pk.chosen.some(function (c) {
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
        b.classList.toggle('aa-on', at < 0);
        pkCount();
    }

    function pkCount() {
        var n = pk.chosen.length;
        el('aa-pkn').textContent = pk.multi ? (n ? n + ' selected' : 'Tap to select one or more') : 'Tap one to use it';
        el('aa-pkuse').disabled = n === 0;
        el('aa-pkuse').textContent = n > 1 ? 'Add ' + n : 'Add';
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
        var d = el('aa-pk');
        if (d) {
            d.classList.remove('aa-open');
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
        el('aa-pkerr').textContent = '';
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type) || f.size > 30 * 1024 * 1024) {
            el('aa-pkerr').textContent = 'Choose a JPG, PNG or WebP photo under 30 MB.';
            input.value = '';
            return;
        }
        var url = URL.createObjectURL(f);
        var img = new Image();
        img.onload = function () {
            URL.revokeObjectURL(url);
            var full = pkShrink(img, 1600, 0.86);
            var thumb = pkShrink(img, 640, 0.8);
            el('aa-pkupl').classList.add('aa-busy');
            api('MediaUp', [{ key: 'full', vlu: full }, { key: 'thumb', vlu: thumb }, { key: 'pname', vlu: f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').substring(0, 80) }, { key: 'pctx', vlu: pk.tag }, { key: 'pw', vlu: String(img.naturalWidth) }, { key: 'ph', vlu: String(img.naturalHeight) }]);
        };
        img.onerror = function () {
            URL.revokeObjectURL(url);
            el('aa-pkerr').textContent = 'That image could not be read.';
        };
        img.src = url;
        input.value = '';
    }

    function pkUploaded(ok, msg) {
        el('aa-pkupl').classList.remove('aa-busy');
        if (!ok) {
            el('aa-pkerr').textContent = msg;
            return;
        }
        el('aa-pkq').value = '';
        el('aa-pkkind').value = pk.allow === 'photo' ? 'photo' : '';
        el('aa-pktag').value = '';
        pkList(1);
        toast('Photo added to Media.');
    }

    var items = [];
    var cover = '';
    var canEdit = false;

    window.AdminAlbumsJsloaded = function () {
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
        api('Form', [{ key: 'alid', vlu: id || '' }]);
    }

    function setup(list, cov, can) {
        items = list;
        cover = cov;
        canEdit = can;
        draw();
    }

    function draw() {
        var g = el('aa-aitems');
        if (!g) {
            return;
        }
        if (!items.length) {
            g.innerHTML = '<div class="aa-aempty" style="grid-column:1/-1">No photos yet.' + (canEdit ? ' Use Add photos to pick from Media.' : '') + '</div>';
        } else {
            if (items.every(function (x) { return x.src !== cover; })) {
                cover = '';
            }
            var cov = cover || items[0].src;
            g.innerHTML = items.map(function (x, i) {
                return '<div class="aa-ait' + (x.src === cov ? ' aa-cov' : '') + '"><img src="' + esc(x.thumb) + '" alt="' + esc(x.title) + '" loading="lazy">' + (x.src === cov ? '<i>Cover</i>' : '') + (canEdit ? '<div class="aa-aact"><button type="button" aria-label="Move left" onclick="AdminAlbumsJs.move(' + i + ',-1)"' + (i === 0 ? ' disabled' : '') + '>&larr;</button><button type="button" onclick="AdminAlbumsJs.setCover(' + i + ')"' + (x.src === cov ? ' disabled' : '') + '>Cover</button><button type="button" aria-label="Remove" onclick="AdminAlbumsJs.take(' + i + ')">&times;</button><button type="button" aria-label="Move right" onclick="AdminAlbumsJs.move(' + i + ',1)"' + (i === items.length - 1 ? ' disabled' : '') + '>&rarr;</button></div>' : '') + '</div>';
            }).join('');
        }
        if (el('aa-acount')) {
            el('aa-acount').textContent = items.length === 1 ? '1 photo' : items.length + ' photos';
        }
    }

    function addPhotos() {
        pickMedia({ allow: 'photo', multi: true, tag: 'albums', title: 'Add photos to the album', done: function (list) {
            list.forEach(function (it) {
                if (!items.some(function (x) { return x.src === it.src; })) {
                    items.push({ src: it.src, thumb: it.thumb, title: it.title || '' });
                }
            });
            draw();
        } });
    }

    function move(i, d) {
        var j = i + d;
        if (j < 0 || j >= items.length) {
            return;
        }
        var t = items[i];
        items[i] = items[j];
        items[j] = t;
        draw();
    }

    function setCover(i) {
        cover = items[i].src;
        draw();
    }

    function take(i) {
        items.splice(i, 1);
        draw();
    }

    function save(id) {
        api('Save', [{ key: 'alid', vlu: id || '' }, { key: 'altitle', vlu: val('aa-altitle') }, { key: 'altext', vlu: val('aa-altext') }, { key: 'aldate', vlu: val('aa-aldate') }, { key: 'alpub', vlu: el('aa-alpub').checked ? '1' : '0' }, { key: 'alcover', vlu: cover }, { key: 'alitems', vlu: JSON.stringify(items.map(function (x) { return x.src; })) }]);
    }

    function drop(id, name) {
        ask({ title: 'Delete the album ' + name + '?', text: 'The album leaves the website. The photos stay in Media.', ok: 'Delete', danger: true }, function () {
            api('Drop', [{ key: 'alid', vlu: id }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('aa-', 'aa-e-') : '';
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
        setup: setup,
        addPhotos: addPhotos,
        move: move,
        setCover: setCover,
        take: take,
        save: save,
        drop: drop,
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
    document.addEventListener('DOMContentLoaded', AdminAlbumsJs.reveal);
} else {
    AdminAlbumsJs.reveal();
}
