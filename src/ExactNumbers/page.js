// Exact Numbers' web half. Runs in Root.html, the document every game page iframe sits in. The game's tooltips draw a
// stat chip per change in a preview text such as "Satiety:-12;Stamina:-20", showing only one to three + or - marks.
// This swaps each mark for the number the plugin works out for that preview. Every decision about what a message
// means and what a chip should say is the plugin's; this script only carries messages and edits the DOM. It only
// changes chip text, and only when the chip still shows exactly what the game drew for that preview, so a page a game
// update reshaped stays as it was. Talks to the plugin through vuplex.postMessage("slmod|exactnumbers|<verb>") and is
// driven by window.__slexactnumbers.
(function () {
  if (window.__slexactnumbers) return;

  var PREFIX = 'slmod|exactnumbers|';
  var POLL_MS = 200;
  var ASK_AGAIN_MS = 3000;
  var MARK = '__slexactnumbers';
  var VANILLA_ATTR = 'data-slexact-vanilla';
  var TITLE_ATTR = 'data-slexact-title';
  // The only game messages sent on to the plugin, which reads each in full. An object menu's button list says which
  // preview each button's chips are drawn from, so menus cannot be numbered without it. The crafting table's recipe list
  // (ToolTable.html:2922) holds every product preview, so their numbers are on the page before the first hover instead
  // of the tooltip being built again when they arrive.
  var PASSED_ON = { WebUI_SelectMenu_MenuMsg: true, WebUI_ToolTable_RecipeMsg: true };
  // Root.html's field separator: "type<sep>page id<sep>message name<sep>json" (Root.html:252-262).
  var FIELD_SEPARATOR = '\u001e';
  // The object menu's own spacing for choosing a tooltip side (SelectMenu.html:428-429).
  var MENU_TOOLTIP_OFFSET = 12;
  var MENU_SCREEN_PADDING = 15;

  var PENDING = 'pending';
  var SAME = 'same';
  var CHANGED = 'changed';

  // Preview text -> list of {k: chip class, v: what the game drew, s: text to show or null, t: hover text or null},
  // or null for a preview whose chips stay as the game drew them.
  var labels = {};
  var askedAt = {};
  var reported = {};
  // Object menu page id (its iframe's id) -> each button's preview, in button order, as the plugin read it.
  var menus = {};
  // Game messages sent to the plugin, and the highest one it answered. While one is in flight, a menu may still hold
  // the previous open's previews, so menus wait for the answer.
  var forwarded = 0;
  var answered = 0;

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  // One note per kind of failure, so a page a game update reshaped says so once instead of every poll.
  function diag(kind, error) {
    if (reported[kind]) return;

    reported[kind] = true;
    send('diag|' + kind + ': ' + error);
  }

  function has(map, key) {
    return Object.prototype.hasOwnProperty.call(map, key);
  }

  // ===== Messages from the game =====

  // Root.html hears every message the game sends a page and hands it to the page's iframe (Root.html:269-299), so a
  // menu's button list is caught here even when its iframe was made a moment ago and has no listener of its own yet.
  function listenToGame() {
    if (window.vuplex) return window.vuplex.addEventListener('message', passOn);

    window.addEventListener('vuplexready', function () {
      window.vuplex.addEventListener('message', passOn);
    });
  }

  function passOn(event) {
    var message = event && event.data;

    if (typeof message !== 'string' || !has(PASSED_ON, messageName(message))) return;

    forwarded += 1;
    send('game|' + forwarded + '|' + message);
  }

  // The third field, found without splitting a message that can be very long.
  function messageName(message) {
    var first = message.indexOf(FIELD_SEPARATOR);
    var second = first < 0 ? -1 : message.indexOf(FIELD_SEPARATOR, first + 1);
    var third = second < 0 ? -1 : message.indexOf(FIELD_SEPARATOR, second + 1);

    return third < 0 ? '' : message.substring(second + 1, third);
  }

  // ===== Answers from the plugin =====

  function takeGame(data) {
    // A number above any sent belongs to a script from before a reload.
    if (!data || typeof data.seq !== 'number' || data.seq > forwarded) return;
    if (data.seq > answered) answered = data.seq;

    if (data.menu) {
      forgetClosedMenus();
      menus[data.menu.page] = data.menu.previews;
    }

    setLabels(data.labels);
  }

  function setLabels(data) {
    var updated = {};

    data = data || {};

    for (var preview in data) {
      if (has(data, preview)) {
        labels[preview] = data[preview];
        updated[preview] = true;
      }
    }

    patchPages(updated);
  }

  // Each open makes a new menu iframe and removes the old one, so a menu whose iframe is gone is never drawn again.
  function forgetClosedMenus() {
    for (var pageId in menus) {
      if (has(menus, pageId) && !document.getElementById(pageId)) delete menus[pageId];
    }
  }

  // Undefined while the plugin has not answered yet; the first sight of a preview asks, and so does every later sight
  // once an ask has gone unanswered for a while.
  function labelsFor(preview) {
    if (has(labels, preview)) return labels[preview];

    var now = Date.now();

    if (!askedAt[preview] || now - askedAt[preview] >= ASK_AGAIN_MS) {
      askedAt[preview] = now;
      send('need|' + preview);
    }

    return undefined;
  }

  // ===== Chips =====

  // wrap is the .tip-attrs element the game built from preview. Every chip is checked before any is changed.
  function rewrite(wrap, preview) {
    var list = labelsFor(preview);

    if (list === undefined) return PENDING;
    if (!list || !wrap || wrap.children.length !== list.length) return SAME;

    for (var i = 0; i < list.length; i++) {
      if (!matches(wrap.children[i], list[i])) return SAME;
    }

    var result = SAME;

    for (var j = 0; j < list.length; j++) {
      if (list[j].s != null && write(wrap.children[j], list[j])) result = CHANGED;
    }

    return result;
  }

  function matches(chip, label) {
    var value = chip.lastElementChild;

    if (!value || !chip.classList.contains('attr-chip') || !chip.classList.contains(label.k)) return false;

    var drawn = chip.getAttribute(VANILLA_ATTR);

    return (drawn === null ? value.textContent : drawn) === label.v;
  }

  // True when the chip's text changed, which can change its width.
  function write(chip, label) {
    var value = chip.lastElementChild;
    var isChanged = value.textContent !== label.s;

    if (chip.getAttribute(VANILLA_ATTR) === null) chip.setAttribute(VANILLA_ATTR, label.v);
    if (isChanged) value.textContent = label.s;

    if (label.t) {
      chip.title = label.t;
      chip.setAttribute(TITLE_ATTR, '');
    } else if (chip.getAttribute(TITLE_ATTR) !== null) {
      chip.removeAttribute('title');
      chip.removeAttribute(TITLE_ATTR);
    }

    return isChanged;
  }

  // ===== Object menus (SelectMenu.html) =====

  // Each button's data-index is its place in the message's btnList (SelectMenu.html:439-443, openMenu from 675), and its
  // tooltip chips come from btnList[i].attrDelta (line 537).
  function patchSelectMenu(win, doc, frame) {
    var container = doc.getElementById('menu-container');
    var pageId = frame.id;

    if (!container) return;

    // The page rebuilds the menu on every open; the observer puts the numbers in before the first hover.
    if (!container[MARK]) {
      container[MARK] = true;
      new win.MutationObserver(function () {
        try {
          applyMenu(win, container, pageId);
        } catch (e) {
          diag('menu redraw', e);
        }
      }).observe(container, { childList: true, subtree: true });
    }

    applyMenu(win, container, pageId);
  }

  function applyMenu(win, container, pageId) {
    var previews = menus[pageId];

    if (!previews || answered < forwarded) return;

    var buttons = container.querySelectorAll('.menu-btn[data-index]');
    var widened = [];

    for (var i = 0; i < buttons.length; i++) {
      var preview = previews[parseInt(buttons[i].getAttribute('data-index'), 10)];
      var wrap = preview ? buttons[i].querySelector('.tip-attrs') : null;
      var column = buttons[i].closest('.menu-col');

      if (wrap && rewrite(wrap, preview) === CHANGED && column && widened.indexOf(column) < 0) widened.push(column);
    }

    for (var c = 0; c < widened.length; c++) placeMenuTooltips(win, widened[c]);
  }

  // The page picks each column's tooltip side once, from the widths of the tooltips it drew (applyTooltipSide,
  // SelectMenu.html:656-673). A number is wider than its marks, so the side is picked again the same way: the main
  // column prefers the left and the second column the right (lines 765-767), switching only when that side is short.
  function placeMenuTooltips(win, column) {
    var tips = column.querySelectorAll('.menu-btn-tooltip');
    var widest = 0;

    for (var i = 0; i < tips.length; i++) {
      if (tips[i].offsetWidth > widest) widest = tips[i].offsetWidth;
    }

    if (widest === 0) return;

    var bounds = column.getBoundingClientRect();
    var need = widest + MENU_TOOLTIP_OFFSET + MENU_SCREEN_PADDING;
    var fitsRight = win.innerWidth - bounds.right >= need;
    var fitsLeft = bounds.left >= need;
    var toRight = column.classList.contains('maint');

    if (toRight && !fitsRight && fitsLeft) toRight = false;
    else if (!toRight && !fitsLeft && fitsRight) toRight = true;

    column.classList.remove('tip-left', 'tip-right');
    column.classList.add(toRight ? 'tip-right' : 'tip-left');
  }

  // ===== Crafting table (ToolTable.html) =====

  // buildAttrChips and showRecipeTooltip are page-wide functions there (ToolTable.html:3445, 3491), so the page's own
  // calls go through these wrappers. The page measures and places the tooltip right after building it (3614-3618), so
  // numbers are put in while it builds, and numbers that arrive later are put in by having the page build it again.
  function patchToolTable(win, doc, frame, updated) {
    var state = win[MARK];

    if (!state) {
      if (typeof win.buildAttrChips !== 'function' || typeof win.showRecipeTooltip !== 'function') return diag('crafting page', 'tooltip functions not found');

      state = win[MARK] = { building: null, shown: null, x: 0, y: 0 };
      wrapToolTable(win, state);
      doc.addEventListener('mousemove', function (event) {
        state.x = event.clientX;
        state.y = event.clientY;
      }, true);
    }

    if (updated) redrawTooltip(win, doc, state, updated);
  }

  function wrapToolTable(win, state) {
    var build = win.buildAttrChips;
    var show = win.showRecipeTooltip;

    win.buildAttrChips = function (preview) {
      var wrap = build.apply(this, arguments);

      try {
        if (wrap && typeof preview === 'string') {
          if (state.building) {
            state.building.previews.push(preview);
            state.building.wraps.push(wrap);
          }

          rewrite(wrap, preview);
        }
      } catch (e) {
        diag('crafting chips', e);
      }

      return wrap;
    };

    win.showRecipeTooltip = function (event) {
      var building = { rest: Array.prototype.slice.call(arguments, 1), previews: [], wraps: [] };

      if (event) {
        state.x = event.clientX;
        state.y = event.clientY;
      }

      state.building = building;

      try {
        return show.apply(this, arguments);
      } finally {
        state.building = null;
        state.shown = building;
      }
    };
  }

  // Only the tooltip on screen is built again, and only once every number in it is known, so it is built once. The
  // page reuses the tooltip element for other hovers (showMaterialTooltip, 3624), so it must still hold these chips.
  function redrawTooltip(win, doc, state, updated) {
    var shown = state.shown;
    var tooltip = doc.getElementById('recipe-tooltip');

    if (!shown || !tooltip || tooltip.style.display !== 'block') return;

    if (!shown.wraps.length || !tooltip.contains(shown.wraps[0])) {
      state.shown = null;

      return;
    }

    var isTouched = false;

    for (var i = 0; i < shown.previews.length; i++) {
      if (!has(labels, shown.previews[i])) return;
      if (updated[shown.previews[i]]) isTouched = true;
    }

    if (isTouched) win.showRecipeTooltip.apply(win, [{ clientX: state.x, clientY: state.y }].concat(shown.rest));
  }

  // ===== Pages =====

  var PAGES = [
    { pattern: /SelectMenu\.html/, patch: patchSelectMenu },
    { pattern: /ToolTable\.html/, patch: patchToolTable }
  ];

  // updated holds the previews whose numbers just arrived, or is null on a plain poll.
  function patchPages(updated) {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      var src = frames[i].src || '';

      for (var p = 0; p < PAGES.length; p++) {
        if (PAGES[p].pattern.test(src)) patchFrame(frames[i], PAGES[p], src, updated);
      }
    }
  }

  function patchFrame(frame, page, src, updated) {
    var win = null;
    var doc = null;

    try {
      win = frame.contentWindow;
      doc = frame.contentDocument;
    } catch (e) {}

    if (!win || !doc) return diag('page access', 'cannot reach ' + src);
    if (doc.readyState === 'loading') return;

    try {
      page.patch(win, doc, frame, updated);
    } catch (e) {
      diag(String(page.pattern), e);
    }
  }

  setInterval(function () {
    try {
      patchPages(null);
    } catch (e) {
      diag('poll', e);
    }
  }, POLL_MS);

  try {
    listenToGame();
  } catch (e) {
    diag('game messages', e);
  }

  window.__slexactnumbers = { setLabels: setLabels, takeGame: takeGame };
  send('ready');
})();
