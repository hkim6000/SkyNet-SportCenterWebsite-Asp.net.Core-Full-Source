var AdminInstructorsJs = (function () {

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
        $ApiRequest('AdminInstructors/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ai-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ai-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ai-open');
        }, 3200);
    }

    function openNav() {
        el('ai-nav').classList.add('ai-open');
        el('ai-scrim').classList.add('ai-open');
    }

    function closeNav() {
        el('ai-nav').classList.remove('ai-open');
        el('ai-scrim').classList.remove('ai-open');
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
        el('ai-modal').classList.add('ai-open');
        times(el('ai-mbody'));
        var f = el('ai-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('ai-modal');
        if (m) {
            m.classList.remove('ai-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ai-per')) {
            el('ai-per').value = k;
        }
        if (k !== 'range') {
            if (el('ai-from')) {
                el('ai-from').value = '';
            }
            if (el('ai-to')) {
                el('ai-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ai-per') }, { key: 'pfrom', vlu: val('ai-from') }, { key: 'pto', vlu: val('ai-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ai-main'));
        if (window.MutationObserver && el('ai-main')) {
            new MutationObserver(function () {
                clearTimeout(otimer);
                otimer = setTimeout(function () {
                    times(el('ai-main'));
                }, 30);
            }).observe(el('ai-main'), { childList: true, subtree: true });
        }
        if (window.AdminInstructorsJsloaded) {
            window.AdminInstructorsJsloaded();
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
        if (!askBox || !askBox.classList.contains('ai-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ai-open');
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
        askBox.className = 'ai-ask';
        askBox.innerHTML = '<div class="ai-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ai-err" role="alert"></em><div class="ai-askacts"><button type="button" class="ai-btn ai-btn-ghost ai-btn-sm"></button><button type="button" class="ai-btn ai-btn-sm"></button></div></div>';
        (el('ai-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ai-open')) {
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
        x.b[1].className = 'ai-btn ai-btn-sm' + (o.danger ? ' ai-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ai-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ai-open');
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
        if (el('ai-pk')) {
            return;
        }
        var d = document.createElement('div');
        d.className = 'ai-pk';
        d.id = 'ai-pk';
        d.setAttribute('role', 'dialog');
        d.setAttribute('aria-modal', 'true');
        d.innerHTML = '<div class="ai-pkbox"><header><h2 id="ai-pkt">Choose from Media</h2><button type="button" class="ai-ibtn" aria-label="Close" onclick="AdminInstructorsJs.pkClose()">&times;</button></header><div class="ai-pkbar"><input type="search" id="ai-pkq" placeholder="Search by name or tag" maxlength="60" oninput="AdminInstructorsJs.pkSearch()"><select id="ai-pkkind" aria-label="Type" onchange="AdminInstructorsJs.pkList(1)"><option value="">Photos and videos</option><option value="photo">Photos</option><option value="video">Videos</option><option value="doc">Documents</option></select><select id="ai-pktag" aria-label="Tag" onchange="AdminInstructorsJs.pkList(1)"><option value="">Any tag</option></select><label class="ai-btn ai-btn-sm ai-btn-ghost ai-pkup" id="ai-pkupl">Upload a photo<input type="file" id="ai-pkfile" accept="image/jpeg,image/png,image/webp" onchange="AdminInstructorsJs.pkUpload(this)"></label></div><em class="ai-err ai-pkerr" id="ai-pkerr" role="alert"></em><div class="ai-pkgrid" id="ai-pkgrid"></div><div class="ai-pkmore" id="ai-pkmore"></div><footer><span id="ai-pkn"></span><button type="button" class="ai-btn ai-btn-ghost" onclick="AdminInstructorsJs.pkClose()">Cancel</button><button type="button" class="ai-btn ai-btn-accent" id="ai-pkuse" onclick="AdminInstructorsJs.pkUse()">Add</button></footer></div>';
        (el('ai-site') || document.body).appendChild(d);
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
        el('ai-pkt').textContent = opts.title || 'Choose from Media';
        el('ai-pkq').value = '';
        el('ai-pkerr').textContent = '';
        var k = el('ai-pkkind');
        k.value = pk.allow === 'photo' || pk.allow === 'video' || pk.allow === 'doc' ? pk.allow : '';
        k.disabled = !!pk.allow;
        el('ai-pkupl').hidden = pk.allow === 'video' || pk.allow === 'doc';
        el('ai-pkuse').hidden = !pk.multi;
        pkCount();
        el('ai-pk').classList.add('ai-open');
        pkList(1);
        setTimeout(function () {
            el('ai-pkq').focus();
        }, 30);
    }

    function pkList(pg) {
        pk.page = pg || 1;
        api('MediaPick', [{ key: 'pkind', vlu: val('ai-pkkind') }, { key: 'pallow', vlu: pk.allow }, { key: 'pterm', vlu: val('ai-pkq') }, { key: 'ptag', vlu: val('ai-pktag') }, { key: 'pctx', vlu: pk.tag }, { key: 'ppg', vlu: String(pk.page) }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            pkList(1);
        }, 300);
    }

    function pkShown(html, more, tags, append) {
        var g = el('ai-pkgrid');
        if (append) {
            g.insertAdjacentHTML('beforeend', html);
        } else {
            g.innerHTML = html;
            g.scrollTop = 0;
        }
        el('ai-pkmore').innerHTML = more ? '<button type="button" class="ai-btn ai-btn-sm ai-btn-ghost" onclick="AdminInstructorsJs.pkList(' + (pk.page + 1) + ')">Show more</button>' : '';
        if (tags !== null) {
            var t = el('ai-pktag');
            var cur = t.value;
            t.innerHTML = '<option value="">Any tag</option>' + tags.map(function (x) {
                return '<option value="' + esc(x) + '">' + esc(x) + '</option>';
            }).join('');
            t.value = cur;
        }
        var b = g.querySelectorAll('.ai-pki');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('ai-on', pk.chosen.some(function (c) {
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
        b.classList.toggle('ai-on', at < 0);
        pkCount();
    }

    function pkCount() {
        var n = pk.chosen.length;
        el('ai-pkn').textContent = pk.multi ? (n ? n + ' selected' : 'Tap to select one or more') : 'Tap one to use it';
        el('ai-pkuse').disabled = n === 0;
        el('ai-pkuse').textContent = n > 1 ? 'Add ' + n : 'Add';
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
        var d = el('ai-pk');
        if (d) {
            d.classList.remove('ai-open');
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
        el('ai-pkerr').textContent = '';
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type) || f.size > 30 * 1024 * 1024) {
            el('ai-pkerr').textContent = 'Choose a JPG, PNG or WebP photo under 30 MB.';
            input.value = '';
            return;
        }
        var url = URL.createObjectURL(f);
        var img = new Image();
        img.onload = function () {
            URL.revokeObjectURL(url);
            var full = pkShrink(img, 1600, 0.86);
            var thumb = pkShrink(img, 640, 0.8);
            el('ai-pkupl').classList.add('ai-busy');
            api('MediaUp', [{ key: 'full', vlu: full }, { key: 'thumb', vlu: thumb }, { key: 'pname', vlu: f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').substring(0, 80) }, { key: 'pctx', vlu: pk.tag }, { key: 'pw', vlu: String(img.naturalWidth) }, { key: 'ph', vlu: String(img.naturalHeight) }]);
        };
        img.onerror = function () {
            URL.revokeObjectURL(url);
            el('ai-pkerr').textContent = 'That image could not be read.';
        };
        img.src = url;
        input.value = '';
    }

    function pkUploaded(ok, msg) {
        el('ai-pkupl').classList.remove('ai-busy');
        if (!ok) {
            el('ai-pkerr').textContent = msg;
            return;
        }
        el('ai-pkq').value = '';
        el('ai-pkkind').value = pk.allow === 'photo' ? 'photo' : '';
        el('ai-pktag').value = '';
        pkList(1);
        toast('Photo added to Media.');
    }

    function picPick(id, allow) {
        pickMedia({ allow: allow || 'photo', tag: 'instructors', title: 'Choose a photo', done: function (list) {
            var it = list[0];
            el(id).value = it.src;
            el(id + '-b').innerHTML = '<img src="' + esc(it.thumb) + '" alt="">';
        } });
    }

    function picClear(id) {
        el(id).value = '';
        el(id + '-b').innerHTML = '<span>Choose a photo</span>';
    }

    window.AdminInstructorsJsloaded = function () {
        list(1);
        if (qs('id')) {
            edit(qs('id') === 'new' ? '' : qs('id'));
        }
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', [{ key: 'term', vlu: val('ai-q') }]);
    }

    function edit(id) {
        api('Form', [{ key: 'iid', vlu: id || '' }]);
    }

    function save(id) {
        var sp = [].map.call(document.querySelectorAll('#ai-isports input:checked'), function (x) {
            return x.value;
        }).join(',');
        api('Save', [{ key: 'iid', vlu: id || '' }, { key: 'iname', vlu: val('ai-iname') }, { key: 'ititle', vlu: val('ai-ititle') }, { key: 'ibio', vlu: val('ai-ibio') }, { key: 'imail', vlu: val('ai-imail') },
            { key: 'icerts', vlu: val('ai-icerts') }, { key: 'isince', vlu: val('ai-isince') }, { key: 'iorder', vlu: val('ai-iorder') }, { key: 'iphoto', vlu: val('ai-iphoto') }, { key: 'isports', vlu: sp },
            { key: 'ipub', vlu: el('ai-ipub').checked ? '1' : '0' }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('ai-', 'ai-e-') : '';
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
    document.addEventListener('DOMContentLoaded', AdminInstructorsJs.reveal);
} else {
    AdminInstructorsJs.reveal();
}
