window.ceremonyGlide = function (logoId, targetId, durationMs) {
    var logo = document.getElementById(logoId);
    var target = document.getElementById(targetId);
    if (!logo || !target) return Promise.resolve();

    // Sample two frames to capture current direction of travel
    var r1 = logo.getBoundingClientRect();

    return new Promise(function (resolve) {
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                var r2 = logo.getBoundingClientRect();

                // Velocity vector (direction the logo is currently moving)
                var vx = r2.left - r1.left;
                var vy = r2.top - r1.top;
                var speed = Math.sqrt(vx * vx + vy * vy);
                // Ensure a minimum momentum so the arc is always visible
                if (speed < 2) { vx = 0; vy = -2; speed = 2; }

                // Scale velocity out to create a dramatic arc (bigger = wider curve)
                var arcFactor = 35;
                var cvx = (vx / speed) * speed * arcFactor;
                var cvy = (vy / speed) * speed * arcFactor;

                // Kill CSS orbit — from here it's all manual
                logo.getAnimations().forEach(function (a) { a.cancel(); });
                logo.classList.remove('orbit-0', 'orbit-1', 'orbit-2');

                // Pin at 0,0 with transform (same sync block = no flash)
                logo.style.position = 'fixed';
                logo.style.left = '0px';
                logo.style.top = '0px';
                logo.style.width = r2.width + 'px';
                logo.style.height = r2.height + 'px';
                logo.style.margin = '0';
                logo.style.zIndex = '40';
                logo.style.animation = 'none';

                // Target position
                var tr = target.getBoundingClientRect();
                var tx = tr.left + (tr.width - r2.width) / 2;
                var ty = tr.top + (tr.height - r2.height) / 2;

                // Cubic bezier path:
                //   P0 = current position (where logo is now)
                //   P1 = current + velocity (continues current direction — the key to "no stop")
                //   P2 = above/beside target (creates a nice approach arc)
                //   P3 = target (podium spot)
                var p0x = r2.left, p0y = r2.top;
                var p1x = r2.left + cvx, p1y = r2.top + cvy;
                var p2x = tx + (r2.left - tx) * 0.15, p2y = ty - 100;
                var p3x = tx, p3y = ty;

                // Build keyframes along the bezier curve
                var steps = 40;
                var keyframes = [];
                for (var i = 0; i <= steps; i++) {
                    var t = i / steps;
                    var mt = 1 - t;

                    // Cubic bezier interpolation
                    var x = mt*mt*mt*p0x + 3*mt*mt*t*p1x + 3*mt*t*t*p2x + t*t*t*p3x;
                    var y = mt*mt*mt*p0y + 3*mt*mt*t*p1y + 3*mt*t*t*p2y + t*t*t*p3y;

                    // Gentle scale pulse peaking at ~30% of the journey
                    var scalePulse = 1 + 0.12 * Math.sin(t * Math.PI * 0.8);
                    // Glow fades in then out
                    var glow = Math.sin(t * Math.PI);

                    keyframes.push({
                        transform: 'translate(' + x + 'px,' + y + 'px) scale(' + scalePulse.toFixed(3) + ')',
                        boxShadow: '0 0 ' + Math.round(30 + glow * 50) + 'px rgba(245,200,0,' + (0.15 + glow * 0.55).toFixed(2) + ')',
                        offset: t
                    });
                }

                // Override last keyframe to land precisely
                keyframes[steps] = {
                    transform: 'translate(' + p3x + 'px,' + p3y + 'px) scale(1)',
                    boxShadow: '0 0 30px rgba(0,0,0,.4)',
                    offset: 1
                };

                var anim = logo.animate(keyframes, {
                    duration: durationMs,
                    easing: 'ease-in-out',
                    fill: 'forwards'
                });

                anim.finished.then(function () {
                    logo.style.transform = 'translate(' + p3x + 'px,' + p3y + 'px) scale(1)';
                    logo.style.boxShadow = '0 0 30px rgba(0,0,0,.4)';
                    resolve();
                });
            });
        });
    });
};

window.ceremonyBounce = function (logoId) {
    var logo = document.getElementById(logoId);
    if (!logo) return;
    var cs = getComputedStyle(logo).transform || 'none';
    logo.animate([
        { transform: cs + ' scale(1)', offset: 0 },
        { transform: cs + ' scale(1.2)', boxShadow: '0 0 60px rgba(245,200,0,.6)', offset: 0.4 },
        { transform: cs + ' scale(1)', boxShadow: '0 0 30px rgba(0,0,0,.4)', offset: 1 }
    ], { duration: 500, easing: 'cubic-bezier(.34,1.56,.64,1)' });
};
