mergeInto(LibraryManager.library, {
    ProjectB_RegisterBrowserHooks: function(gameObjectNamePtr) {
        if (window.__projectbHooksInstalled) return;
        window.__projectbHooksInstalled = true;

        var goName = UTF8ToString(gameObjectNamePtr);

        window.addEventListener("keydown", function(e) {
            var active = document.activeElement;
            if (active && (active.tagName === "INPUT" || active.tagName === "TEXTAREA")) return;
            if (e.code === "Space" || e.code === "ArrowUp" || e.code === "ArrowDown" || e.code === "ArrowLeft" || e.code === "ArrowRight") {
                e.preventDefault();
            }
        }, { passive: false });

        function notifyHidden(isHidden) {
            try {
                if (typeof SendMessage === "function") {
                    SendMessage(goName, "OnBrowserVisibilityChanged", isHidden ? "1" : "0");
                }
            } catch (err) {}
        }

        document.addEventListener("visibilitychange", function() {
            notifyHidden(document.hidden);
        });

        window.addEventListener("blur", function() {
            notifyHidden(true);
        });

        window.addEventListener("pagehide", function() {
            notifyHidden(true);
        });

        function onFirstGesture() {
            window.removeEventListener("pointerdown", onFirstGesture, true);
            window.removeEventListener("keydown", onFirstGesture, true);
            window.removeEventListener("touchstart", onFirstGesture, true);
            try {
                if (typeof SendMessage === "function") {
                    SendMessage(goName, "OnBrowserUserGesture", "");
                }
            } catch (err) {}
        }

        window.addEventListener("pointerdown", onFirstGesture, { passive: true, capture: true });
        window.addEventListener("keydown", onFirstGesture, { passive: true, capture: true });
        window.addEventListener("touchstart", onFirstGesture, { passive: true, capture: true });
    },

    ProjectB_FlushStorage: function() {
        try {
            if (typeof FS !== "undefined" && FS.syncfs) {
                FS.syncfs(false, function(err) {});
            }
        } catch (err) {}
    }
});
