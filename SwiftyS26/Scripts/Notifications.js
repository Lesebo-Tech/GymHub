(function () {
    var POLL_INTERVAL_MS = 20000;
    var pollTimer = null;

    function fetchNotifications() {
        $.getJSON('/Notification/GetRecent', function (res) {
            if (!res.success) return;
            renderBadge(res.unreadCount);
            renderList(res.notifications);
        });
    }

    function renderBadge(count) {
        var badge = document.getElementById('notif-badge');
        if (!badge) return;
        if (count > 0) {
            badge.textContent = count > 9 ? '9+' : count;
            badge.style.display = 'flex';
        } else {
            badge.style.display = 'none';
        }
    }

    function renderList(items) {
        var list = document.getElementById('notif-list');
        if (!list) return;

        if (!items || items.length === 0) {
            list.innerHTML = '<div class="notif-empty">No notifications yet</div>';
            return;
        }

        list.innerHTML = items.map(function (n) {
            return '<div class="notif-item ' + (n.IsRead ? '' : 'unread') + '" data-id="' + n.NotificationId + '" data-link="' + (n.LinkUrl || '') + '">' +
                '<div class="notif-dot ' + n.Type + '"></div>' +
                '<div>' +
                '<div class="notif-item-title">' + escapeHtml(n.Title) + '</div>' +
                '<div class="notif-item-msg">' + escapeHtml(n.Message) + '</div>' +
                '<div class="notif-item-time">' + n.timeAgo + '</div>' +
                '</div>' +
                '</div>';
        }).join('');

        list.querySelectorAll('.notif-item').forEach(function (el) {
            el.addEventListener('click', function () {
                var id = this.getAttribute('data-id');
                var link = this.getAttribute('data-link');
                markRead(id, function () {
                    if (link) window.location.href = link;
                });
            });
        });
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }

    function markRead(id, cb) {
        $.post('/Notification/MarkAsRead', { id: id }, function () {
            fetchNotifications();
            if (cb) cb();
        });
    }

    window.markAllNotifsRead = function () {
        $.post('/Notification/MarkAllAsRead', function () {
            fetchNotifications();
        });
    };

    window.toggleNotifDropdown = function () {
        var dd = document.getElementById('notif-dropdown');
        if (!dd) return;
        dd.classList.toggle('open');
    };

    document.addEventListener('click', function (e) {
        var wrapper = document.getElementById('notif-wrapper');
        if (wrapper && !wrapper.contains(e.target)) {
            var dd = document.getElementById('notif-dropdown');
            if (dd) dd.classList.remove('open');
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        fetchNotifications();
        pollTimer = setInterval(fetchNotifications, POLL_INTERVAL_MS);
    });
})();