// Exact Numbers' web half. Runs in Root.html, the document every game page iframe sits in. The game's tooltips draw a
// stat chip per change in a preview text such as "Satiety:-12;Stamina:-20", showing only one to three + or - marks.
// This swaps each mark for the number the plugin works out for that preview. It only changes chip text, and only when
// the chip still shows exactly what the game drew for that preview, so a page a game update reshaped stays as it was.
// Talks to the plugin through vuplex.postMessage("slmod|exactnumbers|<verb>") and is driven by window.__slexactnumbers.
(function () {
  if (window.__slexactnumbers) return;

  var PREFIX = 'slmod|exactnumbers|';
  var POLL_MS = 200;
  var ASK_AGAIN_MS = 3000;
  var MAX_RECENT = 20;
  var MARK = '__slexactnumbers';
  var VANILLA_ATTR = 'data-slexact-vanilla';
  var TITLE_ATTR = 'data-slexact-title';

  // Preview text -> list of {k: chip class, v: what the game drew, s: number to show or null, b: base number or null},
  // or null for a preview whose chips stay as the game drew them.
  var labels = {};
  var askedAt = {};
  var reported = {};
  // Crafting table tooltips the page built before their numbers arrived, put right when they do.
  var recent = [];

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

  // ===== Numbers from the plugin =====

  function setLabels(data) {
    data = data || {};

    for (var preview in data) {
      if (Object.prototype.hasOwnProperty.call(data, preview)) labels[preview] = data[preview];
    }

    refreshRecent();
    patchPages();
  }

  // Undefined while the plugin has not answered yet; the first sight of a preview asks, and so does every later sight
  // once an ask has gone unanswered for a while.
  function labelsFor(preview) {
    if (Object.prototype.hasOwnProperty.call(labels, preview)) return labels[preview];

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

    if (!list || !wrap || wrap.children.length !== list.length) return;

    for (var i = 0; i < list.length; i++) {
      if (!matches(wrap.children[i], list[i])) return;
    }

    for (var j = 0; j < list.length; j++) {
      if (list[j].s != null) write(wrap.children[j], list[j]);
    }
  }

  function matches(chip, label) {
    var value = chip.lastElementChild;

    if (!value || !chip.classList.contains('attr-chip') || !chip.classList.contains(label.k)) return false;

    var drawn = chip.getAttribute(VANILLA_ATTR);

    return (drawn === null ? value.textContent : drawn) === label.v;
  }

  function write(chip, label) {
    var value = chip.lastElementChild;

    if (chip.getAttribute(VANILLA_ATTR) === null) chip.setAttribute(VANILLA_ATTR, label.v);
    if (value.textContent !== label.s) value.textContent = label.s;

    if (label.b) {
      chip.title = 'Base ' + label.b;
      chip.setAttribute(TITLE_ATTR, '');
    } else if (chip.getAttribute(TITLE_ATTR) !== null) {
      chip.removeAttribute('title');
      chip.removeAttribute(TITLE_ATTR);
    }
  }

  // ===== Object menus (SelectMenu.html) =====

  // The menu's builder is private to the page, so the button list is read from the same message the page draws it
  // from: each button's data-index is its place in btnList, and its tooltip chips come from btnList[i].attrDelta.
  function patchSelectMenu(win, doc) {
    var state = win[MARK];

    if (!state) {
      state = win[MARK] = { previews: [] };
      win.addEventListener('message', function (event) {
        try {
          takeMenu(state, event.data);
        } catch (e) {
          diag('menu message', e);
        }
      });
    }

    var container = doc.getElementById('menu-container');

    if (!container) return;

    // The game rebuilds the menu on every open; the observer puts the numbers in before the first hover.
    if (!container[MARK]) {
      container[MARK] = true;
      new win.MutationObserver(function () {
        try {
          applyMenu(state, container);
        } catch (e) {
          diag('menu redraw', e);
        }
      }).observe(container, { childList: true, subtree: true });
    }

    applyMenu(state, container);
  }

  function takeMenu(state, message) {
    if (!message || message.type !== 'WebUI_SelectMenu_MenuMsg') return;

    var data = typeof message.data === 'string' ? JSON.parse(message.data) : message.data;

    // The page ignores a message without a button list, so the old list stays here too.
    if (!data || !Array.isArray(data.btnList)) return;

    state.previews = data.btnList.map(function (button) {
      return button && typeof button.attrDelta === 'string' ? button.attrDelta : '';
    });
  }

  function applyMenu(state, container) {
    var buttons = container.querySelectorAll('.menu-btn[data-index]');

    for (var i = 0; i < buttons.length; i++) {
      var preview = state.previews[parseInt(buttons[i].getAttribute('data-index'), 10)];
      var wrap = preview ? buttons[i].querySelector('.tip-attrs') : null;

      if (wrap) rewrite(wrap, preview);
    }
  }

  // ===== Crafting table (ToolTable.html) =====

  // buildAttrChips is a page-wide function there, so the page's own calls go through this wrapper.
  function patchToolTable(win) {
    var original = win.buildAttrChips;

    if (typeof original !== 'function' || original[MARK]) return;

    var wrapped = function (preview) {
      var wrap = original.apply(this, arguments);

      try {
        if (wrap && typeof preview === 'string') {
          remember(wrap, preview);
          rewrite(wrap, preview);
        }
      } catch (e) {
        diag('crafting chips', e);
      }

      return wrap;
    };

    wrapped[MARK] = true;
    win.buildAttrChips = wrapped;
  }

  function remember(wrap, preview) {
    recent.push({ wrap: wrap, preview: preview });

    if (recent.length > MAX_RECENT) recent.shift();
  }

  function refreshRecent() {
    for (var i = 0; i < recent.length; i++) {
      try {
        rewrite(recent[i].wrap, recent[i].preview);
      } catch (e) {
        diag('crafting refresh', e);
      }
    }
  }

  // ===== Pages =====

  var PAGES = [
    { pattern: /SelectMenu\.html/, patch: patchSelectMenu },
    { pattern: /ToolTable\.html/, patch: patchToolTable }
  ];

  function patchPages() {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      var src = frames[i].src || '';

      for (var p = 0; p < PAGES.length; p++) {
        if (PAGES[p].pattern.test(src)) patchFrame(frames[i], PAGES[p], src);
      }
    }
  }

  function patchFrame(frame, page, src) {
    var win = null;
    var doc = null;

    try {
      win = frame.contentWindow;
      doc = frame.contentDocument;
    } catch (e) {}

    if (!win || !doc) return diag('page access', 'cannot reach ' + src);
    if (doc.readyState === 'loading') return;

    try {
      page.patch(win, doc);
    } catch (e) {
      diag(String(page.pattern), e);
    }
  }

  setInterval(function () {
    try {
      patchPages();
    } catch (e) {
      diag('poll', e);
    }
  }, POLL_MS);

  window.__slexactnumbers = { setLabels: setLabels };
  send('ready');
})();
