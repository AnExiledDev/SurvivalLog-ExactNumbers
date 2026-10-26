// Exact Numbers' web half. Runs in Root.html, the document every game page iframe sits in. The game's tooltips draw a
// stat chip per change in a preview text such as "Satiety:-12;Stamina:-20", showing only one to three + or - marks.
// This swaps each mark for the number the plugin works out for that chip's label key: the preview itself, or, when the
// plugin knows which furniture function the chips belong to, "@furniture/func/preview", so a function's measured numbers
// are not put on another's chips that happen to share a preview. Every decision about what a message
// means and what a chip should say is the plugin's; this script only carries messages and edits the DOM. It only
// changes chip text, and only when the chip still shows exactly what the game drew for that preview, so a page a game
// update reshaped stays as it was. Talks to the plugin through vuplex.postMessage("slmod|exactnumbers|<verb>") and is
// driven by window.__slexactnumbers.
(function () {
  if (window.__slexactnumbers) return;

  var PREFIX = 'slmod|exactnumbers|';
  // Only finds pages that just opened; whatever is already open is watched or patched once.
  var POLL_MS = 250;
  var ASK_AGAIN_MS = 3000;
  var MARK = '__slexactnumbers';
  var VANILLA_ATTR = 'data-slexact-vanilla';
  var TITLE_ATTR = 'data-slexact-title';
  // The only game messages sent on to the plugin, which reads each in full. An object menu's button list says which
  // preview each button's chips are drawn from, so menus cannot be numbered without it. The crafting table's recipe list
  // holds every product preview, so their numbers are on the page before the first hover instead of the tooltip being
  // built again when they arrive. The game now sends the previews in its static recipe list (ToolTable.html:3113) and
  // the plain recipe list carries only keys; both are passed on so either build is read.
  var PASSED_ON = { WebUI_SelectMenu_MenuMsg: true, WebUI_ToolTable_RecipeMsg: true, WebUI_ToolTable_RecipeStaticMsg: true };
  // Root.html's field separator: "type<sep>page id<sep>message name<sep>json" (Root.html:252-262).
  var FIELD_SEPARATOR = '\u001e';
  // The object menu's own spacing for choosing a tooltip side (SelectMenu.html:428-429).
  var MENU_TOOLTIP_OFFSET = 12;
  var MENU_SCREEN_PADDING = 15;

  var PENDING = 'pending';
  var SAME = 'same';
  var CHANGED = 'changed';

  // Label key -> list of {k: chip class, v: what the game drew, s: text to show or null, t: hover text or null}, or
  // null for a key whose chips stay as the game drew them.
  var labels = {};
  var askedAt = {};
  var reported = {};
  // Object menu page id (its iframe's id) -> each button's label key, in button order, as the plugin read it.
  var menus = {};
  // Crafting recipe key -> the label key of its product chips, for recipes whose furniture function the plugin knows.
  var recipeKeys = {};
  // Game messages sent to the plugin, and the highest one it answered. While one is in flight, a menu may still hold
  // the previous open's keys, so menus wait for the answer.
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

    // Root.html hands the message to its page with postMessage, so the page draws it later; patching now lets the
    // crafting table's recipe list be drawn through the wrappers even the first time the table opens.
    try {
      patchPages(null);
    } catch (e) {
      diag('poll', e);
    }

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
      menus[data.menu.page] = data.menu.keys;
    }

    takeRecipes(data.recipes);
    setLabels(data.labels);
  }

  function takeRecipes(data) {
    data = data || {};

    for (var recipeKey in data) {
      if (has(data, recipeKey)) recipeKeys[recipeKey] = data[recipeKey];
    }
  }

  function setLabels(data) {
    var updated = {};

    data = data || {};

    for (var key in data) {
      if (has(data, key)) {
        labels[key] = data[key];
        updated[key] = true;
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

  // Undefined while the plugin has not answered yet; the first sight of a key asks, and so does every later sight once
  // an ask has gone unanswered for a while.
  function labelsFor(key) {
    if (has(labels, key)) return labels[key];

    var now = Date.now();

    if (!askedAt[key] || now - askedAt[key] >= ASK_AGAIN_MS) {
      askedAt[key] = now;
      send('need|' + key);
    }

    return undefined;
  }

  // ===== Chips =====

  // wrap is the .tip-attrs element the game built from the preview in key. Every chip is checked before any is changed.
  function rewrite(wrap, key) {
    var list = labelsFor(key);

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
  function patchSelectMenu(win, doc, frame, updated) {
    var container = menuContainer(doc);
    var pageId = frame.id;

    if (!container) return;

    // The page rebuilds the menu on every open; the observer puts the numbers in before the first hover. Once it
    // watches the menu, a plain poll has nothing to do here: a redraw wakes the observer, and every answer from the
    // plugin comes through setLabels with the keys it updated.
    if (!container[MARK]) {
      container[MARK] = watchMenu(win, container, pageId);
    } else if (!updated) {
      return;
    }

    applyMenu(win, container, pageId);
    // The pass just read the menu as it is now, so what the observer holds is done, the script's own edits included.
    container[MARK].takeRecords();
  }

  // Kept on the document and reused while it is still in it, so a poll does not search the menu page again.
  function menuContainer(doc) {
    var container = doc[MARK];

    if (container && container.isConnected) return container;

    container = doc.getElementById('menu-container');
    doc[MARK] = container;

    return container;
  }

  // One pass per frame however many changes the page makes while it draws a menu, and none for the script's own edits:
  // their records are taken and dropped after the pass.
  function watchMenu(win, container, pageId) {
    var isPending = false;
    var observer = new win.MutationObserver(function (records) {
      if (isPending || !hasNodeChanges(records)) return;

      isPending = true;
      nextFrame(win, function () {
        isPending = false;

        try {
          applyMenu(win, container, pageId);
        } catch (e) {
          diag('menu redraw', e);
        }

        observer.takeRecords();
      });
    });

    observer.observe(container, { childList: true, subtree: true });

    return observer;
  }

  function hasNodeChanges(records) {
    for (var i = 0; i < records.length; i++) {
      if (records[i].addedNodes.length || records[i].removedNodes.length) return true;
    }

    return false;
  }

  // Before the page is painted, so the numbers are in place the first time the menu shows.
  function nextFrame(win, callback) {
    if (typeof win.requestAnimationFrame === 'function') win.requestAnimationFrame(callback);
    else callback();
  }

  function applyMenu(win, container, pageId) {
    var keys = menus[pageId];

    if (!keys || answered < forwarded) return;

    var buttons = container.querySelectorAll('.menu-btn[data-index]');
    var widened = [];

    for (var i = 0; i < buttons.length; i++) {
      var key = keys[parseInt(buttons[i].getAttribute('data-index'), 10)];
      var wrap = key ? buttons[i].querySelector('.tip-attrs') : null;
      var column = buttons[i].closest('.menu-col');

      if (wrap && rewrite(wrap, key) === CHANGED && column && widened.indexOf(column) < 0) widened.push(column);
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

  // buildAttrChips, showRecipeTooltip and buildRecipeProduct are page-wide functions there (ToolTable.html:3445, 3491,
  // 3420), so the page's own calls go through these wrappers. The page measures and places the tooltip right after
  // building it (3614-3618), so numbers are put in while it builds, and numbers that arrive later are put in by having
  // the page build it again. The product chips are drawn from product.attrDelta (3540), and the product object is built
  // from its recipe (3271, 3359), which is where the recipe key is noted on it, so its chips can be asked for by the key
  // of the furniture function the plugin found for that recipe.
  function patchToolTable(win, doc, frame, updated) {
    var state = win[MARK];

    if (!state) {
      if (typeof win.buildAttrChips !== 'function' || typeof win.showRecipeTooltip !== 'function') return diag('crafting page', 'tooltip functions not found');

      if (typeof win.buildRecipeProduct !== 'function') diag('crafting products', 'buildRecipeProduct not found, products keep the estimates');

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
    var product = win.buildRecipeProduct;

    win.buildAttrChips = function (preview) {
      var wrap = build.apply(this, arguments);

      try {
        if (wrap && typeof preview === 'string') {
          var key = chipKey(state.building, preview);

          if (state.building) {
            state.building.keys.push(key);
            state.building.wraps.push(wrap);
          }

          rewrite(wrap, key);
        }
      } catch (e) {
        diag('crafting chips', e);
      }

      return wrap;
    };

    if (typeof product === 'function') {
      win.buildRecipeProduct = function (recipe) {
        var built = product.apply(this, arguments);

        try {
          if (built && typeof built === 'object' && recipe && recipe.recipeKey != null && recipe.recipeKey !== '') built[MARK] = String(recipe.recipeKey);
        } catch (e) {
          diag('crafting products', e);
        }

        return built;
      };
    }

    win.showRecipeTooltip = function (event) {
      var building = { rest: Array.prototype.slice.call(arguments, 1), product: arguments[3], keys: [], wraps: [] };

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

  // A product's chips go by the key the plugin sent for its recipe, but only while that key is for the preview being
  // drawn; anything else goes by the plain preview.
  function chipKey(building, preview) {
    var product = building ? building.product : null;
    var recipeKey = product && typeof product === 'object' ? product[MARK] : undefined;
    var key = recipeKey !== undefined && has(recipeKeys, recipeKey) ? recipeKeys[recipeKey] : null;
    var tail = '/' + preview;

    if (typeof key !== 'string' || product.attrDelta !== preview || key.length <= tail.length) return preview;

    return key.substring(key.length - tail.length) === tail ? key : preview;
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

    for (var i = 0; i < shown.keys.length; i++) {
      if (!has(labels, shown.keys[i])) return;
      if (updated[shown.keys[i]]) isTouched = true;
    }

    if (isTouched) win.showRecipeTooltip.apply(win, [{ clientX: state.x, clientY: state.y }].concat(shown.rest));
  }

  // ===== Pages =====

  var PAGES = [
    { pattern: /SelectMenu\.html/, patch: patchSelectMenu },
    { pattern: /ToolTable\.html/, patch: patchToolTable }
  ];

  // updated holds the label keys whose numbers just arrived, or is null on a plain poll.
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
