mergeInto(LibraryManager.library, {
    BulletRushIsMobileBrowser: function () {
        // A real touch also covers mobile browsers requesting a desktop UA.
        if (Module['bulletRushTouches'] && Module['bulletRushTouches'].sawTouch) return 1;
        if (typeof navigator === 'undefined') return 0;
        if (navigator.userAgentData && navigator.userAgentData.mobile) return 1;
        if (/Android|iPhone|iPad|iPod|Windows Phone|Mobile|Tablet/i.test(navigator.userAgent || '')) return 1;
        if (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) return 1;
        // Prefer the host's class over the mere presence of a touch screen.
        if (typeof ysdk !== 'undefined' && ysdk && ysdk.deviceInfo) {
            var type = ysdk.deviceInfo.type;
            if (type === 'mobile' || type === 'tablet') return 1;
            if (type === 'desktop' || type === 'tv') return 0;
        }
        return navigator.maxTouchPoints > 0 && typeof matchMedia === 'function' &&
            matchMedia('(pointer: coarse)').matches ? 1 : 0;
    },

    BulletRushInstallTouchInput: function () {
        if (Module['bulletRushTouches']) return;
        var canvas = Module['canvas'] || document.getElementById('unity-canvas');
        if (!canvas) return;
        var state = { active: {}, pending: {}, frame: [], sawTouch: false, nextId: 0 };
        Module['bulletRushTouches'] = state;
        canvas.style.touchAction = 'none';

        function updatePoint(point, touch) {
            var bounds = canvas.getBoundingClientRect();
            point.x = (touch.clientX - bounds.left) / Math.max(1, bounds.width);
            point.y = 1 - (touch.clientY - bounds.top) / Math.max(1, bounds.height);
        }
        function begin(event) {
            state.sawTouch = true;
            for (var i = 0; i < event.changedTouches.length; ++i) {
                var touch = event.changedTouches[i];
                var point = { id: ++state.nextId, x: 0, y: 0, pressed: 1 };
                updatePoint(point, touch);
                point.startX = point.x;
                point.startY = point.y;
                state.active[touch.identifier] = point;
                state.pending[point.id] = point;
            }
        }
        function move(event) {
            for (var i = 0; i < event.changedTouches.length; ++i) {
                var touch = event.changedTouches[i];
                var point = state.active[touch.identifier];
                if (point) updatePoint(point, touch);
            }
        }
        function end(event) {
            for (var i = 0; i < event.changedTouches.length; ++i) {
                var touch = event.changedTouches[i];
                var point = state.active[touch.identifier];
                if (!point) continue;
                point.pressed = 0;
                // Retain very short taps until the next Unity frame. Cancelled
                // touches must never trigger a gameplay action.
                if (event.type === 'touchcancel') delete state.pending[point.id];
                delete state.active[touch.identifier];
            }
        }
        if (typeof window.TouchEvent === 'undefined' && typeof window.PointerEvent !== 'undefined') {
            // Pointer-only browsers (e.g. some touch-enabled Windows browsers).
            function fromPointer(callback, type) {
                return function (event) {
                    if (event.pointerType !== 'touch') return;
                    callback({ type: type, changedTouches: [{ identifier: event.pointerId,
                        clientX: event.clientX, clientY: event.clientY }] });
                };
            }
            canvas.addEventListener('pointerdown', fromPointer(begin, 'touchstart'), { passive: true, capture: true });
            window.addEventListener('pointermove', fromPointer(move, 'touchmove'), { passive: true, capture: true });
            window.addEventListener('pointerup', fromPointer(end, 'touchend'), { passive: true, capture: true });
            window.addEventListener('pointercancel', fromPointer(end, 'touchcancel'), { passive: true, capture: true });
        } else {
            canvas.addEventListener('touchstart', begin, { passive: true, capture: true });
            window.addEventListener('touchmove', move, { passive: true, capture: true });
            window.addEventListener('touchend', end, { passive: true, capture: true });
            window.addEventListener('touchcancel', end, { passive: true, capture: true });
        }
        function clear() { state.active = {}; state.pending = {}; state.frame = []; }
        window.addEventListener('blur', clear);
        document.addEventListener('visibilitychange', function () { if (document.hidden) clear(); });
    },

    BulletRushReadTouchCount: function () {
        var state = Module['bulletRushTouches'];
        if (!state) return -1;
        state.frame = [];
        for (var id in state.active) state.frame.push(state.active[id]);
        for (var id in state.pending) if (!state.pending[id].pressed) state.frame.push(state.pending[id]);
        state.pending = {};
        return state.frame.length;
    },
    BulletRushTouchId: function (index) { return Module['bulletRushTouches'].frame[index].id; },
    BulletRushTouchX: function (index) { return Module['bulletRushTouches'].frame[index].x; },
    BulletRushTouchY: function (index) { return Module['bulletRushTouches'].frame[index].y; },
    BulletRushTouchStartX: function (index) { return Module['bulletRushTouches'].frame[index].startX; },
    BulletRushTouchStartY: function (index) { return Module['bulletRushTouches'].frame[index].startY; },
    BulletRushTouchPressed: function (index) { return Module['bulletRushTouches'].frame[index].pressed; }
});
