// Token animation queue — v3
// Positions are applied 400ms apart so the CSS transition on each step
// completes before the next one starts.
var _ccnQueue = [];
var _ccnRunning = false;

function _ccnProcessNext() {
    if (_ccnQueue.length === 0) { _ccnRunning = false; return; }
    _ccnRunning = true;
    var item = _ccnQueue.shift();
    var el = document.getElementById(item.id);
    if (el) {
        el.style.transition = 'transform 0.35s ease';
        el.style.transform = 'translate(' + item.x + 'px,' + item.y + 'px)';
    }
    setTimeout(_ccnProcessNext, 400);
}

window.ccnMoveToken = function (tokenId, svgX, svgY) {
    _ccnQueue.push({ id: tokenId, x: svgX, y: svgY });
    if (!_ccnRunning) _ccnProcessNext();
};

window.ccnClearQueue = function () {
    _ccnQueue = [];
    _ccnRunning = false;
};
