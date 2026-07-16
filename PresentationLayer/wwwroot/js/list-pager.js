/*
 * list-pager.js — phân trang phía client, dùng lại được.
 *
 * Cách dùng: thêm data-pager="N" vào phần tử chứa các dòng/mục cần phân trang.
 *   - Bảng:   <tbody data-pager="10"> … <tr>… </tbody>
 *   - List div: <div class="my-list" data-pager="8"> … <div>… </div>
 * Tùy chọn: data-pager-label="tài liệu" đổi chữ đơn vị trong nhãn "… N tài liệu".
 *
 * Đặc điểm:
 *  - Không cần sửa backend, chuyển trang tức thì.
 *  - Ẩn bằng class .lp-hidden (KHÔNG đụng inline style.display) nên SỐNG CHUNG được với
 *    filter/search client-side hiện có (các filter đó set inline display). Pager chỉ phân
 *    trang trên những item mà filter CHƯA ẩn.
 *  - Tự phân trang lại khi danh sách đổi (thêm dòng realtime) hoặc khi filter chạy.
 */
(function () {
    function pageNumbers(current, pageCount) {
        var nums = [];
        var add = function (n) { if (nums[nums.length - 1] !== n) nums.push(n); };
        add(1);
        for (var i = current - 1; i <= current + 1; i++)
            if (i > 1 && i < pageCount) add(i);
        if (pageCount > 1) add(pageCount);
        var out = [];
        for (var j = 0; j < nums.length; j++) {
            if (j > 0 && nums[j] - nums[j - 1] > 1) out.push('...');
            out.push(nums[j]);
        }
        return out;
    }

    function setup(container) {
        if (container.__pagerBound) return;
        container.__pagerBound = true;

        var pageSize = parseInt(container.getAttribute('data-pager'), 10) || 10;
        var unit = container.getAttribute('data-pager-label') || 'mục';

        var anchor = container.tagName === 'TBODY' ? (container.closest('table') || container) : container;
        var nav = document.createElement('nav');
        nav.className = 'list-pager';
        anchor.parentNode.insertBefore(nav, anchor.nextSibling);

        var current = 1;
        var rendering = false;

        function allItems() {
            return Array.prototype.filter.call(container.children, function (el) {
                return el.nodeType === 1;
            });
        }
        // Item "đủ điều kiện" = chưa bị filter/search ẩn (filter set inline display:none).
        function isFilterHidden(el) { return el.style.display === 'none'; }

        function makeBtn(label, opts) {
            opts = opts || {};
            var b = document.createElement('button');
            b.type = 'button';
            b.textContent = label;
            if (opts.disabled) b.disabled = true;
            if (opts.active) b.classList.add('active');
            if (opts.onClick && !opts.disabled && !opts.active) b.addEventListener('click', opts.onClick);
            return b;
        }

        function go(p, pageCount) {
            current = Math.min(Math.max(1, p), pageCount);
            render();
            anchor.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }

        function render() {
            rendering = true;
            var all = allItems();
            all.forEach(function (el) { el.classList.remove('lp-hidden'); });      // reset
            var eligible = all.filter(function (el) { return !isFilterHidden(el); });
            var total = eligible.length;
            var pageCount = Math.max(1, Math.ceil(total / pageSize));
            if (current > pageCount) current = pageCount;

            if (pageCount <= 1) {
                nav.style.display = 'none';
                rendering = false;
                return;
            }
            nav.style.display = '';

            var start = (current - 1) * pageSize;
            var end = start + pageSize;
            eligible.forEach(function (el, i) {
                if (i < start || i >= end) el.classList.add('lp-hidden');
            });

            nav.innerHTML = '';
            var info = document.createElement('span');
            info.className = 'list-pager-info';
            info.textContent = 'Trang ' + current + ' / ' + pageCount + ' · ' + total + ' ' + unit;
            nav.appendChild(info);

            nav.appendChild(makeBtn('«', { disabled: current === 1, onClick: function () { go(current - 1, pageCount); } }));
            pageNumbers(current, pageCount).forEach(function (n) {
                if (n === '...') {
                    var e = document.createElement('span');
                    e.className = 'list-pager-ellipsis';
                    e.textContent = '…';
                    nav.appendChild(e);
                } else {
                    nav.appendChild(makeBtn(String(n), { active: n === current, onClick: function () { go(n, pageCount); } }));
                }
            });
            nav.appendChild(makeBtn('»', { disabled: current === pageCount, onClick: function () { go(current + 1, pageCount); } }));
            rendering = false;
        }

        // Re-paginate khi: danh sách thêm/bớt dòng (realtime) hoặc filter đổi inline display.
        // Bỏ qua mutation do chính pager gây ra (chỉ đổi class .lp-hidden, đã lọc theo attributeFilter).
        var timer = null;
        var mo = new MutationObserver(function () {
            if (rendering) return;
            clearTimeout(timer);
            timer = setTimeout(render, 60);
        });
        mo.observe(container, { childList: true, subtree: true, attributes: true, attributeFilter: ['style'] });

        render();
    }

    function init() {
        document.querySelectorAll('[data-pager]').forEach(setup);
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', init);
    else
        init();
})();
